# Instruções para Atualização do Banco de Dados Modbus

## ⚠️ ORDEM DE EXECUÇÃO OBRIGATÓRIA

Execute os scripts nesta ordem exata:

### 1. Se houver transação abortada, faça rollback primeiro:
```sql
ROLLBACK;
```

### 2. Execute o script de correção de estrutura:
```sql
-- Execute: corrigir_constraint_modbus_v2.sql
-- (ou corrigir_constraint_modbus.sql se preferir)
```
Este script:
- Remove a constraint única antiga em `Nome` (se existir)
- Cria a constraint correta em `Nome + FuncaoModbus`
- Verifica se há duplicatas que precisam ser resolvidas

**Recomendado:** Use `corrigir_constraint_modbus_v2.sql` que é mais simples e direto.

### 3. Execute a migration (se ainda não executou):
```bash
dotnet ef database update
```

### 4. Execute o script de atualização de dados:
```sql
-- Execute: atualizar_banco_modbus.sql
```
Este script atualiza e insere os registros conforme o arquivo JSON.

## 🔍 Verificação

Após executar os scripts, verifique se está tudo correto:

```sql
-- Verificar constraints
SELECT 
    indexname,
    indexdef,
    CASE 
        WHEN indexdef LIKE '%UNIQUE%' THEN 'SIM'
        ELSE 'NÃO'
    END as "É Único"
FROM pg_indexes 
WHERE tablename = 'ModbusConfigs' 
AND (indexname LIKE '%Nome%' OR indexname LIKE '%FuncaoModbus%')
ORDER BY indexname;

-- Verificar se MOTOR_BOMBA existe em ambas as funções
SELECT "Nome", "FuncaoModbus", "EnderecoRegistro"
FROM "ModbusConfigs"
WHERE "Nome" = 'MOTOR_BOMBA'
ORDER BY "FuncaoModbus";
```

## ❌ Problemas Comuns

### Erro: "duplicate key value violates unique constraint IX_ModbusConfigs_Nome"
**Solução:** Execute o script `corrigir_constraint_modbus.sql` primeiro.

### Erro: "current transaction is aborted"
**Solução:** Execute `ROLLBACK;` e depois execute os scripts novamente na ordem correta.

### MOTOR_BOMBA não aparece em ambas as funções
**Solução:** Verifique se a constraint única antiga foi removida executando o script de correção.
