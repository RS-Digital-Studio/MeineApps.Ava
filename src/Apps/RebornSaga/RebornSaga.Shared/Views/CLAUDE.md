# Views — Avalonia-Host für das SkiaSharp-Spiel

Das Views-Verzeichnis enthält nur den Avalonia-Rahmen um das Spiel.
Die gesamte Spiellogik und Darstellung liegt in Engine/Scenes/Overlays/Rendering.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Einzige View: `SKCanvasView` + ~30fps `DispatcherTimer` (Bedarfs-Rendering), DPI-skalierte Touch-Koordinaten, KeyDown-Event-Verdrahtung. |
| `MainWindow.axaml(.cs)` | Desktop-only: leerer Fenster-Wrapper (450×800, MinWidth 360, MinHeight 640). `MainView` wird von `Program.cs` als Content gesetzt, nicht per XAML. |

## MainView — Game-Loop-Pattern

```csharp
// StartGameLoop(): Falls bereits laufend, nur _gameLoopTimer?.Stop() —
// NICHT StopGameLoop() aufrufen, da dieses _gameLoopTimer auf null setzt.
_gameLoopTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameIntervalMs) };
_gameLoopTimer.Tick += (_, _) =>
{
    var dt = (float)(currentTicks - _lastFrameTicks) / Stopwatch.Frequency;
    if (dt > 0.05f) dt = 0.05f;   // Max 50ms — verhindert Sprünge bei Alt-Tab/Pause
    _vm?.Update(dt);                       // Logik IMMER (Timer/Cooldowns laufen weiter)
    if (_vm == null || _vm.ShouldRender()) // Paint nur bei Bedarf (Akku)
        GameCanvas.InvalidateSurface();
};
```

**30-FPS-Cap (`FrameIntervalMs = 33`, benannte Konstante):** Der Loop läuft mit ~30fps statt
60fps. Verlustfrei, weil `deltaTime` aus der echten Stopwatch-Zeit kommt (nicht hartkodiert) —
Spielgeschwindigkeit und Animations-Timing bleiben identisch, nur die Bildrate halbiert sich.

**Bedarfs-Rendering:** `_vm.Update(dt)` läuft jeden Tick (Logik), aber `InvalidateSurface()`
wird nur aufgerufen, wenn `MainViewModel.ShouldRender()` (delegiert an `SceneManager.ShouldRender()`)
true liefert — statische Szenen ohne Animation/Zustandsänderung sparen den teuren Paint.
Mechanik (`NeedsContinuousRender`/`RequestRedraw`) → [Engine/CLAUDE.md](../Engine/CLAUDE.md).

**`ConfigureSpriteTargetHeight()`** (in `OnAttachedToVisualTree`): ermittelt die physische
Display-Pixelhöhe (`TopLevel.Screens`/`RenderScaling`, Portrait → längere Kante) und gibt sie an
`SpriteCache.SetTargetDisplayHeight()`. Damit dekodiert der SpriteCache Sprites nie höher als sie
dargestellt werden (Akku/RAM). Fehlerpfade unkritisch — der SpriteCache hat einen konservativen
Default (volle Auflösung). Detail → [Services/CLAUDE.md](../Services/CLAUDE.md).

- **`OnAttachedToVisualTree`:** Events verdrahten (`PaintSurface`, `PointerPressed/Moved/Released`, `KeyDown`), `ConfigureSpriteTargetHeight()`, dann `InitializeServicesAsync()` (fire-and-forget) und `StartGameLoop()`.
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
