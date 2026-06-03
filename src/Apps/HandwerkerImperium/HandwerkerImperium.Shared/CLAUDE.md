# HandwerkerImperium.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte Spiel-Logik (ViewModels,
Views, Services, Grafik, Mini-Games, Gilden) und wird von `HandwerkerImperium.Android` und
`HandwerkerImperium.Desktop` referenziert.
Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md). App-Überblick → [../CLAUDE.md](../CLAUDE.md).

---

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem alle ~70 Services + ~50 ViewModels verdrahtet werden.

### `Initialize()`

XAML laden, `RequestedThemeVariant = Dark` (fest). FpsProfile-Default: Android=Medium,
Desktop=High (überschrieben beim Laden von `SettingsData.GraphicsQuality`).

### `OnFrameworkInitializationCompleted()`

```
ConfigureServices() → BuildServiceProvider()
    → AsyncExtensions.Logger setzen (Release-sicheres Logging)
    → AscensionService.ExternalGoldenScrewBonusProvider + ChallengeConstraints anbinden
      (vermeidet zirkuläre DI — Injection des Callbacks statt Interface-Zirkel)
    → IMiniGameMasteryService eager auflösen (subscribed im Ctor auf PerfectRatingIncremented
      — ohne expliziten Resolve erst nach 1. MainViewModel-Auflösung aktiv)
    → LocalizationService.Initialize() + LocalizationManager.Initialize()
    → SkiaThemeHelper.RefreshColors()
    → Statische Renderer initialisieren: MeisterHansRenderer, WorkerAvatarRenderer,
      WorkshopGameCardRenderer, Icons.GameIcon (alle erhalten IGameAssetService)
    → Lifetime-Branch: Desktop-Window / ISingleViewApplicationLifetime (Android)
    → RunLoadingAsync(splash)
    → Desktop: ShutdownRequested → DisposeServices()
```

**Kein `IActivityApplicationLifetime`-Branch** — HandwerkerImperium nutzt
`ISingleViewApplicationLifetime` auf Android (kein BomberBlast-`MainViewFactory`-Pattern).

### `RunLoadingAsync(splash)`

```
HandwerkerImperiumLoadingPipeline.ExecuteAsync() (Progress → Splash)
    → mindestens GameBalanceConstants.SplashMinimumDisplayMs anzeigen
    → MainViewModel auf UI-Thread via Dispatcher.Post als DataContext setzen
    → splash.FadeOut()
```

Fehler → `FadeOut()` still (kein Leerbildschirm).

### `DisposeServices()` — Reihenfolge (KRITISCH)

Idempotent via `_servicesDisposed`-Flag (OnDestroy kann mehrfach feuern).

1. `IGameLoopService` — ZUERST stoppen (tickt sonst gegen bereits gecleante Services)
2. `GameJuiceEngine` — GPU-Ressourcen (SKPaint/SKFont/SKPath) deterministisch freigeben
3. `ServiceProvider as IDisposable` — disposed automatisch ALLE registrierten IDisposable-Singletons
   in Reverse-Resolution-Order
4. `Icons.GameIcon.ClearCache()` + `Icons.GameIconRenderer.Cleanup()` — statisch, nicht im DI
5. Statische Renderer-Caches: `InventGameRenderer`, `BlueprintGameRenderer`, `CraftTextures`,
   `FireworksRenderer`, `GameCardRenderer`, `LoadingScreenRenderer` — je `DisposeStaticResources()`
   (Shader/MaskFilter/Path/Bitmaps in static Feldern, nicht im DI-Container)

### Platform-Factories (VOR `base.OnCreate` in MainActivity)

```csharp
App.RewardedAdServiceFactory   // IRewardedAdService  → AndroidRewardedAdService
App.PurchaseServiceFactory     // IPurchaseService    → AndroidPurchaseService
App.AudioServiceFactory        // IAudioService       → AndroidAudioService / DesktopAudioService
App.NotificationServiceFactory // INotificationService → AndroidNotificationService
App.PlayGamesServiceFactory    // IPlayGamesService   → AndroidPlayGamesService
App.ReviewPromptRequested      // Action              → LaunchReviewFlow()
App.PlatformKeepScreenOn       // Action<bool>        → FLAG_KEEP_SCREEN_ON
GameAssetService.PlatformAssetLoader  // Assets.Open("visuals/{path}")
```

### DI-Konfiguration (alle Singleton)

**70+ Services**, **50+ ViewModels** — alle Singleton. Wichtige Gruppen:

| Gruppe | Registrierung |
|--------|--------------|
| Core | `IPreferencesService("HandwerkerImperium")`, `ILocalizationService`, `IFrameClock`, `IUiEffectBus` |
| Premium | `services.AddMeineAppsPremium()` + Android-Override-Factories |
| Game-Loop | `IGameStateService`, `ISaveGameService`, `IGameLoopService`, `IIncomeCalculatorService` |
| Coordinator | `IGameStartupCoordinator`, `IProgressionFeedbackCoordinator`, `IGameTickCoordinator`, `ICinematicCoordinator` |
| Navigation | `INavigationService`, `IDialogOrchestrator`, `IMiniGameNavigator` |
| Gilde | `IGuildFacade` (bündelt 9 Sub-Services), `IGuildTickService` (Facade für 5 Tick-Services) |
| Facade | `IMissionsFacade` (bündelt 5 Mission-Services) |
| Live-Ops | `ILiveEventService`, `ILiveEventScoreTracker`, `IWhatsNewService`, `ISeasonalEventService`, `IBattlePassService` |
| VMs | `MainViewModel`, `DialogViewModel`, `MissionsFeatureViewModel`, `HeaderViewModel`, `PrestigeBannerViewModel`, `WelcomeFlowViewModel`, `GoalBannerViewModel`, alle MiniGame-VMs, alle Guild-Sub-VMs |

**Besonderheiten:**
- `EconomyFeatureViewModel` → per `new` in `MainViewModel.Economy.cs` (KEIN DI — braucht mainVM-Kontext)
- Thin-Wrapper-VMs (GuildResearchVM, GuildChatVM, ...) → im `GuildViewModel`-Ctor manuell erstellt

---

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `HandwerkerImperium.ViewModels` |
| `ViewModels/Guild/` | `HandwerkerImperium.ViewModels.Guild` |
| `ViewModels/MiniGames/` | `HandwerkerImperium.ViewModels.MiniGames` |
| `ViewModels/Auctions/` | `HandwerkerImperium.ViewModels.Auctions` |
| `Views/` | `HandwerkerImperium.Views` |
| `Services/` | `HandwerkerImperium.Services` |
| `Services/Interfaces/` | `HandwerkerImperium.Services.Interfaces` |
| `Models/` | `HandwerkerImperium.Models` |
| `Models/Enums/` | `HandwerkerImperium.Models.Enums` |
| `Models/Firebase/` | `HandwerkerImperium.Models.Firebase` |
| `Graphics/` | `HandwerkerImperium.Graphics` |
| `Icons/` | `HandwerkerImperium.Icons` |
| `Helpers/` | `HandwerkerImperium.Helpers` |
| `Loading/` | `HandwerkerImperium.Loading` |

---

## Unterordner-Index

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `ViewModels/` | MainViewModel (13 Partials), alle Feature-/Dialog-/Sub-VMs | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | 5-Tab-Navigation, MiniGame-Views, Gilde-Views, Dialoge | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Services/` | 70+ Services + Interfaces (GameLoop, Gilden, Live-Ops, Firebase) | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `Models/` | GameState (V7), Balancing-Konstanten, Domain-Entities | [Models/CLAUDE.md](Models/CLAUDE.md) |
| `Graphics/` | ~55 SkiaSharp-Renderer, GameJuiceEngine, FpsProfile | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |
| `Icons/` | GameIconKind (224 Icons), GameIcon-Control, GameIconRenderer | [Icons/CLAUDE.md](Icons/CLAUDE.md) |
| `Helpers/` | AsyncExtensions, ProfanityFilter, PageNavigationHelper, HitTester | [Helpers/CLAUDE.md](Helpers/CLAUDE.md) |
| `Loading/` | HandwerkerImperiumLoadingPipeline | [Loading/CLAUDE.md](Loading/CLAUDE.md) |
| `Controls/` | EmptyStateCard, WorkerAvatarControl | [Controls/CLAUDE.md](Controls/CLAUDE.md) |
| `Converters/` | App-eigene AXAML-Converter | [Converters/CLAUDE.md](Converters/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (AppPalette.axaml, Amber #D97706),
`Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/` (Bitmaps, Sounds, Music).

---

## Build

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
```
