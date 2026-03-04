using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// In-Game FloatingText: Text der an einer Spielfeld-Position erscheint, nach oben schwebt und verblasst.
/// Verwendet für Score-Popups, Combo-Anzeige, PowerUp-Collect-Text.
/// Struct-basiert, Pool von max 20 Texten.
/// </summary>
public sealed class GameFloatingTextSystem : IDisposable
{
    private const int MAX_TEXTS = 20;
    private const float DEFAULT_DURATION = 1.2f;
    private const float RISE_SPEED = 40f; // Pixel pro Sekunde

    private readonly FloatingTextEntry[] _entries = new FloatingTextEntry[MAX_TEXTS];
    private int _activeCount;

    // Gecachte SKPaint/SKFont (einmalig, keine per-Frame Allokation)
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _outlinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
    private readonly SKFont _font = new() { Embolden = true };

    private struct FloatingTextEntry
    {
        public bool IsActive;
        public string Text;
        public float X, Y;         // Spielfeld-Koordinaten (nicht Screen)
        public float Timer;
        public float Duration;
        public SKColor Color;
        public float FontSize;
    }

    /// <summary>
    /// Text an Spielfeld-Position anzeigen
    /// </summary>
    public void Spawn(float worldX, float worldY, string text, SKColor color, float fontSize = 16f, float duration = DEFAULT_DURATION)
    {
        // Freien Slot finden oder ältesten überschreiben
        int slot = -1;
        float oldestTimer = 0;
        int oldestSlot = 0;

        for (int i = 0; i < MAX_TEXTS; i++)
        {
            if (!_entries[i].IsActive)
            {
                slot = i;
                break;
            }
            if (_entries[i].Timer > oldestTimer)
            {
                oldestTimer = _entries[i].Timer;
                oldestSlot = i;
            }
        }

        if (slot == -1) slot = oldestSlot;

        _entries[slot] = new FloatingTextEntry
        {
            IsActive = true,
            Text = text,
            X = worldX,
            Y = worldY,
            Timer = 0,
            Duration = duration,
            Color = color,
            FontSize = fontSize
        };

        if (slot >= _activeCount) _activeCount = slot + 1;
    }

    /// <summary>
    /// Texte aktualisieren (Timer + Position)
    /// </summary>
    public void Update(float deltaTime)
    {
        bool anyActive = false;
        for (int i = 0; i < _activeCount; i++)
        {
            if (!_entries[i].IsActive) continue;

            _entries[i].Timer += deltaTime;
            _entries[i].Y -= RISE_SPEED * deltaTime;

            if (_entries[i].Timer >= _entries[i].Duration)
            {
                _entries[i].IsActive = false;
            }
            else
            {
                anyActive = true;
            }
        }

        if (!anyActive) _activeCount = 0;
    }

    /// <summary>
    /// Texte im Canvas rendern (mit Viewport-Transformation)
    /// </summary>
    public void Render(SKCanvas canvas, float scale, float offsetX, float offsetY)
    {
        for (int i = 0; i < _activeCount; i++)
        {
            ref var entry = ref _entries[i];
            if (!entry.IsActive) continue;

            float progress = entry.Timer / entry.Duration;
            byte alpha = (byte)(255 * (1f - MathF.Sqrt(progress))); // Sanfteres Ausblenden (sqrt statt quadratisch)

            // Spielfeld → Screen Transformation
            float screenX = entry.X * scale + offsetX;
            float screenY = entry.Y * scale + offsetY;

            _font.Size = entry.FontSize * scale;

            // Outline (dunkler Rand für Lesbarkeit)
            _outlinePaint.Color = new SKColor(0, 0, 0, alpha);
            canvas.DrawText(entry.Text, screenX, screenY, SKTextAlign.Center, _font, _outlinePaint);

            // Text
            _textPaint.Color = entry.Color.WithAlpha(alpha);
            canvas.DrawText(entry.Text, screenX, screenY, SKTextAlign.Center, _font, _textPaint);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < MAX_TEXTS; i++)
            _entries[i].IsActive = false;
        _activeCount = 0;
    }

    public void Dispose()
    {
        _textPaint.Dispose();
        _outlinePaint.Dispose();
        _font.Dispose();
    }
}
