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

1. Unity Hub → **New Project** → Template **Universal 3D (URP)** → Unity 6 LTS.
2. Projektname **BomberBlast**, Location: `F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\` →
   Ergebnis-Pfad **`…\BomberBlast.Unity\Unity\`** (das `Unity/`-Unterverzeichnis ist die Projektwurzel).
3. Editor schließen, bevor Git/Asmdefs angelegt werden.

### 1.2 Versions-Pinning (Pflicht — siehe CLAUDE.md)

- `Unity/ProjectSettings/ProjectVersion.txt` enthält die exakte Editor-Version → **diese Patch-Version**
  ist die Wahrheit; alle Maschinen/CI nutzen exakt sie.
- In `Unity/Packages/manifest.json` **jede** Version auf konkrete Patch-Version festnageln (keine
  `x`-Platzhalter, keine ungewollten Pre-Releases). Vorlage → §2.

---

## 2. Packages (`manifest.json`)

> Versionen unten sind **Soll-Vorgaben aus [ARCHITECTURE.md §1](ARCHITECTURE.md)** — beim Anlegen die im
> Package-Manager real verfügbaren Patch-Versionen einsetzen und pinnen. Registry-Pakete (VContainer,
> UniTask, R3) via **OpenUPM** oder Git-URL.

```jsonc
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [ "jp.hadashikick.vcontainer", "com.cysharp", "com.cysharp.unitask", "com.cysharp.r3" ]
    }
  ],
  "dependencies": {
    "com.unity.render-pipelines.universal": "17.0.x",   // URP
    "com.unity.inputsystem":                "1.8.x",    // neues Input System (Joystick/Bomb)
    "com.unity.cinemachine":                "3.0.x",    // Top-Down-Kamera
    "com.unity.addressables":               "2.x",      // Asset-Streaming/Asset-Packs
    "com.unity.localization":               "1.5.x",    // 6 Sprachen, ICU-Plurale
    "com.unity.test-framework":             "1.4.x",    // EditMode/PlayMode
    "com.unity.burst":                      "1.8.x",    // optional: deterministische Math/Hot-Path
    "com.unity.mathematics":                "1.3.x",    // Vektoren/Fixed-Point-Hilfen
    "jp.hadashikick.vcontainer":            "1.16.x",   // DI
    "com.cysharp.unitask":                  "2.5.x",    // Async ohne Allocations
    "com.cysharp.r3":                       "1.x"       // Reactive (ReactiveProperty)
  }
}
```

**Per-Asset-Store/Manuell (nicht via manifest):**
- **DOTween** (UI-Tweens, Cinematics) — Asset-Store/„DOTween Pro" oder Free; nach Import `Setup`.
- **TextMeshPro** ist in Unity 6 Teil von UGUI (`com.unity.ugui`) — Essentials importieren.
- **NSubstitute** (Tests) — als DLL unter `Assets/_Project/Scripts/Tests/Plugins/` (AOT-freundlich).
- **Firebase Unity SDK** — erst Phase 2/3 (`bomberblast-arena`), nicht in Phase 0.
- **Kein Photon / kein Netcode** — reiner Single-Player (siehe DESIGN §24, CLAUDE §3).

> Nach dem Pinnen: `git add Packages/manifest.json Packages/packages-lock.json` — die Lock-Datei
> mitcommitten (reproduzierbare CI-Builds).

---

## 3. Ordnerstruktur (`Unity/Assets/_Project/`)

```
Assets/_Project/
├── Scripts/
│   ├── Core/            (BomberBlast.Core.asmdef          — KEINE Refs)
│   ├── Domain/          (BomberBlast.Domain.asmdef        — Core; KEINE Unity-API)
│   ├── Game/            (BomberBlast.Game.asmdef          — Core, Domain)
│   ├── UI/              (BomberBlast.UI.asmdef            — Core, Domain, Game)
│   ├── LiveOps/         (BomberBlast.LiveOps.asmdef       — Core, Domain, Game)
│   ├── Bootstrap/       (BomberBlast.Bootstrap.asmdef     — alles)
│   └── Tests/
│       ├── EditMode/    (BomberBlast.Tests.EditMode.asmdef — Core, Domain, NSubstitute)
│       └── PlayMode/    (BomberBlast.Tests.PlayMode.asmdef — + Game, UI)
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

| Asmdef | References | Besonderheit |
|--------|-----------|--------------|
| `BomberBlast.Core` | — | reine POCOs/Enums/Math; `Auto Referenced` aus |
| `BomberBlast.Domain` | Core | **`noEngineReferences: true`** (keine `UnityEngine`-DLL) → erzwingt Unity-Freiheit |
| `BomberBlast.Game` | Core, Domain | MonoBehaviours, Sim-Treiber, Rendering-Adapter |
| `BomberBlast.UI` | Core, Domain, Game | Binder + UI Toolkit/UGUI |
| `BomberBlast.LiveOps` | Core, Domain, Game | Daily/Weekly/Events/Shop/Rankings/Plattform-IAP |
| `BomberBlast.Bootstrap` | alle | Composition Root, Boot-Scene |
| `BomberBlast.Tests.EditMode` | Core, Domain | `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, NSubstitute |
| `BomberBlast.Tests.PlayMode` | + Game, UI | Headless/PlayMode-Smoke |

**`BomberBlast.Domain.asmdef` (Beispiel):**

```json
{
  "name": "BomberBlast.Domain",
  "references": [ "BomberBlast.Core" ],
  "noEngineReferences": true,
  "autoReferenced": false
}
```

> CI-Gate (§6) bricht bei Schichten-/Zirkel-Verstößen oder `UnityEngine`-Nutzung in `Domain`.

---

## 5. Git: `.gitignore` + `.gitattributes` (LFS)

**`.gitignore`** (Repo nutzt bereits ein Root-`.gitignore`; Unity-spezifisch ergänzen unter
`src/Apps/BomberBlast.Unity/`):

```gitignore
Unity/Library/
Unity/Temp/
Unity/Obj/
Unity/Build/
Unity/Builds/
Unity/Logs/
Unity/UserSettings/
Unity/[Mm]emoryCaptures/
Unity/*.csproj
Unity/*.sln
*.user
.vs/
.idea/
.vscode/
```

**`.gitattributes`** (Git-LFS — Pflicht für Binär-Assets):

```gitattributes
*.psd     filter=lfs diff=lfs merge=lfs -text
*.png     filter=lfs diff=lfs merge=lfs -text
*.jpg     filter=lfs diff=lfs merge=lfs -text
*.tga     filter=lfs diff=lfs merge=lfs -text
*.fbx     filter=lfs diff=lfs merge=lfs -text
*.glb     filter=lfs diff=lfs merge=lfs -text
*.wav     filter=lfs diff=lfs merge=lfs -text
*.ogg     filter=lfs diff=lfs merge=lfs -text
*.mp4     filter=lfs diff=lfs merge=lfs -text
*.unity   filter=lfs diff=lfs merge=lfs -text
# Meta-Dateien NICHT in LFS — sie müssen als Text diffbar bleiben
*.meta    -text
```

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

- [ ] Projekt (URP-Template) angelegt, Editor-Version gepinnt (§1)
- [ ] `manifest.json` Pakete + Lock committed (§2)
- [ ] Ordnerstruktur + 9 Asmdefs (Domain `noEngineReferences`) (§3/§4)
- [ ] `.gitignore` + `.gitattributes` (LFS) committed (§5)
- [ ] CI-Workflow grün (1 Dummy-EditMode-Test) (§6)
- [ ] Seed-JSON importiert, `BalancingConfig.asset` erzeugt (§7)
- [ ] Boot→Game-Szenenfluss über VContainer, „Boot OK"-Log (§8)
- [ ] **→ weiter mit [VERTICAL_SLICE.md](VERTICAL_SLICE.md)**

---

## Änderungslog

| Datum | Version | Änderung |
|-------|---------|----------|
| 2026-06-08 | v0.5 | Initiales Setup-Doc für die v0.5-Richtung (modernes 3D-Bomberman, kein Idle). |
