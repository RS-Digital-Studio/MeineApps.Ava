using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

public record SignalResult(
    Signal Signal,
    decimal Confidence,
    decimal? EntryPrice,
    decimal? StopLoss,
    decimal? TakeProfit,
    string Reason,
    decimal? TakeProfit2 = null,
    /// <summary>Optionaler Confidence-Score (0-10) für Position-Sizing. 0 = neutral, ≥5 = volle Größe.</summary>
    int ConfluenceScore = 0,
    /// <summary>SL bei Korrektionen NICHT in den Gewinn verschieben — aktiviert den A-Bruch/2x-SL-Break-Even.</summary>
    bool DisableSmartBreakeven = false,
    /// <summary>ATR-Wert zum Entry-Zeitpunkt, für die Trailing-Stop-Berechnung beim Runner.</summary>
    decimal? EntryAtr = null,
    /// <summary>
    /// Optionaler PointA-Preis als zweiter Break-Even-Trigger (Preis durchbricht A → BE setzen).
    /// Null/0 = nur der 2x-SL-Distanz-Trigger greift.
    /// </summary>
    decimal? NavPointA = null,
    /// <summary>Hard-Cap-Preis für den Runner. Sobald erreicht, wird zwangsgeschlossen.</summary>
    decimal? RunnerHardCap = null,
    /// <summary>Multiplikator für Position-Size (z.B. 0.5 = halbe Position). Null = keine Anpassung.</summary>
    decimal? PositionScaleOverride = null);
