# ViewModels — Verbindungsmanagement & Tab-Logik

Alle ViewModels sind **Singleton** (in `App.axaml.cs` registriert) und werden vom
`MainViewModel` per Constructor Injection gehalten. Nur UI-Logik — Netzwerk-Kommunikation
delegiert an `IConnectionService` (SignalR) und `IApiService` (REST).
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | Tab-Navigation, Verbindungsmanagement, Fehler-Banner, Back-Press-Flow, `ExitHintRequested`. Implementiert `IAsyncDisposable`. |
| `DashboardViewModel.cs` | Live-Übersicht aller Zonen: Feuchtigkeitswerte, Systemstatus, Wetterinfo, Schnellaktionen (Start/Stopp/Notstopp). Enthält `ZoneDisplayItem` (ObservableObject). |
| `ZoneControlViewModel.cs` | Manuelle Ventil-/Pumpen-Steuerung mit konfigurierbarer Dauer. `TestAllZones()` fährt alle aktivierten Zonen nacheinander an. |
| `ScheduleViewModel.cs` | Automatik-Konfiguration: Schwellenwerte, Bewässerungsdauer, Cooldown, Poll-Intervall, Zeitpläne. Enthält `ZoneConfigItem` + `ScheduleItem`. |
| `CalibrationViewModel.cs` | Sensor-Kalibrierung (Trocken/Nass-Referenzwerte). Live-ADC-Werte via SignalR. Enthält `CalibrationItem`. |
| `HistoryViewModel.cs` | Verlaufsdaten: Feuchtigkeitsverlauf-Chart-Daten, Bewässerungsereignisse, Statistiken. Zeitfilter (1h, 6h, 24h, 7d, 30d). Debouncing via `SemaphoreSlim`. |
| `SettingsViewModel.cs` | Server-URL, Verbindungstest, Server-Info. Feuert `ServerUrlChanged` → MainViewModel reagiert mit Reconnect. |

## Tab-Navigation (MainViewModel)

```csharp
[ObservableProperty] private string _currentPage = "Dashboard";
// "Dashboard" | "ZoneControl" | "Schedule" | "Calibration" | "History" | "Settings"

// Tab-Aktiv-Properties für UI (alle via OnCurrentPageChanged aktualisiert)
public bool IsDashboardActive => CurrentPage == "Dashboard";
// ... analog für alle Tabs
```

`NavigateCommand(string page)` setzt `CurrentPage`. ViewLocator oder direktes ContentControl-Binding
zeigt die passende View.

## Verbindungsmanagement

`MainViewModel` verdrahtet zwei Events aus dem DI:

- `_connection.ConnectionChanged` → `IsConnected` + `ConnectionStatus` auf UI-Thread aktualisieren.
- `_api.ErrorOccurred` → `ShowError(msg)` (setzt `HasError` + `ErrorMessage`).
- `Settings.ServerUrlChanged` → `ServerUrl` setzen + `ConnectAsync()` aufrufen.

`ConnectAsync()` setzt beide Services (`_api.SetServerUrl()` + `_connection.ConnectAsync()`).
Fehlertypen werden unterschieden: `HttpRequestException` (Server weg), `TaskCanceledException`
(Timeout), allgemeine Exception.

## Back-Navigation (MainViewModel.HandleBackPressed)

1. `HasError == true` → Fehler-Banner schließen (`ClearError()`).
2. `CurrentPage != "Dashboard"` → zurück zum Dashboard.
3. Auf Dashboard → Double-Back-to-Exit via `BackPressHelper`.

## SignalR-Callbacks — Thread-Safety

Alle `IConnectionService`-Events kommen auf einem **Hintergrund-Thread** (SignalR-Client-Thread).
Daher gilt in jedem ViewModel das gleiche Muster:

```csharp
_connection.SystemStatusReceived += status =>
    Dispatcher.UIThread.Post(() => { /* UI-Properties setzen */ });
```

**Nie** direkt in einem SignalR-Callback auf ObservableCollection oder `[ObservableProperty]`
schreiben — das löst Property-Changed-Events auf dem falschen Thread aus.

## ZoneDisplayItem (in DashboardViewModel + ZoneControlViewModel)

`ZoneDisplayItem` ist ein `ObservableObject` das von `ZoneStatusDto` befüllt wird.
`Update(ZoneStatusDto)` setzt alle Properties inkl. Zustandstext + -farbe (switch expression):

| State | Text | Farbe |
|-------|------|-------|
| `Watering` | "Bewässert (Ns)" | `#2196F3` Blau |
| `Cooldown` | "Abkühlphase" | `#FF9800` Orange |
| `Error` | "Fehler" | `#F44336` Rot |
| Default | "Bereit" | `#4CAF50` Grün |
| Sensor getrennt | "Sensor getrennt" | `#9E9E9E` Grau |

`MoistureColor` (< 25 % Rot, 25-40 % Orange, 40-70 % Grün, > 70 % Blau) wird bei jedem
`UpdateMoisture()`-Aufruf neu berechnet.

## Domänen-Gotchas

- **HistoryViewModel-Debouncing:** `SemaphoreSlim(1,1)` mit `WaitAsync(0)` (nicht-blockierend)
  verhindert parallele API-Requests wenn der Nutzer schnell den Zeitfilter wechselt.
- **ScheduleViewModel.LoadConfig:** Ruft `GetZonesAsync()` zweimal auf (einmal für ZoneConfigs,
  einmal in `LoadSchedulesAsync()`). Nicht optimiert — beide Aufrufe sind selten und kurz.
- **CalibrationView-Trigger:** `CalibrationView.axaml.cs` ruft `LoadZonesCommand` im
  `OnLoaded`-Event auf, weil die Kalibrierungsdaten nur bei Bedarf geladen werden sollen
  (nicht beim VM-Ctor, da die Verbindung da noch nicht steht).
- **`TestAllZones()` blockiert Tasks-Mechanismus:** `Task.Delay(testDuration + 2)` im
  foreach — funktioniert, aber lässt sich nicht abbrechen. Kein CancellationToken vorgesehen.
