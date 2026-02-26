# Scripts de Importação Modbus

Scripts para importar configurações Modbus do arquivo JSON para o PostgreSQL.

## Arquivo de Origem

`modbus-slave-configuration.json` - Contém o mapeamento de registros Modbus.

## Opções de Importação

### 1. Script SQL (Recomendado para execução direta)

**Arquivo:** `import-modbus-config.sql`

**Como usar:**
```bash
# Conectar ao PostgreSQL
psql -h modec.automais.cloud -U postgres -d datamais

# Executar o script
\i Scripts/import-modbus-config.sql
```

Ou via linha de comando:
```bash
psql -h modec.automais.cloud -U postgres -d datamais -f Scripts/import-modbus-config.sql
```

### 2. Script C# (Recomendado para integração com a aplicação)

**Arquivo:** `ImportModbusConfig.cs`

**Como usar:**

1. Adicionar o arquivo ao projeto DataMais
2. Compilar e executar:

```bash
cd DataMais
dotnet run --project . -- ImportModbusConfig modbus-slave-configuration.json modec.automais.cloud 502 1
```

**Parâmetros:**
- `modbus-slave-configuration.json` - Caminho do arquivo JSON (opcional, padrão: `modbus-slave-configuration.json`)
- `modec.automais.cloud` - IP do servidor Modbus (opcional, padrão: `modec.automais.cloud`)
- `502` - Porta Modbus (opcional, padrão: `502`)
- `1` - Slave ID (opcional, padrão: `1`)

## Mapeamento de Tipos

O script mapeia automaticamente os tipos do JSON para funções Modbus:

| Tipo JSON | Função Modbus | Tipo Dado |
|-----------|---------------|------------|
| `coil` | `ReadCoils` | `Boolean` |
| `discrete_input` | `ReadInputs` | `Boolean` |
| `holding_register` | `ReadHoldingRegisters` | `UInt16` |
| `input_register` | `ReadInputRegisters` | `UInt16` |

## Configuração

- **IP:** `modec.automais.cloud`
- **Porta:** `502` (padrão Modbus)
- **Slave ID:** `1` (padrão)

## Verificação

Após a importação, verifique os registros:

```sql
SELECT COUNT(*) FROM "ModbusConfigs" WHERE "IpAddress" = 'modec.automais.cloud';
SELECT * FROM "ModbusConfigs" WHERE "IpAddress" = 'modec.automais.cloud' ORDER BY "OrdemLeitura";
```

## Notas

- O script SQL remove registros existentes do mesmo IP antes de inserir (comentado por padrão)
- O script C# pergunta antes de remover registros existentes
- A ordem de leitura é definida automaticamente baseada na ordem do JSON
