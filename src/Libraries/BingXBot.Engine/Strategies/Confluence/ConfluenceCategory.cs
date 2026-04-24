namespace BingXBot.Engine.Strategies.Confluence;

/// <summary>
/// Task 2.2 — Confluence-Kategorien nach SK-Buch ("3-4 Bestätigungsgruppen").
/// Ein Signal muss möglichst viele Kategorien gleichzeitig bestätigen.
/// GKL-Treffer hat als wertvollstes Setup doppeltes Gewicht.
/// </summary>
public enum ConfluenceCategory
{
    /// <summary>Sequenz ist aktiviert (Preis hat Punkt A durchbrochen).</summary>
    PriceAction,
    /// <summary>B im Golden Pocket (50-66.7%) — ideale Entry-Zone.</summary>
    FibonacciGoldenPocket,
    /// <summary>GKL-Treffer auf W1 oder D1 — Königsdisziplin (+2 Punkte).</summary>
    GklMasterZone,
    /// <summary>Fahrplan-Alignment (W1 + D1 Bias stimmt mit Trade-Richtung überein).</summary>
    FahrplanAlignment,
    /// <summary>Höhere TF-Sequenz aktiv in gleiche Richtung (Trend im Rücken).</summary>
    HigherTfSequence,
    /// <summary>Volume-Spike in den letzten 3 Kerzen (Smart-Money-Interesse).</summary>
    VolumeSpike,
    /// <summary>BCKL-Re-Entry (pyramidisieren auf laufendem Trend).</summary>
    BcklReEntry,
    /// <summary>
    /// SK-Spec §7 "Heiliger Gral": HTF-GKL-Zone überlappt geometrisch mit LTF-BC-Zone ODER LTF-EXT-1.618
    /// einer Gegenrichtungs-Sequenz. Das ist die stärkste Confluence im SK-System — +2 Punkte.
    /// Doku-Zitat: "Die stärksten Trades entstehen genau dann, wenn verschiedene Fibonacci-Level aus
    /// völlig unterschiedlichen Zügen exakt übereinanderliegen und eine kompakte, massive Box im Chart bilden."
    /// </summary>
    HighProbabilityZone
}
