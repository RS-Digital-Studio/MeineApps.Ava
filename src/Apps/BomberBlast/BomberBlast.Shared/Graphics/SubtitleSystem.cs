using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Audio-Caption-Anzeige für gehörlose Spieler (Accessibility, AAA-Audit Section 6).
/// Wird vom GameEngine bei wichtigen Audio-Events aufgerufen (Boss-Roar, Time-Warning,
/// Death-Sound, LevelComplete). Captions erscheinen am unteren Bildrand für 2s mit Fade-In/Out.
/// Nur aktiv wenn IAccessibilityService.SubtitlesEnabled == true.
/// </summary>
public sealed class SubtitleSystem : IDisposable
{
    private const int MAX_CAPTIONS = 4;
    private const float DEFAULT_DURATION = 2.0f;
    private const float FADE_DURATION = 0.25f;

    private struct Caption
    {
        public string Text;
        public float Lifetime;       // Verbleibende Zeit
        public float TotalLifetime;  // Initiale Dauer für Fade-Berechnung
        public bool Active;
    }

    private readonly Caption[] _captions = new Caption[MAX_CAPTIONS];

    private readonly SKPaint _bgPaint = new()
    {
        Color = new SKColor(0, 0, 0, 200),
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };

    private readonly SKPaint _textPaint = new()
    {
        Color = SKColors.White,
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private readonly SKFont _font = new() { Size = 22 };

    private bool _disposed;

    /// <summary>Anzahl aktiver Captions.</summary>
    public int ActiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < MAX_CAPTIONS; i++)
                if (_captions[i].Active) count++;
            return count;
        }
    }

    /// <summary>
    /// Caption hinzufügen. Bei vollem Pool wird der älteste Eintrag überschrieben.
    /// Wird stillschweigend ignoriert wenn <paramref name="text"/> leer ist.
    /// </summary>
    public void Show(string text, float duration = DEFAULT_DURATION)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Erste freie Slot-Suche
        for (int i = 0; i < MAX_CAPTIONS; i++)
        {
            if (!_captions[i].Active)
            {
                _captions[i] = new Caption
                {
                    Text = text,
                    Lifetime = duration,
                    TotalLifetime = duration,
                    Active = true
                };
                return;
            }
        }

        // Pool voll → ältesten (kleinste verbleibende Zeit) überschreiben
        int oldest = 0;
        float minLifetime = float.MaxValue;
        for (int i = 0; i < MAX_CAPTIONS; i++)
        {
            if (_captions[i].Lifetime < minLifetime)
            {
                minLifetime = _captions[i].Lifetime;
                oldest = i;
            }
        }
        _captions[oldest] = new Caption
        {
            Text = text,
            Lifetime = duration,
            TotalLifetime = duration,
            Active = true
        };
    }

    /// <summary>Update pro Frame: Lifetime ablaufen lassen.</summary>
    public void Update(float deltaTime)
    {
        for (int i = 0; i < MAX_CAPTIONS; i++)
        {
            if (!_captions[i].Active) continue;
            _captions[i].Lifetime -= deltaTime;
            if (_captions[i].Lifetime <= 0)
                _captions[i].Active = false;
        }
    }

    /// <summary>Render: am unteren Bildrand gestapelt mit Fade-In/Out.</summary>
    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        if (ActiveCount == 0) return;

        float bottomY = screenHeight - 80;
        float lineHeight = 36;
        int rendered = 0;

        for (int i = 0; i < MAX_CAPTIONS; i++)
        {
            if (!_captions[i].Active) continue;

            // Fade-Alpha berechnen (Fade-In in den ersten 0.25s, Fade-Out in den letzten 0.25s)
            float lifetime = _captions[i].Lifetime;
            float total = _captions[i].TotalLifetime;
            float age = total - lifetime;
            float alpha;
            if (age < FADE_DURATION)
                alpha = age / FADE_DURATION;
            else if (lifetime < FADE_DURATION)
                alpha = lifetime / FADE_DURATION;
            else
                alpha = 1f;
            alpha = Math.Clamp(alpha, 0f, 1f);
            byte byteAlpha = (byte)(255 * alpha);

            // Text-Bounds messen für Hintergrund-Box
            var bounds = new SKRect();
            _font.MeasureText(_captions[i].Text, out bounds);
            float boxWidth = bounds.Width + 32;
            float boxX = (screenWidth - boxWidth) / 2;
            float boxY = bottomY - rendered * lineHeight;

            _bgPaint.Color = new SKColor(0, 0, 0, (byte)(200 * alpha));
            canvas.DrawRoundRect(boxX, boxY - 24, boxWidth, 32, 6, 6, _bgPaint);

            _textPaint.Color = new SKColor(255, 255, 255, byteAlpha);
            canvas.DrawText(_captions[i].Text, screenWidth / 2, boxY, SKTextAlign.Center, _font, _textPaint);

            rendered++;
        }
    }

    /// <summary>Alle Captions sofort entfernen (z.B. bei Level-Wechsel).</summary>
    public void Clear()
    {
        for (int i = 0; i < MAX_CAPTIONS; i++)
            _captions[i].Active = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bgPaint.Dispose();
        _textPaint.Dispose();
        _font.Dispose();
    }
}
