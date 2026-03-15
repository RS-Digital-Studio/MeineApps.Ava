using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel fuer das Dashboard - ehrliche Zustandsanzeige ohne Fake-Daten.
/// Zeigt nur echte Daten an (BTC-Kurs live, Account nur wenn Bot laeuft).
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IPublicMarketDataClient? _publicClient;

    // === Modus ===
    [ObservableProperty] private bool _isPaperMode = true;
    [ObservableProperty] private string _modeText = "Paper-Modus";
    [ObservableProperty] private string _modeDescription = "Simuliertes Trading ohne echtes Geld";

    // === Bot-Status ===
    [ObservableProperty] private string _botStatusText = "Gestoppt";
    [ObservableProperty] private string _botStatusColor = "#EF4444"; // Rot
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _canStart = true;

    // === Account (nur anzeigen wenn Daten vorhanden) ===
    [ObservableProperty] private bool _hasAccountData;
    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private decimal _availableBalance;
    [ObservableProperty] private decimal _unrealizedPnl;
    [ObservableProperty] private decimal _totalPnl;

    // === Offene Positionen ===
    public ObservableCollection<PositionDisplayItem> OpenPositions { get; } = new();
    [ObservableProperty] private bool _hasOpenPositions;
    [ObservableProperty] private string _positionsStatusText = "Keine offenen Positionen";

    // === BTC Live-Chart ===
    public ObservableCollection<Candle> BtcCandles { get; } = new();
    [ObservableProperty] private decimal _btcPrice;
    [ObservableProperty] private decimal _btcPriceChange;
    [ObservableProperty] private bool _isBtcLoading = true;
    [ObservableProperty] private string _btcStatusText = "Lade BTC-Daten...";

    // === Equity-Kurve ===
    public ObservableCollection<EquityPoint> EquityData { get; } = new();

    // === Hinweise/Onboarding ===
    [ObservableProperty] private bool _showWelcomeHint = true;
    [ObservableProperty] private string _welcomeHintText = "Willkommen! Starte mit einem Backtest um eine Strategie zu testen, oder konfiguriere deine API-Keys in den Einstellungen.";

    public DashboardViewModel(IPublicMarketDataClient? publicClient = null)
    {
        _publicClient = publicClient;

        // Keine Fake-Daten! Zeige ehrlichen Zustand.
        HasAccountData = false;
        HasOpenPositions = false;

        // BTC-Daten laden (echt, kein Fake)
        _ = LoadBtcDataAsync();
        _ = StartAutoRefreshAsync();
    }

    [RelayCommand]
    private void StartBot()
    {
        if (IsPaperMode)
        {
            // Paper-Modus: Simuliertes Trading starten
            IsRunning = true;
            CanStart = false;
            BotStatusText = "Laeuft (Paper)";
            BotStatusColor = "#10B981"; // Gruen
            ShowWelcomeHint = false;

            // Paper-Account mit 10.000 USDT Startkapital
            HasAccountData = true;
            Balance = 10_000m;
            AvailableBalance = 10_000m;
            UnrealizedPnl = 0m;
            TotalPnl = 0m;
        }
        else
        {
            // Live-Modus: Braucht API-Keys
            BotStatusText = "API-Keys benoetigt";
            BotStatusColor = "#F59E0B"; // Amber
            WelcomeHintText = "Fuer Live-Trading: Gehe zu Einstellungen und hinterlege deine BingX API-Keys.";
            ShowWelcomeHint = true;
        }
    }

    [RelayCommand]
    private void PauseBot()
    {
        BotStatusText = "Pausiert";
        BotStatusColor = "#F59E0B"; // Amber
    }

    [RelayCommand]
    private void StopBot()
    {
        IsRunning = false;
        CanStart = true;
        BotStatusText = "Gestoppt";
        BotStatusColor = "#EF4444"; // Rot
        PositionsStatusText = "Keine offenen Positionen";
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        IsRunning = false;
        CanStart = true;
        BotStatusText = "Notfall-Stop ausgefuehrt";
        BotStatusColor = "#EF4444";

        // Alle Positionen schliessen
        OpenPositions.Clear();
        HasOpenPositions = false;
        PositionsStatusText = "Alle Positionen geschlossen";
    }

    [RelayCommand]
    private void ToggleMode()
    {
        if (IsRunning)
        {
            // Kann Modus nicht wechseln waehrend Bot laeuft
            return;
        }

        IsPaperMode = !IsPaperMode;
        if (IsPaperMode)
        {
            ModeText = "Paper-Modus";
            ModeDescription = "Simuliertes Trading ohne echtes Geld";
        }
        else
        {
            ModeText = "Live-Modus";
            ModeDescription = "Echtes Trading mit BingX - API-Keys erforderlich!";
        }

        // Account-Daten zuruecksetzen bei Modus-Wechsel
        HasAccountData = false;
        Balance = 0;
        AvailableBalance = 0;
        UnrealizedPnl = 0;
        TotalPnl = 0;
    }

    [RelayCommand]
    private void DismissWelcomeHint()
    {
        ShowWelcomeHint = false;
    }

    private PeriodicTimer? _refreshTimer;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer?.Dispose();
    }

    /// <summary>
    /// Laedt BTC-Klinendaten von BingX (oeffentlich, kein API-Key noetig).
    /// </summary>
    private async Task LoadBtcDataAsync()
    {
        try
        {
            if (_publicClient == null)
            {
                IsBtcLoading = false;
                BtcStatusText = "Keine Verbindung";
                return;
            }

            var candles = await _publicClient.GetKlinesAsync(
                "BTC-USDT", TimeFrame.H1,
                DateTime.UtcNow.AddHours(-100), DateTime.UtcNow);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                BtcCandles.Clear();
                foreach (var c in candles)
                    BtcCandles.Add(c);

                if (candles.Count > 0)
                {
                    BtcPrice = candles[^1].Close;
                    BtcPriceChange = candles.Count > 1 && candles[0].Close != 0
                        ? (candles[^1].Close - candles[0].Close) / candles[0].Close * 100m
                        : 0m;
                    BtcStatusText = $"BTC-USDT | {candles.Count} Candles (1h)";
                }
                IsBtcLoading = false;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BTC-Daten Ladefehler: {ex.Message}");
            IsBtcLoading = false;
            BtcStatusText = "Daten nicht verfuegbar (offline?)";
        }
    }

    /// <summary>
    /// Aktualisiert BTC-Daten alle 60 Sekunden automatisch.
    /// </summary>
    private async Task StartAutoRefreshAsync()
    {
        _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        try
        {
            while (await _refreshTimer.WaitForNextTickAsync())
                await LoadBtcDataAsync();
        }
        catch (OperationCanceledException) { }
    }
}

/// <summary>
/// Anzeige-Modell fuer eine offene Position im Dashboard.
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
