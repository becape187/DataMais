-- Script para remover o índice único do campo Nome na tabela ModbusConfigs
-- Isso permite múltiplos registros com o mesmo nome (mas funções diferentes)

-- Remover o índice único existente
DROP INDEX IF EXISTS "IX_ModbusConfigs_Nome";

-- Criar índice não único para melhorar performance de buscas
CREATE INDEX IF NOT EXISTS "IX_ModbusConfigs_Nome" ON "ModbusConfigs" ("Nome");

-- Criar índice composto para facilitar buscas por nome e função
CREATE INDEX IF NOT EXISTS "IX_ModbusConfigs_Nome_FuncaoModbus" ON "ModbusConfigs" ("Nome", "FuncaoModbus");
