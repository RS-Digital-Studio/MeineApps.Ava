# ViewModels — UI-Logik & Feature-Module

27 ViewModels (alle Singleton, außer `WhatsNewViewModel` → Transient laut DI-Registrierung;
`BottomTabBarViewModel` → Singleton, da genau eine BottomTabBar existiert).
Nur UI-Logik — Domänenlogik delegiert an Services/GameEngine.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

---

## MainViewModel-Kompositor

`MainViewModel` (~480 LOC) ist ein reiner **Compositor** — er bündelt 5 Feature-Module
und forwarded deren State als Properties an `MainView.axaml`-Bindings.
Alle bestehenden AXAML-Bindings (`{Binding MenuVm}`, `{Binding ActiveView}`,
`{Binding IsAnyDialogOpen}`, …) treffen auf Forwarder-Properties.

### Feature-Module

| Modul | Datei | Verantwortung |
|-------|-------|--------------|
| `INavigationCoordinator` | `Navigation/NavigationCoordinator.cs` | ActiveView + Routen, CloudSave-Race-Guard |
| `IBottomTabController` | `Navigation/BottomTabController.cs` | 4 Sub-Tabs, bidirektionale View↔Tab-Sync |
| `IDialogPresenter` | `Services/DialogPresenter.cs` | Alert/Confirm/IsAnyDialogOpen-Aggregat |
| `IChildViewModelRegistry` | `ChildViewModelRegistry.cs` | 11 Eager + 15 Lazy VMs, EnsureXxx()-Methoden |
| `ILifecycleHub` | `LifecycleHub.cs` | HandleBackPressed, CloudSaveInitTask, OnAdUnavailable |

**Hinweis:** `NavigationCoordinator.cs` und `BottomTabController.cs` liegen in
`BomberBlast.Shared/Navigation/`, nicht im ViewModels-Ordner. Im
`ViewModels/Navigation/`-Unterordner liegen nur `NavigationQueryParser.cs` und
`NavigationRouteMapper.cs` (Hilfsklassen für Routen-Parsing).

### State-Sync-Pattern

Jedes Modul feuert `StateChanged`/`ActiveViewChanged`/`VmInstantiated`. MainViewModel
subscribed und ruft `OnPropertyChanged` für betroffene Forwarder.

---

## ChildViewModelRegistry (`ChildViewModelRegistry.cs`)

Verwaltet 11 Eager + 15 Lazy VMs:

**Eager** (sofort instanziiert, Kern-Navigation): MainMenu, LevelSelect, Settings,
HighScores, GameOver, Help, Victory, BossRush, WhatsNew, PlayHub, BottomTabBar.

**Lazy** (bei Bedarf via `EnsureXxx()`): Game, Shop, Achievements, DailyChallenge, LuckySpin,
WeeklyChallenge, Statistics, QuickPlay, Deck, Dungeon, BattlePass, Collection, League,
Profile, GemShop.

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
| `HandleBackPressed()` | Hierarchische Android-Back-Navigation: Dialoge → Score-Double-Overlay → Pause/Resume → Sub-Pages → Double-Back-to-Exit |
| `OnAppPaused()` | Dialoge abbrechen, Spiel pausieren, Musik stoppen |
| `OnAppResumed()` | Musik wieder aufnehmen (außer im Game-Pause-Overlay) |
| `CloudSaveInitTask` | Im Ctor gestartet (Cloud-Pull). `NavigationCoordinator` wartet darauf (3s-Cap). |
| `OnAdUnavailable()` | Rewarded-Ad nicht verfügbar — zeigt Dialog via `IDialogPresenter` |

---

## ViewModels-Übersicht

| ViewModel | Partial-Files | Besonderheit |
|-----------|--------------|-------------|
| `MainViewModel.cs` | — | Compositor, 5 Feature-Module, `OnAppeared()` |
| `MainMenuViewModel.cs` | `.Dashboard.cs`, `.Onboarding.cs` | Daily-Reward, Comeback-Bonus, Feature-Unlocks in `OnAppearing()` |
| `GameViewModel.cs` | — | GameEngine-Wrapper, Gamepad-API, `IsAnyOverlayOpen`-Aggregat, `IDisposable` |
| `ShopViewModel.cs` | `.Upgrades.cs`, `.Deals.cs`, `.Skins.cs` | `IDisposable` (BalanceChanged-Subscription) |
| `LevelSelectViewModel.cs` | — | World-Grid, Stars-Display, `IDisposable` |
| `SettingsViewModel.cs` | — | App-Einstellungen, AlertRequested + ConfirmationRequested |
| `HighScoresViewModel.cs` | — | Score-Liste |
| `HelpViewModel.cs` | — | Hilfe-Seiten |
| `GameOverViewModel.cs` | — | Tod-Screen, ConfirmationRequested |
| `VictoryViewModel.cs` | — | Sieges-Screen |
| `BossRushViewModel.cs` | — | Boss-Rush-Modus |
| `PlayHubViewModel.cs` | — | Spielmodi-Auswahl |
| `BottomTabBarViewModel.cs` | — | Tab-Brushes (Active/Inactive), `IDisposable` (Hub-Subscription) |
| `WhatsNewViewModel.cs` | — | What's-New-Modal, `Closed`-Event |
| `ProfileViewModel.cs` | `.Customize.cs` | Cosmetics-Galerie, DSGVO-Export, Account-Delete, `IDisposable` |
| `DeckViewModel.cs` | — | Karten-Verwaltung, Crafting, `IDisposable` |
| `DungeonViewModel.cs` | — | Roguelike-Run-Flow, Buff-Selection, Ad + IAP wiring |
| `BattlePassViewModel.cs` | — | 30-Tier Saison, Free/Premium-Track-Anzeige |
| `LeagueViewModel.cs` | — | Leaderboard, NPC-Backfill-Anzeige |
| `LuckySpinViewModel.cs` | — | Glücksrad-Animation, Pity-Anzeige, `IDisposable` |
| `AchievementsViewModel.cs` | — | Achievement-Liste |
| `CollectionViewModel.cs` | — | Cosmetics-Sammlung |
| `StatisticsViewModel.cs` | — | Spielstatistiken |
| `DailyChallengeViewModel.cs` | — | Tägliche Herausforderung |
| `WeeklyChallengeViewModel.cs` | — | Wöchentliche Herausforderung |
| `QuickPlayViewModel.cs` | — | Schnellstart |
| `GemShopViewModel.cs` | — | Edelstein-Shop, ConfirmationRequested |

---

## Interfaces & Records

| Datei | Zweck |
|-------|-------|
| `INavigable.cs` | `event Action<NavigationRequest>? NavigationRequested`. Alle Child-VMs implementieren es. |
| `IGameJuiceEmitter.cs` | Drei Interfaces: `IFloatingTextEmitter`, `ICelebrationEmitter`, `IGameJuiceEmitter` (Kombo). Getrennt damit VMs ohne Celebration (z.B. `GameOverViewModel`) nicht CS0067 erzeugen. |
| `NavigationRequest.cs` | Typsichere Record-Typen: `GoGame`, `GoMainMenu`, `GoBack`, `GoShop`, `GoLeague`, `GoResetThen(Then)`, … (22 Records). |
| `ActiveView.cs` | Enum mit allen Navigations-Zielen. Basis für `ActiveViewEqualsConverter`. |
| `ProfileTab.cs` | Enum für Profile-Sub-Tabs. |
| `MainViewModelDependencies.cs` | Dependency-Aggregat: fasst 34 Ctor-Parameter zusammen (11 Eager-VMs + 15 Lazy-VMs + 8 Services). |
| `IChildViewModelRegistry.cs` | Interface für Registry (Testbarkeit). |
| `ILifecycleHub.cs` | Interface für LifecycleHub. |
| `Navigation/NavigationQueryParser.cs` | Query-Parameter-Parsing für Routen-Strings. |
| `Navigation/NavigationRouteMapper.cs` | Record-Routen-Typen auf `ActiveView`-Enum mappen. |

---

## Overlay-Hit-Test-Aggregat

`GameViewModel.IsAnyOverlayOpen` = `IsPaused || ShowScoreDoubleOverlay || IsContextHelpVisible || IsLoading`
→ `[NotifyPropertyChangedFor]` automatisch neu berechnet.
→ `GameView.GameCanvas.IsHitTestVisible="{Binding !IsAnyOverlayOpen}"` im XAML.

**Nur XAML-Binding** — kein Code-Behind-Setter daneben (Avalonia LocalValue-Precedence
würde das Binding dauerhaft verdrängen). Beschrieben in der App-Root-CLAUDE.md.
