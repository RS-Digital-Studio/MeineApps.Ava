using BingXBot.Core.Enums;

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
///
/// BUCH-REGEL DOCHT-MESSUNG (Task 4.1, Masterclass): Fibonacci-Punkte (Point0, PointA, PointB)
/// werden IMMER an den Kerzendochten (Wicks/Spikes) gemessen, NIE an den Kerzenkörpern oder Closes.
/// Buch-Zitat: "Das SK-System zieht die Fibonacci-Punkte (0, A, B, C) immer exakt an den
/// Spitzen der Kerzendochte (Wicks/Spikes) an, nicht an den Kerzenkörpern. Das Smart Money
/// operiert an den absoluten Extrempunkten der Liquidität."
/// </summary>
public class Sequence
{
    // SK-VERIFY: Abweichung #4 — SK-Nomenklatur
    /// <summary>
    /// Punkt 0: Startpunkt der Sequenz (Swing-Low bei Long, Swing-High bei Short).
    /// Task 4.1: Immer am Kerzendocht gemessen (candle.Low bei Long, candle.High bei Short).
    /// </summary>
    public required SwingPoint Point0 { get; init; }

    // SK-VERIFY: Abweichung #4 — SK-Nomenklatur
    /// <summary>
    /// Punkt A: Ende des Impulses (Swing-High bei Long, Swing-Low bei Short).
    /// Task 4.1: Immer am Kerzendocht gemessen (candle.High bei Long, candle.Low bei Short).
    /// </summary>
    public required SwingPoint PointA { get; init; }

    // SK-VERIFY: Abweichung #4 — SK-Nomenklatur
    /// <summary>
    /// Punkt B: Ende der Korrektur im Fibonacci-Retracement. Null wenn noch nicht gebildet.
    /// Task 4.1: Immer am Kerzendocht gemessen (candle.Low bei Long, candle.High bei Short).
    /// </summary>
    public SwingPoint? PointB { get; init; }

    /// <summary>True = Aufwärts-Sequenz (A=Low→B=High→C=Low), False = Abwärts (A=High→B=Low→C=High).</summary>
    public bool IsLong { get; init; }

    /// <summary>Aktueller Zustand der Sequenz.</summary>
    public SequenceState State { get; set; }

    // === Fibonacci-Level (berechnet aus A-B Bewegung) ===
    // Buch-konforme Retracement-Tabelle: 50 / 55.9 / 61.8 / 66.7 / 71 / 78.6.

    /// <summary>50% Retracement. Obere Grenze der idealen Kaufzone.</summary>
    public decimal Retracement500 { get; init; }
    /// <summary>55.9% Retracement. SK-spezifisches Kernlevel (Buch-Tabelle "Kernlevel").</summary>
    public decimal Retracement559 { get; init; }
    /// <summary>61.8% Retracement. Goldener Schnitt — stärkstes Fib-Level.</summary>
    public decimal Retracement618 { get; init; }
    /// <summary>66.7% Retracement. Untere Grenze der Korrekturbox.</summary>
    public decimal Retracement667 { get; init; }
    /// <summary>70.2% Retracement. Intern für SL-Buffer-Projektionen (nicht im Buch).</summary>
    public decimal Retracement702 { get; init; }
    /// <summary>71.0% Retracement. Buch-Anhang: "Erweiterte Korrektur" zwischen 66.7% und 78.6%.</summary>
    public decimal Retracement71 { get; init; }
    /// <summary>78.6% Retracement. Maximales gültiges Korrekturlevel laut Buch-Anhang.</summary>
    public decimal Retracement786 { get; init; }

    // Buch-konforme Extension-Tabelle: 161.8 / 200 / 261.8 / 423.6 (+ 100% als Intern-Hilfslevel).

    /// <summary>100% Extension (= Punkt B Höhe ab Punkt C). Intern als Retracement-Basis für BCKL.</summary>
    public decimal Extension100 { get; init; }
    /// <summary>161.8% Extension von A-B (projiziert von C) — TP1 laut Buch.</summary>
    public decimal Extension1618 { get; init; }
    /// <summary>200% Extension — TP2 laut Buch.</summary>
    public decimal Extension200 { get; init; }
    /// <summary>261.8% Extension (Buch-Anhang: "Überschießung / Extremer Zielbereich"). Runner-Ziel.</summary>
    public decimal Extension2618 { get; init; }
    /// <summary>423.6% Extension (Buch-Anhang: "Absolute Maximalausdehnung"). Hard-Cap für Runner.</summary>
    public decimal Extension4236 { get; init; }

    /// <summary>
    /// SK-Plan 3.3: Candle-Index zur Zeit der Point0-Entstehung.
    /// Wird für Max-Age-Filter verwendet: Alte Sequenzen (&gt; Max-Age seit Point0) werden verworfen.
    /// 0 = unbekannt (z.B. bei Sequenzen die ohne Index-Tracking gebaut wurden).
    /// </summary>
    public int CreatedAtCandleIndex { get; init; }

    /// <summary>
    /// SK-Plan 4.2: Impuls/Korrektur-Ratio = |Point0→PointA| / |PointA→PointB|.
    /// Goldene Ratio ≥ 1.618 → hochwertige Sequenz, &lt; 0.8 → überkorrigierend (schwach).
    /// Null wenn PointB noch nicht gesetzt ist.
    /// </summary>
    public decimal ImpulseCorrectionRatio
    {
        get
        {
            if (PointB == null) return 0m;
            var impulseRange = Math.Abs(PointA.Price - Point0.Price);
            var correctionRange = Math.Abs(PointA.Price - PointB.Price);
            return correctionRange > 0 ? impulseRange / correctionRange : 0m;
        }
    }

    /// <summary>
    /// SK-VERIFY: [BC-Zone-Guard] True wenn die BC-Zone als Re-Entry unbrauchbar ist
    /// (Preis hat mindestens einmal die 138.2% Extension erreicht seit Aktivierung).
    /// Wird von der State Machine gesetzt und durch ToSequence() propagiert. Persistenter
    /// Flag (Hysterese): Sobald 138.2% erreicht, bleibt BC-Zone invalid auch wenn Preis zurückkommt.
    /// </summary>
    public bool IsBcZoneInvalid { get; set; }

    // === Berechnete Properties ===

    /// <summary>Range der A→B Bewegung in Preis-Einheiten.</summary>
    public decimal Range => Math.Abs(PointA.Price - Point0.Price);

    // BUCH-ONLY: Extension1382 (138.2% Over-Extension-Guard) entfernt — kein Buch-Level.

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
    /// Task 2.4: Overload mit MarketCategory bietet asset-klassen-spezifische Toleranzen.
    /// </summary>
    public bool HasReachedTarget(decimal price)
    {
        var tolerance = Math.Abs(Extension1618) * 0.0003m;  // Buch-Regel: 5 Pips ≈ 0.03%
        return IsLong
            ? price >= Extension1618 - tolerance
            : price <= Extension1618 + tolerance;
    }

    /// <summary>
    /// Task 2.4 — Asset-klassen-spezifische TP-Toleranz. Buch nennt 5 Pips als Beispiel,
    /// was bei Forex (0.01% Pip) 0.05%, bei Stocks aber 0.10%+ sein kann.
    /// Toleranzen: Krypto 0.03%, Forex 0.05%, Commodity 0.05%, Index/Stock 0.10%.
    /// </summary>
    public bool HasReachedTarget(decimal price, MarketCategory category)
    {
        var tolerancePercent = category switch
        {
            MarketCategory.Crypto => 0.0003m,       // 0.03% — Krypto-Standard
            MarketCategory.Forex => 0.0005m,        // 0.05% — Forex-Standard
            MarketCategory.Commodity => 0.0005m,    // 0.05% — Gold/Silber/Öl
            MarketCategory.Index => 0.001m,         // 0.10% — Indices sind großzügiger
            MarketCategory.Stock => 0.001m,         // 0.10% — Einzelaktien-Spread
            _ => 0.0003m
        };
        var tolerance = Math.Abs(Extension1618) * tolerancePercent;
        return IsLong
            ? price >= Extension1618 - tolerance
            : price <= Extension1618 + tolerance;
    }

    /// <summary>Prüft ob die Sequenz vollständig abgearbeitet ist (200% Extension — SK-Regel).</summary>
    public bool HasFullyCompleted(decimal price) => IsLong
        ? price >= Extension200
        : price <= Extension200;

    /// <summary>
    /// Prüft ob die Sequenz invalidiert ist (Preis unter/über Punkt 0).
    /// SK-Buch: Point0-Bruch = Sequenz sofort ungültig, keine Toleranz.
    /// Long: invalidiert wenn price &lt; Point0. Short: price &gt; Point0.
    /// </summary>
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

// BUCH-ONLY: WaveCharacter, SequenceType und SequenceHierarchy waren Zusatz-Heuristiken
// (CWS-Workflow / SK-System-Typ-1-2-3). Das SK-Buch kennt nur die erkannte 0-A-B-C-Sequenz —
// ohne Wellen-Klassifikation, ohne Typ-Unterscheidung und ohne Multi-TF-Hierarchie.

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
