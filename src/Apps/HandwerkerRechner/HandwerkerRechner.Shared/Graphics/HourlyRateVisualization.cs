using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Visualisierung fuer den Stundenrechner: Gestapelte Kosten-Balken.
/// Zeigt Nettolohn, Aufschlag und MwSt als farbige Segmente.
/// </summary>
public static class HourlyRateVisualization
{
    // Einschwing-Animation (konsistent mit allen anderen Renderern)
    private static readonly AnimatedVisualizationBase _animation = new()
    {
        AnimationDurationMs = 500f,
        EasingFunction = EasingFunctions.EaseOutCubic
    };

    /// <summary>Startet die Einschwing-Animation.</summary>
    public static void StartAnimation() => _animation.StartAnimation();

    /// <summary>True wenn noch animiert wird (fuer InvalidateSurface-Loop).</summary>
    public static bool NeedsRedraw => _animation.IsAnimating;

    private static readonly SKPaint NetLaborPaint = new() { Color = new SKColor(59, 130, 246), IsAntialias = true }; // Blau
    private static readonly SKPaint OverheadPaint = new() { Color = new SKColor(245, 158, 11), IsAntialias = true }; // Amber
    private static readonly SKPaint VatPaint = new() { Color = new SKColor(239, 68, 68), IsAntialias = true }; // Rot
    private static readonly SKPaint TextPaint = new() { Color = SKColors.White, IsAntialias = true };
    private static readonly SKPaint LabelPaint = new() { Color = new SKColor(180, 180, 180), IsAntialias = true };
    private static readonly SKPaint TotalPaint = new() { Color = SKColors.White, IsAntialias = true };
    private static readonly SKPaint BgPaint = new() { Color = new SKColor(30, 30, 30, 120), IsAntialias = true };
    private static readonly SKPaint DotPaint = new() { IsAntialias = true };
    private static readonly SKPaint _layerPaint = new();

    // SKFont-Objekte (nicht-veraltete API, konsistent mit bestehenden Renderern)
    private static readonly SKFont TextFont = new() { Size = 13f };
    private static readonly SKFont LabelFont = new() { Size = 11f };
    private static readonly SKFont TotalFont = new() { Size = 18f, Embolden = true };

    public static void Render(SKCanvas canvas, SKRect bounds,
        double netLaborCost, double overheadAmount, double vatAmount, double totalGross,
        string netLabel = "Net", string overheadLabel = "Overhead",
        string vatLabel = "VAT", string totalLabel = "Gross total",
        float alpha = 1f)
    {
        if (totalGross <= 0) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        // Alpha-Fade via SaveLayer
        _layerPaint.Color = _layerPaint.Color.WithAlpha((byte)(255 * progress));
        canvas.SaveLayer(_layerPaint);

        // Hintergrund
        canvas.DrawRoundRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height, 12, 12, BgPaint);

        var padding = 16f;
        var barLeft = bounds.Left + padding;
        var barRight = bounds.Right - padding;
        var barWidth = barRight - barLeft;
        var barHeight = 36f;
        var barTop = bounds.Top + 50;

        // Proportionale Segmente
        var netFrac = (float)(netLaborCost / totalGross);
        var overFrac = (float)(overheadAmount / totalGross);

        var netWidth = barWidth * netFrac * alpha;
        var overWidth = barWidth * overFrac * alpha;
        var vatWidth = barWidth * (1f - netFrac - overFrac) * alpha;

        // Balken zeichnen
        var x = barLeft;
        canvas.DrawRoundRect(x, barTop, netWidth, barHeight, 6, 6, NetLaborPaint);
        x += netWidth;
        canvas.DrawRect(x, barTop, overWidth, barHeight, OverheadPaint);
        x += overWidth;
        canvas.DrawRoundRect(x, barTop, vatWidth, barHeight, 6, 6, VatPaint);

        // Legende
        var legendY = barTop + barHeight + 24;
        var legendSpacing = 80f;

        DrawLegendItem(canvas, barLeft, legendY, NetLaborPaint.Color, netLabel, $"{netLaborCost:F0}€");
        DrawLegendItem(canvas, barLeft + legendSpacing, legendY, OverheadPaint.Color, overheadLabel, $"{overheadAmount:F0}€");
        DrawLegendItem(canvas, barLeft + legendSpacing * 2, legendY, VatPaint.Color, vatLabel, $"{vatAmount:F0}€");

        // Gesamtsumme (rechts oben)
        var totalText = $"{totalGross:F2} €";
        canvas.DrawText(totalText, bounds.Right - padding, bounds.Top + 34,
            SKTextAlign.Right, TotalFont, TotalPaint);

        // Label (links oben)
        canvas.DrawText(totalLabel, barLeft, bounds.Top + 30,
            SKTextAlign.Left, LabelFont, LabelPaint);

        canvas.Restore();
    }

    private static void DrawLegendItem(SKCanvas canvas, float x, float y, SKColor color, string label, string value)
    {
        DotPaint.Color = color;
        canvas.DrawCircle(x + 5, y - 4, 5, DotPaint);
        canvas.DrawText(label, x + 14, y, SKTextAlign.Left, LabelFont, LabelPaint);
        canvas.DrawText(value, x + 14, y + 16, SKTextAlign.Left, TextFont, TextPaint);
    }
}
