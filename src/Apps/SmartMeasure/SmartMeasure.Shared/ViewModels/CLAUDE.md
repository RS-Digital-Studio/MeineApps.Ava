# ViewModels — Navigation, Messung, Analyse, Export

Alle ViewModels sind **Singleton** (in `App.axaml.cs` registriert) und werden vom `MainViewModel`
per Constructor-Injection gehalten. Nur UI-Logik — Berechnungen delegieren immer an Services.
Service-Events können vom Background-Thread kommen → IMMER `Dispatcher.UIThread.Post` vor
UI-Property-Änderung.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Verantwortlichkeit |
|-------|-------------------|
| `MainViewModel.cs` | 6 `IsXxxActive`-Properties (Navigation), AR-Transfer-Orchestrierung, Export-Banner, Back-Button |
| `SurveyViewModel.cs` | AR-Capture starten (Hero-CTA), Punkte-Liste + Projekt-Statistik aus `IMeasurementService`-Events |
| `TerrainViewModel.cs` | Mesh berechnen (Bowyer-Watson via `ITerrainService`), Konturlinien, Rotation/Zoom/Pan, `TerrainRenderer` steuern |
| `GardenPlanViewModel.cs` | Gartenelemente CRUD, `GardenPlanRenderer` steuern, Volumen-Panel, PointsJson v2 |
| `MapViewModel.cs` | Mapsui-Karte initialisieren (lazy), Punkte als Pins, Export-Trigger |
| `ProjectsViewModel.cs` | SQLite-Projekte laden/erstellen/löschen, Export-Flows, Differential-Snapshot-Aufruf |
| `SettingsViewModel.cs` | Einheiten (metrisch/imperial), App-/Datenbank-Info → persistiert via `IPreferencesService`. Version aus Assembly |

---

## MainViewModel

### Navigation

6 `IsXxxActive`-Properties gesteuert durch `NavigateCommand(string page)`.
Seiten: `Survey` | `Terrain` | `Garden` | `Map` | `Projects` | `Settings`.
`IsSurveyActive = true` beim Start.

### Events

| Event | Konsument |
|-------|----------|
| `ExitHintRequested` | `MainActivity` → Toast (double-back) |
| `MessageRequested` | `MainActivity` → Toast (Long) |

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

- `RecentPoints` ist `ObservableCollection<SurveyPointDisplay>` (Insert an Position 0 = neueste oben).
- `SurveyPointDisplay.FromPoint` zeigt die ARCore-Konfidenz als Qualitäts-Badge
  ("AR · 85%", Ampel-Farbe via `QualityColor`-Hex + `StringToColorBrushConverter`).
- Statistik (`PointCount`, `AreaText`, `PerimeterText`) wird aus
  `IMeasurementService.PointAdded/PointsReset` gespeist.
- `StartArCaptureAsync`: Doppel-Tap-Schutz via `IsArBusy`, setzt Site-/Preload-Punkte vor dem
  Start und räumt die Brücken im `finally` wieder ab; differenziert nach
  `LastCompletionStatus` (UserCancelled/Error/Success).

---

## TerrainViewModel

- `PointAdded` → inkrementelles Neu-Triangulieren (Live-Ansicht beim AR-Transfer).
- `PointsReset` → einmalige Neuberechnung (Projekt-Load).
- Rotation via `HandleDrag(dx, dy)`, Zoom via `HandleZoom(factor)`, Pan via `HandlePan(dx, dy)`.
  Alle drei delegieren direkt an `Renderer.*`, kein eigener State-Speicher nötig.
- `ContourInterval` default 0,25 m (25 cm); Änderung triggert nur Konturlinien-Neuberechnung, nicht neues Mesh.

---

## SettingsViewModel

- Verwendet `IAppPaths` statt `Environment.GetFolderPath` (Android-Sandbox-Fix).
- Persistenz-Key: `sm.use_metric`.
- `_isLoaded`-Guard verhindert, dass `OnXxxChanged`-Partial-Methoden beim Initialisieren in Preferences schreiben.

---

## Domänen-Gotchas

| Problem | Fix |
|---------|-----|
| Service-Events auf Background-Thread, UI crasht | IMMER `Dispatcher.UIThread.Post` für alle Property-Änderungen aus Service-Callbacks |
| TerrainViewModel wird N× trianguliert bei Projekt-Load | `ReplacePoints` + `PointsReset` statt `AddPoint` für Batch-Load verwenden |
| `SettingsViewModel` crasht auf Android | `IAppPaths` per DI, NIEMALS `Environment.GetFolderPath` direkt |
