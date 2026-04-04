using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

/// <summary>
/// Exit-Phase einer offenen Position im Multi-Stage Exit System.
/// Initial → Tp1Hit → Trailing (oder direkt SL/Regime-Exit).
/// </summary>
public enum ExitPhase
{
    /// <summary>Position offen, wartet auf TP1 oder SL.</summary>
    Initial,
    /// <summary>TP1 erreicht: 50% geschlossen, SL auf Break-Even, Trailing aktiv.</summary>
    Tp1Hit,
    /// <summary>Chandelier-Trailing aktiv nach TP1 (SL folgt dem Höchstpunkt).</summary>
    Trailing
}

/// <summary>
/// Vollständiger Zustand einer offenen Position für das Multi-Stage Exit System.
/// Ersetzt die separaten Dictionaries (_positionSignals, _extremePriceSinceEntry, _positionTrailingPercent).
/// Thread-safe durch ConcurrentDictionary im TradingServiceBase.
/// </summary>
public class PositionExitState
{
    /// <summary>Originales Signal mit SL/TP1 (wird bei TP1-Hit modifiziert: SL→Break-Even).</summary>
    public SignalResult Signal { get; set; } = null!;

    /// <summary>Aktuelle Exit-Phase.</summary>
    public ExitPhase Phase { get; set; } = ExitPhase.Initial;

    /// <summary>Entry-Preis der Position.</summary>
    public decimal EntryPrice { get; set; }

    /// <summary>Ursprüngliche Positionsgröße (für 50% Partial-Close bei TP1).</summary>
    public decimal OriginalQuantity { get; set; }

    /// <summary>Take-Profit 2 (weiterer TP nach TP1 Partial-Close).</summary>
    public decimal? Tp2 { get; set; }

    /// <summary>Zeitpunkt des Entries (für Time-based Exit: 48h ohne TP1 → schließen).</summary>
    public DateTime EntryTime { get; set; } = DateTime.UtcNow;

    /// <summary>Höchster (Long) oder niedrigster (Short) Preis seit Entry (für Chandelier-Trailing).</summary>
    public decimal ExtremePriceSinceEntry { get; set; }

    /// <summary>ATR-Multiplikator für den Chandelier-Trailing-Stop (vol-adaptiv, default 2.5).</summary>
    public decimal TrailingAtrMultiplier { get; set; } = 2.5m;

    /// <summary>Aktueller ATR-Wert zum Zeitpunkt des Entries (für vol-adaptive Exits).</summary>
    public decimal CurrentAtr { get; set; }

    /// <summary>Confluence-Score des Entry-Signals (0-12, für ATI-Lernen).</summary>
    public int ConflueceScore { get; set; }

    /// <summary>Seite der Position (Buy/Sell).</summary>
    public Side Side { get; set; }

    /// <summary>Symbol der Position.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Max. Haltezeit in Stunden (48h default, 96h nach TP1). 0 = unbegrenzt.</summary>
    public int MaxHoldHours { get; set; } = 48;

    /// <summary>Ob die Position bereits teilweise geschlossen wurde (TP1 Partial-Close).</summary>
    public bool PartialClosed { get; set; }
}
