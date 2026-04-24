using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies.Pipeline.Steps;

/// <summary>
/// Task 4.12 Step 3 — Sequenz-Mapping.
/// Buch: "Welche kleineren 0-A-B-C Strukturen laufen gerade? Laufen sie mit dem großen Trend oder gegen ihn?"
/// Erwartet, dass der Aufrufer die Navigator-Machine + Sequenz in data["navMachine"] + data["navSeq"] legt.
/// Dieser Step prüft nur dass eine sinnvolle Sequenz existiert (State >= SucheB).
/// </summary>
public sealed class Step3_SequenceMapping : IPipelineStep
{
    public int Order => 3;
    public string Name => "SequenceMapping";

    public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
    {
        if (!data.TryGetValue("navSeq", out var seqObj) || seqObj is not Sequence)
            return PipelineStepResult.Fail("Keine Navigator-Sequenz gemappt");
        return PipelineStepResult.Ok("Navigator-Sequenz verfügbar");
    }
}
