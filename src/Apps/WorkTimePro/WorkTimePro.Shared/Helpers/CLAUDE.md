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

`WorkDay`, `WorkWeek` und `WorkMonth` importieren `TimeFormatter` per `using static WorkTimePro.Helpers.TimeFormatter`
für alle Display-Properties (`TargetWorkDisplay`, `ActualWorkDisplay`, `BalanceDisplay`, …).

## DurationMath

`static class` für DST-bewusste Dauer-Berechnung. Arbeitszeiten werden als Ortszeit
(`DateTime.Now`) gespeichert; eine naive Subtraktion über die Sommer-/Winterzeit-Umstellung
liefert falsche Werte (Spring-Forward: real 1h weniger, Fall-Back: 1h mehr).

| Methode | Zweck |
|---------|-------|
| `RealElapsed(start, end)` | Tatsächlich verstrichene `TimeSpan`, DST-korrigiert. Sind beide Zeitpunkte `DateTimeKind.Utc`, direkte Differenz (kein Offset-Abzug nötig). Sonst: `(end - start) - (offset(end) - offset(start))`. |
| `RealElapsedMinutes(start, end)` | Dasselbe als `double` Minuten (`TotalMinutes`). |

Nutzt `TimeZoneInfo.GetUtcOffset` statt `ConvertTimeToUtc`, weil Letzteres bei mehrdeutigen oder
ungültigen lokalen Zeiten (z.B. übersprungene Stunde beim Spring-Forward) eine Exception wirft.
