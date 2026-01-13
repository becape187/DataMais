using DotNetEnv;
using DataMais.Configuration;
using System.Text.Json;

namespace DataMais.Services;

public class ConfigService
{
    private readonly string _envFilePath;
    private AppConfig _config;

    public ConfigService(string? envFilePath = null)
    {
        // Tenta encontrar o arquivo .env em ordem de prioridade:
        // 1. Caminho fornecido explicitamente
        // 2. Variável de ambiente DATAMAIS_ENV_FILE
        // 3. /home/becape/datamais.env (caminho padrão no servidor)
        // 4. .env no diretório da aplicação
        _envFilePath = envFilePath 
            ?? Environment.GetEnvironmentVariable("DATAMAIS_ENV_FILE")
            ?? "/home/becape/datamais.env"
            ?? Path.Combine(AppContext.BaseDirectory, ".env");
        _config = LoadConfig();
    }

    public AppConfig GetConfig() => _config;

    public void ReloadConfig()
    {
        _config = LoadConfig();
    }

    private AppConfig LoadConfig()
    {
        // Carrega o arquivo .env se existir
        if (File.Exists(_envFilePath))
        {
            Env.Load(_envFilePath);
        }

        return new AppConfig
        {
            Database = new DatabaseConfig
            {
                Host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost",
                Port = int.Parse(Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432"),
                Database = Environment.GetEnvironmentVariable("POSTGRES_DATABASE") ?? "datamais",
                Username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres",
                Password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? ""
            },
            Influx = new InfluxConfig
            {
                Url = Environment.GetEnvironmentVariable("INFLUX_URL") ?? "http://localhost:8086",
                Token = Environment.GetEnvironmentVariable("INFLUX_TOKEN") ?? "",
                Organization = Environment.GetEnvironmentVariable("INFLUX_ORG") ?? "datamais",
                Bucket = Environment.GetEnvironmentVariable("INFLUX_BUCKET") ?? "sensores"
            },
            Modbus = new ModbusConfig
            {
                TimeoutMs = int.Parse(Environment.GetEnvironmentVariable("MODBUS_TIMEOUT_MS") ?? "5000"),
                RetryCount = int.Parse(Environment.GetEnvironmentVariable("MODBUS_RETRY_COUNT") ?? "3"),
                PoolingIntervalMs = int.Parse(Environment.GetEnvironmentVariable("MODBUS_POOLING_INTERVAL_MS") ?? "100")
            }
        };
    }

    public void SaveConfig(AppConfig config)
    {
        var envContent = new List<string>
        {
            "# PostgreSQL Configuration",
            $"POSTGRES_HOST={config.Database.Host}",
            $"POSTGRES_PORT={config.Database.Port}",
            $"POSTGRES_DATABASE={config.Database.Database}",
            $"POSTGRES_USER={config.Database.Username}",
            $"POSTGRES_PASSWORD={config.Database.Password}",
            "",
            "# InfluxDB Configuration",
            $"INFLUX_URL={config.Influx.Url}",
            $"INFLUX_TOKEN={config.Influx.Token}",
            $"INFLUX_ORG={config.Influx.Organization}",
            $"INFLUX_BUCKET={config.Influx.Bucket}",
            "",
            "# Modbus Configuration",
            $"MODBUS_TIMEOUT_MS={config.Modbus.TimeoutMs}",
            $"MODBUS_RETRY_COUNT={config.Modbus.RetryCount}",
            $"MODBUS_POOLING_INTERVAL_MS={config.Modbus.PoolingIntervalMs}"
        };

        File.WriteAllLines(_envFilePath, envContent);
        _config = config;
    }

    public string GetEnvFilePath() => _envFilePath;
}
