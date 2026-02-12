using BomberBlast.Models.Entities;
using SkiaSharp;

namespace BomberBlast.Input;

/// <summary>
/// Klassischer fester D-Pad Input-Handler
/// </summary>
public class DPadHandler : IInputHandler, IDisposable
{
    public string Name => "Classic D-Pad";

    // D-Pad Konfiguration
    private float _dpadSize = 150f;
    private float _buttonSize = 45f;
    private float _dpadX, _dpadY;
    private float _bombButtonX, _bombButtonY;
    private float _bombButtonRadius = 50f;
    private float _detonatorButtonRadius = 40f;
    private float _detonatorButtonX, _detonatorButtonY;
    private float _opacity = 0.8f;

    // Gecachte SKFont/SKPaint (einmalig erstellt, vermeidet per-Frame Allokationen)
    private readonly SKFont _arrowFont = new() { Size = 24 };
    private readonly SKPaint _arrowTextPaint = new() { IsAntialias = true };
    private readonly SKPaint _bgPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPath _crossPath = new();
    private readonly SKPaint _buttonPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _buttonBorderPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
    private readonly SKPaint _bombBgPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _bombPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _fusePaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
    private readonly SKPath _fusePath = new();
    private readonly SKPaint _detonatorBgPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _detonatorIconPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, IsAntialias = true };

    // Gecachte Arrays für Button-Positionen (vermeidet Allokation pro Frame)
    private readonly float[] _btnX = new float[4];
    private readonly float[] _btnY = new float[4];

    // Touch-Zustand
    private Direction _currentDirection = Direction.None;
    private bool _bombPressed;
    private bool _bombConsumed;
    private bool _bombButtonTouched;
    private bool _detonatorButtonTouched;
    private bool _detonatePressed;
    private bool _detonateConsumed;

    // Welcher Button ist gedrueckt
    private Direction? _pressedButton;

    // Statische Button-Definitionen (Richtung + Symbol)
    private static readonly (Direction dir, string symbol)[] ButtonDefs =
    {
        (Direction.Up, "\u25B2"),
        (Direction.Down, "\u25BC"),
        (Direction.Left, "\u25C0"),
        (Direction.Right, "\u25B6")
    };

    public Direction MovementDirection => _currentDirection;
    public bool BombPressed => _bombPressed && !_bombConsumed;
    public bool DetonatePressed => _detonatePressed && !_detonateConsumed;
    public bool IsActive => _pressedButton.HasValue || _bombButtonTouched || _detonatorButtonTouched;

    /// <summary>Ob der Detonator-Button angezeigt wird</summary>
    public bool HasDetonator { get; set; }

    public float DPadSize
    {
        get => _dpadSize;
        set => _dpadSize = value;
    }

    public float Opacity
    {
        get => _opacity;
        set => _opacity = Math.Clamp(value, 0.1f, 1f);
    }

    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight)
    {
        UpdatePositions(screenWidth, screenHeight);

        // Detonator-Button pruefen
        if (HasDetonator)
        {
            float ddx = x - _detonatorButtonX;
            float ddy = y - _detonatorButtonY;
            if (ddx * ddx + ddy * ddy <= _detonatorButtonRadius * _detonatorButtonRadius * 1.3f)
            {
                _detonatorButtonTouched = true;
                _detonatePressed = true;
                _detonateConsumed = false;
                return;
            }
        }

        // Bomb-Button pruefen
        float dx = x - _bombButtonX;
        float dy = y - _bombButtonY;
        if (dx * dx + dy * dy <= _bombButtonRadius * _bombButtonRadius * 1.3f)
        {
            _bombButtonTouched = true;
            _bombPressed = true;
            _bombConsumed = false;
            return;
        }

        // D-Pad Buttons pruefen
        CheckDPadPress(x, y);
    }

    public void OnTouchMove(float x, float y)
    {
        if (_bombButtonTouched || _detonatorButtonTouched)
            return;

        CheckDPadPress(x, y);
    }

    public void OnTouchEnd()
    {
        _pressedButton = null;
        _currentDirection = Direction.None;
        _bombButtonTouched = false;
        _detonatorButtonTouched = false;
    }

    public void Update(float deltaTime)
    {
        // Bomb-Press konsumieren
        if (_bombConsumed)
            _bombPressed = false;
        if (_bombPressed)
            _bombConsumed = true;

        // Detonate-Press konsumieren
        if (_detonateConsumed)
            _detonatePressed = false;
        if (_detonatePressed)
            _detonateConsumed = true;

        _currentDirection = _pressedButton ?? Direction.None;
    }

    public void Reset()
    {
        _pressedButton = null;
        _currentDirection = Direction.None;
        _bombPressed = false;
        _bombConsumed = false;
        _bombButtonTouched = false;
        _detonatePressed = false;
        _detonateConsumed = false;
        _detonatorButtonTouched = false;
    }

    private void UpdatePositions(float screenWidth, float screenHeight)
    {
        _dpadX = 30 + _dpadSize / 2;
        _dpadY = screenHeight - 20 - _dpadSize / 2;

        _bombButtonX = screenWidth - _bombButtonRadius - 30;
        _bombButtonY = screenHeight - _bombButtonRadius - 20;

        _detonatorButtonX = _bombButtonX;
        _detonatorButtonY = _bombButtonY - _bombButtonRadius - _detonatorButtonRadius - 15;

        // Button-Positionen in gecachte Arrays schreiben
        float buttonDist = _dpadSize / 2 - _buttonSize / 2;
        _btnX[0] = _dpadX;                    _btnY[0] = _dpadY - buttonDist;  // Up
        _btnX[1] = _dpadX;                    _btnY[1] = _dpadY + buttonDist;  // Down
        _btnX[2] = _dpadX - buttonDist;       _btnY[2] = _dpadY;              // Left
        _btnX[3] = _dpadX + buttonDist;       _btnY[3] = _dpadY;              // Right
    }

    private void CheckDPadPress(float x, float y)
    {
        _pressedButton = null;

        for (int i = 0; i < 4; i++)
        {
            float dx = x - _btnX[i];
            float dy = y - _btnY[i];
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist <= _buttonSize)
            {
                _pressedButton = ButtonDefs[i].dir;
                break;
            }
        }
    }

    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        UpdatePositions(screenWidth, screenHeight);
        byte alpha = (byte)(_opacity * 255);

        float centerX = _dpadX;
        float centerY = _dpadY;
        float buttonDist = _dpadSize / 2 - _buttonSize / 2;

        // D-Pad Hintergrund (Kreuz-Form)
        _bgPaint.Color = new SKColor(50, 50, 50, (byte)(alpha * 0.5f));
        float cs = _buttonSize;
        float cd = buttonDist;

        _crossPath.Reset();
        _crossPath.AddRect(new SKRect(centerX - cs, centerY - cd - cs, centerX + cs, centerY + cd + cs));
        _crossPath.AddRect(new SKRect(centerX - cd - cs, centerY - cs, centerX + cd + cs, centerY + cs));
        canvas.DrawPath(_crossPath, _bgPaint);

        // Richtungs-Buttons zeichnen (gecachte Arrays aus UpdatePositions)
        for (int i = 0; i < 4; i++)
        {
            var (dir, symbol) = ButtonDefs[i];
            float bx = _btnX[i];
            float by = _btnY[i];
            bool isPressed = _pressedButton == dir;

            _buttonPaint.Color = isPressed
                ? new SKColor(150, 150, 150, alpha)
                : new SKColor(100, 100, 100, alpha);
            canvas.DrawCircle(bx, by, _buttonSize, _buttonPaint);

            _buttonBorderPaint.Color = new SKColor(200, 200, 200, alpha);
            canvas.DrawCircle(bx, by, _buttonSize, _buttonBorderPaint);

            _arrowTextPaint.Color = new SKColor(255, 255, 255, alpha);
            canvas.DrawText(symbol, bx, by + 8, SKTextAlign.Center, _arrowFont, _arrowTextPaint);
        }

        // Bomb-Button zeichnen
        BombButtonRenderer.RenderBombButton(canvas, _bombButtonX, _bombButtonY, _bombButtonRadius,
            _bombButtonTouched, alpha, _bombBgPaint, _bombPaint, _fusePaint, _fusePath);

        // Detonator-Button zeichnen (nur wenn aktiv)
        if (HasDetonator)
        {
            BombButtonRenderer.RenderDetonatorButton(canvas, _detonatorButtonX, _detonatorButtonY,
                _detonatorButtonRadius, _detonatorButtonTouched, alpha,
                _detonatorBgPaint, _detonatorIconPaint);
        }
    }

    public void Dispose()
    {
        _arrowFont.Dispose();
        _arrowTextPaint.Dispose();
        _bgPaint.Dispose();
        _crossPath.Dispose();
        _buttonPaint.Dispose();
        _buttonBorderPaint.Dispose();
        _bombBgPaint.Dispose();
        _bombPaint.Dispose();
        _fusePaint.Dispose();
        _fusePath.Dispose();
        _detonatorBgPaint.Dispose();
        _detonatorIconPaint.Dispose();
    }
}
