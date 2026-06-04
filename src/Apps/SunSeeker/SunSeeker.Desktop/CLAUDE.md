# SunSeeker.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App ist
Android-first. App-Überblick, generische Conventions → [App-CLAUDE.md](../CLAUDE.md).

---

## Einstiegspunkt

`Program.cs`: `AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()`
→ `StartWithClassicDesktopLifetime(args)`.

Desktop nutzt die Shared-Mock-Fallbacks (Plattform-Factories bleiben null, da es kein
`MainActivity.OnCreate` gibt):

- `ILocationService` → `MockLocationService` (fester Standort Berlin, per `SetLocation` änderbar)
- `IHeadingService` → `MockHeadingService` (feste Messung Süd/35°, per `SetReading` änderbar)
- `IAnkerMonitorService` → `MockAnkerMonitorService` (Watt aus Sonnenstand simuliert)

Fenster: 450×900 (Telefon-Format). Sensoren sind auf Desktop simuliert — die Live-Ausrichtung
zeigt feste Mock-Werte; der Sonnenstand, die Sonnenbahn und die Leistung laufen realistisch.

---

## Build

```bash
dotnet run --project src/Apps/SunSeeker/SunSeeker.Desktop
```
