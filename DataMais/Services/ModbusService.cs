using NModbus;
using NModbus.Device;
using System.Net.Sockets;
using DataMais.Data;
using DataMais.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataMais.Services;

public class ModbusService
{
    private readonly DataMaisDbContext _context;
    private readonly ILogger<ModbusService> _logger;
    private readonly Dictionary<string, IModbusMaster> _connections = new();
    private readonly Dictionary<string, TcpClient> _tcpClients = new();
    private readonly Dictionary<string, DateTime> _lastUsed = new();
    private readonly object _lockObject = new();
    private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _operationTimeout = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _maxIdleTime = TimeSpan.FromMinutes(5);

    public ModbusService(DataMaisDbContext context, ILogger<ModbusService> logger)
    {
        _context = context;
        _logger = logger;
        
        // Inicia limpeza periódica de conexões inativas
        _ = Task.Run(async () => await LimparConexoesInativasAsync());
    }

    /// <summary>
    /// Lê todos os registros Modbus ativos, agrupando por IP para otimizar conexões TCP
    /// </summary>
    public async Task<Dictionary<int, object>> LerTodosRegistrosAsync()
    {
        var resultados = new Dictionary<int, object>();

        // Busca todos os registros ativos ordenados por IP e ordem de leitura
        var registros = await _context.ModbusConfigs
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

            try
            {
                var master = await ObterOuCriarConexaoAsync(ipAddress, port);

                foreach (var registro in grupo)
                {
                    try
                    {
                        object valor = await LerRegistroAsync(master, registro);
                        resultados[registro.Id] = valor;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erro ao ler registro {RegistroId} ({Nome})", registro.Id, registro.Nome);
                        resultados[registro.Id] = null;
                        // Marca conexão para remoção (será removida na próxima verificação)
                        // Não remove aqui para evitar problemas de concorrência
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao conectar em {IpAddress}:{Port}", ipAddress, port);
                foreach (var registro in grupo)
                {
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
        var registro = await _context.ModbusConfigs.FindAsync(registroId);
        if (registro == null || !registro.Ativo)
            return null;

        return await ExecutarComRetryAsync(async () =>
        {
            var master = await ObterOuCriarConexaoAsync(registro.IpAddress, registro.Port);
            return await LerRegistroAsync(master, registro);
        }, registro.IpAddress, registro.Port);
    }

    /// <summary>
    /// Escreve um valor em um registro Modbus
    /// </summary>
    public async Task<bool> EscreverRegistroAsync(int registroId, object valor)
    {
        var registro = await _context.ModbusConfigs.FindAsync(registroId);
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

        return await ExecutarComRetryAsync(async () =>
        {
            var master = await ObterOuCriarConexaoAsync(registro.IpAddress, registro.Port);

            switch (registro.FuncaoModbus)
            {
                case "WriteSingleRegister":
                    if (valor is ushort ushortValue)
                    {
                        master.WriteSingleRegister(registro.SlaveId, registro.EnderecoRegistro, ushortValue);
                    }
                    else
                    {
                        _logger.LogWarning("Valor inválido para WriteSingleRegister: esperado ushort, recebido {Tipo}", valor.GetType().Name);
                        return false;
                    }
                    break;

                case "WriteSingleCoil":
                    if (valor is bool boolValue)
                    {
                        master.WriteSingleCoil(registro.SlaveId, registro.EnderecoRegistro, boolValue);
                    }
                    else
                    {
                        _logger.LogWarning("Valor inválido para WriteSingleCoil: esperado bool, recebido {Tipo}", valor.GetType().Name);
                        return false;
                    }
                    break;

                default:
                    _logger.LogWarning("Função Modbus {Funcao} não suporta escrita", registro.FuncaoModbus);
                    return false;
            }

            return true;
        }, registro.IpAddress, registro.Port);
    }

    private async Task<object> LerRegistroAsync(IModbusMaster master, ModbusConfig registro)
    {
        switch (registro.FuncaoModbus)
        {
            case "ReadCoils":
                var coils = master.ReadCoils(registro.SlaveId, registro.EnderecoRegistro, registro.QuantidadeRegistros);
                return coils != null && coils.Length > 0 ? coils[0] : false;

            case "ReadInputs":
                var inputs = master.ReadInputs(registro.SlaveId, registro.EnderecoRegistro, registro.QuantidadeRegistros);
                return inputs != null && inputs.Length > 0 ? inputs[0] : false;

            case "ReadHoldingRegisters":
                var holdingRegisters = master.ReadHoldingRegisters(registro.SlaveId, registro.EnderecoRegistro, registro.QuantidadeRegistros);
                return ConverterValor(holdingRegisters, registro);

            case "ReadInputRegisters":
                var inputRegisters = master.ReadInputRegisters(registro.SlaveId, registro.EnderecoRegistro, registro.QuantidadeRegistros);
                return ConverterValor(inputRegisters, registro);

            default:
                throw new NotSupportedException($"Função Modbus {registro.FuncaoModbus} não suportada");
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
            if (!connectTask.Wait(_connectionTimeout))
            {
                tcpClient.Close();
                throw new TimeoutException($"Timeout ao conectar em {ipAddress}:{port}");
            }

            if (!tcpClient.Connected)
            {
                tcpClient.Close();
                throw new SocketException((int)SocketError.NotConnected);
            }

            var factory = new ModbusFactory();
            var master = factory.CreateMaster(tcpClient);

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
            return false;

        try
        {
            // Verifica se o socket está conectado e não foi fechado
            if (tcpClient == null || !tcpClient.Connected)
                return false;

            // Verifica se o socket ainda está válido
            var socket = tcpClient.Client;
            if (socket == null)
                return false;

            // Testa se o socket está realmente conectado
            // Poll com timeout 0 verifica o estado sem bloquear
            bool isConnected = !(socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0);
            
            return isConnected;
        }
        catch
        {
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
            catch (Exception ex) when (ex is SocketException || ex is TimeoutException || ex is InvalidOperationException)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Erro na tentativa {Tentativa}/{MaxRetries} para {IpAddress}:{Port}", 
                    tentativa + 1, maxRetries + 1, ipAddress, port);

                // Remove conexão problemática
                RemoverConexao(ipAddress, port);

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

    private async Task LimparConexoesInativasAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1)); // Verifica a cada minuto

                lock (_lockObject)
                {
                    var keysToRemove = new List<string>();

                    foreach (var kvp in _lastUsed.ToList())
                    {
                        var key = kvp.Key;
                        var lastUsed = kvp.Value;

                        // Remove conexões inativas por muito tempo
                        if (DateTime.UtcNow - lastUsed > _maxIdleTime)
                        {
                            keysToRemove.Add(key);
                            continue;
                        }

                        // Remove conexões inválidas
                        if (!IsConexaoValida(key))
                        {
                            keysToRemove.Add(key);
                        }
                    }

                    foreach (var key in keysToRemove)
                    {
                        _logger.LogInformation("Removendo conexão inativa: {Key}", key);
                        RemoverConexaoInterna(key);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar conexões inativas");
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
