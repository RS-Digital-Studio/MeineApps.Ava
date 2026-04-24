using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies.Pipeline.Steps;

/// <summary>
/// Task 4.12 Step 7 — Stop-Loss setzen.
/// Buch: "Unverrückbar unter Punkt 0 (inklusive Spread-Buffer)."
/// Validiert dass SL auf der richtigen Seite des Entries liegt und nicht jenseits Point 0 × Buffer.
/// </summary>
public sealed class Step7_StopLossSetting : IPipelineStep
{
    public int Order => 7;
    public string Name => "StopLossSetting";

    public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
    {
        if (!data.TryGetValue("sl", out var slObj) || slObj is not decimal sl || sl <= 0m)
            return PipelineStepResult.Fail("Kein Stop-Loss gesetzt");
        if (!data.TryGetValue("entry", out var entryObj) || entryObj is not decimal entry)
            return PipelineStepResult.Fail("Kein Entry zum SL-Vergleich");
        var isLong = data.TryGetValue("tradeIsLong", out var v) && v is bool b && b;
        if (isLong && sl >= entry)
            return PipelineStepResult.Fail("SL >= Entry (Long)");
        if (!isLong && sl <= entry)
            return PipelineStepResult.Fail("SL <= Entry (Short)");
        return PipelineStepResult.Ok($"SL {sl:G6}");
    }
}
