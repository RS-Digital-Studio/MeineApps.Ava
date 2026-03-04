using SkiaSharp;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert einen kooperativen Gilden-Boss mit Silhouette, HP-Balken,
/// Atem-Animation, Damage-Feed und pulsierendem Countdown.
/// Struct-Pool für Floating-Text, alle Paints gecacht.
/// </summary>
public sealed class GuildBossRenderer : IDisposable
{
    private bool _disposed;
    private float _time;

    // Boss-Farben pro Typ
    private static readonly SKColor[] BossColors =
    [
        new(0xDC, 0x26, 0x26), // SteelGolem - Rot
        new(0x7C, 0x3A, 0xED), // TimberTitan - Violett
        new(0xEA, 0x58, 0x0C), // ForgePhoenix - Orange
        new(0x05, 0x96, 0x69), // CircuitSerpent - Grün
        new(0x00, 0xBF, 0xFF), // FrostConstruct - Blau
        new(0xFF, 0xD7, 0x00)  // MasterArchitect - Gold
    ];

    // Gecachte Paints
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _hpBarPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _hpTrailPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKFont _nameFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };
    private readonly SKFont _hpFont = new() { Edging = SKFontEdging.Antialias };
    private readonly SKFont _feedFont = new() { Edging = SKFontEdging.Antialias };
    private readonly SKPath _silhouettePath = new();

    // Damage-Feed (letzte 8 Hits als Floating-Text)
    private const int MaxFeedEntries = 8;
    private readonly DamageFeedEntry[] _feedEntries = new DamageFeedEntry[MaxFeedEntries];
    private int _feedCount;

    // HP-Trail-Effekt
    private float _trailHpPercent = 1f;
    private float _currentHpPercent = 1f;

    private struct DamageFeedEntry
    {
        public string Text;
        public float Life;
        public float Y;
    }

    /// <summary>Setzt Boss-Daten und resettet visuelle States.</summary>
    public void SetData(GuildBossDisplayData? data)
    {
        if (data == null) return;
        _currentHpPercent = (float)data.HpPercent;
    }

    /// <summary>Fügt einen Schadens-Eintrag zum Feed hinzu.</summary>
    public void AddDamageHit(string playerName, long damage)
    {
        if (_feedCount < MaxFeedEntries)
        {
            _feedEntries[_feedCount++] = new DamageFeedEntry
            {
                Text = $"{playerName}: -{damage:N0}",
                Life = 3f,
                Y = 0
            };
        }
    }

    /// <summary>
    /// Rendert den Boss mit Silhouette, HP-Balken und Damage-Feed.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, GuildBossDisplayData? data, float deltaTime)
    {
        if (data == null) return;
        _time += deltaTime;

        float w = bounds.Width;
        float h = bounds.Height;
        float cx = w / 2;

        var bossColor = GetBossColor(data.BossType);

        // 1. Boss-Silhouette mit Atem-Animation
        float breathScale = 1f + MathF.Sin(_time * 1.5f) * 0.02f;
        float silhouetteSize = MathF.Min(w * 0.5f, h * 0.4f);
        float silhouetteY = h * 0.3f;

        canvas.Save();
        canvas.Translate(cx, silhouetteY);
        canvas.Scale(breathScale, breathScale);
        DrawBossSilhouette(canvas, silhouetteSize, bossColor, data.BossType);
        canvas.Restore();

        // 2. Boss-Name
        _nameFont.Size = 18;
        _fillPaint.Color = SKColors.White;
        canvas.DrawText(data.BossName, cx, silhouetteY + silhouetteSize * 0.6f + 24,
            SKTextAlign.Center, _nameFont, _fillPaint);

        // 3. HP-Balken
        float barY = silhouetteY + silhouetteSize * 0.6f + 40;
        float barW = w * 0.7f;
        float barH = 14;
        float barX = (w - barW) / 2;
        DrawHpBar(canvas, barX, barY, barW, barH, (float)data.HpPercent, bossColor, deltaTime);

        // 4. HP-Text
        _hpFont.Size = 12;
        _fillPaint.Color = new SKColor(0xFF, 0x60, 0x60);
        canvas.DrawText($"{data.CurrentHp:N0} / {data.MaxHp:N0}", cx, barY + barH + 16,
            SKTextAlign.Center, _hpFont, _fillPaint);

        // 5. Damage-Feed
        float feedY = barY + barH + 36;
        DrawDamageFeed(canvas, cx, feedY, deltaTime);
    }

    private void DrawBossSilhouette(SKCanvas canvas, float size, SKColor color, GuildBossType bossType)
    {
        float half = size / 2;

        // Generische Silhouette (Kreis mit Zacken oben)
        _silhouettePath.Rewind();

        // Körper (Ellipse)
        _silhouettePath.AddOval(new SKRect(-half * 0.6f, -half * 0.3f, half * 0.6f, half * 0.5f));

        // Kopf
        float headR = half * 0.3f;
        _silhouettePath.AddCircle(0, -half * 0.35f, headR);

        // Hintergrund-Glow
        _fillPaint.Color = color.WithAlpha(30);
        canvas.DrawCircle(0, 0, half * 0.9f, _fillPaint);

        // Silhouette
        _fillPaint.Color = color.WithAlpha(60);
        canvas.DrawPath(_silhouettePath, _fillPaint);
        _strokePaint.Color = color.WithAlpha(150);
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawPath(_silhouettePath, _strokePaint);

        // Augen (2 leuchtende Punkte)
        float eyeY = -half * 0.38f;
        float eyeGlow = 0.7f + MathF.Sin(_time * 3f) * 0.3f;
        _fillPaint.Color = color.WithAlpha((byte)(200 * eyeGlow));
        canvas.DrawCircle(-headR * 0.35f, eyeY, 3, _fillPaint);
        canvas.DrawCircle(headR * 0.35f, eyeY, 3, _fillPaint);
    }

    private void DrawHpBar(SKCanvas canvas, float x, float y, float w, float h,
        float hpPercent, SKColor bossColor, float deltaTime)
    {
        // Trail-Effekt (HP sinkt langsam nach)
        if (_trailHpPercent > hpPercent)
            _trailHpPercent = MathF.Max(hpPercent, _trailHpPercent - deltaTime * 0.15f);
        else
            _trailHpPercent = hpPercent;

        // Hintergrund
        _fillPaint.Color = new SKColor(0x20, 0x10, 0x08);
        canvas.DrawRoundRect(x, y, w, h, 4, 4, _fillPaint);

        // Trail (verzögerter HP-Verlust)
        if (_trailHpPercent > hpPercent)
        {
            _hpTrailPaint.Color = new SKColor(0xFF, 0x40, 0x40, 80);
            canvas.DrawRoundRect(x, y, w * _trailHpPercent, h, 4, 4, _hpTrailPaint);
        }

        // Aktueller HP
        var hpColor = hpPercent > 0.5f ? new SKColor(0x22, 0xC5, 0x5E) :
            hpPercent > 0.2f ? new SKColor(0xF5, 0x9E, 0x0B) :
            new SKColor(0xDC, 0x26, 0x26);
        _hpBarPaint.Color = hpColor;
        float barFill = MathF.Max(w * hpPercent, h);
        canvas.DrawRoundRect(x, y, barFill, h, 4, 4, _hpBarPaint);

        // Segment-Linien (10er-Markierungen)
        _strokePaint.Color = new SKColor(0x00, 0x00, 0x00, 60);
        _strokePaint.StrokeWidth = 1f;
        for (int i = 1; i < 10; i++)
        {
            float sx = x + w * i / 10f;
            canvas.DrawLine(sx, y, sx, y + h, _strokePaint);
        }

        // Rand
        _strokePaint.Color = bossColor.WithAlpha(80);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(x, y, w, h, 4, 4, _strokePaint);
    }

    private void DrawDamageFeed(SKCanvas canvas, float cx, float startY, float deltaTime)
    {
        _feedFont.Size = 11;
        float y = startY;

        for (int i = _feedCount - 1; i >= 0; i--)
        {
            var entry = _feedEntries[i];
            entry.Life -= deltaTime;

            if (entry.Life <= 0)
            {
                // Swap-Remove
                _feedEntries[i] = _feedEntries[--_feedCount];
                continue;
            }

            byte alpha = (byte)(MathF.Min(entry.Life, 1f) * 180);
            _fillPaint.Color = new SKColor(0xFF, 0x80, 0x80, alpha);
            canvas.DrawText(entry.Text ?? "", cx, y, SKTextAlign.Center, _feedFont, _fillPaint);
            y += 16;

            _feedEntries[i] = entry;
        }
    }

    private static SKColor GetBossColor(GuildBossType type) =>
        (int)type >= 0 && (int)type < BossColors.Length ? BossColors[(int)type] : BossColors[0];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _hpBarPaint.Dispose();
        _hpTrailPaint.Dispose();
        _nameFont.Dispose();
        _hpFont.Dispose();
        _feedFont.Dispose();
        _silhouettePath.Dispose();
    }
}
