using SkiaSharp;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert einzelne Gilden-Achievement-Karten mit Tier-Farbe,
/// Fortschrittsbalken, Icon und Completion-Checkmark.
/// Abgeschlossene Achievements haben Gold-Shimmer.
/// </summary>
public sealed class GuildAchievementRenderer : IDisposable
{
    private bool _disposed;
    private float _time;

    private const float CardHeight = 64;
    private const float IconSize = 40;

    // Tier-Farben
    private static readonly SKColor BronzeColor = new(0xCD, 0x7F, 0x32);
    private static readonly SKColor SilverColor = new(0xC0, 0xC0, 0xC0);
    private static readonly SKColor GoldColor = new(0xFF, 0xD7, 0x00);

    // Gecachte Paints
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKFont _nameFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };
    private readonly SKFont _descFont = new() { Edging = SKFontEdging.Antialias };
    private readonly SKFont _rewardFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };
    private readonly SKPath _checkPath = new();

    /// <summary>
    /// Rendert eine Liste von Achievement-Karten.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, IReadOnlyList<GuildAchievementDisplay>? achievements, float deltaTime)
    {
        if (achievements == null || achievements.Count == 0) return;
        _time += deltaTime;

        float x = bounds.Left;
        float w = bounds.Width;
        float y = bounds.Top;

        for (int i = 0; i < achievements.Count; i++)
        {
            float cardY = y + i * (CardHeight + 8);
            if (cardY > bounds.Bottom) break; // Ausserhalb des sichtbaren Bereichs

            DrawAchievementCard(canvas, x, cardY, w, achievements[i]);
        }
    }

    private void DrawAchievementCard(SKCanvas canvas, float x, float y, float w, GuildAchievementDisplay achievement)
    {
        var tierColor = GetTierColor(achievement.Tier);

        // Karten-Hintergrund
        _fillPaint.Color = new SKColor(0x2A, 0x1E, 0x14, 200);
        canvas.DrawRoundRect(x, y, w, CardHeight, 8, 8, _fillPaint);

        // Tier-Akzent links
        _fillPaint.Color = tierColor.WithAlpha(40);
        canvas.DrawRoundRect(x, y, 6, CardHeight, 3, 3, _fillPaint);

        // Icon-Hintergrund
        float iconX = x + 16;
        float iconCy = y + CardHeight / 2;
        _fillPaint.Color = tierColor.WithAlpha(25);
        canvas.DrawCircle(iconX + IconSize / 2, iconCy, IconSize / 2, _fillPaint);
        _strokePaint.Color = tierColor.WithAlpha(80);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawCircle(iconX + IconSize / 2, iconCy, IconSize / 2, _strokePaint);

        // Name
        float textX = iconX + IconSize + 12;
        _nameFont.Size = 13;
        _fillPaint.Color = SKColors.White;
        canvas.DrawText(achievement.Name ?? "", textX, y + 22, SKTextAlign.Left, _nameFont, _fillPaint);

        // Beschreibung
        _descFont.Size = 10;
        _fillPaint.Color = new SKColor(0x99, 0x99, 0x99);
        canvas.DrawText(achievement.Description ?? "", textX, y + 36, SKTextAlign.Left, _descFont, _fillPaint);

        // Fortschrittsbalken (nur wenn nicht abgeschlossen)
        if (!achievement.IsCompleted)
        {
            float barX = textX;
            float barW = w - textX - 60 - x;
            float barY = y + 44;

            _fillPaint.Color = new SKColor(0x20, 0x10, 0x08);
            canvas.DrawRoundRect(barX, barY, barW, 6, 3, 3, _fillPaint);

            float progress = (float)achievement.ProgressPercent;
            _fillPaint.Color = tierColor;
            canvas.DrawRoundRect(barX, barY, MathF.Max(barW * progress, 3), 6, 3, 3, _fillPaint);

            // Fortschritts-Text
            _descFont.Size = 9;
            _fillPaint.Color = new SKColor(0x88, 0x88, 0x88);
            canvas.DrawText(achievement.ProgressDisplay, barX + barW + 4, barY + 6,
                SKTextAlign.Left, _descFont, _fillPaint);
        }
        else
        {
            // Checkmark + Shimmer
            DrawCheckmark(canvas, x + w - 30, iconCy, tierColor);

            // Gold-Shimmer bei abgeschlossenen
            float shimmer = MathF.Sin(_time * 2f) * 0.5f + 0.5f;
            _fillPaint.Color = tierColor.WithAlpha((byte)(20 * shimmer));
            canvas.DrawRoundRect(x, y, w, CardHeight, 8, 8, _fillPaint);
        }

        // Belohnung rechts
        _rewardFont.Size = 10;
        _fillPaint.Color = GoldColor;
        string reward = achievement.RewardDisplay;
        if (!string.IsNullOrEmpty(reward))
        {
            canvas.DrawText(reward, x + w - 8, y + 18, SKTextAlign.Right, _rewardFont, _fillPaint);
        }
    }

    private void DrawCheckmark(SKCanvas canvas, float cx, float cy, SKColor color)
    {
        _checkPath.Rewind();
        _checkPath.MoveTo(cx - 6, cy);
        _checkPath.LineTo(cx - 2, cy + 5);
        _checkPath.LineTo(cx + 7, cy - 5);

        _strokePaint.Color = color;
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawPath(_checkPath, _strokePaint);
    }

    private static SKColor GetTierColor(AchievementTier tier) => tier switch
    {
        AchievementTier.Bronze => BronzeColor,
        AchievementTier.Silver => SilverColor,
        AchievementTier.Gold => GoldColor,
        _ => BronzeColor
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _nameFont.Dispose();
        _descFont.Dispose();
        _rewardFont.Dispose();
        _checkPath.Dispose();
    }
}
