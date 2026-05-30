# BomberBlast 3D — Projekt-Conventions & Stolperfallen

> **Leitprinzip:** Treuer 3D-Remake des produktiven BomberBlast (`../BomberBlast/`) — **dasselbe Spiel,
> nur in 3D und besser**. Game-Design/Inhalte/Balancing 1:1 aus dem Original-Code (Quelle der Wahrheit).
> Siehe [PLAN.md](PLAN.md) §5/§6.
>
> Pflichtlektüre vor jeder Code-Änderung am Unity-Projekt. Komplementär zu
> [PLAN.md](PLAN.md), [DESIGN.md](DESIGN.md), [ARCHITECTURE.md](ARCHITECTURE.md), [ROADMAP.md](ROADMAP.md), [ASSETS_AI.md](ASSETS_AI.md).
>
> Diese Datei wächst mit dem Projekt — jede Erkenntnis ("Gotcha"), die mehr als einmal aufgetreten ist, gehört hier dokumentiert.

---

## Inhaltsverzeichnis

1. [Projektübersicht (Schnell-Referenz)](#1-projektübersicht-schnell-referenz)
2. [Build-Befehle](#2-build-befehle)
3. [Code-Conventions](#3-code-conventions)
4. [Architektur-Regeln (Pflicht)](#4-architektur-regeln-pflicht)
5. [VContainer DI-Pattern](#5-vcontainer-di-pattern)
6. [Determinismus-Pflicht](#6-determinismus-pflicht)
7. [Photon Fusion + Realtime — Patterns](#7-photon-fusion--realtime--patterns)
8. [UI/UX-Patterns (UI Toolkit + UGUI Hybrid)](#8-uiux-patterns-ui-toolkit--ugui-hybrid)
9. [Performance-Mandate (60/30 FPS)](#9-performance-mandate-6030-fps)
10. [Test-Pflichten](#10-test-pflichten)
11. [Git-Workflow](#11-git-workflow)
12. [Commit-Verhalten](#12-commit-verhalten)
13. [Bekannte Stolperfallen (Unity 6 + URP)](#13-bekannte-stolperfallen-unity-6--urp)
14. [Bekannte Stolperfallen (Photon)](#14-bekannte-stolperfallen-photon)
15. [Bekannte Stolperfallen (Firebase Unity SDK)](#15-bekannte-stolperfallen-firebase-unity-sdk)
16. [Bekannte Stolperfallen (Addressables)](#16-bekannte-stolperfallen-addressables)
17. [Bekannte Stolperfallen (Localization)](#17-bekannte-stolperfallen-localization)
18. [Bekannte Stolperfallen (IL2CPP)](#18-bekannte-stolperfallen-il2cpp)
19. [Agent-Trigger-Hinweise (für Claude Code Agents)](#19-agent-trigger-hinweise-für-claude-code-agents)
20. [Checkliste vor Commit](#20-checkliste-vor-commit)
21. [Verweise](#21-verweise)

---

## 1. Projektübersicht (Schnell-Referenz)

> **Versions-Pinning (Pflicht):** Alle Versionen unten beim Setup auf eine **konkrete, existierende
> Patch-Version** festnageln (manifest.json — keine `x`-Platzhalter, keine Pre-Release ungewollt).
> Die exakte Unity-6-LTS-Patch-Version beim Projekt-Anlegen aus dem Unity Hub übernehmen.

| Aspekt | Wert |
|--------|------|
| Engine | Unity 6 (6000.x LTS — exakte Patch-Version beim Setup pinnen) |
| Render-Pipeline | URP 17.x |
| Sprache | C# (.NET Standard 2.1) |
| DI | VContainer 1.16+ |
| Async | UniTask 2.5+ |
| Reactive | R3 (Nachfolger UniRx) |
| Netcode (optional, nur für Online-MP) | Photon Fusion 2 (Versus) / Photon Realtime 5 (Co-op) / Photon Chat 4 |
| Backend | Firebase (Auth/RTDB/Functions/Storage/Crashlytics/Analytics/Remote Config) |
| Voice | **deferred/optional** (Original ist voice-los; nur falls bewusst eingeführt) |
| 3D-Asset-Pipeline | ComfyUI + TRELLIS/SPAR3D + Cloud-Fallback (siehe [ASSETS_AI.md](ASSETS_AI.md)) |
| Audio | Aufgewertete Bestands-Loops (Kenney-CC0-Basis) + optional FMOD |
| Min-Android | API 24 (Android 7) |
| Min-iOS | 13 |
| Plattformen | Android (primär) + iOS + Steam (Windows/macOS/Linux) |
| Performance | 60 FPS High-End, 30 FPS Low-End (Hardware-Tier-System aus Original) |

---

## 2. Build-Befehle

### 2.1 Unity-Editor (lokal)

```powershell
# Unity-Editor öffnen
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" -projectPath "F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\Unity"
```

### 2.2 EditMode-Tests (CLI)

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
  -batchmode -quit `
  -projectPath "F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\Unity" `
  -runTests -testPlatform EditMode `
  -testResults "test-results-editmode.xml" `
  -logFile "test-editmode.log"
```

### 2.3 PlayMode-Tests (CLI)

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
  -batchmode -quit `
  -projectPath "F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\Unity" `
  -runTests -testPlatform PlayMode `
  -testResults "test-results-playmode.xml" `
  -logFile "test-playmode.log"
```

### 2.4 Android-Build (Debug)

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath "F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\Unity" `
  -buildTarget Android `
  -executeMethod BuildScripts.BuildAndroidDebug `
  -logFile "build-android-debug.log"
```

### 2.5 Android-Release (AAB)

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath "F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\Unity" `
  -buildTarget Android `
  -executeMethod BuildScripts.BuildAndroidRelease `
  -logFile "build-android-release.log"
```

### 2.6 Daten-Importer

```
Unity-Menü → BomberBlast → Data → Import All
```

Importiert alle JSON-Files aus `Resources/Data/` zu ScriptableObjects unter `Assets/_Project/ScriptableObjects/`.

### 2.7 Cloud-Functions deployen

```powershell
cd F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\Server\CloudFunctions
npm install
npm run build
npm test
npx firebase deploy --only functions --project bomberblast-arena
```

### 2.8 Firebase-Security-Rules deployen

```powershell
cd F:\Meine_Apps_Ava\src\Apps\BomberBlast.Unity\Server
npx firebase deploy --only database --project bomberblast-arena
npx firebase deploy --only storage --project bomberblast-arena
```

---

## 3. Code-Conventions

### 3.1 Naming

| Element | Convention | Beispiel |
|---------|-----------|----------|
| Namespace | `BomberBlast.{Module}` | `BomberBlast.Domain.Bombs` |
| Class | PascalCase | `BattleController`, `BombDefinition` |
| Interface | `I` + PascalCase | `IBombService`, `INetworkService` |
| Method | PascalCase | `PlaceBomb()`, `ProcessInput()` |
| Field (private) | `_camelCase` | `private int _currentMana;` |
| Property | PascalCase | `public int CurrentMana { get; }` |
| Constant | `UPPER_SNAKE` | `private const int MAX_BOMBS = 8;` |
| Static-readonly | PascalCase | `private static readonly int[] ComboScores = ...;` |
| Event | `On{Verb}{Past}` (Unity-Standard) | `OnBombPlaced`, `OnPlayerDied` |
| Async-Method | `Async`-Suffix | `LoadProfileAsync()` |
| ScriptableObject Asset | PascalCase + Underscore-ID | `Hero_Default.asset`, `Bomb_Frost.asset` |
| Scene | PascalCase | `Boot.unity`, `MainMenu.unity` |
| Prefab | PascalCase + `Prefab`-Suffix | `BombPrefab`, `HeroViewPrefab` |
| asmdef | `BomberBlast.{Module}` | `BomberBlast.Game.asmdef` |
| Test-Class | `{Subject}Tests` | `ComboSystemTests`, `LeagueServiceTests` |
| Test-Method | `{Method}_{Scenario}_{Expected}` | `ProcessInput_ValidMove_UpdatesPosition` |
| Folder | PascalCase | `Heroes/`, `Cards/`, `Multiplayer/` |
| AXAML/UXML | PascalCase | `HeroPickModal.uxml`, `HudView.uxml` |
| USS | PascalCase | `HeroPickModal.uss` |

### 3.2 Code-Style

- **Modernes C#** verwenden:
  - Primary Constructors (`class Foo(IBar bar) { }`)
  - Records für DTOs/Events (`record BombPlacedEvent(int X, int Y, int OwnerId);`)
  - Pattern-Matching (`if (x is { Type: BombType.Frost })`)
  - Switch-Expressions (`var result = mode switch { ... }`)
  - Collection-Expressions (`int[] arr = [1, 2, 3];`)
  - File-scoped Namespaces (`namespace BomberBlast.Game;`)
  - Raw-String-Literals für JSON/SQL (`"""..."""`)
- **Nullable Reference Types** aktiv (`<Nullable>enable</Nullable>`)
- **var** wenn Typ aus Kontext klar, sonst expliziter Typ
- **Async-Konventionen:** `Async`-Suffix, `CancellationToken` als letzter Parameter
- **UniTask** statt `Task<T>`, außer bei reinen Library-Calls
- **Kommentare auf Deutsch** (Umlaute erlaubt im Code, ASCII-only nur für Identifier)
- **Keine TODOs ohne Issue-Verweis:** `// TODO(#42): ...`
- **Keine Emojis im Code** außer wenn vom User explizit gewünscht
- **Strukturierte Logs:** `_logger.LogError(ex, "Failed at {Route}", route)` NIE `_logger.LogError($"Failed at {route}", ex)`

### 3.3 Test-Conventions

- Tests in `Assets/_Project/Scripts/Tests/{Domain,Game,...}/`
- Naming: `{Subject}Tests.cs`, Test-Methoden `{Method}_{Scenario}_{Expected}`
- Mock-Framework: **NSubstitute** (besser als Moq für Unity-AOT)
- Coverage-Ziele:
  - **BomberBlast.Domain:** ≥ 90 %
  - **BomberBlast.Core:** ≥ 80 %
  - **BomberBlast.Game:** ≥ 60 %
  - **BomberBlast.UI:** Best-Effort
  - **BomberBlast.Multiplayer:** ≥ 70 % (Photon-Mock)

### 3.4 Defensive Programming

- `UnityEngine.Assertions.Assert` für Editor/Development-Builds
- Production: `Result<T>`-Type für fehlbare Operationen, **kein Silent-Fail**
- Logging mit Kontext-Tags: `_logger.LogInformation("[Battle] {Event}", eventName)`
- **Nie `Debug.Log()`** direkt — immer via `ILogger<T>`

### 3.5 MVVM-Pattern

View (UXML / UGUI) → Binder (MonoBehaviour) → ViewModel (POCO):

```csharp
// ViewModel ist Unity-API-frei (testbar)
public class BattleHUDViewModel
{
    public ReactiveProperty<int> Coins { get; } = new(0);
    public ReactiveProperty<int> Lives { get; } = new(3);
    public ReactiveProperty<int> ComboCount { get; } = new(0);
    
    private readonly ICoinService _coinService;
    
    public BattleHUDViewModel(ICoinService coinService) // VContainer-Injection
    {
        _coinService = coinService;
        _coinService.CoinsChanged
            .Subscribe(c => Coins.Value = c);
    }
    
    public void OnBombButtonPressed() => /* ... */;
}

// Binder ist Adapter zur Unity-UI
public class BattleHUDBinder : MonoBehaviour
{
    [Inject] private BattleHUDViewModel _vm;
    [SerializeField] private TextMeshProUGUI _coinsLabel;
    [SerializeField] private Button _bombButton;
    
    private void Start()
    {
        _vm.Coins.Subscribe(c => _coinsLabel.text = c.ToString()).AddTo(this);
        _bombButton.OnClickAsObservable().Subscribe(_ => _vm.OnBombButtonPressed()).AddTo(this);
    }
}
```

---

## 4. Architektur-Regeln (Pflicht)

### 4.1 Asmdef-Constraints

- **`BomberBlast.Domain` darf KEINE Unity-API verwenden** (Compile-Constraint via Reflection-Check + CI-Gate)
- **`BomberBlast.Multiplayer` ist Define-konditional** (`UNITY_INCLUDE_NETWORK`) → ermöglicht PvP-freie Demo-Builds
- **Tests:** `defineConstraints: ["UNITY_INCLUDE_TESTS"]`
- **Zirkuläre Referenzen verboten** (CI-Gate fängt das ab)

### 4.2 Schichten-Modell

```
Bootstrap → UI → Game → Domain → Core
                ↓
                LiveOps, Multiplayer
```

- **Core** referenziert NICHTS
- **Domain** referenziert NUR Core
- **Game** referenziert Core + Domain
- **UI/LiveOps/Multiplayer** referenzieren Core + Domain + Game
- **Bootstrap** referenziert alles

### 4.3 Anti-Patterns (verboten)

| Anti-Pattern | Warum verboten | Stattdessen |
|--------------|----------------|-------------|
| `ServiceLocator.Resolve<T>()` außerhalb Composition Root | Versteckte Abhängigkeiten | Constructor Injection via VContainer |
| Statische Singletons (`Xxx.Instance`) | Keine Test-Isolation, Lifetime-Bugs | Interface via DI |
| God Interfaces (>5 Methoden) | Verstößt gegen ISP | Pro Verantwortlichkeit ein Interface |
| `MonoBehaviour.FindObjectOfType` im Game-Code | Slow, untestbar | DI via VContainer-Inject |
| Hardcoded Werte (Farben, Größen, Speeds) im Code | Kein Tweaking ohne Build | ScriptableObject (`BalancingConfig`) |
| `DateTime.Now` für Persistenz | UTC-Konvertierungs-Bugs | `DateTime.UtcNow` + `"O"`-Format + `DateTimeStyles.RoundtripKind` |
| Direkte AdMob/Billing-Calls in Game-Code | Plattform-Lock-In | `IAdService` / `IIapService` via DI |
| `await SceneManager.LoadSceneAsync()` ohne Cancellation | Race-Conditions | `await sceneLoaderService.LoadAsync(name, ct)` |
| `Resources.Load<T>()` außerhalb Bootstrap | Bypass Addressables | Addressables-API verwenden |
| Direkte Random-Calls in Gameplay (`UnityEngine.Random.Range`) | Verletzt Determinismus | `IRngProvider.NextInt/NextFloat` |
| `Time.deltaTime` für Gameplay-Logik (statt Sim-Tick) | Frame-Rate-abhängig | `FixedTimestepRunner` mit 60Hz-Fixed-Step |

---

## 5. VContainer DI-Pattern

### 5.1 Lifetime-Wahl

| Lifetime | Verwendung | Beispiel |
|----------|-----------|----------|
| **Singleton** | Stateful Services über Scene-Hinweg | `IAuthService`, `IRemoteConfigService`, `IHeroService` |
| **Scoped** | Pro Scene/Sub-Scope | `BattleController`, `MatchState` |
| **Transient** | Pro Aufruf neu | `ViewModel`s, Modals |

### 5.2 Constructor Injection (Default)

```csharp
public class HeroService : IHeroService
{
    private readonly IRngProvider _rng;
    private readonly HeroDatabase _heroDb;
    private readonly ILogger<HeroService> _logger;

    public HeroService(IRngProvider rng, HeroDatabase heroDb, ILogger<HeroService> logger)
    {
        _rng = rng;
        _heroDb = heroDb;
        _logger = logger;
    }
}
```

### 5.3 Inject-Attribute auf MonoBehaviour

```csharp
public class PlayerController : MonoBehaviour
{
    private IInputService _inputService;
    private ILogger<PlayerController> _logger;

    [Inject]
    public void Construct(IInputService inputService, ILogger<PlayerController> logger)
    {
        _inputService = inputService;
        _logger = logger;
    }
}
```

### 5.4 Sub-Scopes pro Scene

```csharp
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<BattleController>(Lifetime.Scoped);
        builder.Register<MatchState>(Lifetime.Scoped);
        // Scene-spezifische Services
    }
}
```

### 5.5 Verbotene Patterns

```csharp
// FALSCH: Service-Locator
var svc = LifetimeScope.Find<IBombService>(); // ❌

// FALSCH: Static singleton
public static IBombService Instance { get; } = new BombService(); // ❌

// RICHTIG: Constructor Injection
public class Foo
{
    private readonly IBombService _bombService;
    public Foo(IBombService bombService) => _bombService = bombService;
}
```

---

## 6. Determinismus-Pflicht

> **Wichtigste Regel der Codebase:** Alle gameplay-relevanten Random-Calls gehen über `IRngProvider`.
> Alle Tick-Updates laufen über `FixedTimestepRunner` bei 60 Hz. Alle Inputs werden in `ReplayCapture` aufgezeichnet.
>
> **Status (aus dem Original):** Im produktiven BomberBlast ist Determinismus nur **Foundation, NICHT
> integriert** — der Live-Loop nutzt `System.Random`. Die Integration (alle ~50 Random-Calls auf
> `IRngProvider`, Sim/Render-Trennung) ist **Neu-Arbeit** dieses Projekts, kein reiner Port.
>
> **Float-Mandat:** `DeterministicRandom` (xoshiro256+) ist nur **integer**-bit-stabil. Für hash-stabile
> Sim (Replay-Re-Sim / Online-Versus) gilt: **Fixed-Point/Integer** für hash-relevante Zustände ODER
> dokumentierte Quantisierung **vor** dem State-Hash — float-Physik divergiert IL2CPP/ARM64 ↔ Server
> (siehe [ARCHITECTURE.md](ARCHITECTURE.md) §13.0). Online-Versus ist optional/Post-Launch, daher kein Launch-Blocker.

### 6.1 RNG-Verwendung

```csharp
public class LootDropper
{
    private readonly IRngProvider _rng;
    
    public LootDropper(IRngProvider rng) => _rng = rng;
    
    public LootDrop GetRandomLoot()
    {
        var roll = _rng.NextFloat();  // ✅ Deterministisch
        // NICHT: UnityEngine.Random.Range(0f, 1f);  // ❌
        return ResolveLoot(roll);
    }
}
```

### 6.2 SystemRng für Visual-Random

Particle-Jitter, Screen-Shake-Offset, Camera-Tremor → **NICHT** deterministisch (würde künstlich wirken):

```csharp
public class ScreenShake
{
    [Inject(Id = "visual")]  // ← Visual-Rng als Sub-Key
    private IRngProvider _visualRng;
    
    public void Apply()
    {
        var offset = _visualRng.NextFloat() * 0.5f;
        // ...
    }
}
```

### 6.3 Tick-System

```csharp
// FixedTimestepRunner läuft 60 Hz
// Gameplay-Logik IMMER mit fixedDeltaTime
public void SimulateTick(float fixedDeltaTime)
{
    // fixedDeltaTime = 1/60f (immer)
    var bombTimer = bomb.Timer - fixedDeltaTime;  // ✅
    
    // NICHT: bomb.Timer - Time.deltaTime;  // ❌ Frame-Rate-abhängig
}
```

### 6.4 Replay-Capture-Pflicht in PvP/Co-op

```csharp
public class PvpNetworkPlayer : NetworkBehaviour
{
    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerInput input))
        {
            // PFLICHT: Input recorden
            _replayCapture.RecordTick(input);
            
            ProcessInput(input);
        }
    }
}
```

---

## 7. Photon Fusion + Realtime — Patterns

### 7.1 Photon Fusion (PvP)

```csharp
public class PvpNetworkPlayer : NetworkBehaviour
{
    // [Networked] Properties werden synced
    [Networked] public int HeroId { get; set; }
    [Networked] public Vector2Int GridPosition { get; set; }
    [Networked] public TickTimer InvulnerableUntil { get; set; }
    
    // NICHT: regulärer setter ohne [Networked]
    public override void Spawned() { /* Init */ }
    
    public override void FixedUpdateNetwork()
    {
        // PFLICHT: Server-Authoritative-Code hier
        if (GetInput(out PlayerInput input))
            ProcessInput(input);
    }
}
```

### 7.2 Photon Realtime (Co-op)

```csharp
public class CoopRoomManager : MonoBehaviourPunCallbacks
{
    public override void OnJoinedRoom() { /* ... */ }
    public override void OnPlayerEnteredRoom(Player newPlayer) { /* ... */ }
    public override void OnMasterClientSwitched(Player newMaster)
    {
        if (newMaster.IsLocal)
            BecomeHost();
    }
}
```

### 7.3 Verbotene Patterns

```csharp
// FALSCH: Client setzt eigene Liga-Punkte
PhotonNetwork.LocalPlayer.SetCustomProperty("league_points", 9999); // ❌

// RICHTIG: Cloud Function setzt nach Server-Validation
await CloudFunctions.SubmitMatchResultAsync(matchId, result);
// Server validiert → schreibt Liga-Punkte
```

---

## 8. UI/UX-Patterns (UI Toolkit + UGUI Hybrid)

### 8.1 UI Toolkit (statische UIs)

Für Hub, Settings, Inventory, Shop, Clan-Liste, Chat-Listen.

```xml
<!-- HeroPickModal.uxml -->
<UXML xmlns:ui="UnityEngine.UIElements">
  <ui:VisualElement name="root" class="modal-root">
    <ui:Label name="title" text="@hero.pick.title" />
    <ui:ListView name="hero-list" />
    <ui:Button name="confirm-btn" text="@common.confirm" />
  </ui:VisualElement>
</UXML>
```

```csharp
// HeroPickModalBinder.cs
public class HeroPickModalBinder : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDoc;
    [Inject] private HeroPickModalViewModel _vm;
    
    private void OnEnable()
    {
        var root = _uiDoc.rootVisualElement;
        var heroList = root.Q<ListView>("hero-list");
        heroList.itemsSource = _vm.Heroes;
        heroList.makeItem = () => new HeroListItem();
        heroList.bindItem = (e, i) => ((HeroListItem)e).Bind(_vm.Heroes[i]);
    }
}
```

### 8.2 UGUI (animations-haftig)

Für Battle-HUD, Combat-FX-Overlay, Tutorial-Overlay, Cinematic-Sequenzen.

```csharp
// BattleHUD.cs (UGUI-MonoBehaviour)
public class BattleHUDBinder : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _comboLabel;
    [SerializeField] private Image _comboBar;
    [Inject] private BattleHUDViewModel _vm;
    
    private void Start()
    {
        _vm.ComboCount
            .Subscribe(c => 
            {
                _comboLabel.text = $"x{c}";
                _comboBar.DOFillAmount(c / 10f, 0.3f).SetEase(Ease.OutBack);
            })
            .AddTo(this);
    }
}
```

### 8.3 Modal-System

Zentraler `ModalService.ShowAsync<TViewModel>(args)`:
- Modal-Stack (max 3 tief)
- Back-Button schließt oberstes Modal
- IsHitTestVisible-Aggregat pro Layer
- 200 ms Slide-In + Fade-Background mit DOTween

```csharp
var result = await _modalService.ShowAsync<ConfirmModal>(new ConfirmArgs
{
    Title = "@deletion.title",
    Message = "@deletion.message",
    ConfirmText = "@common.delete",
});
if (result == ConfirmResult.Confirmed) { /* ... */ }
```

### 8.4 Lokalisierung

**Pflicht:** Alle UI-Texte als `@key.path` (Localization-Keys), nie hardcoded.

```csharp
// FALSCH
_button.text = "Bestätigen"; // ❌

// RICHTIG
_button.text = _localization.GetString("common.confirm"); // ✅
```

USS-Klassen analog mit Localization-Hint:

```xml
<ui:Label text="@hero.pick.title" class="modal-title" />
```

---

## 9. Performance-Mandate (60/30 FPS)

### 9.1 Pro Sprint Performance-Check

Vor PR-Merge: Performance-Check auf Min-Spec-Device (z.B. Galaxy A50).

**Tools:**
- Unity Profiler (CPU/GPU/Memory)
- Frame-Debugger (DrawCall-Analyse)
- Memory-Profiler (Heap-Analyse)

### 9.2 Performance-Anti-Patterns

| Anti-Pattern | Warum schlecht | Stattdessen |
|--------------|----------------|-------------|
| `Update()` in 100+ MonoBehaviours | Frame-Drop | `GameLoopService` mit Manager-Pattern |
| `GetComponent<T>()` im `Update()` | Heap-Allokation | Cache in `Awake()` |
| `string` per `+`-Konkatenation pro Frame | GC-Pressure | `StringBuilder`-Pool oder Format-Methoden |
| `LINQ` im Hot-Path | GC-Pressure | Manuelle for-Loops |
| `Instantiate()` pro Frame | Heap | Object-Pool |
| Synchrones Resource-Loading | UI-Freeze | `Addressables.LoadAsync` |
| `Camera.main` Aufruf pro Frame | Internal Find-Call | Cache in Field |
| Activate/Deactivate von vielen GOs | OnEnable/Disable-Storm | Pool + Position weit weg |
| `Animator.Play()` mit String | Hash-Lookup pro Call | `Animator.Play(Hash)` (gecacht) |

### 9.3 LOD-System

```csharp
// Pflicht für alle Heroes/Bosse/Environment-Props
public class LODGroupConfig
{
    public LODLevel LOD0 = new() { ScreenSize = 0.6f }; // Hi-Detail
    public LODLevel LOD1 = new() { ScreenSize = 0.3f }; // Mid
    public LODLevel LOD2 = new() { ScreenSize = 0.1f }; // Low
}
```

LOD-Budgets siehe [ASSETS_AI.md §12.1](ASSETS_AI.md#121-polygon-budgets-mid-tier-android-ziel).

### 9.4 Hardware-Tier-aware Code

```csharp
public class ParticleSpawner
{
    private readonly IHardwareProfileService _hwProfile;
    
    public int GetParticleCount()
    {
        return _hwProfile.Tier switch
        {
            HardwareTier.Low => 100,
            HardwareTier.Mid => 300,
            HardwareTier.High => 800,
            HardwareTier.Ultra => 1500,
            _ => 300,
        };
    }
}
```

---

## 10. Test-Pflichten

### 10.1 Tests bei Code-Änderung

Pflicht für:
- Domain-Logik (Card-Resolution, AI-Pathfinding, Combat-Berechnungen)
- Service-Logik (Liga-Berechnung, BP-XP, Achievement-Trigger)
- Algorithmen (LevelLayoutGenerator, DungeonSynergyResolver)
- Math-Funktionen (Overflow-Guard, Liga-Punkt-/Sub-Tier-Berechnung, Combo-Score, ISO-Wochen-Seed)

Optional für:
- Reine UI-Verdrahtung
- Trivial-Property-Wrapper
- Avalonia-/Unity-API-Wrapper

### 10.2 Determinismus-Suite

```csharp
[Test]
public void Determinism_AllReplays_ProduceIdenticalHash()
{
    var replays = LoadAllReplays("ReplayCorpus/");
    foreach (var replay in replays)
    {
        var simulator = new BattleSimulator(seed: replay.Seed);
        var finalState = simulator.RunReplay(replay.Inputs);
        var hash = ComputeStateHash(finalState);
        Assert.AreEqual(replay.ExpectedHash, hash, 
            $"Replay {replay.MatchId} produced different hash!");
    }
}
```

CI-Gate: Pflicht-Check in jedem PR. Failure blockt Merge.

### 10.3 Mock-Framework

```csharp
[Test]
public void HeroService_UnlockHero_TriggersAnalyticsEvent()
{
    var analytics = Substitute.For<IAnalyticsService>();
    var saveService = Substitute.For<ISaveService>();
    var heroDb = ScriptableObject.CreateInstance<HeroDatabase>();
    var sut = new HeroService(new SystemRngProvider(), heroDb, NullLogger<HeroService>.Instance);
    
    sut.UnlockHero("pulse");
    
    analytics.Received().Track("hero_unlock", Arg.Any<Dictionary<string, object>>());
}
```

---

## 11. Git-Workflow

### 11.1 Branch-Strategie

- `main` — Production-ready, deploy-bar
- `develop` — Integration-Branch, alle Features mergen hier rein
- `feature/{name}` — Feature-Branches
- `hotfix/{name}` — Notfall-Fixes auf Production
- `release/v{n}` — Release-Branches (Saison-Cuts)

### 11.2 PR-Checkliste

- [ ] Branch von `develop` (oder `main` für Hotfix)
- [ ] CI grün (Build + EditMode + PlayMode + Determinismus)
- [ ] Code-Review approved (mindestens 1 Reviewer)
- [ ] Tests für neue Logik
- [ ] Lokale Performance-Check auf Mid-Tier-Device
- [ ] CLAUDE.md / DESIGN.md / ARCHITECTURE.md aktualisiert falls relevant
- [ ] Commit-Message auf Deutsch, klar formuliert

### 11.3 Git-LFS

Folgende Datei-Typen sind LFS-pflichtig:
- `*.psd, *.ai, *.png, *.jpg, *.tga` (Art)
- `*.fbx, *.obj, *.gltf, *.glb` (3D-Models)
- `*.wav, *.mp3, *.ogg` (Audio)
- `*.mp4, *.webm` (Video)
- `*.unity` (Scenes, große Binär-Files)
- `*.asset` (ScriptableObject-Assets > 1 MB)

`.gitattributes`:
```
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.fbx filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.unity filter=lfs diff=lfs merge=lfs -text
```

### 11.4 .gitignore (Unity-Standard)

```
Library/
Temp/
obj/
Build/
Builds/
Logs/
UserSettings/
*.csproj
*.sln
*.suo
*.user
*.pidb
.vs/
.vscode/
.idea/
```

---

## 12. Commit-Verhalten

### 12.1 Auto-Commit (wie globale Regel)

- **Nach abgeschlossenen Änderungen selbstständig sinnvoll/logisch committen** (globale Arbeitsweise).
- Mehrere thematisch unabhängige Änderungen = mehrere Commits. Keine Mega-Commits, keine Mini-Commits pro Datei.
- Nur bei Massen-Reverts/Force-Push/History-Rewrite vorher fragen.

### 12.2 Commit-Message-Format

**Sprache: Deutsch**
**Format:** `BomberBlast.Unity: Kurze Beschreibung`

```
BomberBlast.Unity: Hero-Service portiert

- HeroDefinition als ScriptableObject (5 echte Helden: Default/SpeedySam/BrickBoris/TwinTina/LuckyLola)
- HeroDatabase mit Stats + HeroTrait + Unlock-Bedingungen (1:1 aus dem Original)
- Unit-Tests für HeroService.UnlockHero()
- DI-Registrierung in RootLifetimeScope

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

## 13. Bekannte Stolperfallen (Unity 6 + URP)

| Problem | Ursache | Lösung |
|---------|---------|---------|
| URP-Material wird pink im Build | Shader nicht im Build inkludiert | "Always Included Shaders" in `Edit → Project Settings → Graphics` |
| Forward+ Lichter werden nicht gerendert | Rendering-Pipeline-Asset nicht im Graphics-Settings | URP-Asset zuweisen in `Project Settings → Graphics → Default Render Pipeline` |
| `Camera.main` ist null auf Android | Tag "MainCamera" nicht gesetzt | Camera-Tag in Inspector setzen |
| Sprite Atlas wird nicht gepackt | "Include in Build" Toggle aus | In Sprite-Atlas-Inspector aktivieren |
| `OnMouse{Down/Up/Enter}` funktioniert nicht auf UI | Falsche Layer/EventSystem | Verwende `PointerEventData` via `IPointerXxxHandler` |
| Animator wechselt nicht in Sub-State | Transitions ohne Conditions oder Has-Exit-Time | Conditions setzen, "Has Exit Time" deaktivieren wenn nicht gewollt |
| `SerializeField` zeigt nicht im Inspector | Private Field ohne `[SerializeField]` | `[SerializeField] private int _value;` |
| Coroutine läuft nicht im Editor | `StartCoroutine` auf inaktivem GO | GO aktivieren ODER `EditorCoroutineUtility.StartCoroutineOwnerless` |
| Build hängt bei IL2CPP-Schritt | Code-Stripping aggressiv | `link.xml` mit Preserve-Direktiven für Reflection-Code |

---

## 14. Bekannte Stolperfallen (Photon)

| Problem | Ursache | Lösung |
|---------|---------|---------|
| Photon Fusion Lag-Spike auf 4G | Snapshot-Größe zu groß | Snapshot-Komprimierung aktivieren, irrelevante Properties nicht `[Networked]` |
| `OnConnectedToMaster` wird nicht gefeuert | Photon-AppId falsch | `PhotonServerSettings` prüfen, Region-Setting |
| Room-Join schlägt fehl mit "Room not found" | Race-Condition Master-Server vs Room-Server | Retry mit Exponential-Backoff |
| Voice-Chat hat Echo | Mic-Loopback an | `PhotonVoiceNetwork.SetSendingEnabled(false)` für eigene Audio-Source |
| Sync-Diskrepanzen in PvP | Determinismus-Bug (UnityEngine.Random?) | Determinismus-Audit, IRngProvider checken |
| `MasterClient`-Switch klappt nicht | Lokaler Spieler hat IsMasterClient=false aber wird neuer Master | `OnMasterClientSwitched` abwarten, dann Logic |
| Photon-Chat-Channel-Join schlägt fehl | Falsche `ChatChannelName`-Konvention | `globalChannel_{region}_{lang}` |
| `[Networked]` Property updated nicht visuell | RPC nicht angetriggert ODER falsche Authority | `[Networked, OnChangedRender]` Callback prüfen |

---

## 15. Bekannte Stolperfallen (Firebase Unity SDK)

| Problem | Ursache | Lösung |
|---------|---------|---------|
| Firebase-Auth crasht auf iOS | Missing PListEntry | `Info.plist` mit `CFBundleURLSchemes` für Google-SignIn |
| RTDB-Listener fires nicht nach Reconnect | Connection-State-Listener fehlt | `database.GoOnline()` + Connection-State checken |
| `auth.SignInAnonymouslyAsync()` hängt | Network-Timeout, kein Cancellation | `CancellationTokenSource` mit Timeout (5s) |
| Crashlytics-Symbol-Upload schlägt fehl im CI | Missing `google-services.json` | Secret in GitHub Actions setzen |
| Remote Config returnt Default obwohl Server-Wert gesetzt | Minimum-Fetch-Interval | `remoteConfig.SetConfigSettings(new ConfigSettings { MinimumFetchInternal = TimeSpan.Zero })` (NUR Debug) |
| `FirebaseInitProvider` crasht ohne Crashlytics-Gradle-Plugin | Gradle-Plugin fehlt | `mainTemplate.gradle` mit Crashlytics-Plugin-Dependency |
| Server-Timestamp wird zu Client-Local-Time konvertiert | Falsche Deserialization | `ServerValue.TIMESTAMP` als `long` lesen, manuell `DateTimeOffset.FromUnixTimeMilliseconds()` |

---

## 16. Bekannte Stolperfallen (Addressables)

| Problem | Ursache | Lösung |
|---------|---------|---------|
| Addressable lädt nicht im Build | Group nicht im Catalog inkludiert | "Include in Build" in Group-Settings |
| `AsyncOperationHandle` ist null | Handle nicht awaited | `await handle.Task` |
| Memory-Leak nach Scene-Unload | Addressables nicht released | `Addressables.Release(handle)` in `OnDestroy` |
| Catalog-Cache outdated nach Update | Cache nicht invalidiert | `Addressables.UpdateCatalogs()` beim App-Start |
| iOS-CDN-Loading schlägt fehl | App Transport Security blockiert HTTP | HTTPS verwenden ODER ATS-Exception in `Info.plist` |
| Bundle-Sizes zu groß | Falsche Asset-Gruppierung | Re-grouping nach Loading-Pattern |

---

## 17. Bekannte Stolperfallen (Localization)

| Problem | Ursache | Lösung |
|---------|---------|---------|
| Localization-Strings nicht aktualisiert nach Sprach-Wechsel | Static Cache | `LocalizationSettings.SelectedLocale = newLocale` + Listener auf `OnLanguageChanged` |
| Sprache erkennt System nicht | `Locale.CurrentLocale` falsch | `Locale.SystemLanguage` + Fallback auf EN |
| String-Tables nicht im Build | Nicht in Build inkludiert | "Preload Tables" in Localization-Settings |
| TextMeshPro zeigt Boxes (□□□) | Font hat kein Glyph für Unicode | TMP-Font-Asset mit erweitertem Character-Set |
| Plurale falsch in DE | Falsche ICU-Format-String | `{count, plural, one {1 Münze} other {{count} Münzen}}` |

---

## 18. Bekannte Stolperfallen (IL2CPP)

| Problem | Ursache | Lösung |
|---------|---------|---------|
| `JsonConvert.DeserializeObject<T>()` crash | Reflection-Type wurde gestripped | `link.xml` mit `<preserve>` für T |
| `Activator.CreateInstance<T>()` returnt null | Konstruktor gestripped | `[Preserve]`-Attribute auf Class |
| Generic-Methods fehlen | Generic-Code nicht eager kompiliert | `[Preserve]` auf Generic-Method |
| Native-Plugin-DLL nicht gefunden | Architektur-Mismatch | x86/x64/ARM64-Variante korrekt zuweisen |
| `DateTime.Parse()` mit Custom-Format crash | Strict-Parsing aktiv | `DateTimeStyles.AllowWhiteSpaces`-Flag setzen |

---

## 19. Agent-Trigger-Hinweise (für Claude Code Agents)

Wenn ein Agent (aus Repo-Root `.claude/agents/`) für BomberBlast Unity arbeitet, gelten zusätzlich:

### 19.1 Welche Agents passen

| Agent | Verwendung |
|-------|-----------|
| `architect` | Design-Entscheidungen, Modul-Grenzen, Asmdef-Struktur |
| `planner` | Feature-Planung mit Dateiliste + DI + Tests |
| `code-review` | Code-Review mit Unity-Pattern-Checks |
| `debugger` | Bug-Diagnose mit Hypothesen-Methode |
| `tester` | Unit-Tests + Edge-Cases (NUnit, Unity Test Framework) |
| `refactor` | Strukturelle Änderungen, Duplikate |
| `migrator` | Unity-Upgrades, Photon-Updates |
| `performance` | CPU/GC/UI-Stutter/Startup |
| `security` | Anti-Cheat, Firebase-Rules, AI-Voice-Lizenz |
| `documenter` | Kommentare, CLAUDE.md, Changelog |

### 19.2 Welche Agents NICHT passen

| Agent | Warum nicht |
|-------|------------|
| `mvvm-auditor` | Avalonia-spezifisch — Unity nutzt UGUI/UI-Toolkit-MVVM-Variante |
| `skiasharp` | Avalonia-spezifisch — nicht für URP |
| `xaml-ui` | Avalonia-spezifisch — wir nutzen UXML+USS |
| `stylist` | Avalonia-Theming — Unity hat eigenes Material/Shader-System |
| `proto-sync` | nicht für dieses Projekt |
| `geometry-expert` | nicht für 2D-Grid-Game |
| `bingxbot` | falsches Projekt |

### 19.3 Architektur-Verletzungen proaktiv korrigieren

Beim Lesen von Code auf Verletzungen achten:
- Schichten-Modell-Verstöße (Domain referenziert Game?)
- Service-Locator statt DI
- `UnityEngine.Random` im Gameplay-Code (statt IRngProvider)
- Hardcoded Texte statt Localization-Keys
- Hardcoded Werte (Farben, Speeds, Caps) statt ScriptableObject

Bei Verstoß: **Sofort korrigieren oder User informieren**. Nicht stillschweigend ignorieren.

---

## 20. Checkliste vor Commit

- [ ] Build erfolgreich (`Unity-Editor → Build → Android-AAB-Debug`)
- [ ] EditMode-Tests grün (alle Domain-Tests)
- [ ] Determinismus-Suite grün (1000+ Replays passen)
- [ ] Keine ungenutzten Events/Handler
- [ ] Keine hardcoded Werte (Farben, Speeds, Texte) im Code
- [ ] Einheitlicher Stil (gleiche Dateitypen gleich aufgebaut)
- [ ] CLAUDE.md / DESIGN.md / ARCHITECTURE.md aktualisiert wenn nötig
- [ ] PR-Description verlinkt relevante Sektion
- [ ] Code-Review angefragt

---

## 21. Verweise

### Repo-intern

- **[PLAN.md](PLAN.md)** — Master-Übersicht, Vision, KPIs, Roadmap-Summary
- **[DESIGN.md](DESIGN.md)** — Game-Design (Helden, Welten, Story, Karten, Modi, Live-Service)
- **[PARITY.md](PARITY.md)** — Parity-Matrix: jedes Original-System → Unity-Äquivalent + Port-Status (lebende Port-Checkliste)
- **[ARCHITECTURE.md](ARCHITECTURE.md)** — Tech-Stack, Asmdefs, Netcode, Anti-Cheat, Performance
- **[ROADMAP.md](ROADMAP.md)** — Team, Marketing, Compliance, Risiken, Sprint-Struktur
- **[ASSETS_AI.md](ASSETS_AI.md)** — KI-Asset-Pipeline (TRELLIS 2, SPAR3D, Stable Audio 3)
- **SETUP.md** — First-Time-Setup für Entwickler (folgt nach Projekt-Anlage)
- **Server/SERVEROPS.md** — Cloud-Functions-Server-Doku (folgt)

### Repo-Root (übergreifend)

- **[../../../CLAUDE.md](../../../CLAUDE.md)** — Repo-übergreifende Conventions (Build, deutsche Umlaute, MVVM)
- **[../ArcaneKingdom/CLAUDE.md](../ArcaneKingdom/CLAUDE.md)** — ArcaneKingdom (Schwester-Projekt, Unity-Lehrgeld bezahlt)
- **[../BomberBlast/CLAUDE.md](../BomberBlast/CLAUDE.md)** — Alt-BomberBlast (Domain-Code-Quelle)

### Tool-Docs

- Unity 6 — `https://docs.unity3d.com/6000.0/Documentation/Manual/`
- URP 17 — `https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/`
- VContainer — `https://vcontainer.hadashikick.jp/`
- UniTask — `https://github.com/Cysharp/UniTask`
- R3 — `https://github.com/Cysharp/R3`
- Photon Fusion — `https://doc.photonengine.com/fusion/current/`
- Photon Realtime — `https://doc.photonengine.com/realtime/current/`
- Firebase Unity SDK — `https://firebase.google.com/docs/unity/setup`
- DOTween — `http://dotween.demigiant.com/documentation.php`
- Cinemachine 3 — `https://docs.unity3d.com/Packages/com.unity.cinemachine@3.0/`
