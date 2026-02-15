using Avalonia.Input;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Services;
using SkiaSharp;

namespace BomberBlast.Input;

/// <summary>
/// Verwaltet Input-Handler: Joystick (Android) + Keyboard (Desktop).
/// Joystick hat zwei Modi: Floating (Standard) und Fixed (immer sichtbar).
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

    public InputType CurrentInputType
    {
        get => _currentType;
        set => SetInputType(value);
    }

    public Direction MovementDirection => _activeHandler.MovementDirection;
    public bool BombPressed => _activeHandler.BombPressed;
    public bool DetonatePressed => _activeHandler.DetonatePressed;

    /// <summary>
    /// Detonator-Button auf Joystick-Handler anzeigen
    /// </summary>
    public bool HasDetonator
    {
        set
        {
            if (_handlers.TryGetValue(InputType.FloatingJoystick, out var fj))
                ((FloatingJoystick)fj).HasDetonator = value;
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

    public InputManager(IPreferencesService preferences)
    {
        _preferences = preferences;

        _handlers = new Dictionary<InputType, IInputHandler>
        {
            { InputType.FloatingJoystick, new FloatingJoystick() },
            { InputType.Keyboard, new KeyboardHandler() }
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
        _currentType = savedType == (int)InputType.Keyboard ? InputType.Keyboard : InputType.FloatingJoystick;

        _joystickSize = (float)_preferences.Get("JoystickSize", 120.0);
        _joystickOpacity = (float)_preferences.Get("JoystickOpacity", 0.7);
        _hapticEnabled = _preferences.Get("HapticEnabled", true);
        _joystickFixed = _preferences.Get("JoystickFixed", false);
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
            var fj = (FloatingJoystick)joystick;
            fj.JoystickSize = _joystickSize;
            fj.Opacity = _joystickOpacity;
            fj.IsFixed = _joystickFixed;
        }
    }

    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight)
    {
        _activeHandler.OnTouchStart(x, y, screenWidth, screenHeight);
    }

    public void OnTouchMove(float x, float y)
    {
        _activeHandler.OnTouchMove(x, y);
    }

    public void OnTouchEnd()
    {
        _activeHandler.OnTouchEnd();
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
    /// Wechselt automatisch zu Keyboard-Input beim ersten Tastendruck.
    /// </summary>
    public void OnKeyDown(Key key)
    {
        // Auto-switch zu Keyboard beim ersten Tastendruck
        if (_currentType != InputType.Keyboard)
        {
            SetInputType(InputType.Keyboard);
        }

        if (_handlers.TryGetValue(InputType.Keyboard, out var handler))
        {
            ((KeyboardHandler)handler).OnKeyDown(key);
        }
    }

    /// <summary>
    /// Keyboard Key-Up an den Keyboard-Handler weiterleiten.
    /// </summary>
    public void OnKeyUp(Key key)
    {
        if (_handlers.TryGetValue(InputType.Keyboard, out var handler))
        {
            ((KeyboardHandler)handler).OnKeyUp(key);
        }
    }

    /// <summary>
    /// Handler-Ressourcen freigeben (SKPaint/SKFont/SKPath in FloatingJoystick)
    /// </summary>
    public void Dispose()
    {
        foreach (var handler in _handlers.Values)
        {
            if (handler is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
