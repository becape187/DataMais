using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using DataMais.Data;
using DataMais.Models;
using DataMais.Services;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace DataMais.Controllers;

/// <summary>
/// Ciclo de vida do ensaio hidráulico. Um ensaio é o cabeçalho do teste (cliente,
/// cilindro, vessel, OS) e tem SEMPRE as duas câmaras, cada uma rodada como uma
/// <see cref="EnsaioEtapa"/> — em qualquer ordem, repetíveis. O laudo só nasce
/// quando o operador ACEITA o ensaio com as duas câmaras concluídas.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EnsaioController : ControllerBase
{
    /// <summary>
    /// Pressão residual máxima admitida em QUALQUER uma das câmaras para liberar o
    /// início de uma corrida. Acima disso o cilindro ainda está carregado: a câmara
    /// oposta já começaria pressurizada e o critério de passagem não valeria nada.
    /// </summary>
    private const double PressaoMaximaParaIniciarBar = 3.0;

    private readonly DataMaisDbContext _context;
    private readonly ModbusService _modbusService;
    private readonly ConfigService _configService;
    private readonly EstadoSinaisClp _estadoSinais;
    private readonly ILogger<EnsaioController> _logger;

    public EnsaioController(
        DataMaisDbContext context,
        ModbusService modbusService,
        ConfigService configService,
        EstadoSinaisClp estadoSinais,
        ILogger<EnsaioController> logger)
    {
        _context = context;
        _modbusService = modbusService;
        _configService = configService;
        _estadoSinais = estadoSinais;
        _logger = logger;
    }

    /// <summary>
    /// Última leitura dos sinais de ciclo feita pelo monitor (cache — zero Modbus).
    /// Vai junto nas respostas de polling para a tela mostrar o estado real do CLP.
    /// </summary>
    private object MontarSinaisClp()
    {
        // Captura CADA record uma única vez: o monitor troca a referência a ~1 Hz, e
        // reler a propriedade por campo poderia misturar pedaços de duas leituras
        // (ex.: ligado do publish antigo com erro do novo).
        var rodando = _estadoSinais.RegistroRodando;
        var contagem = _estadoSinais.IniciaContagem;

        return new
        {
            registroRodando = new { ligado = rodando.Ligado, lidoEm = rodando.LidoEm, erro = rodando.Erro },
            iniciaContagem = new { ligado = contagem.Ligado, lidoEm = contagem.LidoEm, erro = contagem.Erro }
        };
    }

    // ── Consulta ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensaio com uma câmara RODANDO agora, com todas as etapas. A tela usa isso para se
    /// reidratar: o backend é a fonte da verdade do que está em execução.
    /// Ensaio aberto porém parado não entra aqui — ele vive em /pendentes, para o operador
    /// escolher explicitamente se retoma. Sem isso, um ensaio esquecido sequestraria a tela.
    /// </summary>
    [HttpGet("ativo")]
    public async Task<IActionResult> GetEnsaioAtivo()
    {
        try
        {
            var ensaio = await CarregarEnsaioEmExecucaoAsync();

            if (ensaio == null)
            {
                return Ok(new { ativo = false });
            }

            return Ok(new { ativo = true, ensaio = MontarDto(ensaio) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter ensaio ativo");
            return StatusCode(500, new { message = "Erro ao obter ensaio ativo", error = ex.Message });
        }
    }

    /// <summary>
    /// Ensaios que ficaram pendentes: falta rodar uma câmara ou falta o aceite.
    /// Sair da tela no meio do ensaio não perde nada — ele reaparece aqui.
    /// </summary>
    [HttpGet("pendentes")]
    public async Task<IActionResult> GetPendentes()
    {
        try
        {
            var ensaios = await _context.Ensaios
                .Include(e => e.Cliente)
                .Include(e => e.Cilindro)
                .Include(e => e.Etapas)
                .Where(e => e.Status == StatusEnsaio.EmAndamento || e.Status == StatusEnsaio.AguardandoAceite)
                .OrderByDescending(e => e.DataCriacao)
                .ToListAsync();

            return Ok(ensaios.Select(MontarDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar ensaios pendentes");
            return StatusCode(500, new { message = "Erro ao listar ensaios pendentes", error = ex.Message });
        }
    }

    /// <summary>Detalhe de um ensaio com suas etapas.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var ensaio = await _context.Ensaios
                .Include(e => e.Cliente)
                .Include(e => e.Cilindro)
                .Include(e => e.Etapas)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            return Ok(MontarDto(ensaio));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao obter ensaio", error = ex.Message });
        }
    }

    // ── Ciclo de vida ───────────────────────────────────────────────────────

    /// <summary>
    /// Cria o cabeçalho do ensaio (passo 1). Nenhuma câmara roda ainda.
    /// Sempre cria um ensaio NOVO — ensaios abertos e parados ficam em /pendentes e só
    /// voltam se o operador retomar. O único bloqueio é uma câmara em execução, porque
    /// a bancada é uma só.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Operador")]
    public async Task<IActionResult> CriarEnsaio([FromBody] CriarEnsaioRequest request)
    {
        try
        {
            var rodando = await _context.EnsaioEtapas
                .Include(e => e.Ensaio)
                .FirstOrDefaultAsync(e => e.Status == StatusEtapa.EmExecucao);

            if (rodando != null)
            {
                return Conflict(new
                {
                    message = $"O ensaio {rodando.Ensaio?.Numero} está com a câmara {rodando.Camara} rodando. Encerre-a antes de começar outro."
                });
            }

            // O cliente/cilindro vêm da tela; o último escolhido fica gravado como padrão
            // para pré-selecionar no próximo ensaio.
            var sistema = _configService.GetConfig().Sistema;
            var clienteId = request.ClienteId ?? sistema.ClienteId;
            var cilindroId = request.CilindroId ?? sistema.CilindroId;

            if (!clienteId.HasValue || !cilindroId.HasValue)
            {
                return BadRequest(new { message = "Selecione o cliente e o cilindro do ensaio." });
            }

            var cliente = await _context.Clientes.FindAsync(clienteId.Value);
            var cilindro = await _context.Cilindros.FindAsync(cilindroId.Value);

            if (cliente == null || cilindro == null)
            {
                return BadRequest(new { message = "Cliente ou cilindro não encontrado no banco de dados." });
            }

            if (cilindro.ClienteId != cliente.Id)
            {
                return BadRequest(new { message = $"O cilindro {cilindro.Nome} não pertence ao cliente {cliente.Nome}." });
            }

            var agora = DateTime.UtcNow;

            var ensaio = new Ensaio
            {
                Numero = $"ENSAIO-{agora:yyyyMMdd-HHmmss}",
                Status = StatusEnsaio.EmAndamento,
                ClienteId = cliente.Id,
                CilindroId = cilindro.Id,
                Vessel = Limpar(request.Vessel),
                LocalTeste = Limpar(request.LocalTeste),
                Departamento = Limpar(request.Departamento),
                OrdemServico = Limpar(request.OrdemServico),
                DataCriacao = agora,
                DataAtualizacao = agora
            };

            _context.Ensaios.Add(ensaio);
            await _context.SaveChangesAsync();

            await _context.Entry(ensaio).Reference(e => e.Cliente).LoadAsync();
            await _context.Entry(ensaio).Reference(e => e.Cilindro).LoadAsync();

            // Guarda a escolha como padrão do próximo ensaio (best-effort: falhar aqui
            // não pode derrubar um ensaio que já está criado no banco).
            try
            {
                _configService.SaveSistemaConfig(new Configuration.SistemaConfig
                {
                    ClienteId = cliente.Id,
                    CilindroId = cilindro.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao gravar cliente/cilindro padrão após criar o ensaio {EnsaioId}", ensaio.Id);
            }

            _logger.LogInformation("Ensaio {Numero} criado (cliente {Cliente}, cilindro {Cilindro})",
                ensaio.Numero, cliente.Nome, cilindro.Nome);

            // Manda para o CLP os limites deste cilindro e a calibração dos sensores.
            // Antes isso acontecia ao salvar a tela "Configurar Sistema"; agora acontece
            // quando o ensaio do cilindro começa, que é quando os valores importam.
            var avisos = await AplicarParametrosDoCilindroNoClpAsync(cilindro);

            return Ok(new
            {
                ensaio = MontarDto(ensaio),
                avisosModbus = avisos.Any() ? avisos : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar ensaio");
            return StatusCode(500, new { message = "Erro ao criar ensaio", error = ex.Message });
        }
    }

    /// <summary>
    /// Inicia uma câmara do ensaio. Repetir uma câmara já concluída é permitido:
    /// entra como a próxima tentativa e a anterior só é aposentada quando ESTA for salva.
    /// </summary>
    [HttpPost("{id:int}/etapa")]
    [Authorize(Roles = "Admin,Operador")]
    public async Task<IActionResult> IniciarEtapa(int id, [FromBody] IniciarEtapaRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ensaio = await _context.Ensaios
                .Include(e => e.Cliente)
                .Include(e => e.Cilindro)
                .Include(e => e.Etapas)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            if (ensaio.Status != StatusEnsaio.EmAndamento && ensaio.Status != StatusEnsaio.AguardandoAceite)
            {
                return BadRequest(new { message = $"Ensaio não está aberto (status atual: {ensaio.Status})." });
            }

            var camara = request.Camara?.Trim().ToUpperInvariant();
            if (camara != "A" && camara != "B")
            {
                return BadRequest(new { message = "Câmara inválida. Use 'A' ou 'B'." });
            }

            if (request.PressaoCarga <= 0)
            {
                return BadRequest(new { message = "Pressão de carga deve ser maior que zero." });
            }

            if (request.TempoCarga <= 0)
            {
                return BadRequest(new { message = "Tempo de carga deve ser maior que zero." });
            }

            // Só uma etapa pode rodar por vez em toda a bancada — o CLP é um só.
            var emExecucao = await _context.EnsaioEtapas
                .Include(e => e.Ensaio)
                .FirstOrDefaultAsync(e => e.Status == StatusEtapa.EmExecucao);

            if (emExecucao != null)
            {
                return Conflict(new
                {
                    message = emExecucao.EnsaioId == id
                        ? $"A câmara {emExecucao.Camara} deste ensaio ainda está rodando. Encerre-a antes de iniciar outra."
                        : $"O ensaio {emExecucao.Ensaio?.Numero} está com a câmara {emExecucao.Camara} rodando. Encerre-o antes."
                });
            }

            // Trava de segurança: as DUAS câmaras precisam estar despressurizadas.
            // Começar com pressão residual invalida o ensaio (o pico da câmara oposta
            // é justamente o critério de passagem) e ainda arrisca o equipamento.
            var pressoes = await LerPressoesDasCamarasAsync(ensaio.Id);
            var impedimento = AvaliarPressaoParaInicio(pressoes);

            if (impedimento != null)
            {
                _logger.LogWarning("Início da câmara {Camara} do ensaio {EnsaioId} bloqueado: {Motivo} (A={PressaoA}, B={PressaoB})",
                    camara, id, impedimento, pressoes.A, pressoes.B);

                return Conflict(new
                {
                    message = impedimento,
                    bloqueioPressao = true,
                    pressaoA = pressoes.A,
                    pressaoB = pressoes.B,
                    limite = PressaoMaximaParaIniciarBar
                });
            }

            var agora = DateTime.UtcNow;
            var proximaTentativa = ensaio.Etapas
                .Where(e => e.Camara == camara)
                .Select(e => e.Tentativa)
                .DefaultIfEmpty(0)
                .Max() + 1;

            var etapa = new EnsaioEtapa
            {
                EnsaioId = ensaio.Id,
                Camara = camara,
                Tentativa = proximaTentativa,
                Status = StatusEtapa.EmExecucao,
                DataInicio = agora,
                PressaoCargaConfigurada = request.PressaoCarga,
                TempoCargaConfigurado = request.TempoCarga,
                DataCriacao = agora,
                DataAtualizacao = agora
            };

            _context.EnsaioEtapas.Add(etapa);

            // O ensaio volta a "em andamento" mesmo que já estivesse aguardando aceite
            // (caso de repetição de uma câmara depois das duas terem rodado).
            ensaio.Status = StatusEnsaio.EmAndamento;
            ensaio.DataInicio ??= agora;
            ensaio.DataAtualizacao = agora;

            await _context.SaveChangesAsync();

            // Comandos Modbus de partida. Falha na SELEÇÃO DA CÂMARA é fatal: sem garantia
            // de qual câmara está pressurizada, o dado não vale — a etapa é removida.
            var inicio = await IniciarRegistroNoClpAsync(camara, request.PressaoCarga, request.TempoCarga);

            if (!inicio.Ok)
            {
                _context.EnsaioEtapas.Remove(etapa);
                await _context.SaveChangesAsync();

                _logger.LogError("Etapa da câmara {Camara} abortada no ensaio {EnsaioId}: {Motivo}",
                    camara, ensaio.Id, inicio.Falha);

                return StatusCode(500, new
                {
                    message = $"Etapa abortada: {inicio.Falha}",
                    abortado = true
                });
            }

            await _context.Entry(ensaio).Collection(e => e.Etapas).LoadAsync();

            return Ok(new
            {
                ensaio = MontarDto(ensaio),
                etapaId = etapa.Id,
                avisosModbus = inicio.Avisos.Any() ? inicio.Avisos : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar etapa do ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao iniciar etapa", error = ex.Message });
        }
    }

    /// <summary>
    /// Encerra a etapa em execução. <paramref name="salvar"/> = false descarta a corrida
    /// e apaga as leituras dela no InfluxDB; a tentativa anterior da mesma câmara,
    /// se houver, permanece valendo.
    /// </summary>
    [HttpPost("etapa/{etapaId:int}/encerrar")]
    [Authorize(Roles = "Admin,Operador")]
    public async Task<IActionResult> EncerrarEtapa(int etapaId, [FromQuery] bool salvar = true)
    {
        try
        {
            var etapa = await _context.EnsaioEtapas
                .Include(e => e.Ensaio)
                    .ThenInclude(en => en.Etapas)
                .FirstOrDefaultAsync(e => e.Id == etapaId);

            if (etapa == null)
            {
                return NotFound(new { message = "Etapa não encontrada" });
            }

            if (etapa.Status != StatusEtapa.EmExecucao)
            {
                return Ok(new
                {
                    message = "Etapa já encerrada",
                    status = etapa.Status,
                    ensaio = MontarDto(etapa.Ensaio)
                });
            }

            await PararRegistroNoClpAsync($"encerrar a etapa {etapaId}");

            var agora = DateTime.UtcNow;
            etapa.DataFim = agora;
            etapa.Status = salvar ? StatusEtapa.Concluida : StatusEtapa.Descartada;
            etapa.DataAtualizacao = agora;

            // Parada manual no meio da contagem: fecha a janela aqui, senão o laudo
            // analisaria daí até o fim dos dados.
            if (etapa.DataInicioContagem != null && etapa.DataFimContagem == null)
            {
                etapa.DataFimContagem = agora;
            }

            if (salvar)
            {
                // A tentativa nova substitui as anteriores da mesma câmara.
                foreach (var anterior in etapa.Ensaio.Etapas.Where(e =>
                             e.Id != etapa.Id &&
                             e.Camara == etapa.Camara &&
                             e.Status == StatusEtapa.Concluida))
                {
                    anterior.Status = StatusEtapa.Repetida;
                    anterior.DataAtualizacao = agora;
                }
            }

            AtualizarStatusEnsaio(etapa.Ensaio, agora);
            await _context.SaveChangesAsync();

            if (!salvar)
            {
                // Descartada: as leituras dessa janela não podem sobrar no Influx,
                // senão contaminariam o gráfico e o veredito da tentativa válida.
                try
                {
                    await RemoverLeiturasInfluxAsync(etapa.EnsaioId, etapa.DataInicio, agora);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao remover leituras da etapa {EtapaId} no InfluxDB", etapaId);
                }
            }

            _logger.LogInformation("Etapa {EtapaId} (câmara {Camara}) do ensaio {EnsaioId} encerrada como {Status}",
                etapaId, etapa.Camara, etapa.EnsaioId, etapa.Status);

            return Ok(new
            {
                message = salvar ? "Etapa salva" : "Etapa descartada",
                status = etapa.Status,
                ensaio = MontarDto(etapa.Ensaio)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao encerrar etapa {EtapaId}", etapaId);
            return StatusCode(500, new { message = "Erro ao encerrar etapa", error = ex.Message });
        }
    }

    /// <summary>
    /// Aceita o ensaio e gera o relatório. Exige as duas câmaras concluídas —
    /// é aqui que o número REH-MPR é queimado, não antes.
    /// </summary>
    [HttpPost("{id:int}/aceitar")]
    [Authorize(Roles = "Admin,Operador")]
    public async Task<IActionResult> AceitarEnsaio(int id)
    {
        try
        {
            var ensaio = await _context.Ensaios
                .Include(e => e.Etapas)
                .Include(e => e.Relatorios)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            // Idempotência: aceitar duas vezes devolve o mesmo laudo, não gera outro número.
            if (ensaio.Status == StatusEnsaio.Aceito)
            {
                var existente = ensaio.Relatorios.OrderByDescending(r => r.DataCriacao).FirstOrDefault();
                return Ok(new
                {
                    message = "Ensaio já aceito",
                    relatorioId = existente?.Id,
                    relatorioNumero = existente?.Numero,
                    jaExistia = true
                });
            }

            if (ensaio.Status == StatusEnsaio.Cancelado)
            {
                return BadRequest(new { message = "Ensaio cancelado não pode ser aceito." });
            }

            if (ensaio.Etapas.Any(e => e.Status == StatusEtapa.EmExecucao))
            {
                return BadRequest(new { message = "Há uma câmara ainda rodando. Encerre-a antes de aceitar o ensaio." });
            }

            var faltando = new[] { "A", "B" }
                .Where(c => !ensaio.Etapas.Any(e => e.Camara == c && e.Status == StatusEtapa.Concluida))
                .ToList();

            if (faltando.Any())
            {
                return BadRequest(new
                {
                    message = $"Faltam câmaras concluídas: {string.Join(" e ", faltando)}. O ensaio só vira laudo com as duas."
                });
            }

            var agora = DateTime.UtcNow;
            var dataRelatorio = ensaio.Etapas
                .Where(e => e.Status == StatusEtapa.Concluida && e.DataFim.HasValue)
                .Max(e => e.DataFim!.Value);

            var numeroRelatorio = await NumeroRelatorioService.GerarProximoAsync(_context, dataRelatorio);

            var relatorio = new Relatorio
            {
                Numero = numeroRelatorio,
                Data = dataRelatorio,
                Observacoes = $"Relatório gerado a partir do ensaio {ensaio.Numero} (câmaras A e B).",
                ClienteId = ensaio.ClienteId,
                CilindroId = ensaio.CilindroId,
                EnsaioId = ensaio.Id,
                DataCriacao = agora,
                DataAtualizacao = agora
            };

            _context.Relatorios.Add(relatorio);

            ensaio.Status = StatusEnsaio.Aceito;
            ensaio.DataFim = dataRelatorio;
            ensaio.DataAtualizacao = agora;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Ensaio {EnsaioId} aceito; relatório {Numero} gerado", ensaio.Id, relatorio.Numero);

            return Ok(new
            {
                message = "Ensaio aceito e relatório gerado",
                relatorioId = relatorio.Id,
                relatorioNumero = relatorio.Numero,
                jaExistia = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aceitar ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao aceitar ensaio", error = ex.Message });
        }
    }

    /// <summary>
    /// Cancela o ensaio inteiro: para o CLP, descarta a etapa em execução e apaga
    /// as leituras do ensaio no InfluxDB. Não gera laudo nem consome número.
    /// </summary>
    [HttpPost("{id:int}/cancelar")]
    [Authorize(Roles = "Admin,Operador")]
    public async Task<IActionResult> CancelarEnsaio(int id)
    {
        try
        {
            var ensaio = await _context.Ensaios
                .Include(e => e.Etapas)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            if (ensaio.Status == StatusEnsaio.Aceito)
            {
                return BadRequest(new { message = "Ensaio já aceito não pode ser cancelado — o laudo já existe." });
            }

            await PararRegistroNoClpAsync($"cancelar o ensaio {id}");

            var agora = DateTime.UtcNow;

            foreach (var etapa in ensaio.Etapas.Where(e => e.Status == StatusEtapa.EmExecucao))
            {
                etapa.Status = StatusEtapa.Descartada;
                etapa.DataFim = agora;
                etapa.DataAtualizacao = agora;
            }

            ensaio.Status = StatusEnsaio.Cancelado;
            ensaio.DataFim = agora;
            ensaio.DataAtualizacao = agora;

            await _context.SaveChangesAsync();

            try
            {
                var inicio = ensaio.DataInicio ?? ensaio.DataCriacao;
                await RemoverLeiturasInfluxAsync(ensaio.Id, inicio, agora);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover leituras do ensaio {EnsaioId} no InfluxDB", id);
            }

            return Ok(new { message = "Ensaio cancelado (não salvo)", status = ensaio.Status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao cancelar ensaio", error = ex.Message });
        }
    }

    // ── Coleta ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Lê as pressões A e B via Modbus, grava no InfluxDB e devolve o ponto para o gráfico.
    /// Sempre lê as DUAS — é o vazamento para a câmara oposta que decide o veredito.
    /// </summary>
    [HttpGet("{id:int}/pressao-atual")]
    public async Task<IActionResult> LerPressaoAtual(int id)
    {
        try
        {
            var ensaio = await _context.Ensaios
                .Include(e => e.Etapas)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            var etapaAtiva = ensaio.Etapas.FirstOrDefault(e => e.Status == StatusEtapa.EmExecucao);
            if (etapaAtiva == null)
            {
                return BadRequest(new { message = "Nenhuma câmara em execução neste ensaio." });
            }

            var pressaoARegistro = await BuscarRegistroPressaoAsync("PRESSAO_A_CONV", "PRESSAO_A");
            var pressaoBRegistro = await BuscarRegistroPressaoAsync("PRESSAO_B_CONV", "PRESSAO_B");

            var pressaoA = await LerPressaoAsync(pressaoARegistro, "A", id);
            var pressaoB = await LerPressaoAsync(pressaoBRegistro, "B", id);

            if (!pressaoA.HasValue && !pressaoB.HasValue)
            {
                // Mesmo sem pressões, os sinais vão no corpo do erro: é justamente com
                // o CLP caindo que a tela mais precisa mostrar o estado real (e a idade)
                // dos sinais, em vez de congelar no último valor bom.
                return StatusCode(500, new
                {
                    message = "Falha ao ler pressões A e B do Modbus",
                    sinais = MontarSinaisClp()
                });
            }

            var timestamp = DateTime.UtcNow;

            try
            {
                var appConfig = _configService.GetConfig();

                if (InfluxConfigurado(appConfig))
                {
                    using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
                    var writeApi = influxClient.GetWriteApiAsync();

                    var point = PointData
                        .Measurement("ensaio_pressao")
                        .Tag("ensaioId", ensaio.Id.ToString())
                        .Tag("clienteId", ensaio.ClienteId.ToString())
                        .Tag("cilindroId", ensaio.CilindroId.ToString())
                        .Timestamp(timestamp, WritePrecision.Ns);

                    if (pressaoA.HasValue) point = point.Field("pressaoA", pressaoA.Value);
                    if (pressaoB.HasValue) point = point.Field("pressaoB", pressaoB.Value);

                    await writeApi.WritePointAsync(point, appConfig.Influx.Bucket, appConfig.Influx.Organization);
                }
                else
                {
                    _logger.LogWarning("Configuração do InfluxDB incompleta. Leituras de ensaio não serão persistidas.");
                }
            }
            catch (Exception ex)
            {
                // Não falha o endpoint se apenas a persistência der erro
                _logger.LogError(ex, "Erro ao gravar leitura de ensaio no InfluxDB");
            }

            return Ok(new
            {
                agoraServidor = DateTime.UtcNow,
                time = DateTime.Now.ToString("HH:mm:ss"),
                pressaoA,
                pressaoB,
                etapaId = etapaAtiva.Id,
                camara = etapaAtiva.Camara,
                sinais = MontarSinaisClp()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler pressão atual do ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao ler pressão atual do ensaio", error = ex.Message });
        }
    }

    /// <summary>
    /// Pressões atuais das duas câmaras, sem exigir etapa em execução e sem gravar no
    /// Influx. A tela usa para mostrar o estado da bancada e avisar que o cilindro ainda
    /// está pressurizado antes mesmo do operador tentar iniciar.
    /// </summary>
    [HttpGet("pressoes")]
    public async Task<IActionResult> LerPressoesDaBancada()
    {
        try
        {
            var pressoes = await LerPressoesDasCamarasAsync(0);
            var impedimento = AvaliarPressaoParaInicio(pressoes);

            return Ok(new
            {
                agoraServidor = DateTime.UtcNow,
                pressaoA = pressoes.A,
                pressaoB = pressoes.B,
                limite = PressaoMaximaParaIniciarBar,
                podeIniciar = impedimento == null,
                impedimento,
                sinais = MontarSinaisClp()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler pressões da bancada");
            return StatusCode(500, new { message = "Erro ao ler pressões da bancada", error = ex.Message });
        }
    }

    /// <summary>
    /// Diagnóstico dos sinais que comandam o ciclo (REGISTRO_RODANDO e INICIA_CONTAGEM):
    /// o que está cadastrado, o valor bruto lido agora e como ele foi interpretado.
    /// Serve para responder "por que a contagem não começou / por que a etapa não fechou"
    /// sem precisar de journalctl na VM.
    /// </summary>
    [HttpGet("sinais-clp")]
    public async Task<IActionResult> DiagnosticarSinaisDoClp()
    {
        try
        {
            // Com a bancada parada, os dois sinais TÊM que estar desligados. Um deles
            // ligado nessa condição é endereço errado (ou coil retido no CLP) — e é
            // exatamente o que faz a contagem "começar sozinha".
            var etapaRodando = await _context.EnsaioEtapas
                .FirstOrDefaultAsync(e => e.Status == StatusEtapa.EmExecucao);

            var bancadaParada = etapaRodando == null;

            return Ok(new
            {
                bancadaParada,
                camaraEmExecucao = etapaRodando?.Camara,
                // Janela de contagem carimbada na etapa em execução — é ela que o
                // cronômetro da tela e o laudo usam. Congelou? Olhe aqui primeiro.
                janelaContagem = etapaRodando == null ? null : new
                {
                    etapaId = etapaRodando.Id,
                    dataInicioContagem = etapaRodando.DataInicioContagem,
                    dataFimContagem = etapaRodando.DataFimContagem
                },
                registroRodando = await DiagnosticarSinalAsync("REGISTRO_RODANDO", bancadaParada),
                iniciaContagem = await DiagnosticarSinalAsync("INICIA_CONTAGEM", bancadaParada),
                ultimaLeituraMonitor = MontarSinaisClp()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao diagnosticar sinais do CLP");
            return StatusCode(500, new { message = "Erro ao diagnosticar sinais do CLP", error = ex.Message });
        }
    }

    private async Task<object> DiagnosticarSinalAsync(string nome, bool bancadaParada)
    {
        var registro = await _context.ModbusConfigs.FirstOrDefaultAsync(m => m.Nome == nome && m.Ativo);

        if (registro == null)
        {
            var inativo = await _context.ModbusConfigs.AnyAsync(m => m.Nome == nome);

            return new
            {
                nome,
                cadastrado = inativo,
                ativo = false,
                problema = inativo
                    ? $"'{nome}' existe no cadastro Modbus mas está INATIVO."
                    : $"'{nome}' não está cadastrado em Registros Modbus."
            };
        }

        object? valorBruto = null;
        string? erro = null;

        try
        {
            valorBruto = await _modbusService.LerRegistroAsync(registro.Id);
        }
        catch (Exception ex)
        {
            var raiz = ex is AggregateException ae ? ae.InnerException ?? ex : ex;
            erro = $"{raiz.GetType().Name}: {raiz.Message}";
        }

        // Ler só é possível com função de leitura; cadastro com função de escrita
        // lança NotSupportedException lá no ModbusService.
        var funcaoDeLeitura = registro.FuncaoModbus is "ReadCoils" or "ReadInputs"
            or "ReadHoldingRegisters" or "ReadInputRegisters";

        var ligado = erro == null ? ModbusService.InterpretarComoLigado(valorBruto) : (bool?)null;

        // Diagnóstico automático, na ordem em que os problemas realmente aparecem.
        string? problema =
            !funcaoDeLeitura
                ? $"'{nome}' está cadastrado com a função {registro.FuncaoModbus}, que é de ESCRITA — não dá para ler o estado dele. Use ReadInputs (0x02) ou ReadCoils."
            : erro != null
                ? $"A leitura de '{nome}' falhou. Confira endereço, slaveId e função no cadastro."
            : ligado == true && bancadaParada
                ? $"'{nome}' está LIGADO com a bancada parada. Como nenhuma câmara está rodando, ele deveria estar desligado — o endereço {registro.EnderecoRegistro} provavelmente aponta para outro ponto, ou o CLP está segurando esse sinal."
            : null;

        return new
        {
            nome,
            cadastrado = true,
            ativo = true,
            registroId = registro.Id,
            funcaoModbus = registro.FuncaoModbus,
            funcaoDeLeitura,
            enderecoRegistro = registro.EnderecoRegistro,
            slaveId = registro.SlaveId,
            tipoDado = registro.TipoDado,
            ip = $"{registro.IpAddress}:{registro.Port}",
            valorBruto = valorBruto?.ToString(),
            tipoRetorno = valorBruto?.GetType().Name,
            ligado,
            erro,
            problema
        };
    }

    /// <summary>
    /// Histórico de pressões do InfluxDB para reconstruir o gráfico ao reentrar na tela.
    /// Com <paramref name="etapaId"/>, recorta só a janela daquela câmara.
    /// </summary>
    [HttpGet("{id:int}/historico")]
    public async Task<IActionResult> GetHistoricoPressao(int id, [FromQuery] int? etapaId = null)
    {
        try
        {
            var ensaio = await _context.Ensaios
                .Include(e => e.Etapas)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ensaio == null)
            {
                return NotFound(new { message = "Ensaio não encontrado" });
            }

            var appConfig = _configService.GetConfig();

            if (!InfluxConfigurado(appConfig))
            {
                _logger.LogWarning("Configuração do InfluxDB incompleta. Não será possível reconstruir o histórico do ensaio {EnsaioId}.", id);
                return Ok(new { dados = Array.Empty<object>(), totalPontos = 0 });
            }

            DateTime de;
            DateTime ate;

            if (etapaId.HasValue)
            {
                var etapa = ensaio.Etapas.FirstOrDefault(e => e.Id == etapaId.Value);
                if (etapa == null)
                {
                    return NotFound(new { message = "Etapa não encontrada neste ensaio" });
                }

                de = etapa.DataInicio;
                ate = etapa.DataFim ?? DateTime.UtcNow;
            }
            else
            {
                de = ensaio.DataInicio ?? ensaio.DataCriacao;
                ate = ensaio.DataFim ?? DateTime.UtcNow;
            }

            var dados = await BuscarSeriesInfluxAsync(appConfig, ensaio.Id, de, ate);

            return Ok(new { dados, totalPontos = dados.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reconstruir histórico do ensaio {EnsaioId}", id);
            return StatusCode(500, new { message = "Erro ao reconstruir histórico do ensaio", error = ex.Message });
        }
    }

    // ── Helpers de domínio ──────────────────────────────────────────────────

    private Task<Ensaio?> CarregarEnsaioEmExecucaoAsync() =>
        _context.Ensaios
            .Include(e => e.Cliente)
            .Include(e => e.Cilindro)
            .Include(e => e.Etapas)
            .Where(e => e.Etapas.Any(et => et.Status == StatusEtapa.EmExecucao))
            .OrderByDescending(e => e.DataCriacao)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Move o ensaio para AguardandoAceite quando as duas câmaras têm etapa concluída,
    /// ou de volta para EmAndamento se ainda falta alguma.
    /// </summary>
    private static void AtualizarStatusEnsaio(Ensaio ensaio, DateTime agora)
    {
        if (ensaio.Status == StatusEnsaio.Aceito || ensaio.Status == StatusEnsaio.Cancelado)
        {
            return;
        }

        var completo = new[] { "A", "B" }
            .All(c => ensaio.Etapas.Any(e => e.Camara == c && e.Status == StatusEtapa.Concluida));

        ensaio.Status = completo ? StatusEnsaio.AguardandoAceite : StatusEnsaio.EmAndamento;
        ensaio.DataAtualizacao = agora;
    }

    private static object MontarDto(Ensaio ensaio)
    {
        var etapas = ensaio.Etapas
            .OrderBy(e => e.Camara)
            .ThenBy(e => e.Tentativa)
            .Select(e => new
            {
                id = e.Id,
                camara = e.Camara,
                tentativa = e.Tentativa,
                status = e.Status,
                dataInicio = e.DataInicio,
                dataFim = e.DataFim,
                dataInicioContagem = e.DataInicioContagem,
                dataFimContagem = e.DataFimContagem,
                pressaoCargaConfigurada = e.PressaoCargaConfigurada,
                tempoCargaConfigurado = e.TempoCargaConfigurado
            })
            .ToList();

        var podeAceitar = new[] { "A", "B" }
            .All(c => ensaio.Etapas.Any(e => e.Camara == c && e.Status == StatusEtapa.Concluida));

        var emExecucao = ensaio.Etapas.FirstOrDefault(e => e.Status == StatusEtapa.EmExecucao);

        return new
        {
            // Relógio do servidor na resposta: a tela calcula os tempos decorridos
            // contra ELE, nunca contra o relógio do PC da bancada — que já esteve
            // horas fora e travava todos os cronômetros em 00:00:00.
            agoraServidor = DateTime.UtcNow,
            id = ensaio.Id,
            numero = ensaio.Numero,
            status = ensaio.Status,
            dataInicio = ensaio.DataInicio,
            dataFim = ensaio.DataFim,
            dataCriacao = ensaio.DataCriacao,
            vessel = ensaio.Vessel,
            localTeste = ensaio.LocalTeste,
            departamento = ensaio.Departamento,
            ordemServico = ensaio.OrdemServico,
            clienteId = ensaio.ClienteId,
            clienteNome = ensaio.Cliente?.Nome,
            cilindroId = ensaio.CilindroId,
            cilindroNome = ensaio.Cilindro?.Nome,
            etapas,
            etapaEmExecucaoId = emExecucao?.Id,
            podeAceitar = podeAceitar && emExecucao == null
        };
    }

    private static string? Limpar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    // ── Modbus ──────────────────────────────────────────────────────────────

    private sealed record ResultadoInicioClp(bool Ok, string? Falha, List<string> Avisos);

    /// <summary>
    /// Sequência de partida no CLP: seleciona a câmara (com double-check), escreve
    /// pressão e tempo de carga e liga INICIA_REGISTRO confirmando REGISTRO_RODANDO.
    /// Falha na seleção da câmara é FATAL (retorna Ok=false); o resto vira aviso.
    /// </summary>
    private async Task<ResultadoInicioClp> IniciarRegistroNoClpAsync(string camara, decimal pressaoCarga, decimal tempoCarga)
    {
        var avisos = new List<string>();

        try
        {
            // 1. Seleciona a câmara (Avança para A, Recua para B). Desliga o botão OPOSTO
            //    antes de ligar o escolhido — se o operador deixou o outro coil retido pela
            //    IHM, os dois ficariam ligados ao mesmo tempo. Depois relê os dois para confirmar.
            var nomeBotaoCamara = camara == "A" ? "BOTAO_AVANCA_IHM" : "BOTAO_RECUA_IHM";
            var nomeBotaoOposto = camara == "A" ? "BOTAO_RECUA_IHM" : "BOTAO_AVANCA_IHM";

            var botaoCamara = await _context.ModbusConfigs.FirstOrDefaultAsync(m => m.Nome == nomeBotaoCamara && m.Ativo);
            var botaoOposto = await _context.ModbusConfigs.FirstOrDefaultAsync(m => m.Nome == nomeBotaoOposto && m.Ativo);

            if (botaoCamara == null)
            {
                return await AbortarPorFalhaCamaraAsync(botaoCamara, botaoOposto,
                    $"Registro '{nomeBotaoCamara}' não encontrado no cadastro Modbus.");
            }

            if (botaoOposto != null)
            {
                var opostoDesligado = await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botaoOposto), false);
                if (!opostoDesligado)
                {
                    return await AbortarPorFalhaCamaraAsync(botaoCamara, botaoOposto,
                        $"Não foi possível desligar {nomeBotaoOposto} antes de ativar a câmara {camara}.");
                }
            }
            else
            {
                avisos.Add($"Registro '{nomeBotaoOposto}' não encontrado — não foi possível garantir que o botão oposto está desligado");
            }

            var camaraAtivada = await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botaoCamara), true);
            if (!camaraAtivada)
            {
                return await AbortarPorFalhaCamaraAsync(botaoCamara, botaoOposto, $"Não foi possível ativar {nomeBotaoCamara}.");
            }

            // Double-check: relê os dois coils e confirma escolhido=ON, oposto=OFF.
            // Se não bater, reaplica os comandos e confere mais uma vez.
            bool confirmado = false;
            bool? estadoCamara = null, estadoOposto = null;

            for (int tentativa = 1; tentativa <= 2 && !confirmado; tentativa++)
            {
                await Task.Delay(200);
                estadoCamara = await LerCoilAsync(botaoCamara.Id);
                estadoOposto = botaoOposto != null ? await LerCoilAsync(botaoOposto.Id) : false;

                if (estadoCamara == true && estadoOposto != true)
                {
                    confirmado = true;
                }
                else if (tentativa < 2)
                {
                    _logger.LogWarning(
                        "Estado dos botões de câmara divergente ({BotaoCamara}={EstadoCamara}, {BotaoOposto}={EstadoOposto}), reaplicando comandos",
                        nomeBotaoCamara, estadoCamara, nomeBotaoOposto, estadoOposto);

                    if (botaoOposto != null && estadoOposto == true)
                        await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botaoOposto), false);
                    if (estadoCamara != true)
                        await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botaoCamara), true);
                }
            }

            if (!confirmado)
            {
                return await AbortarPorFalhaCamaraAsync(botaoCamara, botaoOposto,
                    $"Estado dos botões de câmara não confirmado após reaplicar: {nomeBotaoCamara}={FormatarEstadoCoil(estadoCamara)}, {nomeBotaoOposto}={FormatarEstadoCoil(estadoOposto)} (esperado: ligado / desligado). Verifique se o botão Avança/Recua não está pressionado na tela.");
            }

            _logger.LogInformation("Câmara {Camara} ({Botao}) ativada e confirmada por leitura ({Oposto} desligado)",
                camara, nomeBotaoCamara, nomeBotaoOposto);

            // 2. Pressão de carga
            await EscreverParametroAsync("PRESSAO_CARGA", pressaoCarga, avisos);

            // 3. Tempo de carga
            await EscreverParametroAsync("TEMPO_CARGA", tempoCarga, avisos);

            // 4. Inicia o registro e confirma por REGISTRO_RODANDO. Não confirmar é FATAL:
            //    sem o CLP rodando de fato, a etapa ficaria "em execução" na tela sem nada
            //    acontecendo na bancada e sem ninguém para fechá-la.
            var registroLigou = await LigarIniciaRegistroAsync(avisos);

            if (!registroLigou)
            {
                await PararRegistroNoClpAsync("abortar etapa sem confirmação do REGISTRO_RODANDO");

                return new ResultadoInicioClp(false,
                    "o CLP não confirmou o REGISTRO_RODANDO. A bancada não começou a rodar — verifique a unidade hidráulica e tente de novo.",
                    avisos);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar comandos Modbus ao iniciar etapa");
            avisos.Add($"Erro ao executar comandos Modbus: {ex.Message}");
        }

        return new ResultadoInicioClp(true, null, avisos);
    }

    /// <summary>
    /// Escreve no CLP os parâmetros que dependem do equipamento montado na bancada:
    /// os limites de pressão do cilindro (LIMITE_A/LIMITE_B) e a calibração de 2 pontos
    /// dos sensores de pressão. Best-effort — devolve avisos em vez de falhar o ensaio.
    /// </summary>
    private async Task<List<string>> AplicarParametrosDoCilindroNoClpAsync(Cilindro cilindro)
    {
        var avisos = new List<string>();

        try
        {
            if (cilindro.MaximaPressaoA > 0)
                await EscreverParametroAsync("LIMITE_A", cilindro.MaximaPressaoA, avisos);
            else
                avisos.Add("Cilindro sem pressão máxima da câmara A — LIMITE_A não foi enviado ao CLP");

            if (cilindro.MaximaPressaoB > 0)
                await EscreverParametroAsync("LIMITE_B", cilindro.MaximaPressaoB, avisos);
            else
                avisos.Add("Cilindro sem pressão máxima da câmara B — LIMITE_B não foi enviado ao CLP");

            var sensores = await _context.Sensores.Where(s => s.Ativo).ToListAsync();

            // O sensor "geral" é identificado primeiro: o nome dele também contém a letra A
            // (de "GERAL") e cairia na regra da câmara A se a ordem fosse outra.
            var sensorGeral = sensores.FirstOrDefault(s => s.Nome.ToUpperInvariant().Contains("GERAL"));
            var candidatos = sensores.Where(s => s != sensorGeral).ToList();

            var sensorA = candidatos.FirstOrDefault(s => EhCamara(s.Nome, 'A'));
            var sensorB = candidatos.FirstOrDefault(s => EhCamara(s.Nome, 'B'));

            await EscreverCalibracaoAsync(sensorA, "", "câmara A", avisos);
            await EscreverCalibracaoAsync(sensorB, "_1", "câmara B", avisos);
            await EscreverCalibracaoAsync(sensorGeral, "_2", "pressão geral", avisos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aplicar parâmetros do cilindro {CilindroId} no CLP", cilindro.Id);
            avisos.Add($"Erro ao aplicar parâmetros do cilindro no CLP: {ex.Message}");
        }

        if (avisos.Any())
        {
            _logger.LogWarning("Parâmetros do cilindro {CilindroId} aplicados com avisos: {Avisos}",
                cilindro.Id, string.Join(", ", avisos));
        }

        return avisos;
    }

    /// <summary>
    /// Reconhece o sensor de uma câmara pela letra isolada no nome ("Pressão A",
    /// "Sensor B"), sem casar com a letra no meio de outra palavra.
    /// </summary>
    private static bool EhCamara(string nome, char letra)
    {
        var upper = nome.ToUpperInvariant();
        if (!upper.Contains("PRESS")) return false;

        return System.Text.RegularExpressions.Regex.IsMatch(upper, $@"(^|[^A-Z]){letra}([^A-Z]|$)");
    }

    /// <summary>
    /// Escreve os 4 registros de calibração de um sensor. O sufixo separa os blocos no
    /// CLP: "" = câmara A, "_1" = câmara B, "_2" = pressão geral.
    /// </summary>
    private async Task EscreverCalibracaoAsync(Sensor? sensor, string sufixo, string descricao, List<string> avisos)
    {
        if (sensor == null)
        {
            avisos.Add($"Nenhum sensor ativo identificado para {descricao} — calibração não enviada");
            return;
        }

        if (!sensor.InputMin.HasValue || !sensor.InputMax.HasValue ||
            !sensor.OutputMin.HasValue || !sensor.OutputMax.HasValue)
        {
            avisos.Add($"Sensor '{sensor.Nome}' ({descricao}) sem calibração completa — não enviada ao CLP");
            return;
        }

        await EscreverParametroAsync($"INPUT_MIN{sufixo}", sensor.InputMin.Value, avisos);
        await EscreverParametroAsync($"INPUT_MAX{sufixo}", sensor.InputMax.Value, avisos);
        await EscreverParametroAsync($"OUTPUT_MIN{sufixo}", sensor.OutputMin.Value, avisos);
        await EscreverParametroAsync($"OUTPUT_MAX{sufixo}", sensor.OutputMax.Value, avisos);
    }

    private async Task EscreverParametroAsync(string nome, decimal valor, List<string> avisos)
    {
        var registro = await _context.ModbusConfigs.FirstOrDefaultAsync(m => m.Nome == nome && m.Ativo);

        if (registro == null)
        {
            avisos.Add($"Registro '{nome}' não encontrado");
            return;
        }

        var configTemp = ConfigParaEscrita(registro);

        object v = configTemp.FuncaoModbus == "WriteSingleRegister"
            ? (ushort)Math.Round(valor)
            : (object)(valor > 0);

        if (await _modbusService.EscreverRegistroAsync(configTemp, v))
        {
            _logger.LogInformation("{Nome} = {Valor} escrito com sucesso", nome, valor);
        }
        else
        {
            avisos.Add($"Erro ao escrever {nome}");
        }
    }

    /// <summary>
    /// Liga INICIA_REGISTRO e confirma por leitura do REGISTRO_RODANDO.
    /// Retorna false se o CLP não confirmou que começou a rodar.
    /// </summary>
    private async Task<bool> LigarIniciaRegistroAsync(List<string> avisos)
    {
        var iniciaRegistro = await _context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == "INICIA_REGISTRO" && m.Ativo);

        var registroRodando = await _context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == "REGISTRO_RODANDO" && m.Ativo && m.FuncaoModbus == "ReadInputs");

        if (iniciaRegistro == null)
        {
            avisos.Add("Registro 'INICIA_REGISTRO' não encontrado");
        }

        if (registroRodando == null)
        {
            avisos.Add("Registro 'REGISTRO_RODANDO' (ReadInputs) não encontrado");
        }

        if (iniciaRegistro == null || registroRodando == null)
        {
            return false;
        }

        // INICIA_REGISTRO é coil de NÍVEL (não pulso): permanece LIGADO durante todo o
        // registro e só é desligado ao encerrar a etapa ou cancelar o ensaio.
        if (!await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(iniciaRegistro), true))
        {
            avisos.Add("Erro ao ativar INICIA_REGISTRO");
            return false;
        }

        await Task.Delay(300);

        // 5 s: agora não confirmar ABORTA a etapa, então um falso negativo custa caro —
        // vale esperar mais do que os 2 s de quando isso era só um aviso.
        var timeout = TimeSpan.FromSeconds(5);
        var inicioVerificacao = DateTime.UtcNow;
        var tentativas = 0;

        while (DateTime.UtcNow - inicioVerificacao < timeout)
        {
            await Task.Delay(200);
            tentativas++;

            try
            {
                var status = await _modbusService.LerRegistroAsync(registroRodando.Id);
                bool rodando = status is bool b ? b : (status?.ToString() == "1" || status?.ToString() == "True");

                if (rodando)
                {
                    _logger.LogInformation("REGISTRO_RODANDO confirmado após {Tentativas} tentativas", tentativas);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao ler REGISTRO_RODANDO na tentativa {Tentativa}", tentativas);
            }
        }

        _logger.LogWarning("REGISTRO_RODANDO não confirmou em {Timeout}s após {Tentativas} leituras",
            timeout.TotalSeconds, tentativas);

        return false;
    }

    /// <summary>
    /// Desliga INICIA_REGISTRO e os DOIS botões de câmara da IHM. Best-effort:
    /// falhas viram log, não impedem o encerramento no banco.
    /// </summary>
    private async Task PararRegistroNoClpAsync(string contexto)
    {
        if (!await SetIniciaRegistroAsync(false))
        {
            _logger.LogWarning("Não foi possível desligar INICIA_REGISTRO ao {Contexto}", contexto);
        }

        var falhas = await DesligarBotoesCamaraAsync();
        if (falhas.Any())
        {
            _logger.LogWarning("Falhas ao desligar botões de câmara ao {Contexto}: {Falhas}", contexto, string.Join(", ", falhas));
        }
    }

    private async Task<bool> SetIniciaRegistroAsync(bool valor)
    {
        var iniciaRegistro = await _context.ModbusConfigs
            .FirstOrDefaultAsync(m => m.Nome == "INICIA_REGISTRO" && m.Ativo);

        if (iniciaRegistro == null)
        {
            _logger.LogWarning("Registro 'INICIA_REGISTRO' não encontrado ao tentar definir {Valor}", valor);
            return false;
        }

        var configTemp = ConfigParaEscrita(iniciaRegistro);

        object v = configTemp.FuncaoModbus == "WriteSingleCoil"
            ? valor
            : (object)(ushort)(valor ? 1 : 0);

        return await _modbusService.EscreverRegistroAsync(configTemp, v);
    }

    /// <summary>
    /// Monta uma cópia do registro com a função de ESCRITA correspondente — os registros
    /// de botão são cadastrados como ReadCoils (leitura de estado) e precisam virar
    /// WriteSingleCoil/WriteSingleRegister para escrever.
    /// </summary>
    private static ModbusConfig ConfigParaEscrita(ModbusConfig registro)
    {
        string funcaoEscrita = registro.TipoDado == "Boolean" || registro.FuncaoModbus == "ReadCoils"
            ? "WriteSingleCoil"
            : "WriteSingleRegister";

        return new ModbusConfig
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
    }

    private async Task<bool?> LerCoilAsync(int registroId)
    {
        try
        {
            var valor = await _modbusService.LerRegistroAsync(registroId);
            if (valor == null) return null;
            return valor is bool b ? b : valor.ToString() == "1" || valor.ToString() == "True";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao ler estado do coil {RegistroId} para confirmação", registroId);
            return null;
        }
    }

    private static string FormatarEstadoCoil(bool? estado) =>
        estado == null ? "leitura falhou" : (estado.Value ? "ligado" : "desligado");

    private async Task<List<string>> DesligarBotoesCamaraAsync()
    {
        var falhas = new List<string>();

        foreach (var nome in new[] { "BOTAO_AVANCA_IHM", "BOTAO_RECUA_IHM" })
        {
            try
            {
                var botao = await _context.ModbusConfigs.FirstOrDefaultAsync(m => m.Nome == nome && m.Ativo);

                if (botao == null)
                {
                    _logger.LogWarning("Registro '{Nome}' não encontrado ao desligar botões de câmara", nome);
                    falhas.Add($"Registro '{nome}' não encontrado");
                    continue;
                }

                if (await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botao), false))
                {
                    _logger.LogInformation("{Nome} desligado ao parar o registro", nome);
                }
                else
                {
                    falhas.Add($"Erro ao desligar {nome}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao desligar {Nome} ao parar o registro", nome);
                falhas.Add($"Erro ao desligar {nome}: {ex.Message}");
            }
        }

        return falhas;
    }

    /// <summary>
    /// Desliga os dois botões por segurança e devolve a falha fatal — sem certeza de
    /// qual câmara está pressurizada, a corrida não vale.
    /// </summary>
    private async Task<ResultadoInicioClp> AbortarPorFalhaCamaraAsync(
        ModbusConfig? botaoCamara, ModbusConfig? botaoOposto, string motivo)
    {
        foreach (var botao in new[] { botaoCamara, botaoOposto })
        {
            if (botao == null) continue;
            try
            {
                await _modbusService.EscreverRegistroAsync(ConfigParaEscrita(botao), false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao desligar {Botao} durante aborto da etapa", botao.Nome);
            }
        }

        return new ResultadoInicioClp(false, motivo, new List<string>());
    }

    private async Task<ModbusConfig?> BuscarRegistroPressaoAsync(string nomePreferido, string nomeAlternativo)
    {
        return await _context.ModbusConfigs.FirstOrDefaultAsync(m => m.Ativo && m.Nome == nomePreferido)
            ?? await _context.ModbusConfigs.FirstOrDefaultAsync(m => m.Ativo &&
                (m.Nome == nomeAlternativo || m.Nome == "PRESSAO_GERAL_CONV" || m.Nome == "PRESSAO_GERAL"));
    }

    private async Task<double?> LerPressaoAsync(ModbusConfig? registro, string camara, int ensaioId)
    {
        if (registro == null) return null;

        try
        {
            var valorObj = await _modbusService.LerRegistroAsync(registro.Id);
            if (valorObj == null) return null;

            var valor = Convert.ToDouble(valorObj);
            if (double.IsNaN(valor) || double.IsInfinity(valor)) return null;

            return Math.Max(0, Math.Min(1000, valor));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao ler pressão {Camara} do Modbus para ensaio {EnsaioId}", camara, ensaioId);
            return null;
        }
    }

    private sealed record PressoesBancada(double? A, double? B);

    /// <summary>Lê as duas câmaras de uma vez (só leitura — não grava no Influx).</summary>
    private async Task<PressoesBancada> LerPressoesDasCamarasAsync(int ensaioId)
    {
        var registroA = await BuscarRegistroPressaoAsync("PRESSAO_A_CONV", "PRESSAO_A");
        var registroB = await BuscarRegistroPressaoAsync("PRESSAO_B_CONV", "PRESSAO_B");

        return new PressoesBancada(
            await LerPressaoAsync(registroA, "A", ensaioId),
            await LerPressaoAsync(registroB, "B", ensaioId));
    }

    /// <summary>
    /// Motivo pelo qual a bancada NÃO pode iniciar uma corrida agora, ou null se pode.
    /// Pressão que não pôde ser lida também bloqueia: sem confirmar que o cilindro está
    /// aliviado, o certo é não começar.
    /// </summary>
    private static string? AvaliarPressaoParaInicio(PressoesBancada pressoes)
    {
        var naoLidas = new List<string>();
        if (!pressoes.A.HasValue) naoLidas.Add("A");
        if (!pressoes.B.HasValue) naoLidas.Add("B");

        if (naoLidas.Count > 0)
        {
            return $"Não foi possível ler a pressão da câmara {string.Join(" e ", naoLidas)} no CLP. " +
                   "Sem confirmar que o cilindro está aliviado, o ensaio não pode começar.";
        }

        var acima = new List<string>();
        if (pressoes.A!.Value >= PressaoMaximaParaIniciarBar) acima.Add($"câmara A com {pressoes.A.Value:0.##} bar");
        if (pressoes.B!.Value >= PressaoMaximaParaIniciarBar) acima.Add($"câmara B com {pressoes.B.Value:0.##} bar");

        if (acima.Count > 0)
        {
            return $"O cilindro ainda está pressurizado ({string.Join(" e ", acima)}). " +
                   $"As duas câmaras precisam estar abaixo de {PressaoMaximaParaIniciarBar:0.##} bar para iniciar o ensaio. " +
                   "Alivie a pressão e tente de novo.";
        }

        return null;
    }

    // ── InfluxDB ────────────────────────────────────────────────────────────

    private static bool InfluxConfigurado(Configuration.AppConfig appConfig) =>
        !string.IsNullOrWhiteSpace(appConfig.Influx.Url) &&
        !string.IsNullOrWhiteSpace(appConfig.Influx.Token) &&
        !string.IsNullOrWhiteSpace(appConfig.Influx.Organization) &&
        !string.IsNullOrWhiteSpace(appConfig.Influx.Bucket);

    private async Task<List<object>> BuscarSeriesInfluxAsync(
        Configuration.AppConfig appConfig, int ensaioId, DateTime de, DateTime ate)
    {
        var from = de.ToUniversalTime().AddMinutes(-1);
        var to = ate.ToUniversalTime().AddMinutes(1);

        var flux = $@"from(bucket: ""{appConfig.Influx.Bucket}"")
  |> range(start: {from:o}, stop: {to:o})
  |> filter(fn: (r) => r._measurement == ""ensaio_pressao"" and r.ensaioId == ""{ensaioId}"" and (r._field == ""pressaoA"" or r._field == ""pressaoB""))
  |> sort(columns: [""_time""])
  |> keep(columns: [""_time"", ""_value"", ""_field""])";

        using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
        var queryApi = influxClient.GetQueryApi();
        var tables = await queryApi.QueryAsync(flux, appConfig.Influx.Organization);

        // Agrupa por timestamp, combinando A e B do mesmo instante
        var pontos = new SortedDictionary<DateTime, Dictionary<string, double>>();

        foreach (var table in tables)
        {
            foreach (var record in table.Records)
            {
                var time = record.GetTime();
                var field = record.GetField();
                var value = record.GetValue();
                if (time == null || field == null || value is not IConvertible) continue;

                try
                {
                    var dt = time.Value.ToDateTimeUtc();
                    if (!pontos.ContainsKey(dt)) pontos[dt] = new Dictionary<string, double>();
                    pontos[dt][field] = Convert.ToDouble(value);
                }
                catch { }
            }
        }

        return pontos.Select(kv => (object)new
        {
            time = kv.Key.ToLocalTime().ToString("HH:mm:ss"),
            pressaoA = kv.Value.ContainsKey("pressaoA") ? (double?)Math.Round(kv.Value["pressaoA"], 2) : null,
            pressaoB = kv.Value.ContainsKey("pressaoB") ? (double?)Math.Round(kv.Value["pressaoB"], 2) : null
        }).ToList();
    }

    /// <summary>
    /// Remove do InfluxDB as leituras de um ensaio numa janela de tempo — usado ao
    /// descartar uma etapa (só a janela dela) ou cancelar o ensaio (janela inteira).
    /// </summary>
    private async Task RemoverLeiturasInfluxAsync(int ensaioId, DateTime de, DateTime ate)
    {
        var appConfig = _configService.GetConfig();

        if (!InfluxConfigurado(appConfig))
        {
            _logger.LogWarning("Configuração do InfluxDB incompleta. Não será possível remover leituras do ensaio {EnsaioId}.", ensaioId);
            return;
        }

        var from = de.ToUniversalTime().AddMinutes(-1);
        var to = ate.ToUniversalTime().AddMinutes(1);
        var predicate = $"_measurement=\"ensaio_pressao\" AND ensaioId=\"{ensaioId}\"";

        using var influxClient = new InfluxDBClient(appConfig.Influx.Url, appConfig.Influx.Token);
        var deleteApi = influxClient.GetDeleteApi();

        await deleteApi.Delete(from, to, predicate, appConfig.Influx.Bucket, appConfig.Influx.Organization);

        _logger.LogInformation("Leituras do ensaio {EnsaioId} removidas do InfluxDB no intervalo {From} - {To}", ensaioId, from, to);
    }
}

// ── DTOs ────────────────────────────────────────────────────────────────────

public class CriarEnsaioRequest
{
    /// <summary>Cliente dono do cilindro. Se omitido, usa o último ensaio como padrão.</summary>
    public int? ClienteId { get; set; }

    /// <summary>Cilindro sob teste. Se omitido, usa o último ensaio como padrão.</summary>
    public int? CilindroId { get; set; }

    /// <summary>Embarcação / unidade testada (ex.: MV29 / Frota).</summary>
    public string? Vessel { get; set; }

    /// <summary>Local do teste (ex.: Macaé).</summary>
    public string? LocalTeste { get; set; }

    /// <summary>Departamento responsável (ex.: ONSHORE PRESERVATION).</summary>
    public string? Departamento { get; set; }

    /// <summary>Ordem de Serviço / Work Order.</summary>
    public string? OrdemServico { get; set; }
}

public class IniciarEtapaRequest
{
    /// <summary>Câmara a pressurizar: "A" (avança) ou "B" (recua).</summary>
    [Required]
    public string Camara { get; set; } = string.Empty;

    /// <summary>Pressão de carga desta câmara (bar).</summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "Pressão de carga deve ser maior que zero.")]
    public decimal PressaoCarga { get; set; }

    /// <summary>Tempo de carga desta câmara (minutos).</summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "Tempo de carga deve ser maior que zero.")]
    public decimal TempoCarga { get; set; }
}
