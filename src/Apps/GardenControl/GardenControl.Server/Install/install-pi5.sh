#!/bin/bash
# ═══════════════════════════════════════════════════════════════════
# GardenControl - Komplettes Setup für Raspberry Pi 5 mit Display
#
# Dieses Skript richtet den Pi als Kiosk-Station ein:
# - GardenControl Server (systemd, immer an)
# - GardenControl Desktop App (Fullscreen auf dem 7"-Display)
# - Auto-Login + Auto-Start beim Booten
# - I2C aktivieren (für ADS1115 ADC)
#
# Voraussetzung: Raspberry Pi OS (Desktop-Version, 64-bit) installiert
#
# Aufruf: sudo bash install-pi5.sh
# ═══════════════════════════════════════════════════════════════════

set -e

APP_DIR="/home/pi/gardencontrol"
USER="pi"
DISPLAY_APP_DIR="/home/pi/gardencontrol-app"

echo ""
echo "═══════════════════════════════════════════"
echo "  GardenControl - Pi 5 Kiosk Installation"
echo "═══════════════════════════════════════════"
echo ""

# ──────────────────────────────────────
# 1. System-Pakete installieren
# ──────────────────────────────────────
echo "[1/7] System-Pakete installieren..."
apt-get update -qq
apt-get install -y -qq i2c-tools libgbm1 libdrm2 libinput10 mesa-utils > /dev/null

# I2C aktivieren
if ! grep -q "^dtparam=i2c_arm=on" /boot/firmware/config.txt 2>/dev/null; then
    echo "dtparam=i2c_arm=on" >> /boot/firmware/config.txt
    echo "  I2C aktiviert (Neustart erforderlich)"
fi

# User zu Gruppen hinzufügen
usermod -aG i2c,gpio,video $USER 2>/dev/null || true

# ──────────────────────────────────────
# 2. .NET 10 Runtime installieren
# ──────────────────────────────────────
echo "[2/7] .NET Runtime prüfen..."
if ! su - $USER -c "dotnet --info" &>/dev/null; then
    echo "  .NET 10 Runtime installieren..."
    su - $USER -c "curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --runtime aspnetcore"
    su - $USER -c "curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --runtime dotnet"

    # PATH permanent setzen
    if ! grep -q "DOTNET_ROOT" /home/$USER/.bashrc; then
        echo 'export DOTNET_ROOT=$HOME/.dotnet' >> /home/$USER/.bashrc
        echo 'export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools' >> /home/$USER/.bashrc
    fi
    echo "  .NET 10 installiert"
else
    echo "  .NET bereits installiert"
fi

# ──────────────────────────────────────
# 3. Server installieren
# ──────────────────────────────────────
echo "[3/7] Server einrichten..."
mkdir -p $APP_DIR
chown $USER:$USER $APP_DIR

if [ -d "./server-publish" ]; then
    cp -r ./server-publish/* $APP_DIR/
    chmod +x $APP_DIR/GardenControl.Server
    chown -R $USER:$USER $APP_DIR
    echo "  Server-Dateien kopiert"
else
    echo "  WARNUNG: ./server-publish nicht gefunden!"
    echo "  Bitte zuerst auf dem PC bauen:"
    echo "    dotnet publish GardenControl.Server -c Release -r linux-arm64 --self-contained"
fi

# Server systemd Service
cat > /etc/systemd/system/gardencontrol.service << 'SVCEOF'
[Unit]
Description=GardenControl Bewässerungssteuerung Server
After=network.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi/gardencontrol
ExecStart=/home/pi/gardencontrol/GardenControl.Server
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_RUNNING_IN_CONTAINER=false

[Install]
WantedBy=multi-user.target
SVCEOF

systemctl daemon-reload
systemctl enable gardencontrol
echo "  Server-Service installiert"

# ──────────────────────────────────────
# 4. Desktop-App installieren
# ──────────────────────────────────────
echo "[4/7] Desktop-App einrichten..."
mkdir -p $DISPLAY_APP_DIR
chown $USER:$USER $DISPLAY_APP_DIR

if [ -d "./desktop-publish" ]; then
    cp -r ./desktop-publish/* $DISPLAY_APP_DIR/
    chmod +x $DISPLAY_APP_DIR/GardenControl.Desktop
    chown -R $USER:$USER $DISPLAY_APP_DIR
    echo "  Desktop-App-Dateien kopiert"
else
    echo "  WARNUNG: ./desktop-publish nicht gefunden!"
    echo "  Bitte zuerst auf dem PC bauen:"
    echo "    dotnet publish GardenControl.Desktop -c Release -r linux-arm64 --self-contained"
fi

# ──────────────────────────────────────
# 5. Auto-Login konfigurieren
# ──────────────────────────────────────
echo "[5/7] Auto-Login konfigurieren..."
# B4 = Desktop Auto-Login
raspi-config nonint do_boot_behaviour B4 2>/dev/null || true
echo "  Auto-Login für User '$USER' aktiviert"

# ──────────────────────────────────────
# 6. Kiosk-Autostart einrichten
# ──────────────────────────────────────
echo "[6/7] Kiosk-Autostart einrichten..."

# Autostart-Verzeichnis
mkdir -p /home/$USER/.config/autostart
chown -R $USER:$USER /home/$USER/.config

# Desktop-Entry für Auto-Start
cat > /home/$USER/.config/autostart/gardencontrol.desktop << 'DTEOF'
[Desktop Entry]
Type=Application
Name=GardenControl
Comment=Bewässerungssteuerung
Exec=/home/pi/gardencontrol-app/GardenControl.Desktop
Terminal=false
X-GNOME-Autostart-enabled=true
StartupNotify=false
DTEOF
chown $USER:$USER /home/$USER/.config/autostart/gardencontrol.desktop

# Bildschirmschoner deaktivieren (Display bleibt an)
mkdir -p /home/$USER/.config/lxsession/LXDE-pi 2>/dev/null || true
cat > /home/$USER/.config/lxsession/LXDE-pi/autostart 2>/dev/null << 'LXEOF' || true
@xset s off
@xset -dpms
@xset s noblank
LXEOF

# Wayland/labwc: Bildschirmschoner deaktivieren
mkdir -p /home/$USER/.config/labwc 2>/dev/null || true
if [ -f /home/$USER/.config/labwc/autostart ]; then
    if ! grep -q "gardencontrol" /home/$USER/.config/labwc/autostart; then
        echo "/home/pi/gardencontrol-app/GardenControl.Desktop &" >> /home/$USER/.config/labwc/autostart
    fi
fi

echo "  Autostart konfiguriert"

# ──────────────────────────────────────
# 7. Display-Einstellungen
# ──────────────────────────────────────
echo "[7/7] Display-Einstellungen..."

# Display-Rotation falls nötig (0=normal, 1=90°, 2=180°, 3=270°)
# Auskommentiert - bei Bedarf aktivieren:
# echo "display_rotate=0" >> /boot/firmware/config.txt

# Cursor ausblenden (Kiosk-Modus)
apt-get install -y -qq unclutter > /dev/null 2>&1 || true

echo ""
echo "═══════════════════════════════════════════"
echo "  Installation abgeschlossen!"
echo "═══════════════════════════════════════════"
echo ""
echo "  Server-Status:    systemctl status gardencontrol"
echo "  Server-Logs:      journalctl -u gardencontrol -f"
echo "  I2C prüfen:       i2cdetect -y 1"
echo "  API testen:       curl http://localhost:5000/api/status"
echo ""
echo "  Nach dem Neustart:"
echo "  - Server startet automatisch als systemd-Service"
echo "  - Desktop-App startet automatisch in Fullscreen"
echo "  - Touch-Bedienung auf dem 7\"-Display"
echo "  - Handy/PC können sich über WLAN verbinden"
echo ""
echo "  NEUSTART ERFORDERLICH: sudo reboot"
echo ""
