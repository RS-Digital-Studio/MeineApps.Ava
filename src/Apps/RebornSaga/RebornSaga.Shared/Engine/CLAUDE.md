# Engine — Szenen-Framework

Kern-Framework des Spiels: Scene-Abstraktion, SceneManager (Stack + Overlays + Transitions),
InputManager und Camera. Keine Spiellogik — nur Infrastruktur.
SkiaSharp-Gotchas → [MeineApps.UI/CLAUDE.md](../../../../UI/MeineApps.UI/CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Scene.cs` | Abstrakte Basisklasse: Lifecycle (`OnEnter/Exit/Pause/Resume`), Game-Loop (`Update/Render`), Input (`HandleInput/HandlePointerDown/Move/Up`), `ConsumesInput`-Property. |
| `SceneManager.cs` | Scene-Stack + Overlays-Liste, Szenen-Wechsel mit Transitions, `ActivatorUtilities.CreateInstance<T>()` für Constructor Injection. |
| `InputManager.cs` | Pointer-Events → `InputAction` (Tap, Hold, Swipe, Drag) → delegiert an aktive Szene. |
| `Camera.cs` | Viewport-Kamera: Pan, Zoom, Screen-Shake. |
| `InputAction.cs` | Enum: Tap, Hold, SwipeLeft/Right/Up/Down, Drag. |
| `Transitions/TransitionEffect.cs` | Abstrakte Basis: `Update(dt)`, `Render(canvas, bounds)`, `IsComplete`. |
| `Transitions/FadeTransition.cs` | Schwarzer Fade (Opacity 0→1→0). |
| `Transitions/SlideTransition.cs` | Horizontales Hineinschieben. |
| `Transitions/GlitchCutTransition.cs` | Horizontale RGB-Verschiebung (Anime-Glitch-Ästhetik). |
| `Transitions/DissolveTransition.cs` | Pixel-Dissolve — `SKPath` als Instanzfeld mit `Rewind()` statt `new` pro Frame. |
| `Transitions/MangaWipeTransition.cs` | Diagonaler Manga-Panel-Wipe. |
| `Transitions/IrisTransition.cs` | Kreisförmige Ein-/Ausblendung (Iris-Blende). |

## Scene-Lifecycle

```
OnEnter() → Update(dt) / Render(canvas, bounds) / HandleInput() → OnPause() ↔ OnResume() → OnExit()
```

| Methode | Wann aufrufen |
|---------|---------------|
| `ChangeScene<T>()` | Ersetzt aktive Szene vollständig (alte: OnExit, neue: OnEnter) |
| `PushScene<T>()` | Neue Szene oben drauflegen (alte: OnPause) |
| `PopScene()` | Obere Szene entfernen (untere: OnResume) |
| `ShowOverlay<T>()` | Transparentes Overlay einblenden |
| `HideOverlay(overlay)` | Spezifisches Overlay ausblenden |

**`ConsumesInput = false`:** Overlay reicht Input durch (Beispiel: `EffectFeedbackOverlay`).
Szenen werden via `ActivatorUtilities.CreateInstance<T>(serviceProvider)` mit Constructor
Injection erstellt — kein `new Szene()`.

## Camera

`Camera.ApplyTransform(canvas, bounds)` setzt `canvas.Translate/Scale` vor dem Szenen-Render.
`Shake(intensity, duration)` → Translation-Offset mit Exponential-Decay.
Properties: `X`, `Y`, `Zoom`. `Update(dt)` reduziert Shake-Amplitude pro Frame.

## InputManager

Erkennt Gesten aus rohen Pointer-Events: Tap (<200ms, <10px Move), Hold (>500ms), Swipe
(>50px in eine Richtung). Gibt `InputAction` + Position an `SceneManager.HandleInput()` weiter.
