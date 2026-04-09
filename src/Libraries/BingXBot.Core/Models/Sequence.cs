namespace BingXBot.Core.Models;

/// <summary>
/// Ein erkannter Swing-Punkt (lokales High oder Low) in der Preisstruktur.
/// Basis für A-B-C Sequenzen im SK-System.
/// </summary>
public record SwingPoint(
    decimal Price,        // Preis des Swing-Punkts (High oder Low)
    int CandleIndex,      // Index in der Candle-Liste (für zeitliche Einordnung)
    DateTime Time,        // Zeitstempel der Candle
    bool IsHigh);         // True = Swing-High, False = Swing-Low

/// <summary>
/// Eine A-B-C Sequenz im SK-System (Sequenz-Konzept).
/// Grundeinheit des Systems: Impuls (A→B) → Korrektur (B→C) → Zielbewegung (C→Extension).
/// </summary>
public class Sequence
{
    /// <summary>Punkt A: Startpunkt der Sequenz (Swing-Low bei Long, Swing-High bei Short).</summary>
    public required SwingPoint PointA { get; init; }

    /// <summary>Punkt B: Ende des Impulses (Swing-High bei Long, Swing-Low bei Short).</summary>
    public required SwingPoint PointB { get; init; }

    /// <summary>Punkt C: Ende der Korrektur im Fibonacci-Retracement. Null wenn noch nicht gebildet.</summary>
    public SwingPoint? PointC { get; init; }

    /// <summary>True = Aufwärts-Sequenz (A=Low→B=High→C=Low), False = Abwärts (A=High→B=Low→C=High).</summary>
    public bool IsLong { get; init; }

    /// <summary>Aktueller Zustand der Sequenz.</summary>
    public SequenceState State { get; set; }

    // === Fibonacci-Level (berechnet aus A-B Bewegung) ===

    /// <summary>38.2% Retracement von A→B. Obere Grenze der erweiterten Kaufzone.</summary>
    public decimal Retracement382 { get; init; }
    /// <summary>50% Retracement. Obere Grenze der idealen Kaufzone.</summary>
    public decimal Retracement500 { get; init; }
    /// <summary>55.9% Retracement. SK-spezifisches GKL-Level (untere ideale Zone).</summary>
    public decimal Retracement559 { get; init; }
    /// <summary>61.8% Retracement. Goldener Schnitt — stärkstes Fib-Level.</summary>
    public decimal Retracement618 { get; init; }
    /// <summary>66.7% Retracement. SK-spezifisches GKL-Level (untere Grenze).</summary>
    public decimal Retracement667 { get; init; }
    /// <summary>78.6% Retracement. Äußerste Grenze — danach droht Invalidierung.</summary>
    public decimal Retracement786 { get; init; }

    /// <summary>100% Extension (= Punkt B Höhe ab Punkt C). Konservatives erstes Ziel.</summary>
    public decimal Extension100 { get; init; }
    /// <summary>127.2% Extension — zwischen 100% und 161.8%.</summary>
    public decimal Extension1272 { get; init; }
    /// <summary>161.8% Extension von A-B (projiziert von C) — primäres Take-Profit.</summary>
    public decimal Extension1618 { get; init; }
    /// <summary>200% Extension — aggressives Ziel (doppelte A-B Bewegung ab C).</summary>
    public decimal Extension200 { get; init; }
    /// <summary>261.8% Extension — sekundäres Take-Profit (starke Trends).</summary>
    public decimal Extension2618 { get; init; }

    // === Hierarchie ===

    /// <summary>Übergeordnete Sequenz (für verschachtelte Analyse). Null bei Top-Level.</summary>
    public Sequence? ParentSequence { get; init; }
    /// <summary>True wenn dies eine interne Korrektur-Sequenz (IKI) innerhalb einer größeren ist.</summary>
    public bool IsIKI { get; init; }

    // === Berechnete Properties ===

    /// <summary>Range der A→B Bewegung in Preis-Einheiten.</summary>
    public decimal Range => Math.Abs(PointB.Price - PointA.Price);

    /// <summary>Ideale Kaufzone: 50-61.8% Retracement.</summary>
    public (decimal Upper, decimal Lower) IdealBuyZone => IsLong
        ? (Retracement500, Retracement618)
        : (Retracement618, Retracement500);

    /// <summary>GKL-Zone: 55.9-66.7% Retracement (SK-spezifisch).</summary>
    public (decimal Upper, decimal Lower) GklZone => IsLong
        ? (Retracement559, Retracement667)
        : (Retracement667, Retracement559);

    /// <summary>Prüft ob ein Preis in der idealen Kaufzone (50-61.8%) liegt.</summary>
    public bool IsInBuyZone(decimal price)
    {
        var (upper, lower) = IsLong
            ? (Retracement500, Retracement618)  // Long: 50% ist höher als 61.8%
            : (Retracement618, Retracement500); // Short: invertiert
        return price >= Math.Min(upper, lower) && price <= Math.Max(upper, lower);
    }

    /// <summary>Prüft ob ein Preis in der erweiterten GKL-Zone (55.9-66.7%) liegt.</summary>
    public bool IsInGklZone(decimal price)
    {
        var min = Math.Min(Retracement559, Retracement667);
        var max = Math.Max(Retracement559, Retracement667);
        return price >= min && price <= max;
    }

    /// <summary>Prüft ob der Preis die Extension-Zielzone (161.8%) erreicht hat.</summary>
    public bool HasReachedTarget(decimal price) => IsLong
        ? price >= Extension1618
        : price <= Extension1618;

    /// <summary>Prüft ob die Sequenz invalidiert ist (Preis unter/über Punkt A).</summary>
    public bool IsInvalidated(decimal price) => IsLong
        ? price < PointA.Price
        : price > PointA.Price;

    /// <summary>Berechnet die RRR für einen Entry am aktuellen Preis.</summary>
    public decimal CalculateRRR(decimal entryPrice)
    {
        var slDistance = Math.Abs(entryPrice - PointA.Price);
        var tpDistance = Math.Abs(Extension1618 - entryPrice);
        return slDistance > 0 ? tpDistance / slDistance : 0;
    }
}

/// <summary>Zustand einer SK-Sequenz.</summary>
public enum SequenceState
{
    /// <summary>A erkannt, B bildet sich gerade.</summary>
    Forming,
    /// <summary>B bestätigt. Preis korrigiert Richtung C im Fibonacci-Retracement.</summary>
    CorrectionZone,
    /// <summary>C gebildet (im Retracement). Wartet auf Break über/unter B (Aktivierung).</summary>
    WaitingBreak,
    /// <summary>Aktiviert: Kurs hat B durchbrochen. Ziel ist Extension-Zone.</summary>
    Active,
    /// <summary>Zielzone (161.8% oder 261.8%) erreicht. Sequenz abgeschlossen.</summary>
    TargetReached,
    /// <summary>Kurs unter A (Long) oder über A (Short). Sequenz ungültig.</summary>
    Invalidated
}

/// <summary>
/// Candlestick-Bestätigung am Entry-Punkt (SK-System: "Stabilisierung bei Punkt C").
/// Stärkere Bestätigung = höheres Confluence-Vertrauen.
/// </summary>
public enum CandleConfirmation
{
    /// <summary>Keine Bestätigung — Preis hat die Zone noch nicht betreten.</summary>
    None,
    /// <summary>Preis berührt die Zone nur kurz (schwächstes Signal).</summary>
    PriceTouches,
    /// <summary>2-3 Kerzen schließen in der Zone (Stabilisierung — gutes Signal).</summary>
    StableInZone,
    /// <summary>Hammer/Pin-Bar-Muster in der Zone (Umkehr-Bestätigung).</summary>
    HammerOrPin,
    /// <summary>Bullish/Bearish Engulfing in der Zone (starkes Umkehr-Signal).</summary>
    Engulfing,
    /// <summary>Starkes Volumen bei Entry (institutionelles Interesse).</summary>
    HighVolume
}

/// <summary>Break of Structure — ein signifikantes Swing-Level wurde durchbrochen.</summary>
public record StructureBreak(
    SwingPoint BrokenPoint,   // Der durchbrochene Swing-Punkt
    bool IsBullish,           // True = bullischer Break (höheres High), False = bärisch
    DateTime Time);           // Zeitpunkt des Breaks

/// <summary>Change of Character — Trendwechsel erkannt (erster Lower-Low nach Uptrend oder umgekehrt).</summary>
public record CharacterChange(
    bool FromBullishToBearish,  // True = Wechsel von bullisch zu bärisch
    SwingPoint TriggerPoint,    // Der Swing-Punkt der den Wechsel ausgelöst hat
    DateTime Time);

/// <summary>Eine erkannte Liquiditätszone (Stop-Loss-Cluster / Volume-Node).</summary>
public record LiquidityZone(
    decimal PriceLevel,         // Preis-Level der Zone
    decimal Strength,           // Stärke (0-1): Mehrere Swings am Level = stärker
    LiquidityType Type);        // Art der Liquidität

/// <summary>Art einer Liquiditätszone.</summary>
public enum LiquidityType
{
    /// <summary>Cluster von Stop-Losses unter Swing-Lows (Long-Liquidationen).</summary>
    StopLossCluster,
    /// <summary>Einzelner prominenter Swing der als Liquidationsziel dient.</summary>
    SwingLiquidation,
    /// <summary>Preis-Level mit hohem kumulativem Handelsvolumen.</summary>
    HighVolumeNode
}
