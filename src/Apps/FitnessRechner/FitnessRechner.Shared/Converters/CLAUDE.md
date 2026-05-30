# Converters — XAML-Binding-Converter

App-spezifische Converter. Generische Converter (BoolToOpacity, NullToVisibility, ...)
liegen in [MeineApps.Core.Ava](../../../../../Libraries/MeineApps.Core.Ava/CLAUDE.md).
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Klassen | Zweck |
|-------|---------|-------|
| `TabColorConverters.cs` | `TabColorConverters` (static), `TabBackgroundConverter`, `TabTextConverter` | Tab-Hintergrund (aktiv = 20% Theme-Brush, inaktiv = transparent) + Tab-Text (aktiv = TextPrimaryBrush, inaktiv = TextMutedBrush) |
| `FoodCategoryConverters.cs` | `FoodCategoryToIconConverter`, `FoodCategoryToColorConverter` | `FoodCategory` → `MaterialIconKind` bzw. `IBrush` für Kategorie-Icons in der Lebensmittel-Liste |
| `LocalizeKeyConverter.cs` | `LocalizeKeyConverter` | RESX-Key → lokalisierten String via `ILocalizationService` |
| `IsNotNullConverter.cs` | `IsNotNullConverter` | `object? → bool` (null → false) |
| `StringToBoolConverter.cs` | `StringToBoolConverter` | Leerer String → false, sonst true |

---

## `TabColorConverters`

Statische Converter-Instanzen für die 5 Progress-Sub-Tabs:

| Property | Theme-Brush | Verwendung |
|----------|-------------|------------|
| `WeightTab` | `SuccessBrush` (Lila #8B5CF6) | Gewicht-Sub-Tab |
| `BodyTab` | `InfoBrush` (Blau #3B82F6) | BMI-Sub-Tab |
| `BodyFatTab` | `WarningBrush` (Amber) | Körperfett-Sub-Tab |
| `WaterTab` | `InfoBrush` (Blau) | Wasser/Kalorien-Sub-Tab |
| `CaloriesTab` | `ErrorBrush` (Rot) | Kalorien-Sub-Tab |
| `ActiveText` | PrimaryBrush / TextMutedBrush | Tab-Beschriftung |

**Gotcha:** `TabBackgroundConverter` löst den Brush-Key per `Application.Current.TryGetResource`
zur Laufzeit auf — Brush-Objekte werden bei jedem Convert-Aufruf neu erzeugt (bewusste
Entscheidung, da Tab-Switching selten). Fallback: `SolidColorBrush(50, 100, 100, 255)`.

---

## `FoodCategoryToColorConverter`

Statische `IBrush`-Felder im Klassen-Initializer (nicht im Convert — verhindert Allokation
bei jeder ListView-Aktualisierung). Frühere Version hatte CS1717 Self-Assignments
(`FruitBrush = FruitBrush`) die null-Brushes erzeugten — gefixt.

13 Kategorien: Fruit (Grün), Vegetable (Hellgrün), Meat (Rot), Fish (Blau), Dairy (Gelb),
Grain (Amber), Beverage (Cyan), Snack (Amber), FastFood (Orange), Sweet (Pink), Nut (Braun),
Legume (Dunkelgrün), Other (Grau).
