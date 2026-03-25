#!/bin/bash
# GardenControl - Installations-Skript für Raspberry Pi
# Voraussetzung: Raspberry Pi OS (Lite reicht), .NET 10 Runtime
#
# Aufruf: sudo bash install.sh

set -e

APP_DIR="/home/pi/gardencontrol"
SERVICE_FILE="gardencontrol.service"
USER="pi"

echo "=== GardenControl Installation ==="

# 1. I2C aktivieren (für ADS1115)
echo "I2C aktivieren..."
if ! grep -q "^dtparam=i2c_arm=on" /boot/config.txt 2>/dev/null; then
    echo "dtparam=i2c_arm=on" >> /boot/config.txt
    echo "  I2C in /boot/config.txt aktiviert (Neustart erforderlich)"
fi

# I2C-Tools installieren
apt-get install -y i2c-tools

# User zur I2C-Gruppe hinzufügen
usermod -aG i2c $USER 2>/dev/null || true
usermod -aG gpio $USER 2>/dev/null || true

# 2. .NET Runtime prüfen
if ! command -v dotnet &> /dev/null; then
    echo ".NET Runtime installieren..."
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --runtime aspnetcore
    echo 'export DOTNET_ROOT=$HOME/.dotnet' >> /home/$USER/.bashrc
    echo 'export PATH=$PATH:$DOTNET_ROOT' >> /home/$USER/.bashrc
fi

# 3. Anwendungsverzeichnis
echo "Anwendungsverzeichnis erstellen..."
mkdir -p $APP_DIR
chown $USER:$USER $APP_DIR

# 4. Dateien kopieren (muss vorher per scp/rsync auf den Pi kopiert werden)
if [ -d "./publish" ]; then
    cp -r ./publish/* $APP_DIR/
    chown -R $USER:$USER $APP_DIR
    echo "  Dateien kopiert"
else
    echo "  HINWEIS: ./publish Verzeichnis nicht gefunden."
    echo "  Bitte zuerst auf dem Entwicklungs-PC bauen:"
    echo "    dotnet publish GardenControl.Server -c Release -r linux-arm64 --self-contained"
    echo "  Dann den publish-Ordner per SCP auf den Pi kopieren:"
    echo "    scp -r bin/Release/net10.0/linux-arm64/publish/ pi@<IP>:~/gardencontrol/"
fi

# 5. Systemd-Service installieren
echo "Systemd-Service installieren..."
cp $SERVICE_FILE /etc/systemd/system/
systemctl daemon-reload
systemctl enable gardencontrol
systemctl start gardencontrol

echo ""
echo "=== Installation abgeschlossen ==="
echo "  Service-Status: systemctl status gardencontrol"
echo "  Logs:           journalctl -u gardencontrol -f"
echo "  Neustart:       systemctl restart gardencontrol"
echo ""
echo "  I2C-Test:       i2cdetect -y 1  (ADS1115 sollte auf 0x48 erscheinen)"
echo ""
echo "  WICHTIG: Bei erstmaliger I2C-Aktivierung ist ein Neustart nötig!"
echo "           sudo reboot"
