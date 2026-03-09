# RebornSaga: Komplette Pipeline — Implementierungsplan

> **Status (7. März 2026):** Code-Phasen (0, 3a, 3b, 4) ERLEDIGT. Python-Scripts (1, 2, 5) ERSTELLT. Code-Review durchgeführt + alle Findings gefixt. Verbleibend: Scripts ausführen (ComfyUI/kohya starten), Audio-Assets beschaffen, Firebase-Key erstellen.

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Komplette Asset-Pipeline (Style-LoRA Training → ~301 visuelle Assets + 33 Overlays generieren) + App-Integration (Download-Screen, Sprites, Kampf-Verbesserungen, Dialog-Panels, Audio, Lokalisierung, Firebase Delivery) für RebornSaga fertigstellen.

**Architecture:** Style-LoRA auf Solo-Leveling-Ästhetik trainieren, mit existierenden Character-LoRAs stacken, alle Assets generieren (Sprites, Enemies, Backgrounds, Items, Map), in die bestehende SkiaSharp-Engine integrieren (Szenen + Overlays), Audio-Assets beschaffen, UI-Strings lokalisieren, Firebase-Upload und Download-Screen implementieren.

**Tech Stack:** ComfyUI + Animagine XL 4.0 (SDXL), kohya_ss (LoRA-Training), SkiaSharp (Rendering), Avalonia 11.3 + .NET 10, Firebase Storage, Python 3.9

**Design-Dokument:** `docs/plans/2026-03-07-reborn-saga-complete-pipeline-design.md`

### Pflicht-Regeln für die Ausführung

1. **Code-Review nach JEDER Phase:** Nach Abschluss aller Tasks einer Phase ein umfassendes Code-Review gegen die tatsächliche Codebase durchführen. Geschriebener Code muss gegen die echten APIs/Signaturen/Property-Namen verifiziert werden. Falsche Aufrufe sofort fixen — NICHT erst am Ende.

2. **Keine offenen TODOs:** Am Ende der Implementierung darf KEIN `// TODO`, `// OFFEN`, `// PRÜFEN`, `// Phase X` oder ähnlicher Platzhalter-Kommentar im Code stehen. Alles muss erledigt sein. Wenn ein TODO identifiziert wird, sofort umsetzen oder als separaten Task dokumentieren.

3. **Build nach jedem Task:** `dotnet build src/Apps/RebornSaga/RebornSaga.Shared` muss nach jedem Code-Task erfolgreich sein. Kein kaputten Zwischenstand hinterlassen.

4. **Dateien selbst kopieren:** Asset-Dateien (Enemies, Backgrounds, Items, Map) werden per Shell-Befehle in die richtigen Deploy-Verzeichnisse kopiert — kein manueller Schritt für Robert.

---

## Phase 0: Kritischer Bugfix (SKMaskFilter Disposal)

### Task 0.1: SKMaskFilter Disposal-Bug fixen

**Problem:** 7 Dateien haben `static readonly SKMaskFilter _glowBlur` (bzw. `_glow`) und rufen `.Dispose()` in `Cleanup()` auf. Nach Dispose crasht jede weitere Nutzung.

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Overlays/LevelUpOverlay.cs:218`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Overlays/ChapterUnlockOverlay.cs:267`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Overlays/SystemMessageOverlay.cs:120`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Overlays/PauseOverlay.cs:256`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs:1408`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/UI/StatusWindowRenderer.cs:258`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Effects/SplashArtRenderer.cs:136`

**Step 1: Dispose-Aufrufe entfernen** ✅ ERLEDIGT

In allen 7 Dateien die Zeile `_glowBlur.Dispose();` (bzw. `_glow.Dispose();`) aus `Cleanup()` entfernt. Statische Ressourcen dürfen NICHT pro-Instanz disposed werden.

`LevelUpOverlay.cs:218` — Zeile entfernen
`ChapterUnlockOverlay.cs:267` — Zeile entfernen
`SystemMessageOverlay.cs:120` — Zeile entfernen
`BattleScene.cs:1408` — Zeile entfernen
`StatusWindowRenderer.cs:258` — Zeile entfernen
`SplashArtRenderer.cs:136` — `_glow.Dispose();` entfernen (gleiches Pattern)

**Step 2: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

Expected: Build Succeeded

**Step 3: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Overlays/LevelUpOverlay.cs \
        src/Apps/RebornSaga/RebornSaga.Shared/Overlays/ChapterUnlockOverlay.cs \
        src/Apps/RebornSaga/RebornSaga.Shared/Overlays/SystemMessageOverlay.cs \
        src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs \
        src/Apps/RebornSaga/RebornSaga.Shared/Rendering/UI/StatusWindowRenderer.cs \
        src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Effects/SplashArtRenderer.cs
git commit -m "Fix: SKMaskFilter Disposal-Bug in 6 Dateien — statische Filter nicht in Cleanup() disposen"
```

### Phase 0 — Code-Review Checkpoint

Build ausführen, alle 6 geänderten Dateien prüfen: Dispose-Zeilen tatsächlich entfernt? Keine neuen Compile-Fehler? Grep nach `_glowBlur.Dispose\|_glow.Dispose` in der gesamten RebornSaga-Codebase — 0 Treffer erwartet.

---

## Phase 1: Style-LoRA Training

### Task 1.1: Training-Daten kuratieren

**Ziel:** 25-30 hochwertige Solo-Leveling-Referenzbilder aus dem SL:A Dataset auswählen.

**Files:**
- Create: `F:/AI/ComfyUI_workflows/curate_style_training.py`

**Step 1: Curation-Script schreiben**

Das Script soll:
1. `F:/AI/datasets/solo_leveling_arise_assets/` durchsuchen
2. Bilder ≥ 1024px (beide Seiten) filtern
3. Diverse Kategorien abdecken (8 Characters, 5 Bosses, 4 Weapons, 5 Backgrounds, 3 Items, 2-3 Icons)
4. In `F:/AI/datasets/sololeveling_style_training/` kopieren
5. Zusammenfassung ausgeben

```python
"""
Kuratiert 25-30 Solo Leveling Arise Bilder für Style-LoRA Training.
Filtert nach Mindest-Auflösung und wählt diverse Kategorien.
"""
import os
import shutil
from pathlib import Path
from PIL import Image

SRC = Path("F:/AI/datasets/solo_leveling_arise_assets")
DST = Path("F:/AI/datasets/sololeveling_style_training")

# Kategorie → (Unterordner, Anzahl gewünscht)
CATEGORIES = {
    "characters": (["Characters"], 8),
    "bosses": (["Boss - Mobs"], 5),
    "weapons": (["Weapons"], 4),
    "backgrounds": (["Stuff/Backgrounds"], 5),
    "items": (["Artifacts", "Gems"], 3),
    "icons": (["Stuff/Icons"], 3),
}

MIN_SIZE = 1024  # Minimum für beide Dimensionen

def get_valid_images(folders, min_size):
    """Findet Bilder >= min_size in beiden Dimensionen."""
    results = []
    for folder in folders:
        path = SRC / folder
        if not path.exists():
            continue
        for f in path.rglob("*"):
            if f.suffix.lower() in (".png", ".jpg", ".jpeg", ".webp"):
                try:
                    with Image.open(f) as img:
                        w, h = img.size
                        if w >= min_size and h >= min_size:
                            results.append((f, w * h))  # Sortierung nach Pixelanzahl
                except Exception:
                    continue
    # Größte zuerst
    results.sort(key=lambda x: x[1], reverse=True)
    return results

def main():
    DST.mkdir(parents=True, exist_ok=True)
    total = 0

    for cat_name, (folders, count) in CATEGORIES.items():
        images = get_valid_images(folders, MIN_SIZE)
        selected = images[:count]

        for i, (src_path, _) in enumerate(selected):
            dst_name = f"{cat_name}_{i+1:02d}{src_path.suffix}"
            shutil.copy2(src_path, DST / dst_name)
            total += 1
            print(f"  [{cat_name}] {src_path.name} -> {dst_name}")

    print(f"\n{total} Bilder kuratiert in {DST}")

if __name__ == "__main__":
    main()
```

**Step 2: Script ausführen**

```bash
py F:/AI/ComfyUI_workflows/curate_style_training.py
```

Expected: 25-30 Bilder in `F:/AI/datasets/sololeveling_style_training/`

**Step 3: Manuelle Qualitätsprüfung**

Ordner öffnen, jedes Bild prüfen:
- Ist der Solo-Leveling-Stil klar erkennbar?
- Keine Duplikate oder zu ähnliche Bilder?
- Mix aus Action/Still-Life/Detail?

Ungeeignete Bilder manuell ersetzen.

---

### Task 1.2: Automatisches Captioning

**Ziel:** Captions mit WD14 Tagger generieren, Trigger-Word voranstellen.

**Step 1: WD14 Tagger ausführen**

```bash
cd F:/AI/kohya_ss && PYTHONIOENCODING=utf-8 uv run --link-mode=copy --index-strategy unsafe-best-match \
  python finetune/tag_images_by_wd14_tagger.py \
  --onnx --batch_size 4 --caption_extension .txt \
  "F:/AI/datasets/sololeveling_style_training"
```

Expected: `.txt` Datei pro Bild mit WD14-Tags

**Step 2: Trigger-Word + Caption-Cleanup**

Für jede `.txt` Datei:
1. `sololeveling_style, ` am Anfang einfügen
2. Character-spezifische Tags entfernen (z.B. "sung jin-woo", "cha hae-in")
3. Copyright-Tags entfernen ("solo leveling", "solo leveling: arise")
4. Resultat: `sololeveling_style, dark atmosphere, glowing eyes, armor, dramatic lighting, ...`

Script dafür:

```python
"""Bereinigt WD14-Captions für Style-LoRA Training."""
from pathlib import Path

DIR = Path("F:/AI/datasets/sololeveling_style_training")
TRIGGER = "sololeveling_style"
REMOVE_TAGS = {
    "sung jin-woo", "cha hae-in", "solo leveling", "solo leveling: arise",
    "igris", "beru", "goto ryuji", "thomas andre", "liu zhigang",
    "copyright", "artist name", "watermark", "web address",
}

for txt in DIR.glob("*.txt"):
    tags = txt.read_text(encoding="utf-8").strip().split(", ")
    cleaned = [t for t in tags if t.lower().strip() not in REMOVE_TAGS]
    result = f"{TRIGGER}, {', '.join(cleaned)}"
    txt.write_text(result, encoding="utf-8")
    print(f"  {txt.name}: {len(cleaned)} Tags")
```

**Step 3: Stichproben-Prüfung**

3-5 `.txt` Dateien öffnen, prüfen ob:
- Trigger-Word am Anfang
- Keine Character-Namen mehr
- Sinnvolle Stil-Tags erhalten

---

### Task 1.3: Style-LoRA trainieren

**Ziel:** `sololeveling_style.safetensors` trainieren.

**Files:**
- Create: `F:/AI/ComfyUI_workflows/train_style_lora.py`

**Step 1: Training-Script schreiben**

Basiert auf `train_lora_generic.py`, aber mit Style-spezifischen Parametern:
- `text_encoder_lr = 0` (deaktiviert — Stil ist visuell, nicht semantisch)
- `unet_lr = 3e-4` (höher als Character-LoRAs)
- `max_train_epochs = 15` (mehr als Character)
- `network_dim = 32, alpha = 16`
- `noise_offset = 0.05`
- `min_snr_gamma = 5`

```python
"""
Trainiert sololeveling_style LoRA auf Animagine XL 4.0 Opt.
Kein Text-Encoder-Training (reines Stil-LoRA).
"""
import subprocess
import sys
from pathlib import Path

KOHYA_DIR = Path("F:/AI/kohya_ss")
MODEL = "F:/AI/ComfyUI_windows_portable/ComfyUI/models/checkpoints/animagine-xl-4.0-opt.safetensors"
TRAIN_DIR = "F:/AI/datasets/sololeveling_style_training"
OUTPUT_DIR = "F:/AI/ComfyUI_windows_portable/ComfyUI/models/loras"
OUTPUT_NAME = "sololeveling_style"

# Trainingsdaten mit 10 Repeats
REPEATS = 10  # 25 Bilder x 10 = 250 pro Epoche

def main():
    # Repeats-Ordner erstellen (kohya_ss erwartet {repeats}_{name} Ordner-Struktur)
    img_dir = Path(TRAIN_DIR)
    repeat_dir = img_dir.parent / f"sololeveling_style_train" / f"{REPEATS}_sololeveling_style"
    repeat_dir.mkdir(parents=True, exist_ok=True)

    # Bilder + Captions in Repeat-Ordner symlinken/kopieren
    import shutil
    for f in img_dir.iterdir():
        if f.suffix.lower() in (".png", ".jpg", ".jpeg", ".webp", ".txt"):
            dst = repeat_dir / f.name
            if not dst.exists():
                shutil.copy2(f, dst)

    train_data_dir = repeat_dir.parent

    # ComfyUI VRAM freigeben
    import urllib.request
    try:
        req = urllib.request.Request(
            "http://127.0.0.1:8188/free",
            data=b'{"unload_models":true,"free_memory":true}',
            method="POST"
        )
        urllib.request.urlopen(req, timeout=5)
        print("ComfyUI VRAM freigegeben")
    except Exception:
        print("ComfyUI nicht erreichbar (OK wenn nicht gestartet)")

    cmd = [
        sys.executable, "-m", "sdxl_train_network",
        "--pretrained_model_name_or_path", str(MODEL),
        "--train_data_dir", str(train_data_dir),
        "--output_dir", str(OUTPUT_DIR),
        "--output_name", OUTPUT_NAME,
        "--network_module", "networks.lora",
        "--network_dim", "32",
        "--network_alpha", "16",
        "--resolution", "1024,1024",
        "--train_batch_size", "1",
        "--max_train_epochs", "15",
        "--learning_rate", "3e-4",
        "--unet_lr", "3e-4",
        "--text_encoder_lr", "0",
        "--optimizer_type", "AdamW8bit",
        "--lr_scheduler", "cosine",
        "--lr_warmup_steps", "100",
        "--mixed_precision", "bf16",
        "--save_precision", "bf16",
        "--save_every_n_epochs", "5",
        "--seed", "42",
        "--cache_latents",
        "--cache_latents_to_disk",
        "--xformers",
        "--shuffle_caption",
        "--keep_tokens", "1",
        "--noise_offset", "0.05",
        "--min_snr_gamma", "5",
        "--lowram",
        "--bucket_no_upscale",
        "--enable_bucket",
    ]

    print(f"Training startet: ~{250 * 15} Steps, ~66 Min auf RTX 4080")
    print(f"Output: {OUTPUT_DIR}/{OUTPUT_NAME}.safetensors")

    result = subprocess.run(
        cmd,
        cwd=str(KOHYA_DIR),
        env={**__import__("os").environ, "PYTHONIOENCODING": "utf-8"},
    )

    if result.returncode != 0:
        print(f"FEHLER: Training fehlgeschlagen (Exit Code {result.returncode})")
        sys.exit(1)

    print(f"Training abgeschlossen: {OUTPUT_DIR}/{OUTPUT_NAME}.safetensors")

if __name__ == "__main__":
    main()
```

**Step 2: Training starten**

WICHTIG: ComfyUI vorher schließen ODER VRAM freigeben!

```bash
cd F:/AI/kohya_ss && PYTHONIOENCODING=utf-8 uv run --link-mode=copy --index-strategy unsafe-best-match \
  python F:/AI/ComfyUI_workflows/train_style_lora.py
```

Expected: ~66 Min Laufzeit, Output in `ComfyUI/models/loras/sololeveling_style.safetensors`
Checkpoints bei Epoche 5 und 10 als Backup.

---

### Task 1.4: Style-LoRA validieren

**Ziel:** 8 Test-Bilder generieren, CLIP-Score messen.

**Files:**
- Create: `F:/AI/ComfyUI_workflows/validate_style_lora.py`

**Step 1: Validierungs-Script schreiben**

Generiert 8 Bilder (1 pro Kategorie) mit Style-LoRA:
- 2× Charakter (stehend, Kampfpose)
- 2× Enemy (Monster, Boss)
- 1× Waffe
- 1× Hintergrund (Dungeon)
- 1× Item (Trank)
- 1× Map-Node

Verwendet gleiche ComfyUI-API wie `validate_lora_generic.py`.

**Step 2: Generieren und visuell prüfen**

```bash
py F:/AI/ComfyUI_workflows/validate_style_lora.py
```

Prüfkriterien:
- Dunkle Atmosphäre, Blau/Gold/Violett-Akzente ✓
- Scharfe Linien, cel-shading ✓
- Keine Artefakte oder Verzerrungen ✓
- Konsistenter Stil über alle Kategorien ✓

**Step 3: Bei schlechter Qualität**

Falls Ergebnis nicht zufriedenstellend:
- Epoche-10-Checkpoint testen (evtl. besser als 15)
- Training-Daten nachbessern (unpassende Bilder tauschen)
- LR anpassen (2e-4 statt 3e-4 wenn übertrainiert)

### Phase 1 — Review Checkpoint

Validierungsbilder visuell prüfen: Stil konsistent? Charakter-Gesichter korrekt bei gestacktem LoRA? Training-Loss nicht divergiert? Wenn Style-LoRA schlecht stackt: Weight reduzieren (0.5 statt 0.6).

---

## Phase 2: Asset-Generierung (356 Assets)

### Task 2.1: Alle Generierungs-Scripts mit Style-LoRA updaten

**Ziel:** Style-LoRA in alle 5 Scripts integrieren.

**Files:**
- Modify: `F:/AI/ComfyUI_workflows/generate_game_sprites_v2.py` (aktuellste Version!)
- Modify: `F:/AI/ComfyUI_workflows/generate_enemies_v4.py` (aktuellste Version!)
- Modify: `F:/AI/ComfyUI_workflows/generate_backgrounds.py`
- Modify: `F:/AI/ComfyUI_workflows/generate_items_v5.py` → v6
- Modify: `F:/AI/ComfyUI_workflows/generate_map_v2.py` (aktuellste Version!)
- Create: `F:/AI/ComfyUI_workflows/generate_overlays.py` (33 Blink/Mund-Overlays, NEU!)

**Änderung pro Script:**

Einen `LoraLoader`-Node hinzufügen NACH dem Checkpoint-Loader:

```python
# Style-LoRA Node (in jeden Workflow einfügen)
STYLE_LORA = "sololeveling_style.safetensors"
STYLE_STRENGTH = 0.8  # 0.6 bei Character-Sprites (weil Character-LoRA gestackt)

style_lora_node = {
    "class_type": "LoraLoader",
    "inputs": {
        "model": [checkpoint_node_id, 0],
        "clip": [checkpoint_node_id, 1],
        "lora_name": STYLE_LORA,
        "strength_model": STYLE_STRENGTH,
        "strength_clip": STYLE_STRENGTH,
    }
}
```

Für `generate_game_sprites_v2.py`: Style-LoRA (0.6) → Character-LoRA (0.8) → KSampler
Für alle anderen: Style-LoRA (0.8) → KSampler (kein Character-LoRA)

**Neues Script: `generate_overlays.py`**

Generiert 33 Overlay-Assets (11 Chars × 3: blink, mouth_open, mouth_wide).
Basiert auf `generate_aria_emotions_v2.py` (hat bereits Overlay-Logik für Aria), aber generisch für alle Chars:

- Input: Referenz-Sprite (standing_neutral) pro Charakter
- Methode: LoRA + spezialisierter Prompt (z.B. "eyes fully closed" für blink)
- Output: Overlay-WebP (nur Augen-/Mund-Partien, Rest transparent)
- Rembg NICHT verwenden (Overlay bleibt auf voller Auflösung 832×1216)
- Manuelles Cropping/Masking der relevanten Region als Post-Processing

Trigger-Word `sololeveling_style` am Anfang jedes Prompts einfügen.

---

### Task 2.2: Assets generieren (Batch)

**Reihenfolge:** Hintergründe → Enemies → Items → Map → Sprites (aufsteigend nach Komplexität)

**Step 1: Hintergründe (14 Bilder, ~15 Min)**

```bash
py F:/AI/ComfyUI_workflows/generate_backgrounds.py
```

**Step 2: Gegner (30 Bilder, ~30 Min)**

```bash
py F:/AI/ComfyUI_workflows/generate_enemies_v4.py
```

**Step 3: Items (58 Bilder, ~25 Min)**

```bash
py F:/AI/ComfyUI_workflows/generate_items_v5.py
```

Oder v6 wenn umbenannt nach Style-LoRA-Update.

**Step 4: Map-Assets (19 Bilder, ~15 Min)**

```bash
py F:/AI/ComfyUI_workflows/generate_map_v2.py
```

**Step 5: Charakter-Sprites (147 Bilder, ~60 Min)**

```bash
py F:/AI/ComfyUI_workflows/generate_game_sprites_v2.py all
```

**Step 6: Charakter-Overlays (33 Bilder, ~30 Min)**

```bash
py F:/AI/ComfyUI_workflows/generate_overlays.py all
```

Generiert pro Charakter: blink.webp, mouth_open.webp, mouth_wide.webp (= 11 × 3 = 33).

**Step 7: Qualitätsprüfung**

Alle Outputs in `F:/AI/ComfyUI_windows_portable/ComfyUI/output/` prüfen:
- Konsistenter Stil über alle Kategorien?
- Keine abgeschnittenen Elemente?
- BG-Removal sauber (keine Ränder)?
- Richtige Auflösungen?

Schlechte Bilder einzeln regenerieren.

---

### Task 2.3: Assets in Deploy-Ordner kopieren

**ACHTUNG:** `copy_sprites_to_deploy.py` kopiert aktuell NUR Charakter-Sprites nach `F:\AI\RebornSaga_Assets\deploy\assets\characters\`. Enemies, Backgrounds, Items und Map werden NICHT kopiert! Das Script muss erweitert oder manuelle Copy-Schritte ausgeführt werden.

**Step 1: Charakter-Sprites kopieren (existierendes Script)**

```bash
py F:/AI/ComfyUI_workflows/copy_sprites_to_deploy.py
```

**Step 2: Restliche Assets in Deploy-Ordner kopieren**

```bash
# Deploy-Verzeichnisse erstellen
mkdir -p "F:/AI/RebornSaga_Assets/deploy/assets/enemies"
mkdir -p "F:/AI/RebornSaga_Assets/deploy/assets/backgrounds"
mkdir -p "F:/AI/RebornSaga_Assets/deploy/assets/items/weapon"
mkdir -p "F:/AI/RebornSaga_Assets/deploy/assets/items/armor"
mkdir -p "F:/AI/RebornSaga_Assets/deploy/assets/items/accessory"
mkdir -p "F:/AI/RebornSaga_Assets/deploy/assets/items/consumable"
mkdir -p "F:/AI/RebornSaga_Assets/deploy/assets/map/regions"
mkdir -p "F:/AI/RebornSaga_Assets/deploy/assets/map/nodes"

# Enemies kopieren (generate_enemies_v4.py Output)
cp F:/AI/ComfyUI_windows_portable/ComfyUI/output/rebornsaga_enemies/*.webp \
   "F:/AI/RebornSaga_Assets/deploy/assets/enemies/"

# Backgrounds kopieren (generate_backgrounds.py Output)
cp F:/AI/ComfyUI_windows_portable/ComfyUI/output/rebornsaga_backgrounds/*.webp \
   "F:/AI/RebornSaga_Assets/deploy/assets/backgrounds/"

# Items kopieren (generate_items_v5.py Output — nach Kategorie sortiert)
for cat in weapon armor accessory consumable; do
  cp F:/AI/ComfyUI_windows_portable/ComfyUI/output/rebornsaga_items/${cat}_*.webp \
     "F:/AI/RebornSaga_Assets/deploy/assets/items/${cat}/" 2>/dev/null || true
done

# Map-Regionen kopieren (generate_map_v2.py Output)
cp F:/AI/ComfyUI_windows_portable/ComfyUI/output/rebornsaga_map/region_*.webp \
   "F:/AI/RebornSaga_Assets/deploy/assets/map/regions/"

# Map-Node-Icons kopieren
cp F:/AI/ComfyUI_windows_portable/ComfyUI/output/rebornsaga_map/node_*.webp \
   "F:/AI/RebornSaga_Assets/deploy/assets/map/nodes/"
```

Endgültige Deploy-Ordnerstruktur:
```
F:/AI/RebornSaga_Assets/deploy/assets/
├── characters/{charId}/full/{pose}_{emotion}.webp
├── enemies/{enemyId}.webp
├── backgrounds/{sceneKey}.webp
├── items/{category}/{itemId}.webp
├── map/regions/{regionId}.webp
└── map/nodes/{nodeType}.webp
```

### Phase 2 — Review Checkpoint

Deploy-Ordner prüfen: Alle Unterordner vorhanden? Dateianzahl stimmt (~356)? Stichproben visuell checken (Transparenz bei Sprites, korrekte Auflösung). `ls -R F:/AI/RebornSaga_Assets/deploy/assets/ | wc -l` zur Kontrolle.

---

## Phase 3a: App-Integration (Code)

### Task 3.1: AssetDownloadScene implementieren

**Ziel:** Download-Screen beim ersten Start (wenn Assets fehlen).

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/AssetDownloadScene.cs`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/App.axaml.cs` (Start-Logik)

**Step 1: AssetDownloadScene erstellen**

```csharp
namespace RebornSaga.Scenes;

using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;

/// <summary>
/// Download-Screen für Assets beim ersten Start.
/// Zeigt Fortschritt, Partikel-Animation und Hinweis auf WLAN.
/// </summary>
public class AssetDownloadScene : Scene
{
    private readonly IAssetDeliveryService _deliveryService;
    private readonly IAudioService _audioService;

    private float _progress;        // 0.0 - 1.0
    private long _downloadedBytes;
    private long _totalBytes;
    private string _statusText = "";
    private bool _isDownloading;
    private bool _downloadComplete;
    private bool _downloadFailed;
    private string _errorMessage = "";

    // Partikel für Animation
    private readonly (float x, float y, float speed, float alpha, float size)[] _particles
        = new (float, float, float, float, float)[40];
    private float _time;

    // Gepoolte Paints
    private static readonly SKPaint _bgPaint = new() { Color = new SKColor(0x0A, 0x16, 0x28) };
    private static readonly SKPaint _barBgPaint = new() { Color = new SKColor(0x1E, 0x29, 0x3B), IsAntialias = true };
    private static readonly SKPaint _barFillPaint = new() { IsAntialias = true };
    private static readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private static readonly SKPaint _subtextPaint = new() { Color = new SKColor(0x9C, 0xA3, 0xAF), IsAntialias = true };
    private static readonly SKPaint _particlePaint = new() { IsAntialias = true };
    private static readonly SKPaint _errorPaint = new() { Color = new SKColor(0xEF, 0x44, 0x44), IsAntialias = true };

    public AssetDownloadScene(IAssetDeliveryService deliveryService, IAudioService audioService)
    {
        _deliveryService = deliveryService;
        _audioService = audioService;
        InitializeParticles();
    }

    private void InitializeParticles()
    {
        var rng = new Random(42);
        for (int i = 0; i < _particles.Length; i++)
        {
            _particles[i] = (
                x: rng.NextSingle(),
                y: rng.NextSingle(),
                speed: 0.02f + rng.NextSingle() * 0.05f,
                alpha: 0.2f + rng.NextSingle() * 0.5f,
                size: 2f + rng.NextSingle() * 4f
            );
        }
    }

    public override void OnEnter()
    {
        base.OnEnter();
        StartDownload();
    }

    private async void StartDownload()
    {
        _isDownloading = true;
        _statusText = "Prüfe Assets...";

        try
        {
            var check = await _deliveryService.CheckForUpdatesAsync();
            if (!check.UpdateAvailable)
            {
                _downloadComplete = true;
                return;
            }

            _totalBytes = check.BytesToDownload;
            _statusText = "Lade Assets herunter...";

            var progress = new Progress<AssetDownloadProgress>(p =>
            {
                _progress = p.TotalBytes > 0 ? (float)p.BytesDownloaded / p.TotalBytes : 0f;
                _downloadedBytes = p.BytesDownloaded;
                _statusText = p.CurrentFileName ?? "Lade...";
            });

            await _deliveryService.DownloadAssetsAsync(progress);
            _downloadComplete = true;
        }
        catch (Exception ex)
        {
            _downloadFailed = true;
            _errorMessage = ex.Message;
        }
        finally
        {
            _isDownloading = false;
        }
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;

        // Partikel bewegen
        for (int i = 0; i < _particles.Length; i++)
        {
            var p = _particles[i];
            p.y -= p.speed * deltaTime;
            if (p.y < -0.05f) p.y = 1.05f;
            _particles[i] = p;
        }

        if (_downloadComplete)
        {
            // Kurz warten, dann zur TitleScene
            SceneManager?.ChangeScene<TitleScene>();
        }
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        // Hintergrund
        canvas.DrawRect(bounds, _bgPaint);

        // Partikel
        for (int i = 0; i < _particles.Length; i++)
        {
            var p = _particles[i];
            var color = i % 3 == 0
                ? new SKColor(0x4A, 0x90, 0xD9, (byte)(p.alpha * 255))  // Blau
                : i % 3 == 1
                    ? new SKColor(0x8B, 0x5C, 0xF6, (byte)(p.alpha * 255))  // Violett
                    : new SKColor(0xFF, 0xD7, 0x00, (byte)(p.alpha * 255)); // Gold
            _particlePaint.Color = color;
            canvas.DrawCircle(p.x * w, p.y * h, p.size, _particlePaint);
        }

        var centerY = h * 0.45f;

        // Titel
        _textPaint.TextSize = Math.Min(w * 0.06f, 28f);
        var title = "REBORN SAGA";
        var titleWidth = _textPaint.MeasureText(title);
        canvas.DrawText(title, (w - titleWidth) / 2f, centerY - 60f, _textPaint);

        // Status-Text
        _subtextPaint.TextSize = Math.Min(w * 0.035f, 16f);
        var statusWidth = _subtextPaint.MeasureText(_statusText);
        canvas.DrawText(_statusText, (w - statusWidth) / 2f, centerY - 20f, _subtextPaint);

        // Progress-Bar
        var barX = w * 0.15f;
        var barW = w * 0.7f;
        var barH = 12f;
        var barY = centerY;
        var barRect = new SKRect(barX, barY, barX + barW, barY + barH);
        var barRadius = barH / 2f;

        canvas.DrawRoundRect(barRect, barRadius, barRadius, _barBgPaint);

        if (_progress > 0.001f)
        {
            var fillRect = new SKRect(barX, barY, barX + barW * _progress, barY + barH);
            _barFillPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(barX, barY), new SKPoint(barX + barW, barY),
                new[] { new SKColor(0x4A, 0x90, 0xD9), new SKColor(0x8B, 0x5C, 0xF6) },
                SKShaderTileMode.Clamp
            );
            canvas.DrawRoundRect(fillRect, barRadius, barRadius, _barFillPaint);
            _barFillPaint.Shader = null;
        }

        // Prozent + MB
        _subtextPaint.TextSize = Math.Min(w * 0.03f, 14f);
        var pctText = $"{_progress * 100f:F0}%";
        if (_totalBytes > 0)
        {
            var mbDown = _downloadedBytes / 1_048_576.0;
            var mbTotal = _totalBytes / 1_048_576.0;
            pctText = $"{mbDown:F1} / {mbTotal:F1} MB ({_progress * 100f:F0}%)";
        }
        var pctWidth = _subtextPaint.MeasureText(pctText);
        canvas.DrawText(pctText, (w - pctWidth) / 2f, barY + barH + 24f, _subtextPaint);

        // WLAN-Hinweis
        if (_isDownloading)
        {
            var hint = "Bitte WLAN-Verbindung verwenden";
            var hintWidth = _subtextPaint.MeasureText(hint);
            canvas.DrawText(hint, (w - hintWidth) / 2f, barY + barH + 50f, _subtextPaint);
        }

        // Fehler-Anzeige
        if (_downloadFailed)
        {
            _errorPaint.TextSize = Math.Min(w * 0.035f, 16f);
            var errWidth = _errorPaint.MeasureText(_errorMessage);
            canvas.DrawText(_errorMessage, (w - errWidth) / 2f, centerY + 100f, _errorPaint);

            // Retry-Button
            // ACHTUNG: UIRenderer.DrawButton(canvas, SKRect, text, isHovered, isPressed, color)
            var retryRect = new SKRect(w / 2f - 80f, centerY + 120f, w / 2f + 80f, centerY + 164f);
            UIRenderer.DrawButton(canvas, retryRect, "Erneut versuchen", false, false, new SKColor(0x4A, 0x90, 0xD9));
        }
    }

    public override bool HandleInput(InputAction action)
    {
        if (_downloadFailed && action.Type == InputType.Tap)
        {
            // Retry
            _downloadFailed = false;
            _errorMessage = "";
            _progress = 0;
            StartDownload();
            return true;
        }
        return false;
    }
}
```

**Step 2: Start-Logik in App.axaml.cs anpassen**

In `InitializeServicesAsync()` oder beim Scene-Start:
- `AssetDeliveryService.CheckForUpdatesAsync()` aufrufen
- Wenn `UpdateAvailable` → `AssetDownloadScene` als erste Szene
- Wenn Assets vorhanden → direkt `TitleScene`

**Step 3: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

---

### Task 3.2: BattleScene — AI Enemy-Sprites integrieren

**Ziel:** Prozedurale Enemy-Darstellung durch AI-Sprites ersetzen.

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs:443-476`

**Step 1: DrawEnemySprite umschreiben**

Die bestehende `DrawEnemySprite()` Methode (Zeile 443-476) ersetzen:

```csharp
private void DrawEnemySprite(SKCanvas canvas, SKRect rect, float time)
{
    // AI-Sprite laden (gecacht über SpriteCache)
    var spriteKey = GetEnemySpriteKey();
    var sprite = _spriteCache?.GetEnemySprite(spriteKey);

    if (sprite != null)
    {
        // AI-Sprite zeichnen mit Skalierung
        var srcRect = new SKRect(0, 0, sprite.Width, sprite.Height);
        canvas.DrawBitmap(sprite, srcRect, rect);

        // Treffer-Flash
        if (_enemyFlashTimer > 0f)
        {
            using var flashPaint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(180 * _enemyFlashTimer)), BlendMode = SKBlendMode.SrcATop };
            canvas.DrawRect(rect, flashPaint);
        }

        // Enraged-Overlay (Boss Phase 5)
        if (_currentBossPhase >= 4)
        {
            using var ragePaint = new SKPaint { Color = new SKColor(0xDC, 0x26, 0x26, 40), BlendMode = SKBlendMode.SrcATop };
            canvas.DrawRect(rect, ragePaint);
        }
    }
    else
    {
        // Fallback: Prozeduraler Enemy (altes System)
        DrawProceduralEnemy(canvas, rect, time);
    }
}

private string GetEnemySpriteKey()
{
    // Boss: Phase-basiertes Sprite (Enemy hat kein IsBoss — Phases > 1 bedeutet Boss)
    if (_enemy.Phases > 1)
    {
        var phase = _currentBossPhase switch
        {
            0 => "idle",
            1 => "attack",
            2 => "special",
            3 => "hurt",
            _ => "enraged"
        };
        return $"{_enemy.Id}_{phase}";
    }
    return _enemy.Id;
}

// Altes prozedurales System als Fallback (bis Assets da sind)
private void DrawProceduralEnemy(SKCanvas canvas, SKRect rect, float time)
{
    // ... bestehender Code aus Zeile 443-476 hierher verschieben ...
}
```

**Step 2: SpriteCache.GetEnemySprite() existiert bereits**

`GetEnemySprite(string enemyId)` ist in SpriteCache.cs:65 bereits implementiert. Nutzt `SpriteAssetPaths.GetEnemySpritePath()`. Keine Änderung nötig.

**Step 3: Build + prüfen**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

---

### Task 3.3: InventoryScene — Item-Icons integrieren

**Ziel:** Text-basierte Item-Anzeige durch AI-Icons ersetzen.

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/InventoryScene.cs:152`

**Step 1: Item-Icon-Rendering hinzufügen**

An der Stelle wo Items gezeichnet werden (Zeile ~152), ein Icon-Rendering einfügen:

```csharp
// Item-Icon zeichnen (AI-generiert, mit Preis-basiertem Glow)
// ACHTUNG: Item hat kein Category-Property — ItemType (Weapon/Armor/etc.) als Kategorie nutzen
var category = item.Type.ToString().ToLowerInvariant();
var icon = _spriteCache?.GetItemIcon(category, item.Id);
if (icon != null)
{
    var iconRect = new SKRect(rect.Left + 4, rect.Top + 4, rect.Left + 4 + iconSize, rect.Top + 4 + iconSize);
    canvas.DrawBitmap(icon, new SKRect(0, 0, icon.Width, icon.Height), iconRect);

    // Qualitäts-Glow-Border (basiert auf BuyPrice, da kein Rarity-Enum existiert)
    DrawQualityGlow(canvas, iconRect, item.BuyPrice);
}
```

**Step 2: DrawQualityGlow Hilfsmethode**

```csharp
// Gecachte Paints (NICHT per-Frame allokieren!)
private static readonly SKMaskFilter _qualityGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
private static readonly SKPaint _glowBorderPaint = new()
{
    IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f
};

/// <summary>
/// Preis-basierter Glow (Item hat kein Rarity-Enum).
/// Grün=günstig (≤200G), Blau=mittel (≤800G), Lila=teuer (≤2000G), Gold=legendär (>2000G).
/// </summary>
private static void DrawQualityGlow(SKCanvas canvas, SKRect rect, int buyPrice)
{
    // Billige Items (Common) = kein Glow
    if (buyPrice <= 50) return;

    var color = buyPrice switch
    {
        <= 200 => new SKColor(0x10, 0xB9, 0x81),  // Grün (Uncommon)
        <= 800 => new SKColor(0x3B, 0x82, 0xF6),   // Blau (Rare)
        <= 2000 => new SKColor(0x8B, 0x5C, 0xF6),  // Lila (Epic)
        _ => new SKColor(0xFF, 0xD7, 0x00)          // Gold (Legendary)
    };

    _glowBorderPaint.Color = color.WithAlpha(80);
    _glowBorderPaint.MaskFilter = _qualityGlow; // Gecacht, KEIN CreateBlur per Frame!
    canvas.DrawRoundRect(rect, 4f, 4f, _glowBorderPaint);
    _glowBorderPaint.MaskFilter = null; // Nur Referenz entfernen, Filter lebt weiter
}
```

---

### Task 3.4: OverworldScene — AI Map-Assets integrieren

**Ziel:** Node-Icons und Region-Hintergründe durch AI-Assets ersetzen.

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Map/NodeRenderer.cs:78-79`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/OverworldScene.cs:325`

**Step 1: NodeRenderer — AI-Icons für Node-Typen**

In `DrawNodeIcon()` (Zeile ~78) AI-Icon laden und zeichnen:

```csharp
private static void DrawNodeIcon(SKCanvas canvas, MapNodeType type, float cx, float cy, float iconRadius)
{
    var iconKey = type switch
    {
        MapNodeType.Story => "map/nodes/battle",
        MapNodeType.Boss => "map/nodes/boss",
        MapNodeType.Shop => "map/nodes/shop",
        MapNodeType.Rest => "map/nodes/rest",
        MapNodeType.SideQuest => "map/nodes/event",
        MapNodeType.Treasure => "map/nodes/treasure",
        MapNodeType.Elite => "map/nodes/elite",
        _ => null
    };

    if (iconKey != null && _spriteCache != null)
    {
        // GetMapNodeIcon() muss als neue Methode in SpriteCache hinzugefügt werden (siehe Step 2)
        var icon = _spriteCache.GetMapNodeIcon(iconKey);
        if (icon != null)
        {
            var iconRect = new SKRect(cx - iconRadius, cy - iconRadius, cx + iconRadius, cy + iconRadius);
            canvas.DrawBitmap(icon, new SKRect(0, 0, icon.Width, icon.Height), iconRect);
            return;
        }
    }

    // Fallback: Prozedurales Icon (bestehender Code)
    DrawProceduralIcon(canvas, type, cx, cy, iconRadius);
}
```

**Step 2: SpriteCache — GetMapNodeIcon() hinzufügen**

NodeRenderer ist statisch, braucht also SpriteCache als Parameter oder statische Referenz.
SpriteCache.cs bekommt eine neue Methode:

```csharp
/// <summary>Gibt ein Map-Node-Icon zurück (gecacht).</summary>
public SKBitmap? GetMapNodeIcon(string nodeKey)
{
    var path = $"{nodeKey}.webp"; // z.B. "map/nodes/boss.webp"
    return GetBitmap(path);
}
```

NodeRenderer.DrawNodeIcon() bekommt `SpriteCache` als zusätzlichen Parameter:
```csharp
private static void DrawNodeIcon(SKCanvas canvas, MapNodeType type, float cx, float cy,
    float iconRadius, SpriteCache? _spriteCache)
```

**Step 3: OverworldScene — Region-Hintergrund**

In `Render()` (Zeile ~325) AI-Hintergrund laden:

```csharp
// Region-Hintergrund (AI-generiert)
var regionBg = _spriteCache?.GetBackground($"map/regions/{_currentRegion}");
if (regionBg != null)
{
    canvas.DrawBitmap(regionBg, new SKRect(0, 0, regionBg.Width, regionBg.Height), bounds);
}
else
{
    // Fallback: BackgroundCompositor (prozedural)
    BackgroundCompositor.RenderBack(canvas, bounds, _time);
}
```

---

### Task 3.5: Altes prozedurales Character-System entfernen

**Voraussetzung:** AI-Sprites auf Android verifiziert und funktionierend.

**Files:**
- Delete: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/CharacterParts.cs`
- Delete: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/FaceRenderer.cs`
- Delete: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/BodyRenderer.cs`
- Delete: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/EyeRenderer.cs`
- Delete: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/HairRenderer.cs`
- Delete: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/ClothingRenderer.cs`
- Delete: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/AccessoryRenderer.cs`
- Delete: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/CharacterEffects.cs`

**Step 1: Prüfen dass kein Code mehr referenziert**

```bash
# In RebornSaga.Shared nach Referenzen suchen
grep -r "CharacterParts\|FaceRenderer\|BodyRenderer\|EyeRenderer\|HairRenderer\|ClothingRenderer\|AccessoryRenderer\|CharacterEffects" \
  src/Apps/RebornSaga/RebornSaga.Shared/ --include="*.cs" -l
```

Nur die zu löschenden Dateien selbst sollten erscheinen.

**Step 2: Dateien löschen**

```bash
rm src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/CharacterParts.cs \
   src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/FaceRenderer.cs \
   src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/BodyRenderer.cs \
   src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/EyeRenderer.cs \
   src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/HairRenderer.cs \
   src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/ClothingRenderer.cs \
   src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/AccessoryRenderer.cs \
   src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/CharacterEffects.cs
```

**Step 3: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

---

### Task 3.9: Element-Partikel-Configs (6 neue Presets)

**Ziel:** 6 neue Element-spezifische ParticleConfigs in ParticleSystem.cs erstellen (analog zu den 5 bestehenden Presets MagicSparkle/LevelUpGlow/SystemGlitch/BloodSplatter/AmbientFloat).

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Effects/ParticleSystem.cs:106` (nach AmbientFloat)
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs:1001` (ExecuteSkillAttack Partikel-Trigger)

**Step 1: 6 neue Presets in ParticleSystem.cs einfügen (nach AmbientFloat, Zeile ~106)**

```csharp
/// <summary>Aufsteigende Flammen-Partikel (Feuer-Element).</summary>
public static readonly ParticleConfig FireBurst = new()
{
    MinSpeed = 30f, MaxSpeed = 70f, MinLife = 0.5f, MaxLife = 1.2f,
    MinSize = 2f, MaxSize = 5f, SpreadAngle = 90f, BaseAngle = 270f,
    Gravity = -50f, Color = new SKColor(0xFF, 0x45, 0x00), FadeOut = true, Shape = 0
};

/// <summary>Eiskristall-Splitter nach außen (Eis-Element).</summary>
public static readonly ParticleConfig IceShatter = new()
{
    MinSpeed = 40f, MaxSpeed = 80f, MinLife = 0.4f, MaxLife = 0.9f,
    MinSize = 2f, MaxSize = 4f, SpreadAngle = 360f,
    Color = new SKColor(0x00, 0xBF, 0xFF), FadeOut = true, Shape = 2
};

/// <summary>Schnelle vertikale Linien (Blitz-Element).</summary>
public static readonly ParticleConfig LightningStrike = new()
{
    MinSpeed = 150f, MaxSpeed = 250f, MinLife = 0.05f, MaxLife = 0.15f,
    MinSize = 1f, MaxSize = 3f, SpreadAngle = 30f, BaseAngle = 270f,
    Color = new SKColor(0xFF, 0xD7, 0x00), FadeOut = true, Shape = 3
};

/// <summary>Horizontale Bögen (Wind-Element).</summary>
public static readonly ParticleConfig WindSlash = new()
{
    MinSpeed = 60f, MaxSpeed = 120f, MinLife = 0.3f, MaxLife = 0.7f,
    MinSize = 1.5f, MaxSize = 3.5f, SpreadAngle = 60f, BaseAngle = 0f,
    Color = new SKColor(0x7C, 0xFC, 0x00), FadeOut = true, Shape = 3
};

/// <summary>Strahlenförmig nach außen, langsam (Licht-Element).</summary>
public static readonly ParticleConfig HolyBurst = new()
{
    MinSpeed = 10f, MaxSpeed = 25f, MinLife = 1.5f, MaxLife = 2.5f,
    MinSize = 1.5f, MaxSize = 3f, SpreadAngle = 360f,
    Color = new SKColor(0xFF, 0xFF, 0xE0), FadeOut = true, ShrinkOut = true, Shape = 1
};

/// <summary>Pulsierend, langsam nach außen (Dunkel-Element).</summary>
/// HINWEIS: Negative Speed funktioniert mathematisch, aber Partikel starten am Zentrum
/// und würden sich kaum bewegen. Stattdessen: langsame Ausbreitung + FadeOut für "Void"-Effekt.
public static readonly ParticleConfig ShadowVoid = new()
{
    MinSpeed = 5f, MaxSpeed = 15f, MinLife = 0.8f, MaxLife = 1.5f,
    MinSize = 2f, MaxSize = 4f, SpreadAngle = 360f,
    Color = new SKColor(0x8B, 0x00, 0x8B), FadeOut = true, ShrinkOut = true, Shape = 0
};
```

**Step 2: Element-Partikel in ExecuteSkillAttack() verdrahten (BattleScene.cs:~1001)**

Bestehende Zeile:
```csharp
_particles.Emit(enemyCenter.X, enemyCenter.Y, isCrit ? 20 : 10, ParticleSystem.MagicSparkle);
```

Ersetzen durch:
```csharp
var elementConfig = skill.Element switch
{
    Element.Fire => ParticleSystem.FireBurst,
    Element.Ice => ParticleSystem.IceShatter,
    Element.Lightning => ParticleSystem.LightningStrike,
    Element.Wind => ParticleSystem.WindSlash,
    Element.Light => ParticleSystem.HolyBurst,
    Element.Dark => ParticleSystem.ShadowVoid,
    _ => ParticleSystem.MagicSparkle
};
_particles.Emit(enemyCenter.X, enemyCenter.Y, isCrit ? 20 : 10, elementConfig);
```

**Step 3: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

---

### Task 3.10: Angriffs-Animation in BattleScene

**Ziel:** Spieler-Charakter-Sprite bei Angriff kurz nach vorne bewegen (Translate-Animation) + Slash-Effekt über Gegner.

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs`

**Step 1: Neue Felder für Angriffs-Animation**

```csharp
// Angriffs-Animation
private float _attackAnimTimer;
private const float AttackAnimDuration = 0.4f; // 0.2s vor + 0.2s zurück
private float _attackOffsetX;
private bool _showSlashEffect;
private float _slashTimer;
private const float SlashDuration = 0.15f;
```

**Step 2: In Update() die Animation aktualisieren**

In der `PlayerAttack`-Phase (Zeile ~273):
```csharp
// Angriffs-Animation updaten
if (_attackAnimTimer > 0)
{
    _attackAnimTimer -= deltaTime;
    var half = AttackAnimDuration / 2f;
    if (_attackAnimTimer > half)
    {
        // Vorwärts-Phase: 0→30px
        var t = 1f - (_attackAnimTimer - half) / half;
        _attackOffsetX = 30f * t;
    }
    else
    {
        // Rückwärts-Phase: 30→0px
        var t = _attackAnimTimer / half;
        _attackOffsetX = 30f * t;
    }
}

// Slash-Effekt updaten
if (_slashTimer > 0)
{
    _slashTimer -= deltaTime;
    _showSlashEffect = _slashTimer > 0;
}
```

**Step 3: In ExecutePlayerAttack() die Animation starten (BattleScene.cs:1074)**

Nach Zeile 1074 (`private void ExecutePlayerAttack()`), am Anfang:
```csharp
_attackAnimTimer = AttackAnimDuration;
// Slash-Effekt wird in Update() gestartet wenn Animation am Umkehrpunkt ist
// KEIN Task.Delay() verwenden — Race-Condition mit Render-Thread!
```

Im Update()-Block nach dem _attackAnimTimer-Code, Slash-Effekt am Umkehrpunkt starten:
```csharp
// Slash-Effekt am Umkehrpunkt starten (wenn Timer unter die Hälfte fällt)
if (_attackAnimTimer > 0 && _attackAnimTimer <= AttackAnimDuration * 0.5f && !_showSlashEffect)
{
    _slashTimer = SlashDuration;
    _showSlashEffect = true;
}
```

**Step 4: In Render() den Slash-Effekt zeichnen**

Nach dem Gegner-Rendering (DrawEnemySprite), wenn `_showSlashEffect`:
```csharp
if (_showSlashEffect)
{
    var slashAlpha = (byte)(255 * (_slashTimer / SlashDuration));
    using var slashPaint = new SKPaint
    {
        Color = SKColors.White.WithAlpha(slashAlpha),
        StrokeWidth = 3f, Style = SKPaintStyle.Stroke, IsAntialias = true
    };
    var cx = enemyRect.MidX;
    var cy = enemyRect.MidY;
    var size = enemyRect.Width * 0.4f;
    // 3 diagonale Slash-Linien
    canvas.DrawLine(cx - size, cy - size * 0.5f, cx + size, cy + size * 0.5f, slashPaint);
    canvas.DrawLine(cx - size * 0.8f, cy - size * 0.7f, cx + size * 0.8f, cy + size * 0.3f, slashPaint);
    canvas.DrawLine(cx - size * 0.6f, cy + size * 0.2f, cx + size * 0.6f, cy + size * 0.8f, slashPaint);
}
```

**Step 5: Spieler-Sprite Offset in Render() anwenden**

Beim Zeichnen des Spieler-Sprites `canvas.Translate(_attackOffsetX, 0)` anwenden (Save/Restore).

**Step 6: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

---

### Task 3.11: Dodge-Ghosting-Effekt

**Ziel:** Bei erfolgreichem Ausweichen einen kurzen Nachbild-Effekt (Ghost) auf dem Spieler-Sprite.

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs`

**Step 1: Neue Felder**

```csharp
// Dodge-Ghosting
private float _dodgeGhostTimer;
private const float DodgeGhostDuration = 0.3f;
```

**Step 2: In der Dodge-Erfolgs-Logik starten**

Wo "Ausgewichen!" Floating-Text gespawnt wird:
```csharp
_dodgeGhostTimer = DodgeGhostDuration;
```

**Step 3: In Update() Timer runterz¨hlen**

```csharp
if (_dodgeGhostTimer > 0) _dodgeGhostTimer -= deltaTime;
```

**Step 4: In Render() Ghosting zeichnen**

Beim Spieler-Sprite, wenn `_dodgeGhostTimer > 0`:
```csharp
if (_dodgeGhostTimer > 0)
{
    var ghostAlpha = (byte)(50 * (_dodgeGhostTimer / DodgeGhostDuration));
    // Duplikat mit 20% Alpha, 10px versetzt
    canvas.Save();
    canvas.Translate(10f, 0);
    using var ghostPaint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(
        SKColors.White.WithAlpha(ghostAlpha), SKBlendMode.DstIn) };
    // Spieler-Sprite nochmal zeichnen mit reduzierter Alpha
    canvas.Restore();

    // Hauptsprite mit 50% Alpha
    var mainAlpha = (byte)(128 + 127 * (1f - _dodgeGhostTimer / DodgeGhostDuration));
    // Alpha auf den Sprite-Paint anwenden
}
```

**Step 5: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

---

### Task 3.12: SplashArt für Ultimate-Skills verdrahten

**Ziel:** Den bereits implementierten `SplashArtRenderer` in BattleScene bei Ultimate-Skills (IsUltimate) auslösen.

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs`

**Step 1: SplashArtRenderer + IAudioService als Felder hinzufügen**

```csharp
private readonly SplashArtRenderer _splashArt = new();
private readonly IAudioService? _audioService;
```

**WICHTIG:** BattleScene hat aktuell KEIN `_audioService` Feld. `IAudioService` muss als zusätzlicher Constructor-Parameter hinzugefügt werden:
```csharp
public BattleScene(BattleEngine battleEngine, SkillService skillService,
    InventoryService inventoryService, StoryEngine storyEngine, IAudioService? audioService = null)
{
    // ... bestehende Zuweisungen ...
    _audioService = audioService;
}
```

**Step 2: In ExecuteSkillAttack() auslösen (BattleScene.cs:~963)**

Nach dem Partikel-Emit, vor dem Combo-Counter:
```csharp
// SplashArt bei Ultimate-Skills
if (skill.IsUltimate)
{
    var accentColor = GetElementColor(skill.Element ?? Element.Light);
    // Player hat kein ClassName-Property — Player.Class ist ClassName-Enum
    _splashArt.Start(skill.Name, _player.Class.ToString(), accentColor, 1.5f);
    _audioService?.PlaySfx(GameSfx.UltimateActivate);
}
```

**Step 3: In Update() SplashArt updaten**

```csharp
_splashArt.Update(deltaTime);
```

**Step 4: In Render() SplashArt als Overlay rendern (nach allem anderen)**

```csharp
_splashArt.Render(canvas, bounds);
```

**Step 5: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

---

### Task 3.13: MangaPanelRenderer in DialogueScene integrieren

**Ziel:** Den bestehenden (aber ungenutzten) `MangaPanelRenderer` in DialogueScene für dramatische Schlüsselszenen einbauen.

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/DialogueScene.cs`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Models/StoryNode.cs` (neues Feld `MangaPanel`)

**Step 1: MangaPanelMode Property in DialogueScene**

```csharp
// Manga-Panel-Modus (Off/Dual/Triple)
private string _mangaPanelMode = "off"; // "off", "dual", "triple"

public void SetMangaPanelMode(string mode) => _mangaPanelMode = mode?.ToLowerInvariant() ?? "off";
```

**Step 2: In Render() Branch nach Mode**

In `Render()` (DialogueScene.cs:213), vor dem normalen Rendering:
```csharp
if (_mangaPanelMode == "dual" || _mangaPanelMode == "triple")
{
    RenderMangaPanels(canvas, bounds);
    // Textbox trotzdem normal rendern (über den Panels)
    DialogBoxRenderer.Render(canvas, bounds, _currentSpeakerName, _typewriter.VisibleText, ...);
    RenderUIButtons(canvas, bounds);
    return;
}
// ... normales Rendering ...
```

**Step 3: RenderMangaPanels Methode**

```csharp
private void RenderMangaPanels(SKCanvas canvas, SKRect bounds)
{
    if (_mangaPanelMode == "dual" && _activeSpeakers.Count >= 2)
    {
        // ACHTUNG: Methode heißt RenderDualPanel (nicht RenderDual)!
        // Signatur: RenderDualPanel(canvas, bounds, renderTop, renderBottom, slant)
        MangaPanelRenderer.RenderDualPanel(canvas, bounds,
            (c, r) => RenderSpeakerInPanel(c, r, _activeSpeakers[0]),
            (c, r) => RenderSpeakerInPanel(c, r, _activeSpeakers[1]));
    }
    else if (_mangaPanelMode == "triple" && _activeSpeakers.Count >= 2)
    {
        // ACHTUNG: Methode heißt RenderTriplePanel (nicht RenderTriple)!
        // Signatur: RenderTriplePanel(canvas, bounds, renderTop, renderMiddle, renderBottom, slant)
        MangaPanelRenderer.RenderTriplePanel(canvas, bounds,
            (c, r) => RenderSpeakerInPanel(c, r, _activeSpeakers[0]),
            (c, r) => { /* Hintergrund-Panel */ BackgroundCompositor.RenderBack(c, r, _time); },
            (c, r) => RenderSpeakerInPanel(c, r, _activeSpeakers.Count > 1 ? _activeSpeakers[1] : _activeSpeakers[0]));
    }
}

private void RenderSpeakerInPanel(SKCanvas canvas, SKRect rect, DialogueSpeaker speaker)
{
    // Hintergrund
    BackgroundCompositor.RenderBack(canvas, rect, _time);
    // Charakter zentriert im Panel
    // ACHTUNG: SpriteCharacterRenderer.Draw() — NICHT DrawCharacter()!
    // Signatur: Draw(canvas, charId, pose, emotion, cx, cy, scale, time, cache)
    // DialogueSpeaker hat KEIN AnimState — Definition.Id als charId verwenden
    var cx = rect.MidX;
    var cy = rect.MidY;
    var scale = rect.Height / 1216f; // Normalisiert auf Sprite-Höhe
    SpriteCharacterRenderer.Draw(canvas, speaker.Definition.Id, speaker.Pose, speaker.Emotion,
        cx, cy, scale, _time, _spriteCache);
}
```

**Step 4: StoryNode — neues Feld MangaPanel**

In `StoryNode.cs`:
```csharp
[JsonPropertyName("mangaPanel")]
public string? MangaPanel { get; set; } // "dual", "triple", oder null
```

**Step 5: In StoryEngine/DialogueScene das Feld auswerten**

Beim Node-Wechsel (in `PresentNode` oder wo der StoryNode geladen wird):
```csharp
_mangaPanelMode = node.MangaPanel ?? "off";
```

**Step 6: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

---

### Task 3.14: GlitchEffect in DialogueScene für ARIA-Szenen

**Ziel:** Den bestehenden (aber ungenutzten) `GlitchEffect` in DialogueScene einbauen, getriggert bei ARIA-Dialogen oder Dimensions-Rissen.

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/DialogueScene.cs`

**Step 1: GlitchEffect als Feld**

```csharp
private readonly GlitchEffect _glitchEffect = new();
```

**Step 2: In Update() updaten**

```csharp
_glitchEffect.Update(deltaTime);
```

**Step 3: In Render() als Post-Processing (nach allem anderen)**

```csharp
_glitchEffect.Render(canvas, bounds);
```

**Step 4: Trigger bei ARIA-Sprecher oder Effekt-Tag**

In `PresentSpeakerLine()` (DialogueScene.cs:327):
```csharp
// GlitchEffect bei ARIA-Dialogen
// ACHTUNG: SpeakerLine hat "Character" (nicht "Speaker")!
if (line.Character?.Equals("ARIA", StringComparison.OrdinalIgnoreCase) == true)
{
    _glitchEffect.Start(0.7f, 0.8f); // Intensität 0.7, Dauer 0.8s
}
```

Optional: Neues Feld `VisualEffects` in StoryNode hinzufügen (StoryEffects ist ein Objekt, keine Liste — hat KEIN `Contains()`):
```csharp
// In StoryNode.cs hinzufügen:
[JsonPropertyName("visualEffects")]
public List<string>? VisualEffects { get; set; } // "glitch", "zoom", "shake"

// Dann in DialogueScene bei Node-Wechsel:
if (node.VisualEffects?.Contains("glitch") == true)
    _glitchEffect.Start(0.5f, 0.5f);
```

**Step 5: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

---

### Task 3.15: Kamera-Effekte in DialogueScene

**Ziel:** Subtile Canvas-Transformationen für emotionale Momente (Zoom, Shake) ohne neue Systeme.

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/DialogueScene.cs`

**Step 1: Neue Felder für Kamera-Effekte**

```csharp
// Kamera-Effekte
private float _cameraZoom = 1f;
private float _cameraZoomTarget = 1f;
private float _cameraZoomSpeed = 2f; // pro Sekunde
```

**Step 2: In Update() Zoom interpolieren**

```csharp
// Smooth Zoom
if (Math.Abs(_cameraZoom - _cameraZoomTarget) > 0.001f)
{
    _cameraZoom += (_cameraZoomTarget - _cameraZoom) * Math.Min(1f, deltaTime * _cameraZoomSpeed);
}
```

**Step 3: In Render() Canvas-Transformation anwenden**

Am Anfang von `Render()`, nach `canvas.Save()`:
```csharp
if (Math.Abs(_cameraZoom - 1f) > 0.001f)
{
    // Zoom zum Zentrum
    canvas.Translate(bounds.MidX, bounds.MidY);
    canvas.Scale(_cameraZoom);
    canvas.Translate(-bounds.MidX, -bounds.MidY);
}
```

Am Ende von `Render()`: `canvas.Restore();`

**Step 4: Trigger-Methoden**

```csharp
// ACHTUNG: ScreenShake ist eine Instanz-Klasse (nicht statisch)!
// Light() ist Instanz-Methode: void Light(float duration = 0.15f) => Start(3f, duration)
private readonly ScreenShake _cameraShake = new();

public void ZoomToSpeaker(float zoom = 1.05f) => _cameraZoomTarget = zoom;
public void ZoomReset() => _cameraZoomTarget = 1f;
public void ShakeCamera() => _cameraShake.Light(); // 3px, 0.15s
```

In `Update()`:
```csharp
_cameraShake.Update(deltaTime);
```

In `Render()` (zusätzlich zum Zoom):
```csharp
_cameraShake.Apply(canvas); // Translate wenn aktiv
```

**Step 5: In PresentSpeakerLine() bei emotionalen Tags triggern**

```csharp
// Zoom bei emotionalen Momenten
if (line.Emotion == "angry" || line.Emotion == "shocked")
    ZoomToSpeaker(1.05f);
else
    ZoomReset();
```

**Step 6: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

### Phase 3a — Code-Review Checkpoint

Umfassendes Code-Review aller geänderten/erstellten Dateien:
1. `dotnet build` für Shared + Android erfolgreich
2. Alle neuen Methoden-Aufrufe gegen echte APIs verifizieren (Signaturen, Property-Namen, Parameter-Typen)
3. Grep nach `// TODO`, `// OFFEN`, `// Phase` in geänderten Dateien — 0 Treffer erwartet
4. Neue SpriteCache-Methoden (GetMapNodeIcon) tatsächlich hinzugefügt?
5. AssetDownloadScene: CheckResult/Progress-Properties korrekt? (UpdateAvailable, BytesToDownload, BytesDownloaded, CurrentFileName)
6. Keine `using`/`new` per-Frame in Render()-Methoden (SKPaint-Leak-Prävention)

---

## Phase 3b: Audio-Assets beschaffen

### Task 3.6: SFX-Dateien sammeln

**Ziel:** 28 OGG SFX-Dateien beschaffen (entspricht dem `GameSfx` Enum in AudioService.cs:149-216).

**Quellen (CC0/Public Domain):**
- Freesound.org (CC0 Filter)
- Kenney.nl/assets (Public Domain Game Assets)
- SFXR/jsfxr (generiert für Retro-Sounds)

**Format:** OGG Vorbis, Mono, 44.1kHz, <200KB pro Datei

**GameSfx Enum bereits definiert (AudioService.cs:149-216):**
ButtonTap, MenuOpen, MenuClose, Confirm, Error, TextTick, ChoiceSelect,
SwordSlash, MagicCast, HitImpact, CriticalHit, Dodge, EnemyDefeat, Block,
Heal, BuffApply, DebuffApply, LevelUp, SkillUnlock, ItemPickup, GoldCollect,
CodexDiscover, BondUp, ChapterComplete, GlitchSound, TimeRift, KarmaShift,
UltimateActivate

**Zielordner:** `src/Apps/RebornSaga/RebornSaga.Shared/Assets/audio/sfx/`

---

### Task 3.7: BGM-Dateien sammeln

**Ziel:** 10 OGG BGM-Tracks beschaffen (entspricht `BgmTracks` in AudioService.cs:222-234).

**BgmTracks bereits definiert:**
TitleScreen, Village, Dungeon, BossBattle, NormalBattle, Emotional,
AriaSystem, OverworldMap, Dreamworld, PrologBattle

**Format:** OGG Vorbis, Stereo, 44.1kHz, 128kbps, loop-fähig, <3MB pro Track

**Quellen:**
- OpenGameArt.org (CC0/CC-BY Musik)
- Freesound.org (CC0 Ambient Loops)
- LMMS (Open-Source DAW) für eigene Kompositionen

**Zielordner:** `src/Apps/RebornSaga/RebornSaga.Shared/Assets/audio/bgm/`

---

### Task 3.8: AndroidAudioService implementieren (falls nicht vorhanden)

**Prüfe erst:** Gibt es bereits ein `AndroidAudioService.cs`?

**Files:**
- Check: `src/Apps/RebornSaga/RebornSaga.Android/` nach AudioService-Dateien

Falls nicht vorhanden:
- Create: `src/Apps/RebornSaga/RebornSaga.Android/Services/AndroidAudioService.cs`

**Implementierung:**
- Erbt von `AudioService` (Desktop-Stub)
- `SoundPool` für SFX (low-latency, pre-loaded)
- `MediaPlayer` für BGM (streaming, loop)
- `SoundFileMap`: `GameSfx` → Asset-Pfad
- Factory in `MainActivity.cs`: `App.AudioServiceFactory = sp => new AndroidAudioService(sp.GetRequiredService<IPreferencesService>(), this);`

### Phase 3b — Review Checkpoint

Audio-Dateien vorhanden: 28 SFX + 10 BGM als OGG im Assets-Ordner? AndroidAudioService kompiliert? SoundPool lädt ohne Fehler? Build OK?

---

## Phase 4: UI-Lokalisierung

### Task 4.1: AppStrings.Designer.cs generieren

**Problem:** `AppStrings.Designer.cs` existiert nicht (CLI-Build generiert nicht automatisch).

**Step 1: Generator-Script oder manuell**

Aus `AppStrings.resx` die 82 existierenden Keys in eine `AppStrings.Designer.cs` übertragen:

```csharp
namespace RebornSaga.Resources.Strings;

using System.Resources;
using System.Globalization;

public class AppStrings
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "RebornSaga.Resources.Strings.AppStrings",
            typeof(AppStrings).Assembly);

    public static string GetString(string name) =>
        ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    // Generierte Properties...
    public static string AppName => GetString("AppName");
    public static string Attack => GetString("Attack");
    // ... etc.
}
```

---

### Task 4.2: Neue Lokalisierungs-Keys hinzufügen (~50 Keys)

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.resx`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.de.resx`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.en.resx`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.es.resx`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.fr.resx`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.it.resx`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.pt.resx`

**Neue Keys (aus Hardcode-Analyse):**

| Key | DE | Kontext |
|-----|-----|---------|
| Victory | SIEG! | BattleScene |
| Defeat | Gefallen... | BattleScene/GameOver |
| TapToContinue | Tippen zum Fortfahren | Battle/Dialog |
| ChooseSkill | Skill wählen | BattleScene |
| ChooseItem | Item wählen | BattleScene |
| NoSkillsAvailable | Keine Skills verfügbar | BattleScene |
| NoItemsAvailable | Keine Items verfügbar | BattleScene |
| BossPhase | Phase {0} | BattleScene |
| Combo | COMBO | BattleScene |
| Boss | BOSS | BattleScene |
| LevelUp | LEVEL UP! | LevelUpOverlay |
| DistributePoints | Verteile {0} Punkte: | LevelUpOverlay |
| AllPointsDistributed | Alle Punkte verteilt! | LevelUpOverlay |
| Confirm | Bestätigen | LevelUpOverlay |
| YourJourneyEnds | Deine Reise endet hier... oder doch nicht? | GameOverOverlay |
| ReviveAd | Wiederbeleben (Werbung) | GameOverOverlay |
| LoadSaveGame | Spielstand laden | GameOverOverlay |
| UnlockChapter | Kapitel freischalten | ChapterUnlockOverlay |
| Unlock | Freischalten | ChapterUnlockOverlay |
| FateChanged | Das Schicksal hat sich verändert... | FateChangedOverlay |
| TapToContinueTutorial | Tippe zum Fortfahren | TutorialOverlay |
| ResumeGame | Fortsetzen | PauseOverlay |
| SaveGame | Speichern | PauseOverlay |
| StatusMenu | Status | PauseOverlay |
| InventoryMenu | Inventar | PauseOverlay |
| CodexMenu | Kodex | PauseOverlay |
| SettingsMenu | Einstellungen | PauseOverlay/SettingsScene |
| MainMenuReturn | Hauptmenü | PauseOverlay |
| TextSpeed | Geschwindigkeit | SettingsScene |
| CheckingAssets | Prüfe Assets... | AssetDownloadScene |
| DownloadingAssets | Lade Assets herunter... | AssetDownloadScene |
| UseWifi | Bitte WLAN-Verbindung verwenden | AssetDownloadScene |
| RetryDownload | Erneut versuchen | AssetDownloadScene |

---

### Task 4.3: Hardcodierte Strings in Szenen/Overlays ersetzen

**Files (alle modifizieren):**
- `BattleScene.cs:139,599,613,648,662,670,699,724,746,794,801,822,867,874`
- `LevelUpOverlay.cs:100-101,114,165`
- `GameOverOverlay.cs:82,84,92,108,116`
- `ChapterUnlockOverlay.cs:137,168,182`
- `PauseOverlay.cs:28`
- `FateChangedOverlay.cs:81`
- `TutorialOverlay.cs:105,120`
- `BacklogOverlay.cs:162`
- `StatusWindowOverlay.cs:52`
- `SettingsScene.cs:95,135`

**Pattern für jede Datei:**

1. `ILocalizationService` per DI injizieren (Constructor-Parameter)
2. Gecachte String-Felder anlegen (Performance)
3. `UpdateLocalizedTexts()` Methode für Sprachwechsel
4. Hardcodierte Strings durch `_localization.GetString("Key")` ersetzen

**Beispiel BattleScene:**

```csharp
// Vorher (Zeile 139):
_actionLabels = { "Angriff", "Ausweichen", "Skill", "Item" };

// Nachher:
_actionLabels = {
    _localization.GetString("Attack"),
    _localization.GetString("Dodge"),
    _localization.GetString("Skill"),
    _localization.GetString("Item")
};
```

### Phase 4 — Code-Review Checkpoint

1. `dotnet build` erfolgreich (Shared + Android)
2. AppStrings.Designer.cs vorhanden + alle 82 Keys kompilieren
3. Grep nach hardcodierten deutschen UI-Strings in Scenes/Overlays — 0 Treffer (alles via `_localization.GetString()`)
4. Alle 6 Sprach-Dateien (DE/EN/ES/FR/IT/PT) vorhanden + gleiche Key-Anzahl

---

## Phase 5: Firebase Setup & Upload

### Task 5.1: Firebase Service Account Key erstellen

**Aktion:** Robert muss in Firebase Console → Projekt "rebornsaga-671b6" → Einstellungen → Dienstkonten → Neuen privaten Schlüssel generieren.

JSON-Datei speichern als: `F:/AI/firebase/rebornsaga-service-account.json`

**NICHT in Git committen!**

---

### Task 5.2: Asset-Manifest generieren

**Files:**
- Create: `F:/AI/ComfyUI_workflows/generate_manifest.py`

**Step 1: Manifest-Script schreiben**

```python
"""
Generiert asset_manifest.json mit SHA256-Hashes fuer alle Deploy-Assets.
Scannt den Deploy-Ordner und erstellt ein Manifest fuer AssetDeliveryService.
"""
import hashlib
import json
import os
from pathlib import Path

DEPLOY_DIR = Path("F:/AI/RebornSaga_Assets/deploy/assets")
OUTPUT = DEPLOY_DIR / "asset_manifest.json"

# Pack-Definitionen (passend zu AssetManifest.cs)
PACKS = {
    "characters": "characters/**/*.webp",
    "enemies": "enemies/*.webp",
    "backgrounds": "backgrounds/*.webp",
    "items": "items/**/*.webp",
    "map": "map/**/*.webp",
    "scenes": "scenes/*.webp",
}

def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(8192), b""):
            h.update(chunk)
    return h.hexdigest()

def main():
    # WICHTIG: AssetManifest.cs erwartet "packs" als Dictionary<string, AssetPack>, NICHT als Array!
    # AssetFile hat "hash" (nicht "sha256") als Property-Name!
    manifest = {"version": 1, "minAppVersion": "1.0.0", "packs": {}}

    for pack_name, glob_pattern in PACKS.items():
        files = sorted(DEPLOY_DIR.glob(glob_pattern))
        if not files:
            print(f"  WARNUNG: Keine Dateien fuer Pack '{pack_name}'")
            continue

        pack_files = []
        total_size = 0
        for f in files:
            rel = f.relative_to(DEPLOY_DIR).as_posix()
            size = f.stat().st_size
            total_size += size
            pack_files.append({
                "path": rel,
                "hash": sha256_file(f),  # "hash" nicht "sha256" (AssetFile.Hash)
                "size": size
            })

        # Pack als Dictionary-Eintrag (Key = pack_name)
        manifest["packs"][pack_name] = {
            "required": pack_name in ("characters", "enemies", "backgrounds"),
            "totalSize": total_size,
            "files": pack_files
        }
        print(f"  {pack_name}: {len(files)} Dateien, {total_size/1024/1024:.1f} MB")

    with open(OUTPUT, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)

    total_files = sum(len(p["files"]) for p in manifest["packs"].values())
    total_mb = sum(p["totalSize"] for p in manifest["packs"].values()) / 1024 / 1024
    print(f"\nManifest: {total_files} Dateien, {total_mb:.1f} MB gesamt")
    print(f"Geschrieben: {OUTPUT}")

if __name__ == "__main__":
    main()
```

**Step 2: Script ausführen**

```bash
py F:/AI/ComfyUI_workflows/generate_manifest.py
```

Expected: `F:/AI/RebornSaga_Assets/deploy/assets/asset_manifest.json` mit SHA256-Hashes.

---

### Task 5.3: Assets zu Firebase hochladen

**Files:**
- Create: `F:/AI/ComfyUI_workflows/upload_assets.py`

**Step 1: Upload-Script schreiben**

```python
"""
Laedt alle Deploy-Assets + Manifest zu Firebase Storage hoch.
Benoetigt Service Account Key (JSON).
"""
import json
import os
from pathlib import Path
from google.cloud import storage

DEPLOY_DIR = Path("F:/AI/RebornSaga_Assets/deploy/assets")
KEY_FILE = Path("F:/AI/firebase/rebornsaga-service-account.json")
BUCKET_NAME = "rebornsaga-671b6.firebasestorage.app"

def main():
    if not KEY_FILE.exists():
        print(f"FEHLER: Service Account Key nicht gefunden: {KEY_FILE}")
        print("Robert muss den Key in Firebase Console generieren!")
        return

    client = storage.Client.from_service_account_json(str(KEY_FILE))
    bucket = client.bucket(BUCKET_NAME)

    # Alle Dateien im Deploy-Ordner hochladen
    files = [f for f in DEPLOY_DIR.rglob("*") if f.is_file()]
    total = len(files)
    uploaded = 0

    for f in files:
        rel = f.relative_to(DEPLOY_DIR).as_posix()
        blob = bucket.blob(f"assets/{rel}")

        # Content-Type setzen
        content_type = "image/webp" if f.suffix == ".webp" else "application/json"
        blob.upload_from_filename(str(f), content_type=content_type)

        uploaded += 1
        print(f"  [{uploaded}/{total}] {rel}")

    print(f"\n{uploaded} Dateien hochgeladen nach gs://{BUCKET_NAME}/assets/")

if __name__ == "__main__":
    main()
```

**Voraussetzung:** `pip install google-cloud-storage` (oder `py -m pip install google-cloud-storage`)

**Step 2: Upload ausführen**

```bash
py F:/AI/ComfyUI_workflows/upload_assets.py
```

Lädt alle Assets + Manifest hoch.

### Phase 5 — Review Checkpoint

1. Manifest JSON valide? `py -c "import json; json.load(open('F:/AI/RebornSaga_Assets/deploy/assets/asset_manifest.json'))"`
2. Manifest-Format: `packs` ist Dictionary (nicht Array), jede File hat `hash` (nicht `sha256`)
3. Firebase Upload erfolgreich? Stichproben-Download testen
4. AssetDeliveryService.CheckForUpdatesAsync() findet die neuen Assets

---

## Phase 6: Build & Release

### Task 6.1: Vollständiger Build + AppChecker

**Step 1: Clean Build**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
dotnet build src/Apps/RebornSaga/RebornSaga.Android
```

**Step 2: AppChecker**

```bash
dotnet run --project tools/AppChecker RebornSaga
```

Alle Checks müssen PASS sein.

---

### Task 6.2: Android-Test auf physischem Gerät

**Step 1: Debug-APK auf Gerät installieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Android -t:Install
```

**Prüfpunkte:**
- [ ] App startet → Download-Screen erscheint
- [ ] Assets werden heruntergeladen (Progress-Bar)
- [ ] TitleScene mit AI-Hintergrund
- [ ] DialogueScene mit AI-Character-Portraits
- [ ] BattleScene mit AI-Enemy-Sprites
- [ ] OverworldScene mit AI-Map-Hintergrund + Node-Icons
- [ ] InventoryScene mit AI-Item-Icons
- [ ] SFX spielen bei Button-Tap, Kampf-Aktionen
- [ ] BGM wechselt zwischen Szenen
- [ ] Kein OOM / kein Crash bei längerem Spielen
- [ ] Performance: 60fps ohne Stutter

---

### Task 6.3: CLAUDE.md aktualisieren

**Files:**
- Modify: `src/Apps/RebornSaga/CLAUDE.md` — Asset-Status, Audio-Status, Lokalisierung
- Modify: `F:/Meine_Apps_Ava/CLAUDE.md` — Version aktualisieren wenn released

---

### Task 6.4: Version erhöhen + AAB erstellen (NUR auf Anfrage)

**Step 1: Version in csproj erhöhen**

```xml
<!-- src/Apps/RebornSaga/RebornSaga.Android/RebornSaga.Android.csproj -->
<ApplicationVersion>2</ApplicationVersion>
<ApplicationDisplayVersion>1.0.1</ApplicationDisplayVersion>
```

**Step 2: AAB erstellen**

```bash
dotnet publish src/Apps/RebornSaga/RebornSaga.Android -c Release
```

**Step 3: In Releases kopieren**

```bash
cp src/Apps/RebornSaga/RebornSaga.Android/bin/Release/net10.0-android/publish/*.aab \
   Releases/RebornSaga/
```

---

## Zusammenfassung: Task-Übersicht

| Phase | Task | Beschreibung | Abhängig von |
|-------|------|-------------|--------------|
| 0 | 0.1 | SKMaskFilter Disposal-Bug fixen (6 Dateien) | — |
| 1 | 1.1 | Training-Daten kuratieren (25-30 Bilder) | — |
| 1 | 1.2 | Automatisches Captioning (WD14 Tagger) | 1.1 |
| 1 | 1.3 | Style-LoRA trainieren (~66 Min) | 1.2 |
| 1 | 1.4 | Style-LoRA validieren (8 Testbilder) | 1.3 |
| 2 | 2.1 | Scripts mit Style-LoRA updaten | 1.4 |
| 2 | 2.2 | Alle Assets generieren (~3h): 147 Sprites + 33 Overlays + 30 Enemies + 14 BGs + 58 Items + 19 Map | 2.1 |
| 2 | 2.3 | Assets in Deploy-Ordner kopieren | 2.2 |
| 3a | 3.1 | AssetDownloadScene implementieren | 0.1 |
| 3a | 3.2 | BattleScene — AI Enemy-Sprites | 0.1 |
| 3a | 3.3 | InventoryScene — Item-Icons | 0.1 |
| 3a | 3.4 | OverworldScene — Map-Assets | 0.1 |
| 3a | 3.5 | Altes prozedurales System entfernen | 2.3 + 3.2 + 3.4 |
| 3a | 3.9 | Element-Partikel-Configs (6 Presets) | 0.1 |
| 3a | 3.10 | Angriffs-Animation (Translate + Slash) | 0.1 |
| 3a | 3.11 | Dodge-Ghosting-Effekt | 0.1 |
| 3a | 3.12 | SplashArt für Ultimate-Skills verdrahten | 0.1 |
| 3a | 3.13 | MangaPanelRenderer in DialogueScene | 0.1 |
| 3a | 3.14 | GlitchEffect in DialogueScene (ARIA) | 0.1 |
| 3a | 3.15 | Kamera-Effekte in DialogueScene | 0.1 |
| 3b | 3.6 | SFX-Dateien sammeln (28 Stück) | — |
| 3b | 3.7 | BGM-Dateien sammeln (10 Stück) | — |
| 3b | 3.8 | AndroidAudioService implementieren | 3.6 + 3.7 |
| 4 | 4.1 | AppStrings.Designer.cs generieren | — |
| 4 | 4.2 | Neue Lokalisierungs-Keys (~50) | 4.1 |
| 4 | 4.3 | Hardcodierte Strings ersetzen | 4.2 |
| 5 | 5.1 | Firebase Service Account Key (Robert) | — |
| 5 | 5.2 | Asset-Manifest generieren | 2.3 |
| 5 | 5.3 | Firebase Upload | 5.1 + 5.2 |
| 6 | 6.1 | Build + AppChecker | Alle Phase 3-4 |
| 6 | 6.2 | Android-Test auf Gerät | 5.3 + 6.1 |
| 6 | 6.3 | CLAUDE.md aktualisieren | 6.2 |
| 6 | 6.4 | Version + AAB (NUR auf Anfrage) | 6.3 |

**Parallel ausführbar:**
- Phase 3a (Code) + Phase 3b (Audio) + Phase 4 (Lokalisierung) — alle unabhängig voneinander
- Phase 1 (LoRA) kann parallel zu Phase 3a/4 laufen (Code braucht noch keine Assets)
- Task 0.1 (Bugfix) sofort, blockiert nichts
- Tasks 3.9-3.15 (Kampf- und Dialog-Verbesserungen) sind unabhängig von 3.1-3.5 (Asset-Integration)
- Tasks 3.9-3.12 (BattleScene) können parallel zu 3.13-3.15 (DialogueScene) implementiert werden

---

## Abschluss-Pflichten

Nach Abschluss aller Phasen:

1. **Finales Code-Review:** Alle geänderten Dateien gegen die echte Codebase verifizieren (Methoden-Signaturen, Property-Namen, Enum-Werte, Import-Namespaces)
2. **TODO-Scan:** `grep -rn "TODO\|OFFEN\|PRÜFEN\|FIXME\|HACK\|Phase [0-9]" src/Apps/RebornSaga/` — 0 Treffer erforderlich. Jeder Fund muss vor Abschluss erledigt werden
3. **Build-Verifizierung:** `dotnet build src/Apps/RebornSaga/RebornSaga.Shared && dotnet build src/Apps/RebornSaga/RebornSaga.Android` — beide müssen erfolgreich sein
4. **AppChecker:** `dotnet run --project tools/AppChecker RebornSaga` — alle Checks PASS
5. **Memory aktualisieren:** `reborn-saga-visuals.md` mit finalem Status aller Phasen
6. **CLAUDE.md aktualisieren:** RebornSaga CLAUDE.md mit neuen Services, Szenen und Assets

**Geschätzte Gesamtdauer:**
- Phase 0: 5 Min
- Phase 1: ~2h (inkl. 66 Min Training)
- Phase 2: ~3h (inkl. 2.5h Generierung)
- Phase 3a (3.1-3.5): ~4-6h Code (Asset-Integration)
- Phase 3a (3.9-3.15): ~3-4h Code (Kampf- und Dialog-Verbesserungen)
- Phase 3b: ~2-3h Audio-Recherche
- Phase 4: ~2-3h Lokalisierung
- Phase 5: ~30 Min (Firebase Key von Robert)
- Phase 6: ~1-2h Test + Fix
