#!/usr/bin/env bash
#
# limpar-historico.sh — Apaga TODO o histórico de ensaios e laudos, zerando a numeração.
#
# ⚠️  DESTRUTIVO E IRREVERSÍVEL. Apaga:
#       - Relatorios, RespostasCampoRelatorio, RelatorioVersoes
#       - Ensaios e EnsaioEtapas
#       - ContadoresRelatorio  (o próximo laudo volta a ser REH-MPR-0000001-<ano>)
#       - measurement "ensaio_pressao" no InfluxDB (todas as curvas de pressão)
#
#     NÃO apaga: clientes, cilindros, sensores, usuários, configuração Modbus,
#     campos de relatório (o checklist configurável).
#
# Motivo de existir: os laudos antigos são de câmara única e o formato novo exige as
# duas câmaras com gráficos separados. Misturar os dois formatos na lista de relatórios
# só confunde quem consulta.
#
# USO:
#   ./Scripts/limpar-historico.sh                   # banco local (DataMais/.env)
#   ./Scripts/limpar-historico.sh --env <arquivo>   # outro .env (ex.: o da VM)
#   ./Scripts/limpar-historico.sh --sem-influx      # só o Postgres, preserva as séries
#
# O script SEMPRE faz pg_dump antes e exige que você digite APAGAR para confirmar.
# Se der erro de "\r": rode  sed -i 's/\r$//' Scripts/limpar-historico.sh

set -euo pipefail

ENV_FILE=""
LIMPAR_INFLUX=1

while [ $# -gt 0 ]; do
  case "$1" in
    --env)         ENV_FILE="${2:-}"; shift 2 ;;
    --sem-influx)  LIMPAR_INFLUX=0; shift ;;
    -h|--help)
      echo "Uso: ./Scripts/limpar-historico.sh [--env <arquivo>] [--sem-influx]"
      exit 0 ;;
    *)
      echo "Argumento desconhecido: $1"
      echo "Uso: ./Scripts/limpar-historico.sh [--env <arquivo>] [--sem-influx]"
      exit 1 ;;
  esac
done

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_DIR"

ENV_FILE="${ENV_FILE:-DataMais/.env}"
BACKUP_DIR="$REPO_DIR/db/backups"

[ -f "$ENV_FILE" ] || { echo "❌ $ENV_FILE não encontrado."; exit 1; }

set -a
# shellcheck disable=SC2046
eval $(grep -E '^(POSTGRES_|INFLUX_)' "$ENV_FILE" | sed 's/\r$//' | sed 's/^/export /')
set +a

PGHOST_="${POSTGRES_HOST:-localhost}"
PGPORT_="${POSTGRES_PORT:-5432}"
PGUSER_="${POSTGRES_USER:-postgres}"
PGDB_="${POSTGRES_DATABASE:-datamais}"

[ -n "${POSTGRES_PASSWORD:-}" ] || { echo "❌ POSTGRES_PASSWORD vazia em $ENV_FILE."; exit 1; }
command -v psql    >/dev/null 2>&1 || { echo "❌ psql não encontrado."; exit 1; }
command -v pg_dump >/dev/null 2>&1 || { echo "❌ pg_dump não encontrado — sem backup não rodo."; exit 1; }

pg() { PGPASSWORD="$POSTGRES_PASSWORD" psql -h "$PGHOST_" -p "$PGPORT_" -U "$PGUSER_" -d "$PGDB_" "$@"; }

# ── Mostra o que será perdido ───────────────────────────────────────────────
echo "==> Banco: $PGUSER_@$PGHOST_:$PGPORT_/$PGDB_"
echo "==> O que será apagado:"
pg -v ON_ERROR_STOP=1 -t <<'SQL'
SELECT '    relatórios:  ' || count(*) FROM "Relatorios";
SELECT '    ensaios:     ' || count(*) FROM "Ensaios";
SELECT '    etapas:      ' || count(*) FROM "EnsaioEtapas";
SQL

echo ""
echo "⚠️  Isso é IRREVERSÍVEL. Cadastros (clientes, cilindros, sensores, usuários,"
echo "    Modbus e campos de relatório) permanecem intactos."
read -r -p "    Digite APAGAR para confirmar: " confirmacao
[ "$confirmacao" = "APAGAR" ] || { echo "Abortado."; exit 1; }

# ── Backup ──────────────────────────────────────────────────────────────────
mkdir -p "$BACKUP_DIR"
TS="$(date +%Y%m%d_%H%M%S)"
DUMP="$BACKUP_DIR/${PGDB_}_antes_limpeza_${TS}.sql"
echo "==> Backup em $DUMP ..."
PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -h "$PGHOST_" -p "$PGPORT_" -U "$PGUSER_" -d "$PGDB_" -F p -f "$DUMP"
echo "    ✓ $(du -h "$DUMP" | cut -f1)"

# ── Postgres ────────────────────────────────────────────────────────────────
# RESTART IDENTITY devolve os Ids para 1; CASCADE cobre as FKs entre essas tabelas.
echo "==> Limpando o Postgres..."
pg -v ON_ERROR_STOP=1 <<'SQL'
BEGIN;
TRUNCATE TABLE
    "RespostasCampoRelatorio",
    "RelatorioVersoes",
    "Relatorios",
    "EnsaioEtapas",
    "Ensaios",
    "ContadoresRelatorio"
RESTART IDENTITY CASCADE;
COMMIT;
SQL
echo "    ✓ tabelas zeradas"

# ── InfluxDB ────────────────────────────────────────────────────────────────
if [ "$LIMPAR_INFLUX" = "1" ]; then
  if [ -z "${INFLUX_URL:-}" ] || [ -z "${INFLUX_TOKEN:-}" ] || \
     [ -z "${INFLUX_ORG:-}" ] || [ -z "${INFLUX_BUCKET:-}" ]; then
    echo "⚠️  Configuração do InfluxDB incompleta em $ENV_FILE — as curvas de pressão"
    echo "    ficaram no bucket. Sem relatório apontando para elas, viram lixo inerte."
  else
    echo "==> Apagando o measurement ensaio_pressao no InfluxDB..."
    RESP="$(curl -sS -o /dev/null -w '%{http_code}' \
      --request POST "${INFLUX_URL%/}/api/v2/delete?org=${INFLUX_ORG}&bucket=${INFLUX_BUCKET}" \
      --header "Authorization: Token ${INFLUX_TOKEN}" \
      --header 'Content-Type: application/json' \
      --data '{
        "start": "1970-01-01T00:00:00Z",
        "stop":  "2100-01-01T00:00:00Z",
        "predicate": "_measurement=\"ensaio_pressao\""
      }')" || RESP="erro"

    if [ "$RESP" = "204" ]; then
      echo "    ✓ séries removidas"
    else
      echo "⚠️  InfluxDB respondeu HTTP $RESP — as séries podem não ter sido apagadas."
      echo "    Confira INFLUX_URL/TOKEN/ORG/BUCKET em $ENV_FILE."
    fi
  fi
else
  echo "==> --sem-influx: séries de pressão preservadas."
fi

# ── Conferência ─────────────────────────────────────────────────────────────
echo ""
echo "==> Estado final:"
pg -t <<'SQL'
SELECT '    relatórios:  ' || count(*) FROM "Relatorios";
SELECT '    ensaios:     ' || count(*) FROM "Ensaios";
SELECT '    etapas:      ' || count(*) FROM "EnsaioEtapas";
SELECT '    clientes:    ' || count(*) FROM "Clientes"  || '   (preservados)';
SELECT '    cilindros:   ' || count(*) FROM "Cilindros" || '   (preservados)';
SELECT '    sensores:    ' || count(*) FROM "Sensores"  || '   (preservados)';
SQL

echo ""
echo "✅ Histórico limpo. O próximo laudo sai como REH-MPR-0000001-$(date +%Y)."
echo "   Backup do estado anterior: $DUMP"
