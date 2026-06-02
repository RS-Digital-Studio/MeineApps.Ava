# Converters — AXAML-Wert-Konverter

App-eigene `IValueConverter`-Implementierungen für Bindings die Core-Library-Converter
nicht abdecken.

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `MoneyDisplayConverter.cs` | Formatiert `double` → lokalisierte Geld-Anzeige mit K/M/B-Suffix |
| `WorkshopColorConverter.cs` | `WorkshopType` → `SolidColorBrush`. Einzige erlaubte Quelle neben `WorkshopTypeExtensions.GetColorHex()` |
| `StringToGameIconKindConverter.cs` | `string` → `GameIconKind` für XAML-Bindings auf string-Properties |
| `GreaterThanZeroConverter.cs` | `double/int` → `bool` (für IsVisible-Bindings auf Zählern) |
| `BoolToChallengeBackgroundConverter.cs` | `bool IsActive` → Challenge-Hintergrundfarbe (aktiviert vs. inaktiv) |

---

## Gotcha — WorkshopColorConverter ist NICHT die Quelle der Wahrheit

Die einzige Quelle für Workshop-Farben ist `WorkshopTypeExtensions.GetColorHex()`.
`WorkshopColorConverter` ruft intern diese Methode auf — niemals in einem Converter
oder Renderer eigene Farb-Definitionen hinzufügen.

Alle Consumer leiten von `GetColorHex()` ab:
`WorkshopCardRenderer.GetWorkshopColor()`, `WorkshopSceneRenderer`, `WorkshopColorConverter`, `WorkshopGameCardRenderer`.

## Gotcha — ConverterParameter für BoolToBrush

Für Use-Cases wo `BoolToBrushConverter` aus `MeineApps.Core.Ava` keinen `ConverterParameter`
unterstützt (z.B. Heirloom-Selection-Farben), werden dedizierte Converter-Instanzen als
App.axaml-Resources angelegt (`HeirloomSelectedBgConverter`, `HeirloomSelectedBorderConverter`).
NICHT versuchen den Core-Converter zu erweitern — das bricht die Library-Boundary.
