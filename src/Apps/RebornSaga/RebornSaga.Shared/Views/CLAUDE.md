# Views — Avalonia-Host für das SkiaSharp-Spiel

Das Views-Verzeichnis enthält nur den Avalonia-Rahmen um das Spiel.
Die gesamte Spiellogik und Darstellung liegt in Engine/Scenes/Overlays/Rendering.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Einzige View: `SKCanvasView` + 60fps `DispatcherTimer`, DPI-skalierte Touch-Koordinaten, Event-Verdrahtung. |
| `MainWindow.axaml(.cs)` | Desktop-only: Fenster-Wrapper für `MainView`. |

## MainView — Game-Loop-Pattern

```csharp
// OnAttachedToVisualTree: Game-Loop starten
_gameLoopTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
_gameLoopTimer.Tick += (_, _) =>
{
    var dt = (float)(currentTicks - _lastFrameTicks) / Stopwatch.Frequency;
    if (dt > 0.05f) dt = 0.05f;   // Max 50ms — verhindert Sprünge bei Alt-Tab/Pause
    _vm?.Update(dt);
    GameCanvas.InvalidateSurface();
};
```

- **Timer-Start:** Nur `_gameLoopTimer?.Stop()` (kein `StopGameLoop()` — das nullt Canvas-Referenz).
- **`OnDetachedFromVisualTree`:** `StopGameLoop()` + alle Events abmelden.
- **`DataContextChanged`:** `_vm = DataContext as MainViewModel` — saubere Referenz ohne cast-Fehler.

## DPI-skalierte Touch-Koordinaten (KRITISCH)

```csharp
// FALSCH: e.Info.Width/Height → physische Pixel (DPI > 1 → zu groß)
// RICHTIG: canvas.LocalClipBounds → sichtbarer Bereich
private SKPoint GetSkiaPoint(PointerEventArgs e)
{
    var pos = e.GetPosition(GameCanvas);
    var scaleX = _lastBounds.Width / (float)_controlWidth;
    var scaleY = _lastBounds.Height / (float)_controlHeight;
    return new SKPoint((float)pos.X * scaleX, (float)pos.Y * scaleY);
}
```

`_lastBounds` wird in `OnPaintSurface` aus `canvas.LocalClipBounds` gesetzt.
`_controlWidth/Height` aus `GameCanvas.Bounds.Width/Height` (Avalonia-Koordinaten).

## Service-Initialisierung

`InitializeServicesAsync()` in `OnAttachedToVisualTree` — fire-and-forget mit try/catch.
`TitleScene` + `AssetDownloadScene` funktionieren ohne geladene Daten, daher kein Blocking.
