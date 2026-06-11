# BomberBlast 3D — SETUP (Phase 0: Projekt-Scaffolding)

> **Zweck:** Schritt-für-Schritt vom leeren Ordner zu einem lauffähigen Unity-6-Skelett mit Asmdefs, DI,
> CI und Daten-Pipeline — der Unterbau für den [Vertical-Slice](VERTICAL_SLICE.md). Richtung/Design →
> [PLAN.md](PLAN.md) · [DESIGN.md](DESIGN.md); Tech-Tiefe → [ARCHITECTURE.md](ARCHITECTURE.md);
> Conventions/Stolperfallen → [CLAUDE.md](CLAUDE.md).
> **Stand:** v0.5 (2026-06-08) — modernes 3D-Bomberman, aktiv gespielt, kein Idle/AFK.
>
> **Definition of Done (Phase 0):** Projekt öffnet ohne Fehler · URP aktiv · alle Asmdefs kompilieren
> (Domain ohne Unity-API) · Boot-Scene lädt über VContainer · 1 EditMode-Test grün · CI läuft auf Push ·
> `BalancingConfig` lädt aus `Resources/Data/*.json` via Importer.

---

## 0. Voraussetzungen (einmalig installieren)

| Tool | Version / Hinweis |
|------|-------------------|
| **Unity Hub** | aktuell |
| **Unity Editor** | **Unity 6 LTS (6000.x)** — exakte Patch-Version aus dem Hub notieren und in §1.2 pinnen |
| Unity-Module | **Android Build Support** (inkl. **OpenJDK + SDK + NDK**), **IL2CPP**, optional iOS/Mac/Linux |
| **Git** + **Git LFS** | `git lfs install` einmal pro Maschine |
| **Node.js LTS** | erst für Cloud Functions später (Phase 3+) |
| IDE | Rider **oder** VS 2022 mit „Game development with Unity" |
| (Art, optional) | Blender 4.x, ComfyUI — siehe [ASSETS_AI.md](ASSETS_AI.md) |

> **Keystore:** Release-Signing nutzt denselben `Releases\meineapps.keystore` wie das Avalonia-Portfolio
> (Alias `meineapps`). Erst ab Release-Builds relevant; in Phase 0 reicht der Debug-Keystore.

---

## 1. Projekt anlegen

### 1.1 Editor & Template

> **Ist-Stand:** Das Projekt existiert bereits unter `…\BomberBlast.Unity\Unity\`
> (6000.4.8f1, noch ohne `Library/` — der Editor wurde noch nie geöffnet,
> Editor-Open-Verifikation steht aus). Die Anleitung unten gilt nur für ein Neuaufsetzen.

1. Unity Hub → **New Project** → Template **Universal 3D (URP)** → Unity 6 LTS.
2. **Achtung Ordnername:** Der Hub erzeugt `Location\Projektname`. Damit die Projektwurzel
   **`…\BomberBlast.Unity\Unity\`** heißt, als Projektname **`Unity`** wählen (Location:
   `F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\`) — **oder** nach der Anlage den Ordner
   `BomberBlast` → `Unity` umbenennen (bei geschlossenem Editor).
3. Editor schließen, bevor Git/Asmdefs angelegt werden.

### 1.2 Versions-Pinning + Pflicht-Settings (siehe CLAUDE.md)

- `Unity/ProjectSettings/ProjectVersion.txt` enthält die exakte Editor-Version → **diese Patch-Version**
  ist die Wahrheit; alle Maschinen/CI nutzen exakt sie.
- In `Unity/Packages/manifest.json` **jede** Version auf konkrete Patch-Version festnageln (keine
  `x`-Platzhalter, keine ungewollten Pre-Releases). Realer Stand → §2.
- **Asset-Serialisierung „Force Text" (Pflicht):** `Edit → Project Settings → Editor →
  Asset Serialization → Force Text` — Szenen/Assets bleiben YAML-Text, diffbar und
  UnityYAMLMerge-fähig (Grund, warum `*.unity` **nicht** in LFS liegt, §5).

---

## 2. Packages (`manifest.json`)

> **§2 ist die Single Source für Pakete** — [ARCHITECTURE.md §1.8](ARCHITECTURE.md) verweist hierher.
> Der Block unten ist der **real gepinnte Stand** aus
> [`Unity/Packages/manifest.json`](Unity/Packages/manifest.json). Die Versionen beim **ersten
> Editor-Open gegen den Package Manager verifizieren** (der Editor lief noch nie). Registry-Pakete
> (VContainer, UniTask, R3) via **OpenUPM**.

```jsonc
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [ "com.cysharp", "jp.hadashikick.vcontainer" ]  // com.cysharp deckt unitask + r3 ab (keine redundanten Unter-Scopes)
    }
  ],
  "dependencies": {
    "com.cysharp.r3":                       "1.3.0",    // Reactive (ReactiveProperty) — Kern-DLL via NuGetForUnity, s.u.
    "com.cysharp.unitask":                  "2.5.10",   // Async ohne Allocations
    "com.unity.addressables":               "2.9.1",    // Asset-Streaming/Asset-Packs
    "com.unity.cinemachine":                "3.1.4",    // Top-Down-Kamera
    "com.unity.inputsystem":                "1.19.0",   // neues Input System (Joystick/Bomb)
    "com.unity.localization":               "1.5.11",   // 6 Sprachen, Smart-String-Plurale
    "com.unity.mathematics":                "1.3.2",    // Vektoren/Fixed-Point-Hilfen
    "com.unity.mobile.notifications":       "2.4.3",    // lokale Notifications
    "com.unity.nuget.newtonsoft-json":      "3.2.2",    // JSON (Domain + Daten-Importer)
    "com.unity.render-pipelines.universal": "17.0.4",   // URP
    "com.unity.test-framework":             "1.5.1",    // EditMode/PlayMode
    "com.unity.timeline":                   "1.8.12",   // Cinematics
    "com.unity.ugui":                       "2.0.0",    // UGUI inkl. TextMeshPro
    "jp.hadashikick.vcontainer":            "1.16.9"    // DI
    // + IDE-Pakete (rider/visualstudio) und com.unity.modules.* — siehe reale Datei
  }
}
```

- **R3 (wichtig):** `com.cysharp.r3` via OpenUPM liefert nur die Unity-Integration — die
  **R3-Kern-DLL + Abhängigkeiten** kommen zusätzlich via **NuGetForUnity** (erst NuGetForUnity
  installieren, R3-NuGet-Paket ziehen, dann `com.cysharp.r3`). R3.Unity via OpenUPM allein
  reicht **nicht**.
- **Burst bewusst weggelassen** — erst bei Bedarf nachrüsten (Fixed-Point-Hot-Paths).
- **Entfernt gegenüber dem Template-Default:** UniRx (Entscheidung: R3),
  `com.unity.textmeshpro` (in `com.unity.ugui` enthalten), AI-Pakete, `com.unity.collab-proxy`.

**Per-Asset-Store/Manuell (nicht via manifest):**
- **DOTween** (UI-Tweens, Cinematics) — Asset-Store/„DOTween Pro" oder Free; nach Import `Setup`.
- **TextMeshPro** ist in Unity 6 Teil von UGUI (`com.unity.ugui`) — Essentials importieren.
- **NSubstitute** (Tests) — als DLL unter `Assets/_Project/Tests/Plugins/` (läuft im EditMode/Mono).
- **Firebase Unity SDK** — erst Phase 2/3 (`bomberblast-arena`), nicht in Phase 0.
- **Kein Photon / kein Netcode** — reiner Single-Player (siehe DESIGN §24, CLAUDE §3).

> Nach dem Pinnen: `git add Packages/manifest.json Packages/packages-lock.json` — die Lock-Datei
> mitcommitten (reproduzierbare CI-Builds).

---

## 3. Ordnerstruktur (`Unity/Assets/_Project/`)

```
Assets/_Project/
├── Scripts/
│   ├── Core/            (BomberBlast.Core.asmdef          — UniTask; autoReferenced aus)
│   ├── Domain/          (BomberBlast.Domain.asmdef        — Core, Newtonsoft; KEINE Unity-API)
│   ├── Game/            (BomberBlast.Game.asmdef          — Core, Domain)
│   ├── UI/              (BomberBlast.UI.asmdef            — Core, Domain, Game)
│   ├── LiveOps/         (BomberBlast.LiveOps.asmdef       — Core, Domain, Game)
│   ├── Bootstrap/       (BomberBlast.Bootstrap.asmdef     — alles)
│   └── Editor/          (BomberBlast.Editor.asmdef        — includePlatforms: Editor; Daten-Importer §7)
├── Tests/
│   └── Domain/          (BomberBlast.Domain.Tests.asmdef  — Core, Domain; UNITY_INCLUDE_TESTS)
├── ScriptableObjects/   (BalancingConfig.asset, Hero_*.asset, Bomb_*.asset, Warden_*.asset)
├── Prefabs/             (Bomber, Block, Bomb, Enemy_*, Warden_*, HUD)
├── Scenes/              (Boot.unity, Game.unity)
├── Art/                 (Models, Materials, Shaders, VFX — LFS)
├── Audio/               (Music, Sfx, Ambient — LFS)
├── Addressables/        (Group-Configs)
└── Resources/Data/      (worlds.json, balancing.json, … — Seed-Import-Quelle)
```

> Spiegelt das Schichten-Modell aus [CLAUDE.md §1](CLAUDE.md): **Bootstrap → UI/LiveOps →
> Game → Domain → Core.** `Domain` ist **Unity-frei** (testbar, CI-Gate). **Kein Multiplayer-Asmdef** (Single-Player).

---

## 4. Assembly Definitions

Realer Stand: **8 Asmdefs.**

| Asmdef | References | Besonderheit |
|--------|-----------|--------------|
| `BomberBlast.Core` | UniTask | reine POCOs/Enums/Math; `autoReferenced: false` |
| `BomberBlast.Domain` | Core, Newtonsoft | **`noEngineReferences: true`** (keine `UnityEngine`-DLL) → erzwingt Unity-Freiheit; `autoReferenced: false` |
| `BomberBlast.Game` | Core, Domain (+ Addressables, InputSystem, Localization, UniTask, VContainer) | MonoBehaviours, Sim-Treiber, Rendering-Adapter |
| `BomberBlast.UI` | Core, Domain, Game (+ TMP, UIElements, UGUI) | Binder + UI Toolkit/UGUI |
| `BomberBlast.LiveOps` | Core, Domain, Game | Daily/Weekly/Events/Shop/Rankings/Plattform-IAP |
| `BomberBlast.Bootstrap` | alle | Composition Root, Boot-Scene |
| `BomberBlast.Editor` | Core, Domain, Game, UI | `includePlatforms: ["Editor"]` — Heimat des Daten-Importers (§7) |
| `BomberBlast.Domain.Tests` | Core, Domain | liegt unter `Tests/Domain/`; `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, `nunit.framework.dll` precompiled |

> **Offen:** Ein PlayMode-Test-Asmdef (`BomberBlast.Tests.PlayMode` — + Game, UI) folgt mit dem
> ersten PlayMode-Test.

**`BomberBlast.Domain.asmdef` (real):**

```json
{
  "name": "BomberBlast.Domain",
  "references": [ "BomberBlast.Core", "Unity.Nuget.Newtonsoft-Json" ],
  "noEngineReferences": true,
  "autoReferenced": false
}
```

> CI-Gate (§6) bricht bei Schichten-/Zirkel-Verstößen oder `UnityEngine`-Nutzung in `Domain`.

---

## 5. Git: `.gitignore` + `.gitattributes` (LFS)

Beide Dateien liegen **real** unter `src/Apps/BomberBlast.Unity/` — [`.gitignore`](.gitignore)
und [`.gitattributes`](.gitattributes). Eckpunkte:

**`.gitignore`** (`src/Apps/BomberBlast.Unity/.gitignore`):

- Unity-Editor-Artefakte mit `Unity/`-Prefix (`Unity/Library/`, `Unity/Temp/`, `Unity/Obj/`,
  `Unity/Build(s)/`, `Unity/Logs/`, `Unity/UserSettings/`, `Unity/[Mm]emoryCaptures/`,
  `Unity/*.csproj`, `Unity/*.sln`).
- Dieselben Patterns **zusätzlich ohne `Unity/`-Prefix** — Schutz gegen versehentliches
  Editor-Öffnen auf der falschen Ordner-Ebene (Streuner-Projekt-Schutz).
- `.~lock.*#` (LibreOffice-Locks von `prep/BalancingConfig.xlsx`), `*.user`, `.vs/`, `.idea/`, `.vscode/`.
- Das **Root-`.gitignore`** des Repos enthält Negations-Ausnahmen, damit
  `src/Apps/*/Unity/Packages/manifest.json`, `packages-lock.json` und `*.meta` unter `Assets/`
  nicht verschluckt werden.

**`.gitattributes`** (Git-LFS — Pflicht für Binär-Assets):

- **In LFS:** `*.psd/.ai/.png/.jpg/.tga` (Art), `*.fbx/.obj/.glb/.gltf` (3D),
  `*.wav/.mp3/.ogg` (Audio), `*.mp4/.webm` (Video), `*.xlsx` (Balancing-Workbook).
- **`*.unity` ist bewusst NICHT in LFS:** Szenen sind mit „Force Text"-Serialisierung (§1.2)
  YAML-Text — diffbar und UnityYAMLMerge-fähig. `*.meta` bleibt ebenfalls Text.

> Reihenfolge: erst `.gitattributes` committen, **dann** Assets hinzufügen — sonst landen Binaries nicht in LFS.

---

## 6. CI (GitHub Actions, game-ci)

Skeleton `.github/workflows/bomberblast-unity.yml` — EditMode-Tests + Determinismus-Gate bei jedem Push/PR:

```yaml
name: BomberBlast.Unity CI
on:
  push: { paths: [ "src/Apps/BomberBlast.Unity/**" ] }
  pull_request: { paths: [ "src/Apps/BomberBlast.Unity/**" ] }
jobs:
  tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { lfs: true }
      - uses: game-ci/unity-test-runner@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
        with:
          projectPath: src/Apps/BomberBlast.Unity/Unity
          testMode: EditMode
          # Determinismus-Replay-Suite läuft als EditMode-Test mit (Replay-Corpus → identischer Hash)
```

- **Secrets:** `UNITY_LICENSE` (Personal/Pro) in GitHub hinterlegen.
- Android-Build-Job (AAB) erst ab Beta ergänzen (`game-ci/unity-builder`, `targetPlatform: Android`).

---

## 7. Daten-Pipeline (Seed → ScriptableObject)

1. Seed-JSON nach `Assets/_Project/Resources/Data/` legen — Start-Set liegt vor in
   [`prep/seed/`](prep/seed/): `worlds.sector1.json`, `balancing.seed.json`.
2. `BalancingConfig` (ScriptableObject) + Importer (Editor-Menü `BomberBlast → Data → Import All`):
   liest JSON → schreibt `ScriptableObjects/*.asset`. **Alle Balancing-Werte leben hier — nie hardcoded**
   (Anti-Pattern, CLAUDE.md). Editierbare Quelle: [`prep/BalancingConfig.xlsx`](prep/BalancingConfig.xlsx)
   → bei Änderungen JSON neu exportieren.
3. Zahlen-Referenz (1:1 aus dem Original, dann tunen): [DESIGN.md §25](DESIGN.md) + Memory `balancing.md`.
4. **Build-Hygiene:** Die JSONs unter `Resources/Data/` sind **reine Editor-Import-Quelle** —
   alles unter `Resources/` landet sonst komplett im Build. **Festlegung:** Der Ordner bleibt
   `Resources/Data`, das Build-Skript (`BuildScripts.BuildAndroid*`) schließt ihn vor dem Build
   aus (einfachste Lösung: Ordner im Pre-Build-Schritt temporär nach `Assets/_Project/Data~/`
   verschieben — `~`-Suffix wird von Unity ignoriert — und nach dem Build zurück). Die
   Runtime-Quelle sind die importierten `ScriptableObjects/*.asset`, nicht die JSONs.

---

## 8. Boot-Scene (VContainer Composition Root)

Minimaler Einstieg (Details + Skelett → [VERTICAL_SLICE.md](VERTICAL_SLICE.md)):

1. `Scenes/Boot.unity` mit einem `LifetimeScope`-GameObject (`RootLifetimeScope`).
2. `RootLifetimeScope.Configure(builder)` registriert: `IRngProvider` (Default + `[Key("visual")]`),
   `IBalancingService` (lädt `BalancingConfig`), `ISceneLoader`, `ILogger<>`-Adapter.
3. Boot lädt additiv `Game.unity` mit `GameLifetimeScope` (Scoped: `GameSimulation`, `BattleController`).
4. Smoke-Test: Boot startet, `IBalancingService.Sektor(1)` liefert Daten → Log „Boot OK".

---

## 9. Reihenfolge (Phase-0-Checkliste)

- [x] Projekt (URP-Template) angelegt, Editor-Version gepinnt (6000.4.8f1) (§1)
- [x] `manifest.json` gepinnt + committed (§2) — `packages-lock.json` entsteht beim ersten Editor-Open
- [x] Ordnerstruktur + 8 Asmdefs (Domain `noEngineReferences`) (§3/§4)
- [x] `.gitignore` + `.gitattributes` (LFS) committed (§5)
- [ ] Editor-Open-Verifikation: Projekt öffnet ohne Fehler, Paket-Versionen gegen Package Manager prüfen (§1/§2)
- [ ] CI-Workflow grün (1 Dummy-EditMode-Test) (§6)
- [ ] Seed-JSON importiert, `BalancingConfig.asset` erzeugt (§7)
- [ ] Boot→Game-Szenenfluss über VContainer, „Boot OK"-Log (§8)
- [ ] **→ weiter mit [VERTICAL_SLICE.md](VERTICAL_SLICE.md)**

---

## Änderungslog

| Datum | Version | Änderung |
|-------|---------|----------|
| 2026-06-08 | v0.5 | Initiales Setup-Doc für die v0.5-Richtung (modernes 3D-Bomberman, kein Idle). |
