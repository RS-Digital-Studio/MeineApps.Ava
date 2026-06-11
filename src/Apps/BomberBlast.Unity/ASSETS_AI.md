# BomberBlast 3D — KI-Asset-Pipeline (3D + Audio + Animation)

> **Status:** Produktions-Plan (Stand 2026-05-30, recherchiert; Richtung v0.5 2026-06-08)
> **Ziel:** Skalierbarer, EU-konformer und kommerziell sauberer Workflow für 3D-Assets, Animationen, Texturen und Audio mit KI-Tools — primär lokal (ComfyUI + EU-konforme OSS-Modelle), Cloud-Services als Production-Standard wo Qualität es rechtfertigt.
> **Geltungsbereich:** die **5 Helden-Charaktere**, 12 Gegner, 5 Bosse, 14 Bomben-Typen, 12 PowerUps, 10 Sektor-Tile-Sets, Environment, Props, Animationen, Texturen, Game-Audio — alles im **Neon-Arcade-Stil des Originals**, jetzt in 3D.
> **Nicht im Scope:** UI-Icons (bleiben 2D), redaktionelle Texte, Story-Schreiben, Voice (deferred — Original ist voice-los).

> **WICHTIG — Richtung v0.5 + Subjekte sind KEINE Mechs:** Dies ist ein **modernes 3D-Bomberman** (aktiv
> gespielt, **kein Idle/AFK**) mit **neuer Story** (Neo-Grid/Overseer/Reborn). Die Charaktere sind die 5
> bestehenden Helden (Default/SpeedySam/BrickBoris/TwinTina/LuckyLola), die Gegner die klassischen
> Bomberman-Typen. Die 5 Bosse werden als **Sektor-Wardens neu benannt/eingekleidet** (Granite Warden /
> Frostwyrm / Magma Revenant / Null Phantom / The Overseer = Archetypen StoneGolem/IceDragon/FireDemon/
> ShadowMaster/FinalBoss) — **gleicher Mesh-Workflow, neue Optik/Namen**. „Welten" heißen jetzt **Sektoren**.
> Die spielbaren Charaktere sind die 5 Helden (humanoid, Neon-Arcade) — keine „Mechs".

> ⚠️ **EU-Compliance-Warnung:** Hunyuan3D (Tencent) ist in der EU/UK/Südkorea per Lizenz **explizit ausgeschlossen** und erfordert schriftliche Tencent-Sonderfreigabe. Wir bauen bewusst eine **EU-konforme Pipeline** ohne Hunyuan als Default. Details: [§14](#14-eu-compliance--lizenz-recherche-stand-2026-05).

---

## Inhaltsverzeichnis

1. [Strategische Entscheidung](#1-strategische-entscheidung)
2. [Pipeline-Überblick](#2-pipeline-überblick)
3. [Tool-Stack (recherchiert + EU-validiert)](#3-tool-stack-recherchiert--eu-validiert)
4. [Hardware & Setup](#4-hardware--setup)
5. [Stage 1 — 2D-Konzept (Flux/SDXL + Style-LoRA)](#5-stage-1--2d-konzept-fluxsdxl--style-lora)
6. [Stage 2 — Image-to-3D (TRELLIS 2 / SPAR3D / TripoSG)](#6-stage-2--image-to-3d-trellis-2--spar3d--triposg)
7. [Stage 3 — Blender-Cleanup](#7-stage-3--blender-cleanup)
8. [Stage 4 — Texturing + Materialien](#8-stage-4--texturing--materialien)
9. [Stage 5 — Rigging + Animation](#9-stage-5--rigging--animation)
10. [Stage 6 — Unity-Import](#10-stage-6--unity-import)
11. [Stage 7 — Audio (Musik + SFX + Voice)](#11-stage-7--audio-musik--sfx--voice)
12. [Asset-Kategorien & Budgets (Neon-Arcade)](#12-asset-kategorien--budgets-neon-arcade)
13. [Stil-Konsistenz (Neon-Arcade)](#13-stil-konsistenz-neon-arcade)
14. [EU-Compliance & Lizenz-Recherche (Stand 2026-05)](#14-eu-compliance--lizenz-recherche-stand-2026-05)
15. [Pilot-Plan (5 Assets vor Skalierung)](#15-pilot-plan-5-assets-vor-skalierung)
16. [Output-Ablage + Versionierung](#16-output-ablage--versionierung)
17. [Risiken & Mitigation](#17-risiken--mitigation)
18. [Verweise](#18-verweise)

---

## 1. Strategische Entscheidung

3D-Asset-Generierung mit KI ist 2026 für **stylisierte Neon-Arcade-Charaktere/Props** Production-reif. Wir setzen es als Standard-Pipeline, nicht als Notlösung. Aufwändigere Boss-Modelle für Cinematics gehen optional über Cloud-Services mit Artist-Polish.

**Kern-Entscheidungen (verbindlich):**

- **EU-konformer OSS-Stack** als Default — kein Hunyuan3D ohne Tencent-Sonderfreigabe.
- **Lokale Pipeline primär**: ComfyUI 0.3.x + ComfyUI-3D-Pack mit **TRELLIS 2** (Microsoft, MIT) als Geometrie-Hauptmodell.
- **Cloud-Services für Production**: Meshy 6 oder Rodin Gen-2.5 für die ~10-15% Assets, wo OSS-Qualität nicht reicht oder Auto-Rigging beschleunigt.
- **Tripo 3.0** als optionales Komplett-Werkzeug mit integriertem Auto-Rigging (Cloud, Saas).
- **Audio**: Stable Audio 3 (Open-Weight, lizenzierte Trainingsdaten) als Default. Suno wegen ungeklärter Trainingsdaten-Lawsuits **gemieden**.
- **Animation**: Cascadeur für AI-assistierte Keyframes, DeepMotion/RADiCAL für Video-to-Motion. Mixamo bleibt für Standard-Humanoid-Skeletts.
- **Workflow-JSONs versioniert** unter `F:\AI\ComfyUI_workflows\bomberblast_unity\` mit Git-LFS.
- **Output-Format:** GLB für Pipeline-Transport, FBX nach Cleanup für Unity-Import.
- **Polygon-Budget Mobile** (Mid-Tier-Android, 3 GB RAM Ziel): siehe [§12](#12-asset-kategorien--budgets-neon-arcade).

---

## 2. Pipeline-Überblick

```
┌────────────────────────────────────────────────────────────────────┐
│ Stage 1: 2D-Konzept                                                │
│  Flux.1-dev / SDXL + Style-LoRA + IP-Adapter (ComfyUI)             │
│  → PNG 1024² / 2048², transparenter BG empfohlen                   │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 2: Image-to-3D                                               │
│  Primär: TRELLIS 2 (Microsoft, MIT) — Geometrie + Gaussian         │
│  Backup: SPAR3D (Stability) — Punktwolke-Editierung                │
│  Alternative: TripoSG (VAST, OSS) — Single-Image-Foundation        │
│  Cloud-Fallback: Rodin Gen-2.5 / Meshy 6 (Hero-Assets)             │
│  → GLB mit PBR-Texturen (BaseColor, Normal, MRA)                   │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 3: Blender-Cleanup (Props 5-10min, Chars: Retopo separat)    │
│  Decimate (Props) / Retopo (Chars), UV-Repair, Scale, FBX-Export   │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 4: Texturing/PBR (optional, wenn Stage-2-Tex unzureichend)   │
│  Substance 3D Sampler 4.4 — Image-to-Material, Upscale             │
│  oder Unity AI Texture-Generator (in Unity 6.2 integriert)         │
│  oder ComfyUI Material-LoRA für stilkonsistente PBR-Sets           │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 5: Rigging + Animation (humanoide Helden + Wardens)             │
│  Auto-Rig: Mixamo (Standard) ODER Tripo Auto-Rig (universal)       │
│  Animation: Cascadeur (AI-Posing) ODER DeepMotion (Video-to-Motion)│
│  → FBX mit Animation-Set (Idle, Walk, Attack, Death, Mood-States)  │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 6: Unity-Import (Unity 6000.4.8f1 + URP 17.0.4)              │
│  Addressables-Gruppe, LOD-Group, Material-Setup (URP/Lit)          │
│  Layer, Collider, Prefab-Variant                                   │
└────────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌──────────────────────────┐    ┌────────────────────────────┐
│ Stage 7: Audio           │    │ Final: AssetReview Scene   │
│ Stable Audio 3 (Musik)   │    │ Cinematic-Lighting-Test    │
│ Stable Audio 3 (SFX)     │    │ Mobile-Performance-Profile │
│ ElevenLabs (Voice)       │    │ Build-Smoke (Android-AAB)  │
└──────────────────────────┘    └────────────────────────────┘
```

---

## 3. Tool-Stack (recherchiert + EU-validiert)

### 3.1 Primär — Lokal, EU-konform (Apache/MIT/Stability-Community)

| Tool | Version (Mai 2026) | Lizenz | Rolle | URL |
|------|---------------------|--------|-------|-----|
| **ComfyUI** | 0.3.x (laufend) | GPL-3.0 (Tool) | Orchestrator | github.com/comfyanonymous/ComfyUI |
| **ComfyUI-3D-Pack** | 5/Jun/2025 + Updates | MIT | Image-to-3D-Suite | github.com/MrForExample/ComfyUI-3D-Pack |
| **TRELLIS 2** (Microsoft) | CVPR'25 + 2.0 update | MIT | Primärer Image-to-3D-Algorithmus | github.com/microsoft/TRELLIS.2 |
| **SPAR3D** (Stability AI) | 1.0 (Jan 2025) | Stability Community ≤ $1M Umsatz | Punktwolke + schnelle Edits (<1s) | github.com/Stability-AI/stable-point-aware-3d |
| **Stable Fast 3D (SF3D)** | 1.0 | Stability Community | Schnelle Vorschau | huggingface.co/stabilityai/stable-fast-3d |
| **TripoSG** (VAST) | 1.5B Params (Mar 2025) | OSS, kommerziell OK | Foundation-Modell Single-Image | github.com/VAST-AI-Research/TripoSG |
| **TripoSF** (VAST) | (Mar 2025) | OSS, kommerziell OK | Open-Surface-Assets (Tuch, dünne Geometrie) | github.com/VAST-AI-Research/TripoSF |
| **InstantMesh** | 1.0 | Apache-2.0 | Multi-View → Mesh (Backup) | github.com/TencentARC/InstantMesh |
| **Stable Audio Open Small/Medium** | 3.0 (Mai 2026) | Stability Community ≤ $1M | Musik + SFX (lizenzierte Trainingsdaten!) | huggingface.co/stabilityai |
| **Blender** | 4.3+ | GPL | Cleanup, Decimation, Export | blender.org |

> ⚠️ **NICHT genutzt (EU-Lizenz-Ausschluss):**
> - **Hunyuan3D-2** und **Hunyuan3D-2.5** (Tencent) — Lizenz schließt EU/UK/Korea per Definition `Territory` aus. Nur mit schriftlicher Sonderfreigabe. Source: [Hunyuan3D-2 LICENSE](https://github.com/Tencent-Hunyuan/Hunyuan3D-2/blob/main/LICENSE), bestätigt in [Issue #94](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1/issues/94).

### 3.2 Cloud (Production, mit kommerzieller Lizenz)

| Service | Version (Mai 2026) | Preis | Lizenz | Stärke |
|---------|---------------------|-------|--------|--------|
| **Meshy** | 6 (Jan 2026) | $20-$60/Mo (Pro+) | Pro-Tier: Volle Commercial Rights | Schnelle Iteration, Unity-Plugin, Blender-Plugin |
| **Rodin** (Hyper3D) | Gen-2.5 | $0.40-$1.50/Asset, **Free Tier mit Commercial Rights** | Alle Tiers kommerziell | Beste PBR-Texturen, Quad-Mesh-Output |
| **Tripo3D** (Studio) | 3.0 | Tier-Pricing (Saas) | Pro-Tier kommerziell | Komplett-Pipeline mit Auto-Rigging integriert |

> **Hinweis Free-Tier:** Meshy Free-Tier liefert nur CC BY 4.0 (Attribution-Pflicht — untauglich für Production). Rodin Free-Tier hat laut Anbieter "full commercial rights" auf allen Tiers — vor Production-Nutzung dennoch Lizenz-PDF archivieren.

### 3.3 Texturing/Material

| Tool | Version | Lizenz | Rolle |
|------|---------|--------|-------|
| **Adobe Substance 3D Sampler** | 4.4 | Adobe Creative Cloud (CC-Sub) | Image-to-Material, Text-to-Texture (Beta), Upscale |
| **Unity AI Texture** (in Unity 6.2) | 6.2 (Aug 2025) | Unity-Sub erforderlich | PBR-Generation aus Text/Image direkt im Editor |

### 3.4 Animation + Rigging

| Tool | Lizenz | Stärke | Hinweis |
|------|--------|--------|---------|
| **Mixamo** (Adobe) | Kostenlos, kommerziell OK | Standard-Humanoid-Rigging + Animations-Bibliothek | Funktioniert nur bei Standard-Humanoid-Proportionen |
| **Tripo Auto-Rigging** | Tripo-Sub | Universal-Rig (humanoid + non-humanoid) | In Tripo 3.0 Cloud-Pipeline integriert |
| **Reallusion AccuRIG 2** | Free (mit RL-Acc) | Auto-Rig humanoid + non-humanoid, AI Body-Detection | Solider Mixamo-Konkurrent |
| **Cascadeur** (Nekki) | Free für Indie < $100k Rev | AI-AutoPosing, Finger-Posing, Mixamo-Skelett-Kompatibel | Best-in-Class Keyframe-Animation mit Physics |
| **DeepMotion Animate 3D** | Saas, Tier-Pricing | Video-to-3D-Animation, Retargeting, Echtzeit | Eigene Video-Aufnahme = sauberste Lizenz |
| **RADiCAL** (von Autodesk übernommen Apr 2026) | Saas | Video-to-Motion, Stream-fähig (Unity/Unreal) | Indie-Tier verfügbar |

### 3.5 Audio

| Tool | Version | Lizenz | Rolle | Risiko |
|------|---------|--------|-------|--------|
| **Stable Audio 3** (Stability) | 3.0 (Mai 2026) | Stability Community ≤ $1M; Open-Weight Small/Medium | Musik (bis 6min), SFX | Niedrig — **lizenzierte Trainingsdaten** |
| **ElevenLabs Music + Voice** | aktuell | Pro-Sub kommerziell | Stinger, Voice-Lines, kurze Tracks | Niedrig |
| **AIVA** | aktuell | Pro-Sub | Cinematic-Scoring | Niedrig |
| **Suno v4/v5** | — | Pro-Sub, **aber Trainingsdaten-Lawsuits laufend** | (vermieden) | **Hoch** — Output-Rights unklar |
| **Udio** | — | Pro-Sub, **gleiches Risiko wie Suno** | (vermieden) | **Hoch** |

---

## 4. Hardware & Setup

### 4.1 Empfohlene Workstation (Stand Mai 2026)

| Komponente | Mindest | Empfohlen |
|-----------|---------|-----------|
| GPU | RTX 3090 (24 GB) | RTX 4090 / 5090 (24-32 GB) — TRELLIS 2 + TripoSG wollen 16 GB+ |
| RAM | 32 GB | 64 GB (Blender bei Hi-Poly-Imports) |
| CPU | 8 Cores | 16 Cores (parallele Blender-Cleanups) |
| Disk | 1 TB NVMe | 2 TB NVMe (Modelle + Workspace) |

### 4.2 ComfyUI-3D-Pack Installation (Windows)

Verifizierte Anforderungen (Stand 5/Jun/2025 lt. README):

- **Python:** 3.12
- **CUDA:** 12.4
- **PyTorch:** 2.5.1+cu124
- **Visual Studio Build Tools** (Windows) für native Module
- **VRAM:** 16 GB empfohlen (manche Algorithmen wie Era3D brauchen das hart)

Installation:

```powershell
# In ComfyUI/custom_nodes/
cd F:\ComfyUI\custom_nodes\

# 3D-Pack klonen (enthält TRELLIS, SPAR3D, TripoSG, InstantMesh, SF3D usw.):
git clone https://github.com/MrForExample/ComfyUI-3D-Pack
cd ComfyUI-3D-Pack
python install.py
# install.py lädt Pre-Built-Wheels (Win10/11 + Python 3.12 + CU124 + PyTorch 2.5.1)
# oder triggert automatischen Build (braucht VS Build Tools)
```

Alternativen-Installation: **ComfyUI-Manager** (One-Click).

### 4.3 Modell-Downloads

| Modell | Größe | Ablage | Lizenz-OK in EU |
|--------|-------|--------|------------------|
| TRELLIS 2 (image-large) | ~5 GB | `ComfyUI/models/TRELLIS/` | ✅ MIT |
| SPAR3D | ~2 GB | `ComfyUI/models/SPAR3D/` | ✅ Stability Community |
| Stable Fast 3D | ~1.5 GB | `ComfyUI/models/SF3D/` | ✅ Stability Community |
| TripoSG (1.5B) | ~3 GB | `ComfyUI/models/TripoSG/` | ✅ OSS (VAST) |
| InstantMesh | ~1 GB | `ComfyUI/models/InstantMesh/` | ✅ Apache-2.0 |
| Flux.1-dev (für 2D) | ~24 GB | `ComfyUI/models/checkpoints/` | ⚠️ Non-commercial Default; Dev-Lizenz für interne Konzeptarbeit OK, kein redistribuierbarer Output ohne Pro |
| SDXL 1.0 base + refiner | ~13 GB | `ComfyUI/models/checkpoints/` | ✅ Stability Community |

> **Wichtig:** Für die finalen Konzeptbilder, die in den 3D-Generator gehen, **SDXL bevorzugt** (kommerziell sauber). Flux.1-dev nur für interne Iteration, finale Konzepte via SDXL+LoRA produzieren (oder Flux.1-pro mit kommerzieller Lizenz buchen).

### 4.4 Workflow-Ablage

```
F:\AI\ComfyUI_workflows\bomberblast_unity\
├── 00_style_reference\
│   ├── world_neon_arcade\          (15-20 Style-Refs)
│   ├── bomb_arcade_neon\
│   ├── tile_world_themes\
│   └── boss_world_themes\          (Wardens: Granite Warden/Frostwyrm/Magma Revenant/Null Phantom/The Overseer)
├── 01_concept_2d\                   (Stage 1)
│   ├── sdxl_hero_lora.json
│   ├── flux_hero_iter.json          (interne Iteration, nicht Production-Output)
│   └── concept_to_orthographic_views.json
├── 02_image_to_3d\                  (Stage 2)
│   ├── trellis2_full_quality.json
│   ├── spar3d_fast_preview.json
│   ├── triposg_single_image.json
│   └── batch_props.json             (Loop für Prop-Batches)
├── 03_texture_refine\               (Stage 4, optional)
│   ├── stable_diff_pbr_upgrade.json
│   └── material_lora_apply.json
├── 04_audio\                        (Stage 7)
│   ├── stable_audio_music.json
│   └── stable_audio_sfx.json
├── pilot_log.md                     (Pilot-Phase-Erkenntnisse)
└── README.md                        (Workflow-Auswahl-Guide)
```

Versionierung via Git-LFS für die JSONs und Style-References.

---

## 5. Stage 1 — 2D-Konzept (Flux/SDXL + Style-LoRA)

### 5.1 Style-LoRA-Training (einmalig, ~1 Tag)

Wir trainieren eine eigene **Style-LoRA** für den BomberBlast-Neon-Arcade-Stil (Orange/Cyan, Glow, oktagonale Formen). Damit ist Stil-Konsistenz über 200+ Assets garantiert.

**Trainings-Setup:**
- Tool: **Kohya_ss** (Standard für SDXL/Flux-LoRA-Training)
- Trainings-Set: 15-20 hochwertige eigene Konzept-Bilder (SDXL ohne LoRA + Inpainting + Photo-Touchups in Krita/Photoshop)
- Trainings-Zeit: 4-8h auf RTX 4080 (LoRA Rank 64)
- Output: `bomberblast_neon_v1.safetensors` (~150-300 MB)

**Style-Lock-Prompt (Template):**

```
Style-Block (Pflicht in jedem Asset-Konzept-Prompt):
<lora:bomberblast_neon_v1:0.85>,
neon arcade style, primary orange #FF6B35 and cyan #22D3EE accents,
octagonal shapes, sharp edges, glowing neon edges, stylized PBR,
two style variants (clean HD / neon), single object centered,
T-pose if humanoid character, 
plain white or transparent background,
3D render quality, octane-style lighting

Negative-Prompt (Pflicht):
realistic photo, blurry, watercolor, painterly,
multiple objects, busy background, text, watermark,
NSFW, deformed, extra limbs
```

### 5.2 IP-Adapter als Alternative

Wenn LoRA-Training nicht praktikabel ist: **IP-Adapter** mit dem Style-Reference-Set in den ComfyUI-Workflow einspeisen. Schwächer als LoRA, aber sofort startklar.

⚠️ **IP-Adapter funktioniert mit SDXL gut, mit Flux schlecht** — daher bei Flux-Path immer LoRA bevorzugen.

### 5.3 ControlNet für orthographische Views

Für Image-to-3D-Algorithmen sind **orthographische Single-Object-Views auf weißem BG** ideal. ControlNet mit `Canny` oder `Depth` aus einem groben Block-Sketch sichert die richtige Perspektive.

---

## 6. Stage 2 — Image-to-3D (TRELLIS 2 / SPAR3D / TripoSG)

### 6.1 Algorithmus-Wahl pro Asset-Typ

| Asset-Typ | Primär-Algorithmus | Backup | Grund |
|-----------|---------------------|--------|-------|
| Standard-Prop (Bombe, Power-Up, Kiste) | **SPAR3D** | TRELLIS 2 | <1s pro Asset, Punktwolke editierbar |
| Held / humanoides Modell | **TRELLIS 2** | TripoSG | Beste Topologie für animierbare Char |
| Boss / Hi-Detail-Hero | **TRELLIS 2** + Cloud-Polish | Rodin Gen-2.5 | OSS für Basis, Cloud für Cinematic-Polish |
| Tile / modulares Element | **TripoSG** | TRELLIS 2 | TripoSG handelt gleichförmige Geometrie gut |
| Tuch / dünne Geometrie (Flagge, Banner) | **TripoSF** | TRELLIS 2 | TripoSF speziell für Open-Surface |
| Schnelle Vorschau / Skizzen | **Stable Fast 3D** | InstantMesh | Sekunden pro Asset |

### 6.2 TRELLIS 2 — Default-Workflow

Workflow-Datei: `02_image_to_3d/trellis2_full_quality.json`

Eingabe: PNG 1024², transparenter BG.
Ausgabe: GLB mit Mesh (50-200k Tris vor Cleanup) + PBR-Texturen (BaseColor, Normal, MetalRough).
Dauer: ~30-60s auf RTX 4080 (16 GB VRAM).

### 6.3 Batch-Generation

Für Prop-Batches (z.B. 20 Power-Ups): `02_image_to_3d/batch_props.json` mit Queue-Node. Über Nacht laufen lassen.

---

## 7. Stage 3 — Blender-Cleanup

> **Wichtig — Decimate ≠ Retopo:** TRELLIS-2/SPAR3D liefern 50-200k Tris mit unsauberer
> Triangle-Soup-Topologie. Für **animierbare Charaktere** (Helden, Gegner, Bosse) reicht Decimate
> **nicht** — sie brauchen echte **Retopologie** (saubere Quad-Loops an Schulter/Hüfte/Knie/Ellbogen,
> sonst zerreißt das Deform beim Skinning). Der "5-10min/Asset"-Wert gilt nur für **statische Props**
> (Schritt-Liste unten). Charakter-Retopo ist Handarbeit (QuadRemesher/RetopoFlow, ~1-3h/Asset) bzw.
> wird über Cloud-Quad-Output (Rodin Gen-2.5 Quad-Mesh) abgekürzt.

Pflicht-Schritte pro Asset (Template: `F:\AI\Blender\bomberblast_unity_cleanup.blend`):

1. **Import GLB** (Standard-Importer).
2. **Decimate (Props) / Retopo (Charaktere)** (Decimate-Ratio bzw. Ziel-Quad-Count aus Budget-Tabelle, [§12](#12-asset-kategorien--budgets-neon-arcade)).
3. **UV-Repair:** Smart UV Project mit Margin 0.02 bei Überlappung.
4. **Origin** auf Boden-Mitte setzen (`Set Origin > Origin to Geometry` + manuell Z=0).
5. **Scale anwenden** (`Ctrl+A > Scale`) — 1 Blender-Unit = 1 Meter = 1 Unity-Unit.
6. **Normals** neu berechnen (`Mesh > Normals > Recalculate Outside`).
7. **Texturen prüfen** + neutrale Defaults bei fehlenden Maps (Normal #8080FF, Roughness 0.5, Metal 0).
8. **Export FBX:**
   - Pfad: `F:\AI\3d_output\bomberblast_unity\{kategorie}\{asset_id}.fbx`
   - Optionen: `Apply Scalings: FBX Units Scale`, `Forward: -Z`, `Up: Y`, `Embed Textures: ja`.

**Automatisierung:** `F:\AI\Blender\scripts\bomberblast_batch_cleanup.py` (Python) für **Prop-GLB-Batches** (Decimate-Path). ~30s pro Asset im Headless-Modus. Charaktere mit Retopo-Bedarf laufen **nicht** batch — Handarbeit pro Asset.

---

## 8. Stage 4 — Texturing + Materialien

### 8.1 Wann diesen Schritt brauchen?

- TRELLIS-2-Texturen sind teilweise nur Albedo + Normal — kein gutes MetalRough → Refine nötig.
- Style-Drift in PBR-Maps → vereinheitlichen.
- Material-Variation (Hero-Skin-Stufen, Charakter-Skins) → re-texturing eines Basis-Modells.

### 8.2 Option A — Adobe Substance 3D Sampler 4.4

- **Image-to-Material:** Foto/Konzept → vollständige PBR-Maps (BaseColor, Normal, Roughness, Metal, Height, AO).
- **Text-to-Texture (Beta in 4.4):** Prompt → tileable Material.
- **AI-Upscale:** Texturen von 512² → 2048² mit AI-Smoothing pro PBR-Channel.
- Lizenz: Adobe CC Sub (~25 €/Monat) — Pflicht für Texture-Artist-Workstation.

### 8.3 Option B — Unity AI Texture (in Unity 6.2 embedded)

- Texture-Generation direkt im Editor (Prompt → tileable PBR).
- Animation-Generation (Text-to-Animation + Kinetix Video-to-Animation).
- Lizenz: Unity-Subscription erforderlich.
- Vorteil: Kein Plugin-Setup, direkt in der Asset-Pipeline.

### 8.4 Option C — Material-LoRA in ComfyUI

Für ein App-spezifisches Material-Set (z.B. "Cyber-Stahl mit Neon-Ätzung") eigene LoRA trainieren. ControlNet-Tile-Mode für Tileable Output.

**Empfehlung:** Substance Sampler 4.4 als Production-Standard, Unity AI für Quick-Iteration im Editor, ComfyUI nur wenn Style-LoRA gepflegt wird.

---

## 9. Stage 5 — Rigging + Animation

### 9.1 Auto-Rigging — Tool-Wahl

| Asset-Typ | Tool | Grund |
|-----------|------|-------|
| Standard-Humanoid (Helden in T-Pose, humanoide Gegner) | **Mixamo** | Beste Animation-Library — nur bei Standard-Humanoid-Proportionen verlässlich |
| Humanoider Warden (Magma Revenant, Null Phantom, The Overseer) | **Mixamo** ODER **AccuRIG 2** | Auto-Rig greift bei aufrechter humanoider Silhouette |
| **Non-humanoider Warden (Frostwyrm, Granite Warden, Multi-Cell)** | **Hand-Rigging in Blender** (+ AccuRIG-Versuch als Startpunkt) | Auto-Rig/Mixamo scheitern an Vierbeiner/Multi-Cell-Topologie → Skelett + Weights von Hand, Animation per **Hand-Keyframing** (kein Mocap-Retarget) |

### 9.2 Animation-Quellen

| Quelle | Workflow | Output |
|--------|----------|--------|
| **Mixamo-Library** (kostenlos) | Online-Auswahl: Idle/Walk/Run/Death/Attack | FBX mit Animation-Clip pro Move |
| **Cascadeur** (Free Indie-Tier < $100k Rev) | AI-Auto-Posing aus Keyframes, Physics-Korrektur | FBX-Animation oder Direct-Import in Unity |
| **DeepMotion Animate 3D** | Eigenes Video → 3D-Animation (kein Mocap-Suit nötig) | FBX, retargetable auf jedes Rig |
| **RADiCAL** (Autodesk-acquired 04/2026) | Video → Motion, Cloud-Stream zu Unity möglich | FBX oder Live-Stream |

### 9.3 Helden-Animation-Set

Pro Held (5 Helden):
- Idle (Loop)
- Walk + Run (Loop)
- Bomb-Place (One-Shot, ~0.5s)
- Detonate (One-Shot, ~0.3s)
- Hit (One-Shot)
- Death (One-Shot)
- Victory-Pose (One-Shot)

> Die 5 Helden unterscheiden sich nur durch Stats/Trait + Skin-Farben (keine eigenen Skills/Ultimates) —
> sie teilen sich dasselbe Animation-Set (Material-/Farb-Variation pro Held).

Insgesamt: ~7 Animations, geteilt über 5 Helden = ~7 Basis-Clips + Skin-Varianten. Mixamo deckt alle ab.

### 9.4 Boss-Animations

Bosse brauchen größere Animation-Sets (Mehrkomponenten-Hitboxes, Phase-Wechsel). Empfehlung:
- **Humanoide Wardens** (Magma Revenant, Null Phantom, The Overseer): Standard-Loops (Idle, Attack) aus
  Mixamo retargeted, Cinematics (Reveal, Phase-2-Transition) per Hand in **Cascadeur**.
- **Non-humanoide Wardens** (Frostwyrm, Granite Warden): kein Mixamo-Retarget möglich — **alle** Clips
  (Idle, Attack, Enrage, Death) per **Hand-Keyframing** in Cascadeur/Blender auf das Hand-Rig.
- Das Zeitbudget der Pilot-Tabelle (Boss "2 Tage") deckt **nur Modeling + Texturing** —
  Hand-Rigging und Hand-Animation der non-humanoiden Bosse kommen separat obendrauf.

---

## 10. Stage 6 — Unity-Import

### 10.1 Pro-Asset-Checkliste

- [ ] FBX in `Assets/_Project/Art/Models/{Kategorie}/` ablegen.
- [ ] **Model Tab:** `Scale Factor = 1`, `Read/Write = false`, `Mesh Compression = High`.
- [ ] **Rig Tab** (humanoide Helden/Wardens): `Animation Type = Humanoid`, Avatar `Create From This Model`.
- [ ] **Animation Tab:** Mixamo-Clips als Sub-Assets, Loop für Idle prüfen, `Bake Into Pose: Root` bei Walk.
- [ ] **Materials Tab:** `Extract Materials` → `Assets/_Project/Art/Materials/`. Shader `URP/Lit` (oder `URP/Toon` falls Toon-Stil).
- [ ] **LOD-Group** als separates Prefab (3 LODs aus Budget-Tabelle).
- [ ] **Collider:** Box/Capsule manuell platzieren. **Kein Mesh-Collider** (Performance).
- [ ] **Layer:** Player / Enemy / Environment / Bomb / PowerUp (siehe ARCHITECTURE.md).
- [ ] **Prefab speichern** in `Assets/_Project/Prefabs/{Kategorie}/{asset_id}.prefab`.
- [ ] **Addressables-Group** zuweisen.

### 10.2 Addressables-Gruppen

```
BomberBlast.Unity Addressables:
├── BootstrapAssets         # Sofort (Logo, Splash, Default-Material)
├── Heroes                  # 5 Helden + Skins, lazy bei HeroSelection
├── Bombs_Common            # Standard-Bomben (immer geladen)
├── Bombs_Special           # Karten-Spezialbomben (lazy bei Deck-Equip)
├── PowerUps                # 12 PowerUp-Typen (immer geladen)
├── Tiles_World{1..10}      # Pro Sektor eine Gruppe (lazy bei Level-Start)
├── Bosses_Standard         # 4 Standard-Bosse (lazy bei Boss-Level)
├── Bosses_Final            # The Overseer (Archetyp FinalBoss) + Duo-Varianten (lazy)
│                           # (Mini-Bosse brauchen keine eigene Group — Prefab-Variant des Sektor-Warden-Assets)
├── Environment_World{1..10} # Props pro Sektor
├── UI3D                    # Hologramme, 3D-Buttons, Karten-Backs
└── Audio_Music + Audio_SFX # Stage 7 Output
```

### 10.3 Texture-Compression

- **Android:** ASTC 6×6 (BC7-Fallback bei sehr alten Geräten).
- **Windows:** BC7.
- **Pro Addressables-Group** als Texture-Importer-Preset persistieren.
- **Mip-Bias +1** für Mid-Tier-Android-Profil (Speicher-Cap).

---

## 11. Stage 7 — Audio (Musik + SFX + Voice)

### 11.1 Musik — Stable Audio 3 (Open-Weight)

Stability AI hat Stable Audio 3 am 20. Mai 2026 veröffentlicht:
- **Open-Weight** für Small/Medium-Modelle (lokal lauffähig).
- **Trainingsdaten lizenziert** (AudioSparx u.a.) — saubere Lizenzkette.
- **6-Minuten-Tracks** möglich (Vorgänger waren auf ~3 Minuten beschränkt).
- **Stability Community License** ≤ $1M Umsatz frei kommerziell.

**Asset-Plan (Musik):**
- 10 Sektor-Themes (jeweils Loop 1-2min)
- 1 Menu-Track
- 1 Boss-Track (Variante pro Boss-Typ, 5 Stück)
- 1 Victory-Stinger + 1 Defeat-Stinger
- 1 Mega-Combo-Stinger
- **Gesamt:** ~20 Tracks

**Workflow:** `04_audio/stable_audio_music.json` mit Prompt-Templates pro Sektor-Stil.

### 11.2 SFX — Stable Audio Open Small (SFX-Variante)

- Bomben-Detonationen (Standard + 13 Karten-Spezialtypen)
- Power-Up-Pickups (12 Typen)
- Player-Schritte (8 Material-Varianten)
- UI-Sounds (Button-Tap, Menu-Open, Achievement-Unlock)
- Boss-Roars (5 Typen)
- Gegner-Sounds (12 Enemy-Typen × 3 States = ~36 SFX)

**Insgesamt:** ~150 SFX. Generation in Batches á 20 SFX über Nacht.

### 11.3 Voice — DEFERRED (Original ist voice-los)

> Das produktive BomberBlast hat **keine** Voice (bewusstes "kein Geld"-Mandat). Im Remake bleibt Voice
> **deferred/optional**. Falls später eingeführt (bewusste Erweiterung), kämen in Frage:
- Announcer-Lines (6 Sprachen), Stinger-Vocals, optionale Boss-Roar-Samples
- Saubere Lizenzkette (Standard-Voices/Consent), Disclosure in Credits, Manual-QA-Pflicht

Bis dahin: Announcer/Feedback rein über SFX-Stinger (Cinematic-Bus), keine gesprochenen Lines.

### 11.4 Mastering

- Alle Tracks auf **−16 LUFS** (Mobile-Standard, EBU R128).
- Tool: **Adobe Audition** oder **iZotope Ozone** (lokal, kein KI nötig).
- Pro Track Pre-Master-Backup + Final-Master in `F:\AI\audio_output\bomberblast_unity\`.

---

## 12. Asset-Kategorien & Budgets (Neon-Arcade)

### 12.1 Polygon-Budgets (Mid-Tier-Android Ziel)

| Asset-Klasse | Anzahl | LOD0 | LOD1 | LOD2 | KI direkt? |
|--------------|-------:|-----:|-----:|-----:|------------|
| **Helden** (5 Charaktere) | 5 | 12 000 | 6 000 | 3 000 | ✅ + Mixamo |
| **Hero-Skins** (Coin-/Gem-Skins, Material-Variation) | ~20 | (Re-Tex) | — | — | ✅ Re-Texturing |
| **Bomben** (14 Typen) | 14 | 1 500 | 800 | 400 | ✅ Direkt |
| **Power-Ups** (12 Typen) | 12 | 1 000 | 500 | 250 | ✅ Direkt |
| **Tiles/Blocks** (10 Sektoren × 4 Typen) | 40 | 800 | 400 | 200 | ✅ Direkt, Tiling-Check |
| **Floor-Tiles** (10 Sektoren) | 10 | 400 | 200 | 100 | ✅ Direkt |
| **Karten-FX-Meshes** (10 Spezial-Karten) | 10 | 1 500 | — | — | ✅ Direkt |
| **Standard-Wardens** (Granite Warden, Frostwyrm, Magma Revenant, Null Phantom) | 4 | 18 000 | 9 000 | 4 500 | ⚠️ humanoide (Magma Revenant/Null Phantom) + Mixamo; non-humanoide (Granite Warden/Frostwyrm) Hand-Rig |
| **The Overseer (FinalBoss) + Duo-Varianten** | 3 | 25 000 | 12 000 | 6 000 | ⚠️ TRELLIS-Basis + Cloud-Polish |
| **Mini-Bosse** (L7/L17/.../L97) | 0 (Reskin) | — | — | — | ♻️ kein eigenes Modell — reskinter Sektor-Warden (50 % HP/Punkte) |
| **Environment-Props** (Crates, Pipes, Trash, Holo-Displays) | ~50 | 600 | 300 | 150 | ✅ Direkt |
| **UI-3D-Hologramme** | ~20 | 500 | — | — | ✅ Direkt |

**Total:** ~210 Modelle + ~30 Re-Texture-Varianten = **~240 Asset-Slots**. Die 9 Mini-Bosse
(L7/L17/.../L97) sind **nicht** mitgezählt — sie sind reskinte Sektor-Wardens (50 % HP/Punkte) und
brauchen kein eigenes Modell, nur eine Material-/Skalierungs-Variante des jeweiligen Sektor-Warden-Assets.

### 12.2 Texture-Auflösungen

| Klasse | Albedo | Normal | MRA | Notes |
|--------|-------:|-------:|----:|-------|
| Helden / Bosse | 2048² | 2048² | 2048² | Mip-Bias 0 |
| Standard-Gegner / Mini-Bosse | 1024² | 1024² | 1024² | Mip-Bias +1 |
| Bomben / Power-Ups | 512² | 512² | 512² | Mip-Bias +1 |
| Blocks / Tiles | 1024² (atlassed pro Sektor) | 1024² | 1024² | Texture-Atlas |
| Props | 512² | 512² | 512² | Mip-Bias +2 |

### 12.3 Audio-Budgets

| Klasse | Sample-Rate | Bit-Tiefe | Komprimierung Unity | Gesamtgröße-Ziel |
|--------|-------------|-----------|----------------------|-------------------|
| Musik (Sektor-Themes, 2min) | 44.1 kHz | 16-bit | Vorbis Quality 0.5 | ~1.5 MB/Track |
| Musik (Stinger, 5s) | 44.1 kHz | 16-bit | Vorbis Quality 0.7 | ~100 KB |
| SFX | 44.1 kHz | 16-bit | ADPCM (Loop-fähig) | <50 KB |
| Voice-Lines | 44.1 kHz | 16-bit | Vorbis Quality 0.6 | ~80 KB/sec |

**Total-Audio-Budget:** ~80-120 MB (alle Sprachen, kompressed). Zusammen mit ~210 3D-Modellen
(+ LODs/Texturen) sprengt das den 200-MB-Base-APK-Rahmen — der Gesamt-Build passt nur mit
**Play Asset Delivery** (On-Demand-/Fast-Follow-Asset-Packs für Sektor-Tile-Sets, Wardens, Audio)
unter ~250 MB. Zur Einordnung: das 2D-Original ist bereits ~95 MB. Bootstrap-Assets (Logo,
Default-Material, Sektor-1) bleiben im Base-AAB, alles andere kommt per Asset-Pack/Addressables nach.

---

## 13. Stil-Konsistenz (Neon-Arcade)

### 13.1 Brand-Identität (aus PLAN.md / Original-`AppPalette.axaml`)

| Aspekt | Wert |
|--------|------|
| Markenfarben | Neon-Orange `#FF6B35` (Primär) + Cyan `#22D3EE` + Gold-Trail `#FFDD33` |
| Tonalität | Energetisch, Arcade, "Game Juice" — Nostalgie an SNES-Bomberman |
| Visual-Sprache | Oktagonale Formen, scharfe Kanten, Neon-Glow; zwei Styles (Classic HD + Neon) in 3D |
| Setting | 10 thematische Sektoren (Neon-Arcade) + neue Sektor-/Neo-Grid-Story-Beats |

### 13.2 Style-Lock (zentrales Prompt-Template)

Der `bomberblast_neon_v1.safetensors`-LoRA hält den Stil. Zusätzlich Pflicht-Style-Block (siehe §5.1).

### 13.3 Konsistenz-Check pro Batch

Vor Unity-Import: 5-Asset-Vergleich in Blender-AssetReview-Szene (gleiche Lighting-Rig, gleiche Kamera). Bei 1+ Asset Stil-Drift → Style-LoRA erweitern oder Regenerieren.

---

## 14. EU-Compliance & Lizenz-Recherche (Stand 2026-05)

### 14.1 Hunyuan3D — warum nicht?

Tencents Hunyuan3D-2 / 2.1 / 2.5 ist technisch top, aber die Lizenz definiert in den Terms eine `Territory`-Klausel, die **EU, UK und Südkorea explizit ausschließt** (siehe [Hunyuan3D-2 LICENSE](https://github.com/Tencent-Hunyuan/Hunyuan3D-2/blob/main/LICENSE), [Issue #94 für 2.1](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1/issues/94)). Für eine deutsche Game-Studio-Produktion ist das ein Show-Stopper ohne schriftliche Tencent-Sonderfreigabe. Wir bauen die Pipeline **bewusst Hunyuan-frei**.

### 14.2 EU-konformer OSS-Stack

| Modell | Lizenz | Notiz |
|--------|--------|-------|
| TRELLIS 2 | MIT | Microsoft, keine geographische Einschränkung |
| SPAR3D, SF3D, Stable Audio 3 | Stability Community License | Kommerziell OK bis $1M Umsatz/Jahr |
| TripoSG, TripoSF | OSS (VAST) | Foundation-Modelle, kommerziell OK |
| InstantMesh | Apache-2.0 | Kommerziell OK |
| Mixamo | Adobe-Standard | Kommerziell OK |
| Cascadeur | Free Indie < $100k Rev | Kommerziell OK |

### 14.3 EU AI Act — Compliance-Status

Der EU AI Act wird am **2. August 2026 voll wirksam**. Game-Apps fallen in der Regel in **"minimal risk"** — wir sind nicht GPAI-Provider, sondern nur **Deployer**. Das heißt:
- Keine GPAI-Pflichten für uns.
- **Transparenz-Pflicht**: Spieler müssen wissen, wenn signifikante Inhalte AI-generiert sind. → In der App-Beschreibung (Play Store) + Credits-Sektion erwähnen.
- **Marketing-Inhalte** (Trailer, Screenshots): Wenn AI-generiert → kennzeichnen.
- **Kein Deepfake-Risiko** in unserem Setting (keine realen Personen).

### 14.4 Lizenz-Archiv

```
F:\AI\Licenses\bomberblast_unity\
├── 2026-05-26_meshy_pro_commercial.pdf
├── 2026-05-26_rodin_gen2_free_commercial.pdf
├── 2026-05-26_substance_3d_sub.pdf
├── 2026-05-26_elevenlabs_pro.pdf
├── 2026-05-26_unity_ai_sub.pdf
└── 2026-05-26_cascadeur_indie.pdf
```

Pro Asset-Metadata-JSON ein Eintrag `"license_source": "Rodin Gen-2.5 Free Tier"` + `"license_archive": "2026-05-26_rodin_gen2_free_commercial.pdf"`. Audit-fähig.

### 14.5 Trainingsdaten-Risiko

- **Stable Audio 3**: lizenzierte Trainingsdaten (AudioSparx u.a.) → **risikolos**.
- **Suno / Udio**: laufende Trainingsdaten-Lawsuits (Stand April 2026) → **vermieden**.
- **TRELLIS 2**: Microsoft-Research, akademische Datensätze + öffentlich verfügbare 3D-Modelle → **niedriges Risiko**, bisher keine bekannten Klagen.
- **SDXL/Flux**: Pre-trained-Layer ist Common-Knowledge. Eigenes LoRA-Fine-Tuning auf eigenes Material → **risikolos**.

---

## 15. Pilot-Plan (5 Assets vor Skalierung)

| # | Pilot-Asset | Kategorie | Pipeline-Test | Erfolgs-Kriterium |
|---|-------------|-----------|---------------|-------------------|
| 1 | Held "Default" (Pilot Echo) | Held | SDXL+LoRA → TRELLIS 2 → Mixamo-Rig | Animiert in Unity, < 12k Tris LOD0, Neon-Style-LoRA hält |
| 2 | Bombe "Standard" | Bomb | SPAR3D → Blender-Cleanup → Unity | < 1.5k Tris, Emissive-Glow funktioniert |
| 3 | Block "Destructible (Sektor 1)" | Tile | TripoSG → Tile-Check 4× nebeneinander | Naht-frei, < 800 Tris |
| 4 | Warden "Granite Warden" (Archetyp StoneGolem, non-humanoid) | Boss | TRELLIS 2 → Hand-Retopo → Hand-Rig + Hand-Keyframing (Cascadeur) | Phase-1 + Enrage (Material-Swap), Multi-Cell-Hitbox |
| 5 | PowerUp "BombUp" + "Fire" | PowerUp | SDXL → SPAR3D → URP + Glow | URP-Glow funktioniert, < 1k Tris |
| **Audio-Pilot** | Sektor-1-Theme (2min) | Music | Stable Audio 3 + Mastering | LUFS −16 ±1, Loop sauber |

> Kein Voice-Pilot — Voice ist deferred (Original ist voice-los). Stattdessen ein SFX-Pilot (Bomben-Explosion
> + Combo-Stinger) zur Validierung der Audio-Bus-/Spatial-Pipeline.

**Zeitplan:** ~5 Arbeitstage Pilot — Held 1 Tag, Bomb+Block 1 Tag, Boss 2 Tage (**nur Modeling +
Texturing**; Hand-Retopo, Hand-Rigging und Hand-Keyframing des non-humanoiden Granite Warden (Archetyp StoneGolem) kommen
mit ~2-3 Tagen separat obendrauf), PowerUps 0.5 Tage, Audio 0.5 Tage.

**Output:** Lessons-Learned in `F:\AI\ComfyUI_workflows\bomberblast_unity\pilot_log.md` mit:
- Tatsächliche Generations-Zeit pro Asset
- Erfolgsquote (wie oft musste regeneriert werden)
- Polygon-Counts vor/nach Cleanup
- Texture-Qualitäts-Score (subjektiv 1-5)
- Probleme + Workarounds

**Skalierungs-Freigabe:** 5/5 Pilots OK → Phase 2 Skalierung auf alle ~240 Assets. Bei 4/5 → Pipeline iterieren, dann erneut. Bei < 4/5 → Stack neu bewerten (Cloud-Anteil erhöhen).

---

## 16. Output-Ablage + Versionierung

```
F:\AI\
├── ComfyUI_workflows\
│   └── bomberblast_unity\           (Workflows, Git-LFS)
├── 3d_output\
│   └── bomberblast_unity\
│       ├── concept_2d\              (Stage 1 Output)
│       ├── raw_glb\                 (Stage 2 Output, pre-Cleanup)
│       ├── final_fbx\               (Stage 6 Input)
│       └── metadata\                (JSON pro Asset: Lizenz, Prompts, Versionen)
├── audio_output\
│   └── bomberblast_unity\
│       ├── music\
│       ├── sfx\
│       └── voice\
├── animation_output\
│   └── bomberblast_unity\
│       ├── mixamo_fbx\
│       ├── cascadeur_export\
│       └── deepmotion_export\
├── Licenses\
│   └── bomberblast_unity\           (PDF-Archiv aller kommerziellen Lizenzen)
└── Blender\
    ├── scripts\
    │   └── bomberblast_batch_cleanup.py
    └── bomberblast_unity_cleanup.blend (Template)
```

**Asset-Metadata-JSON** (Pflicht pro Asset):

```json
{
  "asset_id": "hero_default_v1",
  "category": "player_hero",
  "stage_1_concept": {
    "model": "sdxl_1.0",
    "lora": "bomberblast_neon_v1@0.85",
    "prompt": "...",
    "seed": 123456,
    "output_png": "concept_2d/hero_default_v1.png"
  },
  "stage_2_3d": {
    "tool": "trellis_2",
    "version": "2.0",
    "input_png": "concept_2d/hero_default_v1.png",
    "raw_glb": "raw_glb/hero_default_v1.glb",
    "duration_seconds": 47
  },
  "stage_5_rig": {
    "tool": "mixamo",
    "skeleton": "humanoid_standard",
    "animations": ["idle", "walk", "run", "bomb_place", "death"]
  },
  "license_source": "TRELLIS 2 (MIT) + Mixamo (Adobe Standard)",
  "license_archive": null,
  "compliance_status": "EU-conformant"
}
```

---

## 17. Risiken & Mitigation

| Risiko | Wahrscheinlichkeit | Auswirkung | Mitigation |
|--------|-------------------|------------|------------|
| TRELLIS-2-Qualität reicht nicht für Helden-Detailgrad | Mittel | Hoch | Cloud-Fallback Rodin Gen-2.5 für die 5 Helden (Free Tier, ~10$ Credits gesamt) |
| Stil-Drift über > 50 Assets | Mittel | Mittel | Style-LoRA Pflicht ab Pilot-Erfolg, festes Prompt-Template versioniert |
| Mixamo versagt bei nicht-standard-humanoidem Charakter | Hoch | Mittel | Tripo Auto-Rig oder AccuRIG 2 als Fallback |
| ASTC zu groß auf Mid-Tier-Android | Niedrig | Mittel | Texture-Atlas-Pflicht für Tiles/Props, Mip-Bias +1 pro Klasse |
| EU AI Act Transparenz-Pflicht missachten | Niedrig | Hoch | Play-Store-Description + Credits enthalten KI-Hinweis; Marketing-Material gekennzeichnet |
| Tencent klagt rückwirkend gegen ein verkauftes Hunyuan-Asset | Niedrig | Hoch | **Hunyuan komplett vermeiden**, Asset-Metadata dokumentiert Tool-Quelle pro Asset |
| Suno/Udio-Lawsuits eskalieren | Hoch | — | **Suno/Udio vermieden**, Audio nur Stable Audio 3 + ElevenLabs |
| Polygon-Inflation (>200k Tris von KI) | Hoch (Default) | Niedrig | Blender-Decimate Pflicht, kein Asset ohne Cleanup ins Unity |
| Trainingsdaten-Bias (Charaktere sehen alle gleich aus) | Niedrig | Mittel | Style-Reference-Set pro Held diversifizieren, Pro-Held einzelne Sub-LoRAs falls nötig |
| Tile-Naht-Probleme (Repeating Patterns) | Mittel | Mittel | Pre-Gen Symmetrie-Prompt, Post-Gen Naht-Heal in Blender |
| Audio-LUFS-Inkonsistenz zwischen Sektoren | Mittel | Mittel | Master-Pass mit iZotope Ozone als Pflicht-Schritt |
| GPU-Lieferengpass (RTX 50xx-Knappheit Mai 2026) | Mittel | Mittel | Cloud-Workstation (RunPod, vast.ai) als Backup-Plan |

---

## 18. Verweise

### Projekt-interne Docs

| Bereich | Datei |
|---------|-------|
| Master-Plan | [PLAN.md](PLAN.md) |
| Conventions | [CLAUDE.md](CLAUDE.md) — falls vorhanden |
| Tech-Architektur (URP, LOD, Addressables) | [ARCHITECTURE.md](ARCHITECTURE.md) — falls vorhanden |

### Tool-URLs (verifiziert Mai 2026)

- ComfyUI: `https://github.com/comfyanonymous/ComfyUI`
- ComfyUI-3D-Pack: `https://github.com/MrForExample/ComfyUI-3D-Pack`
- TRELLIS 2 (Microsoft): `https://github.com/microsoft/TRELLIS.2`
- SPAR3D (Stability): `https://github.com/Stability-AI/stable-point-aware-3d`
- Stable Fast 3D: `https://huggingface.co/stabilityai/stable-fast-3d`
- TripoSG (VAST): `https://github.com/VAST-AI-Research/TripoSG`
- TripoSF (VAST): `https://github.com/VAST-AI-Research/TripoSF`
- Stable Audio 3: `https://stableaudio.com/`
- Meshy: `https://www.meshy.ai/`
- Rodin (Hyper3D): `https://hyper3d.io/`
- Tripo3D Studio: `https://www.tripo3d.ai/`
- Mixamo: `https://www.mixamo.com/`
- Cascadeur: `https://cascadeur.com/`
- AccuRIG 2 (Reallusion): in Reallusion Character Creator
- DeepMotion Animate 3D: `https://www.deepmotion.com/`
- RADiCAL (Autodesk): `https://ariusai.com/products/radical/`
- Adobe Substance 3D Sampler: Adobe Creative Cloud
- Unity AI (Unity 6.2): `https://unity.com/products/muse`
- ElevenLabs: `https://elevenlabs.io/`

### EU-Compliance

- EU AI Act offiziell: `https://digital-strategy.ec.europa.eu/en/policies/regulatory-framework-ai`
- Hunyuan3D EU-Restriction: [Issue #94](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1/issues/94)

### Lokale Ablage

- 3D-Workflows: `F:\AI\ComfyUI_workflows\bomberblast_unity\`
- Asset-Output: `F:\AI\3d_output\bomberblast_unity\`
- Audio-Output: `F:\AI\audio_output\bomberblast_unity\`
- Animation-Output: `F:\AI\animation_output\bomberblast_unity\`
- Pilot-Log: `F:\AI\ComfyUI_workflows\bomberblast_unity\pilot_log.md`
- Lizenz-Archiv: `F:\AI\Licenses\bomberblast_unity\`
- Blender-Template: `F:\AI\Blender\bomberblast_unity_cleanup.blend`
- Batch-Scripts: `F:\AI\Blender\scripts\bomberblast_batch_cleanup.py`
