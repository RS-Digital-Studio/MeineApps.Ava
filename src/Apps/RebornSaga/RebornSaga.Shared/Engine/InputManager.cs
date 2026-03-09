namespace RebornSaga.Engine;

using SkiaSharp;
using System;
using System.Diagnostics;

/// <summary>
/// Erkennt Gesten aus rohen Pointer/Keyboard-Events und leitet abstrahierte InputActions
/// an den SceneManager weiter. Unterstützt Tap, DoubleTap, Hold, Swipe (4 Richtungen).
/// </summary>
public class InputManager
{
    private readonly SceneManager _sceneManager;

    // Gesten-Erkennung
    private SKPoint _pointerDownPos;
    private long _pointerDownTime;
    private long _lastTapTime;
    private bool _isPointerDown;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    // Schwellwerte
    private const float SwipeThreshold = 30f;       // Mindest-Distanz für Swipe (Pixel)
    private const long HoldThresholdMs = 500;        // Millisekunden für Hold
    private const long DoubleTapThresholdMs = 300;   // Max ms zwischen Double-Taps
    private const long TapMaxDurationMs = 300;       // Max ms für einen Tap

    public InputManager(SceneManager sceneManager)
    {
        _sceneManager = sceneManager;
    }

    /// <summary>
    /// Touch/Maus gedrückt.
    /// </summary>
    public void OnPointerDown(SKPoint position)
    {
        _isPointerDown = true;
        _pointerDownPos = position;
        _pointerDownTime = _stopwatch.ElapsedMilliseconds;
        _sceneManager.HandlePointerDown(position);
    }

    /// <summary>
    /// Touch/Maus bewegt.
    /// </summary>
    public void OnPointerMove(SKPoint position)
    {
        if (_isPointerDown)
            _sceneManager.HandlePointerMove(position);
    }

    /// <summary>
    /// Touch/Maus losgelassen. Hier wird die Geste erkannt und als InputAction weitergeleitet.
    /// </summary>
    public void OnPointerUp(SKPoint position)
    {
        _isPointerDown = false;
        var elapsed = _stopwatch.ElapsedMilliseconds - _pointerDownTime;
        var dx = position.X - _pointerDownPos.X;
        var dy = position.Y - _pointerDownPos.Y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);

        // Roher Pointer-Up immer weiterleiten
        _sceneManager.HandlePointerUp(position);

        if (distance > SwipeThreshold)
        {
            // Swipe erkennen (horizontal vs vertikal)
            if (MathF.Abs(dx) > MathF.Abs(dy))
                _sceneManager.HandleInput(dx > 0 ? InputAction.SwipeRight : InputAction.SwipeLeft, position);
            else
                _sceneManager.HandleInput(dy > 0 ? InputAction.SwipeDown : InputAction.SwipeUp, position);
        }
        else if (elapsed >= HoldThresholdMs)
        {
            // Lange gedrückt ohne Bewegung → Hold
            _sceneManager.HandleInput(InputAction.Hold, position);
        }
        else if (elapsed < TapMaxDurationMs)
        {
            // Kurzer Tap → DoubleTap oder einfacher Tap
            var now = _stopwatch.ElapsedMilliseconds;
            if (now - _lastTapTime < DoubleTapThresholdMs)
            {
                _sceneManager.HandleInput(InputAction.DoubleTap, position);
                _lastTapTime = 0; // Reset um Triple-Tap zu verhindern
            }
            else
            {
                _sceneManager.HandleInput(InputAction.Tap, position);
                _lastTapTime = now;
            }
        }
    }

    /// <summary>
    /// Keyboard-Events in InputActions umwandeln (Desktop).
    /// WASD + Pfeiltasten für Navigation, Enter/Space für Tap, Escape für Back.
    /// </summary>
    public void OnKeyDown(Avalonia.Input.Key key)
    {
        var action = key switch
        {
            Avalonia.Input.Key.Enter or Avalonia.Input.Key.Space => InputAction.Tap,
            Avalonia.Input.Key.Escape => InputAction.Back,
            Avalonia.Input.Key.Up or Avalonia.Input.Key.W => InputAction.SwipeUp,
            Avalonia.Input.Key.Down or Avalonia.Input.Key.S => InputAction.SwipeDown,
            Avalonia.Input.Key.Left or Avalonia.Input.Key.A => InputAction.SwipeLeft,
            Avalonia.Input.Key.Right or Avalonia.Input.Key.D => InputAction.SwipeRight,
            _ => (InputAction?)null
        };

        if (action.HasValue)
            _sceneManager.HandleInput(action.Value, SKPoint.Empty);
    }
}
