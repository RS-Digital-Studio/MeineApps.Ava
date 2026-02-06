# Meine Apps Avalonia - Projektübersicht

## Migration von MAUI zu Avalonia

Dieses Projekt ist die Avalonia-Version der MAUI-Apps aus `F:\Meine Apps`.

### Ziele:
- Verbesserte UX mit modernem Design
- Multi-Plattform: Android + Windows + Linux
- Keine Feature-Reduktion
- Bekannte MAUI-Bugs behoben

---

## Build-Befehle

```bash
# Gesamte Solution bauen
dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln

# Desktop Release (Windows)
dotnet publish src/Apps/RechnerPlus/RechnerPlus.Desktop -c Release -r win-x64

# Desktop Release (Linux)
dotnet publish src/Apps/RechnerPlus/RechnerPlus.Desktop -c Release -r linux-x64

# Android Release (AAB)
dotnet publish src/Apps/RechnerPlus/RechnerPlus.Android -c Release
```

---

## Projektstruktur

```
F:\Meine_Apps_Ava\
├── MeineApps.Ava.sln
├── Directory.Build.props           # Globale Build-Settings
├── Directory.Packages.props        # Central Package Management
├── CLAUDE.md
├── Releases/
│   └── meineapps.keystore
│
├── src/
│   ├── Libraries/
│   │   ├── MeineApps.CalcLib/      # Calculator Engine (net10.0)
│   │   ├── MeineApps.Core.Ava/     # Themes, Services, Converters
│   │   └── MeineApps.Core.Premium.Ava/  # Ads, IAP, Trial
│   │
│   ├── UI/
│   │   └── MeineApps.UI/           # Shared UI Components
│   │
│   └── Apps/
│       ├── RechnerPlus/
│       │   ├── RechnerPlus.Shared/  # Shared Code (RootNamespace=RechnerPlus)
│       │   ├── RechnerPlus.Android/
│       │   └── RechnerPlus.Desktop/
│       ├── ZeitManager/
│       │   ├── ZeitManager.Shared/  # Shared Code (RootNamespace=ZeitManager)
│       │   ├── ZeitManager.Android/
│       │   └── ZeitManager.Desktop/
│       ├── FinanzRechner/
│       │   ├── FinanzRechner.Shared/  # Shared Code (Models, Services, ViewModels, Views, Converters)
│       │   ├── FinanzRechner.Android/
│       │   └── FinanzRechner.Desktop/
│       ├── HandwerkerRechner/
│       │   ├── HandwerkerRechner.Shared/  # Shared Code (Models, Services, ViewModels, Views, Converters)
│       │   ├── HandwerkerRechner.Android/
│       │   └── HandwerkerRechner.Desktop/
│       ├── HandwerkerImperium/
│       │   ├── HandwerkerImperium.Shared/  # Shared Code (Game, Models, Services, ViewModels, Views)
│       │   ├── HandwerkerImperium.Android/
│       │   └── HandwerkerImperium.Desktop/
│       ├── WorkTimePro/
│       │   ├── WorkTimePro.Shared/  # Shared Code (Models, Services, ViewModels, Views)
│       │   ├── WorkTimePro.Android/
│       │   └── WorkTimePro.Desktop/
│       └── BomberBlast/
│           ├── BomberBlast.Shared/  # Shared Code (Core, AI, Graphics, Input, Models, Services, ViewModels, Views)
│           ├── BomberBlast.Android/ # Landscape-only
│           └── BomberBlast.Desktop/
│
└── tests/
```

---

## Status (06. Februar 2026)

| App | Version | Status |
|-----|---------|--------|
| RechnerPlus | v2.0.0 | In Entwicklung |
| ZeitManager | v2.0.0 | In Entwicklung |
| HandwerkerRechner | v2.0.0 | In Entwicklung |
| FinanzRechner | v2.0.0 | In Entwicklung |
| FitnessRechner | v2.0.0 | In Entwicklung |
| WorkTimePro | v2.0.0 | In Entwicklung |
| HandwerkerImperium | v2.0.0 | In Entwicklung |
| BomberBlast | v2.0.0 | In Entwicklung |

---

## 4 Themes

| Theme | Beschreibung |
|-------|--------------|
| Midnight (Default) | Dark, Indigo Primary |
| Aurora | Dark, Pink/Violet/Cyan Gradient |
| Daylight | Light, Blue Primary |
| Forest | Dark, Green Primary |

---

## Packages (Avalonia 11.3.11)

| Package | Version | Zweck |
|---------|---------|-------|
| Avalonia | 11.3.11 | UI Framework |
| Material.Avalonia | 3.13.4 | Material Design |
| Material.Icons.Avalonia | 2.4.1 | 7000+ SVG Icons |
| DialogHost.Avalonia | 0.10.4 | Dialogs |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM |
| Xaml.Behaviors.Avalonia | 11.3.9.3 | Behaviors |
| LiveChartsCore.SkiaSharpView.Avalonia | 2.0.0-rc6.1 | Charts |
| sqlite-net-pcl | 1.9.172 | Database |

---

## Keystore

| Eigenschaft | Wert |
|-------------|------|
| Speicherort | `Releases/meineapps.keystore` |
| Alias | `meineapps` |
| Passwort | `MeineApps2025` |

---

## Migrationsfortschritt

### Phase 1: Infrastruktur ✓
- [x] Solution-Struktur erstellt
- [x] Directory.Build.props + Directory.Packages.props
- [x] MeineApps.CalcLib kopiert
- [x] MeineApps.Core.Ava erstellt (Themes, Services, Converters, Behaviors)
- [x] MeineApps.UI erstellt (Cards, FAB, EmptyState, Styles)

### Phase 2: RechnerPlus ✓
- [x] Shared Code Projekt
- [x] Android Projekt
- [x] Desktop Projekt
- [x] ViewModels (Calculator, Converter, Settings) - vollständig lokalisiert
- [x] Views (AXAML) - vollständig lokalisiert
- [x] ConverterViewModel: Temperature-Fix, 8 Kategorien, Lokalisierung
- [x] CalculatorViewModel: Lokalisierte Fehlermeldungen + Modus-Labels
- [x] ThemeService: Dynamisches Theme-Switching korrekt (kein statisches Theme in App.axaml)
- [x] MainView: Tab-Fade-Transitions mit CSS-Klassen (Border.TabContent/.Active)
- [x] Keyboard-Support: Ziffern, Operatoren, Enter, Backspace, Escape, Dezimalpunkt
- [x] Desktop Build erfolgreich (0 Fehler, 0 Warnungen)
- [x] Android Build erfolgreich
- [x] 6 Sprachen (DE, EN, ES, FR, IT, PT) - alle .resx komplett
- [ ] Android APK auf Gerät testen
- [ ] Desktop App manuell testen

### Phase 3: ZeitManager ✓
- [x] Shared Projekt + Grundstruktur (3 Projekte, DI, App.axaml)
- [x] 13 Models portiert (Timer, Alarm, Shift, Challenge, Stopwatch) + SoundItem
- [x] Services (DatabaseService, TimerService, AudioService, AlarmSchedulerService, ShiftScheduleService, NotificationService)
- [x] ViewModels (Timer, Stopwatch, Alarm, Settings, Main, AlarmOverlay, ShiftSchedule)
- [x] Views (AXAML) mit Material Icons, Empty States, Dialogs, AlarmOverlay, ShiftScheduleView
- [x] 6 Sprachen (.resx) + Avalonia Theme-Keys (Midnight/Aurora/Daylight/Forest)
- [x] Bugfixes: Timer PropertyChanged + UI-Thread, Theme TextBrush→TextPrimaryBrush
- [x] Features: Audio (6 Toene), Snooze (Timer+Wecker), Tonauswahl, Fullscreen Overlay
- [x] Schichtplan: 15-Schicht + 21-Schicht, Kalender-View, Editor Overlay
- [x] Android: Foreground Service, AlarmReceiver, BootReceiver, AndroidNotificationService
- [x] Desktop: Notifications (Windows Toast / Linux notify-send)
- [x] Full Solution Build: 0 Fehler, 0 Warnungen (Desktop), 0 Fehler (Android, 11 CA1416 Warnungen erwartet)
- [x] Code-Review Round 1+2: Thread-Safety, Android Permissions, Snackbar, Lokalisierung, ProgressPercent Clamping
- [ ] Android APK auf Gerät testen
- [ ] Desktop App manuell testen

### Phase 4: Premium Library + FinanzRechner ✓
- [x] MeineApps.Core.Premium.Ava erstellt (Interfaces, Services, AdBannerView, DI-Extensions)
- [x] FinanzRechner Shared Projekt + Grundstruktur (3 Projekte, DI, App.axaml)
- [x] FinanzRechner ViewModels + Views portiert
- [x] FinanzRechner Desktop Build: 0 Fehler

### Phase 5: HandwerkerRechner ✓
- [x] 3 Projekte: HandwerkerRechner.Shared, HandwerkerRechner.Desktop, HandwerkerRechner.Android
- [x] Models: CraftEngine (16 Berechnungsmethoden), Project, CalculatorCategory (6 Kategorien, 17 CalculatorTypes)
- [x] Services: ProjectService (JSON, SemaphoreSlim), CalculationHistoryService, UnitConverterService
- [x] ViewModels: MainVM, SettingsVM, ProjectsVM + 4 Floor VMs + 5 Premium VMs + CalculatorViewModelBase
- [x] Views: 10 AXAML Views (Floor: Tile, Wallpaper, Paint, Flooring; Premium: Drywall, Electrical, Metal, Garden, RoofSolar)
- [x] 6 Sprachen (.resx) - 291+ Strings + 22 neue Keys (Metalle, Profile, Orientierungen, Einheiten)
- [x] Android Resources: mipmap Icons, styles.xml
- [x] Bugfixes: SaveProject komplett neu (Dialog-Pattern), MessageRequested Events, Button-Klassen (Outlined), lokalisierte Listen
- [x] Calculator-Navigation implementiert (CurrentPage + DataTemplate Overlay, 9 VMs)
- [x] Projekt-Navigation verdrahtet (ProjectsVM -> Calculator mit projectId)
- [x] Tab-Wechsel schliesst offenen Calculator
- [x] Desktop Build: 0 Fehler, 0 Warnungen
- [x] Android Build: 0 Fehler, 0 Warnungen
- [ ] Android APK auf Geraet testen
- [ ] Desktop App manuell testen

### Phase 6: FitnessRechner (in Arbeit)
- [x] 3 Projekte: FitnessRechner.Shared, FitnessRechner.Desktop, FitnessRechner.Android
- [x] Solution-Struktur korrigiert (unter Apps > _FitnessRechner)
- [x] ThemeService Init in App.axaml.cs
- [x] Child-VMs per Constructor Injection (SettingsVM, ProgressVM, FoodSearchVM)
- [x] BMI-Kategorien lokalisiert (8 Keys in 6 Sprachen)
- [x] BodyFat-Kategorien lokalisiert (5 Keys in 6 Sprachen)
- [x] Debug.WriteLine durch MessageRequested Events ersetzt (alle VMs)
- [x] 10 neue Alert-Message Keys in 6 Sprachen
- [x] Dynamische App-Version (statt hardcoded v1.4.0)
- [x] Calculator-Navigation (CurrentPage + DataTemplate Overlay, Tab-Wechsel schliesst Calculator)
- [x] ProgressViewModel.OnAppearingAsync() bei Tab-Wechsel (Charts laden)
- [x] Full Solution Build: 0 Fehler
- [ ] Android APK auf Geraet testen
- [ ] Desktop App manuell testen

### Phase 6a: HandwerkerImperium ✓
- [x] 3 Projekte: HandwerkerImperium.Shared, HandwerkerImperium.Desktop, HandwerkerImperium.Android
- [x] Models (12), Services (4), ViewModels (9), Views (8 AXAML), Converters (6)
- [x] 6 Sprachen (.resx), Android Resources
- [x] Build: Desktop + Android 0 Fehler
- [x] AVLN2000 Compiled Binding Fixes (65 Fehler behoben)
- [x] Deep Code Review: Workshop integer division fix, Worker GUID fix, SaveGameService atomic write + backup, GameStateService thread safety, DailyRewardService local time, MiniGame race conditions (_isEnding), SawingGameVM localization, Dialog overlays (4 Dialoge), Icons.cs Duplikate, AchievementService IDisposable, 20+ neue .resx Keys

### Phase 6b: BomberBlast ✓
- [x] 3 Projekte: BomberBlast.Shared, BomberBlast.Desktop, BomberBlast.Android
- [x] Core Game Engine: GameEngine, GameState, GameTimer, SoundManager
- [x] AI: EnemyAI + A* Pathfinding
- [x] Graphics: GameRenderer (SkiaSharp), SpriteSheet
- [x] Input System: FloatingJoystick, SwipeGesture, DPad + InputManager
- [x] Models: 14 Dateien (Entities, Grid, Levels - 50 Level + Arcade)
- [x] Services: Progress, HighScore, ISoundService
- [x] ViewModels: 9 VMs (Main, Game 60fps loop, Menu, LevelSelect, Settings, HighScores, GameOver, Pause, Help)
- [x] Views: 9 AXAML Views (inkl. GameView mit SKCanvasView, HelpView)
- [x] 6 Sprachen (.resx), Android (Landscape-only)
- [x] Build: Shared + Desktop + Android 0 Fehler
- [x] Deep Code Review: Navigation-Bugs gefixt, InvalidateCanvas verdrahtet, GC-Optimierung, HelpView erstellt

### Phase 6c: AdMob Integration ✓
- [x] AdMobHelper.cs: Native Android Banner-Ad als FrameLayout-Overlay (linked file pattern)
- [x] UMP GDPR Consent: `Xamarin.Google.UserMesssagingPlatform` (Namespace-Typo: dreifaches 's')
- [x] AdConfig.cs: Echte Ad-Unit-IDs fuer alle 6 Apps (2 Publisher-Accounts)
- [x] 6 AndroidManifest.xml: APPLICATION_ID Meta-Data
- [x] 6 Android .csproj: AdMob + UMP PackageReferences + Linked AdMobHelper
- [x] 6 MainActivity.cs: AdMob Init + Consent + Banner mit tabBarHeightDp
- [x] D8 Duplicate Class Fix: Compose.Runtime.Annotation.Jvm ExcludeAssets
- [x] Ad-Platzierung direkt UEBER Tab-Bar (FrameLayout Overlay mit BottomMargin)
- [x] Build: 0 Fehler

### Phase 7: Polish + Testing
- [ ] Android APKs auf Geraet testen
- [ ] Desktop Apps manuell testen

---

## Changelog

### 05.02.2026
- Projekt initialisiert
- Infrastruktur erstellt (Directory.Build.props, Directory.Packages.props)
- MeineApps.CalcLib 1:1 kopiert
- MeineApps.Core.Ava erstellt (4 Themes, Services, Converters, Behaviors)
- MeineApps.UI erstellt (Card, FAB, Button/Text/Input Styles)
- RechnerPlus Migration gestartet
- RechnerPlus Desktop Build erfolgreich
- RechnerPlus Desktop App startet und funktioniert
- RechnerPlus Phase 2 abgeschlossen:
  - ConverterViewModel: Temperature-Fix (Offset-basierte Konvertierung), 8 Kategorien, vollständige Lokalisierung
  - CalculatorViewModel: Lokalisierte Fehlermeldungen + Modus-Labels
  - ThemeService: Dynamisches Theme-Switching korrekt implementiert (kein statisches Theme in App.axaml)
  - MainView: Tab-Fade-Transitions (Border.TabContent + .Active CSS-Klassen, 150ms Opacity)
  - Keyboard-Support: Ziffern, Operatoren, Enter/=, Backspace, Delete, Escape, Dezimalpunkt
  - Alle .resx Dateien komplett (6 Sprachen, 80+ Keys)
  - Full Solution Build: 0 Fehler, 0 Warnungen
- ZeitManager Phase 3 abgeschlossen:
  - 3 Projekte: ZeitManager.Shared, ZeitManager.Desktop, ZeitManager.Android
  - 13 Models: TimerItem, AlarmItem, ShiftSchedule, ShiftException, MathChallenge, StopwatchLap + Enums
  - Services: IDatabaseService + DatabaseService (SQLite), ITimerService + TimerService
  - ViewModels: MainVM (4 Tabs), TimerVM (Multi-Timer, Quick-Timer), StopwatchVM (Laps, Undo), AlarmVM (CRUD, Editor), SettingsVM
  - Views: TimerView (Chips + Dialog), StopwatchView (Circular Ring), AlarmView (Editor Overlay), SettingsView (Themes + Languages)
  - 6 Sprachen, neue Keys: ThemeMidnight/Aurora/Daylight/Forest, FeedbackButton, PrivacyPolicy
  - Full Solution Build: 0 Fehler, 0 Warnungen (9 Projekte)
- MeineApps.Core.Premium.Ava Phase 4a erstellt:
  - Services: IAdService, IPurchaseService, ITrialService, AdConfig, AdMobService, PurchaseService, TrialService
  - Controls: AdBannerView (Avalonia UserControl)
  - Extensions: ServiceCollectionExtensions (AddMeineAppsPremium)
  - Nutzt IPreferencesService statt MAUI Preferences.Default
  - PurchaseService mit virtual Methods fuer plattformspezifische Billing-Integration
  - Build: 0 Fehler, 0 Warnungen (10 Projekte)
- RechnerPlus Design-Fix + History + Theme-Preview:
  - Calculator-Buttons: CalcButton-Klasse (MinHeight=52, CornerRadius=12), FontSize 24
  - History Bottom-Sheet: IHistoryService DI, CalculatorViewModel History-Commands, MainView Overlay mit Slide-Animation
  - Swipe-Gesten: Up=ShowHistory, Down=HideHistory (nur Calculator-Tab)
  - Theme-Preview: Neutrale Hintergruende pro Theme, farbiger Border + CheckCircle fuer ausgewaehlt
  - Full Solution Build: 0 Fehler, 0 Warnungen
- ZeitManager Bugfixes + Feature-Erweiterungen:
  - Timer-Bug gefixt: PropertyChanged fuer RemainingTimeFormatted/IsRunning + Dispatcher.UIThread.Post()
  - Theme-Farben: TextBrush→TextPrimaryBrush in allen Views
  - AudioService: 6 eingebaute Toene (Console.Beep / BEL Fallback), Loop-Support
  - Snooze: Timer (SnoozeTimerAsync) + Wecker (MaxSnoozeCount, CurrentSnoozeCount)
  - Tonauswahl: Wecker-Editor (ComboBox + Play) + Timer-Sound in Settings
  - Fullscreen AlarmOverlay: Pulsier-Animation, Dismiss + Snooze, Timer/Alarm Events
  - Schichtplan: 15-Schicht (3 Gruppen Mo-Fr) + 21-Schicht (5 Gruppen 24/7), Kalender-Grid, Editor
  - AlarmView: Toggle-Bar Wecker|Schichtplan, embedded ShiftScheduleView
  - Desktop Notifications: Windows Toast (PowerShell), Linux (notify-send)
  - Android Services: TimerForegroundService, AlarmReceiver, BootReceiver, AndroidNotificationService
  - AndroidManifest: FOREGROUND_SERVICE, SCHEDULE_EXACT_ALARM, POST_NOTIFICATIONS, etc.
  - Build: Desktop 0 Fehler/0 Warnungen, Android 0 Fehler/11 CA1416 Warnungen (erwartet)
- **Material.Icons.Avalonia Fix:** `<materialIcons:MaterialIconStyles />` in App.axaml registriert (RechnerPlus, ZeitManager, FinanzRechner) - ohne diese Registrierung werden Icons nicht gerendert
- **Timer-Bug gefixt:** RemainingAtStartTicks Snapshot verhindert kumulative Drift bei Timer-Ticks
- **Timer-Anzeige:** Immer HH:MM:SS Format (statt MM:SS das wie HH:MM aussah)
- **WheelPicker Control:** Drum-Style Swipe-Picker in MeineApps.UI/Controls/ fuer Timer-Erstellung + Wecker-Editor
- **RechnerPlus Fixes:** Button-Content zentriert (HorizontalContentAlignment/VerticalContentAlignment), History-Swipe ueber Buttons (Tunnel-Routing), History-Panel hoeher (MaxHeight 500)
- FinanzRechner Phase 4b abgeschlossen:
  - 3 Projekte: FinanzRechner.Shared, FinanzRechner.Desktop, FinanzRechner.Android
  - Models: Expense, Budget (BudgetStatus record), RecurringTransaction, FinanceEngine
  - Services: IExpenseService + ExpenseService (SQLite), IExportService + ExportService (CSV/PDF), INotificationService
  - Converters: 14 Converter (CategoryToIcon, BoolToResourceColor, AlertLevelToColor, TransactionType*, BalanceToColor, etc.)
  - ViewModels: MainVM (4 Tabs), ExpenseTrackerVM (CRUD, Filter, Sort, Undo), StatisticsVM (Charts, Export), SettingsVM
  - ViewModels Calculators: CompoundInterest, SavingsPlan, Loan, Amortization, Yield
  - ViewModels Sub: BudgetsVM, RecurringTransactionsVM
  - Views: HomeView (Dashboard), ExpenseTrackerView, StatisticsView, SettingsView, BudgetsView, RecurringTransactionsView
  - Views Calculators: CompoundInterest, SavingsPlan, Loan, Amortization, Yield
  - Fixes: TryFindResource→TryGetResource, Async Command-Suffix entfernt, Grid.Padding→Margin, Button.Text→Content
  - Ancestor-Binding fuer Commands in DataTemplates (ToggleActiveCommand)
  - FAB-Buttons von Border zu Button mit Command-Binding konvertiert
  - Desktop Build: 0 Fehler (nur NuGet SixLabors.ImageSharp Warnungen via PdfSharpCore)

### 06.02.2026
- HandwerkerRechner Phase 5 abgeschlossen:
  - 3 Projekte: HandwerkerRechner.Shared, HandwerkerRechner.Desktop, HandwerkerRechner.Android
  - Code-Review und Bugfixes aller ViewModels:
    - Android Build gefixt: Resources/ Verzeichnis (mipmap Icons, styles.xml) fehlte komplett
    - SaveProject in allen 9 Calculator-VMs komplett neu: Dialog-Pattern (ShowSaveDialog, ConfirmSaveProject, CancelSaveProject)
    - 5 Premium VMs: Kaputte SaveProject-Methode (name=null → sofort return) durch funktionierendes Dialog-Pattern ersetzt
    - Debug.WriteLine durch MessageRequested Events ersetzt (alle VMs)
    - Button-Klassen in 5 Premium Views gefixt: "Outline"→"Outlined", "Accent"→"Secondary"
    - MetalViewModel: Hardcodierte deutsche Metall- und Profil-Listen lokalisiert
    - RoofSolarViewModel: Hardcodierte deutsche Orientierungen lokalisiert
    - DrywallViewModel: Hardcodierte deutsche Ergebnis-Labels lokalisiert
    - SettingsViewModel: Hardcodierte Version "v2.0.0" durch dynamische Assembly-Version ersetzt
    - ProjectsViewModel: Delete-Bestaetigung hinzugefuegt (ShowDeleteConfirmation, ConfirmDeleteProject)
    - 22 neue Lokalisierungs-Keys in allen 6 Sprachen (Metalle, Profile, Orientierungen, Einheiten)
  - Build: Desktop 0 Fehler/0 Warnungen, Android 0 Fehler/0 Warnungen
- FitnessRechner Phase 6 Bugfixes:
  - Solution-Struktur korrigiert: Falsche dotnet sln add Ordnerstruktur (src/Apps/FitnessRechner) durch korrekte _FitnessRechner unter Apps ersetzt
  - App.axaml.cs: ThemeService wird beim Start aufgeloest (gespeichertes Theme wird angewendet)
  - MainViewModel: Child-VMs (SettingsVM, ProgressVM, FoodSearchVM) per Constructor Injection statt nullable [ObservableProperty]
  - MainViewModel: IThemeService im Constructor damit DI ihn auflöst + MessageRequested Event
  - BmiViewModel: ILocalizationService injiziert, 8 BMI-Kategorien lokalisiert, Debug.WriteLine→MessageRequested
  - BodyFatViewModel: ILocalizationService injiziert, 5 BodyFat-Kategorien lokalisiert, Debug.WriteLine→MessageRequested
  - CaloriesViewModel: ILocalizationService injiziert, Debug.WriteLine→MessageRequested (SetAsCalorieGoal, SetSpecificCalorieGoal)
  - WaterViewModel: ILocalizationService injiziert, Debug.WriteLine→MessageRequested (SaveWaterGoal)
  - SettingsViewModel: Debug.WriteLine→MessageRequested (PurchasePremium, RestorePurchases), dynamische Version
  - 23 neue Lokalisierungs-Keys in 6 Sprachen (BMI-Kategorien, BodyFat-Kategorien, Alert-Messages)
  - Build: 0 Fehler (56 Warnungen nur SixLabors.ImageSharp via FinanzRechner)
- FitnessRechner Calculator-Navigation:
  - Calculator Overlay via DataTemplates (CurrentPage + CurrentCalculatorVm in MainViewModel)
  - Tab-Wechsel schliesst offenen Calculator automatisch (CurrentPage = null)
  - ProgressViewModel.OnAppearingAsync() wird beim Tab-Wechsel aufgerufen (Charts laden)
  - NavigationRequested Event aus MainViewModel entfernt (nicht mehr benoetigt)
- FinanzRechner Bugfixes:
  - Sort/Filter ComboBoxes: ItemTemplate mit Convertern (zeigten rohe Enum-Werte statt lokalisierte Strings)
  - Duplizierte SortOption/FilterTypeOption Enums aus Converter-Dateien entfernt (nutzen jetzt VM-Enums via using static)
  - Budgets/RecurringTransactions Navigation: MainVM haelt BudgetsVM + RecurringTransactionsVM, Sub-Page Overlay (ZIndex=60)
  - ExpenseTrackerVM.NavigationRequested verdrahtet fuer BudgetsPage/RecurringTransactionsPage
- WorkTimePro + HandwerkerImperium in Solution eingebunden (3 Projekte je App, Solution Folders _WorkTimePro/_HandwerkerImperium)
- WorkTimePro AVLN2000 Compiled Binding Fixes (alle ~80+ Fehler behoben, 0 Errors):
  - MainViewModel: Tab-Navigation (CurrentTab, IsTodayActive etc., SelectXxxTabCommand), StatusIconKind, IsWorking, HasCheckedIn
  - SettingsViewModel: Theme-Selection (IsMidnightSelected etc., SelectThemeCommand), Language-Selection, MorningReminderTimeDisplay, EveningReminderTimeDisplay, dynamische AppVersion
  - CalendarViewModel: CalendarWeeks (gruppierte Kalender-Tage fuer nested ItemsControl)
  - DayDetailViewModel: StatusIconKind, HasWarnings, HasNoTimeEntries, HasNoPauseEntries
  - WeekOverviewViewModel: HasVacationDays, HasSickDays
  - MonthOverviewViewModel: GoBackCommand
  - YearOverviewViewModel: GoBackCommand, AverageHoursPerDayDisplay, VacationDaysTakenDisplay, SickDaysDisplay
  - StatisticsViewModel: IsWeekSelected/IsMonthSelected/IsQuarterSelected/IsYearSelected, HasPauseChartData, HasNoTableData
  - ShiftPlanViewModel: GoBackCommand, HasNoPatterns, PatternStartTimeDisplay, PatternEndTimeDisplay, SelectPatternCommand
  - ShiftDayItem: TodayBorderThickness (Avalonia.Thickness), DayOpacity
  - VacationViewModel: GoBackCommand, CalculatedDaysDisplay, HasNoVacations, VacationTypeItem Klasse (statt ValueTuple)
  - TimeEntry Model: TypeIconKind (MaterialIconKind), HasNote
  - VacationEntry Model: TypeIconKind (MaterialIconKind), HasNote
  - WorkDay Model: DayName, DateShortDisplay
- HandwerkerImperium AVLN2000 Compiled Binding Fixes (65 Fehler behoben, 0 Errors)
- BomberBlast Phase 6b abgeschlossen:
  - 3 Projekte: BomberBlast.Shared, BomberBlast.Desktop, BomberBlast.Android
  - Core: GameEngine, GameState, GameTimer, SoundManager
  - AI: EnemyAI + A* Pathfinding
  - Graphics: GameRenderer (SkiaSharp), SpriteSheet
  - Input: FloatingJoystick, SwipeGesture, DPad, InputManager
  - Models: 14 Dateien (Entities, Grid 11x9, Levels 50+Arcade)
  - Services: Progress, HighScore, ISoundService
  - ViewModels: 9 VMs (Game mit 60fps DispatcherTimer Loop)
  - Views: 8 AXAML (GameView mit SKCanvasView)
  - 6 Sprachen, Android Landscape-only
  - Build: Shared + Desktop + Android 0 Fehler
- HandwerkerImperium Deep Code Review:
  - Workshop.cs: Integer division fix (BaseIncomePerWorker)
  - Worker.cs: GUID regeneration fix + international names
  - SaveGameService: Atomic write (temp+rename), backup, SemaphoreSlim, corruption recovery
  - GameStateService: Full thread safety (locks auf alle mutierenden Operationen)
  - DailyRewardService: UTC → Local time fuer Tagesgrenzen
  - AchievementService: IDisposable mit Event-Unsubscription
  - 4 MiniGame VMs: _isEnding Race Condition Guard
  - SawingGameVM: ILocalizationService, hardcoded German strings lokalisiert
  - MainViewModel: Dialog overlay state + dismiss commands, Debug.WriteLine entfernt
  - MainView.axaml: 4 Dialog-Overlays (OfflineEarnings, LevelUp, DailyReward, AchievementUnlocked)
  - Icons.cs: Roofer/CurrencyEur Duplikate gefixt
  - AsyncExtensions: Trace statt Debug, StackTrace logging
  - MoneyFormatter: Inkonsistenter Threshold gefixt
  - 20+ neue .resx Keys in 6 Sprachen
  - Build: Shared + Desktop 0 Fehler, 0 Warnungen

- **Code-Review RechnerPlus + Cross-App Fixes (06.02.2026):**
  - RechnerPlus: AppVersion dynamisch aus Assembly (statt hardcoded "2.0.0")
  - RechnerPlus: ThemeService explizit beim Start aufgeloest (gespeichertes Theme vor Window-Erstellung)
  - ZeitManager: Gleiche 2 Fixes (AppVersion + ThemeService Init)
  - FinanzRechner: Gleiche 2 Fixes (AppVersion + ThemeService Init)
  - BomberBlast: Fehlende `using System.Diagnostics` in SettingsViewModel
  - WorkTimePro: 8 fehlende Chart-Keys im Designer.cs (ChartHours, ChartWorkHours, etc.)
  - Full Solution Build: 0 Fehler
- **Code-Review BomberBlast (06.02.2026):**
  - 12x Debug.WriteLine entfernt (GameViewModel 1, SettingsViewModel 11)
  - GameEngine: Alle Overlay-Strings lokalisiert via ILocalizationService (Wave, Stage, Paused, TapToResume, LevelComplete, Score, TimeBonus, GameOver, FinalScore, WaveReached, Level)
  - GameOverViewModel: "Wave/Level" Strings lokalisiert (ILocalizationService injiziert)
  - DI Fix: GameEngine + SpriteSheet in App.axaml.cs registriert (fehlten)
  - SettingsViewModel: ShowAlert-Fallback durch Null-Conditional ersetzt
  - Pre-existing Fixes: HandwerkerImperium using System.Diagnostics, FitnessRechner GoalReached/Warning/HighCalorieWarning Designer.cs
  - Full Solution Build: 0 Fehler
- **Code-Review FinanzRechner (06.02.2026):**
  - 5 Calculator VMs: Hardcodierte deutsche Chart-Labels lokalisiert (Kapital, Einzahlungen, Tilgung, Zinsen, Restschuld, Anfangswert, Endwert, Jahre, Monate)
  - MainViewModel: Tab-Wechsel schliesst jetzt Calculator + SubPage Overlays
  - ExpenseTrackerViewModel: Hardcodiertes "von" durch FilteredCountFormat ersetzt, dead code entfernt (GetCategoryName/GetCategoryIcon)
  - Alle Calculator VMs + ExpenseTrackerVM: "EUR" durch Unicode-Euro-Zeichen ersetzt
  - 10 neue Lokalisierungs-Keys in 6 Sprachen (ChartCapital, ChartDeposits, ChartRepayment, ChartInterest, ChartRemainingDebt, ChartInitialValue, ChartFinalValue, ChartYears, ChartMonths, FilteredCountFormat)
  - FitnessRechner: AppStrings.Designer.cs komplett neu generiert (CS0102 Duplikat-Fehler behoben)
  - Full Solution Build: 0 Fehler
- **Deep Review RechnerPlus (06.02.2026):**
  - CalculatorVM: FormatResult lokalisiert (non-static, _localization.GetString("Error") statt hardcoded "Error")
  - CalculatorVM: SelectHistoryEntry ruft jetzt ClearError() auf (HasError wird zurueckgesetzt)
  - CalculatorVM: Tan() Validation - Ergebnis > 1e15 wird als undefiniert erkannt (tan(90°) → Fehler)
  - ConverterVM: FormatResult lokalisiert (gleicher Fix wie CalculatorVM)
  - ConverterVM: km/h Faktor von 0.277778 auf 1.0/3.6 (exaktere Berechnung)
  - MainView.axaml.cs: NullRef Fix in OnHistoryBackdropTapped (vm?.CalculatorViewModel?.HideHistoryCommand)
  - CalculatorView.axaml + .cs: Alle Operatoren auf Unicode normalisiert (×, ÷, −) fuer konsistente Expression-Anzeige
  - Build: 0 Fehler, 0 Warnungen
- **Deep Review BomberBlast (06.02.2026):**
  - MainViewModel: 6 kritische Navigation-Bugs gefixt:
    - GameOver-Parameter nie geparst (Score/Level immer 0) → SetParameters() wird jetzt aufgerufen
    - Game-Loop nie gestartet (OnAppearingAsync fehlte) → wird jetzt aufgerufen
    - Compound-Routes ("//MainMenu/Game?mode=arcade") fielen in default → korrekt geparst
    - OnDisappearing fehlte beim Verlassen des Games → Lifecycle korrekt
    - Settings→Back ging zu MainMenu statt zurueck zum Game → _returnToGameFromSettings
    - IsHelpActive + Help-Case fehlte komplett → hinzugefuegt
  - GameViewModel: SetParameters setzt _isInitialized=false (TryAgain startet neues Spiel)
  - GameView.axaml.cs: InvalidateCanvasRequested Event verdrahtet (Canvas hat NIE gerendert!)
  - HelpView.axaml erstellt (fehlte komplett trotz HelpViewModel): Lokalisierte Hilfe mit 5 Sektionen
  - GameRenderer: 6 per-frame GC-Allokationen eliminiert + Memory Leak (fusePath ohne using) gefixt
  - Build: Shared + Desktop + Android jeweils 0 Fehler, 0 Warnungen
- **Deep Review FitnessRechner (06.02.2026):**
  - Preferences Key-Mismatch gefixt: WaterVM "WaterGoal"→"daily_water_goal", CaloriesVM "CalorieGoal"→"daily_calorie_goal" (kritischer Bug: Calculator-Goals kamen nie im Tracking an)
  - DateTime.Today statt .Now: BmiVM (2x) + BodyFatVM (1x) fuer korrekte datumsbasierte Gruppierung
  - TrackingViewModel: IDisposable implementiert (CancellationTokenSource Cleanup mit Dispose-Pattern)
  - HistoryViewModel: "Avg"/"Min"/"Max" hardcoded → AppStrings.Average/Min/Max (6 Stellen)
  - ProgressVM: CaloriesRemaining.Replace("kcal","ml") Hack → eigener AppStrings.WaterRemaining Key
  - IdealWeightViewModel: MessageRequested Event hinzugefuegt (Konsistenz)
  - BarcodeLookupService: SemaphoreSlim Thread-Safety, 8x Debug.WriteLine entfernt, "Unbekanntes Produkt"→AppStrings.UnknownProduct
  - 8x catch (Exception ex)→catch (Exception) (unused variable warnings)
  - 2 neue resx-Keys in 6 Sprachen: WaterRemaining, UnknownProduct
  - Build: Shared + Desktop + Android jeweils 0 Fehler, 0 Warnungen
- **Deep Review FinanzRechner (06.02.2026):**
  - 6 ViewModels (Main, ExpenseTracker, Statistics, Settings, Budgets, RecurringTransactions): Debug.WriteLine→MessageRequested Events
  - ExpenseService: 4x Debug.WriteLine entfernt, NotificationService: 1x Debug.WriteLine entfernt
  - ExpenseTrackerViewModel: 2 Debug-Marker ("=== ShowBudgets CALLED ===") entfernt
  - MainViewModel.OnLanguageChanged(): BudgetsVM + RecurringTransactionsVM UpdateLocalizedTexts() hinzugefuegt
  - BudgetsViewModel: UpdateLocalizedTexts() mit 16 Properties
  - RecurringTransactionsViewModel: UpdateLocalizedTexts() mit 18 Properties
  - 5 Calculator VMs: Ungenutztes `using System.Diagnostics` entfernt
  - CategoryLocalizationHelper: Freelance-Kategorie ES/FR/IT/PT Uebersetzungen hinzugefuegt
  - Build: 0 Fehler
- **Deep Review WorkTimePro (06.02.2026):**
  - Services: 25 Debug.WriteLine entfernt (BackupService 10, CalendarSyncService 9, ExportService 6)
  - Services: `using System.Diagnostics` entfernt (BackupService, CalendarSyncService)
  - Services: 13 `catch (Exception ex)` zu `catch (Exception)` (CS0168 behoben)
  - ExportService.GetStatusText(): 12 deutsche DayStatus-Strings lokalisiert
  - CalendarSyncService: FormatEventTitle/FormatEventDescription/GetStatusName/ExportVacation komplett lokalisiert
  - CalculationService: "Automatically added (legal requirement)" -> AppStrings.AutoPauseLegal
  - 9 ViewModels: ~40 hardcodierte deutsche/englische Strings durch AppStrings ersetzt
  - ShiftPlanViewModel: 6 Fehlerstrings lokalisiert
  - 5 Placeholder-Meldungen ("implement platform-specific dialog") -> TODO-Kommentare
  - 7 Debug-Meldungen entfernt (DeleteEntry/Pause, LockMonth, Export, DeleteVacation)
  - StatisticsView.axaml: Hardcodierte Farbe #7B1FA2 -> DynamicResource PrimaryBrush
  - AppStrings.resx: Fehlender RemainingDays Key ergaenzt (existierte nur in Sprachdateien)
  - 8 neue Lokalisierungs-Keys + PdfExportPremium/ExportFailedMessage/RemainingDays in Designer.cs
  - Build: Shared + Desktop + Android 0 Fehler
- **Deep Review ZeitManager Round 2 (06.02.2026):**
  - Android: Runtime Permission-Checks (POST_NOTIFICATIONS, SCHEDULE_EXACT_ALARM), FullScreenIntent API 12+ Guard
  - Android: TimerForegroundService NotSticky, BootReceiver try-catch, Math.Abs() fuer Notification-IDs
  - MainViewModel: Snackbar-System + MessageRequested von allen Child-VMs verdrahtet
  - TimerService: Thread-safe _timers mit lock, Event-Unsubscribe in Dispose
  - AudioService: Lock-swap CTS Pattern, using var process (Linux)
  - DesktopNotificationService: ConcurrentDictionary, DateTime.UtcNow, EscapeXml/EscapeShell
  - TimerItem: ProgressPercent Math.Clamp(0, 100)
  - CustomShiftPattern: ShortName() lokalisiert (ShiftEarlyShort/LateShort/NightShort)
  - AlarmItem: NotifyLocalizationChanged() + AlarmVM ruft es bei Sprachwechsel auf
  - Build: Desktop 0 Fehler/0 Warnungen, Android 0 Fehler/11 CA1416 (erwartet)
- **App Icons + SplashScreen (06.02.2026):**
  - 7 einzigartige App-Icons programmatisch generiert (SkiaSharp): ZeitManager (Uhr), FinanzRechner (Euro+Trend), HandwerkerRechner (Lineal+Schraubenschluessel), FitnessRechner (Herz+Puls), WorkTimePro (Uhr+Haken), HandwerkerImperium (Burg+Flagge), BomberBlast (Bombe+Lunte)
  - Android mipmap Icons in 5 Dichten (mdpi-xxxhdpi) fuer alle 8 Apps, inkl. adaptive Icons (foreground/background/round)
  - Desktop Icons (256px) + Splash Screens (720x1280) fuer alle 8 Apps
  - SplashOverlay Control in MeineApps.UI: Wiederverwendbar, AppName + IconSource Properties, LoadingBar Animation, Auto-Fade (1.5s)
  - SplashOverlay in alle 8 MainWindow.axaml eingebaut
  - Window.Icon mit avares:// URI in allen 8 MainWindows gesetzt
  - AvaloniaResource Include="Assets\**" in allen 8 Shared csproj Dateien
  - Full Solution Build: 0 Fehler
- **Deep Review HandwerkerRechner Premium VMs (06.02.2026):**
  - 5 Premium VMs (Metal, Electrical, Garden, RoofSolar, Drywall): 15x Debug.WriteLine entfernt, 15x catch (Exception ex) zu catch (Exception)
  - Lokalisierung: Kabelkosten, Gesamtkosten (4x), Anlagenkosten, Amortisation/Jahre, h/Tag, Ohm'sches Gesetz, Gewinde, Spannweite/Hoehe, Dachflaeche, (doppelt)
  - 8 neue resx-Keys in 6 Sprachen + Designer.cs
  - Build: 0 Fehler
- **HandwerkerImperium MiniGame Visual Fixes (06.02.2026):**
  - PipePuzzle CRITICAL: `CheckIfSolved()` passed `Direction.Right` statt `Direction.Left` → Puzzle war NIEMALS loesbar
  - PipePuzzle: Unicode box-drawing chars (┃, ┏, ┣, ╋) durch Border-Segmente ersetzt (HasTop/Bottom/Left/RightOpening) → plattformunabhaengig, kein RotateTransform-Binding noetig
  - PipePuzzle: OnRotationChanged notifiziert Opening-Properties → Border-Segmente updaten bei Drehung
  - PipePuzzle + PaintingGame: WrapPanel Width constraint via PuzzleGridWidth/PaintGridWidth → korrekte Grid-Darstellung
  - PaintingGame: PaintCell.DisplayColor + IsPaintedCorrectly feuerten kein PropertyChanged bei IsPainted-Aenderung → bemalte Zellen zeigten weder Farbe noch Checkmark
  - WiringGame: Wire.BackgroundColor/ContentOpacity/BorderWidth als computed properties + PropertyChanged-Notifications fuer IsSelected/IsConnected/HasError → visuelles Feedback (Highlight, gruener Tint, Checkmark, gedimmte Opacity)
  - Full Solution Build: 0 Fehler
- **BomberBlast Grid-Erweiterung + Visuelles Redesign (06.02.2026):**
  - Grid: 11x9 → 15x10 (nutzt 16:9 Bildschirm besser aus, nur ~14% ungenutzt statt 47%)
  - HUD: Von oben nach rechts verschoben (120px Panel, vertikales Layout: TIME, SCORE, LIVES als Herz-Icons, BOMBS/FIRE, aktive PowerUps)
  - GameRenderer komplett umgebaut: Paletten-System (Classic HD + Neon/Cyberpunk), IGameStyleService per DI
  - Classic HD: Helle Bodenfliesen, 3D-Stein-Waende, Ziegel-Bloecke mit Moertel, gruene Tuer-Exit
  - Neon: Dunkle Flaechen mit Cyan-Gridlinien, leuchtende Kanten an Waenden, orange Glow-Risse in Bloecken, Neon-Glow Exit
  - Player: Abgerundeter Koerper mit Helm/Muetze, Augen mit Blickrichtung, Neon-Aura
  - Enemies: Ovaler Koerper, boese Augenbrauen, verschiedene Muender je Typ, Neon-Glow in Typ-Farbe
  - Bombs: Glanz-Highlight, gewellte Lunte mit Funken, Neon-Glow-Puls
  - Explosionen: 3-Schicht (Outer/Inner/Core), Neon: Cyan-Core + Blur
  - PowerUps: Runde Icons mit Symbol-Formen (statt Buchstaben), Neon-Glow-Aura
  - IGameStyleService + GameStyleService: Persistenz via IPreferencesService, StyleChanged Event
  - SettingsView: Visual Style Sektion mit Preview-Farbverlauf + RadioButtons
  - Slider-Texte Fix: Width="80" entfernt, Auto-Breite mit FontSize=11
  - 5 neue resx-Keys in 6 Sprachen (VisualStyle, StyleClassic/Desc, StyleNeon/Desc)
  - Build: Shared + Desktop + Android 0 Fehler

### 07.02.2026
- **AdMob Integration (Phase 6c):**
  - AdMobHelper.cs erstellt: Native Android Banner-Ad als FrameLayout-Overlay mit GDPR-Consent (UMP)
  - Linked-File-Pattern: AdMobHelper lebt in Premium-Library, wird per `<Compile Include>` in jedes Android-Projekt eingebunden
  - AdConfig.cs: Echte Ad-Unit-IDs fuer alle 6 werbe-unterstuetzten Apps (2 Publisher-Accounts)
  - 6 AndroidManifest.xml: `com.google.android.gms.ads.APPLICATION_ID` Meta-Data
  - 6 Android .csproj: `Xamarin.GooglePlayServices.Ads.Lite` + `Xamarin.Google.UserMessagingPlatform` Packages
  - 6 MainActivity.cs: AdMob Init + UMP Consent + Banner-Overlay mit app-spezifischem tabBarHeightDp
  - Ad-Platzierung: FrameLayout-Overlay mit `GravityFlags.Bottom` + `BottomMargin = tabBarHeightDp * density`, positioniert Banner direkt UEBER der Avalonia Tab-Bar
  - AdInsetListener: Passt BottomMargin fuer Navigation-Bar-Insets an (Edge-to-Edge Support)
  - D8 Duplicate Class Fix: `Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm` mit `ExcludeAssets="all"` in Directory.Build.targets
  - UMP Namespace-Typo: C# Namespace ist `Xamarin.Google.UserMesssagingPlatform` (dreifaches 's')
  - `AdView.AdSize` ist Property-Setter (nicht `SetAdSize()` Methode) in neueren Ads.Lite Bindings
  - BomberBlast SettingsView: Broken `BoolConverters.TrueIsVisible` fuer `BorderThickness` entfernt (AVLN2000)
  - Full Solution Build: 0 Fehler
