# BomberBlast.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte Spiel-Logik (GameEngine,
ViewModels, Views, Services, Graphics, AI, Input) und wird von `BomberBlast.Android` und
`BomberBlast.Desktop` referenziert.
Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md). App-Überblick → [../CLAUDE.md](../CLAUDE.md).

---

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden.

### `Initialize()`

XAML laden, `RequestedThemeVariant = Dark` (fest, kein Theme-Wechsel).

### `OnFrameworkInitializationCompleted()`

Crash-Recovery-Counter inkrementieren **VOR** Init (persistiert Crashes). Bei >= 3 Crashes:
`safeModeRequested = true` → optionale Services (Push, RemoteConfig) werden übersprungen, damit
die App garantiert startet. Danach `InitializeServicesAndUi(safeMode)`.

### `InitializeServicesAndUi(safeMode)`

```
ConfigureServices() → BuildServiceProvider()
    → Statische Sinks setzen: ShaderEffects.Logger, PersistenceHealth.Logger
    → RewardedAdCooldownTracker.Preferences setzen (Restart-Bypass-Schutz)
    → GameLoopSettings.Initialize() (TargetFps 30/60 aus Prefs)
    → GameAssetService.Current setzen (statischer Accessor für Renderer ohne DI)
    → LocalizationService.Initialize() + LocalizationManager.Initialize()
    → SkiaThemeHelper.RefreshColors()
    → Lifetime-Branch: Desktop-Window / Android-Activity / SingleView
    → RunLoadingAsync(splash, safeMode)
```

Push/RemoteConfig werden NICHT mehr hier (vor dem ersten Frame) initialisiert — sie wandern in
`RunLoadingAsync` nach dem ersten Frame (siehe unten).

**Android (`IActivityApplicationLifetime`)**: `MainViewFactory` baut `Panel(MainView + Splash)`,
speichert Root in `_activityRoot`. `RunLoadingAsync` setzt DataContext auf `_activityRoot`
(nicht auf `ISingleViewApplicationLifetime.MainView` — Avalonia 12 spiegelt
`MainViewFactory` NICHT in `SingleView.MainView`).

### `RunLoadingAsync(splash, safeMode)`

```
BomberBlastLoadingPipeline.ExecuteAsync() (Progress → Splash)
    → mindestens 800 ms Splash-Anzeige
    → DataContext = MainViewModel (UI-Thread, Dispatcher.Post)
    → splash.FadeOut()
    → mainVm.OnAppeared() ← NACH DataContext-Set (Game-Juice braucht View-Subscriber)
    → CrashCounter.Reset() (Pipeline-Erfolg)
    → InitializeOptionalServices(safeMode) via Task.Run ← NACH erstem Frame, off-UI-Thread
```

**Fehler** → `FadeOut()` (kein Leerbildschirm). `MenuVm.OnAppearing()` (Daily-Reward,
Feature-Unlocks) läuft in `OnAppeared()` try/catch-geschützt. `InitializeOptionalServices`
löst Push/RemoteConfig auf + startet deren `InitializeAsync()` (im Safe-Mode übersprungen) —
bewusst erst nach dem ersten Frame, damit Service-Resolve + Netz-/IO-Init den Start nicht bremsen.

### `DisposeServices()`

Lazy-aufgelöste VMs (GameVm über `mainVm.GameVm`-Null-Check; Shop/LevelSelect/Menu/Deck/
Dungeon/GemShop/LuckySpin/ProfileVm über `mainVm.XxxVm as IDisposable`) werden nur disposed
wenn sie instanziiert wurden. `GameRenderer` wird **nicht** disposed (Android-OnDestroy ist
kein echter Process-Kill, Renderer würde mit invaliden SKPaint crashen). `InputManager`
disposen (NeonJoystick: 20 SKPaint + 5 SKPath).

### Platform-Factories (vor `base.OnCreate` registrieren)

```csharp
App.RewardedAdServiceFactory   // IRewardedAdService  → AndroidRewardedAdService
App.PurchaseServiceFactory     // IPurchaseService    → AndroidPurchaseService
App.SoundServiceFactory        // ISoundService       → AndroidSoundService
App.VibrationServiceFactory    // IVibrationService   → AndroidVibrationService
App.PlayGamesServiceFactory    // IPlayGamesService   → AndroidPlayGamesService
App.PushNotificationServiceFactory  // → AndroidPushNotificationService
App.RemoteConfigServiceFactory // IRemoteConfigService → FirebaseRemoteConfigService (Debug-Flag)
GameAssetService.PlatformAssetLoader  // Assets.Open("visuals/{path}")
```

Desktop-Fallbacks: `NullSoundService`, `NullVibrationService`, `NullPushNotificationService`,
`DefaultsRemoteConfigService`, `NullPlayGamesService`, `NullCloudSaveService`.

### DI-Konfiguration

**~65 Services** + **27 ViewModels** (alle Singleton außer `WhatsNewViewModel` → Transient;
`BottomTabBarViewModel` ist **Singleton**, da genau eine BottomTabBar existiert).

Besonderheiten:
- `services.AddLazyResolution()` → `LazyServiceExtensions.cs` für zirkuläre Abhängigkeiten.
- **5 Feature-Module** als Singletons registriert mit Lazy-Lambda-Auflösung für die zwei
  Zirkel (`BottomTabController`↔`NavigationCoordinator`,
  `NavigationCoordinator`↔`LifecycleHub`) — Lambda läuft erst zur Laufzeit.
- `MainViewModelDependencies` aggregiert 34 Ctor-Parameter (11 Eager-VMs + 15 Lazy-VMs + 8 Services).
- `IRngProvider` → `DeterministicRngProvider` (xoshiro256+, Seed aus `DateTime.UtcNow.Ticks`).

---

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `BomberBlast.ViewModels` |
| `Views/` | `BomberBlast.Views` |
| `Core/` | `BomberBlast.Core` |
| `Core/Modes/` | `BomberBlast.Core.Modes` |
| `Core/Combat/` | `BomberBlast.Core.Combat` |
| `Graphics/` | `BomberBlast.Graphics` |
| `Services/` | `BomberBlast.Services` |
| `Models/` | `BomberBlast.Models` |
| `Icons/` | `BomberBlast.Icons` |
| `Navigation/` | `BomberBlast.Navigation` |
| `Loading/` | `BomberBlast.Loading` |
| `Input/` | `BomberBlast.Input` |
| `AI/` | `BomberBlast.AI` |

---

## Unterordner-Index

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `AI/` | A\*-Pathfinding, BFS Safe-Cell, Danger-Zone | [AI/CLAUDE.md](AI/CLAUDE.md) |
| `Controls/` | SkiaSharp-Canvas-Controls (GameButton, Achievement, Shop, Medal, EmptyState, MenuBackground) | [Controls/CLAUDE.md](Controls/CLAUDE.md) |
| `Converters/` | AXAML-Converter (ActiveView, Bool→Opacity, GameIconKind) | [Converters/CLAUDE.md](Converters/CLAUDE.md) |
| `Core/` | GameEngine (Partial), GameState, Modes, Combat, Audio, Multiplayer, Replay | [Core/CLAUDE.md](Core/CLAUDE.md) |
| `Extensions/` | `LazyServiceExtensions` für DI-Zirkel | [Extensions/CLAUDE.md](Extensions/CLAUDE.md) |
| `Graphics/` | GameRenderer (10 Partials), Atmosphärische Subsysteme, Splash, Shader | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |
| `Icons/` | Eigenes Neon-Arcade Icon-System (159 Icons, PathIcon-Ableitung) | [Icons/CLAUDE.md](Icons/CLAUDE.md) |
| `Input/` | InputManager, NeonJoystick, Keyboard, Gamepad, KonamiCode | [Input/CLAUDE.md](Input/CLAUDE.md) |
| `Loading/` | `BomberBlastLoadingPipeline`, `LoadingTips` | [Loading/CLAUDE.md](Loading/CLAUDE.md) |
| `Models/` | Entities, Grid, Levels, Dungeon, Cards, BattlePass, Cosmetics, CloudSave | [Models/CLAUDE.md](Models/CLAUDE.md) |
| `Navigation/` | NavigationCoordinator, BottomTabController, NavigationRouteParser | [Navigation/CLAUDE.md](Navigation/CLAUDE.md) |
| `Services/` | ~65 Services + Interfaces + DialogPresenter + Logging-Provider | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `ViewModels/` | 27 ViewModels + ChildViewModelRegistry + LifecycleHub + 5 Feature-Module | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | 26 Views + 5 Components (GameView, MainView, BottomTabBar, Overlays) | [Views/CLAUDE.md](Views/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (AppPalette.axaml, Orange #FF6B35),
`Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/` (Bild-Assets),
`Resources/` (`remote_config_defaults.json`).

---

## Build

```bash
dotnet build src/Apps/BomberBlast/BomberBlast.Shared
```
