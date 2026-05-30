# Views — AXAML-Views & Code-Behind

Alle Views folgen ViewModel-First: `DataContext` kommt vom `ViewLocator` oder wird vom
`MainView`-Code-Behind per `DataContextChanged`-Pattern verdrahtet. `x:CompileBindings="True"`
auf jeder View-Root. Generische UI-Patterns → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Root-View. `ClockworkBackgroundRenderer` per 200ms-DispatcherTimer (~5fps). Onboarding (2-Schritt TooltipBubble). FloatingText + Celebration via VM-Events. |
| `TimerView.axaml(.cs)` | Timer-Liste + Quick-Timer. `TimerVisualization`-Render-Loop (30fps) nur wenn `vm.ShowVisualization == true`. Render-Loop über `OnViewModelPropertyChanged` gestartet/gestoppt. |
| `StopwatchView.axaml(.cs)` | Stoppuhr-Ring. `StopwatchVisualization` invalidiert selektiv bei `TotalElapsedSeconds`, `IsRunning` oder `Laps`. 30fps-Timer nur bei `IsRunning`. |
| `PomodoroView.axaml(.cs)` | Pomodoro-Ring + Statistiken. Drei separate Canvases (PomodoroRingCanvas, WeeklyBarsCanvas, HeatmapCanvas) mit selektiver Invalidierung je Property. Config-Bottom-Sheet mit Swipe-to-Dismiss (Spring-Animation via `DispatcherTimer`). |
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
OnboardingTooltip.Text = _vm.OnboardingQuickTimerText;
```
Nach Schritt 2: `_vm.MarkOnboardingCompleted()`.

## SkiaSharp-Render-Loop-Pattern

Alle Views mit SkiaSharp-Animationen verwenden dasselbe Pattern:
1. `OnDataContextChanged`: alten VM abmelden, neuen anmelden via `PropertyChanged`.
2. `OnViewModelPropertyChanged`: selektiv `Canvas?.InvalidateSurface()` + Timer starten/stoppen.
3. Timer nur so lange aktiv wie nötig (z.B. Stoppuhr: nur bei `IsRunning`).
4. `OnDetachedFromVisualTree`: Handler abmelden + Timer stoppen (Memory-Leak-Prävention).

```csharp
// RICHTIG: Selektive Invalidierung
if (args.PropertyName is nameof(vm.TotalElapsedSeconds) or nameof(vm.IsRunning))
    StopwatchCanvas?.InvalidateSurface();
```

## Alarm-Overlay — Content-Swap statt ZIndex

`IsAlarmOverlayVisible` steuert `IsVisible` auf dem Alarm-Overlay-Panel SOWIE
`IsVisible="{Binding !IsAlarmOverlayVisible}"` auf dem normalen Content + Tab-Bar.
Grund: Avalonia ZIndex für Hit-Testing auf Android nicht zuverlässig.

## PomodoroView — Config-Bottom-Sheet Swipe

`PointerPressed`/`PointerMoved`/`PointerReleased` auf der Drag-Zone des Sheets.
`TranslateTransform.Y` verschiebt das Sheet nach unten. Bei `>= 80dp` Dismiss, sonst
Spring-Back (CubicEaseOut, 10 Frames × 16ms). `PointerCaptureLost` federt ebenfalls zurück.
