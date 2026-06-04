# ViewModels — Navigator + Tab-VMs

ViewModel-First, Constructor Injection, `CommunityToolkit.Mvvm` (`[ObservableProperty]`,
`[RelayCommand]`). Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

| ViewModel | Zweck |
|-----------|-------|
| `MainViewModel` | Navigator: hält die 3 Tab-VMs, `IsAlignActive`/`IsPowerActive`/`IsDashboardActive` + `Show*`-Commands. Ruft bei Tab-Wechsel `Activate()`/`Deactivate()` der sensor-/monitor-gebundenen Tabs (Akku sparen). Default-Tab: Ausrichten. |
| `AlignViewModel` | Live-Ausrichtung: liest `IHeadingService` (Azimut/Neigung), berechnet AOI + Soll-Abweichung, gibt Dreh-/Neigungs-Anweisungen. Hält `SunCompassRenderer`. `IDisposable`. |
| `LivePowerViewModel` | Live-Watt + Trend + Tagesertrag (Wh-Integration) + Spitze. Hält `PowerChartRenderer`. `IDisposable`. |
| `DashboardViewModel` | Übersicht: Standort, Sonnenstand, Sonnenbahn (`SunPathRenderer`), Sonnenzeiten, Empfehlung, Bifazial. App-langer Timer. |

`GoalOption`/`GroundOption` (Records) leben in `DashboardViewModel.cs` — Enum + lokalisiertes Label.

---

## Patterns

- **Sensor-/Monitor-Lifecycle:** `Activate()` startet die Hardware (`IHeadingService.Start` /
  `IAnkerMonitorService.ConnectAsync`) + einen Sekunden-Timer, `Deactivate()` stoppt sie. Vom
  Navigator bei Tab-Wechsel getrieben. `Dispose()` meldet Events ab + disposed den Renderer.
- **Thread-Marshalling:** Sensor-/Monitor-Events kommen vom Background-Thread → `Dispatcher.UIThread.Post`.
- **Renderer-Invalidate:** VM hält den SkiaSharp-Renderer + feuert ein `*InvalidateRequested`-Event;
  das View-Code-Behind ruft `InvalidateSurface()`.
- **Lokalisierung:** dynamische Strings über `ILocalizationService.GetString(key)` + `string.Format`
  (Zahlen mit `CultureInfo.CurrentCulture` formatieren). Statische Labels macht die View via
  `{loc:Translate}`. Erklärungs-/Tipp-/Quality-Texte werden aus Engine-Keys lokalisiert.
- **Konvention Panel-Azimut** (Align): `PanelAzimuth = HeadingReading.DeviceAzimuth` (Handy mit
  Display flach an die Panel-Vorderseite).
