---
name: server-deploy
description: Deployt BingXBot.Server/GardenControl.Server auf den Raspberry Pi via scp + unzip + systemctl. Bezieht Release-ZIP aus Releases-Ordner.
user-invocable: true
allowed-tools: Bash, Read, Grep, Glob
argument-hint: "<AppName> [Version]"
---

# Server-Deploy auf Pi (BingXBot-Flow)

Deployt die Server-Komponente per scp + unzip + systemctl-Restart. Nutzt vorgebaute Release-ZIPs aus dem Releases-Ordner.

**WICHTIG**: Produktions-Deploy. IMMER User-Bestaetigung abwarten BEVOR scp laeuft. NIEMALS ohne expliziten Auftrag deployen.

## Argumente parsen
- Erstes Wort: App-Name (BingXBot, GardenControl)
- Zweites Wort (optional): Version (z.B. `v1.1.0`), Default = hoechste im Releases-Ordner

## Konstanten (BingXBot-Referenz)
```
SSH-Ziel:          steuerung@raspberrypi.local
Pi-User:           steuerung
Binaries-Pfad:     /home/steuerung/bingxbot
Daten-Pfad:        /var/lib/bingxbot (bleibt unberuehrt!)
Release-Ordner:    F:\Meine_Apps_Ava\Releases\{App}\v{V}\
Release-ZIP:       {App}-Server-Pi-linux-arm64-v{V}.zip
systemd-Unit:      bingxbot (bzw. {app}-lowercase)
API-Port:          5050
```

Falls GardenControl: analog mit angepassten Pfaden (app-lowercase).

## Vorgehen

### 1. Version validieren
```bash
ls F:/Meine_Apps_Ava/Releases/{App}/
```
→ Wenn Version-Argument fehlt: hoechste v*.* nehmen.

### 2. Release-ZIP pruefen
```bash
test -f F:/Meine_Apps_Ava/Releases/{App}/{Version}/{App}-Server-Pi-linux-arm64-{Version}.zip
```
→ Wenn nicht vorhanden: STOPP mit Hinweis "Build erst (oder Version falsch)".

### 3. Uncommitted Changes
`git status --porcelain` → Warnen bei aenderungen, nicht blockieren.

### 4. User-Bestaetigung einholen
```
Deploy-Plan:
- App: {App}
- Version: {Version}
- ZIP: {Pfad} ({Groesse} MB)
- Ziel: steuerung@raspberrypi.local
- Letzter Commit: {hash} "{msg}"
- Uncommitted Changes: {JA/NEIN}
- Daten auf Pi (bot.db, credentials.bin) bleiben unberuehrt

Fortfahren? (Ja/Nein)
```

### 5. Transfer (scp)
```bash
scp F:/Meine_Apps_Ava/Releases/{App}/{Version}/{App}-Server-Pi-linux-arm64-{Version}.zip \
    steuerung@raspberrypi.local:/tmp/
```

### 6. Install (SSH-One-Liner)
```bash
ssh steuerung@raspberrypi.local "sudo systemctl stop bingxbot ; \
    cd ~ && rm -rf bingxbot && \
    unzip -q /tmp/{App}-Server-Pi-linux-arm64-{Version}.zip -d bingxbot && \
    sudo chmod +x /home/steuerung/bingxbot/{App}.Server && \
    sudo chown -R steuerung:steuerung /home/steuerung/bingxbot && \
    sudo systemctl reset-failed bingxbot && \
    sudo systemctl start bingxbot"
```

### 7. Post-Deploy-Verifikation
```bash
# 3s warten damit Service hoch kommt
sleep 3
ssh steuerung@raspberrypi.local "sudo systemctl status bingxbot --no-pager -n 20"
ssh steuerung@raspberrypi.local "sudo journalctl -u bingxbot -n 30 --no-pager"
```

### 8. API-Probe (optional wenn Bearer-Token bekannt)
Der User kann den Bearer-Token aus `%APPDATA%\BingXBot\Client\connection.json` mitgeben. Dann:
```bash
curl -s -H "Authorization: Bearer {TOKEN}" \
     "http://100.106.158.83:5050/api/v1/status"
```

### 9. ZIP auf Pi aufraeumen
```bash
ssh steuerung@raspberrypi.local "rm /tmp/{App}-Server-Pi-linux-arm64-{Version}.zip"
```

## Ausgabe

```
## Server-Deploy: {App} {Version}

### Vor-Check
- Release-ZIP: {Pfad} ({Groesse} MB) [OK]
- Uncommitted Changes: {JA/NEIN}
- Letzter Commit: {hash} "{msg}"

### Transfer
- scp nach steuerung@raspberrypi.local:/tmp/ [OK/FAIL]

### Install
- systemctl stop: [OK]
- unzip + chmod + chown: [OK]
- systemctl reset-failed + start: [OK]

### Verifikation
- systemctl status: {active/failed/inactive}
- Letzte 10 Log-Zeilen: {Auszug}
- API-Status (falls Token): state={0|2} [Stopped/Running]

### Naechste Schritte (manuell)
- [ ] Clients neu verbinden falls Pairing-Code rotiert: ssh steuerung@raspberrypi.local "cat /var/lib/bingxbot/pairing-code.txt"
- [ ] Bot starten falls gewuenscht (State=0 nach Deploy ist normal):
      POST /api/v1/bot/start mit Preset
- [ ] Eine Test-Aktion (Balance abrufen, Status checken)
```

## Fehlerbehandlung

- **ZIP fehlt**: STOPP mit Hinweis welche ZIPs vorhanden sind und wie man einen Build macht (`dotnet publish`).
- **scp schlaegt fehl**: SSH-Key / Host-Erreichbarkeit pruefen (`ssh steuerung@raspberrypi.local "echo ok"`). Hinweis: Passwort-Auth ist aktuell, SSH-Key waere besser.
- **unzip schlaegt fehl**: Platz auf Pi (`df -h`), Permissions (`sudo chown`).
- **Service startet, aber ungesund (failed)**: `journalctl -u bingxbot -n 100` voll ausgeben, haeufigste Ursachen: fehlende Berechtigung auf `/var/lib/bingxbot/`, Port 5050 belegt, DB-Migration gescheitert.
- **API nicht erreichbar (nach Start)**: Tailscale-Verbindung am PC pruefen, Port 5050 am Pi offen (`ss -tlnp | grep 5050`), Firewall auf dem Pi.

## Abgrenzung

- Fuer tiefe Server-Diagnose (journalctl-Analyse, systemd-Unit-Debugging) -> Agent `server-ops`
- Fuer App-Release (AAB) -> Skill `release`
- Fuer Changelog/Social-Posts -> Skill `changelog`
- Fuer Trading-Logik-Checks am Server -> Agent `bingxbot`
