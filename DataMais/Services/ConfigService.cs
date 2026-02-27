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
            ?? (File.Exists("/home/becape/datamais.env") ? "/home/becape/datamais.env" : null)
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
        // Primeiro, verifica se as variáveis já estão nas variáveis de ambiente
        // (carregadas pelo systemd via EnvironmentFile)
        var passwordFromEnv = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        
        // Se não estiver nas variáveis de ambiente, tenta carregar do arquivo
        if (string.IsNullOrWhiteSpace(passwordFromEnv))
        {
            // Carrega o arquivo .env se existir
            if (File.Exists(_envFilePath))
            {
                Console.WriteLine($"📄 Carregando variáveis do arquivo: {_envFilePath}");
                Env.Load(_envFilePath);
            }
            else
            {
                // Log warning se o arquivo não existir
                Console.WriteLine($"⚠️ Arquivo .env não encontrado em: {_envFilePath}");
            }
        }
        else
        {
            Console.WriteLine($"✓ Variáveis de ambiente já carregadas pelo systemd (EnvironmentFile)");
        }

        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        
        // Validação: se a senha estiver vazia, tenta carregar novamente ou loga aviso
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine($"❌ POSTGRES_PASSWORD não está definida ou está vazia!");
            Console.WriteLine($"   Arquivo .env esperado: {_envFilePath}");
            Console.WriteLine($"   Arquivo existe: {File.Exists(_envFilePath)}");
            Console.WriteLine($"   Variável de ambiente POSTGRES_PASSWORD: {(passwordFromEnv != null ? "definida mas vazia" : "não definida")}");
            
            if (File.Exists(_envFilePath))
            {
                Console.WriteLine($"   Conteúdo do arquivo (primeiras 10 linhas):");
                try
                {
                    var lines = File.ReadAllLines(_envFilePath).Take(10);
                    foreach (var line in lines)
                    {
                        // Não mostra a senha completa, apenas indica se existe
                        if (line.Contains("POSTGRES_PASSWORD", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                var hasValue = !string.IsNullOrWhiteSpace(parts[1]);
                                var passwordLength = Math.Min(parts[1]?.Length ?? 0, 10);
                                var maskedPassword = new string('*', passwordLength);
                                Console.WriteLine($"   {parts[0]}={maskedPassword} (tem valor: {hasValue})");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"   {line}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   Erro ao ler arquivo: {ex.Message}");
                }
            }
        }
        else
        {
            Console.WriteLine($"✓ POSTGRES_PASSWORD carregada com sucesso ({password.Length} caracteres)");
        }

        return new AppConfig
        {
            Database = new DatabaseConfig
            {
                Host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost",
                Port = int.Parse(Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432"),
                Database = Environment.GetEnvironmentVariable("POSTGRES_DATABASE") ?? "datamais",
                Username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres",
                Password = password ?? ""
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
            },
            Sistema = new SistemaConfig
            {
                ClienteId = ParseIntOrNull(Environment.GetEnvironmentVariable("SISTEMA_CLIENTE_ID")),
                CilindroId = ParseIntOrNull(Environment.GetEnvironmentVariable("SISTEMA_CILINDRO_ID"))
            }
        };
    }

    private int? ParseIntOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        
        if (int.TryParse(value, out int result))
            return result;
        
        return null;
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
            $"MODBUS_POOLING_INTERVAL_MS={config.Modbus.PoolingIntervalMs}",
            "",
            "# Sistema Configuration",
            config.Sistema.ClienteId.HasValue ? $"SISTEMA_CLIENTE_ID={config.Sistema.ClienteId.Value}" : "# SISTEMA_CLIENTE_ID=",
            config.Sistema.CilindroId.HasValue ? $"SISTEMA_CILINDRO_ID={config.Sistema.CilindroId.Value}" : "# SISTEMA_CILINDRO_ID="
        };

        File.WriteAllLines(_envFilePath, envContent);
        _config = config;
    }

    public string GetEnvFilePath() => _envFilePath;
}
