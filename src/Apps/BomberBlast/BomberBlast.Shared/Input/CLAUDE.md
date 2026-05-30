# Input — Eingabe-System

Verwaltet alle Eingabegeräte: Touch-Joystick, Tastatur und Gamepad.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `InputManager.cs` | Aktiven Handler halten, Auto-Switch zwischen Touch/Keyboard/Gamepad, `IDisposable` |
| `NeonJoystick.cs` | Custom Touch-Joystick (SKCanvasView-basiert), Floating/Fixed-Modi |
| `KeyboardHandler.cs` | Pfeiltasten/WASD + Space (Bombe) + E (Detonate) + T (ToggleSpecialBomb) + Escape (Pause) |
| `GamepadHandler.cs` | D-Pad + Analog-Stick (4-Wege, Deadzone 0.25) + Face-Buttons |
| `IInputHandler.cs` | Interface für alle Handler |
| `InputType.cs` | Enum: Touch / Keyboard / Gamepad |
| `GamepadButton.cs` | Enum: A / B / X / Y / Start / Select |
| `KonamiCodeDetector.cs` | Easter-Egg: Up Up Down Down Left Right Left Right Bomb Detonate → 1500 Coins |

---

## InputManager

### Auto-Switch-Logik

- Touch-Event → aktiviert `NeonJoystick`
- WASD-Key-Event → aktiviert `KeyboardHandler`
- Gamepad-Button-Event → aktiviert `GamepadHandler`

`InputManager.Dispose()` ist **idempotent** (`_disposed`-Guard).
`GameEngine` disposes ihn **nicht** — Lifetime gehört dem DI-Container.
`App.DisposeServices()` disposed ihn explizit.

---

## NeonJoystick

Neon-Arcade-Optik: Oktagonal, Orange-Glow `#FF6B35`, Cyan-Akzent `#22D3EE`, Gold-Trail `#FFDD33`.

### Modi

| Modus | Beschreibung |
|-------|-------------|
| Floating | Erscheint bei erstem Touch, linke 60% des Screens |
| Fixed | Immer sichtbar unten links (Default für Neuinstallationen — bessere 4-Wege-Bomberman-Bewegung) |

### Technisch

- Radius 75dp, Bomb-Button 52dp, Detonator-Button 48dp
- Deadzone: Fixed 15% / Floating 5%, Richtungs-Hysterese 1.15×
- **Separate Pointer-IDs** (`_bombButtonPointerId`, `_detonatorPointerId`) — verhindert
  Button-Hang bei gleichzeitigem Multi-Touch-Tap
- **BombPressed-Race-Schutz**: `OnTouchEnd` setzt `_bombPressed/_detonatePressed`
  sofort nach Konsum auf false — Taps < 16ms bleiben nicht hängen
- **Performance**: 3 statisch gecachte `SKMaskFilter`, `SKPath` via `Rewind()`,
  Arrow-Path einmal gebaut + zweimal gezeichnet (Glow + Fill)
- **SoftGlow-Skip**: alle 2 Frames für Bomb/Detonator (bei Press immer), spart 2-4ms GPU

---

## Pre-Turn Buffering (`Player.cs`)

Richtung wird gepuffert wenn Spieler nicht am Zellzentrum ist. Turn bei 40% Zellzentrum-Nähe.
Sorgt für responsive Eingabe trotz Grid-basierter Bewegung.

---

## KonamiCodeDetector

```
Up Up Down Down Left Right Left Right Bomb Detonate
```

- 3s-Timeout zwischen Schritten, 1× pro Session
- Trigger: 1500 Coins + Gold-Konfetti + Floating-Text + Vibration + Victory-Stinger
- `InputManager.TickKonamiDetector(deltaTime)` im Engine-Update-Loop
- Subscription beim `Dispose()` abgemeldet

---

## Gamepad (Android Controller-Support)

Gamepad-Events kommen in `MainActivity.DispatchKeyEvent` und `DispatchGenericMotionEvent`
an. Avalonia leitet Face-Buttons **nicht** weiter — daher direkte Abfangung in MainActivity.
`gameVm.OnGamepadButtonDown/Up()` und `gameVm.SetAnalogStick(x, y)` sind die VM-Schnittstelle.
