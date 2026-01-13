using NModbus;
using NModbus.Device;
using System.Net;
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
    private readonly object _lockObject = new();

    public ModbusService(DataMaisDbContext context, ILogger<ModbusService> logger)
    {
        _context = context;
        _logger = logger;
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
                var master = ObterOuCriarConexao(ipAddress, port);

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

        var master = ObterOuCriarConexao(registro.IpAddress, registro.Port);
        return await LerRegistroAsync(master, registro);
    }

    /// <summary>
    /// Escreve um valor em um registro Modbus
    /// </summary>
    public async Task<bool> EscreverRegistroAsync(int registroId, object valor)
    {
        var registro = await _context.ModbusConfigs.FindAsync(registroId);
        if (registro == null || !registro.Ativo)
            return false;

        var master = ObterOuCriarConexao(registro.IpAddress, registro.Port);

        try
        {
            switch (registro.FuncaoModbus)
            {
                case "WriteSingleRegister":
                    if (valor is ushort ushortValue)
                    {
                        master.WriteSingleRegister(registro.SlaveId, registro.EnderecoRegistro, ushortValue);
                    }
                    break;

                case "WriteSingleCoil":
                    if (valor is bool boolValue)
                    {
                        master.WriteSingleCoil(registro.SlaveId, registro.EnderecoRegistro, boolValue);
                    }
                    break;

                default:
                    _logger.LogWarning("Função Modbus {Funcao} não suporta escrita", registro.FuncaoModbus);
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao escrever registro {RegistroId}", registroId);
            return false;
        }
    }

    private async Task<object> LerRegistroAsync(IModbusMaster master, ModbusConfig registro)
    {
        ushort[] valores;

        switch (registro.FuncaoModbus)
        {
            case "ReadHoldingRegisters":
                valores = master.ReadHoldingRegisters(registro.SlaveId, registro.EnderecoRegistro, registro.QuantidadeRegistros);
                break;

            case "ReadInputRegisters":
                valores = master.ReadInputRegisters(registro.SlaveId, registro.EnderecoRegistro, registro.QuantidadeRegistros);
                break;

            default:
                throw new NotSupportedException($"Função Modbus {registro.FuncaoModbus} não suportada");
        }

        // Converte os valores conforme o tipo de dado
        return ConverterValor(valores, registro);
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
                        valorConvertido = BitConverter.ToSingle(bytes, 0);
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

    private IModbusMaster ObterOuCriarConexao(string ipAddress, int port)
    {
        var key = $"{ipAddress}:{port}";

        lock (_lockObject)
        {
            if (_connections.TryGetValue(key, out var existingMaster))
            {
                return existingMaster;
            }

            var tcpClient = new TcpClient(ipAddress, port);
            var factory = new ModbusFactory();
            var master = factory.CreateMaster(tcpClient);

            _connections[key] = master;
            _logger.LogInformation("Nova conexão Modbus criada: {IpAddress}:{Port}", ipAddress, port);

            return master;
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
