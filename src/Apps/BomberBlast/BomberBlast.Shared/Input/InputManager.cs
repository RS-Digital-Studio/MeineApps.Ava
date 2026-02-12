using Avalonia.Input;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using SkiaSharp;

namespace BomberBlast.Input;

/// <summary>
/// Verwaltet Input-Handler und wechselt zwischen Input-Methoden.
/// </summary>
public class InputManager
{
    private readonly Dictionary<InputType, IInputHandler> _handlers;
    private readonly IPreferencesService _preferences;
    private IInputHandler _activeHandler;
    private InputType _currentType;

    // Settings
    private float _joystickSize = 120f;
    private float _joystickOpacity = 0.7f;
    private bool _hapticEnabled = true;

    public InputType CurrentInputType
    {
        get => _currentType;
        set => SetInputType(value);
    }

    public Direction MovementDirection => _activeHandler.MovementDirection;
    public bool BombPressed => _activeHandler.BombPressed;
    public bool DetonatePressed => _activeHandler.DetonatePressed;

    /// <summary>
    /// Detonator-Button auf Touch-Handlern anzeigen
    /// </summary>
    public bool HasDetonator
    {
        set
        {
            if (_handlers.TryGetValue(InputType.FloatingJoystick, out var fj))
                ((FloatingJoystick)fj).HasDetonator = value;
            if (_handlers.TryGetValue(InputType.ClassicDPad, out var dp))
                ((DPadHandler)dp).HasDetonator = value;
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

    public InputManager(IPreferencesService preferences, ILocalizationService? localizationService = null)
    {
        _preferences = preferences;

        _handlers = new Dictionary<InputType, IInputHandler>
        {
            { InputType.FloatingJoystick, new FloatingJoystick() },
            { InputType.SwipeGesture, new SwipeGestureHandler(localizationService) },
            { InputType.ClassicDPad, new DPadHandler() },
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
    /// Load input settings from preferences
    /// </summary>
    private void LoadSettings()
    {
        _currentType = (InputType)_preferences.Get("InputType", (int)InputType.FloatingJoystick);
        _joystickSize = (float)_preferences.Get("JoystickSize", 120.0);
        _joystickOpacity = (float)_preferences.Get("JoystickOpacity", 0.7);
        _hapticEnabled = _preferences.Get("HapticEnabled", true);
    }

    /// <summary>
    /// Save input settings to preferences
    /// </summary>
    public void SaveSettings()
    {
        _preferences.Set("InputType", (int)_currentType);
        _preferences.Set("JoystickSize", (double)_joystickSize);
        _preferences.Set("JoystickOpacity", (double)_joystickOpacity);
        _preferences.Set("HapticEnabled", _hapticEnabled);
    }

    /// <summary>
    /// Set the active input type
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
    /// Apply settings to handlers
    /// </summary>
    private void ApplySettings()
    {
        if (_handlers.TryGetValue(InputType.FloatingJoystick, out var joystick))
        {
            var fj = (FloatingJoystick)joystick;
            fj.JoystickSize = _joystickSize;
            fj.Opacity = _joystickOpacity;
        }

        if (_handlers.TryGetValue(InputType.ClassicDPad, out var dpad))
        {
            var dp = (DPadHandler)dpad;
            dp.DPadSize = _joystickSize * 1.25f;
            dp.Opacity = _joystickOpacity;
        }
    }

    /// <summary>
    /// Handle touch start
    /// </summary>
    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight)
    {
        _activeHandler.OnTouchStart(x, y, screenWidth, screenHeight);

        // Haptic feedback is platform-specific; skip in shared library
        // The View layer can hook into this if needed
    }

    /// <summary>
    /// Handle touch move
    /// </summary>
    public void OnTouchMove(float x, float y)
    {
        _activeHandler.OnTouchMove(x, y);
    }

    /// <summary>
    /// Handle touch end
    /// </summary>
    public void OnTouchEnd()
    {
        _activeHandler.OnTouchEnd();
    }

    /// <summary>
    /// Update input state
    /// </summary>
    public void Update(float deltaTime)
    {
        _activeHandler.Update(deltaTime);
    }

    /// <summary>
    /// Reset input state
    /// </summary>
    public void Reset()
    {
        _activeHandler.Reset();
    }

    /// <summary>
    /// Render input UI
    /// </summary>
    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        _activeHandler.Render(canvas, screenWidth, screenHeight);
    }

    /// <summary>
    /// Forward keyboard key-down event to the keyboard handler.
    /// Automatically switches to keyboard input on first key press.
    /// </summary>
    public void OnKeyDown(Key key)
    {
        // Auto-switch to keyboard when a game key is pressed
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
    /// Forward keyboard key-up event to the keyboard handler.
    /// </summary>
    public void OnKeyUp(Key key)
    {
        if (_handlers.TryGetValue(InputType.Keyboard, out var handler))
        {
            ((KeyboardHandler)handler).OnKeyUp(key);
        }
    }

    /// <summary>
    /// Get all available input types
    /// </summary>
    public static IEnumerable<(InputType type, string name)> GetAvailableInputTypes()
    {
        yield return (InputType.FloatingJoystick, "Floating Joystick");
        yield return (InputType.SwipeGesture, "Swipe Gestures");
        yield return (InputType.ClassicDPad, "Classic D-Pad");
        yield return (InputType.Keyboard, "Keyboard (WASD/Arrows)");
    }
}
