#!/bin/bash

# Script para verificar configuração do arquivo .env no servidor

ENV_FILE="/home/becape/datamais.env"

echo "=========================================="
echo "Diagnóstico do arquivo .env"
echo "=========================================="
echo ""

# Verificar se o arquivo existe
if [ ! -f "$ENV_FILE" ]; then
    echo "❌ ERRO: Arquivo não encontrado: $ENV_FILE"
    echo ""
    echo "Criar o arquivo com o seguinte conteúdo:"
    echo ""
    echo "# PostgreSQL Configuration"
    echo "POSTGRES_HOST=localhost"
    echo "POSTGRES_PORT=5432"
    echo "POSTGRES_DATABASE=datamais"
    echo "POSTGRES_USER=postgres"
    echo "POSTGRES_PASSWORD=sua_senha_aqui"
    echo ""
    echo "# InfluxDB Configuration"
    echo "INFLUX_URL=http://localhost:8086"
    echo "INFLUX_TOKEN=seu_token_aqui"
    echo "INFLUX_ORG=datamais"
    echo "INFLUX_BUCKET=sensores"
    echo ""
    echo "# Modbus Configuration"
    echo "MODBUS_TIMEOUT_MS=5000"
    echo "MODBUS_RETRY_COUNT=3"
    echo "MODBUS_POOLING_INTERVAL_MS=100"
    exit 1
fi

echo "✓ Arquivo encontrado: $ENV_FILE"
echo ""

# Verificar permissões
PERMS=$(stat -c "%a" "$ENV_FILE")
echo "Permissões: $PERMS"
if [ "$PERMS" != "600" ] && [ "$PERMS" != "640" ]; then
    echo "⚠️  AVISO: Permissões recomendadas: 600 ou 640"
    echo "   Corrigir com: chmod 600 $ENV_FILE"
fi
echo ""

# Verificar variáveis obrigatórias
echo "Verificando variáveis obrigatórias:"
echo ""

REQUIRED_VARS=(
    "POSTGRES_HOST"
    "POSTGRES_PORT"
    "POSTGRES_DATABASE"
    "POSTGRES_USER"
    "POSTGRES_PASSWORD"
)

MISSING_VARS=()
EMPTY_VARS=()

for var in "${REQUIRED_VARS[@]}"; do
    # Carregar o arquivo .env
    value=$(grep "^${var}=" "$ENV_FILE" | cut -d '=' -f2- | tr -d '"' | tr -d "'")
    
    if [ -z "$value" ]; then
        if grep -q "^${var}=" "$ENV_FILE"; then
            echo "❌ $var: definida mas vazia"
            EMPTY_VARS+=("$var")
        else
            echo "❌ $var: não encontrada"
            MISSING_VARS+=("$var")
        fi
    else
        # Mascarar senha
        if [ "$var" == "POSTGRES_PASSWORD" ]; then
            masked=$(echo "$value" | sed 's/./*/g')
            echo "✓ $var: $masked (${#value} caracteres)"
        else
            echo "✓ $var: $value"
        fi
    fi
done

echo ""

# Verificar se há problemas
if [ ${#MISSING_VARS[@]} -gt 0 ] || [ ${#EMPTY_VARS[@]} -gt 0 ]; then
    echo "=========================================="
    echo "❌ PROBLEMAS ENCONTRADOS:"
    echo "=========================================="
    
    if [ ${#MISSING_VARS[@]} -gt 0 ]; then
        echo "Variáveis faltando:"
        for var in "${MISSING_VARS[@]}"; do
            echo "  - $var"
        done
        echo ""
    fi
    
    if [ ${#EMPTY_VARS[@]} -gt 0 ]; then
        echo "Variáveis vazias:"
        for var in "${EMPTY_VARS[@]}"; do
            echo "  - $var"
        done
        echo ""
    fi
    
    echo "Para corrigir, edite o arquivo:"
    echo "  nano $ENV_FILE"
    echo ""
    echo "Ou use o comando:"
    echo "  sudo nano $ENV_FILE"
    echo ""
    exit 1
fi

echo "=========================================="
echo "✓ Todas as variáveis obrigatórias estão configuradas!"
echo "=========================================="
echo ""

# Verificar se o serviço está usando o arquivo correto
echo "Verificando configuração do serviço systemd:"
if systemctl cat datamais.service > /dev/null 2>&1; then
    echo ""
    echo "Configuração do serviço:"
    systemctl cat datamais.service | grep -E "(EnvironmentFile|Environment=)" || echo "  (nenhuma configuração de ambiente encontrada)"
    echo ""
    
    # Verificar se o serviço está rodando
    if systemctl is-active --quiet datamais.service; then
        echo "✓ Serviço está rodando"
    else
        echo "⚠️  Serviço não está rodando"
        echo "   Iniciar com: sudo systemctl start datamais.service"
    fi
else
    echo "⚠️  Serviço datamais.service não encontrado"
fi

echo ""
echo "=========================================="
echo "Teste de conexão com PostgreSQL"
echo "=========================================="

# Tentar carregar variáveis e testar conexão
export $(grep -v '^#' "$ENV_FILE" | xargs)

if command -v psql &> /dev/null; then
    echo "Testando conexão com PostgreSQL..."
    if PGPASSWORD="$POSTGRES_PASSWORD" psql -h "$POSTGRES_HOST" -p "$POSTGRES_PORT" -U "$POSTGRES_USER" -d "$POSTGRES_DATABASE" -c "SELECT 1;" > /dev/null 2>&1; then
        echo "✓ Conexão com PostgreSQL bem-sucedida!"
    else
        echo "❌ Erro ao conectar ao PostgreSQL"
        echo "   Verifique se:"
        echo "   1. PostgreSQL está rodando: sudo systemctl status postgresql"
        echo "   2. As credenciais estão corretas no arquivo .env"
        echo "   3. O usuário tem permissão para acessar o banco"
    fi
else
    echo "⚠️  psql não encontrado (opcional)"
fi

echo ""
echo "=========================================="
echo "Diagnóstico concluído"
echo "=========================================="
