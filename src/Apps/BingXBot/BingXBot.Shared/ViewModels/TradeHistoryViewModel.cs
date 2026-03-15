using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für Trade-History (abgeschlossene Trades, PnL-Übersicht).
/// </summary>
public partial class TradeHistoryViewModel : ObservableObject
{
    // Filter
    [ObservableProperty] private string _selectedMode = "Alle";
    [ObservableProperty] private string _symbolFilter = "";
    [ObservableProperty] private string _selectedPeriod = "Letzte 7 Tage";

    // Zusammenfassung
    [ObservableProperty] private decimal _totalPnl;
    [ObservableProperty] private decimal _winRate;
    [ObservableProperty] private int _tradeCount;

    public string[] Modes => new[] { "Alle", "Live", "Paper", "Backtest" };
    public string[] Periods => new[] { "Heute", "Letzte 7 Tage", "Letzte 30 Tage", "Alles" };

    public ObservableCollection<TradeHistoryItem> Trades { get; } = new();

    public TradeHistoryViewModel()
    {
        // Demo-Daten laden
        LoadDemoData();
    }

    partial void OnSelectedModeChanged(string value) => ApplyFilter();
    partial void OnSymbolFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedPeriodChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        // Platzhalter - später wird hier aus SQLite gefiltert
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        TradeCount = Trades.Count;
        TotalPnl = Trades.Sum(t => t.Pnl);
        var wins = Trades.Count(t => t.Pnl > 0);
        WinRate = TradeCount > 0 ? (decimal)wins / TradeCount * 100m : 0m;
    }

    [RelayCommand]
    private void ClearFilter()
    {
        SelectedMode = "Alle";
        SymbolFilter = "";
        SelectedPeriod = "Letzte 7 Tage";
    }

    private void LoadDemoData()
    {
        Trades.Add(new("BTC-USDT", "Long", 50000m, 51200m, 0.1m, 120m, 5m, "EMA Cross", "Live", DateTime.UtcNow.AddHours(-2), true));
        Trades.Add(new("ETH-USDT", "Short", 3200m, 3100m, 1m, 100m, 3m, "RSI", "Paper", DateTime.UtcNow.AddHours(-5), true));
        Trades.Add(new("BTC-USDT", "Long", 51000m, 50500m, 0.1m, -50m, 5m, "EMA Cross", "Live", DateTime.UtcNow.AddHours(-8), false));
        Trades.Add(new("SOL-USDT", "Short", 150m, 155m, 10m, -50m, 2m, "Bollinger", "Paper", DateTime.UtcNow.AddDays(-1), false));
        Trades.Add(new("ETH-USDT", "Long", 3050m, 3180m, 2m, 260m, 6m, "MACD", "Live", DateTime.UtcNow.AddDays(-2), true));
        UpdateSummary();
    }
}

/// <summary>
/// Einzelner Trade-Eintrag für die Historie.
/// </summary>
public record TradeHistoryItem(
    string Symbol, string Side, decimal EntryPrice, decimal ExitPrice,
    decimal Quantity, decimal Pnl, decimal Fee, string Strategy,
    string Mode, DateTime Time, bool IsWin)
{
    /// <summary>Farbe für Side: Long=Grün, Short=Rot.</summary>
    public string SideColor => Side == "Long" ? "#10B981" : "#EF4444";

    /// <summary>Farbe für P&amp;L: Positiv=Grün, Negativ=Rot, Neutral=Grau.</summary>
    public string PnlColor => Pnl > 0 ? "#10B981" : Pnl < 0 ? "#EF4444" : "#94A3B8";

    /// <summary>Formatierter Zeitstempel.</summary>
    public string TimeText => Time.ToString("dd.MM HH:mm");
}
