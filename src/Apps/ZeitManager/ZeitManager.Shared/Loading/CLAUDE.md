# Loading — Startup-Pipeline

Sequentielle Lade-Pipeline mit Fortschrittsanzeige auf dem `SkiaLoadingSplash`. Verhindert
fire-and-forget-Initialisierung und stellt sicher, dass alle Daten geladen sind bevor der
Splash verschwindet. Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `ZeitManagerLoadingPipeline.cs` | `LoadingPipelineBase`-Subklasse (aus `MeineApps.UI.Loading`). 3 Schritte, Gewichtung nach tatsächlichen Ladezeiten auf Android. |

## Pipeline-Schritte

| Schritt | Name | Gewicht | Was |
|---------|------|---------|-----|
| 1 | `DB+Shader` | 40 | `IDatabaseService.InitializeAsync()` + `ShaderPreloader.PreloadAll()` parallel (`Task.WhenAll`). Größter Zeitblock. |
| 2 | `AlarmScheduler` | 8 | `IAlarmSchedulerService.InitializeAsync()` — lädt Alarme aus DB, startet 60s-Check-Timer, prüft Alarm-Permission. |
| 3 | `ViewModel` | 20 | `GetRequiredService<MainViewModel>()` (löst alle Child-VMs aus). `WaitForInitializationAsync()` wartet auf Timer- + Alarm-DB-Laden. |

## Warum parallel in Schritt 1?

DB-Init und Shader-Kompilierung sind voneinander unabhängig. Auf Mid-Tier-Android dauern
beide ~100-200ms. Parallel spart messbare Zeit bei jedem Kaltstart. AlarmScheduler braucht
die fertige DB (Schritt 2 nach Schritt 1).

## Mindest-Splash-Dauer

`App.axaml.cs` misst die Pipeline-Laufzeit und ergänzt auf mindestens 800ms:
```csharp
var remaining = 800 - (int)sw.ElapsedMilliseconds;
if (remaining > 0) await Task.Delay(remaining);
```
Verhindert Flash-of-Empty-Content wenn Pipeline sehr schnell durchläuft (z.B. zweiter Start
nach warmem Cache).
