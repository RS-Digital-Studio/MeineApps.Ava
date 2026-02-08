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

# AppChecker - Alle 8 Apps pruefen
dotnet run --project tools/AppChecker

# AppChecker - Einzelne App pruefen
dotnet run --project tools/AppChecker RechnerPlus
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
├── tools/
│   ├── AppChecker/              # Automatisches Pruef-Tool (6 Check-Kategorien)
│   └── StoreAssetGenerator/     # Store-Assets generieren (SkiaSharp)
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
| HandwerkerImperium | v2.0.2 | Release bereit (AAB+APK+Store Assets) |
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

### Phase 6d: Rewarded Ads Integration ✓
- [x] Zentrale IRewardedAdService + RewardedAdService in Premium Library (Desktop-Simulator)
- [x] RewardedAdHelper.cs + AndroidRewardedAdService.cs (Linked Files fuer Android)
- [x] AdConfig.cs: Rewarded Ad-Unit-IDs (6 Apps, Test-ID als Fallback)
- [x] BomberBlast: Power-Up Boost (Level >= 20) + Level-Skip (3x Game Over)
- [x] FinanzRechner: PDF-Export hinter Rewarded Ad fuer Free User
- [x] HandwerkerRechner: 5 Premium-Rechner mit 30-Min Ad-Zugang (IPremiumAccessService)
- [x] FitnessRechner: Barcode-Scan-Limit (3/Tag, +5 per Ad via IScanLimitService)
- [x] WorkTimePro: "Video ODER Premium" Soft Paywall (Vacation, Export, Statistics)
- [x] HandwerkerImperium + BomberBlast: Lokale IRewardedAdService geloescht → zentrale Library
- [x] 6 Android-Projekte: Linked Files + RewardedAdHelper Lifecycle + DI Override
- [x] ~22 neue Lokalisierungs-Keys x 6 Sprachen (132 Uebersetzungen)
- [x] Full Solution Build: 0 Fehler, 112 Warnungen (nur NuGet)
- [x] Multi-Placement Architektur: AdConfig.cs mit ALLEN 17 Rewarded Ad-Unit-IDs, ShowAdAsync(placement) Overload
- [x] BomberBlast: Score-Verdopplung nach Level-Complete (Placement "score_double")
- [x] FinanzRechner: CSV-Export (Placement "export_csv"), Budget-Analyse (Placement "budget_analysis"), Extended Stats 24h (Placement "extended_stats")
- [x] HandwerkerRechner: Material-PDF (Placement "material_pdf"), Projekt-Export (Placement "project_export"), Extended History 24h (Placement "extended_history")
- [x] FitnessRechner: Wochenanalyse (Placement "detail_analysis"), Tracking-Export CSV (Placement "tracking_export"), Extended Food-DB 24h (Placement "extended_food_db")
- [x] WorkTimePro: Placement-Strings ("vacation_entry", "export", "monthly_stats") + Extended Stats Gate (24h)
- [x] Full Solution Build nach Multi-Placement: 0 Fehler, 171 Warnungen (nur NuGet)

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
- **WorkTimePro: Kalender-Tag-Auswahl mit Status-Eintrag:**
  - CalendarViewModel: Overlay-System statt NavigationRequested (IVacationService injiziert)
  - CalendarDay: StatusIconKind (MaterialIconKind) + HasStatusIcon fuer 10 DayStatus-Werte
  - CalendarView.axaml: Status-Overlay (Typ-ComboBox, DatePicker Von/Bis, Notiz, Save/Remove/Cancel)
  - StatusIcon: mi:MaterialIcon (12x12) statt FontSize=8 TextBlock-Emoji
  - 2 neue resx-Keys (SetStatus, DateRange) in 6 Sprachen + WorkDaysFormat in Designer.cs
  - Full Solution Build: 0 Fehler
- **HandwerkerImperium Release v2.0.2 (07.02.2026):**
  - Version bump: ApplicationVersion 6→7, ApplicationDisplayVersion 2.0.1→2.0.2
  - StoreAssetGenerator: Console-Tool unter tools/StoreAssetGenerator/ (SkiaSharp, NICHT in Solution)
  - Hi-Res Icon 512x512: Lila Gradient, weisse Burg, Flagge, abgerundete Ecken
  - Feature Graphic 1024x500: App-Icon + Titel + Workshop-Icons
  - 6 Phone Screenshots 1080x2340: Dashboard, Saegen, Rohr-Puzzle, Shop, Achievements, Statistiken
  - 4 Tablet Screenshots 1200x1920: Dashboard, Saegen, Shop, Achievements
  - Alle Assets in Releases/HandwerkerImperium/
  - AAB + APK in Releases/HandwerkerImperium-v2.0.2.{aab,apk}
  - Full Solution Build: 0 Fehler
- **AppChecker Tool (07.02.2026):**
  - Console-Tool unter tools/AppChecker/ (keine externen Dependencies, nur BCL)
  - 10 Check-Kategorien: Projekt/Build, Android, Avalonia/UI, Lokalisierung, Code Quality, Assets, DI-Registrierung, VM-Verdrahtung, View-Bindings, Navigation
  - Interaktiver Modus (ohne CLI-Argument): Auswahl-Dialog mit 0=Alle, 1-8=einzeln, Komma-getrennt
  - CLI-Modus weiterhin: `dotnet run --project tools/AppChecker RechnerPlus`
  - Farbige Ausgabe (PASS=Gruen, INFO=Cyan, WARN=Gelb, FAIL=Rot)
  - Exit Codes: 0=ok, 1=Warnings, 2=Failures
  - Checks 1-6: fehlende MaterialIconStyles, Debug.WriteLine Reste, ungenutztes ex, InvalidateVisual statt InvalidateSurface, DateTime.Parse ohne RoundtripKind, fehlende Lokalisierungs-Keys, AdMob-Konfiguration
  - Check 7 (DI): ConfigureServices, PreferencesService AppName, IThemeService, ILocalizationService, AddMeineAppsPremium, MainVM + Cross-Check Constructor-VMs vs DI
  - Check 8 (VM-Verdrahtung): IsXxxActive/IsXxxTab Properties, SelectedTab, NavigateTo Commands, LanguageChanged, UpdateLocalizedTexts Cross-Check, MessageRequested, Overlay-Schliessung, GoBackAction/NavigationRequested
  - Check 9 (View-Bindings): x:DataType, xmlns:vm, View↔ViewModel Paar-Check, StaticResource vs DynamicResource fuer Brushes
  - Check 10 (Navigation): Tab-Buttons mit Commands, Tab-Count Cross-Check (VM vs View), ZIndex Overlays, Ad-Spacer, Calculator-Overlay Verdrahtung, Screen-basierte Navigation Erkennung
  - Erster Lauf: 376 PASS, 28 INFO, 49 WARN, 1 FAIL (7 Apps ohne HandwerkerImperium)
- **AppChecker Fixes (07.02.2026):**
  - ZeitManager: INTERNET Permission + android:icon + android:roundIcon in AndroidManifest.xml
  - BomberBlast: 2x Debug.WriteLine entfernt (HighScoreService, ProgressService) + android:roundIcon
  - FitnessRechner: 24x Debug.WriteLine entfernt (App.axaml.cs 9, FoodSearchService 7, TrackingService 3, VersionedDataService 2, MainActivity 3)
  - FinanzRechner: 10x catch (Exception ex) → catch (Exception) (ExpenseService 4, BudgetsVM 2, ExpenseTrackerVM 1, RecurringTransactionsVM 3)
  - FitnessRechner MainVM: LanguageChanged abonniert (12 Properties aktualisieren)
  - WorkTimePro MainVM: LanguageChanged abonniert + Unsubscribe in Dispose
  - BomberBlast MainVM: ILocalizationService injiziert, LanguageChanged → MenuVm.OnAppearing()
  - HandwerkerRechner MainVM: UpdateLocalizedTexts() Aufruf in OnLanguageChanged ergaenzt
  - HandwerkerRechner: ~440 fehlende Uebersetzungs-Keys in FR/IT/PT ergaenzt (140 FR + 140 IT + 159 PT)
  - AppChecker nach Fixes: 390 PASS, 28 INFO, 4 WARN, 0 FAIL (4 WARNs sind false-positives: HomeView/TodayView ohne eigenem VM, BomberBlast Ad-Spacer)
- **HandwerkerImperium 2.0 Redesign - Alle 12 Phasen abgeschlossen (07.02.2026):**
  - Phase 1-10: Enums, Worker-System, Wirtschaft, Auftraege, Gebaeude, Forschung, Prestige, Events, Offline, Visuals
  - Phase 11 Achievements: 30 neue Achievements (58 total) in 7 Kategorien (Workers, Buildings, Research, Reputation, Prestige, Money, Workshops)
  - Phase 11 Lokalisierung: 160+ neue resx-Keys in 6 Sprachen (Worker-System, Tiers, Personalities, Buildings, Research, Events, Prestige, Achievements)
  - Phase 12 DI: 4 neue Services (IWorkerService, IBuildingService, IResearchService, IEventService) + 4 neue VMs in App.axaml.cs registriert
  - Phase 12 MainViewModel: 14 child VMs (4 neue), 7 Tabs (Home, Workers, Research, Stats, Achievements, Shop, Settings), 4 neue Overlay-States
  - Phase 12 MainView: 4 neue Views eingebunden, Tab-Bar von 5 auf 7 erweitert
  - Phase 12 AVLN2000 Fixes: BoolConverters.FalseIsVisible→DisplayOpacity, Extension Method Bindings→computed Properties
  - Lokalisierungs-Key-Mismatch Fix: 4 Enum-Extensions angepasst (BuildingType, WorkerTier, WorkerPersonality, ResearchBranch)
  - 10 zusaetzliche resx-Keys (7 BuildingEffect + 3 BranchDesc) in 6 Sprachen
  - Full Solution Build: 0 Fehler, 112 Warnungen (nur bekannte NuGet-Warnungen)
- **Performance-Fixes (07.02.2026):**
  - PreferencesService: Debounced Save (500ms Timer statt synchronem Save bei jedem Set), IDisposable fuer Timer-Cleanup, statisch gecachte JsonSerializerOptions
  - ExpenseService: O(n^2) Import-Fix mit HashSet fuer existierende IDs (Expenses + RecurringTransactions)
  - ExpenseService: GetAllBudgetStatusAsync N+1 Fix (Monatsausgaben einmal laden statt pro Budget)
  - ExpenseService: ProcessDueRecurringTransactions Race-Condition Fix (Batch-Add statt einzelne AddExpenseAsync)
  - ExpenseService: Statisch gecachte JsonSerializerOptions (4 Save-Methoden + Export)
  - Build: Core.Ava + FinanzRechner.Shared 0 Fehler
- **Performance-Fixes Round 2 (07.02.2026):**
  - HandwerkerImperium MainViewModel: RefreshWorkshops() In-Place-Update statt Clear/Add (weniger UI-Churn), statisches WorkshopType-Array (vermeidet Enum.GetValues Allokation)
  - HandwerkerImperium MainViewModel: OnGameTick IncomeDisplay nur bei Wertaenderung updaten
  - HandwerkerImperium WorkshopDisplayModel: NotifyAllChanged() Methode fuer Property-Notifications nach In-Place-Update
  - RechnerPlus CalculatorViewModel: IDisposable implementiert (LanguageChanged + HistoryChanged Event-Unsubscription)
  - ZeitManager AlarmOverlayViewModel: IDisposable implementiert (StopClock + AudioService.Stop im Dispose)
  - Build: HandwerkerImperium.Shared + RechnerPlus.Shared + ZeitManager.Shared 0 Fehler, 0 Warnungen
- **BomberBlast Performance-Fixes (07.02.2026):**
  - AStar.cs: PriorityQueue/HashSet/Dictionary als Klassenfelder mit .Clear() (statt neue Instanzen pro Aufruf), BFS-Collections gepoollt, Directions als static readonly Array, Node Klasse durch value tuple ersetzt, GetNeighbors fuellt gepoolte Liste statt yield return
  - EnemyAI.cs: _dangerZone HashSet + _validDirections Liste als Klassenfelder, LINQ .Where().ToList() durch manuelle Loops ersetzt
  - GameEngine.cs: 5 gecachte Overlay-Objekte (_overlayBgPaint, _overlayTextPaint, _overlayFont, 2x _overlayGlowFilter), alle 4 Overlay-Methoden nutzen gecachte Objekte, Dispose() raeumt auf, _powerUps.ToList() durch Rueckwaerts-Iteration ersetzt
  - GameRenderer.cs: _activePowers Liste gepoollt mit .Clear() pro Frame, INV-Timer-String gecacht (nur bei Wertaenderung neu)
  - GameViewModel.cs: System.Diagnostics.Stopwatch statt DateTime.Now fuer Frame-Timing
  - Build: Shared + Desktop + Android 0 Fehler
- **BomberBlast Neon Visual Fixes (07.02.2026):**
  - Neon-Palette aufgehellt: FloorBase/Alt, WallBase/Edge, BlockBase/Mortar/Highlight/Shadow (alle Farbwerte ~40-60% erhoeht)
  - Neon-Bloecke: 3D-Kanten (Highlight oben/links, Shadow unten/rechts), dickere Glow-Risse (1→1.5), diagonaler Riss
  - HUD-Text-Glow Fix: Neuer _hudTextGlow mit SKBlurStyle.Outer (Glow nur aussen, Text bleibt scharf) statt _smallGlow mit SKBlurStyle.Normal (hat Text selbst verwischt → TIME/SCORE unlesbar)
  - B/F Labels durch Mini-Icons ersetzt: RenderMiniBomb() (Kreis+Lunte+Funken), RenderMiniFlame() (2-Schicht QuadTo-Flamme)
  - Build: 0 Fehler
- **HandwerkerImperium Research Tree Lokalisierung (07.02.2026):**
  - 92 neue resx-Keys in 6 Sprachen (EN, DE, ES, FR, IT, PT)
  - 2 UI Keys (StartResearch, CurrentResearch)
  - 45 Research Name Keys (3 Branches x 15 Levels: ResearchBetterSaws...ResearchMarketDomination)
  - 45 Research Description Keys (Effekt-Beschreibungen: "+5% Worker Efficiency", "-10% Costs", etc.)
  - Designer.cs: 92 neue Properties
- **HandwerkerImperium UI Redesign + Neue Features (07.02.2026):**
  - Tab-Bar: 7→5 Tabs (Home, Workers, Research, Shop, Settings), Stats+Achievements als Dashboard-Header-Icons
  - Design: LinearGradientBrush Hintergrund, Workshop-Cards BoxShadow+RadialGradientBrush, kompaktere Research-Cards
  - Quick Jobs: 5 Schnell-Auftraege mit 15min Rotation, direkt zu MiniGame, Level-skalierte Rewards
  - Daily Challenges: 3 taegliche Aufgaben aus 7 Typen, Auto-Tracking, 500€ Komplett-Bonus
  - Tool System: 4 Werkzeuge (Saege/Rohrzange/Schraubendreher/Pinsel), Level 0-5, MiniGame-Boni (ZoneBonus+TimeBonus)
  - Tool-Shop: Werkzeuge-Sektion in ShopView mit Upgrade-Funktion
  - Balancing: Startgeld 100→250€, Workshop Lv.1→2 Upgrade 200→100€
  - Achievement Reset: Achievements werden beim Spielstand-Reset zurueckgesetzt
  - Bugs: €-Bug in WorkerMarketView (\u20AC→€)
  - 3 neue Models (QuickJob, DailyChallenge, Tool), 2 neue Services (QuickJobService, DailyChallengeService)
  - 28 neue Lokalisierungs-Keys in 6 Sprachen
  - Build: Shared + Desktop + Android 0 Fehler, 0 Warnungen
- **HandwerkerImperium Post-Release Bugfixes (07.02.2026):**
  - DailyChallengeService: Englische Fallback-Strings entfernt (direkte GetString-Aufrufe)
  - MainView.axaml: 4-Layer Hintergrund (Base-Gradient + Primary/Amber/Secondary RadialGradientBrush Overlays)
  - MainViewModel: Explizites RefreshWorkshops() nach UpgradeWorkshop, Research-Timer in OnGameTick
  - WorkerMarketViewModel: Slot-Filter (HasAvailableSlots), Video-Ad-Refresh (IRewardedAdService), EUR-Bug Fix
  - WorkerMarketView.axaml: "Keine freien Plaetze" Info-Banner, Video-Icon beim Refresh-Button
  - AchievementsView: Bottom-Margin fuer vollstaendiges Scrollen
  - DashboardView: UnlockDisplay fuer Architect/GU Workshop-Karten (zeigt Prestige-Anforderung)
  - 2 neue resx-Keys in 6 Sprachen (Info, WatchAdToRefresh)
  - Build: Shared + Desktop + Android 0 Fehler
- **FinanzRechner Recurring Transactions Fixes (07.02.2026):**
  - RecurringTransactionsView.axaml: Edit (Pencil) + Delete (Trash) Buttons mit Ancestor-Bindings
  - RecurringTransactionsView.axaml: Pattern/Category Converter-Bindings statt rohe Enum-Namen
  - PatternToStringConverter.cs (NEU): IValueConverter fuer RecurrencePattern → lokalisierter String
  - RecurringTransactionsViewModel: EditTooltipText, DeleteTooltipText Properties
  - MainViewModel.OnAppearingAsync(): Auto-Processing faelliger Dauerauftraege bei App-Start
  - Build: 0 Fehler
- **FinanzRechner HomeView Redesign (07.02.2026):**
  - Hero-Header: Bilanz gross (28px), Einnahmen/Ausgaben als farbige Pill-Chips, Tipp oeffnet Tracker
  - Budget-Status Sektion: Gesamt-ProgressBar + Top-3 Kategorien mit AlertLevel-Farben (nur sichtbar wenn Budgets existieren)
  - Letzte Transaktionen: 3 neueste Buchungen mit Kategorie-Icon und farbigem Betrag, "Alle" Button
  - Horizontale Calculator-ScrollView: 5 kompakte Karten (100x90px) statt 2x3 Grid, farbiger Accent-Balken
  - Premium-Card: Gradient-Hintergrund (AccentColor→SecondaryColor), Stern-Icon, Preis, Pfeil
  - Quick-Add FAB: Rechts unten, Plus-Icon, AccentBrush, oeffnet Quick-Add Overlay
  - Quick-Add Overlay: Betrag, Beschreibung, Kategorie-Chips, Speichern/Abbrechen
  - BudgetDisplayItem Model (NEU): ObservableObject mit CategoryName (Sprachwechsel-faehig)
  - TransactionTypeToPrefixConverter (NEU): Vorzeichen-Converter (+/-)
  - MainViewModel: Budget-Status Properties (HasBudgets, OverallBudgetPercentage, TopBudgets), Recent Transactions (HasRecentTransactions, RecentTransactions), IsBalancePositive, 6 neue lokalisierte Text-Properties
  - 6 neue Lokalisierungs-Keys in 6 Sprachen (SectionBudget, SectionRecent, ViewAll, GetPremium, SectionCalculatorsShort, QuickAddTitle)
  - Build: 0 Fehler
- **IFileShareService - Plattformuebergreifender Export (07.02.2026):**
  - MeineApps.Core.Ava: IFileShareService Interface + DesktopFileShareService (Process.Start + MyDocuments)
  - MeineApps.Core.Premium.Ava: AndroidFileShareService (FileProvider + Intent.ActionSend, Linked File Pattern)
  - FinanzRechner: ExportService mit IFileShareService, ShareFileAsync nach Export, FileShareServiceFactory DI
  - FinanzRechner Android: FileProvider Konfiguration, file_paths.xml, Linked AndroidFileShareService
  - WorkTimePro: ExportService komplett umgeschrieben (echtes PdfSharpCore PDF + ClosedXML Excel), IFileShareService injiziert
  - WorkTimePro Android: FileProvider Konfiguration, file_paths.xml, Linked AndroidFileShareService
  - Build: Core.Ava + FinanzRechner (Desktop+Android) + WorkTimePro (Shared+Android) 0 Fehler
- **HandwerkerImperium Goldschrauben - Premium-Waehrung (07.02.2026):**
  - Neue Premium-Waehrung "Goldschrauben" (Icon: ScrewFlatTop, Farbe: #FFD700)
  - GameState: GoldenScrews, TotalGoldenScrewsEarned, TotalGoldenScrewsSpent Properties
  - GameStateService: AddGoldenScrews, TrySpendGoldenScrews, CanAffordGoldenScrews (lock-Pattern)
  - GoldenScrewsChangedEventArgs fuer Event-Benachrichtigung
  - Tool-Upgrades kosten Goldschrauben statt Euro (3/8/20/45/80 pro Level)
  - Verdienen: Daily Rewards (Tag 2/4/6/7: 2/3/5/10), Daily Challenges (1-3 + 15 Bonus), Achievements, Video-Ads (8), IAP (75/200/600)
  - Research Instant-Finish: Level 8+ Forschung sofort abschliessen (10-120 Goldschrauben)
  - Worker Hiring: Tier A (10) + Tier S (25) Goldschrauben zusaetzlich zum Euro-Preis
  - UI: Goldschrauben-Badge in Dashboard, Shop, Research, WorkerMarket Headers
  - DashboardView: Level-Fortschrittsbalken kompakter (Width 60, Height 4, "Lv.{0}")
  - ~18 neue/umbenannte resx-Keys in 6 Sprachen + NextRotation Duplikate entfernt
  - Build: 0 Fehler, 113 Warnungen (nur bekannte NuGet)
- **Design-Redesign: HandwerkerRechner & FitnessRechner (07.02.2026):**
  - HandwerkerRechner MainView.axaml komplett redesigned:
    - Fade-Transitions (CSS .TabContent/.Active, 150ms Opacity)
    - Hero-Header mit Amber-Gradient (#F59E0B→SecondaryColor→AccentColor), Lineal-Icon-Badge, App-Name, Rechner-Anzahl
    - Alle 9 Rechner in einheitlichem 2-Spalten-Grid (Height=120, CardColor→CardHoverColor Gradient)
    - Gradient-Icon-Badges (48x48) mit individuellen Farben pro Rechner (Amber/Purple/Green/Blue/Red/Orange/Gray/Emerald/Cyan)
    - Section-Headers mit farbigen Accent-Badges ("Boden & Wand", "Profi-Werkzeuge")
    - Premium-Card mit Gradient (AccentColor→SecondaryColor), Star-Icon, dekorative Ellipse, Chevron
  - FitnessRechner HomeView.axaml komplett redesigned:
    - Hero-Header mit Green-Gradient (#22C55E→SecondaryColor→AccentColor), HeartPulse-Icon-Badge
    - Dashboard-Card mit BoxShadow + Gradient-Icon-Badges fuer Gewicht/BMI/Wasser/Kalorien
    - 5 Calculator-Karten mit Gradient-Backgrounds + individuellen Gradient-Icon-Badges (Blue/Amber/Green/Purple/Red)
    - Section-Header mit PrimaryBrush-Badge ("Rechner")
    - Premium-Card im FinanzRechner-Stil (Gradient, Star, Ellipse, Chevron)
    - Disclaimer mit BoxShadow
  - HandwerkerRechner MainViewModel: 5 neue Properties (SectionFloorWallText, SectionPremiumToolsText, CalculatorCountText, GetPremiumText, PremiumPriceText) + UpdateHomeTexts()
  - FitnessRechner MainViewModel: 3 neue Properties (RemoveAdsText, PremiumPriceText, SectionCalculatorsText) + OnLanguageChanged()
  - 6 neue resx-Keys in HandwerkerRechner (6 Sprachen): SectionFloorWall, SectionPremiumTools, CalculatorCount, GetPremium, PremiumPrice, MoreCategories
  - 4 neue resx-Keys in FitnessRechner (6 Sprachen): RemoveAds, PremiumPrice, GetPremium, SectionCalculators
  - Designer.cs beider Apps aktualisiert
  - Build: 0 Fehler, 114 Warnungen (nur bekannte NuGet/SkiaSharp)
- **BomberBlast Coins-Economy + Shop (07.02.2026):**
  - Coin-Waehrung: Score→Coins (1:1 Level-Complete, 0.5x Game Over), persistent via IPreferencesService
  - Shop: 6 Upgrades (StartBombs/Fire/Speed, ExtraLives, ScoreMultiplier, TimeBonus) mit Level-Preisen
  - Rewarded Ads: Coins verdoppeln + Weitermachen (Story, 1x) - Desktop Simulator, Android spaeter
  - World-Gating: 5 Welten a 10 Level, Stern-Anforderungen (0/10/25/45/70)
  - 11 neue Dateien (Models, Services, VM, View), 10 geaenderte Dateien
  - GameEngine: IShopService injiziert, ApplyUpgrades, ContinueAfterGameOver, OnCoinsEarned Event
  - GameOverView: Coins-Sektion, Verdoppeln-Button, Weitermachen-Button
  - LevelSelectView: Welt-Header mit Lock/Earth-Icon, Coin-Badge im Header
  - MainMenuView: Coin-Badge unter Logo, Shop-Button (Store-Icon) im Button-Grid
  - 24 neue Lokalisierungs-Keys in 6 Sprachen
  - Build: Shared + Desktop + Android 0 Fehler
- **Rewarded Ads Lokalisierung Phase 7 (07.02.2026):**
  - BomberBlast: 6 neue Keys (PowerUpBoost, WatchVideo, WithoutBoost, SkipLevel, SkipLevelInfo, WatchVideoBoost) in 6 Sprachen + Designer.cs
  - FinanzRechner: 4 neue Keys (ExportLocked, ExportLockedDesc, WatchVideoExport, ExportAdFailed) in 6 Sprachen + Designer.cs
  - HandwerkerRechner: 6 neue Keys (PremiumCalculatorsLocked, WatchVideoFor30Min, AccessGranted, AccessExpiresIn, TemporaryAccessActive, VideoFor30Min) in 6 Sprachen + Designer.cs
  - WorkTimePro: 6 neue Keys (PremiumFeatureTitle, PremiumFeatureDesc, WatchVideoOnce, BuyPremiumUnlimited, VideoRewardSuccess, VideoAdFailed) in 6 Sprachen + Designer.cs
  - Insgesamt 22 Keys x 6 Sprachen = 132 neue Uebersetzungen + 22 Designer.cs Properties
  - Build: Alle 4 Shared-Projekte 0 Fehler
- **Rewarded Ads Android-Integration Phase 8 (07.02.2026):**
  - RewardedAdHelper.cs + AndroidRewardedAdService.cs als Linked Files in alle 6 Android .csproj eingebunden
  - 6 App.axaml.cs: `RewardedAdServiceFactory` Property (Func<IServiceProvider, IRewardedAdService>) + DI-Override nach AddMeineAppsPremium()
  - 6 MainActivity.cs: RewardedAdHelper erstellt vor base.OnCreate(), Factory gesetzt, Load() nach DI-Build, Dispose() in OnDestroy()
  - 3 AndroidManifest.xml App-ID Fixes: HandwerkerRechner/FinanzRechner/FitnessRechner auf korrekten Publisher-Account (ca-app-pub-2588160251469436)
  - RewardedAdHelper.cs: Inner-Klassen umbenannt (RewardedAdLoadCallback→LoadCallback, RewardedAdShowCallback→ShowCallback)
  - RewardedAdHelper.cs: Java Generics Erasure Fix - `[Register("onAdLoaded", "(Lcom/google/android/gms/ads/rewarded/RewardedAd;)V", "")]` Attribut auf LoadCallback.OnAdLoaded()
  - Apps: HandwerkerRechner, FinanzRechner, FitnessRechner, WorkTimePro, BomberBlast, HandwerkerImperium
  - Full Solution Build: 0 Fehler, 112 Warnungen (nur bekannte NuGet/SkiaSharp)
- **FinanzRechner 3 Rewarded Ad Features (07.02.2026):**
  - Feature 1 (CSV-Export Ad-Gate): StatisticsVM ShowAdAsync("export_pdf"/"export_csv") Placements, CSV-Export in StatisticsVM + ExpenseTrackerVM mit Ad-Gate (IPurchaseService/IRewardedAdService injiziert)
  - Feature 2 (Budget-Analyse-Report): BudgetAnalysisReport Model (NEU), MainVM IRewardedAdService + RequestBudgetAnalysis/ConfirmBudgetAd/CloseBudgetAnalysis, HomeView Monatsreport-Button + 2 Overlays (Ad + Report), Placement "budget_analysis"
  - Feature 3 (Extended Stats 24h): StatisticsVM IPreferencesService, OnSelectedPeriodChanged blockiert Q/H/Y ohne Premium/24h-Zugang, ExtendedStatsAdOverlay + ConfirmExtendedStatsAd, 24h via DateTime.UtcNow+DateTimeStyles.RoundtripKind, Placement "extended_stats"
  - StatisticsView.axaml: Extended-Stats-Ad-Overlay (ChartTimelineVariant-Icon, 24h-Badge)
  - 8 neue resx-Keys in 6 Sprachen (BudgetAnalysisTitle/Desc, MonthlyReport, SavingTip, ComparedToLastMonth, ExtendedStatsTitle/Desc, AccessFor24h)
  - Build: 0 Fehler
- **FitnessRechner 3 Rewarded Ad Features (07.02.2026):**
  - Feature 1: ShowAdAsync Placement-Strings (FoodSearchVM: `ShowAdAsync()` → `ShowAdAsync("barcode_scan")`)
  - Feature 2: Wochenanalyse (ProgressVM: 7-Tage-Durchschnitte Gewicht/Kalorien/Wasser/Trend/Zielerreichung, Ad-Gate "detail_analysis")
  - Feature 3: Tracking Export CSV (ProgressVM: 90-Tage-Daten, IFileShareService, Ad-Gate "tracking_export")
  - Feature 4: Erweiterte Nahrungsmittel-DB (FoodSearchVM: 24h Zugang via Ad "extended_food_db", Hint bei <=5 Ergebnissen)
  - ProgressView: 4-Spalten-Header (Analyse+Export+Add), 3 Overlays (Analysis Ad, Analysis Report 2x3 Grid, Export Ad)
  - FoodSearchView: Extended DB Hint Card, Scan Limit Ad Overlay, Extended DB Ad Overlay
  - App.axaml.cs: FileShareServiceFactory + IFileShareService DI (Desktop: DesktopFileShareService, Android: AndroidFileShareService)
  - Android: FileProvider (AndroidManifest, file_paths.xml, Linked AndroidFileShareService.cs), FileShareServiceFactory in MainActivity
  - 11 neue resx-Keys in 6 Sprachen + Designer.cs (WeeklyAnalysis, WeeklyAnalysisDesc, AvgWeight, AvgCalories, AvgWater, WeightTrend, CalorieTarget, ExportTracking, ExportTrackingDesc, ExtendedFoodDb, ExtendedFoodDbDesc)
  - Build: FitnessRechner.Shared 0 Fehler
- **Multi-Placement Rewarded Ads Architektur (07.02.2026):**
  - AdConfig.cs komplett umgeschrieben: ALLE 17 Rewarded Ad-Unit-IDs aus AdMob.docx (statt 1 pro App)
  - IRewardedAdService: `ShowAdAsync(string placement)` Overload (abwaertskompatibel)
  - RewardedAdHelper: `LoadAndShowAsync(string adUnitId)` fuer On-Demand-Loading (nicht-default Placements)
  - AndroidRewardedAdService: `appName` Parameter fuer AdConfig-Lookup, `ShowAdAsync(placement)` -> `AdConfig.GetRewardedAdUnitId(appName, placement)`
  - 6 MainActivity.cs: `appName` Parameter an AndroidRewardedAdService uebergeben
  - Full Solution Build: 0 Fehler
- **BomberBlast Score-Verdopplung (07.02.2026):**
  - GameViewModel: IRewardedAdService + IPurchaseService injiziert, Score-Double-Overlay nach Level-Complete
  - GameEngine: DoubleScore() Methode (verdoppelt Score, feuert OnScoreChanged + OnCoinsEarned)
  - GameView.axaml: Score-Double-Overlay (ZIndex=50, Video-Button, Weiter-Button)
  - Placement-Strings: continue, level_skip, power_up, score_double (alle ShowAdAsync-Aufrufe)
  - 4 neue resx-Keys in 6 Sprachen (ScoreDoubleTitle/Desc, WatchVideoDouble, ContinueWithout)
  - Build: 0 Fehler
- **HandwerkerRechner 3 Rewarded Ad Features (07.02.2026):**
  - Feature 1: Placement "premium_access" fuer bestehenden Premium-Zugang
  - Feature 2: Extended History (24h, 30 statt 5 Eintraege, Placement "extended_history")
  - Feature 3: Material-Liste PDF Export (PdfSharpCore, IMaterialExportService, Placement "material_pdf")
  - Feature 4: Projekt-Export PDF (ProjectsVM, Placement "project_export")
  - Bugfixes: RoofSolarVM/GardenVM/MetalVM Property-Namen korrigiert
  - 8 neue resx-Keys in 6 Sprachen + Designer.cs
  - Build: 0 Fehler
- **WorkTimePro Placement-Strings + Extended Stats (07.02.2026):**
  - VacationVM: ShowAdAsync("vacation_entry")
  - YearOverviewVM: ShowAdAsync("export")
  - StatisticsVM: ShowAdAsync("monthly_stats") + IPreferencesService, Extended Stats Gate (Quartal/Jahr → 24h Zugang via Rewarded Ad)
  - Build: 0 Fehler
