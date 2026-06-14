# Engine — Szenen-Framework

Kern-Framework des Spiels: Scene-Abstraktion, SceneManager (Stack + Overlays + Transitions),
InputManager und Camera. Keine Spiellogik — nur Infrastruktur.
SkiaSharp-Gotchas → [MeineApps.UI/CLAUDE.md](../../../../UI/MeineApps.UI/CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Scene.cs` | Abstrakte Basisklasse: Lifecycle (`OnEnter/Exit/Pause/Resume`), Game-Loop (`Update/Render`), Input (`HandleInput/HandlePointerDown/Move/Up`), `ConsumesInput`- und `NeedsContinuousRender`-Property, `RequestRedraw()`. |
| `SceneManager.cs` | Scene-Stack + Overlays-Liste, Szenen-Wechsel mit Transitions, `ActivatorUtilities.CreateInstance<T>()` für Constructor Injection, `ShouldRender()` (Bedarfs-Rendering). |
| `InputManager.cs` | Pointer-Events → `InputAction` (Tap, DoubleTap, Hold, Swipe, Back) → delegiert an aktive Szene. Desktop: Keyboard-Events via `OnKeyDown`. |
| `Camera.cs` | Viewport-Kamera: Pan, Zoom, Screen-Shake. |
| `InputAction.cs` | Enum: Tap, DoubleTap, Hold, Back, SwipeLeft/Right/Up/Down. |
| `Transitions/TransitionEffect.cs` | Abstrakte Basis: `Update(dt)`, `Render(canvas, bounds, renderOldScene, renderNewScene)`, `IsComplete`, `Reset()`. |
| `Transitions/FadeTransition.cs` | Schwarzer Fade (Opacity 0→1→0). |
| `Transitions/SlideTransition.cs` | Horizontales Hineinschieben. |
| `Transitions/GlitchCutTransition.cs` | Horizontale RGB-Verschiebung (Anime-Glitch-Ästhetik). |
| `Transitions/DissolveTransition.cs` | Pixel-Dissolve — `SKPath` als Instanzfeld mit `Rewind()` statt `new` pro Frame. Vorgeneriertes Noise-Grid für deterministisches Auflösungsmuster. |
| `Transitions/MangaWipeTransition.cs` | Diagonaler Manga-Panel-Wipe. |
| `Transitions/IrisTransition.cs` | Kreisförmige Ein-/Ausblendung (Iris-Blende). |

## Scene-Lifecycle

```
OnEnter() → Update(dt) / Render(canvas, bounds) / HandleInput() → OnPause() ↔ OnResume() → OnExit()
```

| Methode | Wann aufrufen |
|---------|---------------|
| `ChangeScene<T>(transition?)` | Ersetzt aktive Szene vollständig (alte: OnExit, neue: OnEnter) |
| `ChangeScene<T>(configure, transition?)` | Wie oben, aber `configure` wird nach Erstellung, VOR OnEnter aufgerufen |
| `PushScene<T>(transition?)` | Neue Szene oben drauflegen (alte: OnPause) |
| `PopScene(transition?)` | Obere Szene entfernen (untere: OnResume) |
| `ShowOverlay<T>()` | Transparentes Overlay einblenden, gibt die Overlay-Instanz zurück |
| `HideOverlay<T>()` | Erstes Overlay dieses Typs ausblenden |
| `HideOverlay(overlay)` | Spezifisches Overlay per Referenz ausblenden |

**`ConsumesInput = false`:** Overlay reicht Input durch (Beispiel: `EffectFeedbackOverlay`).
Szenen werden via `ActivatorUtilities.CreateInstance<T>(serviceProvider)` mit Constructor
Injection erstellt — kein `new Szene()`.

**Re-Entrancy-Guard:** `ChangeScene`, `PushScene` und `PopScene` werden während einer laufenden
Transition ignoriert (`IsTransitioning`-Check). Input wird während Transitions ebenfalls blockiert.

## Bedarfs-Rendering (Akku)

Der Game-Loop ruft `_vm.Update(dt)` jeden Tick (Logik läuft immer), aber `InvalidateSurface()`
nur, wenn `SceneManager.ShouldRender()` true liefert. So sparen statische Szenen (Menüs, Listen)
den teuren Paint, ohne dass Timer/Cooldowns stehenbleiben.

- **`Scene.NeedsContinuousRender`** (virtual, Default **`true`** — sicherer Default): Szenen mit
  kontinuierlicher Animation (Partikel, Typewriter, Tweens, Pulse) lassen ihn `true`. Statische
  Szenen überschreiben mit `false`.
- **`Scene.RequestRedraw()`** (protected): Eine `false`-Szene ruft das bei JEDER sichtbaren
  Zustandsänderung (Cursor/Tab/Wert/Scroll/Slider-Drag), um genau einen Frame zu erzwingen —
  sonst „klemmt" die Anzeige. Welche Szenen `false` sind → [Scenes/CLAUDE.md](../Scenes/CLAUDE.md).
- **`SceneManager.ShouldRender()`** zeichnet immer bei laufender Transition oder aktiven Overlays
  (konservativ); sonst nur, wenn die aktuelle Szene `NeedsContinuousRender` ist oder ein Redraw
  angefordert wurde. Verbraucht das einmalige Redraw-Flag.
- **`Scene.RequestRedrawExternal()`** (internal): Der SceneManager erzwingt damit zentral einen
  Frame nach Szenen-Aktivierung (`ChangeScene`/`PushScene`), `PopScene`/`OnResume` und
  `HideOverlay` — so muss keine statische Szene das selbst absichern, und ein geschlossenes
  Overlay verschwindet zuverlässig. Overlays öffnen aktuell nur continuous Szenen (Overworld,
  Dialogue, Battle), die statischen Szenen liegen nie unter einem Overlay.

## Transitions

`TransitionEffect.Render` erhält zwei Render-Callbacks: `renderOldScene` und `renderNewScene`
(`Action<SKCanvas, SKRect>`). Die neue Szene wird **vor** dem Transitions-Start via `OnEnter`
initialisiert, damit sie im Transition-Render bereits vollständig gezeichnet werden kann.

## Camera

`Camera.ApplyTransform(canvas, bounds)` setzt `canvas.Translate/Scale` vor dem Szenen-Render.
Der Shake-Offset nimmt **linear** ab (Faktor = `_shakeTimer / _shakeDuration`).
`Shake(intensity, duration)` startet den Effekt.
Properties: `X`, `Y`, `Zoom`, `ShakeOffsetX`, `ShakeOffsetY`. `Update(dt)` zählt den Shake-Timer herunter.

## InputManager

Schwellwerte (aus dem Code):

| Geste | Bedingung |
|-------|-----------|
| Tap | < 300ms gedrückt, < 30px bewegt |
| DoubleTap | Zweiter Tap innerhalb 300ms nach dem ersten |
| Hold | ≥ 500ms gedrückt, < 30px bewegt |
| Swipe | ≥ 30px bewegt, dominant-Achse bestimmt Richtung |
| Back | Keyboard: Escape |

Gibt `InputAction` + Position an `SceneManager.HandleInput()` weiter.
Desktop-Keyboard: `OnKeyDown` übersetzt WASD + Pfeiltasten, Enter/Space, Escape in InputActions.
