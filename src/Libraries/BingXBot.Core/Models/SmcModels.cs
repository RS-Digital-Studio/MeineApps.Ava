namespace BingXBot.Core.Models;

/// <summary>Richtung einer SMC-Zone (Order Block / Fair Value Gap).</summary>
public enum SmcZoneType
{
    /// <summary>Bullische Zone: Kaufdruck, Unterstützung (Entry für Longs).</summary>
    Bullish,
    /// <summary>Bärische Zone: Verkaufsdruck, Widerstand (Entry für Shorts).</summary>
    Bearish
}

/// <summary>
/// Order Block: Letzte Gegenkerze vor einem Break of Structure (BOS).
/// Market Maker akkumulieren in diesen Zonen — Preis reagiert oft beim Retest.
/// Bullish OB = letzte bärische Kerze vor bullischem BOS (Kaufzone).
/// Bearish OB = letzte bullische Kerze vor bärischem BOS (Verkaufszone).
/// </summary>
public record OrderBlock(
    decimal ZoneHigh,       // Obere Grenze (= High der OB-Kerze)
    decimal ZoneLow,        // Untere Grenze (= Low der OB-Kerze)
    SmcZoneType Type,       // Bullish/Bearish
    int CandleIndex,        // Index der OB-Kerze in der Candle-Liste
    DateTime Time,
    bool IsMitigated,       // True = Preis hat die Zone schon berührt (verbraucht)
    decimal Strength);      // 0-1: Stärke (basierend auf BOS-Distanz + Volume)

/// <summary>
/// Fair Value Gap (FVG / Imbalance): Preislücke die der Markt tendenziell füllt.
/// Bullish FVG: Kerze[i].Low > Kerze[i-2].High — Gap nach oben (Kaufzone beim Fill).
/// Bearish FVG: Kerze[i].High < Kerze[i-2].Low — Gap nach unten (Verkaufszone beim Fill).
/// </summary>
public record FairValueGap(
    decimal ZoneTop,        // Obere Grenze der Lücke
    decimal ZoneBottom,     // Untere Grenze der Lücke
    SmcZoneType Type,       // Bullish/Bearish
    int CandleIndex,        // Index der mittleren Kerze (die den Gap erzeugt)
    DateTime Time,
    bool IsMitigated,       // True = Preis hat die Lücke schon berührt
    decimal GapSize);       // Absolute Größe in Preiseinheiten

/// <summary>
/// Multi-Timeframe Struktur-Konsistenz: Wie viele Zeitebenen die gleiche Richtung zeigen.
/// Im SK-System sollen alle 3 Ebenen (HTF → Primary → Entry) aligned sein.
/// </summary>
public record StructureConsistency(
    bool? HtfDirection,         // HTF-Trend (null = unbekannt, true = bullisch)
    bool? PrimaryDirection,     // Primary-TF Sequenz-Richtung
    bool? EntryDirection,       // Entry-TF Sequenz-Richtung
    int AlignedCount,           // 0-3: Wie viele TFs in die gleiche Richtung zeigen
    int AvailableCount,         // Wie viele TFs Richtungsdaten geliefert haben (0-3)
    bool IsFullyAligned);       // True wenn alle verfügbaren TFs gleich (stärkstes Signal)
