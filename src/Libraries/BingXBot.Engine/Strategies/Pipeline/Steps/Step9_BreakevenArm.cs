using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies.Pipeline.Steps;

/// <summary>
/// Task 4.12 Step 9 — Risiko rausnehmen (Breakeven bei A-Bruch).
/// Buch: "Sobald der Preis aus deiner Korrekturbox herausläuft und das Level A durchbricht → BE."
/// Validiert dass NavPointA (für A-Bruch-BE-Trigger) persistiert ist.
/// </summary>
public sealed class Step9_BreakevenArm : IPipelineStep
{
    public int Order => 9;
    public string Name => "BreakevenArm";

    public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
    {
        if (!data.TryGetValue("navPointA", out var aObj) || aObj is not decimal pointA || pointA <= 0m)
            return PipelineStepResult.Fail("NavPointA nicht persistiert");
        return PipelineStepResult.Ok($"A-Bruch-BE scharfgestellt @ {pointA:G6}");
    }
}
