using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies.Pipeline;

/// <summary>
/// Task 4.12 — Masterclass-Pipeline-Orchestrator (SK-Buch 9-Schritte-Checkliste).
///
/// Buch-Zitat: "Wenn ein erfahrener SK-Trader den Chart öffnet, zeichnet er nicht wild Linien,
/// sondern folgt einer strikten, methodischen Checkliste:
/// 1. Wirtschaftskalender checken
/// 2. Top-Down-Analyse (GKL einzeichnen)
/// 3. Sequenz-Mapping
/// 4. Confluence-Zonen markieren
/// 5. Einstieg definieren
/// 6. Lot-Size berechnen
/// 7. Stop-Loss setzen
/// 8. Ziele setzen
/// 9. Risiko rausnehmen (BE bei A-Bruch)"
///
/// Architektur-Rolle: Die Pipeline ist als **nachgelagerter Validator** aktiv in
/// <see cref="SequenzKonzeptStrategy.Evaluate"/> integriert (<c>RunMasterclassPipeline</c>-Helper).
/// Die eigentlichen Berechnungen passieren inline im <c>Evaluate</c>; die 9 Steps prüfen am Ende
/// formal, dass die berechneten Werte (entry, sl, tp1, tp2, scorer, navPointA etc.) im Daten-
/// Dictionary konsistent sind. Bei einem Step-Fail blockt die Strategie das Signal mit
/// <c>Blocked(navTf, "Pipeline Step {Name}: {Reason}")</c>.
///
/// Die Pipeline ist also **kein Geruest**, sondern ein Safety-Net / Buch-Compliance-Gate nach der
/// Signal-Berechnung. Eine alternative Architektur (Pipeline trägt die Berechnung selbst, Steps
/// statt Inline-Logik) ist ein möglicher zukünftiger Refactor, aktuell aber nicht angesetzt.
/// </summary>
public sealed class SkMasterclassPipeline
{
    private readonly List<IPipelineStep> _steps;

    public SkMasterclassPipeline(IEnumerable<IPipelineStep> steps)
    {
        _steps = steps.OrderBy(s => s.Order).ToList();
    }

    /// <summary>
    /// Führt alle Steps in Buch-Reihenfolge aus. Stopp beim ersten Fail.
    /// Liefert (success, failedStep, reason) — success=true wenn alle 9 Steps grün.
    /// </summary>
    public (bool success, int? failedStepOrder, string? failedStepName, string reason, Dictionary<string, object> data)
        Run(MarketContext context)
    {
        var data = new Dictionary<string, object>();
        foreach (var step in _steps)
        {
            var result = step.Execute(context, data);
            if (!result.Pass)
                return (false, step.Order, step.Name, $"Pipeline-Abbruch Step {step.Order} ({step.Name}): {result.Reason}", data);

            if (result.Data != null)
                foreach (var (key, value) in result.Data) data[key] = value;
        }
        return (true, null, null, "Pipeline komplett bestanden", data);
    }
}
