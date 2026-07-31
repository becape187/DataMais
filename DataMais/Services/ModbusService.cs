using NModbus;
using NModbus.Device;
using System.Net.Sockets;
using DataMais.Data;
using DataMais.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DataMais.Services;

public class ModbusService
{
    /// <summary>
    /// Interpreta a leitura de um registro como sinal ligado/desligado.
    /// Coil/input volta como <c>bool</c>; holding/input register volta como número
    /// (com fator e offset já aplicados), e aí qualquer valor diferente de zero é
    /// "ligado" — comparar o texto com "1" erra em 1,0, 255 ou 65280.
    /// </summary>
    public static bool InterpretarComoLigado(object? leitura)
    {
        if (leitura is bool b) return b;
        if (leitura is null) return false;

        if (leitura is IConvertible)
        {
            try
            {
                return Math.Abs(Convert.ToDouble(leitura, System.Globalization.CultureInfo.InvariantCulture)) > 1e-9;
            }
            catch (FormatException) { /* não é numérico: cai no texto abaixo */ }
            catch (InvalidCastException) { }
            catch (OverflowException) { return true; }
        }

        var texto = leitura.ToString()?.Trim();
        return string.Equals(texto, "true", StringComparison.OrdinalIgnoreCase) || texto == "1";
    }

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ModbusService> _logger;
    private readonly Dictionary<string, IModbusMaster> _connections = new();
    private readonly Dictionary<string, TcpClient> _tcpClients = new();
    private readonly Dictionary<string, DateTime> _lastUsed = new();
    private readonly object _lockObject = new();

    // Um semáforo por conexão (IP:porta). O IModbusMaster do NModbus NÃO é thread-safe:
    // duas transações simultâneas no mesmo socket entrelaçam os frames TCP, e como
    // REGISTRO_RODANDO e INICIA_CONTAGEM têm respostas de formato idêntico (0x02), a
    // resposta de um era entregue ao outro — contagem "ligando sozinha" e etapa que
    // não encerrava. Toda transação passa a ser exclusiva na sua conexão.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _locksPorConexao = new();

    // Teto de espera na fila da conexão. Dimensionado acima do pior caso de UMA
    // transação segurando o semáforo: connect (5s) + send (10s) + receive (10s) + folga
    // — já contando que o retry interno do NModbus está zerado na criação do master
    // (Transport.Retries = 0; sem isso a posse chegaria a ~40s e quem esperasse na
    // fila estouraria sempre).
    private static readonly TimeSpan TimeoutFilaConexao = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _operationTimeout = TimeSpan.FromSeconds(10);
    // Desabilitado: conexões são mantidas abertas permanentemente para evitar múltiplas conexões no CLP
    // private readonly TimeSpan _maxIdleTime = TimeSpan.FromMinutes(5);

    public ModbusService(IServiceScopeFactory serviceScopeFactory, ILogger<ModbusService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        
        // Limpeza periódica apenas de conexões inválidas (não por tempo de inatividade)
        // Conexões válidas são mantidas abertas permanentemente para evitar múltiplas conexões no CLP
        _ = Task.Run(async () => await LimparConexoesInvalidasAsync());
    }

    /// <summary>
    /// Executa UMA transação Modbus com exclusividade na conexão daquele IP:porta.
    /// É o único caminho até o socket: obter/criar a conexão, executar e — em falha de
    /// transporte — derrubar a conexão acontecem todos DENTRO do semáforo, então o
    /// dispose nunca acontece debaixo de outra transação em andamento (era isso que
    /// gerava a cascata de ObjectDisposedException entre o monitor e o polling da tela).
    /// </summary>
    private async Task<T> ExecutarNaConexaoAsync<T>(string ipAddress, int port, Func<IModbusMaster, T> operacao)
    {
        var key = $"{ipAddress}:{port}";
        var semaforo = _locksPorConexao.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        if (!await semaforo.WaitAsync(TimeoutFilaConexao))
        {
            throw new TimeoutException($"Fila de transações Modbus de {key} não liberou em {TimeoutFilaConexao.TotalSeconds:0}s");
        }

        try
        {
            var master = await ObterOuCriarConexaoAsync(ipAddress, port);

            // Operação Modbus síncrona em thread pool para não bloquear o chamador
            return await Task.Run(() => operacao(master));
        }
        catch (Exception ex) when (IsErroTransiente(ex))
        {
            // Conexão suspeita: remove ainda com o semáforo em mãos — ninguém mais a usa.
            RemoverConexao(ipAddress, port);
            throw;
        }
        finally
        {
            semaforo.Release();
        }
    }

    /// <summary>
    /// Lê todos os registros Modbus ativos, agrupando por IP para otimizar conexões TCP
    /// </summary>
    public async Task<Dictionary<int, object>> LerTodosRegistrosAsync()
    {
        var resultados = new Dictionary<int, object>();

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataMaisDbContext>();

        // Busca todos os registros ativos ordenados por IP e ordem de leitura
        var registros = await context.ModbusConfigs
            .Where(r => r.Ativo)
            .OrderBy(r => r.IpAddress)
            .ThenBy(r => r.OrdemLeitura)
            .ToListAsync();

        if (!registros.Any())
            return resultados;

        // Agrupa por IP para usar a mesma conexão TCP
        var gruposPorIp = registros.GroupBy(r => $"{r.IpAddress}:{r.Port}");

        foreach (var grupo in gruposPorIp)
        {
            var ipPort = grupo.Key.Split(':');
            var ipAddress = ipPort[0];
            var port = int.Parse(ipPort[1]);

            // Cada registro adquire o semáforo da conexão individualmente: entre um e
            // outro, o monitor e a tela do ensaio conseguem intercalar as leituras deles.
            foreach (var registro in grupo)
            {
                try
                {
                    resultados[registro.Id] = await ExecutarNaConexaoAsync(
                        ipAddress, port, master => LerRegistroInterno(master, registro));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao ler registro {RegistroId} ({Nome})", registro.Id, registro.Nome);
                    resultados[registro.Id] = null;
                }
            }
        }

        return resultados;
    }

    /// <summary>
    /// Lê um registro Modbus específico
    /// </summary>
    public async Task<object?> LerRegistroAsync(int registroId)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataMaisDbContext>();

        var registro = await context.ModbusConfigs.FindAsync(registroId);
        if (registro == null || !registro.Ativo)
            return null;

        return await ExecutarComRetryAsync(
            () => ExecutarNaConexaoAsync(registro.IpAddress, registro.Port,
                master => LerRegistroInterno(master, registro)),
            registro.IpAddress, registro.Port);
    }

    /// <summary>
    /// Escreve um valor em um registro Modbus
    /// </summary>
    public async Task<bool> EscreverRegistroAsync(int registroId, object valor)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataMaisDbContext>();

        var registro = await context.ModbusConfigs.FindAsync(registroId);
        if (registro == null || !registro.Ativo)
            return false;

        return await EscreverRegistroAsync(registro, valor);
    }

    /// <summary>
    /// Escreve um valor em um registro Modbus usando uma configuração específica
    /// </summary>
    public async Task<bool> EscreverRegistroAsync(ModbusConfig registro, object valor)
    {
        if (registro == null || !registro.Ativo)
            return false;

        return await ExecutarComRetryAsync(
            () => ExecutarNaConexaoAsync(registro.IpAddress, registro.Port, master =>
            {
                EscreverRegistroInterno(master, registro, valor);
                return true;
            }),
            registro.IpAddress, registro.Port);
    }

    /// <summary>
    /// Corpo síncrono da escrita. Só pode ser chamado de dentro de
    /// <see cref="ExecutarNaConexaoAsync"/> — é o semáforo de lá que garante a
    /// exclusividade no socket.
    /// </summary>
    private void EscreverRegistroInterno(IModbusMaster master, ModbusConfig registro, object valor)
    {
        try
        {
            switch (registro.FuncaoModbus)
            {
                case "WriteSingleRegister":
                    if (valor is ushort ushortValue)
                    {
                        master.WriteSingleRegister(registro.SlaveId, registro.EnderecoRegistro, ushortValue);
                        _logger.LogDebug("WriteSingleRegister: SlaveId={SlaveId}, Address={Address}, Value={Value}",
                            registro.SlaveId, registro.EnderecoRegistro, ushortValue);
                    }
                    else
                    {
                        _logger.LogWarning("Valor inválido para WriteSingleRegister: esperado ushort, recebido {Tipo}", valor.GetType().Name);
                        throw new ArgumentException($"Valor inválido para WriteSingleRegister: esperado ushort, recebido {valor.GetType().Name}");
                    }
                    break;

                case "WriteSingleCoil":
                    if (valor is bool boolValue)
                    {
                        master.WriteSingleCoil(registro.SlaveId, registro.EnderecoRegistro, boolValue);
                        _logger.LogDebug("WriteSingleCoil: SlaveId={SlaveId}, Address={Address}, Value={Value}",
                            registro.SlaveId, registro.EnderecoRegistro, boolValue);
                    }
                    else
                    {
                        _logger.LogWarning("Valor inválido para WriteSingleCoil: esperado bool, recebido {Tipo}", valor.GetType().Name);
                        throw new ArgumentException($"Valor inválido para WriteSingleCoil: esperado bool, recebido {valor.GetType().Name}");
                    }
                    break;

                default:
                    _logger.LogWarning("Função Modbus {Funcao} não suporta escrita", registro.FuncaoModbus);
                    throw new NotSupportedException($"Função Modbus {registro.FuncaoModbus} não suporta escrita");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao escrever registro Modbus: Função={Funcao}, SlaveId={SlaveId}, Address={Address}, Valor={Valor}",
                registro.FuncaoModbus, registro.SlaveId, registro.EnderecoRegistro, valor);
            throw;
        }
    }

    /// <summary>
    /// Corpo síncrono da leitura. Só pode ser chamado de dentro de
    /// <see cref="ExecutarNaConexaoAsync"/> — é o semáforo de lá que garante a
    /// exclusividade no socket.
    /// </summary>
    private object LerRegistroInterno(IModbusMaster master, ModbusConfig registro)
    {
        try
        {
            switch (registro.FuncaoModbus)
            {
                case "ReadCoils":
                    var coils = master.ReadCoils(registro.SlaveId, registro.EnderecoRegistro, registro.QuantidadeRegistros);
                    _logger.LogDebug("ReadCoils: SlaveId={SlaveId}, Address={Address}, Result={Result}",
                        registro.SlaveId, registro.EnderecoRegistro, coils != null && coils.Length > 0 ? coils[0].ToString() : "null");
                    return coils != null && coils.Length > 0 ? coils[0] : false;

                case "ReadInputs":
                    var inputs = master.ReadInputs(registro.SlaveId, registro.EnderecoRegistro, registro.QuantidadeRegistros);
                    _logger.LogDebug("ReadInputs: SlaveId={SlaveId}, Address={Address}, Result={Result}",
                        registro.SlaveId, registro.EnderecoRegistro, inputs != null && inputs.Length > 0 ? inputs[0].ToString() : "null");
                    return inputs != null && inputs.Length > 0 ? inputs[0] : false;

                case "ReadHoldingRegisters":
                    var holdingRegisters = master.ReadHoldingRegisters(registro.SlaveId, registro.EnderecoRegistro, registro.QuantidadeRegistros);
                    var valorHolding = ConverterValor(holdingRegisters, registro);
                    _logger.LogDebug("ReadHoldingRegisters: SlaveId={SlaveId}, Address={Address}, Result={Result}",
                        registro.SlaveId, registro.EnderecoRegistro, valorHolding);
                    return valorHolding;

                case "ReadInputRegisters":
                    var inputRegisters = master.ReadInputRegisters(registro.SlaveId, registro.EnderecoRegistro, registro.QuantidadeRegistros);
                    var valorInput = ConverterValor(inputRegisters, registro);
                    _logger.LogDebug("ReadInputRegisters: SlaveId={SlaveId}, Address={Address}, Result={Result}",
                        registro.SlaveId, registro.EnderecoRegistro, valorInput);
                    return valorInput;

                default:
                    throw new NotSupportedException($"Função Modbus {registro.FuncaoModbus} não suportada");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler registro Modbus: Função={Funcao}, SlaveId={SlaveId}, Address={Address}",
                registro.FuncaoModbus, registro.SlaveId, registro.EnderecoRegistro);
            throw;
        }
    }

    private object ConverterValor(ushort[] valores, ModbusConfig registro)
    {
        if (valores == null || valores.Length == 0)
            return 0;

        // Aplica fator de conversão e offset se existirem
        decimal valorConvertido = 0;

        switch (registro.TipoDado)
        {
            case "UInt16":
                valorConvertido = valores[0];
                break;

            case "Int16":
                valorConvertido = (short)valores[0];
                break;

            case "UInt32":
            case "Int32":
            case "Float":
                if (valores.Length >= 2)
                {
                    uint valorUint32;
                    if (registro.ByteOrder == "BigEndian")
                    {
                        valorUint32 = (uint)((valores[0] << 16) | valores[1]);
                    }
                    else
                    {
                        valorUint32 = (uint)((valores[1] << 16) | valores[0]);
                    }

                    if (registro.TipoDado == "Float")
                    {
                        var bytes = BitConverter.GetBytes(valorUint32);
                        if (registro.ByteOrder == "BigEndian")
                        {
                            Array.Reverse(bytes);
                        }
                        valorConvertido = (decimal)BitConverter.ToSingle(bytes, 0);
                    }
                    else if (registro.TipoDado == "Int32")
                    {
                        valorConvertido = (int)valorUint32;
                    }
                    else
                    {
                        valorConvertido = valorUint32;
                    }
                }
                break;

            case "Boolean":
                return valores[0] != 0;
        }

        // Aplica fator de conversão
        if (registro.FatorConversao.HasValue)
        {
            valorConvertido *= registro.FatorConversao.Value;
        }

        // Aplica offset
        if (registro.Offset.HasValue)
        {
            valorConvertido += registro.Offset.Value;
        }

        return valorConvertido;
    }

    private async Task<IModbusMaster> ObterOuCriarConexaoAsync(string ipAddress, int port)
    {
        var key = $"{ipAddress}:{port}";

        lock (_lockObject)
        {
            if (_connections.TryGetValue(key, out var existingMaster))
            {
                // Verifica se a conexão ainda está válida
                if (IsConexaoValida(key))
                {
                    _lastUsed[key] = DateTime.UtcNow;
                    return existingMaster;
                }
                else
                {
                    // Remove conexão inválida
                    _logger.LogWarning("Conexão Modbus inválida detectada, removendo: {Key}", key);
                    RemoverConexaoInterna(key);
                }
            }

            // Cria nova conexão
            return CriarNovaConexao(ipAddress, port, key);
        }
    }

    private IModbusMaster CriarNovaConexao(string ipAddress, int port, string key)
    {
        try
        {
            var tcpClient = new TcpClient();
            tcpClient.ReceiveTimeout = (int)_operationTimeout.TotalMilliseconds;
            tcpClient.SendTimeout = (int)_operationTimeout.TotalMilliseconds;
            
            // Conecta com timeout
            var connectTask = tcpClient.ConnectAsync(ipAddress, port);
            try
            {
                if (!connectTask.Wait(_connectionTimeout))
                {
                    tcpClient.Close();
                    throw new TimeoutException($"Timeout ao conectar em {ipAddress}:{port}");
                }
            }
            catch (AggregateException ae)
            {
                tcpClient.Close();
                throw ae.InnerException ?? ae;
            }

            if (!tcpClient.Connected)
            {
                tcpClient.Close();
                throw new SocketException((int)SocketError.NotConnected);
            }

            var factory = new ModbusFactory();
            var master = factory.CreateMaster(tcpClient);

            // O NModbus retenta internamente 3x por padrão, cada uma esperando o
            // ReceiveTimeout inteiro — uma transação contra CLP travado seguraria o
            // semáforo da conexão por ~40s. O retry fica todo por conta do
            // ExecutarComRetryAsync (que recria a conexão entre tentativas).
            master.Transport.Retries = 0;

            _connections[key] = master;
            _tcpClients[key] = tcpClient;
            _lastUsed[key] = DateTime.UtcNow;

            // Log apenas em Debug para evitar poluição de logs (conexões são cíclicas)
            _logger.LogDebug("Nova conexão Modbus criada: {IpAddress}:{Port}", ipAddress, port);
            return master;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar conexão Modbus: {IpAddress}:{Port}", ipAddress, port);
            throw;
        }
    }

    private bool IsConexaoValida(string key)
    {
        if (!_tcpClients.TryGetValue(key, out var tcpClient))
        {
            _logger.LogDebug("Conexão não encontrada no dicionário: {Key}", key);
            return false;
        }

        try
        {
            // Verifica se o socket está conectado e não foi fechado
            if (tcpClient == null)
            {
                _logger.LogDebug("TcpClient é null para: {Key}", key);
                return false;
            }

            if (!tcpClient.Connected)
            {
                _logger.LogDebug("TcpClient não está conectado: {Key}", key);
                return false;
            }

            // Verifica se o socket ainda está válido
            var socket = tcpClient.Client;
            if (socket == null)
            {
                _logger.LogDebug("Socket é null para: {Key}", key);
                return false;
            }

            // Testa se o socket está realmente conectado
            // Poll com timeout 0 verifica o estado sem bloquear
            bool isConnected = !(socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0);
            
            if (!isConnected)
            {
                _logger.LogDebug("Socket não está realmente conectado: {Key}", key);
            }
            
            return isConnected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao verificar conexão: {Key}", key);
            return false;
        }
    }

    private void RemoverConexao(string ipAddress, int port)
    {
        var key = $"{ipAddress}:{port}";
        lock (_lockObject)
        {
            RemoverConexaoInterna(key);
        }
    }

    private void RemoverConexaoInterna(string key)
    {
        try
        {
            if (_connections.TryGetValue(key, out var master))
            {
                if (master is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _connections.Remove(key);
            }

            if (_tcpClients.TryGetValue(key, out var tcpClient))
            {
                try
                {
                    tcpClient?.Close();
                }
                catch { }
                _tcpClients.Remove(key);
            }

            _lastUsed.Remove(key);
            _logger.LogInformation("Conexão Modbus removida: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao remover conexão Modbus: {Key}", key);
        }
    }

    private async Task<T> ExecutarComRetryAsync<T>(Func<Task<T>> operacao, string ipAddress, int port, int maxRetries = 2)
    {
        Exception? lastException = null;

        for (int tentativa = 0; tentativa <= maxRetries; tentativa++)
        {
            try
            {
                return await operacao();
            }
            catch (Exception ex) when (IsErroTransiente(ex))
            {
                lastException = ex;
                _logger.LogWarning(ex, "Erro na tentativa {Tentativa}/{MaxRetries} para {IpAddress}:{Port}",
                    tentativa + 1, maxRetries + 1, ipAddress, port);

                // A conexão problemática já foi removida DENTRO de ExecutarNaConexaoAsync,
                // com o semáforo em mãos. Removê-la aqui de novo — fora do semáforo —
                // derrubaria uma conexão recém-recriada por outra transação.

                // Aguarda antes de tentar novamente (exceto na última tentativa)
                if (tentativa < maxRetries)
                {
                    await Task.Delay(500 * (tentativa + 1)); // Backoff exponencial
                }
            }
        }

        _logger.LogError(lastException, "Falha após {MaxRetries} tentativas para {IpAddress}:{Port}",
            maxRetries + 1, ipAddress, port);
        throw lastException!;
    }

    /// <summary>
    /// Erros de conexão/transporte que justificam derrubar a conexão e tentar novamente.
    /// Erros de programação (ArgumentException, NotSupportedException) e de protocolo (SlaveException)
    /// não são retentados. Task.Run/Wait podem embrulhar o erro real em AggregateException.
    /// </summary>
    private static bool IsErroTransiente(Exception ex)
    {
        if (ex is AggregateException ae)
            ex = ae.InnerException ?? ex;

        return ex is SocketException
            || ex is TimeoutException
            || ex is InvalidOperationException
            || ex is System.IO.IOException
            || ex is ObjectDisposedException;
    }

    /// <summary>
    /// Limpa apenas conexões inválidas (fechadas ou com erro).
    /// Conexões válidas são mantidas abertas permanentemente para evitar múltiplas conexões no CLP.
    /// </summary>
    private async Task LimparConexoesInvalidasAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5)); // Verifica a cada 5 minutos

                List<string> keys;
                lock (_lockObject)
                {
                    keys = _lastUsed.Keys.ToList();
                }

                foreach (var key in keys)
                {
                    // Só examina a conexão se conseguir o semáforo NA HORA: se está em
                    // uso, tem transação em andamento cuidando dela — não é papel da
                    // limpeza derrubar conexão debaixo de uma transação.
                    var semaforo = _locksPorConexao.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                    if (!semaforo.Wait(0))
                    {
                        continue;
                    }

                    try
                    {
                        lock (_lockObject)
                        {
                            if (!IsConexaoValida(key))
                            {
                                _logger.LogInformation("Removendo conexão inválida: {Key}", key);
                                RemoverConexaoInterna(key);
                            }
                        }
                    }
                    finally
                    {
                        semaforo.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar conexões inválidas");
            }
        }
    }

    public void FecharConexoes()
    {
        lock (_lockObject)
        {
            foreach (var connection in _connections.Values)
            {
                try
                {
                    if (connection is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao fechar conexão Modbus");
                }
            }
            _connections.Clear();
        }
    }
}
