# Converters — AXAML-Wert-Konverter

App-eigene `IValueConverter`-Implementierungen für Bindings die Core-Library-Converter
nicht abdecken.

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `MoneyDisplayConverter.cs` | Formatiert `decimal` → Geld-Anzeige über `MoneyFormatter`; ConverterParameter: `"perhour"`, `"persecond"`, `"~"` (Prefix), Default = kompakt |
| `WorkshopColorConverter.cs` | `WorkshopType` → `SolidColorBrush`. ConverterParameter: `"bg"` (20% Opacity), `"bg40"` (40%), `"bg60"` (60%), Default = voll opak. Brushes gecacht (max 40 Einträge: 10 Typen × 4 Alpha-Varianten) |
| `StringToGameIconKindConverter.cs` | `string` → `GameIconKind` für XAML-Bindings auf string-Properties |
| `GreaterThanZeroConverter.cs` | `int/double/decimal/long/float` → `bool` (für IsVisible-Bindings auf Zählern) |
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
