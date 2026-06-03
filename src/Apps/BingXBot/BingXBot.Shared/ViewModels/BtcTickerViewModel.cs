using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Graphics;
using BingXBot.Trading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für den interaktiven Chart mit Multi-Symbol-Support,
/// Timeframe-Wechsel, Indikatoren, Regime-Hintergrund und Trade-Markers.
/// </summary>
public partial class BtcTickerViewModel : ViewModelBase, IDisposable
{
    private readonly IPublicMarketDataClient? _publicClient;
    private readonly BotEventBus _eventBus;

    public ObservableCollection<Candle> BtcCandles { get; } = new();
    [ObservableProperty] private decimal _btcPrice;
    [ObservableProperty] private decimal _btcPriceChange;
    [ObservableProperty] private bool _isBtcLoading = true;
    [ObservableProperty] private string _btcStatusText = "Lade Daten...";
    [ObservableProperty] private bool _isEnabled = true;

    // Chart-Timeframe (wechselbar über UI-Buttons)
    [ObservableProperty] private string _selectedTimeframe = "1h";

    // Multi-Symbol: Welches Symbol im Chart angezeigt wird
    [ObservableProperty] private string _selectedSymbol = "BTC-USDT";

    // Trade-Markers für den Chart (Entry/Exit-Punkte)
    public ObservableCollection<TradeMarker> TradeMarkers { get; } = new();

    // Aktive Position (SL/TP/Entry-Overlay auf dem Chart)
    [ObservableProperty] private ActivePositionOverlay? _activeOverlay;

    // Interaktiver Renderer (hält ChartState: Viewport, Crosshair, Zoom)
    public InteractiveChartRenderer ChartRenderer { get; } = new();

    // Indikator-Daten (werden beim Laden der Candles berechnet)
    [ObservableProperty] private ChartIndicatorData? _indicators;

    private PeriodicTimer? _refreshTimer;
    private bool _disposed;

    public BtcTickerViewModel(
        IPublicMarketDataClient? publicClient,
        BotEventBus eventBus)
    {
        _publicClient = publicClient;
        _eventBus = eventBus;

        // Chart-Daten laden und Auto-Refresh starten
        _ = LoadChartDataAsync();
        _ = StartAutoRefreshAsync();
    }

    [RelayCommand]
    private async Task SwitchTimeframe(string tf)
    {
        SelectedTimeframe = tf;
        ChartRenderer.State.ResetViewport(0); // Viewport zurücksetzen bei TF-Wechsel
        IsBtcLoading = true;
        await LoadChartDataAsync();
    }

    [RelayCommand]
    private async Task SwitchSymbol(string symbol)
    {
        SelectedSymbol = symbol;
        TradeMarkers.Clear();
        ChartRenderer.State.ResetViewport(0);
        IsBtcLoading = true;
        await LoadChartDataAsync();
    }

    /// <summary>Lädt Chart-Daten für das gewählte Symbol + Timeframe von BingX.</summary>
    public async Task LoadChartDataAsync()
    {
        try
        {
            if (_publicClient == null)
            {
                IsBtcLoading = false;
                BtcStatusText = "Keine Verbindung";
                return;
            }

            var (tf, lookbackHours) = SelectedTimeframe switch
            {
                "4h" => (TimeFrame.H4, 400),
                "1D" => (TimeFrame.D1, 2400),
                _ => (TimeFrame.H1, 100)
            };

            var candles = await _publicClient.GetKlinesAsync(
                SelectedSymbol, tf,
                DateTime.UtcNow.AddHours(-lookbackHours), DateTime.UtcNow);

            // Indikatoren berechnen (off-thread)
            var indicators = CalculateIndicators(candles);

            var lastPrice = candles.Count > 0 ? candles[^1].Close : 0m;
            var change = candles.Count > 1 && candles[0].Close != 0
                ? (candles[^1].Close - candles[0].Close) / candles[0].Close * 100m
                : 0m;
            var status = candles.Count > 0
                ? $"{SelectedSymbol} | {candles.Count} Candles ({SelectedTimeframe})"
                : BtcStatusText;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                BtcCandles.Clear();
                foreach (var c in candles)
                    BtcCandles.Add(c);

                Indicators = indicators;
                BtcPrice = lastPrice;
                BtcPriceChange = change;
                BtcStatusText = status;
                IsBtcLoading = false;

                // Viewport auf geladene Daten setzen
                if (ChartRenderer.State.ViewEnd == 0)
                    ChartRenderer.State.ResetViewport(candles.Count);
            });
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Dashboard",
                $"Chart-Daten Ladefehler: {ex.Message}"));
            IsBtcLoading = false;
            BtcStatusText = "Daten nicht verfügbar (offline?)";
        }
    }

    /// <summary>Berechnet Indikatoren für die geladenen Candles (EMA, Bollinger, Supertrend).</summary>
    private static ChartIndicatorData? CalculateIndicators(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 50) return null;

        try
        {
            var ema50 = Engine.Indicators.IndicatorHelper.CalculateEma(candles, 50);
            var ema200 = candles.Count >= 200 ? Engine.Indicators.IndicatorHelper.CalculateEma(candles, 200) : null;
            var bb = Engine.Indicators.IndicatorHelper.CalculateBollinger(candles);
            var st = Engine.Indicators.IndicatorHelper.CalculateSupertrend(candles);

            return new ChartIndicatorData
            {
                Ema50 = ema50,
                Ema200 = ema200,
                BollingerUpper = bb.Item1,  // Upper
                BollingerLower = bb.Item3,  // Lower
                SupertrendLine = st.SupertrendValue,
                SupertrendBullish = st.IsBullish
            };
        }
        catch { return null; }
    }

    /// <summary>Alias für Abwärtskompatibilität.</summary>
    public Task LoadBtcDataAsync() => LoadChartDataAsync();

    /// <summary>Aktualisiert BTC-Preis alle 10s, volle Candles alle 60s.</summary>
    private async Task StartAutoRefreshAsync()
    {
        _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        var tickCount = 0;
        try
        {
            while (await _refreshTimer.WaitForNextTickAsync())
            {
                if (!IsEnabled) continue;

                tickCount++;

                if (tickCount % 6 == 0)
                {
                    // Alle 60s: Volle Candle-Daten laden
                    await LoadChartDataAsync();
                }
                else
                {
                    // Alle 10s: Nur den aktuellen BTC-Preis aktualisieren
                    await UpdateBtcPriceAsync();
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Aktualisiert nur den aktuellen Preis (schnell, ein API-Call).
    /// Nutzt GetKlinesAsync mit wenigen Candles statt GetAllTickersAsync.
    /// </summary>
    private async Task UpdateBtcPriceAsync()
    {
        if (_publicClient == null) return;
        try
        {
            var candles = await _publicClient.GetKlinesAsync(
                SelectedSymbol, TimeFrame.M1,
                DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow);
            if (candles.Count > 0)
            {
                var lastPrice = candles[^1].Close;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => BtcPrice = lastPrice);
            }
        }
        catch { /* Stille Fehlerbehandlung - nächster Tick versucht es erneut */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer?.Dispose();
    }
}
