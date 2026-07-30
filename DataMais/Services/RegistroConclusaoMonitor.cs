using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;

namespace DataMais.Services;

/// <summary>
/// Serviço de background que monitora o sinal REGISTRO_RODANDO do CLP.
/// Quando há uma ETAPA em execução e o REGISTRO_RODANDO cai (rodando -> parado),
/// significa que o CLP concluiu aquela câmara: o serviço fecha a etapa como Concluida
/// e reseta os coils. Não gera relatório — o laudo só nasce quando o operador ACEITA
/// o ensaio com as duas câmaras prontas.
/// Funciona mesmo sem ninguém na tela (backend é a fonte da verdade).
/// </summary>
public class RegistroConclusaoMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModbusService _modbusService;
    private readonly ILogger<RegistroConclusaoMonitor> _logger;

    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);

    // Estado anterior do REGISTRO_RODANDO (para detectar a borda de descida).
    private bool? _registroAnterior;

    public RegistroConclusaoMonitor(
        IServiceScopeFactory scopeFactory,
        ModbusService modbusService,
        ILogger<RegistroConclusaoMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _modbusService = modbusService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguarda a aplicação subir antes de começar a monitorar
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        _logger.LogInformation("Monitor de conclusão de registro iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await VerificarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no monitor de conclusão de registro");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task VerificarAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataMaisDbContext>();

        // Só há o que monitorar se existir uma etapa rodando agora
        var etapa = await context.EnsaioEtapas
            .Include(e => e.Ensaio)
                .ThenInclude(en => en.Etapas)
            .Where(e => e.Status == StatusEtapa.EmExecucao)
            .OrderByDescending(e => e.DataInicio)
            .FirstOrDefaultAsync(ct);

        if (etapa == null)
        {
            _registroAnterior = null; // reseta o rastreamento quando não há etapa ativa
            return;
        }

        var registroConfig = await context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == "REGISTRO_RODANDO" && m.Ativo && m.FuncaoModbus == "ReadInputs", ct);

        if (registroConfig == null)
        {
            _logger.LogWarning("REGISTRO_RODANDO (ReadInputs) não encontrado; monitor não consegue avaliar conclusão.");
            return;
        }

        bool rodando;
        try
        {
            var leitura = await _modbusService.LerRegistroAsync(registroConfig.Id);
            rodando = leitura is bool b ? b : (leitura?.ToString() == "1" || leitura?.ToString() == "True");
        }
        catch (Exception ex)
        {
            // CLP indisponível: não conclui por engano, apenas tenta de novo no próximo ciclo
            _logger.LogWarning(ex, "Falha ao ler REGISTRO_RODANDO no monitor");
            return;
        }

        // Primeira leitura com etapa ativa: só registra o estado, não conclui
        if (_registroAnterior == null)
        {
            _registroAnterior = rodando;
            return;
        }

        // Borda de descida (rodando -> parado) = CLP concluiu esta câmara
        if (_registroAnterior == true && rodando == false)
        {
            _logger.LogInformation("REGISTRO_RODANDO caiu: concluindo a câmara {Camara} do ensaio {EnsaioId}.",
                etapa.Camara, etapa.EnsaioId);
            await ConcluirEtapaAsync(context, etapa, ct);
        }

        _registroAnterior = rodando;
    }

    /// <summary>
    /// Fecha a etapa como Concluida e, se as duas câmaras já estiverem prontas,
    /// move o ensaio para AguardandoAceite. O relatório NÃO é gerado aqui: quem
    /// decide se o ensaio vira laudo é o operador, na tela.
    /// </summary>
    private async Task ConcluirEtapaAsync(DataMaisDbContext context, EnsaioEtapa etapa, CancellationToken ct)
    {
        // Best-effort: desliga o coil INICIA_REGISTRO para deixar a unidade pronta para a próxima câmara
        try { await SetCoilPorNomeAsync(context, "INICIA_REGISTRO", false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Falha ao desligar INICIA_REGISTRO ao concluir etapa {EtapaId}", etapa.Id); }

        // Best-effort: desliga os dois botões de câmara da IHM (avança e recua),
        // independente da câmara testada, para não deixar coil retido no CLP.
        foreach (var nomeBotao in new[] { "BOTAO_AVANCA_IHM", "BOTAO_RECUA_IHM" })
        {
            try { await SetCoilPorNomeAsync(context, nomeBotao, false); }
            catch (Exception ex) { _logger.LogWarning(ex, "Falha ao desligar {Botao} ao concluir etapa {EtapaId}", nomeBotao, etapa.Id); }
        }

        var agora = DateTime.UtcNow;
        var ensaio = etapa.Ensaio;

        etapa.Status = StatusEtapa.Concluida;
        etapa.DataFim = agora;
        etapa.DataAtualizacao = agora;

        // Esta tentativa substitui as anteriores da mesma câmara
        foreach (var anterior in ensaio.Etapas.Where(e =>
                     e.Id != etapa.Id &&
                     e.Camara == etapa.Camara &&
                     e.Status == StatusEtapa.Concluida))
        {
            anterior.Status = StatusEtapa.Repetida;
            anterior.DataAtualizacao = agora;
        }

        var completo = new[] { "A", "B" }
            .All(c => ensaio.Etapas.Any(e => e.Camara == c && e.Status == StatusEtapa.Concluida));

        if (ensaio.Status != StatusEnsaio.Aceito && ensaio.Status != StatusEnsaio.Cancelado)
        {
            ensaio.Status = completo ? StatusEnsaio.AguardandoAceite : StatusEnsaio.EmAndamento;
            ensaio.DataAtualizacao = agora;
        }

        await context.SaveChangesAsync(ct);

        _registroAnterior = false;
        _logger.LogInformation(
            "Câmara {Camara} do ensaio {EnsaioId} concluída pelo monitor. Ensaio agora está {Status}.",
            etapa.Camara, ensaio.Id, ensaio.Status);
    }

    private async Task SetCoilPorNomeAsync(DataMaisDbContext context, string nome, bool valor)
    {
        var registro = await context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == nome && m.Ativo);

        if (registro == null)
        {
            _logger.LogWarning("Registro '{Nome}' não encontrado ao tentar definir {Valor}", nome, valor);
            return;
        }

        string funcaoEscrita = registro.TipoDado == "Boolean" || registro.FuncaoModbus == "ReadCoils"
            ? "WriteSingleCoil"
            : "WriteSingleRegister";

        var configTemp = new ModbusConfig
        {
            Id = registro.Id,
            Nome = registro.Nome,
            IpAddress = registro.IpAddress,
            Port = registro.Port,
            SlaveId = registro.SlaveId,
            FuncaoModbus = funcaoEscrita,
            EnderecoRegistro = registro.EnderecoRegistro,
            QuantidadeRegistros = registro.QuantidadeRegistros,
            TipoDado = registro.TipoDado,
            Ativo = registro.Ativo
        };

        object v = funcaoEscrita == "WriteSingleCoil" ? (object)valor : (object)(ushort)(valor ? 1 : 0);
        await _modbusService.EscreverRegistroAsync(configTemp, v);
    }
}
