using BingXBot.Core.Models;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für Trade-History (abgeschlossene Trades, PnL-Übersicht).
/// Empfängt echte Trades über den BotEventBus (von Bot und Backtest).
/// </summary>
public partial class TradeHistoryViewModel : ViewModelBase
{
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService? _dbService;
    private readonly List<TradeHistoryItem> _allTrades = new();

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

    public TradeHistoryViewModel(BotEventBus eventBus, BotDatabaseService? dbService = null)
    {
        _eventBus = eventBus;
        _dbService = dbService;
        _eventBus.TradeCompleted += OnTradeCompleted;
        _eventBus.BacktestCompleted += OnBacktestCompleted;

        UpdateSummary();

        // Bestehende Trades aus DB laden
        _ = LoadTradesFromDbAsync();
    }

    /// <summary>
    /// Lädt persisierte Trades aus der SQLite-Datenbank.
    /// </summary>
    private async Task LoadTradesFromDbAsync()
    {
        if (_dbService == null) return;
        try
        {
            var trades = await _dbService.GetTradesAsync();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var t in trades)
                {
                    _allTrades.Add(new TradeHistoryItem(
                        t.Symbol, t.Side.ToString(), t.EntryPrice, t.ExitPrice,
                        t.Quantity, t.Pnl, t.Fee, "", t.Mode.ToString(),
                        t.ExitTime, t.Pnl > 0));
                }
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Trades aus DB laden fehlgeschlagen: {ex.Message}");
        }
    }

    private void OnTradeCompleted(object? sender, CompletedTrade trade)
    {
        var item = new TradeHistoryItem(
            trade.Symbol, trade.Side.ToString(), trade.EntryPrice, trade.ExitPrice,
            trade.Quantity, trade.Pnl, trade.Fee,
            trade.Mode == Core.Enums.TradingMode.Paper ? "Paper-Bot" : "Live-Bot",
            trade.Mode.ToString(),
            trade.ExitTime, trade.Pnl > 0);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _allTrades.Insert(0, item); // Neueste zuerst
            ApplyFilter();
        });
    }

    private void OnBacktestCompleted(object? sender, BacktestCompletedArgs args)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var trade in args.Trades)
            {
                _allTrades.Insert(0, new TradeHistoryItem(
                    trade.Symbol, trade.Side.ToString(), trade.EntryPrice, trade.ExitPrice,
                    trade.Quantity, trade.Pnl, trade.Fee, args.StrategyName, "Backtest",
                    trade.ExitTime, trade.Pnl > 0));
            }
            ApplyFilter();
        });
    }

    partial void OnSelectedModeChanged(string value) => ApplyFilter();
    partial void OnSymbolFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedPeriodChanged(string value) => ApplyFilter();

    /// <summary>
    /// Baut die angezeigte Trade-Liste neu auf basierend auf aktuellen Filtern.
    /// </summary>
    private void ApplyFilter()
    {
        Trades.Clear();
        foreach (var trade in _allTrades)
        {
            if (!PassesFilter(trade)) continue;
            Trades.Add(trade);
        }
        UpdateSummary();
    }

    /// <summary>
    /// Prüft ob ein Trade die aktuellen Filter-Kriterien erfüllt.
    /// </summary>
    private bool PassesFilter(TradeHistoryItem item)
    {
        // Modus-Filter
        if (SelectedMode != "Alle" && item.Mode != SelectedMode)
            return false;

        // Symbol-Filter (Freitext-Suche)
        if (!string.IsNullOrWhiteSpace(SymbolFilter) &&
            !item.Symbol.Contains(SymbolFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        // Zeitraum-Filter
        var now = DateTime.UtcNow;
        var cutoff = SelectedPeriod switch
        {
            "Heute" => now.Date,
            "Letzte 7 Tage" => now.AddDays(-7),
            "Letzte 30 Tage" => now.AddDays(-30),
            _ => DateTime.MinValue // "Alles"
        };
        if (item.Time < cutoff)
            return false;

        return true;
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
    /// <summary>Farbe für Side: Buy/Long=Grün, Sell/Short=Rot.</summary>
    public string SideColor => Side is "Buy" or "Long" ? "#10B981" : "#EF4444";

    /// <summary>Farbe für P&amp;L: Positiv=Grün, Negativ=Rot, Neutral=Grau.</summary>
    public string PnlColor => Pnl > 0 ? "#10B981" : Pnl < 0 ? "#EF4444" : "#94A3B8";

    /// <summary>Formatierter Zeitstempel.</summary>
    public string TimeText => Time.ToString("dd.MM HH:mm");
}
