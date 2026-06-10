# Views — AXAML-Views & UI-Patterns

7 Views mit `x:CompileBindings="True"` und `x:DataType`. Alle folgen ViewModel-First
(DataContext kommt von außen, Views erzeugen keine VMs).
Generische MVVM/View-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Besonderheit |
|-------|-------------|
| `MainView.axaml.cs` | Lazy-MapView: `MapContainer` wird erst bei erster Aktivierung mit `MapView` befüllt |
| `SurveyView.axaml.cs` | Kein Code-Behind-Logik (AR-Hero-CTA + Punkte-Liste sind reine Bindings) |
| `TerrainView.axaml.cs` | Touch/Mouse-Handler: Drag=Rotation, Rechts/Mitte=Pan, Wheel=Zoom |
| `GardenPlanView.axaml.cs` | `SKCanvasView` für Gartenplan-Renderer, Tap-to-draw + Pan + Zoom, Materialliste + Volumen-Panel unten |
| `MapView.axaml.cs` | Mapsui `MapControl` (Lazy-Init über MainView) |
| `ProjectsView.axaml.cs` | `EnsureInitializedAsync()` on `Loaded` (einmaliges Init-Trigger-Pattern) |
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

## SKCanvasView-Pattern (TerrainView, GardenPlanView)

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

## GardenPlanView Touch-Interaktion

| Geste | Aktion |
|-------|--------|
| Tap (Bewegung < 10 px) | `OnCanvasTapped(relX, relY)` — Zeichnungspunkt hinzufügen |
| Drag | Pan (`HandlePan`) |
| Mausrad / Pinch | Zoom (`HandleZoom`) |

**Tap-Threshold:** 10 px Bewegungsdistanz trennt Tap von Pan. Canvas-Koordinate wird relativ zur
Viewport-Mitte und `Renderer.PanX/PanY` berechnet. Wenn `Renderer.LastScale < 0.001` (noch kein
erster Paint), wird erst `InvalidateSurface()` aufgerufen bevor der Tap verarbeitet wird —
sonst stumm verworfen, weil der Renderer die Koordinate nicht skalieren kann.

---

## Gotchas

| Problem | Fix |
|---------|-----|
| Mapsui crasht beim Start | Lazy-Init via Code-Behind, NICHT im AXAML deklarieren |
| SKCanvasView leer nach IsVisible-Toggle | Nach Sichtbar-Werden `InvalidateSurface()` aufrufen (VM-Property setzen triggert `InvalidateRequested`) |
| Handler akkumulieren bei DataContext-Wechsel | `-=` vor `+=` im `DataContextChanged`-Handler |
| `e.Info.Width/Height` falsch bei DPI > 1 | `canvas.LocalClipBounds` verwenden |
