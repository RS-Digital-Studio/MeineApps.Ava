# RebornSaga.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte App-Logik (Engine, Scenes,
Overlays, Rendering, Services, ViewModels, Views, Data, Models) und wird von `RebornSaga.Android`
und `RebornSaga.Desktop` referenziert.
Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md). App-Überblick → [../CLAUDE.md](../CLAUDE.md).

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

- **`Initialize()`** — XAML laden, `RequestedThemeVariant = Dark` (fest, kein Theme-Wechsel).
- **`ConfigureServices(IServiceCollection)`** — alles **Singleton**:
  - Core: `IPreferencesService` → `PreferencesService("RebornSaga")`, `ILocalizationService` →
    `LocalizationService(AppStrings.ResourceManager, …)` + `locService.Initialize()`.
  - Premium: `services.AddMeineAppsPremium()`, dann Android-Override via Factories
    (`RewardedAdServiceFactory`, `PurchaseServiceFactory`).
  - Asset-Delivery: `IAssetDeliveryService` → `AssetDeliveryService`.
  - Rendering: `SpriteCache` (LRU-Cache, max 30 Bilder).
  - Engine: `SceneManager`, `StoryEngine`, `BattleEngine`.
  - RPG-Services: `SkillService`, `InventoryService`, `AffinityService`, `FateTrackingService`,
    `CodexService`, `ProgressionService`, `SaveGameService`, `GoldService`, `ChapterUnlockService`,
    `TutorialService`, `DailyService`.
  - Audio: `AudioServiceFactory?.Invoke(sp)` (Android setzt Factory) ?? `AudioService` (Desktop-Stub).
  - ViewModel: `MainViewModel`.
- **`OnFrameworkInitializationCompleted()`** — DI bauen → `MainView` instanziieren →
  `DataContext = Services.GetRequiredService<MainViewModel>()`. Desktop: `ShutdownRequested` → `DisposeServices()`.
- **`InitializeServicesAsync()`** — wird von `MainView.OnAttachedToVisualTree` via
  `MainViewModel.InitializeAsync()` aufgerufen:
  1. `SkillService.LoadSkills()` + `InventoryService.LoadItems()` via `Task.WhenAll` parallel
     (Voraussetzung für `SaveGameService.LoadGameAsync`).
  2. `CharacterRenderer.Initialize(spriteCache)` + `BackgroundCompositor.SetSpriteCache(spriteCache)`.
  3. `IPurchaseService.InitializeAsync()` fire-and-forget.
- **`DisposeServices()`** — Desktop: `ShutdownRequested` | Android: `OnDestroy`.
  Disposed: `IAudioService`, `SpriteCache`, `SaveGameService`.

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `RebornSaga.ViewModels` |
| `Views/` | `RebornSaga.Views` |
| `Engine/` | `RebornSaga.Engine` |
| `Scenes/` | `RebornSaga.Scenes` |
| `Overlays/` | `RebornSaga.Overlays` |
| `Rendering/` | `RebornSaga.Rendering` |
| `Services/` | `RebornSaga.Services` |
| `Models/` | `RebornSaga.Models` |
| `Data/` | Embedded JSON-Assets (kein Namespace) |

## Unterordner

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `ViewModels/` | MainViewModel — Render/Update/Input-Delegation, Back-Press-Flow | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | MainView (SKCanvasView, ~30fps Game-Loop + Bedarfs-Rendering, DPI-skalierte Touch-Koordinaten) | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Engine/` | Scene-Basisklasse, SceneManager (Stack + Overlays + Transitions), InputManager, Camera | [Engine/CLAUDE.md](Engine/CLAUDE.md) |
| `Scenes/` | 12 konkrete Szenen (Title, Battle, Dialogue, Overworld, …) | [Scenes/CLAUDE.md](Scenes/CLAUDE.md) |
| `Overlays/` | 10 Overlays (Pause, StatusWindow, LevelUp, GameOver, …) | [Overlays/CLAUDE.md](Overlays/CLAUDE.md) |
| `Rendering/` | Alle SkiaSharp-Renderer (Backgrounds, Characters, Effects, Map, UI) | [Rendering/CLAUDE.md](Rendering/CLAUDE.md) |
| `Services/` | 18 Services (Story, Battle, Save, Audio, Asset-Delivery, …) | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `Models/` | Datenmodelle (Player, Enemy, Item, Skill, Chapter, SaveData, Enums) | [Models/CLAUDE.md](Models/CLAUDE.md) |
| `Data/` | Embedded JSON-Dateien (Chapters, Dialogue, Maps, Skills, Items, Enemies) | [Data/CLAUDE.md](Data/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`,
Isekai System Blue #4A90D9), `Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/`,
`Icons/` (`RebornSaga.Icons`).

