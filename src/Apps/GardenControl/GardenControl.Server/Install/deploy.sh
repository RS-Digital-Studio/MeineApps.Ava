#!/bin/bash
# ═══════════════════════════════════════════════════════════════════
# GardenControl - Build + Deploy auf den Raspberry Pi
#
# Dieses Skript wird auf dem WINDOWS-PC (in Git Bash/WSL) ausgeführt!
# Es baut Server + Desktop-App für ARM64 und kopiert alles auf den Pi.
#
# Aufruf: bash deploy.sh [pi-hostname]
# Beispiel: bash deploy.sh gardencontrol.local
#           bash deploy.sh 192.168.178.56
# ═══════════════════════════════════════════════════════════════════

set -e

PI_HOST="${1:-gardencontrol.local}"
PI_USER="pi"
PROJECT_ROOT="$(cd "$(dirname "$0")/../../../.." && pwd)"

echo ""
echo "═══════════════════════════════════════════"
echo "  GardenControl Deploy → $PI_HOST"
echo "═══════════════════════════════════════════"
echo ""

# 1. Server bauen
echo "[1/4] Server bauen (linux-arm64)..."
dotnet publish "$PROJECT_ROOT/src/Apps/GardenControl/GardenControl.Server" \
    -c Release -r linux-arm64 --self-contained -o "$PROJECT_ROOT/deploy/server-publish"

# 2. Desktop-App bauen
echo "[2/4] Desktop-App bauen (linux-arm64)..."
dotnet publish "$PROJECT_ROOT/src/Apps/GardenControl/GardenControl.Desktop" \
    -c Release -r linux-arm64 --self-contained -o "$PROJECT_ROOT/deploy/desktop-publish"

# 3. Auf den Pi kopieren
echo "[3/4] Dateien auf Pi kopieren..."
scp -r "$PROJECT_ROOT/deploy/server-publish/"* $PI_USER@$PI_HOST:~/gardencontrol/
scp -r "$PROJECT_ROOT/deploy/desktop-publish/"* $PI_USER@$PI_HOST:~/gardencontrol-app/

# Install-Skript kopieren
scp "$PROJECT_ROOT/src/Apps/GardenControl/GardenControl.Server/Install/install-pi5.sh" $PI_USER@$PI_HOST:~/

# 4. Services neustarten
echo "[4/4] Services neustarten..."
ssh $PI_USER@$PI_HOST "sudo systemctl restart gardencontrol 2>/dev/null || true"
ssh $PI_USER@$PI_HOST "killall GardenControl.Desktop 2>/dev/null; nohup ~/gardencontrol-app/GardenControl.Desktop &>/dev/null &"

echo ""
echo "Deploy abgeschlossen!"
echo "  Server: http://$PI_HOST:5000"
echo "  App läuft auf dem Pi-Display"
echo ""
