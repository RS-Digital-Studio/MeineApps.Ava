using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BingXBot.ViewModels;

/// <summary>
/// Display-Item für eine offene Position im Dashboard.
/// ObservableObject damit editierbare SL/TP/Trailing-Felder korrekt binden.
/// </summary>
public partial class PositionDisplayItem : ObservableObject
{
    // Basis-Daten (werden bei jedem Account-Update gesetzt)
    [ObservableProperty] private string _symbol = "";
    [ObservableProperty] private Side _side;
    [ObservableProperty] private decimal _entryPrice;
    [ObservableProperty] private decimal _markPrice;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _pnl;
    [ObservableProperty] private decimal _leverage;

    // SL/TP/Trailing (editierbar vom User)
    [ObservableProperty] private decimal? _stopLoss;
    [ObservableProperty] private decimal? _takeProfit;
    [ObservableProperty] private decimal? _trailingStop; // In Prozent

    // Erweiterte Infos (Risiko)
    [ObservableProperty] private string _holdTimeText = "";
    [ObservableProperty] private decimal _liquidationPrice;
    [ObservableProperty] private bool _isSelected;

    /// <summary>Multi-TF Standalone: Navigator-TF als Badge-Text ("D1" / "4H" / "1H" / "15m").</summary>
    [ObservableProperty] private string _timeframeBadge = "";

    // Berechnete Properties
    public bool IsProfit => Pnl > 0;
    public string PnlColor => Pnl >= 0 ? "#10B981" : "#EF4444";
    public string SideText => Side.ToString();
    public string SideColor => Side == Side.Buy ? "#10B981" : "#EF4444";
    public string PnlText => $"{Pnl:+0.00;-0.00}";
    public decimal PnlPercent => EntryPrice > 0 && Quantity > 0
        ? (MarkPrice - EntryPrice) / EntryPrice * 100m * (Side == Side.Buy ? 1 : -1)
        : 0m;
    public string PnlPercentText => $"{PnlPercent:+0.00;-0.00}%";

    /// <summary>Key im ExitState-Dictionary ({Symbol}_{Side}).</summary>
    public string PositionKey => $"{Symbol}_{Side}";

    /// <summary>Close-Action wird vom DashboardViewModel injiziert.</summary>
    public Func<PositionDisplayItem, Task>? CloseRequested { get; set; }

    [RelayCommand]
    private async Task RequestClose()
    {
        if (CloseRequested != null)
            await CloseRequested(this);
    }

    // Berechnete Properties bei Pnl/Side/Price-Aenderungen benachrichtigen
    partial void OnPnlChanged(decimal value)
    {
        OnPropertyChanged(nameof(IsProfit));
        OnPropertyChanged(nameof(PnlColor));
        OnPropertyChanged(nameof(PnlText));
        OnPropertyChanged(nameof(PnlPercent));
        OnPropertyChanged(nameof(PnlPercentText));
    }

    partial void OnSideChanged(Side value)
    {
        OnPropertyChanged(nameof(SideText));
        OnPropertyChanged(nameof(SideColor));
        OnPropertyChanged(nameof(PositionKey));
        OnPropertyChanged(nameof(PnlPercent));
        OnPropertyChanged(nameof(PnlPercentText));
    }

    partial void OnMarkPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(PnlPercent));
        OnPropertyChanged(nameof(PnlPercentText));
    }

    partial void OnEntryPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(PnlPercent));
        OnPropertyChanged(nameof(PnlPercentText));
    }

    partial void OnSymbolChanged(string value)
    {
        OnPropertyChanged(nameof(PositionKey));
    }
}
