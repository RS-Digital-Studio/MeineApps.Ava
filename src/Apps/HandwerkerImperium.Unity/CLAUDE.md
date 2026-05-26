# CLAUDE.md — HandwerkerImperium-Unity

> **Projekt-spezifische Conventions für Claude Code.**
> Diese Datei wird IMMER zuerst gelesen, bevor an HandwerkerImperium-Unity gearbeitet wird.

---

## 1. Projektübersicht

**Was:** Komplette Neuentwicklung von HandwerkerImperium in **Unity 6 (LTS)**, parallel zur bestehenden Avalonia-Version (`src/Apps/HandwerkerImperium/`).

**Warum:** Visueller Sprung um eine Generation (3D, GPU-Particles, Shader Graph, Post-Processing), bessere Mobile-Performance, einheitliche Audio/Animation/Input-Stacks. Avalonia-Version bleibt produktiv und wird weiter gepflegt.

**Aktueller Stand:** Pre-MVP (Konzept). Plan in [PLAN.md](PLAN.md), GDD in [DESIGN.md](DESIGN.md), Tech-Details in [ARCHITECTURE.md](ARCHITECTURE.md), Roadmap in [ROADMAP.md](ROADMAP.md).

**Migrations-Strategie:** Closed Beta parallel zur Avalonia-Production. Unity-Version wird unter eigener App-ID (`com.meineapps.handwerkerimperium2.beta`) released. Avalonia bleibt aktiv in Entwicklung. Cutover-Entscheidung erst nach erfolgreicher Beta.

**Codebase-Lage:**
- Avalonia (alt): `src/Apps/HandwerkerImperium/` — 27k LoC, 177 Services, 80 ViewModels, 74 Views, 59 SkiaSharp-Renderer
- Unity (neu): `src/Apps/HandwerkerImperium.Unity/` — dieses Projekt

---

## 2. Tech-Stack (Pflicht)

| Komponente | Wahl | Hinweis |
|------------|------|---------|
| **Unity** | 6000.4.8f1 (LTS) | Gleiche Version wie ArcaneKingdom — Engine-Patches geteilt |
| **C#** | C# 12 (Unity 6 Backend) | records, pattern matching, file-scoped namespaces |
| **Scripting Backend** | IL2CPP (Release), Mono (Editor) | AOT für Mobile |
| **Render-Pipeline** | URP 17.0.4 | 2D + 3D, Mobile-optimiert |
| **DI** | VContainer 1.16.9 | NICHT Zenject — AOT-kompatibel mit IL2CPP |
| **Async** | UniTask 2.5.10 | NICHT `Task<T>` — GC-frei |
| **JSON** | Newtonsoft.Json 3.2.2 | Kompatibilität mit Avalonia-Save-Format |
| **Lokalisierung** | Unity Localization 1.5.11 | String-Tables, NICHT RESX |
| **Asset-Loading** | Addressables 2.9.1 | NICHT Resources.Load (außer Bootstrap) |
| **Audio** | Unity AudioMixer | NICHT plattform-spezifisch (war Avalonia-Problem) |
| **Animation** | Animator + DOTween + Timeline | NICHT CSS-Hacks (war Avalonia-Problem) |
| **Camera** | Cinemachine 2.10+ | Orbit + Pan + Shake mit Impulse-Source |
| **UI** | UI Toolkit (statische Screens) + uGUI (animierte Screens) | Gemischt nach Bedarf |
| **Text** | TextMesh Pro | NICHT uGUI Text — bessere Typografie, Emoji, Rich Text |
| **Input** | New Input System 1.19.0 | NICHT Legacy Input — Touch-Gesten nativ |
| **Test** | Unity Test Framework 1.5.1 (NUnit) | Domain-Tests EditMode, Game-Tests PlayMode |

**Nicht erlaubt (verbieten):**
- `Task.Run` für GameLogic → `UniTask.RunOnThreadPool` benutzen
- `MonoBehaviour` in Domain-Layer → Domain ist Unity-frei
- `GameObject.Find` / `FindObjectOfType` → DI via VContainer
- `Singleton-Pattern` (statische Instances) → DI-Singleton
- `Resources.Load` außerhalb Bootstrap-Scene → Addressables
- `Coroutines` mit `WaitForSeconds` für Spiellogik → UniTask + GameClock
- `string` für Asset-Pfade → Typed-Reference oder AssetReference
- Hardcoded Spiel-Werte → ScriptableObject BalancingConfig
- `DateTime.Now` → `DateTime.UtcNow` (Timezone-Bugs)

---

## 3. Projekt-Struktur

```
src/Apps/HandwerkerImperium.Unity/
├── CLAUDE.md  ← diese Datei
├── PLAN.md    ← Strategischer Plan
├── DESIGN.md  ← Game Design Document
├── ARCHITECTURE.md  ← Tech-Details
├── ROADMAP.md ← Wochenplan
├── ASSETS_AI.md  ← KI-Asset-Pipeline (3D-Meshes/PBR via ComfyUI/Hunyuan3D)
├── README.md
├── SETUP.md
└── Unity/
    └── Assets/
        ├── _Project/
        │   ├── Scripts/  (7 Assembly Definitions)
        │   │   ├── Bootstrap/   → HandwerkerImperium.Bootstrap.asmdef
        │   │   ├── Core/        → HandwerkerImperium.Core.asmdef
        │   │   ├── Domain/      → HandwerkerImperium.Domain.asmdef (NO UnityEngine reference)
        │   │   ├── Game/        → HandwerkerImperium.Game.asmdef
        │   │   ├── UI/          → HandwerkerImperium.UI.asmdef
        │   │   ├── Editor/      → HandwerkerImperium.Editor.asmdef
        │   │   └── Tests/       → HandwerkerImperium.Domain.Tests.asmdef
        │   ├── ScriptableObjects/
        │   ├── Scenes/
        │   ├── Prefabs/
        │   ├── Art/
        │   ├── Audio/
        │   ├── Addressables/
        │   └── Resources/  (NUR Bootstrap!)
        ├── ThirdParty/
        └── StreamingAssets/
```

**Assembly-Hierarchie (Reihenfolge ist Pflicht — keine zirkulären Refs):**
```
Core
 └── Domain (NO UnityEngine, NO Game, NO UI — pure C#)
      └── Game (use UnityEngine + Domain)
           └── UI (use Game + Domain)
                └── Bootstrap (use UI + Game + Domain)

Editor: standalone, refs Domain + Game (Editor-only)
Tests: refs Domain (NUnit, EditMode-only)
```

**Test-Coverage:**
- Domain-Layer: **≥80%** (Pflicht für jede neue Domain-Klasse)
- Game-Layer: **≥50%** (wo möglich, ohne UnityEngine-Mocks)
- UI-Layer: Optional (manuelle QA bevorzugt)

---

## 4. Namespace-Konventionen

**Pattern:** `HandwerkerImperium.{Module}` (`HWI` kein Prefix außer in IL/Internal-Helpers).

| Layer | Namespaces |
|-------|------------|
| Core | `HandwerkerImperium.Core` |
| Domain | `HandwerkerImperium.Domain.Workshops`, `HandwerkerImperium.Domain.Workers`, `HandwerkerImperium.Domain.Orders`, `HandwerkerImperium.Domain.Research`, `HandwerkerImperium.Domain.Prestige`, `HandwerkerImperium.Domain.Crafting`, `HandwerkerImperium.Domain.Guild`, `HandwerkerImperium.Domain.Save`, `HandwerkerImperium.Domain.Economy`, `HandwerkerImperium.Domain.Achievements`, `HandwerkerImperium.Domain.Quests`, `HandwerkerImperium.Domain.BattlePass`, `HandwerkerImperium.Domain.LiveEvents`, `HandwerkerImperium.Domain.MiniGames`, `HandwerkerImperium.Domain.Chat` |
| Game | `HandwerkerImperium.Game.Services`, `HandwerkerImperium.Game.Controllers`, `HandwerkerImperium.Game.Cloud`, `HandwerkerImperium.Game.Platform`, `HandwerkerImperium.Game.Security`, `HandwerkerImperium.Game.Audio`, `HandwerkerImperium.Game.Rendering` |
| UI | `HandwerkerImperium.UI.Screens`, `HandwerkerImperium.UI.Foundation`, `HandwerkerImperium.UI.Controls`, `HandwerkerImperium.UI.Animations`, `HandwerkerImperium.UI.Bindings` |
| Bootstrap | `HandwerkerImperium.Bootstrap` |
| Editor | `HandwerkerImperium.Editor` |

**File-scoped namespaces (Pflicht):**
```csharp
namespace HandwerkerImperium.Domain.Workshops;

public sealed class WorkshopUpgradeRules
{
    // ...
}
```

---

## 5. Code-Style

### 5.1 C# 12 Features (verbindlich nutzen)

| Feature | Beispiel |
|---------|----------|
| **Primary Constructors** | `public sealed class IncomeCalculator(BalancingConfig config, ILogger logger)` |
| **File-scoped Namespaces** | `namespace HandwerkerImperium.Domain.Workshops;` |
| **Records** für DTOs | `public record OrderCompletedEvent(string OrderId, decimal Reward);` |
| **Pattern Matching** | `if (worker is { Tier: WorkerTier.Legendary, Mood: > 80 }) { ... }` |
| **Switch Expressions** | `var multiplier = tier switch { WorkerTier.F => 1.0f, WorkerTier.E => 1.5f, _ => 0 };` |
| **Collection Expressions** | `string[] colors = ["Red", "Green", "Blue"];` |
| **Required Members** | `public required string DisplayName { get; init; }` |
| **Raw String Literals** | `var json = """{"name":"Hans"}""";` |
| **Nullable Reference Types** | Pflicht: `#nullable enable` in jeder Datei |

### 5.2 Async-Pattern (UniTask)

```csharp
// RICHTIG
public async UniTask<Result<Order>> AcceptOrderAsync(string orderId, CancellationToken ct)
{
    var order = await _orderRepository.GetAsync(orderId, ct);
    return Result.Success(order);
}

// FALSCH
public async Task<Order> AcceptOrder(string orderId)
{
    var order = await _orderRepository.GetAsync(orderId);
    return order;
}
```

**Regeln:**
- `UniTask` statt `Task<T>`
- Methode mit Suffix `Async`
- `CancellationToken` als **letzter Parameter** (Konvention)
- Result<T> bei Fehlern, **keine** Exceptions in der Game-Loop

### 5.3 Naming

| Element | Convention | Beispiel |
|---------|-----------|----------|
| **Klasse** | PascalCase | `WorkshopService`, `IncomeCalculator` |
| **Interface** | I-Prefix | `IWorkshopService`, `IIncomeCalculator` |
| **Methode** | PascalCase | `CalculateIncome`, `LoadAsync` |
| **Private Field** | `_camelCase` | `_balancingConfig`, `_logger` |
| **Property** | PascalCase | `CurrentLevel`, `IsLocked` |
| **Constant** | UPPER_SNAKE | `MAX_WORKERS_PER_WORKSHOP` |
| **Enum** | PascalCase, Members PascalCase | `WorkerTier.Legendary` |
| **Event-Records** | `On{Verb}{Past}` oder `{Subject}{Verb}{Past}Event` | `OnOrderCompleted`, `OrderCompletedEvent` |
| **Service** | Suffix `Service` | `OrderService`, `GuildService` |
| **Controller** | Suffix `Controller` (Feature-Orchestrator) | `BattlePassController` |
| **ViewModel** | Suffix `ViewModel` | `DashboardViewModel` |
| **View** | Suffix `View` (UI Toolkit) oder `Panel` (uGUI) | `DashboardView`, `OrderListPanel` |
| **ScriptableObject** | Suffix `Definition` oder `Config` | `WorkshopDefinition`, `BalancingConfig` |
| **MonoBehaviour-Component** | Suffix `Behaviour` oder `Component` | `WorkerAvatarBehaviour` |

### 5.4 Sprache in Code

- **Kommentare:** Deutsch (mit Umlauten — UTF-8 garantiert)
- **Logging:** Englisch (zur internationalen Lesbarkeit, z.B. via Crashlytics-Logs)
- **Lokalisierungs-Keys:** Englisch (`workshop.holzwerkstatt.name`)
- **String-Values der Localization-Tables:** je Sprache
- **Identifier (Klassen/Methoden):** Englisch
- **Domain-Begriffe** (die spielspezifisch sind): Deutsch (`Werkstatt`, `Auftrag`, `Prestige`) ODER Englisch (`Workshop`, `Order`, `Prestige`) — **konsistent halten**, Empfehlung: **Englisch im Code, Deutsch in UI/Lokalisierung**

### 5.5 Kommentare

- Pflicht: **XML-Docs** auf allen `public` Klassen, Methoden, Properties (für Code-Completion)
- Inline-Kommentare nur wenn das *Warum* nicht-offensichtlich ist
- **TODO-Format:** `// TODO(#42): ...` (mit Issue-Verweis)
- **Keine alten Code-Kommentare** (`// removed`, `// old: ...`) → einfach löschen

```csharp
/// <summary>
/// Berechnet das passive Einkommen einer Werkstatt pro Sekunde.
/// Ohne Prestige-/Event-Multiplikatoren — die werden separat angewendet.
/// </summary>
public decimal CalculateBaseIncome(Workshop workshop)
{
    // Income-Formel aus Avalonia-Balancing (V7): BaseValue × 1.02^Level
    var rawIncome = workshop.BaseValue * (decimal)Math.Pow(1.02, workshop.Level);
    return rawIncome;
}
```

---

## 6. DI-Pattern (VContainer)

### 6.1 Lifetime-Regeln

| Service-Typ | Lifetime |
|-------------|----------|
| **Domain-Calculators** (IncomeCalculator, OrderGenerator, PrestigeRules) | Singleton |
| **Game-Services** (WorkshopService, WorkerService, OrderService) | Singleton |
| **Platform-Services** (IAuthService, IPurchaseService, IRewardedAdService) | Singleton |
| **Coordinators** (GameStartupCoordinator, ProgressionFeedbackCoordinator) | Singleton |
| **ViewModels** (für transient-Modals) | Transient |
| **Scene-Controllers** (BattleController, WorkshopController) | Scoped (per Scene-Scope) |

### 6.2 Constructor Injection (Pflicht)

```csharp
// RICHTIG
public sealed class OrderService(
    IIncomeCalculator incomeCalculator,
    IOrderRepository orderRepository,
    ILogger logger,
    IEventBus eventBus)
{
    // Primary Constructor — keine Felder mehr nötig
    public async UniTask AcceptOrderAsync(string orderId, CancellationToken ct)
    {
        logger.Log($"Accepting order {orderId}");
        // ...
    }
}

// FALSCH
public class OrderService
{
    public OrderService()  // Parameterlos
    {
        _incomeCalculator = ServiceLocator.Resolve<IIncomeCalculator>();  // ServiceLocator verboten!
    }
}
```

**Verbieten:**
- ❌ `ServiceLocator.Resolve<T>()` (außerhalb Bootstrap)
- ❌ `Container.Resolve<T>()` aus VContainer (außer in Bootstrap)
- ❌ Statische `Instance`-Properties (`MyService.Instance`)
- ❌ Property Injection (außer für optionale Dependencies)

### 6.3 Container-Facades (gegen Service-Sprawl)

Aus Avalonia übernommen — Bündel mehrere zusammengehörige Services:

```csharp
public interface IGuildFacade
{
    GuildService Guild { get; }
    GuildCoopOrderService CoopOrders { get; }
    WorkerAuctionService Auctions { get; }
    GuildBossService Boss { get; }
    GuildHallService Hall { get; }
    GuildWarSeasonService WarSeason { get; }
    GuildMegaProjectService MegaProjects { get; }
    GuildChatService Chat { get; }
    GuildAchievementService Achievements { get; }
}

public sealed class GuildFacade(
    GuildService guild,
    GuildCoopOrderService coopOrders,
    /* ... */) : IGuildFacade
{
    public GuildService Guild => guild;
    /* ... */
}
```

**Bekannte Facades:**
- `IGuildFacade` — 9 Gilden-Services
- `IWorkerFacade` — Worker + Auction
- `IProgressionFacade` — Prestige + Rebirth + Ascension + EternalMastery
- `IMissionsFacade` — Daily + Weekly + LuckySpin + QuickJob

---

## 7. MVVM-Light Pattern

### 7.1 Schichten

```
View (UXML oder Prefab)
  └── ViewBinder (MonoBehaviour)
        ├── holt UI-Refs
        ├── registriert UI-Events → ruft VM-Commands
        └── subscribt auf VM-Properties → updated UI
              ▲
              │
        ViewModel (POCO, Unity-frei)
              ├── ObservableProperty<T>
              ├── RelayCommand
              └── Services (per VContainer injected)
```

### 7.2 ViewModel-Regeln

- **Unity-frei** — keine `UnityEngine`-Refs (außer ggf. `Vector2`/`Color`-Records aus Helpers)
- **Constructor Injection** — alle Dependencies als Parameter
- **ObservableProperty<T>** statt `INotifyPropertyChanged` (eigenes Lib)
- **Async-Commands** via UniTask
- **Unit-testbar** ohne Unity-Editor (NUnit)

```csharp
public sealed class DashboardViewModel(
    GameStateService gameState,
    OrderService orderService,
    IEventBus eventBus)
{
    public ObservableProperty<decimal> Money { get; } = new();
    public ObservableProperty<int> Level { get; } = new();
    public ObservableProperty<int> ActiveOrderCount { get; } = new();

    public RelayCommand AcceptNextOrderCommand { get; }

    private readonly IDisposable _moneyChangedSub;

    public DashboardViewModel(/* primary ctor params */) : this(...)
    {
        Money.Value = gameState.Money;
        AcceptNextOrderCommand = new RelayCommand(async ct => await orderService.AcceptNextAsync(ct));

        _moneyChangedSub = eventBus.Subscribe<MoneyChangedEvent>(e => Money.Value = e.NewAmount);
    }
}
```

### 7.3 ViewBinder-Regeln

- **MonoBehaviour** — wohnt in der Scene
- **Holt UI-Refs** im `Awake()` oder Inspector-Reference
- **Subscribt auf VM** im `OnEnable()`, **unsubscribt** im `OnDisable()`
- **Niemals Domain-Logik** im ViewBinder — nur UI-Verdrahtung

```csharp
public sealed class DashboardViewBinder : MonoBehaviour
{
    [Inject] private DashboardViewModel _vm;
    [SerializeField] private TextMeshProUGUI _moneyLabel;
    [SerializeField] private Button _acceptButton;

    private CompositeDisposable _disposables;

    private void OnEnable()
    {
        _disposables = new CompositeDisposable();
        _vm.Money.Subscribe(m => _moneyLabel.text = m.ToString("C")).AddTo(_disposables);
        _acceptButton.onClick.AddListener(() => _vm.AcceptNextOrderCommand.Execute());
    }

    private void OnDisable()
    {
        _disposables?.Dispose();
        _acceptButton.onClick.RemoveAllListeners();
    }
}
```

---

## 8. Scene-Strategie

### 8.1 Scene-Layout

| Scene | Lifetime | Inhalt |
|-------|----------|--------|
| **Boot.unity** | DontDestroyOnLoad | RootLifetimeScope, Splash, Transitions, AudioListener |
| **Hub.unity** | Additive | Haupt-Hub mit 5 Tabs, 3D-City-Übersicht |
| **Workshop.unity** | Additive | 3D-Detail-Ansicht der ausgewählten Werkstatt |
| **MiniGame.unity** | Additive | Container für 3D-Mini-Games (lädt Prefab pro Game) |
| **Prestige.unity** | Additive | Cinematic-Szene für Prestige-Sequenz |
| **Guild.unity** | Additive | Gilden-Hub mit 3D-Gebäuden |

### 8.2 Scene-Loading-Regeln

- **Boot bleibt persistent**, andere Scenes werden additive geladen/entladen
- **`SceneLoaderService.LoadAsync<TScope>(sceneName)`** — lädt Scene + wartet auf LifetimeScope.Ready
- **Cross-Fade-Transitionen** via persistentem Canvas in Boot
- **`Addressables.Release`** nach Scene-Unload — wichtig für Memory!

---

## 9. Lokalisierung

### 9.1 String-Table-Keys (Pattern)

| Kategorie | Pattern | Beispiel |
|-----------|---------|----------|
| Werkstätten | `workshop.<id>.name` / `desc` | `workshop.holzwerkstatt.name` |
| Worker | `worker.tier.<tier>.name` | `worker.tier.legendary.name` |
| Aufträge | `order.<type>.title` | `order.quick.title` |
| Achievements | `achievement.<id>.name` / `desc` | `achievement.first_workshop.name` |
| Story-Chapters | `story.chapter.<n>.title` / `content` | `story.chapter.5.title` |
| Mini-Games | `minigame.<id>.title` / `tip` | `minigame.sawing.tip` |
| Tutorials | `tutorial.step.<n>.title` / `body` | `tutorial.step.3.body` |
| Notifications | `notification.<trigger>.title` / `body` | `notification.research_done.body` |
| UI-Elemente | `ui.<context>.<element>` | `ui.dashboard.accept_button` |

### 9.2 Sprachen

- **DE** (primär, vollständig)
- **EN** (vollständig)
- **ES**, **FR**, **IT**, **PT** (Auto-Übersetzung + 1-2 Pässe Review)
- **CJK** (Phase 2 — TextMeshPro Font-Asset erforderlich)

### 9.3 Migration aus Avalonia-RESX

Einmaliges Editor-Tool (`HandwerkerImperium → Localization → Import from RESX`):
- Liest alle 6 RESX-Dateien aus Avalonia
- Erstellt Unity Localization String-Tables
- Loggt fehlende Keys

---

## 10. Save-Schema

### 10.1 Version

**Aktuell:** v8 (Unity) — basiert auf Avalonia v7 + Unity-Erweiterungen

```csharp
public sealed class HwiSave
{
    public int SchemaVersion { get; set; } = 8;
    // ... Felder
}
```

**`SaveMigrator.CurrentSchemaVersion = 8`** ist Single-Source-of-Truth.

### 10.2 Save-Slices

Save-Daten sind in **Slices** unterteilt — modular, einzeln migrierbar:

```
HwiSave
├── GameStateSlice         (Money, Level, XP, Premium-Flags)
├── WorkshopsSlice         (10 Werkstätten + Levels)
├── WorkersSlice           (Worker-Liste + Stats)
├── OrdersSlice            (Active + Queue + Live-Orders)
├── ResearchSlice          (45 Nodes)
├── PrestigeSlice          (Tier, Count, Boni, Heirloom)
├── AscensionSlice         (Perks, Permanent-Heirlooms)
├── CraftingSlice          (Inventar + Reservierungen)
├── WarehouseSlice         (Slots + Stack-Limit + Auto-Sell)
├── GuildSlice             (Membership + Research-Cache)
├── EquipmentSlice         (5 Rarity × 3 Slots)
├── AchievementSlice       (Unlocked + Progress)
├── DailyChallengeSlice    (Aktuelle Challenges)
├── WeeklyMissionSlice     (Aktuelle Missions)
├── BattlePassSlice        (Tier, Free/Premium-Progress)
├── LiveEventSlice         (Aktuelle Events + Score)
├── StatisticsSlice        (Counters, Totals)
├── TutorialSlice          (FTUE-State, Contextual-Hints)
├── BoostSlice             (Speed-Boost, Crafting-Boost)
├── SettingsSlice          (Language, Graphics, Audio, Vibration, PostFX)
├── CosmeticSlice          (Skins, Cosmetic-Items)
└── UnitySpecificSlice     (PostFx-Quality, Vibration, AssetCatalogVersion)
```

### 10.3 Persistenz-Trigger

| Trigger | Wann |
|---------|------|
| Sofort | Order-Complete, Prestige, Workshop-Kauf, Worker-Hire, IAP-Erfolg |
| 30s AutoSave | Im Hintergrund (UniTask, kein UI-Block) |
| App-Pause/Background | OnApplicationPause(true) |
| Bei Cloud-Sync | Server-Roundtrip |

### 10.4 HMAC-Signierung (Anti-Cheat)

- **`HmacSigner.Sign(data, key)`** für kritische Werte (Money, Goldscrews, BossDamage, AuctionBid)
- Server validiert vor Persist
- Bei Mismatch: Save-Reset, Log-Eintrag, ggf. Account-Flag

---

## 11. Test-Convention

### 11.1 NUnit (Domain-Tests, EditMode)

```csharp
namespace HandwerkerImperium.Domain.Tests.Workshops;

public sealed class IncomeCalculatorTests
{
    [Test]
    public void CalculateBaseIncome_LevelOne_ReturnsBaseValue()
    {
        // Arrange
        var config = TestData.DefaultBalancingConfig();
        var calculator = new IncomeCalculator(config);
        var workshop = TestData.HolzwerkstattLevel1();

        // Act
        var income = calculator.CalculateBaseIncome(workshop);

        // Assert
        Assert.That(income, Is.EqualTo(1m));
    }
}
```

**Naming-Convention:** `MethodName_Scenario_ExpectedResult`

### 11.2 PlayMode-Tests (Game-Layer)

```csharp
namespace HandwerkerImperium.Game.Tests;

public sealed class GameLoopIntegrationTests
{
    [UnityTest]
    public IEnumerator GameLoop_TicksOncePerSecond_IncreasesMoney()
    {
        // Setup mit Test-Container
        yield return new WaitForSeconds(2.5f);
        // Assert: Money increased
    }
}
```

### 11.3 Coverage-Ziele

| Layer | Ziel |
|-------|------|
| Domain | ≥ 80% (Pflicht für PR-Merge) |
| Game | ≥ 50% |
| UI | Optional (manuelle QA) |

---

## 12. Editor-Tools

### 12.1 First-Time-Setup-Wizard

**Menü:** `HandwerkerImperium → Setup → First-Time Setup Wizard`

3-Klick-Initialisierung:
1. `BalancingConfig.asset` erzeugen
2. ScriptableObjects aus JSON importieren
3. Build-Scenes registrieren (Boot.unity Index 0, Hub.unity Index 1, etc.)

### 12.2 DataImporter

**Menü:** `HandwerkerImperium → Data → Import All`

- Liest JSON aus `Assets/StreamingAssets/Data/`
- Erstellt ScriptableObjects unter `Assets/_Project/ScriptableObjects/`
- Validierung: Konstanten, Referenzen, Cap-Checks
- Soft-Fail: Bei Fehler nur Warnung, kein Throw

### 12.3 BalancingDashboard

**Menü:** `HandwerkerImperium → Balancing → Dashboard`

- Editor-Window für `BalancingConfig`
- Live-Editing der Werte (Hot-Reload)
- Export zu Firebase Remote Config

### 12.4 LocalizationCheckTool

**Menü:** `HandwerkerImperium → Localization → Check`

- Prüft String-Table-Vollständigkeit (6 Sprachen)
- Listet fehlende Keys
- Validiert Placeholder-Konsistenz

### 12.5 SaveGameEditor

**Menü:** `HandwerkerImperium → Tools → Save Editor`

- Lädt `persistentDataPath/save.json`
- Tree-View aller Save-Slices
- Editierbar mit Validation

### 12.6 CheatsWindow (Nur Dev-Build)

**Menü:** `HandwerkerImperium → Cheats → Show Window` (nur wenn `DEV_BUILD` definiert)

- Money setzen
- Workshop-Level setzen
- Achievement entsperren
- Prestige forcieren
- Live-Event triggern

### 12.7 BuildScripts

**Menü:**
- `Build → Android Release` (signed AAB für Play Store)
- `Build → Android Dev` (unsigned, mit DEV_BUILD-Define)
- `Build → iOS Release` (Phase 2)

---

## 13. Spielmechanik-Regeln (kritisch)

### 13.1 Service-Caches resetten nach Prestige

**Aus Avalonia-Gotcha #1 übernommen:**

```csharp
public sealed class SomeServiceWithCache
{
    public SomeServiceWithCache(IEventBus eventBus)
    {
        eventBus.Subscribe<StateLoadedEvent>(_ => ResetCaches());
        eventBus.Subscribe<PrestigeCompletedEvent>(_ => ResetCaches());
    }

    private void ResetCaches() { /* ... */ }
}
```

**ALLE Services mit Caches MÜSSEN sich auf `StateLoadedEvent` UND `PrestigeCompletedEvent` subscriben.**

### 13.2 HMAC-Signierung für kritische Werte

Werte, die manipuliert werden könnten, MÜSSEN HMAC-signiert sein:
- Money (decimal)
- GoldenScrews (int)
- BossDamage (int)
- AuctionBid (decimal)
- CoopOrderScore (long)
- MegaProjectContribution (long)

```csharp
var signature = HmacSigner.Sign($"{playerId}|{money}|{timestamp}", _secretKey);
await _firebase.SetAsync($"players/{playerId}/money", new SignedValue(money, signature));
```

### 13.3 Firebase-Pfade

**Schema 1:1 wie Avalonia** — siehe [ARCHITECTURE.md § Network-Layer](ARCHITECTURE.md).

**Wichtig bei neuen Pfaden:**
1. Eintrag in `Server/DatabaseRules/database.rules.json`
2. `.indexOn` für `orderBy`-Queries setzen
3. Test mit Stubs vor Production

### 13.4 DateTime

- Persistenz: IMMER `DateTime.UtcNow.ToString("O")`
- Parse: IMMER `DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)`
- Server-Timestamp via Firebase: `{".sv":"timestamp"}` für Anti-Spoofing

### 13.5 PlayerId

- **Stabile UUID** (nicht Firebase-UID!) — beim ersten Login generiert
- Dauerhaft mit Google Account verknüpft
- Bei Token-Refresh wird `/auth_to_player/{uid} → PlayerId` aktualisiert

---

## 14. UI-Regeln

### 14.1 UI Toolkit vs uGUI

| Verwende **UI Toolkit** (UXML/USS) für: | Verwende **uGUI** (Canvas) für: |
|-----------------------------------------|----------------------------------|
| Listen, Tabellen, Settings, Inventory | Mini-Games (animations-heavy) |
| Statische Dialoge | Achievement-3D-Trophäen-Popup |
| Codex, Achievements-Browser | Floating-Text, Damage-Numbers |
| Forms, Inputs | Card-Reveal-Animationen |

### 14.2 TextMeshPro

- **Pflicht** für alle Texte (statt `Text`)
- Pro Sprache eigenes Font-Asset (DE/EN ein Asset, CJK separat)
- Sprite-Asset für Inline-Icons (Goldscrew, Money-Symbol, etc.)

### 14.3 Layout-Regeln

- **Adaptive Layouts** via UI Toolkit Flexbox oder uGUI ContentSizeFitter
- **Safe-Area** beachten (Notch, Camera-Cut-Out)
- **Min/Max-Größen** für Buttons, Cards (Touch-Target ≥ 44dp)
- **Lokalisierungs-Reserve** (Strings können 1.5x länger werden in DE/FR)

### 14.4 Animation-Regeln

- **Animator** für komplexe State-Machines (Worker, Buttons)
- **DOTween** für simple UI-Animationen (Scale, Move, Fade)
- **Timeline** für längere Sequenzen (Prestige, Tutorial-Tour)
- **Cinemachine** für Camera-Bewegungen

**Verbieten:**
- ❌ `Coroutine` für UI-Animationen → DOTween
- ❌ `Time.deltaTime` für UI-Animationen → DOTween-eigenes Time-Scaling
- ❌ Hardcoded `Animator.Play("StateName")` → typsicheres Wrapper

---

## 15. Audio-Regeln

### 15.1 AudioMixer-Gruppen

```
Master
├── Music (BGM)
├── SFX
│   ├── UI-SFX
│   ├── Game-SFX
│   └── MiniGame-SFX
├── Voice (optional Phase 2)
└── Ambience
```

### 15.2 Ducking

- Music duckt bei Dialog: `-12dB`, 300ms ease
- Music duckt bei wichtigen Sounds (Achievement, Prestige): `-6dB`, 500ms

### 15.3 SFX-Limits

- Max 16 gleichzeitige SFX (sonst Cut-Off)
- Pooling für häufige SFX (Tap, Coin-Drop)

### 15.4 Music-Crossfade

- Default 800ms
- Boss-Music: 1500ms (smooth)
- Stinger-Sounds: Hardcut

---

## 16. Performance-Budgets

| Metrik | Ziel (Mid-Range-Mobile) |
|--------|-------------------------|
| FPS Hub-Idle | 60 |
| FPS Workshop-Detail (3D) | 60 |
| FPS Mini-Game | 60 |
| Cold-Start | <3s |
| Warm-Start | <1s |
| Memory (RAM) | <400 MB |
| Storage (APK/AAB) | <120 MB |
| Tex-Memory | <80 MB |
| Particle-Count | <2.000 gleichzeitig (Mobile) |

**Auf Low-End-Geräten:**
- Quality-Settings: Low (Post-FX off, weniger Particles, lower-res Textures)
- 2D-Fallback für 3D-Effekte (optional)
- Frame-Limit: 30 fps statt 60

---

## 17. Bekannte Probleme & Patterns

### 17.1 Aus Avalonia übernommen (gilt auch für Unity)

| Problem | Lösung |
|---------|--------|
| Service-Caches stale nach Prestige | `eventBus.Subscribe<StateLoadedEvent>(ResetCaches)` |
| Firebase-Pfad gibt null zurück | `.indexOn` in `database.rules.json` eintragen |
| Co-op Score: Last-Write-Wins | PATCH (atomar) statt PUT/SET |
| DateTime Timer falsch | `DateTimeStyles.RoundtripKind` bei Parse |
| Save-Game-Editor-Schutz | `SaveGameSanitizer` validiert vor Persist |
| Multi-Task-Order Timer-Bug | Event-getriebenes Restart pro Task |

### 17.2 Unity-spezifisch (zu beachten)

| Problem | Lösung |
|---------|--------|
| **MonoBehaviour kann nicht im Konstruktor injected werden** | Verwende `[Inject]` Field oder MethodInjection |
| **`Awake()` vs `OnEnable()` Race** | DI komplett im Awake, UI-Subs im OnEnable |
| **`OnDestroy()` wird nicht garantiert aufgerufen bei App-Quit** | Save-Logic muss in `OnApplicationPause(true)` |
| **Addressables-Memory-Leak** | Immer `Addressables.Release(handle)` aufrufen |
| **TextMeshPro CJK-Font fehlt** | Dynamic SDF-Font einrichten oder per Sprache separates Asset |
| **IL2CPP-Stripping entfernt Reflection** | `[Preserve]` Attribute oder `link.xml` |
| **VContainer mit IL2CPP: Generic-Code generieren** | `RuntimeInitializeOnLoadMethod` für Pre-Reservation |
| **Cinemachine Camera-Shake stoppt nicht** | `CinemachineImpulseSource.Pre Cleanup` |
| **DOTween-Tweens auf zerstörten GameObjects** | `tween.SetLink(gameObject)` |

### 17.3 Mobile-Build-Gotchas

| Problem | Lösung |
|---------|--------|
| **AAB-Size >150 MB** | Addressables Remote-Catalog (Phase 2), Texture-Compression-Audit |
| **APK Java Generics Erasure** | NICHT betroffen in Unity (IL2CPP eigene Path) |
| **Android Back-Button** | `Input.GetKeyDown(KeyCode.Escape)` → Double-Back-to-Exit |
| **Mono JIT Assertion Crash** | NICHT in IL2CPP-Build (Mono ist Editor-only) |
| **Notch / Safe-Area** | `Screen.safeArea` lesen, UI-Layout anpassen |

---

## 18. Git-Workflow

### 18.1 Branch-Strategie

- `master` — Avalonia-Hauptbranch (bleibt produktiv!)
- `unity-main` — Unity-Hauptbranch (parallel zur Avalonia-Entwicklung)
- `unity-feature/xxx` — Feature-Branches
- `unity-bugfix/xxx` — Bug-Fix-Branches

### 18.2 Commit-Convention

- **Sprache:** Deutsch
- **Format:** `Unity-HWI: Kurze Beschreibung` (Prefix unterscheidet Unity- von Avalonia-Commits)
- **Beispiele:**
  - `Unity-HWI: VContainer-Setup mit allen Services`
  - `Unity-HWI: Holzwerkstatt 3D-Model + Workshop.unity-Scene`
  - `Unity-HWI: BalancingConfig ScriptableObject + DataImporter`

### 18.3 PR-Reviews

- Min. 1 Reviewer
- Build muss grün sein (Unity Cloud Build oder lokaler Build-Check)
- Tests müssen laufen (NUnit + PlayMode)

---

## 19. Wichtige Skripte (Skill-Hooks)

| Skill | Zweck (analog Avalonia) |
|-------|------|
| `unity-build-check` | Build-Validation (folgt) |
| `unity-mvvm-check` | Audit für Code-Behind-Service-Locator, fehlende DI (folgt) |
| `unity-localize-check` | String-Table-Vollständigkeit (folgt) |

---

## 20. Quick-Reference

### Eine neue Werkstatt hinzufügen

1. `WorkshopDefinition.asset` unter `Assets/_Project/ScriptableObjects/Workshops/` erstellen
2. Ggf. 3D-Model + Material in `Art/3D/Workshops/`
3. ScriptableObject in `BalancingConfig.WorkshopList` registrieren
4. Lokalisierungs-Keys hinzufügen: `workshop.<id>.name`, `workshop.<id>.desc`
5. Tests in `Domain.Tests/Workshops/` erweitern

### Ein neues Mini-Game hinzufügen

1. `MiniGameDefinition.asset` erstellen
2. Prefab unter `Prefabs/MiniGames/<Name>/`
3. `IMiniGame`-Interface implementieren (Start, Update, GetScore)
4. In `MiniGameRegistry` registrieren
5. Lokalisierung: `minigame.<id>.title`, `minigame.<id>.tip`
6. PlayMode-Test in `Game.Tests/MiniGames/`

### Eine neue Achievement hinzufügen

1. `AchievementDefinition.asset` erstellen
2. Trigger-Hook im passenden Service (Achievement-Event-Bus subscriben)
3. Lokalisierung: `achievement.<id>.name`, `achievement.<id>.desc`
4. Test: `AchievementTriggerTests.cs`

---

## 21. Links

- [PLAN.md](PLAN.md) — Strategischer Plan
- [DESIGN.md](DESIGN.md) — Game Design Document
- [ARCHITECTURE.md](ARCHITECTURE.md) — Tech-Details
- [ROADMAP.md](ROADMAP.md) — Wochenplan
- [ASSETS_AI.md](ASSETS_AI.md) — KI-Asset-Pipeline (3D-Meshes + PBR-Texturen via ComfyUI/Hunyuan3D)
- [Avalonia-Version](../HandwerkerImperium/CLAUDE.md) — Referenz für Domain-Logik
- [ArcaneKingdom-Vorlage](../ArcaneKingdom/CLAUDE.md) — Unity-Architektur-Referenz
