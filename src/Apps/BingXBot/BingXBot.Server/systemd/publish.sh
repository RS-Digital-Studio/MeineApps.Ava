#!/usr/bin/env bash
# Lokal: Server-Projekt fuer linux-arm64 self-contained publishen (fuer Raspberry Pi 5).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/../BingXBot.Server.csproj"

echo "=== Publish BingXBot.Server fuer linux-arm64 (self-contained) ==="
dotnet publish "$PROJECT" \
    -c Release \
    -r linux-arm64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:EnableCompressionInSingleFile=false

echo ""
echo "=== Publish abgeschlossen ==="
OUT="$SCRIPT_DIR/../bin/Release/net10.0/linux-arm64/publish"
echo "Ordner: $OUT"
du -sh "$OUT"
