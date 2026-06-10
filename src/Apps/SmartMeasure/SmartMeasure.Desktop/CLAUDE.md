# SmartMeasure.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first (Samsung Galaxy S25 Ultra als Zielgerät).
App-Überblick, Build-Befehle, generische Conventions → [App-CLAUDE.md](../CLAUDE.md).

---

## Einstiegspunkt

`Program.cs`: `AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()`
→ `StartWithClassicDesktopLifetime(args)`.

Desktop nutzt die Shared-Fallbacks aus `App.axaml.cs` (Factories null → Mocks):
- `IArCaptureService` → `MockArCaptureService` (deterministischer Seed, 12×8 m Grundstück,
  optional `SimulateGeospatial` für den VPS-Pfad und `SimulateNoisyPoint` für die
  Confidence-Pipeline)
- `IVoiceAnnotationService` → `NullVoiceAnnotationService`
- `IAppPaths` → `AppPaths` (`Environment.SpecialFolder.LocalApplicationData`)

Warum Fallbacks statt Plattform-Factories: `MainActivity.OnCreate` (der Factory-Setter) gibt
es auf Desktop nicht — `App.axaml.cs` liest die Factory lazy im Resolve-Lambda und
weicht auf den Mock aus. Details zum Lazy-Factory-Pattern → [Shared-CLAUDE.md](../SmartMeasure.Shared/CLAUDE.md).
