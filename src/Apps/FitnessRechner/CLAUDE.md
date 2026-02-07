# FitnessRechner (Avalonia) - v2.0.0

## Uebersicht
Fitness-App mit BMI, Kalorien, Wasser, Idealgewicht, Koerperfett-Rechnern plus Tracking mit Charts und Nahrungsmittel-Suche.

## Projektstruktur
```
FitnessRechner/
├── FitnessRechner.Shared/     # Shared Code (net10.0)
│   ├── Views/                  # Avalonia UserControls
│   │   ├── Calculators/        # 5 Calculator Views
│   │   ├── MainView.axaml      # Tab Navigation (Home/Progress/Food/Settings)
│   │   ├── HomeView.axaml      # Dashboard
│   │   ├── ProgressView.axaml  # Tracking mit 4 Sub-Tabs + Charts
│   │   ├── FoodSearchView.axaml
│   │   └── SettingsView.axaml
│   ├── ViewModels/
│   │   ├── Calculators/        # 5 Calculator VMs
│   │   ├── MainViewModel.cs    # Tab + Dashboard + Navigation
│   │   ├── ProgressViewModel.cs # Tracking + LiveCharts
│   │   ├── FoodSearchViewModel.cs
│   │   └── SettingsViewModel.cs
│   ├── Models/
│   │   ├── FitnessEngine.cs    # All calculation logic
│   │   ├── FoodItem.cs
│   │   ├── TrackingEntry.cs
│   │   └── ...
│   ├── Services/
│   │   ├── TrackingService.cs   # JSON-based persistence
│   │   ├── FoodSearchService.cs # Fuzzy matching, favorites, recipes
│   │   ├── FoodDatabase.cs      # 114 foods with aliases
│   │   ├── BarcodeLookupService.cs # Open Food Facts API
│   │   └── UndoService.cs       # Generic undo with timeout
│   ├── Converters/
│   ├── Resources/Strings/       # 6 Sprachen
│   └── App.axaml(.cs)           # DI Configuration
├── FitnessRechner.Android/      # Android (net10.0-android)
└── FitnessRechner.Desktop/      # Desktop (net10.0, Win/Linux)
```

## Architektur-Entscheidungen
- **IDisposable statt Finalizer**: Verhindert SIGSEGV bei LiveCharts (MAUI-Bug geloest)
- **TrackingViewModel: IDisposable**: CancellationTokenSource Cleanup (Dispose-Pattern mit _disposed Flag)
- **ProgressView mit 4 Sub-Tabs**: Ersetzt separate History/Tracking-Pages (modernere UX)
- **Generischer UndoService<T>**: Nicht per DI registriert, da generisch - ViewModels erstellen eigene Instanzen
- **MainView Tab-Switching**: Statt Shell-Navigation, Content-Wechsel im MainViewModel
- **Child-VMs per Constructor Injection**: SettingsVM, ProgressVM, FoodSearchVM werden in MainVM injiziert (nicht manuell erstellt)
- **MessageRequested Event**: Alle VMs verwenden `event Action<string, string>? MessageRequested` statt Debug.WriteLine fuer Benutzer-Benachrichtigungen
- **ThemeService Init in App.axaml.cs**: Muss beim Start aufgeloest werden damit gespeichertes Theme angewendet wird
- **Calculator Overlay via DataTemplates**: MainViewModel hat CurrentPage/CurrentCalculatorVm, MainView nutzt DataTemplates fuer automatische View-Zuordnung per VM-Typ
- **Tab-Wechsel schliesst Calculator**: SelectHomeTab/SelectProgressTab etc. setzen CurrentPage=null bevor Tab gewechselt wird
- **Preferences Keys**: `daily_water_goal` (WaterVM) und `daily_calorie_goal` (CaloriesVM) - konsistent mit TrackingVM/ProgressVM
- **DateTime.Today fuer Tracking**: BmiVM/BodyFatVM nutzen DateTime.Today (nicht .Now) fuer datumsbasierte Gruppierung
- **BarcodeLookupService Thread-Safety**: SemaphoreSlim fuer _barcodeCache Dictionary, Lock wird waehrend HTTP-Calls released

## Build-Befehle
```bash
dotnet build src/Apps/FitnessRechner/FitnessRechner.Shared/FitnessRechner.Shared.csproj
dotnet build src/Apps/FitnessRechner/FitnessRechner.Desktop/FitnessRechner.Desktop.csproj
dotnet build src/Apps/FitnessRechner/FitnessRechner.Android/FitnessRechner.Android.csproj
```

## Premium-Features
- Werbefrei (AdMob Banner entfernt)
- Preis: 3,99 EUR

## Abhaengigkeiten
- MeineApps.Core.Ava (Themes, Localization, Preferences)
- MeineApps.Core.Premium.Ava (AdMob, IAP)
- MeineApps.UI (Shared UI Components)
- LiveChartsCore.SkiaSharpView.Avalonia (Charts)

## Status: KOMPLETT (06.02.2026)
- Alle 5 Rechner migriert
- Tracking + Charts funktionsfaehig
- Food Search mit Fuzzy Matching
- Alle 3 Projekte bauen fehlerfrei (0 Fehler, 0 Warnungen)
- Solution-Struktur korrigiert (unter Apps > _FitnessRechner)
- ThemeService wird beim Start initialisiert
- Child-VMs per Constructor Injection in MainViewModel
- BMI/BodyFat Kategorien lokalisiert (8+5 neue resx-Keys)
- Debug.WriteLine durch MessageRequested Events ersetzt (alle VMs + BarcodeLookupService)
- 10 neue Alert-Message Keys in 6 Sprachen
- Dynamische App-Version (statt hardcoded v1.4.0)
- Calculator-Navigation implementiert (CurrentPage + DataTemplate Overlay)
- Tab-Wechsel schliesst offenen Calculator automatisch
- ProgressViewModel.OnAppearingAsync() wird beim Tab-Wechsel aufgerufen (Charts laden)
- AppStrings.Designer.cs: WaterRemaining + UnknownProduct Properties hinzugefuegt

### Deep Code Review Fixes (06.02.2026)
- **Preferences Key-Mismatch gefixt**: WaterVM "WaterGoal"→"daily_water_goal", CaloriesVM "CalorieGoal"→"daily_calorie_goal" (2 Stellen)
- **DateTime.Today statt .Now**: BmiVM (2 Stellen) + BodyFatVM (1 Stelle) fuer korrekte datumsbasierte Gruppierung
- **TrackingViewModel IDisposable**: CancellationTokenSource Cleanup implementiert (Dispose-Pattern)
- **HistoryViewModel lokalisiert**: "Avg"/"Min"/"Max" hardcoded Strings → AppStrings.Average/Min/Max (6 Stellen)
- **ProgressVM Water-Status**: CaloriesRemaining.Replace("kcal","ml") Hack → eigener AppStrings.WaterRemaining Key
- **IdealWeightViewModel**: MessageRequested Event hinzugefuegt (Konsistenz mit allen anderen Calculator VMs)
- **BarcodeLookupService Thread-Safety**: SemaphoreSlim fuer _barcodeCache, Lock Release waehrend HTTP-Calls, 8x Debug.WriteLine entfernt, "Unbekanntes Produkt"→AppStrings.UnknownProduct
- **Unused variable warnings**: catch (Exception ex)→catch (Exception) in 8 Stellen (MainVM, HistoryVM, FoodSearchVM 3x, ProgressVM, TrackingVM, BarcodeScannerVM)
- **2 neue resx-Keys in 6 Sprachen**: WaterRemaining ("{0} ml remaining"), UnknownProduct ("Unknown Product")

### Debug.WriteLine Cleanup (07.02.2026)
- **App.axaml.cs**: 9x Debug.WriteLine entfernt, catch (Exception ex)→catch (Exception)
- **FoodSearchService.cs**: 7x Debug.WriteLine entfernt, 4x catch (Exception ex)→catch (Exception), 2x catch→vereinfacht (nur throw)
- **TrackingService.cs**: 3x Debug.WriteLine entfernt, 2x catch (Exception ex)→catch (Exception)/catch
- **VersionedDataService.cs**: 2x Debug.WriteLine entfernt, 1x catch (Exception ex)→catch (Exception)
- **MainActivity.cs**: 3x Debug.WriteLine entfernt (Android.Util.Log.Error bleibt bestehen)
- Build: Shared + Desktop + Android jeweils 0 Fehler, 0 Warnungen

### LanguageChanged Fix (07.02.2026)
- **MainViewModel**: `settingsViewModel.LanguageChanged += OnLanguageChanged` abonniert
- OnLanguageChanged aktualisiert 12 Properties (NavHomeText, NavProgressText, NavFoodText, NavSettingsText, AppDescription, CalcBmiLabel, CalcCaloriesLabel, CalcWaterLabel, CalcIdealWeightLabel, CalcBodyFatLabel, CalculatorsLabel, MyProgressLabel)

### Design-Redesign (07.02.2026)
- **HomeView.axaml** komplett redesigned:
  - Hero-Header: Green-Gradient (#22C55E→SecondaryColor→AccentColor), HeartPulse-Icon-Badge
  - Dashboard-Card mit BoxShadow + Gradient-Icon-Badges (Purple/Blue/Green/Amber)
  - 5 Calculator-Karten: Gradient-Backgrounds (CardColor→CardHoverColor) + Gradient-Icon-Badges (Blue/Amber/Green/Purple/Red)
  - Section-Header mit PrimaryBrush-Badge
  - Premium-Card: Gradient (AccentColor→SecondaryColor), Star-Icon, dekorative Ellipse, Chevron
  - Disclaimer mit BoxShadow
- **MainViewModel**: 3 neue Properties (RemoveAdsText, PremiumPriceText, SectionCalculatorsText) + OnLanguageChanged()
- 4 neue resx-Keys in 6 Sprachen (RemoveAds, PremiumPrice, GetPremium, SectionCalculators)
- Build: 0 Fehler
