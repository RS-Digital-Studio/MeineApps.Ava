# MeineApps.Core.Ava - Core Library

## Zweck
Shared Library für alle Avalonia Apps:
- PreferencesService (JSON-basiert)
- Converters (Bool, String, Number, DateTime)
- Design Tokens (ThemeColors.axaml)
- HINWEIS: Behaviors (TapScale, FadeIn) wurden nach MeineApps.UI verschoben

## Struktur

```
MeineApps.Core.Ava/
├── Services/
│   ├── IPreferencesService.cs
│   ├── PreferencesService.cs
│   ├── IHapticService.cs          # Interface + NoOpHapticService (zentral für alle Apps)
│   ├── BackPressHelper.cs         # Double-Back-to-Exit Logik (verwendet von allen 8 Apps)
├── Themes/
│   └── ThemeColors.axaml       # Design Tokens (Spacing, Radius, Fonts)
├── Converters/
│   ├── BoolConverters.cs
│   ├── StringConverters.cs
│   ├── NumberConverters.cs
│   ├── DateTimeConverters.cs
│   └── ColorConverters.cs
└── (Behaviors/ entfernt → MeineApps.UI.Behaviors nutzen)
```

## App-spezifische Farbpaletten

Jede App hat eine eigene `Themes/AppPalette.axaml` im Shared-Projekt, die statisch in App.axaml geladen wird. Kein dynamischer Theme-Wechsel mehr.

| App | Primary | Charakter |
|-----|---------|-----------|
| RechnerPlus | #7C7FF7 (Indigo) | Retro-Tech Calculator |
| ZeitManager | #F7A833 (Amber) | Warme Zeitverwaltung |
| FinanzRechner | #10B981 (Smaragd) | Living Finance |
| FitnessRechner | #06B6D4 (Cyan) | VitalOS Medical |
| HandwerkerRechner | #3B82F6 (Blau) | Blueprint Professional |
| WorkTimePro | #4F8BF9 (Blau) | Professional Workspace |
| HandwerkerImperium | #D97706 (Amber) | Warme Werkstatt |
| BomberBlast | #FF6B35 (Orange) | Neon Arcade |

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

## Behaviors (ENTFERNT)

Behaviors wurden nach `MeineApps.UI.Behaviors` verschoben (kanonische Quelle).
Alle XAML-Imports müssen `xmlns:behaviors="using:MeineApps.UI.Behaviors"` verwenden.
