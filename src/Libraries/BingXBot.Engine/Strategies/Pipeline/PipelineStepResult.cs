namespace BingXBot.Engine.Strategies.Pipeline;

/// <summary>
/// Task 4.12 — Ergebnis eines einzelnen Pipeline-Steps.
/// Pass=true → nächster Step, Pass=false → Pipeline-Abbruch mit Reason.
/// </summary>
public sealed record PipelineStepResult(bool Pass, string Reason, Dictionary<string, object>? Data = null)
{
    public static PipelineStepResult Ok(string reason = "", Dictionary<string, object>? data = null)
        => new(true, reason, data);

    public static PipelineStepResult Fail(string reason)
        => new(false, reason);
}
