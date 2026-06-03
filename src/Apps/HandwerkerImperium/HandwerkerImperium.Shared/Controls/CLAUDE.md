# Controls — App-eigene Custom Controls

App-spezifische Avalonia-Controls die über XAML-Wiederverwendung hinausgehen.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `EmptyStateCard.axaml(.cs)` | Wiederverwendbarer Empty-State: GameIcon + Title + Subtitle + optionaler ActionButton. Genutzt in Achievement, Tournament, Crafting, Research, LiveEvent und weiteren Listen-Views |
| `WorkerAvatarControl.cs` | Custom `Control`-Ableitung (kein TemplatedControl). Hält intern ein `SKCanvasView` und rendert Pixel-Art-Worker-Avatar via `WorkerAvatarRenderer` |

---

## WorkerAvatarControl — Performance-Patterns

Gemeinsame `IFrameClock`-Subscription (`s_sharedFrameClock`) für alle Instanzen —
ein Tick für alle statt N pro-Instanz-Timer. Spart CPU bei Screens mit vielen Worker-Karten.

- Statische `s_bitmapPaint` + `s_blinkPaint` — keine Allokation pro Frame
- `WeakReference<WorkerAvatarControl>`-Liste (`s_instances`) für Auto-Cleanup toter Controls
- `FpsProfile.CurrentChanged`-Event — Control aktualisiert Subscription-Intervall bei Qualitätswechsel via `s_sharedFrameClock.UpdateInterval()`
- Timer-Intervall aus `FpsProfile.WorkerAvatar()` abgeleitet (konkrete fps-Werte → `Graphics/CLAUDE.md`)
- `_isRegistered`-Flag verhindert Duplikat-Einträge in `s_instances` bei wiederholten Property-Änderungen
- Subscription stoppt automatisch wenn `s_instances` leer ist (Battery-Save)
- Cleanup via `OnDetachedFromVisualTree` → `UnregisterFromAnimation()`; Bitmap-Cache wird NICHT hier disposen

---

## Gotcha — EmptyStateCard ActionButton sichtbar

Der ActionButton-Sichtbarkeit wird im Code-Behind gesetzt, nicht per AXAML-Binding:

```csharp
ActionButton.IsVisible = !string.IsNullOrEmpty(ActionText) && ActionCommand != null;
```

Beide Bedingungen müssen erfüllt sein — ein Command ohne ActionText-Text oder umgekehrt
reicht nicht. Das AXAML setzt `IsVisible="False"` als Default; `UpdateBindings()` überschreibt.
