#!/usr/bin/env bash
#
# deploy-local.sh — Deploy do DataMais rodando NA VM de destino.
#
# Replica o que o GitHub Action (.github/workflows/deploy.yml) faz, mas
# executado localmente na VM a partir de um clone/pull do repositório.
# Útil quando não há acesso VPN/CI: você faz `git pull` numa pasta qualquer
# e roda este script.
#
# O QUE ELE FAZ (mesma ordem do Action):
#   1. Builda o backend (dotnet publish) e o frontend (npm run build)
#   2. Para o serviço systemd
#   3. Faz backup e substitui os arquivos em /home/becape/datamais.api e /var/www/datamais
#   4. Reinicia o serviço  →  a API aplica as migrations do EF Core no startup
#   5. Mostra o log pra você conferir migrations/seed
#
# A ALTERAÇÃO NO BANCO é feita pela própria API ao subir (dbContext.Database.Migrate()),
# exatamente como acontece no deploy via GitHub Action. Não é preciso rodar SQL na mão.
# (Fallback manual, se algum dia quiser: db/migrations.sql — script SQL idempotente.)
#
# USO:
#   cd <pasta-do-repo-pullado>
#   chmod +x deploy-local.sh
#   ./deploy-local.sh
#
# Requisitos na VM: dotnet SDK 8.0, Node 20 (npm), sudo, systemd.
# Se der erro de "\r": rode  sed -i 's/\r$//' deploy-local.sh

set -euo pipefail

# ── Config (ajuste aqui se os caminhos do servidor mudarem) ─────────────────
API_DIR="/home/becape/datamais.api"
WEB_DIR="/var/www/datamais"
SERVICE="datamais.service"
APP_USER="becape"
WEB_USER="www-data"
ENV_FILE="/home/becape/datamais.env"

REPO_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$REPO_DIR"
echo "==> Repositório: $REPO_DIR"

# ── Checagens de ferramentas ────────────────────────────────────────────────
DOTNET_BIN="$(command -v dotnet || true)"
if [ -z "$DOTNET_BIN" ]; then
  for p in /home/becape/.dotnet/dotnet /usr/share/dotnet/dotnet /usr/local/bin/dotnet /usr/bin/dotnet; do
    [ -x "$p" ] && DOTNET_BIN="$p" && break
  done
fi
[ -z "$DOTNET_BIN" ] && { echo "❌ dotnet não encontrado (precisa do SDK 8.0 para buildar)."; exit 1; }
echo "==> dotnet: $DOTNET_BIN ($($DOTNET_BIN --version 2>/dev/null || echo '?'))"

command -v npm >/dev/null 2>&1 || { echo "❌ npm/node não encontrado (precisa do Node 20)."; exit 1; }
echo "==> node:   $(node --version 2>/dev/null || echo '?')"

# ── Aviso: JWT_SECRET no env de produção ────────────────────────────────────
if ! sudo grep -q "^JWT_SECRET=" "$ENV_FILE" 2>/dev/null; then
  echo "⚠️  JWT_SECRET não encontrado em $ENV_FILE."
  echo "    A API vai subir com um segredo de DEV inseguro. Recomendado adicionar:"
  echo "      echo \"JWT_SECRET=\$(openssl rand -base64 48)\" | sudo tee -a $ENV_FILE"
  read -r -p "    Continuar mesmo assim? [s/N] " resp
  [ "${resp,,}" = "s" ] || { echo "Abortado."; exit 1; }
fi

# ── Build backend ───────────────────────────────────────────────────────────
echo "==> Build backend (dotnet publish -c Release)..."
rm -rf "$REPO_DIR/publish/api"
"$DOTNET_BIN" restore DataMais/DataMais.csproj
"$DOTNET_BIN" publish DataMais/DataMais.csproj -c Release -o "$REPO_DIR/publish/api"

# ── Build frontend ──────────────────────────────────────────────────────────
echo "==> Build frontend (npm run build)..."
pushd DataMaisWeb >/dev/null
npm ci || npm install --legacy-peer-deps
npm run build
popd >/dev/null

TS="$(date +%Y%m%d_%H%M%S)"

# ── Para o serviço ──────────────────────────────────────────────────────────
echo "==> Parando $SERVICE..."
sudo systemctl stop "$SERVICE" || true

# ── Backend: backup + substituição ──────────────────────────────────────────
echo "==> Atualizando backend em $API_DIR (backup: /home/becape/backup_api_$TS.tar.gz)..."
sudo mkdir -p "$API_DIR"
sudo tar -czf "/home/becape/backup_api_$TS.tar.gz" -C "$API_DIR" . 2>/dev/null || true
sudo rm -rf "${API_DIR:?}/"*
sudo cp -r "$REPO_DIR/publish/api/." "$API_DIR/"
sudo chown -R "$APP_USER:$APP_USER" "$API_DIR"

# ── Frontend: backup + substituição ─────────────────────────────────────────
echo "==> Atualizando frontend em $WEB_DIR (backup: /home/becape/backup_web_$TS.tar.gz)..."
sudo mkdir -p "$WEB_DIR"
sudo tar -czf "/home/becape/backup_web_$TS.tar.gz" -C "$WEB_DIR" . 2>/dev/null || true
sudo rm -rf "${WEB_DIR:?}/"*
sudo cp -r "$REPO_DIR/DataMaisWeb/dist/." "$WEB_DIR/"
sudo chown -R "$WEB_USER:$WEB_USER" "$WEB_DIR"

# ── Reinicia (migrations aplicam no startup da API) ─────────────────────────
echo "==> Reiniciando $SERVICE (migrations e seeds aplicam automaticamente no boot)..."
sudo systemctl daemon-reload
sudo systemctl restart "$SERVICE" || true

sleep 3
if systemctl is-active --quiet "$SERVICE"; then
  echo "✓ Serviço ativo."
else
  echo "❌ Serviço NÃO subiu. Últimos logs:"
  sudo journalctl -u "$SERVICE" -n 40 --no-pager
  exit 1
fi

echo "==> Log recente (confira migrations/seed):"
sudo journalctl -u "$SERVICE" -n 40 --no-pager | grep -Ei "migrat|seed|admin|usuário|role|✓|error|erro|exception" || \
  sudo journalctl -u "$SERVICE" -n 20 --no-pager

echo ""
echo "✅ Deploy concluído."
