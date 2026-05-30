# HandwerkerImperium Unity — KI-Asset-Pipeline (3D + Audio + Animation)

> **Status:** Produktions-Plan (Stand 2026-05-26, recherchiert)
> **Ziel:** Skalierbarer, EU-konformer und kommerziell sauberer Workflow für 3D-Assets, Animationen, Texturen und Audio mit KI-Tools — primär lokal (ComfyUI + EU-konforme OSS-Modelle), Cloud-Services als Production-Standard wo Qualität es rechtfertigt.
> **Geltungsbereich:** Werkstätten, Arbeiter (inkl. sichtbares Equipment + Mood-States), Werkzeuge, Master-Tools, Crafting-Produkte, Buildings, Mini-Game-Props, Gilden-Hall-Gebäude, Mega-Projekte, Gilden-Bosse, City-Tiles, Cosmetics/Event-Visuals, Prestige-Cinematic-Assets, Animationen, Texturen, Game-Audio.
> **Grundsatz (unverhandelbar):** Die Unity-Version ist **dasselbe Spiel** wie das produktive Avalonia-Original (gleiche Mechaniken, Formeln, Balancing-Werte — Quelle: [DESIGN.md](DESIGN.md) + [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md)), nur in **3D statt 2D-SkiaSharp**. Asset-mengen sind daher **vom realen Spielinhalt abgeleitet, nicht frei geschätzt**. Jedes sichtbare 2D-Element des Originals braucht ein 3D-Pendant.
> **Nicht im Scope:** redaktionelle Texte, Story-Schreiben.

> **EU-Compliance-Warnung:** Hunyuan3D (Tencent) ist in der EU/UK/Südkorea per Lizenz **explizit ausgeschlossen** und erfordert schriftliche Tencent-Sonderfreigabe. Wir bauen bewusst eine **EU-konforme Pipeline** ohne Hunyuan als Default. Details: [§14](#14-eu-compliance--lizenz-recherche-stand-2026-05).

> **2D-zu-3D-Migration:** Das Original rendert alles in 2D (SkiaSharp + 224 Bitmap-/Vektor-Icons aus `Assets/visuals/`). Für die Unity-Version wird **jedes sichtbare 2D-Element zu einem 3D-Pendant** überführt: Werkstätten und Buildings werden begehbare 3D-Strukturen, Worker-Sprites werden riggbare 3D-Charaktere, Crafting-Produkt-Icons werden 3D-Props (im Lager/Showroom sichtbar), Master-Tool-Icons werden Glow-Artefakte, Gilden-Hall und Bosse werden 3D-Szenen. UI-Icons (Buttons, Tab-Symbole, Währungs-Glyphen) bleiben **2D** und werden als TextMeshPro-SpriteAsset aus dem Avalonia-Icon-Bestand (224 Glyphen) übernommen (siehe [§12.3](#123-ui-icons-2d-bleiben-2d)).

---

## Inhaltsverzeichnis

1. [Strategische Entscheidung](#1-strategische-entscheidung)
2. [Pipeline-Überblick](#2-pipeline-überblick)
3. [Tool-Stack (recherchiert + EU-validiert)](#3-tool-stack-recherchiert--eu-validiert)
4. [Hardware & Setup](#4-hardware--setup)
5. [Stage 1 — 2D-Konzept (Flux/SDXL + Style-LoRA)](#5-stage-1--2d-konzept-fluxsdxl--style-lora)
6. [Stage 2 — Image-to-3D (TRELLIS 2 / SPAR3D / TripoSG)](#6-stage-2--image-to-3d-trellis-2--spar3d--triposg)
7. [Stage 3 — Blender-Cleanup + Modulare Werkstätten](#7-stage-3--blender-cleanup--modulare-werkstätten)
8. [Stage 4 — Texturing + Materialien + Decals](#8-stage-4--texturing--materialien--decals)
9. [Stage 5 — Rigging + Animation (Arbeiter + Mood-States)](#9-stage-5--rigging--animation-arbeiter--mood-states)
10. [Stage 6 — Unity-Import](#10-stage-6--unity-import)
11. [Stage 7 — Audio (Werkstatt-Sounds + Musik + Meister-Hans-Voice)](#11-stage-7--audio-werkstatt-sounds--musik--meister-hans-voice)
12. [Asset-Kategorien & Budgets (Toon-Werkstatt)](#12-asset-kategorien--budgets-toon-werkstatt)
13. [Stil-Konsistenz (Stylized Toon-Werkstatt)](#13-stil-konsistenz-stylized-toon-werkstatt)
14. [EU-Compliance & Lizenz-Recherche (Stand 2026-05)](#14-eu-compliance--lizenz-recherche-stand-2026-05)
15. [Pilot-Plan (6 Assets vor Skalierung)](#15-pilot-plan-6-assets-vor-skalierung)
16. [Output-Ablage + Versionierung](#16-output-ablage--versionierung)
17. [Risiken & Mitigation](#17-risiken--mitigation)
18. [Verweise](#18-verweise)

---

## 1. Strategische Entscheidung

Für ein **stylisiertes Idle-Builder-Game mit Cartoon-Werkstatt-Ästhetik** ist KI-3D-Generierung 2026 nicht nur reif, sondern **wirtschaftlich notwendig**. Die schiere Menge an Modellen, die der reale Spielinhalt erfordert (10 Werkstätten × 5 Upgrade-Stufen + 33 Crafting-Produkte über 4 Tiers + 20 Arbeiter-Basis-Modelle mit ~120 Skin-Varianten + 7 Buildings + 12 Master-Tools + 13 Mini-Game-Prop-Sets + 10 Gilden-Hall-Gebäude + 6 Gilden-Bosse + 2 Mega-Projekte + 80 City-Tiles = mehrere hundert Asset-Slots, siehe [§12](#12-asset-kategorien--budgets-toon-werkstatt)) ist mit klassischem Artist-Workflow nicht zu bewältigen. **Alle Stückzahlen sind aus dem produktiven Avalonia-Original abgeleitet** (Quelle: [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) / [DESIGN.md](DESIGN.md)), nicht frei geschätzt.

**Kern-Entscheidungen (verbindlich):**

- **EU-konformer OSS-Stack** als Default — kein Hunyuan3D ohne Tencent-Sonderfreigabe.
- **Lokale Pipeline primär**: ComfyUI 0.3.x + ComfyUI-3D-Pack mit **TRELLIS 2** (Microsoft, MIT) als Geometrie-Hauptmodell, **SPAR3D** (Stability) für schnelle Props.
- **Cloud-Services für Production**: Meshy 6 oder Rodin Gen-2.5 für Prestige-Cinematic-Hero-Modelle, Tripo 3.0 für komplexe Werkstatt-Architektur mit Auto-Rigging.
- **Modulare Werkstätten:** Basis-Modell + austauschbare Material-Decals/Anbauten für Upgrade-Stufen Lv1-5 — spart ~80% Generations-Zeit gegenüber 5× separat generierten Modellen. (WorkshopMaxLevel = 1000 im Code; die 5 sichtbaren Decal-Stufen sind eine visuelle Gruppierung, keine mechanische Abweichung.)
- **Re-Texturing-Workflow:** 30 Workshop-Specialization-Skins (3 Spezialisierungen × 10 Werkstätten, siehe DESIGN.md) über Decal-Material-Layer, nicht über neue Modelle.
- **Audio**: Stable Audio 3 (Open-Weight, lizenzierte Trainingsdaten). Suno wegen ungeklärter Trainingsdaten-Lawsuits **gemieden**.
- **Animation**: Cascadeur (AI-AutoPosing) + Mixamo (Standard-Worker-Loops) + DeepMotion (für individuelle Mood-Animation aus eigenem Video).
- **Bestehende 2D-ComfyUI-Pipeline** für Avalonia-Icons (`F:\AI\ComfyUI_workflows\handwerkerimperium\`) wird um 3D-Schritte erweitert — gemeinsamer Style-LoRA-Pool für 2D-UI und 3D-Welt.
- **Polygon-Budget Mobile** (Mid-Tier-Android, gleiches Ziel wie Avalonia: Huawei P30, Pixel 4a, Galaxy A52): siehe [§12](#12-asset-kategorien--budgets-toon-werkstatt).

---

## 2. Pipeline-Überblick

```
┌────────────────────────────────────────────────────────────────────┐
│ Stage 1: 2D-Konzept                                                │
│  Flux.1-dev / SDXL + Style-LoRA + IP-Adapter (ComfyUI)             │
│  → PNG 1024² / 2048², transparenter BG empfohlen                   │
│  Re-Use des bestehenden Toon-LoRA aus 2D-Icon-Pipeline             │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 2: Image-to-3D                                               │
│  Primär: TRELLIS 2 (Microsoft, MIT) — beste Toon-Topologie         │
│  Backup: SPAR3D (Stability) — schnelle Tools/Items (<1s)           │
│  Werkstätten-Architektur: TRELLIS 2 (komplex) + Modul-Setup        │
│  Cloud-Fallback: Rodin Gen-2.5 (Mega-Projekte, Prestige-Hero)      │
│  → GLB mit PBR-Texturen                                            │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 3: Blender-Cleanup + Modular-Setup (5-15min/Asset)           │
│  Decimate, UV-Repair, Werkstatt-Zerlegung in Module                │
│  Origin/Scale, Normals, FBX-Export                                 │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 4: Texturing + Material-Decals                               │
│  Basis-Material via Substance 3D Sampler 4.4                       │
│  5 Upgrade-Decals pro Werkstatt (Stickers, Schilder, Glow-Maps)    │
│  Worker-Mood-States via Material-Slot-Swap (Gesichtstextur)        │
│  Worker-Affinitäts-Props via Texture-Color-Variation               │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 5: Rigging + Animation                                       │
│  Worker-Rig: Mixamo (Standard-Humanoid)                            │
│  Animation-Loops: Idle, Working, Hammering, Sawing, Walking        │
│  Mood-Animations: Cascadeur (Hand-Setup)                           │
│  Workshop-Anims: kein Rig, statisch + Particle-FX in Unity         │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 6: Unity-Import (Unity 6000.4.8f1 + URP 17.0.4)              │
│  Addressables-Gruppe, LOD-Group, URP/Lit oder URP/Toon-Shader      │
│  Modulare Workshops als Parent-Empty + Child-Modules               │
└────────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌──────────────────────────┐    ┌────────────────────────────┐
│ Stage 7: Audio           │    │ Final: AssetReview Scene   │
│ Stable Audio 3 (Musik)   │    │ Cartoon-Lighting-Test      │
│ Stable Audio 3 (SFX)     │    │ Mobile-Performance-Profile │
│ ElevenLabs (Meister Hans │    │ Build-Smoke (Android-AAB)  │
│ Voice, 6 Sprachen)       │    │                            │
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
| **InstantMesh** | 1.0 | Apache-2.0 | Multi-View → Mesh (Backup) | github.com/TencentARC/InstantMesh |
| **Stable Audio 3** | 3.0 (Mai 2026) | Stability Community ≤ $1M; Open-Weight Small/Medium | Musik + SFX (lizenzierte Trainingsdaten!) | stableaudio.com |
| **Blender** | 4.3+ | GPL | Cleanup, Decimation, Modul-Setup, Export | blender.org |

> **NICHT genutzt (EU-Lizenz-Ausschluss):**
> - **Hunyuan3D-2 / 2.5** (Tencent) — Lizenz schließt EU/UK/Korea per Definition `Territory` aus. Nur mit schriftlicher Sonderfreigabe. Source: [Hunyuan3D-2 LICENSE](https://github.com/Tencent-Hunyuan/Hunyuan3D-2/blob/main/LICENSE).
> - **HunyuanWorld-1.0** (Tencent) — gleiche Lizenz-Restriktion.

### 3.2 Cloud (Production, mit kommerzieller Lizenz)

| Service | Version (Mai 2026) | Preis | Lizenz | Stärke |
|---------|---------------------|-------|--------|--------|
| **Meshy** | 6 (Jan 2026) | $20-$60/Mo (Pro+) | Pro-Tier: Volle Commercial Rights | Schnelle Iteration, Unity-Plugin, Blender-Plugin |
| **Rodin** (Hyper3D) | Gen-2.5 | $0.40-$1.50/Asset, **Free Tier mit Commercial Rights** | Alle Tiers kommerziell | Beste PBR-Texturen, Quad-Mesh-Output |
| **Tripo3D** (Studio) | 3.0 | Tier-Pricing (Saas) | Pro-Tier kommerziell | Komplett-Pipeline mit Auto-Rigging integriert |

> **Empfehlung HWI-spezifisch:** Rodin Gen-2.5 für die 5 Prestige-Cinematic-Hero-Assets (Free Tier reicht für 5-10 Assets/Monat). Meshy 6 für komplexe Werkstatt-Architektur falls TRELLIS-2-Topologie für Modul-Zerlegung nicht reicht. Tripo 3.0 für Worker-Auto-Rigging falls Mixamo-Proportionen scheitern.

### 3.3 Texturing/Material

| Tool | Version | Lizenz | Rolle |
|------|---------|--------|-------|
| **Adobe Substance 3D Sampler** | 4.4 | Adobe Creative Cloud (CC-Sub) | Image-to-Material, Text-to-Texture (Beta), Upscale, **Decal-Generation für Werkstatt-Upgrade-Stufen** |
| **Adobe Substance 3D Painter** | aktuell | Adobe CC | Hand-Polish für Hero-Workshops, Mega-Projekte |
| **Unity AI Texture** (in Unity 6.2) | 6.2 (Aug 2025) | Unity-Sub erforderlich | PBR-Generation aus Text/Image direkt im Editor (für Quick-Iteration) |

### 3.4 Animation + Rigging

| Tool | Lizenz | Stärke | Hinweis |
|------|--------|--------|---------|
| **Mixamo** (Adobe) | Kostenlos, kommerziell OK | Standard-Humanoid-Rigging + Animations-Bibliothek | **Hauptlösung für die 20 Worker-Basis-Modelle** (10 Tiers × m/w, siehe DESIGN.md § 5) |
| **Tripo Auto-Rigging** | Tripo-Sub | Universal-Rig (humanoid + non-humanoid) | Backup falls Mixamo bei stylisierten Worker-Proportionen versagt |
| **Reallusion AccuRIG 2** | Free (mit RL-Acc) | Auto-Rig humanoid + non-humanoid, AI Body-Detection | Solider Mixamo-Konkurrent, gute Toon-Proportions-Unterstützung |
| **Cascadeur** (Nekki) | Free für Indie < $100k Rev | AI-AutoPosing, Mood-States-Animation, Mixamo-Skelett-Kompatibel | Best-in-Class für individuelle Mood/Working-Animations |
| **DeepMotion Animate 3D** | Saas, Tier-Pricing | Video-to-3D-Animation, Retargeting | Eigenes Video als Quelle = saubere Lizenz |

### 3.5 Audio

| Tool | Version | Lizenz | Rolle | Risiko |
|------|---------|--------|-------|--------|
| **Stable Audio 3** (Stability) | 3.0 (Mai 2026) | Stability Community ≤ $1M; Open-Weight Small/Medium | Musik (bis 6min), SFX | Niedrig — **lizenzierte Trainingsdaten** |
| **ElevenLabs Music + Voice** | aktuell | Pro-Sub kommerziell | Stinger, Meister-Hans-Voice in 6 Sprachen, kurze Tracks | Niedrig |
| **AIVA** | aktuell | Pro-Sub | Cinematic-Scoring (Prestige-Cinematic-Tracks) | Niedrig |
| **Suno v4/v5** | — | Pro-Sub, **aber Trainingsdaten-Lawsuits laufend** | (vermieden) | **Hoch** — Output-Rights unklar |
| **Udio** | — | Pro-Sub, **gleiches Risiko wie Suno** | (vermieden) | **Hoch** |

---

## 4. Hardware & Setup

### 4.1 Empfohlene Workstation (Stand Mai 2026)

| Komponente | Mindest | Empfohlen |
|-----------|---------|-----------|
| GPU | RTX 3090 (24 GB) | RTX 4090 / 5090 (24-32 GB) — TRELLIS 2 + Texture-Refine wollen 16 GB+ |
| RAM | 32 GB | 64 GB (Blender bei modularen Werkstätten mit 4-6 Sub-Meshes) |
| CPU | 8 Cores | 16 Cores (Blender-Batch-Cleanup über Nacht) |
| Disk | 1 TB NVMe | 2 TB NVMe (Modelle + Workspace + 80 City-Tiles Atlas-Cache) |

### 4.2 ComfyUI-3D-Pack Installation (Windows)

Verifizierte Anforderungen (Stand 5/Jun/2025 lt. README):

- **Python:** 3.12
- **CUDA:** 12.4
- **PyTorch:** 2.5.1+cu124
- **Visual Studio Build Tools** (Windows) für native Module
- **VRAM:** 16 GB empfohlen

Installation:

```powershell
cd F:\ComfyUI\custom_nodes\

git clone https://github.com/MrForExample/ComfyUI-3D-Pack
cd ComfyUI-3D-Pack
python install.py
# install.py lädt Pre-Built-Wheels (Win10/11 + Python 3.12 + CU124 + PyTorch 2.5.1)
# oder triggert automatischen Build (braucht VS Build Tools)
```

Alternative: **ComfyUI-Manager** (One-Click).

### 4.3 Modell-Downloads

| Modell | Größe | Ablage | EU-Lizenz OK |
|--------|-------|--------|---------------|
| TRELLIS 2 (image-large) | ~5 GB | `ComfyUI/models/TRELLIS/` | Ja — MIT |
| SPAR3D | ~2 GB | `ComfyUI/models/SPAR3D/` | Ja — Stability Community |
| Stable Fast 3D | ~1.5 GB | `ComfyUI/models/SF3D/` | Ja — Stability Community |
| TripoSG (1.5B) | ~3 GB | `ComfyUI/models/TripoSG/` | Ja — OSS (VAST) |
| InstantMesh | ~1 GB | `ComfyUI/models/InstantMesh/` | Ja — Apache-2.0 |
| Flux.1-dev (für interne 2D-Iteration) | ~24 GB | `ComfyUI/models/checkpoints/` | Nur intern — Non-commercial Default |
| SDXL 1.0 base + refiner | ~13 GB | `ComfyUI/models/checkpoints/` | Ja — Stability Community |

> **Wichtig:** Final-Konzepte via **SDXL+LoRA** produzieren (kommerziell sauber). Flux.1-dev nur für interne Iteration.

### 4.4 Workflow-Ablage

```
F:\AI\ComfyUI_workflows\handwerkerimperium_unity\
├── 00_style_reference\
│   ├── workshop_carpenter\           (5 Style-Refs; je Werkstatt-Typ analog)
│   ├── workshop_innovation_lab\
│   ├── building_office\              (7 Buildings analog)
│   ├── worker_tier_F_to_legendary\   (10 Tiers × m/w)
│   ├── tool_master_smith\
│   ├── guild_hall_assembly\          (10 Hall-Gebäude)
│   ├── guild_boss_stone_golem\       (6 Bosse)
│   └── city_tile_dawn\
├── 01_concept_2d\                    (Stage 1)
│   ├── sdxl_workshop_lora.json
│   ├── sdxl_building_lora.json
│   ├── sdxl_worker_lora.json
│   ├── sdxl_guild_lora.json          (Hall-Gebäude + Bosse)
│   └── flux_props_iter.json          (intern)
├── 02_image_to_3d\                   (Stage 2)
│   ├── trellis2_workshop_modular.json
│   ├── trellis2_building_modular.json
│   ├── trellis2_worker_tpose.json
│   ├── trellis2_guild_hall.json
│   ├── rodin_guild_boss.json         (Cloud-Polish, animierbar)
│   ├── spar3d_tools_batch.json
│   ├── spar3d_equipment_batch.json   (4 Typen × 4 Rarities)
│   ├── triposg_crafting_items.json
│   └── batch_city_tiles.json
├── 03_texture_decals\                (Stage 4, HWI-spezifisch!)
│   ├── workshop_upgrade_lv1_to_5.json
│   ├── worker_skin_tier_recolor.json
│   ├── workshop_specialization.json  (Efficiency/Quality/Economy)
│   └── master_tool_glow_emissive.json
├── 04_audio\                         (Stage 7)
│   ├── stable_audio_workshop_ambience.json
│   ├── stable_audio_minigame_sfx.json
│   └── elevenlabs_meister_hans.json
├── pilot_log.md
└── README.md
```

Versionierung via Git-LFS für JSONs und Style-References.

---

## 5. Stage 1 — 2D-Konzept (Flux/SDXL + Style-LoRA)

### 5.1 Style-LoRA — Gemeinsam mit 2D-Pipeline

Die existierende **2D-Avalonia-Icon-Pipeline** (`F:\AI\ComfyUI_workflows\handwerkerimperium\`) hat bereits einen Toon-Stil-Lock. Wir trainieren **eine erweiterte LoRA** `handwerkerimperium_toon_v2.safetensors`, die sowohl 2D-Icons als auch 3D-Konzept-Bilder konsistent generiert.

**Trainings-Setup:**
- Tool: **Kohya_ss** (Standard für SDXL/Flux-LoRA-Training)
- Trainings-Set: 30-50 hochwertige eigene Konzept-Bilder, breit über alle sichtbaren Kategorien gestreut (Workshops, Buildings, Workers + Equipment, Tools, Crafting-Produkte, Gilden-Hall-Gebäude, Gilden-Bosse, City-Tiles) — so bleibt der Style-LoRA über das gesamte Asset-Spektrum konsistent
- Re-Use existierender 2D-Style-Bilder aus der Avalonia-Pipeline als Basis
- Trainings-Zeit: 4-8h auf RTX 4080 (LoRA Rank 64)
- Output: `handwerkerimperium_toon_v2.safetensors` (~150-300 MB)

**Style-Lock-Prompt (Template):**

```
Style-Block (Pflicht in jedem Asset-Konzept-Prompt):
<lora:handwerkerimperium_toon_v2:0.85>,
stylized 3D cartoon, warm wood and amber tones (#D97706 accent),
friendly toon-shaded look, clear silhouette,
slightly exaggerated proportions, soft ambient occlusion,
matte materials, T-pose if humanoid, single object centered,
plain neutral background, 3D render, cozy craftsman aesthetic

Negative-Prompt (Pflicht):
realistic, photoreal, dark gothic, cyberpunk, neon, blurry,
watercolor, painterly, multiple objects, busy background,
text, watermark, NSFW, deformed, extra limbs
```

### 5.2 ControlNet für Workshop-Modul-Layout

Für modulare Werkstätten brauchen wir Single-View-Konzepte mit definierten Modul-Positionen:
- Hauptgebäude zentriert
- Schild oberhalb der Tür
- Werkbank rechts
- Lager-Anbau links
- Skybox: einfarbig (für Decal-Generation in Stage 4)

ControlNet `Canny` aus einem groben Block-Sketch erzwingt die Layout-Konstanz.

---

## 6. Stage 2 — Image-to-3D (TRELLIS 2 / SPAR3D / TripoSG)

### 6.1 Algorithmus-Wahl pro Asset-Typ

| Asset-Typ | Primär-Algorithmus | Backup | Grund |
|-----------|---------------------|--------|-------|
| Werkstatt (10 Typen, komplex, modular zerlegbar) | **TRELLIS 2** | Rodin Gen-2.5 | Beste Topologie für Multi-Mesh-Komposition |
| Building (7 Typen, modular) | **TRELLIS 2** | Rodin Gen-2.5 | Wie Werkstätten, Modul-Split für 5 Level-Stufen |
| Worker (10 Tiers × m/w, humanoid, T-Pose) | **TRELLIS 2** | TripoSG | TRELLIS-2 hält humanoide Topologie für Mixamo-Rigging |
| Worker-Equipment (4 Typen × 4 Rarities) | **SPAR3D** | InstantMesh | Kleine getragene Props (Helm/Handschuhe/Stiefel/Gürtel) |
| Crafting-Produkt T1/T2 (33 gesamt) | **SPAR3D** | TRELLIS 2 | <1s pro Asset, gute Prop-Qualität |
| Crafting-Produkt T3/T4 (Villa, Skyscraper, HQ) | **TRELLIS 2** | Rodin Gen-2.5 | Hero-Quality, Cloud-Polish optional |
| Master-Tools (12 Artefakte) | **SPAR3D** | TRELLIS 2 | Kleine Props mit Glow → Emissive im PBR |
| City-Tiles (80) | **TripoSG** | TRELLIS 2 | Batch-fähig, gleichförmige Geometrie |
| Gilden-Hall-Gebäude (10) | **TRELLIS 2** | Rodin Gen-2.5 | Modular, Level = Größe/Glow |
| Gilden-Bosse (6, animiert) | **Rodin Gen-2.5** | TRELLIS 2 + Cloud-Polish | Hero-Charaktere, eigene Boss-Arena, müssen riggbar sein |
| Mega-Projekt (2 Templates: Cathedral, HQ) | **Rodin Gen-2.5** | TRELLIS 2 + Cloud-Polish | Hero-Hero, Architektur-Qualität, 5 Bauphasen je Template |
| MiniGame-Props (13 Typen / 10 Renderer) | **SPAR3D** | InstantMesh | Schnell, klein |
| Cosmetics / Event-Visuals (saisonal) | **SPAR3D** / **TRELLIS 2** | — | Je nach Komplexität (Deko-Props vs. Themen-Strukturen) |

### 6.2 TRELLIS 2 — Workshop-Workflow

Workflow-Datei: `02_image_to_3d/trellis2_workshop_modular.json`

Spezialität für HWI: Pre-Composition-Hint im Konzept-Bild (siehe §5.2). Nach TRELLIS-2-Output in Stage 3 modular zerlegt.

Eingabe: PNG 1024², transparenter BG, definierte Modul-Positionen.
Ausgabe: GLB mit Mesh + PBR-Texturen.
Dauer: ~45-90s auf RTX 4080.

### 6.3 Batch-Generation (City-Tiles, Crafting-Items)

Für City-Tiles (80 Stück): `02_image_to_3d/batch_city_tiles.json` mit Queue-Node. Über Nacht laufen lassen — ~12h für 80 Tiles auf RTX 4080.

Für Crafting-Items pro Tier (10 × T1-T4 = 40): Tier-spezifischer Batch.

---

## 7. Stage 3 — Blender-Cleanup + Modulare Werkstätten

### 7.1 Standard-Cleanup pro Asset

Template: `F:\AI\Blender\handwerkerimperium_unity_cleanup.blend`

1. **Import GLB**.
2. **Decimate-Modifier** (Collapse, Ratio aus Budget [§12](#12-asset-kategorien--budgets-toon-werkstatt)).
3. **UV-Repair** (Smart UV Project bei Überlappung).
4. **Origin** auf Boden-Mitte (Z=0); Werkstätten: auf Türschwelle (User-Click-Zentrum).
5. **Scale anwenden** (1 Blender-Unit = 1 Meter).
6. **Normals** neu berechnen.
7. **Texturen prüfen** + neutrale Defaults bei fehlenden Maps.
8. **FBX-Export** mit Texture-Embed.

**Automatisierung:** `F:\AI\Blender\scripts\hwi_unity_batch_cleanup.py` für GLB-Batches.

### 7.2 Workshop-spezielle Modul-Zerlegung

HWI-spezifisch und wichtig: **Werkstätten werden in Module zerlegt**, damit Upgrade-Stufen über Decal-Material-Sets visualisiert werden können (nicht über 5 separate Modelle).

**Modul-Zerlegung pro Werkstatt:**

```
Carpenter_Lv1_Base.fbx
├── Carpenter_Building          (Haupt-Hütte/Gebäude)
├── Carpenter_Sign              (Werkstatt-Schild über der Tür)
├── Carpenter_Workbench         (Werkbank/Werkzeug-Setup)
├── Carpenter_StorageAddon      (Lager-Anbau, anfänglich versteckt)
└── Carpenter_Decoration_Lv1    (Lv1-Deko, später ausgetauscht)
```

Mesh-Zerlegung in Blender (manuell beim Pilot, später Script-basiert):

1. Workshop-Mesh nach TRELLIS-Output importieren.
2. Edit-Mode → Loose-Parts-Split (`P > By Loose Parts`).
3. Manuelles Re-Grouping in logische Modulebenen.
4. Parent-Empty `Carpenter_Lv1_Base` erstellen, alle Module dort parenten.
5. FBX-Export mit Children → Unity-Import behält Hierarchie.

**Spezial-Script:** `F:\AI\Blender\scripts\hwi_unity_workshop_modular.py` automatisiert Modul-Zuordnung über Bounding-Box-Heuristik (Schild = höchste Position, Lager-Anbau = außerhalb Haupt-Bounding-Box).

### 7.3 Worker-Cleanup

Pro Worker-Basis-Modell (20 Stück = 10 Tiers F/E/D/C/B/A/S/SS/SSS/Legendary × m/w, siehe DESIGN.md § 5):

1. **Decimate AUF Polygon-Budget VOR Mixamo-Rig** (Mixamo limitiert auf 75k Tris).
2. **T-Pose-Check:** GLB in T-Pose? Falls nicht → Blender-Auto-Pose via Rigify-Stub setzen.
3. **Face-UV-Map separieren** in eigenes UV-Set (für Mood-State-Texture-Swap, 4 States).
4. **Affinity-Prop-Slot** als leerer Empty an der Hand-Bone-Position (5 Material-Affinitäten).
5. **Equipment-Attach-Slots** als leere Empties (Helm-/Hand-/Fuß-/Gürtel-Bone) für sichtbar getragenes Equipment (4 Typen × 4 Rarities).

---

## 8. Stage 4 — Texturing + Materialien + Decals

### 8.1 Workshop-Upgrade-Stufen via Decals (HWI-Killer-Feature)

Statt 5 separater Werkstatt-Modelle: **1 Modell + 5 Decal-Material-Sets**.

**Decal-Pipeline:**
1. **Basis-Modell + Basis-Material** (Carpenter_Lv1).
2. **Decal-Layer** als zusätzliche Texturen, in Unity via `URP/Lit Decal` oder Material-Property-Override:
   - Lv1: keine Decals
   - Lv2: kleine Verbesserungs-Sticker (z.B. "Quality+" Schild)
   - Lv3: zusätzliches Werkzeug-Setup (Hammer/Säge an der Wand)
   - Lv4: Premium-Schild + Lampe + Gold-Akzent
   - Lv5: Master-Werkstatt mit Gold-Aura + dekorativen Sub-Modulen

**Decal-Generation:**

Workflow: `03_texture_decals/workshop_upgrade_lv1_to_5.json`

- **Substance 3D Sampler 4.4** mit Image-to-Material für Sticker-Sets (Photo eines Schilds → Decal-Material).
- **ComfyUI Material-LoRA** für stilkonsistente Glow-Maps (Lv5 Gold-Aura).
- **Adobe Substance 3D Painter** für Hand-Polish bei Hero-Workshops.

### 8.2 Workshop-Specialization-Skins

3 Specializations (Efficiency / Quality / Economy) × 10 Workshops = 30 Skin-Varianten. **Über Material-Property-Override**, nicht über neue Modelle.

| Specialization | Visual-Approach |
|---------------|-----------------|
| Efficiency | Blaue Highlights, schnellere Lüfter-Particle-FX |
| Quality | Gold-Akzente, hochwertigere Werkzeug-Texturen |
| Economy | Sparsame Beleuchtung, recycled-look Texturen |

Workflow: `03_texture_decals/workshop_specialization.json`

### 8.3 Worker-Mood-States via Texture-Swap

Pro Worker: **4 Gesichtstexturen** (Happy / Neutral / Sad / Frustrated) auf separatem UV-Set. Material-Property-Override im Unity-Animator.

**Generation:**
- Basis-Gesicht aus 3D-Modell extrahiert (separate UV-Map in Blender, siehe §7.3).
- 4 Varianten via SDXL+LoRA + ControlNet (Face-Inpainting auf UV-Map).
- Workflow: `03_texture_decals/worker_skin_tier_recolor.json` (recyclet für Mood-Variation)

### 8.4 Worker-Affinitäts-Props

5 Material-Affinitäten (Wood/Metal/Stone/Art/Tech) × 20 Worker = potenziell 100 Varianten. **Über Prop-Anhänger an Hand-Bone**, nicht via neue Modelle:

- Wood-Affinity: Hammer (kleines Modell, gemeinsam für alle Wood-Workers)
- Metal-Affinity: Schweißbrille (Helm-Slot) + Schraubenschlüssel
- Stone-Affinity: Maurer-Kelle
- Art-Affinity: Pinsel-Set
- Tech-Affinity: Tablet/Smartphone

**Generation:** 5 Prop-Modelle insgesamt (kein Re-Generation pro Worker). Unity-Animation-Layer hängt das Prop dynamisch an Hand-Bone.

### 8.5 Worker-Equipment (sichtbar getragen)

Das Original kennt **4 Equipment-Typen** (Helm, Handschuhe, Stiefel, Gürtel) in **4 Rarities** (Common/Uncommon/Rare/Epic) — Quelle: [ORIGINAL_WERTE.md § 6](ORIGINAL_WERTE.md). Jeder Worker hat **genau 1 Equipment-Slot** (`EquippedItem`), kein Multi-Slot. In 3D wird das getragene Stück sichtbar an den passenden Bone-Slot des Workers gehängt (Helm → Kopf, Handschuhe → Hände, Stiefel → Füße, Gürtel → Hüfte).

- **16 Modelle** (4 Typen × 4 Rarities), klein (LOD0 ~300 Tris). Kein Re-Modeling pro Worker.
- **Rarity-Visualisierung** über Material + Farbe (Rarity-Color aus ORIGINAL_WERTE.md § 6.4: Common #9E9E9E, Uncommon #4CAF50, Rare #2196F3, Epic #9C27B0) + Emissive-Akzent bei Epic.
- **Generation:** SPAR3D (kleine Props) + Material-Recolor pro Rarity. Workflow: `03_texture_decals/equipment_rarity_recolor.json`.

### 8.6 Master-Tool Glow + Emissive

12 Master-Tools brauchen Glow-FX (Gold-Hammer, Crystal-Chisel, etc.) — Quelle: [ORIGINAL_WERTE.md § 5](ORIGINAL_WERTE.md), Rarity Common→Legendary:

- Albedo + Normal Standard.
- **Emissive-Map** für Glow-Bereiche, in Unity via `URP/Lit Emission`. Glow-Intensität skaliert mit MasterToolRarity.
- Workflow: `03_texture_decals/master_tool_glow_emissive.json` (SDXL + ControlNet-Mask für Glow-Bereich-Definition).

---

## 9. Stage 5 — Rigging + Animation (Arbeiter + Mood-States)

### 9.1 Auto-Rigging — Tool-Wahl

| Asset-Typ | Tool | Grund |
|-----------|------|-------|
| Standard-Humanoid-Worker (Tiers F-Legendary, m/w) | **Mixamo** | Beste Animation-Library, perfekt für Standard-Proportionen |
| Toon-übertrieben proportionierte Worker | **Tripo Auto-Rigging** oder **AccuRIG 2** | Mixamo versagt bei zu stylisierten Proportionen |
| Werkstatt-Animationen (statisch + Particle) | **Kein Rig** | Werkstätten haben Idle-Animation nur in Particle-Effekten (Rauch aus Schornstein, Funken am Workbench) |

### 9.2 Worker-Animation-Set (Standard)

Pro Worker-Basis-Modell (20 Stück):

| Animation | Quelle | Loop? | Dauer |
|-----------|--------|-------|-------|
| Idle | Mixamo | Ja | 4s |
| Idle (variant for Mood-Variation) | Mixamo | Ja | 4s × 4 Moods = 16s gesamt |
| Walking | Mixamo | Ja | 2s |
| Hammering | Mixamo / Cascadeur-Polish | Ja | 1.5s |
| Sawing | Mixamo / Cascadeur-Polish | Ja | 1.5s |
| Painting | Cascadeur-handgemacht | Ja | 2s |
| Frustrated-Outburst | Cascadeur-handgemacht | Nein | 1s |
| Happy-Cheer | Mixamo | Nein | 1.5s |

Insgesamt: ~8 Anim-Clips × 20 Workers = 160 Animation-Slots. Mixamo deckt 6 davon ab, Cascadeur-Polish für die anderen 2.

### 9.3 Mood-State-Sync

Animator-State-Machine pro Worker. **Mood-Schwellen 1:1 aus dem Original** ([DESIGN.md § 5.3](DESIGN.md) / [ORIGINAL_WERTE.md § 3.4](ORIGINAL_WERTE.md)): Happy ab 80, Neutral ab 50, kritisch unter 20, `WillQuit` bei Mood < 20):

```
Idle_Happy   ─── (Mood < 80) ──> Idle_Neutral
Idle_Neutral ─── (Mood < 50) ──> Idle_Sad
Idle_Sad     ─── (Mood < 20) ──> Idle_Frustrated (WillQuit-Zone, QuitDeadline läuft)
```

Frustrated-Outburst ist ein One-Shot, der bei tatsächlichem Verlassen (`QuitDeadline` abgelaufen) abgespielt wird. Die 4 Face-Texturen mappen auf diese 4 visuellen Zustände (Happy/Neutral/Sad/Frustrated). Material-Slot-Override (Face-Texture-Swap) synchronisiert mit Animator-State. Die Schwellen sind reine Präsentations-Trigger und ändern **keine** Spielmechanik.

### 9.4 Workshop-Idle-Particle-FX

Werkstätten "leben" über Particle-Systems statt Mesh-Animation:
- Carpenter: Sägemehl beim Workbench
- Plumber: Wasser-Tropfen am Pipe-Set
- Electrician: Funken aus dem Sicherungskasten
- Painter: Farbpartikel vom Pinsel
- Roofer: Holzschindeln-Stapel mit subtilem Wind
- Contractor: kleine Baustellen-Lichter
- Architect: Hologramm-Blueprint flackert
- GeneralContractor: Gold-Schimmer-Partikel
- MasterSmith: Funken-Burst alle 5s
- InnovationLab: Holo-Display + Cyan-Glow-Pulse

Alle Particle-FX in Unity, kein KI-Asset nötig.

---

## 10. Stage 6 — Unity-Import

### 10.1 Pro-Asset-Checkliste

- [ ] FBX in `Assets/_Project/Art/Models/{Kategorie}/` ablegen.
- [ ] **Model Tab:** `Scale Factor = 1`, `Read/Write = false`, `Mesh Compression = High`.
- [ ] **Rig Tab** (Arbeiter): `Animation Type = Humanoid`, Avatar aus Mixamo-Rig.
- [ ] **Animation Tab:** Mixamo-Clips als Sub-Assets, Loop für Idle/Walking. Mood-Variants via Avatar-Mask.
- [ ] **Materials Tab:** Extract → `Art/Materials/`. Shader `URP/Lit` oder `URP/Toon`.
- [ ] **LOD-Group** (3 LODs aus Budget [§12](#12-asset-kategorien--budgets-toon-werkstatt)).
- [ ] **Collider:** Box/Capsule manuell. Werkstätten: Box-Collider Gesamt + Trigger-Collider Eingang.
- [ ] **Layer:** Worker / Workshop / Building / Item / CityTile / GuildBuilding / GuildBoss.
- [ ] **Prefab** in `Assets/_Project/Prefabs/{Kategorie}/`.
- [ ] **Addressables-Group** zuweisen.

### 10.2 Modulare Werkstätten in Unity

```
Carpenter_Lv1_Prefab (Parent-Empty)
├── Carpenter_Building       (Mesh-Renderer + Material_Lv1)
├── Carpenter_Sign           (Mesh-Renderer + Decal-Material)
├── Carpenter_Workbench      (Mesh-Renderer)
├── Carpenter_StorageAddon   (Mesh-Renderer, GameObject.SetActive(false) initial)
└── Carpenter_Decoration_Lv1 (Mesh-Renderer, swapped bei Upgrade)
```

Upgrade-Logic (Lv1→Lv2):
- `Carpenter_StorageAddon.SetActive(true)`
- `Building.Material = Carpenter_Material_Lv2`
- `Decoration_Lv1.Mesh = Decoration_Lv2.Mesh` (via MeshFilter-Swap)

### 10.3 Addressables-Gruppen

```
HandwerkerImperium.Unity Addressables:
├── BootstrapAssets          # Logo, Splash, Default-Material, UI-SpriteAtlas (224 Icons)
├── Workshops_Basic          # 10 Basis-Werkstätten + Sub-Module (immer)
├── Workshops_Decals_Lv{1..5} # Decal-Material-Sets (lazy bei Upgrade)
├── Workshops_Specialization # 3 Spez. (Efficiency/Quality/Economy) × 10 (lazy)
├── Buildings                # 7 Buildings (Office/Storage/TrainingCenter/Canteen/Showroom/… — DESIGN.md § 7), je 5 Level-Stufen via Decal
├── Workers_TierF_to_C       # Basis-Tiers (immer) — Tiers F/E/D/C × m/w
├── Workers_TierB_to_S       # Höhere Tiers (lazy bei Erst-Hire) — B/A/S × m/w
├── Workers_TierSS_to_Legendary # SS/SSS/Legendary × m/w (lazy)
├── Workers_Affinity_Props   # 5 Prop-Modelle (Wood/Metal/Stone/Art/Tech), klein
├── Worker_Equipment         # 4 Typen (Helm/Handschuhe/Stiefel/Gürtel) × 4 Rarities, sichtbar am Worker (lazy bei Equip)
├── CraftingItems_T{1..4}    # 33 Produkte pro Tier (10/10/10/3, lazy bei Crafting-Unlock)
├── MasterTools              # 12 Artefakte (lazy bei Unlock)
├── CityTiles_World{1..10}   # Pro Welt-Theme (lazy bei Theme-Wechsel)
├── MegaProjects             # 2 Templates (Cathedral + HQ) + Bauphasen (lazy)
├── Guild_Hall_Buildings     # 10 Hall-Gebäude (DESIGN.md § 33) (lazy bei Gilden-Beitritt)
├── Guild_Bosses             # 6 Boss-Modelle (StoneGolem/IronTitan/… — DESIGN.md § 33) (lazy)
├── MiniGames_{1..13}        # 13 MiniGame-Typen, 10 distinkte Renderer/Prop-Sets (lazy)
├── Cosmetics_Event          # Skins, saisonale Event-Visuals (Spring/Summer/Fall/Winter) (lazy)
├── Prestige_Cinematic       # Hero-Assets (lazy bei Prestige)
└── Audio_Music + Audio_SFX + Audio_Voice  # Stage 7 Output
```

### 10.4 Texture-Compression

- **Android:** ASTC 6×6.
- **Windows:** BC7.
- **Atlas-Pflicht** für City-Tiles (1 Atlas pro Welt-Theme statt 8 Texturen).

---

## 11. Stage 7 — Audio (Werkstatt-Sounds + Musik + Meister-Hans-Voice)

### 11.1 Musik — Stable Audio 3

**Asset-Plan (Musik):**
- 4 Loop-Tracks: IdleWorkshop, GuildBoss, Tournament, Celebration (entspricht Avalonia-Bestand, in 3D-Stil neu erzeugt)
- 1 Menu-Track
- 1 Prestige-Cinematic-Track (orchestral, 30s)
- 4 Saisonal-Themes (Spring/Summer/Fall/Winter Kurz-Loops, entspricht den 4 saisonalen Live-Events — DESIGN.md)

**Gesamt:** ~10 Tracks.

**Workflow:** `04_audio/stable_audio_workshop_ambience.json` mit Prompt-Templates pro Mood.

### 11.2 SFX — Stable Audio Open Small

Werkstatt-spezifische SFX-Klassen (Stückzahlen aus realem Spielinhalt — DESIGN.md / ORIGINAL_WERTE.md):
- 10 Workshop-Idle-Loops (Sägen, Hämmern, Schweißen, etc. — pro WorkshopType)
- 7 Building-Idle-/Interaktions-Loops (Office, Storage, TrainingCenter, Canteen, Showroom, …)
- 12 Master-Tool-Unlock-Stinger (1 pro Artefakt)
- 6 Order-Complete-Stinger (Quality-Stufen: Perfect/Good/… + Order-Type-Akzente)
- 10 MiniGame-SFX-Sets (für die 10 distinkten Renderer — Sawing-Cut, Pipe-Click, Wire-Spark, Paint-Splash, Roof-Tile-Place, Blueprint-Reveal, DesignPuzzle-Snap, Inspection-Beep, Forge-Strike, Invention-Spark). Die 13 MiniGame-Enum-Typen teilen sich diese 10 Renderer (Planing/TileLaying/Measuring nutzen Sawing-Route).
- 6 Gilden-Boss-Sounds (Hit/Defeat/Spawn pro Boss-Archetyp — StoneGolem/IronTitan/MasterArchitect/RustDragon/ShadowTrader/ClockworkColossus)
- Gilden-Hall-Build-/Upgrade-Stinger + Mega-Projekt-Bauphasen-Sounds
- UI-Sounds (Tab-Switch, Achievement-Unlock, Currency-Earn, Premium-Tier-Up, Equip-Click, Prestige-Whoosh)
- Worker-Reactions (Happy-Laugh, Neutral-Hum, Sad-Sigh, Frustrated-Grunt) — 4 Mood-States × m/w = 8 Stimm-Sets, mehrere Varianten

**Gesamt:** ~150 SFX (aus den realen Systemen abgeleitet, nicht frei geschätzt).

### 11.3 Voice — Meister Hans (ElevenLabs Standard-Voice)

Meister-Hans-Persona ist zentral für HWI. **Wir nutzen eine vorgefertigte ElevenLabs-Standard-Voice** (kein Voice-Cloning, kein eigener Sprecher).

Diese Entscheidung ist verbindlich und deckungsgleich mit [DESIGN.md § 36.1](DESIGN.md) (Voice-Strategie geändert Mai 2026 — Standard-Voice statt Cloning).

**Begründung der Entscheidung (Mai 2026):**
- Schneller Setup (kein Aufnahme-Equipment, keine Sprecher-Freigabe-PDF)
- Konsistente Qualität in allen 6 Sprachen via Multilingual v2-Modell
- Keine rechtlichen Risiken (Voice ist von ElevenLabs lizenziert, kommerziell freigegeben mit Pro-Sub)
- Re-Generation jederzeit möglich (z.B. neue Story-Chapters, Live-Event-Texte)
- Geringeres Budget-Risiko (kein Voice-Actor-Honorar)

**Voice-Konfiguration:**
- **6 Sprachen** (DE/EN/ES/FR/IT/PT)
- **Voice-Auswahl:** Eine vorgefertigte Stimme aus ElevenLabs-Library, Filter "warm, friendly, slightly older male, multilingual support"
- **Modell:** `eleven_multilingual_v2` (ein Voice-ID funktioniert für alle 6 Sprachen)
- **Voice-Settings:** `stability = 0.5`, `similarity_boost = 0.75`, `style = 0.3` (leicht karikiert)

**Voice-Lines (Aufteilung am realen Content-Umfang orientiert — DESIGN.md):**
- FTUE-/Tutorial-Hints: 10 FTUE-Schritte + kontextuelle Hints → ~25 Lines/Sprache (DESIGN.md FTUE = 10 Schritte)
- Story-Chapters: 60 Kapitel (40 Haupt + 20 Saison, DESIGN.md § 26) — eine markante Hans-Voice-Line je Kapitel → ~60 Lines/Sprache (Volltext-Vertonung aller Kapitel ist Phase 2)
- Random-Idle-Tipps: ~30 Lines/Sprache
- Achievement-Unlock-Voicelines: eine Auswahl der 79 Spieler-Achievements (Meilensteine, DESIGN.md § 24) → ~40 Lines/Sprache
- Live-Events + Notifications + Premium-Promotion: ~40 Lines/Sprache

Summe ~205 Kern-Lines + Reserve für Live-Event-Nachschub → **~250 Voice-Lines pro Sprache**.

**Insgesamt:** ~250 Voice-Lines × 6 Sprachen = **~1500 Voice-Files** (deckungsgleich mit DESIGN.md § 2 / § 36.1).

**Workflow:** `04_audio/elevenlabs_meister_hans.json` (ElevenLabs-API-Integration, batchable via Python-Skript). Beispiel:

```python
import requests
import os
from pathlib import Path

API_KEY = os.environ["ELEVENLABS_API_KEY"]
VOICE_ID = "selected_meister_hans_voice_id"  # Aus ElevenLabs-Library
MODEL_ID = "eleven_multilingual_v2"

def generate_voice(text: str, lang_code: str, out_path: Path):
    response = requests.post(
        f"https://api.elevenlabs.io/v1/text-to-speech/{VOICE_ID}",
        headers={"xi-api-key": API_KEY, "Content-Type": "application/json"},
        json={
            "text": text,
            "model_id": MODEL_ID,
            "voice_settings": {"stability": 0.5, "similarity_boost": 0.75, "style": 0.3}
        }
    )
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(response.content)

# Batchable über StringTable-CSV-Export
```

**Budget:** ElevenLabs Pro-Subscription 22 €/Monat reicht für ~100k chars/month → 1500 Voice-Lines × ~50 chars = 75k chars → ~1 Monat Pro-Sub reicht für komplette Voice-Generation. Bei Re-Generation einzelner Lines (Live-Events) ist Pro permanent sinnvoll.

**Output-Struktur:**

```
F:\AI\audio_output\handwerkerimperium_unity\voice_meister_hans\
├── de\  (DE-Files, MP3 oder Vorbis q0.6)
├── en\
├── es\
├── fr\
├── it\
└── pt\
```

**Mastering:** Alle Lines auf −16 LUFS (siehe § 11.4).

**Worker-Tier-spezifische Voice-Lines** (eigene Stimmen pro Worker-Tier) kommen erst in **Phase 2** (Post-Launch). Im MVP nur Meister Hans.

### 11.4 Mastering

- Alle Tracks auf **−16 LUFS** (Mobile-Standard, EBU R128).
- Tool: **Adobe Audition** oder **iZotope Ozone** (lokal).
- Pre-Master-Backup + Final-Master in `F:\AI\audio_output\handwerkerimperium_unity\`.

---

## 12. Asset-Kategorien & Budgets (Toon-Werkstatt)

### 12.1 Polygon-Budgets (Mid-Tier-Android Ziel)

Stückzahlen sind 1:1 aus dem realen Spielinhalt abgeleitet (Quelle: [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) / [DESIGN.md](DESIGN.md)). "KI direkt?": Ja = vollständig per lokaler KI-Pipeline; Cloud = Cloud-Polish (Rodin Gen-2.5) empfohlen.

| Asset-Klasse | Anzahl | LOD0 | LOD1 | LOD2 | KI direkt? |
|--------------|-------:|-----:|-----:|-----:|------------|
| **Werkstätten Basis (10 Typen)** — DESIGN.md § 4 | 10 | 6 000 | 3 000 | 1 500 | Ja + Modular |
| **Workshop-Sub-Module** (Schild/Werkbank/Lager × 10) | 30 | 800 | 400 | 200 | Ja (Modul-Split) |
| **Workshop-Upgrade-Decals (Lv1-5 × 10)** | 50 (Material-Sets) | (Re-Tex) | — | — | Ja (Stage 4) |
| **Workshop-Specialization** (Eff/Qual/Eco × 10) | 30 (Re-Tex) | (Re-Tex) | — | — | Ja (Stage 4) |
| **Buildings (7 Typen)** — DESIGN.md § 7 | 7 | 3 000 | 1 500 | 750 | Ja + Modular |
| **Building-Upgrade-Decals (Lv1-5 × 7)** | 35 (Material-Sets) | (Re-Tex) | — | — | Ja (Stage 4) |
| **Arbeiter-Basis (10 Tiers × m/w)** — DESIGN.md § 5 | 20 | 5 000 | 2 500 | 1 200 | Ja + Mixamo |
| **Arbeiter-Affinity-Props** (5 Material-Affinitäten) | 5 | 400 | 200 | 100 | Ja |
| **Worker-Mood-Face-Textures** (4 States × 20 Worker) | 80 (Tex) | (Tex) | — | — | Ja (Stage 4) |
| **Worker-Equipment** (4 Typen × 4 Rarities, sichtbar getragen) — ORIGINAL_WERTE.md § 6 | 16 | 300 | 150 | 80 | Ja |
| **Tier-1-Crafting-Produkte** (10) | 10 | 800 | 400 | 200 | Ja |
| **Tier-2-Crafting-Produkte** (10) | 10 | 1 200 | 600 | 300 | Ja |
| **Tier-3-Crafting-Produkte** (10) | 10 | 1 800 | 900 | 450 | Ja |
| **Tier-4-Crafting-Produkte** (Villa, Skyscraper, Imperium-HQ; IsHeirloomEligible) | 3 | 5 000 | 2 500 | 1 200 | Ja, Hero |
| **Master-Tools (12 Artefakte)** — ORIGINAL_WERTE.md § 5 | 12 | 600 | 300 | 150 | Ja + Glow |
| **City-Tiles (10 Welt-Themes × 8 Tiles)** | 80 | 1 200 | 600 | 300 | Ja (Batch, Tiling-Check) |
| **Gilden-Hall-Gebäude (10)** — DESIGN.md § 33 | 10 | 3 000 | 1 500 | 750 | Ja + Modular (Level = Größe/Glow) |
| **Gilden-Bosse (6 Typen)** — DESIGN.md § 33 / ORIGINAL_WERTE.md § 4 | 6 | 8 000 | 4 000 | 2 000 | Cloud-Polish (animierte 3D-Modelle, eigene Arena) |
| **Mega-Projekte (2 Templates: Cathedral, HQ)** je 5 Bauphasen | 2 (+10 Phasen-Stufen) | 12 000 | 6 000 | 3 000 | Cloud-Polish |
| **MiniGame-Props** (13 MiniGame-Typen, 10 distinkte Renderer — DESIGN.md § 7) | ~40 | 400 | 200 | 100 | Ja |
| **Cosmetics / Event-Visuals** (Skins + 4 saisonale Themes Spring/Summer/Fall/Winter) | ~20 | 1 000 | 500 | 250 | Ja |
| **Prestige-Cinematic-Hero** | 5 | 20 000 | — | — | Cloud + Polish |

**Total geometrische unique Models:** ~250 + ~111 Re-Texture/Decal-Sets (50 Workshop-Decals + 35 Building-Decals + 80 Mood-Faces + 30 Specialization) = **mehrere hundert Asset-Slots**. Aktuelle Aufschlüsselung in PLAN.md Anhang C. Erbstücke sind kein eigenes Modell — die 3 IsHeirloomEligible-T4-Produkte werden mit Material-Aura-Overlay wiederverwendet.

### 12.2 Texture-Auflösungen

| Klasse | Albedo | Normal | MRA | Notes |
|--------|-------:|-------:|----:|-------|
| Werkstätten + Buildings + Tier-4-Items | 2048² | 2048² | 2048² | Hero |
| Gilden-Hall-Gebäude + Mega-Projekte | 2048² | 2048² | 2048² | Hero |
| Gilden-Bosse | 2048² | 2048² | 2048² | Hero, animiert |
| Arbeiter + Tier-2/3-Items | 1024² | 1024² | 1024² | Mid |
| Master-Tools + Tier-1-Items + Worker-Equipment | 512² | 512² | 512² | Klein |
| City-Tiles | 1024² (atlassed) | 1024² | 1024² | Atlas pro Welt-Theme |
| MiniGame-Props | 512² | 512² | 512² | Mip-Bias +1 |
| Worker-Mood-Faces | 256² × 4 | — | — | Pro Worker, kompakt |
| Cosmetics / Event-Visuals | 1024² | 1024² | 1024² | Mid |

### 12.3 UI-Icons (2D bleiben 2D)

Das Avalonia-Original nutzt **224 Bitmap-/Vektor-Icons** (`GameIcon`-PathIcon-Subklasse, `Assets/visuals/`). Diese UI-Glyphen (Tab-Symbole, Button-Icons, Währungs-Glyphen wie Goldschraube/Geld, Rarity-Marker, Achievement-Badges) bleiben **2D** und werden 1:1 als **TextMeshPro-SpriteAsset** bzw. Sprite-Atlas in die Unity-UI übernommen (siehe PLAN.md § Migrations-Mapping). Sie sind **kein** 3D-Generierungs-Scope. In-Welt-Objekte (Werkstätten, Worker, Crafting-Produkte, Master-Tools, Buildings, Gilden-Gebäude, Bosse) sind dagegen vollwertige 3D-Assets (Stage 1-6). Material-Icons-Glyphen aus dem Original (z.B. Boss-/Hall-Icon-Keys in ORIGINAL_WERTE.md) werden ebenfalls als 2D-UI-Sprite gespiegelt — das 3D-Modell ist die spielweltliche Repräsentation, das 2D-Icon der UI-Repräsentant.

### 12.4 Audio-Budgets

| Klasse | Sample-Rate | Bit | Komprimierung | Größe-Ziel |
|--------|-------------|-----|----------------|-------------|
| Musik-Tracks (2-3min Loops) | 44.1 kHz | 16-bit | Vorbis Q 0.5 | ~2 MB/Track |
| Workshop-Idle-Loops (10s Seamless) | 44.1 kHz | 16-bit | Vorbis Q 0.6 | ~300 KB |
| MiniGame-SFX | 44.1 kHz | 16-bit | ADPCM | <50 KB |
| Meister-Hans-Voice (1-3s pro Line) | 44.1 kHz | 16-bit | Vorbis Q 0.6 | ~80 KB/sec |

**Total-Audio (6 Sprachen):** ~150-200 MB komprimiert.

---

## 13. Stil-Konsistenz (Stylized Toon-Werkstatt)

### 13.1 Brand-Identität

| Aspekt | Wert |
|--------|------|
| Markenfarbe | Amber `#D97706` (Hauptakzent), warme Holz-/Brauntöne |
| Tonalität | Freundlich, "Meister Hans"-Persona, leicht karikiert |
| Visual-Sprache | Stylized 3D-Cartoon, Toon-Shading optional, klare Silhouetten |
| Stil-Inspiration | Animal Crossing-Approach (keine direkte Referenz, eigener Style-LoRA) |

### 13.2 Style-Lock

`handwerkerimperium_toon_v2.safetensors` LoRA (siehe §5.1).
Festes Prompt-Template versioniert.
Konsistenz-Check pro 5-Asset-Batch in Blender-AssetReview-Szene mit Cartoon-Lighting-Rig.

### 13.3 Toon-Shading-Entscheidung

URP unterstützt eigenen Toon-Shader. Entscheidung pro Asset-Klasse:
- **Workshops + Buildings + Gilden-Hall-Gebäude + City-Tiles:** `URP/Lit` mit warmem Lighting (Standard-PBR)
- **Workers + Worker-Equipment + Master-Tools + Tier-4-Items:** `URP/Toon` mit Outline-Pass (Cell-Shading-Look)
- **Gilden-Bosse:** `URP/Toon` mit Outline + Emissive-Akzenten (Hero-Charaktere in der Boss-Arena)
- **MiniGame-Props:** `URP/Lit` mit AO-Boost
- **Mega-Projekte + Prestige-Cinematic:** `URP/Lit` mit Post-Processing-Bloom

Toon-Shader-Asset wird im Pilot evaluiert.

---

## 14. EU-Compliance & Lizenz-Recherche (Stand 2026-05)

### 14.1 Hunyuan3D — warum nicht?

Tencents Hunyuan3D-2 / 2.1 / 2.5 ist technisch gut, aber die Lizenz definiert eine `Territory`-Klausel, die **EU, UK und Südkorea explizit ausschließt** (siehe [Hunyuan3D-2 LICENSE](https://github.com/Tencent-Hunyuan/Hunyuan3D-2/blob/main/LICENSE)). Für deutsche Production = Show-Stopper ohne schriftliche Tencent-Sonderfreigabe. Wir bauen Hunyuan-frei.

### 14.2 EU-konformer Stack

| Modell | Lizenz | EU OK |
|--------|--------|-------|
| TRELLIS 2 | MIT | Ja |
| SPAR3D, SF3D, Stable Audio 3 | Stability Community ≤ $1M | Ja |
| TripoSG, TripoSF | OSS (VAST) | Ja |
| InstantMesh | Apache-2.0 | Ja |
| Mixamo | Adobe-Standard | Ja |
| Cascadeur | Free Indie < $100k Rev | Ja |
| Meshy Pro+ / Rodin Pro / Tripo Pro | Pro-Tier Commercial | Ja |

### 14.3 EU AI Act — Compliance-Status

Der EU AI Act ist am **2. August 2026 voll wirksam**. Game-Apps fallen in **"minimal risk"** — wir sind **Deployer**, kein GPAI-Provider.

**Auswirkungen:**
- Keine GPAI-Pflichten für uns.
- **Transparenz-Pflicht**: KI-Hinweis in Play-Store-Description + In-App-Credits.
- **Marketing-Material**: AI-generierten Content kennzeichnen.
- **Meister-Hans-Voice**: ElevenLabs Standard-Voice (vorgefertigte Library-Voice mit kommerziellen Rechten via Pro-Sub) — keine Echtperson abgebildet, kein Voice-Cloning → keine Deepfake-Relevanz.

### 14.4 Lizenz-Archiv

```
F:\AI\Licenses\handwerkerimperium_unity\
├── 2026-05-26_meshy_pro_commercial.pdf
├── 2026-05-26_rodin_gen2_free_commercial.pdf
├── 2026-05-26_substance_3d_sub.pdf
├── 2026-05-26_elevenlabs_pro.pdf
├── 2026-05-26_unity_ai_sub.pdf
├── 2026-05-26_cascadeur_indie.pdf
└── 2026-05-26_elevenlabs_voice_library_terms.pdf  (ElevenLabs Voice-Library kommerzielle Rechte)
```

Pro Asset-Metadata-JSON: `"license_source"` + `"license_archive"`.

### 14.5 Trainingsdaten-Risiko

- **Stable Audio 3**: lizenzierte Trainingsdaten → **risikolos**.
- **Suno / Udio**: laufende Lawsuits (April 2026) → **vermieden**.
- **TRELLIS 2**: Microsoft Research, akademische Datensätze → **niedriges Risiko**.
- **SDXL/Flux + eigene LoRA**: Pre-trained-Layer Common-Knowledge, eigenes Fine-Tuning auf eigenes Material → **risikolos**.
- **Meister-Hans-Voice**: ElevenLabs Standard-Voice mit Pro-Sub kommerziell-Rechten → **risikolos**.

---

## 15. Pilot-Plan (6 Assets vor Skalierung)

| # | Pilot-Asset | Kategorie | Pipeline-Test | Erfolgs-Kriterium |
|---|-------------|-----------|---------------|-------------------|
| 1 | Werkstatt "Carpenter" Basis + Lv1-5 Decals | Workshop | TRELLIS 2 + Modul-Zerlegung + 5 Decal-Sets | Modular Lv1→Lv5 visuell unterscheidbar in Unity, Decal-Material funktioniert |
| 2 | Arbeiter "C-Tier männlich" + 4 Mood-States + 1 sichtbares Equipment | Worker | TRELLIS 2 → Mixamo → 4 Face-Texture-Swaps + Helm-Attach an Bone | Animiert, alle 4 Stimmungen via Material-Slot synchronisiert, Equipment sichtbar getragen |
| 3 | Tier-2-Crafting "Wooden Furniture" | Crafting | SPAR3D → Cleanup → Unity | < 1200 Tris, ASTC-Test bestanden |
| 4 | Master-Tool "Golden Hammer" | Tool | SDXL → SPAR3D → Emissive-Map → URP/Lit Emission | Glow funktioniert in URP, < 600 Tris |
| 5 | City-Tile "Sunny Day Plaza" | City | TripoSG Batch → Tiling-Test 4× nebeneinander | Naht-frei, < 1200 Tris |
| 6 | Gilden-Boss "Stone Golem" | Guild-Boss | Rodin Gen-2.5 → Rig → Hit/Defeat-Anim | Animierter Hero-Charakter in Boss-Arena, Cloud-Pipeline validiert |
| **Audio-Pilot** | Workshop-Idle-Loop "Carpenter" (10s) | Audio | Stable Audio 3 + Mastering | Seamless Loop, LUFS −16 |
| **Voice-Pilot** | Meister-Hans-Line "Bauauftrag bereit!" (DE) | Voice | ElevenLabs Standard-Voice (multilingual v2) | Verständlich, konsistent mit Persona |
| **Workshop-Specialization-Test** | Carpenter Efficiency-Skin | Re-Texturing | Material-Property-Override in Unity | Distinct vom Basis-Carpenter, gleiches Modell-Footprint |

**Zeitplan:** ~7 Arbeitstage (Workshop 2 Tage, Worker+Equipment 1.5 Tage, Items 1 Tag, City+Audio 1 Tag, Guild-Boss 1 Tag, Specialization 0.5 Tage).

**Output:** Lessons-Learned in `F:\AI\ComfyUI_workflows\handwerkerimperium_unity\pilot_log.md`.

**Skalierungs-Freigabe:** Alle 6 Geometrie-Pilots + Audio/Voice/Specialization OK → Phase 2 Skalierung. Bei einem Fehlschlag → betroffene Pipeline-Stufe iterieren. Bei mehreren → Cloud-Anteil erhöhen oder Stack neu bewerten.

---

## 16. Output-Ablage + Versionierung

```
F:\AI\
├── ComfyUI_workflows\
│   └── handwerkerimperium_unity\
├── 3d_output\
│   └── handwerkerimperium_unity\
│       ├── concept_2d\
│       ├── raw_glb\
│       ├── modular_split\           (HWI-spezifisch! Workshop-Sub-Meshes)
│       ├── final_fbx\
│       └── metadata\
├── audio_output\
│   └── handwerkerimperium_unity\
│       ├── music\
│       ├── sfx_workshops\
│       ├── sfx_buildings\
│       ├── sfx_minigames\
│       ├── sfx_guild\               (Boss-Hits/Defeat, Hall-Build, Mega-Projekt)
│       ├── sfx_ui\
│       └── voice_meister_hans\
│           ├── de\
│           ├── en\
│           ├── es\
│           ├── fr\
│           ├── it\
│           └── pt\
├── animation_output\
│   └── handwerkerimperium_unity\
│       ├── mixamo_fbx\
│       └── cascadeur_export\
├── Licenses\
│   └── handwerkerimperium_unity\
└── Blender\
    ├── scripts\
    │   ├── hwi_unity_batch_cleanup.py
    │   └── hwi_unity_workshop_modular.py
    └── handwerkerimperium_unity_cleanup.blend
```

**Asset-Metadata-JSON** (Pflicht pro Asset):

```json
{
  "asset_id": "workshop_carpenter_v1",
  "category": "workshop_modular",
  "stage_1_concept": {
    "model": "sdxl_1.0",
    "lora": "handwerkerimperium_toon_v2@0.85",
    "prompt": "...",
    "seed": 234567
  },
  "stage_2_3d": {
    "tool": "trellis_2",
    "raw_glb": "raw_glb/workshop_carpenter_v1.glb"
  },
  "stage_3_modules": {
    "split_strategy": "loose_parts_plus_heuristic",
    "modules": ["Building", "Sign", "Workbench", "StorageAddon", "Decoration"]
  },
  "stage_4_textures": {
    "decals": ["lv1_default", "lv2_quality_sign", "lv3_tool_wall", "lv4_premium_lamp", "lv5_gold_aura"]
  },
  "license_source": "TRELLIS 2 (MIT)",
  "compliance_status": "EU-conformant"
}
```

---

## 17. Risiken & Mitigation

| Risiko | Wahrscheinlichkeit | Auswirkung | Mitigation |
|--------|-------------------|------------|------------|
| TRELLIS-2-Qualität reicht nicht für detaillierte Werkstatt | Niedrig | Hoch | Cloud-Fallback Rodin Gen-2.5 für 10 Workshop-Basis-Modelle |
| Modul-Zerlegung versagt bei untypischer Workshop-Geometrie | Mittel | Mittel | Hand-Splitten im Pilot, später Heuristik im Script verfeinern |
| Stil-Drift über 80 City-Tiles | Hoch | Mittel | LoRA Pflicht ab Pilot-Erfolg, Atlas-Generation pro Welt-Theme |
| Mixamo bei stylisierten Worker-Proportionen | Mittel | Mittel | Style-Reference dezent-exagerierten Proportionen (nicht zu extrem), AccuRIG 2 als Fallback |
| ASTC zu groß bei 80 City-Tiles + 30 Worker-Skins | Mittel | Hoch | Atlas-Pflicht pro Welt-Theme, Worker-Skins als Decal-Layer (nicht eigener Atlas) |
| Modulare Workshops mit Z-Fighting bei Anbauten | Mittel | Niedrig | Offset Z=0.001 zwischen Modulen, Sorting-Group |
| Re-Texturing-Workflow für 5 Upgrade-Stufen instabil | Mittel | Mittel | Pilot 1 (Carpenter) ist Stress-Test, Workflow-JSON validieren |
| EU AI Act Transparenz-Pflicht missachten | Niedrig | Hoch | Play-Store + In-App-Credits enthalten KI-Hinweis |
| Tencent klagt rückwirkend gegen Hunyuan-Asset | Niedrig | Hoch | Hunyuan **komplett vermeiden**, Asset-Metadata dokumentiert Tool-Quelle |
| Suno/Udio-Lawsuits eskalieren | Hoch | — | Suno/Udio vermieden, Audio nur Stable Audio 3 + ElevenLabs |
| Meister-Hans-Voice-Lizenz-Probleme | Sehr Niedrig | Mittel | ElevenLabs Standard-Voice mit Pro-Sub kommerzielle Rechte; Pro-Sub-Lizenz-PDF archiviert |
| Polygon-Inflation | Hoch (Default) | Niedrig | Blender-Decimate Pflicht |
| Worker-Affinity-Props passen nicht zum Worker-Stil | Mittel | Niedrig | Affinity-Props mit gleichem Style-LoRA generieren |
| Tile-Naht-Probleme | Mittel | Mittel | Pre-Gen Symmetrie-Prompt, Post-Gen Naht-Heal in Blender |
| Audio-LUFS-Inkonsistenz | Mittel | Mittel | Master-Pass mit iZotope Ozone als Pflicht |
| 6-Sprachen-Voice-Konsistenz | Mittel | Niedrig | ElevenLabs Multilingual v2 nutzt EINE Voice-ID für alle 6 Sprachen → konsistente Persona automatisch. A/B-Hörtest pro Sprache nach Initial-Generation. |

---

## 18. Verweise

### Projekt-interne Docs

| Bereich | Datei |
|---------|-------|
| Master-Plan | [PLAN.md](PLAN.md) |
| Conventions | [CLAUDE.md](CLAUDE.md) |
| Tech-Architektur (URP, Addressables, LOD) | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Roadmap | [ROADMAP.md](ROADMAP.md) |

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
- AccuRIG 2 (Reallusion)
- DeepMotion Animate 3D: `https://www.deepmotion.com/`
- RADiCAL (Autodesk): `https://ariusai.com/products/radical/`
- Adobe Substance 3D Sampler 4.4 (Creative Cloud)
- Unity AI (Unity 6.2): `https://unity.com/products/muse`
- ElevenLabs: `https://elevenlabs.io/`

### EU-Compliance

- EU AI Act: `https://digital-strategy.ec.europa.eu/en/policies/regulatory-framework-ai`
- Hunyuan3D EU-Restriction-Issue: [Issue #94](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1/issues/94)

### Lokale Ablage

- 3D-Workflows: `F:\AI\ComfyUI_workflows\handwerkerimperium_unity\`
- Asset-Output: `F:\AI\3d_output\handwerkerimperium_unity\`
- Audio-Output: `F:\AI\audio_output\handwerkerimperium_unity\`
- Animation-Output: `F:\AI\animation_output\handwerkerimperium_unity\`
- Pilot-Log: `F:\AI\ComfyUI_workflows\handwerkerimperium_unity\pilot_log.md`
- Lizenz-Archiv: `F:\AI\Licenses\handwerkerimperium_unity\`
- Blender-Template: `F:\AI\Blender\handwerkerimperium_unity_cleanup.blend`
- Blender-Scripts: `F:\AI\Blender\scripts\hwi_unity_*.py`

### Avalonia-Bestand als Referenz

- Existing 2D-Icons (Avalonia v2.1.1): `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Assets/visuals/`
- Existing 2D-ComfyUI-Pipeline: `F:\AI\ComfyUI_workflows\handwerkerimperium\`
- Bestehender Meister-Hans-Voice (falls vorhanden): in Avalonia-`Assets/Sounds/`
