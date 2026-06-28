#!/usr/bin/env bash
set -euo pipefail

STACK_NAME="isa"
REGISTRY="registry.example.com:5000"
REPO_AGENT="2154/isa_agent"
REPO_WEB="2154/isa_web"
TAG="${1:-}"
COMPOSE_TEMPLATE="$HOME/deploy/docker-compose-stack-isa.yml"
TMP_COMPOSE="/tmp/tmp-compose-${STACK_NAME}.yml"
LOG_FILE="/mnt/nfs/isa/deploy.log"
CFG_DIR="/mnt/nfs/isa/config"
WF_DIR="/mnt/nfs/isa/workflows"
HISTORY_JSON="$CFG_DIR/history.json"
DEPLOY_TYPE="Swarm"
MAILJET_APIKEY="***REMOVED***"
MAILJET_SECRET="***REMOVED***"

log()  { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }
fail() { log "❌ $*"; exit 1; }

# Ensure dir
sudo mkdir -p /mnt/nfs/isa/{config,workflows} || true
sudo chown -R ubuntu:ubuntu /mnt/nfs/isa || true

# Ensure overlay network
(docker network inspect web >/dev/null 2>&1) || docker network create -d overlay web >/dev/null

# Ensure history file
[[ -f "$HISTORY_JSON" ]] || echo '[]' > "$HISTORY_JSON"

# Ensure database file exists (will be created by migrations if not)
DB_FILE="$CFG_DIR/agent.db"
if [[ ! -f "$DB_FILE" ]]; then
    log "📊 Database file does not exist, will be created by migrations"
    touch "$DB_FILE"
    chmod 664 "$DB_FILE"
else
    log "📊 Database file exists: $DB_FILE"
fi
# Require explicit TAG (predictable deploys)
[[ -z "$TAG" ]] && fail "TAG missing. Usage: $0 <YYYY.MM.DD-HHMM>"

AGENT_IMAGE="${REGISTRY}/${REPO_AGENT}:${TAG}"
WEB_IMAGE="${REGISTRY}/${REPO_WEB}:${TAG}"

log "⬇️ Pull images to verify they exist: $AGENT_IMAGE and $WEB_IMAGE"
docker pull "$AGENT_IMAGE" >/dev/null 2>&1 || fail "API image not found: $AGENT_IMAGE"
docker pull "$WEB_IMAGE"  >/dev/null 2>&1 || fail "WEB image not found: $WEB_IMAGE"

[[ -f "$COMPOSE_TEMPLATE" ]] || fail "Compose template not found: $COMPOSE_TEMPLATE"

log "⚙️ Generate temp compose from template"
sed "s|__AGENT_IMAGE__|$AGENT_IMAGE|g; s|__WEB_IMAGE__|$WEB_IMAGE|g" "$COMPOSE_TEMPLATE" > "$TMP_COMPOSE"

# Remove swarm-inkompatibla restart-rader
sed -i "/restart:[[:space:]]\+unless-stopped/d" "$TMP_COMPOSE" || true

log "🚀 Deploy stack $STACK_NAME (TAG=$TAG)"
docker stack deploy -c "$TMP_COMPOSE" "$STACK_NAME" --with-registry-auth

log "🔄 Force update services to use new images"
docker service update --image "registry.example.com:5000/2154/isa_agent:$TAG" "isa_agent" --force
docker service update --image "registry.example.com:5000/2154/isa_web:$TAG" "isa_web" --force
log "⏳ Wait for services to be 1/1"
AGENT_OK=""; WEB_OK=""
for i in {1..60}; do
  AGENT_OK="$(docker service ls --format '{{.Name}} {{.Replicas}}' | awk -v svc="isa_agent" '$1==svc {print $2}')"
  WEB_OK="$(docker service ls --format '{{.Name}} {{.Replicas}}' | awk -v svc="isa_web" '$1==svc {print $2}')"
  if [[ "$AGENT_OK" == "1/1" && "$WEB_OK" == "1/1" ]]; then
    log "✅ Up: agent=$AGENT_OK web=$WEB_OK"
    break
  fi
  sleep 2
done

if [[ "$AGENT_OK" != "1/1" || "$WEB_OK" != "1/1" ]]; then
  log "⚠️ Services did not reach 1/1 within timeout. Current status:"
  docker service ls | grep "^${STACK_NAME}_" || true
  log "📋 Details:"; docker service ps "isa_agent" || true; docker service ps "isa_web" || true
  fail "Deploy finished but services not in desired state."
fi

rm -f "$TMP_COMPOSE" 2>/dev/null || true
TIMESTAMP="$(date '+%Y-%m-%d %H:%M:%S')"
mkdir -p "$(dirname "$LOG_FILE")" || true
echo "$TIMESTAMP - Deployed $STACK_NAME TAG=$TAG AGENT=$AGENT_IMAGE WEB=$WEB_IMAGE" >> "$LOG_FILE"
log "📘 Log saved: $LOG_FILE"

# === 🗑️ Docker cleanup === (omskrivet)
log "🧹 Rensar Docker system (images/volumes/containers/networks äldre än 24h där möjligt)..."
docker image prune -af --filter "until=24h" > /dev/null 2>&1 || true
docker volume prune -f > /dev/null 2>&1 || true
docker container prune -f > /dev/null 2>&1 || true
docker network prune -f > /dev/null 2>&1 || true

# === 📧 Notifiering ===
if [[ -n "${MAILJET_APIKEY:-}" && -n "${MAILJET_SECRET:-}" && "${MAILJET_APIKEY}" != "" && "${MAILJET_SECRET}" != "" ]]; then
  log "📧 Skickar notifiering via Mailjet..."
  set +e
  # Create JSON payload with proper escaping
  JSON_PAYLOAD=$(cat <<EOF
{
  "Messages":[{
    "From":{"Email":"deploy@example.com","Name":"CI/CD"},
    "To":[{"Email":"demo@example.com"}],
    "Subject":"🎭 ISA Deploy klar: ${STACK_NAME} (${TAG}) - ${DEPLOY_TYPE}",
    "TextPart":"ISA deploy slutförd till stack ${STACK_NAME}\nVersion: ${TAG}\nTyp: ${DEPLOY_TYPE}\nTid: ${TIMESTAMP}\n\nAGENT Image: ${AGENT_IMAGE}\nWeb Image: ${WEB_IMAGE}\n\nHealth: curl http://your-domain/health"
  }]
}
EOF
)

  EMAIL_RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/mailjet.out -X POST "https://api.mailjet.com/v3.1/send" \
    -H "Content-Type: application/json" \
    -u "$MAILJET_APIKEY:$MAILJET_SECRET" \
    -d "$JSON_PAYLOAD" )
  if [[ "$EMAIL_RESPONSE" == "200" ]]; then
  set -euo pipefail
    log "✅ E-post skickad!"
  else
    log "⚠️ Misslyckades att skicka e-post (status: $EMAIL_RESPONSE) - fortsätter ändå"
    cat /tmp/mailjet.out || true
  fi
else
  log "ℹ️ Skipping email notification (MAILJET_APIKEY/MAILJET_SECRET empty or not set)."
fi

# 🧼 Rensa registry (om script finns)
if [[ -x "$HOME/deploy/clean-registry.sh" ]]; then
  log "🧼 Kör clean-registry.sh ..."
  bash "$HOME/deploy/clean-registry.sh" || log "⚠️ Registry cleanup misslyckades"
else
  log "ℹ️ clean-registry.sh saknas, hoppar över registry-städning."
fi

log ""
log "🎉 ISA deployment slutförd!"
log "📋 Nästa steg:"
log "   1. Kontrollera services: docker stack ps $STACK_NAME"
log "   2. Testa AGENT Health: curl http://agent.example.com/health"
log "   3. Övervaka loggar: docker service logs isa_agent"

