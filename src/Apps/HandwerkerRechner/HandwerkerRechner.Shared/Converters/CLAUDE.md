# Converters — XAML-Wertkonverter

Namespace: `HandwerkerRechner.Converters`. Kleine, app-spezifische `IValueConverter`-Klassen
für XAML-Bindings. Registriert in `App.axaml` als `<StaticResource>`.

## Dateien

| Datei | Klasse | Zweck |
|-------|--------|-------|
| `IsNotNullConverter.cs` | `IsNotNullConverter` | `value != null` → `bool`. Für `IsVisible`-Bindings auf nullable ViewModels. |
| `IntEqualsConverter.cs` | `IntEqualsConverter` | `int` == `ConverterParameter` → `bool`. Für Tab-Highlighting. Singleton (`Instance`). |

## Nutzung

```xml
<!-- IsNotNullConverter: Calculator-Overlay sichtbar wenn CurrentCalculatorVm != null -->
IsVisible="{Binding CurrentCalculatorVm, Converter={StaticResource IsNotNull}}"

<!-- IntEqualsConverter: Tab aktiv wenn SelectedTab == 0 -->
IsSelected="{Binding SelectedTab, Converter={StaticResource IntEquals}, ConverterParameter=0}"
```

## Gotcha — CommandParameter ist immer string

`IntEqualsConverter` parst `ConverterParameter` als `string` via `int.TryParse()`.
Das ist korrekt — XAML `ConverterParameter="0"` ist IMMER `string`, nie `int`.
Für Commands mit `int`-Parameter: `CommandParameter` auf `string` ändern + `int.TryParse()`
intern im Command (siehe Haupt-CLAUDE.md Troubleshooting-Tabelle).
