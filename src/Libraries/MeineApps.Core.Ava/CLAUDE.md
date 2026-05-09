# MeineApps.Core.Ava — Shared Core Library

> Für app-spezifische Patterns siehe die jeweilige App-CLAUDE.md.
> App-spezifische Farbpaletten sind in [Haupt-CLAUDE.md](../../../CLAUDE.md) dokumentiert.

Shared Library für alle Avalonia Apps. Enthält Preferences-Persistenz, Plattform-
Abstraktionen, Converters und Design-Tokens.

## Struktur

```
MeineApps.Core.Ava/
├── Services/
│   ├── IPreferencesService + PreferencesService     # JSON-basiert, %APPDATA%/{AppName}/preferences.json
│   ├── IHapticService + NoOpHapticService           # Interface + Desktop-Fallback
│   └── BackPressHelper                              # Double-Back-to-Exit (alle Apps)
├── Themes/
│   └── ThemeColors.axaml                            # Design Tokens (Spacing, Radius, Fonts)
└── Converters/
    ├── BoolConverters.cs
    ├── StringConverters.cs
    ├── NumberConverters.cs
    ├── DateTimeConverters.cs
    └── ColorConverters.cs
```

## Design Tokens

```axaml
<!-- Spacing -->
SpacingSm: 8px
SpacingMd: 12px
SpacingLg: 16px
SpacingXl: 24px

<!-- Radius -->
RadiusSm: 4px
RadiusMd: 8px
RadiusLg: 12px

<!-- Typography -->
FontSizeBodyMd: 14px
FontSizeTitleLg: 22px
FontSizeHeadlineMd: 28px
```

## Services

### PreferencesService
```csharp
// Speichert in %APPDATA%/{AppName}/preferences.json
IPreferencesService _prefs;

_prefs.Set("key", value);
var val = _prefs.Get<string>("key", "default");
```

### IHapticService
```csharp
// Plattform-Abstraktion für haptisches Feedback (Vibration)
// Interface: IsEnabled, Tick(), Click(), HeavyClick()
// NoOpHapticService: Desktop-Fallback (leere Methoden)
// Android: Jede App hat eigene AndroidHapticService-Implementierung
IHapticService _haptic;

_haptic.IsEnabled = true;
_haptic.Tick();       // Leichtes Feedback (Ziffern, Tab-Wechsel)
_haptic.Click();      // Mittleres Feedback (Speichern, CheckIn)
_haptic.HeavyClick(); // Starkes Feedback (Berechnung, Achievement)
```

### BackPressHelper
```csharp
// Double-Back-to-Exit Logik (Android-Zurücktaste)
// Alle 8 MainViewModels nutzen diese Klasse statt eigener Felder
private readonly BackPressHelper _backPressHelper = new();

// Im Konstruktor: Event an VM-eigenes Event weiterleiten
_backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);
// WorkTimePro Sonderfall: nutzt FloatingTextRequested statt ExitHintRequested
_backPressHelper.ExitHintRequested += msg => FloatingTextRequested?.Invoke(msg, "info");

// Am Ende von HandleBackPressed() (nach app-spezifischen Checks):
return _backPressHelper.HandleDoubleBack(exitMessage);
```

## Converters

- `BoolToVisibilityConverter` - Bool → IsVisible
- `InverseBoolConverter` - !Bool
- `BoolToOpacityConverter` - Bool → Opacity (Standard: true=1.0, false=0.4, konfigurierbar)
- `BoolToBrushConverter` - Bool → Brush
- `NumberFormatConverter` - Double → "1,234.56"
- `CurrencyConverter` - Decimal → "€ 1,234.56"
- `DateTimeFormatConverter` - DateTime → "dd.MM.yyyy"
- `RelativeTimeConverter` - DateTime → "2 h" (UTC-basiert, Kurzformat)
- `StringTruncateConverter` - "Long text..." → "Long..."
- `StringToColorBrushConverter` - "#RRGGBB" → SolidColorBrush (Gray Fallback, statische `Instance` Property)
- `StringToColorConverter` - "#RRGGBB" → Color (Gray Fallback, für SolidColorBrush.Color Bindings mit Opacity)

## Behaviors

Behaviors leben in `MeineApps.UI.Behaviors` (kanonische Quelle).
XAML-Import: `xmlns:behaviors="using:MeineApps.UI.Behaviors"`.

## Verweise

- [Haupt-CLAUDE.md](../../../CLAUDE.md) — App-spezifische Farbpaletten, generische Conventions
- [MeineApps.UI/CLAUDE.md](../../UI/MeineApps.UI/CLAUDE.md) — Behaviors, Custom Controls, Skia-Helpers
