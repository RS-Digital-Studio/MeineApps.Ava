using Avalonia.Input;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Services;
using SkiaSharp;

namespace BomberBlast.Input;

/// <summary>
/// Verwaltet Input-Handler: Joystick (Android) + Keyboard (Desktop) + Gamepad (Controller).
/// Joystick hat zwei Modi: Floating (Standard) und Fixed (immer sichtbar).
/// Auto-Switch: Touch→Joystick, WASD/Space/E→Keyboard, GamepadButton/AnalogStick→Gamepad.
/// </summary>
public class InputManager : IDisposable
{
    private readonly Dictionary<InputType, IInputHandler> _handlers;
    private readonly IPreferencesService _preferences;
    private IInputHandler _activeHandler;
    private InputType _currentType;

    // Settings
    private float _joystickSize = 120f;
    private float _joystickOpacity = 0.7f;
    private bool _hapticEnabled = true;
    private bool _joystickFixed; // Fixed-Modus
    private bool _reducedEffects; // Reduzierte visuelle Effekte

    public InputType CurrentInputType
    {
        get => _currentType;
        set => SetInputType(value);
    }

    public Direction MovementDirection => _activeHandler.MovementDirection;
    public bool BombPressed => _activeHandler.BombPressed;
    public bool DetonatePressed => _activeHandler.DetonatePressed;

    // === Phase 28b — Konami-Code-Detector-Hookup ============================
    /// <summary>
    /// Easter-Egg-Detector. <see cref="TickKonamiDetector"/> wird pro Frame im Engine-Update
    /// aufgerufen und füttert den Detector mit aktuellen Inputs. <see cref="KonamiCodeDetector.CodeTriggered"/>
    /// kann von außen abonniert werden.
    /// </summary>
    public KonamiCodeDetector KonamiDetector { get; } = new();

    private Direction _lastTrackedDirection = Direction.None;
    private bool _lastTrackedBombPressed;
    private bool _lastTrackedDetonatePressed;

    /// <summary>
    /// Pro Frame im Engine-Update aufrufen. Erkennt Edge-Triggers (neue Direction, neuer Bomb/Detonate-Press)
    /// und füttert sie an den KonamiCodeDetector. Verhindert Doppel-Registrierung wenn der Spieler
    /// die Taste gedrückt hält.
    /// </summary>
    public void TickKonamiDetector(float deltaTime)
    {
        KonamiDetector.Update(deltaTime);

        // Direction-Edge-Detect: Wenn die Richtung wechselt, registrieren
        var currentDir = MovementDirection;
        if (currentDir != _lastTrackedDirection && currentDir != Direction.None)
        {
            var step = KonamiCodeDetector.FromDirection(currentDir);
            if (step.HasValue)
                KonamiDetector.RegisterInput(step.Value);
        }
        _lastTrackedDirection = currentDir;

        // Bomb-Edge-Detect (nur Press, nicht Hold)
        var bomb = BombPressed;
        if (bomb && !_lastTrackedBombPressed)
        {
            KonamiDetector.RegisterInput(KonamiCodeDetector.InputStep.Bomb);
        }
        _lastTrackedBombPressed = bomb;

        // Detonate-Edge-Detect
        var det = DetonatePressed;
        if (det && !_lastTrackedDetonatePressed)
        {
            KonamiDetector.RegisterInput(KonamiCodeDetector.InputStep.Detonate);
        }
        _lastTrackedDetonatePressed = det;
    }

    // === Phase 22 — Input-Buffer (Coyote-Time-Pattern, AAA-Audit G1) =========
    // Bomberman-Variant: Wenn der Spieler eine Bombe drückt aber NICHT auf Cell-Center steht,
    // pufferte das alte System NICHT — Tap wurde verworfen. Mit Buffer wird der Press 6 Frames
    // gespeichert (~100ms bei 60fps) und automatisch ausgelöst sobald Cell-Center erreicht.
    private const int BombInputBufferFrames = 6;
    private int _bombBufferRemaining;

    /// <summary>
    /// Phase 22 — Buffert einen Bomb-Press. Wird in <see cref="GameEngine"/> direkt nach
    /// dem Frame-Input-Read aufgerufen wenn BombPressed true ist aber die Engine den Press
    /// nicht sofort konsumieren kann (z.B. Bomb-Limit, Zellzentrum-Zwischen-Tile-Position).
    /// </summary>
    public void BufferBombPress()
    {
        _bombBufferRemaining = BombInputBufferFrames;
    }

    /// <summary>
    /// True wenn Buffer aktiv ist (auch wenn aktueller BombPressed=false). Wird pro Frame
    /// vom Update-Loop um 1 dekrementiert.
    /// </summary>
    public bool HasBufferedBombPress => _bombBufferRemaining > 0;

    /// <summary>Konsumiert den Buffer (auf 0 setzen) — sobald die Bombe erfolgreich platziert ist.</summary>
    public void ConsumeBufferedBombPress() => _bombBufferRemaining = 0;

    /// <summary>Pro-Frame-Tick. In <see cref="GameEngine.Update"/> aufrufen.</summary>
    public void TickInputBuffer()
    {
        if (_bombBufferRemaining > 0) _bombBufferRemaining--;
    }

    /// <summary>
    /// Detonator-Button auf Joystick-Handler anzeigen
    /// </summary>
    public bool HasDetonator
    {
        set
        {
            if (_handlers.TryGetValue(InputType.FloatingJoystick, out var fj))
                ((NeonJoystick)fj).HasDetonator = value;
        }
    }

    public float JoystickSize
    {
        get => _joystickSize;
        set
        {
            _joystickSize = value;
            ApplySettings();
        }
    }

    public float JoystickOpacity
    {
        get => _joystickOpacity;
        set
        {
            _joystickOpacity = value;
            ApplySettings();
        }
    }

    public bool HapticEnabled
    {
        get => _hapticEnabled;
        set => _hapticEnabled = value;
    }

    /// <summary>
    /// Joystick-Modus: true = fixiert (immer sichtbar), false = schwebend (Standard)
    /// </summary>
    public bool JoystickFixed
    {
        get => _joystickFixed;
        set
        {
            _joystickFixed = value;
            ApplySettings();
        }
    }

    /// <summary>
    /// Reduzierte Effekte: Deaktiviert ScreenShake, Partikel, Hit-Pause, Slow-Motion
    /// </summary>
    public bool ReducedEffects
    {
        get => _reducedEffects;
        set => _reducedEffects = value;
    }

    /// <summary>Event bei Richtungswechsel im Joystick (für haptisches Feedback)</summary>
    public event Action? DirectionChanged;

    public InputManager(IPreferencesService preferences)
    {
        _preferences = preferences;

        var joystick = new NeonJoystick();
        joystick.DirectionChanged += () =>
        {
            if (_hapticEnabled) DirectionChanged?.Invoke();
        };

        _handlers = new Dictionary<InputType, IInputHandler>
        {
            { InputType.FloatingJoystick, joystick },
            { InputType.Keyboard, new KeyboardHandler() },
            { InputType.Gamepad, new GamepadHandler() }
        };

        LoadSettings();

        // Auto-detect Desktop: Standard Keyboard wenn nicht Android
        if (!OperatingSystem.IsAndroid() && _currentType != InputType.Keyboard)
        {
            _currentType = InputType.Keyboard;
        }
        _activeHandler = _handlers[_currentType];
        ApplySettings();
    }

    /// <summary>
    /// Einstellungen aus Preferences laden
    /// </summary>
    private void LoadSettings()
    {
        var savedType = _preferences.Get("InputType", (int)InputType.FloatingJoystick);
        // Migration: Alte Swipe(1)/DPad(2) Werte auf Joystick(0) zurücksetzen
        _currentType = savedType switch
        {
            (int)InputType.Keyboard => InputType.Keyboard,
            (int)InputType.Gamepad => InputType.Gamepad,
            _ => InputType.FloatingJoystick
        };

        _joystickSize = (float)_preferences.Get("JoystickSize", 120.0);
        _joystickOpacity = (float)_preferences.Get("JoystickOpacity", 0.7);
        _hapticEnabled = _preferences.Get("HapticEnabled", true);

        // v2.0.37: Default fuer Neuinstallationen auf Fixed (passt besser zur 4-Wege-Bomberman-Mechanik).
        // Bestand respektieren: ContainsKey-Check verhindert Override existierender Floating-Praeferenzen.
        if (!_preferences.ContainsKey("JoystickFixed"))
        {
            _joystickFixed = true;
            _preferences.Set("JoystickFixed", true);
        }
        else
        {
            _joystickFixed = _preferences.Get("JoystickFixed", true);
        }

        _reducedEffects = _preferences.Get("ReducedEffects", false);
    }

    /// <summary>
    /// Einstellungen in Preferences speichern
    /// </summary>
    public void SaveSettings()
    {
        _preferences.Set("InputType", (int)_currentType);
        _preferences.Set("JoystickSize", (double)_joystickSize);
        _preferences.Set("JoystickOpacity", (double)_joystickOpacity);
        _preferences.Set("HapticEnabled", _hapticEnabled);
        _preferences.Set("JoystickFixed", _joystickFixed);
        _preferences.Set("ReducedEffects", _reducedEffects);
    }

    /// <summary>
    /// Aktiven Input-Typ setzen
    /// </summary>
    public void SetInputType(InputType type)
    {
        if (_currentType == type)
            return;

        _activeHandler.Reset();
        _currentType = type;
        _activeHandler = _handlers[type];
        ApplySettings();
    }

    /// <summary>
    /// Einstellungen auf Handler anwenden
    /// </summary>
    private void ApplySettings()
    {
        if (_handlers.TryGetValue(InputType.FloatingJoystick, out var joystick))
        {
            var fj = (NeonJoystick)joystick;
            fj.JoystickSize = _joystickSize;
            fj.Opacity = _joystickOpacity;
            fj.IsFixed = _joystickFixed;
        }
    }

    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight, long pointerId = 0)
    {
        // Auto-Switch zu Joystick bei Touch-Input
        if (_currentType != InputType.FloatingJoystick)
        {
            SetInputType(InputType.FloatingJoystick);
        }

        _activeHandler.OnTouchStart(x, y, screenWidth, screenHeight, pointerId);
    }

    public void OnTouchMove(float x, float y, long pointerId = 0)
    {
        _activeHandler.OnTouchMove(x, y, pointerId);
    }

    public void OnTouchEnd(long pointerId = 0)
    {
        _activeHandler.OnTouchEnd(pointerId);
    }

    public void Update(float deltaTime)
    {
        _activeHandler.Update(deltaTime);
    }

    public void Reset()
    {
        _activeHandler.Reset();
    }

    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        _activeHandler.Render(canvas, screenWidth, screenHeight);
    }

    /// <summary>
    /// Keyboard Key-Down an den Keyboard-Handler weiterleiten.
    /// Nur WASD/Space/E/T auto-switcht zu Keyboard (klar keyboard-spezifisch).
    /// Pfeiltasten werden an den aktiven Handler weitergeleitet (geteilt zwischen Keyboard + Gamepad D-Pad).
    /// </summary>
    public void OnKeyDown(Key key)
    {
        // WASD/Space/E/T → eindeutig Keyboard, auto-switch
        if (IsKeyboardSpecificKey(key))
        {
            if (_currentType != InputType.Keyboard)
                SetInputType(InputType.Keyboard);

            if (_handlers.TryGetValue(InputType.Keyboard, out var kbHandler))
                ((KeyboardHandler)kbHandler).OnKeyDown(key);
            return;
        }

        // Pfeiltasten → an aktiven Handler weiterleiten (Keyboard oder Gamepad D-Pad)
        if (key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            if (_currentType == InputType.Gamepad && _handlers.TryGetValue(InputType.Gamepad, out var gpHandler))
                ((GamepadHandler)gpHandler).OnKeyDown(key);
            else if (_handlers.TryGetValue(InputType.Keyboard, out var kbHandler2))
            {
                // Bei Pfeiltasten-Druck: Auto-Switch zu Keyboard wenn aktuell Joystick
                if (_currentType == InputType.FloatingJoystick)
                    SetInputType(InputType.Keyboard);
                ((KeyboardHandler)kbHandler2).OnKeyDown(key);
            }
        }
    }

    /// <summary>
    /// Keyboard Key-Up weiterleiten (an Keyboard und Gamepad, da Pfeiltasten geteilt sind).
    /// </summary>
    public void OnKeyUp(Key key)
    {
        // An beide Handler weiterleiten (sicher, da OnKeyUp nur entfernt)
        if (_handlers.TryGetValue(InputType.Keyboard, out var kbHandler))
            ((KeyboardHandler)kbHandler).OnKeyUp(key);

        if (key is Key.Up or Key.Down or Key.Left or Key.Right &&
            _handlers.TryGetValue(InputType.Gamepad, out var gpHandler))
            ((GamepadHandler)gpHandler).OnKeyUp(key);
    }

    /// <summary>
    /// Prüft ob ein Key eindeutig keyboard-spezifisch ist (WASD, Space, E, T).
    /// Pfeiltasten sind NICHT keyboard-spezifisch (geteilt mit Gamepad D-Pad).
    /// </summary>
    private static bool IsKeyboardSpecificKey(Key key) =>
        key is Key.W or Key.A or Key.S or Key.D or Key.Space or Key.E or Key.T;

    // ═══════════════════════════════════════════════════════════════════════
    // GAMEPAD INPUT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gamepad Face-Button gedrückt. Wechselt automatisch zu Gamepad-Input.
    /// </summary>
    public void OnGamepadButtonDown(GamepadButton button)
    {
        // Auto-Switch zu Gamepad bei Face-Button-Nutzung
        if (_currentType != InputType.Gamepad)
            SetInputType(InputType.Gamepad);

        if (_handlers.TryGetValue(InputType.Gamepad, out var handler))
            ((GamepadHandler)handler).OnButtonDown(button);
    }

    /// <summary>
    /// Gamepad Face-Button losgelassen.
    /// </summary>
    public void OnGamepadButtonUp(GamepadButton button)
    {
        if (_handlers.TryGetValue(InputType.Gamepad, out var handler))
            ((GamepadHandler)handler).OnButtonUp(button);
    }

    /// <summary>
    /// Analog-Stick Werte setzen (-1.0 bis 1.0 pro Achse).
    /// Auto-Switch zu Gamepad bei signifikanter Stick-Bewegung.
    /// </summary>
    public void SetAnalogStick(float x, float y)
    {
        // Auto-Switch zu Gamepad bei Analog-Stick-Nutzung (über Deadzone)
        if (_currentType != InputType.Gamepad && (MathF.Abs(x) > 0.25f || MathF.Abs(y) > 0.25f))
            SetInputType(InputType.Gamepad);

        if (_handlers.TryGetValue(InputType.Gamepad, out var handler))
            ((GamepadHandler)handler).SetAnalogStick(x, y);
    }

    private bool _disposed;

    /// <summary>
    /// Handler-Ressourcen freigeben (SKPaint/SKPath in NeonJoystick).
    /// Idempotent — InputManager wird sowohl von GameEngine als auch DI-Container freigegeben (Audit C07).
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var handler in _handlers.Values)
        {
            if (handler is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
