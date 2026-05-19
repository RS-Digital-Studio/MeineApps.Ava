# GardenControl — Bewässerungssteuerung

> Für Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

Automatische Gartenbewässerung mit Raspberry Pi 5 + 7"-Touchscreen als Kiosk-Station,
plus Android- und Desktop-App für mobile Steuerung. Solar-betrieben (kein Strom im Garten).

| Aspekt | Wert |
|--------|------|
| Version | v1.0.0 (VersionCode 1) |
| Pi-Hostname | `gardencontrol.local` |
| Server-URL | `http://<pi-ip>:5000` |
| Theme | Sattes Grün `#2E7D32` + Wasser-Blau `#1E88E5` + Erd-Braun `#8D6E63` |

---

## Build & Zielframework

| Projekt | Framework | Befehl |
|---------|-----------|--------|
| `GardenControl.Shared` | `net10.0` | `dotnet build src/Apps/GardenControl/GardenControl.Shared` |
| `GardenControl.Server` | `net10.0` | `dotnet run --project src/Apps/GardenControl/GardenControl.Server` |
| `GardenControl.Desktop` | `net10.0` | `dotnet run --project src/Apps/GardenControl/GardenControl.Desktop` |
| `GardenControl.Android` | `net10.0-android` | `dotnet build src/Apps/GardenControl/GardenControl.Android` |

Pi-Server-Deploy via Skill `/server-deploy` (siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)).

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `GardenControl.Shared/ViewModels/` | `GardenControl.ViewModels` |
| `GardenControl.Shared/Views/` | `GardenControl.Views` |
| `GardenControl.Shared/Services/` | `GardenControl.Services` |
| `GardenControl.Shared/Models/` | `GardenControl.Models` |
| `GardenControl.Server/Hubs/` | `GardenControl.Server.Hubs` |
| `GardenControl.Server/Api/` | `GardenControl.Server.Api` |
| `GardenControl.Server/Services/` | `GardenControl.Server.Services` |

---

## Architektur

```
[Raspberry Pi 5 + 7" Display]          [Android / Desktop App]
├── ASP.NET Core Server                  ├── Avalonia UI
│   ├── SignalR Hub (Echtzeit)  ←WiFi→  ├── SignalR Client
│   ├── REST API                         ├── REST API Client
│   └── SQLite (Verlauf)                 └── ViewModels (MVVM)
├── Avalonia Desktop-App (Fullscreen)
│   └── Verbindet zu localhost:5000
├── GPIO → 4-Kanal Relais (ALAMSCN Kit)
│   ├── 3x 12V Magnetventile (Beete)
│   └── 1x 12V Tauchpumpe
├── I2C → ADS1115 ADC
│   └── 3x Bodenfeuchtesensoren (10m Kabel)
└── Solar: 50W Panel + 12V Akku + Laderegler
```

## Hardware

- **Raspberry Pi 5 (8GB)** + 7" Touchscreen Display
- **ALAMSCN Kit**: 4-Kanal 5V Relais (Ventile/Pumpe schalten)
- **ADS1115**: 16-Bit ADC (Pi hat keinen analogen Eingang)
- **3x Bodenfeuchtesensoren** (10m Kabel zu den Beeten, leicht bergauf)
- **3x 12V Magnetventile NC** + **1x 12V Tauchpumpe** (5m Förderhöhe)
- **Solar**: 50W Panel + 12V AGM 12Ah + PWM-Laderegler + Buck-Converter 5V
- Details + Einkaufsliste: `docs/HARDWARE.md`

## Projekte

| Projekt | Zweck | Target |
|---------|-------|--------|
| GardenControl.Core | Models, DTOs, Enums (shared) | net10.0 |
| GardenControl.Server | ASP.NET Core Server für den Pi | net10.0 (linux-arm64) |
| GardenControl.Shared | ViewModels, Views, Client-Services, App | net10.0 |
| GardenControl.Desktop | Desktop Entry Point (auch auf Pi) | net10.0 |
| GardenControl.Android | Android Entry Point | net10.0-android |

## Build-Befehle

```bash
# Desktop starten (Mock-Hardware, kein Pi nötig)
dotnet run --project src/Apps/GardenControl/GardenControl.Desktop

# Alles bauen
dotnet build src/Apps/GardenControl/GardenControl.Desktop
dotnet build src/Apps/GardenControl/GardenControl.Server

# Für Pi cross-compilieren
dotnet publish src/Apps/GardenControl/GardenControl.Server -c Release -r linux-arm64 --self-contained
dotnet publish src/Apps/GardenControl/GardenControl.Desktop -c Release -r linux-arm64 --self-contained

# Komplett-Deploy auf den Pi
bash src/Apps/GardenControl/GardenControl.Server/Install/deploy.sh gardencontrol.local
```

## Pi-Kiosk-Modus

- Server als systemd-Service (immer an, auch ohne Display)
- Desktop-App startet automatisch in Fullscreen (Auto-Login + Autostart)
- Pi-Erkennung in `App.axaml.cs`: Prüft `/proc/device-tree/model` → auto-connect localhost
- Touch-optimierte Styles (`TouchStyles.axaml`): Min. 44dp Touch-Targets
- Install-Skript: `install-pi5.sh` (I2C, .NET, Auto-Login, Kiosk)

## GPIO-Belegung (BCM)

| Pin | Funktion |
|-----|----------|
| GPIO 2/3 | I2C SDA/SCL → ADS1115 |
| GPIO 17 | Relais Kanal 1 → Ventil Beet 1 |
| GPIO 27 | Relais Kanal 2 → Ventil Beet 2 |
| GPIO 22 | Relais Kanal 3 → Ventil Beet 3 |
| GPIO 23 | Relais Kanal 4 → Pumpe |

## API

- REST: `http://<pi-ip>:5000/api/...` (status, zones, history, config, calibrate)
- SignalR: `http://<pi-ip>:5000/hub/garden` (Echtzeit-Push)
- Mock-Hardware: Automatisch wenn kein `/sys/class/gpio` erkannt (Desktop-Entwicklung)

## Server-Komponenten

### `Hardware/` (Plattform-Abstraktion)

| Klasse | Aufgabe |
|--------|---------|
| `IGpioService` / `GpioService` | Relais-Steuerung über `System.Device.Gpio` |
| `ISensorService` / `SensorService` | ADC-Werte via `Iot.Device.Ads1115` |
| `MockHardwareService` | Implementiert beide Interfaces ohne echte Hardware (Desktop-Entwicklung) |

Mock-Erkennung: automatisch wenn `/sys/class/gpio` nicht vorhanden ist.

### `Services/`

| Service | Aufgabe |
|---------|---------|
| `IIrrigationService` / `IrrigationService` | Bewässerungslogik (Start/Stop, Schwellenwerte, Cooldown) |
| `IDatabaseService` / `DatabaseService` | SQLite (Messwerte, Ereignisse, Zonen, Konfiguration) |
| `SensorPollingWorker` | `BackgroundService` für periodisches Sensor-Polling + SignalR-Push |
| `Weather/` | Wetterdaten-Integration (regenbasiertes Skip der Bewässerung) |

### `Hubs/`

| Hub | Aufgabe |
|-----|---------|
| `GardenHub` | SignalR-Hub für Echtzeit-Kommunikation (`/hub/garden`) |

## Client-ViewModels

| ViewModel | Tab | Features |
|-----------|-----|----------|
| DashboardViewModel | Dashboard | Live-Werte, Pumpen-Status, Schnell-Bewässerung, Modus-Wechsel, Notstopp |
| ZoneControlViewModel | Steuerung | Manuelle Ventil-/Pumpen-Steuerung, Dauer wählbar |
| ScheduleViewModel | Automatik | Schwellenwerte, Dauer, Cooldown, Pollintervall pro Zone |
| CalibrationViewModel | Kalibrierung | Live-ADC-Werte, Trocken/Nass-Kalibrierung pro Zone |
| HistoryViewModel | Verlauf | Bewässerungsereignisse, Zeitfilter (1h-30d), Zone-Filter |
| SettingsViewModel | Einstellungen | Server-URL, Verbindungstest, Server-Info |

## Android Back-Button

MainViewModel nutzt `BackPressHelper` aus MeineApps.Core.Ava.Services (Convention aller Apps):

- `ExitHintRequested` Event für Toast-Anzeige
- `HandleBackPressed()` Reihenfolge: Error-Banner schließen → Tab zurück zu Dashboard → Double-Back-to-Exit
- MainActivity verdrahtet Event per `OnCreate`, delegiert `OnBackPressed` an VM

## Farbpalette

- Primary: `#2E7D32` (Sattes Grün)
- Wasser: `#1E88E5` (Blau für aktive Bewässerung)
- Akzent: `#8D6E63` (Erd-Braun)
- Hintergrund: `#0F1923` (Premium Dark)
- Gradient-Cards mit `#10-15FFFFFF` Border für Glas-Effekt

---

## Verweise

- [Haupt-CLAUDE.md](../../../CLAUDE.md) — Build, Conventions, Troubleshooting
- [MeineApps.Core.Ava/CLAUDE.md](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) — Preferences, BackPressHelper, ViewLocator
- [MeineApps.UI/CLAUDE.md](../../UI/MeineApps.UI/CLAUDE.md) — Custom Controls, Loading-Pipeline
- `Releases/GardenControl/CHANGELOG_*.md` — Release-Notes
