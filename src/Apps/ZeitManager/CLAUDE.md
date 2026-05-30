# ZeitManager — Timer, Stoppuhr, Wecker

Fünf-Tab-Zeitverwaltungs-App: Multi-Timer, Stoppuhr mit Rundenzeiten, Pomodoro, Wecker mit
Challenges und Schichtplan-Rechner. Komplett werbefrei, kein Premium, kein MeineApps.Core.Premium.

| Aspekt | Wert |
|--------|------|
| Package-ID | `com.meineapps.zeitmanager` |
| Preis | Kostenlos (werbefrei) |
| Tabs | Timer, Stoppuhr, Pomodoro, Wecker/Schichtplan, Settings |

> Für generische Build-Befehle, Conventions, Architektur und Packages → [Haupt-CLAUDE.md](../../../CLAUDE.md)

---

## Architektur-Überblick

Drei Projekte, ViewModel-First, kein Service-Locator:

```
ZeitManager.Android ┐
                    ├─> ZeitManager.Shared ──> MeineApps.Core.Ava  (Preferences, Localization, BackPressHelper, ViewLocator)
ZeitManager.Desktop ┘                       └─> MeineApps.UI       (SkiaLoadingSplash, FloatingText, Behaviors, Controls)
```

Composition-Flow: Host (`AndroidApp` / `Program.cs`) → `ZeitManager.Shared/App.axaml.cs`
(DI + `ConfigurePlatformServices`-Hook + Loading-Pipeline + Splash) → `MainViewModel`
(5 Tabs, Overlay, Event-Relay) → `ViewLocator` löst die Views auf.
**Werbefrei** → keine `MeineApps.Core.Premium.Ava`-Referenz.

---

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Platform-Hook | `App.axaml.cs`, Service-/VM-Registrierung, Loading-Start | [ZeitManager.Shared](ZeitManager.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, Platform-Services, Ringtone-Picker, Manifest | [ZeitManager.Android](ZeitManager.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs`, Desktop-Fallbacks | [ZeitManager.Desktop](ZeitManager.Desktop/CLAUDE.md) |
| ViewModels | MainVM (Back-Nav, Event-Relay, Onboarding), Tab-VMs, Overlay | [Shared/ViewModels](ZeitManager.Shared/ViewModels/CLAUDE.md) |
| Views | AXAML, Code-Behind, SkiaSharp-Handler, Content-Swap, Bottom-Sheet-Swipe | [Shared/Views](ZeitManager.Shared/Views/CLAUDE.md) |
| Services | Timer, Alarm, Audio, DB, Shift, Shake, Notification (Interfaces + Impls) | [Shared/Services](ZeitManager.Shared/Services/CLAUDE.md) |
| Audio | WAV-Generator, Sound-Definitionen, StableHash | [Shared/Audio](ZeitManager.Shared/Audio/CLAUDE.md) |
| SkiaSharp-Renderer | 6 Visualisierungen + Splash, Render-Loop-Pattern | [Shared/Graphics](ZeitManager.Shared/Graphics/CLAUDE.md) |
| Startup-Pipeline | DB+Shader parallel, AlarmScheduler, ViewModel-Wait | [Shared/Loading](ZeitManager.Shared/Loading/CLAUDE.md) |
| DB-Entitäten & Enums | TimerItem, AlarmItem, FocusSession, ShiftSchedule u.a. | [Shared/Models](ZeitManager.Shared/Models/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner ohne eigene Doku: `Shared/Themes/` (AppPalette, Amber #F7A833),
`Shared/Resources/Strings/` (AppStrings.resx, 6 Sprachen), `Shared/Assets/`.

---

## Kritische Architektur-Entscheidungen

### Alarm-Overlay — Content-Swap statt ZIndex

Avalonia ZIndex für Hit-Testing auf Android unzuverlässig. `IsAlarmOverlayVisible` steuert
`IsVisible` auf dem Overlay-Panel sowie `IsVisible="{Binding !IsAlarmOverlayVisible}"` auf
normalem Content + Tab-Bar. Kein ZIndex-Overlay.

### Thread-Safety

`System.Timers.Timer` feuert auf ThreadPool → `Dispatcher.UIThread.Post()` für alle UI-Property-
Updates. `TimerService` und `AlarmSchedulerService` nutzen `lock(_lock)` für Listen-Zugriffe.
Detail: [Shared/Services](ZeitManager.Shared/Services/CLAUDE.md).

### Foreground-Callbacks (Android-only)

`TimerService.ForegroundNotificationCallback` + `StopForegroundCallback` werden von
`MainActivity.OnCreate` (nach `base.OnCreate`) direkt auf dem Service-Objekt gesetzt.
Desktop: Callbacks bleiben null, kein Foreground-Service-Aufruf.

---

## Abhängigkeiten

- `MeineApps.Core.Ava`, `MeineApps.UI`
- `sqlite-net-pcl` + `SQLitePCLRaw.bundle_green`
- `SkiaSharp` + `Avalonia.Labs.Controls`
- **Kein `MeineApps.Core.Premium` — komplett werbefrei**

---

## Verweise

| Was | Wo |
|-----|----|
| Build, Conventions, Architektur | [Haupt-CLAUDE.md](../../../CLAUDE.md) |
| Preferences, BackPressHelper, ViewLocator, Localization | [MeineApps.Core.Ava](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) |
| Custom Controls, Behaviors, Loading-Pipeline, SkiaThemeHelper | [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md) |
| Release-Notes | `Releases/ZeitManager/CHANGELOG_*.md` |
