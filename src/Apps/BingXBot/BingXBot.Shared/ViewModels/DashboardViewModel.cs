using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BingXBot.Core.Models;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für das Dashboard (Übersicht: Kontostand, offene Positionen, PnL).
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    // Account
    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private decimal _availableBalance;
    [ObservableProperty] private decimal _unrealizedPnl;
    [ObservableProperty] private decimal _totalPnl;

    // Bot-Status
    [ObservableProperty] private string _botStatus = "Gestoppt";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaperMode = true;
    [ObservableProperty] private string _tradingModeText = "Paper";

    // Positionen
    public ObservableCollection<PositionDisplayItem> OpenPositions { get; } = new();

    // Equity-Daten für Chart
    public ObservableCollection<EquityPoint> EquityData { get; } = new();

    [RelayCommand]
    private void StartBot()
    {
        IsRunning = true;
        BotStatus = "Läuft";
    }

    [RelayCommand]
    private void PauseBot()
    {
        BotStatus = "Pausiert";
    }

    [RelayCommand]
    private void StopBot()
    {
        IsRunning = false;
        BotStatus = "Gestoppt";
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        IsRunning = false;
        BotStatus = "Emergency Stop";
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsPaperMode = !IsPaperMode;
        TradingModeText = IsPaperMode ? "Paper" : "Live";
    }

    // Demo-Daten für visuellen Test
    public DashboardViewModel()
    {
        Balance = 10000m;
        AvailableBalance = 8500m;
        UnrealizedPnl = 150.50m;
        TotalPnl = 450.75m;

        // Demo Equity-Daten für den Chart
        var rng = new Random(42);
        var equity = 10000m;
        var baseTime = DateTime.UtcNow.AddDays(-30);
        for (int i = 0; i < 100; i++)
        {
            equity += (decimal)(rng.NextDouble() - 0.45) * 50m; // Leichter Aufwärtstrend
            EquityData.Add(new EquityPoint(baseTime.AddHours(i * 8), equity));
        }
    }
}

/// <summary>
/// Anzeige-Modell für eine offene Position im Dashboard.
/// </summary>
public class PositionDisplayItem
{
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = "";
    public decimal EntryPrice { get; set; }
    public decimal MarkPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal Pnl { get; set; }
    public decimal Leverage { get; set; }
    public bool IsProfit => Pnl > 0;
}
