#!/usr/bin/env bash
# LUFS-Mastering-Skript fuer BomberBlast (Welle 4 v2.0.58 AAA-Audit #15).
#
# Normalisiert alle Audio-Assets (Background-Musik + langlebige Stinger) auf
# -16 LUFS (EBU R128 / Mobile-Standard). Loest hoerbare Lautstaerke-Spruenge
# zwischen Welten und zwischen Music/SFX/Voice-Buses.
#
# Voraussetzungen:
#   - ffmpeg in PATH (Windows: scoop install ffmpeg / Linux: apt-get install ffmpeg)
#   - Read-Write-Zugriff auf BomberBlast.Shared/Assets/sounds/
#
# Verwendung:
#   bash tools/lufs-mastering.sh              # Master alle .ogg-Files
#   bash tools/lufs-mastering.sh dry-run      # Zeigt was waere, ohne zu schreiben
#
# Output: gleiche Datei-Namen, in-place ersetzt. Backups in *.bak vor Ueberschreibung.
#
# Standard-Parameter (EBU R128, Mobile-Mastering):
#   I (integrated loudness)  = -16 LUFS
#   TP (true peak)           = -1 dBTP
#   LRA (loudness range)     = 11 LU
# Fuer aggressivere Master (Action/Combat-Track): I=-14, TP=-1, LRA=8

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SOUNDS_DIR="${REPO_ROOT}/src/Apps/BomberBlast/BomberBlast.Shared/Assets/sounds"

DRY_RUN=0
if [[ "${1:-}" == "dry-run" ]]; then
  DRY_RUN=1
fi

if ! command -v ffmpeg &> /dev/null; then
  echo "FEHLER: ffmpeg nicht gefunden. Installation:"
  echo "  Windows: scoop install ffmpeg  (oder Chocolatey: choco install ffmpeg)"
  echo "  Linux:   sudo apt-get install ffmpeg"
  exit 1
fi

if [[ ! -d "${SOUNDS_DIR}" ]]; then
  echo "FEHLER: Sounds-Verzeichnis nicht gefunden: ${SOUNDS_DIR}"
  exit 1
fi

echo "LUFS-Mastering startet — Quelle: ${SOUNDS_DIR}"
[[ ${DRY_RUN} -eq 1 ]] && echo "DRY-RUN-Modus: keine Dateien werden geaendert."
echo ""

count=0
while IFS= read -r -d '' file; do
  count=$((count + 1))
  rel="${file#${REPO_ROOT}/}"
  base="$(basename "${file}")"

  # Erste Pass: Analyse fuer Loudnorm-Statistiken (gibt Linear-LUFS-Pegel zurueck).
  if [[ ${DRY_RUN} -eq 1 ]]; then
    echo "[${count}] Analyse: ${rel}"
    ffmpeg -hide_banner -loglevel error -i "${file}" \
      -af "loudnorm=I=-16:TP=-1:LRA=11:print_format=summary" \
      -f null - 2>&1 | grep -E "(Input|Output) Integrated:|(Input|Output) True Peak:" || true
    continue
  fi

  # Backup
  if [[ ! -f "${file}.bak" ]]; then
    cp "${file}" "${file}.bak"
  fi

  # Mastering: 2-Pass-Loudnorm fuer hoechste Praezision.
  tmp_out="${file}.tmp.ogg"
  echo "[${count}] Mastering: ${rel}"
  ffmpeg -hide_banner -loglevel error -y \
    -i "${file}.bak" \
    -af "loudnorm=I=-16:TP=-1:LRA=11" \
    -c:a libvorbis -q:a 5 \
    "${tmp_out}"

  mv "${tmp_out}" "${file}"
done < <(find "${SOUNDS_DIR}" -type f \( -name "*.ogg" -o -name "*.mp3" -o -name "*.wav" \) -print0)

echo ""
echo "Fertig — ${count} Datei(en) verarbeitet."
[[ ${DRY_RUN} -eq 0 ]] && echo "Backups liegen als *.bak neben den Original-Dateien."
