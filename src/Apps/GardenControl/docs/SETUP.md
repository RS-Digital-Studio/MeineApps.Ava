# GardenControl - Raspberry Pi Einrichtung

## Voraussetzungen

- Raspberry Pi Zero 2 W (oder Pi 3B+/4/5)
- MicroSD-Karte mit Raspberry Pi OS Lite (64-bit)
- WLAN-Zugang konfiguriert (über Raspberry Pi Imager)
- SSH aktiviert

## 1. Raspberry Pi OS installieren

1. Raspberry Pi Imager herunterladen
2. "Raspberry Pi OS Lite (64-bit)" wählen
3. Einstellungen (Zahnrad):
   - Hostname: `gardencontrol`
   - SSH aktivieren
   - WLAN: SSID + Passwort eintragen
   - Benutzername: `pi`, Passwort setzen
4. Auf SD-Karte schreiben

## 2. Server bauen (auf dem Windows-PC)

```bash
# Im Projektverzeichnis
cd F:\Meine_Apps_Ava

# Für Raspberry Pi Zero 2 W (ARM64)
dotnet publish src/Apps/GardenControl/GardenControl.Server -c Release -r linux-arm64 --self-contained

# Für ältere Pi (ARM32, z.B. Pi Zero 1)
# dotnet publish src/Apps/GardenControl/GardenControl.Server -c Release -r linux-arm --self-contained
```

Die Ausgabe liegt in:
`src/Apps/GardenControl/GardenControl.Server/bin/Release/net10.0/linux-arm64/publish/`

## 3. Auf den Pi kopieren

```bash
# Per SCP (von Windows PowerShell)
scp -r src/Apps/GardenControl/GardenControl.Server/bin/Release/net10.0/linux-arm64/publish/* pi@gardencontrol.local:~/gardencontrol/

# Install-Skript auch kopieren
scp src/Apps/GardenControl/GardenControl.Server/Install/* pi@gardencontrol.local:~/
```

## 4. Auf dem Pi installieren

```bash
ssh pi@gardencontrol.local

# Install-Skript ausführen
sudo bash install.sh

# Nach Installation: Neustart (für I2C-Aktivierung)
sudo reboot
```

## 5. Prüfen

```bash
ssh pi@gardencontrol.local

# Service-Status
systemctl status gardencontrol

# Logs anzeigen (live)
journalctl -u gardencontrol -f

# I2C prüfen (ADS1115 sollte auf 0x48 erscheinen)
i2cdetect -y 1

# API testen
curl http://localhost:5000/api/status
```

## 6. Desktop-App starten

```bash
cd F:\Meine_Apps_Ava
dotnet run --project src/Apps/GardenControl/GardenControl.Desktop
```

In der App: Server-URL auf `http://gardencontrol.local:5000` setzen (oder IP-Adresse des Pi).

## 7. Android-App

```bash
dotnet build src/Apps/GardenControl/GardenControl.Android
# APK auf Android-Gerät installieren
```

---

## Updates deployen

```bash
# Auf dem PC bauen
dotnet publish src/Apps/GardenControl/GardenControl.Server -c Release -r linux-arm64 --self-contained

# Auf den Pi kopieren
scp -r src/Apps/GardenControl/GardenControl.Server/bin/Release/net10.0/linux-arm64/publish/* pi@gardencontrol.local:~/gardencontrol/

# Service neustarten
ssh pi@gardencontrol.local "sudo systemctl restart gardencontrol"
```

## Fehlerbehebung

| Problem | Lösung |
|---------|--------|
| I2C nicht erkannt | `sudo raspi-config` → Interface Options → I2C → Enable |
| ADS1115 nicht auf 0x48 | Verkabelung prüfen: SDA→GPIO2, SCL→GPIO3, VCC→3.3V, GND→GND |
| Relais schaltet nicht | GPIO-Test: `pinctrl set 17 op dh` (GPIO 17 HIGH setzen) |
| Server startet nicht | Logs: `journalctl -u gardencontrol -e` |
| App verbindet nicht | Pi-IP prüfen: `hostname -I` auf dem Pi |
| Pumpe zu schwach | Förderhöhe prüfen, Schlauch-Querschnitt vergrößern |
