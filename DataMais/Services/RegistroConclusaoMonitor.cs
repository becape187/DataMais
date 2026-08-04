using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;

namespace DataMais.Services;

/// <summary>
/// Serviço de background que monitora os sinais do CLP durante uma etapa:
/// <list type="bullet">
/// <item>REGISTRO_RODANDO — inicia e para o registro. É o ÚNICO que fecha a etapa:
/// na borda de descida, o CLP terminou aquela câmara.</item>
/// <item>INICIA_CONTAGEM — manda só na contagem de tempo, por NÍVEL: ligou = t0 do
/// laudo, desligou = fim do patamar. Não interfere no encerramento.</item>
/// </list>
/// A etapa é fechada como Concluida na descida do REGISTRO_RODANDO. Não gera relatório — o laudo
/// só nasce quando o operador ACEITA o ensaio com as duas câmaras prontas.
/// Funciona mesmo sem ninguém na tela (backend é a fonte da verdade).
/// </summary>
public class RegistroConclusaoMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModbusService _modbusService;
    private readonly EstadoSinaisClp _estadoSinais;
    private readonly ILogger<RegistroConclusaoMonitor> _logger;

    // 1 s: o t0 do laudo sai daqui, então a resolução do polling é o erro do t0.
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(1);

    // Estado anterior do REGISTRO_RODANDO (borda de conclusão). Chaveado por
    // _etapaRastreadaId: trocar de etapa zera, senão a descida da etapa anterior
    // "vazaria" e concluiria uma etapa recém-iniciada.
    private bool? _registroAnterior;
    private int? _etapaRastreadaId;


    // Ciclos consecutivos com etapa aberta e REGISTRO_RODANDO desligado SEM borda
    // observada. Cobre a descida perdida (ex.: deploy reiniciou o backend enquanto o
    // CLP terminava o ciclo): o estado de borda vive só em memória, então sem isso a
    // etapa ficaria presa em EmExecucao para sempre. É seguro concluir porque a
    // partida só cria a etapa depois de confirmar REGISTRO_RODANDO=true no CLP.
    private int _ciclosParadoSemBorda;
    private const int CiclosParadoParaConcluir = 5;

    // Ciclos consecutivos em que a leitura do INICIA_CONTAGEM falhou (log throttled).
    private int _ciclosComFalhaNaContagem;

    // Sem etapa ativa a leitura é só informativa (alimenta a tela); se o CLP está
    // fora, espaça as tentativas em vez de martelar uma conexão morta a cada segundo.
    private int _ciclo;
    private bool _falhaOciosaRecente;

    public RegistroConclusaoMonitor(
        IServiceScopeFactory scopeFactory,
        ModbusService modbusService,
        EstadoSinaisClp estadoSinais,
        ILogger<RegistroConclusaoMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _modbusService = modbusService;
        _estadoSinais = estadoSinais;
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

        _ciclo++;

        // A etapa em execução, se houver. O monitor lê os sinais SEMPRE — mesmo sem
        // etapa — para a tela do ensaio mostrar o estado real do CLP; a lógica de
        // bordas/conclusão continua valendo só com etapa ativa.
        var etapa = await context.EnsaioEtapas
            .Include(e => e.Ensaio)
                .ThenInclude(en => en.Etapas)
            .Where(e => e.Status == StatusEtapa.EmExecucao)
            .OrderByDescending(e => e.DataInicio)
            .FirstOrDefaultAsync(ct);

        if (etapa == null)
        {
            // Reseta a borda de conclusão quando não há etapa ativa
            _registroAnterior = null;
            _etapaRastreadaId = null;
            _ciclosParadoSemBorda = 0;

            // CLP fora e nada rodando: tenta 1x a cada 10 ciclos, não a cada segundo
            if (_falhaOciosaRecente && _ciclo % 10 != 0)
            {
                return;
            }
        }
        else if (_etapaRastreadaId != etapa.Id)
        {
            // Etapa nova (ou o monitor acabou de subir com etapa já aberta): a borda
            // de conclusão recomeça do zero — a da etapa anterior não vale para esta.
            _etapaRastreadaId = etapa.Id;
            _registroAnterior = null;
            _ciclosParadoSemBorda = 0;
        }

        var registroConfig = await context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == "REGISTRO_RODANDO" && m.Ativo && m.FuncaoModbus == "ReadInputs", ct);

        if (registroConfig == null)
        {
            _estadoSinais.PublicarRegistroRodando(null, "REGISTRO_RODANDO (ReadInputs) não cadastrado ou inativo");
            _estadoSinais.PublicarIniciaContagem(null, "não avaliado: REGISTRO_RODANDO não cadastrado ou inativo");

            if (etapa != null)
            {
                _logger.LogWarning("REGISTRO_RODANDO (ReadInputs) não encontrado; monitor não consegue avaliar conclusão.");
            }

            return;
        }

        // INICIA_CONTAGEM é opcional: se não estiver cadastrado, o monitor se comporta
        // como antes (só a borda de descida do REGISTRO_RODANDO conclui a etapa).
        var contagemConfig = await context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == "INICIA_CONTAGEM" && m.Ativo, ct);

        bool rodando;

        try
        {
            rodando = await LerSinalAsync(registroConfig.Id);
            _estadoSinais.PublicarRegistroRodando(rodando);
            _falhaOciosaRecente = false;
        }
        catch (Exception ex)
        {
            var raiz = ex is AggregateException ae ? ae.InnerException ?? ex : ex;
            _estadoSinais.PublicarRegistroRodando(null, $"{raiz.GetType().Name}: {raiz.Message}");

            // O ciclo aborta aqui, então o INICIA_CONTAGEM não será lido: invalida o
            // cache dele também — senão a tela mostraria um "Ligado" congelado de um
            // CLP que não responde mais.
            _estadoSinais.PublicarIniciaContagem(null, $"não avaliado: falha ao ler REGISTRO_RODANDO ({raiz.GetType().Name})");

            // Falha de leitura não conta como "parado": zera o debounce da descida
            // perdida para nunca concluir uma etapa por CLP indisponível.
            _ciclosParadoSemBorda = 0;

            if (etapa != null)
            {
                // CLP indisponível: não conclui por engano, apenas tenta de novo no próximo ciclo
                _logger.LogWarning(ex, "Falha ao ler REGISTRO_RODANDO no monitor");
            }
            else
            {
                _falhaOciosaRecente = true;
                _logger.LogDebug(ex, "Falha ao ler REGISTRO_RODANDO com a bancada ociosa");
            }

            return;
        }

        // O INICIA_CONTAGEM manda SÓ na contagem de tempo: ele marca o t0 e o fim do
        // patamar, e não opina sobre o fim do ensaio. Quem inicia e para o registro é o
        // REGISTRO_RODANDO, sozinho. Por isso uma falha de leitura aqui nunca trava o
        // encerramento — no máximo o laudo cai na regra antiga do setpoint.
        bool? contando = null;

        if (contagemConfig != null)
        {
            try
            {
                contando = await LerSinalAsync(contagemConfig.Id);
                _estadoSinais.PublicarIniciaContagem(contando);
                _ciclosComFalhaNaContagem = 0;
            }
            catch (Exception ex)
            {
                var raiz = ex is AggregateException ae ? ae.InnerException ?? ex : ex;
                _estadoSinais.PublicarIniciaContagem(null, $"{raiz.GetType().Name}: {raiz.Message}");

                if (etapa != null && _ciclosComFalhaNaContagem++ % 30 == 0)
                {
                    _logger.LogError(ex,
                        "Não foi possível ler INICIA_CONTAGEM (registro {RegistroId}, função {Funcao}, endereço {Endereco}). O t0 do laudo cai na regra do setpoint e o encerramento passa a depender só do REGISTRO_RODANDO.",
                        contagemConfig.Id, contagemConfig.FuncaoModbus, contagemConfig.EnderecoRegistro);
                }
            }
        }
        else
        {
            _estadoSinais.PublicarIniciaContagem(null, "INICIA_CONTAGEM não cadastrado ou inativo");
        }

        if (etapa == null)
        {
            // Bancada parada: as leituras acima já alimentaram a tela; nada a concluir.
            return;
        }

        // Bordas do INICIA_CONTAGEM carimbam a janela de contagem, mesmo na primeira
        // leitura: se o sinal já está ligado e a etapa ainda não tem t0, é agora.
        if (contando.HasValue)
        {
            await RegistrarBordasDaContagemAsync(context, etapa, contando.Value, ct);
        }

        // Quem encerra a etapa é o REGISTRO_RODANDO, por dois caminhos:
        //
        // 1. Borda de descida observada (caminho normal): estava ligado, desligou.
        //
        // 2. Descida PERDIDA: o CLP terminou o ciclo enquanto o monitor não olhava
        //    (ex.: deploy reiniciou o backend — o estado de borda vive só em memória).
        //    Sem este caminho, o monitor acordava vendo rodando=false desde o início,
        //    a borda nunca existia e a etapa ficava presa em EmExecucao para sempre,
        //    obrigando o encerramento manual. Critério: sinal desligado por
        //    N ciclos consecutivos COM a etapa já velha o bastante para não confundir
        //    com a janela de partida (o CLP demora alguns segundos para ligar o sinal
        //    depois que a etapa é criada).
        var bordaObservada = _registroAnterior == true && !rodando;

        if (rodando)
        {
            _ciclosParadoSemBorda = 0;
        }
        else
        {
            _ciclosParadoSemBorda++;
        }

        var idadeEtapa = DateTime.UtcNow - etapa.DataInicio;
        var descidaPerdida = !rodando &&
                             _ciclosParadoSemBorda >= CiclosParadoParaConcluir &&
                             idadeEtapa > TimeSpan.FromSeconds(30);

        if (bordaObservada || descidaPerdida)
        {
            if (bordaObservada)
            {
                _logger.LogInformation(
                    "REGISTRO_RODANDO caiu: concluindo a câmara {Camara} do ensaio {EnsaioId}.",
                    etapa.Camara, etapa.EnsaioId);
            }
            else
            {
                _logger.LogWarning(
                    "REGISTRO_RODANDO está desligado há {Ciclos} ciclos com a câmara {Camara} do ensaio {EnsaioId} aberta (idade {Idade:0}s) — a descida não foi observada (provável restart do backend durante o ciclo). Concluindo a etapa.",
                    _ciclosParadoSemBorda, etapa.Camara, etapa.EnsaioId, idadeEtapa.TotalSeconds);
            }

            await ConcluirEtapaAsync(context, etapa, ct);
        }

        _registroAnterior = rodando;
    }

    private async Task<bool> LerSinalAsync(int registroId)
    {
        var leitura = await _modbusService.LerRegistroAsync(registroId);
        return ModbusService.InterpretarComoLigado(leitura);
    }

    // Loga o flag religado após a janela medida uma única vez por etapa.
    private int? _etapaAvisadaResubidaTardia;

    /// <summary>
    /// Carimba a janela do patamar conforme o INICIA_CONTAGEM, por NÍVEL — a regra da
    /// bancada é literal: "ligou o flag, inicia a contagem; desligou, encerra". O CLP
    /// só liga o sinal quando a pressão de teste é atingida e o desliga no fim, então
    /// NÃO há o que inferir aqui. (Já houve borda/retenção/reabertura — era a
    /// inferência que quebrava a contagem; a leitura é confiável desde a serialização
    /// do Modbus, então o sinal fala por si.)
    /// A janela é medida UMA vez por etapa: flag religado depois dela vira só log.
    /// Toda gravação é condicionada a Status = EmExecucao — etapa encerrada no meio
    /// do ciclo do monitor não ganha carimbo órfão (t0 > DataFim).
    /// </summary>
    private async Task RegistrarBordasDaContagemAsync(
        DataMaisDbContext context, EnsaioEtapa etapa, bool contando, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;

        if (contando)
        {
            if (etapa.DataInicioContagem == null)
            {
                // Ligou o flag → inicia a contagem.
                var linhas = await context.EnsaioEtapas
                    .Where(e => e.Id == etapa.Id && e.Status == StatusEtapa.EmExecucao)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.DataInicioContagem, agora)
                        .SetProperty(e => e.DataAtualizacao, agora), ct);

                if (linhas == 0) return; // etapa foi encerrada no meio do ciclo

                etapa.DataInicioContagem = agora;

                _logger.LogInformation("INICIA_CONTAGEM ligou: t0 da câmara {Camara} do ensaio {EnsaioId} em {T0:o}",
                    etapa.Camara, etapa.EnsaioId, agora);
            }
            else if (etapa.DataFimContagem != null && _etapaAvisadaResubidaTardia != etapa.Id)
            {
                // Janela já medida e o flag religou: não muda nada, só fica registrado.
                _etapaAvisadaResubidaTardia = etapa.Id;
                _logger.LogWarning(
                    "INICIA_CONTAGEM religou na câmara {Camara} do ensaio {EnsaioId} depois da janela medida ({Segundos:0}s) — ignorado; a janela do laudo não muda.",
                    etapa.Camara, etapa.EnsaioId,
                    (etapa.DataFimContagem.Value - etapa.DataInicioContagem.Value).TotalSeconds);
            }

            return;
        }

        // Desligou o flag → encerra a contagem.
        if (etapa.DataInicioContagem != null && etapa.DataFimContagem == null)
        {
            var linhas = await context.EnsaioEtapas
                .Where(e => e.Id == etapa.Id && e.Status == StatusEtapa.EmExecucao)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.DataFimContagem, agora)
                    .SetProperty(e => e.DataAtualizacao, agora), ct);

            if (linhas == 0) return;

            etapa.DataFimContagem = agora;

            _logger.LogInformation("INICIA_CONTAGEM desligou: contagem da câmara {Camara} do ensaio {EnsaioId} durou {Segundos:0}s",
                etapa.Camara, etapa.EnsaioId, (agora - etapa.DataInicioContagem.Value).TotalSeconds);
        }
    }

    /// <summary>
    /// Fecha a etapa como Concluida e, se as câmaras habilitadas já estiverem prontas,
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

        // Só as câmaras HABILITADAS contam: o operador pode ter desmarcado uma para
        // fechar o laudo com a outra sozinha.
        var completo = ensaio.CamarasHabilitadas
            .All(c => ensaio.Etapas.Any(e => e.Camara == c && e.Status == StatusEtapa.Concluida));

        if (ensaio.Status != StatusEnsaio.Aceito && ensaio.Status != StatusEnsaio.Cancelado)
        {
            ensaio.Status = completo ? StatusEnsaio.AguardandoAceite : StatusEnsaio.EmAndamento;
            ensaio.DataAtualizacao = agora;
        }

        await context.SaveChangesAsync(ct);

        _registroAnterior = false;
        _ciclosParadoSemBorda = 0;
        _etapaRastreadaId = null;
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
