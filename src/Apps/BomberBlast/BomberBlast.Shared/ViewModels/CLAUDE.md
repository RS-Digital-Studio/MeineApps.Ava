# ViewModels — UI-Logik & Feature-Module

25 ViewModels (alle Singleton außer `WhatsNewViewModel` + `BottomTabBarViewModel` → Transient).
Nur UI-Logik — Domänenlogik delegiert an Services/GameEngine.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

---

## MainViewModel-Kompositor

`MainViewModel` (~480 LOC) ist ein reiner **Compositor** — er bündelt 5 Feature-Module
und forward deren State als Properties an `MainView.axaml`-Bindings.
Alle bestehenden AXAML-Bindings (`{Binding MenuVm}`, `{Binding ActiveView}`,
`{Binding IsAnyDialogOpen}`, …) treffen auf Forwarder-Properties.

### Feature-Module

| Modul | Datei | Verantwortung |
|-------|-------|--------------|
| `INavigationCoordinator` | `Navigation/NavigationCoordinator.cs` | ActiveView + 26 Routen, CloudSave-Race-Guard |
| `IBottomTabController` | `Navigation/BottomTabController.cs` | 5 Sub-Tabs, bidirektionale View↔Tab-Sync |
| `IDialogPresenter` | `Services/DialogPresenter.cs` | Alert/Confirm/IsAnyDialogOpen-Aggregat |
| `IChildViewModelRegistry` | `ChildViewModelRegistry.cs` | 11 Eager + 15 Lazy VMs, EnsureXxx()-Methoden |
| `ILifecycleHub` | `LifecycleHub.cs` | HandleBackPressed, CloudSaveInitTask, OnAdUnavailable |

### State-Sync-Pattern

Jedes Modul feuert `StateChanged`/`ActiveViewChanged`/`VmInstantiated`. MainViewModel
subscribed und ruft `OnPropertyChanged` für betroffene Forwarder.

---

## ChildViewModelRegistry (`ChildViewModelRegistry.cs`)

Verwaltet 11 Eager + 15 Lazy VMs:

**Eager** (sofort instanziiert, Kern-Navigation): MainMenu, Game, LevelSelect, Settings,
Help, HighScores, GameOver, Victory, BossRush, PlayHub, BottomTabBar.

**Lazy** (bei Bedarf via `EnsureXxx()`): Shop, Achievements, DailyChallenge, LuckySpin,
WeeklyChallenge, Statistics, QuickPlay, Deck, Dungeon, BattlePass, Collection, League,
Profile, GemShop, WhatsNew.

### EnsureXxx()-Pattern

```csharp
public ShopViewModel EnsureShop()
{
    if (_shopVm is { } existing) return existing;
    var vm = _shopVmLazy.Value;
    WireCommon(vm);                          // Navigation + Game-Juice via IGameEventBus
    vm.PurchaseSucceeded += ...;             // VM-spezifisches Sub-Wiring
    _shopVm = vm;
    VmInstantiated?.Invoke(nameof(ShopVm)); // → MainViewModel.OnPropertyChanged
    return vm;
}
```

Idempotent — zweiter Aufruf gibt vorhandene Instanz zurück, keine Re-Verdrahtung.

### ChildViewModelWiring (`ChildViewModelWiring.cs`)

```csharp
// Isolierter Helper — enthält die WireCommon-Logik für Unit-Tests
ChildViewModelWiring.Wire(vm, onNavigate, eventBus);
```

VMs sind `sealed` → NSubstitute kann nicht mocken → Helper als Testbarkeits-Brücke.

---

## LifecycleHub (`LifecycleHub.cs`)

| Methode/Property | Beschreibung |
|-----------------|-------------|
| `HandleBackPressed()` | Hierarchische Android-Back-Navigation: Overlays → Sub-Pages → Double-Back-to-Exit |
| `CloudSaveInitTask` | Im Ctor gestartet (Cloud-Pull). `NavigationCoordinator` wartet darauf (3s-Cap). |
| `OnAdUnavailable()` | Rewarded-Ad nicht verfügbar — zeigt Dialog via `IDialogPresenter` |

---

## ViewModels-Übersicht

| ViewModel | Partial-Files | Besonderheit |
|-----------|--------------|-------------|
| `MainViewModel.cs` | — | Compositor, 5 Feature-Module, `IDisposable`, `OnAppeared()` |
| `MainMenuViewModel.cs` | `.Dashboard.cs`, `.Onboarding.cs` | Daily-Reward, Comeback-Bonus, Feature-Unlocks in `OnAppeared()` |
| `GameViewModel.cs` | — | GameEngine-Wrapper, Gamepad-API, `IsAnyOverlayOpen`-Aggregat |
| `ShopViewModel.cs` | `.Upgrades.cs`, `.Deals.cs`, `.Skins.cs` | `IDisposable` (BalanceChanged-Subscription) |
| `LevelSelectViewModel.cs` | — | World-Grid, Stars-Display, `IDisposable` |
| `MainMenuViewModel.cs` | `.Dashboard.cs`, `.Onboarding.cs` | `IDisposable` (DispatcherTimer) |
| `ProfileViewModel.cs` | `.Customize.cs` | Cosmetics-Galerie, `IDisposable` |
| `DeckViewModel.cs` | — | Karten-Verwaltung, Crafting, `IDisposable` |
| `DungeonViewModel.cs` | — | Roguelike-Run-Flow, Buff-Selection |
| `BattlePassViewModel.cs` | — | 30-Tier Saison, Free/Premium-Track-Anzeige |
| `LeagueViewModel.cs` | — | Leaderboard, NPC-Backfill-Anzeige |
| `ProfileViewModel.cs` | `.Customize.cs` | DSGVO-Export, Account-Delete |
| `LuckySpinViewModel.cs` | — | Glücksrad-Animation, Pity-Anzeige, `IDisposable` |
| `WhatsNewViewModel.cs` | — | **Transient** — wird via `Services.GetService<WhatsNewViewModel>()` erzeugt |
| `BottomTabBarViewModel.cs` | — | **Transient** — View hat eigene Instanz |

---

## Interfaces & Records

| Datei | Zweck |
|-------|-------|
| `INavigable.cs` | `event Action<NavigationRequest>? NavigationRequested`. Alle Child-VMs implementieren es. |
| `IGameJuiceEmitter.cs` | Einheitliches Interface für FloatingText + Celebration (LevelSelectVM, MainMenuVM, ShopVM, GameOverVM, ProfileVM). |
| `NavigationRequest.cs` | Record-Typen: `GoGame`, `GoBack`, `GoShop`, `GoLeague`, … |
| `ActiveView.cs` | Enum mit allen 26 Navigations-Zielen. Basis für `ActiveViewEqualsConverter`. |
| `ProfileTab.cs` | Enum für Profile-Sub-Tabs. |
| `MainViewModelDependencies.cs` | Dependency-Aggregat: fasst 32 Ctor-Parameter zusammen. |
| `IChildViewModelRegistry.cs` | Interface für Registry (Testbarkeit). |
| `ILifecycleHub.cs` | Interface für LifecycleHub. |

---

## Overlay-Hit-Test-Aggregat

`GameViewModel.IsAnyOverlayOpen` = `IsPaused || ShowScoreDoubleOverlay || IsContextHelpVisible || IsLoading`
→ `[NotifyPropertyChangedFor]` automatisch neu berechnet.
→ `GameView.GameCanvas.IsHitTestVisible="{Binding !IsAnyOverlayOpen}"` im XAML.

**Nur XAML-Binding** — kein Code-Behind-Setter daneben (Avalonia LocalValue-Precedence
würde das Binding dauerhaft verdrängen). Beschrieben in der App-Root-CLAUDE.md.
