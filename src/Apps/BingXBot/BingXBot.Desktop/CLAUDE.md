# BingXBot.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). Kann sowohl als **Standalone** (Engine läuft
lokal) als auch als **Remote-Client** (verbindet sich zum Pi-Server) gestartet werden — je nachdem
ob `~/.config/bingxbot/client/connection.json` (Linux) bzw. `%APPDATA%\bingxbot\client\connection.json`
(Windows) existiert. Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`BingXBot.Shared/App.axaml.cs`). Keine plattformspezifischen Service-Implementierungen nötig —
`IAppPaths` fällt auf `AppPaths` (Standard-Umgebungspfade) zurück.

## Modus-Auswahl

Der Modus (Standalone vs. Remote-Client) wird NICHT per Kommandozeile gesteuert, sondern
über das Vorhandensein der `connection.json`-Datei:

- **Datei fehlt** → Standalone (Engine im Prozess, BingX-Credentials lokal via DPAPI).
- **Datei vorhanden** → Remote-Client (HTTP + SignalR, keine lokale Engine).

Pairing über Settings → "Server verbinden" in der App.

## Build / Run

```bash
dotnet run     --project src/Apps/BingXBot/BingXBot.Desktop
dotnet publish src/Apps/BingXBot/BingXBot.Desktop -c Release -r win-x64 --self-contained true
dotnet publish src/Apps/BingXBot/BingXBot.Desktop -c Release -r linux-x64 --self-contained true
# Ausgabe: F:\BingXBot-Client\
```
