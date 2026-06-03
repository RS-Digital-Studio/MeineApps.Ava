# Converters — XAML-Binding-Converter

App-spezifische Converter. Generische Converter (BoolToOpacity, NullToVisibility, ...)
liegen in [MeineApps.Core.Ava](../../../../../Libraries/MeineApps.Core.Ava/CLAUDE.md).
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Klassen | Zweck |
|-------|---------|-------|
| `TabColorConverters.cs` | `TabColorConverters` (static), `TabBackgroundConverter`, `TabTextConverter` | Tab-Hintergrund (aktiv = ~20 % Opacity des Theme-Brush, inaktiv = transparent) + Tab-Text (aktiv = `TextPrimaryBrush`, inaktiv = `TextMutedBrush`) |
| `FoodCategoryConverters.cs` | `FoodCategoryToIconConverter`, `FoodCategoryToColorConverter` | `FoodCategory` → `MaterialIconKind` bzw. `IBrush` für Kategorie-Icons in der Lebensmittel-Liste; beide mit statischer `.Instance`-Property |
| `LocalizeKeyConverter.cs` | `LocalizeKeyConverter` | RESX-Key → lokalisierten String via `ILocalizationService` |
| `IsNotNullConverter.cs` | `IsNotNullConverter` | `object? → bool` (null → false) |
| `StringToBoolConverter.cs` | `StringToBoolConverter` | Leerer String → false, sonst true |

---

## `TabColorConverters`

Statische Converter-Instanzen für die 5 Progress-Sub-Tabs:

| Property | Theme-Brush-Key | Sub-Tab |
|----------|-----------------|---------|
| `WeightTab` | `SuccessBrush` (Lila #8B5CF6) | Gewicht |
| `BodyTab` | `InfoBrush` (Blau #3B82F6) | BMI |
| `BodyFatTab` | `WarningBrush` (Amber) | Körperfett |
| `WaterTab` | `InfoBrush` (Blau) | Wasser |
| `CaloriesTab` | `ErrorBrush` (Rot) | Kalorien |
| `ActiveText` | `TextPrimaryBrush` / `TextMutedBrush` | Tab-Beschriftung |

**Gotcha:** `TabBackgroundConverter` löst den Brush-Key per `Application.Current.TryGetResource`
zur Laufzeit auf — es wird ein neues `SolidColorBrush` mit Alpha 50 (~20 % Opacity) erzeugt.
Da Tab-Switching selten ist, ist die Allokation pro Convert-Aufruf akzeptabel.
Fallback bei fehlendem Resource-Key: `SolidColorBrush(50, 100, 100, 255)`.

---

## `FoodCategoryToColorConverter`

Statische `IBrush`-Felder auf Klassenebene (nicht im `Convert`) — verhindert Allokation
bei jeder ListView-Aktualisierung.

12 explizite Kategorien + Default (Grau `#6B7280`):

| Kategorie | Farbe |
|-----------|-------|
| Fruit | Grün `#22C55E` |
| Vegetable | Hellgrün `#84CC16` |
| Meat | Rot `#EF4444` |
| Fish | Blau `#3B82F6` |
| Dairy | Gelb `#EAB308` |
| Grain | Amber `#D97706` |
| Beverage | Cyan `#06B6D4` |
| Snack | Amber `#F59E0B` |
| FastFood | Orange `#F97316` |
| Sweet | Pink `#EC4899` |
| Nut | Braun `#A16207` |
| Legume | Dunkelgrün `#65A30D` |

---

## `LocalizeKeyConverter`

**Gotcha:** Nutzt intern `App.Services.GetRequiredService<ILocalizationService>()` (Service-Locator).
Das ist ein bewusstes Zugeständnis, da Converter kein Constructor Injection unterstützen —
Converter-Instanzen werden von Avalonia im XAML-Kontext erzeugt, nicht per DI. Bei
fehlgeschlagener Auflösung wird der Key als Fallback zurückgegeben.
