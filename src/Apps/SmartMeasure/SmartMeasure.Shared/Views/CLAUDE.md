# Views — AXAML-Views & UI-Patterns

9 Views mit `x:CompileBindings="True"` und `x:DataType`. Alle folgen ViewModel-First
(DataContext kommt von außen, Views erzeugen keine VMs).
Generische MVVM/View-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Besonderheit |
|-------|-------------|
| `MainView.axaml.cs` | Lazy-MapView: `MapContainer` wird erst bei erster Aktivierung mit `MapView` befüllt |
| `SurveyView.axaml.cs` | `SKCanvasView` für Kompass, Handler-Dedup im `DataContextChanged` |
| `TerrainView.axaml.cs` | Touch/Mouse-Handler: Drag=Rotation, Rechts/Mitte=Pan, Wheel=Zoom |
| `GardenPlanView.axaml.cs` | Gartenelemente-Liste + Volumen-Panel, SkiaSharp-Canvas |
| `MapView.axaml.cs` | Mapsui `MapControl` (Lazy-Init über MainView) |
| `ProjectsView.axaml.cs` | `EnsureInitializedAsync()` on `Loaded` (einmaliges Init-Trigger-Pattern) |
| `ConnectView.axaml.cs` | BLE-Scan-Liste, NTRIP-Formular, GNSS-Condition-Panel |
| `StakeoutView.axaml.cs` | `SKCanvasView` für `StakeoutRenderer` |
| `SettingsView.axaml.cs` | Einstellungsformular, kein Code-Behind-Logik |

---

## Lazy-MapView (MainView)

Mapsui `MapControl` crasht auf Android wenn der GL-Kontext beim App-Start noch nicht bereit ist.
`MapView` wird deshalb NICHT in AXAML deklariert, sondern per Code-Behind eingebaut — genau einmal,
wenn der Karten-Tab erstmals die CSS-Klasse `Active` erhält:

```csharp
if (!mapContainer.Classes.Contains("Active")) return;
_mapViewCreated = true;
vm.MapVm.EnsureInitialized();
mapContainer.Child = new MapView { DataContext = vm.MapVm };
```

**Kein weiterer `MapView` im XAML** — das würde den Crash bei jedem Start produzieren.

---

## SKCanvasView-Pattern (SurveyView, TerrainView, StakeoutView)

SKCanvasView benötigt Code-Behind, weil `PaintSurface` und `InvalidateSurface()` nicht über
reine Bindings zugänglich sind. Schematisch:

```csharp
DataContextChanged += (_, _) =>
{
    if (_boundVm != null) _boundVm.InvalidateRequested -= _handler;
    _boundVm = DataContext as XxxViewModel;
    if (_boundVm != null) { _handler = () => _canvas.InvalidateSurface(); _boundVm.InvalidateRequested += _handler; }
};

private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
{
    var bounds = e.Surface.Canvas.LocalClipBounds;   // IMMER LocalClipBounds, nicht e.Info.Width/Height
    _vm?.Renderer.Render(e.Surface.Canvas, bounds, ...);
}
```

`e.Info.Width/Height` gibt physische Pixel (DPI-skaliert), `LocalClipBounds` gibt den sichtbaren Bereich — IMMER `LocalClipBounds` verwenden.

---

## TerrainView Touch-Interaktion

| Geste | Aktion |
|-------|--------|
| Linksklick / 1-Finger-Drag | Rotation (Azimut + Elevation), Elevation geclampt 5–85° |
| Rechts-/Mitte-Klick / 2-Finger | Pan |
| Mausrad / Pinch | Zoom, geclampt 0,2–5,0 |

Touch-Events delegieren sofort an `_vm.HandleDrag/HandlePan/HandleZoom` → Renderer aktualisiert sich.

---

## Gotchas

| Problem | Fix |
|---------|-----|
| Mapsui crasht beim Start | Lazy-Init via Code-Behind, NICHT im AXAML deklarieren |
| SKCanvasView leer nach IsVisible-Toggle | Nach Sichtbar-Werden `InvalidateSurface()` aufrufen (VM-Property setzen triggert `InvalidateRequested`) |
| Handler akkumulieren bei DataContext-Wechsel | `-=` vor `+=` im `DataContextChanged`-Handler |
| `e.Info.Width/Height` falsch bei DPI > 1 | `canvas.LocalClipBounds` verwenden |
