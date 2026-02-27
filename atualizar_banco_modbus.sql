-- Script SQL para adequar o banco de dados ao arquivo modbus-slave-configuration(1).json
-- Data: 2026-02-26
-- IP: modec.automais.cloud
-- Porta: 502
-- SlaveId: 1

-- ============================================
-- INSTRUÇÕES IMPORTANTES - LEIA ANTES DE EXECUTAR!
-- ============================================
-- 1. Se você recebeu erro de transação abortada, execute primeiro:
--    ROLLBACK;
--
-- 2. Execute PRIMEIRO o script: corrigir_constraint_modbus.sql
--    Este script remove a constraint única antiga e cria a correta
--
-- 3. Execute a migration (se ainda não executou):
--    dotnet ef database update
--
-- 4. A constraint correta é em (Nome + FuncaoModbus), permitindo:
--    - Mesmo nome com funções diferentes (ex: MOTOR_BOMBA como ReadCoils E ReadInputs)
--    - Mas NÃO permite duplicatas da mesma combinação Nome + FuncaoModbus

BEGIN;

-- ============================================
-- ATUALIZAR REGISTROS EXISTENTES
-- ============================================

-- Primeiro, criar/atualizar MOTOR_BOMBA como ReadCoils (address 5)
-- Se já existe como ReadCoils, apenas atualiza o endereço
-- Se existe como ReadInputs, cria um novo registro ReadCoils
INSERT INTO "ModbusConfigs" ("Nome", "Descricao", "IpAddress", "Port", "SlaveId", "FuncaoModbus", "EnderecoRegistro", "QuantidadeRegistros", "TipoDado", "ByteOrder", "OrdemLeitura", "Ativo", "DataCriacao")
SELECT 'MOTOR_BOMBA', 'Motor Bomba', 'modec.automais.cloud', 502, 1, 'ReadCoils', 5, 1, 'Boolean', 'BigEndian', 6, true, NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM "ModbusConfigs" 
    WHERE "Nome" = 'MOTOR_BOMBA' 
    AND "FuncaoModbus" = 'ReadCoils'
    AND "IpAddress" = 'modec.automais.cloud'
);

-- Atualizar MOTOR_BOMBA ReadCoils se já existir
UPDATE "ModbusConfigs"
SET 
    "EnderecoRegistro" = 5,
    "TipoDado" = 'Boolean',
    "DataAtualizacao" = NOW()
WHERE "Nome" = 'MOTOR_BOMBA' 
AND "FuncaoModbus" = 'ReadCoils' 
AND "IpAddress" = 'modec.automais.cloud';

-- Atualizar registros Coils existentes (garantir endereços corretos)
UPDATE "ModbusConfigs"
SET 
    "EnderecoRegistro" = CASE "Nome"
        WHEN 'BT_EMERGENCIA' THEN 0
        WHEN 'SL_SOLENOIDE_A' THEN 1
        WHEN 'SL_SOLENOIDE_B' THEN 2
        WHEN 'ST_FILTRO_OLEO' THEN 3
        WHEN 'ST_NIVEL_OLEO' THEN 4
        WHEN 'MOTOR_BOMBA' THEN 5
        WHEN 'RADIADOR' THEN 6
        WHEN 'SOLENOIDE_2' THEN 7
        WHEN 'SOLENOIDE_4' THEN 8
        WHEN 'BOTAO_DESLIGA_MOTOR' THEN 12
        WHEN 'BOTAO_LIGA_MOTOR' THEN 13
        WHEN 'BOTAO_DESLIGA_RADIADOR' THEN 14
        WHEN 'BOTAO_LIGA_RADIADOR' THEN 15
        WHEN 'BOTAO_INICIA' THEN 16
        WHEN 'BOTAO_PARA' THEN 17
        WHEN 'BOTAO_AVANCA_IHM' THEN 18
        WHEN 'BOTAO_RECUA_IHM' THEN 19
    END,
    "DataAtualizacao" = NOW()
WHERE "Nome" IN (
    'BT_EMERGENCIA', 'SL_SOLENOIDE_A', 'SL_SOLENOIDE_B', 'ST_FILTRO_OLEO', 'ST_NIVEL_OLEO',
    'MOTOR_BOMBA', 'RADIADOR', 'SOLENOIDE_2', 'SOLENOIDE_4',
    'BOTAO_DESLIGA_MOTOR', 'BOTAO_LIGA_MOTOR', 'BOTAO_DESLIGA_RADIADOR', 'BOTAO_LIGA_RADIADOR',
    'BOTAO_INICIA', 'BOTAO_PARA', 'BOTAO_AVANCA_IHM', 'BOTAO_RECUA_IHM'
)
AND "FuncaoModbus" = 'ReadCoils'
AND "IpAddress" = 'modec.automais.cloud';

-- Atualizar registros Discrete Inputs existentes
UPDATE "ModbusConfigs"
SET 
    "EnderecoRegistro" = CASE "Nome"
        WHEN 'ALARME_FILTRO_OLEO' THEN 16
        WHEN 'ALARME_NIVEL_OLEO' THEN 17
        WHEN 'REGISTRO_RODANDO' THEN 18
        WHEN 'AUX_AVANCA' THEN 19
        WHEN 'AUX_RECUA' THEN 20
        WHEN 'MOTOR_BOMBA' THEN 21
    END,
    "DataAtualizacao" = NOW()
WHERE "Nome" IN (
    'ALARME_FILTRO_OLEO', 'ALARME_NIVEL_OLEO', 'REGISTRO_RODANDO',
    'AUX_AVANCA', 'AUX_RECUA'
)
AND "FuncaoModbus" = 'ReadInputs'
AND "IpAddress" = 'modec.automais.cloud';

-- Criar/atualizar MOTOR_BOMBA como ReadInputs (address 21)
INSERT INTO "ModbusConfigs" ("Nome", "Descricao", "IpAddress", "Port", "SlaveId", "FuncaoModbus", "EnderecoRegistro", "QuantidadeRegistros", "TipoDado", "ByteOrder", "OrdemLeitura", "Ativo", "DataCriacao")
SELECT 'MOTOR_BOMBA', 'Motor Bomba (Discrete Input)', 'modec.automais.cloud', 502, 1, 'ReadInputs', 21, 1, 'Boolean', 'BigEndian', 43, true, NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM "ModbusConfigs" 
    WHERE "Nome" = 'MOTOR_BOMBA' 
    AND "FuncaoModbus" = 'ReadInputs'
    AND "IpAddress" = 'modec.automais.cloud'
);

-- Atualizar MOTOR_BOMBA ReadInputs se já existir
UPDATE "ModbusConfigs"
SET 
    "EnderecoRegistro" = 21,
    "TipoDado" = 'Boolean',
    "DataAtualizacao" = NOW()
WHERE "Nome" = 'MOTOR_BOMBA' 
AND "FuncaoModbus" = 'ReadInputs' 
AND "IpAddress" = 'modec.automais.cloud';

-- Atualizar registros Holding Registers existentes
UPDATE "ModbusConfigs"
SET 
    "EnderecoRegistro" = CASE "Nome"
        WHEN 'LIMITE_A' THEN 0
        WHEN 'LIMITE_B' THEN 1
    END,
    "DataAtualizacao" = NOW()
WHERE "Nome" IN ('LIMITE_A', 'LIMITE_B')
AND "FuncaoModbus" = 'ReadHoldingRegisters'
AND "IpAddress" = 'modec.automais.cloud';

-- Atualizar registros Input Registers existentes
UPDATE "ModbusConfigs"
SET 
    "EnderecoRegistro" = CASE "Nome"
        WHEN 'PRESSAO_GERAL' THEN 9
        WHEN 'PRESSAO_A' THEN 10
        WHEN 'PRESSAO_B' THEN 11
    END,
    "DataAtualizacao" = NOW()
WHERE "Nome" IN ('PRESSAO_GERAL', 'PRESSAO_A', 'PRESSAO_B')
AND "FuncaoModbus" = 'ReadInputRegisters'
AND "IpAddress" = 'modec.automais.cloud';

-- ============================================
-- INSERIR NOVOS REGISTROS
-- ============================================

-- Inserir novos Holding Registers
INSERT INTO "ModbusConfigs" ("Nome", "Descricao", "IpAddress", "Port", "SlaveId", "FuncaoModbus", "EnderecoRegistro", "QuantidadeRegistros", "TipoDado", "ByteOrder", "OrdemLeitura", "Ativo", "DataCriacao")
SELECT * FROM (VALUES
    ('INPUT_MAX_1', 'Input Max 1', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 2, 1, 'UInt16', 'BigEndian', 28, true, NOW()),
    ('INPUT_MIN_1', 'Input Min 1', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 3, 1, 'UInt16', 'BigEndian', 29, true, NOW()),
    ('INPUT_MAX_2', 'Input Max 2', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 4, 1, 'UInt16', 'BigEndian', 30, true, NOW()),
    ('INPUT_MIN_2', 'Input Min 2', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 5, 1, 'UInt16', 'BigEndian', 31, true, NOW()),
    ('INPUT_MAX', 'Input Max', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 6, 1, 'UInt16', 'BigEndian', 32, true, NOW()),
    ('INPUT_MIN', 'Input Min', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 7, 1, 'UInt16', 'BigEndian', 33, true, NOW()),
    ('OUTPUT_MAX', 'Output Max', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 8, 1, 'UInt16', 'BigEndian', 34, true, NOW()),
    ('OUTPUT_MIN', 'Output Min', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 9, 1, 'UInt16', 'BigEndian', 35, true, NOW()),
    ('OUTPUT_MAX_1', 'Output Max 1', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 10, 1, 'UInt16', 'BigEndian', 36, true, NOW()),
    ('OUTPUT_MIN_1', 'Output Min 1', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 11, 1, 'UInt16', 'BigEndian', 37, true, NOW()),
    ('OUTPUT_MAX_2', 'Output Max 2', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 12, 1, 'UInt16', 'BigEndian', 38, true, NOW()),
    ('OUTPUT_MIN_2', 'Output Min 2', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 13, 1, 'UInt16', 'BigEndian', 39, true, NOW())
) AS v("Nome", "Descricao", "IpAddress", "Port", "SlaveId", "FuncaoModbus", "EnderecoRegistro", "QuantidadeRegistros", "TipoDado", "ByteOrder", "OrdemLeitura", "Ativo", "DataCriacao")
WHERE NOT EXISTS (
    SELECT 1 FROM "ModbusConfigs" 
    WHERE "ModbusConfigs"."Nome" = v."Nome" 
    AND "ModbusConfigs"."FuncaoModbus" = v."FuncaoModbus"
    AND "ModbusConfigs"."IpAddress" = v."IpAddress"
);

-- Inserir novos Input Registers
INSERT INTO "ModbusConfigs" ("Nome", "Descricao", "IpAddress", "Port", "SlaveId", "FuncaoModbus", "EnderecoRegistro", "QuantidadeRegistros", "TipoDado", "ByteOrder", "OrdemLeitura", "Ativo", "DataCriacao")
SELECT * FROM (VALUES
    ('PRESSAO_A_CONV', 'Pressão A Convertida', 'modec.automais.cloud', 502, 1, 'ReadInputRegisters', 12, 1, 'UInt16', 'BigEndian', 40, true, NOW()),
    ('PRESSAO_B_CONV', 'Pressão B Convertida', 'modec.automais.cloud', 502, 1, 'ReadInputRegisters', 13, 1, 'UInt16', 'BigEndian', 41, true, NOW()),
    ('PRESSAO_GERAL_CONV', 'Pressão Geral Convertida', 'modec.automais.cloud', 502, 1, 'ReadInputRegisters', 14, 1, 'UInt16', 'BigEndian', 42, true, NOW())
) AS v("Nome", "Descricao", "IpAddress", "Port", "SlaveId", "FuncaoModbus", "EnderecoRegistro", "QuantidadeRegistros", "TipoDado", "ByteOrder", "OrdemLeitura", "Ativo", "DataCriacao")
WHERE NOT EXISTS (
    SELECT 1 FROM "ModbusConfigs" 
    WHERE "ModbusConfigs"."Nome" = v."Nome" 
    AND "ModbusConfigs"."FuncaoModbus" = v."FuncaoModbus"
    AND "ModbusConfigs"."IpAddress" = v."IpAddress"
);

COMMIT;

-- ============================================
-- VERIFICAÇÃO FINAL
-- ============================================

-- Contar registros por tipo
SELECT 
    "FuncaoModbus",
    COUNT(*) as "Total"
FROM "ModbusConfigs"
WHERE "IpAddress" = 'modec.automais.cloud'
GROUP BY "FuncaoModbus"
ORDER BY "FuncaoModbus";

-- Listar todos os registros
SELECT 
    "Id",
    "Nome",
    "FuncaoModbus",
    "EnderecoRegistro",
    "TipoDado",
    "Ativo"
FROM "ModbusConfigs"
WHERE "IpAddress" = 'modec.automais.cloud'
ORDER BY "FuncaoModbus", "EnderecoRegistro";
