namespace DataMais.Configuration;

public class AppConfig
{
    public DatabaseConfig Database { get; set; } = new();
    public InfluxConfig Influx { get; set; } = new();
    public ModbusConfig Modbus { get; set; } = new();
}

public class ModbusConfig
{
    public int TimeoutMs { get; set; } = 5000;
    public int RetryCount { get; set; } = 3;
    public int PoolingIntervalMs { get; set; } = 100;
}
