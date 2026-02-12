using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Localization;
using SkiaSharp;

namespace BomberBlast.Input;

/// <summary>
/// Wisch-basierter Input-Handler
/// </summary>
public class SwipeGestureHandler : IInputHandler, IDisposable
{
    public string Name => "Swipe Gestures";

    // Touch-Zustand
    private float _startX, _startY;
    private float _currentX, _currentY;
    private bool _isTouching;
    private float _touchTime;

    // Swipe-Erkennung
    private const float MIN_SWIPE_DISTANCE = 30f;
    private const float TAP_MAX_TIME = 0.3f;
    private const float TAP_MAX_DISTANCE = 20f;

    // Aktueller Input-Zustand
    private Direction _currentDirection = Direction.None;
    private bool _bombPressed;
    private bool _bombConsumed;

    // Gecachte SKFont/SKPaint/SKPath (einmalig erstellt, vermeidet per-Frame Allokationen)
    private readonly SKFont _hintFont = new() { Size = 16 };
    private readonly SKPaint _hintTextPaint = new() { IsAntialias = true };
    private readonly SKPaint _linePaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round
    };
    private readonly SKPath _arrowPath = new();

    // Lokalisierter Hinweis-Text
    private readonly ILocalizationService? _localizationService;
    private string? _cachedHintText;

    public Direction MovementDirection => _currentDirection;
    public bool BombPressed => _bombPressed && !_bombConsumed;
    public bool DetonatePressed => false;
    public bool IsActive => _isTouching;

    public SwipeGestureHandler(ILocalizationService? localizationService = null)
    {
        _localizationService = localizationService;
    }

    /// <summary>Hinweis-Text aktualisieren bei Sprachwechsel</summary>
    public void UpdateLocalizedTexts()
    {
        _cachedHintText = null;
    }

    private string GetHintText()
    {
        if (_cachedHintText == null)
        {
            _cachedHintText = _localizationService?.GetString("SwipeHint")
                ?? "Swipe to move, Tap to bomb";
        }
        return _cachedHintText;
    }

    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight)
    {
        _startX = x;
        _startY = y;
        _currentX = x;
        _currentY = y;
        _isTouching = true;
        _touchTime = 0;
    }

    public void OnTouchMove(float x, float y)
    {
        if (!_isTouching)
            return;

        _currentX = x;
        _currentY = y;

        float dx = _currentX - _startX;
        float dy = _currentY - _startY;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance >= MIN_SWIPE_DISTANCE)
        {
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
    }

    public void OnTouchEnd()
    {
        if (!_isTouching)
            return;

        float dx = _currentX - _startX;
        float dy = _currentY - _startY;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        // Tap erkennen (kurz + wenig Bewegung = Bombe)
        if (_touchTime <= TAP_MAX_TIME && distance <= TAP_MAX_DISTANCE)
        {
            _bombPressed = true;
            _bombConsumed = false;
        }

        _isTouching = false;
    }

    public void Update(float deltaTime)
    {
        if (_isTouching)
            _touchTime += deltaTime;

        if (_bombConsumed)
            _bombPressed = false;
        if (_bombPressed)
            _bombConsumed = true;
    }

    public void Reset()
    {
        _isTouching = false;
        _currentDirection = Direction.None;
        _bombPressed = false;
        _bombConsumed = false;
    }

    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        // Wisch-Indikator zeichnen
        if (_isTouching)
        {
            float dx = _currentX - _startX;
            float dy = _currentY - _startY;
            float distance = MathF.Sqrt(dx * dx + dy * dy);

            if (distance > 10)
            {
                _linePaint.Color = new SKColor(255, 255, 255, 128);
                canvas.DrawLine(_startX, _startY, _currentX, _currentY, _linePaint);

                // Pfeilspitze
                float angle = MathF.Atan2(dy, dx);
                float arrowSize = 15;
                float arrowAngle = MathF.PI / 6;

                _arrowPath.Reset();
                _arrowPath.MoveTo(_currentX, _currentY);
                _arrowPath.LineTo(
                    _currentX - arrowSize * MathF.Cos(angle - arrowAngle),
                    _currentY - arrowSize * MathF.Sin(angle - arrowAngle));
                _arrowPath.MoveTo(_currentX, _currentY);
                _arrowPath.LineTo(
                    _currentX - arrowSize * MathF.Cos(angle + arrowAngle),
                    _currentY - arrowSize * MathF.Sin(angle + arrowAngle));

                canvas.DrawPath(_arrowPath, _linePaint);
            }
        }

        // Hinweis-Text (lokalisiert, gecachte Font/Paint)
        _hintTextPaint.Color = new SKColor(255, 255, 255, 100);
        canvas.DrawText(GetHintText(),
            screenWidth / 2, screenHeight - 14, SKTextAlign.Center, _hintFont, _hintTextPaint);
    }

    public void Dispose()
    {
        _hintFont.Dispose();
        _hintTextPaint.Dispose();
        _linePaint.Dispose();
        _arrowPath.Dispose();
    }
}
