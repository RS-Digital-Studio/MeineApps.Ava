# Views — Avalonia-Host für das SkiaSharp-Spiel

Das Views-Verzeichnis enthält nur den Avalonia-Rahmen um das Spiel.
Die gesamte Spiellogik und Darstellung liegt in Engine/Scenes/Overlays/Rendering.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Einzige View: `SKCanvasView` + 60fps `DispatcherTimer`, DPI-skalierte Touch-Koordinaten, KeyDown-Event-Verdrahtung. |
| `MainWindow.axaml(.cs)` | Desktop-only: leerer Fenster-Wrapper (450×800, MinWidth 360, MinHeight 640). `MainView` wird von `Program.cs` als Content gesetzt, nicht per XAML. |

## MainView — Game-Loop-Pattern

```csharp
// StartGameLoop(): Falls bereits laufend, nur _gameLoopTimer?.Stop() —
// NICHT StopGameLoop() aufrufen, da dieses _gameLoopTimer auf null setzt.
_gameLoopTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
_gameLoopTimer.Tick += (_, _) =>
{
    var dt = (float)(currentTicks - _lastFrameTicks) / Stopwatch.Frequency;
    if (dt > 0.05f) dt = 0.05f;   // Max 50ms — verhindert Sprünge bei Alt-Tab/Pause
    _vm?.Update(dt);
    GameCanvas.InvalidateSurface();
};
```

- **`OnAttachedToVisualTree`:** Events verdrahten (`PaintSurface`, `PointerPressed/Moved/Released`, `KeyDown`), dann `InitializeServicesAsync()` (fire-and-forget) und `StartGameLoop()`.
- **`OnDetachedFromVisualTree`:** `StopGameLoop()` (stoppt Timer, setzt `_gameLoopTimer = null`, stoppt `Stopwatch`) + alle Events abmelden inkl. `DataContextChanged`.
- **`DataContextChanged`:** `_vm = DataContext as MainViewModel` — saubere Referenz ohne cast-Fehler.
- **`KeyDown`:** Wird in `OnAttachedToVisualTree` angemeldet und an `_vm?.HandleKeyDown(e.Key)` delegiert (Desktop-Tastatursteuerung).

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
Guard: wenn `_controlWidth/Height < 1` → direkte Übergabe ohne Skalierung.

## Service-Initialisierung

`InitializeServicesAsync()` delegiert an `_vm.InitializeAsync()` (fire-and-forget mit leerem catch).
`TitleScene` + `AssetDownloadScene` funktionieren ohne geladene Daten, daher kein Blocking.
