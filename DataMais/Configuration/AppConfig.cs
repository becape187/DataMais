namespace DataMais.Configuration;

public class AppConfig
{
    public DatabaseConfig Database { get; set; } = new();
    public InfluxConfig Influx { get; set; } = new();
    public ModbusConfig Modbus { get; set; } = new();
    public SistemaConfig Sistema { get; set; } = new();
}

public class SistemaConfig
{
    public int? ClienteId { get; set; }
    public int? CilindroId { get; set; }
}

public class ModbusConfig
{
    public int TimeoutMs { get; set; } = 5000;
    public int RetryCount { get; set; } = 3;
    public int PoolingIntervalMs { get; set; } = 100;
}
