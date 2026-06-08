# HandwerkerImperium-Unity

> **Neuentwicklung von HandwerkerImperium in Unity 6 (LTS), parallel zur Avalonia-Version.**
> **NEUAUSRICHTUNG (8.6.2026):** ein **eigenständiger 3D-Walk-around-Idle-Tycoon** (Stil: My Perfect Hotel /
> My Mini Mart / Idle Office Tycoon) — gleiches Thema (Handwerk) & Personal (Meister Hans), aber genre-typische
> Schleife: Avatar läuft durch die Werkstatt-Stadt, sammelt Cash, stellt Arbeiter an, baut Werkstätten aus,
> saniert die Stadt, expandiert (Prestige = neue Stadt). **Mechanik darf vom Avalonia-Original abweichen.**
> Verbindlicher Spiel-Plan: **[3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md)**. Das Avalonia-Original bleibt produktiv.

| | |
|---|---|
| **Status** | Pre-MVP — Konzept-Phase, Foundation startet |
| **Engine** | Unity 6000.4.8f1 (LTS) + URP 17.0.4 + IL2CPP |
| **Plattform** | Android (Phase 1), iOS (Phase 2) |
| **Stack** | VContainer + UniTask + Addressables + Firebase + TextMesh Pro + Cinemachine + DOTween |
| **Avalonia-Original** | Produktiv unter [`../HandwerkerImperium/`](../HandwerkerImperium/) — ~28k LOC C#, 91 Services, 77 Models, 80 ViewModels, 74 Views |
| **Persona-Anker** | "Meister Hans" (~1500 Voice-Files via ElevenLabs-Standard-Voice in 6 Sprachen, kein Cloning) |
| **Asset-Pipeline** | KI-basiert, EU-konform (TRELLIS 2 + ComfyUI + Stable Audio + ElevenLabs) |

---

## Schnelleinstieg

### Erstes Mal hier?

0. **Spiel-Design (zuerst!):** Lies **[3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md)** — der verbindliche GDD der 3D-Idle-Neuausrichtung.
1. **Hintergrund:** [PLAN.md](PLAN.md) (alte Vision/Strategie — Tech gültig, Mechanik-Teil nur Referenz)
2. **Setup:** Folge [SETUP.md](SETUP.md) (Unity, Firebase, KI-Pipeline)
3. **Original-Sim als Referenz:** [DESIGN.md](DESIGN.md) / [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) (Werte/Formeln zum Wiederverwenden — nicht mehr Soll)
4. **Code:** Lies [CLAUDE.md](CLAUDE.md) (Conventions) und [ARCHITECTURE.md](ARCHITECTURE.md) (Tech-Details)
5. **Roadmap:** [ROADMAP.md](ROADMAP.md) (an GDD-Phasen anzugleichen)
6. **Assets:** [ASSETS_AI.md](ASSETS_AI.md) (KI-Asset-Pipeline + neuer Bedarf: Avatar/NPC/Stadt)

### Existierende Codebase anschauen

```bash
# Avalonia-Version (Referenz für Domain-Logik)
ls src/Apps/HandwerkerImperium/

# Unity-Version (dieses Projekt)
ls src/Apps/HandwerkerImperium.Unity/
```

### Bauen & Starten

```bash
# Unity-Editor öffnen (nach SETUP.md)
# 1. Boot.unity öffnen
# 2. Play drücken

# Build Android Dev
# In Unity: Build → Android Dev

# Build Android Release (AAB für Play Store Beta-Track)
# In Unity: Build → Android Release
```

---

## Dokumentations-Index

### Verbindliche Werte-Referenz

| Datei | Beschreibung |
|-------|--------------|
| [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) | **Single Source of Truth** — alle echten Mechaniken, Formeln und Balancing-Werte, direkt aus dem Avalonia-Code extrahiert. Jede Abweichung eines anderen Dokuments ist ein Fehler und auf diese Werte zu korrigieren. |

### Strategie & Planung

| Datei | Beschreibung |
|-------|--------------|
| [PLAN.md](PLAN.md) | Strategischer Plan: Vision, Tech-Stack, Architektur, was 1:1/umgebaut/neu, Roadmap-Übersicht, MVP, Risiken |
| [DESIGN.md](DESIGN.md) | Game Design Document: 37 Sektionen, alle Werte 1:1 aus ORIGINAL_WERTE.md, Meister-Hans-Persona, Handwerker-Stadt |
| [ROADMAP.md](ROADMAP.md) | 72-Wochen-Sprint-Plan: 8 Phasen, KI-Pipeline parallel, Milestones |

### Code & Conventions

| Datei | Beschreibung |
|-------|--------------|
| [CLAUDE.md](CLAUDE.md) | Projekt-Conventions: Namespaces, DI, MVVM-Light, Tests, bekannte Probleme |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Code-Level-Spec: VContainer-Reg, EventBus, Save-Pipeline, Firebase-Pfade |

### Assets

| Datei | Beschreibung |
|-------|--------------|
| [ASSETS_AI.md](ASSETS_AI.md) | KI-Asset-Pipeline: TRELLIS 2 + ComfyUI + Blender + Mixamo + Stable Audio + ElevenLabs, EU-konform |
| [SETUP.md](SETUP.md) | First-Time-Setup: Unity, Firebase, ComfyUI, ElevenLabs, Adobe CC (folgt) |

---

## Projekt-Vision in 60 Sekunden

**Was wir bauen:**
Ein **3D-stylized Idle-Incremental-Game** mit aktiven Mini-Games. Der Spieler erbt Meister Hans' Werkstatt und baut ein Imperium aus 10 Handwerks-Werkstätten in einer **wachsenden Toon-Cartoon-Stadt** auf.

**Was es besser macht als Avalonia:**

> **Wichtig:** "Besser" heißt ausschließlich **Präsentation** (Grafik, 3D, Hub, Cinematics,
> Audio, Input, UI-Tech). Mechaniken, Formeln und Balancing-Werte sind **identisch** zum
> Avalonia-Original (siehe [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md)).

| Avalonia (Präsentation) | Unity (Präsentation) |
|----------|-------|
| 2D SkiaSharp-Renderer | **3D-Werkstatt-Welt** (10 Gebäude in lebender Stadt) |
| CPU-Partikel | **GPU-Particles** |
| C#-hardcoded Shader | **Shader Graph** (visuell editierbar) |
| Plattform-spezifische Audio-Impls | **Unity AudioMixer** (1 API, Ducking) |
| Statische Worker-Grafik | **Animierte 3D-Worker** (Mecanim, NavMesh) |
| Stille Spielfigur | **Meister-Hans-Voice** (~1500 Voice-Files in 6 Sprachen, ElevenLabs-Standard-Voice) |

**Migrations-Strategie:**
- Avalonia-Version bleibt im Play Store **aktiv und in Entwicklung**
- Unity-Version startet als **Closed Beta** unter eigener App-ID (`com.meineapps.handwerkerimperium2.beta`)
- Erst nach erfolgreicher Beta wird über Cutover entschieden

---

## Designentscheidungen (final, Stand Mai 2026)

| Frage | Entscheidung |
|-------|-------------|
| **Art-Direction** | Low-Poly Stylized (Township/Hay-Day-Stil) |
| **Hub-Layout** | Handwerker-Stadt mit allen 10 Werkstätten als Gebäude |
| **Worker-Style** | 3D-Charaktere mit Mecanim-Animationen (Walk/Idle/Work/Mood) |
| **Audio-Scope** | BGM + SFX + Meister-Hans-Voice in 6 Sprachen (ElevenLabs Standard-Voice, kein Cloning, keine Worker-Voice-Lines im MVP) |
| **Save-Slots** | 1 pro Account (wie Avalonia) |
| **Migration** | Closed Beta parallel zur Avalonia-Production |
| **iOS** | Erstmal nur Android — iOS-Entscheidung nach Beta-Erfolg (frühestens Monat 22-24) |
| **Live-PvP** | Phase 2: Photon Fusion Echtzeit-Klan-Matches (Monat 19-21, nach Beta-Erfolg) |
| **Save-Konverter Avalonia→Unity** | Nicht im MVP (Beta-Tester starten frisch) |
| **Asset-Pipeline** | KI-basiert (TRELLIS 2 für 3D, ElevenLabs für Voice, EU-konform, kein Hunyuan) |

---

## Spielmechanik in 60 Sekunden

**5-Minuten-Loop:**
1. Werkstätten verdienen passiv Geld
2. Auftrag annehmen (3 Strategien: Safe/Standard/Risk)
3. Mini-Game spielen (13 Typen, 3D)
4. Auftrag abschließen → Reward
5. Investieren (Upgrade, Worker, Forschung)

**Stunden-Loop:**
- 10 Werkstätten leveln (Lv 1 → 1000, WorkshopMaxLevel = 1000)
- 10 Worker-Tiers (F → Legendary)
- 72 Research-Nodes (4 Branches)
- Reputation-Tier-Aufstieg (4 Tiers: Beginner → Industry Legend)

**Wochen/Monate-Loop:**
- 7 Prestige-Tiers (Bronze → Legende)
- 12 Master-Tools (+74% Income)
- Gilde + Co-op-Orders + 6 Bosse + Mega-Projekte (Cathedral, HQ)
- BattlePass (50 Tier, 30-Tage-Saison)
- 109 Achievements (17 Kategorien)
- 4 Saisons pro Jahr

**Endgame:**
- Nach 3× Legende → **Ascension**
- 6 Perks × 3 Levels = 54 AP
- Eternal-Mastery (+0.5% Income pro Prestige, max 50)

Vollständige Spec: [DESIGN.md](DESIGN.md).

---

## Technologie-Stack im Detail

| Komponente | Wahl | Begründung |
|------------|------|------------|
| **Engine** | Unity 6000.4.8f1 (LTS) | Gleiche Version wie ArcaneKingdom |
| **Sprache** | C# 12 | Modernes C# (records, pattern matching, primary ctors) |
| **Scripting Backend** | IL2CPP | AOT für Mobile |
| **Render Pipeline** | URP 17.0.4 | 2D + 3D, Mobile-optimiert |
| **DI Container** | VContainer 1.16.9 | AOT-kompatibel (nicht Zenject!) |
| **Async** | UniTask 2.5.10 | GC-frei statt Task<T> |
| **Asset-Loading** | Addressables 2.9.1 | Phase-2: Remote Catalog |
| **Lokalisierung** | Unity Localization 1.5.11 | 6 Sprachen + TMP-Font-Assets |
| **Audio** | Unity AudioMixer | 1 API für alle Plattformen |
| **Animation** | Animator + DOTween + Timeline | UI + Mood-States + Cinematics |
| **Camera** | Cinemachine 2.10+ | Orbit + Pan + Shake |
| **Text** | TextMesh Pro | Inline-Sprites + Rich Text + CJK-ready |
| **Input** | New Input System | Multi-Touch + Gesten |
| **Tests** | Unity Test Framework + NUnit | EditMode + PlayMode |
| **Backend** | Firebase Suite | Auth + RTDB + Functions + Analytics + Crashlytics + RC + FCM |
| **IAP** | Google Play Billing 6.x | Premium + Bundles |
| **Ads** | Google Mobile Ads | 13 Ad-Placements (1:1 Original, siehe DESIGN.md § 29.3) |

Vollständige Asmdef-Hierarchie, DI-Setup, Service-Lifetimes: [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Quickstart-Tasks

### Tag 1 (Setup)

1. Unity 6000.4.8f1 installieren
2. Repository klonen (oder fortfahren falls schon da)
3. Unity-Projekt unter `Unity/` anlegen
4. Folge [SETUP.md](SETUP.md) für komplettes First-Time-Setup
5. Firebase-Console: Neues Projekt `handwerkerimperium2-beta` anlegen
6. ElevenLabs Pro-Account einrichten + erste Meister-Hans-Voice-Sample aufnehmen

### Woche 1 (Foundation)

1. 7 Assembly-Definitions anlegen (siehe ARCHITECTURE.md § 2)
2. VContainer-DI mit Boot.unity
3. Firebase Anonymous Auth
4. Save-Service Stub
5. Erstes ScriptableObject: BalancingConfig
6. Style-LoRA-Training (parallel zur Code-Arbeit)

### Pilot-Phase (Woche 4-6)

5 KI-Pilot-Assets durchlaufen vollständige Pipeline:
- Carpenter Workshop Lv 1-5 (mit Modul-Split)
- C-Tier Worker mit 4 Mood-States
- Wooden Furniture (T2)
- Golden Hammer (Master-Tool mit Emissive)
- Sunny Day Plaza (City-Tile)

Plus:
- Audio-Pilot: Workshop-Idle-Loop
- Voice-Pilot: Meister-Hans "Bauauftrag bereit!" (DE)

**Skalierungs-Freigabe:** 5/5 Pilots OK → Phase 2 starten.

---

## Test-Strategie

| Layer | Framework | Coverage-Ziel |
|-------|-----------|---------------|
| **Domain** | NUnit (EditMode) | ≥ 80% |
| **Game** | NUnit + UnityTest (PlayMode) | ≥ 50% |
| **UI** | Manuell + UnityTest | Optional |
| **E2E** | Manuell + Cheats-Window | Pre-Release-QA |

Erwartete Test-Klassen (200+ Tests): siehe [PLAN.md § 13](PLAN.md).

---

## Performance-Budgets (Mid-Range-Mobile)

| Metrik | Ziel |
|--------|------|
| FPS Hub-Idle | 60 |
| FPS Workshop-Detail (3D) | 60 |
| FPS Mini-Game | 60 |
| Cold-Start | <3s |
| Memory (RAM) | <400 MB |
| Storage (APK/AAB) | <120 MB |
| Particle-Count gleichzeitig | <2.000 |

---

## Verzeichnis-Struktur

```
HandwerkerImperium.Unity/
├── README.md             ← diese Datei
├── PLAN.md               ← Strategischer Plan
├── DESIGN.md             ← Game Design Document
├── CLAUDE.md             ← Conventions für Claude Code
├── ARCHITECTURE.md       ← Tech-Details
├── ROADMAP.md            ← 72-Wochen-Plan
├── ASSETS_AI.md          ← KI-Asset-Pipeline
├── SETUP.md              ← First-Time-Setup
│
├── Unity/                ← Unity-Projekt (wird in Woche 1 angelegt)
│   ├── Assets/
│   │   ├── _Project/     ← Unser Code & Assets
│   │   ├── ThirdParty/   ← DOTween, Firebase
│   │   └── StreamingAssets/  ← Migrations-JSON aus Avalonia
│   ├── Packages/manifest.json
│   └── ProjectSettings/
│
└── Server/               ← Cloud Functions (TypeScript, ab Woche 36)
    ├── CloudFunctions/
    ├── DatabaseRules/
    └── SERVEROPS.md
```

---

## Externe Ablage (KI-Pipeline)

```
F:\AI\
├── ComfyUI\                            ← Lokales Setup (siehe SETUP.md)
├── ComfyUI_workflows\
│   └── handwerkerimperium_unity\       ← Workflow-JSONs für alle Stages
├── 3d_output\
│   └── handwerkerimperium_unity\       ← GLB-Output von TRELLIS 2 etc.
├── audio_output\
│   └── handwerkerimperium_unity\       ← Stable Audio + ElevenLabs Output
├── animation_output\
│   └── handwerkerimperium_unity\       ← Mixamo + Cascadeur
├── Licenses\
│   └── handwerkerimperium_unity\       ← Tool-Lizenz-PDFs (EU AI Act!)
└── Blender\
    └── scripts\
        ├── hwi_unity_batch_cleanup.py
        └── hwi_unity_workshop_modular.py
```

Vollständige Pipeline-Spec: [ASSETS_AI.md](ASSETS_AI.md).

---

## Git-Workflow

| Branch | Zweck |
|--------|-------|
| `master` | Avalonia-Hauptbranch (bleibt aktiv produktiv!) |
| `unity-main` | Unity-Hauptbranch (parallel zur Avalonia-Entwicklung) |
| `unity-feature/{xxx}` | Feature-Branches |
| `unity-bugfix/{xxx}` | Bug-Fixes |

**Commit-Convention:** `Unity-HWI: Kurze Beschreibung` (Prefix unterscheidet von Avalonia-Commits)

---

## EU AI Act Compliance

Diese App nutzt KI-generierte Assets (3D-Modelle, Texturen, Audio, Voice).

**Pflicht-Maßnahmen (EU AI Act, ab August 2026):**
- Play-Store-Description enthält KI-Hinweis
- In-App-Credits dokumentieren Tools
- Pro-Asset-Metadata mit `license_source`
- Lizenz-Archiv unter `F:\AI\Licenses\handwerkerimperium_unity\`
- Voice ausschließlich über ElevenLabs-Standard-Voice (von ElevenLabs lizenziert, kein Cloning, keine Sprecher-Freigabe nötig)
- Bewusst Hunyuan-frei (EU-Lizenz-Ausschluss)
- Suno/Udio gemieden (Trainingsdaten-Lawsuits)

Details: [ASSETS_AI.md § 14](ASSETS_AI.md).

---

## Lizenz-Hinweis

Dieses Projekt ist Teil des **MeineApps-Portfolios** (`Robert Schneider`).
Code: privat, nicht zur Weitergabe.
Assets (KI-generiert): vollständige kommerzielle Rechte, dokumentiert pro Asset-Metadata.

---

## Kontakt & Support

- **Maintainer:** Robert Schneider (`robert.schneider97@gmail.com`)
- **Repository:** Lokal unter `F:\Meine_Apps_Ava\` (App-Pfad: `src\Apps\HandwerkerImperium.Unity\`)
- **Documentation-Updates:** Alle Markdown-Dateien in diesem Ordner

---

## Nächste Schritte

1. Alle Doku-Dateien existieren und sind konsistent (verbindliche Werte in [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md))
2. **Setup durchführen:** Folge [SETUP.md](SETUP.md)
3. **Pilot-Assets starten** (parallel zu Foundation): 5 Pilots gemäß ASSETS_AI.md § 15
4. **Code-Foundation:** Woche 1-8 gemäß ROADMAP.md
5. **Pilot-Review nach Woche 6:** Go/No-Go für Skalierung
