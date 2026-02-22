using Avalonia.Input;
using BomberBlast.Models.Entities;
using SkiaSharp;

namespace BomberBlast.Input;

/// <summary>
/// Gamepad/Controller Input-Handler.
/// Unterstützt D-Pad (Key.Up/Down/Left/Right), Analog-Stick und Face-Buttons.
/// D-Pad hat Priorität über Analog-Stick bei gleichzeitiger Nutzung.
/// </summary>
public class GamepadHandler : IInputHandler
{
    public string Name => "Gamepad";

    private const float DEADZONE = 0.25f;

    // D-Pad State (gleiche Keys wie Keyboard: Up/Down/Left/Right)
    private readonly HashSet<Key> _pressedKeys = new();

    // Analog-Stick State (-1.0 bis 1.0)
    private float _analogX;
    private float _analogY;

    // Button State (Konsumptions-Pattern wie KeyboardHandler)
    private bool _bombPressed;
    private bool _bombConsumed;
    private bool _detonatePressed;
    private bool _detonateConsumed;

    public Direction MovementDirection
    {
        get
        {
            // D-Pad hat Priorität über Analog-Stick
            if (_pressedKeys.Contains(Key.Up))
                return Direction.Up;
            if (_pressedKeys.Contains(Key.Down))
                return Direction.Down;
            if (_pressedKeys.Contains(Key.Left))
                return Direction.Left;
            if (_pressedKeys.Contains(Key.Right))
                return Direction.Right;

            // Analog-Stick (4-Wege-Quantisierung mit Deadzone)
            var absX = MathF.Abs(_analogX);
            var absY = MathF.Abs(_analogY);

            if (absX > DEADZONE || absY > DEADZONE)
            {
                // Stärkere Achse gewinnt (4-Wege statt 8-Wege)
                if (absX > absY)
                    return _analogX > 0 ? Direction.Right : Direction.Left;
                else
                    return _analogY > 0 ? Direction.Down : Direction.Up;
            }

            return Direction.None;
        }
    }

    public bool BombPressed => _bombPressed && !_bombConsumed;
    public bool DetonatePressed => _detonatePressed && !_detonateConsumed;
    public bool IsActive => _pressedKeys.Count > 0
                            || MathF.Abs(_analogX) > DEADZONE
                            || MathF.Abs(_analogY) > DEADZONE;

    // ═══════════════════════════════════════════════════════════════════════
    // D-PAD (Key-basiert, kommt via Avalonia KeyDown/KeyUp)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>D-Pad Key-Down (Key.Up/Down/Left/Right)</summary>
    public void OnKeyDown(Key key)
    {
        _pressedKeys.Add(key);
    }

    /// <summary>D-Pad Key-Up</summary>
    public void OnKeyUp(Key key)
    {
        _pressedKeys.Remove(key);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ANALOG-STICK (gesetzt via Android DispatchGenericMotionEvent)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Analog-Stick Werte setzen (-1.0 bis 1.0 pro Achse)</summary>
    public void SetAnalogStick(float x, float y)
    {
        _analogX = x;
        _analogY = y;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FACE-BUTTONS (via Android DispatchKeyEvent → GamepadButton Mapping)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Gamepad-Button gedrückt</summary>
    public void OnButtonDown(GamepadButton button)
    {
        switch (button)
        {
            case GamepadButton.A:
                _bombPressed = true;
                _bombConsumed = false;
                break;
            case GamepadButton.B:
            case GamepadButton.X:
                _detonatePressed = true;
                _detonateConsumed = false;
                break;
        }
    }

    /// <summary>Gamepad-Button losgelassen (Konsumptions-Pattern räumt automatisch auf)</summary>
    public void OnButtonUp(GamepadButton _)
    {
        // Buttons werden durch Konsumptions-Pattern in Update() automatisch zurückgesetzt
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IINPUTHANDLER INTERFACE
    // ═══════════════════════════════════════════════════════════════════════

    // Touch-Methoden sind No-Ops für Gamepad
    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight, long pointerId = 0) { }
    public void OnTouchMove(float x, float y, long pointerId = 0) { }
    public void OnTouchEnd(long pointerId = 0) { }

    public void Update(float deltaTime)
    {
        // Konsumptions-Pattern (identisch mit KeyboardHandler)
        if (_bombConsumed) _bombPressed = false;
        if (_bombPressed) _bombConsumed = true;

        if (_detonateConsumed) _detonatePressed = false;
        if (_detonatePressed) _detonateConsumed = true;
    }

    public void Reset()
    {
        _pressedKeys.Clear();
        _analogX = 0;
        _analogY = 0;
        _bombPressed = false;
        _bombConsumed = false;
        _detonatePressed = false;
        _detonateConsumed = false;
    }

    // Kein visuelles Rendering für Gamepad (anders als Joystick)
    public void Render(SKCanvas canvas, float screenWidth, float screenHeight) { }
}
