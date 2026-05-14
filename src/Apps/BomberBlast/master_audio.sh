#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
# LUFS-Mastering-Pass fuer BomberBlast-Musik (Sprint 5.3 AAA-Audit #10)
# ═══════════════════════════════════════════════════════════════════════════
# Normalisiert alle music_*.ogg auf -16 LUFS (Mobile-Standard, EBU R128).
# Loest hoerbares Lautstaerke-Spring-Problem beim Welt-Wechsel — die 10 Tracks
# wurden zu unterschiedlichen Zeiten generiert und haben driftende Loudness
# (gemessen: -11.5 bis -13.0 LUFS, also zu laut + inkonsistent).
#
# Two-Pass-Verfahren fuer Studio-Praezision:
#   Pass 1 — ffmpeg loudnorm misst die echte Integrated/TruePeak/LRA/Threshold.
#   Pass 2 — loudnorm normalisiert mit den gemessenen Werten (lineare Korrektur
#            statt adaptivem Single-Pass) → exakt -16 LUFS statt ~1.5 LU Drift.
#
# Code-only-konform: ffmpeg ist ein einmaliger Build-Tool-Schritt, KEINE
# Runtime-Audio-Pipeline in der App. Idempotent: das Original-Backup in
# _premaster_backup/ ist die Quelle, ein erneuter Lauf re-mastered von dort.
#
# Aufruf:  bash src/Apps/BomberBlast/master_audio.sh
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOUNDS_DIR="$SCRIPT_DIR/BomberBlast.Android/Assets/Sounds"
BACKUP_DIR="$SOUNDS_DIR/_premaster_backup"

# Mobile-Standard nach EBU R128: -16 LUFS integriert, -1 dBTP True-Peak, 11 LU Range.
TARGET_I="-16"
TARGET_TP="-1"
TARGET_LRA="11"

if ! command -v ffmpeg >/dev/null 2>&1; then
    echo "FEHLER: ffmpeg nicht gefunden. Bitte installieren." >&2
    exit 1
fi
if [ ! -d "$SOUNDS_DIR" ]; then
    echo "FEHLER: Sounds-Verzeichnis nicht gefunden: $SOUNDS_DIR" >&2
    exit 1
fi

# Liest einen String-Wert aus dem loudnorm-JSON-Report ("key" : "value").
# Reines grep/sed — keine python/jq-Abhaengigkeit.
json_value() {
    printf '%s\n' "$1" | grep -m1 "\"$2\"" | sed 's/.*: *"\([^"]*\)".*/\1/'
}

mkdir -p "$BACKUP_DIR"

echo "LUFS-Mastering (Two-Pass): Ziel I=${TARGET_I} TP=${TARGET_TP} LRA=${TARGET_LRA}"
echo "─────────────────────────────────────────────────────────────"

count=0
for f in "$SOUNDS_DIR"/music_*.ogg; do
    [ -e "$f" ] || continue
    name="$(basename "$f")"

    # Idempotenz: das unveraenderte Original wird einmalig gesichert und bleibt
    # die Mastering-Quelle. Re-Runs mastern immer vom Original, nicht kumulativ.
    if [ ! -f "$BACKUP_DIR/$name" ]; then
        cp "$f" "$BACKUP_DIR/$name"
    fi
    src="$BACKUP_DIR/$name"

    # ── Pass 1: Loudness messen (JSON-Report) ──────────────────────────────
    report="$(ffmpeg -hide_banner -i "$src" \
        -af "loudnorm=I=${TARGET_I}:TP=${TARGET_TP}:LRA=${TARGET_LRA}:print_format=json" \
        -f null - 2>&1)"
    m_i="$(json_value "$report" input_i)"
    m_tp="$(json_value "$report" input_tp)"
    m_lra="$(json_value "$report" input_lra)"
    m_thresh="$(json_value "$report" input_thresh)"
    m_offset="$(json_value "$report" target_offset)"

    if [ -z "$m_i" ] || [ -z "$m_thresh" ]; then
        echo "  WARNUNG: $name — Pass-1-Report unvollstaendig, ueberspringe." >&2
        continue
    fi

    # ── Pass 2: linear normalisieren mit den gemessenen Werten ─────────────
    tmp="$SOUNDS_DIR/.tmp_$name"
    ffmpeg -y -hide_banner -loglevel error \
        -i "$src" \
        -af "loudnorm=I=${TARGET_I}:TP=${TARGET_TP}:LRA=${TARGET_LRA}:measured_I=${m_i}:measured_TP=${m_tp}:measured_LRA=${m_lra}:measured_thresh=${m_thresh}:offset=${m_offset}:linear=true" \
        -c:a libvorbis -q:a 5 \
        "$tmp"
    mv "$tmp" "$f"
    count=$((count + 1))
    echo "  gemastered: $name  (war ${m_i} LUFS)"
done

echo "─────────────────────────────────────────────────────────────"
echo "Fertig — $count Tracks auf ${TARGET_I} LUFS normalisiert (Two-Pass)."
echo "Originale liegen unter: $BACKUP_DIR"
