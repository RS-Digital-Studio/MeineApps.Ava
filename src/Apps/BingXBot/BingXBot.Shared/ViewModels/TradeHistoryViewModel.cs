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

    [ObservableProperty] private string _emptyStateText = "Noch keine Trades. Starte den Bot oder einen Backtest.";

    public ObservableCollection<TradeHistoryItem> Trades { get; } = new();

    public TradeHistoryViewModel()
    {
        UpdateSummary();
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
        TotalPnl = Trades.Count > 0 ? Trades.Sum(t => t.Pnl) : 0m;
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
