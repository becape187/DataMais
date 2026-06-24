using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;

namespace DataMais.Services;

/// <summary>
/// Serviço de background que monitora o sinal REGISTRO_RODANDO do CLP.
/// Quando há um ensaio em execução e o REGISTRO_RODANDO cai (rodando -> parado),
/// significa que o CLP CONCLUIU o ensaio: o serviço finaliza o ensaio (Concluido),
/// gera o relatório automaticamente e reseta o coil INICIA_REGISTRO.
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

        // Só há o que monitorar se existir um ensaio em execução
        var ensaio = await context.Ensaios
            .Where(e => e.Status == "EmExecucao")
            .OrderByDescending(e => e.DataInicio)
            .FirstOrDefaultAsync(ct);

        if (ensaio == null)
        {
            _registroAnterior = null; // reseta o rastreamento quando não há ensaio ativo
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

        // Primeira leitura com ensaio ativo: só registra o estado, não conclui
        if (_registroAnterior == null)
        {
            _registroAnterior = rodando;
            return;
        }

        // Borda de descida (rodando -> parado) = CLP concluiu o ensaio
        if (_registroAnterior == true && rodando == false)
        {
            _logger.LogInformation("REGISTRO_RODANDO caiu: concluindo ensaio {EnsaioId} e gerando relatório automaticamente.", ensaio.Id);
            await ConcluirEnsaioAsync(context, ensaio, ct);
        }

        _registroAnterior = rodando;
    }

    private async Task ConcluirEnsaioAsync(DataMaisDbContext context, Ensaio ensaio, CancellationToken ct)
    {
        // Best-effort: desliga o coil INICIA_REGISTRO para deixar a unidade pronta para o próximo ciclo
        try { await SetIniciaRegistroAsync(context, false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Falha ao desligar INICIA_REGISTRO ao concluir ensaio {EnsaioId}", ensaio.Id); }

        ensaio.Status = "Concluido";
        ensaio.DataFim = DateTime.UtcNow;
        ensaio.DataAtualizacao = DateTime.UtcNow;

        var relatorio = new Relatorio
        {
            Numero = $"REL-{ensaio.Numero}",
            Data = ensaio.DataFim ?? DateTime.UtcNow,
            Observacoes = $"Relatório gerado automaticamente: CLP concluiu o ensaio {ensaio.Numero}.",
            ClienteId = ensaio.ClienteId,
            CilindroId = ensaio.CilindroId,
            EnsaioId = ensaio.Id,
            DataCriacao = DateTime.UtcNow,
            DataAtualizacao = DateTime.UtcNow
        };

        context.Relatorios.Add(relatorio);
        await context.SaveChangesAsync(ct);

        _registroAnterior = false;
        _logger.LogInformation("Ensaio {EnsaioId} concluído e relatório {Relatorio} gerado pelo monitor.", ensaio.Id, relatorio.Numero);
    }

    private async Task SetIniciaRegistroAsync(DataMaisDbContext context, bool valor)
    {
        var iniciaRegistro = await context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == "INICIA_REGISTRO" && m.Ativo);

        if (iniciaRegistro == null) return;

        string funcaoEscrita = iniciaRegistro.TipoDado == "Boolean" || iniciaRegistro.FuncaoModbus == "ReadCoils"
            ? "WriteSingleCoil"
            : "WriteSingleRegister";

        var configTemp = new ModbusConfig
        {
            Id = iniciaRegistro.Id,
            Nome = iniciaRegistro.Nome,
            IpAddress = iniciaRegistro.IpAddress,
            Port = iniciaRegistro.Port,
            SlaveId = iniciaRegistro.SlaveId,
            FuncaoModbus = funcaoEscrita,
            EnderecoRegistro = iniciaRegistro.EnderecoRegistro,
            QuantidadeRegistros = iniciaRegistro.QuantidadeRegistros,
            TipoDado = iniciaRegistro.TipoDado,
            Ativo = iniciaRegistro.Ativo
        };

        object v = funcaoEscrita == "WriteSingleCoil" ? (object)valor : (object)(ushort)(valor ? 1 : 0);
        await _modbusService.EscreverRegistroAsync(configTemp, v);
    }
}
