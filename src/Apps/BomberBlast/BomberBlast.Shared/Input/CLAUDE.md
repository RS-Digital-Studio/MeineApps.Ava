# Input — Eingabe-System

Verwaltet alle Eingabegeräte: Touch-Joystick, Tastatur und Gamepad.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `InputManager.cs` | Aktiven Handler halten, Auto-Switch zwischen Touch/Keyboard/Gamepad, Bomb-Input-Buffer, `IDisposable` |
| `NeonJoystick.cs` | Custom Touch-Joystick (SKCanvasView-basiert), Floating/Fixed-Modi |
| `KeyboardHandler.cs` | Pfeiltasten/WASD + Space (Bombe) + E (Detonate) |
| `GamepadHandler.cs` | D-Pad + Analog-Stick (4-Wege, Deadzone 0.25) + Face-Buttons |
| `IInputHandler.cs` | Interface für alle Handler |
| `InputType.cs` | Enum: `FloatingJoystick` / `Keyboard` / `Gamepad` |
| `GamepadButton.cs` | Enum: A / B / X / Y / Start / Select |
| `KonamiCodeDetector.cs` | Easter-Egg: Up Up Down Down Left Right Left Right Bomb Detonate |

---

## InputManager

### Auto-Switch-Logik

- Touch-Event → aktiviert `NeonJoystick`
- WASD/Space/E/T/Pfeiltasten-Event → aktiviert `KeyboardHandler`
- Gamepad-Button/Analog-Stick-Event → aktiviert `GamepadHandler`

**Hinweis:** `T` (ToggleSpecialBomb) löst den Auto-Switch zu Keyboard aus (via
`IsKeyboardSpecificKey`), wird aber nicht als eigener Handler-Event verarbeitet —
der KeyboardHandler liefert `T` ans `InputManager.OnKeyDown` weiter, das ViewModel
wertet es direkt aus.

`InputManager.Dispose()` ist **idempotent** (`_disposed`-Guard). Der `InputManager`
wird von der `GameEngine` und vom DI-Container freigegeben; der Guard verhindert
Doppel-Dispose. `App.DisposeServices()` disposed ihn explizit.

### Bomb-Input-Buffering (Coyote-Time-Pattern)

Wenn der Spieler Bombe drückt, aber nicht auf dem Zellzentrum steht, wird der Press
bis zu 6 Frames (~100 ms bei 60 fps) gepuffert und automatisch ausgelöst sobald das
Zentrum erreicht wird:

```csharp
inputManager.BufferBombPress();          // Press puffern
inputManager.HasBufferedBombPress        // true solange Buffer aktiv
inputManager.ConsumeBufferedBombPress(); // nach erfolgreicher Bombe löschen
inputManager.TickInputBuffer();          // pro Frame dekrementieren
```

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
  sofort nach Konsum auf false — Taps < 16 ms bleiben nicht hängen
- **Performance**: 19 statisch gepoolte `SKPaint`, 5 `SKPath` via `Rewind()`,
  3 gecachte `SKMaskFilter` (SoftGlow/MediumGlow/HardGlow — teuer zu erstellen),
  Trail als Struct-Array (kein GC)
- **Glow-Rendering**: Bomb- und Detonator-Aura jeden Frame mit reduziertem Alpha statt
  Frame-Skip — verhindert 15-Hz-Flackern bei gleichem GPU-Aufwand
- **ReducedEffects-Flag**: deaktiviert Idle-Pulsation (Accessibility + Mid-Tier-Performance)

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
- Trigger: `CodeTriggered`-Event (Belohnung wird vom abonnierenden Service festgelegt)
- `InputManager.TickKonamiDetector(deltaTime)` im Engine-Update-Loop
- Edge-Detect für Direction + Bomb + Detonate (Halten zählt nicht doppelt)
- Subscription beim `Dispose()` abgemeldet

---

## Gamepad (Android Controller-Support)

Gamepad-Events kommen in `MainActivity.DispatchKeyEvent` und `DispatchGenericMotionEvent`
an. Avalonia leitet Face-Buttons **nicht** weiter — daher direkte Abfangung in MainActivity.
`gameVm.OnGamepadButtonDown/Up()` und `gameVm.SetAnalogStick(x, y)` sind die VM-Schnittstelle.
