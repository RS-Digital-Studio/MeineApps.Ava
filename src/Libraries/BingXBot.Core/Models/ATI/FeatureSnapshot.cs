namespace BingXBot.Core.Models.ATI;

/// <summary>
/// Normalisierter Feature-Vektor eines Marktzustands zum Zeitpunkt eines Signals.
/// Wird fuer ML-Training gespeichert (Features + Outcome = Trainingsdaten).
/// Alle Werte sind auf [-1, 1] oder [0, 1] normalisiert.
/// </summary>
public class FeatureSnapshot
{
    // === Preis-Features (Abweichung vom Durchschnitt) ===
    /// <summary>(Close - EMA20) / EMA20</summary>
    public float PriceVsEma20 { get; set; }
    /// <summary>(Close - EMA50) / EMA50</summary>
    public float PriceVsEma50 { get; set; }
    /// <summary>(Close - EMA200) / EMA200</summary>
    public float PriceVsEma200 { get; set; }
    /// <summary>sign(EMA20 - EMA50): 1=bullish cross, -1=bearish cross</summary>
    public float EmaCrossDirection { get; set; }

    // === Momentum-Features ===
    /// <summary>RSI / 100 -> [0, 1]</summary>
    public float RsiNormalized { get; set; }
    /// <summary>MACD Histogram / ATR -> normalisiert</summary>
    public float MacdHistogramNormalized { get; set; }
    /// <summary>Stochastik %K / 100 -> [0, 1]</summary>
    public float StochKNormalized { get; set; }
    /// <summary>Stochastik %D / 100 -> [0, 1]</summary>
    public float StochDNormalized { get; set; }

    // === Volatilitaets-Features ===
    /// <summary>ATR / Close -> Volatilitaet als Prozent</summary>
    public float AtrPercent { get; set; }
    /// <summary>(BB_Upper - BB_Lower) / BB_Middle -> normalisierte Bandbreite</summary>
    public float BollingerWidth { get; set; }
    /// <summary>(Close - BB_Lower) / (BB_Upper - BB_Lower) -> [0, 1] Position im Band</summary>
    public float BollingerPosition { get; set; }

    // === Trend-Features ===
    /// <summary>ADX / 100 -> [0, 1] Trend-Staerke</summary>
    public float AdxNormalized { get; set; }
    /// <summary>Higher-Timeframe Trend: -1=bearish, 0=neutral, 1=bullish</summary>
    public float HtfTrend { get; set; }

    // === Volumen-Features ===
    /// <summary>Aktuelles Volumen / SMA20(Volumen) -> >1 = ueberdurchschnittlich</summary>
    public float VolumeRatio { get; set; }

    // === Markt-Features ===
    /// <summary>Funding Rate (0 wenn nicht verfuegbar)</summary>
    public float FundingRate { get; set; }
    /// <summary>Trading-Session: 0=Asia(0-8 UTC), 1=Europe(8-14 UTC), 2=US(14-22 UTC), 3=Late(22-0)</summary>
    public float SessionId { get; set; }

    // === Cross-Market Features (BTC-Kontext für Alt-Trading) ===
    /// <summary>BTC 24h-Return normalisiert auf [-1, 1]. Positiv = BTC steigt, Alts folgen oft.</summary>
    public float BtcReturn24h { get; set; }
    /// <summary>BTC-Trend: -1=bearish, 0=neutral, 1=bullish (Preis vs EMA50 auf D1).</summary>
    public float BtcTrend { get; set; }
    /// <summary>Korrelation des Symbols mit BTC über 20 Perioden. Hoch (>0.7) = BTC-abhängig.</summary>
    public float BtcCorrelation { get; set; }
    /// <summary>Gesamtmarkt-Stimmung: Mittlerer 24h-Return der Top-Coins. Positiv = Altseason.</summary>
    public float MarketSentiment { get; set; }
    /// <summary>Fear & Greed Index (0-100, normalisiert auf [0, 1]). 0 = Extreme Fear, 1 = Extreme Greed.</summary>
    public float FearGreedIndex { get; set; }

    // === Derivatives-Features ===
    /// <summary>Open Interest Change (normalisiert): Positive Werte = OI steigt, negative = OI sinkt.</summary>
    public float OpenInterestChange { get; set; }

    // === Fibonacci-Features ===
    /// <summary>Nähe zum nächsten Fib-Key-Level (38.2%/50%/61.8%): 1.0=exakt, 0.0=weiter als 1 ATR</summary>
    public float FibProximity { get; set; }

    // === Pattern-Features ===
    /// <summary>Aufeinanderfolgende gruene Kerzen / 10 -> normalisiert</summary>
    public float ConsecutiveUpCandles { get; set; }
    /// <summary>Aufeinanderfolgende rote Kerzen / 10 -> normalisiert</summary>
    public float ConsecutiveDownCandles { get; set; }
    /// <summary>20-Perioden Return: (Close - Close[20]) / Close[20]</summary>
    public float RecentReturnPercent { get; set; }

    // === Metadaten (nicht als Features fuer ML, aber fuer Analyse) ===
    /// <summary>Symbol des Handelsinstruments</summary>
    public string Symbol { get; set; } = "";
    /// <summary>Zeitpunkt der Feature-Extraktion</summary>
    public DateTime Timestamp { get; set; }
    /// <summary>Erkanntes Regime zum Zeitpunkt</summary>
    public MarketRegime Regime { get; set; }

    /// <summary>
    /// Konvertiert die numerischen Features in ein float-Array fuer ML-Inferenz.
    /// Reihenfolge muss konsistent mit dem Training sein!
    /// </summary>
    public float[] ToFeatureArray() =>
    [
        PriceVsEma20, PriceVsEma50, PriceVsEma200, EmaCrossDirection,
        RsiNormalized, MacdHistogramNormalized, StochKNormalized, StochDNormalized,
        AtrPercent, BollingerWidth, BollingerPosition,
        AdxNormalized, HtfTrend,
        VolumeRatio,
        FundingRate, SessionId,
        BtcReturn24h, BtcTrend, BtcCorrelation, MarketSentiment, FearGreedIndex,
        OpenInterestChange,
        FibProximity,
        ConsecutiveUpCandles, ConsecutiveDownCandles, RecentReturnPercent
    ];

    /// <summary>Anzahl der numerischen Features (fuer ML-Pipeline).</summary>
    public const int FeatureCount = 26;

    /// <summary>Feature-Namen in der gleichen Reihenfolge wie ToFeatureArray().</summary>
    public static readonly string[] FeatureNames =
    [
        "PriceVsEma20", "PriceVsEma50", "PriceVsEma200", "EmaCrossDirection",
        "RsiNormalized", "MacdHistogramNormalized", "StochKNormalized", "StochDNormalized",
        "AtrPercent", "BollingerWidth", "BollingerPosition",
        "AdxNormalized", "HtfTrend",
        "VolumeRatio",
        "FundingRate", "SessionId",
        "BtcReturn24h", "BtcTrend", "BtcCorrelation", "MarketSentiment", "FearGreedIndex",
        "OpenInterestChange",
        "FibProximity",
        "ConsecutiveUpCandles", "ConsecutiveDownCandles", "RecentReturnPercent"
    ];
}
