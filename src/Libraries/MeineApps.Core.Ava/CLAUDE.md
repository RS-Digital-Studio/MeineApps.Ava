# MeineApps.Core.Ava — Shared Core Library

> Für app-spezifische Patterns siehe die jeweilige App-CLAUDE.md.
> App-spezifische Farbpaletten sind in [Haupt-CLAUDE.md](../../../CLAUDE.md) dokumentiert.

Shared Library für alle 12 Avalonia-Apps. Enthält Preferences-Persistenz, Plattform-
Abstraktionen, Lokalisierung, Calculator-History, Converters, Design-Tokens und
ViewLocator.

## Zielframework

- .NET 10.0
- C# 14
- Avalonia 12.0.2

## Build

```bash
dotnet build src/Libraries/MeineApps.Core.Ava/MeineApps.Core.Ava.csproj
```

## Abhängigkeiten

| Package | Zweck |
|---------|-------|
| `Avalonia` | UI-Framework (für ViewLocator, Converters, Themes) |
| `CommunityToolkit.Mvvm` | `ObservableObject` für `ViewModelBase` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | DI-Interfaces |

Versionen zentral in `Directory.Packages.props`.

## Struktur

```
MeineApps.Core.Ava/
├── Services/
│   ├── IPreferencesService + PreferencesService           # JSON-basiert, %APPDATA%/{AppName}/preferences.json
│   ├── IHapticService                                     # Plattform-Abstraktion (NoOp-Fallback inline definiert)
│   ├── BackPressHelper                                    # Double-Back-to-Exit-Logik (Android)
│   ├── ICalculationHistoryService + CalculationHistoryService  # History pro Rechner (Calculator-Apps)
│   ├── IUnitConverterService + UnitConverterService       # Länge, Fläche, Volumen, Gewicht (Metrisch/Imperial)
│   ├── IFileShareService + DesktopFileShareService        # Plattform-Abstraktion (Android-Override per Factory)
│   └── UriLauncher                                        # Static Helper: OpenUri + ShareText (plattformübergreifend)
├── Localization/
│   ├── ILocalizationService + LocalizationService        # ResourceManager-Wrapper, LanguageChanged-Event
│   └── TranslateExtension                                 # XAML Markup-Extension für Lokalisierung
├── ViewModels/
│   └── ViewModelBase                                      # ObservableObject-Basis mit gängigen Properties
├── ViewLocator.cs                                         # VM → View per Konvention (BingXBot.ViewModels.X → BingXBot.Views.X)
├── Themes/
│   └── ThemeColors.axaml                                  # Design Tokens (Spacing, Radius, Fonts)
└── Converters/
    ├── BoolConverters.cs       # BoolToVisibility, InverseBool, BoolToOpacity, BoolToBrush
    ├── StringConverters.cs     # StringTruncate, StringToColorBrush, StringToColor
    ├── NumberConverters.cs     # NumberFormat, CurrencyConverter
    ├── DateTimeConverters.cs   # DateTimeFormat, RelativeTime (UTC-basiert)
    └── ColorConverters.cs      # Hex-String → Color/Brush
```

## Design Tokens (`ThemeColors.axaml`)

| Token-Gruppe | Werte |
|--------------|-------|
| Spacing | `SpacingSm: 8px`, `SpacingMd: 12px`, `SpacingLg: 16px`, `SpacingXl: 24px` |
| Radius | `RadiusSm: 4px`, `RadiusMd: 8px`, `RadiusLg: 12px` |
| Typography | `FontSizeBodyMd: 14px`, `FontSizeTitleLg: 22px`, `FontSizeHeadlineMd: 28px` |

App-spezifische Primary-Farben werden via `Themes/AppPalette.axaml` im jeweiligen
Shared-Projekt geladen.

## Service-Patterns

### `IPreferencesService` / `PreferencesService`

JSON-basiert in `%APPDATA%/{AppName}/preferences.json` (Windows) bzw.
`~/.config/{AppName}/preferences.json` (Linux). Generische Get/Set-API mit Defaults.

```csharp
_prefs.Set("key", value);
var val = _prefs.Get<string>("key", "default");
```

### `IHapticService`

Plattform-Abstraktion für haptisches Feedback. `NoOpHapticService` als Desktop-Fallback
inline definiert. Android-Apps registrieren eigene `AndroidHapticService`-Implementierung
via Factory-Pattern in `App.axaml.cs`.

```csharp
_haptic.IsEnabled = true;
_haptic.Tick();        // Leicht (Ziffern, Tab-Wechsel)
_haptic.Click();       // Mittel (Speichern, CheckIn)
_haptic.HeavyClick();  // Stark (Berechnung, Achievement)
```

### `BackPressHelper`

Double-Back-to-Exit-Logik für Android. Alle App-MainViewModels nutzen diese Klasse statt
eigener Felder.

```csharp
private readonly BackPressHelper _backPressHelper = new();

// Im Konstruktor: Event-Forwarding an VM-eigenes Event
_backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

// Am Ende von HandleBackPressed() (nach app-spezifischen Checks):
return _backPressHelper.HandleDoubleBack(exitMessage);
```

WorkTimePro-Sonderfall: nutzt `FloatingTextRequested` statt `ExitHintRequested`.

### `ICalculationHistoryService` / `CalculationHistoryService`

Persistierte History pro Calculator (max 30 Items). Verwendet von `RechnerPlus`,
`HandwerkerRechner`, `FinanzRechner`, `FitnessRechner`.

- **Live-Calculate-Stutter-Fix**: `ScheduleDebouncedSave()` statt `AddCalculationAsync` direkt
  → 2s-Debounce, nur letztes Resultat pro Calculator wird geschrieben
- **Parallel-Load**: `GetAllHistoryAsync` liest N JSON-Files parallel via `Task.WhenAll`
- **Static `JsonSerializerOptions`**: kein Allocate pro Save

### `IUnitConverterService` / `UnitConverterService`

Einheiten-Umrechnung Länge/Fläche/Volumen/Gewicht (Metrisch/Imperial). Verwendet von
`HandwerkerRechner`.

### `IFileShareService` / `DesktopFileShareService`

Plattform-Abstraktion für Datei-Sharing. Desktop-Default kopiert in Clipboard. Android-Apps
überschreiben via Factory in `App.axaml.cs` mit `AndroidFileShareService` (Linked File aus
`MeineApps.Core.Premium.Ava`).

### `UriLauncher` (Static Helper)

Plattformübergreifender Wrapper für URL-Öffnen und Text-Sharing.

```csharp
UriLauncher.OpenUri(url);              // Desktop: Process.Start mit UseShellExecute, Android: Intent.ActionView
UriLauncher.ShareText(text, title);    // Desktop: Clipboard, Android: Intent.ActionSend
```

Plattform-Hooks `App.PlatformOpenUri` und `App.PlatformShareText` werden in
`MainActivity.cs` (Android) auf die nativen Intents gesetzt.

## Lokalisierung

### `ILocalizationService` / `LocalizationService`

ResourceManager-basiert. Jede App hat eigenes `AppStrings.resx` mit 6 Sprachen
(DE, EN, ES, FR, IT, PT). Service feuert `LanguageChanged`-Event bei Sprachwechsel; alle
ViewModels reagieren via `UpdateLocalizedTexts()`-Methode.

```csharp
var text = _localization.GetString("Key");
_localization.SetLanguage("en");  // Triggert LanguageChanged → alle VMs aktualisieren ihre Strings
```

### `TranslateExtension`

XAML-Markup-Extension für inline-Lokalisierung in AXAML. Selten verwendet (die meisten
Texte werden über ViewModel-Properties gebunden).

```xml
<TextBlock Text="{loc:Translate Key=WelcomeMessage}" />
```

## ViewLocator

VM → View Konvention: `{App}.ViewModels.DashboardViewModel` → `{App}.Views.DashboardView`.
Mobile-Shell-Override: `App.IsMobileShell = true` → sucht `DashboardViewMobile` zuerst,
fällt auf `DashboardView` zurück.

```csharp
// In App.axaml als globale Resource:
<local:ViewLocator />
```

## ViewModelBase

ObservableObject-Basis mit `[RelayCommand]`-Support via CommunityToolkit.Mvvm. Alle App-
spezifischen ViewModels erben davon (statt direkt von `ObservableObject`).

## Converters (Übersicht)

| Konverter | Funktion |
|-----------|----------|
| `BoolToVisibilityConverter` | Bool → IsVisible |
| `InverseBoolConverter` | `!Bool` |
| `BoolToOpacityConverter` | Bool → Opacity (Standard: true=1.0, false=0.4, konfigurierbar) |
| `BoolToBrushConverter` | Bool → Brush |
| `NumberFormatConverter` | Double → "1,234.56" |
| `CurrencyConverter` | Decimal → "€ 1,234.56" |
| `DateTimeFormatConverter` | DateTime → "dd.MM.yyyy" |
| `RelativeTimeConverter` | DateTime → "2 h" (UTC-basiert, Kurzformat) |
| `StringTruncateConverter` | "Long text..." → "Long..." |
| `StringToColorBrushConverter` | "#RRGGBB" → SolidColorBrush (Gray-Fallback, statische `Instance`) |
| `StringToColorConverter` | "#RRGGBB" → Color (Gray-Fallback, für `SolidColorBrush.Color`-Bindings mit Opacity) |

## Behaviors

Behaviors leben in `MeineApps.UI.Behaviors` (kanonische Quelle).
XAML-Import: `xmlns:behaviors="using:MeineApps.UI.Behaviors"`.

## Verweise

- [Haupt-CLAUDE.md](../../../CLAUDE.md) — App-spezifische Farbpaletten, generische Conventions
- [MeineApps.UI/CLAUDE.md](../../UI/MeineApps.UI/CLAUDE.md) — Custom Controls, Behaviors, Skia-Helpers
- [MeineApps.Core.Premium.Ava/CLAUDE.md](../MeineApps.Core.Premium.Ava/CLAUDE.md) — `AndroidFileShareService` (Linked File)
