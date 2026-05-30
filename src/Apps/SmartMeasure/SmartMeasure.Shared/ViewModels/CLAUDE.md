# ViewModels — Navigation, Messung, Analyse, Export

Alle ViewModels sind **Singleton** (in `App.axaml.cs` registriert) und werden vom `MainViewModel`
per Constructor-Injection gehalten. Nur UI-Logik — Berechnungen delegieren immer an Services.
BLE-Events kommen vom Background-Thread → IMMER `Dispatcher.UIThread.Post` vor UI-Property-Änderung.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Verantwortlichkeit |
|-------|-------------------|
| `MainViewModel.cs` | Tab-Navigation (8 Seiten), Status-Bar (BLE/Fix/Sat), AR-Transfer-Orchestrierung, Export-Banner, Back-Button, ForegroundService-Kopplung |
| `SurveyViewModel.cs` | Live-Position (BLE-Events), Punkte empfangen/anzeigen, AR-Capture starten, `CompassRenderer` versorgen |
| `TerrainViewModel.cs` | Mesh berechnen (Bowyer-Watson via `ITerrainService`), Konturlinien, Rotation/Zoom/Pan, `TerrainRenderer` steuern |
| `GardenPlanViewModel.cs` | Gartenelemente CRUD, `GardenPlanRenderer` steuern, Volumen-Panel, PointsJson v2 |
| `MapViewModel.cs` | Mapsui-Karte initialisieren (lazy), Punkte als Pins, Export-Trigger |
| `ProjectsViewModel.cs` | SQLite-Projekte laden/erstellen/löschen, Export-Flows, Differential-Snapshot-Aufruf |
| `StakeoutViewModel.cs` | Stakeout-Ziele laden, `StakeoutRenderer` versorgen, Reached-Feedback |
| `ConnectViewModel.cs` | BLE-Scan, Verbinden/Trennen, NTRIP-Config, `GnssConditionService`-Anzeige |
| `SettingsViewModel.cs` | Stabhöhe, Einheiten, Min-Fix-Quality → persistiert via `IPreferencesService`. Version aus Assembly |

---

## MainViewModel

### Navigation

8 `IsXxxActive`-Properties gesteuert durch `NavigateCommand(string page)`.
Seiten: `Survey` | `Terrain` | `Garden` | `Map` | `Projects` | `Stakeout` | `Connect` | `Settings`.
`IsSurveyActive = true` beim Start.

### Events

| Event | Konsument |
|-------|----------|
| `ExitHintRequested` | `MainActivity` → Toast |
| `MessageRequested` | `MainActivity` → Toast (Long) |
| `ForegroundServiceRequested` | `MainActivity` → `MeasurementForegroundService.Start/Stop` |

### AR-Transfer-Flow

```
SurveyVm.ArCaptureCompleted
  → ProjectsVm.EnsureProjectExistsAsync()   (Auto-Projekt falls noch keins)
  → IArTransferService.TransferToProjectAsync(result, projectId)
  → GardenPlanVm.LoadElementsFromProjectAsync(projectId)
```

### Projekt-Load-Flow

```
ProjectsVm.ProjectSelected
  → IProjectService.GetProjectAsync(id)
  → IMeasurementService.ReplacePoints(full.Points)   (1 PointsReset-Event!)
  → GardenPlanVm.LoadElementsFromProjectAsync
  → StakeoutVm.LoadTargetsAsync
  → Navigate("Survey")
```

`ReplacePoints` + `PointsReset`-Event ist entscheidend: TerrainViewModel rechnet Delaunay
einmal für N Punkte statt N-mal (O(N²) vermieden).

### Export-Banner

`ProjectsVm.FileExportReady` setzt `LastExportPath` + `IsExportBannerVisible = true`.
`ShareLastExportCommand` / `OpenLastExportCommand` → `UriLauncher.ShareFile` / `OpenFile`.
MIME-Type aus Dateiendung: `.pdf` / `.csv` / `.geojson` / `.kmz` / `.dxf` / `.obj` u.a.

---

## SurveyViewModel

- `IsMockMode = bleService is MockBleService` — steuert Debug-Panel-Sichtbarkeit.
- `RecentPoints` ist `ObservableCollection<SurveyPointDisplay>` (Insert an Position 0 = neueste oben).
- Label wird beim nächsten `PointReceived`-Event auf dem Punkt gesetzt und danach zurückgesetzt.
- Fix-Verlust setzt alle Live-Werte zurück (`ResetLivePositionUi`), damit keine Stale-Daten gemessen werden.
- MagWarning erscheint wenn `MagAccuracy < 2 && IsConnected` — Horizontal-Tilt-Korrektur greift nicht.

---

## TerrainViewModel

- `PointAdded` → inkrementelles Neu-Triangulieren (RTK-Live-Ansicht).
- `PointsReset` → einmalige Neuberechnung (Projekt-Load).
- Rotation via `HandleDrag(dx, dy)`, Zoom via `HandleZoom(factor)`, Pan via `HandlePan(dx, dy)`.
  Alle drei delegieren direkt an `Renderer.*`, kein eigener State-Speicher nötig.
- `ContourInterval` default 0,25 m (25 cm); Änderung triggert nur Konturlinien-Neuberechnung, nicht neues Mesh.

---

## SettingsViewModel

- Verwendet `IAppPaths` statt `Environment.GetFolderPath` (Android-Sandbox-Fix).
- Persistenz-Keys: `sm.stab_height`, `sm.use_metric`, `sm.min_fix_quality`.
- `_isLoaded`-Guard verhindert, dass `OnXxxChanged`-Partial-Methoden beim Initialisieren in Preferences schreiben.

---

## Domänen-Gotchas

| Problem | Fix |
|---------|-----|
| BLE-Events auf Background-Thread, UI crasht | IMMER `Dispatcher.UIThread.Post` für alle Property-Änderungen aus BLE-Callbacks |
| SurveyView-Handler akkumulieren bei DataContext-Wechsel | Handler-Dedup: `_boundVm.CompassInvalidateRequested -= _handler` VOR `+=` in `DataContextChanged` |
| TerrainViewModel wird N× trianguliert bei Projekt-Load | `ReplacePoints` + `PointsReset` statt `AddPoint` für Batch-Load verwenden |
| `SettingsViewModel` crasht auf Android | `IAppPaths` per DI, NIEMALS `Environment.GetFolderPath` direkt |
