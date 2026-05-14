"""
HandwerkerImperium SoundForge.

Synthetisiert SFX + Music-Loops algorithmisch und konvertiert sie
ueber FFMPEG nach OGG-Vorbis. Ziel: 60+ SFX + 4 Music-Loops fuer
das AAA-Audio-Pack.

Aufruf:
    py generate_audio.py
    py generate_audio.py --only sfx
    py generate_audio.py --only music

Ausgabe:
    F:\\Meine_Apps_Ava\\src\\Apps\\HandwerkerImperium\\HandwerkerImperium.Android\\Assets\\Sounds\\*.ogg
    F:\\Meine_Apps_Ava\\src\\Apps\\HandwerkerImperium\\HandwerkerImperium.Android\\Assets\\Music\\*.ogg
"""
from __future__ import annotations

import argparse
import math
import os
import random
import struct
import subprocess
import sys
import wave
from pathlib import Path

# ----------------------------------------------------------------------
# Pfade — Sounds liegen im Shared-Projekt (eine Quelle fuer Desktop+Android).
# Android linkt sie via <AndroidAsset Include="..\HandwerkerImperium.Shared\Assets\Sounds\**" />.
# Desktop nutzt sie via <AvaloniaResource Include="Assets\**" />.
# ----------------------------------------------------------------------
ROOT = Path(r"F:\Meine_Apps_Ava\src\Apps\HandwerkerImperium\HandwerkerImperium.Shared\Assets")
SFX_DIR = ROOT / "Sounds"
MUSIC_DIR = ROOT / "Music"
WORK_DIR = Path(r"F:\Meine_Apps_Ava\tools\SoundForge\_workdir")

SFX_DIR.mkdir(parents=True, exist_ok=True)
MUSIC_DIR.mkdir(parents=True, exist_ok=True)
WORK_DIR.mkdir(parents=True, exist_ok=True)

# ----------------------------------------------------------------------
# Audio-Settings
# ----------------------------------------------------------------------
SAMPLE_RATE = 44100
SAMPLE_WIDTH = 2  # 16-bit
CHANNELS = 2      # Stereo

# ----------------------------------------------------------------------
# Helpers
# ----------------------------------------------------------------------
def make_buf(duration: float) -> list[float]:
    """Erstellt einen Float-Buffer (mono) der angegebenen Laenge in Sekunden."""
    return [0.0] * int(duration * SAMPLE_RATE)


def add_sine(buf: list[float], start: float, dur: float, freq: float,
             vol: float = 0.4, phase: float = 0.0) -> None:
    n_start = int(start * SAMPLE_RATE)
    n_end = min(len(buf), n_start + int(dur * SAMPLE_RATE))
    inv_sr = 1.0 / SAMPLE_RATE
    for i in range(n_start, n_end):
        t = (i - n_start) * inv_sr
        buf[i] += math.sin(2 * math.pi * freq * t + phase) * vol


def add_sweep(buf: list[float], start: float, dur: float, f0: float, f1: float,
              vol: float = 0.4, kind: str = "lin") -> None:
    n_start = int(start * SAMPLE_RATE)
    n_end = min(len(buf), n_start + int(dur * SAMPLE_RATE))
    n = max(1, n_end - n_start)
    phase = 0.0
    inv_sr = 1.0 / SAMPLE_RATE
    for i in range(n_start, n_end):
        t = (i - n_start) / n
        if kind == "exp":
            f = f0 * (f1 / f0) ** t
        else:
            f = f0 + (f1 - f0) * t
        phase += 2 * math.pi * f * inv_sr
        buf[i] += math.sin(phase) * vol


def add_square(buf: list[float], start: float, dur: float, freq: float,
               vol: float = 0.3) -> None:
    n_start = int(start * SAMPLE_RATE)
    n_end = min(len(buf), n_start + int(dur * SAMPLE_RATE))
    inv_sr = 1.0 / SAMPLE_RATE
    for i in range(n_start, n_end):
        t = (i - n_start) * inv_sr
        s = math.sin(2 * math.pi * freq * t)
        buf[i] += (1.0 if s >= 0 else -1.0) * vol


def add_triangle(buf: list[float], start: float, dur: float, freq: float,
                 vol: float = 0.4) -> None:
    n_start = int(start * SAMPLE_RATE)
    n_end = min(len(buf), n_start + int(dur * SAMPLE_RATE))
    period = SAMPLE_RATE / freq
    for i in range(n_start, n_end):
        local = (i - n_start) % period
        x = local / period  # 0..1
        if x < 0.5:
            v = 4 * x - 1.0  # -1..1
        else:
            v = 3.0 - 4 * x  # 1..-1
        buf[i] += v * vol


def add_saw(buf: list[float], start: float, dur: float, freq: float,
            vol: float = 0.3) -> None:
    n_start = int(start * SAMPLE_RATE)
    n_end = min(len(buf), n_start + int(dur * SAMPLE_RATE))
    period = SAMPLE_RATE / freq
    for i in range(n_start, n_end):
        local = (i - n_start) % period
        x = local / period
        v = 2 * x - 1.0
        buf[i] += v * vol


def add_noise(buf: list[float], start: float, dur: float, vol: float = 0.3,
              seed: int | None = None) -> None:
    if seed is not None:
        random.seed(seed)
    n_start = int(start * SAMPLE_RATE)
    n_end = min(len(buf), n_start + int(dur * SAMPLE_RATE))
    for i in range(n_start, n_end):
        buf[i] += (random.random() * 2 - 1) * vol


def apply_envelope(buf: list[float], start: float, dur: float,
                   attack: float = 0.01, decay: float = 0.1,
                   sustain: float = 0.7, release: float = 0.2,
                   peak: float = 1.0) -> None:
    n_start = int(start * SAMPLE_RATE)
    n_total = int(dur * SAMPLE_RATE)
    n_end = min(len(buf), n_start + n_total)
    n_atk = int(attack * SAMPLE_RATE)
    n_dec = int(decay * SAMPLE_RATE)
    n_rel = int(release * SAMPLE_RATE)
    n_sus = max(0, n_total - n_atk - n_dec - n_rel)

    for j in range(n_total):
        if n_start + j >= n_end:
            break
        if j < n_atk:
            env = (j / max(1, n_atk)) * peak
        elif j < n_atk + n_dec:
            t = (j - n_atk) / max(1, n_dec)
            env = peak + (sustain * peak - peak) * t
        elif j < n_atk + n_dec + n_sus:
            env = sustain * peak
        else:
            t = (j - n_atk - n_dec - n_sus) / max(1, n_rel)
            env = sustain * peak * (1 - t)
        buf[n_start + j] *= env


def apply_fade_out(buf: list[float], start: float, dur: float, curve: float = 1.0) -> None:
    n_start = int(start * SAMPLE_RATE)
    n_end = min(len(buf), n_start + int(dur * SAMPLE_RATE))
    n = max(1, n_end - n_start)
    for i in range(n_start, n_end):
        t = (i - n_start) / n
        buf[i] *= (1 - t) ** curve


def apply_fade_in(buf: list[float], start: float, dur: float, curve: float = 1.0) -> None:
    n_start = int(start * SAMPLE_RATE)
    n_end = min(len(buf), n_start + int(dur * SAMPLE_RATE))
    n = max(1, n_end - n_start)
    for i in range(n_start, n_end):
        t = (i - n_start) / n
        buf[i] *= t ** curve


def apply_lowpass(buf: list[float], cutoff: float = 4000.0) -> None:
    """Einfacher 1-pole IIR Lowpass."""
    if cutoff <= 0:
        return
    rc = 1.0 / (2 * math.pi * cutoff)
    dt = 1.0 / SAMPLE_RATE
    alpha = dt / (rc + dt)
    prev = 0.0
    for i in range(len(buf)):
        prev = prev + alpha * (buf[i] - prev)
        buf[i] = prev


def apply_highpass(buf: list[float], cutoff: float = 200.0) -> None:
    if cutoff <= 0:
        return
    rc = 1.0 / (2 * math.pi * cutoff)
    dt = 1.0 / SAMPLE_RATE
    alpha = rc / (rc + dt)
    prev_in = 0.0
    prev_out = 0.0
    for i in range(len(buf)):
        x = buf[i]
        y = alpha * (prev_out + x - prev_in)
        prev_in = x
        prev_out = y
        buf[i] = y


def normalize(buf: list[float], target: float = 0.85) -> None:
    peak = max((abs(s) for s in buf), default=0.0)
    if peak <= 1e-9:
        return
    g = target / peak
    for i in range(len(buf)):
        buf[i] *= g


def soft_limit(buf: list[float]) -> None:
    """Tanh-Limiter, vermeidet Clipping ohne abrupten Schnitt."""
    for i in range(len(buf)):
        buf[i] = math.tanh(buf[i])


def write_wav(path: Path, buf: list[float]) -> None:
    """Schreibt mono Float-Buffer als 16-bit Stereo-WAV."""
    with wave.open(str(path), "wb") as f:
        f.setnchannels(CHANNELS)
        f.setsampwidth(SAMPLE_WIDTH)
        f.setframerate(SAMPLE_RATE)
        for v in buf:
            sample = max(-1.0, min(1.0, v))
            i16 = int(sample * 32767)
            data = struct.pack("<hh", i16, i16)
            f.writeframesraw(data)


def write_wav_stereo(path: Path, left: list[float], right: list[float]) -> None:
    n = min(len(left), len(right))
    with wave.open(str(path), "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(SAMPLE_WIDTH)
        f.setframerate(SAMPLE_RATE)
        for i in range(n):
            l = max(-1.0, min(1.0, left[i]))
            r = max(-1.0, min(1.0, right[i]))
            data = struct.pack("<hh", int(l * 32767), int(r * 32767))
            f.writeframesraw(data)


def wav_to_ogg(wav_path: Path, ogg_path: Path, bitrate: str = "96k") -> None:
    """Konvertiert WAV → OGG-Vorbis via FFMPEG."""
    cmd = [
        "ffmpeg", "-y", "-loglevel", "error",
        "-i", str(wav_path),
        "-c:a", "libvorbis",
        "-b:a", bitrate,
        str(ogg_path),
    ]
    subprocess.run(cmd, check=True)


# ----------------------------------------------------------------------
# SFX-Generatoren
# ----------------------------------------------------------------------
def sfx_ui_hover(out: Path) -> None:
    buf = make_buf(0.08)
    add_sine(buf, 0.0, 0.06, 1200, vol=0.25)
    apply_envelope(buf, 0.0, 0.08, attack=0.005, decay=0.04, sustain=0.0, release=0.03, peak=0.8)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_ui_swipe(out: Path) -> None:
    buf = make_buf(0.25)
    add_sweep(buf, 0.0, 0.2, 600, 1800, vol=0.35, kind="exp")
    apply_envelope(buf, 0.0, 0.25, attack=0.02, decay=0.1, sustain=0.4, release=0.13, peak=1.0)
    apply_lowpass(buf, 5000)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_ui_modal_open(out: Path) -> None:
    buf = make_buf(0.4)
    add_sweep(buf, 0.0, 0.3, 400, 900, vol=0.35)
    add_sine(buf, 0.05, 0.3, 1200, vol=0.15)
    apply_envelope(buf, 0.0, 0.4, attack=0.01, decay=0.15, sustain=0.5, release=0.24, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_ui_modal_close(out: Path) -> None:
    buf = make_buf(0.3)
    add_sweep(buf, 0.0, 0.25, 900, 350, vol=0.35)
    apply_envelope(buf, 0.0, 0.3, attack=0.005, decay=0.1, sustain=0.4, release=0.2, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_ui_tab_switch(out: Path) -> None:
    buf = make_buf(0.15)
    add_sine(buf, 0.0, 0.06, 1500, vol=0.3)
    add_sine(buf, 0.04, 0.08, 2200, vol=0.25)
    apply_envelope(buf, 0.0, 0.15, attack=0.005, decay=0.06, sustain=0.2, release=0.08, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_ui_notification_pop(out: Path) -> None:
    buf = make_buf(0.3)
    add_sine(buf, 0.0, 0.1, 880, vol=0.35)
    add_sine(buf, 0.08, 0.15, 1320, vol=0.3)
    apply_envelope(buf, 0.0, 0.3, attack=0.005, decay=0.1, sustain=0.4, release=0.19, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_ui_back(out: Path) -> None:
    buf = make_buf(0.15)
    add_sine(buf, 0.0, 0.12, 600, vol=0.35)
    apply_envelope(buf, 0.0, 0.15, attack=0.005, decay=0.05, sustain=0.3, release=0.09, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_money_big(out: Path) -> None:
    buf = make_buf(0.7)
    # Mehrere "Coin"-Sounds mit Decay
    for i, freq in enumerate([1200, 1500, 1800, 2200, 1700]):
        add_sine(buf, 0.05 * i, 0.18, freq, vol=0.25)
        add_sine(buf, 0.05 * i, 0.18, freq * 2, vol=0.12)
    apply_envelope(buf, 0.0, 0.7, attack=0.005, decay=0.15, sustain=0.6, release=0.5, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_low_money_warning(out: Path) -> None:
    buf = make_buf(0.5)
    add_sweep(buf, 0.0, 0.18, 500, 350, vol=0.4)
    add_sweep(buf, 0.22, 0.18, 500, 350, vol=0.4)
    apply_lowpass(buf, 2500)
    apply_envelope(buf, 0.0, 0.5, attack=0.01, decay=0.1, sustain=0.6, release=0.3, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_costs_paid(out: Path) -> None:
    buf = make_buf(0.25)
    add_sine(buf, 0.0, 0.12, 700, vol=0.3)
    add_sine(buf, 0.06, 0.12, 600, vol=0.25)
    apply_envelope(buf, 0.0, 0.25, attack=0.005, decay=0.08, sustain=0.4, release=0.16, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_worker_promoted(out: Path) -> None:
    buf = make_buf(0.6)
    # Aufstrebende Note + Triumph-Akkord
    add_sweep(buf, 0.0, 0.25, 440, 880, vol=0.3)
    add_sine(buf, 0.25, 0.35, 880, vol=0.3)
    add_sine(buf, 0.25, 0.35, 1108, vol=0.25)  # cis7
    add_sine(buf, 0.25, 0.35, 1318, vol=0.2)   # e7
    apply_envelope(buf, 0.0, 0.6, attack=0.005, decay=0.1, sustain=0.7, release=0.49, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_worker_quit(out: Path) -> None:
    buf = make_buf(0.7)
    add_sweep(buf, 0.0, 0.6, 400, 200, vol=0.4)
    add_sweep(buf, 0.05, 0.6, 600, 300, vol=0.25)
    apply_lowpass(buf, 1500)
    apply_envelope(buf, 0.0, 0.7, attack=0.01, decay=0.15, sustain=0.5, release=0.54, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_worker_mood_warn(out: Path) -> None:
    buf = make_buf(0.4)
    for i, freq in enumerate([700, 600, 700]):
        add_sine(buf, 0.1 * i, 0.08, freq, vol=0.35)
    apply_envelope(buf, 0.0, 0.4, attack=0.005, decay=0.05, sustain=0.6, release=0.34, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_worker_resting(out: Path) -> None:
    buf = make_buf(0.6)
    add_noise(buf, 0.0, 0.3, vol=0.15, seed=42)
    add_sweep(buf, 0.0, 0.3, 80, 60, vol=0.35)
    add_noise(buf, 0.4, 0.2, vol=0.1, seed=43)
    apply_lowpass(buf, 800)
    apply_envelope(buf, 0.0, 0.6, attack=0.05, decay=0.1, sustain=0.5, release=0.45, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_intern_ready(out: Path) -> None:
    buf = make_buf(0.5)
    # Aufmerksamkeitsmelodie: G-C-E
    add_sine(buf, 0.0, 0.1, 784, vol=0.3)   # G
    add_sine(buf, 0.1, 0.1, 1047, vol=0.3)  # C
    add_sine(buf, 0.2, 0.25, 1319, vol=0.3) # E
    apply_envelope(buf, 0.0, 0.5, attack=0.005, decay=0.05, sustain=0.5, release=0.44, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_achievement_unlock(out: Path) -> None:
    buf = make_buf(0.9)
    # Major Arpeggio C-E-G-C
    notes = [(0.00, 0.18, 523),  # C5
             (0.10, 0.18, 659),  # E5
             (0.20, 0.18, 784),  # G5
             (0.30, 0.55, 1047)] # C6
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.28)
        add_sine(buf, s, d, f * 2, vol=0.12)
    apply_envelope(buf, 0.0, 0.9, attack=0.005, decay=0.1, sustain=0.7, release=0.79, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_achievement_legendary(out: Path) -> None:
    buf = make_buf(1.5)
    # Triumph-Fanfare: C-E-G-C-E-G-C (1.5s)
    notes = [(0.00, 0.20, 523),
             (0.15, 0.20, 659),
             (0.30, 0.20, 784),
             (0.45, 0.30, 1047),
             (0.65, 0.20, 1319),
             (0.80, 0.20, 1568),
             (0.95, 0.55, 2093)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.25)
        add_sine(buf, s, d, f * 0.5, vol=0.2)  # Bass
        add_sine(buf, s, d, f * 2, vol=0.1)    # Oberton
    apply_envelope(buf, 0.0, 1.5, attack=0.01, decay=0.2, sustain=0.7, release=1.29, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_streak_save(out: Path) -> None:
    buf = make_buf(0.7)
    add_sweep(buf, 0.0, 0.4, 300, 1200, vol=0.35)
    add_sine(buf, 0.4, 0.3, 1500, vol=0.3)
    apply_envelope(buf, 0.0, 0.7, attack=0.01, decay=0.15, sustain=0.6, release=0.54, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_streak_milestone(out: Path) -> None:
    buf = make_buf(1.0)
    notes = [(0.00, 0.15, 523), (0.12, 0.15, 659), (0.24, 0.15, 784),
             (0.36, 0.6, 1047)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
    apply_envelope(buf, 0.0, 1.0, attack=0.005, decay=0.1, sustain=0.6, release=0.89, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_milestone_minor(out: Path) -> None:
    buf = make_buf(0.4)
    add_sine(buf, 0.0, 0.15, 880, vol=0.3)
    add_sine(buf, 0.1, 0.25, 1175, vol=0.3)
    apply_envelope(buf, 0.0, 0.4, attack=0.005, decay=0.05, sustain=0.6, release=0.34, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_milestone_major(out: Path) -> None:
    buf = make_buf(0.8)
    notes = [(0.00, 0.20, 880),
             (0.15, 0.20, 1175),
             (0.30, 0.45, 1568)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 0.5, vol=0.15)
    apply_envelope(buf, 0.0, 0.8, attack=0.005, decay=0.1, sustain=0.7, release=0.69, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_prestige_start(out: Path) -> None:
    """Cinematic Build-up — 1.5s rising drone + impact"""
    buf = make_buf(1.5)
    add_sweep(buf, 0.0, 1.2, 80, 220, vol=0.35)
    add_sweep(buf, 0.0, 1.2, 160, 440, vol=0.25)
    add_noise(buf, 1.0, 0.3, vol=0.15, seed=100)
    add_sine(buf, 1.2, 0.3, 110, vol=0.5)  # Impact
    add_sine(buf, 1.2, 0.3, 55, vol=0.4)
    apply_lowpass(buf, 3500)
    apply_envelope(buf, 0.0, 1.5, attack=0.05, decay=0.15, sustain=0.7, release=1.0, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_prestige_complete(out: Path) -> None:
    """Triumph-Stinger — 2s major arpeggio + gong"""
    buf = make_buf(2.0)
    # Major-Akkord-Aufbau
    base_notes = [(0.0, 0.5, 261),    # C4
                  (0.1, 0.5, 329),    # E4
                  (0.2, 0.5, 392),    # G4
                  (0.3, 0.5, 523),    # C5
                  (0.4, 0.5, 659),    # E5
                  (0.5, 0.5, 784),    # G5
                  (0.6, 1.4, 1047)]   # C6 hold
    for s, d, f in base_notes:
        add_sine(buf, s, d, f, vol=0.2)
        add_sine(buf, s, d, f * 2, vol=0.1)
    # Gong-Hit am Anfang
    add_sine(buf, 0.0, 1.5, 110, vol=0.45)
    add_sine(buf, 0.0, 1.5, 55, vol=0.35)
    apply_envelope(buf, 0.0, 2.0, attack=0.005, decay=0.2, sustain=0.7, release=1.79, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_ascension(out: Path) -> None:
    """Otherworldly — 2.5s ethereal swell"""
    buf = make_buf(2.5)
    add_sweep(buf, 0.0, 2.0, 220, 880, vol=0.3)
    add_sweep(buf, 0.5, 2.0, 330, 1320, vol=0.25)
    add_sweep(buf, 1.0, 1.5, 440, 1760, vol=0.2)
    add_sine(buf, 0.0, 2.5, 55, vol=0.25)  # Sub-Bass
    apply_lowpass(buf, 5000)
    apply_envelope(buf, 0.0, 2.5, attack=0.3, decay=0.5, sustain=0.7, release=1.7, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_rebirth_star(out: Path) -> None:
    """Sparkle — 0.6s shimmer"""
    buf = make_buf(0.6)
    for i in range(8):
        f = 1500 + i * 350
        add_sine(buf, 0.05 * i, 0.15, f, vol=0.18)
    apply_highpass(buf, 800)
    apply_envelope(buf, 0.0, 0.6, attack=0.005, decay=0.1, sustain=0.5, release=0.49, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_order_accept(out: Path) -> None:
    buf = make_buf(0.3)
    add_sine(buf, 0.0, 0.1, 800, vol=0.3)
    add_sine(buf, 0.07, 0.2, 1100, vol=0.3)
    apply_envelope(buf, 0.0, 0.3, attack=0.005, decay=0.05, sustain=0.5, release=0.24, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_order_live_spawn(out: Path) -> None:
    """Urgent — kurze Alarm-Sequenz"""
    buf = make_buf(0.45)
    add_sine(buf, 0.0, 0.1, 1200, vol=0.35)
    add_sine(buf, 0.15, 0.1, 1500, vol=0.35)
    add_sine(buf, 0.3, 0.15, 1800, vol=0.35)
    apply_envelope(buf, 0.0, 0.45, attack=0.005, decay=0.05, sustain=0.6, release=0.39, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_order_vip_spawn(out: Path) -> None:
    """Special — Royal-Fanfare"""
    buf = make_buf(0.7)
    notes = [(0.0, 0.15, 880), (0.15, 0.15, 1108), (0.3, 0.4, 1319)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 0.5, vol=0.2)
    apply_envelope(buf, 0.0, 0.7, attack=0.005, decay=0.1, sustain=0.7, release=0.59, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_order_expired(out: Path) -> None:
    buf = make_buf(0.6)
    add_sweep(buf, 0.0, 0.5, 700, 200, vol=0.4)
    apply_lowpass(buf, 1200)
    apply_envelope(buf, 0.0, 0.6, attack=0.01, decay=0.1, sustain=0.5, release=0.49, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_order_risk_win(out: Path) -> None:
    """Doppelte Belohnung — Eclipse-Stinger"""
    buf = make_buf(1.0)
    # Build-up
    add_sweep(buf, 0.0, 0.3, 400, 800, vol=0.25)
    # Money-Cascade
    for i in range(6):
        add_sine(buf, 0.3 + i * 0.1, 0.2, 1500 + i * 200, vol=0.22)
    apply_envelope(buf, 0.0, 1.0, attack=0.005, decay=0.15, sustain=0.6, release=0.84, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_order_risk_loss(out: Path) -> None:
    buf = make_buf(0.7)
    add_sweep(buf, 0.0, 0.6, 600, 100, vol=0.45)
    add_noise(buf, 0.0, 0.6, vol=0.15, seed=200)
    apply_lowpass(buf, 1500)
    apply_envelope(buf, 0.0, 0.7, attack=0.005, decay=0.1, sustain=0.6, release=0.59, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_building_complete(out: Path) -> None:
    buf = make_buf(0.8)
    # Aufbau-Klang (Holz/Stein) + Triumph
    add_noise(buf, 0.0, 0.2, vol=0.2, seed=300)
    add_sine(buf, 0.2, 0.6, 523, vol=0.3)
    add_sine(buf, 0.3, 0.5, 659, vol=0.3)
    add_sine(buf, 0.4, 0.4, 784, vol=0.3)
    apply_lowpass(buf, 4500)
    apply_envelope(buf, 0.0, 0.8, attack=0.005, decay=0.1, sustain=0.7, release=0.69, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_research_complete(out: Path) -> None:
    """Magic-Spark"""
    buf = make_buf(1.0)
    add_sweep(buf, 0.0, 0.5, 400, 1600, vol=0.35)
    add_noise(buf, 0.0, 0.3, vol=0.12, seed=400)
    apply_highpass(buf, 600)
    add_sine(buf, 0.5, 0.5, 1760, vol=0.25)
    apply_envelope(buf, 0.0, 1.0, attack=0.005, decay=0.2, sustain=0.6, release=0.79, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_research_start(out: Path) -> None:
    buf = make_buf(0.4)
    add_sweep(buf, 0.0, 0.3, 200, 800, vol=0.35)
    apply_envelope(buf, 0.0, 0.4, attack=0.01, decay=0.05, sustain=0.6, release=0.34, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_crafting_complete(out: Path) -> None:
    buf = make_buf(0.7)
    add_sine(buf, 0.0, 0.15, 800, vol=0.3)
    add_sine(buf, 0.15, 0.15, 1100, vol=0.3)
    add_sine(buf, 0.3, 0.4, 1500, vol=0.3)
    apply_envelope(buf, 0.0, 0.7, attack=0.005, decay=0.1, sustain=0.6, release=0.59, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_daily_reward(out: Path) -> None:
    """Geschenkbox-Stinger"""
    buf = make_buf(0.9)
    # Glocke
    add_sine(buf, 0.0, 0.7, 1318, vol=0.3)
    add_sine(buf, 0.0, 0.7, 1976, vol=0.18)
    # Dazu Coin
    add_sine(buf, 0.1, 0.3, 1500, vol=0.2)
    add_sine(buf, 0.4, 0.3, 1800, vol=0.2)
    apply_envelope(buf, 0.0, 0.9, attack=0.005, decay=0.1, sustain=0.6, release=0.79, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_daily_challenge_complete(out: Path) -> None:
    buf = make_buf(0.8)
    notes = [(0.0, 0.15, 587), (0.12, 0.15, 740), (0.24, 0.5, 880)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
    apply_envelope(buf, 0.0, 0.8, attack=0.005, decay=0.1, sustain=0.7, release=0.69, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_weekly_mission_complete(out: Path) -> None:
    buf = make_buf(1.2)
    notes = [(0.0, 0.18, 587), (0.15, 0.18, 740), (0.3, 0.18, 880),
             (0.45, 0.7, 1175)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 0.5, vol=0.15)
    apply_envelope(buf, 0.0, 1.2, attack=0.005, decay=0.1, sustain=0.7, release=1.09, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_battle_pass_tier_up(out: Path) -> None:
    buf = make_buf(1.0)
    add_sweep(buf, 0.0, 0.3, 400, 1200, vol=0.3)
    add_sine(buf, 0.3, 0.7, 1568, vol=0.3)
    add_sine(buf, 0.3, 0.7, 1976, vol=0.25)
    apply_envelope(buf, 0.0, 1.0, attack=0.005, decay=0.1, sustain=0.7, release=0.89, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_lucky_spin_spin(out: Path) -> None:
    """Ratter-Loop, ~1s"""
    buf = make_buf(1.0)
    for i in range(20):
        add_sine(buf, 0.05 * i, 0.04, 800 + (i % 3) * 50, vol=0.25)
    apply_envelope(buf, 0.0, 1.0, attack=0.01, decay=0.05, sustain=0.8, release=0.14, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_lucky_spin_tick(out: Path) -> None:
    buf = make_buf(0.06)
    add_sine(buf, 0.0, 0.04, 1500, vol=0.4)
    apply_envelope(buf, 0.0, 0.06, attack=0.001, decay=0.04, sustain=0.0, release=0.019, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_lucky_spin_win_small(out: Path) -> None:
    buf = make_buf(0.6)
    notes = [(0.0, 0.15, 880), (0.12, 0.4, 1175)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
    apply_envelope(buf, 0.0, 0.6, attack=0.005, decay=0.1, sustain=0.6, release=0.49, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_lucky_spin_win_big(out: Path) -> None:
    buf = make_buf(1.0)
    notes = [(0.0, 0.15, 880), (0.12, 0.15, 1175), (0.24, 0.15, 1568),
             (0.36, 0.6, 2093)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 0.5, vol=0.15)
    apply_envelope(buf, 0.0, 1.0, attack=0.005, decay=0.1, sustain=0.7, release=0.89, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_lucky_spin_jackpot(out: Path) -> None:
    """JACKPOT! 1.8s lange Cascade"""
    buf = make_buf(1.8)
    # Build-up
    for i in range(15):
        add_sine(buf, 0.05 * i, 0.15, 800 + i * 80, vol=0.18)
    # Hold-Note
    add_sine(buf, 0.8, 1.0, 2093, vol=0.3)
    add_sine(buf, 0.8, 1.0, 1568, vol=0.25)
    add_sine(buf, 0.8, 1.0, 1047, vol=0.2)
    apply_envelope(buf, 0.0, 1.8, attack=0.005, decay=0.2, sustain=0.7, release=1.59, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_manager_unlocked(out: Path) -> None:
    buf = make_buf(0.9)
    notes = [(0.0, 0.2, 523), (0.15, 0.2, 659), (0.3, 0.55, 784)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
    apply_envelope(buf, 0.0, 0.9, attack=0.005, decay=0.1, sustain=0.7, release=0.79, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_master_tool_unlock(out: Path) -> None:
    """Magisches Funkeln"""
    buf = make_buf(1.2)
    add_sweep(buf, 0.0, 0.4, 800, 2400, vol=0.3)
    add_sine(buf, 0.4, 0.8, 2349, vol=0.25)
    add_sine(buf, 0.4, 0.8, 1568, vol=0.2)
    apply_highpass(buf, 600)
    apply_envelope(buf, 0.0, 1.2, attack=0.005, decay=0.15, sustain=0.7, release=1.04, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_guild_join(out: Path) -> None:
    buf = make_buf(0.7)
    notes = [(0.0, 0.15, 587), (0.12, 0.15, 740), (0.24, 0.4, 880)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 0.5, vol=0.15)
    apply_envelope(buf, 0.0, 0.7, attack=0.005, decay=0.1, sustain=0.7, release=0.59, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_guild_leave(out: Path) -> None:
    buf = make_buf(0.6)
    add_sweep(buf, 0.0, 0.5, 880, 440, vol=0.35)
    apply_lowpass(buf, 2000)
    apply_envelope(buf, 0.0, 0.6, attack=0.01, decay=0.1, sustain=0.5, release=0.49, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_boss_spawn(out: Path) -> None:
    """Bedrohlich — tiefer Drone + Rumble"""
    buf = make_buf(1.5)
    add_sine(buf, 0.0, 1.5, 55, vol=0.45)
    add_sine(buf, 0.0, 1.5, 82, vol=0.3)
    add_sweep(buf, 0.3, 1.0, 200, 100, vol=0.3)
    add_noise(buf, 0.0, 1.5, vol=0.12, seed=500)
    apply_lowpass(buf, 800)
    apply_envelope(buf, 0.0, 1.5, attack=0.1, decay=0.2, sustain=0.7, release=1.0, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_boss_defeated(out: Path) -> None:
    """Triumph + Explosion"""
    buf = make_buf(1.5)
    add_noise(buf, 0.0, 0.4, vol=0.3, seed=600)
    add_sine(buf, 0.0, 0.5, 80, vol=0.4)
    notes = [(0.4, 0.2, 880), (0.55, 0.2, 1175), (0.7, 0.7, 1568)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 0.5, vol=0.2)
    apply_envelope(buf, 0.0, 1.5, attack=0.005, decay=0.2, sustain=0.7, release=1.29, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_war_start(out: Path) -> None:
    """Kriegs-Trompete"""
    buf = make_buf(1.2)
    notes = [(0.0, 0.3, 523), (0.25, 0.3, 698), (0.5, 0.7, 880)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.32)
        add_sine(buf, s, d, f * 0.5, vol=0.2)
        add_sine(buf, s, d, f * 2, vol=0.1)
    apply_envelope(buf, 0.0, 1.2, attack=0.01, decay=0.15, sustain=0.7, release=1.04, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_war_end(out: Path) -> None:
    buf = make_buf(1.0)
    add_sweep(buf, 0.0, 0.8, 880, 440, vol=0.35)
    apply_envelope(buf, 0.0, 1.0, attack=0.01, decay=0.15, sustain=0.6, release=0.84, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_welcome_back(out: Path) -> None:
    buf = make_buf(1.0)
    notes = [(0.0, 0.3, 523), (0.2, 0.3, 659), (0.4, 0.55, 784)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 0.5, vol=0.15)
    apply_envelope(buf, 0.0, 1.0, attack=0.005, decay=0.1, sustain=0.7, release=0.89, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_starter_offer(out: Path) -> None:
    """Limited-Time-Offer Stinger"""
    buf = make_buf(1.0)
    add_sweep(buf, 0.0, 0.4, 600, 1200, vol=0.3)
    add_sine(buf, 0.4, 0.6, 1568, vol=0.3)
    add_sine(buf, 0.4, 0.6, 2093, vol=0.2)
    apply_envelope(buf, 0.0, 1.0, attack=0.005, decay=0.15, sustain=0.7, release=0.84, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_offline_earnings(out: Path) -> None:
    """Cash-Cascade — viele Coins"""
    buf = make_buf(1.5)
    for i in range(10):
        f = 1500 + i * 80
        add_sine(buf, 0.1 * i, 0.2, f, vol=0.18)
    apply_envelope(buf, 0.0, 1.5, attack=0.005, decay=0.2, sustain=0.6, release=1.29, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_tutorial_step(out: Path) -> None:
    buf = make_buf(0.25)
    add_sine(buf, 0.0, 0.1, 1175, vol=0.3)
    add_sine(buf, 0.07, 0.12, 1568, vol=0.3)
    apply_envelope(buf, 0.0, 0.25, attack=0.005, decay=0.05, sustain=0.5, release=0.19, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_tax_audit(out: Path) -> None:
    """Ominoeses Kotelett"""
    buf = make_buf(1.0)
    add_sweep(buf, 0.0, 0.8, 200, 100, vol=0.4)
    add_noise(buf, 0.0, 1.0, vol=0.1, seed=700)
    apply_lowpass(buf, 800)
    apply_envelope(buf, 0.0, 1.0, attack=0.05, decay=0.2, sustain=0.6, release=0.74, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_worker_strike(out: Path) -> None:
    """Stress-Pfeifen"""
    buf = make_buf(0.8)
    add_sweep(buf, 0.0, 0.6, 800, 1500, vol=0.3)
    add_sweep(buf, 0.0, 0.6, 1600, 800, vol=0.25)
    apply_envelope(buf, 0.0, 0.8, attack=0.01, decay=0.1, sustain=0.6, release=0.69, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_event_positive(out: Path) -> None:
    buf = make_buf(0.7)
    notes = [(0.0, 0.2, 880), (0.15, 0.2, 1175), (0.3, 0.4, 1568)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
    apply_envelope(buf, 0.0, 0.7, attack=0.005, decay=0.1, sustain=0.7, release=0.59, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_event_negative(out: Path) -> None:
    buf = make_buf(0.7)
    add_sweep(buf, 0.0, 0.6, 600, 250, vol=0.4)
    apply_lowpass(buf, 1500)
    apply_envelope(buf, 0.0, 0.7, attack=0.01, decay=0.1, sustain=0.6, release=0.59, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_save_success(out: Path) -> None:
    buf = make_buf(0.3)
    add_sine(buf, 0.0, 0.1, 1175, vol=0.3)
    add_sine(buf, 0.08, 0.15, 1568, vol=0.3)
    apply_envelope(buf, 0.0, 0.3, attack=0.005, decay=0.05, sustain=0.5, release=0.24, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_error_general(out: Path) -> None:
    buf = make_buf(0.4)
    add_sine(buf, 0.0, 0.1, 400, vol=0.4)
    add_sine(buf, 0.15, 0.1, 350, vol=0.4)
    apply_envelope(buf, 0.0, 0.4, attack=0.005, decay=0.05, sustain=0.6, release=0.34, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_purchase_success(out: Path) -> None:
    """Fanfare fuer Premium-Kauf"""
    buf = make_buf(1.5)
    notes = [(0.0, 0.2, 523), (0.15, 0.2, 659), (0.3, 0.2, 784),
             (0.45, 0.2, 1047), (0.6, 0.85, 1319)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 0.5, vol=0.18)
    apply_envelope(buf, 0.0, 1.5, attack=0.005, decay=0.15, sustain=0.7, release=1.34, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_rush_activated(out: Path) -> None:
    """Power-Up Sweep"""
    buf = make_buf(0.6)
    add_sweep(buf, 0.0, 0.5, 200, 1500, vol=0.4)
    add_sweep(buf, 0.1, 0.4, 400, 2000, vol=0.25)
    apply_envelope(buf, 0.0, 0.6, attack=0.005, decay=0.1, sustain=0.7, release=0.49, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_delivery_received(out: Path) -> None:
    """Postbote-Klingel"""
    buf = make_buf(0.6)
    add_sine(buf, 0.0, 0.4, 1318, vol=0.3)
    add_sine(buf, 0.0, 0.4, 1568, vol=0.25)
    add_sine(buf, 0.2, 0.3, 1976, vol=0.2)
    apply_envelope(buf, 0.0, 0.6, attack=0.005, decay=0.1, sustain=0.6, release=0.49, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_rebirth_complete(out: Path) -> None:
    """Stern-Geburt"""
    buf = make_buf(2.0)
    add_sweep(buf, 0.0, 1.5, 220, 1760, vol=0.3)
    add_sine(buf, 1.5, 0.5, 2093, vol=0.35)
    add_sine(buf, 1.5, 0.5, 2637, vol=0.25)
    apply_highpass(buf, 400)
    apply_envelope(buf, 0.0, 2.0, attack=0.05, decay=0.3, sustain=0.7, release=1.49, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_celebration_horn(out: Path) -> None:
    """Konfetti-Hupe"""
    buf = make_buf(0.8)
    add_sweep(buf, 0.0, 0.7, 400, 1200, vol=0.35)
    add_sweep(buf, 0.05, 0.7, 600, 1500, vol=0.25)
    apply_envelope(buf, 0.0, 0.8, attack=0.01, decay=0.1, sustain=0.7, release=0.69, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


# ----------------------------------------------------------------------
# Erweiterung 100-Pack
# ----------------------------------------------------------------------
def sfx_button_tap_alt(out: Path) -> None:
    """Alternative Button-Tap (UI-Variation gegen Repetition-Fatigue)."""
    buf = make_buf(0.06)
    add_sine(buf, 0.0, 0.04, 1400, vol=0.3)
    apply_envelope(buf, 0.0, 0.06, attack=0.001, decay=0.04, sustain=0.0, release=0.019, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_button_tap_negative(out: Path) -> None:
    """Disabled-Button-Tap (kurz + tiefer)."""
    buf = make_buf(0.08)
    add_sine(buf, 0.0, 0.06, 350, vol=0.3)
    apply_envelope(buf, 0.0, 0.08, attack=0.005, decay=0.04, sustain=0.0, release=0.035, peak=0.7)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_close_dialog_subtle(out: Path) -> None:
    buf = make_buf(0.18)
    add_sweep(buf, 0.0, 0.15, 800, 400, vol=0.3)
    apply_envelope(buf, 0.0, 0.18, attack=0.005, decay=0.05, sustain=0.3, release=0.125, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_swipe_left(out: Path) -> None:
    buf = make_buf(0.18)
    add_sweep(buf, 0.0, 0.15, 1500, 700, vol=0.3)
    apply_envelope(buf, 0.0, 0.18, attack=0.01, decay=0.05, sustain=0.4, release=0.12, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_swipe_right(out: Path) -> None:
    buf = make_buf(0.18)
    add_sweep(buf, 0.0, 0.15, 700, 1500, vol=0.3)
    apply_envelope(buf, 0.0, 0.18, attack=0.01, decay=0.05, sustain=0.4, release=0.12, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_long_press(out: Path) -> None:
    """Hold-to-Upgrade Dauerton."""
    buf = make_buf(0.5)
    add_sine(buf, 0.0, 0.45, 600, vol=0.25)
    apply_envelope(buf, 0.0, 0.5, attack=0.05, decay=0.05, sustain=0.7, release=0.4, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_perfect_chain(out: Path) -> None:
    """Perfect-Combo-Stinger (Chain 3+)."""
    buf = make_buf(0.4)
    notes = [(0.0, 0.1, 880), (0.1, 0.1, 1108), (0.2, 0.18, 1319)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
    apply_envelope(buf, 0.0, 0.4, attack=0.005, decay=0.05, sustain=0.7, release=0.34, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_combo_break(out: Path) -> None:
    """Combo gerissen — abfallender Sweep."""
    buf = make_buf(0.4)
    add_sweep(buf, 0.0, 0.35, 800, 200, vol=0.4)
    apply_lowpass(buf, 1500)
    apply_envelope(buf, 0.0, 0.4, attack=0.005, decay=0.05, sustain=0.5, release=0.34, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_combo_milestone(out: Path) -> None:
    """Combo-Meilenstein x10."""
    buf = make_buf(0.6)
    notes = [(0.0, 0.1, 880), (0.1, 0.1, 1175), (0.2, 0.1, 1568), (0.3, 0.3, 2093)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 0.5, vol=0.15)
    apply_envelope(buf, 0.0, 0.6, attack=0.005, decay=0.1, sustain=0.7, release=0.49, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_xp_pickup(out: Path) -> None:
    buf = make_buf(0.2)
    add_sine(buf, 0.0, 0.07, 1200, vol=0.3)
    add_sine(buf, 0.05, 0.12, 1600, vol=0.3)
    apply_envelope(buf, 0.0, 0.2, attack=0.005, decay=0.05, sustain=0.5, release=0.14, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_screw_pickup(out: Path) -> None:
    """Goldschrauben-Pickup."""
    buf = make_buf(0.3)
    add_sine(buf, 0.0, 0.1, 1500, vol=0.3)
    add_sine(buf, 0.07, 0.18, 2000, vol=0.25)
    apply_envelope(buf, 0.0, 0.3, attack=0.005, decay=0.05, sustain=0.5, release=0.24, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_progress_tick(out: Path) -> None:
    """Fortschritts-Tick (Progress-Bar bewegt sich)."""
    buf = make_buf(0.05)
    add_sine(buf, 0.0, 0.04, 2000, vol=0.25)
    apply_envelope(buf, 0.0, 0.05, attack=0.001, decay=0.03, sustain=0.0, release=0.019, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_unlock_feature(out: Path) -> None:
    """Feature-Unlock (z.B. Workshop, Tab)."""
    buf = make_buf(0.7)
    add_sweep(buf, 0.0, 0.4, 400, 1200, vol=0.3)
    add_sine(buf, 0.4, 0.3, 1568, vol=0.3)
    add_sine(buf, 0.4, 0.3, 1976, vol=0.2)
    apply_envelope(buf, 0.0, 0.7, attack=0.005, decay=0.1, sustain=0.7, release=0.59, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_tooltip_show(out: Path) -> None:
    buf = make_buf(0.15)
    add_sine(buf, 0.0, 0.1, 1100, vol=0.25)
    apply_envelope(buf, 0.0, 0.15, attack=0.01, decay=0.05, sustain=0.3, release=0.09, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_workshop_idle_loop(out: Path) -> None:
    """Workshop-Idle-Ambient (Wirtschafts-Hintergrund)."""
    buf = make_buf(0.8)
    add_noise(buf, 0.0, 0.8, vol=0.08, seed=900)
    apply_lowpass(buf, 600)
    apply_envelope(buf, 0.0, 0.8, attack=0.1, decay=0.1, sustain=0.6, release=0.6, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_card_flip(out: Path) -> None:
    """Karten-Flip (Lucky-Spin / Pack-Open)."""
    buf = make_buf(0.3)
    add_sweep(buf, 0.0, 0.1, 500, 1500, vol=0.3)
    add_noise(buf, 0.05, 0.05, vol=0.2, seed=901)
    add_sweep(buf, 0.15, 0.15, 1500, 700, vol=0.3)
    apply_envelope(buf, 0.0, 0.3, attack=0.005, decay=0.05, sustain=0.5, release=0.24, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_pack_open(out: Path) -> None:
    """Pack-Reveal (z.B. Worker-Markt-Refresh)."""
    buf = make_buf(0.7)
    add_noise(buf, 0.0, 0.2, vol=0.25, seed=902)
    apply_lowpass(buf, 2000)
    notes = [(0.2, 0.15, 880), (0.35, 0.35, 1319)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
    apply_envelope(buf, 0.0, 0.7, attack=0.005, decay=0.1, sustain=0.6, release=0.59, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_dice_roll(out: Path) -> None:
    """Wuerfeln (z.B. RNG-Drop)."""
    buf = make_buf(0.5)
    for i in range(8):
        add_noise(buf, 0.05 * i, 0.04, vol=0.3, seed=903 + i)
    apply_lowpass(buf, 1500)
    apply_envelope(buf, 0.0, 0.5, attack=0.01, decay=0.05, sustain=0.6, release=0.43, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_drop_common(out: Path) -> None:
    """Common-Drop-Stinger."""
    buf = make_buf(0.25)
    add_sine(buf, 0.0, 0.2, 800, vol=0.3)
    apply_envelope(buf, 0.0, 0.25, attack=0.005, decay=0.05, sustain=0.5, release=0.19, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_drop_rare(out: Path) -> None:
    """Rare-Drop-Stinger."""
    buf = make_buf(0.5)
    notes = [(0.0, 0.15, 880), (0.12, 0.35, 1319)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 2, vol=0.1)
    apply_envelope(buf, 0.0, 0.5, attack=0.005, decay=0.1, sustain=0.6, release=0.39, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_drop_epic(out: Path) -> None:
    """Epic-Drop-Stinger."""
    buf = make_buf(0.8)
    notes = [(0.0, 0.15, 880), (0.12, 0.15, 1175), (0.24, 0.5, 1568)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
        add_sine(buf, s, d, f * 2, vol=0.12)
    apply_envelope(buf, 0.0, 0.8, attack=0.005, decay=0.1, sustain=0.7, release=0.69, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_drop_legendary(out: Path) -> None:
    """Legendary-Drop-Stinger (rare event!)"""
    buf = make_buf(1.2)
    add_sweep(buf, 0.0, 0.4, 200, 800, vol=0.3)
    notes = [(0.4, 0.2, 880), (0.6, 0.2, 1319), (0.8, 0.4, 2093)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.32)
        add_sine(buf, s, d, f * 0.5, vol=0.18)
        add_sine(buf, s, d, f * 2, vol=0.12)
    apply_envelope(buf, 0.0, 1.2, attack=0.005, decay=0.15, sustain=0.7, release=1.04, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_inventory_full(out: Path) -> None:
    """Inventar voll Warnung."""
    buf = make_buf(0.4)
    add_sine(buf, 0.0, 0.1, 600, vol=0.4)
    add_sine(buf, 0.15, 0.1, 500, vol=0.4)
    apply_envelope(buf, 0.0, 0.4, attack=0.005, decay=0.05, sustain=0.6, release=0.34, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_chest_open(out: Path) -> None:
    """Truhe-Öffnen."""
    buf = make_buf(0.6)
    add_noise(buf, 0.0, 0.15, vol=0.2, seed=910)
    apply_lowpass(buf, 1500)
    notes = [(0.15, 0.15, 660), (0.3, 0.3, 990)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
    apply_envelope(buf, 0.0, 0.6, attack=0.005, decay=0.1, sustain=0.6, release=0.49, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_chest_locked(out: Path) -> None:
    """Truhe gesperrt."""
    buf = make_buf(0.3)
    add_sine(buf, 0.0, 0.08, 350, vol=0.4)
    add_noise(buf, 0.08, 0.05, vol=0.2, seed=911)
    add_sine(buf, 0.15, 0.1, 300, vol=0.4)
    apply_lowpass(buf, 1000)
    apply_envelope(buf, 0.0, 0.3, attack=0.005, decay=0.05, sustain=0.6, release=0.24, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_speedboost_activated(out: Path) -> None:
    """Speed-Boost aktiviert (Power-Up)."""
    buf = make_buf(0.5)
    add_sweep(buf, 0.0, 0.4, 300, 1800, vol=0.4)
    add_sine(buf, 0.4, 0.1, 2000, vol=0.3)
    apply_envelope(buf, 0.0, 0.5, attack=0.005, decay=0.1, sustain=0.7, release=0.39, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_speedboost_expired(out: Path) -> None:
    """Boost abgelaufen."""
    buf = make_buf(0.4)
    add_sweep(buf, 0.0, 0.35, 1500, 600, vol=0.3)
    apply_envelope(buf, 0.0, 0.4, attack=0.01, decay=0.1, sustain=0.5, release=0.29, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_research_unlocked(out: Path) -> None:
    """Forschungs-Branch unlocked."""
    buf = make_buf(0.7)
    add_sweep(buf, 0.0, 0.3, 600, 1500, vol=0.3)
    add_sine(buf, 0.3, 0.4, 1568, vol=0.3)
    add_sine(buf, 0.3, 0.4, 1976, vol=0.2)
    apply_envelope(buf, 0.0, 0.7, attack=0.005, decay=0.1, sustain=0.7, release=0.59, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_event_starting(out: Path) -> None:
    """Game-Event startet."""
    buf = make_buf(1.0)
    add_sweep(buf, 0.0, 0.6, 250, 800, vol=0.35)
    notes = [(0.6, 0.15, 880), (0.75, 0.25, 1319)]
    for s, d, f in notes:
        add_sine(buf, s, d, f, vol=0.3)
    apply_envelope(buf, 0.0, 1.0, attack=0.05, decay=0.1, sustain=0.7, release=0.84, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_event_ending(out: Path) -> None:
    """Game-Event endet."""
    buf = make_buf(0.8)
    add_sweep(buf, 0.0, 0.6, 1100, 400, vol=0.35)
    apply_envelope(buf, 0.0, 0.8, attack=0.01, decay=0.1, sustain=0.6, release=0.69, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_news_ping(out: Path) -> None:
    """Notification-Center Bell-Ping."""
    buf = make_buf(0.4)
    add_sine(buf, 0.0, 0.35, 1318, vol=0.3)
    add_sine(buf, 0.0, 0.35, 1976, vol=0.18)
    apply_envelope(buf, 0.0, 0.4, attack=0.005, decay=0.05, sustain=0.6, release=0.34, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_typing(out: Path) -> None:
    """Story-Text-Typing."""
    buf = make_buf(0.04)
    add_sine(buf, 0.0, 0.03, 1200, vol=0.18)
    apply_envelope(buf, 0.0, 0.04, attack=0.001, decay=0.025, sustain=0.0, release=0.014, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_warning_general(out: Path) -> None:
    """Allgemeine Warnung."""
    buf = make_buf(0.5)
    add_sine(buf, 0.0, 0.1, 700, vol=0.4)
    add_sine(buf, 0.15, 0.1, 700, vol=0.4)
    add_sine(buf, 0.3, 0.15, 700, vol=0.4)
    apply_envelope(buf, 0.0, 0.5, attack=0.005, decay=0.05, sustain=0.6, release=0.44, peak=1.0)
    soft_limit(buf)
    write_wav(out, buf)


def sfx_step_grass(out: Path) -> None:
    """Atmosphaere: Schritt auf Gras."""
    buf = make_buf(0.15)
    add_noise(buf, 0.0, 0.08, vol=0.3, seed=920)
    apply_lowpass(buf, 1200)
    apply_envelope(buf, 0.0, 0.15, attack=0.005, decay=0.04, sustain=0.0, release=0.105, peak=0.8)
    soft_limit(buf)
    write_wav(out, buf)


# ----------------------------------------------------------------------
# Music-Generator
# ----------------------------------------------------------------------
def make_music_pad(duration: float, root: float, scale_intervals: list[float],
                   layers: int = 3, pad_vol: float = 0.18) -> list[float]:
    buf = make_buf(duration)
    for i, semi in enumerate(scale_intervals[:layers]):
        f = root * (2 ** (semi / 12))
        # Detune-Layer fuer Tiefe
        add_sine(buf, 0.0, duration, f * 0.998, vol=pad_vol)
        add_sine(buf, 0.0, duration, f * 1.002, vol=pad_vol)
        add_sine(buf, 0.0, duration, f, vol=pad_vol * 1.2)
    return buf


def music_idle_workshop(out: Path, duration: float = 60.0) -> None:
    """Warm, akustisch, idle-loop. C-major Pad mit sanften Bewegungen."""
    print(f"  -> music idle ({duration:.0f}s)")
    buf = make_buf(duration)

    # Bass-Drone (C2)
    add_sine(buf, 0.0, duration, 65.41, vol=0.18)
    add_sine(buf, 0.0, duration, 65.41 * 1.005, vol=0.12)

    # Pad-Layer (C-E-G-C-major)
    pad_notes = [261.63, 329.63, 392.00, 523.25]  # C4 E4 G4 C5
    for f in pad_notes:
        add_sine(buf, 0.0, duration, f, vol=0.08)
        add_sine(buf, 0.0, duration, f * 1.003, vol=0.06)

    # Langsame melodische Bewegung — 8s pro Phrase
    melody = [
        (0.0, 4.0, 523.25),    # C5
        (4.0, 4.0, 587.33),    # D5
        (8.0, 4.0, 659.25),    # E5
        (12.0, 4.0, 587.33),   # D5
        (16.0, 4.0, 523.25),   # C5
        (20.0, 4.0, 392.00),   # G4
        (24.0, 4.0, 440.00),   # A4
        (28.0, 4.0, 523.25),   # C5
    ]
    # Wiederholen so oft wie noetig
    cycle_dur = 32.0
    cycles = int(math.ceil(duration / cycle_dur))
    for c in range(cycles):
        for s, d, f in melody:
            t = c * cycle_dur + s
            if t >= duration:
                break
            actual_d = min(d, duration - t)
            add_sine(buf, t, actual_d, f, vol=0.07)
            # Vibrato: Untere Oktav-Layer
            add_sine(buf, t, actual_d, f * 0.5, vol=0.04)

    # Sanfte Hi-Hat-aehnliche Akzente alle 4 Sekunden
    for i in range(int(duration / 4)):
        t = i * 4.0
        add_noise(buf, t, 0.05, vol=0.03, seed=1000 + i)

    # Lowpass fuer Waerme
    apply_lowpass(buf, 4000)
    # Loop-Fade
    apply_fade_in(buf, 0.0, 0.5)
    apply_fade_out(buf, duration - 0.5, 0.5)
    soft_limit(buf)
    normalize(buf, target=0.78)
    write_wav(out, buf)


def music_boss_tournament(out: Path, duration: float = 60.0) -> None:
    """Energisch, treibend, MiniGame/Tournament Loop. A-minor."""
    print(f"  -> music boss ({duration:.0f}s)")
    buf = make_buf(duration)

    # Bass-Pulse (A2 mit On-Beat-Hits)
    bpm = 130
    beat_dur = 60.0 / bpm
    n_beats = int(duration / beat_dur)
    for i in range(n_beats):
        t = i * beat_dur
        if t + beat_dur > duration:
            break
        # On-beat Bass-Hit (A2 = 110)
        add_sine(buf, t, beat_dur * 0.4, 110, vol=0.35)
        add_sine(buf, t, beat_dur * 0.4, 55, vol=0.25)

    # Kick-aehnliche Akzente alle 2 Beats
    for i in range(0, n_beats, 2):
        t = i * beat_dur
        if t >= duration:
            break
        add_sweep(buf, t, 0.05, 200, 60, vol=0.4)

    # Lead-Melodie (A-minor pentatonic) — 16s pro Phrase
    lead_pattern = [
        (0.0, 0.5, 440.00),    # A4
        (0.5, 0.5, 523.25),    # C5
        (1.0, 0.5, 659.25),    # E5
        (1.5, 1.0, 880.00),    # A5
        (2.5, 0.5, 783.99),    # G5
        (3.0, 0.5, 659.25),    # E5
        (3.5, 0.5, 523.25),    # C5
        (4.0, 1.0, 440.00),    # A4
        (5.0, 0.5, 392.00),    # G4
        (5.5, 0.5, 440.00),    # A4
        (6.0, 0.5, 523.25),    # C5
        (6.5, 1.0, 659.25),    # E5
        (7.5, 0.5, 880.00),    # A5
    ]
    cycle = 8.0
    cycles = int(math.ceil(duration / cycle))
    for c in range(cycles):
        for s, d, f in lead_pattern:
            t = c * cycle + s
            if t >= duration:
                break
            actual_d = min(d, duration - t)
            add_saw(buf, t, actual_d, f, vol=0.18)
            add_sine(buf, t, actual_d, f * 2, vol=0.06)

    # Hi-Hat-Pattern — alle 1/4 Beats
    for i in range(n_beats * 2):
        t = i * beat_dur * 0.5
        if t >= duration:
            break
        add_noise(buf, t, 0.04, vol=0.06, seed=2000 + i)

    # Filter
    apply_highpass(buf, 60)
    apply_lowpass(buf, 6000)
    apply_fade_in(buf, 0.0, 0.3)
    apply_fade_out(buf, duration - 0.3, 0.3)
    soft_limit(buf)
    normalize(buf, target=0.78)
    write_wav(out, buf)


def music_celebration(out: Path, duration: float = 30.0) -> None:
    """Triumph-Loop fuer Prestige/DailyReward. Major-Tonart."""
    print(f"  -> music celebration ({duration:.0f}s)")
    buf = make_buf(duration)

    # Bass G3
    add_sine(buf, 0.0, duration, 196, vol=0.25)
    add_sine(buf, 0.0, duration, 98, vol=0.2)

    # Akkord-Layer (G-major: G-B-D)
    chord = [196, 246.94, 293.66, 392, 493.88]  # G3 B3 D4 G4 B4
    for f in chord:
        add_sine(buf, 0.0, duration, f, vol=0.08)
        add_sine(buf, 0.0, duration, f * 1.005, vol=0.05)

    # Melodische Hooks (8s Phrase)
    hook = [
        (0.0, 1.0, 587.33),    # D5
        (1.0, 1.0, 783.99),    # G5
        (2.0, 0.5, 880.00),    # A5
        (2.5, 1.5, 987.77),    # B5
        (4.0, 1.0, 880.00),    # A5
        (5.0, 1.0, 783.99),    # G5
        (6.0, 1.0, 587.33),    # D5
        (7.0, 1.0, 783.99),    # G5
    ]
    cycle = 8.0
    cycles = int(math.ceil(duration / cycle))
    for c in range(cycles):
        for s, d, f in hook:
            t = c * cycle + s
            if t >= duration:
                break
            actual_d = min(d, duration - t)
            add_sine(buf, t, actual_d, f, vol=0.12)
            add_sine(buf, t, actual_d, f * 2, vol=0.05)

    # Glitter-Akzente (2x pro 4s)
    for i in range(int(duration * 2 / 4)):
        t = i * 2.0 + 0.5
        if t >= duration:
            break
        add_sine(buf, t, 0.3, 1568, vol=0.05)
        add_sine(buf, t, 0.3, 2093, vol=0.04)

    apply_lowpass(buf, 5500)
    apply_fade_in(buf, 0.0, 0.3)
    apply_fade_out(buf, duration - 0.3, 0.3)
    soft_limit(buf)
    normalize(buf, target=0.80)
    write_wav(out, buf)


def music_ambient_evening(out: Path, duration: float = 90.0) -> None:
    """Chill-Out Saison-Variant. Dorian-Modus."""
    print(f"  -> music ambient ({duration:.0f}s)")
    buf = make_buf(duration)

    # Sub-Bass D2
    add_sine(buf, 0.0, duration, 73.42, vol=0.2)

    # Slow-Pad: D-F-A-C
    pad = [146.83, 174.61, 220.00, 261.63]
    for f in pad:
        add_sine(buf, 0.0, duration, f, vol=0.07)
        add_sine(buf, 0.0, duration, f * 1.004, vol=0.05)

    # Sehr langsame Melodie
    slow_melody = [
        (0.0, 8.0, 440.00),   # A4
        (8.0, 8.0, 523.25),   # C5
        (16.0, 8.0, 587.33),  # D5
        (24.0, 8.0, 523.25),  # C5
        (32.0, 8.0, 440.00),  # A4
        (40.0, 8.0, 392.00),  # G4
    ]
    cycle = 48.0
    cycles = int(math.ceil(duration / cycle))
    for c in range(cycles):
        for s, d, f in slow_melody:
            t = c * cycle + s
            if t >= duration:
                break
            actual_d = min(d, duration - t)
            # Mit Schwebung
            add_sine(buf, t, actual_d, f, vol=0.06)
            add_sine(buf, t, actual_d, f * 1.003, vol=0.04)

    # Subtile Atmosphaere
    for i in range(int(duration / 6)):
        t = i * 6.0
        add_noise(buf, t, 0.5, vol=0.02, seed=3000 + i)

    apply_lowpass(buf, 3500)
    apply_fade_in(buf, 0.0, 1.0)
    apply_fade_out(buf, duration - 1.0, 1.0)
    soft_limit(buf)
    normalize(buf, target=0.72)
    write_wav(out, buf)


# ----------------------------------------------------------------------
# Registry
# ----------------------------------------------------------------------
SFX_REGISTRY: dict[str, callable] = {
    # UI
    "sfx_ui_hover": sfx_ui_hover,
    "sfx_ui_swipe": sfx_ui_swipe,
    "sfx_ui_modal_open": sfx_ui_modal_open,
    "sfx_ui_modal_close": sfx_ui_modal_close,
    "sfx_ui_tab_switch": sfx_ui_tab_switch,
    "sfx_ui_notification_pop": sfx_ui_notification_pop,
    "sfx_ui_back": sfx_ui_back,

    # Wirtschaft
    "sfx_money_big": sfx_money_big,
    "sfx_low_money_warning": sfx_low_money_warning,
    "sfx_costs_paid": sfx_costs_paid,

    # Worker
    "sfx_worker_promoted": sfx_worker_promoted,
    "sfx_worker_quit": sfx_worker_quit,
    "sfx_worker_mood_warn": sfx_worker_mood_warn,
    "sfx_worker_resting": sfx_worker_resting,
    "sfx_intern_ready": sfx_intern_ready,

    # Achievement / Progression
    "sfx_achievement_unlock": sfx_achievement_unlock,
    "sfx_achievement_legendary": sfx_achievement_legendary,
    "sfx_streak_save": sfx_streak_save,
    "sfx_streak_milestone": sfx_streak_milestone,
    "sfx_milestone_minor": sfx_milestone_minor,
    "sfx_milestone_major": sfx_milestone_major,

    # Prestige / Ascension / Rebirth
    "sfx_prestige_start": sfx_prestige_start,
    "sfx_prestige_complete": sfx_prestige_complete,
    "sfx_ascension": sfx_ascension,
    "sfx_rebirth_star": sfx_rebirth_star,
    "sfx_rebirth_complete": sfx_rebirth_complete,

    # Order
    "sfx_order_accept": sfx_order_accept,
    "sfx_order_live_spawn": sfx_order_live_spawn,
    "sfx_order_vip_spawn": sfx_order_vip_spawn,
    "sfx_order_expired": sfx_order_expired,
    "sfx_order_risk_win": sfx_order_risk_win,
    "sfx_order_risk_loss": sfx_order_risk_loss,

    # Building / Research / Crafting
    "sfx_building_complete": sfx_building_complete,
    "sfx_research_complete": sfx_research_complete,
    "sfx_research_start": sfx_research_start,
    "sfx_crafting_complete": sfx_crafting_complete,

    # Daily / Weekly / BattlePass
    "sfx_daily_reward": sfx_daily_reward,
    "sfx_daily_challenge_complete": sfx_daily_challenge_complete,
    "sfx_weekly_mission_complete": sfx_weekly_mission_complete,
    "sfx_battle_pass_tier_up": sfx_battle_pass_tier_up,

    # Lucky Spin
    "sfx_lucky_spin_spin": sfx_lucky_spin_spin,
    "sfx_lucky_spin_tick": sfx_lucky_spin_tick,
    "sfx_lucky_spin_win_small": sfx_lucky_spin_win_small,
    "sfx_lucky_spin_win_big": sfx_lucky_spin_win_big,
    "sfx_lucky_spin_jackpot": sfx_lucky_spin_jackpot,

    # Manager / Tools
    "sfx_manager_unlocked": sfx_manager_unlocked,
    "sfx_master_tool_unlock": sfx_master_tool_unlock,

    # Guild
    "sfx_guild_join": sfx_guild_join,
    "sfx_guild_leave": sfx_guild_leave,
    "sfx_boss_spawn": sfx_boss_spawn,
    "sfx_boss_defeated": sfx_boss_defeated,
    "sfx_war_start": sfx_war_start,
    "sfx_war_end": sfx_war_end,

    # Welcome / Special
    "sfx_welcome_back": sfx_welcome_back,
    "sfx_starter_offer": sfx_starter_offer,
    "sfx_offline_earnings": sfx_offline_earnings,
    "sfx_tutorial_step": sfx_tutorial_step,

    # Negative Events
    "sfx_tax_audit": sfx_tax_audit,
    "sfx_worker_strike": sfx_worker_strike,

    # Generic Events
    "sfx_event_positive": sfx_event_positive,
    "sfx_event_negative": sfx_event_negative,

    # System
    "sfx_save_success": sfx_save_success,
    "sfx_error_general": sfx_error_general,

    # Purchase / Rush
    "sfx_purchase_success": sfx_purchase_success,
    "sfx_rush_activated": sfx_rush_activated,
    "sfx_delivery_received": sfx_delivery_received,

    # Misc
    "sfx_celebration_horn": sfx_celebration_horn,

    # Erweiterung 100-Pack
    "sfx_button_tap_alt": sfx_button_tap_alt,
    "sfx_button_tap_negative": sfx_button_tap_negative,
    "sfx_close_dialog_subtle": sfx_close_dialog_subtle,
    "sfx_swipe_left": sfx_swipe_left,
    "sfx_swipe_right": sfx_swipe_right,
    "sfx_long_press": sfx_long_press,
    "sfx_perfect_chain": sfx_perfect_chain,
    "sfx_combo_break": sfx_combo_break,
    "sfx_combo_milestone": sfx_combo_milestone,
    "sfx_xp_pickup": sfx_xp_pickup,
    "sfx_screw_pickup": sfx_screw_pickup,
    "sfx_progress_tick": sfx_progress_tick,
    "sfx_unlock_feature": sfx_unlock_feature,
    "sfx_tooltip_show": sfx_tooltip_show,
    "sfx_workshop_idle_loop": sfx_workshop_idle_loop,
    "sfx_card_flip": sfx_card_flip,
    "sfx_pack_open": sfx_pack_open,
    "sfx_dice_roll": sfx_dice_roll,
    "sfx_drop_common": sfx_drop_common,
    "sfx_drop_rare": sfx_drop_rare,
    "sfx_drop_epic": sfx_drop_epic,
    "sfx_drop_legendary": sfx_drop_legendary,
    "sfx_inventory_full": sfx_inventory_full,
    "sfx_chest_open": sfx_chest_open,
    "sfx_chest_locked": sfx_chest_locked,
    "sfx_speedboost_activated": sfx_speedboost_activated,
    "sfx_speedboost_expired": sfx_speedboost_expired,
    "sfx_research_unlocked": sfx_research_unlocked,
    "sfx_event_starting": sfx_event_starting,
    "sfx_event_ending": sfx_event_ending,
    "sfx_news_ping": sfx_news_ping,
    "sfx_typing": sfx_typing,
    "sfx_warning_general": sfx_warning_general,
    "sfx_step_grass": sfx_step_grass,
}

MUSIC_REGISTRY: dict[str, tuple[callable, float]] = {
    "music_idle_workshop": (music_idle_workshop, 60.0),
    "music_boss_tournament": (music_boss_tournament, 60.0),
    "music_celebration": (music_celebration, 30.0),
    "music_ambient_evening": (music_ambient_evening, 90.0),
}


# ----------------------------------------------------------------------
# Pipeline
# ----------------------------------------------------------------------
def build_sfx() -> int:
    print(f"=== SFX-Pack ({len(SFX_REGISTRY)} Files) ===")
    count = 0
    for name, fn in SFX_REGISTRY.items():
        wav_path = WORK_DIR / f"{name}.wav"
        ogg_path = SFX_DIR / f"{name}.ogg"
        try:
            fn(wav_path)
            wav_to_ogg(wav_path, ogg_path, bitrate="80k")
            wav_path.unlink(missing_ok=True)
            print(f"  {name}.ogg")
            count += 1
        except Exception as e:
            print(f"  FEHLER {name}: {e}")
    return count


def build_music() -> int:
    print(f"=== Music-Pack ({len(MUSIC_REGISTRY)} Loops) ===")
    count = 0
    for name, (fn, duration) in MUSIC_REGISTRY.items():
        wav_path = WORK_DIR / f"{name}.wav"
        ogg_path = MUSIC_DIR / f"{name}.ogg"
        try:
            fn(wav_path, duration)
            wav_to_ogg(wav_path, ogg_path, bitrate="128k")
            wav_path.unlink(missing_ok=True)
            print(f"  {name}.ogg ({duration:.0f}s)")
            count += 1
        except Exception as e:
            print(f"  FEHLER {name}: {e}")
    return count


def main() -> None:
    parser = argparse.ArgumentParser(description="HandwerkerImperium SoundForge")
    parser.add_argument("--only", choices=["sfx", "music"], help="Nur SFX oder nur Music")
    args = parser.parse_args()

    if args.only != "music":
        n_sfx = build_sfx()
        print(f"=> {n_sfx} SFX erzeugt in {SFX_DIR}")

    if args.only != "sfx":
        n_music = build_music()
        print(f"=> {n_music} Music-Loops erzeugt in {MUSIC_DIR}")


if __name__ == "__main__":
    main()
