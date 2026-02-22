namespace BomberBlast.Input;

/// <summary>
/// Gamepad-Buttons (plattformunabhängige Abstraktion).
/// Android: Mapping in MainActivity via Keycode.ButtonA/B/X/Y/Start/Select.
/// </summary>
public enum GamepadButton
{
    /// <summary>Bombe legen (Standard-Aktion)</summary>
    A,

    /// <summary>Detonator auslösen / Zurück</summary>
    B,

    /// <summary>Detonator auslösen (Alternative)</summary>
    X,

    /// <summary>Spezial-Bomben-Typ wechseln</summary>
    Y,

    /// <summary>Pause</summary>
    Start,

    /// <summary>Reserviert</summary>
    Select
}
