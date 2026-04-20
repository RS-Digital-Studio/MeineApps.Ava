---
name: SmartMeasure ViewModels + Views Review Apr 2026
description: Tiefgründiges Review aller 6 VMs (ohne Main/Survey) und aller 8 Views. 14 Findings, 3 kritisch (Android-Startup-Crashes, Dead-Code-Exports, Map-Lazy-Init-Bruch)
type: project
---

# SmartMeasure ViewModels + Views Review (17.04.2026)

## Kritische Bugs (Android-Crash / Dead-Code)

### KRIT-1: SettingsViewModel nutzt SpecialFolder.LocalApplicationData -> Android-Crash
- Datei: SmartMeasure.Shared/ViewModels/SettingsViewModel.cs:18-19
- Ctor wird bei Singleton-DI-Resolve von App.Services in App.axaml.cs:39 aufgerufen
- Auf Android ist LocalApplicationData nicht zugaenglich (Sandbox) -> UnauthorizedAccessException -> kompletter DI-Chain-Abbruch, App crasht sofort beim Start
- CLAUDE.md der App listet das explizit als bekannten Android-Startup-Crash mit IAppPaths-Fix — aber dieser VM umgeht das Pattern
- IAppPaths muss per Constructor-Injection verwendet werden (ist schon als Singleton registriert)

### KRIT-2: ProjectsViewModel Export-Pfade nutzen LocalApplicationData
- Datei: SmartMeasure.Shared/ViewModels/ProjectsViewModel.cs:187-188 + 218-219
- ExportBlenderAsync + ExportPdfAsync bauen Pfad mit `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`
- Auf Android crasht das entweder oder schreibt in einen Pfad der von FileProvider nicht geshared werden kann
- Fix: IAppPaths.ExportFolder per Constructor-Injection nutzen (bereits als Property im Interface definiert)

### KRIT-3: ExportReady-Event hat keinen Subscriber -> CSV/GeoJSON-Buttons sind Dead-Code
- Datei: SmartMeasure.Shared/ViewModels/ProjectsViewModel.cs:30,155,168
- User klickt in ProjectsView auf "CSV" oder "GeoJSON" Button -> ExportService liefert Inhalt -> `ExportReady?.Invoke(...)` -> NIEMAND hoert zu
- MainViewModel.cs:123 abonniert NUR FileExportReady, nicht ExportReady
- Grep `ExportReady\s*\+=` liefert 0 Treffer
- Android: UriLauncher.ShareText waere der richtige Weg. Desktop: Speichern-Dialog oder Clipboard.

## Hohe Bugs

### HOCH-1: ConnectViewModel ScanAsync schluckt alle Exceptions ohne UX-Feedback
- Datei: ConnectViewModel.cs:62-78
- try/finally ohne catch -> BLE-Scan-Fehler (Permission denied, Adapter off) laesst IsScanning=false aber FoundDevices leer
- User sieht nichts — denkt "keine Geraete da", waehrend in Wahrheit Bluetooth aus ist
- Fix: catch mit ConnectionStatus = "Scan fehlgeschlagen: {ex.Message}"

### HOCH-2: ConnectViewModel ConfigureNtripAsync ohne Exception-Handling
- Datei: ConnectViewModel.cs:104-117, 119-124, 126-129, 132-135
- SendNtripConfig, SendWiFiConfig, UpdateStabHeight, CalibrateImu rufen _bleService-Methoden ohne try/catch
- BLE-Write kann WerfCharacteristic-Exception/Disconnect werfen -> unhandled in async Task -> User bekommt kein Feedback
- Fix: try/catch + ConnectionStatus setzen oder MessageRequested-Event analog zu MainViewModel

### HOCH-3: MapView ignoriert CLAUDE.md-Lazy-Init-Regel
- Datei: SmartMeasure.Shared/Views/MapView.axaml:12
- `<mapsui:MapControl x:Name="MapControl" Grid.Row="0" />` ist direkt im XAML
- CLAUDE.md + MainView.axaml.cs-Kommentar sagen: "Mapsui MapControl crasht auf Android wenn GL-Kontext noch nicht bereit -> NICHT im XAML, nur Lazy per Code-Behind"
- Praktisch rettet MainView.axaml.cs, dass MapView erst bei Tab-Aktivierung gecreated wird -> GL ist dann bereit
- Aber: Wenn die MapView je direkt instanziiert wird (z.B. durch Navigation-Change wo Map-Tab default, oder durch Deeplink, oder spaeter im Refactoring) crasht die App sofort.
- Empfehlung: Code-Behind-Erstellung des MapControl in MapView.axaml.cs (`new MapControl()` im DataContextChanged) — dann ist das Pattern robust.

### HOCH-4: SurveyView CompassInvalidateRequested ohne Unsubscribe -> Handler-Multiplikation bei DataContext-Reuse
- Datei: SmartMeasure.Shared/Views/SurveyView.axaml.cs:20-27
- DataContextChanged haengt jeden Aufruf einen neuen Handler an `vm.CompassInvalidateRequested` an
- In der heutigen App wird DataContext einmal gesetzt (Singleton VM) -> praktisch kein Leak
- ABER: bei Hot-Reload / DataTemplate-Recycling / wenn die View ge-unloaded und re-loaded wird (MainView nutzt Opacity-Switch, nicht Unload — aktuell safe), fuerht das zu 2x/3x Invalidate pro Event
- Fix: Pattern aus TerrainView/GardenPlanView uebernehmen (altes vm -= Subscription, neues vm += Subscription)

### HOCH-5: GardenPlanView Pointer-Release Tap-Koordinaten inkonsistent mit Zoom
- Datei: SmartMeasure.Shared/Views/GardenPlanView.axaml.cs:94-96
- `relX = pos.X - canvasBounds.Width/2 - _vm.Renderer.PanX` — beachtet Pan, ABER nicht Zoom!
- `OnCanvasTapped` (VM) teilt dann durch `LastScale` — LastScale wird aber beim Rendern gesetzt
- Das funktioniert nur solange der letzte Render fertig ist (LastScale aktuell). Beim ersten Tap direkt nach Zoom oder wenn LastScale=0 (VM Init) -> Punkt liegt komplett daneben
- GardenPlanViewModel.cs:159: `if (scale < 0.001) return;` — Tap wird stillschweigend verworfen, User denkt "App reagiert nicht"
- Fix: Zoom auch im Code-Behind beruecksichtigen oder in OnCanvasTapped einen Fallback auf View-Bounds nehmen

### HOCH-6: TerrainViewModel RecalculateMesh N^2 bei Projekt-Laden
- Datei: MainViewModel.cs:107-109 ruft `_measurementService.ClearPoints()` + N mal `AddPoint(pt)`
- `AddPoint` feuert `PointAdded` -> TerrainViewModel.cs:47 ruft RecalculateMesh auf
- Bei 50 Punkten = 50 Delaunay-Triangulierungen nacheinander -> O(N^2 log N) wo O(N log N) reicht wuerde
- User sieht kurzes Freeze beim Projekt-Oeffnen (Delaunay + Convex Hull + Contours fuer jedes n von 1..50)
- Fix: `ClearPoints` Event oder Batch-Flag in MeasurementService, oder `LoadPoints(IEnumerable<SurveyPoint>)` ohne per-Point Event

## Mittlere Bugs / Code-Smells

### MITTEL-1: ProjectsViewModel LoadProjectsAsync setzt IsLoading ohne Dispatcher
- Datei: ProjectsViewModel.cs:96-110
- IsLoading=true (Zeile 96) wird vom Aufrufer-Thread gesetzt. `LoadProjectsAsync` ist [RelayCommand] — Commands laufen auf UI-Thread, also OK.
- Aber: Wenn extern als `await vm.LoadProjectsAsync()` von einem BG-Thread aufgerufen wird, kommt der Setter vom BG-Thread -> ObservableProperty schickt PropertyChanged -> Bindings crashen
- Aktuell nur aus EnsureInitialized (UI-Thread-Ctx) -> OK, aber nicht robust

### MITTEL-2: ConnectViewModel.IsConnected vs MainViewModel.IsBleConnected doppelte Wahrheit
- Beide VMs haben eigene IsConnected-Properties, beide abonnieren StateChanged
- Inkonsistenz bei Race-Condition moeglich (z.B. ConnectVm aktualisiert first, View bindet an MainVm)
- MainViewModel sollte als Single Source of Truth gelten und ConnectVm nur delegieren
- Tief gruendig nicht kritisch da beide das gleiche Event verarbeiten, aber Code-Smell

### MITTEL-3: ProjectsViewModel.ExportBlenderAsync berechnet Mesh blockierend im UI-Thread
- Datei: ProjectsViewModel.cs:184-186
- CreateMesh ist synchron (Delaunay, O(N log N)). Bei 200+ Punkten spuerbar. [RelayCommand]-async laeuft auf UI-Thread (AsyncRelayCommand startet auf current context)
- `_blenderExportService.ExportObjAsync` ist await-t, aber Mesh-Berechnung vorher blockiert
- Fix: `await Task.Run(() => _terrainService.CreateMesh(...))` vor dem ExportObjAsync

### MITTEL-4: SurveyView Buttons "Ecke/Grenze/Baum" haben zu kleine Touch-Targets
- Datei: SurveyView.axaml:87-111
- Padding="12,4" + FontSize=12 -> ca. 28-32dp Hoehe, unter Android 48dp-Minimum
- Material Design + WCAG verlangen 48x48dp Touch-Targets
- ProjectsView.axaml:76-125 Action-Buttons "CSV/GeoJSON/OBJ/PDF/Kopieren" haben gleiches Problem (Padding=8,4 + FontSize=11 -> ~24dp)
- Loesch-Button (SurveyView.axaml:172) Padding=8,2 ist noch kleiner
- Fix: MinHeight=44 setzen oder Padding erhoehen

### MITTEL-5: SettingsView bietet Settings an, die nirgendwo gelesen werden
- Datei: SettingsViewModel.cs:10-11, SettingsView.axaml:39-48, 65-71
- `UseMetric` + `MinFixQuality` sind ObservableProperties mit Binding, werden aber nirgendwo persistiert oder von anderen Services abgefragt
- User aendert Wert -> neustart -> Wert zurueckgesetzt -> User verwirrt
- Grep `MinFixQuality` / `UseMetric` liefert nur die View-Deklarationen
- Fix: IPreferencesService anbinden oder die Settings entfernen

### MITTEL-6: TerrainView fehlt Pan-Support, XAML erwaehnt keinen Pan-Gesture
- Datei: TerrainView.axaml.cs:47-65
- ViewModel hat `HandlePan(deltaX, deltaY)` Methode aber kein Pointer-Event ruft es auf
- Drag macht immer Rotate, nicht Pan. User kann 3D-Modell nicht verschieben
- ResetViewCommand resettet Pan=0 — Pan existiert also im Design, wurde aber in der View vergessen
- Fix: Right-Click/2-Finger-Drag auf Pan mappen

## Nicht-Bugs (aber zu beachten)

- TerrainView/GardenPlanView: `e.Handled = true` in PointerPressed ist aggressiv und unterbindet evt. Gesten von Parent-Controls. Ist hier OK da direkt auf dem Canvas.
- async void Lambdas in MainViewModel.cs:96,127 (Event-Handler) sind korrekt weil Event-Handler.
- ConnectViewModel hat kein Dispose-Pattern — Events auf Singleton-BleService leben so lange wie die App, also OK.
- `Dispatcher.UIThread.Post` in ConnectVm/SurveyVm/MainVm: konsistent und korrekt fuer BLE-BG-Threads.

## MVVM-Compliance Summary

| Check | Status | Notiz |
|-------|--------|-------|
| `x:CompileBindings="True"` + `x:DataType` | OK (8/8) | Alle Views konform |
| Kein `App.Services.GetRequiredService` im View-Ctor | OK | Sauber |
| Kein `DataContext = ...` im Code-Behind | FAST OK | MainView.axaml.cs:33 macht `new MapView { DataContext = vm.MapVm }` — das ist die bewusste Lazy-Ausnahme |
| `[RelayCommand]` statt manueller ICommand | OK | Konsistent |
| Services per Constructor-Injection in VM | TEILWEISE | SettingsViewModel hat 0 Dependencies und greift direkt auf Environment.SpecialFolder zu — MVVM-Violation + Android-Crash |
| Business-Logik im VM, nicht Code-Behind | OK | Views sind duenn |
| Events auf UI-Thread marshalled | OK | Alle BLE-Events durch Dispatcher |

## UI/UX Summary

- Theme-Konsistenz: OK, DynamicResource fuer Farben/Radius/Padding durchgaengig
- Fehlende ScrollViewer: Survey/Connect/Settings haben ScrollViewer (OK). GardenPlan Materialliste hat MaxHeight=120 + ScrollViewer (OK).
- Touch-Targets: 3 Stellen unter 48dp (HOCH)
- Accessibility: Keine Probleme mit Farb-only Indicators gefunden, alle Status haben Text + Icon
- Hardcoded Strings: Durchgaengig Deutsch hardcoded (privates Projekt, per CLAUDE.md akzeptabel)
- RenderTransform scale(1): Nicht verwendet -> kein Button-OnAttachedToLogicalTree Crash-Risiko

## Top-3 Prioritaeten

1. KRIT-1 + KRIT-2: SpecialFolder.LocalApplicationData durch IAppPaths ersetzen (blockiert Android komplett)
2. KRIT-3: ExportReady-Event anhoeren oder CSV/GeoJSON-Buttons entfernen (aktuell Dead-Code)
3. HOCH-3: MapView MapControl per Code-Behind erstellen (robust gegen kuenftiges Refactoring)
