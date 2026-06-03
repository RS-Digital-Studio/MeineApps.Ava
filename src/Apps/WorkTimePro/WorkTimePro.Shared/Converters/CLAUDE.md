# Converters — IValueConverter-Implementierungen

App-eigene `IValueConverter` für XAML-Bindings. Alle in einer einzigen Datei (`AdditionalConverters.cs`).
Generische Converter-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Converter-Übersicht

| Klasse | Eingabe → Ausgabe | Besonderheit |
|--------|-------------------|--------------|
| `InvertBoolConverter` | `bool` → `!bool` | Bidirektional für `bool`-Werte; bei `null`/Nicht-bool → `DoNothing` (kein Hard-`false`, sonst kippt invertierte `IsVisible`-Logik bei null-Quelle). |
| `IntToBoolConverter` | numerisch → `> 0` | Akzeptiert `int`, `long` und `double` — nicht nur `int`, sonst `false` bei `long`/`double`-Quellen. Einwegig. |
| `StringToBoolConverter` | `string` → `!IsNullOrEmpty` | Einwegig. |
| `NullToBoolConverter` | `object?` → `!= null` | Einwegig. |
| `StringNotNullConverter` | `string` → `!IsNullOrEmpty` | Delegiert intern an `StringToBoolConverter`. Existiert nur für XAML-Kompatibilität — verschiedene Converter-Typen haben verschiedene Instanz-Identität in AXAML. Nicht mit `StringToBoolConverter` zusammenführen. |
| `RoundingDisplayConverter` | `int` (Minuten) → lokalisierter Text | `0` → `AppStrings.NoRounding`, sonst `AppStrings.MinutesShortFormat`. Einwegig. |

## Gotcha — StringNotNullConverter vs. StringToBoolConverter

Beide Klassen haben identische Logik. `StringNotNullConverter` darf **nicht** entfernt werden —
bestehende AXAML-Bindings referenzieren den Typ, und XAML unterscheidet Converter-Instanzen per
Typ-Identität. Eine Zusammenführung würde alle Verwendungsstellen von `StringNotNullConverter`
zur Compile-Zeit brechen.
