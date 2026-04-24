#!/usr/bin/env bash
#
# BingXBot Server — Installations-Skript fuer Raspberry Pi 5 (Ubuntu 24.04 / Debian 12 / Raspberry Pi OS)
#
# Voraussetzungen:
#  - Self-contained Publish hat BingXBot.Server + alle Abhaengigkeiten in ./publish/ erstellt
#    (siehe `publish.sh` oder `dotnet publish -c Release -r linux-arm64 --self-contained true`)
#  - SSH-Zugriff auf den Pi mit sudo-Rechten
#
# Verwendung:
#   ./install.sh <pi-host>        z.B. ./install.sh raspberrypi.local
#   ./install.sh 192.168.1.42
#
set -euo pipefail

PI_HOST="${1:-raspberrypi.local}"
PI_USER="${PI_USER:-pi}"
INSTALL_DIR="/opt/bingxbot"
DATA_DIR="/var/lib/bingxbot"
SERVICE_USER="bingxbot"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$SCRIPT_DIR/../bin/Release/net10.0/linux-arm64/publish"

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "FEHLER: Publish-Ordner nicht gefunden: $PUBLISH_DIR"
    echo "Fuehre zuerst 'publish.sh' oder 'dotnet publish -c Release -r linux-arm64 --self-contained true' aus."
    exit 1
fi

echo "=== BingXBot-Server Installation auf $PI_USER@$PI_HOST ==="

# 1. Service-User + Verzeichnisse anlegen (falls noch nicht vorhanden)
ssh "$PI_USER@$PI_HOST" "sudo bash -s" <<EOF
set -e
id -u $SERVICE_USER >/dev/null 2>&1 || sudo useradd --system --no-create-home --shell /usr/sbin/nologin $SERVICE_USER
sudo mkdir -p $INSTALL_DIR $DATA_DIR
sudo chown -R $SERVICE_USER:$SERVICE_USER $DATA_DIR
EOF

# 2. Server-Binaries kopieren (rsync: inkrementell + schnell)
echo "=== Kopiere Server-Binaries nach $INSTALL_DIR ==="
rsync -avz --delete \
    --rsync-path="sudo rsync" \
    "$PUBLISH_DIR/" \
    "$PI_USER@$PI_HOST:$INSTALL_DIR/"

# 3. systemd-Unit installieren
echo "=== Installiere systemd-Unit ==="
scp "$SCRIPT_DIR/bingxbot.service" "$PI_USER@$PI_HOST:/tmp/bingxbot.service"
ssh "$PI_USER@$PI_HOST" "sudo bash -s" <<EOF
set -e
sudo mv /tmp/bingxbot.service /etc/systemd/system/bingxbot.service
sudo chown root:root /etc/systemd/system/bingxbot.service
sudo chmod 644 /etc/systemd/system/bingxbot.service
sudo chown -R $SERVICE_USER:$SERVICE_USER $INSTALL_DIR
sudo chmod +x $INSTALL_DIR/BingXBot.Server
sudo systemctl daemon-reload
sudo systemctl enable bingxbot.service
sudo systemctl restart bingxbot.service
sleep 2
sudo systemctl status bingxbot.service --no-pager
EOF

echo ""
echo "=== Installation abgeschlossen ==="
echo "Logs ansehen:   ssh $PI_USER@$PI_HOST 'sudo journalctl -u bingxbot -f'"
echo "Pairing-Code:   ssh $PI_USER@$PI_HOST 'sudo cat $DATA_DIR/pairing-code.txt'"
echo "Health-Check:   curl http://$PI_HOST:5050/api/v1/health"
