# Helpers — Formatierung & Icon-Konstanten

Utility-Klassen die keine Abhängigkeiten haben und in mehreren ViewModels/Models genutzt werden.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `TimeFormatter.cs` | Statische Formatierungsmethoden für Zeiten und Status. |
| `Icons.cs` | MDI (Material Design Icons) Codepoint-Konstanten für FontFamily="MDI". |

## TimeFormatter

`static class` — zentrale Formatierung statt Duplikation in ViewModels/Models:

| Methode | Eingabe | Ausgabe | Beispiel |
|---------|---------|---------|---------|
| `FormatMinutes(int)` | Minuten (auch negativ) | `"H:MM"` mit Vorzeichen | `-90` → `"-1:30"` |
| `FormatBalance(int)` | Minuten (auch negativ) | `"+H:MM"` / `"-H:MM"` | `90` → `"+1:30"` |
| `GetStatusName(DayStatus)` | `DayStatus`-Enum | Lokalisierter String | `DayStatus.Vacation` → `AppStrings.DayStatus_Vacation` |

`WorkDay` (Model) importiert `TimeFormatter` direkt (`using static`) für `TargetWorkDisplay`,
`ActualWorkDisplay`, `BalanceDisplay` Properties.

## Icons

`static class` mit `const string` MDI-Codepoints (Unicode-Escapes) für `FontFamily="MDI"`
(materialdesignicons-webfont.ttf v7.x). Kategorien: Navigation, Zeit-Tracking, Work-Status,
Kalender, Charts, Aktionen, Premium, Schichttypen.

**Achtung:** `Icons.*`-Strings sind für Text-Bindings gedacht (zusammen mit MDI-FontFamily).
Für Icon-Controls `<mi:MaterialIcon Kind="..."/>` verwenden — das ist die bevorzugte Methode
(automatisches Sizing, Caching, kein Font-Setup nötig). `Icons.*` nur dort wo das Icon als
Teil eines zusammengesetzten Label-Strings erscheint (z.B. `$"{Icons.Coffee} {AppStrings.Break}"`).
