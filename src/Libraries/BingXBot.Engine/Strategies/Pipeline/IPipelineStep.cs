using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies.Pipeline;

/// <summary>
/// Task 4.12 — Ein Step in der Masterclass-Pipeline (SK-Buch Checkliste).
/// Jeder Step prüft eine Bedingung oder erweitert das Pipeline-Ergebnis.
/// </summary>
public interface IPipelineStep
{
    /// <summary>Step-Nummer (1-9) für Logging + Reihenfolge.</summary>
    int Order { get; }

    /// <summary>Step-Name (für ActivityFeed und Abbruch-Meldungen).</summary>
    string Name { get; }

    /// <summary>
    /// Führt den Step aus. Data kann Zwischenergebnisse für Folgeschritte enthalten.
    /// </summary>
    PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data);
}
