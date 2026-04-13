using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

/// <summary>
/// Exit-Phase einer offenen Position im Multi-Stage Exit System.
/// Buch-konform: Initial → Tp1Hit (nach TP1 Partial Close).
/// </summary>
public enum ExitPhase
{
    /// <summary>Position offen, wartet auf TP1 oder SL.</summary>
    Initial,
    /// <summary>TP1 (161.8%) erreicht: 50% geschlossen, Rest läuft bis TP2 (200%+Buffer).</summary>
    Tp1Hit,
}

/// <summary>
/// Vollständiger Zustand einer offenen Position für das Buch-konforme Exit-System.
/// Gespeichert in ConcurrentDictionary im TradingServiceBase.
/// THREAD-SAFETY: Properties werden NUR aus dem PriceTickerLoop mutiert (sequentiell pro Service).
/// Kein paralleler Schreibzugriff erlaubt — ConcurrentDictionary sichert nur Add/Get/Remove.
/// </summary>
public class PositionExitState
{
    /// <summary>Originales Signal mit SL/TP1 (wird bei TP1-Hit modifiziert: TP auf TP2).</summary>
    public SignalResult Signal { get; set; } = null!;

    /// <summary>Aktuelle Exit-Phase.</summary>
    public ExitPhase Phase { get; set; } = ExitPhase.Initial;

    /// <summary>Entry-Preis der Position.</summary>
    public decimal EntryPrice { get; set; }

    /// <summary>Ursprüngliche Positionsgröße (für 50% Partial-Close bei TP1).</summary>
    public decimal OriginalQuantity { get; set; }

    /// <summary>Take-Profit 2 (200% Extension + 20 Pips Buffer, Buch Workflow 4.5).</summary>
    public decimal? Tp2 { get; set; }

    /// <summary>Zeitpunkt des Entries (für Time-based Exit).</summary>
    public DateTime EntryTime { get; set; } = DateTime.UtcNow;

    /// <summary>Seite der Position (Buy/Sell).</summary>
    public Side Side { get; set; }

    /// <summary>Symbol der Position.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Max. Haltezeit in Stunden. 0 = unbegrenzt (Buch-konform).</summary>
    public int MaxHoldHours { get; set; } = 0;

    /// <summary>Ob die Position bereits teilweise geschlossen wurde (TP1 Partial-Close).</summary>
    public bool PartialClosed { get; set; }

    /// <summary>Ob der Auto-Breakeven bereits gesetzt wurde (Buch Workflow 4.2).</summary>
    public bool BreakevenSet { get; set; }

    /// <summary>SK-Buch: SL bereits halbiert (Workflow 4.1 — bei 1× SL-Distanz im Gewinn).</summary>
    public bool SlHalved { get; set; }

    /// <summary>
    /// Ob diese Position nach App-Neustart wiederhergestellt wurde.
    /// Time-Exit bekommt eine Karenz, da die echte Haltezeit unbekannt ist
    /// (BingX liefert kein OpenTime in GetPositionsAsync).
    /// </summary>
    public bool IsRecovered { get; set; }
}
