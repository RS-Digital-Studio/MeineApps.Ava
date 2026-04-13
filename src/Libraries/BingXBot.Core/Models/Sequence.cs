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
    // SK-VERIFY: Abweichung #4 — SK-Nomenklatur
    /// <summary>Punkt 0: Startpunkt der Sequenz (Swing-Low bei Long, Swing-High bei Short).</summary>
    public required SwingPoint Point0 { get; init; }

    // SK-VERIFY: Abweichung #4 — SK-Nomenklatur
    /// <summary>Punkt A: Ende des Impulses (Swing-High bei Long, Swing-Low bei Short).</summary>
    public required SwingPoint PointA { get; init; }

    // SK-VERIFY: Abweichung #4 — SK-Nomenklatur
    /// <summary>Punkt B: Ende der Korrektur im Fibonacci-Retracement. Null wenn noch nicht gebildet.</summary>
    public SwingPoint? PointB { get; init; }

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

    /// <summary>Hierarchie der Sequenz im Multi-TF-Kontext.</summary>
    public SequenceHierarchy Hierarchy { get; set; } = SequenceHierarchy.Primary;

    /// <summary>Sequenztyp: Normal (handelbar), Überextendiert oder Langgezogen (nur Analyse).</summary>
    public SequenceType Type { get; set; }

    /// <summary>True wenn dieser Sequenztyp für Entries geeignet ist (nur Typ 1 = Normal).</summary>
    public bool IsTradeableType => Type == SequenceType.Normal;

    /// <summary>Charakter der A→B Welle (initialer Impuls). Impulsiv = gut.</summary>
    public WaveCharacter WaveAB { get; set; }
    /// <summary>Charakter der B→C Welle (Korrektur). Korrektiv = gut.</summary>
    public WaveCharacter WaveBC { get; set; }

    /// <summary>Gesamtcharakter der Sequenz: IKI (ideal), IKK, KIK, etc.</summary>
    public string CharacterPattern => $"{(WaveAB == WaveCharacter.Impulsive ? 'I' : 'K')}" +
                                       $"{(WaveBC == WaveCharacter.Corrective ? 'K' : 'I')}";

    /// <summary>True wenn der Sequenzcharakter gut ist (IK = impulsiver Impuls + korrektive Korrektur).</summary>
    public bool HasGoodCharacter => WaveAB == WaveCharacter.Impulsive && WaveBC == WaveCharacter.Corrective;

    // === Berechnete Properties ===

    /// <summary>Range der A→B Bewegung in Preis-Einheiten.</summary>
    public decimal Range => Math.Abs(PointA.Price - Point0.Price);

    // SK-VERIFY: [Abweichung #1] Kaufzone = 50-66.7% (SK Golden Pocket), NICHT 50-61.8%
    /// <summary>Ideale Kaufzone: 50-66.7% Retracement (SK Golden Pocket).</summary>
    public (decimal Upper, decimal Lower) IdealBuyZone => IsLong
        ? (Retracement500, Retracement667)
        : (Retracement667, Retracement500);

    // SK-VERIFY: [Abweichung #2] GKL-Zone = 50-66.7%, NICHT 55.9-66.7%
    /// <summary>GKL-Zone: 50-66.7% Retracement (SK Golden Pocket).</summary>
    public (decimal Upper, decimal Lower) GklZone => IsLong
        ? (Retracement500, Retracement667)
        : (Retracement667, Retracement500);

    // SK-VERIFY: [Abweichung #1] Kaufzone korrigiert: 50-66.7% statt 50-61.8%
    /// <summary>Prüft ob ein Preis in der Kaufzone (50-66.7% SK Golden Pocket) liegt.</summary>
    public bool IsInBuyZone(decimal price)
    {
        var (upper, lower) = IsLong
            ? (Retracement500, Retracement667)  // Long: 50% ist höher als 66.7%
            : (Retracement667, Retracement500); // Short: invertiert
        return price >= Math.Min(upper, lower) && price <= Math.Max(upper, lower);
    }

    // SK-VERIFY: [Abweichung #2] GKL korrigiert: 50-66.7% statt 55.9-66.7%
    /// <summary>Prüft ob ein Preis in der GKL-Zone (50-66.7%) liegt.</summary>
    public bool IsInGklZone(decimal price)
    {
        var min = Math.Min(Retracement500, Retracement667);
        var max = Math.Max(Retracement500, Retracement667);
        return price >= min && price <= max;
    }

    /// <summary>
    /// Prüft ob der Preis die Extension-Zielzone (161.8%) erreicht hat — erste TP-Zone.
    /// Buch Workflow 6.5: "Zielbereich gilt als abgearbeitet bei 5 Pips Toleranz vor dem 161.8er."
    /// Toleranz = 0.03% des Preises (Krypto: ≈ 5 "Pips" bei 1 Pip = 1/10000).
    /// </summary>
    public bool HasReachedTarget(decimal price)
    {
        var tolerance = Math.Abs(Extension1618) * 0.0003m;  // Buch-Regel: 5 Pips ≈ 0.03%
        return IsLong
            ? price >= Extension1618 - tolerance
            : price <= Extension1618 + tolerance;
    }

    /// <summary>Prüft ob die Sequenz vollständig abgearbeitet ist (200% Extension — SK-Regel).</summary>
    public bool HasFullyCompleted(decimal price) => IsLong
        ? price >= Extension200
        : price <= Extension200;

    /// <summary>Prüft ob die Sequenz invalidiert ist (Preis unter/über Punkt A).</summary>
    public bool IsInvalidated(decimal price) => IsLong
        ? price < Point0.Price
        : price > Point0.Price;

    /// <summary>Berechnet die RRR für einen Entry am aktuellen Preis.</summary>
    public decimal CalculateRRR(decimal entryPrice)
    {
        var slDistance = Math.Abs(entryPrice - Point0.Price);
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
    /// <summary>Zielzone (161.8%) erreicht. Erste Gewinnmitnahme-Zone.</summary>
    TargetReached,
    /// <summary>200% Extension erreicht. Sequenz vollständig abgearbeitet (SK-Regel).</summary>
    FullyCompleted,
    /// <summary>Kurs unter 0 (Long) oder über 0 (Short). Sequenz ungültig.</summary>
    Invalidated
}

/// <summary>
/// Wellencharakter im SK-System: Impulsiv (schnell, gerichtet) oder Korrektiv (langsam, seitwärts).
/// Ideal: A→B impulsiv (starker Impuls), B→C korrektiv (ordentliche Korrektur).
/// </summary>
public enum WaveCharacter
{
    /// <summary>Noch nicht klassifiziert.</summary>
    Unknown,
    /// <summary>Impulsiv: Schnelle, gerichtete Bewegung mit großen Kerzen-Bodies. Gut für Impulswelle (A→B).</summary>
    Impulsive,
    /// <summary>Korrektiv: Langsame, seitwärts-gerichtete Bewegung mit kleinen Kerzen. Gut für Korrekturwelle (B→C).</summary>
    Corrective
}

/// <summary>
/// Sequenztyp nach SK-System (Stefan Kassing).
/// Typ 1 = handelbar, Typ 2+3 = nur für übergeordnete Analyse.
/// </summary>
public enum SequenceType
{
    /// <summary>Normale Sequenz: B im 50-66.7% Retracement. Valid für Entry UND Analyse.</summary>
    Normal,
    /// <summary>Überextendiert: B-C Bewegung war stark impulsiv/zielstrebig. NUR Analyse, KEIN Entry.</summary>
    Overextended,
    /// <summary>Langgezogen: B-C Bewegung durch anhaltenden Druck verlängert. NUR Analyse, KEIN Entry.</summary>
    Elongated
}

/// <summary>
/// Hierarchie einer Sequenz im Multi-Timeframe-Kontext.
/// Primary = Haupt-Sequenz, Secondary = untergeordnet in übergeordneter Zone, Breakout = nach Invalidierung.
/// </summary>
public enum SequenceHierarchy
{
    /// <summary>Haupt-Sequenz (direkt auf dem TF erkannt).</summary>
    Primary,
    /// <summary>Sekundäre Sequenz: 1H-Sequenz innerhalb einer 4H-Zone.</summary>
    Secondary,
    /// <summary>Breakout-Sequenz: Gegensequenz wurde invalidiert → Ausbruch in Hauptrichtung.</summary>
    Breakout
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
