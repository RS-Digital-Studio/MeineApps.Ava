# HandwerkerImperium - Avalonia Port

## Architektur

```
HandwerkerImperium/
├── HandwerkerImperium.Shared/     # net10.0 (Shared code)
│   ├── App.axaml/.cs              # DI configuration
│   ├── Models/                    # 16 model files
│   │   ├── Enums/                 # WorkshopType, MiniGameType, MiniGameResult, OrderDifficulty, AchievementCategory
│   │   ├── Events/                # GameEvents (all event args)
│   │   ├── GameState.cs           # Main game state (QuickJobs, DailyChallengeState, Tools)
│   │   ├── Workshop.cs            # Workshop model
│   │   ├── Worker.cs              # Worker model
│   │   ├── Order.cs               # Order/contract model
│   │   ├── QuickJob.cs            # Schnell-Auftraege (15min Rotation)
│   │   ├── DailyChallenge.cs      # Taegliche Aufgaben (7 Typen + DailyChallengeState)
│   │   ├── Tool.cs                # Werkzeuge (Saw/PipeWrench/Screwdriver/Paintbrush, 5 Level)
│   │   ├── DailyReward.cs         # Daily reward config
│   │   ├── Achievement.cs         # Achievement definitions
│   │   └── TutorialStep.cs        # Tutorial step definitions
│   ├── Services/                  # 34 files (17 interfaces + 17 implementations)
│   │   ├── Interfaces/            # All service interfaces (incl. IWorkerService, IBuildingService, IResearchService, IEventService, IQuickJobService, IDailyChallengeService)
│   │   ├── GameStateService.cs    # Central game state management
│   │   ├── SaveGameService.cs     # JSON save/load
│   │   ├── GameLoopService.cs     # Main game tick loop
│   │   ├── AchievementService.cs  # Achievement tracking
│   │   ├── AudioService.cs        # Sound effects (stub)
│   │   ├── DailyRewardService.cs  # Daily login rewards
│   │   ├── OfflineProgressService.cs # Offline earnings
│   │   ├── OrderGeneratorService.cs  # Order generation
│   │   ├── PrestigeService.cs     # Prestige/rebirth system
│   │   ├── RewardedAdService.cs   # Ad rewards (simulated on desktop)
│   │   ├── TutorialService.cs     # Tutorial system
│   │   ├── QuickJobService.cs     # Schnell-Auftraege (15min Rotation, 5 Jobs)
│   │   └── DailyChallengeService.cs # Taegliche Aufgaben (3 pro Tag, 7 Typen)
│   ├── ViewModels/                # 15 ViewModels
│   │   ├── MainViewModel.cs       # Tab navigation, game init (7 tabs + overlays)
│   │   ├── AchievementsViewModel.cs
│   │   ├── OrderViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   ├── ShopViewModel.cs
│   │   ├── StatisticsViewModel.cs
│   │   ├── WorkshopViewModel.cs
│   │   ├── SawingGameViewModel.cs
│   │   ├── PipePuzzleViewModel.cs
│   │   ├── WiringGameViewModel.cs
│   │   ├── PaintingGameViewModel.cs
│   │   ├── WorkerMarketViewModel.cs  # Worker hiring market
│   │   ├── WorkerProfileViewModel.cs # Worker detail/management
│   │   ├── BuildingsViewModel.cs     # Building management
│   │   └── ResearchViewModel.cs      # Research skill tree
│   ├── Views/                     # 17 Views (axaml + cs)
│   │   ├── MainWindow.axaml/.cs
│   │   ├── MainView.axaml/.cs     # Tab container
│   │   ├── DashboardView.axaml/.cs
│   │   ├── WorkshopView.axaml/.cs
│   │   ├── OrderView.axaml/.cs
│   │   ├── ShopView.axaml/.cs
│   │   ├── SettingsView.axaml/.cs
│   │   ├── StatisticsView.axaml/.cs
│   │   ├── AchievementsView.axaml/.cs
│   │   └── MiniGames/
│   │       ├── SawingGameView.axaml/.cs
│   │       ├── PipePuzzleView.axaml/.cs
│   │       ├── WiringGameView.axaml/.cs
│   │       └── PaintingGameView.axaml/.cs
│   ├── Converters/                # 7 converters
│   ├── Helpers/                   # Icons, MoneyFormatter, AnimationHelper, AsyncExtensions
│   └── Resources/Strings/        # 6 languages + Designer.cs
├── HandwerkerImperium.Desktop/    # net10.0 (Windows/Linux)
└── HandwerkerImperium.Android/    # net10.0-android
```

## Features
- Idle Tycoon game with 8 workshop types (incl. Architect, GeneralContractor)
- 4 mini-games (Sawing, Pipe Puzzle, Wiring, Painting)
- Worker Market (tier-based hiring, personality, talent, specialization, wage)
- Worker Management (profile, training, rest, mood, fatigue, transfer)
- Building System (7 building types, level 0-5)
- Research Tree (3 branches: Tools, Management, Marketing, 15 levels each)
- Order/contract system with difficulty scaling
- Quick Jobs (5 Schnell-Auftraege mit 15min Rotation, direkt zu MiniGame)
- Daily Challenges (3 taegliche Aufgaben aus 7 Typen, Bonus bei Komplett-Abschluss)
- Tool System (4 Werkzeuge: Saege/Rohrzange/Schraubendreher/Pinsel, Level 0-5, MiniGame-Boni)
- 3-tier prestige system (Bronze/Silver/Gold) with permanent multipliers + prestige shop
- Achievement system (58 achievements, Reset bei Spielstand-Reset)
- Daily rewards
- Offline earnings
- Tutorial system
- JSON save/load to LocalApplicationData
- Premium (remove ads + extended offline)
- 6 languages (DE, EN, ES, FR, IT, PT)
- 5-Tab Navigation (Home, Workers, Research, Shop, Settings) + Stats/Achievements als Dashboard-Icons

## Dependencies
- MeineApps.Core.Ava (themes, localization, preferences)
- MeineApps.Core.Premium.Ava (ads, purchases)
- MeineApps.UI (shared styles/controls)
- CommunityToolkit.Mvvm
- Avalonia 11.3.x

## Build
```bash
dotnet build HandwerkerImperium.Shared/HandwerkerImperium.Shared.csproj
dotnet build HandwerkerImperium.Desktop/HandwerkerImperium.Desktop.csproj
dotnet build HandwerkerImperium.Android/HandwerkerImperium.Android.csproj
```

## Migration Notes
- MAUI Shell navigation replaced with tab-based state management in MainViewModel
- All MAUI-specific APIs replaced with Avalonia equivalents
- DispatcherTimer from Avalonia.Threading (not System.Timers)
- IDisposable pattern for ViewModels with timers (prevents SIGSEGV)
- MiniGames Views in separate namespace `HandwerkerImperium.Views.MiniGames`
- Save game uses `Environment.GetFolderPath(LocalApplicationData)` instead of MAUI FileSystem
- Audio service is a stub (needs platform-specific implementation)

## DataContext Architecture
- **MainViewModel** holds 14 child VMs as properties (injected via constructor)
- **MainView.axaml** sets `DataContext="{Binding ShopViewModel}"` etc. on each sub-view
- **IsVisible bindings** use `#ContentPanel.((vm:MainViewModel)DataContext).IsXxxActive` to resolve against MainViewModel
- **NavigationRequested** events from child VMs are handled by `OnChildNavigation(route)` in MainViewModel
- All VMs are registered as **Singleton** in DI (MainViewModel holds references)
- DashboardView is the only sub-view that directly uses MainViewModel as DataContext

## Deep Code Review Fixes (06.02.2026)

### Models
- **Workshop.cs**: Integer division fix `(Level-1)/2` → `(Level-1)/2.0` in BaseIncomePerWorker
- **Worker.cs**: GUID no longer regenerated on deserialization (empty default, set only in CreateRandom()), HiredAt no longer initialized to UtcNow on deserialize, international names added

### Services
- **SaveGameService**: Atomic write (temp file + rename), backup file (.bak), SemaphoreSlim for thread-safe I/O, corruption recovery from backup, all Debug.WriteLine removed
- **GameStateService**: Full thread safety (locks on AddXp, TryUpgradeWorkshop, TryHireWorker, StartOrder, RecordMiniGameResult, CompleteActiveOrder, CancelActiveOrder, Reset), removed unused _isDirty field
- **DailyRewardService**: Switched from UTC to local time for daily boundary (IsRewardAvailable, WasStreakBroken, TimeUntilNextReward)
- **AchievementService**: Added IDisposable with proper event unsubscription

### ViewModels
- **MainViewModel**: Dialog overlay state properties + commands (LevelUp, OfflineEarnings, DailyReward, AchievementUnlocked), removed Debug.WriteLine/System.Diagnostics
- **SawingGameViewModel**: ILocalizationService injected, all hardcoded German strings localized (UpdateGameTypeVisuals), _isEnding race condition guard
- **PipePuzzleViewModel**: _isEnding race condition guard in EndGameAsync + OnTimerTick
- **WiringGameViewModel**: _isEnding race condition guard in EndGameAsync + OnTimerTick
- **PaintingGameViewModel**: _isEnding race condition guard in EndGameAsync + OnTimerTick

### Views
- **MainView.axaml**: 4 dialog overlays (OfflineEarnings, LevelUp, DailyReward, AchievementUnlocked) with Material Design styling

### Helpers
- **Icons.cs**: Fixed Roofer duplicate (was same as Home), fixed CurrencyEur duplicate (was same as ChartBar)
- **AsyncExtensions**: Switched from Debug.WriteLine to Trace.WriteLine (works in Release builds), added StackTrace to log
- **MoneyFormatter**: Fixed inconsistent threshold in FormatCompact (10_000 → 1_000)

### Localization (all 6 languages)
- 20+ new keys: PlaneNow, LayNow, MeasureNow, StopInGreenZone, StopForSmoothSurface, StopAtPerfectMoment, StopAtRightLength, LevelUpTitle, plus previous batch (ResetGameTitle, etc.)
- 160+ new keys for v2.0 Redesign: Workshop Types (Architect, GeneralContractor), Worker System (12 keys), Worker Profile (22 keys), Worker Tiers (7 keys), Worker Personalities (6 keys), Buildings (8 keys), Building Descriptions (7 keys), Building Effect Formats (11 keys), Research (13 keys), Events (8 keys), Prestige 3-Tier (5 keys), DifficultyExpert, 30 new Achievement keys (60 title+desc entries)

### Localization Key Mismatch Fix (07.02.2026)
- **BuildingType.cs**: `GetLocalizationKey` `Building{type}` -> `type.ToString()` (Canteen statt BuildingCanteen)
- **BuildingType.cs**: `GetDescriptionKey` `Building{type}Desc` -> `{type}Desc` (CanteenDesc statt BuildingCanteenDesc)
- **BuildingType.cs**: `GetEffectKey` `Building{type}Effect` -> `{type}Effect` (CanteenEffect statt BuildingCanteenEffect)
- **WorkerTier.cs**: `GetLocalizationKey` `WorkerTier{tier}` -> `Tier{tier}` (TierF statt WorkerTierF)
- **WorkerPersonality.cs**: `GetLocalizationKey` `Personality{p}` -> `Person{p}` (PersonSteady statt PersonalitySteady)
- **ResearchBranch.cs**: `GetLocalizationKey` `Research{b}` -> `Branch{b}` (BranchTools statt ResearchTools)
- **ResearchBranch.cs**: `GetDescriptionKey` `Research{b}Desc` -> `Branch{b}Desc` (BranchToolsDesc statt ResearchToolsDesc)
- 7 neue BuildingEffect Keys in 6 Sprachen (CanteenEffect, StorageEffect, OfficeEffect, ShowroomEffect, TrainingCenterEffect, VehicleFleetEffect, WorkshopExtensionEffect) - Fallback fuer ungebaute Gebaeude
- 3 neue BranchDesc Keys in 6 Sprachen (BranchToolsDesc, BranchManagementDesc, BranchMarketingDesc)

### Research Tree Localization (07.02.2026)
- 92 neue Keys in 6 Sprachen (EN, DE, ES, FR, IT, PT) fuer Research Tree
- 2 UI Keys: StartResearch, CurrentResearch
- 45 Research Name Keys: ResearchBetterSaws, ResearchPrecisionTools, ..., ResearchMarketDomination (3 Branches x 15 Levels)
- 45 Research Description Keys: ResearchBetterSawsDesc, ..., ResearchMarketDominationDesc (Effekt-Beschreibungen)
- Designer.cs: 92 neue Properties eingefuegt

## Game Loop & Income Fix (06.02.2026)
- **CRITICAL: Game Loop never started** - `_gameLoopService.Start()` was never called in `MainViewModel.Initialize()` → 0€/s passive income
- **CRITICAL: No starting worker** - `GameState.CreateNew()` created Carpenter with 0 workers → income was 0 even if loop ran
- **CRITICAL: No auto-save** - `SaveAsync()` was only called on prestige/import → progress lost on app close
- **Fix: Game Loop started** in `MainViewModel.Initialize()` after state load
- **Fix: Starting worker** added to Carpenter in `GameState.CreateNew()`
- **Fix: Auto-save** every 30 seconds in `GameLoopService.OnTimerTick()`
- **Fix: Save on pause/stop** in `GameLoopService.Pause()` and `Stop()`
- **Fix: Window lifecycle** in `MainWindow.axaml.cs` - Activated→Resume, Deactivated→Pause, Closing→Dispose
- **Fix: Dispose stops game loop** in `MainViewModel.Dispose()`
- **Fix: Workshop card icons** - Removed broken Opacity binding (made unlocked workshops invisible), Panel→Grid for icon overlay

## Order System Fix (06.02.2026)
- **CRITICAL: Multi-task orders broken** - `OnChildNavigation` used `route.StartsWith("..")` which caught ALL routes starting with "..", including relative routes like `"../minigame/sawing?orderId=X"` for next task navigation → navigated to dashboard instead of next mini-game, order stuck as active, all buttons greyed out
- **Fix: OnChildNavigation refactored** - Relative routes `"../minigame/..."` are now stripped of `"../"` prefix and re-routed correctly; pure back-navigation only matches exact `".."` or `"../.."`
- **Fix: Minigame route handling consolidated** - Eliminated duplicated switch statements for minigame navigation into `NavigateToMiniGame()` helper; both `orderId` and `difficulty` query params handled in single code path
- **Fix: OrderCompleted event subscribed** - MainViewModel now listens to `_gameStateService.OrderCompleted` to reset `HasActiveOrder` and replenish orders when running low
- **Fix: Double StartOrder removed** - `OrderViewModel.StartOrderAsync()` no longer calls `_gameStateService.StartOrder()` (already called by `MainViewModel.StartOrderAsync`)

## MiniGame Visual Fixes (06.02.2026)

### PipePuzzle
- **CRITICAL: Puzzle was UNSOLVABLE** - `CheckIfSolved()` passed `Direction.Right` but should be `Direction.Left` → water enters source tile from Left, not Right → TracePath always failed → puzzle could never be solved, always "time up"
- **Fix: Tile rendering replaced** - Unicode box-drawing characters (┃, ┏, ┣, ╋) replaced with colored Border segments (HasTopOpening, HasBottomOpening, HasLeftOpening, HasRightOpening) → works reliably on all platforms, no RotateTransform binding issues
- **Fix: OnRotationChanged notifies opening properties** - When tile is rotated, computed opening properties fire PropertyChanged so border segments update
- **Fix: WrapPanel width constraint** - Added `PuzzleGridWidth` computed property (GridSize * 64) bound to ItemsControl Width → tiles wrap correctly into grid

### PaintingGame
- **Fix: PaintCell visual updates** - `DisplayColor` and `IsPaintedCorrectly` are computed properties that didn't fire PropertyChanged when `IsPainted` changed → painted cells never showed background color or checkmark. Added `OnIsPaintedChanged` and `OnPaintColorChanged` partial methods
- **Fix: WrapPanel width constraint** - Added `PaintGridWidth` computed property (GridSize * 54) bound to ItemsControl Width

### WiringGame
- **Fix: Visual feedback for wire states** - Added `BackgroundColor` (green tint=connected, white highlight=selected, red tint=error), `ContentOpacity` (dimmed when connected), `BorderWidth` (thicker when selected) properties to Wire model
- **Fix: View updated** - Left/Right wire templates show background color, opacity, border width changes + checkmark icon overlay when connected
- **Fix: PropertyChanged notifications** - `OnIsSelectedChanged`, `OnIsConnectedChanged`, `OnHasErrorChanged` notify visual computed properties

## Prestige System Rewrite (07.02.2026)
- **IPrestigeService**: New 3-tier API: `CanPrestige(PrestigeTier)`, `GetPrestigePoints(decimal)`, `DoPrestige(PrestigeTier)`, `GetShopItems()`, `BuyShopItem(string)`, `GetPermanentMultiplier()`
- **PrestigeService**: Full rewrite for Bronze/Silver/Gold tiers, prestige point calculation with tier multiplier, shop item purchase, reset logic per tier
- **Reset preserves**: Achievements, Premium, Settings, PrestigeData, Tutorial, TotalMoneyEarned, TotalPlayTimeSeconds. Silver keeps Research. Gold keeps Shop items.
- **Reset creates**: Carpenter Level 1 with 1 worker (tier from shop bonus), starting money 100 + shop bonus
- **SaveGameService**: v1→v2 migration in LoadFromFileAsync (calls GameState.MigrateFromV1 if Version < 2)
- **StatisticsViewModel**: Updated to use new IPrestigeService API (3-tier system, prestige points display)

## v2.0 Redesign Integration (07.02.2026)
- **App.axaml.cs**: 4 neue Services (IWorkerService, IBuildingService, IResearchService, IEventService) + 4 neue VMs registriert
- **MainViewModel**: 14 child VMs (4 neue: WorkerMarketVM, WorkerProfileVM, BuildingsVM, ResearchVM)
- **MainViewModel**: 4 neue Tab/Overlay States (IsWorkerMarketActive, IsWorkerProfileActive, IsBuildingsActive, IsResearchActive)
- **MainViewModel**: IsTabBarVisible prueft alle neuen Overlays, DeactivateAllTabs setzt alle zurueck
- **MainViewModel**: 3 neue Tab-Commands (SelectWorkerMarketTab, SelectBuildingsTab, SelectResearchTab)
- **MainViewModel**: "worker?id=X" Navigation-Route fuer Worker-Profil
- **MainViewModel**: GetWorkshopIconKind erweitert (Architect=Compass, GeneralContractor=HardHat)
- **MainViewModel**: IncomePerSecond/IncomeDisplay nutzt NetIncomePerSecond statt TotalIncomePerSecond
- **MainViewModel**: WorkshopDisplayModel.LevelProgress max 50 statt 10
- **MainViewModel**: EventHandler<string> wrapper fuer neue VM NavigationRequested Events (vs Action<string> der alten VMs)
- **MainView.axaml**: 4 neue Views eingebunden (WorkerMarket, WorkerProfile, Buildings, Research)
- **MainView.axaml**: Tab-Bar von 5 auf 7 Tabs (Home, Workers, Research, Stats, Achievements, Shop, Settings)
- **AVLN2000 Fixes**: BoolConverters.FalseIsVisible -> DisplayOpacity Property (Buildings+Research), Extension Method Bindings -> computed Properties (Worker.PersonalityIcon, Worker.HiringCost)
- Build: 0 Fehler

## MiniGame Difficulty Tuning (07.02.2026)

### PipePuzzle
- **CRITICAL: Corner Rotation Mapping komplett falsch** - Alle 4 Corner-Rotationen waren falsch zugeordnet
  - Basis-Corner (Rotation 0) hat Oeffnungen: Right + Down
  - Fix: (Right,Down)→0, (Left,Down)→90, (Left,Up)→180, (Right,Up)→270
- **Tile-Groesse**: 60→52px (matching GridCols * 56 Berechnung mit 2px Margin)
- **Pipe-Segmente**: 14→12px breit, 30→26px hoch
- **Lock-Indicator**: Kleines Schloss-Icon (10x10) oben rechts bei Source/Drain Tiles
- **Zeiten reduziert**: Easy 50→40s, Medium 70→55s, Hard 100→75s, Expert 120→95s

### SawingGame
- **MARKER_SPEED**: 0.015→0.022 (~47% schneller)

### OrderDifficulty (alle Minispiele)
- **Perfect Zones kleiner**: Easy 0.25→0.20, Medium 0.15→0.12, Hard 0.12→0.09, Expert 0.08→0.06
- **Speed Multiplier hoeher**: Easy 0.8→0.9, Medium 1.0→1.2, Hard 1.4→1.6, Expert 1.8→2.2

### WiringGame
- **Zeiten reduziert**: Easy 15→12s, Medium 18→15s, Hard 22→18s
- **Expert-Schwierigkeit**: 6 Kabel, 22s (neu hinzugefuegt)

### PaintingGame
- **Zeiten reduziert**: Easy 25→20s, Medium 35→28s, Hard 40→32s
- **Expert-Schwierigkeit**: 6x6 Grid, 38s (neu hinzugefuegt)

## Research View Redesign (07.02.2026)
- **Layout**: 3-Spalten → Tabbed Single-Column (zu schmal auf Handys)
- **Tab-Selector**: 3 Buttons (Tools/Management/Marketing) mit Opacity-basiertem Active-State (1.0 vs 0.5)
- **Branch Description**: Text unter Tabs erklaert den ausgewaehlten Zweig
- **Research Cards**: Level-Badge (Lv.X), Name (14px), Description, Kosten+Icon, Dauer+Icon, Progress Bar, Start-Button
- **ResearchViewModel**: SelectedBranch, SelectedBranchDescription, SelectedTab Properties + 3 Tab-Commands + Tab-Opacity Computed Properties
- **ResearchDisplayItem**: LevelDisplay Property ($"Lv.{Level}")
- **MainViewModel Fix**: OrderViewModel.AlertRequested Subscription entfernt (existiert nicht, nur ConfirmationRequested)

## Game Reset Fix (07.02.2026)
- **Generisches Alert/Confirm Dialog System**: MainViewModel nutzt ShowGenericAlert/ShowGenericConfirm fuer Child-VM Events
- **Spielstand loeschen**: SettingsViewModel → ConfirmationRequested → MainViewModel zeigt Dialog → GameStateService.Reset() + Neu-Initialisierung

## UI Redesign + Neue Features (07.02.2026)

### Tab-Bar Redesign
- **7→5 Tabs**: Home, Workers, Research, Shop, Settings
- **Stats + Achievements**: Als Icon-Buttons im Dashboard-Header (ChartBar + Trophy Icons)
- **Design**: LinearGradientBrush Hintergrund (BackgroundColor→SurfaceColor), Workshop-Cards mit BoxShadow + RadialGradientBrush, kompaktere Research-Cards

### Quick Jobs
- **QuickJob Model**: WorkshopType, MiniGameType, Reward, XP, 15min Rotation
- **QuickJobService**: 5 Jobs generiert, Level-skalierte Rewards (20+Level*5 max 100€, 5+Level*2 max 25 XP)
- **8 TitleKeys**: QuickRepair, QuickFix, ExpressService, SmallOrder, QuickMeasure, QuickInstall, QuickPaint, QuickCheck
- **Dashboard**: Quick Jobs Sektion mit Timer-Display + Start-Buttons + Completed-Indicator
- **MiniGame-Rueckkehr**: QuickJob als completed markiert, Reward+XP vergeben, DailyChallenge benachrichtigt

### Daily Challenges
- **DailyChallenge Model**: 7 Typen (CompleteOrders, EarnMoney, UpgradeWorkshop, HireWorker, CompleteQuickJob, PlayMiniGames, AchieveMinigameScore)
- **DailyChallengeService**: 3 taegliche Challenges, Level-basierte Targets, Auto-Tracking via GameState Events, IDisposable
- **500€ Bonus** bei Komplett-Abschluss aller 3 Challenges
- **Dashboard**: Collapsible Challenge-Banner mit ProgressBars + Claim-Buttons

### Tool System
- **Tool Model**: 4 Typen (Saw/PipeWrench/Screwdriver/Paintbrush), Level 0-5
- **ZoneBonus** (Saw): +5% bis +25% Zielzone in SawingGame
- **TimeBonus** (PipeWrench/Screwdriver/Paintbrush): +5s bis +15s in PipePuzzle/WiringGame/PaintingGame
- **UpgradeCost**: 50→150→400→1000→2500€
- **ShopView**: Werkzeuge-Sektion mit Icon, Level, EffectDescription, Upgrade-Button
- **ShopViewModel**: LoadTools() + UpgradeToolCommand

### Balancing
- **Startgeld**: 100€→250€
- **Workshop Lv.1→2 Upgrade**: 200€→100€ (erstes Upgrade guenstiger)
- **Achievement Reset**: Achievements werden beim Spielstand-Zuruecksetzen zurueckgesetzt

### Lokalisierung
- 28 neue Keys in 6 Sprachen (QuickJobs, DailyChallenges, Tools, etc.)

## Bugfixes (07.02.2026 - Post-Release)

### Lokalisierung
- **DailyChallengeService**: Englische Fallback-Strings entfernt (`?? "English"` → direkte GetString-Aufrufe)
- **MainViewModel**: LanguageChanged-Handler aktualisiert QuickJobs, Challenges, Workshops und alle Child-VMs
- **Clean Build**: Satellite-Assembly-Regenerierung fuer korrekte Sprachaufloesung

### UI/Design
- **MainView.axaml**: Mehrschichtiger Hintergrund (4 Layer: vertikaler Base-Gradient + Primary-Schein oben + Amber/Gold-Schein unten + Secondary-Akzent rechts) statt langweiligem 2-Punkt-Gradient
- **AchievementsView.axaml**: Bottom-Margin (0,0,0,60) auf ItemsControl → kann bis zum Ende scrollen
- **DashboardView.axaml**: UnlockDisplay statt "Lvl X" fuer Workshop-Karten → zeigt "Prestige X" fuer Architect/GU

### Workshop Progress
- **MainViewModel**: Explizites RefreshWorkshops() nach UpgradeWorkshop (zusaetzlich zum Event-Handler) → Progress-Bar aktualisiert zuverlässig
- **WorkshopDisplayModel**: RequiredPrestige + UnlockDisplay computed Property

### Forschungs-Timer
- **MainViewModel.OnGameTick**: ResearchViewModel.UpdateTimer() wird bei aktivem Research aufgerufen → Timer laeuft in Echtzeit statt nur bei Seitenwechsel

### Arbeitermarkt
- **WorkerMarketViewModel**: Slot-Filter → zeigt nur Arbeiter wenn Workshops mit freien Plaetzen existieren
- **WorkerMarketViewModel**: Video-Ad-Refresh (IRewardedAdService.ShowAdAsync()) statt freiem Refresh
- **WorkerMarketViewModel**: HasAvailableSlots + NoSlotsMessage Properties
- **WorkerMarketView.axaml**: "Keine freien Plaetze" Info-Banner wenn alle Workshops voll
- **WorkerMarketView.axaml**: Video-Icon statt Refresh-Icon beim Markt-Aktualisieren Button
- **EUR-Bug**: `\u20AC` → echtes `€` Zeichen in CurrentBalance Default

### Lokalisierung (neu)
- 2 neue resx-Keys in 6 Sprachen: Info, WatchAdToRefresh

## Version
- v2.0.2 (vc7) - Release mit Store Assets
- v2.0.1 (vc6) - Bugfixes, Deep Reviews
- v2.0.0 (vc5) - Avalonia port
