# Views — AXAML-Views & Code-Behind

Alle Views folgen ViewModel-First: `DataContext` kommt vom `ViewLocator` oder wird vom
`MainView`-Code-Behind per `DataContextChanged`-Pattern verdrahtet. `x:CompileBindings="True"`
auf jeder View-Root. Generische UI-Patterns → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Root-View. `ClockworkBackgroundRenderer` per 200ms-`DispatcherTimer` (~5fps, immer aktiv, startet in `OnAttachedToVisualTree`). Onboarding (2-Schritt TooltipBubble). FloatingText + Celebration via VM-Events. |
| `TimerView.axaml(.cs)` | Timer-Liste + Quick-Timer. `TimerVisualization`-Render-Loop (30fps) nur wenn `vm.ShowVisualization == true`. Render-Loop über `OnViewModelPropertyChanged` gestartet/gestoppt. |
| `StopwatchView.axaml(.cs)` | Stoppuhr-Ring. `StopwatchVisualization` invalidiert selektiv bei `TotalElapsedSeconds`, `IsRunning` oder `Laps`. 30fps-Timer nur bei `IsRunning`. |
| `PomodoroView.axaml(.cs)` | Pomodoro-Ring + Statistiken. Vier Canvases: `PomodoroRingCanvas`, `WeeklyBarsCanvas`, `HeatmapCanvas` (selektive Invalidierung je Property) + Balken-Einfahranimation (CubicEaseOut, ~500ms, 33ms-Takt). Config-Bottom-Sheet mit Swipe-to-Dismiss (Spring-Back via `DispatcherTimer`, 10 Frames × 16ms). |
| `AlarmView.axaml(.cs)` | Alarm-Liste, Editor-Bottom-Sheet, Schichtplan-Einbettung. |
| `AlarmOverlayView.axaml(.cs)` | Fullscreen-Alarm-Overlay (Content-Swap, kein ZIndex). Nur Dismiss/Snooze-Buttons — kein Back-Press. |
| `ShiftScheduleView.axaml(.cs)` | Schichtplan-Kalender mit Ausnahmen. Eingebettet im AlarmTab über `IsShiftScheduleMode`. |
| `SettingsView.axaml(.cs)` | Sprache, Vibration, Snooze. |
| `MainWindow.axaml(.cs)` | Desktop-only Wrapper-Window. |

## MainView — Onboarding

2-Schritt-Onboarding via `TooltipBubble` aus `MeineApps.UI`. Startet 800ms nach VM-Attach
(Splash-FadeOut abgewartet). VM-Snapshot vor Delay verhindert Race bei schnellem VM-Wechsel.
Completion-State und lokalisierte Texte kommen aus `MainViewModel` (nicht Code-Behind):
```csharp
if (_vm.IsOnboardingCompleted) return;
OnboardingTooltip.Text = _vm.OnboardingQuickTimerText;  // Schritt 1
OnboardingTooltip.Text = _vm.OnboardingCreateTimerText; // Schritt 2
```
Nach Schritt 2: `_vm.MarkOnboardingCompleted()`.

## SkiaSharp-Render-Loop-Pattern

`TimerView`, `StopwatchView` und `PomodoroView` verwenden dasselbe property-gesteuerte Pattern:
1. `OnDataContextChanged` (Override): alten VM abmelden, neuen anmelden via `PropertyChanged`.
2. `OnViewModelPropertyChanged`: selektiv `Canvas?.InvalidateSurface()` + Timer starten/stoppen.
3. Timer nur so lange aktiv wie nötig (Stoppuhr/Pomodoro: nur bei `IsRunning`, Timer: nur bei `ShowVisualization`).
4. `OnDetachedFromVisualTree`: Handler abmelden + Timer stoppen (Memory-Leak-Prävention).

`MainView` weicht ab: Der Hintergrund-Timer ist **immer aktiv** und startet in
`OnAttachedToVisualTree` (kein Property-Trigger nötig, da der Clockwork-Hintergrund dauerhaft animiert ist).

```csharp
// RICHTIG: Selektive Invalidierung (StopwatchView)
if (args.PropertyName is nameof(vm.TotalElapsedSeconds) or nameof(vm.IsRunning) or nameof(vm.Laps))
    StopwatchCanvas?.InvalidateSurface();
```

## Alarm-Overlay

Content-Swap statt ZIndex → Architektur-Entscheidung dokumentiert in [../CLAUDE.md](../CLAUDE.md).
