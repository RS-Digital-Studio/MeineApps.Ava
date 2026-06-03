# SmartMeasure.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first (Samsung Galaxy S25 Ultra als Zielgerät).
App-Überblick, Build-Befehle, generische Conventions → [App-CLAUDE.md](../CLAUDE.md).

---

## Einstiegspunkt

`Program.cs`: `AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()`
→ `StartWithClassicDesktopLifetime(args)`.

Desktop nutzt die Shared-Fallbacks aus `App.axaml.cs` (Factories null → Mocks):
- `IBleService` → `MockBleService` (simuliert RTK-Daten + Edge-Cases)
- `IArCaptureService` → `MockArCaptureService` (deterministischer Seed, 12×8 m Grundstück)
- `IVoiceAnnotationService` → `NullVoiceAnnotationService`
- `IAppPaths` → `AppPaths` (`Environment.SpecialFolder.LocalApplicationData`)

Warum Fallbacks statt Plattform-Factories: `MainActivity.OnCreate` (der Factory-Setter) gibt
es auf Desktop nicht — `App.axaml.cs` prüft zur DI-Build-Zeit ob eine Factory gesetzt ist und
weicht auf den Mock aus. Details zum Lazy-Factory-Pattern → [Shared-CLAUDE.md](../SmartMeasure.Shared/CLAUDE.md).

---

## Mock-Debug-Panel (`SurveyView`)

Bei `IsMockMode = true` schaltet die `SurveyView` ein Debug-Panel frei. Buttons für
Edge-Cases, die im Feld nicht reproduzierbar sind:

| Button | `MockBleService`-Methode | Testet |
|--------|--------------------------|--------|
| CycleFixDegradation | `CycleFixDegradation()` | Fix 4 → 5 → 2 → 0 → 4 (RTK/Float/DGPS/NoFix) |
| SimulatePacketLoss | `SimulatePacketLoss(int seconds)` | Position-Updates einfrieren |
| SimulateBatteryDrain | `SimulateBatteryDrain()` | ~3 %/s bis 15 % (Low-Battery-Warnung) |
| SimulateMagLoss | `SimulateMagLoss()` | MagAccuracy 0 → Kompass-Warnung + Tilt-Korrektur nur vertikal |
| SimulateSpuriousDisconnect | `SimulateSpuriousDisconnect()` | Unerwarteter Disconnect ohne User-Aktion |
