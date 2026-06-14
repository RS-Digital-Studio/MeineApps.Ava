# Converters — XAML-Wertkonverter

Namespace: `HandwerkerRechner.Converters`. Kleine, app-spezifische `IValueConverter`-Klassen
für XAML-Bindings. Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Klasse | Zweck |
|-------|--------|-------|
| `IntEqualsConverter.cs` | `IntEqualsConverter` | `int` == `ConverterParameter` → `bool`. Für Sub-View-Selektion in Multi-Rechner-Views. Singleton (`Instance`). |

## Registrierung

`IntEqualsConverter` wird **lokal pro View** als Ressource registriert (nicht global in `App.axaml`):

```xml
<!-- In der View-Ressourcensektion, z.B. ElectricalView.axaml, GardenView.axaml, RoofSolarView.axaml, MetalView.axaml -->
<conv:IntEqualsConverter x:Key="IntEquals" />

<!-- Nutzung: Panels je nach SelectedCalculator ein-/ausblenden -->
<Panel IsVisible="{Binding SelectedCalculator, Converter={StaticResource IntEquals}, ConverterParameter=0}">
```

## Gotcha — ConverterParameter ist immer string

`IntEqualsConverter` parst `ConverterParameter` als `string` via `int.TryParse()`.
Das ist korrekt — XAML übergibt `ConverterParameter="0"` immer als `string`, nie als `int`.
Ohne `int.TryParse()` würde der Vergleich `value is int` vs. `parameter is string` immer `false`
liefern und alle Panels wären sichtbar oder unsichtbar.
