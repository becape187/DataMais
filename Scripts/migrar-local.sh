#!/usr/bin/env bash
#
# migrar-local.sh — Aplica as migrations do EF Core no banco LOCAL de desenvolvimento.
#
# Diferente do deploy (onde a própria API roda Database.Migrate() ao subir), aqui a
# migration é aplicada explicitamente, com backup antes — a migration AddEnsaioEtapa
# faz um BACKFILL que reescreve o Status de todos os ensaios e cria uma etapa para
# cada um. Isso não é reversível linha a linha: o Down apaga a tabela inteira.
#
# USO:
#   cd <raiz-do-repo>
#   chmod +x Scripts/migrar-local.sh
#   ./Scripts/migrar-local.sh              # backup + aplica as migrations pendentes
#   ./Scripts/migrar-local.sh --dry-run    # só mostra o que está pendente, não aplica
#   ./Scripts/migrar-local.sh --sem-backup # pula o pg_dump (banco descartável)
#
# Requisitos: dotnet SDK 8.0. pg_dump/psql são opcionais (sem eles, não há backup
# nem verificação — o script avisa e segue).
#
# Lê as credenciais de DataMais/.env (mesmo arquivo que o ConfigService usa).
# Se der erro de "\r": rode  sed -i 's/\r$//' Scripts/migrar-local.sh

set -euo pipefail

DRY_RUN=0
COM_BACKUP=1

for arg in "$@"; do
  case "$arg" in
    --dry-run)    DRY_RUN=1 ;;
    --sem-backup) COM_BACKUP=0 ;;
    -h|--help)
      echo "Uso: ./Scripts/migrar-local.sh [--dry-run] [--sem-backup]"
      exit 0 ;;
    *)
      echo "Argumento desconhecido: $arg"
      echo "Uso: ./Scripts/migrar-local.sh [--dry-run] [--sem-backup]"
      exit 1 ;;
  esac
done

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_DIR"

PROJ="DataMais/DataMais.csproj"
ENV_FILE="DataMais/.env"
BACKUP_DIR="$REPO_DIR/db/backups"

echo "==> Repositório: $REPO_DIR"

# ── .env ────────────────────────────────────────────────────────────────────
if [ ! -f "$ENV_FILE" ]; then
  echo "❌ $ENV_FILE não encontrado."
  echo "   Copie o modelo e preencha a senha do Postgres:"
  echo "     cp DataMais/env.example $ENV_FILE"
  exit 1
fi

# Carrega só o que interessa, ignorando comentários e linhas vazias
set -a
# shellcheck disable=SC2046
eval $(grep -E '^(POSTGRES_HOST|POSTGRES_PORT|POSTGRES_USER|POSTGRES_DATABASE|POSTGRES_PASSWORD)=' "$ENV_FILE" \
       | sed 's/\r$//' | sed 's/^/export /')
set +a

PGHOST_="${POSTGRES_HOST:-localhost}"
PGPORT_="${POSTGRES_PORT:-5432}"
PGUSER_="${POSTGRES_USER:-postgres}"
PGDB_="${POSTGRES_DATABASE:-datamais}"

if [ -z "${POSTGRES_PASSWORD:-}" ]; then
  echo "❌ POSTGRES_PASSWORD vazia em $ENV_FILE — o EF não consegue conectar."
  exit 1
fi

echo "==> Banco: $PGUSER_@$PGHOST_:$PGPORT_/$PGDB_"

# ── dotnet ──────────────────────────────────────────────────────────────────
command -v dotnet >/dev/null 2>&1 || { echo "❌ dotnet não encontrado (precisa do SDK 8.0)."; exit 1; }

if ! dotnet ef --version >/dev/null 2>&1; then
  echo "==> dotnet-ef não instalado; instalando como ferramenta global..."
  dotnet tool install --global dotnet-ef
  export PATH="$PATH:$HOME/.dotnet/tools"
fi

# ── O que está pendente ─────────────────────────────────────────────────────
echo "==> Migrations pendentes:"
PENDENTES="$(dotnet ef migrations list --project "$PROJ" --no-build 2>/dev/null | grep -i "pending" || true)"

if [ -n "$PENDENTES" ]; then
  echo "$PENDENTES" | sed 's/^/    /'
else
  echo "    (não deu para listar — o EF só marca 'Pending' se conseguir ler o banco)"
  echo "    Migrations no código:"
  dotnet ef migrations list --project "$PROJ" --no-build 2>/dev/null | tail -5 | sed 's/^/      /' || true
fi

if [ "$DRY_RUN" = "1" ]; then
  echo ""
  echo "==> --dry-run: SQL que seria aplicado está em db/migrations.sql (idempotente)."
  echo "    Regenere com: dotnet ef migrations script --idempotent -o db/migrations.sql --project $PROJ"
  exit 0
fi

# ── Backup ──────────────────────────────────────────────────────────────────
if [ "$COM_BACKUP" = "1" ]; then
  if command -v pg_dump >/dev/null 2>&1; then
    mkdir -p "$BACKUP_DIR"
    TS="$(date +%Y%m%d_%H%M%S)"
    DUMP="$BACKUP_DIR/${PGDB_}_${TS}.sql"
    echo "==> Backup em $DUMP ..."
    PGPASSWORD="$POSTGRES_PASSWORD" pg_dump \
      -h "$PGHOST_" -p "$PGPORT_" -U "$PGUSER_" -d "$PGDB_" -F p -f "$DUMP"
    echo "    ✓ $(du -h "$DUMP" | cut -f1)"
  else
    echo "⚠️  pg_dump não encontrado — SEM backup."
    echo "    A migration AddEnsaioEtapa reescreve o Status de todos os ensaios."
    read -r -p "    Continuar mesmo assim? [s/N] " resp
    [ "${resp,,}" = "s" ] || { echo "Abortado."; exit 1; }
  fi
else
  echo "==> --sem-backup: pulando o pg_dump."
fi

# ── Aplica ──────────────────────────────────────────────────────────────────
echo "==> Aplicando migrations (dotnet ef database update)..."
dotnet ef database update --project "$PROJ"

# ── Verificação ─────────────────────────────────────────────────────────────
if command -v psql >/dev/null 2>&1; then
  echo ""
  echo "==> Conferindo o backfill:"
  PGPASSWORD="$POSTGRES_PASSWORD" psql \
    -h "$PGHOST_" -p "$PGPORT_" -U "$PGUSER_" -d "$PGDB_" -v ON_ERROR_STOP=1 <<'SQL'
\echo '-- Ensaios por status --'
SELECT "Status", count(*) AS total FROM "Ensaios" GROUP BY "Status" ORDER BY 1;
\echo '-- Etapas por status --'
SELECT "Status", count(*) AS total FROM "EnsaioEtapas" GROUP BY "Status" ORDER BY 1;
\echo '-- Ensaios sem etapa (deveria ser 0) --'
SELECT count(*) AS ensaios_sem_etapa
FROM "Ensaios" e
WHERE NOT EXISTS (SELECT 1 FROM "EnsaioEtapas" et WHERE et."EnsaioId" = e."Id");
\echo '-- Contador de relatórios por ano --'
SELECT * FROM "ContadoresRelatorio" ORDER BY "Ano";
SQL
else
  echo "⚠️  psql não encontrado — pulei a verificação do backfill."
fi

echo ""
echo "✅ Banco local migrado."
