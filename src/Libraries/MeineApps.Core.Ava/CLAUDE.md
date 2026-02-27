# MeineApps.Core.Ava - Core Library

## Zweck
Shared Library für alle Avalonia Apps:
- ThemeService (4 Themes)
- PreferencesService (JSON-basiert)
- Converters (Bool, String, Number, DateTime)
- Behaviors (TapScale, FadeIn)

## Struktur

```
MeineApps.Core.Ava/
├── Services/
│   ├── IThemeService.cs
│   ├── ThemeService.cs
│   ├── IPreferencesService.cs
│   └── PreferencesService.cs
├── Themes/
│   ├── ThemeColors.axaml       # Design Tokens
│   ├── MidnightTheme.axaml     # Dark, Indigo
│   ├── AuroraTheme.axaml       # Dark, Colorful
│   ├── DaylightTheme.axaml     # Light, Blue
│   └── ForestTheme.axaml       # Dark, Green
├── Converters/
│   ├── BoolConverters.cs
│   ├── StringConverters.cs
│   ├── NumberConverters.cs
│   ├── DateTimeConverters.cs
│   └── ColorConverters.cs
└── Behaviors/
    ├── TapScaleBehavior.cs
    └── FadeInBehavior.cs
```

## Themes

### Midnight (Default)
- Primary: #6366F1 (Indigo)
- Background: #0F172A (Slate 900)
- Inspiration: Discord, VS Code

### Aurora
- Primary: #EC4899 (Pink)
- Gradient: Pink → Violet → Cyan
- Inspiration: Spotify, Instagram

### Daylight
- Primary: #2563EB (Blue)
- Background: #F8FAFC (Light)
- Inspiration: Apple, Google

### Feature Accent Colors (alle Themes)
- TimerAccentColor/Brush: Amber-Töne (Timer-Feature)
- StopwatchAccentColor/Brush: Cyan-Töne (Stoppuhr-Feature)
- AlarmAccentColor/Brush: Violet-Töne (Wecker-Feature)
- PomodoroAccentColor/Brush: Rot-Töne (Pomodoro-Feature)

### Forest
- Primary: #10B981 (Emerald)
- Background: #022C22 (Dark Green)
- Inspiration: Notion Dark, Nature

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

### ThemeService
```csharp
// Inject
IThemeService _themeService;

// Set Theme
_themeService.SetTheme(AppTheme.Aurora);

// Get Current
var current = _themeService.CurrentTheme;
var isDark = _themeService.IsDarkTheme;
```

### PreferencesService
```csharp
// Speichert in %APPDATA%/{AppName}/preferences.json
IPreferencesService _prefs;

_prefs.Set("key", value);
var val = _prefs.Get<string>("key", "default");
```

## Converters

- `BoolToVisibilityConverter` - Bool → IsVisible
- `InverseBoolConverter` - !Bool
- `BoolToBrushConverter` - Bool → Brush
- `NumberFormatConverter` - Double → "1,234.56"
- `CurrencyConverter` - Decimal → "€ 1,234.56"
- `DateTimeFormatConverter` - DateTime → "dd.MM.yyyy"
- `RelativeTimeConverter` - DateTime → "2 hours ago"
- `StringTruncateConverter` - "Long text..." → "Long..."
- `StringToColorBrushConverter` - "#RRGGBB" → SolidColorBrush (Gray Fallback, statische `Instance` Property)
- `StringToColorConverter` - "#RRGGBB" → Color (Gray Fallback, für SolidColorBrush.Color Bindings mit Opacity)

## Behaviors

### TapScaleBehavior
```axaml
<Border>
  <Interaction.Behaviors>
    <behaviors:TapScaleBehavior PressedScale="0.95" Duration="100" />
  </Interaction.Behaviors>
</Border>
```

### FadeInBehavior
```axaml
<Border>
  <Interaction.Behaviors>
    <behaviors:FadeInBehavior Duration="300" Delay="100" />
  </Interaction.Behaviors>
</Border>
```
