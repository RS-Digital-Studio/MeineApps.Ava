using SkiaSharp;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert ein Liga-Wappen (Bronze/Silver/Gold/Diamond) als Schild-Form.
/// Kann als kompaktes Badge (32dp) oder großes Wappen (80dp) gerendert werden.
/// Shimmer-Animation auf höheren Ligen. Alle Paints gecacht, 0 GC pro Frame.
/// </summary>
public sealed class GuildLeagueBadgeRenderer : IDisposable
{
    private bool _disposed;
    private float _time;

    // Farben pro Liga
    private static readonly SKColor BronzeBase = new(0xCD, 0x7F, 0x32);
    private static readonly SKColor BronzeLight = new(0xE8, 0xA0, 0x50);
    private static readonly SKColor SilverBase = new(0xC0, 0xC0, 0xC0);
    private static readonly SKColor SilverLight = new(0xE8, 0xE8, 0xE8);
    private static readonly SKColor GoldBase = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor GoldLight = new(0xFF, 0xEA, 0x70);
    private static readonly SKColor DiamondBase = new(0x00, 0xBF, 0xFF);
    private static readonly SKColor DiamondLight = new(0x80, 0xE0, 0xFF);

    // Gecachte Paints
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private readonly SKPaint _shimmerPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPath _shieldPath = new();
    private readonly SKFont _tierFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };

    /// <summary>
    /// Rendert das Liga-Badge.
    /// </summary>
    /// <param name="canvas">Ziel-Canvas.</param>
    /// <param name="cx">Zentrum X.</param>
    /// <param name="cy">Zentrum Y.</param>
    /// <param name="size">Größe (32 = kompakt, 80 = groß).</param>
    /// <param name="league">Liga-Stufe.</param>
    /// <param name="deltaTime">Delta seit letztem Frame.</param>
    public void Render(SKCanvas canvas, float cx, float cy, float size, GuildLeague league, float deltaTime)
    {
        _time += deltaTime;

        var (baseColor, lightColor) = GetLeagueColors(league);

        // Schild-Form erstellen
        float half = size / 2;
        _shieldPath.Rewind();
        _shieldPath.MoveTo(cx, cy - half);                              // Oben Mitte
        _shieldPath.LineTo(cx + half * 0.85f, cy - half * 0.6f);       // Oben Rechts
        _shieldPath.LineTo(cx + half * 0.85f, cy + half * 0.15f);      // Mitte Rechts
        _shieldPath.QuadTo(cx + half * 0.6f, cy + half * 0.7f,         // Kurve unten rechts
            cx, cy + half);                                              // Unten Spitze
        _shieldPath.QuadTo(cx - half * 0.6f, cy + half * 0.7f,         // Kurve unten links
            cx - half * 0.85f, cy + half * 0.15f);                      // Mitte Links
        _shieldPath.LineTo(cx - half * 0.85f, cy - half * 0.6f);       // Oben Links
        _shieldPath.Close();

        // Füllung mit Gradient-Effekt
        _fillPaint.Color = baseColor;
        canvas.DrawPath(_shieldPath, _fillPaint);

        // Obere Hälfte heller (Licht-Effekt)
        canvas.Save();
        canvas.ClipRect(new SKRect(cx - half, cy - half, cx + half, cy));
        _fillPaint.Color = lightColor.WithAlpha(60);
        canvas.DrawPath(_shieldPath, _fillPaint);
        canvas.Restore();

        // Rand
        _strokePaint.Color = lightColor.WithAlpha(180);
        _strokePaint.StrokeWidth = size > 50 ? 2.5f : 1.5f;
        canvas.DrawPath(_shieldPath, _strokePaint);

        // Shimmer auf Gold/Diamond
        if (league >= GuildLeague.Gold)
        {
            float shimmer = MathF.Sin(_time * 2f) * 0.5f + 0.5f;
            _shimmerPaint.Color = SKColors.White.WithAlpha((byte)(40 * shimmer));
            canvas.Save();
            canvas.ClipPath(_shieldPath);
            float shimX = cx - half + size * ((_time * 0.3f) % 2f - 0.5f);
            canvas.DrawRect(shimX, cy - half, size * 0.3f, size, _shimmerPaint);
            canvas.Restore();
        }

        // Liga-Kürzel in der Mitte
        _tierFont.Size = size * 0.3f;
        string tierText = league switch
        {
            GuildLeague.Bronze => "B",
            GuildLeague.Silver => "S",
            GuildLeague.Gold => "G",
            GuildLeague.Diamond => "D",
            _ => "?"
        };

        _fillPaint.Color = league >= GuildLeague.Gold ? new SKColor(0x40, 0x20, 0x00) : SKColors.White;
        canvas.DrawText(tierText, cx, cy + _tierFont.Size * 0.35f, SKTextAlign.Center, _tierFont, _fillPaint);
    }

    private static (SKColor baseColor, SKColor lightColor) GetLeagueColors(GuildLeague league) => league switch
    {
        GuildLeague.Bronze => (BronzeBase, BronzeLight),
        GuildLeague.Silver => (SilverBase, SilverLight),
        GuildLeague.Gold => (GoldBase, GoldLight),
        GuildLeague.Diamond => (DiamondBase, DiamondLight),
        _ => (BronzeBase, BronzeLight)
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _shimmerPaint.Dispose();
        _shieldPath.Dispose();
        _tierFont.Dispose();
    }
}
