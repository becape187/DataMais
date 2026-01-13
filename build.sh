#!/bin/bash

set -e  # Exit on error

echo "🚀 Iniciando deploy do DataMais..."

# Cores para output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Diretórios
API_DIR="/home/becape/datamais.api"
WEB_DIR="/var/www/datamais"
SERVICE_FILE="/etc/systemd/system/datamais.service"
BACKEND_DIR="DataMais"
FRONTEND_DIR="DataMaisWeb"

echo -e "${YELLOW}📦 Verificando e instalando .NET SDK...${NC}"

# Verifica se .NET está instalado
if ! command -v dotnet &> /dev/null; then
    echo "Instalando .NET 8.0 SDK..."
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
    chmod +x dotnet-install.sh
    ./dotnet-install.sh --channel 8.0
    export PATH="$PATH:$HOME/.dotnet"
    export DOTNET_ROOT="$HOME/.dotnet"
else
    echo -e "${GREEN}✓ .NET já está instalado${NC}"
    dotnet --version
fi

echo -e "${YELLOW}🔨 Compilando backend .NET...${NC}"

cd "$BACKEND_DIR"

# Restaura dependências
echo "Restaurando dependências..."
dotnet restore

# Compila a aplicação
echo "Compilando aplicação..."
dotnet build -c Release --no-restore

# Publica a aplicação
echo "Publicando aplicação..."
dotnet publish -c Release -o ../publish/api --no-build

cd ..

echo -e "${GREEN}✓ Backend compilado com sucesso${NC}"

echo -e "${YELLOW}📁 Copiando arquivos do backend...${NC}"

# Cria diretório se não existir
sudo mkdir -p "$API_DIR"

# Copia arquivos publicados
sudo cp -r publish/api/* "$API_DIR/"

# Copia arquivo de serviço
sudo cp "$BACKEND_DIR/datamais.service" "$SERVICE_FILE"

# Ajusta permissões
sudo chown -R becape:becape "$API_DIR"
sudo chmod +x "$API_DIR/DataMais"

echo -e "${GREEN}✓ Backend copiado para $API_DIR${NC}"

echo -e "${YELLOW}🌐 Compilando frontend React...${NC}"

cd "$FRONTEND_DIR"

# Verifica se Node.js está instalado
if ! command -v node &> /dev/null; then
    echo -e "${RED}❌ Node.js não encontrado. Instale Node.js primeiro.${NC}"
    exit 1
fi

# Instala dependências
echo "Instalando dependências do frontend..."
npm ci

# Compila para produção
echo "Compilando frontend para produção..."
npm run build

cd ..

echo -e "${GREEN}✓ Frontend compilado com sucesso${NC}"

echo -e "${YELLOW}📁 Copiando arquivos do frontend...${NC}"

# Cria diretório web se não existir
sudo mkdir -p "$WEB_DIR"

# Copia arquivos compilados
sudo cp -r "$FRONTEND_DIR/dist"/* "$WEB_DIR/"

# Ajusta permissões
sudo chown -R www-data:www-data "$WEB_DIR"
sudo chmod -R 755 "$WEB_DIR"

echo -e "${GREEN}✓ Frontend copiado para $WEB_DIR${NC}"

# Limpa arquivos temporários
echo -e "${YELLOW}🧹 Limpando arquivos temporários...${NC}"
rm -rf publish

echo -e "${GREEN}✅ Deploy concluído com sucesso!${NC}"
echo ""
echo "📋 Próximos passos:"
echo "  1. Verifique se o arquivo de configuração existe: /home/becape/datamais.env"
echo "  2. Recarregue o systemd: sudo systemctl daemon-reload"
echo "  3. Reinicie o serviço: sudo systemctl restart datamais.service"
echo "  4. Verifique o status: sudo systemctl status datamais.service"
