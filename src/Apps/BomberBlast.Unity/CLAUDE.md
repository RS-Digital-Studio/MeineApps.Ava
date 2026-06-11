# BomberBlast.Unity — Unity-Conventions & Stolperfallen

**Modernes 3D-Bomberman** auf Basis des produktiven Avalonia-BomberBlast (`../BomberBlast/`) —
klassisches, **aktiv gespieltes** Bomberman in 3D, mit **neuer Story** (Neo-Grid/Overseer/Reborn)
und der bewährten Bomberman-Meta-Progression. Gameplay-Mechanik & Domain-Code des Originals sind
**wiederverwendetes Fundament** (modernisiert, **kein striktes 1:1-Remake**). **KEIN Idle-Game,
KEIN AFK/Auto-Battle, kein Offline-Income** — Fortschritt nur durch aktives Spielen. Eigener Stack
(Unity 6, **nicht** von `dotnet build` erfasst). Richtung/Story → [PLAN.md](PLAN.md) + [DESIGN.md](DESIGN.md).

| Aspekt | Wert |
|--------|------|
| Status | Phase 0 (Scaffolding) — Projekt-Skelett unter `Unity/` angelegt (6000.4.8f1), Editor-Open/CI ausstehend |
| Engine | Unity 6 (6000.x LTS) + URP 17.x |
| Sprache | C# (.NET Standard 2.1), Nullable enable |
| DI | VContainer 1.16+ · Async: UniTask 2.5+ · Reactive: R3 (R3-Kern via NuGetForUnity) |
| Backend | Firebase (Auth/RTDB/Storage/Remote Config) — Cloud-Save + **asynchrone** Grid-Rankings (kein Echtzeit-MP) |
| 3D-Asset-Pipeline | Hunyuan3D-2.1 (Standalone-Runner, isolierte ComfyUI-Instanz) + Cloud-Fallback ([ASSETS_AI.md](ASSETS_AI.md)) |
| Plattformen | Android (primär, API 24+) · Desktop (Test) — **kein iOS/Steam** |
| Performance | 60 FPS High-End, 30 FPS Low-End (Hardware-Tier-System aus dem Original) |

> Pflichtlektüre vor jeder Code-Änderung. Diese Datei enthält **nur Unity-Spezifisches**;
> generische Repo-Conventions (deutsche Umlaute, MVVM-Grundprinzip, DI, DateTime-UTC,
> Localization-Prinzip, Commit-/Auto-Commit-Verhalten) stehen in der
> [Haupt-CLAUDE.md](../../../CLAUDE.md). Game-Design/Inhalte/Story → [DESIGN.md](DESIGN.md),
> Tech-Stack/Asmdefs → [ARCHITECTURE.md](ARCHITECTURE.md), Content-Reuse-Map (was wird aus
> dem Original übernommen/modernisiert) → [PARITY.md](PARITY.md).

> **Versions-Pinning (Pflicht):** Editor-Version ist **6000.4.8f1**
> (`Unity/ProjectSettings/ProjectVersion.txt`); in `manifest.json` ist jede Paket-Version auf
> eine konkrete Patch-Version gepinnt (keine `x`-Platzhalter, keine ungewollten Pre-Releases) —
> Paket-Liste → [SETUP.md §2](SETUP.md). Die Build-Befehle in §7 verwenden diese Version —
> die real installierte Editor-Version prüfen.

---

## 1. Architektur (Unity-spezifisch)

### Schichten-Modell (Asmdefs)

```
            ┌─→ UI ──────┐
Bootstrap ──┤            ├─→ Game ─→ Domain ─→ Core
            └─→ LiveOps ─┘
```

- Pfeil = „referenziert". **Core** referenziert nichts · **Domain** nur Core ·
  **Game** Core + Domain · **UI/LiveOps** Core + Domain + Game · **Bootstrap** alles.
- **`BomberBlast.Domain` darf KEINE Unity-API verwenden** (testbar, Compile-Constraint + CI-Gate).
- Test-Asmdefs: `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.
- Zirkuläre Asmdef-Referenzen verboten (CI-Gate fängt das ab).
- **Kein Multiplayer-Asmdef** — Single-Player-Fokus (siehe §3).

### Unity-spezifische Anti-Patterns (ergänzend zur Root)

Generische Anti-Patterns (Service-Locator, statische Singletons, God-Interfaces, Hardcoded
Werte, `DateTime.Now`-Persistenz, direkte Ad/Billing-Calls) → Root-CLAUDE.md. **Zusätzlich
in Unity verboten:**

| Anti-Pattern | Warum | Stattdessen |
|--------------|-------|-------------|
| `MonoBehaviour.FindObjectOfType` im Game-Code | Slow, untestbar | DI via VContainer-Inject |
| Hardcoded Balancing-Werte (Farben, Speeds, Caps) | Kein Tweaking ohne Build | ScriptableObject (`BalancingConfig`) |
| `await SceneManager.LoadSceneAsync()` ohne Cancellation | Race-Conditions | `sceneLoaderService.LoadAsync(name, ct)` |
| `Resources.Load<T>()` außerhalb Bootstrap | Bypass Addressables | Addressables-API |
| `UnityEngine.Random.Range` in Gameplay | Verletzt Determinismus | `IRngProvider.NextInt/NextFloat` |
| `Time.deltaTime` für Gameplay-Logik | Frame-Rate-abhängig | `FixedTimestepRunner` (60 Hz Fixed-Step) |
| `Debug.Log()` direkt | Kein Kontext, nicht filterbar | `ILogger<T>` mit Kontext-Tag |

### VContainer DI-Pattern

| Lifetime | Verwendung | Beispiel |
|----------|-----------|----------|
| **Singleton** | Stateful Services über Scenes hinweg | `IAuthService`, `IRemoteConfigService`, `IHeroService` |
| **Scoped** | Pro Scene/Sub-Scope | `BattleController`, `MatchState` |
| **Transient** | Pro Aufruf neu | ViewModels, Modals |

Constructor Injection ist Default. MonoBehaviours bekommen `[Inject]` auf eine
`Construct(...)`-Methode (keine Constructor-Injection bei MonoBehaviours möglich). Scene-Services
in einem `GameLifetimeScope : LifetimeScope` via `builder.Register<T>(Lifetime.Scoped)` registrieren.

```csharp
public class PlayerController : MonoBehaviour
{
    private IInputService _inputService;
    [Inject] public void Construct(IInputService inputService) => _inputService = inputService;
}
```

Verboten: `LifetimeScope.Find<T>()` (Service-Locator), `public static T Instance`.

### MVVM in Unity: View → Binder → ViewModel

Avalonia-MVVM gilt hier **nicht** direkt. Unity-Variante: ViewModel ist ein Unity-API-freies
POCO (testbar, R3-`ReactiveProperty`), der **Binder** (MonoBehaviour) ist der Adapter zur
Unity-UI (UGUI-`TextMeshProUGUI`/`Button` oder UI-Toolkit-`VisualElement`). VContainer injiziert
das VM in den Binder.

```csharp
public class BattleHUDViewModel               // Unity-frei, testbar
{
    public ReactiveProperty<int> ComboCount { get; } = new(0);
    public BattleHUDViewModel(ICoinService coins) { /* abonniert coins.CoinsChanged */ }
    public void OnBombButtonPressed() { /* ... */ }
}

public class BattleHUDBinder : MonoBehaviour  // Adapter zur Unity-UI
{
    [SerializeField] private TextMeshProUGUI _comboLabel;
    private BattleHUDViewModel _vm;
    [Inject] public void Construct(BattleHUDViewModel vm) => _vm = vm;
    private void Start() =>
        _vm.ComboCount.Subscribe(c => _comboLabel.text = $"x{c}").AddTo(this);
}
```

---

## 2. Determinismus-Pflicht (wichtigste Regel der Codebase)

Alle gameplay-relevanten Random-Calls über `IRngProvider`. Alle Tick-Updates über
`FixedTimestepRunner` bei 60 Hz mit `fixedDeltaTime` (nie `Time.deltaTime` in Sim-Logik). Inputs für
**Daily-Race/Replay** via `ReplayCapture.RecordTick(input)` aufzeichnen (Single-Player-Verifikation).

- **Status (aus dem Original):** Im produktiven BomberBlast ist Determinismus nur **Foundation,
  NICHT integriert** — der Live-Loop nutzt `System.Random`. Die Integration (alle ~50
  Gameplay-Random-Calls auf `IRngProvider`, Sim/Render-Trennung) ist **Neu-Arbeit** dieses Projekts,
  kein reiner Port.
- **Visual-Random** (Particle-Jitter, Screen-Shake, Camera-Tremor) ist **bewusst NICHT
  deterministisch** (sonst künstlich wirkend) → separater `[Inject] [Key("visual")] IRngProvider`.
- **Float-Mandat:** `DeterministicRandom` (xoshiro256+) ist nur **integer**-bit-stabil. Für
  hash-stabile **Daily-Race-/Replay-Verifikation** denselben `IRngProvider` + Fixed-Step nutzen, damit
  ein Replay denselben State-Hash reproduziert. (Kein Server-/Online-Re-Sim — Single-Player.)
- **CI-Gate:** Determinismus-Suite (Replay-Corpus → identischer State-Hash) ist Pflicht-Check
  in jedem PR; Failure blockt Merge.

---

## 3. Multiplayer / Netcode — nicht Teil von v0.5

**Single-Player-Fokus. Kein Echtzeit-Multiplayer, kein Photon/Netcode, kein Online-PvP/Co-op.**
„Grid-Rankings" und „Daily-Race" sind **asynchrone** Leaderboards (Firebase RTDB, Score-Submit), kein
Live-Match. Falls später doch Multiplayer gewünscht wird, ist das ein **separates Projekt** — diese
Codebase setzt es nicht voraus (kein Netcode-Asmdef, keine `[Networked]`-Properties, keine
Match-Cloud-Functions).

---

## 4. UI/UX (UI Toolkit + UGUI Hybrid)

- **UI Toolkit (UXML/USS):** statische UIs — Hub, Settings, Shop, Inventory, Grid-Rankings.
  Binder holt `_uiDoc.rootVisualElement.Q<T>("name")` und verdrahtet gegen das VM.
- **UGUI:** animations-lastige UIs — Battle-HUD, Combat-FX-Overlay, Tutorial, Cinematics
  (DOTween-Tweens).
- **Modal-System:** zentraler `ModalService.ShowAsync<TViewModel>(args)` — Modal-Stack (max 3
  tief), Back-Button schließt oberstes Modal, 200 ms Slide-In + Fade-Background.
- **Lokalisierung:** alle UI-Texte als `@key.path` (Unity-Localization-Keys), nie hardcoded.
  6 Sprachen wie im Rest des Portfolios. Plurale via Unity-Localization **Smart Strings**
  (`{0:plural:1 Münze|{} Münzen}`).

---

## 5. Performance-Mandate (60/30 FPS)

Vor jedem PR-Merge: Performance-Check auf Min-Spec-Device (z.B. Galaxy A50) mit Unity Profiler,
Frame-Debugger, Memory-Profiler.

| Anti-Pattern | Stattdessen |
|--------------|-------------|
| `Update()` in 100+ MonoBehaviours | `GameLoopService` mit Manager-Pattern |
| `GetComponent<T>()` / `Camera.main` pro Frame | In `Awake()` cachen |
| String-Konkatenation pro Frame, LINQ im Hot-Path | StringBuilder-Pool / manuelle for-Loops |
| `Instantiate()` pro Frame, GO-Activate-Storm | Object-Pool (inaktive Objekte weit weg parken) |
| Synchrones Resource-Loading | `Addressables.LoadAsync` |
| `Animator.Play("name")` (String) | `Animator.Play(hash)` (gecachter Hash) |

- **LOD-System** Pflicht für Heroes/Bosse/Environment-Props (LOD0/1/2 nach ScreenSize).
  Budgets → [ASSETS_AI.md §12.1](ASSETS_AI.md).
- **Hardware-Tier-aware Code:** Partikel-Counts u.ä. via `IHardwareProfileService.Tier`
  (`Low/Mid/High/Ultra`) skalieren.

---

## 6. Tests & Code-Style (Unity-spezifisch)

Naming und Async-Konventionen → Root-CLAUDE.md. **Sprachfeatures sind von dieser Delegation
ausdrücklich ausgenommen** — Unity 6 ist **C# 9 / netstandard 2.1** (im Workspace validiert,
Memory `unity-domain-port`), nicht das C# 14 des Avalonia-Portfolios:

- **KEINE** Primary Constructors, **KEINE** file-scoped Namespaces, **KEINE** Collection
  Expressions (`[...]`).
- **Kein** `Random.Shared`, kein `Enum.GetValues<T>()`, kein `HashCode.Combine`.
- Records nur mit `IsExternalInit`-Shim; kein `record struct`.
- **Newtonsoft.Json** statt `System.Text.Json`.

**Unity-Eigenes:**

- **Mock-Framework: NSubstitute** (läuft im EditMode/Mono — Wahl wegen API-Qualität, nicht AOT).
- **Test-Layout:** `Assets/_Project/Tests/{Domain,...}/` mit eigenem Asmdef je Bereich (real:
  `Tests/Domain/BomberBlast.Domain.Tests.asmdef`), Klasse `{Subject}Tests`,
  Methode `{Method}_{Scenario}_{Expected}`.
- **Coverage-Ziele:** Domain ≥ 90 %, Core ≥ 80 %, Game ≥ 60 %, UI Best-Effort.
- **Pflicht-Tests** bei Domain-Logik, Service-Logik (Liga/BP-XP/Achievements), Algorithmen
  (LevelLayoutGenerator, DungeonSynergyResolver), Math (Combo-Score, Liga-Punkte, ISO-Wochen-Seed).
  Optional bei reiner UI-Verdrahtung / Unity-API-Wrappern.
- **Defensive Programming:** `UnityEngine.Assertions.Assert` in Dev-Builds; in Production
  `Result<T>` statt Silent-Fail; Logging immer mit Kontext-Tag.
- **ScriptableObject-IDs** PascalCase + Underscore: `Hero_Default.asset`, `Bomb_Frost.asset`.
- **Prefabs** PascalCase + `Prefab`-Suffix; asmdefs `BomberBlast.{Module}.asmdef`.

---

## 7. Build & Werkzeuge

Editor-Version ist auf **6000.4.8f1** gepinnt — die real installierte Version prüfen
(`Get-ChildItem "C:\Program Files\Unity\Hub\Editor"`). Projektpfad:
`F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\Unity`.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe"
$proj  = "F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\Unity"

# Editor öffnen
& $unity -projectPath $proj

# EditMode-/PlayMode-Tests (CLI)
& $unity -batchmode -quit -projectPath $proj -runTests -testPlatform EditMode `
  -testResults "test-results-editmode.xml" -logFile "test-editmode.log"
& $unity -batchmode -quit -projectPath $proj -runTests -testPlatform PlayMode `
  -testResults "test-results-playmode.xml" -logFile "test-playmode.log"

# Android-Build (Debug / Release-AAB)
& $unity -batchmode -quit -nographics -projectPath $proj -buildTarget Android `
  -executeMethod BuildScripts.BuildAndroidDebug   -logFile "build-android-debug.log"
& $unity -batchmode -quit -nographics -projectPath $proj -buildTarget Android `
  -executeMethod BuildScripts.BuildAndroidRelease -logFile "build-android-release.log"
```

- **Daten-Importer:** Unity-Menü → `BomberBlast → Data → Import All` (JSON aus `Resources/Data/`
  → ScriptableObjects unter `Assets/_Project/ScriptableObjects/`).
- **Cloud-Functions / Firebase-Rules** (Projekt `bomberblast-arena`, sobald `Server/` existiert):
  `npm install && npm run build && npm test`, dann `npx firebase deploy --only functions|database|storage`.

### Git-LFS & .gitignore

Maßgeblich sind die realen Dateien unter `src/Apps/BomberBlast.Unity/`:
[`.gitattributes`](.gitattributes) (LFS) und [`.gitignore`](.gitignore). LFS ist
**pattern-basiert** (keine Größenregel): `*.psd/.ai/.png/.jpg/.tga` (Art),
`*.fbx/.obj/.gltf/.glb` (3D), `*.wav/.mp3/.ogg` (Audio), `*.mp4/.webm` (Video), `*.xlsx`
(Balancing-Workbook). **`*.unity` bewusst NICHT in LFS** — Szenen sind bei
Force-Text-Serialisierung YAML-Text (diffbar, UnityYAMLMerge-fähig); `*.meta` bleibt Text.
Details + Root-`.gitignore`-Negationen → [SETUP.md §5](SETUP.md).

---

## 8. Agent-Hinweise

**Passend:** `architect` (Asmdef-Struktur), `planner`, `code-review`, `debugger`, `tester`
(NUnit/Unity Test Framework), `refactor`, `migrator` (Unity-Upgrades), `performance`,
`security` (Single-Player-Anti-Cheat-Timer, Firebase-Rules), `documenter`.

**Nicht passend** (Avalonia-spezifisch): `mvvm-auditor`, `skiasharp`, `ui` (AXAML-basiert),
`localize` (RESX-basiert), `deploy` (AAB-Pipeline für .NET-Android), `bingxbot`.

Beim Lesen von Code proaktiv auf Verletzungen achten und sofort korrigieren oder melden:
Schichten-Verstöße, Service-Locator statt DI, `UnityEngine.Random` in Gameplay, hardcoded
Texte/Werte statt Localization-Keys/ScriptableObject.

---

## 9. Verweise

**Repo-intern:** [PLAN.md](PLAN.md) (Vision/KPIs/Roadmap) · [DESIGN.md](DESIGN.md)
(Game-Design) · [PARITY.md](PARITY.md) (Original-System → Unity-Äquivalent, Reuse/Modernisierung) ·
[ARCHITECTURE.md](ARCHITECTURE.md) (Tech-Stack/Asmdefs/Determinismus/Anti-Cheat) ·
[SETUP.md](SETUP.md) (Phase-0-Scaffolding, Single Source für Pakete) ·
[VERTICAL_SLICE.md](VERTICAL_SLICE.md) (Phase 1: Sektor 1 + Granite Warden) ·
[ROADMAP.md](ROADMAP.md) · [ASSETS_AI.md](ASSETS_AI.md) (KI-Asset-Pipeline).
`Server/SERVEROPS.md` folgt.

**Repo-übergreifend:** [Haupt-CLAUDE.md](../../../CLAUDE.md) (Conventions, Umlaute, MVVM,
Build) · [../ArcaneKingdom/CLAUDE.md](../ArcaneKingdom/CLAUDE.md) (Schwester-Unity-Projekt,
Unity-Lehrgeld) · [../BomberBlast/CLAUDE.md](../BomberBlast/CLAUDE.md) (Original, Domain-Quelle).

**Tool-Docs:** [Unity 6](https://docs.unity3d.com/6000.0/Documentation/Manual/) ·
[URP 17](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/) ·
[VContainer](https://vcontainer.hadashikick.jp/) · [UniTask](https://github.com/Cysharp/UniTask) ·
[R3](https://github.com/Cysharp/R3) ·
[Firebase Unity](https://firebase.google.com/docs/unity/setup) ·
[DOTween](http://dotween.demigiant.com/documentation.php) ·
[Cinemachine 3](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.0/).

---

## 10. Bekannte Stolperfallen

### Unity 6 + URP

| Problem | Ursache | Lösung |
|---------|---------|--------|
| URP-Material wird pink im Build | Shader nicht inkludiert | "Always Included Shaders" in `Project Settings → Graphics` |
| Forward+ Lichter nicht gerendert | URP-Asset nicht zugewiesen | `Project Settings → Graphics → Default Render Pipeline` |
| `Camera.main` null auf Android | Tag "MainCamera" fehlt | Camera-Tag im Inspector setzen |
| `OnMouse*` reagiert nicht auf UI | Falsche Layer/EventSystem | `IPointerXxxHandler` via `PointerEventData` |
| Animator wechselt nicht in Sub-State | Transition ohne Condition / Has-Exit-Time | Conditions setzen, "Has Exit Time" deaktivieren |
| `SerializeField` nicht im Inspector | Private Field ohne Attribut | `[SerializeField] private int _value;` |
| Build hängt im IL2CPP-Schritt | Aggressives Code-Stripping | `link.xml` mit Preserve-Direktiven |

### IL2CPP / Stripping

| Problem | Ursache | Lösung |
|---------|---------|--------|
| `JsonConvert.DeserializeObject<T>()` crasht | Reflection-Type gestripped | `link.xml` `<preserve>` für T |
| `Activator.CreateInstance<T>()` null | Konstruktor gestripped | `[Preserve]` auf Klasse |
| Generic-Methods fehlen | Nicht eager kompiliert | `[Preserve]` auf Generic-Method |
| Native-Plugin-DLL nicht gefunden | Architektur-Mismatch | x86/x64/ARM64-Variante korrekt zuweisen |

### Firebase Unity SDK

| Problem | Ursache | Lösung |
|---------|---------|--------|
| RTDB-Listener feuert nicht nach Reconnect | Connection-State-Listener fehlt | `database.GoOnline()` + State checken |
| `SignInAnonymouslyAsync()` hängt | Kein Timeout | `CancellationTokenSource` mit 5s-Timeout |
| Remote Config liefert Default trotz Server-Wert | Minimum-Fetch-Interval | `MinimumFetchInterval = TimeSpan.Zero` (NUR Debug) |
| Server-Timestamp wird Client-Local-Time | Falsche Deserialization | `ServerValue.TIMESTAMP` als `long` lesen, `DateTimeOffset.FromUnixTimeMilliseconds()` |

### Addressables & Localization

| Problem | Ursache | Lösung |
|---------|---------|--------|
| Addressable lädt nicht im Build | Group nicht im Catalog | "Include in Build" in Group-Settings |
| Memory-Leak nach Scene-Unload | Handle nicht released | `Addressables.Release(handle)` in `OnDestroy` |
| Catalog-Cache outdated nach Update | Cache nicht invalidiert | `Addressables.UpdateCatalogs()` beim App-Start |
| Localization-Strings nicht aktualisiert nach Sprach-Wechsel | Static Cache | `LocalizationSettings.SelectedLocale = ...` + `OnLanguageChanged`-Listener |
| TextMeshPro zeigt Boxes (□) | Font ohne Glyph | TMP-Font-Asset mit erweitertem Character-Set |
