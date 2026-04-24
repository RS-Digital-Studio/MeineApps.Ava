using BingXBot.Core.Models;
using BingXBot.Engine.Strategies.Confluence;

namespace BingXBot.Engine.Strategies.Pipeline.Steps;

/// <summary>
/// Task 4.12 Step 4 — Confluence-Zonen markieren.
/// Buch: "Wo treffen Zielbereiche (C) kleiner Bewegungen auf Korrekturlevel (B, BC, GKL) der großen Bewegungen?"
/// Prüft das akkumulierte Scorer-Ergebnis gegen die TF-spezifische Mindest-Confluence.
/// </summary>
public sealed class Step4_ConfluenceMarking : IPipelineStep
{
    private readonly int _minConfluence;

    public Step4_ConfluenceMarking(int minConfluence)
    {
        _minConfluence = minConfluence;
    }

    public int Order => 4;
    public string Name => "ConfluenceMarking";

    public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
    {
        if (!data.TryGetValue("scorer", out var scorerObj) || scorerObj is not SkConfluenceScorer scorer)
            return PipelineStepResult.Fail("Kein Confluence-Scorer");
        if (scorer.Score < _minConfluence)
            return PipelineStepResult.Fail($"Confluence {scorer.Score}/{_minConfluence}");
        return PipelineStepResult.Ok($"Confluence {scorer.Score}/{_minConfluence} ok");
    }
}
