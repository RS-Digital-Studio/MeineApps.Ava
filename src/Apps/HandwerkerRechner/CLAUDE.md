# HandwerkerRechner (Avalonia) - v2.0.0

## Uebersicht
Handwerker-App mit 9 Rechnern (4 Free + 5 Premium), Projektverwaltung und Einheiten-Umrechnung.

## Projektstruktur
```
HandwerkerRechner/
├── HandwerkerRechner.Shared/     # Shared Code (net10.0)
│   ├── Views/
│   │   ├── Floor/                # 4 Floor Calculator Views
│   │   ├── Premium/              # 5 Premium Calculator Views
│   │   ├── MainView.axaml        # Tab Navigation (Home/Projects/Settings) + Calculator Overlay
│   │   ├── ProjectsView.axaml
│   │   └── SettingsView.axaml
│   ├── ViewModels/
│   │   ├── Floor/                # TileCalculator, Wallpaper, Paint, Flooring VMs
│   │   ├── Premium/              # Drywall, Electrical, Metal, Garden, RoofSolar VMs
│   │   ├── Base/                 # CalculatorViewModelBase (abstract, nicht von Floor VMs genutzt)
│   │   ├── MainViewModel.cs      # Tab + Calculator Navigation + Premium
│   │   ├── ProjectsViewModel.cs  # CRUD + Navigation zu Calculator
│   │   └── SettingsViewModel.cs
│   ├── Models/
│   │   ├── CraftEngine.cs        # 16 Berechnungsmethoden
│   │   ├── Project.cs
│   │   └── CalculatorCategory.cs # 6 Kategorien, 17 CalculatorTypes
│   ├── Services/
│   │   ├── ProjectService.cs     # JSON, SemaphoreSlim
│   │   ├── CalculationHistoryService.cs
│   │   └── UnitConverterService.cs
│   ├── Converters/
│   ├── Resources/Strings/        # 6 Sprachen
│   └── App.axaml(.cs)            # DI Configuration
├── HandwerkerRechner.Android/    # Android (net10.0-android)
└── HandwerkerRechner.Desktop/    # Desktop (net10.0)
```

## Architektur-Entscheidungen
- **Calculator Overlay via DataTemplates**: MainViewModel hat CurrentPage/CurrentCalculatorVm, MainView nutzt DataTemplates fuer automatische View-Zuordnung per VM-Typ (9 VMs)
- **Tab-Wechsel schliesst Calculator**: SelectHomeTab/SelectProjectsTab/SelectSettingsTab setzen CurrentPage=null
- **Projekt-Navigation**: ProjectsVM.NavigationRequested wird in MainVM abonniert -> CurrentPage = route (inkl. projectId Query-Parameter)
- **Floor VMs erben von ObservableObject**: Nicht von CalculatorViewModelBase (Premium VMs ebenfalls direkt ObservableObject)
- **WireCalculatorEvents per switch/case**: Da kein gemeinsames Interface, werden NavigationRequested/MessageRequested/LoadFromProjectIdAsync per Typ-Match verdrahtet
- **MessageRequested Event**: Alle VMs verwenden `event Action<string, string>? MessageRequested` fuer Benutzer-Benachrichtigungen
- **SaveProject Dialog-Pattern**: ShowSaveDialog, ConfirmSaveProject, CancelSaveProject

## Build-Befehle
```bash
dotnet build src/Apps/HandwerkerRechner/HandwerkerRechner.Desktop/HandwerkerRechner.Desktop.csproj
dotnet build src/Apps/HandwerkerRechner/HandwerkerRechner.Android/HandwerkerRechner.Android.csproj
```

## Abhaengigkeiten
- MeineApps.Core.Ava (Themes, Localization, Preferences)
- MeineApps.Core.Premium.Ava (AdMob, IAP)
- MeineApps.UI (Shared UI Components)

## Status: KOMPLETT (06.02.2026)
- 9 Calculator Views + VMs (4 Floor + 5 Premium)
- Projektverwaltung mit CRUD
- Calculator-Navigation implementiert (CurrentPage + DataTemplate Overlay)
- Projekt-Navigation verdrahtet (ProjectsVM -> Calculator mit projectId)
- Tab-Wechsel schliesst offenen Calculator
- Debug.WriteLine durch MessageRequested Events ersetzt
- Lokalisierte Metall-/Profil-/Orientierungslisten
- Desktop Build: 0 Fehler, 0 Warnungen
- Android Build: 0 Fehler, 0 Warnungen

## Bugfixes (06.02.2026 - Abend)

### Vollstaendige Lokalisierung
- **SettingsView.axaml**: Alle ~25 hardcoded English Strings durch Bindings ersetzt
  - Theme-Namen + Beschreibungen (Midnight/Aurora/Daylight/Forest) lokalisiert
  - Abschnittstitel (Choose Design, Language, Unit System, Premium, About) lokalisiert
  - Buttons (Remove Ads, Restore Purchases, Send Feedback) lokalisiert
  - Ad-Free Confirmation lokalisiert
- **SettingsViewModel.cs**: 22 lokalisierte Text-Properties + UpdateLocalizedTexts()
- **MainViewModel.cs**: Tab-Text Properties (TabHomeText, TabProjectsText, TabSettingsText) + LanguageChanged Subscription + UpdateHomeTexts()
- **MainView.axaml**: Tab-Bar-Texte via Bindings statt hardcoded English
- **.resx Dateien**: 16 neue Keys in allen 6 Sprachen (Theme-Namen, Settings-Titel, Unit-System etc.)

### Tab-Bar Verbesserungen
- Aktiver Tab-Indicator (farbiger Border am oberen Rand)
- Aktiver Tab-Text in Primary-Farbe mit FontWeight Medium
- Inaktiver Tab-Text in Muted-Farbe
- Tab-Bar versteckt sich wenn Calculator-Overlay offen (IsVisible=!IsCalculatorOpen)
- Theme-Buttons Height="NaN" (Auto-Sizing)

### Calculator-Overlay Fix
- ContentControl.Background existiert nicht in Avalonia -> Panel-Wrapper mit Background
- Calculator-Views werden jetzt korrekt ueber dem Tab-Content angezeigt

### Deep Code Review (06.02.2026)

#### Services
- **ProjectService**: DateTime.Now → DateTime.UtcNow (Konsistenz mit Project.cs Model), 2x Debug.WriteLine entfernt, catch (Exception ex) → catch (Exception)

#### Floor ViewModels (4 VMs: Tile, Wallpaper, Paint, Flooring)
- **Alle 4 Floor VMs**: 16x Debug.WriteLine entfernt, `using System.Diagnostics` entfernt, 16x catch (Exception ex) → catch (Exception)
- **WallpaperVM**: History-Titel lokalisiert (HistoryWallHeight Key)
- **PaintVM**: History-Titel lokalisiert (HistoryPaintCoats Key)

#### Premium ViewModels (5 VMs: Metal, Electrical, Garden, RoofSolar, Drywall)
- **Alle 5 Premium VMs**: 15x Debug.WriteLine entfernt (3 pro VM), 15x catch (Exception ex) → catch (Exception)
- **MetalVM**: History-Titel lokalisiert (HistoryThreadSize Key)
- **ElectricalVM**: Kabelkosten-Label lokalisiert (CableCostLabel), h/Tag lokalisiert (HistoryHoursPerDay), Ohm'sches Gesetz lokalisiert (OhmsLaw Key)
- **GardenVM**: 3x "Gesamtkosten" lokalisiert (TotalCost Key), "cm tief" lokalisiert (HistorySoilDepth Format-String)
- **RoofSolarVM**: "Gesamtkosten" lokalisiert (TotalCost), "Anlagenkosten" lokalisiert (ResultSystemCost), "Amortisation/Jahre" lokalisiert (ResultPaybackTime/HistoryYears), "Spannweite/Hoehe" lokalisiert (HistoryRoofPitch Format-String), "Dachflaeche" lokalisiert (HistoryRoofArea)
- **DrywallVM**: "(doppelt)" lokalisiert (HistoryDoubleLayered)

#### Other ViewModels
- **ProjectsVM**: 3x Debug.WriteLine entfernt, `using System.Diagnostics` entfernt, 3x catch fix
- **SettingsVM**: `using System.Diagnostics` entfernt

#### Lokalisierung
- **10 neue resx-Keys** in 6 Sprachen: HistoryThreadSize, CableCostLabel, HistoryHoursPerDay, HistorySoilDepth, HistoryYears, HistoryRoofPitch, HistoryRoofArea, HistoryDoubleLayered, HistoryWallHeight, HistoryPaintCoats
- **AppStrings.Designer.cs**: 10 neue Properties hinzugefuegt

#### Zusammenfassung
- 36x Debug.WriteLine entfernt (16 Floor + 15 Premium + 2 ProjectService + 3 ProjectsVM)
- 34x catch (Exception ex) → catch (Exception)
- 4x `using System.Diagnostics` entfernt
- 3x DateTime.Now → DateTime.UtcNow (ProjectService)
- 10 hardcodierte deutsche/englische Strings lokalisiert
- Full Solution Build: 0 Fehler

### AppChecker Fixes (07.02.2026)
- **MainViewModel.OnLanguageChanged()**: `SettingsViewModel.UpdateLocalizedTexts()` Aufruf ergaenzt
- **AppStrings.fr.resx**: ~140 fehlende Keys ergaenzt (PDF Export, Units, Info Texts, History, Electrical, Drywall, Semantic Properties)
- **AppStrings.it.resx**: ~140 fehlende Keys ergaenzt (identische Kategorien wie FR, italienische Uebersetzungen)
- **AppStrings.pt.resx**: ~152 fehlende Keys ergaenzt (FR/IT-Kategorien + 12 PT-spezifische Label/Result-Keys)
- Verifiziert: FR 500/500, IT 500/500, PT 535/500 unique Keys (0 fehlend vs. Base)
- Base-Datei hat 7 Duplikat-Keys (ExportPdf, Date, MaterialList, GeneratedBy, ShareMaterialList, PdfExportSuccess, PdfExportFailed)

### Design-Redesign (07.02.2026)
- **MainView.axaml** komplett redesigned:
  - Fade-Transitions (CSS .TabContent/.Active, 150ms Opacity)
  - Hero-Header: Amber-Gradient, RulerSquareCompass-Icon-Badge, App-Name + Rechner-Anzahl
  - 9 Rechner in 2-Spalten-Grid (Height=120, CardColor→CardHoverColor Gradient)
  - Gradient-Icon-Badges (48x48): Amber/Purple/Green/Blue/Red/Orange/Gray/Emerald/Cyan
  - Section-Headers mit farbigen Badges ("Boden & Wand", "Profi-Werkzeuge")
  - Premium-Card mit AccentColor→SecondaryColor Gradient, Star-Icon, dekorative Ellipse
- **MainViewModel**: 5 neue Properties (SectionFloorWallText, SectionPremiumToolsText, CalculatorCountText, GetPremiumText, PremiumPriceText) + UpdateHomeTexts()
- 6 neue resx-Keys in 6 Sprachen (SectionFloorWall, SectionPremiumTools, CalculatorCount, GetPremium, PremiumPrice, MoreCategories)
- Build: 0 Fehler
