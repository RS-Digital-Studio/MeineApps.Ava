using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für Trade-History (abgeschlossene Trades, PnL-Übersicht).
/// Empfängt neue Trades über IBotEventStream und lädt Historie über ITradeHistoryService.
/// </summary>
public partial class TradeHistoryViewModel : ViewModelBase, IDisposable
{
    private readonly IBotEventStream _eventStream;
    private readonly ITradeHistoryService _tradeHistory;
    private readonly List<TradeHistoryItem> _allTrades = new();

    // Filter
    [ObservableProperty] private string _selectedMode = "Alle";
    [ObservableProperty] private string _symbolFilter = "";
    [ObservableProperty] private string _selectedPeriod = "Letzte 7 Tage";

    // Zusammenfassung
    [ObservableProperty] private decimal _totalPnl;
    [ObservableProperty] private decimal _winRate;
    [ObservableProperty] private int _tradeCount;
    [ObservableProperty] private string _totalPnlColor = "#94A3B8";

    public string[] Modes => new[] { "Alle", "Live", "Paper", "Backtest" };
    public string[] Periods => new[] { "Heute", "Letzte 7 Tage", "Letzte 30 Tage", "Alles" };

    [ObservableProperty] private string _emptyStateText = "Noch keine Trades. Starte den Bot oder einen Backtest.";

    public ObservableCollection<TradeHistoryItem> Trades { get; } = new();

    public TradeHistoryViewModel(IBotEventStream eventStream, ITradeHistoryService tradeHistory)
    {
        _eventStream = eventStream;
        _tradeHistory = tradeHistory;
        _eventStream.TradeClosed += OnTradeClosed;
        _eventStream.BacktestCompleted += OnBacktestCompleted;

        UpdateSummary();

        // Bestehende Trades aus Server laden
        _ = LoadTradesAsync();
    }

    private async Task LoadTradesAsync()
    {
        try
        {
            var result = await _tradeHistory.QueryAsync(new TradeQueryDto(Page: 0, PageSize: 500));
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var t in result.Items)
                {
                    _allTrades.Add(ToItem(t));
                }
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Trades laden fehlgeschlagen: {ex.Message}");
        }
    }

    private void OnTradeClosed(TradeDto trade)
    {
        var item = ToItem(trade);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _allTrades.Insert(0, item); // Neueste zuerst
            ApplyFilter();
        });
    }

    private void OnBacktestCompleted(BacktestResultDto result)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Bulk-Insert: InsertRange statt N-facher Insert(0, ...) — O(N) statt O(N²).
            // Reihenfolge: neueste zuerst, also Trades reversen, damit InsertRange(0, ...) korrekt stapelt.
            var newItems = new List<TradeHistoryItem>(result.Trades.Count);
            for (int i = result.Trades.Count - 1; i >= 0; i--)
            {
                var trade = result.Trades[i];
                newItems.Add(new TradeHistoryItem(
                    trade.Symbol,
                    trade.Side.ToString(),
                    trade.EntryPrice,
                    trade.ExitPrice,
                    trade.Quantity,
                    trade.Pnl,
                    0m,
                    result.Request.StrategyName,
                    "Backtest",
                    trade.ExitTimeUtc,
                    trade.Pnl > 0));
            }
            _allTrades.InsertRange(0, newItems);
            ApplyFilter();
        });
    }

    private static TradeHistoryItem ToItem(TradeDto t) =>
        new(
            t.Symbol,
            t.Side.ToString(),
            t.EntryPrice,
            t.ExitPrice,
            t.Quantity,
            t.Pnl,
            t.Fee,
            t.StrategyName ?? "",
            t.Mode.ToString(),
            t.ExitTimeUtc,
            t.Pnl > 0,
            FormatTfBadge(t.NavigatorTimeframe));

    private static string FormatTfBadge(Core.Enums.TimeFrame tf) => tf switch
    {
        Core.Enums.TimeFrame.D1 => "1D",
        Core.Enums.TimeFrame.H4 => "4H",
        Core.Enums.TimeFrame.H1 => "1H",
        Core.Enums.TimeFrame.M5 => "5m",
        Core.Enums.TimeFrame.M15 => "15m",
        Core.Enums.TimeFrame.M30 => "30m",
        _ => ""
    };

    partial void OnSelectedModeChanged(string value) => ApplyFilter();
    partial void OnSymbolFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedPeriodChanged(string value) => ApplyFilter();

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

    private bool PassesFilter(TradeHistoryItem item)
    {
        if (SelectedMode != "Alle" && item.Mode != SelectedMode)
            return false;

        if (!string.IsNullOrWhiteSpace(SymbolFilter) &&
            !item.Symbol.Contains(SymbolFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        var now = DateTime.UtcNow;
        var cutoff = SelectedPeriod switch
        {
            "Heute" => now.Date,
            "Letzte 7 Tage" => now.AddDays(-7),
            "Letzte 30 Tage" => now.AddDays(-30),
            _ => DateTime.MinValue
        };
        if (item.Time < cutoff)
            return false;

        return true;
    }

    private void UpdateSummary()
    {
        TradeCount = Trades.Count;
        TotalPnl = Trades.Count > 0 ? Trades.Sum(t => t.Pnl) : 0m;
        TotalPnlColor = TotalPnl > 0 ? "#10B981" : TotalPnl < 0 ? "#EF4444" : "#94A3B8";
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

    public void Dispose()
    {
        _eventStream.TradeClosed -= OnTradeClosed;
        _eventStream.BacktestCompleted -= OnBacktestCompleted;
    }
}

public record TradeHistoryItem(
    string Symbol, string Side, decimal EntryPrice, decimal ExitPrice,
    decimal Quantity, decimal Pnl, decimal Fee, string Strategy,
    string Mode, DateTime Time, bool IsWin, string TimeframeBadge = "")
{
    public string SideColor => Side is "Buy" or "Long" ? "#10B981" : "#EF4444";
    public string PnlColor => Pnl > 0 ? "#10B981" : Pnl < 0 ? "#EF4444" : "#94A3B8";
    public string TimeText => Time.ToString("dd.MM HH:mm");
}
