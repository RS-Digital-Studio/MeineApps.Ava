namespace BingXBot.Backtest.Simulation;

/// <summary>Tracking-State für Multi-Stage Exit im Backtest.</summary>
internal sealed class BacktestExitState
{
    /// <summary>Einstiegspreis der Position (für Break-Even-Berechnung).</summary>
    public decimal EntryPrice { get; init; }
    /// <summary>Ursprüngliche Positionsgröße (für Partial-Close-Berechnung).</summary>
    public decimal OriginalQuantity { get; init; }
    /// <summary>Zeitpunkt des Einstiegs (für Time-Exit).</summary>
    public DateTime EntryTime { get; init; }
    /// <summary>Zweites Take-Profit-Ziel (200% + Buffer).</summary>
    public decimal? Tp2 { get; set; }
    /// <summary>Ob TP1 bereits erreicht und Partial Close ausgeführt wurde.</summary>
    public bool PartialClosed { get; set; }
    /// <summary>SK-Buch Masterclass: BE bereits gesetzt (ausgelöst durch A-Bruch).</summary>
    public bool BreakevenSet { get; set; }
    /// <summary>Navigator-PointA für den A-Bruch-BE-Trigger. 0 = unbekannt (kein BE möglich).</summary>
    public decimal NavPointA { get; init; }

    // === Runner-Trailing (Task 4.7 / EnableRunner) ===
    /// <summary>Runner aktiv: Rest-Position laeuft nach TP2 mit ATR-Trailing-Stop weiter.</summary>
    public bool RunnerActive { get; set; }
    /// <summary>Bestpreis seit Runner-Aktivierung (Trailing-Anker).</summary>
    public decimal RunnerTrailAnchor { get; set; }
    /// <summary>ATR-Basis fuer die Trailing-Distanz (aus SignalResult.EntryAtr).</summary>
    public decimal RunnerAtrBase { get; init; }
}
