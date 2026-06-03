using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

/// <summary>
/// Marktkontext für die SK-Strategy-Evaluation (Multi-TF Standalone, 15.04.2026).
/// Jede TF (D1/H4/H1/M5) wird eigenständig evaluiert — die Strategie bekommt per
/// <see cref="NavigatorTimeframe"/> mitgeteilt welcher TF gerade geprüft wird und greift
/// auf die passenden Kerzen + Filter-TF zu.
/// </summary>
public record MarketContext(
    string Symbol,
    /// <summary>Navigator-Kerzen für die aktuell geprüfte TF (D1/H4/H1/M5).</summary>
    IReadOnlyList<Candle> Candles,
    Ticker CurrentTicker,
    IReadOnlyList<Position> OpenPositions,
    AccountInfo Account,
    /// <summary>Filter-Kerzen (nächst tiefere TF relativ zum Navigator — z.B. H1 für H4-Navigator).</summary>
    IReadOnlyList<Candle>? FilterTimeframeCandles = null,
    MarketCategory Category = MarketCategory.Crypto,
    /// <summary>Daily-Candles für übergeordneten Fahrplan (BLASH, Daily-GKLs, ATH/ATL-Position).</summary>
    IReadOnlyList<Candle>? DailyCandles = null,
    /// <summary>Weekly-Candles für Top-Level Fahrplan (Weekly-GKLs).</summary>
    IReadOnlyList<Candle>? WeeklyCandles = null,
    /// <summary>Navigator-TF, auf der die Strategie gerade evaluiert wird (D1/H4/H1/M5).</summary>
    TimeFrame NavigatorTimeframe = TimeFrame.H4,
    /// <summary>ScannerSettings für alle Scoring/Filter-Erweiterungen. Null = Defaults.</summary>
    ScannerSettings? ScannerSettings = null,
    /// <summary>RiskSettings für Strategy-Zugriff auf PipScalingByTf etc. Null = Defaults.</summary>
    RiskSettings? RiskSettings = null,
    /// <summary>SK-Plan 4.10: Aktuelle Funding-Rate in Prozent (0.05 = 0.05%). 0 = unbekannt/neutral.</summary>
    decimal FundingRatePercent = 0m,
    /// <summary>SK-Plan 5.8: BTC 24h-Change (für Dominance-Proxy). Null = nicht verfügbar.</summary>
    decimal? Btc24hChangePercent = null,
    /// <summary>SK-Plan 5.8: Alt-Median 24h-Change (für Dominance-Proxy). Null = nicht verfügbar.</summary>
    decimal? AltMedian24hChangePercent = null,
    /// <summary>SK-Plan 5.6: Symbol-Qualitäts-Score (WinRate 0-100). Null = unbekannt/neu.</summary>
    decimal? SymbolQualityWinRate = null,
    /// <summary>SK-Plan 5.6: Anzahl historischer Trades für Qualitäts-Bewertung.</summary>
    int SymbolQualityTradeCount = 0,
    /// <summary>SK-Plan 4.9: Aktueller Zeitstempel (Live: UtcNow, Backtest: Candle-Zeit).
    /// Null = Live-Modus, Scorer fällt auf DateTime.UtcNow zurück.</summary>
    DateTime? NowUtc = null);
