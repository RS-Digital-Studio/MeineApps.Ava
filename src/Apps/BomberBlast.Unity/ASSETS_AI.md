# BomberBlast 3D — KI-Asset-Pipeline (3D + Audio + Animation)

> **Status:** Produktions-Plan (Stand 2026-05-30, recherchiert; Richtung v0.5 2026-06-08; Stage-2-Pipeline validiert 2026-06-06: Hunyuan3D-2.1 als Primärpfad)
> **Ziel:** Skalierbarer, kommerziell dokumentierter Workflow für 3D-Assets, Animationen, Texturen und Audio mit KI-Tools — primär lokal (Standalone-Runner auf isolierter 3D-Instanz, Hunyuan3D-2.1 als validiertes Stage-2-Modell), Cloud-Services als Fallback wo Qualität es rechtfertigt. **Reiner KI-Durchlauf — keine Handarbeit als Pipeline-Schritt** (Entscheidung 2026-06-06: „probiere alles, aber wir wollen kein Handarbeit").
> **Geltungsbereich:** die **5 Helden-Charaktere**, 12 Gegner, 5 Bosse, 14 Bomben-Typen, 12 PowerUps, 10 Sektor-Tile-Sets, Environment, Props, Animationen, Texturen, Game-Audio — alles im **Neon-Arcade-Stil des Originals**, jetzt in 3D.
> **Nicht im Scope:** UI-Icons (bleiben 2D), redaktionelle Texte, Story-Schreiben, Voice (deferred — Original ist voice-los).

> **WICHTIG — Richtung v0.5 + Subjekte sind KEINE Mechs:** Dies ist ein **modernes 3D-Bomberman** (aktiv
> gespielt, **kein Idle/AFK**) mit **neuer Story** (Neo-Grid/Overseer/Reborn). Die Charaktere sind die 5
> bestehenden Helden (Default/SpeedySam/BrickBoris/TwinTina/LuckyLola), die Gegner die klassischen
> Bomberman-Typen. Die 5 Bosse werden als **Sektor-Wardens neu benannt/eingekleidet** (Granite Warden /
> Frostwyrm / Magma Revenant / Null Phantom / The Overseer = Archetypen StoneGolem/IceDragon/FireDemon/
> ShadowMaster/FinalBoss) — **gleicher Mesh-Workflow, neue Optik/Namen**. „Welten" heißen jetzt **Sektoren**.
> Die spielbaren Charaktere sind die 5 Helden (humanoid, Neon-Arcade) — keine „Mechs".

> Achtung — **EU-Lizenz-Caveat Hunyuan3D:** Die Hunyuan3D-Lizenz (Tencent) schließt EU/UK/Südkorea kommerziell aus. **Hunyuan3D-2.1 ist dennoch der validierte Primärpfad** für Image-to-3D (Entscheidung 2026-06-06, Rechtsrisiko bewusst akzeptiert — qualitativ klar bestes Ergebnis, per Render belegt). **Vor kommerziellem Shipping erneut prüfen** — Sonderfreigabe einholen oder betroffene Assets via TRELLIS.2/Cloud regenerieren (Asset-Metadata macht sie auffindbar). Details: [§14](#14-eu-compliance--lizenz-recherche-stand-2026-05).

---

## Inhaltsverzeichnis

1. [Strategische Entscheidung](#1-strategische-entscheidung)
2. [Pipeline-Überblick](#2-pipeline-überblick)
3. [Tool-Stack (recherchiert + EU-validiert)](#3-tool-stack-recherchiert--eu-validiert)
4. [Hardware & Setup](#4-hardware--setup)
5. [Stage 1 — 2D-Konzept (Flux/SDXL + Style-LoRA)](#5-stage-1--2d-konzept-fluxsdxl--style-lora)
6. [Stage 2 — Image-to-3D (Hunyuan3D-2.1 primär)](#6-stage-2--image-to-3d-hunyuan3d-21-primär)
7. [Stage 3 — Blender-Cleanup (automatisiert)](#7-stage-3--blender-cleanup-automatisiert)
8. [Stage 4 — Texturing + Materialien](#8-stage-4--texturing--materialien)
9. [Stage 5 — Rigging + Animation](#9-stage-5--rigging--animation)
10. [Stage 6 — Unity-Import](#10-stage-6--unity-import)
11. [Stage 7 — Audio (Musik + SFX + Voice)](#11-stage-7--audio-musik--sfx--voice)
12. [Asset-Kategorien & Budgets (Neon-Arcade)](#12-asset-kategorien--budgets-neon-arcade)
13. [Stil-Konsistenz (Neon-Arcade)](#13-stil-konsistenz-neon-arcade)
14. [EU-Compliance & Lizenz-Recherche (Stand 2026-05)](#14-eu-compliance--lizenz-recherche-stand-2026-05)
15. [Pilot-Plan (7 Pilots vor Skalierung)](#15-pilot-plan-7-pilots-vor-skalierung)
16. [Output-Ablage + Versionierung](#16-output-ablage--versionierung)
17. [Risiken & Mitigation](#17-risiken--mitigation)
18. [Verweise](#18-verweise)

---

## 1. Strategische Entscheidung

3D-Asset-Generierung mit KI ist 2026 für **stilisierte Neon-Arcade-Charaktere/Props** Production-reif. Wir setzen es als Standard-Pipeline, nicht als Notlösung. Aufwändigere Boss-Modelle für Cinematics gehen optional über Cloud-Services (Rodin/Meshy).

**Kern-Entscheidungen (verbindlich):**

- **Hunyuan3D-2.1 als validierter Primärpfad** für Image-to-3D (Entscheidung 2026-06-06, per Render belegt — sauber PBR-texturiert, ~150 s/Asset auf RTX 4080 16 GB via mmgp-Offload). **EU-Lizenz-Caveat:** Rechtsrisiko bewusst akzeptiert, vor kommerziellem Shipping erneut prüfen ([§14](#14-eu-compliance--lizenz-recherche-stand-2026-05)).
- **Keine Handarbeit als Pipeline-Schritt** (Entscheidung 2026-06-06: „probiere alles, aber wir wollen kein Handarbeit") — kein manuelles Retopo/Nachmodellieren/Texture-Paint in Blender. Automatisierte Alternativen: `decimate_glb.py`, Auto-Rigging (AccuRIG 2/Mixamo/Tripo), Cloud-Quad-Output (Rodin).
- **Lokale Pipeline primär**: isolierte 3D-ComfyUI-Instanz `D:\AI\Comfy3D_WinPortable` (Port 8189, torch 2.5.1/cu124) + **Standalone-Runner** (`hy3d_runner.py`, `stage2_partcrafter.py`, `decimate_glb.py`) statt ComfyUI-3D-Pack-Node-Graph. Maßgebliche Setup-Doku: `D:\AI\ComfyUI_workflows\STAGE2_3D_SETUP.md`.
- **TRELLIS.2-4B** nur Option bei 24-GB-Hardware oder Cloud (passt nicht auf die reale 16-GB-RTX-4080); **TRELLIS-1 qualitativ unzureichend** (matschige Textur-Bakes, per Render belegt). **PartCrafter** (MIT) für Segmentierung (1 Bild → N Teil-Meshes), aber texturlos.
- **Cloud-Services für Production**: Meshy 6 oder Rodin Gen-2.5 für die ~10-15% Assets, wo die lokale Qualität nicht reicht oder Auto-Rigging beschleunigt.
- **Tripo 3.0** als optionales Komplett-Werkzeug mit integriertem Auto-Rigging (Cloud, SaaS).
- **Audio**: Stable Audio 3 (Open-Weight, lizenzierte Trainingsdaten) als Default. Suno wegen ungeklärter Trainingsdaten-Lawsuits **gemieden**.
- **Animation**: Cascadeur für AI-assistierte Keyframes, DeepMotion/RADiCAL für Video-to-Motion. Mixamo bleibt für Standard-Humanoid-Skeletts.
- **Runner-Skripte + Konzept-Workflows versioniert** unter `D:\AI\ComfyUI_workflows\` (Runner, projektübergreifend) bzw. `D:\AI\ComfyUI_workflows\bomberblast_unity\` (projekt-spezifisch) mit Git-LFS.
- **Output-Format:** GLB durchgängig — Unity-Import via **glTFast** (`com.unity.cloud.gltfast`); FBX nur für den Mixamo-Animations-Roundtrip ([§10](#10-stage-6--unity-import)).
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
│ Stage 2: Image-to-3D (Standalone-Runner, isolierte Instanz)        │
│  Primär: Hunyuan3D-2.1 (hy3d_runner.py) — Shape + PBR-Textur       │
│  Segmentierung: PartCrafter (stage2_partcrafter.py, texturlos)     │
│  Alternativen: SPAR3D / TripoSG · Option: TRELLIS.2-4B (24 GB)     │
│  Cloud-Fallback: Rodin Gen-2.5 / Meshy 6 (Problemfälle)            │
│  → GLB/OBJ mit PBR-Texturen (BaseColor, Normal, MRA)               │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 3: Blender-Cleanup (automatisiert, decimate_glb.py)          │
│  Decimate auf Tris-Budget, UV/Texturen erhalten, GLB-Re-Export     │
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
│ Stage 5: Rigging + Animation (humanoide Helden + Wardens)          │
│  Auto-Rig: Mixamo / AccuRIG 2 / Tripo Auto-Rig (universal)         │
│  Animation: Cascadeur (AI-Posing) ODER DeepMotion (Video-to-Motion)│
│  → FBX-Set: Idle, Walk, Run, Bomb-Place, Detonate, Hit, Death,     │
│    Victory (8 Clips, geteilt über die 5 Helden, §9.3)              │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Stage 6: Unity-Import (Unity 6000.4.8f1 + URP 17.0.4)              │
│  GLB via glTFast (com.unity.cloud.gltfast), Addressables-Gruppe,   │
│  LOD-Group, Material-Setup, Layer, Collider, Prefab-Variant        │
└────────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌──────────────────────────┐    ┌────────────────────────────┐
│ Stage 7: Audio           │    │ Final: AssetReview Scene   │
│ Stable Audio 3 (Musik)   │    │ Cinematic-Lighting-Test    │
│ Stable Audio 3 (SFX)     │    │ Mobile-Performance-Profile │
│ (Voice: deferred)        │    │ Build-Smoke (Android-AAB)  │
└──────────────────────────┘    └────────────────────────────┘
```

---

## 3. Tool-Stack (recherchiert + EU-validiert)

### 3.1 Primär — Lokal (Standalone-Runner auf isolierter 3D-Instanz)

| Tool | Version (Mai 2026) | Lizenz | Rolle | URL |
|------|---------------------|--------|-------|-----|
| **Hunyuan3D-2.1** (Tencent) | 2.1 | Tencent Community — Achtung: EU/UK/SK kommerziell ausgeschlossen ([§14.1](#141-hunyuan3d--lizenz-lage--bewusste-entscheidung)) | **Primärer Image-to-3D-Pfad** (Shape + PBR, validiert 2026-06-06) | github.com/Tencent-Hunyuan/Hunyuan3D-2.1 |
| **PartCrafter** | (2025) | MIT | Segmentierung — 1 Bild → N Teil-Meshes (texturlos) | github.com/wgsxm/PartCrafter |
| **ComfyUI** (2D-Instanz, Port 8188) | 0.3.x (laufend) | GPL-3.0 (Tool) | Stage-1-Konzepte (SDXL/Flux) | github.com/comfyanonymous/ComfyUI |
| **TRELLIS 2** (Microsoft) | CVPR'25 + 2.0 update | MIT | Nur Option bei 24-GB-Hardware/Cloud — passt nicht auf die 16-GB-4080. TRELLIS-1 qualitativ unzureichend (matschig, per Render belegt) | github.com/microsoft/TRELLIS.2 |
| **SPAR3D** (Stability AI) | 1.0 (Jan 2025) | Stability Community ≤ $1M Umsatz | Alternative — Punktwolke + schnelle Edits (<1s) | github.com/Stability-AI/stable-point-aware-3d |
| **Stable Fast 3D (SF3D)** | 1.0 | Stability Community | Schnelle Vorschau | huggingface.co/stabilityai/stable-fast-3d |
| **TripoSG** (VAST) | 1.5B Params (Mar 2025) | OSS, kommerziell OK | Foundation-Modell Single-Image | github.com/VAST-AI-Research/TripoSG |
| **TripoSF** (VAST) | (Mar 2025) | OSS, kommerziell OK | Open-Surface-Assets (Tuch, dünne Geometrie) | github.com/VAST-AI-Research/TripoSF |
| **InstantMesh** | 1.0 | Apache-2.0 | Multi-View → Mesh (Backup) | github.com/TencentARC/InstantMesh |
| **Stable Audio Open Small/Medium** | 3.0 (Mai 2026) | Stability Community ≤ $1M | Musik + SFX (lizenzierte Trainingsdaten!) | huggingface.co/stabilityai |
| **Blender** | 4.3+ | GPL | Cleanup, Decimation, Export | blender.org |

> Achtung — **Hunyuan3D-Lizenz-Lage:** Die Lizenz schließt EU/UK/Korea per Definition `Territory` aus (Source: [Hunyuan3D-2 LICENSE](https://github.com/Tencent-Hunyuan/Hunyuan3D-2/blob/main/LICENSE), bestätigt in [Issue #94](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1/issues/94)). **Hunyuan3D-2.1 wird trotzdem als Primärpfad genutzt** — Entscheidung 2026-06-06, Rechtsrisiko bewusst akzeptiert. **Vor kommerziellem Shipping erneut prüfen** ([§14.1](#141-hunyuan3d--lizenz-lage--bewusste-entscheidung)).

### 3.2 Cloud (Production, mit kommerzieller Lizenz)

| Service | Version (Mai 2026) | Preis | Lizenz | Stärke |
|---------|---------------------|-------|--------|--------|
| **Meshy** | 6 (Jan 2026) | $20-$60/Mo (Pro+) | Pro-Tier: Volle Commercial Rights | Schnelle Iteration, Unity-Plugin, Blender-Plugin |
| **Rodin** (Hyper3D) | Gen-2.5 | $0.40-$1.50/Asset, **Free Tier mit Commercial Rights** | Alle Tiers kommerziell | Beste PBR-Texturen, Quad-Mesh-Output |
| **Tripo3D** (Studio) | 3.0 | Tier-Pricing (SaaS) | Pro-Tier kommerziell | Komplett-Pipeline mit Auto-Rigging integriert |

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
| **DeepMotion Animate 3D** | SaaS, Tier-Pricing | Video-to-3D-Animation, Retargeting, Echtzeit | Eigene Video-Aufnahme = sauberste Lizenz |
| **RADiCAL** (von Autodesk übernommen Apr 2026) | SaaS | Video-to-Motion, Stream-fähig (Unity/Unreal) | Indie-Tier verfügbar |

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

### 4.1 Reale Workstation (Basis der Pipeline)

Die Pipeline läuft validiert auf der **realen Basis: RTX 4080 (16 GB VRAM)**. Hunyuan3D-2.1 passt
via mmgp-Offload (`LowRAM_LowVRAM`, CPU-Auslagerung) hinein (~150 s/Asset). Nur **TRELLIS.2-4B**
braucht 24 GB VRAM und ist deshalb auf Option-Status (GPU-Upgrade oder Cloud-Workstation, [§6.1](#61-modell-wahl-pro-asset-typ)).

| Komponente | Reale Basis (validiert) | Hinweis |
|-----------|--------------------------|---------|
| GPU | RTX 4080 (16 GB) | Hunyuan3D-2.1 (mmgp-Offload), PartCrafter, SPAR3D, TripoSG laufen; TRELLIS.2-4B (24 GB) nur via Upgrade/Cloud |
| RAM | 32 GB+ | mmgp lagert aufs System-RAM aus; 64 GB komfortabel für Blender-Hi-Poly |
| CPU | 8-16 Cores | parallele Blender-Decimations (headless) |
| Disk | NVMe, AI-Daten unter `D:\AI\` | C: knapp halten — TEMP/HF-Caches umgelenkt (siehe `STAGE2_3D_SETUP.md`) |

### 4.2 Isolierte 3D-Instanz + Standalone-Runner

**Maßgebliche Setup-Doku: `D:\AI\ComfyUI_workflows\STAGE2_3D_SETUP.md`** (Env, Builds,
Stolpersteine, Aufruf-Beispiele) — Setup nicht neu herleiten, sondern daraus reproduzieren.

- **Isolierte 3D-Instanz** `D:\AI\Comfy3D_WinPortable\` (Port 8189, eigenes `python_standalone`,
  Python 3.12, torch 2.5.1+cu124). Die produktive 2D-ComfyUI (`D:\AI\ComfyUI_windows_portable\`,
  Port 8188) bleibt unangetastet — ein 3D-Pack dort würde torch downgraden und CUDA-Extensions
  gegen die falsche ABI bauen.
- **Standalone-Runner statt ComfyUI-3D-Pack-Node-Graph** (Architektur-Entscheidung, verbindlich):
  Beide geprüften 3D-Pack-Stände importieren monolithisch — ein kaputter Node killt den ganzen
  Pack, für eine Batch-Asset-Pipeline ungeeignet. Der Node-Graph-Ansatz ist **verworfen**; die
  Modelle werden scriptbar standalone aufgerufen (alle unter `D:\AI\ComfyUI_workflows\`):
  - `hy3d_runner.py` — **Hunyuan3D-2.1** (Shape + PBR-Textur), Portable `D:\AI\HY3D2\Hunyuan3D2_WinPortable`
  - `stage2_partcrafter.py` — PartCrafter-Segmentierung (Repo `D:\AI\PartCrafter\`)
  - `decimate_glb.py` — Stage-3-Decimation (Blender headless)
  - `render_glb.py` — QA-Kontaktblatt-Renders (Blender headless)
- Das Hunyuan3D-Portable ist self-contained (eigenes `python_standalone`, torch 2.8.0+cu129) —
  unabhängig von der cu124-Instanz.

> Achtung — **VRAM-Gotcha (teuer gelernt):** Den Hunyuan-Runner **nie parallel zu einer laufenden
> ComfyUI** (Port 8188/8189) starten. Belegte VRAM zwingt mmgp das Shape-DiT auf die CPU — die
> Diffusion bricht um Faktor ~300 ein (ein 3,5-min-Asset lief >10 h fest). Vor Stage 2 alle
> ComfyUI-Prozesse beenden.

### 4.3 Modell-Downloads

| Modell | Größe | Ablage | Lizenz |
|--------|-------|--------|--------|
| Hunyuan3D-2.1 (Shape + Paint) | im Portable enthalten | `D:\AI\HY3D2\Hunyuan3D2_WinPortable\` | Achtung: Tencent Community — EU-Caveat ([§14.1](#141-hunyuan3d--lizenz-lage--bewusste-entscheidung)) |
| PartCrafter (+ RMBG-1.4) | ~2 GB | `D:\AI\PartCrafter\pretrained_weights\` | OK — MIT |
| TRELLIS-image-large (TRELLIS-1) | ~5 GB | HF-Cache (`D:\AI\_hf`) | OK — MIT (qualitativ unzureichend, nur Vergleichs-Referenz) |
| SPAR3D | ~2 GB | HF-Cache (`D:\AI\_hf`) | OK — Stability Community |
| Stable Fast 3D | ~1.5 GB | HF-Cache (`D:\AI\_hf`) | OK — Stability Community |
| TripoSG (1.5B) | ~3 GB | HF-Cache (`D:\AI\_hf`) | OK — OSS (VAST) |
| InstantMesh | ~1 GB | HF-Cache (`D:\AI\_hf`) | OK — Apache-2.0 |
| Flux.1-dev (für 2D) | ~24 GB | `D:\AI\ComfyUI_windows_portable\ComfyUI\models\checkpoints\` | Achtung: Non-commercial Default; Dev-Lizenz für interne Konzeptarbeit OK, kein redistribuierbarer Output ohne Pro |
| SDXL 1.0 base + refiner | ~13 GB | `D:\AI\ComfyUI_windows_portable\ComfyUI\models\checkpoints\` | OK — Stability Community |

> **Wichtig:** Für die finalen Konzeptbilder, die in den 3D-Generator gehen, **SDXL bevorzugt** (kommerziell sauber). Flux.1-dev nur für interne Iteration, finale Konzepte via SDXL+LoRA produzieren (oder Flux.1-pro mit kommerzieller Lizenz buchen).

### 4.4 Workflow-Ablage

```
D:\AI\ComfyUI_workflows\
├── STAGE2_3D_SETUP.md               (maßgebliche Setup-Doku, projektübergreifend)
├── hy3d_runner.py                   (Stage 2 — Hunyuan3D-2.1, geteilt)
├── stage2_partcrafter.py            (Stage 2 — PartCrafter-Segmentierung, geteilt)
├── decimate_glb.py                  (Stage 3 — Decimation, geteilt)
├── render_glb.py                    (QA — Kontaktblatt-Renders, geteilt)
└── bomberblast_unity\
    ├── 00_style_reference\
    │   ├── sector_neon_arcade\          (15-20 Style-Refs)
    │   ├── bomb_arcade_neon\
    │   ├── tile_sector_themes\
    │   └── boss_sector_themes\          (Wardens: Granite Warden/Frostwyrm/Magma Revenant/Null Phantom/The Overseer)
    ├── 01_concept_2d\                   (Stage 1 — Workflow-JSONs für die 2D-ComfyUI, Port 8188)
    │   ├── sdxl_hero_lora.json
    │   ├── flux_hero_iter.json          (interne Iteration, nicht Production-Output)
    │   └── concept_to_orthographic_views.json
    ├── 03_texture_refine\               (Stage 4, optional)
    │   ├── stable_diff_pbr_upgrade.json
    │   └── material_lora_apply.json
    ├── 04_audio\                        (Stage 7)
    │   ├── stable_audio_music.json
    │   └── stable_audio_sfx.json
    ├── pilot_log.md                     (Pilot-Phase-Erkenntnisse)
    └── README.md                        (Workflow-Auswahl-Guide)
```

Stage 2/3 brauchen **keine** Workflow-JSONs — sie laufen über die Standalone-Runner (§4.2).
Versionierung via Git-LFS für die JSONs und Style-References.

> Fußnote Pfade: `F:\AI` ist nur eine NTFS-Junction auf `D:\AI` — Pfadangaben in dieser Doku
> einheitlich `D:\AI`.

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

Achtung: **IP-Adapter funktioniert mit SDXL gut, mit Flux schlecht** — daher bei Flux-Path immer LoRA bevorzugen.

### 5.3 ControlNet für orthographische Views

Für Image-to-3D-Algorithmen sind **orthographische Single-Object-Views auf weißem BG** ideal. ControlNet mit `Canny` oder `Depth` aus einem groben Block-Sketch sichert die richtige Perspektive.

---

## 6. Stage 2 — Image-to-3D (Hunyuan3D-2.1 primär)

### 6.1 Modell-Wahl pro Asset-Typ

| Asset-Typ | Primär | Backup | Grund |
|-----------|--------|--------|-------|
| Held / humanoides Modell | **Hunyuan3D-2.1** | Cloud (Rodin Gen-2.5) | validiert: knackig PBR-texturierte Toon-Charaktere, kohärent rundum (auch Rückseite) |
| Gegner (12 Typen) | **Hunyuan3D-2.1** | Cloud (Rodin Gen-2.5) | wie Helden |
| Warden / Hi-Detail-Boss | **Hunyuan3D-2.1** | Rodin Gen-2.5 (Quad-Mesh) | Cloud-Polish nur bei Cinematic-Bedarf |
| Standard-Prop (Bombe, Power-Up, Kiste) | **Hunyuan3D-2.1** | SPAR3D / TripoSG | ein Pfad für alles hält Stil + Workflow konsistent |
| Modulares/segmentiertes Element | **PartCrafter** (Geometrie) + Re-Texturing | Hunyuan3D-2.1 | 1 Bild → N Teil-Meshes (texturlos) |
| Tuch / dünne Geometrie (Flagge, Banner) | **TripoSF** | Hunyuan3D-2.1 | TripoSF speziell für Open-Surface |
| Schnelle Vorschau / Skizzen | **Stable Fast 3D** | InstantMesh | Sekunden pro Asset |
| Flache Objekte (Floor-Tiles, Pads) | Unity-Primitive + Textur | — | Image-to-3D für flache Geometrie ungeeignet |

> **TRELLIS-Status:** **TRELLIS-1** ist qualitativ unzureichend (rauschig-matschige Textur-Bakes,
> Charaktere zerfallen bei Single-View — per Blender-Render belegt) und nur noch Vergleichs-Referenz.
> **TRELLIS.2-4B** (stärkstes EU-konformes Modell) braucht 24 GB VRAM und passt **nicht** auf die
> reale 16-GB-RTX-4080 — nur Option bei GPU-Upgrade oder Cloud-Workstation.

### 6.2 Hunyuan3D-2.1 — Default-Workflow

Runner: `D:\AI\ComfyUI_workflows\hy3d_runner.py` (Portable `D:\AI\HY3D2\Hunyuan3D2_WinPortable`,
Setup/Gotchas → `STAGE2_3D_SETUP.md`).

- Eingabe: PNG 1024², transparenter/weißer BG (Stage-1-Konzept).
- Ablauf: Shape (`Hunyuan3DDiTFlowMatchingPipeline`) + PBR-Textur (`Hunyuan3DPaintPipeline`) →
  texturiertes Mesh + PBR-Maps; danach `decimate_glb.py` → Unity-GLB ([§7](#7-stage-3--blender-cleanup-automatisiert)).
- Dauer: ~150 s/Asset auf RTX 4080 (16 GB, mmgp-Offload; Shape ~62 s + Textur ~105 s).
- **1 Prozess pro Asset** — und vorher alle ComfyUI-Instanzen beenden (VRAM-Gotcha, §4.2).

### 6.3 Batch-Generation

Sequentielles Skripting über den Standalone-Runner: Schleife über die Konzept-PNGs,
1 Prozess pro Asset, über Nacht laufen lassen. Kein Queue-Node/Node-Graph (verworfen, §4.2).

---

## 7. Stage 3 — Blender-Cleanup (automatisiert)

> **Keine Handarbeit** (Entscheidung 2026-06-06: „probiere alles, aber wir wollen kein Handarbeit") —
> kein manuelles Retopo/Nachmodellieren/Texture-Paint als Pipeline-Schritt. Hunyuan3D-2.1 liefert
> direkt sauber texturierte Meshes; die Reduktion aufs Polygon-Budget übernimmt `decimate_glb.py`
> (Blender headless, Decimate-Collapse, UV/Texturen bleiben erhalten). Zeigt ein Charakter beim
> Skinning Deform-Probleme: **Cloud-Quad-Output** (Rodin Gen-2.5 Quad-Mesh) bzw. Regenerieren
> mit anderem Seed/Konzept — nicht Hand-Retopo.

Automatisierte Kette pro Asset (`D:\AI\ComfyUI_workflows\decimate_glb.py`):

1. **Import** des Stage-2-Outputs (GLB bzw. Hunyuan-OBJ + PBR-Maps).
2. **Decimate-Collapse** auf Tris-Budget (`--target` aus Budget-Tabelle, [§12](#12-asset-kategorien--budgets-neon-arcade)) oder festen Faktor (`--ratio`).
3. **UV/Texturen erhalten** — PBR-Maps werden ins GLB eingebettet.
4. **Export GLB:** `D:\AI\3d_output\bomberblast_unity\unity_glb\{kategorie}\{asset_id}.glb`.

Konventionen, die der Skript-Durchlauf sicherstellt: Origin auf Boden-Mitte, 1 Unit = 1 Meter =
1 Unity-Unit, Normals Outside, neutrale Defaults bei fehlenden Maps (Normal #8080FF, Roughness 0.5,
Metal 0).

**Batch:** Schleife über die raw-Outputs, ~30 s pro Asset im Headless-Modus — gilt für Props **und**
Charaktere gleichermaßen (ein Pfad, keine Sonderbehandlung).

---

## 8. Stage 4 — Texturing + Materialien

### 8.1 Wann diesen Schritt brauchen?

- Hunyuan3D-2.1 liefert i.d.R. vollständige PBR-Maps — Refine nur bei Ausreißern; PartCrafter-Geometrie (texturlos) braucht diesen Schritt immer.
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
| **Non-humanoider Warden (Frostwyrm, Granite Warden, Multi-Cell)** | **AccuRIG 2** (AI Body-Detection, non-humanoid) ODER **Tripo Auto-Rig** (universal) — Fallback: **Cloud-Service** (Tripo 3.0 Komplett-Pipeline / Rodin) | Keine Handarbeit (Entscheidung 2026-06-06) — erst Auto-Rig-Versuch, bei Scheitern Cloud-Auto-Rig statt Hand-Rigging |

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

Insgesamt: 8 Animations-Clips (Walk und Run separat), geteilt über 5 Helden = 8 Basis-Clips + Skin-Varianten. Mixamo deckt alle ab.

### 9.4 Boss-Animations

Bosse brauchen größere Animation-Sets (Mehrkomponenten-Hitboxes, Phase-Wechsel). **Keine
Handarbeit** (Entscheidung 2026-06-06) — automatisierte Pfade:
- **Humanoide Wardens** (Magma Revenant, Null Phantom, The Overseer): Standard-Loops (Idle, Attack)
  aus Mixamo retargeted, Cinematics (Reveal, Phase-2-Transition) via **Cascadeur-AI-AutoPosing**
  oder **DeepMotion** (Video-to-Motion, eigene Aufnahme).
- **Non-humanoide Wardens** (Frostwyrm, Granite Warden): Auto-Rig (AccuRIG 2/Tripo, §9.1), Clips
  (Idle, Attack, Enrage, Death) via DeepMotion-Retargeting bzw. Cascadeur-AI-AutoPosing auf das
  Auto-Rig; bei Scheitern **Cloud-Service** (Tripo 3.0 Komplett-Pipeline) für Rig + Basis-Clips.
- Das Zeitbudget der Pilot-Tabelle (Warden, [§15](#15-pilot-plan-7-pilots-vor-skalierung)) deckt
  Modeling + Texturing + Auto-Rig ab.

---

## 10. Stage 6 — Unity-Import

### 10.1 Pro-Asset-Checkliste

> **GLB via glTFast** (`com.unity.cloud.gltfast` — gehört ins Paket-Soll der `manifest.json`):
> Unity importiert `.glb` nicht nativ, und der Umweg über FBX/OBJ **verliert die
> Metallic/Roughness-PBR-Maps**. FBX wird nur noch für den **Mixamo-Animations-Roundtrip**
> verwendet (Mesh hoch, animierte FBX zurück, Clips aufs glTFast-Modell retargeten).

- [ ] GLB in `Assets/_Project/Art/Models/{Kategorie}/` ablegen (glTFast-Import).
- [ ] **Import-Settings:** Scale prüfen (1 Unit = 1 m), `Read/Write = false`, Mesh-Kompression aktivieren.
- [ ] **Rig** (humanoide Helden/Wardens): Avatar über den Mixamo-FBX-Roundtrip, `Animation Type = Humanoid`.
- [ ] **Animation:** Mixamo-Clips (FBX) als Clip-Quellen, Loop für Idle prüfen, `Bake Into Pose: Root` bei Walk.
- [ ] **Materialien:** glTFast erzeugt URP-kompatible PBR-Materialien (Metallic/Roughness); bei Bedarf nach `Assets/_Project/Art/Materials/` extrahieren (`URP/Lit` bzw. `URP/Toon` falls Toon-Stil).
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
├── Enemies                 # 12 Gegner-Typen (lazy bei Level-Start)
├── Tiles_Sector{1..10}     # Pro Sektor eine Gruppe (lazy bei Level-Start)
├── Bosses_Standard         # 4 Standard-Bosse (lazy bei Boss-Level)
├── Bosses_Final            # The Overseer (Archetyp FinalBoss) + Duo-Varianten (lazy)
│                           # (Mini-Bosse brauchen keine eigene Group — Prefab-Variant des Sektor-Warden-Assets)
├── Environment_Sector{1..10} # Props pro Sektor
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

**Insgesamt:** ~80-100 SFX (Kern-Liste oben ~80, plus Reserve für einzelne Varianten/Stinger).
Generation in Batches à 20 SFX über Nacht.

### 11.3 Voice — DEFERRED (Original ist voice-los)

> Das produktive BomberBlast hat **keine** Voice (bewusstes "kein Geld"-Mandat). Im Remake bleibt Voice
> **deferred/optional**. Falls später eingeführt (bewusste Erweiterung), kämen in Frage:
- Announcer-Lines (6 Sprachen), Stinger-Vocals, optionale Boss-Roar-Samples
- Saubere Lizenzkette (Standard-Voices/Consent), Disclosure in Credits, Manual-QA-Pflicht

Bis dahin: Announcer/Feedback rein über SFX-Stinger (Cinematic-Bus), keine gesprochenen Lines.

### 11.4 Mastering

- Alle Tracks auf **−16 LUFS** (Mobile-Standard, EBU R128).
- Tool: **Adobe Audition** oder **iZotope Ozone** (lokal, kein KI nötig).
- Pro Track Pre-Master-Backup + Final-Master in `D:\AI\audio_output\bomberblast_unity\`.

---

## 12. Asset-Kategorien & Budgets (Neon-Arcade)

### 12.1 Polygon-Budgets (Mid-Tier-Android Ziel)

| Asset-Klasse | Anzahl | LOD0 | LOD1 | LOD2 | KI direkt? |
|--------------|-------:|-----:|-----:|-----:|------------|
| **Helden** (5 Charaktere) | 5 | 12 000 | 6 000 | 3 000 | OK + Mixamo |
| **Hero-Skins** (Coin-/Gem-Skins, Material-Variation) | ~20 | (Re-Tex) | — | — | OK — Re-Texturing |
| **Bomben** (14 Typen) | 14 | 1 500 | 800 | 400 | OK — Direkt |
| **Power-Ups** (12 Typen) | 12 | 1 000 | 500 | 250 | OK — Direkt |
| **Gegner** (12 Typen) | 12 | 4 000 | 2 000 | 1 000 | OK + Mixamo/AccuRIG |
| **Tiles/Blocks** (10 Sektoren × 4 Typen) | 40 | 800 | 400 | 200 | OK — Direkt, Tiling-Check |
| **Floor-Tiles** (10 Sektoren) | 10 | 400 | 200 | 100 | OK — Direkt |
| **Karten-FX-Meshes** (10 Spezial-Karten) | 10 | 1 500 | — | — | OK — Direkt |
| **Standard-Wardens** (Granite Warden, Frostwyrm, Magma Revenant, Null Phantom) | 4 | 18 000 | 9 000 | 4 500 | Achtung: humanoide (Magma Revenant/Null Phantom) + Mixamo; non-humanoide (Granite Warden/Frostwyrm) Auto-Rig (AccuRIG 2/Tripo, §9.1) |
| **The Overseer (FinalBoss) + Duo-Varianten** | 3 | 25 000 | 12 000 | 6 000 | Achtung: Hunyuan3D-Basis + Cloud-Polish |
| **Mini-Bosse** (L7/L17/.../L97 = 10 Stück) | 0 (Reskin) | — | — | — | Reskin — kein eigenes Modell, reskinter Sektor-Warden (50 % HP/Punkte) |
| **Environment-Props** (Crates, Pipes, Trash, Holo-Displays) | ~50 | 600 | 300 | 150 | OK — Direkt |
| **UI-3D-Hologramme** | ~20 | 500 | — | — | OK — Direkt |

**Total:** ~180 Modelle + ~30 Re-Texture-Varianten (~20 Hero-Skins + ~10 Mini-Boss-Reskins) =
**~210 Asset-Slots**. Die 10 Mini-Bosse (L7/L17/.../L97, Zehnerschritte) sind in den ~30
Re-Texture-Varianten enthalten — sie brauchen kein eigenes Modell, nur eine Material-/
Skalierungs-Variante des jeweiligen Sektor-Warden-Assets (50 % HP/Punkte).

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
| Voice-Lines (deferred — nicht budgetiert) | 44.1 kHz | 16-bit | Vorbis Quality 0.6 | ~80 KB pro Line |

**Total-Audio-Budget (ohne Voice — deferred):** ~30-40 MB (komprimiert). Zusammen mit ~180 3D-Modellen
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

### 14.1 Hunyuan3D — Lizenz-Lage + bewusste Entscheidung

Tencents Hunyuan3D-2 / 2.1 / 2.5 definiert in den Terms eine `Territory`-Klausel, die **EU, UK und
Südkorea explizit ausschließt** (siehe [Hunyuan3D-2 LICENSE](https://github.com/Tencent-Hunyuan/Hunyuan3D-2/blob/main/LICENSE),
[Issue #94 für 2.1](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1/issues/94)). Ursprünglich war
die Pipeline deshalb Hunyuan-frei geplant.

**Entscheidung 2026-06-06 (verbindlich):** Nach per Render belegtem Qualitätsvergleich (TRELLIS-1
matschig, PartCrafter texturlos, Hunyuan klar bestes Ergebnis — sauber PBR-texturiert, kohärent
rundum) ist **Hunyuan3D-2.1 der validierte Primärpfad** für Image-to-3D. Das EU-Lizenz-Risiko wird
**bewusst akzeptiert** („probiere alles").

**Caveat (Pflicht):** Vor kommerziellem Shipping erneut prüfen — Optionen dann: schriftliche
Tencent-Sonderfreigabe einholen, ODER betroffene Assets über TRELLIS.2-4B/Cloud-Services
regenerieren. Die Asset-Metadata-JSONs ([§14.4](#144-lizenz-archiv), [§16](#16-output-ablage--versionierung))
dokumentieren das Tool pro Asset und machen betroffene Assets auffindbar und regenerierbar.

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
D:\AI\Licenses\bomberblast_unity\
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

## 15. Pilot-Plan (7 Pilots vor Skalierung)

| # | Pilot-Asset | Kategorie | Pipeline-Test | Erfolgs-Kriterium |
|---|-------------|-----------|---------------|-------------------|
| 1 | Held "Default" | Held | SDXL+LoRA → Hunyuan3D-2.1 → decimate_glb.py → Mixamo-Rig | Animiert in Unity, < 12k Tris LOD0, Neon-Style-LoRA hält |
| 2 | Bombe "Standard" | Bomb | Hunyuan3D-2.1 → decimate_glb.py → Unity (glTFast) | < 1.5k Tris, Emissive-Glow funktioniert |
| 3 | Block "Destructible (Sektor 1)" | Tile | Hunyuan3D-2.1 → Tile-Check 4× nebeneinander | Naht-frei, < 800 Tris |
| 4 | Warden "Granite Warden" (Archetyp StoneGolem, non-humanoid) | Boss | Hunyuan3D-2.1 → decimate_glb.py → AccuRIG-2-Auto-Rig (Fallback Tripo/Cloud, §9.1) | Phase-1 + Enrage (Material-Swap), Multi-Cell-Hitbox |
| 5 | PowerUp "BombUp" + "Fire" | PowerUp | SDXL → Hunyuan3D-2.1 → URP + Glow | URP-Glow funktioniert, < 1k Tris |
| 6 | Sektor-1-Theme (2min) | Music | Stable Audio 3 + Mastering | LUFS −16 ±1, Loop sauber |
| 7 | SFX "Bomben-Explosion" + "Combo-Stinger" | SFX | Stable Audio (SFX) → Unity Audio-Bus/Spatial | Transienten sauber, Bus-Routing + Spatial OK |

> Kein Voice-Pilot — Voice ist deferred (Original ist voice-los). Der SFX-Pilot (#7) validiert
> die Audio-Bus-/Spatial-Pipeline.

**Zeitplan:** ~5 Arbeitstage Pilot — Held 1 Tag, Bombe+Block 1 Tag, Warden 1,5 Tage (Modeling +
Texturing + Auto-Rig — **keine Handarbeit**, §9), PowerUps 0,5 Tage, Musik 0,5 Tage, SFX 0,5 Tage.

**Output:** Lessons-Learned in `D:\AI\ComfyUI_workflows\bomberblast_unity\pilot_log.md` mit:
- Tatsächliche Generations-Zeit pro Asset
- Erfolgsquote (wie oft musste regeneriert werden)
- Polygon-Counts vor/nach Cleanup
- Texture-Qualitäts-Score (subjektiv 1-5)
- Probleme + Workarounds

**Skalierungs-Freigabe:** 7/7 Pilots OK (inkl. Musik + SFX) → Phase 2 Skalierung auf alle ~210
Asset-Slots. Bei 6/7 → Pipeline iterieren, dann erneut. Bei < 6/7 → Stack neu bewerten
(Cloud-Anteil erhöhen).

---

## 16. Output-Ablage + Versionierung

```
D:\AI\
├── ComfyUI_workflows\
│   ├── STAGE2_3D_SETUP.md           (maßgebliche Setup-Doku)
│   ├── hy3d_runner.py / stage2_partcrafter.py / decimate_glb.py / render_glb.py
│   └── bomberblast_unity\           (Konzept-Workflows + Style-Refs, Git-LFS)
├── 3d_output\
│   └── bomberblast_unity\
│       ├── concept_2d\              (Stage 1 Output)
│       ├── raw_glb\                 (Stage 2 Output, pre-Cleanup)
│       ├── unity_glb\               (Stage 3 Output → Unity-Import via glTFast)
│       └── metadata\                (JSON pro Asset: Lizenz, Prompts, Versionen)
├── audio_output\
│   └── bomberblast_unity\
│       ├── music\
│       ├── sfx\
│       └── voice\                   (deferred — leer bis Voice-Entscheidung)
├── animation_output\
│   └── bomberblast_unity\
│       ├── mixamo_fbx\              (FBX nur für den Animations-Roundtrip)
│       ├── cascadeur_export\
│       └── deepmotion_export\
├── Licenses\
│   └── bomberblast_unity\           (PDF-Archiv aller kommerziellen Lizenzen)
├── HY3D2\Hunyuan3D2_WinPortable\    (Hunyuan3D-2.1-Portable, self-contained)
└── Comfy3D_WinPortable\             (isolierte 3D-Instanz, Port 8189)
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
    "tool": "hunyuan3d_2.1",
    "version": "2.1",
    "input_png": "concept_2d/hero_default_v1.png",
    "raw_glb": "raw_glb/hero_default_v1.glb",
    "duration_seconds": 150
  },
  "stage_5_rig": {
    "tool": "mixamo",
    "skeleton": "humanoid_standard",
    "animations": ["idle", "walk", "run", "bomb_place", "death"]
  },
  "license_source": "Hunyuan3D-2.1 (Tencent Community, EU-Caveat) + Mixamo (Adobe Standard)",
  "license_archive": null,
  "compliance_status": "EU-Lizenz-Caveat — Re-Check vor kommerziellem Launch (§14.1)"
}
```

---

## 17. Risiken & Mitigation

| Risiko | Wahrscheinlichkeit | Auswirkung | Mitigation |
|--------|-------------------|------------|------------|
| Hunyuan3D-Qualität reicht bei einzelnen Assets nicht | Niedrig-Mittel | Mittel | Cloud-Fallback Rodin Gen-2.5/Meshy 6 für die ~10-15 % Problemfälle |
| Stil-Drift über > 50 Assets | Mittel | Mittel | Style-LoRA Pflicht ab Pilot-Erfolg, festes Prompt-Template versioniert |
| Mixamo versagt bei nicht-standard-humanoidem Charakter | Hoch | Mittel | Tripo Auto-Rig oder AccuRIG 2 als Fallback |
| ASTC zu groß auf Mid-Tier-Android | Niedrig | Mittel | Texture-Atlas-Pflicht für Tiles/Props, Mip-Bias +1 pro Klasse |
| EU AI Act Transparenz-Pflicht missachten | Niedrig | Hoch | Play-Store-Description + Credits enthalten KI-Hinweis; Marketing-Material gekennzeichnet |
| Hunyuan-EU-Lizenz wird beim kommerziellen Launch zum Problem | Mittel | Hoch | Risiko bewusst akzeptiert (Entscheidung 2026-06-06); **Re-Check vor kommerziellem Shipping** (§14.1); Asset-Metadata dokumentiert Tool-Quelle pro Asset → betroffene Assets via TRELLIS.2-4B/Cloud regenerierbar |
| Suno/Udio-Lawsuits eskalieren | Hoch | — | **Suno/Udio vermieden**, Audio nur Stable Audio 3 + ElevenLabs |
| Polygon-Inflation (>200k Tris von KI) | Hoch (Default) | Niedrig | `decimate_glb.py` Pflicht, kein Asset ohne Cleanup ins Unity |
| Trainingsdaten-Bias (Charaktere sehen alle gleich aus) | Niedrig | Mittel | Style-Reference-Set pro Held diversifizieren, Pro-Held einzelne Sub-LoRAs falls nötig |
| Tile-Naht-Probleme (Repeating Patterns) | Mittel | Mittel | Pre-Gen Symmetrie-Prompt, Post-Gen Naht-Heal in Blender |
| Audio-LUFS-Inkonsistenz zwischen Sektoren | Mittel | Mittel | Master-Pass mit iZotope Ozone als Pflicht-Schritt |
| GPU-Lieferengpass (RTX 50xx-Knappheit Mai 2026) | Mittel | Mittel | Cloud-Workstation (RunPod, vast.ai) als Backup-Plan — zugleich der Pfad für TRELLIS.2-4B (24 GB) |

---

## 18. Verweise

### Projekt-interne Docs

| Bereich | Datei |
|---------|-------|
| Master-Plan | [PLAN.md](PLAN.md) |
| Conventions | [CLAUDE.md](CLAUDE.md) |
| Tech-Architektur (URP, LOD, Addressables) | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Game-Design (Helden, Wardens, Sektoren) | [DESIGN.md](DESIGN.md) |
| Content-Reuse-Map (Original → Unity) | [PARITY.md](PARITY.md) |
| Roadmap | [ROADMAP.md](ROADMAP.md) |

### Tool-URLs (verifiziert Mai 2026)

- Hunyuan3D-2.1 (Tencent): `https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1`
- PartCrafter: `https://github.com/wgsxm/PartCrafter`
- ComfyUI: `https://github.com/comfyanonymous/ComfyUI`
- ComfyUI-3D-Pack: `https://github.com/MrForExample/ComfyUI-3D-Pack` (verworfen — monolithischer Node-Graph-Import, §4.2)
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

- Maßgebliche Setup-Doku: `D:\AI\ComfyUI_workflows\STAGE2_3D_SETUP.md`
- Standalone-Runner: `D:\AI\ComfyUI_workflows\hy3d_runner.py` / `stage2_partcrafter.py` / `decimate_glb.py` / `render_glb.py`
- Konzept-/Audio-Workflows: `D:\AI\ComfyUI_workflows\bomberblast_unity\`
- Asset-Output: `D:\AI\3d_output\bomberblast_unity\` (Unity-Import aus `unity_glb\`)
- Audio-Output: `D:\AI\audio_output\bomberblast_unity\`
- Animation-Output: `D:\AI\animation_output\bomberblast_unity\`
- Pilot-Log: `D:\AI\ComfyUI_workflows\bomberblast_unity\pilot_log.md`
- Lizenz-Archiv: `D:\AI\Licenses\bomberblast_unity\`
- Hunyuan3D-Portable: `D:\AI\HY3D2\Hunyuan3D2_WinPortable\` · Isolierte 3D-Instanz: `D:\AI\Comfy3D_WinPortable\` (Port 8189)

> `F:\AI` ist nur eine NTFS-Junction auf `D:\AI` — Pfadangaben einheitlich `D:\AI`.
