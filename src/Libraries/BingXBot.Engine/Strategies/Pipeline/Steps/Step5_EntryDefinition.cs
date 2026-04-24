using BingXBot.Core.Configuration;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies.Pipeline.Steps;

/// <summary>
/// Task 4.12 Step 5 — Einstieg definieren.
/// Buch: "Aggressiv ans 50/55.9%-Level oder konservativ auf eine Reaktion warten."
/// Akzeptiert Entry-Preis, Mid-Flag, Additional-Flag und DeepCorr-Level aus data.
/// </summary>
public sealed class Step5_EntryDefinition : IPipelineStep
{
    public int Order => 5;
    public string Name => "EntryDefinition";

    public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
    {
        if (!data.TryGetValue("entry", out var entryObj) || entryObj is not decimal entry || entry <= 0m)
            return PipelineStepResult.Fail("Entry-Preis nicht berechnet");

        // Conservative-Mode braucht LTF-Reversal
        var entryMode = context.RiskSettings?.EntryMode ?? EntryMode.Both;
        if (entryMode == EntryMode.Conservative)
        {
            var hasLtfReversal = data.TryGetValue("ltfReversal", out var rev) && rev != null;
            if (!hasLtfReversal)
                return PipelineStepResult.Fail("Konservativer Modus: Kein LTF-Reversal bestätigt");
        }
        return PipelineStepResult.Ok($"Entry {entry:G6}");
    }
}
