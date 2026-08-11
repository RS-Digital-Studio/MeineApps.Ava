# HandwerkerImperium-Unity

> **Neuentwicklung von HandwerkerImperium in Unity 6 (LTS), parallel zur Avalonia-Version.**
> **NEUAUSRICHTUNG (8.6.2026):** ein **eigenständiger 3D-Walk-around-Idle-Tycoon** (Stil: My Perfect Hotel /
> My Mini Mart / Idle Office Tycoon) — gleiches Thema (Handwerk) & Personal (Meister Hans), aber genre-typische
> Schleife: Avatar läuft durch die Werkstatt-Stadt, sammelt Cash, stellt Arbeiter an, baut Werkstätten aus,
> saniert die Stadt, expandiert (Prestige = neue Stadt). **Mechanik darf vom Avalonia-Original abweichen.**
> Verbindlicher Spiel-Plan: **[3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md)**. Das Avalonia-Original bleibt produktiv.

| | |
|---|---|
| **Status** | Pre-MVP — **P0 komplett + headless verifiziert**: spielbare 3D-Welt an die Runtime gekoppelt, Android-APK baut (IL2CPP/ARM64), P1–P4-Domain-Logik gebaut + getestet (~181 NUnit grün). Offen: Ads/IAP-SDK, 6-Sprachen-Lokalisierung, APK-Größe, Beta/Store/KPI/Cutover |
| **Engine** | Unity 6000.4.8f1 (LTS) + URP 17.0.4 + IL2CPP |
| **Plattform** | Android (Phase 1), iOS (Phase 2) |
| **Stack** | VContainer + UniTask + Addressables + Firebase + TextMesh Pro + Cinemachine + DOTween |
| **Avalonia-Original** | Produktiv unter [`../HandwerkerImperium/`](../HandwerkerImperium/) — ~28k LOC C#, 91 Services, 77 Models, 80 ViewModels, 74 Views |
| **Persona-Anker** | "Meister Hans" (~1500 Voice-Files via ElevenLabs-Standard-Voice in 6 Sprachen, kein Cloning) |
| **Asset-Pipeline** | KI-basiert (real **Hunyuan3D** — 44 GLBs gebaut). EU-Lizenz-Konflikt offen (siehe [EU AI Act](#eu-ai-act-compliance)); TRELLIS-2-Pipeline = dokumentierte EU-konforme Fallback-Variante |

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

### Design-Quelle & Werte-Referenz

| Datei | Beschreibung |
|-------|--------------|
| [3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md) | **Verbindliche Design-Quelle (GDD)** der 3D-Idle-Neuausrichtung — Loop, Systeme, Monetarisierung, Roadmap. Mechanik darf bewusst vom Avalonia-Original abweichen. |
| [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) | **Referenz für wiederverwendete Formeln** (Income-Soft-Cap/Log2, Offline-Staffel, Auto-Produktion), direkt aus dem Avalonia-Code extrahiert. **Nicht mehr global verbindlich** — wo der GDD eine Original-Formel wiederverwendet, gelten deren Werte; neue Genre-Mechanik weicht bewusst ab. |

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
Ein **eigenständiges 3D-Walk-around-Idle-Tycoon-Spiel** (Stil: My Perfect Hotel / My Mini Mart / Idle Office
Tycoon). Der Spieler erbt Meister Hans' Werkstatt und baut ein Imperium aus 10 Handwerks-Werkstätten in einer
**wachsenden Toon-Cartoon-Stadt** auf — der Avatar läuft durch den Hof, sammelt Cash, stellt Arbeiter an, baut
Werkstätten/Plots aus, saniert die Stadt und expandiert (max. 3 Prestige). Verbindlicher GDD:
[3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md).

**Wie es sich von Avalonia unterscheidet:**

> **Wichtig:** Die Unity-Version ist ein **eigenständiges Spiel** — die **Mechanik weicht bewusst ab**
> (Avatar läuft & sammelt, Arbeiter-Automatisierung, Plot-Ausbau, Stadt-Wiederaufbau, max. 3 Prestige). Gleich
> bleiben nur **Thema** (Handwerk) und **Personal** (Meister Hans). Die folgende Tabelle vergleicht nur die
> **Präsentation** (2D→3D, Hub, Cinematics, Audio, Input, UI-Tech) — sie ist **keine** Aussage über Mechanik-Gleichheit.

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

## Designentscheidungen (Stand vor Neuausrichtung — Mechanik-Zeilen siehe GDD)

> Die **Mechanik-Zeilen** (Loop, Prestige, Worker-Tiefe) sind durch die Neuausrichtung abgelöst — verbindlich ist
> der [GDD](3D_IDLE_GAME_PLAN.md). Die **Präsentations-Zeilen** (Low-Poly, Hub-Stadt, 3D-Worker, Audio) gelten weiter.

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
| **Asset-Pipeline** | **Real Hunyuan3D** (44 GLBs gebaut) — EU-Lizenz-Konflikt **offenes Compliance-Risiko** (siehe [EU AI Act](#eu-ai-act-compliance)). TRELLIS 2 + ElevenLabs = dokumentierte EU-konforme Fallback-Variante |

---

## Spielmechanik in 60 Sekunden

**Sekunde-zu-Sekunde (der eigentliche Loop):**
1. Stationen produzieren sichtbar Waren
2. Avatar läuft hin, nimmt automatisch einen Trag-Stapel auf
3. Avatar trägt die Ware zum Tresen, lädt ab → Kunden bedienen → Geld spawnt physisch
4. Avatar läuft über das Geld → Auto-Pickup (Sammelradius upgradebar)
5. Hold-to-Pay-Pad → Upgrade (Tempo / Kapazität / Sammelradius)
6. Hold-to-Pay an gesperrtem Plot → neue Werkstatt/Distrikt schaltet auf
7. Arbeiter anstellen → NPC übernimmt Tragen/Bedienen (Automatisierung) → die Kette läuft ohne den Spieler

**Meta (über Sessions):**
- **Stern-Rating** der Stadt (1→5★) steigt durch Werkstätten + sanierte Distrikte + Auftragsvolumen → Distrikt-Gate
- **Offline-Verdienst** beim Wiederkommen (gedeckelt, per Ad verdoppelbar)
- **Prestige = Akt-Finale** bei 5★ → permanenter Multiplikator + Umzug. **Maximal 3×** (4 Städte), selten & zeremoniell

**Langzeit (Monate):**
- **Meisterschafts-Track** (kontoweit, nie reset) — das permanente Rückgrat
- **Master-Tools** + **Imperium-Marken-Perkboard** (permanente Boni)
- **Endgame-Meistergrade** (Soft-Infinite nach dem 3. Prestige)

Langzeit-/Prestige-Modell: [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md). Vollständiger GDD: [3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md).

---

## Technologie-Stack im Detail

| Komponente | Wahl | Begründung |
|------------|------|------------|
| **Engine** | Unity 6000.4.8f1 (LTS) | Gleiche Version wie ArcaneKingdom |
| **Sprache** | **C# 9 / netstandard 2.1** | Unity-6000.4.8f1-Default — file-scoped Namespaces, records, Collection-Expressions `[…]` brechen (siehe CLAUDE.md §2/§4). Erlaubt: block-Namespaces, pattern matching, `new[]{}` |
| **Scripting Backend** | IL2CPP | AOT für Mobile |
| **Render Pipeline** | URP 17.0.4 | 2D + 3D, Mobile-optimiert |
| **DI Container** | VContainer 1.16.9 | AOT-kompatibel (nicht Zenject!) |
| **Async** | UniTask 2.5.10 | GC-frei statt Task<T> |
| **Asset-Loading** | Addressables 2.9.1 | Phase-2: Remote Catalog |
| **Lokalisierung** | Unity Localization 1.5.11 | 6 Sprachen + TMP-Font-Assets |
| **Audio** | Unity AudioMixer | 1 API für alle Plattformen |
| **Animation** | Animator + DOTween + Timeline | UI + Mood-States + Cinematics |
| **Camera** | Cinemachine 3.x | Orbit + Pan + Shake (Unity-6-Default, API-inkompatibel zu 2.10) |
| **Text** | TextMesh Pro | Inline-Sprites + Rich Text + CJK-ready |
| **Input** | New Input System | Multi-Touch + Gesten |
| **Tests** | Unity Test Framework + NUnit | EditMode + PlayMode |
| **Backend** | Firebase Suite | Auth + RTDB + Functions + Analytics + Crashlytics + RC + FCM |
| **IAP** | Google Play Billing 6.x | Premium + Bundles |
| **Ads** | Google Mobile Ads | **~8 Rewarded-Placements** (GDD §9.1, kein Banner — idle-arcade-typisch Rewarded-getrieben) |

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
| Storage (APK/AAB) | <120 MB (**P4-Ziel** — aktueller Durchstich-APK ~536 MB; Texture-Compression ASTC + Strip-Pass stehen aus) |
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
- Suno/Udio gemieden (Trainingsdaten-Lawsuits)

> ⚠️ **Offenes Compliance-Risiko (Hunyuan3D):** Die real genutzte Asset-Pipeline setzt **Hunyuan3D** ein (44
> gebaute GLBs; auch das Schwesterprojekt BomberBlast.Unity nutzt Hunyuan3D). Dessen Lizenz schließt
> **EU/UK/Korea** aus und erfordert eine **schriftliche Tencent-Sonderfreigabe** — die „Hunyuan-frei"-Annahme ist
> damit faktisch **widerlegt**. **Entscheidung ausstehend** (extern): Sonderfreigabe einholen **ODER** EU-konform
> neu generieren (TRELLIS-2-Pipeline ist dokumentiert). Details: [ASSETS_AI.md § 14](ASSETS_AI.md).

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
