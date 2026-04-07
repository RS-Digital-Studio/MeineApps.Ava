namespace BingXBot.Core.Enums;

/// <summary>
/// Trading-Modus-Voreinstellung: Bestimmt Timeframe, Indikatoren, Risk und Session-Filter.
/// </summary>
public enum TradingModePreset
{
    /// <summary>M15-Scalping: Schnelle Trades, enge SL/TP, hohe Frequenz, nur in liquiden Sessions.</summary>
    Scalping,
    /// <summary>H1-Day-Trading: Intraday-Trades, mittlere SL/TP, Session-basiert.</summary>
    DayTrading,
    /// <summary>H4-Swing-Trading: Mehrtägige Trades, weite SL/TP, alle Sessions. (Standard)</summary>
    Swing,
    /// <summary>Manuelle Konfiguration: Alle Parameter frei editierbar.</summary>
    Custom
}
