using SkiaSharp;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert das Gilden-Krieg-Dashboard: Versus-Anzeige mit zwei Wappen,
/// Score-Balken, Phasen-Timeline (Angriff/Verteidigung/Auswertung),
/// Bonus-Missionen als Fortschrittsbalken.
/// Eingebettet: GuildLeagueBadgeRenderer für Liga-Anzeige.
/// </summary>
public sealed class GuildWarDashboardRenderer : IDisposable
{
    private bool _disposed;
    private float _time;

    private readonly GuildLeagueBadgeRenderer _leagueBadge = new();

    // Gecachte Paints
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKFont _scoreFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };
    private readonly SKFont _labelFont = new() { Edging = SKFontEdging.Antialias };
    private readonly SKFont _vsFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };

    private static readonly SKColor OwnColor = new(0x22, 0xC5, 0x5E);
    private static readonly SKColor EnemyColor = new(0xDC, 0x26, 0x26);
    private static readonly SKColor PhaseAttack = new(0xEA, 0x58, 0x0C);
    private static readonly SKColor PhaseDefense = new(0x3B, 0x82, 0xF6);
    private static readonly SKColor PhaseEval = new(0xFF, 0xD7, 0x00);

    /// <summary>
    /// Rendert das War-Dashboard.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, WarSeasonDisplayData? data, float deltaTime)
    {
        if (data == null) return;
        _time += deltaTime;

        float w = bounds.Width;
        float cx = w / 2;
        float y = bounds.Top + 8;

        // 1. Liga-Badge oben Mitte
        _leagueBadge.Render(canvas, cx, y + 20, 36, data.OwnLeague, deltaTime);
        y += 48;

        // 2. Score-Vergleich (Versus-Anzeige)
        DrawScoreComparison(canvas, cx, y, w, data);
        y += 80;

        // 3. Phasen-Timeline
        DrawPhaseTimeline(canvas, bounds.Left + 16, y, w - 32, data.CurrentPhase);
        y += 40;

        // 4. Bonus-Missionen
        if (data.BonusMissions?.Count > 0)
        {
            DrawBonusMissions(canvas, bounds.Left + 16, y, w - 32, data.BonusMissions);
        }
    }

    private void DrawScoreComparison(SKCanvas canvas, float cx, float y, float w, WarSeasonDisplayData data)
    {
        // Eigener Score (links)
        _scoreFont.Size = 22;
        _fillPaint.Color = OwnColor;
        canvas.DrawText(data.OwnScore.ToString("N0"), cx - 60, y + 30,
            SKTextAlign.Center, _scoreFont, _fillPaint);

        _labelFont.Size = 11;
        _fillPaint.Color = new SKColor(0x99, 0x99, 0x99);
        canvas.DrawText("★", cx - 60, y + 10,
            SKTextAlign.Center, _labelFont, _fillPaint);

        // VS
        _vsFont.Size = 16;
        float vsPulse = 0.8f + MathF.Sin(_time * 3f) * 0.2f;
        _fillPaint.Color = new SKColor(0x88, 0x88, 0x88, (byte)(200 * vsPulse));
        canvas.DrawText("VS", cx, y + 26, SKTextAlign.Center, _vsFont, _fillPaint);

        // Gegner Score (rechts)
        _scoreFont.Size = 22;
        _fillPaint.Color = EnemyColor;
        canvas.DrawText(data.OpponentScore.ToString("N0"), cx + 60, y + 30,
            SKTextAlign.Center, _scoreFont, _fillPaint);

        _labelFont.Size = 11;
        _fillPaint.Color = new SKColor(0x99, 0x99, 0x99);
        canvas.DrawText(data.OpponentName ?? "", cx + 60, y + 10,
            SKTextAlign.Center, _labelFont, _fillPaint);

        // Score-Balken
        float barY = y + 44;
        float barW = w * 0.75f;
        float barX = (w - barW) / 2;
        long total = data.OwnScore + data.OpponentScore;
        float ownRatio = total > 0 ? (float)data.OwnScore / total : 0.5f;

        _fillPaint.Color = new SKColor(0x20, 0x10, 0x08);
        canvas.DrawRoundRect(barX, barY, barW, 8, 4, 4, _fillPaint);

        _fillPaint.Color = OwnColor;
        float ownW = MathF.Max(barW * ownRatio, 4);
        canvas.DrawRoundRect(barX, barY, ownW, 8, 4, 4, _fillPaint);

        _fillPaint.Color = EnemyColor;
        float enemyW = MathF.Max(barW * (1f - ownRatio), 4);
        canvas.DrawRoundRect(barX + barW - enemyW, barY, enemyW, 8, 4, 4, _fillPaint);
    }

    private void DrawPhaseTimeline(SKCanvas canvas, float x, float y, float w, WarPhase currentPhase)
    {
        // 3 Phasen: Angriff | Verteidigung | Auswertung
        float segW = w / 3f;
        var phases = new[] { (WarPhase.Attack, PhaseAttack, "ATK"), (WarPhase.Defense, PhaseDefense, "DEF"), (WarPhase.Evaluation, PhaseEval, "END") };

        for (int i = 0; i < phases.Length; i++)
        {
            var (phase, color, label) = phases[i];
            float sx = x + i * segW;
            bool active = currentPhase == phase;

            // Hintergrund
            byte bgAlpha = active ? (byte)40 : (byte)15;
            _fillPaint.Color = color.WithAlpha(bgAlpha);
            canvas.DrawRoundRect(sx + 2, y, segW - 4, 24, 4, 4, _fillPaint);

            // Rahmen bei aktiver Phase
            if (active)
            {
                float pulse = 0.6f + MathF.Sin(_time * 4f) * 0.4f;
                _strokePaint.Color = color.WithAlpha((byte)(180 * pulse));
                _strokePaint.StrokeWidth = 1.5f;
                canvas.DrawRoundRect(sx + 2, y, segW - 4, 24, 4, 4, _strokePaint);
            }

            // Label
            _labelFont.Size = 11;
            _fillPaint.Color = active ? color : color.WithAlpha(120);
            canvas.DrawText(label, sx + segW / 2, y + 16, SKTextAlign.Center, _labelFont, _fillPaint);
        }
    }

    private void DrawBonusMissions(SKCanvas canvas, float x, float y, float w, IReadOnlyList<WarBonusMission> missions)
    {
        _labelFont.Size = 10;

        for (int i = 0; i < missions.Count && i < 3; i++)
        {
            var m = missions[i];
            float my = y + i * 28;

            // Name
            _fillPaint.Color = new SKColor(0xCC, 0xCC, 0xCC);
            canvas.DrawText(m.NameKey ?? "", x, my + 10, SKTextAlign.Left, _labelFont, _fillPaint);

            // Fortschrittsbalken
            float barX = x + w * 0.5f;
            float barW = w * 0.4f;

            _fillPaint.Color = new SKColor(0x20, 0x10, 0x08);
            canvas.DrawRoundRect(barX, my + 2, barW, 10, 3, 3, _fillPaint);

            float progress = m.Target > 0 ? MathF.Min((float)m.Progress / m.Target, 1f) : 0;
            var barColor = m.IsCompleted ? new SKColor(0x22, 0xC5, 0x5E) : new SKColor(0xEA, 0x58, 0x0C);
            _fillPaint.Color = barColor;
            canvas.DrawRoundRect(barX, my + 2, MathF.Max(barW * progress, 4), 10, 3, 3, _fillPaint);

            // Bonus-Punkte
            _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
            canvas.DrawText($"+{m.BonusPoints}", x + w, my + 10, SKTextAlign.Right, _labelFont, _fillPaint);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _leagueBadge.Dispose();
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _scoreFont.Dispose();
        _labelFont.Dispose();
        _vsFont.Dispose();
    }
}
