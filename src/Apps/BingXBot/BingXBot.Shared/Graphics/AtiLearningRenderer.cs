using SkiaSharp;

namespace BingXBot.Graphics;

/// <summary>
/// Zeichnet das ATI-Lernfortschritt-Widget: Zeigt wie viel der Bot bereits gelernt hat.
/// Sektionen: Lernstatus, ML-Pipeline, Regime-Performance, Top-Features.
/// </summary>
public static class AtiLearningRenderer
{
    private static readonly SKColor BgColor = SKColor.Parse("#1E1E2E");
    private static readonly SKColor TextColor = SKColor.Parse("#E2E8F0");
    private static readonly SKColor MutedColor = SKColor.Parse("#94A3B8");
    private static readonly SKColor DimColor = SKColor.Parse("#64748B");
    private static readonly SKColor BarBgColor = SKColor.Parse("#2D2D44");
    private static readonly SKColor GreenColor = SKColor.Parse("#10B981");
    private static readonly SKColor AmberColor = SKColor.Parse("#F59E0B");
    private static readonly SKColor RedColor = SKColor.Parse("#EF4444");
    // Regime-Farben (konsistent mit RegimeDetector-Logik)
    private static readonly SKColor BullColor = SKColor.Parse("#10B981");
    private static readonly SKColor BearColor = SKColor.Parse("#EF4444");
    private static readonly SKColor RangeColor = SKColor.Parse("#F59E0B");
    private static readonly SKColor ChaoticColor = SKColor.Parse("#8B5CF6");

    private static readonly SKFont LabelFont = new(SKTypeface.Default, 11);
    private static readonly SKFont SmallFont = new(SKTypeface.Default, 9.5f);
    private static readonly SKFont ValueFont = new(SKTypeface.Default, 20);
    private static readonly SKPaint TextPaint = new() { Color = TextColor, IsAntialias = true };
    private static readonly SKPaint MutedPaint = new() { Color = MutedColor, IsAntialias = true };
    private static readonly SKPaint DimPaint = new() { Color = DimColor, IsAntialias = true };
    private static readonly SKPaint BarBgPaint = new() { Color = BarBgColor, Style = SKPaintStyle.Fill };
    private static readonly SKPaint SeparatorPaint = new() { Color = BarBgColor, StrokeWidth = 1 };

    /// <summary>
    /// Zeichnet das ATI-Lernfortschritt-Widget.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds, AtiLearningData data)
    {
        canvas.Clear(BgColor);
        if (data == null) return;

        var padding = 10f;
        var y = padding;
        var contentW = bounds.Width - padding * 2;

        // === Sektion 1: Lernstatus (große Zahl + Fortschrittsbalken) ===
        y = RenderLearningStatus(canvas, padding, y, contentW, data);

        // === Sektion 2: ML-Pipeline Status ===
        y = RenderPipelineStatus(canvas, padding, y, contentW, data);

        // === Sektion 3: Regime-Performance (4 Mini-Balken) ===
        y = RenderRegimePerformance(canvas, padding, y, contentW, data);

        // === Sektion 4: Top-Features (was prediziert Gewinne) ===
        RenderTopFeatures(canvas, padding, y, contentW, bounds.Height - y - padding, data);
    }

    /// <summary>Sektion 1: Lernstatus mit großer Trade-Zahl und Fortschrittsbalken.</summary>
    private static float RenderLearningStatus(SKCanvas canvas, float x, float y, float w, AtiLearningData data)
    {
        // Große Zahl: Gelernte Trades
        var tradeText = $"{data.TotalTrades}";
        using var valuePaint = new SKPaint { Color = data.TotalTrades >= data.MinTradesRequired ? GreenColor : AmberColor, IsAntialias = true };
        canvas.DrawText(tradeText, x, y + 20, ValueFont, valuePaint);

        // Label rechts neben der Zahl
        var tradeTextWidth = ValueFont.MeasureText(tradeText);
        canvas.DrawText($" / {data.MinTradesRequired} Trades gelernt", x + tradeTextWidth + 4, y + 20, LabelFont, MutedPaint);

        y += 30;

        // Fortschrittsbalken
        var barH = 6f;
        var progress = data.MinTradesRequired > 0
            ? Math.Min(1f, (float)data.TotalTrades / data.MinTradesRequired)
            : 0f;

        canvas.DrawRoundRect(x, y, w, barH, 3, 3, BarBgPaint);
        if (progress > 0.01f)
        {
            using var progressPaint = new SKPaint
            {
                Color = progress >= 1f ? GreenColor : AmberColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRoundRect(x, y, w * progress, barH, 3, 3, progressPaint);
        }

        y += barH + 6;

        // Zweite Zeile: Buckets + WinRate
        var bucketText = $"{data.ActiveBuckets} Buckets";
        var wrText = data.TotalTrades > 0 ? $"WR {data.LearnedWinRate:F1}%" : "WR ---";
        canvas.DrawText(bucketText, x, y + 10, SmallFont, DimPaint);
        var wrColor = data.LearnedWinRate >= 50 ? GreenColor : (data.LearnedWinRate > 0 ? RedColor : DimColor);
        using var wrPaint = new SKPaint { Color = wrColor, IsAntialias = true };
        canvas.DrawText(wrText, x + w - SmallFont.MeasureText(wrText), y + 10, SmallFont, wrPaint);

        return y + 18;
    }

    /// <summary>Sektion 2: Pipeline-Status (Bayesian, LightGBM, ONNX).</summary>
    private static float RenderPipelineStatus(SKCanvas canvas, float x, float y, float w, AtiLearningData data)
    {
        // Trennlinie
        // Gecachter SeparatorPaint (statisches Feld)
        canvas.DrawLine(x, y, x + w, y, SeparatorPaint);
        y += 8;

        // 3 Phasen nebeneinander
        var phaseW = w / 3f;

        DrawPhaseDot(canvas, x + phaseW * 0.5f, y, "Bayesian",
            data.TotalTrades > 0, data.TotalTrades >= data.MinTradesRequired);
        DrawPhaseDot(canvas, x + phaseW * 1.5f, y, "LightGBM",
            data.IsLightGbmReady, data.IsLightGbmReady);
        DrawPhaseDot(canvas, x + phaseW * 2.5f, y, "ONNX",
            data.IsOnnxLoaded, data.IsOnnxLoaded);

        y += 30;

        // Metriken (falls LightGBM trainiert)
        if (data.IsLightGbmReady && data.LightGbmAuc > 0)
        {
            var metricsText = $"AUC {data.LightGbmAuc:F2}  F1 {data.LightGbmF1:F2}";
            var metricsW = SmallFont.MeasureText(metricsText);
            canvas.DrawText(metricsText, x + (w - metricsW) / 2, y + 9, SmallFont, MutedPaint);
            y += 16;
        }

        return y;
    }

    /// <summary>Zeichnet einen farbigen Punkt mit Label für eine Pipeline-Phase.</summary>
    private static void DrawPhaseDot(SKCanvas canvas, float cx, float y, string label, bool active, bool complete)
    {
        var dotR = 5f;
        var dotColor = complete ? GreenColor : (active ? AmberColor : BarBgColor);
        using var dotPaint = new SKPaint { Color = dotColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawCircle(cx, y + 6, dotR, dotPaint);

        // Outline bei inaktiv
        if (!active)
        {
            using var outlinePaint = new SKPaint { Color = DimColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            canvas.DrawCircle(cx, y + 6, dotR, outlinePaint);
        }

        // Label unter dem Punkt
        var labelW = SmallFont.MeasureText(label);
        canvas.DrawText(label, cx - labelW / 2, y + 22, SmallFont, active ? MutedPaint : DimPaint);
    }

    /// <summary>Sektion 3: Regime-Performance (4 Mini-Balken mit WinRate pro Regime).</summary>
    private static float RenderRegimePerformance(SKCanvas canvas, float x, float y, float w, AtiLearningData data)
    {
        // Trennlinie
        // Gecachter SeparatorPaint (statisches Feld)
        canvas.DrawLine(x, y, x + w, y, SeparatorPaint);
        y += 8;

        canvas.DrawText("Regime-Performance", x, y + 11, LabelFont, TextPaint);
        y += 18;

        if (data.RegimeStats.Count == 0)
        {
            canvas.DrawText("Noch keine Daten", x, y + 10, SmallFont, DimPaint);
            return y + 18;
        }

        var barH = 14f;
        var labelW = 50f;
        var maxBarW = w - labelW - 55;

        foreach (var regime in data.RegimeStats)
        {
            var color = regime.Regime switch
            {
                "Bull" => BullColor,
                "Bear" => BearColor,
                "Range" => RangeColor,
                "Chaotic" => ChaoticColor,
                _ => MutedColor
            };

            // Regime-Label + farbiger Punkt
            using var dotPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawCircle(x + 4, y + barH / 2, 3, dotPaint);
            canvas.DrawText(regime.Regime, x + 12, y + barH / 2 + 3.5f, SmallFont, MutedPaint);

            // Balken
            canvas.DrawRoundRect(x + labelW, y, maxBarW, barH, 3, 3, BarBgPaint);
            if (regime.Trades > 0)
            {
                var barW = Math.Max(4, maxBarW * (float)regime.WinRate / 100f);
                var barColor = regime.WinRate >= 50 ? GreenColor : RedColor;
                using var barPaint = new SKPaint { Color = barColor, Style = SKPaintStyle.Fill };
                canvas.DrawRoundRect(x + labelW, y, barW, barH, 3, 3, barPaint);

                // Highlight: Aktuelles Regime
                if (regime.IsCurrentRegime)
                {
                    using var highlightPaint = new SKPaint
                    {
                        Color = color.WithAlpha(80),
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 1.5f,
                        IsAntialias = true
                    };
                    canvas.DrawRoundRect(x + labelW - 1, y - 1, maxBarW + 2, barH + 2, 4, 4, highlightPaint);
                }
            }

            // Wert rechts: "58% (23)"
            var valueText = regime.Trades > 0 ? $"{regime.WinRate:F0}% ({regime.Trades})" : "---";
            canvas.DrawText(valueText, x + labelW + maxBarW + 6, y + barH / 2 + 3.5f, SmallFont, MutedPaint);

            y += barH + 4;
        }

        return y + 2;
    }

    /// <summary>Sektion 4: Top-Features (welche Bayesian Buckets predizieren Gewinne am besten).</summary>
    private static void RenderTopFeatures(SKCanvas canvas, float x, float y, float w, float maxH, AtiLearningData data)
    {
        // Trennlinie
        // Gecachter SeparatorPaint (statisches Feld)
        canvas.DrawLine(x, y, x + w, y, SeparatorPaint);
        y += 8;

        canvas.DrawText("Top-Prädiktoren", x, y + 11, LabelFont, TextPaint);
        y += 18;

        if (data.TopBuckets.Count == 0)
        {
            canvas.DrawText("Sammelt Daten...", x, y + 10, SmallFont, DimPaint);
            return;
        }

        var barH = 12f;
        var labelW = w * 0.42f;
        var maxBarW = w - labelW - 45;
        var maxItems = Math.Min(data.TopBuckets.Count, (int)((maxH - 18) / (barH + 4)));

        for (int i = 0; i < maxItems; i++)
        {
            var bucket = data.TopBuckets[i];

            // Bucket-Name (gekürzt)
            var name = bucket.Name.Length > 18 ? bucket.Name[..18] : bucket.Name;
            canvas.DrawText(name, x, y + barH / 2 + 3.5f, SmallFont, MutedPaint);

            // Mini-Balken
            canvas.DrawRoundRect(x + labelW, y, maxBarW, barH, 2, 2, BarBgPaint);
            if (bucket.WinRate > 0)
            {
                var barW = Math.Max(3, maxBarW * (float)bucket.WinRate / 100f);
                var barColor = bucket.WinRate >= 60 ? GreenColor : (bucket.WinRate >= 45 ? AmberColor : RedColor);
                using var barPaint = new SKPaint { Color = barColor, Style = SKPaintStyle.Fill };
                canvas.DrawRoundRect(x + labelW, y, barW, barH, 2, 2, barPaint);
            }

            // Wert
            var valText = $"{bucket.WinRate:F0}%";
            canvas.DrawText(valText, x + labelW + maxBarW + 6, y + barH / 2 + 3.5f, SmallFont, MutedPaint);

            y += barH + 4;
        }
    }
}

/// <summary>
/// Datenmodell für den ATI-Lernfortschritt-Renderer.
/// Alle Werte sind Snapshots (thread-safe auf dem UI-Thread zugewiesen).
/// </summary>
public class AtiLearningData
{
    // Lernstatus
    public int TotalTrades { get; init; }
    public int MinTradesRequired { get; init; } = 20;
    public decimal LearnedWinRate { get; init; }
    public int ActiveBuckets { get; init; }

    // ML-Pipeline
    public bool IsLightGbmReady { get; init; }
    public bool IsOnnxLoaded { get; init; }
    public decimal LightGbmAuc { get; init; }
    public decimal LightGbmF1 { get; init; }
    public string ActiveModelName { get; init; } = "Bayesian";
    public DateTime? LastTrainedAt { get; init; }

    // Regime-Performance
    public List<RegimeStat> RegimeStats { get; init; } = [];

    // Top-Prädiktoren (Bayesian Buckets)
    public List<BucketStat> TopBuckets { get; init; } = [];
}

public record RegimeStat(string Regime, int Trades, decimal WinRate, bool IsCurrentRegime);
public record BucketStat(string Name, int Samples, decimal WinRate);
