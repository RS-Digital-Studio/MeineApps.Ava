# Converters — AXAML-Converter

App-eigene `IValueConverter`-Implementierungen für Compiled Bindings.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `ActiveViewEqualsConverter.cs` | `ActiveView`-Enum == `ConverterParameter` → `bool`. Wird für Tab-/View-Sichtbarkeit verwendet (`Classes.Active`-Binding in AXAML). |
| `BoolToOpacityConverter.cs` | `bool` → `double` (true=1.0, false=0.0). Fade-Effekt für Disabled-States. |
| `StringToGameIconKindConverter.cs` | String-Key → `GameIconKind`-Enum. Erlaubt dynamische Icon-Bindungen ohne Code-Behind. |

---

## Nutzungs-Pattern

```axaml
<!-- ActiveView-Converter: Tab-Hervorhebung -->
<Border Classes.Active="{Binding ActiveView,
    Converter={StaticResource ActiveViewEqualsConverter},
    ConverterParameter=Game}" />

<!-- GameIconKind aus String-Binding (z.B. RemoteConfig-Icon-Keys) -->
<icons:GameIcon Kind="{Binding IconKey,
    Converter={StaticResource StringToGameIconKindConverter}}" />
```

`ActiveViewEqualsConverter` ist der Kern des `ActiveView`-Enum-Patterns (statt 17
`IsXxxActive`-Booleans in der Root-CLAUDE.md beschrieben).
