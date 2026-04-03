using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für den BTC-USDT Live-Ticker und Chart.
/// Vollständig unabhängig - benötigt nur den öffentlichen Marktdaten-Client.
/// Aktualisiert BTC-Preis alle 10s, volle Candle-Daten alle 60s.
/// </summary>
public partial class BtcTickerViewModel : ViewModelBase, IDisposable
{
    private readonly IPublicMarketDataClient? _publicClient;
    private readonly BotEventBus _eventBus;

    public ObservableCollection<Candle> BtcCandles { get; } = new();
    [ObservableProperty] private decimal _btcPrice;
    [ObservableProperty] private decimal _btcPriceChange;
    [ObservableProperty] private bool _isBtcLoading = true;
    [ObservableProperty] private string _btcStatusText = "Lade BTC-Daten...";
    [ObservableProperty] private bool _isEnabled = true;

    private PeriodicTimer? _refreshTimer;
    private bool _disposed;

    public BtcTickerViewModel(
        IPublicMarketDataClient? publicClient,
        BotEventBus eventBus)
    {
        _publicClient = publicClient;
        _eventBus = eventBus;

        // BTC-Daten laden und Auto-Refresh starten
        _ = LoadBtcDataAsync();
        _ = StartAutoRefreshAsync();
    }

    /// <summary>Lädt BTC-Klinendaten von BingX (öffentlich, kein API-Key nötig).</summary>
    public async Task LoadBtcDataAsync()
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

            // Preis/Status zuerst berechnen (off-thread), dann ein einzelner UI-Update
            var lastPrice = candles.Count > 0 ? candles[^1].Close : 0m;
            var change = candles.Count > 1 && candles[0].Close != 0
                ? (candles[^1].Close - candles[0].Close) / candles[0].Close * 100m
                : 0m;
            var status = candles.Count > 0 ? $"BTC-USDT | {candles.Count} Candles (1h)" : BtcStatusText;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Batch-Update: CollectionChanged erst nach komplettem Befüllen feuern
                // indem wir Replace statt Clear+Add nutzen, wenn Anzahl gleich
                BtcCandles.Clear();
                foreach (var c in candles)
                    BtcCandles.Add(c);

                BtcPrice = lastPrice;
                BtcPriceChange = change;
                BtcStatusText = status;
                IsBtcLoading = false;
            });
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Dashboard",
                $"BTC-Daten Ladefehler: {ex.Message}"));
            IsBtcLoading = false;
            BtcStatusText = "Daten nicht verfuegbar (offline?)";
        }
    }

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
                    await LoadBtcDataAsync();
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

    /// <summary>Aktualisiert nur den BTC-Preis (schnell, ein API-Call).</summary>
    private async Task UpdateBtcPriceAsync()
    {
        if (_publicClient == null) return;
        try
        {
            var tickers = await _publicClient.GetAllTickersAsync();
            var btc = tickers.FirstOrDefault(t => t.Symbol == "BTC-USDT");
            if (btc != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    BtcPrice = btc.LastPrice;
                    BtcPriceChange = btc.PriceChangePercent24h;
                });
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
