# ARCHITECTURE.md — HandwerkerImperium-Unity

> **Tech-Detailspezifikation auf Code-Level.**
> Diese Datei beschreibt: Wie ist das System aufgebaut? Wer ruft wen? Welche Lifetimes? Welche Events? Welche Persistence-Pipeline?

---

## Inhaltsverzeichnis

1. [Layer-Architektur](#1-layer-architektur)
2. [Assembly-Definitions](#2-assembly-definitions)
3. [DI mit VContainer](#3-di-mit-vcontainer)
4. [Scene-Loading-Pipeline](#4-scene-loading-pipeline)
5. [EventBus-Schema](#5-eventbus-schema)
6. [Service-Lifetimes](#6-service-lifetimes)
7. [Game-Loop](#7-game-loop)
8. [Persistence-Layer](#8-persistence-layer)
9. [Save-Migration-Pipeline](#9-save-migration-pipeline)
10. [Network-Layer (Firebase)](#10-network-layer-firebase)
11. [Asset-Loading (Addressables)](#11-asset-loading-addressables)
12. [Input-Handling](#12-input-handling)
13. [Audio-Architektur](#13-audio-architektur)
14. [Rendering-Architektur](#14-rendering-architektur)
15. [Lokalisierungs-Pipeline](#15-lokalisierungs-pipeline)
16. [Anti-Cheat & Security](#16-anti-cheat--security)
17. [Multi-Plattform-Abstraktion](#17-multi-plattform-abstraktion)
18. [Editor-Architektur](#18-editor-architektur)
19. [Build-Pipeline](#19-build-pipeline)

---

## 1. Layer-Architektur

### 1.1 Schichten

```
┌─────────────────────────────────────────────────────────────────┐
│                      BOOTSTRAP LAYER                             │
│  RootLifetimeScope (VContainer, Boot.unity)                      │
│  ↳ GameInstaller (registriert alle Services)                     │
│  ↳ BootEntryPoint (Initial-Sequence)                             │
└────────────────────────────┬────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                          UI LAYER                                │
│  Screens, ViewModels, ViewBinder                                 │
│  Mix: UI Toolkit (UXML/USS) + uGUI (Canvas)                      │
│  → ObservableProperty<T>, RelayCommand, DataBinding              │
└────────────────────────────┬────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                         GAME LAYER                               │
│  Services + Controllers + Coordinators                           │
│  Unity-API erlaubt (MonoBehaviour-Wrapper, Coroutines via UniTask)│
│  Platform-Abstrahierung (IAuth, IPurchase, IAd, IAudio)          │
└────────────────────────────┬────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                        DOMAIN LAYER                              │
│  Pure C# — Calculators, Rules, State-Machines                    │
│  KEINE UnityEngine-Refs → 100% unit-testbar                       │
│  IncomeCalculator, OrderGenerator, PrestigeRules, ...            │
└────────────────────────────┬────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                         CORE LAYER                               │
│  Logger, Result<T>, GameClock, EventBus, Extensions              │
│  Foundation für alle Layer                                       │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Abhängigkeits-Regeln (Pflicht)

- **Core** hat keine Refs nach oben
- **Domain** kennt nur Core (KEIN UnityEngine, KEIN Game, KEIN UI)
- **Game** kennt Domain + Core (darf UnityEngine nutzen)
- **UI** kennt Game + Domain + Core (für ViewModels)
- **Bootstrap** kennt UI + Game + Domain (für DI-Wiring)

**Verbotene Refs (Build bricht):**
- Domain → Game/UI/Bootstrap
- Game → UI/Bootstrap
- UI → Bootstrap
- Core → irgendwas

---

## 2. Assembly-Definitions

### 2.1 Asmdef-Dateien

| Datei | Dependencies | Allow-Unsafe |
|-------|--------------|--------------|
| `HandwerkerImperium.Core.asmdef` | — | false |
| `HandwerkerImperium.Domain.asmdef` | Core | false |
| `HandwerkerImperium.Game.asmdef` | Domain, Core, **UnityEngine** | false |
| `HandwerkerImperium.UI.asmdef` | Game, Domain, Core, UnityEngine, TextMeshPro, Unity.UIElements | false |
| `HandwerkerImperium.Bootstrap.asmdef` | UI, Game, Domain, Core, VContainer | false |
| `HandwerkerImperium.Editor.asmdef` | Domain, Game, Core (Editor-only!) | false |
| `HandwerkerImperium.Domain.Tests.asmdef` | Domain, Core, NUnit, **UNITY_INCLUDE_TESTS define constraint** | false |

### 2.2 Beispiel-asmdef

```json
{
    "name": "HandwerkerImperium.Domain",
    "rootNamespace": "HandwerkerImperium.Domain",
    "references": [
        "HandwerkerImperium.Core"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

**Wichtig:** `"noEngineReferences": true` für Domain-Layer → kein UnityEngine.dll wird verlinkt → 100% pur C#.

---

## 3. DI mit VContainer

### 3.1 RootLifetimeScope

**Datei:** `Assets/_Project/Scripts/Bootstrap/RootLifetimeScope.cs`

```csharp
namespace HandwerkerImperium.Bootstrap;

using VContainer;
using VContainer.Unity;
using HandwerkerImperium.Core;
using HandwerkerImperium.Domain.Workshops;
using HandwerkerImperium.Domain.Orders;
// ... weitere Domain-Namespaces
using HandwerkerImperium.Game.Services;
using HandwerkerImperium.Game.Cloud;
using HandwerkerImperium.Game.Platform;
// ... weitere Game-Namespaces

public sealed class RootLifetimeScope : LifetimeScope
{
    [SerializeField] private BalancingConfig _balancingConfig;
    [SerializeField] private UnityAudioServiceBehaviour _audioService;

    protected override void Configure(IContainerBuilder builder)
    {
        RegisterCore(builder);
        RegisterDomain(builder);
        RegisterGame(builder);
        RegisterPlatform(builder);
        RegisterCoordinators(builder);
        RegisterConfig(builder);
        RegisterEntryPoints(builder);
    }

    private void RegisterCore(IContainerBuilder b)
    {
        b.Register<ILogger, UnityLogger>(Lifetime.Singleton);
        b.Register<IGameClock, RealtimeGameClock>(Lifetime.Singleton);
        b.Register<IEventBus, EventBus>(Lifetime.Singleton);
        b.Register<IRandomProvider, UnityRandomProvider>(Lifetime.Singleton);
    }

    private void RegisterDomain(IContainerBuilder b)
    {
        // Domain-Calculators (pure C#)
        b.Register<IIncomeCalculator, IncomeCalculator>(Lifetime.Singleton);
        b.Register<IOrderGenerator, OrderGenerator>(Lifetime.Singleton);
        b.Register<IPrestigeRules, PrestigeRules>(Lifetime.Singleton);
        b.Register<ICraftingRules, CraftingRules>(Lifetime.Singleton);
        b.Register<IWorkerStatsCalculator, WorkerStatsCalculator>(Lifetime.Singleton);
        b.Register<IUpgradeCostFormula, UpgradeCostFormula>(Lifetime.Singleton);
        b.Register<IMarketPriceCalculator, MarketPriceCalculator>(Lifetime.Singleton);

        // Save-Pipeline
        b.Register<ISaveSerializer<HwiSave>, JsonSaveSerializer<HwiSave>>(Lifetime.Singleton);
        b.Register<ISaveMigrator<HwiSave>, HwiSaveMigrator>(Lifetime.Singleton);
        b.Register<ISaveSanitizer, SaveSanitizer>(Lifetime.Singleton);
    }

    private void RegisterGame(IContainerBuilder b)
    {
        // Core-Services
        b.Register<GameLoopService>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
        b.Register<GameStateService>(Lifetime.Singleton);
        b.Register<ISaveService<HwiSave>, LocalFirstSaveService<HwiSave>>(Lifetime.Singleton);
        b.Register<ICloudSaveService, FirebaseCloudSaveService>(Lifetime.Singleton);

        // Feature-Services
        b.Register<WorkshopService>(Lifetime.Singleton);
        b.Register<WorkerService>(Lifetime.Singleton);
        b.Register<OrderService>(Lifetime.Singleton);
        b.Register<ResearchService>(Lifetime.Singleton);
        b.Register<PrestigeService>(Lifetime.Singleton);
        b.Register<CraftingService>(Lifetime.Singleton);
        b.Register<WarehouseService>(Lifetime.Singleton);
        b.Register<MarketService>(Lifetime.Singleton);
        b.Register<EventService>(Lifetime.Singleton);
        b.Register<AchievementService>(Lifetime.Singleton);
        b.Register<DailyChallengeService>(Lifetime.Singleton);
        b.Register<WeeklyMissionService>(Lifetime.Singleton);
        b.Register<BattlePassService>(Lifetime.Singleton);
        b.Register<LiveEventService>(Lifetime.Singleton);
        b.Register<TutorialService>(Lifetime.Singleton);
        b.Register<NotificationService>(Lifetime.Singleton);
        b.Register<ReferralService>(Lifetime.Singleton);
        b.Register<DailyRewardService>(Lifetime.Singleton);
        b.Register<EquipmentService>(Lifetime.Singleton);
        b.Register<AscensionService>(Lifetime.Singleton);

        // Guild-Facade (Pattern aus Avalonia)
        b.Register<GuildService>(Lifetime.Singleton);
        b.Register<GuildCoopOrderService>(Lifetime.Singleton);
        b.Register<WorkerAuctionService>(Lifetime.Singleton);
        b.Register<GuildBossService>(Lifetime.Singleton);
        b.Register<GuildHallService>(Lifetime.Singleton);
        b.Register<GuildWarSeasonService>(Lifetime.Singleton);
        b.Register<GuildMegaProjectService>(Lifetime.Singleton);
        b.Register<GuildChatService>(Lifetime.Singleton);
        b.Register<GuildAchievementService>(Lifetime.Singleton);
        b.Register<IGuildFacade, GuildFacade>(Lifetime.Singleton);

        // Mini-Game-Registry
        b.Register<IMiniGameRegistry, MiniGameRegistry>(Lifetime.Singleton);

        // Scene-Loader
        b.Register<ISceneLoaderService, AdditiveSceneLoaderService>(Lifetime.Singleton);
    }

    private void RegisterPlatform(IContainerBuilder b)
    {
        // Firebase
        b.Register<IAuthService, FirebaseAuthService>(Lifetime.Singleton);
        b.Register<IAnalyticsService, FirebaseAnalyticsService>(Lifetime.Singleton);
        b.Register<ICrashlyticsService, FirebaseCrashlyticsService>(Lifetime.Singleton);
        b.Register<IRemoteConfigService, FirebaseRemoteConfigService>(Lifetime.Singleton);
        b.Register<IPushNotificationService, FcmPushNotificationService>(Lifetime.Singleton);
        b.Register<ICloudFunctionsService, FirebaseCloudFunctionsService>(Lifetime.Singleton);

        // AdMob + Billing
        b.Register<IRewardedAdService, AdMobRewardedAdService>(Lifetime.Singleton);
        b.Register<IBannerAdService, AdMobBannerAdService>(Lifetime.Singleton);
        b.Register<IPurchaseService, GooglePlayBillingService>(Lifetime.Singleton);
        b.Register<IConsentService, UmpConsentService>(Lifetime.Singleton);

        // Mobile-APIs
        b.Register<IHapticFeedbackService, UnityHapticFeedbackService>(Lifetime.Singleton);
        b.Register<IShareService, NativeShareService>(Lifetime.Singleton);
        b.Register<IPlayReviewService, GooglePlayReviewService>(Lifetime.Singleton);

        // Audio (MonoBehaviour-Service)
        b.RegisterComponent(_audioService).AsImplementedInterfaces();

        // Security
        b.Register<IHmacSigner, HmacSha256Signer>(Lifetime.Singleton);
        b.Register<IDeviceIdentifierService, AndroidDeviceIdService>(Lifetime.Singleton);
    }

    private void RegisterCoordinators(IContainerBuilder b)
    {
        b.Register<GameStartupCoordinator>(Lifetime.Singleton);
        b.Register<ProgressionFeedbackCoordinator>(Lifetime.Singleton);
        b.Register<GameTickCoordinator>(Lifetime.Singleton);
        b.Register<CinematicCoordinator>(Lifetime.Singleton);
        b.Register<ReputationTierEffectsCoordinator>(Lifetime.Singleton);
        b.Register<TutorialCoordinator>(Lifetime.Singleton);
        b.Register<NotificationScheduleCoordinator>(Lifetime.Singleton);
    }

    private void RegisterConfig(IContainerBuilder b)
    {
        b.RegisterInstance(_balancingConfig).AsImplementedInterfaces();
    }

    private void RegisterEntryPoints(IContainerBuilder b)
    {
        b.RegisterEntryPoint<BootEntryPoint>();
    }
}
```

### 3.2 Sub-Scopes (Scene-spezifisch)

#### HubLifetimeScope (Hub.unity)

```csharp
public sealed class HubLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Hub-spezifische ViewModels
        builder.Register<DashboardViewModel>(Lifetime.Singleton);
        builder.Register<ImperiumViewModel>(Lifetime.Singleton);
        builder.Register<MissionsViewModel>(Lifetime.Singleton);
        builder.Register<GuildViewModel>(Lifetime.Singleton);
        builder.Register<ShopViewModel>(Lifetime.Singleton);

        // Hub-Controllers
        builder.Register<HubNavigationController>(Lifetime.Singleton);
    }
}
```

#### MiniGameLifetimeScope (MiniGame.unity)

```csharp
public sealed class MiniGameLifetimeScope : LifetimeScope
{
    [SerializeField] private MiniGameType _type;

    protected override void Configure(IContainerBuilder builder)
    {
        // Per-MiniGame transient
        builder.Register<MiniGameSessionViewModel>(Lifetime.Singleton);

        // Spiel-spezifischer Controller
        switch (_type)
        {
            case MiniGameType.Sawing:
                builder.Register<SawingMiniGameController>(Lifetime.Singleton);
                break;
            case MiniGameType.Forge:
                builder.Register<ForgeMiniGameController>(Lifetime.Singleton);
                break;
            // ...
        }
    }
}
```

### 3.3 BootEntryPoint (IInitializable + IAsyncStartable)

```csharp
public sealed class BootEntryPoint(
    GameStartupCoordinator startupCoordinator,
    ISaveService<HwiSave> saveService,
    IAuthService authService,
    ISceneLoaderService sceneLoader,
    ILogger logger) : IAsyncStartable
{
    public async UniTask StartAsync(CancellationToken cancellation)
    {
        logger.Log("Boot: Start");

        // 1. Auth
        await authService.SignInAnonymouslyAsync(cancellation);

        // 2. Save laden
        var save = await saveService.LoadAsync(cancellation);

        // 3. Startup-Sequenz (FTUE-Check, Dialoge, Daily-Reward, Welcome-Back)
        await startupCoordinator.RunAsync(save, cancellation);

        // 4. Hub-Scene additive laden
        await sceneLoader.LoadAsync("Hub", LoadSceneMode.Additive, cancellation);

        logger.Log("Boot: Complete");
    }
}
```

---

## 4. Scene-Loading-Pipeline

### 4.1 Scene-Hierarchie

```
Boot.unity (persistent, DontDestroyOnLoad)
  └── RootLifetimeScope (VContainer)
  └── PersistentCanvas (Cross-Fade-Transitions)
  └── AudioListener
  └── PersistentEventSystem

Bei Game-Start additive geladen:
Hub.unity (additive)
  └── HubLifetimeScope
  └── HubCamera (Cinemachine Virtual Camera)
  └── 3D-Hub-Szene (10 Werkstätten)
  └── UI-Canvas (UI Toolkit Root)

Bei Workshop-Tap zusätzlich:
Workshop.unity (additive, on top of Hub)
  └── WorkshopLifetimeScope
  └── WorkshopOrbitCamera
  └── 3D-Workshop-Detail

Bei MiniGame-Start:
MiniGame.unity (additive, ersetzt Workshop visuell)
  └── MiniGameLifetimeScope
  └── MiniGameCamera
  └── MiniGame-Prefab (gemäß Type)
```

### 4.2 SceneLoaderService

```csharp
public interface ISceneLoaderService
{
    UniTask LoadAsync(string sceneName, LoadSceneMode mode, CancellationToken ct);
    UniTask UnloadAsync(string sceneName, CancellationToken ct);
    UniTask SwapAsync(string outScene, string inScene, CancellationToken ct);
}

public sealed class AdditiveSceneLoaderService(
    ITransitionService transitions,
    ILogger logger) : ISceneLoaderService
{
    public async UniTask LoadAsync(string sceneName, LoadSceneMode mode, CancellationToken ct)
    {
        await transitions.FadeOutAsync(0.3f, ct);

        var op = SceneManager.LoadSceneAsync(sceneName, mode);
        await op.ToUniTask(cancellationToken: ct);

        var scene = SceneManager.GetSceneByName(sceneName);
        SceneManager.SetActiveScene(scene);

        // Wartet auf LifetimeScope.Ready (initialization complete)
        var scope = GetLifetimeScopeInScene(scene);
        await scope.WaitForReadyAsync(ct);

        await transitions.FadeInAsync(0.3f, ct);
    }
}
```

### 4.3 Transitions (Cross-Fade)

PersistentCanvas in Boot.unity hat:
- Full-Screen Image (schwarz, Alpha=0)
- DOTween-Fade: `image.DOFade(1f, duration)` für FadeOut, `DOFade(0f, duration)` für FadeIn

---

## 5. EventBus-Schema

### 5.1 Interface

```csharp
namespace HandwerkerImperium.Core;

public interface IGameEvent { }

public interface IEventBus
{
    void Publish<T>(T evt) where T : IGameEvent;
    IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent;
}

public sealed class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly object _lock = new();

    public void Publish<T>(T evt) where T : IGameEvent
    {
        lock (_lock)
        {
            if (_subscribers.TryGetValue(typeof(T), out var handlers))
            {
                foreach (var handler in handlers.OfType<Action<T>>())
                {
                    try { handler(evt); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            }
        }
    }

    public IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var handlers))
            {
                handlers = new List<Delegate>();
                _subscribers[typeof(T)] = handlers;
            }
            handlers.Add(handler);
        }
        return new DisposableAction(() => Unsubscribe<T>(handler));
    }

    private void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
    {
        lock (_lock)
        {
            if (_subscribers.TryGetValue(typeof(T), out var handlers))
                handlers.Remove(handler);
        }
    }
}
```

### 5.2 Event-Inventar (record-based)

**State-Events:**
```csharp
public record StateLoadedEvent(HwiSave Save) : IGameEvent;
public record StateSavedEvent : IGameEvent;
public record StateCorruptedEvent(string Reason) : IGameEvent;
```

**Currency-Events:**
```csharp
public record MoneyChangedEvent(decimal NewAmount, decimal Delta) : IGameEvent;
public record GoldenScrewsChangedEvent(int NewAmount, int Delta) : IGameEvent;
public record XpGainedEvent(int Delta, int NewTotal, int Level) : IGameEvent;
public record LevelUpEvent(int OldLevel, int NewLevel) : IGameEvent;
public record PrestigePointsChangedEvent(long NewAmount, long Delta) : IGameEvent;
```

**Workshop-Events:**
```csharp
public record WorkshopBoughtEvent(string WorkshopId) : IGameEvent;
public record WorkshopUpgradedEvent(string WorkshopId, int NewLevel) : IGameEvent;
public record WorkshopRebirthedEvent(string WorkshopId, int Stars) : IGameEvent;
public record WorkshopSpecializedEvent(string WorkshopId, WorkshopSpecialization Spec) : IGameEvent;
```

**Worker-Events:**
```csharp
public record WorkerHiredEvent(Worker Worker) : IGameEvent;
public record WorkerFiredEvent(string WorkerId) : IGameEvent;
public record WorkerPromotedEvent(string WorkerId, WorkerTier NewTier) : IGameEvent;
public record WorkerMoodChangedEvent(string WorkerId, int NewMood) : IGameEvent;
public record WorkerTrainedEvent(string WorkerId, TrainingType Type) : IGameEvent;
```

**Order-Events:**
```csharp
public record OrderGeneratedEvent(Order Order) : IGameEvent;
public record OrderAcceptedEvent(string OrderId, OrderStrategy Strategy) : IGameEvent;
public record OrderCompletedEvent(string OrderId, OrderResult Result, decimal Reward) : IGameEvent;
public record OrderFailedEvent(string OrderId, OrderFailReason Reason) : IGameEvent;
public record LiveOrderSpawnedEvent(Order Order) : IGameEvent;
public record VipCustomerArrivedEvent(string OrderId) : IGameEvent;
```

**Mini-Game-Events:**
```csharp
public record MiniGameStartedEvent(MiniGameType Type, string OrderId) : IGameEvent;
public record MiniGameCompletedEvent(MiniGameType Type, MiniGameRating Rating, float Score) : IGameEvent;
public record MiniGamePerfectEvent(MiniGameType Type) : IGameEvent;
```

**Progression-Events:**
```csharp
public record AchievementUnlockedEvent(AchievementId Id, AchievementTier Tier) : IGameEvent;
public record DailyChallengeCompletedEvent(string ChallengeId) : IGameEvent;
public record WeeklyMissionCompletedEvent(string MissionId) : IGameEvent;
public record BattlePassTierReachedEvent(int Tier, bool IsPremium) : IGameEvent;
public record StoryChapterReadEvent(int ChapterId) : IGameEvent;
```

**Prestige-Events:**
```csharp
public record PrestigeTriggeredEvent(PrestigeTier OldTier, PrestigeTier NewTier, int Count) : IGameEvent;
public record PrestigeCompletedEvent(HwiSave NewSave) : IGameEvent;  // wichtig für Cache-Reset!
public record AscensionTriggeredEvent(int AscensionLevel) : IGameEvent;
public record HeirloomChosenEvent(string ItemId) : IGameEvent;
```

**Guild-Events:**
```csharp
public record GuildJoinedEvent(string GuildId) : IGameEvent;
public record GuildLeftEvent(string GuildId) : IGameEvent;
public record CoopOrderContributedEvent(string OrderId, long Score) : IGameEvent;
public record BossHitEvent(string BossId, int Damage) : IGameEvent;
public record BossDefeatedEvent(string BossId) : IGameEvent;
public record AuctionBidPlacedEvent(string AuctionId, decimal Amount) : IGameEvent;
public record AuctionWonEvent(string AuctionId, Worker Worker) : IGameEvent;
public record MegaProjectContributedEvent(string ProjectId, MaterialType Type, int Amount) : IGameEvent;
public record ChatMessageReceivedEvent(string GuildId, ChatMessage Message) : IGameEvent;
```

**UI-Events:**
```csharp
public record FloatingTextRequestedEvent(string Text, FloatingTextStyle Style, Vector3 WorldPosition) : IGameEvent;
public record CelebrationRequestedEvent(CelebrationType Type) : IGameEvent;
public record ScreenShakeRequestedEvent(float Intensity, float Duration) : IGameEvent;
public record CinematicRequestedEvent(string TimelineAssetName) : IGameEvent;
public record DialogRequestedEvent(DialogType Type, DialogData Data) : IGameEvent;
public record NavigationRequestedEvent(string Route) : IGameEvent;
```

**Platform-Events:**
```csharp
public record AppPausedEvent : IGameEvent;
public record AppResumedEvent(TimeSpan AwayDuration) : IGameEvent;
public record ConnectionStateChangedEvent(bool IsOnline) : IGameEvent;
public record IapPurchasedEvent(string ProductId, string Receipt) : IGameEvent;
public record AdRewardedEvent(string Placement, int Reward) : IGameEvent;
public record NotificationTappedEvent(string NotificationId) : IGameEvent;
```

---

## 6. Service-Lifetimes

### 6.1 Übersicht

| Service-Kategorie | Lifetime | Anzahl |
|-------------------|----------|--------|
| Core (Logger, Clock, EventBus) | Singleton (Root) | 4 |
| Domain-Calculators (pure C#) | Singleton (Root) | ~12 |
| Game-Services (mit State) | Singleton (Root) | ~25 |
| Platform-Services | Singleton (Root) | ~15 |
| Coordinators | Singleton (Root) | ~7 |
| ViewModels (langlebig) | Singleton (Hub/Workshop-Scope) | ~10 |
| ViewModels (Modal/Dialog) | Transient | je nach Bedarf |
| Mini-Game-Controllers | Singleton (MiniGame-Scope) | 1 pro Scope |

### 6.2 Spezialfälle

**MonoBehaviour-Services:**
- AudioService (braucht AudioSource-Komponenten)
- ParticleService (braucht ParticleSystem-Pool)
- HapticFeedbackService (kann POCO sein)

→ Diese werden via `RegisterComponent(...).AsImplementedInterfaces()` registriert.

**Lazy-Resolution (für selten genutzte Services):**
```csharp
b.Register<IExpensiveService, ExpensiveService>(Lifetime.Singleton).AsSelf();
b.RegisterFactory<IExpensiveService>(container => () => container.Resolve<IExpensiveService>());
```

---

## 7. Game-Loop

### 7.1 GameLoopService (MonoBehaviour + ITickable)

```csharp
public sealed class GameLoopService(
    IGameClock gameClock,
    IEventBus eventBus,
    WorkshopService workshopService,
    WorkerService workerService,
    OrderService orderService,
    AutomationService automationService,
    GuildTickService guildTickService,
    ILogger logger) : IStartable, ITickable, IDisposable
{
    private float _secondsAccumulator;
    private float _threeSecondAccumulator;
    private float _fiveSecondAccumulator;
    private float _twentyFiveSecondAccumulator;
    private float _sixtySecondAccumulator;
    private float _threeHundredSecondAccumulator;
    private float _threeSixtySecondAccumulator;

    private bool _isPaused;

    public void Start()
    {
        eventBus.Subscribe<AppPausedEvent>(_ => _isPaused = true);
        eventBus.Subscribe<AppResumedEvent>(_ => _isPaused = false);
    }

    public void Tick()
    {
        if (_isPaused) return;

        var dt = Time.deltaTime;
        gameClock.Advance(dt);

        // 1s
        _secondsAccumulator += dt;
        if (_secondsAccumulator >= 1f)
        {
            _secondsAccumulator -= 1f;
            workshopService.TickIncome();
            workerService.TickStates();
        }

        // 3s
        _threeSecondAccumulator += dt;
        if (_threeSecondAccumulator >= 3f)
        {
            _threeSecondAccumulator -= 3f;
            orderService.TickLiveOrderExpiry();
        }

        // 5s
        _fiveSecondAccumulator += dt;
        if (_fiveSecondAccumulator >= 5f)
        {
            _fiveSecondAccumulator -= 5f;
            automationService.TickAutoCollect();
            automationService.TickAutoAccept();
        }

        // 25s
        _twentyFiveSecondAccumulator += dt;
        if (_twentyFiveSecondAccumulator >= 25f)
        {
            _twentyFiveSecondAccumulator -= 25f;
            orderService.TickLiveOrderSpawn();
        }

        // 60s
        _sixtySecondAccumulator += dt;
        if (_sixtySecondAccumulator >= 60f)
        {
            _sixtySecondAccumulator -= 60f;
            orderService.TickQuickJobRotation();
            orderService.TickOrderExpiry();
            workerService.TickAutoAssign();
            guildTickService.TickBoss();    // Offset 20s wird intern berücksichtigt
            guildTickService.TickHall();    // Offset 40s
        }

        // 300s
        _threeHundredSecondAccumulator += dt;
        if (_threeHundredSecondAccumulator >= 300f)
        {
            _threeHundredSecondAccumulator -= 300f;
            // Event-Check, Guild-Achievements, War-Season
        }

        // 360s
        _threeSixtySecondAccumulator += dt;
        if (_threeSixtySecondAccumulator >= 360f)
        {
            _threeSixtySecondAccumulator -= 360f;
            automationService.TickAutoCraftHigherTiers();
        }
    }

    public void Dispose()
    {
        // Event-Cleanup
    }
}
```

### 7.2 Pause-Verhalten

- App-Pause (Background): GameLoop pausiert, Save wird sofort persistiert
- App-Resume (Foreground): `OfflineProgressService` berechnet Offline-Income, GameLoop läuft weiter

---

## 8. Persistence-Layer

### 8.1 Save-Service (Local-First)

```csharp
public interface ISaveService<T>
{
    UniTask<T> LoadAsync(CancellationToken ct);
    UniTask SaveAsync(T data, CancellationToken ct);
    UniTask DeleteAsync(CancellationToken ct);
    string Path { get; }
}

public sealed class LocalFirstSaveService<T>(
    ISaveSerializer<T> serializer,
    ISaveMigrator<T> migrator,
    ISaveSanitizer sanitizer,
    ICloudSaveService cloudSaveService,
    ILogger logger) : ISaveService<T>
{
    public string Path => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public async UniTask<T> LoadAsync(CancellationToken ct)
    {
        await _ioLock.WaitAsync(ct);
        try
        {
            // 1. Cloud zuerst versuchen (mit Timeout)
            var cloudSave = await TryLoadCloudAsync(ct);
            if (cloudSave is not null) return cloudSave;

            // 2. Local fallback
            if (!File.Exists(Path)) return default!;

            var json = await File.ReadAllTextAsync(Path, ct);
            var save = serializer.Deserialize(json);

            // 3. Migrate
            var schemaVersion = serializer.ReadSchemaVersion(json);
            save = migrator.Migrate(save, schemaVersion);

            // 4. Sanitize
            save = sanitizer.Sanitize(save);

            return save;
        }
        finally { _ioLock.Release(); }
    }

    public async UniTask SaveAsync(T data, CancellationToken ct)
    {
        await _ioLock.WaitAsync(ct);
        try
        {
            // Serialize off-thread (UniTask.RunOnThreadPool)
            var json = await UniTask.RunOnThreadPool(() => serializer.Serialize(data), cancellationToken: ct);
            await File.WriteAllTextAsync(Path, json, ct);

            // Cloud-Save Fire-and-Forget
            cloudSaveService.UploadAsync(json, CancellationToken.None).Forget();
        }
        finally { _ioLock.Release(); }
    }
}
```

### 8.2 Save-Trigger

| Trigger | Mechanismus |
|---------|-------------|
| Sofort | Service ruft direkt `_saveService.SaveAsync()` |
| AutoSave 30s | `GameLoopService` tickt einen Counter, ruft Save |
| App-Pause | `OnApplicationPause(true)` in MonoBehaviour-Bridge |
| Cloud-Sync | Nach jeder Sofort-Save |

### 8.3 Save-Layout (HwiSave)

Siehe [CLAUDE.md § 10 Save-Schema](CLAUDE.md). Slices: 22 Stück.

---

## 9. Save-Migration-Pipeline

### 9.1 Migrator

```csharp
public interface ISaveMigrator<T>
{
    int CurrentSchemaVersion { get; }
    T Migrate(T save, int fromVersion);
}

public sealed class HwiSaveMigrator(ILogger logger) : ISaveMigrator<HwiSave>
{
    public int CurrentSchemaVersion => 8;

    public HwiSave Migrate(HwiSave save, int fromVersion)
    {
        if (fromVersion >= CurrentSchemaVersion) return save;

        logger.Log($"Migrating save from v{fromVersion} to v{CurrentSchemaVersion}");

        if (fromVersion < 2) save = MigrateV1ToV2(save);
        if (fromVersion < 3) save = MigrateV2ToV3(save);
        if (fromVersion < 4) save = MigrateV3ToV4(save);
        if (fromVersion < 5) save = MigrateV4ToV5(save);
        if (fromVersion < 6) save = MigrateV5ToV6(save);
        if (fromVersion < 7) save = MigrateV6ToV7(save);  // V7 = Warehouse + Mega-Projects
        if (fromVersion < 8) save = MigrateV7ToV8(save);  // V8 = Unity-Specific

        save.SchemaVersion = CurrentSchemaVersion;
        return save;
    }

    private HwiSave MigrateV7ToV8(HwiSave save)
    {
        // Unity-Specific Slice initialisieren
        save.UnitySpecific ??= new UnitySpecificSlice
        {
            PostFxQuality = PostFxQuality.High,
            VibrationEnabled = true,
            AssetCatalogVersion = 1,
            AudioMixerLevels = new AudioMixerLevelsData(),
        };

        // Cosmetic-Slice initialisieren (war optional in V7)
        save.Cosmetics ??= new CosmeticSlice();

        return save;
    }
}
```

### 9.2 Migrations-Tests

Jede Migration MUSS Tests haben:
```csharp
public class HwiSaveMigratorTests
{
    [Test]
    public void Migrate_FromV7_AddsUnitySpecificSlice()
    {
        var save = TestData.V7Save();
        var migrator = new HwiSaveMigrator(NullLogger.Instance);

        var result = migrator.Migrate(save, 7);

        Assert.That(result.UnitySpecific, Is.Not.Null);
        Assert.That(result.SchemaVersion, Is.EqualTo(8));
    }
}
```

---

## 10. Network-Layer (Firebase)

### 10.1 Firebase-Pfade (1:1 wie Avalonia)

**Schema-Datei:** `Server/DatabaseRules/database.rules.json`

```
/auth_to_player/{uid} → PlayerId (.write nur auth.uid == uid)
/players/{playerId}/
  ├── profile { name, level, lastSeen }
  ├── progress { ... }
  ├── achievements { ... }
  ├── battlePass { ... }
  ├── guilds (membership-refs)
  └── invites { ... }
/available_players/{playerId} { name, level, lastSeen }   (Suchindex, .indexOn: ["lastSeen"])
/guild_invite_codes/{code} { guildId, expiresAt }
/guilds/{guildId}/
  ├── meta { name, tag, ownerId, createdAt }
  ├── memberList/{playerId}/{ joinedAt, role, contribution }
  ├── coopOrders/{orderId}/{ ... }
  ├── megaProjects/active/{ ... }
  ├── research/{ ... }
  ├── boss/{bossType}/{ hp, attackers, ... }
  ├── hall/{buildingId}/{ level, timer }
  ├── achievements/{ ... }
  ├── warSeason/{ ... }
  └── chat/{messageId}/{ ... }
```

### 10.2 Firebase-Service-Interface

```csharp
public interface IFirebaseDatabase
{
    UniTask<T?> GetAsync<T>(string path, CancellationToken ct);
    UniTask SetAsync<T>(string path, T value, CancellationToken ct);
    UniTask UpdateAsync(string path, Dictionary<string, object> updates, CancellationToken ct);  // PATCH
    UniTask DeleteAsync(string path, CancellationToken ct);
    UniTask<TransactionResult<T>> RunTransactionAsync<T>(string path, Func<T?, T> mutator, CancellationToken ct);
    IDisposable Subscribe<T>(string path, Action<T?> callback);  // Realtime-Updates
    UniTask<bool> IsOnlineAsync(CancellationToken ct);
}
```

### 10.3 Atomar updaten (PATCH)

```csharp
// Co-op-Order Score
var updates = new Dictionary<string, object>
{
    [$"guilds/{guildId}/coopOrders/{orderId}/scores/{playerId}"] = newScore,
    [$"guilds/{guildId}/coopOrders/{orderId}/lastUpdate"] = ServerValue.Timestamp,
};
await _firebase.UpdateAsync("", updates, ct);  // Root-PATCH
```

### 10.4 Cloud-Functions-Calls

```csharp
public interface ICloudFunctionsService
{
    UniTask<T> CallAsync<T>(string functionName, object payload, CancellationToken ct);
}

// Beispiel:
var result = await _cloudFunctions.CallAsync<ValidateBattleResultResponse>(
    "validateBattleResult",
    new { battleSeed, deck, claimedReward },
    ct);
```

### 10.5 Firebase-Pfad-Sicherheit

- Jeder neue Pfad MUSS in `database.rules.json` eingetragen sein
- Bei `orderBy` MUSS `.indexOn` gesetzt sein
- Schreibrechte streng: `auth.uid == playerId` oder via Cloud-Function

---

## 11. Asset-Loading (Addressables)

### 11.1 Asset-Groups

| Group | Inhalt | Strategy |
|-------|--------|----------|
| `Bootstrap` | Splash, Default-Font, Logos | Sync, im Build |
| `UI.Common` | Buttons, Cards, Icons | Pre-Load bei Boot |
| `Workshops.{Type}` | 3D-Model, Material, Audio | Lazy bei Workshop-Tap |
| `Workers.{Tier}` | Avatare, Animationen | LRU-Cache, max 20 |
| `MiniGames.{Type}` | Prefab + Audio | Lazy bei Game-Start |
| `Audio.BGM` | Music-Loops | Streaming |
| `Audio.SFX` | SFX-Pool | Pre-Load pro Scene |
| `FX` | Particle-Systems | Pre-Load mit Workshop |
| `Localization.{Lang}` | String-Tables | Sync bei Sprach-Wechsel |

### 11.2 Memory-Management

```csharp
public sealed class AddressableLoader
{
    private readonly Dictionary<string, AsyncOperationHandle> _handles = new();

    public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct) where T : Object
    {
        if (_handles.TryGetValue(key, out var existing))
            return (T)existing.Result;

        var handle = Addressables.LoadAssetAsync<T>(key);
        _handles[key] = handle;
        return await handle.ToUniTask(cancellationToken: ct);
    }

    public void Release(string key)
    {
        if (_handles.TryGetValue(key, out var handle))
        {
            Addressables.Release(handle);
            _handles.Remove(key);
        }
    }

    public void ReleaseAll()
    {
        foreach (var (_, handle) in _handles)
            Addressables.Release(handle);
        _handles.Clear();
    }
}
```

### 11.3 Remote-Catalog (Phase 2)

- Firebase Storage hostet AB-Catalog
- Update-Manager im Hub: "Neue Inhalte verfügbar" → Download
- Versionierung via `AssetCatalogVersion` im Save

---

## 12. Input-Handling

### 12.1 New Input System (PlayerInput-basiert)

```csharp
public sealed class HwiInputActions : IInputActionCollection2
{
    public InputAction Tap { get; }
    public InputAction Drag { get; }
    public InputAction Pinch { get; }
    public InputAction Swipe { get; }
    public InputAction Back { get; }
    public InputAction Cheats { get; }  // Dev-Only
}
```

### 12.2 Touch-Gesten

- **Tap:** Klick auf UI
- **Drag:** Camera-Pan, Card-Drag
- **Pinch:** Camera-Zoom
- **Swipe:** Tab-Wechsel
- **Long-Press:** Context-Menu

### 12.3 Back-Button

```csharp
private void OnBack()
{
    if (DialogService.AnyOpen) { DialogService.CloseTopmost(); return; }
    if (NavigationStack.Count > 0) { NavigationStack.Pop(); return; }

    // Double-Back-to-Exit (analog Avalonia BackPressHelper)
    if (_backPressTimer.Elapsed < 2.0f)
    {
        Application.Quit();
    }
    else
    {
        ToastService.Show("Nochmal drücken zum Beenden");
        _backPressTimer.Restart();
    }
}
```

---

## 13. Audio-Architektur

### 13.1 AudioMixer-Hierarchie

```
Master (Output: Audio Listener)
├── Music (BGM)
├── SFX
│   ├── UI-SFX
│   ├── Game-SFX
│   └── MiniGame-SFX
├── Voice (Phase 2)
└── Ambience
```

### 13.2 AudioService

```csharp
public interface IAudioService
{
    void PlaySfx(SfxId id, float volumeScale = 1f);
    void PlaySfx3D(SfxId id, Vector3 worldPos, float volumeScale = 1f);
    UniTask PlayMusicAsync(MusicTrack track, float fadeDuration = 0.8f, CancellationToken ct = default);
    void StopMusic(float fadeDuration = 0.8f);
    void DuckMusic(float dbAttenuation, float duration);
    void SetMixerLevel(MixerGroup group, float dbLevel);
}
```

### 13.3 Ducking-Snapshots

```csharp
// In AudioMixer:
- Snapshot "Normal" (Music: 0dB)
- Snapshot "DuckedDialog" (Music: -12dB)
- Snapshot "DuckedAchievement" (Music: -6dB)

// Code:
_audioMixer.FindSnapshot("DuckedDialog").TransitionTo(0.3f);
```

---

## 14. Rendering-Architektur

### 14.1 URP-Konfiguration

- **Mobile-Renderer:** Forward Renderer
- **MSAA:** 2x (Mid-Tier), Off (Low-End)
- **HDR:** Off (Mobile)
- **Shadows:** Soft, max 2 Lichter

### 14.2 Post-Processing-Profile

Pro Quality-Level eines:
- `Profile_Low.asset` (kein Bloom, kein Color Grading)
- `Profile_Medium.asset` (Bloom Light, einfaches Color Grading)
- `Profile_High.asset` (Bloom + Vignette + Color Grading + Chromatic Aberration)
- `Profile_Cinematic.asset` (alles + Depth of Field + Film Grain) — nur für Prestige-Cinematic

### 14.3 Shader-Architektur (Shader Graph)

Pro Effekt eigene `.shadergraph`-Datei unter `Assets/_Project/Art/Shaders/`:
- `WorkshopGlow.shadergraph`
- `HolographicCard.shadergraph`
- `DissolveTransition.shadergraph`
- `MoneyShimmer.shadergraph`
- `WaterFlow.shadergraph` (Pipe Puzzle)
- `Fire.shadergraph` (Forge)
- `Electricity.shadergraph` (Wiring)

---

## 15. Lokalisierungs-Pipeline

### 15.1 Unity Localization Package

- String-Tables pro Locale (DE, EN, ES, FR, IT, PT)
- Smart-Format für Placeholder (`{0}`, `{0:C}`, etc.)
- TextMesh Pro Font-Assets pro Sprach-Gruppe

### 15.2 Import aus Avalonia-RESX

```csharp
// Editor-Tool: HandwerkerImperium → Localization → Import from RESX
[MenuItem("HandwerkerImperium/Localization/Import from RESX")]
public static void ImportFromRESX()
{
    var rextPaths = new[]
    {
        "Avalonia/Resources/AppStrings.de.resx",
        "Avalonia/Resources/AppStrings.en.resx",
        // ...
    };

    foreach (var resxPath in rextPaths)
    {
        var entries = ParseRESX(resxPath);
        var locale = ExtractLocale(resxPath);
        var stringTable = LoadStringTable(locale);

        foreach (var (key, value) in entries)
        {
            stringTable.AddEntry(key, value);
        }
    }
}
```

---

## 16. Anti-Cheat & Security

### 16.1 HMAC-Signierung

```csharp
public interface IHmacSigner
{
    string Sign(string payload, string key);
    bool Verify(string payload, string signature, string key);
}

public sealed class HmacSha256Signer : IHmacSigner
{
    public string Sign(string payload, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    public bool Verify(string payload, string signature, string key) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Sign(payload, key)),
            Encoding.UTF8.GetBytes(signature));
}
```

### 16.2 Save-Sanitizer

```csharp
public interface ISaveSanitizer
{
    HwiSave Sanitize(HwiSave save);
}

public sealed class SaveSanitizer(
    IProductCatalog catalog,
    BalancingConfig config) : ISaveSanitizer
{
    public HwiSave Sanitize(HwiSave save)
    {
        // Heirloom-Items gegen Catalog validieren
        save.Prestige.Heirlooms.RemoveAll(h => !catalog.Has(h.ItemId));

        // Money-Cap
        if (save.GameState.Money > config.MaxMoney)
            save.GameState.Money = config.MaxMoney;

        // Worker-Level-Cap
        foreach (var w in save.Workers)
            if (w.Level > config.MaxWorkerLevel)
                w.Level = config.MaxWorkerLevel;

        // Orphan-Reservierungen entfernen
        var activeOrderIds = save.Orders.Active.Select(o => o.Id).ToHashSet();
        save.Crafting.Reservations.RemoveAll(r => !activeOrderIds.Contains(r.OrderId));

        return save;
    }
}
```

### 16.3 Cloud-Functions (Server-Side)

Spec-Datei: `Server/SERVEROPS.md`

8 Functions analog Avalonia + ArcaneKingdom (siehe [PLAN.md § 8.4](PLAN.md)).

---

## 17. Multi-Plattform-Abstraktion

### 17.1 Platform-Interfaces

| Interface | Android-Impl | iOS-Impl (Phase 2) |
|-----------|-------------|---------------------|
| `IAuthService` | FirebaseAuthAndroid | FirebaseAuthIos |
| `IPurchaseService` | GooglePlayBilling | StoreKit |
| `IRewardedAdService` | AdMobAndroid | AdMobIos |
| `IPushNotificationService` | FcmAndroid | FcmIos |
| `INotificationService` | AndroidLocalNotifications | iOSLocalNotifications |
| `IHapticFeedbackService` | AndroidVibrator | iOSHaptic |
| `IShareService` | AndroidShare | iOSShare |

Unity macht Plattform-Auswahl via `#if UNITY_ANDROID` / `#if UNITY_IOS`. DI-Registrierung erfolgt im Bootstrap mit Platform-Check:

```csharp
#if UNITY_ANDROID
    builder.Register<IPurchaseService, GooglePlayBillingService>(Lifetime.Singleton);
#elif UNITY_IOS
    builder.Register<IPurchaseService, StoreKitPurchaseService>(Lifetime.Singleton);
#endif
```

---

## 18. Editor-Architektur

### 18.1 Editor-Tools-Ordnerstruktur

```
Assets/_Project/Scripts/Editor/
├── Setup/
│   └── FirstTimeSetupWizard.cs
├── Data/
│   ├── DataImporter.cs
│   └── RESXLocalizationImporter.cs
├── Inspectors/
│   ├── WorkshopDefinitionInspector.cs
│   ├── WorkerTierInspector.cs
│   ├── BalancingConfigInspector.cs
│   └── ScriptableObjectPreviewWindow.cs
├── Windows/
│   ├── BalancingDashboard.cs
│   ├── SaveGameEditor.cs
│   ├── CheatsWindow.cs
│   ├── LocalizationCheckTool.cs
│   └── BuildScripts.cs
└── Validation/
    ├── ProjectValidator.cs
    └── PreBuildHooks.cs
```

### 18.2 PreBuild-Hooks

```csharp
public class PreBuildHooks : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        // 1. Lokalisierungs-Check
        ValidateLocalization();

        // 2. ScriptableObject-Validierung
        ValidateScriptableObjects();

        // 3. Cheats nur in Dev-Build
        if (!Debug.isDebugBuild && CheatsAreEnabled())
            throw new BuildFailedException("Cheats sind in Release-Build aktiv!");
    }
}
```

---

## 19. Build-Pipeline

### 19.1 Build-Profile

```csharp
public static class BuildScripts
{
    [MenuItem("Build/Android Release")]
    public static void BuildAndroidRelease()
    {
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = "../../../../Releases/meineapps.keystore";
        PlayerSettings.Android.keystorePass = "MeineApps2025";
        PlayerSettings.Android.keyaliasName = "meineapps";
        PlayerSettings.Android.keyaliasPass = "MeineApps2025";
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.useAPKExpansionFiles = false;
        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Master);

        var options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = "../../../../Releases/HandwerkerImperium-Unity.aab",
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };

        BuildPipeline.BuildPlayer(options);
    }
}
```

### 19.2 CI/CD (GitHub Actions)

```yaml
# .github/workflows/unity-build.yml
name: Unity Android Build

on:
  push:
    branches: [unity-main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/cache@v4
        with:
          path: Unity/Library
          key: Library-${{ hashFiles('Unity/Assets/**', 'Unity/Packages/**', 'Unity/ProjectSettings/**') }}
      - uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
        with:
          projectPath: src/Apps/HandwerkerImperium.Unity/Unity
          buildName: HandwerkerImperium-Unity
          targetPlatform: Android
          androidAppBundle: true
          androidKeystoreBase64: ${{ secrets.ANDROID_KEYSTORE }}
          androidKeystorePass: ${{ secrets.ANDROID_KEYSTORE_PASS }}
          androidKeyaliasName: meineapps
          androidKeyaliasPass: ${{ secrets.ANDROID_KEY_PASS }}
      - uses: actions/upload-artifact@v4
        with:
          name: HandwerkerImperium-Unity.aab
          path: build/Android/Android.aab
```

### 19.3 Versionierung

```csharp
[InitializeOnLoad]
public static class VersionBumper
{
    static VersionBumper()
    {
        EditorUserBuildSettings.activeBuildTargetChanged += () =>
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                PlayerSettings.Android.bundleVersionCode++;
                AssetDatabase.SaveAssets();
            }
        };
    }
}
```

---

## 20. Test-Architektur

### 20.1 Test-Layer

| Layer | Framework | Speed | Coverage |
|-------|-----------|-------|----------|
| **Domain-Tests** | NUnit (EditMode) | <1s pro Test | ≥80% |
| **Game-Tests** | NUnit + UnityTest (PlayMode) | <5s pro Test | ≥50% |
| **UI-Tests** | UnityTest + Input-Simulation | <30s pro Test | Optional |
| **E2E-Tests** | Manual + Cheats | Stunden | Manuell |

### 20.2 Test-Setup

```csharp
public abstract class DomainTestBase
{
    protected IContainerBuilder Container { get; private set; }
    protected ILogger Logger { get; private set; }
    protected IEventBus EventBus { get; private set; }

    [SetUp]
    public virtual void SetUp()
    {
        Container = new ContainerBuilder();
        Logger = new NullLogger();
        EventBus = new EventBus();

        Container.RegisterInstance(Logger);
        Container.RegisterInstance(EventBus);
    }

    protected IObjectResolver Build() => Container.Build();
}
```

---

## 21. Performance-Architektur

### 21.1 Quality-Settings

| Setting | Low | Medium | High |
|---------|-----|--------|------|
| Texture-Resolution | 0.5x | 0.75x | 1.0x |
| Anti-Aliasing | Off | 2x MSAA | 2x MSAA |
| Shadow-Quality | Off | Hard | Soft |
| Post-Processing | Off | Light | Full |
| Particle-Pool-Size | 60% | 80% | 100% |
| Frame-Rate-Cap | 30 | 60 | 60 |

### 21.2 Auto-Detection

```csharp
public class QualityAutoDetector
{
    public QualityLevel Detect()
    {
        var systemMemory = SystemInfo.systemMemoryMB;
        var graphicsMemory = SystemInfo.graphicsMemoryMB;
        var processor = SystemInfo.processorFrequency;

        if (systemMemory < 3000 || graphicsMemory < 1000) return QualityLevel.Low;
        if (systemMemory < 6000) return QualityLevel.Medium;
        return QualityLevel.High;
    }
}
```

### 21.3 Memory-Watcher

```csharp
public class MemoryWatcher : MonoBehaviour
{
    private void Update()
    {
        var usedMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        if (usedMb > 350)
        {
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }
    }
}
```

---

## 22. Server-Side (Cloud Functions)

Datei: `Server/SERVEROPS.md` (folgt separat)

**Stack:** Node.js 20 + TypeScript + Firebase Functions v2

**8 Functions:**
1. `validateIapReceipt`
2. `validateMiniGameScore`
3. `settleBattlePassRewards`
4. `createGuild`
5. `onPlayerWriteValidate`
6. `onReportReceived`
7. `onWarSeasonCompleted`
8. `liveEventRefresh`

---

## 22.5 Phase 2: Photon Fusion Live-PvP (geplant Monat 19-21)

Für die geplante Phase-2-Erweiterung "Echtzeit-Klan-Match 5v5" wird eine neue Architektur-Schicht eingeführt.

### 22.5.1 Stack-Erweiterung

| Komponente | Wahl |
|------------|------|
| **Netcode** | Photon Fusion 2.x (NetworkObject + NetworkBehaviour) |
| **Server** | Photon Cloud (europe-west1) |
| **Auth** | Firebase Auth Token → Photon Custom Authentication Webhook |
| **Latency-Ziel** | < 100ms zwischen DACH-Spielern |

### 22.5.2 Neue Scene & Lifetime-Scope

```
LivePvP.unity (additive bei Match-Start)
└── LivePvPLifetimeScope
    ├── PhotonNetworkRunner
    ├── LivePvPMatchController
    ├── LivePvPNetworkSyncService (HMAC-frei, Server-Authoritative)
    └── LivePvPViewBinder
```

### 22.5.3 Match-Lifecycle

1. Klan-Leader startet Match-Request
2. Server-Matchmaking (Photon Lobby) findet Gegner-Klan
3. Beide Klans laden LivePvP.unity additive
4. Match läuft 5 Minuten (Build-Output-Race)
5. Server berechnet Sieger
6. Belohnungen via Firebase verteilt
7. LivePvP.unity wird unloaded

### 22.5.4 Anti-Cheat

- **Server-Authoritative State** (Photon-Server validiert alle Actions)
- Kein HMAC nötig (Photon-Server vertraut)
- Replay-Speicherung für Streit-Fälle

### 22.5.5 Sub-Phasen

| Sub-Phase | Aufwand | Output |
|-----------|---------|--------|
| Photon-Setup | 1 Woche | Lobby + Matchmaking |
| Network-Sync | 2-3 Wochen | NetworkObjects für Worker/Workshop/Orders |
| Match-Logic | 2 Wochen | Match-Engine, Sieger-Berechnung |
| UI-Polish | 1 Woche | Spectator-Cam, Result-Screen |
| Beta-Test | 2 Wochen | Stress-Test mit echten Klans |

**Voraussetzung:** Beta-MAU > 1000 (sonst nicht wirtschaftlich)

---

## 23. Diagramme

### 23.1 Sequenz: Order-Akzept-Flow

```
User-Tap → DashboardView
  ↓
DashboardViewModel.AcceptOrderCommand.Execute()
  ↓
OrderService.AcceptAsync(orderId, ct)
  ↓
GameStateService.ExecuteWithLock(state => state.Orders.Add(order))
  ↓
EventBus.Publish(OrderAcceptedEvent)
  ↓
SaveService.SaveAsync(state, ct)  ⇒ Local + Cloud
  ↓
ProgressionFeedbackCoordinator (subscribed)
  ↓
EventBus.Publish(FloatingTextRequestedEvent("+€100", green))
  ↓
DashboardView (subscribed) → animiert Floating-Text
```

### 23.2 Sequenz: Mini-Game-Start

```
User tippt "Auftrag starten" → DashboardView
  ↓
OrderService.StartMiniGameAsync(orderId)
  ↓
MiniGameRegistry.GetForOrder(orderId) → MiniGameType.Sawing
  ↓
SceneLoader.LoadAsync("MiniGame", Additive)
  ↓
MiniGameLifetimeScope.Configure (registriert SawingController)
  ↓
SawingMiniGameController.Initialize(orderId)
  ↓
Game.Update() läuft, ParticleSystem aktiv
  ↓
User schließt Mini-Game ab
  ↓
EventBus.Publish(MiniGameCompletedEvent(Sawing, Perfect))
  ↓
OrderService (subscribed) → schreibt Reward, completed Order
  ↓
SceneLoader.UnloadAsync("MiniGame")
```

---

## 24. Links

- [CLAUDE.md](CLAUDE.md) — Conventions
- [PLAN.md](PLAN.md) — Strategischer Plan
- [DESIGN.md](DESIGN.md) — Game Design Document
- [ROADMAP.md](ROADMAP.md) — Wochenplan
