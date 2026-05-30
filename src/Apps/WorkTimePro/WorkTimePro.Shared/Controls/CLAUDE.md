# Controls — Custom Avalonia Controls

App-eigene Custom Controls (Avalonia `Control`-Subklassen). Generische Control-Patterns
→ [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `CircularProgressControl.cs` | Kreisförmiger Fortschrittsring (0–100%) für die TodayView. |

## CircularProgressControl

`AvaloniaProperty`-basiertes Custom Control (`Render(DrawingContext)` überschrieben):
- `Progress` (0–100): Sweep-Angle der Arc.
- `TrackBrush`: Hintergrund-Kreis (Default: #333333).
- `ProgressBrush`: Fortschritts-Arc (Default: #4CAF50).
- `StrokeWidth`: Ring-Breite (Default: 6.0).
- `IsPulsing` (bool): Aktiviert Puls-Animation (Opacity + Scale via XAML `Transitions`).

**KRITISCH — Property-Name:** `IsPulsing` (NICHT `IsAnimating`). `AvaloniaObject.IsAnimating()`
ist eine Methode — eine gleichnamige Property würde kollidieren und auf manchen GPU-Treibern
crashen. Jede Umbenennung bricht alle XAML-Bindings in `TodayView.axaml`.

Arc-Geometrie: Start bei 12 Uhr (−90°), Uhrzeigersinn, `SweepDirection.Clockwise`,
`PenLineCap.Round` für abgerundete Enden. `AffectsRender<>` auf allen sichtbaren Properties.
