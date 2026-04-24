using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies.Pipeline.Steps;

/// <summary>
/// Task 4.12 Step 1 — Wirtschaftskalender checken.
/// Buch: "Wirtschaftskalender checken: Stehen große News an?"
/// Blockiert Trades im ±blackoutMinutes-Fenster um High-Impact-Events.
/// Nutzt <see cref="MarketContext.NewsBlackoutCheck"/>-Delegate — wenn null, passt Step durch (graceful degradation).
/// </summary>
public sealed class Step1_NewsCheck : IPipelineStep
{
    private readonly int _blackoutMinutes;

    public Step1_NewsCheck(int blackoutMinutes)
    {
        _blackoutMinutes = blackoutMinutes;
    }

    public int Order => 1;
    public string Name => "NewsCheck";

    public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
    {
        if (context.NewsBlackoutCheck == null || _blackoutMinutes <= 0)
            return PipelineStepResult.Ok("News-Filter inaktiv (graceful degradation)");
        try
        {
            var now = context.NowUtc ?? DateTime.UtcNow;
            var blackoutEvent = context.NewsBlackoutCheck(now, _blackoutMinutes, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(blackoutEvent))
                return PipelineStepResult.Fail($"News-Blackout: {blackoutEvent}");
            return PipelineStepResult.Ok("Keine High-Impact-News im Blackout-Fenster");
        }
        catch (Exception ex)
        {
            return PipelineStepResult.Ok($"News-Check-Fehler ignoriert: {ex.Message}");
        }
    }
}
