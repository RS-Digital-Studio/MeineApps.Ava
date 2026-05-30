# Services — SignalR-Client & REST-API-Client

Client-seitige Kommunikationsschicht. Beide Services sind **Singleton** und werden in
`App.axaml.cs` registriert. Sie kapseln die gesamte Netzwerkkommunikation — ViewModels
rufen nur die Service-Interfaces auf, nie direkt `HttpClient` oder `HubConnection`.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `IConnectionService.cs` | Interface: SignalR-Verbindung, Echtzeit-Events, Steuer-Befehle. Erbt `IAsyncDisposable`. |
| `ConnectionService.cs` | Impl: `HubConnection` mit automatischem Reconnect. Hub-URL `/hub/garden`. |
| `IApiService.cs` | Interface: REST-API, Konfiguration, Verlauf, Zeitpläne. |
| `ApiService.cs` | Impl: `HttpClient` (10s Timeout). Alle Methoden geben bei Fehlern leere Defaults zurück + `ErrorOccurred`-Event. |

## ConnectionService — Architektur

`HubConnection` wird bei jedem `ConnectAsync()` neu aufgebaut (bestehende Verbindung erst
per `DisposeAsync()` beendet). Das ist gewollt: Wenn die Server-URL sich ändert, muss eine
neue Verbindung aufgebaut werden.

**Automatischer Reconnect** mit exponentiell wachsenden Intervallen:
```csharp
.WithAutomaticReconnect(new[] { 0s, 2s, 5s, 10s, 30s })
```

**Hub-Nachrichten** (Server → Client via `_hub.On<T>()`):

| Nachricht | Payload | Empfänger-Event |
|-----------|---------|-----------------|
| `"SensorData"` | `SensorDataDto` | `SensorDataReceived` |
| `"SystemStatus"` | `SystemStatusDto` | `SystemStatusReceived` |
| `"WateringStarted"` | `int zoneId` | `WateringStarted` |
| `"WateringStopped"` | `int zoneId` | `WateringStopped` |

**Steuer-Befehle** (Client → Server via `_hub.InvokeAsync()`):

| Methode | Hub-Methode | Rückgabe |
|---------|-------------|---------|
| `StartWateringAsync(zoneId, duration?)` | `"StartWatering"` | `bool` (Erfolg) |
| `StopWateringAsync(zoneId)` | `"StopWatering"` | — |
| `EmergencyStopAsync()` | `"EmergencyStop"` | — |
| `SetModeAsync(mode)` | `"SetMode"` | — |
| `GetStatusAsync()` | `"GetStatus"` | `SystemStatusDto?` |

`ConnectionChanged?.Invoke(bool)` wird bei Reconnecting, Reconnected und Closed gefeuert —
MainViewModel zeigt den Status direkt in der UI.

## ApiService — Architektur

`SetServerUrl(url)` muss vor dem ersten Aufruf gesetzt sein (Standardwert `192.168.178.56:5000`
aus dem Feld-Initializer). MainViewModel ruft es in `ConnectAsync()` auf.

Alle Methoden fangen alle Exceptions und melden über `ErrorOccurred` statt zu werfen.
Daraus folgt: **ViewModels müssen nach dem Aufruf immer auf `null`/leere Liste prüfen**.

**REST-Endpunkte:**

| Methode | HTTP | Pfad |
|---------|------|------|
| `TestConnectionAsync()` | GET | `/api/status` (3s Timeout) |
| `GetZonesAsync()` | GET | `/api/zones` |
| `UpdateZoneAsync(config)` | PUT | `/api/zones/{zoneId}` |
| `CalibrateAsync(zoneId, type)` | POST | `/api/zones/{zoneId}/calibrate/{type}` (`dry`/`wet`) |
| `GetReadingsAsync(...)` | GET | `/api/history/readings?from=&to=&limit=&zoneId=` |
| `GetEventsAsync(...)` | GET | `/api/history/events?from=&to=&limit=&zoneId=` |
| `GetConfigAsync()` | GET | `/api/config` |
| `UpdateConfigAsync(config)` | PUT | `/api/config` |
| `GetSchedulesAsync(zoneId?)` | GET | `/api/schedules[?zoneId=]` |
| `SaveScheduleAsync(schedule)` | POST/PUT | `/api/schedules[/{id}]` (Id==0 → POST, sonst PUT) |
| `DeleteScheduleAsync(id)` | DELETE | `/api/schedules/{id}` |

**Zeitstempel in URLs** werden mit `from={from:O}&to={to:O}` im ISO-8601-Format übergeben —
passend zur Haupt-Konvention `DateTime.UtcNow` + "O"-Format.

## Gotchas

- **ConnectionService.IsConnected** prüft `_hub?.State == HubConnectionState.Connected` —
  kann kurzzeitig `false` sein während Reconnect läuft. ViewModels müssen damit umgehen
  (z.B. Buttons deaktivieren wenn `!IsConnected`).
- **ApiService-Fehler verschluckt:** Methoden geben immer `null` / leere Liste zurück statt zu werfen.
  `ErrorOccurred` wird synchron aus dem catch-Block gefeuert (kein Background-Thread) —
  MainViewModel dispatcht es dann auf den UI-Thread.
- **Mock-Modus** (ohne Server): Beide Services schlagen sofort fehl → `ErrorOccurred` wird gefeuert,
  `IsConnected` bleibt `false`. Kein separater Mock-Service im Client — im Gegensatz zum Server
  (`MockHardwareService`). Für Entwicklung ohne Pi: `GardenControl.Server` lokal starten.
