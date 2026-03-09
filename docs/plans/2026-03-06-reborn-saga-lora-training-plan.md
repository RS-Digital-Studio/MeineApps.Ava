# RebornSaga LoRA Training Pipeline - Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Alle 10 Charakter-LoRAs trainieren und validieren, bevor die Asset-Produktion startet.

**Architecture:** Pro Charakter: Referenz-Sheet generieren → 20 Trainingsbilder mit Outfit-Variationen → kohya_ss SDXL LoRA Training → Validierung mit Test-Prompts. Aria zuerst (Referenzbild vorhanden), dann restliche 9. Jedes LoRA wird einzeln validiert bevor das nächste beginnt.

**Tech Stack:** ComfyUI + Animagine XL 4.0 Opt, kohya_ss (SDXL LoRA Training), Python 3.9+, RTX 4080 16GB VRAM

---

## Task 1: kohya_ss installieren und konfigurieren

**Files:**
- Create: `F:\AI\kohya_ss\` (Installation)
- Create: `F:\AI\RebornSaga_Assets\training\` (Trainings-Ordner-Struktur)

**Step 1: kohya_ss klonen und installieren**

```bash
cd F:\AI
git clone https://github.com/bmaltais/kohya_ss.git
cd kohya_ss
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu126
```

Falls Python 3.9 zu alt: ComfyUI's embedded Python verwenden:
```bash
"F:\AI\ComfyUI_windows_portable\python_embeded\python.exe" -m venv venv
```

**Step 2: Ordnerstruktur für Trainingsbilder erstellen**

```bash
mkdir -p F:\AI\RebornSaga_Assets\training\{aria,protag_sword,protag_mage,protag_assassin,luna,kael,aldric,system_aria,vex,nihilus}\img
mkdir -p F:\AI\RebornSaga_Assets\training\{aria,protag_sword,protag_mage,protag_assassin,luna,kael,aldric,system_aria,vex,nihilus}\model
mkdir -p F:\AI\RebornSaga_Assets\loras
mkdir -p F:\AI\RebornSaga_Assets\validation
```

**Step 3: Basis-Trainings-Config erstellen**

Create: `F:\AI\RebornSaga_Assets\training\sdxl_lora_config.toml`

```toml
[model]
pretrained_model_name_or_path = "F:\\AI\\ComfyUI_windows_portable\\ComfyUI\\models\\checkpoints\\animagine-xl-4.0-opt.safetensors"
v2 = false
v_parameterization = false
sdxl = true

[training]
resolution = "1024,1024"
train_batch_size = 1
max_train_epochs = 10
learning_rate = 1e-4
unet_lr = 1e-4
text_encoder_lr = 5e-5
lr_scheduler = "cosine_with_restarts"
lr_warmup_steps = 100
optimizer_type = "AdamW8bit"
mixed_precision = "bf16"
save_precision = "fp16"
gradient_checkpointing = true
seed = 42

[network]
network_module = "networks.lora"
network_dim = 32
network_alpha = 16

[dataset]
enable_bucket = true
min_bucket_reso = 256
max_bucket_reso = 1536
bucket_reso_steps = 64

[saving]
save_every_n_epochs = 2
save_model_as = "safetensors"
```

**Step 4: Verifizieren**

```bash
cd F:\AI\kohya_ss
venv\Scripts\activate
python sdxl_train_network.py --help
```

Expected: Hilfe-Ausgabe ohne Fehler.

---

## Task 2: Aria LoRA trainieren (Template-Charakter)

Aria ist der erste Charakter. Referenzbild existiert bereits (Seed 3141).

**Files:**
- Input: `F:\AI\ComfyUI_windows_portable\ComfyUI\input\aria_reference.png`
- Create: `F:\AI\ComfyUI_workflows\generate_training_aria.py`
- Create: `F:\AI\RebornSaga_Assets\training\aria\img\` (20 Trainingsbilder + Captions)
- Output: `F:\AI\RebornSaga_Assets\loras\aria_rebornsaga.safetensors`

### Step 1: Referenz-Sheet generieren (5 Bilder)

Basis-Referenzbild (Seed 3141) als Ausgangspunkt. 5 weitere Varianten für Multi-Angle:

```python
# F:\AI\ComfyUI_workflows\generate_training_aria.py
"""Generiert 20 Trainingsbilder fuer Aria LoRA."""
import json, urllib.request, time

COMFYUI_URL = "http://127.0.0.1:8188/prompt"

# Aria Kern-Beschreibung (immer identisch)
ARIA_CORE = "aria_char, 1girl, long flowing red hair, bright green eyes, beautiful detailed eyes"

# 20 Trainingsbilder mit Variationen
TRAINING_IMAGES = [
    # Outfit 1: Lederrüstung (Hauptoutfit)
    {"prompt": f"{ARIA_CORE}, leather armor with gold buckles, brown chestpiece, standing, facing viewer, confident smile, upper body", "seed": 3141, "name": "01_leather_standing_front"},
    {"prompt": f"{ARIA_CORE}, leather armor with gold buckles, brown chestpiece, standing, three quarter view, slight smile, upper body", "seed": 3142, "name": "02_leather_standing_34"},
    {"prompt": f"{ARIA_CORE}, leather armor with gold buckles, brown chestpiece, battle stance, holding sword, determined expression, full body", "seed": 3143, "name": "03_leather_battle_fullbody"},
    {"prompt": f"{ARIA_CORE}, leather armor with gold buckles, brown chestpiece, sitting on chair, relaxed, happy smile, upper body", "seed": 3144, "name": "04_leather_sitting"},
    {"prompt": f"{ARIA_CORE}, leather armor with gold buckles, brown chestpiece, close up face, neutral expression, portrait", "seed": 3145, "name": "05_leather_closeup"},
    {"prompt": f"{ARIA_CORE}, leather armor with gold buckles, brown chestpiece, looking away, profile view, serious expression", "seed": 3146, "name": "06_leather_profile"},
    {"prompt": f"{ARIA_CORE}, leather armor with gold buckles, brown chestpiece, arms crossed, angry furrowed brows, upper body", "seed": 3147, "name": "07_leather_angry"},
    {"prompt": f"{ARIA_CORE}, leather armor with gold buckles, brown chestpiece, surprised expression, wide eyes, upper body", "seed": 3148, "name": "08_leather_surprised"},

    # Outfit 2: Casual (Taverne)
    {"prompt": f"{ARIA_CORE}, simple white blouse, brown skirt, casual clothes, standing relaxed, gentle smile, upper body", "seed": 4001, "name": "09_casual_standing"},
    {"prompt": f"{ARIA_CORE}, simple white blouse, brown skirt, casual clothes, sitting at table, drinking, happy, upper body", "seed": 4002, "name": "10_casual_sitting"},
    {"prompt": f"{ARIA_CORE}, simple white blouse, brown skirt, casual clothes, laughing, eyes closed from joy, upper body", "seed": 4003, "name": "11_casual_laughing"},

    # Outfit 3: Kampfrüstung (verstärkt)
    {"prompt": f"{ARIA_CORE}, heavy battle armor, steel pauldrons, red cape, battle ready, determined, full body", "seed": 5001, "name": "12_heavy_battle"},
    {"prompt": f"{ARIA_CORE}, heavy battle armor, steel pauldrons, red cape, kneeling, exhausted, wounded, upper body", "seed": 5002, "name": "13_heavy_wounded"},

    # Verschiedene Beleuchtungen
    {"prompt": f"{ARIA_CORE}, leather armor, warm orange firelight, campfire scene, soft smile, upper body", "seed": 6001, "name": "14_firelight"},
    {"prompt": f"{ARIA_CORE}, leather armor, cold blue moonlight, night scene, serious expression, upper body", "seed": 6002, "name": "15_moonlight"},
    {"prompt": f"{ARIA_CORE}, leather armor, dramatic rim lighting, dark background, intense stare, upper body", "seed": 6003, "name": "16_dramatic"},

    # Emotionen
    {"prompt": f"{ARIA_CORE}, leather armor, crying, tears streaming, sad expression, upper body", "seed": 7001, "name": "17_crying"},
    {"prompt": f"{ARIA_CORE}, leather armor, blushing, embarrassed shy smile, looking away, upper body", "seed": 7002, "name": "18_blushing"},
    {"prompt": f"{ARIA_CORE}, leather armor, shouting, fierce battle cry, wide open mouth, upper body", "seed": 7003, "name": "19_shouting"},
    {"prompt": f"{ARIA_CORE}, leather armor, gentle warm smile, peaceful serene expression, wind in hair, upper body", "seed": 7004, "name": "20_peaceful"},
]

NEGATIVE = (
    "lowres, bad anatomy, bad hands, text, error, missing fingers, extra digit, "
    "fewer digits, cropped, worst quality, low quality, normal quality, "
    "jpeg artifacts, signature, watermark, username, blurry, nsfw, "
    "multiple girls, 2girls, extra arms, deformed, ugly, duplicate, "
    "missing mouth, no mouth, chibi"
)

STYLE_SUFFIX = ", anime style, clean lineart, cel shading, masterpiece, best quality, very aesthetic, absurdres"


def make_workflow(img_data):
    positive = img_data["prompt"] + STYLE_SUFFIX
    return {
        "prompt": {
            "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": "animagine-xl-4.0-opt.safetensors"}},
            "2": {"class_type": "CLIPTextEncode", "inputs": {"text": positive, "clip": ["1", 1]}},
            "3": {"class_type": "CLIPTextEncode", "inputs": {"text": NEGATIVE, "clip": ["1", 1]}},
            "4": {"class_type": "EmptyLatentImage", "inputs": {"width": 832, "height": 1216, "batch_size": 1}},
            "5": {"class_type": "KSampler", "inputs": {
                "model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0],
                "latent_image": ["4", 0], "seed": img_data["seed"],
                "steps": 28, "cfg": 7.0, "sampler_name": "euler_ancestral",
                "scheduler": "normal", "denoise": 1.0
            }},
            "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
            "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": f"aria_train_{img_data['name']}"}}
        }
    }


if __name__ == "__main__":
    print(f"Generiere {len(TRAINING_IMAGES)} Aria-Trainingsbilder...")
    for i, img in enumerate(TRAINING_IMAGES):
        workflow = make_workflow(img)
        data = json.dumps(workflow).encode("utf-8")
        req = urllib.request.Request(COMFYUI_URL, data=data, headers={"Content-Type": "application/json"})
        resp = urllib.request.urlopen(req)
        result = json.loads(resp.read())
        print(f"  [{i+1}/{len(TRAINING_IMAGES)}] {img['name']}: {result.get('prompt_id', 'OK')}")
        time.sleep(0.3)
    print("\nAlle in der Queue.")
```

Run:
```bash
python generate_training_aria.py
```

**Step 2: Trainingsbilder kuratieren**

1. Alle 20 generierten Bilder ansehen
2. Bilder mit Fehlern entfernen (falsche Augenfarbe, fehlende Haare, etc.)
3. Mindestens 15 gute Bilder behalten
4. In `F:\AI\RebornSaga_Assets\training\aria\img\20_aria_char\` kopieren (Ordnername = Repeats_Triggerwort)

**Step 3: Caption-Dateien erstellen**

Pro Trainingsbild eine `.txt`-Datei mit gleichem Namen:

```
aria_char, 1girl, long flowing red hair, bright green eyes, leather armor with gold buckles, standing, facing viewer, confident smile, upper body, anime style
```

Format: `{trigger_word}, {booru-style tags}`
- Trigger-Wort: `aria_char` (einheitlich für alle Bilder)
- Tags beschreiben was auf dem Bild zu sehen ist

**Step 4: LoRA Training starten**

```bash
cd F:\AI\kohya_ss
venv\Scripts\activate

python sdxl_train_network.py \
  --pretrained_model_name_or_path="F:\AI\ComfyUI_windows_portable\ComfyUI\models\checkpoints\animagine-xl-4.0-opt.safetensors" \
  --train_data_dir="F:\AI\RebornSaga_Assets\training\aria\img" \
  --output_dir="F:\AI\RebornSaga_Assets\training\aria\model" \
  --output_name="aria_rebornsaga" \
  --save_model_as=safetensors \
  --resolution=1024 \
  --train_batch_size=1 \
  --max_train_epochs=10 \
  --learning_rate=1e-4 \
  --unet_lr=1e-4 \
  --text_encoder_lr=5e-5 \
  --lr_scheduler=cosine_with_restarts \
  --lr_warmup_steps=100 \
  --optimizer_type=AdamW8bit \
  --mixed_precision=bf16 \
  --save_precision=fp16 \
  --network_module=networks.lora \
  --network_dim=32 \
  --network_alpha=16 \
  --gradient_checkpointing \
  --enable_bucket \
  --min_bucket_reso=256 \
  --max_bucket_reso=1536 \
  --save_every_n_epochs=2 \
  --seed=42 \
  --cache_latents \
  --cache_latents_to_disk
```

Trainingszeit: ~30-60 Min auf RTX 4080.

**Step 5: LoRA in ComfyUI kopieren**

```bash
cp F:\AI\RebornSaga_Assets\training\aria\model\aria_rebornsaga.safetensors \
   F:\AI\ComfyUI_windows_portable\ComfyUI\models\loras\
```

**Step 6: Validierung - Test-Prompts generieren**

Create: `F:\AI\ComfyUI_workflows\validate_aria_lora.py`

Test-Prompts die NICHT in den Trainingsdaten waren:

```python
VALIDATION_PROMPTS = [
    # Neue Posen
    "aria_char, 1girl, running, action pose, leather armor, determined expression, full body",
    "aria_char, 1girl, leaning against wall, arms crossed, smirk, casual clothes, upper body",
    "aria_char, 1girl, kneeling on one knee, sword planted in ground, exhausted, full body",
    # Neue Emotionen
    "aria_char, 1girl, leather armor, disgusted expression, looking away, upper body",
    "aria_char, 1girl, leather armor, confused puzzled expression, head tilted, upper body",
    # Neue Szenen
    "aria_char, 1girl, leather armor, snowy landscape, cold breath visible, determined, upper body",
    "aria_char, 1girl, tavern clothes, dancing, joyful, festive scene, full body",
    # Andere Charaktere im Bild (Aria bleibt Aria)
    "aria_char, 1girl, leather armor, forest background, standing with sword, warrior pose, full body",
]
```

ComfyUI-Workflow mit LoRA:
```json
{
    "class_type": "LoraLoader",
    "inputs": {
        "model": ["checkpoint", 0],
        "clip": ["checkpoint", 1],
        "lora_name": "aria_rebornsaga.safetensors",
        "strength_model": 0.8,
        "strength_clip": 0.8
    }
}
```

**Step 7: Validierung prüfen**

Erfolgskriterien:
- [ ] Rote Haare in allen Bildern konsistent
- [ ] Grüne Augen in allen Bildern
- [ ] Gesichtszüge wiedererkennbar über alle Posen
- [ ] Outfit folgt dem Prompt (nicht immer Lederrüstung)
- [ ] Mund sichtbar in allen Bildern
- [ ] Keine Artefakte oder Qualitätsverlust vs. Basis-Modell
- [ ] Unterschiedliche Posen/Emotionen deutlich erkennbar

Falls nicht zufrieden: Trainingsbilder anpassen, Training wiederholen mit angepassten Parametern.

---

## Task 3-11: Restliche 9 Charaktere

Jeder Charakter folgt dem gleichen Workflow wie Aria (Task 2). Hier die charakter-spezifischen Prompts.

### Task 3: Protagonist Sword LoRA

**Trigger-Wort:** `protag_sword_char`
**Kern-Prompt:** `protag_sword_char, 1boy, short spiky dark blue hair, bright blue eyes, muscular build`
**Outfit-Variationen:**
- Dunkelblau-graue Rüstung mit roten Akzenten + Schwert (Hauptoutfit)
- Einfache Reisekleidung (Start-Outfit)
- Verstärkte Plattenrüstung (Upgrade)

### Task 4: Protagonist Mage LoRA

**Trigger-Wort:** `protag_mage_char`
**Kern-Prompt:** `protag_mage_char, 1boy, long straight purple hair, mystical purple eyes, slim build`
**Outfit-Variationen:**
- Dunkelblau-lila Robe + Kristallstab (Hauptoutfit)
- Einfache Reisekleidung (Start-Outfit)
- Erzmagier-Robe mit Runen (Upgrade)

### Task 5: Protagonist Assassin LoRA

**Trigger-Wort:** `protag_assassin_char`
**Kern-Prompt:** `protag_assassin_char, 1boy, short black hair, green eyes, lean agile build`
**Outfit-Variationen:**
- Sehr dunkle Kleidung mit grünen Akzenten + Dolche (Hauptoutfit)
- Einfache Reisekleidung (Start-Outfit)
- Schatten-Assassinen-Outfit (Upgrade)

### Task 6: Luna LoRA

**Trigger-Wort:** `luna_char`
**Kern-Prompt:** `luna_char, 1girl, very long light blue hair in braid, lavender eyes, gentle soft features`
**Outfit-Variationen:**
- Weißes Heilergewand mit hellblauen Akzenten + Heilstab (Hauptoutfit)
- Einfaches weißes Kleid (Casual)
- Verstärktes Heilergewand mit Runen (Upgrade)

### Task 7: Kael LoRA

**Trigger-Wort:** `kael_char`
**Kern-Prompt:** `kael_char, 1boy, medium length messy brown hair, amber eyes, lean build, roguish smirk`
**Outfit-Variationen:**
- Dunkelbraune Lederkleidung mit Goldakzenten + Dolche (Hauptoutfit)
- Zerrissene beschädigte Kleidung (nach Kampf)
- Casual Reisekleidung (Taverne)

### Task 8: Aldric LoRA

**Trigger-Wort:** `aldric_char`
**Kern-Prompt:** `aldric_char, 1boy, long white silver hair, glowing blue eyes, elder wise face, tall stature`
**Outfit-Variationen:**
- Dunkel-lila Robe mit Gold + Magierstab (Hauptoutfit)
- Einfache dunkle Reise-Robe (verdeckt)
- Zeremonie-Robe mit leuchtenden Runen (Macht-Moment)

### Task 9: System ARIA LoRA

**Trigger-Wort:** `system_aria_char`
**Kern-Prompt:** `system_aria_char, 1girl, medium blue translucent hair, glowing blue eyes, ethereal floating figure, holographic digital appearance`
**Outfit-Variationen:**
- Blau-transparentes digitales Gewand (Hauptoutfit, einziges)
- Verschiedene Hologramm-Intensitäten (schwach/stark)

**Spezial:** SkiaSharp-Shader fügt Scanlines/Glow hinzu. LoRA trainiert ohne Hologramm-Effekt, nur den Charakter.

### Task 10: Vex LoRA

**Trigger-Wort:** `vex_char`
**Kern-Prompt:** `vex_char, 1boy, very short black hair, red eyes, dark skin, sly cunning expression`
**Outfit-Variationen:**
- Dunkelbraune Diebeskleidung mit Gold + Dolche (Hauptoutfit)
- Händler-Kleidung mit Umhang (Shop-Szenen)

### Task 11: Nihilus LoRA

**Trigger-Wort:** `nihilus_char`
**Kern-Prompt:** `nihilus_char, 1boy, wild black hair, glowing dark red eyes, very dark skin, menacing aura, imposing figure`
**Outfit-Variationen:**
- Dunkle Robe mit blutroten Akzenten (Hauptoutfit)
- Entfesselte Form (Phase 2, mehr Aura, wildere Haare)

**Spezial:** SkiaSharp-Shader fügt Aura-Glow hinzu. LoRA trainiert den Basis-Charakter.

---

## Task 12: Xaroth LoRA (Bonus, niedrige Priorität)

**Trigger-Wort:** `xaroth_char`
**Kern-Prompt:** `xaroth_char, 1boy, long grey hair, red eyes, pale skin, sinister mage, dark robes with red accents`

Xaroth erscheint erst spät in Arc 1. Kann nach den anderen trainiert werden.

---

## Validierungs-Checkliste (pro Charakter)

Nach jedem LoRA-Training ALLE diese Punkte prüfen:

- [ ] **Identität:** Charakter ist über alle Test-Prompts wiedererkennbar
- [ ] **Haarfarbe:** Konsistent in allen Bildern
- [ ] **Augenfarbe:** Konsistent in allen Bildern
- [ ] **Gesichtszüge:** Gleiche Person über alle Varianten
- [ ] **Outfit-Wechsel:** LoRA erlaubt verschiedene Outfits per Prompt
- [ ] **Posen-Vielfalt:** Stehen, Sitzen, Kampf, Nahaufnahme funktionieren
- [ ] **Emotionen:** Neutral, Happy, Angry, Sad, Surprised, Determined erkennbar
- [ ] **Mund sichtbar:** In ALLEN Bildern
- [ ] **Qualität:** Kein Qualitätsverlust gegenüber Basis-Modell
- [ ] **LoRA-Stärke:** 0.7-0.9 funktioniert, bei 1.0 kein Overfitting sichtbar

---

## Reihenfolge und Abhängigkeiten

```
Task 1: kohya_ss Setup
  ↓
Task 2: Aria LoRA (Template, ausführlich)
  ↓ (nur wenn Aria validiert und gut)
Task 3-5: Protagonist Sword/Mage/Assassin (parallel möglich)
  ↓
Task 6-8: Luna, Kael, Aldric
  ↓
Task 9-11: System ARIA, Vex, Nihilus
  ↓
Task 12: Xaroth (optional, niedrige Prio)
```

**Kritischer Gate:** Aria LoRA MUSS gut sein bevor weitere Charaktere trainiert werden. Falls Aria nicht funktioniert, Training-Parameter anpassen (Rank, Steps, LR, Trainingsbilder-Qualität).

---

## Ergebnis

Nach Abschluss aller Tasks:
- 10 (oder 11) LoRA-Dateien in `F:\AI\ComfyUI_windows_portable\ComfyUI\models\loras\`
- Jedes LoRA validiert und für gut befunden
- Bereit für Phase 2: Story-getriebene Asset-Generierung (beliebig viele Posen/Emotionen/Szenen pro Charakter)
