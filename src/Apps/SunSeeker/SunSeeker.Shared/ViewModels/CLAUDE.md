# ViewModels — Navigator + Tab-VMs

ViewModel-First, Constructor Injection, `CommunityToolkit.Mvvm` (`[ObservableProperty]`,
`[RelayCommand]`). Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

| ViewModel | Zweck |
|-----------|-------|
| `MainViewModel` | Navigator: hält die 3 Tab-VMs, `IsAlignActive`/`IsPowerActive`/`IsDashboardActive` + `Show*`-Commands. Ruft bei Tab-Wechsel `Activate()`/`Deactivate()` der sensor-/monitor-gebundenen Tabs (Akku sparen). Default-Tab: Ausrichten. |
| `AlignViewModel` | Live-Ausrichtung: liest `IHeadingService` (Azimut/Neigung), berechnet AOI + Soll-Abweichung, gibt Dreh-/Neigungs-Anweisungen. Hält `SunCompassRenderer`. `IDisposable`. |
| `LivePowerViewModel` | Live-Watt + Trend + Tagesertrag (Wh-Integration) + Spitze. Hält `PowerChartRenderer`. `IDisposable`. Plus Anker-Zugangsdaten-Eingabe (E-Mail/Passwort/Land) + `Connect`/`Forget`/`ToggleSettings`-Commands; speichert via `AnkerCredentialStore` und triggert Reconnect (echte Anbindung → [Services/Anker](../Services/Anker/CLAUDE.md)). |
| `DashboardViewModel` | Übersicht: Standort, Sonnenstand, Sonnenbahn (`SunPathRenderer`), Sonnenzeiten, Empfehlung, Bifazial. App-langer Timer. |

`GoalOption`/`GroundOption` (Records) leben in `DashboardViewModel.cs` — Enum + lokalisiertes Label.

---

## Patterns

- **Sensor-/Monitor-Lifecycle:** `Activate()` startet die Hardware (`IHeadingService.Start` /
  `IAnkerMonitorService.ConnectAsync`) + einen Sekunden-Timer, `Deactivate()` stoppt sie. Vom
  Navigator bei Tab-Wechsel getrieben. `Dispose()` meldet Events ab + disposed den Renderer.
  **GPS-Ausnahme:** Der Standort ist NICHT tab-gebunden (Ausrichten + Übersicht brauchen ihn) — er
  läuft am Vordergrund-Lifecycle des Android-Hosts (`MainActivity.OnResume/OnPause`), daher startet/stoppt
  `AlignViewModel` nur den Heading-Sensor, nicht `ILocationService`.
- **Thread-Marshalling:** Sensor-/Monitor-Events kommen vom Background-Thread → `Dispatcher.UIThread.Post`.
- **Renderer-Invalidate:** VM hält den SkiaSharp-Renderer + feuert ein `*InvalidateRequested`-Event;
  das View-Code-Behind ruft `InvalidateSurface()`.
- **Lokalisierung:** dynamische Strings über `ILocalizationService.GetString(key)` + `string.Format`
  (Zahlen mit `CultureInfo.CurrentCulture` formatieren). Statische Labels macht die View via
  `{loc:Translate}`. Erklärungs-/Tipp-/Quality-Texte werden aus Engine-Keys lokalisiert.
- **Konvention Panel-Azimut** (Align): `PanelAzimuth = HeadingReading.DeviceAzimuth` (Handy mit
  Display flach an die Panel-Vorderseite).
- **Ausricht-Ziel = OPTIMALE Neigung, nicht der gesnappte Kickstand** (Align): Marker, Soll-Text und
  Neigungs-Hinweis zielen auf `AlignmentRecommendation.TargetTilt` (Saison-/Sonnen-Optimum), NICHT auf
  `RecommendedKickstandTilt`. Sonst würde z.B. „Jetzt maximal" vom echten Sonnen-Zenit weg auf 35°
  „korrigieren". Bei festem Standwinkel erklärt `BuildKickstandHint()` (Keys `KickstandHintAlign`/
  `KickstandSlopeSteeper`/`KickstandSlopeFlatter`), die Differenz Optimum↔Kickstand über die Aufstell-
  Neigung (Hang) zu holen — Vorderseite leicht bergauf = steiler, bergab = flacher. Der Neigungssensor
  misst die ECHTE effektive Neigung inkl. Hang, die Ampel bewertet gegen das Optimum.
