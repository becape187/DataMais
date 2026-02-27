-- Script para corrigir a estrutura do banco ANTES de executar atualizar_banco_modbus.sql
-- Execute este script PRIMEIRO, mesmo se já executou a migration
-- Este script garante que a constraint única antiga seja removida

-- IMPORTANTE: Se você recebeu erro de transação abortada, este script faz ROLLBACK primeiro

-- 1. Fazer ROLLBACK de qualquer transação abortada
DO $$
BEGIN
    -- Tentar fazer rollback (pode dar erro se não houver transação, mas não importa)
    BEGIN
        ROLLBACK;
    EXCEPTION WHEN OTHERS THEN
        -- Não há transação ativa, tudo bem
        NULL;
    END;
END $$;

-- 2. Verificar e remover constraint única antiga em Nome
DO $$
BEGIN
    -- Verificar se existe índice único antigo em Nome
    IF EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_ModbusConfigs_Nome' 
        AND tablename = 'ModbusConfigs'
        AND indexdef LIKE '%UNIQUE%'
    ) THEN
        RAISE NOTICE 'Removendo índice único antigo IX_ModbusConfigs_Nome...';
        DROP INDEX IF EXISTS "IX_ModbusConfigs_Nome";
        
        -- Recriar como índice não único
        CREATE INDEX IF NOT EXISTS "IX_ModbusConfigs_Nome" ON "ModbusConfigs" ("Nome");
        RAISE NOTICE 'Índice IX_ModbusConfigs_Nome recriado como não único.';
    ELSE
        RAISE NOTICE 'Índice único antigo não encontrado ou já foi removido.';
        
        -- Garantir que o índice existe (mesmo que não único)
        IF NOT EXISTS (
            SELECT 1 FROM pg_indexes 
            WHERE indexname = 'IX_ModbusConfigs_Nome' 
            AND tablename = 'ModbusConfigs'
        ) THEN
            CREATE INDEX IF NOT EXISTS "IX_ModbusConfigs_Nome" ON "ModbusConfigs" ("Nome");
            RAISE NOTICE 'Índice IX_ModbusConfigs_Nome criado como não único.';
        END IF;
    END IF;
END $$;

-- 3. Garantir que o índice composto único existe
DO $$
BEGIN
    -- Remover se existir como não único
    IF EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_ModbusConfigs_Nome_FuncaoModbus' 
        AND tablename = 'ModbusConfigs'
        AND indexdef NOT LIKE '%UNIQUE%'
    ) THEN
        RAISE NOTICE 'Removendo índice composto não único...';
        DROP INDEX IF EXISTS "IX_ModbusConfigs_Nome_FuncaoModbus";
    END IF;
    
    -- Criar como único se não existir
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_ModbusConfigs_Nome_FuncaoModbus' 
        AND tablename = 'ModbusConfigs'
    ) THEN
        RAISE NOTICE 'Criando índice composto único IX_ModbusConfigs_Nome_FuncaoModbus...';
        CREATE UNIQUE INDEX "IX_ModbusConfigs_Nome_FuncaoModbus" 
        ON "ModbusConfigs" ("Nome", "FuncaoModbus");
        RAISE NOTICE 'Índice composto único criado com sucesso.';
    ELSE
        -- Verificar se é único
        IF NOT EXISTS (
            SELECT 1 FROM pg_indexes 
            WHERE indexname = 'IX_ModbusConfigs_Nome_FuncaoModbus' 
            AND tablename = 'ModbusConfigs'
            AND indexdef LIKE '%UNIQUE%'
        ) THEN
            RAISE NOTICE 'Recriando índice composto como único...';
            DROP INDEX IF EXISTS "IX_ModbusConfigs_Nome_FuncaoModbus";
            CREATE UNIQUE INDEX "IX_ModbusConfigs_Nome_FuncaoModbus" 
            ON "ModbusConfigs" ("Nome", "FuncaoModbus");
            RAISE NOTICE 'Índice composto único recriado com sucesso.';
        ELSE
            RAISE NOTICE 'Índice composto único já existe corretamente.';
        END IF;
    END IF;
END $$;

-- 4. Verificar estrutura final
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

-- 5. Verificar se há duplicatas que precisam ser resolvidas
SELECT 
    "Nome",
    "FuncaoModbus",
    COUNT(*) as "Quantidade"
FROM "ModbusConfigs"
WHERE "IpAddress" = 'modec.automais.cloud'
GROUP BY "Nome", "FuncaoModbus"
HAVING COUNT(*) > 1;
