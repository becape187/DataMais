-- Script para importar configurações Modbus do arquivo JSON
-- IP: modec.automais.cloud
-- Porta padrão: 502
-- SlaveId padrão: 1

-- Limpar registros existentes do mesmo IP (opcional - descomente se necessário)
-- DELETE FROM "ModbusConfigs" WHERE "IpAddress" = 'modec.automais.cloud';

-- Inserir registros Coil (WriteSingleCoil/ReadCoils)
INSERT INTO "ModbusConfigs" ("Nome", "Descricao", "IpAddress", "Port", "SlaveId", "FuncaoModbus", "EnderecoRegistro", "QuantidadeRegistros", "TipoDado", "ByteOrder", "OrdemLeitura", "Ativo", "DataCriacao")
VALUES
('BT_EMERGENCIA', 'Botão Emergência', 'modec.automais.cloud', 502, 1, 'ReadCoils', 0, 1, 'Boolean', 'BigEndian', 1, true, NOW()),
('SL_SOLENOIDE_A', 'Solenoide A', 'modec.automais.cloud', 502, 1, 'ReadCoils', 1, 1, 'Boolean', 'BigEndian', 2, true, NOW()),
('SL_SOLENOIDE_B', 'Solenoide B', 'modec.automais.cloud', 502, 1, 'ReadCoils', 2, 1, 'Boolean', 'BigEndian', 3, true, NOW()),
('ST_FILTRO_OLEO', 'Status Filtro Óleo', 'modec.automais.cloud', 502, 1, 'ReadCoils', 3, 1, 'Boolean', 'BigEndian', 4, true, NOW()),
('ST_NIVEL_OLEO', 'Status Nível Óleo', 'modec.automais.cloud', 502, 1, 'ReadCoils', 4, 1, 'Boolean', 'BigEndian', 5, true, NOW()),
('MOTOR_BOMBA', 'Motor Bomba', 'modec.automais.cloud', 502, 1, 'ReadCoils', 5, 1, 'Boolean', 'BigEndian', 6, true, NOW()),
('RADIADOR', 'Radiador', 'modec.automais.cloud', 502, 1, 'ReadCoils', 6, 1, 'Boolean', 'BigEndian', 7, true, NOW()),
('SOLENOIDE_2', 'Solenoide 2', 'modec.automais.cloud', 502, 1, 'ReadCoils', 7, 1, 'Boolean', 'BigEndian', 8, true, NOW()),
('SOLENOIDE_4', 'Solenoide 4', 'modec.automais.cloud', 502, 1, 'ReadCoils', 8, 1, 'Boolean', 'BigEndian', 9, true, NOW()),
('BOTAO_DESLIGA_MOTOR', 'Botão Desliga Motor', 'modec.automais.cloud', 502, 1, 'ReadCoils', 12, 1, 'Boolean', 'BigEndian', 10, true, NOW()),
('BOTAO_LIGA_MOTOR', 'Botão Liga Motor', 'modec.automais.cloud', 502, 1, 'ReadCoils', 13, 1, 'Boolean', 'BigEndian', 11, true, NOW()),
('BOTAO_DESLIGA_RADIADOR', 'Botão Desliga Radiador', 'modec.automais.cloud', 502, 1, 'ReadCoils', 14, 1, 'Boolean', 'BigEndian', 12, true, NOW()),
('BOTAO_LIGA_RADIADOR', 'Botão Liga Radiador', 'modec.automais.cloud', 502, 1, 'ReadCoils', 15, 1, 'Boolean', 'BigEndian', 13, true, NOW()),
('BOTAO_INICIA', 'Botão Inicia', 'modec.automais.cloud', 502, 1, 'ReadCoils', 16, 1, 'Boolean', 'BigEndian', 14, true, NOW()),
('BOTAO_PARA', 'Botão Para', 'modec.automais.cloud', 502, 1, 'ReadCoils', 17, 1, 'Boolean', 'BigEndian', 15, true, NOW()),
('BOTAO_AVANCA_IHM', 'Botão Avança IHM', 'modec.automais.cloud', 502, 1, 'ReadCoils', 18, 1, 'Boolean', 'BigEndian', 16, true, NOW()),
('BOTAO_RECUA_IHM', 'Botão Recua IHM', 'modec.automais.cloud', 502, 1, 'ReadCoils', 19, 1, 'Boolean', 'BigEndian', 17, true, NOW());

-- Inserir registros Discrete Input (ReadInputs)
INSERT INTO "ModbusConfigs" ("Nome", "Descricao", "IpAddress", "Port", "SlaveId", "FuncaoModbus", "EnderecoRegistro", "QuantidadeRegistros", "TipoDado", "ByteOrder", "OrdemLeitura", "Ativo", "DataCriacao")
VALUES
('ALARME_FILTRO_OLEO', 'Alarme Filtro Óleo', 'modec.automais.cloud', 502, 1, 'ReadInputs', 16, 1, 'Boolean', 'BigEndian', 18, true, NOW()),
('ALARME_NIVEL_OLEO', 'Alarme Nível Óleo', 'modec.automais.cloud', 502, 1, 'ReadInputs', 17, 1, 'Boolean', 'BigEndian', 19, true, NOW()),
('REGISTRO_RODANDO', 'Registro Rodando', 'modec.automais.cloud', 502, 1, 'ReadInputs', 18, 1, 'Boolean', 'BigEndian', 20, true, NOW()),
('AUX_AVANCA', 'Auxiliar Avança', 'modec.automais.cloud', 502, 1, 'ReadInputs', 19, 1, 'Boolean', 'BigEndian', 21, true, NOW()),
('AUX_RECUA', 'Auxiliar Recua', 'modec.automais.cloud', 502, 1, 'ReadInputs', 20, 1, 'Boolean', 'BigEndian', 22, true, NOW());

-- Inserir registros Holding Register (ReadHoldingRegisters)
INSERT INTO "ModbusConfigs" ("Nome", "Descricao", "IpAddress", "Port", "SlaveId", "FuncaoModbus", "EnderecoRegistro", "QuantidadeRegistros", "TipoDado", "ByteOrder", "OrdemLeitura", "Ativo", "DataCriacao")
VALUES
('LIMITE_A', 'Limite A', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 0, 1, 'UInt16', 'BigEndian', 23, true, NOW()),
('LIMITE_B', 'Limite B', 'modec.automais.cloud', 502, 1, 'ReadHoldingRegisters', 1, 1, 'UInt16', 'BigEndian', 24, true, NOW());

-- Inserir registros Input Register (ReadInputRegisters)
INSERT INTO "ModbusConfigs" ("Nome", "Descricao", "IpAddress", "Port", "SlaveId", "FuncaoModbus", "EnderecoRegistro", "QuantidadeRegistros", "TipoDado", "ByteOrder", "OrdemLeitura", "Ativo", "DataCriacao")
VALUES
('PRESSAO_GERAL', 'Pressão Geral', 'modec.automais.cloud', 502, 1, 'ReadInputRegisters', 9, 1, 'UInt16', 'BigEndian', 25, true, NOW()),
('PRESSAO_A', 'Pressão A', 'modec.automais.cloud', 502, 1, 'ReadInputRegisters', 10, 1, 'UInt16', 'BigEndian', 26, true, NOW()),
('PRESSAO_B', 'Pressão B', 'modec.automais.cloud', 502, 1, 'ReadInputRegisters', 11, 1, 'UInt16', 'BigEndian', 27, true, NOW());

-- Verificar registros inseridos
SELECT COUNT(*) as "TotalRegistros" FROM "ModbusConfigs" WHERE "IpAddress" = 'modec.automais.cloud';
