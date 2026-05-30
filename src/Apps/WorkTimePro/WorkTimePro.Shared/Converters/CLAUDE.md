# Converters — IValueConverter-Implementierungen

App-eigene `IValueConverter` für XAML-Bindings. Alle in `AdditionalConverters.cs`.
Generische Converter-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `AdditionalConverters.cs` | Alle Converter in einer Datei. |

## Converter-Übersicht

| Klasse | Eingabe → Ausgabe | Besonderheit |
|--------|-------------------|--------------|
| `InvertBoolConverter` | `bool` → `!bool` | Bidirektional |
| `IntToBoolConverter` | `int` → `int > 0` | Einwegig |
| `StringToBoolConverter` | `string` → `!IsNullOrEmpty` | Einwegig |
| `NullToBoolConverter` | `object?` → `!= null` | Einwegig |
| `StringNotNullConverter` | `string` → `!IsNullOrEmpty` | Delegiert intern an `StringToBoolConverter`; existiert nur für XAML-Kompatibilität (andere Instanz-Identity als `StringToBoolConverter`). |
| `RoundingDisplayConverter` | `int` (Minuten) → lokalisierter Text | 0 → `AppStrings.NoRounding`, sonst `AppStrings.MinutesShortFormat`. |

## Gotcha — StringNotNullConverter vs. StringToBoolConverter

Beide haben identische Logik. `StringNotNullConverter` existiert nur weil XAML verschiedene
Converter-Instanzen per Typ identifiziert. NICHT zusammenführen — bestehende AXAML-Referenzen
würden brechen.
