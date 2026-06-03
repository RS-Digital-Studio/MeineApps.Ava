# GardenControl — Bewässerungssteuerung

> Build-Befehle, Conventions und Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md)

Automatische Gartenbewässerung mit Raspberry Pi 5 + 7"-Touchscreen als Kiosk-Station,
plus Android- und Desktop-App für mobile Steuerung. Solar-betrieben (kein Strom im Garten).

**Package:** `com.meineapps.gardencontrol`

| Aspekt | Wert |
|--------|------|
| Pi-Hostname | `gardencontrol.local` |
| Server-URL | `http://<pi-ip>:5000` |
| Theme | Sattes Grün `#2E7D32` + Wasser-Blau `#1E88E5` + Erd-Braun `#8D6E63` |

---

## Architektur-Überblick

Fünf Projekte — Server auf dem Pi, Avalonia-Client auf Desktop + Android:

```
[Raspberry Pi 5 + 7" Display]          [Android / Desktop App]
├── ASP.NET Core Server                  ├── Avalonia UI
│   ├── SignalR Hub (Echtzeit)  ←WiFi→  ├── SignalR Client (ConnectionService)
│   ├── REST API                         ├── REST API Client (ApiService)
│   └── SQLite (Verlauf)                 └── ViewModels (MVVM)
├── Avalonia Desktop-App (Fullscreen)
│   └── Verbindet zu localhost:5000
├── GPIO → 4-Kanal Relais
│   ├── 3x 12V Magnetventile (Beete)
│   └── 1x 12V Tauchpumpe
├── I2C → ADS1115 ADC
│   └── 3x Bodenfeuchtesensoren (10m Kabel)
└── Solar: 50W Panel + 12V Akku + Laderegler
```

Composition-Flow: Host → `App.axaml.cs` (DI + Pi-Erkennung + Lifecycle-Zweig) → `MainViewModel` (6 Tabs).
Details → [GardenControl.Shared/CLAUDE.md](GardenControl.Shared/CLAUDE.md).
**Werbefrei** → keine `MeineApps.Core.Premium.Ava`-Referenz.

## Projekte

| Projekt | Framework | Zweck |
|---------|-----------|-------|
| `GardenControl.Core` | `net10.0` | Models, DTOs, Enums (geteilt zwischen Client + Server) |
| `GardenControl.Server` | `net10.0` | ASP.NET Core Server für den Pi (linux-arm64) |
| `GardenControl.Shared` | `net10.0` | Avalonia-Client (ViewModels, Views, Services, Controls) |
| `GardenControl.Desktop` | `net10.0` | Desktop Entry Point (auch Kiosk auf Pi) |
| `GardenControl.Android` | `net10.0-android` | Android Entry Point |

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Pi-Erkennung, Lifecycle | `App.axaml.cs`, Service-/VM-Registrierung, Kiosk-/Dev-Zweig | [GardenControl.Shared](GardenControl.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, Back-Button, Manifest/Permissions | [GardenControl.Android](GardenControl.Android/CLAUDE.md) |
| Desktop-Host / Pi-Kiosk | `Program.cs`, Deployment auf Pi | [GardenControl.Desktop](GardenControl.Desktop/CLAUDE.md) |
| Tab-ViewModels, Verbindungsmanagement | MainVM, DashboardVM, ZoneControlVM, ScheduleVM, CalibrationVM, HistoryVM, SettingsVM | [Shared/ViewModels](GardenControl.Shared/ViewModels/CLAUDE.md) |
| Views + View-Converter | MainView, DashboardView, ZoneControlView, … | [Shared/Views](GardenControl.Shared/Views/CLAUDE.md) |
| SignalR-Client, REST-API-Client | ConnectionService, ApiService | [Shared/Services](GardenControl.Shared/Services/CLAUDE.md) |
| SkiaSharp Custom Controls | MoistureGaugeControl, MoistureChartControl | [Shared/Controls](GardenControl.Shared/Controls/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner ohne eigene Doku: `Shared/Themes/` (`AppPalette.axaml`, Grün #2E7D32),
`Shared/Assets/`.

---

## Hardware

- **Raspberry Pi 5 (8GB)** + 7" Touchscreen Display
- **ALAMSCN Kit**: 4-Kanal 5V Relais (Ventile/Pumpe schalten)
- **ADS1115**: 16-Bit ADC (Pi hat keinen analogen Eingang)
- **3x Bodenfeuchtesensoren** (10m Kabel zu den Beeten, leicht bergauf)
- **3x 12V Magnetventile NC** + **1x 12V Tauchpumpe** (5m Förderhöhe)
- **Solar**: 50W Panel + 12V AGM 12Ah + PWM-Laderegler + Buck-Converter 5V
- Details + Einkaufsliste: `docs/HARDWARE.md`

## GPIO-Belegung (BCM)

| Pin | Funktion |
|-----|----------|
| GPIO 2/3 | I2C SDA/SCL → ADS1115 |
| GPIO 17 | Relais Kanal 1 → Ventil Beet 1 |
| GPIO 27 | Relais Kanal 2 → Ventil Beet 2 |
| GPIO 22 | Relais Kanal 3 → Ventil Beet 3 |
| GPIO 23 | Relais Kanal 4 → Pumpe |

## Pi-Kiosk-Modus

- Server als systemd-Service (immer an, auch ohne Display)
- Desktop-App startet automatisch in Fullscreen (Auto-Login + Autostart)
- Install-Skript: `install-pi5.sh` (I2C, .NET, Auto-Login, Kiosk)
- Touch-optimierte Styles (`TouchStyles.axaml`): Min. 44dp Touch-Targets
- Pi-Erkennung und Kiosk-Lifecycle-Zweig → [GardenControl.Shared/CLAUDE.md](GardenControl.Shared/CLAUDE.md)

## Server-Komponenten (GardenControl.Server)

### `Hardware/` (Plattform-Abstraktion)

| Klasse | Aufgabe |
|--------|---------|
| `IGpioService` / `GpioService` | Relais-Steuerung über `System.Device.Gpio` |
| `ISensorService` / `SensorService` | ADC-Werte via `Iot.Device.Ads1115` |
| `MockGpioService` | GPIO-Mock ohne echte Hardware (Desktop-Entwicklung) |
| `MockSensorService` | Sensor-Mock mit simulierten Feuchtewerten (langsames Austrocknen + Rauschen) |

Mock-Erkennung: automatisch wenn `/sys/class/gpio` nicht vorhanden ist (`Program.cs` prüft `Directory.Exists("/sys/class/gpio")`).

### `Services/`

| Service | Aufgabe |
|---------|---------|
| `IIrrigationService` / `IrrigationService` | Bewässerungslogik (Start/Stop, Schwellenwerte, Cooldown) |
| `IDatabaseService` / `DatabaseService` | SQLite (Messwerte, Ereignisse, Zonen, Konfiguration) |
| `SensorPollingWorker` | `BackgroundService` für periodisches Sensor-Polling + SignalR-Push |
| `Weather/` | Wetterdaten-Integration (regenbasiertes Skip der Bewässerung) |

### `Hubs/`

| Hub | Pfad |
|-----|------|
| `GardenHub` | `/hub/garden` — SignalR-Hub, empfängt Befehle und sendet Status-Push |

## Build-Befehle

```bash
# Entwicklung lokal (Mock-Hardware, kein Pi nötig)
dotnet run --project src/Apps/GardenControl/GardenControl.Desktop

# Pi cross-compile (linux-arm64, self-contained)
dotnet publish src/Apps/GardenControl/GardenControl.Server  -c Release -r linux-arm64 --self-contained
dotnet publish src/Apps/GardenControl/GardenControl.Desktop -c Release -r linux-arm64 --self-contained

# Komplett-Deploy auf den Pi
bash src/Apps/GardenControl/GardenControl.Server/Install/deploy.sh gardencontrol.local
```

Pi-Server-Deploy via Skill `/server-deploy` (siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)).
Android-AAB-Build → [GardenControl.Android/CLAUDE.md](GardenControl.Android/CLAUDE.md).

---

## Verweise

| Was | Wo |
|-----|----|
| Build, Conventions, Architektur | [Haupt-CLAUDE.md](../../../CLAUDE.md) |
| Preferences, BackPressHelper, ViewLocator | [MeineApps.Core.Ava](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) |
| SkiaSharp Custom Controls Grundlagen | [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md) |
| `Releases/GardenControl/CHANGELOG_*.md` | Release-Notes |
