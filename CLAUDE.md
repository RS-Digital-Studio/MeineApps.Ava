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

## Status (08. Februar 2026)

Alle 8 Apps im geschlossenen Test, warten auf 12 Tester fuer Produktion.

| App | Version | Ads | Premium |
|-----|---------|-----|---------|
| RechnerPlus | v2.0.0 | Nein | Nein |
| ZeitManager | v2.0.0 | Nein | Nein |
| HandwerkerRechner | v2.0.0 | Banner + Rewarded | 3,99 remove_ads |
| FinanzRechner | v2.0.0 | Banner + Rewarded | 3,99 remove_ads |
| FitnessRechner | v2.0.0 | Banner + Rewarded | 3,99 remove_ads |
| WorkTimePro | v2.0.0 | Banner + Rewarded | 3,99/Mo oder 19,99 Lifetime |
| HandwerkerImperium | v2.0.2 | Banner + Rewarded | 4,99 Premium |
| BomberBlast | v2.0.0 | Banner + Rewarded | 3,99 remove_ads |

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

## Wichtige Regeln & Patterns

### Ad-Banner Layout (WICHTIG)
- **MainView Grid**: `RowDefinitions="*,Auto,Auto"` → Row 0 Content, Row 1 Ad-Spacer (50dp), Row 2 Tab-Bar
- **Jeder MainViewModel**: Muss `_adService.ShowBanner()` explizit aufrufen (AdMobHelper verschluckt Fehler)
- **ScrollViewer Bottom-Padding**: Mindestens 60dp in ALLEN scrollbaren Sub-Views (sonst verdeckt Banner den letzten Inhalt)
- **Tab-Bar Heights (tabBarHeightDp)**: FinanzRechner/FitnessRechner/HandwerkerRechner/WorkTimePro=56, HandwerkerImperium=64, BomberBlast=0

### AdMob Linked-File-Pattern
- `AdMobHelper.cs` + `RewardedAdHelper.cs` + `AndroidRewardedAdService.cs` + `AndroidFileShareService.cs` leben in Premium-Library unter `Android/`
- Werden per `<Compile Include="..." Link="..." />` in jedes Android-Projekt eingebunden
- `<Compile Remove="Android\**" />` verhindert Kompilierung im net10.0 Library-Projekt
- **UMP Namespace-Typo**: `Xamarin.Google.UserMesssagingPlatform` (DREIFACHES 's')
- **Java Generics Erasure**: RewardedAdHelper.LoadCallback braucht `[Register]`-Attribut statt override

### Rewarded Ads Multi-Placement
- `AdConfig.cs`: 17 Rewarded Ad-Unit-IDs (alle aus AdMob.docx)
- `ShowAdAsync(string placement)` → placement-spezifische Ad-Unit-ID via AdConfig
- Jede App hat `RewardedAdServiceFactory` Property in App.axaml.cs fuer Android DI-Override

### AdMob Publisher-Account
- **ca-app-pub-2588160251469436** fuer alle 6 werbe-unterstuetzten Apps
- RechnerPlus + ZeitManager sind werbefrei (keine AdMob-Integration)

### Releases
- **Alle im geschlossenen Test** 
