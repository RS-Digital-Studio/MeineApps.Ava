using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies.Pipeline.Steps;

/// <summary>
/// Task 4.12 Step 6 — Lot-Size berechnen (Risiko-Cap).
/// Buch: "Das Risiko auf 1-2% vom Konto fixieren."
/// Validiert dass <c>MaxRiskPercentPerTrade</c> im Range [0.1%, 5%] liegt (User-Entscheidung: 5% erlaubt).
/// </summary>
public sealed class Step6_LotSizing : IPipelineStep
{
    public int Order => 6;
    public string Name => "LotSizing";

    public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
    {
        var maxRisk = context.RiskSettings?.MaxRiskPercentPerTrade ?? 3m;
        if (maxRisk < 0.1m || maxRisk > 5m)
            return PipelineStepResult.Fail($"MaxRiskPercentPerTrade {maxRisk:F2}% außerhalb [0.1-5.0]%");
        return PipelineStepResult.Ok($"Risiko-Cap {maxRisk:F2}%");
    }
}
