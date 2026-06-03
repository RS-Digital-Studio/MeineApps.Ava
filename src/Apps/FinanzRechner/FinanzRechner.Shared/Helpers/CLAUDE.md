# Helpers — Quer-schneidende Hilfsklassen

Statische Hilfsklassen für Aufgaben, die in mehreren ViewModels und Convertern gebraucht
werden. Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `CategoryLocalizationHelper.cs` | Zentrale Quelle für Kategorie-Icons, -Farben und -Namen (6 Sprachen) |
| `CurrencyHelper.cs` | Globale Währungs-Formatierung (einmalig konfiguriert, kein DI nötig) |

---

## CategoryLocalizationHelper

```csharp
GetCategoryIcon(ExpenseCategory)    → Emoji-String
GetCategoryColor(ExpenseCategory)   → SKColor (für Charts)
GetCategoryKey(ExpenseCategory)     → RESX-Key
GetLocalizedName(ExpenseCategory, ILocalizationService?)  → lokalisierter Name
```

Alle Duplikate (früher in ViewModels und Convertern einzeln) wurden hier zentralisiert.
`GetCategoryIcon` und `GetCategoryColor` werden direkt von `CategoryToIconConverter` und
den SkiaSharp-Chart-Renderern aufgerufen.

---

## CurrencyHelper

Konfiguriert **einmalig in der Loading-Pipeline** (Schritt 1):

```csharp
var preset = CurrencySettings.Presets.FirstOrDefault(p => p.CurrencyCode == currencyCode);
if (preset != null)
    CurrencyHelper.Configure(preset);
```

16 Währungs-Presets. Symbol-Position und Dezimalformat automatisch korrekt.
Keine DI-Registrierung nötig — globaler statischer Zustand (Währung ändert sich nur bei
expliziter Nutzer-Aktion in Settings).

**Overloads für decimal + double:** `Format`, `FormatSigned`, `FormatCompactSigned`,
`FormatAxis`, `FormatInvariant` existieren für beide Typen. Bei Aufrufen mit Literal `0`
immer disambiguieren: `CurrencyHelper.Format(0m)` statt `Format(0)`.

**Hilfsmethoden für `CountUpBehavior`:** `GetSuffix()` gibt `" €"` (Symbol nach Betrag)
bzw. `""` zurück; `GetPrefix()` gibt `"$"` (Symbol vor Betrag) bzw. `""` zurück.
Damit kann das Behavior das Symbol korrekt anhängen ohne die Zahl selbst zu formatieren.
