---
name: Code-Behind-Pattern SmartMeasure
description: Welche Views Code-Behind brauchen und warum das KEIN MVVM-Verstoss ist
type: project
---

SmartMeasure hat 6 Views mit nicht-trivialem Code-Behind. Alle legitim:

- **MainView.axaml.cs**: Lazy MapView-Erstellung (Mapsui GL-Crash auf Android) via `MapContainer.PropertyChanged`. Dokumentiert in CLAUDE.md.
- **SurveyView.axaml.cs**: SKCanvasView Compass-Render-Hook (`PaintSurface` + `CompassInvalidateRequested`-Event-Subscription via `DataContextChanged`).
- **TerrainView.axaml.cs**: SKCanvasView 3D-Render + Pointer-Pan/Zoom (delegiert an `_vm.HandleDrag`/`HandleZoom` — nur UI-Mechanik, keine Business-Logik).
- **GardenPlanView.axaml.cs**: SKCanvasView Tap-vs-Pan-Handling (TapThreshold 10px) mit Koordinaten-Transformation. Delegiert an `_vm.OnCanvasTapped`.
- **MapView.axaml.cs**: `MapControl.Map = vm.MapInstance` weil Mapsui kein `AvaloniaProperty` fuer Map bietet (Mapsui-Limitation, in CLAUDE.md dokumentiert).
- **ProjectsView.axaml.cs**: `Loaded += await vm.EnsureInitializedAsync()` — legitimer Init-Hook.

**Why:** Diese Views verwenden SkiaSharp SKCanvasView oder Mapsui MapControl, die kein sauberes Binding-Interface haben. Reine UI-Mechanik (Pointer-Events, Render-Hooks) im Code-Behind ist MVVM-konform solange keine Business-Logik drin ist.

**How to apply:** Bei SkiaSharp/Mapsui-Views nicht reflexartig als Verstoss melden. Pruefen: Wird Business-Logik ausgefuehrt oder nur Event-Weiterleitung an VM?
