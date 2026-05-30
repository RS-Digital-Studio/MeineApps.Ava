# ViewModels — Composition & Feature-Logik

Alle ViewModels sind **Singleton** (in `App.axaml.cs` registriert), Ausnahme:
`EconomyFeatureViewModel` (per `new` in MainViewModel.Economy.cs).
Nur UI-Logik — Domänen-Berechnungen liegen in Services.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## MainViewModel — Partial-Split (13 Dateien)

`MainViewModel.cs` ist Composition-, Binding-Anker- und Host-Layer. Die echte Feature-Logik
liegt in Coordinator-Services + Feature-VMs. MainViewModel implementiert 5 Host-Interfaces
(`INavigationHost`, `IWelcomeFlowHost`, `IStartupHost`, `IProgressionFeedbackHost`, `IGameTickHost`)
und verdrahtet Coordinator-Services über `AttachHost(this)` im Ctor.

| Datei | Z. | Inhalt |
|-------|----|----|
| `MainViewModel.cs` | 478 | Service-Felder, Konstruktor, Coordinator-/Host-Wiring |
| `MainViewModel.EventHandlers.cs` | 401 | Money, Order, Lieferant, Event-System, Cinematic, Reputation, StateLoaded, Premium, Sprachwechsel |
| `MainViewModel.Properties.cs` | 399 | ObservableProperties, Computed-Properties, Events, Child-VM-Exposures |
| `MainViewModel.Tabs.cs` | 368 | ActivePage-Enum, IsXxxActive, ActivePageContent, Imperium-Sub-Tabs |
| `MainViewModel.Navigation.cs` | 296 | Tab-Auswahl-Commands, HandleBackPressed, Child-Navigation-Routing |
| `MainViewModel.Helpers.cs` | 182 | FormatMoney, UpdateNetIncomeHeader, UpdateWorkerWarning, Money-Animation, EternalMastery-Refresh |
| `MainViewModel.Dialogs.cs` | 171 | Dialog-Weiterleitungen, Prestige-Durchführung, Notification-Center-Routing |
| `MainViewModel.Lifecycle.cs` | 169 | PauseGameLoop, ResumeGameLoop, OnLiveOrderSpawned, Dispose |
| `MainViewModel.Host.cs` | 163 | 5 Host-Interface-Implementierungen |
| `MainViewModel.Economy.cs` | 161 | RelayCommand-Forwarder zu EconomyFeatureViewModel |
| `MainViewModel.Automation.cs` | 132 | Automation-Property-Wrapper (GameState.Automation), Reputation-Tier-Properties |
| `MainViewModel.Missions.cs` | 28 | LuckySpin-Overlay-Steuerung |
| `MainViewModel.Init.cs` | 15 | InitializeAsync-Forwarder an GameStartupCoordinator |

**Bewusst beibehalten im MainViewModel** (Auslagerung würde AXAML-Bindings brechen):
- Tab-Select-Commands + RelayCommand-Forwarder (`Navigation.cs`, `Economy.cs`)
- EventHandlers.cs-Handler die direkt MainViewModel-Properties setzen

---

## DialogViewModel — Partial-Split (7 Dateien)

`DialogViewModel` ist Coordinator + Confirm-Dialog-Properties + `IDialogService`-Impl.
`PrestigeConfirmationViewModel` ist eine **eigenständige VM** (kein Partial).

| Datei | Z. | Inhalt |
|-------|----|----|
| `DialogViewModel.cs` | 269 | Service-Felder, Konstruktor, Confirm-Dialog, ShowAlert/Confirm, IsAnyDialogVisible, Prestige-Summary, Reputation-Info |
| `DialogViewModel.Achievement.cs` | 25 | Achievement-Dialog |
| `DialogViewModel.Alert.cs` | 29 | Alert-Dialog |
| `DialogViewModel.Hint.cs` | 106 | Hint-Dialog |
| `DialogViewModel.LevelUp.cs` | 30 | LevelUp-Dialog |
| `DialogViewModel.PrestigeSummary.cs` | 45 | Post-Prestige-Summary |
| `DialogViewModel.Story.cs` | 152 | Story-Dialog |
| **`PrestigeConfirmationViewModel.cs`** | **380** | **Eigenständige VM** — Prestige-Tier-Auswahl + Heirloom-Selektion. XAML-Bindings: `DialogVM.PrestigeConfirmation.X` |

---

## Feature-ViewModels (Source-of-Truth)

| ViewModel | Instanziierung | Kernverantwortung |
|-----------|---------------|------------------|
| `HeaderViewModel` | DI Singleton | 16 Properties: Money, Level, GoldenScrews, XP, Net-Income |
| `PrestigeBannerViewModel` | DI Singleton | 18 Properties: IsPrestigeAvailable, Tier-Preview |
| `GoalBannerViewModel` | DI Singleton | CurrentGoal + NavigateToGoalCommand, INavigationService direkt |
| `WelcomeFlowViewModel` | DI Singleton | 14 Props + Welcome-Flow-Logik in `.Logic.cs` Partial |
| `MissionsFeatureViewModel` | DI Singleton | Daily/Weekly/QuickJobs/LuckySpin-Props |
| `EconomyFeatureViewModel` | `new` in MainViewModel.Economy.cs | Workshop/Order/Rush-Commands |

---

## Guild-Sub-ViewModels

| ViewModel | Instanziierung | Muster |
|-----------|---------------|--------|
| `GuildViewModel` | DI Singleton | Hält alle Sub-VMs; Sub-VMs werden im Ctor manuell erstellt |
| `GuildWarSeasonViewModel` | DI Singleton | ViewLocator-Konvention |
| `GuildBossViewModel` | DI Singleton | ViewLocator-Konvention |
| `GuildHallViewModel` | DI Singleton | ViewLocator-Konvention |
| `GuildResearchViewModel` | `new` in GuildViewModel-Ctor | Thin-Wrapper: hält `GuildViewModel Guild { get; }` |
| `GuildChatViewModel` | `new` in GuildViewModel-Ctor | Thin-Wrapper |
| `GuildInviteViewModel` | `new` in GuildViewModel-Ctor | Thin-Wrapper |
| `GuildMembersViewModel` | `new` in GuildViewModel-Ctor | Thin-Wrapper |
| `GuildAchievementsViewModel` | `new` in GuildViewModel-Ctor | Thin-Wrapper |
| `GuildWarViewModel` | `new` in GuildViewModel-Ctor | Thin-Wrapper |
| `GuildCoopOrderViewModel` | DI Singleton | Firebase Co-op-Aufträge |
| `GuildMegaProjectViewModel` | DI Singleton | Material-Spenden-Pipeline |

**ViewLocator-Konvention:** `HandwerkerImperium.ViewModels.Guild.GuildResearchViewModel`
→ `HandwerkerImperium.Views.Guild.GuildResearchView`

**Thin-Wrapper-Pattern:** Sub-VM hat nur `GuildViewModel Guild { get; }`.
Bindings im AXAML via `{Binding Guild.X}`.

---

## MiniGame-ViewModels

Alle 10 MiniGame-VMs erben von `BaseMiniGameViewModel`:

| ViewModel | Datei |
|-----------|-------|
| `BaseMiniGameViewModel` | `MiniGames/BaseMiniGameViewModel.cs` |
| `SawingGameViewModel` | `MiniGames/SawingGameViewModel.cs` |
| `PipePuzzleViewModel` | `MiniGames/PipePuzzleViewModel.cs` |
| `WiringGameViewModel` | `MiniGames/WiringGameViewModel.cs` |
| `PaintingGameViewModel` | `MiniGames/PaintingGameViewModel.cs` |
| `RoofTilingGameViewModel` | `MiniGames/RoofTilingGameViewModel.cs` |
| `BlueprintGameViewModel` | `MiniGames/BlueprintGameViewModel.cs` |
| `DesignPuzzleGameViewModel` | `MiniGames/DesignPuzzleGameViewModel.cs` |
| `InspectionGameViewModel` | `MiniGames/InspectionGameViewModel.cs` |
| `ForgeGameViewModel` | `MiniGames/ForgeGameViewModel.cs` |
| `InventGameViewModel` | `MiniGames/InventGameViewModel.cs` |
| `MiniGameViewModels` | `MiniGameViewModels.cs` — Container-Klasse, aggregiert alle 10 VMs |

`BaseMiniGameViewModel` (27 gemeinsame ObservableProperties, 9 Commands):
- Direktstart: `StartGameAsync()` sofort ohne Start-Button
- `GameRestarted`-Event in `SetOrderId()` — alle Views abonnieren es für `StartRenderLoop()`
- `ContinueCommand` hat Reentrancy-Guard `if (!IsResultShown) return;`
- Countdown 350ms nach 50+ Spielen

---

## Weitere Singleton-ViewModels

| ViewModel | Zweck |
|-----------|-------|
| `AchievementsViewModel` | Achievement-Liste + Fortschritt |
| `OrderViewModel` | Auftrags-Details, Accept/Rush/Cancel |
| `SettingsViewModel` | Grafik, Audio, Sprache, Premium-Toggle, Keep-Screen-On |
| `ShopViewModel` | IAP-Bundles, GS-Pakete, Whale-Tiers |
| `StatisticsViewModel` | Spielzeit, Auftragsstatistiken |
| `WorkshopViewModel` | Workshop-Kauf/Upgrade/Spezialisierung/Rebirth |
| `WorkerMarketViewModel` | Marktpool, Hire, Dismiss |
| `WorkerProfileViewModel` | Training, Bonus, Promotion (Praktikant→E-Tier) |
| `BuildingsViewModel` | Gebäude-Upgrades (7 Gebäude) |
| `ResearchViewModel` | 45 Forschungs-Nodes, Timer, Branch-Tabs |
| `ManagerViewModel` | Manager-Unlocks, Auto-Assign |
| `TournamentViewModel` | Wettbewerbs-Turniere, Score, Rangliste |
| `SeasonalEventViewModel` | Saison-Event, SP-Währung, Event-Shop |
| `BattlePassViewModel` | 30-Tier BattlePass, Free/Premium-Track |
| `CraftingViewModel` | Crafting-Rezepte, Aktive Jobs, Collect |
| `WarehouseSectionViewModel` | Lager-Übersicht, Slot-Upgrade, Auto-Sell-Regeln |
| `MarketViewModel` | Material-Kauf/Verkauf, Heatmap, Preiskurve |
| `LuckySpinViewModel` | Glücksrad-Spin, Pity-Counter |
| `AscensionViewModel` | Ascension-Perks (6 Perks × MaxLevel 3) |
| `NotificationCenterViewModel` | Bell-Icon-Benachrichtigungsliste |
| `ReputationShopViewModel` | Reputations-Shop (5 Items) |
| `CrossPromoViewModel` | House-Ad-Karte in SettingsView |
| `FtueOverlayViewModel` | FTUE-Spotlight-Overlay (10 Steps) |
| `ReferralCardViewModel` | Referral-Code anzeigen/teilen/eingeben |
| `LiveEventBannerViewModel` | Dashboard-Banner-Chip für Live-Events |
| `WorkerAuctionViewModel` | Auktion-View (Auctions-Sub-Namespace) |

---

## IsBusy-Pattern

Alle async-Methoden in `GuildViewModel`, `SettingsViewModel`, `ShopViewModel` und
`WorkerMarketViewModel` sind durch `private bool _isBusy` + try/finally gegen Doppel-Tap-Race geschützt.

## INavigable

`INavigable`-Interface mit `NavigationRequested`-Event ermöglicht Child-VMs
die Navigation ohne direkte MainViewModel-Abhängigkeit:

```csharp
// Child-VM:
public event Action<string>? NavigationRequested;
NavigationRequested?.Invoke("route");

// MainViewModel.Navigation.cs:
_childVm.NavigationRequested += route => NavigationService.NavigateToRoute(route);
```

## Aktiver Gotcha — was NICHT in ViewModels gehört

- Keine `App.Services.GetRequiredService<T>()` im View-Ctor (Android-Crash-Pattern)
- Keine direkten AdMob-/Billing-Calls — nur über `IRewardedAdService` / `IPurchaseService`
- Keine `DateTime.Now` — immer `DateTime.UtcNow`
