using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies.Pipeline.Steps;

/// <summary>
/// Task 4.12 Step 8 — Ziele setzen (TP1 + TP2).
/// Buch: "TP1 (161.8%), TP2 (200.0%)."
/// Validiert dass TP1 und TP2 existieren und TP1 vor TP2 liegt.
/// </summary>
public sealed class Step8_TargetSetting : IPipelineStep
{
    public int Order => 8;
    public string Name => "TargetSetting";

    public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
    {
        if (!data.TryGetValue("tp1", out var tp1Obj) || tp1Obj is not decimal tp1 || tp1 <= 0m)
            return PipelineStepResult.Fail("Kein TP1 gesetzt");
        if (!data.TryGetValue("tp2", out var tp2Obj) || tp2Obj is not decimal tp2 || tp2 <= 0m)
            return PipelineStepResult.Fail("Kein TP2 gesetzt");
        var isLong = data.TryGetValue("tradeIsLong", out var v) && v is bool b && b;
        var tp1BeforeTp2 = isLong ? tp1 <= tp2 : tp1 >= tp2;
        if (!tp1BeforeTp2)
            return PipelineStepResult.Fail($"TP1 {tp1:G6} nicht vor TP2 {tp2:G6}");
        return PipelineStepResult.Ok($"TP1 {tp1:G6}, TP2 {tp2:G6}");
    }
}
