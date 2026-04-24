#!/usr/bin/env bash
#
# BingXBot Server — Einfacher Starter (ohne systemd, ohne sudo).
# ZIP entpacken → Rechtsklick → "In Terminal ausführen" ODER: bash start.sh
#
# Daten-Verzeichnis: ~/.config/bingxbot/ (bot.db, credentials.bin, pairing-code.txt, tokens.json)
# Port: 5050 (HTTP, kein TLS — WAN nur via Tailscale/Caddy absichern!)
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Executable-Bit setzen (wird beim Entpacken oft nicht gesetzt)
chmod +x ./BingXBot.Server 2>/dev/null || true

# Daten-Verzeichnis vorbereiten
DATA_DIR="$HOME/.config/bingxbot"
mkdir -p "$DATA_DIR"

# Lokale IP ermitteln (für Pairing-Anleitung)
LOCAL_IP="$(hostname -I 2>/dev/null | awk '{print $1}')"
[ -z "$LOCAL_IP" ] && LOCAL_IP="$(hostname -s).local"

# Farben (optional)
GRN='\033[0;32m'; YLW='\033[1;33m'; CYN='\033[0;36m'; NC='\033[0m'

echo ""
echo -e "${CYN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYN}║           BingXBot Server v1.1.0 — wird gestartet          ║${NC}"
echo -e "${CYN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "Daten-Verzeichnis: ${YLW}$DATA_DIR${NC}"
echo -e "API-URL:           ${YLW}http://$LOCAL_IP:5050${NC}"
echo -e "Health-Check:      ${YLW}http://$LOCAL_IP:5050/api/v1/health${NC}"
echo ""
echo -e "${GRN}Pairing-Code wird nach dem Start gleich angezeigt.${NC}"
echo -e "${GRN}Zum Beenden: Strg+C${NC}"
echo ""

# Pairing-Code nach 3 Sekunden im Hintergrund anzeigen (Server braucht kurz zum Starten)
(
    sleep 3
    CODE_FILE="$DATA_DIR/pairing-code.txt"
    if [ -f "$CODE_FILE" ]; then
        CODE="$(cat "$CODE_FILE" 2>/dev/null || echo "")"
        if [ -n "$CODE" ]; then
            echo ""
            echo -e "${CYN}╔════════════════════════════════════════════════════════════╗${NC}"
            echo -e "${CYN}║  PAIRING-CODE für Desktop/Android-Client:                  ║${NC}"
            echo -e "${CYN}║                                                            ║${NC}"
            printf "${CYN}║                    ${GRN}%-6s${NC}                                  ${CYN}║${NC}\n" "$CODE"
            echo -e "${CYN}║                                                            ║${NC}"
            echo -e "${CYN}║  Gültig für 5 Minuten, max. 5 Fehlversuche.                ║${NC}"
            echo -e "${CYN}║  Server-URL: http://$LOCAL_IP:5050"
            echo -e "${CYN}╚════════════════════════════════════════════════════════════╝${NC}"
            echo ""
        fi
    fi
) &

# Server starten (blockiert — Strg+C beendet sauber)
exec ./BingXBot.Server --urls "http://0.0.0.0:5050"
