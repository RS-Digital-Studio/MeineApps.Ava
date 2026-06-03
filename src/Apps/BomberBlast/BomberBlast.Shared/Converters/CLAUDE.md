# Converters — AXAML-Converter

App-eigene `IValueConverter`-Implementierungen für Compiled Bindings.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `ActiveViewEqualsConverter.cs` | `ActiveView`-Enum == `ConverterParameter` → `bool`. Wird für Tab-/View-Sichtbarkeit verwendet (`Classes.Active`-Binding in AXAML). ConverterParameter-Strings werden beim ersten Aufruf in `ActiveView` geparst und in einem `ConcurrentDictionary` gecacht — kein Enum-Parse-Overhead pro Frame. |
| `BoolToOpacityConverter.cs` | `bool` → `double` (true=0.25, false=0.0). Hintergrund-Tint für aktiven Tab. Optionaler `ConverterParameter`-Override (z.B. `"0.5"`) erlaubt andere Intensitäten. |
| `StringToGameIconKindConverter.cs` | String → `GameIconKind`-Enum. Erlaubt dynamische Icon-Bindungen aus ViewModels. Fallback bei unbekanntem String: `GameIconKind.HelpCircle`. |

---

## Nutzungs-Pattern

Alle Converter exponieren eine statische `Instance`-Property und werden per `x:Static` eingebunden
(kein `StaticResource`-Eintrag in App.axaml nötig):

```axaml
<!-- ActiveView-Converter: Tab-Hervorhebung -->
<Border Classes.Active="{Binding ActiveView,
    Converter={x:Static conv:ActiveViewEqualsConverter.Instance},
    ConverterParameter=Game}" />

<!-- GameIconKind aus String-Binding (z.B. ViewModel-Property) -->
<icons:GameIcon Kind="{Binding IconKey,
    Converter={x:Static conv:StringToGameIconKindConverter.Instance}}" />
```

`ActiveViewEqualsConverter` ist der Kern des `ActiveView`-Enum-Patterns — ein einziges Enum
statt vieler `IsXxxActive`-Booleans im ViewModel.
