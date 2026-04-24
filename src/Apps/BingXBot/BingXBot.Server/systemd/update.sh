#!/usr/bin/env bash
# Schnelles Update eines bereits installierten BingXBot-Servers auf dem Pi.
# Uebertraegt das publish-Verzeichnis via tar-Stream (kein rsync noetig, laeuft auch auf Git-Bash Windows)
# und startet den Service neu.
set -euo pipefail

PI_HOST="${1:-raspberrypi.local}"
PI_USER="${PI_USER:-steuerung}"
INSTALL_DIR="${INSTALL_DIR:-/home/steuerung/bingxbot}"
SERVICE_USER="${SERVICE_USER:-steuerung}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$SCRIPT_DIR/../bin/Release/net10.0/linux-arm64/publish"

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "FEHLER: Publish-Ordner nicht gefunden. Fuehre publish.sh aus."
    exit 1
fi

echo "=== Update BingXBot-Server auf $PI_USER@$PI_HOST -> $INSTALL_DIR ==="

ssh "$PI_USER@$PI_HOST" "sudo systemctl stop bingxbot.service"

(cd "$PUBLISH_DIR" && tar -cf - .) | ssh "$PI_USER@$PI_HOST" \
    "sudo rm -rf $INSTALL_DIR/* $INSTALL_DIR/.??* 2>/dev/null || true; \
     sudo tar -xf - -C $INSTALL_DIR && \
     sudo chown -R $SERVICE_USER:$SERVICE_USER $INSTALL_DIR && \
     sudo chmod +x $INSTALL_DIR/BingXBot.Server"

ssh "$PI_USER@$PI_HOST" \
    "sudo systemctl start bingxbot.service && sleep 3 && sudo systemctl is-active bingxbot.service && sudo systemctl status bingxbot.service --no-pager | head -10"

echo "=== Update fertig ==="
