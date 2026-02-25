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

# Publica a aplicação (sem --output para evitar warning em solution)
echo "Publicando aplicação..."
dotnet publish DataMais.csproj -c Release -o ../publish/api --no-build

cd ..

echo -e "${GREEN}✓ Backend compilado com sucesso${NC}"

echo -e "${YELLOW}📁 Copiando arquivos do backend...${NC}"

# Cria diretório se não existir (sem sudo, o usuário deve ter permissão)
mkdir -p "$API_DIR"

# Copia arquivos publicados
cp -r publish/api/* "$API_DIR/"

# Ajusta permissões básicas (sem sudo, assume que o usuário tem permissão)
chmod +x "$API_DIR/DataMais" 2>/dev/null || true

# Nota: O arquivo de serviço será copiado no step separado do workflow
echo "⚠️  Arquivo de serviço será copiado no próximo step do workflow"

echo -e "${GREEN}✓ Backend copiado para $API_DIR${NC}"

echo -e "${YELLOW}🌐 Compilando frontend React...${NC}"

cd "$FRONTEND_DIR"

# Verifica se Node.js está instalado
if ! command -v node &> /dev/null; then
    echo -e "${RED}❌ Node.js não encontrado. Instale Node.js primeiro.${NC}"
    exit 1
fi

# Instala dependências (incluindo tipos do React)
echo "Instalando dependências do frontend..."
# Tenta npm ci primeiro, se falhar usa npm install para atualizar o lock file
if npm ci --legacy-peer-deps 2>&1; then
    echo "✓ Dependências instaladas com npm ci"
else
    echo "⚠️  package-lock.json desatualizado, atualizando com npm install..."
    npm install --legacy-peer-deps
fi

# Compila para produção
echo "Compilando frontend para produção..."
npm run build

cd ..

echo -e "${GREEN}✓ Frontend compilado com sucesso${NC}"

echo -e "${YELLOW}📁 Preparando arquivos do frontend...${NC}"

# Nota: A cópia para /var/www será feita no step separado do workflow
# Aqui apenas garantimos que os arquivos estão compilados
if [ ! -d "$FRONTEND_DIR/dist" ]; then
    echo -e "${RED}❌ Diretório dist não encontrado. Frontend não foi compilado.${NC}"
    exit 1
fi

echo "✓ Frontend compilado e pronto para deploy"
echo "⚠️  Arquivos serão copiados para $WEB_DIR no próximo step do workflow"

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
