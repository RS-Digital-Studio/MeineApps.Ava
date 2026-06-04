# Views — AXAML-Views & UI-Patterns

Alle Views mit `x:CompileBindings="True"` + `x:DataType`. ViewModel-First (DataContext kommt von
außen, kein `new VM` im Code-Behind). Generische View-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

| Datei | Besonderheit |
|-------|-------------|
| `MainView.axaml(.cs)` | Tab-Hülle: 3 Tabs (Ausrichten/Leistung/Übersicht) + Tab-Bar unten. **Wrapper-`Panel`** trägt `IsVisible` (geerbter Navigator-DataContext), die Child-View setzt ihren DataContext. |
| `AlignView.axaml(.cs)` | `labs:SKCanvasView` (Sonnen-Kompass) + Anweisungen + Ist/Soll + Kalibrier-Warnung. |
| `LivePowerView.axaml(.cs)` | `labs:SKCanvasView` (Power-Trend) + aktuelle Watt + Tagesertrag/Spitze + Demo-Hinweis. |
| `DashboardView.axaml(.cs)` | `labs:SKCanvasView` (Sonnenbahn) + Standort/Sonnenstand/Zeiten/Empfehlung/Bifazial. |

---

## SKCanvasView-Pattern (Align, LivePower, Dashboard)

Code-Behind (nicht über Bindings zugänglich): `PaintSurface` + `InvalidateSurface()`.

```csharp
_canvas = this.FindControl<SKCanvasView>("XxxCanvas");
_canvas.PaintSurface += OnPaint;
DataContextChanged += (_, _) => {           // Handler-Dedup: -= vor +=
    if (_boundVm != null) _boundVm.InvalidateRequested -= _handler;
    _boundVm = DataContext as XxxViewModel;
    if (_boundVm != null) { _handler = () => _canvas.InvalidateSurface(); _boundVm.InvalidateRequested += _handler; }
};
private void OnPaint(object? s, SKPaintSurfaceEventArgs e)
    => vm.Renderer.Render(e.Surface.Canvas, e.Surface.Canvas.LocalClipBounds, ...);  // IMMER LocalClipBounds
```

`xmlns:labs="using:Avalonia.Labs.Controls"`. Code-Behind sonst nur `InitializeComponent` + Abo.

## Lokalisierung

Statische Labels via `{loc:Translate Key=...}` (`xmlns:loc="using:MeineApps.Core.Ava.Localization"`,
load-time aufgelöst). Dynamische/formatierte Strings kommen als VM-Properties (dort `GetString`).

## Tab-Switching (Wrapper-Pattern)

`IsVisible` MUSS am Wrapper-Element (Panel/Border mit geerbtem Navigator-DataContext) hängen, NICHT
an der Child-View (die ihren eigenen DataContext via `DataContext="{Binding Tab}"` setzt) — sonst
wird `IsVisible` gegen den Child-DataContext aufgelöst (AVLN2000).
