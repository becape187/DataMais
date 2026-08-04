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
#   3. Faz backup do BANCO (pg_dump) e dos arquivos atuais
#   4. Substitui /home/becape/datamais.api e /var/www/datamais
#   5. Reinicia o serviço  →  a API aplica as migrations do EF Core no startup
#   6. Mostra o log pra você conferir migrations/seed
#
# A ALTERAÇÃO NO BANCO é feita pela própria API ao subir (dbContext.Database.Migrate()),
# exatamente como acontece no deploy via GitHub Action. Não é preciso rodar SQL na mão.
# (Fallback manual, se algum dia quiser: db/migrations.sql — script SQL idempotente.)
#
# ⚠️  A migration AddEnsaioEtapa faz um BACKFILL: cria uma etapa para cada ensaio
#     existente e reescreve o Status de todos eles (Concluido→Aceito, EmExecucao→
#     EmAndamento, Pendente→Cancelado). O Down reverte os status mas APAGA as etapas.
#     Por isso o dump do banco virou parte do deploy, e não uma sugestão.
#
# 📌 Release "janela de contagem" (migration AddJanelaContagemEnsaioEtapa):
#     Adiciona EnsaioEtapas.DataInicioContagem/DataFimContagem (duas colunas nullable,
#     sem backfill).
#     Quem manda em quê, no CLP:
#       REGISTRO_RODANDO  inicia e para o registro — a descida dele, sozinha, fecha a etapa.
#                         Precisa existir como ReadInputs, senão NENHUMA câmara inicia
#                         (a confirmação de partida virou bloqueante).
#       INICIA_CONTAGEM   manda só na contagem de tempo (t0 do laudo e relógio da tela).
#                         Precisa estar cadastrado com função de LEITURA; com função de
#                         escrita a leitura lança exceção e a contagem nunca começa.
#     Este script confere as colunas e o cadastro dos dois no fim do deploy.
#
# 🔴 Release "checklist volta a aparecer" (migration CorrigeFkRespostasCampoRelatorio):
#     O checklist gravava e NUNCA voltava — todo laudo reaberto mostrava "Não respondido".
#     Causa: DataMaisDbContext mapeava a FK com .WithMany() vazio, então o EF criou uma
#     coluna sombra RespostasCampoRelatorio.RelatorioId1 e passou a LER por ela, enquanto
#     o controller GRAVAVA em RelatorioId. A migration dropa a coluna sombra.
#     ⚠️  As respostas antigas NÃO se perderam: estavam certas em RelatorioId o tempo todo
#         e voltam a aparecer sozinhas depois deste deploy.
#     ⚠️  O override "reprova se Sim" (ex.: Vazamentos visíveis) lia pela mesma navegação
#         quebrada e NUNCA disparou. Como o veredito é calculado ao abrir o laudo, um laudo
#         já assinado pode passar a mostrar REPROVADO depois deste deploy. Confira os
#         laudos emitidos que tenham "Vazamentos visíveis = Sim".
#
# 📌 Release "checklist congela no aceite" (SEM migration — só código e seed):
#     - Laudo Concluido passa a RECUSAR gravação de checklist (409). Reabrir virou
#       ato explícito (botão "Editar (gera vN+1)" → POST /relatorio/{id}/reabrir).
#       Antes, tocar num radio de laudo assinado o reabria sozinho e sumia o PDF.
#     - Em laudo assinado o checklist é exibido como TEXTO (✔ Sim / ✕ Não), não input:
#       input radio marcado não aparece no PDF.
#     ⚠️  APAGA DADOS: no boot, DbSeeder.RemoverCamposDescontinuados faz soft-delete
#         da pergunta "Estado das conexões e flanges" E DELETA as respostas dadas a
#         ela (em todos os laudos, inclusive assinados). É de propósito — a pergunta
#         foi retirada do formulário. O histórico fica preservado no snapshot da
#         versão assinada (RelatorioVersao.RespostasJson), e o dump feito por este
#         script logo antes do restart é a volta, se for o caso.
#         Procure no log: "✓ 1 pergunta(s) descontinuada(s) do checklist removida(s)".
#
# 📌 Release "ensaio de câmara única" (migration AddCamarasHabilitadasEnsaio):
#     Adiciona Ensaios.CamaraAHabilitada/CamaraBHabilitada (bool NOT NULL, DEFAULT TRUE).
#     O default é TRUE de propósito: todo ensaio já existente continua sendo um ensaio
#     de duas câmaras. Se o default tivesse ficado FALSE (o que o scaffold do EF gera),
#     nenhum ensaio aberto poderia mais ser aceito.
#     O que muda no uso: cada câmara tem um checkbox na tela de Ensaio. Desmarcar uma
#     libera o aceite sem ela e o laudo sai com uma câmara só (modal confirma no aceite).
#     ⚠️  APAGA DADOS quando usado: desmarcar uma câmara que JÁ RODOU marca as corridas
#         dela como Descartada e APAGA as leituras dela no InfluxDB. A tela confirma antes.
#     Também nesta release (sem efeito no banco): o "Histórico de Versões" continua na
#     tela do laudo mas não entra mais no PDF.
#
# USO:
#   cd <pasta-do-repo-pullado>
#   chmod +x deploy-local.sh
#   ./deploy-local.sh                  # deploy normal (com dump do banco)
#   ./deploy-local.sh --reset-admin    # deploy + FORÇA admin/admin no banco
#   ./deploy-local.sh --sem-backup-db  # pula o pg_dump (só se souber o que está fazendo)
#
# --reset-admin: faz um upsert do usuário admin (email 'admin', senha 'admin', perfil Admin)
#   direto no Postgres. Use quando não conseguir logar. Sem a flag, o deploy NÃO mexe na senha.
#   Requer o cliente psql na VM.
#
# Requisitos na VM: dotnet SDK 8.0, Node 20 (npm), sudo, systemd, pg_dump (e psql p/ --reset-admin).
# Se der erro de "\r": rode  sed -i 's/\r$//' deploy-local.sh

set -euo pipefail

# ── Flags ───────────────────────────────────────────────────────────────────
RESET_ADMIN=0
BACKUP_DB=1
USO="Uso: ./deploy-local.sh [--reset-admin] [--sem-backup-db]"
for arg in "$@"; do
  case "$arg" in
    --reset-admin)   RESET_ADMIN=1 ;;
    --sem-backup-db) BACKUP_DB=0 ;;
    -h|--help) echo "$USO"; exit 0 ;;
    *) echo "Argumento desconhecido: $arg"; echo "$USO"; exit 1 ;;
  esac
done

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

# ── Backup do banco (com o serviço parado, ninguém escrevendo) ──────────────
# Feito ANTES de trocar os binários: se a migration der errado, o restore volta
# ao estado exato de agora.
if [ "$BACKUP_DB" = "1" ]; then
  DUMP="/home/becape/backup_db_$TS.sql.gz"
  echo "==> Backup do banco em $DUMP ..."

  if ! command -v pg_dump >/dev/null 2>&1; then
    echo "⚠️  pg_dump não encontrado na VM — o deploy seguiria SEM rede de segurança."
    echo "    Instale (sudo apt install postgresql-client) ou rode com --sem-backup-db."
    read -r -p "    Continuar sem backup do banco? [s/N] " resp
    [ "${resp,,}" = "s" ] || { echo "Abortado. Serviço segue parado: sudo systemctl start $SERVICE"; exit 1; }
  else
    TMPENV="$(mktemp)"
    sudo grep -E '^(POSTGRES_HOST|POSTGRES_PORT|POSTGRES_USER|POSTGRES_DATABASE|POSTGRES_PASSWORD)=' \
      "$ENV_FILE" > "$TMPENV" 2>/dev/null || true
    set -a; . "$TMPENV"; set +a
    rm -f "$TMPENV"

    if PGPASSWORD="${POSTGRES_PASSWORD:-}" pg_dump \
         -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
         -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" \
         -F p | gzip > "$DUMP"; then
      echo "    ✓ $(du -h "$DUMP" | cut -f1)"
      echo "    Restore, se precisar:"
      echo "      gunzip -c $DUMP | PGPASSWORD=... psql -h ... -U ... -d ${POSTGRES_DATABASE:-datamais}"
    else
      rm -f "$DUMP"
      echo "❌ pg_dump falhou — abortando antes de mexer no banco."
      echo "   Serviço segue parado: sudo systemctl start $SERVICE"
      exit 1
    fi
  fi
else
  echo "==> --sem-backup-db: pulando o dump do banco."
fi

# ── Pré-voo da migration CorrigeFkRespostasCampoRelatorio ───────────────────
# Ela DROPA a coluna sombra RespostasCampoRelatorio.RelatorioId1. A coluna nunca foi
# escrita pela aplicação (todo insert seta RelatorioId), mas conferir custa nada e
# depois do restart a coluna não existe mais para ser olhada.
if command -v psql >/dev/null 2>&1; then
  TMPENV="$(mktemp)"
  sudo grep -E '^(POSTGRES_HOST|POSTGRES_PORT|POSTGRES_USER|POSTGRES_DATABASE|POSTGRES_PASSWORD)=' \
    "$ENV_FILE" > "$TMPENV" 2>/dev/null || true
  set -a; . "$TMPENV"; set +a
  rm -f "$TMPENV"

  ORFAS="$(PGPASSWORD="${POSTGRES_PASSWORD:-}" psql -tAq \
    -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
    -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" \
    -c "SELECT count(*) FROM \"RespostasCampoRelatorio\" WHERE \"RelatorioId1\" IS NOT NULL;" \
    2>/dev/null || echo "sem-coluna")"

  case "$ORFAS" in
    0|sem-coluna) echo "==> Pré-voo OK: nada depende da coluna sombra RelatorioId1." ;;
    *)
      echo "⚠️  $ORFAS resposta(s) com RelatorioId1 preenchido — a migration vai dropar essa coluna."
      echo "    Não deveria acontecer (a aplicação só escreve RelatorioId). O dump acabou de ser"
      echo "    feito; se quiser conferir antes, aborte agora e olhe a tabela."
      read -r -p "    Continuar o deploy? [s/N] " resp
      [ "${resp,,}" = "s" ] || { echo "Abortado. Serviço parado: sudo systemctl start $SERVICE"; exit 1; }
      ;;
  esac
fi

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

# ── Conferência do backfill de etapas ───────────────────────────────────────
if command -v psql >/dev/null 2>&1; then
  echo ""
  echo "==> Conferindo o backfill de etapas:"
  TMPENV="$(mktemp)"
  sudo grep -E '^(POSTGRES_HOST|POSTGRES_PORT|POSTGRES_USER|POSTGRES_DATABASE|POSTGRES_PASSWORD)=' \
    "$ENV_FILE" > "$TMPENV" 2>/dev/null || true
  set -a; . "$TMPENV"; set +a
  rm -f "$TMPENV"

  PGPASSWORD="${POSTGRES_PASSWORD:-}" psql \
    -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
    -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" <<'SQL' || \
    echo "⚠️  Não deu para conferir via psql (siga pelo log acima)."
\echo '-- Ensaios por status --'
SELECT "Status", count(*) AS total FROM "Ensaios" GROUP BY "Status" ORDER BY 1;
\echo '-- Ensaios sem etapa (esperado: 0) --'
SELECT count(*) AS ensaios_sem_etapa
FROM "Ensaios" e
WHERE NOT EXISTS (SELECT 1 FROM "EnsaioEtapas" et WHERE et."EnsaioId" = e."Id");
SQL

  # ── Release "janela de contagem": colunas novas + registro do CLP ──────────
  echo ""
  echo "==> Conferindo a release da janela de contagem:"

  COLUNAS="$(PGPASSWORD="${POSTGRES_PASSWORD:-}" psql -tAq \
    -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
    -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" \
    -c "SELECT count(*) FROM information_schema.columns
        WHERE table_name = 'EnsaioEtapas'
          AND column_name IN ('DataInicioContagem','DataFimContagem');" 2>/dev/null || echo "?")"

  if [ "$COLUNAS" = "2" ]; then
    echo "    ✓ EnsaioEtapas.DataInicioContagem/DataFimContagem criadas pela migration."
  else
    echo "    ❌ As colunas da janela de contagem NÃO estão no banco (encontradas: $COLUNAS de 2)."
    echo "       A migration não aplicou. Veja o log do serviço acima antes de rodar ensaio."
  fi

  # O INICIA_CONTAGEM é cadastrado pela tela de Registros, não por seed — em uma VM
  # nova (ou banco restaurado de backup antigo) ele simplesmente não existe.
  # A FUNÇÃO importa: cadastrar com função de ESCRITA faz a leitura lançar exceção
  # (foi o que aconteceu no primeiro ensaio desta release).
  echo "    -- Cadastro dos sinais de ciclo --"
  PGPASSWORD="${POSTGRES_PASSWORD:-}" psql \
    -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
    -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" <<'SQL' || \
    echo "    ⚠️  Não deu para listar os registros."
SELECT "Nome", "FuncaoModbus", "EnderecoRegistro" AS "Endereco", "SlaveId",
       "TipoDado", "Ativo"
FROM "ModbusConfigs"
WHERE "Nome" IN ('REGISTRO_RODANDO','INICIA_CONTAGEM')
ORDER BY "Nome";
SQL

  CONTAGEM_OK="$(PGPASSWORD="${POSTGRES_PASSWORD:-}" psql -tAq \
    -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
    -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" \
    -c "SELECT count(*) FROM \"ModbusConfigs\"
        WHERE \"Nome\" = 'INICIA_CONTAGEM' AND \"Ativo\" = true
          AND \"FuncaoModbus\" IN ('ReadCoils','ReadInputs','ReadHoldingRegisters','ReadInputRegisters');" \
    2>/dev/null || echo "?")"

  CONTAGEM_EXISTE="$(PGPASSWORD="${POSTGRES_PASSWORD:-}" psql -tAq \
    -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
    -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" \
    -c "SELECT count(*) FROM \"ModbusConfigs\" WHERE \"Nome\" = 'INICIA_CONTAGEM';" \
    2>/dev/null || echo "?")"

  if [ "${CONTAGEM_OK:-0}" -ge 1 ] 2>/dev/null; then
    echo "    ✓ INICIA_CONTAGEM cadastrado, ativo e com função de LEITURA."
  elif [ "${CONTAGEM_EXISTE:-0}" -ge 1 ] 2>/dev/null; then
    echo "    ❌ INICIA_CONTAGEM existe, mas está INATIVO ou com função de ESCRITA."
    echo "       Ler um registro cadastrado como Write* lança exceção: a contagem nunca"
    echo "       começa e o t0 do laudo cai na regra antiga (setpoint)."
    echo "       Corrija a função para ReadCoils/ReadInputs em Registros Modbus."
  else
    echo "    ⚠️  INICIA_CONTAGEM não está cadastrado em ModbusConfigs."
    echo "       Sem ele o ensaio roda e encerra normalmente, mas o tempo de teste não"
    echo "       aparece na tela e o t0 do laudo usa a regra antiga (setpoint)."
  fi

  echo ""
  echo "    Conferência ao vivo — DUAS formas agora:"
  echo "    1. A própria tela do Ensaio mostra REGISTRO_RODANDO e INICIA_CONTAGEM"
  echo "       (● Ligado / ○ Desligado / ⚠ falha), atualizados pelo monitor do backend."
  echo "       Com a bancada parada, os dois têm que estar ○ Desligado."
  echo "    2. GET https://<host>/api/ensaio/sinais-clp (diagnóstico completo: valor"
  echo "       bruto, tipo, erro e a última leitura do monitor)."
  echo ""
  echo "    Nesta release as transações Modbus passaram a ser SERIALIZADAS por conexão:"
  echo "    era o monitor e o polling da tela dividindo o mesmo socket sem lock que"
  echo "    corrompia as leituras (contagem ligando sozinha, etapa que não encerrava)."

  # Sem o REGISTRO_RODANDO como ReadInputs, a partida da câmara agora ABORTA
  # (o double-check virou bloqueante nesta release).
  RODANDO="$(PGPASSWORD="${POSTGRES_PASSWORD:-}" psql -tAq \
    -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
    -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" \
    -c "SELECT count(*) FROM \"ModbusConfigs\"
        WHERE \"Nome\" = 'REGISTRO_RODANDO' AND \"Ativo\" = true
          AND \"FuncaoModbus\" = 'ReadInputs';" 2>/dev/null || echo "?")"

  if [ "${RODANDO:-0}" -ge 1 ] 2>/dev/null; then
    echo "    ✓ Registro REGISTRO_RODANDO (ReadInputs) cadastrado e ativo."
  else
    echo "    ❌ REGISTRO_RODANDO (ReadInputs) ausente/inativo — NENHUMA câmara vai iniciar."
    echo "       A confirmação de partida virou bloqueante: sem esse registro a etapa é abortada."
  fi

  # ── Release "checklist congela no aceite": pergunta descontinuada ──────────
  # Sem migration nesta release. O que muda no banco é o seed do boot, que retira
  # a pergunta "Estado das conexões e flanges" e apaga as respostas dela.
  echo ""
  echo "==> Conferindo a release do checklist do laudo:"

  psql_um() {
    PGPASSWORD="${POSTGRES_PASSWORD:-}" psql -tAq \
      -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
      -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" \
      -c "$1" 2>/dev/null || echo "?"
  }

  FLANGE_ATIVA="$(psql_um "SELECT count(*) FROM \"CamposRelatorio\"
                           WHERE \"Nome\" = 'Estado das conexões e flanges'
                             AND \"DataExclusao\" IS NULL;")"

  FLANGE_RESP="$(psql_um "SELECT count(*) FROM \"RespostasCampoRelatorio\" r
                          JOIN \"CamposRelatorio\" c ON c.\"Id\" = r.\"CampoRelatorioId\"
                          WHERE c.\"Nome\" = 'Estado das conexões e flanges';")"

  if [ "${FLANGE_ATIVA:-1}" = "0" ] && [ "${FLANGE_RESP:-1}" = "0" ]; then
    echo "    ✓ Pergunta 'Estado das conexões e flanges' retirada do checklist (sem respostas órfãs)."
  else
    echo "    ❌ A pergunta 'Estado das conexões e flanges' ainda está no banco"
    echo "       (ativa: ${FLANGE_ATIVA}, respostas: ${FLANGE_RESP})."
    echo "       O seed do boot não rodou — veja o log do serviço acima procurando"
    echo "       por 'descontinuada'. Enquanto isso ela continua aparecendo no laudo."
  fi

  COL_SOMBRA="$(psql_um "SELECT count(*) FROM information_schema.columns
                         WHERE table_name = 'RespostasCampoRelatorio'
                           AND column_name = 'RelatorioId1';")"

  if [ "${COL_SOMBRA:-1}" = "0" ]; then
    echo "    ✓ Coluna sombra RelatorioId1 removida — o checklist volta a ser lido."
  else
    echo "    ❌ RelatorioId1 ainda existe: a migration CorrigeFkRespostasCampoRelatorio não"
    echo "       aplicou. O checklist continuará abrindo como 'Não respondido'."
  fi

  echo "    -- Respostas de checklist gravadas (por laudo, top 5) --"
  PGPASSWORD="${POSTGRES_PASSWORD:-}" psql \
    -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
    -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" <<'SQL' || \
    echo "    ⚠️  Não deu para listar as respostas."
SELECT r."Numero", count(*) AS respostas
FROM "RespostasCampoRelatorio" resp
JOIN "Relatorios" r ON r."Id" = resp."RelatorioId"
GROUP BY r."Numero" ORDER BY r."Numero" DESC LIMIT 5;
SQL

  echo "    -- Laudos por situação (assinado x rascunho) --"
  PGPASSWORD="${POSTGRES_PASSWORD:-}" psql \
    -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
    -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" <<'SQL' || \
    echo "    ⚠️  Não deu para listar os laudos."
SELECT "Situacao", count(*) AS total, max("Versao") AS maior_versao
FROM "Relatorios" GROUP BY "Situacao" ORDER BY 1;
SQL

  echo ""
  echo "    Teste de bancada desta release (na tela do laudo):"
  echo "    1. Abrir um laudo JÁ ASSINADO — o checklist tem que MOSTRAR as respostas"
  echo "       (✔ Sim / ✕ Não, em texto) e não deve haver nada clicável."
  echo "    2. Baixar o PDF várias vezes seguidas: o botão não pode mais desabilitar"
  echo "       sozinho, e as respostas do checklist têm que sair impressas."
  echo "    3. 'Editar (gera vN+1)' → mudar uma resposta → 'Concluir / Assinar':"
  echo "       o Histórico de Versões ganha 'Reaberto' e depois 'Concluido' na v seguinte."

  # ── Release "ensaio de câmara única" ──────────────────────────────────────
  echo ""
  echo "==> Conferindo a release de câmara única:"

  COL_CAMARAS="$(psql_um "SELECT count(*) FROM information_schema.columns
                          WHERE table_name = 'Ensaios'
                            AND column_name IN ('CamaraAHabilitada','CamaraBHabilitada');")"

  # Ensaio aberto sem NENHUMA câmara marcada não deveria existir: seria um ensaio
  # que nunca pode ser aceito. Se aparecer, o default da migration entrou errado.
  SEM_CAMARA="$(psql_um "SELECT count(*) FROM \"Ensaios\"
                         WHERE \"CamaraAHabilitada\" = false
                           AND \"CamaraBHabilitada\" = false;" 2>/dev/null)"

  if [ "${COL_CAMARAS:-0}" = "2" ]; then
    echo "    ✓ Ensaios.CamaraAHabilitada/CamaraBHabilitada criadas pela migration."
    if [ "${SEM_CAMARA:-0}" = "0" ]; then
      echo "    ✓ Nenhum ensaio ficou sem câmara habilitada (default TRUE aplicado)."
    else
      echo "    ❌ $SEM_CAMARA ensaio(s) com as DUAS câmaras desmarcadas — esses não podem"
      echo "       ser aceitos. Corrija com:"
      echo "       UPDATE \"Ensaios\" SET \"CamaraAHabilitada\"=true, \"CamaraBHabilitada\"=true"
      echo "       WHERE \"CamaraAHabilitada\"=false AND \"CamaraBHabilitada\"=false;"
    fi
  else
    echo "    ❌ As colunas de câmara habilitada NÃO estão no banco (encontradas: ${COL_CAMARAS} de 2)."
    echo "       A migration AddCamarasHabilitadasEnsaio não aplicou — veja o log acima."
  fi

  echo ""
  echo "    Teste de bancada do ensaio de câmara única:"
  echo "    1. Novo ensaio: as duas câmaras entram MARCADAS."
  echo "    2. Desmarcar a B (sem ter rodado) → some o botão de iniciar dela e o aceite"
  echo "       libera só com a A concluída."
  echo "    3. Aceitar → sobe o modal 'Fechar o laudo com uma câmara só?'; o laudo sai"
  echo "       com 'Câmaras Testadas: A' e o critério citando só a câmara A."
  echo "    4. PDF do laudo: NÃO pode mais ter a seção 'Histórico de Versões'."
fi

# ── (--reset-admin) Força admin/admin no banco ──────────────────────────────
if [ "$RESET_ADMIN" = "1" ]; then
  echo "==> --reset-admin: garantindo admin/admin no banco..."
  if ! command -v psql >/dev/null 2>&1; then
    echo "⚠️  psql não encontrado na VM — não deu para resetar o admin automaticamente."
    echo "    Instale o cliente psql ou rode o SQL de db/migrations.sql/manual à mão."
  else
    # Lê só as credenciais do Postgres do env (via sudo, pois o arquivo é protegido).
    TMPENV="$(mktemp)"
    sudo grep -E '^(POSTGRES_HOST|POSTGRES_PORT|POSTGRES_USER|POSTGRES_DATABASE|POSTGRES_PASSWORD)=' "$ENV_FILE" > "$TMPENV" 2>/dev/null || true
    set -a; . "$TMPENV"; set +a
    rm -f "$TMPENV"

    # Hash BCrypt válido para a senha "admin" (verificado com BCrypt.Net-Next).
    ADMIN_HASH='$2b$12$vppjdQS7bYEZRCa70f4x/.AakEg5Ax0IH9mWWp3TeM4YWKS2IS78a'

    if PGPASSWORD="${POSTGRES_PASSWORD:-}" psql \
         -h "${POSTGRES_HOST:-localhost}" -p "${POSTGRES_PORT:-5432}" \
         -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DATABASE:-datamais}" \
         -v ON_ERROR_STOP=1 -v adminhash="$ADMIN_HASH" <<'SQL'
INSERT INTO "Usuarios" ("Nome","Email","SenhaHash","Role","Ativo","DataCriacao")
VALUES ('Administrador','admin', :'adminhash', 'Admin', true, now())
ON CONFLICT ("Email") DO UPDATE
  SET "SenhaHash" = EXCLUDED."SenhaHash", "Role" = 'Admin', "Ativo" = true;
SQL
    then
      echo "✓ admin/admin garantido (usuário: admin / senha: admin). TROQUE A SENHA depois."
    else
      echo "⚠️  Falha ao resetar admin via psql — cheque as credenciais em $ENV_FILE."
    fi
  fi
fi

echo ""
echo "✅ Deploy concluído."
