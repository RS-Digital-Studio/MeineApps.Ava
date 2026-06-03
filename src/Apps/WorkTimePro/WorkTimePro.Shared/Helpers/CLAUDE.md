# Helpers — Formatierung & Dauer-Berechnung

Utility-Klassen die keine Abhängigkeiten haben und in mehreren ViewModels/Models genutzt werden.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `TimeFormatter.cs` | Statische Formatierungsmethoden für Zeiten und Status. |
| `DurationMath.cs` | DST-bewusste Dauer-Berechnung (Arbeitszeit über die Zeitumstellung). |

## TimeFormatter

`static class` — zentrale Formatierung statt Duplikation in ViewModels/Models:

| Methode | Eingabe | Ausgabe | Beispiel |
|---------|---------|---------|---------|
| `FormatMinutes(int)` | Minuten (auch negativ) | `"H:MM"` mit Vorzeichen | `-90` → `"-1:30"` |
| `FormatBalance(int)` | Minuten (auch negativ) | `"+H:MM"` / `"-H:MM"` | `90` → `"+1:30"` |
| `GetStatusName(DayStatus)` | `DayStatus`-Enum | Lokalisierter String | `DayStatus.Vacation` → `AppStrings.DayStatus_Vacation` |

`WorkDay` (Model) importiert `TimeFormatter` direkt (`using static`) für `TargetWorkDisplay`,
`ActualWorkDisplay`, `BalanceDisplay` Properties.

## DurationMath

`static class` für DST-bewusste Dauer-Berechnung. Arbeitszeiten werden als Ortszeit
(`DateTime.Now`) gespeichert; eine naive Subtraktion über die Sommer-/Winterzeit-Umstellung
liefert falsche Werte (Spring-Forward: real 1h weniger, Fall-Back: 1h mehr).

| Methode | Zweck |
|---------|-------|
| `RealElapsed(start, end)` | Tatsächlich verstrichene `TimeSpan`, DST-korrigiert (zieht die UTC-Offset-Differenz ab). |
| `RealElapsedMinutes(start, end)` | Dasselbe als `double` Minuten. |

Nutzt `TimeZoneInfo.GetUtcOffset` (wirft — anders als `ConvertTimeToUtc` — bei mehrdeutigen/
ungültigen Zeiten keine Exception). Zwei UTC-Zeitpunkte werden direkt subtrahiert.
