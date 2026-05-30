# Controls — App-eigene Custom Controls

App-spezifische Avalonia-Controls die über XAML-Wiederverwendung hinausgehen.

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `EmptyStateCard.axaml(.cs)` | Wiederverwendbarer Empty-State: Icon + Title + Subtitle + optionaler ActionButton. Wird in Listen-Views (Aufträge, Gilde, Markt) genutzt wenn keine Einträge vorhanden |
| `WorkerAvatarControl.cs` | Custom `Control`-Ableitung (kein TemplatedControl, kein SKCanvasView direkt). Rendert Pixel-Art-Worker-Avatar via `WorkerAvatarRenderer`. |

---

## WorkerAvatarControl — Performance-Patterns

Gemeinsamer statischer `DispatcherTimer` (`s_sharedTimer`) für alle Instanzen —
ein Tick für alle statt N pro-Instanz-Timer. Spart CPU bei Screens mit vielen Worker-Karten.

- Statische `s_bitmapPaint` + `s_blinkPaint` — keine Allokation pro Frame
- `WeakReference<WorkerAvatarControl>`-Liste (`s_instances`) für Auto-Cleanup toter Controls
- `FpsProfile.CurrentChanged`-Event — Control subscribed für Live-FPS-Anpassung bei Qualitätswechsel
- Timer-Intervall direkt aus `FpsProfile.Current` abgeleitet (WorkerAvatar: Low=5fps, Medium=8fps, High=10fps)

---

## Gotcha — EmptyStateCard ActionButton sichtbar

Der ActionButton in `EmptyStateCard` ist nur sichtbar wenn `ActionCommand != null`.
Binding: `IsVisible="{Binding ActionCommand, Converter={x:Static ObjectConverters.IsNotNull}}"`.
Kein `IsVisible`-Property nötig — Binding auf Command reicht.
