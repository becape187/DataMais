-- Script para corrigir a estrutura do banco
-- Execute este script PRIMEIRO antes de atualizar_banco_modbus.sql
-- 
-- IMPORTANTE: Se você está em uma transação abortada, execute manualmente:
-- ROLLBACK;
-- 
-- Depois execute este script completo.

-- ============================================
-- PASSO 1: Remover constraint única antiga em Nome
-- ============================================

-- Remover o índice único antigo (se existir)
DROP INDEX IF EXISTS "IX_ModbusConfigs_Nome";

-- Criar índice não único
CREATE INDEX IF NOT EXISTS "IX_ModbusConfigs_Nome" ON "ModbusConfigs" ("Nome");

-- ============================================
-- PASSO 2: Criar constraint única correta em Nome + FuncaoModbus
-- ============================================

-- Remover índice composto antigo (se existir)
DROP INDEX IF EXISTS "IX_ModbusConfigs_Nome_FuncaoModbus";

-- Criar índice composto ÚNICO (PostgreSQL não suporta IF NOT EXISTS em UNIQUE INDEX)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_ModbusConfigs_Nome_FuncaoModbus' 
        AND tablename = 'ModbusConfigs'
    ) THEN
        CREATE UNIQUE INDEX "IX_ModbusConfigs_Nome_FuncaoModbus" 
        ON "ModbusConfigs" ("Nome", "FuncaoModbus");
    END IF;
END $$;

-- ============================================
-- PASSO 3: Verificar estrutura final
-- ============================================

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

-- ============================================
-- PASSO 4: Verificar se há duplicatas
-- ============================================

SELECT 
    "Nome",
    "FuncaoModbus",
    COUNT(*) as "Quantidade"
FROM "ModbusConfigs"
WHERE "IpAddress" = 'modec.automais.cloud'
GROUP BY "Nome", "FuncaoModbus"
HAVING COUNT(*) > 1;
