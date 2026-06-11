# Graphics — SkiaSharp-Visualisierungen

8 App-eigene SkiaSharp-Visualisierungen + Splash + animierter Hintergrund + Empty-State-Helper
("Professional Dashboard"-Charakter). Nutzen `SkiaThemeHelper` + Helpers aus
[MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).
SkiaSharp-Grundlagen/Gotchas (Paint-Lifecycle, DPI, MaskFilter-Leak) → dort dokumentiert.

## Dateien

| Datei | Zweck |
|-------|-------|
| `DayTimelineVisualization.cs` | 24h-Timeline: Arbeits-/Pausen-Blöcke als farbige Segmente, Stundenticks, Jetzt-Markierung. `static` Klasse mit static Paints. `TimeBlock`-Struct (StartHour/EndHour/IsPause). |
| `WeekBarVisualization.cs` | 7-Tage-Balken: Ist/Soll-Vergleich, grün/gelb/rot je nach Balance. |
| `OvertimeSplineVisualization.cs` | Überstunden-Trend: Tagesbalken (grün/rot) + kumulative Spline-Kurve. |
| `WeekdayRadialVisualization.cs` | Radiales Balkendiagramm Mo–So, gestrichelte Soll-Linie. |
| `WeeklyWorkChartVisualization.cs` | Wöchentliche Arbeitsstunden + Soll-Linie. |
| `MonthlyBarChartVisualization.cs` | Monatsbalken + kumulative Saldo-Kurve. |
| `VacationQuotaGaugeVisualization.cs` | 3 konzentrische Ringe: Genommen/Geplant/Rest (Farben aus `SkiaThemeHelper.Info/Secondary/Accent`). Zentraltext-Farbe grün→gelb→rot nach Verbrauch. |
| `MonthWeekProgressVisualization.cs` | Alle Wochen eines Monats als Gradient-Balken in einem Canvas. |
| `QrStampRenderer.cs` | Stempel-QR-Code (Deep-Link `worktimepro://stamp`, Konstante `StampUri`): `Render()` für die Settings-Vorschau, `CreatePngBytes()` für Teilen/Drucken. Bewusst Schwarz/Weiß (Scanner-Kontrast), QR-Matrix gecacht (Inhalt konstant). Paket: `Net.Codecrete.QrCodeGenerator`. |
| `WorkTimeProSplashRenderer.cs` | "Die Stechuhr": Stempelzyklus (3s) + 10 Business-Partikel. Erbt von `SplashRendererBase`. |
| `WorkspaceBackgroundRenderer.cs` | "Professional Dashboard": 5-Layer animierter Hintergrund (~5fps, 0 GC/Frame). `sealed class`, implementiert `IDisposable`. |
| `ChartEmptyState.cs` | Zentrierter "Keine Daten"-Platzhalter (`static class`). Verhindert, dass leere Karten kaputt wirken. Nutzt `SkiaThemeHelper.TextMuted`. |

Geteilte Controls aus `MeineApps.UI` (nicht hier implementiert):
- `LinearProgressVisualization` (Gradient-Fortschrittsbalken + Glow, in `WeekOverviewView`)
- `DonutChartVisualization` (Pausen, Projekte, Arbeitgeber)
- `SkiaGradientRing` (Tagesfortschritts-Ring auf TodayView)

## TimeBlock-Cache (TodayView)

Geschlossene Segmente (`DayTimelineVisualization`) werden nur bei strukturellen Änderungen
(neue Entries/Pauses/Status-Wechsel) berechnet. Offene Segmente (laufender CheckIn, aktive Pause)
werden pro Frame aus den gespeicherten Start-Timestamps + `DateTime.Now` zusammengesetzt —
verhindert LINQ-Aufruf pro Sekunde im 1s-Update-Zyklus.

## WorkspaceBackgroundRenderer — 0-GC-Regel

5 Layer mit gecachten Paints. `~5fps` genügt für einen subtilen animierten Hintergrund.
KEINE Objekt-Allokation im Render-Pfad — gecachte `SKPaint`-Felder, keine `new SKPaint()` pro Frame.
`Dispose()` muss aufgerufen werden, wenn der Renderer nicht mehr benötigt wird.

## Static Visualizations

`DayTimelineVisualization`, `ChartEmptyState` und die meisten Chart-Klassen sind `static class`
(alle Methoden `static`, keine Instanz-State). Paint-Felder sind `static readonly` initialisiert —
kein Leak, kein Dispose nötig (Prozess-Lifetime).
