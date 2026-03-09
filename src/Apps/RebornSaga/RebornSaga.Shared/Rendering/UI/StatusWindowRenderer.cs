namespace RebornSaga.Rendering.UI;

using RebornSaga.Models;
using SkiaSharp;
using System;

/// <summary>
/// Rendert das Status-Fenster im Solo-Leveling-Stil:
/// Dunkles Panel mit blau leuchtenden Rändern, Glitch-Effekt, CountUp-Animation.
/// </summary>
public static class StatusWindowRenderer
{
    // Gecachte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f };
    private static readonly SKPaint _barBgPaint = new() { IsAntialias = true, Color = new SKColor(0x15, 0x18, 0x22, 200) };
    private static readonly SKPaint _barFillPaint = new() { IsAntialias = true };
    private static readonly SKFont _nameFont = new() { LinearMetrics = true };
    private static readonly SKFont _labelFont = new() { LinearMetrics = true };
    private static readonly SKFont _valueFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };

    // Gecachter Blur für Glow-Effekt
    private static readonly SKMaskFilter _glowBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    // Gecachte Strings (vermeidet per-Frame String-Interpolation)
    private static string _cachedClassText = "";
    private static string _cachedGoldText = "";
    private static string _cachedKarmaText = "";
    private static string _cachedFreePointsText = "";
    private static int _lastLevel, _lastGold, _lastKarma, _lastFreePoints;
    private static string _lastClassName = "";

    // DrawBar gecachte Strings (3 Bars: EXP, HP, MP)
    private static readonly string[] _cachedBarTexts = new string[3];
    private static readonly int[] _lastBarDisplayValues = new int[3];
    private static readonly int[] _lastBarMaxValues = new int[3];

    // DrawStat gecachte Strings (5 Stats: ATK, DEF, INT, SPD, LUK)
    private static readonly string[] _cachedStatLabels = { "ATK:", "DEF:", "INT:", "SPD:", "LUK:" };
    private static readonly string[] _cachedStatValues = new string[5];
    private static readonly int[] _lastStatDisplayValues = new int[5];

    /// <summary>
    /// Zeichnet das vollständige Status-Fenster für einen Spieler.
    /// </summary>
    /// <param name="animProgress">0-1 für Einblend-/CountUp-Animation.</param>
    public static void Render(SKCanvas canvas, SKRect bounds, Player player, float time, float animProgress = 1f)
    {
        var margin = bounds.Width * 0.05f;
        var panelRect = new SKRect(margin, bounds.Height * 0.05f,
            bounds.Right - margin, bounds.Height * 0.92f);

        // Hintergrund
        _bgPaint.Color = new SKColor(0x0D, 0x11, 0x17, 217); // 85% Alpha
        using var roundRect = new SKRoundRect(panelRect, 8f);
        canvas.DrawRoundRect(roundRect, _bgPaint);

        // Glow-Rand (blau leuchtend)
        var glowAlpha = (byte)(80 + MathF.Sin(time * 2f) * 30);
        _glowPaint.Color = UIRenderer.Primary.WithAlpha(glowAlpha);
        _glowPaint.MaskFilter = _glowBlur;
        canvas.DrawRoundRect(roundRect, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Normaler Rand
        _borderPaint.Color = UIRenderer.Primary.WithAlpha(120);
        canvas.DrawRoundRect(roundRect, _borderPaint);

        // Glitch-Effekt bei Animation < 0.3
        if (animProgress < 0.3f)
        {
            DrawGlitchOverlay(canvas, panelRect, animProgress / 0.3f, time);
            return; // Inhalt erst nach Glitch zeigen
        }

        var contentAlpha = animProgress < 0.5f
            ? (byte)((animProgress - 0.3f) / 0.2f * 255)
            : (byte)255;

        var x = panelRect.Left + 20;
        var y = panelRect.Top + 25;
        var contentWidth = panelRect.Width - 40;

        // Spieler-Name
        _nameFont.Size = contentWidth * 0.06f;
        _textPaint.Color = UIRenderer.PrimaryGlow.WithAlpha(contentAlpha);
        canvas.DrawText(player.Name, panelRect.MidX, y,
            SKTextAlign.Center, _nameFont, _textPaint);
        y += _nameFont.Size * 1.8f;

        // Klasse + Level (gecacht)
        _labelFont.Size = contentWidth * 0.04f;
        _textPaint.Color = UIRenderer.TextSecondary.WithAlpha(contentAlpha);
        var displayLevel = (int)(player.Level * Math.Min(1f, animProgress * 2));
        var className = player.Class.ToString();
        if (displayLevel != _lastLevel || className != _lastClassName)
        {
            _cachedClassText = $"{className} - Level {displayLevel}";
            _lastLevel = displayLevel;
            _lastClassName = className;
        }
        canvas.DrawText(_cachedClassText, panelRect.MidX, y,
            SKTextAlign.Center, _labelFont, _textPaint);
        y += _labelFont.Size * 2f;

        // EXP-Bar
        DrawBar(canvas, x, y, contentWidth, panelRect.Height * 0.025f,
            "EXP", player.Exp, player.ExpToNextLevel, UIRenderer.Accent, contentAlpha, animProgress, 0);
        y += panelRect.Height * 0.06f;

        // HP-Bar
        DrawBar(canvas, x, y, contentWidth, panelRect.Height * 0.03f,
            "HP", player.Hp, player.MaxHp, UIRenderer.Success, contentAlpha, animProgress, 1);
        y += panelRect.Height * 0.06f;

        // MP-Bar
        DrawBar(canvas, x, y, contentWidth, panelRect.Height * 0.03f,
            "MP", player.Mp, player.MaxMp, new SKColor(0x58, 0xA6, 0xFF), contentAlpha, animProgress, 2);
        y += panelRect.Height * 0.08f;

        // Stats-Grid (2 Spalten)
        var statFontSize = contentWidth * 0.035f;
        _valueFont.Size = statFontSize;
        _labelFont.Size = statFontSize * 0.9f;
        var colWidth = contentWidth / 2;
        var statSpacing = statFontSize * 2f;

        DrawStat(canvas, x, y, 0, player.Atk, contentAlpha, animProgress);
        DrawStat(canvas, x + colWidth, y, 1, player.Def, contentAlpha, animProgress);
        y += statSpacing;
        DrawStat(canvas, x, y, 2, player.Int, contentAlpha, animProgress);
        DrawStat(canvas, x + colWidth, y, 3, player.Spd, contentAlpha, animProgress);
        y += statSpacing;
        DrawStat(canvas, x, y, 4, player.Luk, contentAlpha, animProgress);

        // Gold-Counter (unten, gecacht)
        y += statSpacing * 1.5f;
        _labelFont.Size = contentWidth * 0.04f;
        _textPaint.Color = new SKColor(0xF3, 0x9C, 0x12).WithAlpha(contentAlpha);
        var displayGold = (int)(player.Gold * Math.Min(1f, animProgress * 1.5f));
        if (displayGold != _lastGold)
        {
            _cachedGoldText = $"Gold: {displayGold}";
            _lastGold = displayGold;
        }
        canvas.DrawText(_cachedGoldText, panelRect.MidX, y,
            SKTextAlign.Center, _labelFont, _textPaint);

        // Karma-Anzeige (gecacht)
        y += _labelFont.Size * 2f;
        var karmaColor = player.Karma >= 0 ? UIRenderer.Success : UIRenderer.Danger;
        _textPaint.Color = karmaColor.WithAlpha(contentAlpha);
        if (player.Karma != _lastKarma)
        {
            var karmaSign = player.Karma >= 0 ? "+" : "";
            _cachedKarmaText = $"Karma: {karmaSign}{player.Karma}";
            _lastKarma = player.Karma;
        }
        canvas.DrawText(_cachedKarmaText, panelRect.MidX, y,
            SKTextAlign.Center, _labelFont, _textPaint);

        // Freie Punkte (wenn vorhanden, gecacht)
        if (player.FreeStatPoints > 0)
        {
            y += _labelFont.Size * 2f;
            _textPaint.Color = UIRenderer.Accent.WithAlpha(contentAlpha);
            if (player.FreeStatPoints != _lastFreePoints)
            {
                _cachedFreePointsText = $"+{player.FreeStatPoints} Punkte verfügbar";
                _lastFreePoints = player.FreeStatPoints;
            }
            canvas.DrawText(_cachedFreePointsText, panelRect.MidX, y,
                SKTextAlign.Center, _labelFont, _textPaint);
        }
    }

    private static void DrawBar(SKCanvas canvas, float x, float y, float width, float height,
        string label, int value, int maxValue, SKColor color, byte alpha, float anim, int barIndex)
    {
        // Label links
        _labelFont.Size = height * 1.2f;
        _textPaint.Color = UIRenderer.TextSecondary.WithAlpha(alpha);
        canvas.DrawText(label, x, y + height * 0.8f, SKTextAlign.Left, _labelFont, _textPaint);

        // Bar-Hintergrund
        var barX = x + width * 0.15f;
        var barW = width * 0.7f;
        canvas.DrawRect(barX, y, barW, height, _barBgPaint);

        // Bar-Füllung (animiert)
        var ratio = maxValue > 0 ? Math.Min(1f, (float)value / maxValue * Math.Min(1f, anim * 2f)) : 0f;
        _barFillPaint.Color = color.WithAlpha(alpha);
        canvas.DrawRect(barX, y, barW * ratio, height, _barFillPaint);

        // Wert rechts (gecacht pro Bar-Index)
        var displayValue = (int)(value * Math.Min(1f, anim * 2f));
        _valueFont.Size = height * 1.1f;
        _textPaint.Color = UIRenderer.TextPrimary.WithAlpha(alpha);
        if (displayValue != _lastBarDisplayValues[barIndex] || maxValue != _lastBarMaxValues[barIndex])
        {
            _cachedBarTexts[barIndex] = $"{displayValue}/{maxValue}";
            _lastBarDisplayValues[barIndex] = displayValue;
            _lastBarMaxValues[barIndex] = maxValue;
        }
        canvas.DrawText(_cachedBarTexts[barIndex], barX + barW + 8, y + height * 0.8f,
            SKTextAlign.Left, _valueFont, _textPaint);
    }

    private static void DrawStat(SKCanvas canvas, float x, float y,
        int statIndex, int value, byte alpha, float anim)
    {
        // Label aus gecachtem Array (kein String-Concat pro Frame)
        _textPaint.Color = UIRenderer.TextMuted.WithAlpha(alpha);
        canvas.DrawText(_cachedStatLabels[statIndex], x, y, SKTextAlign.Left, _labelFont, _textPaint);

        // Wert nur bei Änderung neu erzeugen
        var displayValue = (int)(value * Math.Min(1f, anim * 2f));
        _textPaint.Color = UIRenderer.TextPrimary.WithAlpha(alpha);
        if (displayValue != _lastStatDisplayValues[statIndex])
        {
            _cachedStatValues[statIndex] = displayValue.ToString();
            _lastStatDisplayValues[statIndex] = displayValue;
        }
        canvas.DrawText(_cachedStatValues[statIndex], x + _labelFont.Size * 4, y,
            SKTextAlign.Left, _valueFont, _textPaint);
    }

    /// <summary>
    /// Zeichnet den Glitch-Effekt beim Einblenden (verschobene horizontale Streifen).
    /// </summary>
    private static void DrawGlitchOverlay(SKCanvas canvas, SKRect rect, float progress, float time)
    {
        var stripeH = rect.Height / 12;
        _bgPaint.Color = UIRenderer.Primary.WithAlpha((byte)(progress * 40));

        for (int i = 0; i < 12; i++)
        {
            var offset = MathF.Sin(time * 20f + i * 2.5f) * (1f - progress) * 30f;
            var y = rect.Top + i * stripeH;
            canvas.DrawRect(rect.Left + offset, y, rect.Width, stripeH * 0.8f, _bgPaint);
        }
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _glowPaint.Dispose();
        _barBgPaint.Dispose();
        _barFillPaint.Dispose();
        _nameFont.Dispose();
        _labelFont.Dispose();
        _valueFont.Dispose();
        _textPaint.Dispose();
        // _glowBlur ist static readonly — NICHT disposen
    }
}
