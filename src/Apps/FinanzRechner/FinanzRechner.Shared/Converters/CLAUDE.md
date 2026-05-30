# Converters — IValueConverter-Implementierungen

18 Converter für die spezifischen Darstellungs-Anforderungen des Finanz-Trackers.
Alle als `public class XxxConverter : IValueConverter` implementiert, viele mit
statischer `Instance`-Property für XAML-Ressourcen-Wiederverwendung.
Generische Converter-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Konvertierung |
|-------|---------------|
| `CategoryToIconConverter.cs` | `ExpenseCategory` → Emoji-Icon-String (delegiert an `CategoryLocalizationHelper.GetCategoryIcon`) |
| `CategoryToStringConverter.cs` | `ExpenseCategory` → lokalisierter Name (delegiert an `CategoryLocalizationHelper.GetLocalizedName`) |
| `CategoryToColorBrushConverter.cs` | `ExpenseCategory` (oder `CustomCategoryId`) → farbiger `SolidColorBrush` für Kategorie-Chips |
| `TransactionTypeToColorConverter.cs` | `TransactionType` → Farbe (Income=grün, Expense=rot, Transfer=cyan) |
| `TransactionTypeToggleColorConverter.cs` | `TransactionType` → Toggle-Button Akzentfarbe |
| `TransactionTypeToPrefixConverter.cs` | `TransactionType` → Vorzeichen-String (`+` / `-` / `⇄`) |
| `TransactionTypeFontAttributesConverter.cs` | `TransactionType` → FontWeight (Income=Bold) |
| `TransactionTypeToValueConverter.cs` | `(TransactionType, decimal Amount)` → vorzeichenbehafteter Wert |
| `AlertLevelToColorConverter.cs` | `BudgetAlertLevel` → Farbe (Safe=grün, Warning=gelb, Exceeded=rot) |
| `AlertLevelToBoxShadowConverter.cs` | `BudgetAlertLevel` → `BoxShadows` (Glow-Stärke je Status) |
| `BalanceToColorConverter.cs` | `decimal` → Farbe (positiv=grün, negativ=rot, null=grau) |
| `FilterTypeToStringConverter.cs` | `FilterType`-Enum → lokalisierter Filter-Label |
| `SortOptionToStringConverter.cs` | `SortOption`-Enum → lokalisierter Sort-Label |
| `PatternToStringConverter.cs` | `RecurringPattern`-Enum → lokalisierter Wiederholungs-Label |
| `BoolToStringConverter.cs` | `bool` → konfigurierbarer Text (Parameter: "TrueText|FalseText") |
| `BoolToResourceColorConverter.cs` | `bool` → Farbe aus AppPalette-Ressource |
| `BoolToDoubleConverter.cs` | `bool` → `double` (z.B. Opacity, Scale) via Parameter |
| `EnumToBoolConverter.cs` | `Enum`-Wert == Parameter → `bool` (Radio-Button-Binding) |

---

## Semantic-Brushes (NIEMALS Hex-Literale in Views)

Für Income/Expense/Transfer IMMER `{DynamicResource …}` nutzen statt direkter Hex-Werte
— damit Farb-Änderungen zentral in `Themes/AppPalette.axaml` greifen:

```xml
IncomeBrush          #22C55E
ExpenseBrush         #EF4444
TransferBrush        #06B6D4
IncomeBackgroundBrush   #3322C55E  (20 % Alpha)
ExpenseBackgroundBrush  #33EF4444
TransferBackgroundBrush #3306B6D4
```

---

## Gotcha — AlertLevelToBoxShadowConverter

Wird auch für Budget-Cards ohne expliziten Status genutzt — Default-Branch gibt immer
den Safe-Glow zurück (`BoxShadows.Parse("0 0 8 0 #3022C55E")`), niemals `null` (sonst
Compiler-Fehler bei nicht-nullbarer `BoxShadows`-Struct).
