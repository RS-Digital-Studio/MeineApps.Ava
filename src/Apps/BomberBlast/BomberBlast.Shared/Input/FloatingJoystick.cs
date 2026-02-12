using BomberBlast.Models.Entities;
using SkiaSharp;

namespace BomberBlast.Input;

/// <summary>
/// Floating Joystick der erscheint wo der Spieler tippt
/// </summary>
public class FloatingJoystick : IInputHandler, IDisposable
{
    public string Name => "Floating Joystick";

    // Joystick-Zustand
    private bool _isPressed;
    private float _baseX, _baseY;      // Mittelpunkt des Joysticks
    private float _stickX, _stickY;    // Aktuelle Stick-Position
    // Bomb-Button Zustand
    private bool _bombPressed;
    private bool _bombConsumed;
    private float _bombButtonX, _bombButtonY;
    private bool _bombButtonPressed;
    // Detonator-Button Zustand
    private float _detonatorButtonX, _detonatorButtonY;
    private bool _detonatorButtonPressed;
    private bool _detonatePressed;
    private bool _detonateConsumed;

    // Konfiguration
    private float _joystickRadius = 60f;
    private float _deadZone = 0.08f;
    private float _bombButtonRadius = 50f;
    private float _detonatorButtonRadius = 40f;
    private float _opacity = 0.7f;

    // Bewegung
    private Direction _currentDirection = Direction.None;

    // Gecachte SKPaint/SKPath (einmalig erstellt, vermeidet per-Frame Allokationen)
    private readonly SKPaint _basePaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _borderPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
    private readonly SKPaint _stickPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _bombBgPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _bombPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _fusePaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
    private readonly SKPath _fusePath = new();
    private readonly SKPaint _detonatorBgPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _detonatorIconPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, IsAntialias = true };

    public Direction MovementDirection => _currentDirection;
    public bool BombPressed => _bombPressed && !_bombConsumed;
    public bool DetonatePressed => _detonatePressed && !_detonateConsumed;
    public bool IsActive => _isPressed;

    /// <summary>Ob der Detonator-Button angezeigt wird</summary>
    public bool HasDetonator { get; set; }

    public float JoystickSize
    {
        get => _joystickRadius * 2;
        set => _joystickRadius = value / 2;
    }

    public float Opacity
    {
        get => _opacity;
        set => _opacity = Math.Clamp(value, 0.1f, 1f);
    }

    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight)
    {
        UpdateBombButtonPosition(screenWidth, screenHeight);

        // Detonator-Button pruefen (ueber dem Bomb-Button)
        if (HasDetonator)
        {
            float ddx = x - _detonatorButtonX;
            float ddy = y - _detonatorButtonY;
            if (ddx * ddx + ddy * ddy <= _detonatorButtonRadius * _detonatorButtonRadius * 1.5f)
            {
                _detonatorButtonPressed = true;
                _detonatePressed = true;
                _detonateConsumed = false;
                return;
            }
        }

        // Bomb-Button pruefen (rechte Seite)
        float dx = x - _bombButtonX;
        float dy = y - _bombButtonY;
        if (dx * dx + dy * dy <= _bombButtonRadius * _bombButtonRadius * 1.5f)
        {
            _bombButtonPressed = true;
            _bombPressed = true;
            _bombConsumed = false;
            return;
        }

        // Linke Haelfte - Joystick
        if (x < screenWidth * 0.6f)
        {
            _isPressed = true;
            _baseX = x;
            _baseY = y;
            _stickX = x;
            _stickY = y;
            UpdateDirection();
        }
    }

    public void OnTouchMove(float x, float y)
    {
        if (!_isPressed)
            return;

        _stickX = x;
        _stickY = y;

        // Stick auf Joystick-Radius begrenzen
        float dx = _stickX - _baseX;
        float dy = _stickY - _baseY;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance > _joystickRadius)
        {
            float ratio = _joystickRadius / distance;
            _stickX = _baseX + dx * ratio;
            _stickY = _baseY + dy * ratio;
        }

        UpdateDirection();
    }

    public void OnTouchEnd()
    {
        _isPressed = false;
        _stickX = _baseX;
        _stickY = _baseY;
        _currentDirection = Direction.None;
        _bombButtonPressed = false;
        _detonatorButtonPressed = false;
    }

    public void Update(float deltaTime)
    {
        // Bomb-Press nach Frame konsumieren
        if (_bombConsumed)
            _bombPressed = false;
        if (_bombPressed)
            _bombConsumed = true;

        // Detonate-Press nach Frame konsumieren
        if (_detonateConsumed)
            _detonatePressed = false;
        if (_detonatePressed)
            _detonateConsumed = true;
    }

    public void Reset()
    {
        _isPressed = false;
        _currentDirection = Direction.None;
        _bombPressed = false;
        _bombConsumed = false;
        _bombButtonPressed = false;
        _detonatePressed = false;
        _detonateConsumed = false;
        _detonatorButtonPressed = false;
    }

    private void UpdateDirection()
    {
        float dx = _stickX - _baseX;
        float dy = _stickY - _baseY;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance < _joystickRadius * _deadZone)
        {
            _currentDirection = Direction.None;
            return;
        }

        float angle = MathF.Atan2(dy, dx);

        if (angle >= -MathF.PI / 4 && angle < MathF.PI / 4)
            _currentDirection = Direction.Right;
        else if (angle >= MathF.PI / 4 && angle < 3 * MathF.PI / 4)
            _currentDirection = Direction.Down;
        else if (angle >= -3 * MathF.PI / 4 && angle < -MathF.PI / 4)
            _currentDirection = Direction.Up;
        else
            _currentDirection = Direction.Left;
    }

    private void UpdateBombButtonPosition(float screenWidth, float screenHeight)
    {
        _bombButtonX = screenWidth - _bombButtonRadius - 30;
        _bombButtonY = screenHeight - _bombButtonRadius - 20;
        // Detonator-Button ueber dem Bomb-Button
        _detonatorButtonX = _bombButtonX;
        _detonatorButtonY = _bombButtonY - _bombButtonRadius - _detonatorButtonRadius - 15;
    }

    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        UpdateBombButtonPosition(screenWidth, screenHeight);
        byte alpha = (byte)(_opacity * 255);

        // Joystick zeichnen wenn gedrueckt
        if (_isPressed)
        {
            _basePaint.Color = new SKColor(100, 100, 100, (byte)(alpha * 0.5f));
            canvas.DrawCircle(_baseX, _baseY, _joystickRadius, _basePaint);

            _borderPaint.Color = new SKColor(255, 255, 255, alpha);
            canvas.DrawCircle(_baseX, _baseY, _joystickRadius, _borderPaint);

            _stickPaint.Color = new SKColor(255, 255, 255, alpha);
            canvas.DrawCircle(_stickX, _stickY, _joystickRadius * 0.4f, _stickPaint);
        }

        // Bomb-Button zeichnen
        BombButtonRenderer.RenderBombButton(canvas, _bombButtonX, _bombButtonY, _bombButtonRadius,
            _bombButtonPressed, alpha, _bombBgPaint, _bombPaint, _fusePaint, _fusePath);

        // Detonator-Button zeichnen (nur wenn Detonator aktiv)
        if (HasDetonator)
        {
            BombButtonRenderer.RenderDetonatorButton(canvas, _detonatorButtonX, _detonatorButtonY,
                _detonatorButtonRadius, _detonatorButtonPressed, alpha,
                _detonatorBgPaint, _detonatorIconPaint);
        }
    }

    public void Dispose()
    {
        _basePaint.Dispose();
        _borderPaint.Dispose();
        _stickPaint.Dispose();
        _bombBgPaint.Dispose();
        _bombPaint.Dispose();
        _fusePaint.Dispose();
        _fusePath.Dispose();
        _detonatorBgPaint.Dispose();
        _detonatorIconPaint.Dispose();
    }
}
