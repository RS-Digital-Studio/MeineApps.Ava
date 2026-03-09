namespace RebornSaga.Engine;

/// <summary>
/// Abstrahierte Eingabe-Aktionen für die Szenen-Engine.
/// Werden vom InputManager aus rohen Pointer/Keyboard-Events erzeugt.
/// </summary>
public enum InputAction
{
    Tap,
    Back,
    SwipeUp,
    SwipeDown,
    SwipeLeft,
    SwipeRight,
    Hold,
    DoubleTap
}
