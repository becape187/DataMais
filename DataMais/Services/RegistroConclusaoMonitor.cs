using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;

namespace DataMais.Services;

/// <summary>
/// Serviço de background que monitora os sinais do CLP durante uma etapa:
/// <list type="bullet">
/// <item>REGISTRO_RODANDO — o ciclo está rodando (inclui a rampa).</item>
/// <item>INICIA_CONTAGEM — o patamar de teste começou; a borda de SUBIDA é o t0 do laudo
/// e a de descida marca o fim da contagem.</item>
/// </list>
/// A etapa é fechada como Concluida quando os DOIS caem. Não gera relatório — o laudo
/// só nasce quando o operador ACEITA o ensaio com as duas câmaras prontas.
/// Funciona mesmo sem ninguém na tela (backend é a fonte da verdade).
/// </summary>
public class RegistroConclusaoMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModbusService _modbusService;
    private readonly ILogger<RegistroConclusaoMonitor> _logger;

    // 1 s: o t0 do laudo sai daqui, então a resolução do polling é o erro do t0.
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(1);

    // Estado anterior dos sinais (para detectar as bordas). Null = ainda não observado.
    private bool? _registroAnterior;
    private bool? _contagemAnterior;

    // Ciclos com o REGISTRO_RODANDO já parado mas o INICIA_CONTAGEM ainda ligado.
    // Se o CLP deixar esse coil retido, a etapa nunca fecha sozinha — vira log.
    private int _ciclosParadoComContagemLigada;

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
            // Reseta o rastreamento quando não há etapa ativa
            _registroAnterior = null;
            _contagemAnterior = null;
            return;
        }

        var registroConfig = await context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == "REGISTRO_RODANDO" && m.Ativo && m.FuncaoModbus == "ReadInputs", ct);

        if (registroConfig == null)
        {
            _logger.LogWarning("REGISTRO_RODANDO (ReadInputs) não encontrado; monitor não consegue avaliar conclusão.");
            return;
        }

        // INICIA_CONTAGEM é opcional: se não estiver cadastrado, o monitor se comporta
        // como antes (só a borda de descida do REGISTRO_RODANDO conclui a etapa).
        var contagemConfig = await context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == "INICIA_CONTAGEM" && m.Ativo, ct);

        bool rodando;
        bool? contando = null;

        try
        {
            rodando = await LerSinalAsync(registroConfig.Id);

            if (contagemConfig != null)
            {
                contando = await LerSinalAsync(contagemConfig.Id);
            }
        }
        catch (Exception ex)
        {
            // CLP indisponível: não conclui por engano, apenas tenta de novo no próximo ciclo
            _logger.LogWarning(ex, "Falha ao ler sinais do CLP no monitor");
            return;
        }

        // Bordas do INICIA_CONTAGEM carimbam a janela de contagem, mesmo na primeira
        // leitura: se o sinal já está ligado e a etapa ainda não tem t0, é agora.
        if (contando.HasValue)
        {
            await RegistrarBordasDaContagemAsync(context, etapa, contando.Value, ct);
        }

        // Primeira leitura com etapa ativa: só registra o estado, não conclui
        if (_registroAnterior == null)
        {
            _registroAnterior = rodando;
            _contagemAnterior = contando;
            return;
        }

        // Fim do ciclo: os DOIS sinais desligados, tendo pelo menos um estado ligado
        // no ciclo anterior. Enquanto a contagem estiver ligada, o ensaio continua —
        // mesmo que o REGISTRO_RODANDO caia antes.
        var estavaAtivo = _registroAnterior == true || _contagemAnterior == true;
        var tudoParado = !rodando && contando != true;

        // Diagnóstico: CLP parado mas contagem retida — a etapa fica presa esperando.
        if (!rodando && contando == true)
        {
            _ciclosParadoComContagemLigada++;

            if (_ciclosParadoComContagemLigada % 30 == 0)
            {
                _logger.LogWarning(
                    "REGISTRO_RODANDO parado há {Segundos}s mas INICIA_CONTAGEM continua ligado; a câmara {Camara} do ensaio {EnsaioId} não será concluída enquanto isso. Verifique o coil no CLP ou encerre pela tela.",
                    _ciclosParadoComContagemLigada * Intervalo.TotalSeconds, etapa.Camara, etapa.EnsaioId);
            }
        }
        else
        {
            _ciclosParadoComContagemLigada = 0;
        }

        if (estavaAtivo && tudoParado)
        {
            _logger.LogInformation(
                "Sinais do CLP caíram (rodando={Rodando}, contando={Contando}): concluindo a câmara {Camara} do ensaio {EnsaioId}.",
                rodando, contando, etapa.Camara, etapa.EnsaioId);

            await ConcluirEtapaAsync(context, etapa, ct);
        }

        _registroAnterior = rodando;
        _contagemAnterior = contando;
    }

    private async Task<bool> LerSinalAsync(int registroId)
    {
        var leitura = await _modbusService.LerRegistroAsync(registroId);
        return leitura is bool b ? b : (leitura?.ToString() == "1" || leitura?.ToString() == "True");
    }

    /// <summary>
    /// Carimba na etapa o começo e o fim do patamar de teste conforme o INICIA_CONTAGEM.
    /// O t0 só é gravado uma vez por etapa; a queda fecha a janela.
    /// </summary>
    private async Task RegistrarBordasDaContagemAsync(
        DataMaisDbContext context, EnsaioEtapa etapa, bool contando, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;

        if (contando && etapa.DataInicioContagem == null)
        {
            etapa.DataInicioContagem = agora;
            etapa.DataFimContagem = null;
            etapa.DataAtualizacao = agora;
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("INICIA_CONTAGEM subiu: t0 da câmara {Camara} do ensaio {EnsaioId} em {T0:o}",
                etapa.Camara, etapa.EnsaioId, agora);
            return;
        }

        if (!contando && etapa.DataInicioContagem != null && etapa.DataFimContagem == null)
        {
            etapa.DataFimContagem = agora;
            etapa.DataAtualizacao = agora;
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("INICIA_CONTAGEM caiu: contagem da câmara {Camara} do ensaio {EnsaioId} durou {Segundos:0}s",
                etapa.Camara, etapa.EnsaioId, (agora - etapa.DataInicioContagem.Value).TotalSeconds);
        }
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

        // Rede de segurança: se a contagem começou e o CLP parou sem que a borda de
        // descida fosse observada, fecha a janela aqui para o laudo não ficar em aberto.
        if (etapa.DataInicioContagem != null && etapa.DataFimContagem == null)
        {
            etapa.DataFimContagem = agora;
        }

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
        _contagemAnterior = false;
        _ciclosParadoComContagemLigada = 0;
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
