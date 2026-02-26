using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;
using DataMais.Configuration;

namespace DataMais.Scripts;

public class ImportModbusConfig
{
    public static async Task Main(string[] args)
    {
        // Carregar configuração do .env
        var configService = new ConfigService();
        var config = configService.LoadConfig();

        // Configurar DbContext
        var optionsBuilder = new DbContextOptionsBuilder<DataMaisDbContext>();
        optionsBuilder.UseNpgsql(
            $"Host={config.Database.Host};Port={config.Database.Port};Database={config.Database.Database};Username={config.Database.Username};Password={config.Database.Password}");

        using var context = new DataMaisDbContext(optionsBuilder.Options);

        // Ler arquivo JSON
        var jsonPath = args.Length > 0 ? args[0] : "modbus-slave-configuration.json";
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"❌ Arquivo não encontrado: {jsonPath}");
            return;
        }

        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        var jsonDoc = JsonDocument.Parse(jsonContent);

        var ipAddress = args.Length > 1 ? args[1] : "modec.automais.cloud";
        var port = args.Length > 2 ? int.Parse(args[2]) : 502;
        var slaveId = args.Length > 3 ? byte.Parse(args[3]) : (byte)1;

        Console.WriteLine($"📥 Importando configurações Modbus...");
        Console.WriteLine($"   IP: {ipAddress}");
        Console.WriteLine($"   Porta: {port}");
        Console.WriteLine($"   Slave ID: {slaveId}");

        // Limpar registros existentes do mesmo IP (opcional)
        var existing = await context.ModbusConfigs
            .Where(m => m.IpAddress == ipAddress)
            .ToListAsync();

        if (existing.Any())
        {
            Console.WriteLine($"⚠️  Encontrados {existing.Count} registros existentes para {ipAddress}");
            Console.WriteLine("   Deseja remover os registros existentes? (s/n)");
            var resposta = Console.ReadLine()?.ToLower();
            if (resposta == "s" || resposta == "sim")
            {
                context.ModbusConfigs.RemoveRange(existing);
                await context.SaveChangesAsync();
                Console.WriteLine($"✓ {existing.Count} registros removidos");
            }
        }

        var mappings = jsonDoc.RootElement.GetProperty("mapping").EnumerateArray();
        var ordemLeitura = 1;
        var novosRegistros = new List<ModbusConfig>();

        foreach (var mapping in mappings)
        {
            var address = mapping.GetProperty("address").GetUInt16();
            var datatype = mapping.GetProperty("datatype").GetString();
            var variable = mapping.GetProperty("variable").GetString();

            if (string.IsNullOrEmpty(datatype) || string.IsNullOrEmpty(variable))
                continue;

            // Mapear tipo de dado para função Modbus
            var funcaoModbus = datatype switch
            {
                "coil" => "ReadCoils",
                "discrete_input" => "ReadInputs",
                "holding_register" => "ReadHoldingRegisters",
                "input_register" => "ReadInputRegisters",
                _ => "ReadHoldingRegisters"
            };

            // Determinar tipo de dado
            var tipoDado = datatype switch
            {
                "coil" or "discrete_input" => "Boolean",
                "holding_register" or "input_register" => "UInt16",
                _ => "UInt16"
            };

            var registro = new ModbusConfig
            {
                Nome = variable,
                Descricao = $"Registro Modbus {variable} (Address: {address}, Type: {datatype})",
                IpAddress = ipAddress,
                Port = port,
                SlaveId = slaveId,
                FuncaoModbus = funcaoModbus,
                EnderecoRegistro = address,
                QuantidadeRegistros = 1,
                TipoDado = tipoDado,
                ByteOrder = "BigEndian",
                OrdemLeitura = ordemLeitura++,
                Ativo = true,
                DataCriacao = DateTime.UtcNow
            };

            novosRegistros.Add(registro);
        }

        // Inserir no banco
        await context.ModbusConfigs.AddRangeAsync(novosRegistros);
        await context.SaveChangesAsync();

        Console.WriteLine($"✅ {novosRegistros.Count} registros Modbus importados com sucesso!");
        Console.WriteLine($"\n📊 Resumo:");
        Console.WriteLine($"   - Coils: {novosRegistros.Count(r => r.FuncaoModbus == "ReadCoils")}");
        Console.WriteLine($"   - Discrete Inputs: {novosRegistros.Count(r => r.FuncaoModbus == "ReadInputs")}");
        Console.WriteLine($"   - Holding Registers: {novosRegistros.Count(r => r.FuncaoModbus == "ReadHoldingRegisters")}");
        Console.WriteLine($"   - Input Registers: {novosRegistros.Count(r => r.FuncaoModbus == "ReadInputRegisters")}");
    }
}
