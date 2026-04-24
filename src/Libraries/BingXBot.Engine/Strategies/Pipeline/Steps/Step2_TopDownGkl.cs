using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies.Pipeline.Steps;

/// <summary>
/// Task 4.12 Step 2 — Top-Down-Analyse (GKL einzeichnen).
/// Buch: "Wo befindet sich die ultimative 50-66% Box des Makrotrends? (Gemessen von Docht zu Docht!)"
/// Delegiert an <see cref="MultiTfGklDetector"/>. Liefert GklHit? in Data["gklHit"].
/// </summary>
public sealed class Step2_TopDownGkl : IPipelineStep
{
    public int Order => 2;
    public string Name => "TopDownGkl";

    public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
    {
        var currentPrice = context.CurrentTicker.LastPrice;
        var tradeIsLong = data.TryGetValue("tradeIsLong", out var v) && v is bool b && b;
        var gklHit = MultiTfGklDetector.Detect(
            currentPrice,
            context.WeeklyCandles,
            context.DailyCandles,
            requireMatchDirection: true,
            preferredLong: tradeIsLong);

        if (gklHit != null)
            data["gklHit"] = gklHit;
        return PipelineStepResult.Ok(
            gklHit != null ? $"GKL-{gklHit.Tf}-Trefferzone aktiv" : "Kein GKL-Treffer",
            new Dictionary<string, object>());
    }
}
