using BomberBlast.Models.Entities;
using SkiaSharp;

namespace BomberBlast.Input;

/// <summary>
/// Joystick-Input-Handler mit zwei Modi:
/// - Floating: Erscheint wo der Spieler tippt (Standard, wie Brawl Stars)
/// - Fixed: Immer sichtbar an fester Position unten links
///
/// Richtungsbestimmung per dominanter Achse (|dx| vs |dy|), kein Winkel-/Hysterese-System.
/// Following-Base: Wenn der Daumen über den Radius hinausgeht, folgt die Basis dem Finger.
/// </summary>
public class FloatingJoystick : IInputHandler, IDisposable
{
    public string Name => "Joystick";

    // Joystick-Zustand
    private bool _isPressed;
    private float _baseX, _baseY;      // Mittelpunkt des Joysticks
    private float _stickX, _stickY;    // Aktuelle Stick-Position (= Finger-Position, geclampt)

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

    // Multi-Touch Pointer-ID Tracking
    private long _joystickPointerId = -1;
    private long _bombPointerId = -1;

    // Konfiguration (Werte basierend auf Mobile-Touch-Best-Practices: größere Targets für Daumen)
    private float _joystickRadius = 75f;
    private const float DEAD_ZONE = 0.15f; // 15% des Radius als Ruhezone (Touch ist ungenauer als Controller)
    private const float DIRECTION_HYSTERESIS = 1.15f; // 15% Hysterese gegen Richtungsflackern bei ~45°
    private float _bombButtonRadius = 70f;
    private float _detonatorButtonRadius = 48f;
    private float _opacity = 0.7f;
    private bool _isFixed;

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

    /// <summary>Event bei Richtungswechsel (für haptisches Feedback)</summary>
    public event Action? DirectionChanged;

    /// <summary>Ob der Detonator-Button angezeigt wird</summary>
    public bool HasDetonator { get; set; }

    /// <summary>Fixed-Modus: Joystick immer sichtbar an fester Position unten links</summary>
    public bool IsFixed
    {
        get => _isFixed;
        set => _isFixed = value;
    }

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

    /// <summary>
    /// Feste Position des Joysticks berechnen (unten links)
    /// </summary>
    private void UpdateFixedPosition(float screenWidth, float screenHeight)
    {
        _baseX = 30 + _joystickRadius;
        _baseY = screenHeight - 20 - _joystickRadius;
        if (!_isPressed)
        {
            _stickX = _baseX;
            _stickY = _baseY;
        }
    }

    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight, long pointerId = 0)
    {
        UpdateBombButtonPosition(screenWidth, screenHeight);

        // Detonator-Button prüfen (über dem Bomb-Button)
        if (HasDetonator)
        {
            float ddx = x - _detonatorButtonX;
            float ddy = y - _detonatorButtonY;
            if (ddx * ddx + ddy * ddy <= _detonatorButtonRadius * _detonatorButtonRadius * 1.6f)
            {
                _detonatorButtonPressed = true;
                _detonatePressed = true;
                _detonateConsumed = false;
                _bombPointerId = pointerId;
                return;
            }
        }

        // Bomb-Button prüfen (rechte Seite)
        float dx = x - _bombButtonX;
        float dy = y - _bombButtonY;
        if (dx * dx + dy * dy <= _bombButtonRadius * _bombButtonRadius * 1.6f)
        {
            _bombButtonPressed = true;
            _bombPressed = true;
            _bombConsumed = false;
            _bombPointerId = pointerId;
            return;
        }

        if (_isFixed)
        {
            // Fixed-Modus: Nur auf Joystick-Bereich reagieren (1.8x Radius als Touch-Zone)
            UpdateFixedPosition(screenWidth, screenHeight);
            float jdx = x - _baseX;
            float jdy = y - _baseY;
            float touchZone = _joystickRadius * 1.8f;
            if (jdx * jdx + jdy * jdy <= touchZone * touchZone)
            {
                _isPressed = true;
                _joystickPointerId = pointerId;
                _stickX = x;
                _stickY = y;
                ClampAndFollow();
                UpdateDirection();
            }
        }
        else
        {
            // Floating-Modus: Linke 60% - Joystick erscheint wo getippt wird
            if (x < screenWidth * 0.6f)
            {
                _isPressed = true;
                _joystickPointerId = pointerId;
                _baseX = x;
                _baseY = y;
                _stickX = x;
                _stickY = y;
                _currentDirection = Direction.None;
            }
        }
    }

    public void OnTouchMove(float x, float y, long pointerId = 0)
    {
        // Nur auf Joystick-Finger reagieren (exakter Pointer-Match)
        if (!_isPressed || pointerId != _joystickPointerId)
            return;

        _stickX = x;
        _stickY = y;
        ClampAndFollow();
        UpdateDirection();
    }

    /// <summary>
    /// Stick auf Radius begrenzen + Following-Base:
    /// Wenn der Finger über den Radius hinausgeht, folgt die Basis dem Finger.
    /// So bleibt der Stick immer am Rand und der Spieler kann intuitiv die Richtung ändern,
    /// ohne seinen Finger zurückziehen zu müssen.
    /// </summary>
    private void ClampAndFollow()
    {
        float dx = _stickX - _baseX;
        float dy = _stickY - _baseY;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance > _joystickRadius)
        {
            // Basis folgt dem Finger (Following-Mode)
            float excess = distance - _joystickRadius;
            float nx = dx / distance; // Normalisierte Richtung
            float ny = dy / distance;
            _baseX += nx * excess;
            _baseY += ny * excess;

            // Stick bleibt am Rand des Radius
            _stickX = _baseX + nx * _joystickRadius;
            _stickY = _baseY + ny * _joystickRadius;
        }
    }

    public void OnTouchEnd(long pointerId = 0)
    {
        // Joystick-Finger losgelassen (exakter Pointer-Match oder kein Pointer zugewiesen)
        if (pointerId == _joystickPointerId || _joystickPointerId == -1)
        {
            _isPressed = false;
            _stickX = _baseX;
            _stickY = _baseY;
            _currentDirection = Direction.None;
            _joystickPointerId = -1;
        }

        // Bomb/Detonator-Finger losgelassen (exakter Pointer-Match oder kein Pointer zugewiesen)
        if (pointerId == _bombPointerId || _bombPointerId == -1)
        {
            _bombButtonPressed = false;
            _detonatorButtonPressed = false;
            _bombPointerId = -1;
        }
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
        _joystickPointerId = -1;
        _bombPointerId = -1;
    }

    /// <summary>
    /// Richtung per dominanter Achse mit Hysterese bestimmen (4-Wege).
    /// Aktuelle Richtung wird beibehalten wenn die andere Achse nicht deutlich dominant ist.
    /// Verhindert Richtungsflackern bei ~45° Daumenhaltung.
    /// </summary>
    private void UpdateDirection()
    {
        float dx = _stickX - _baseX;
        float dy = _stickY - _baseY;
        float distSq = dx * dx + dy * dy;
        float deadZonePx = _joystickRadius * DEAD_ZONE;

        // In der Dead Zone → keine Bewegung
        if (distSq < deadZonePx * deadZonePx)
        {
            if (_currentDirection != Direction.None)
            {
                _currentDirection = Direction.None;
                DirectionChanged?.Invoke();
            }
            return;
        }

        float absDx = MathF.Abs(dx);
        float absDy = MathF.Abs(dy);

        Direction newDir;
        bool isCurrentHorizontal = _currentDirection is Direction.Left or Direction.Right;
        bool isCurrentVertical = _currentDirection is Direction.Up or Direction.Down;

        if (isCurrentHorizontal)
        {
            // Aktuelle Richtung ist horizontal → nur wechseln wenn vertikal DEUTLICH dominiert
            if (absDy > absDx * DIRECTION_HYSTERESIS)
                newDir = dy > 0 ? Direction.Down : Direction.Up;
            else
                newDir = dx > 0 ? Direction.Right : Direction.Left;
        }
        else if (isCurrentVertical)
        {
            // Aktuelle Richtung ist vertikal → nur wechseln wenn horizontal DEUTLICH dominiert
            if (absDx > absDy * DIRECTION_HYSTERESIS)
                newDir = dx > 0 ? Direction.Right : Direction.Left;
            else
                newDir = dy > 0 ? Direction.Down : Direction.Up;
        }
        else
        {
            // Keine aktuelle Richtung (None) → einfache dominante Achse
            if (absDx > absDy)
                newDir = dx > 0 ? Direction.Right : Direction.Left;
            else
                newDir = dy > 0 ? Direction.Down : Direction.Up;
        }

        if (newDir != _currentDirection)
        {
            _currentDirection = newDir;
            DirectionChanged?.Invoke();
        }
    }

    private void UpdateBombButtonPosition(float screenWidth, float screenHeight)
    {
        // Bomb-Button weiter in die Spielfläche (mehr Abstand vom Rand)
        _bombButtonX = screenWidth - _bombButtonRadius - 80;
        _bombButtonY = screenHeight - _bombButtonRadius - 30;
        // Detonator-Button über dem Bomb-Button
        _detonatorButtonX = _bombButtonX;
        _detonatorButtonY = _bombButtonY - _bombButtonRadius - _detonatorButtonRadius - 15;
    }

    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        UpdateBombButtonPosition(screenWidth, screenHeight);
        byte alpha = (byte)(_opacity * 255);

        if (_isFixed)
        {
            // Fixed-Modus: Joystick immer sichtbar
            UpdateFixedPosition(screenWidth, screenHeight);

            _basePaint.Color = new SKColor(100, 100, 100, (byte)(alpha * 0.4f));
            canvas.DrawCircle(_baseX, _baseY, _joystickRadius, _basePaint);

            _borderPaint.Color = new SKColor(255, 255, 255, (byte)(alpha * 0.6f));
            canvas.DrawCircle(_baseX, _baseY, _joystickRadius, _borderPaint);

            // Stick (zeigt Auslenkung wenn gedrückt)
            byte stickAlpha = _isPressed ? alpha : (byte)(alpha * 0.7f);
            _stickPaint.Color = new SKColor(255, 255, 255, stickAlpha);
            canvas.DrawCircle(_stickX, _stickY, _joystickRadius * 0.4f, _stickPaint);
        }
        else
        {
            // Floating-Modus: Joystick nur wenn gedrückt
            if (_isPressed)
            {
                _basePaint.Color = new SKColor(100, 100, 100, (byte)(alpha * 0.5f));
                canvas.DrawCircle(_baseX, _baseY, _joystickRadius, _basePaint);

                _borderPaint.Color = new SKColor(255, 255, 255, alpha);
                canvas.DrawCircle(_baseX, _baseY, _joystickRadius, _borderPaint);

                _stickPaint.Color = new SKColor(255, 255, 255, alpha);
                canvas.DrawCircle(_stickX, _stickY, _joystickRadius * 0.4f, _stickPaint);
            }
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
