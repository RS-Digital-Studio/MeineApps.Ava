using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für den Markt-Scanner (Filterkriterien, Scan-Ergebnisse).
/// Nutzt BingXPublicClient für echte Ticker-Daten, IMarketScanner für Signal-Analyse.
/// </summary>
public partial class ScannerViewModel : ObservableObject
{
    private readonly ScannerSettings _scannerSettings;
    private readonly IMarketScanner? _marketScanner;
    private readonly IPublicMarketDataClient? _publicClient;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private decimal _minVolume;
    [ObservableProperty] private decimal _minPriceChange;
    [ObservableProperty] private string _selectedTimeFrame = "H1";
    [ObservableProperty] private string _selectedScanMode = "Momentum";
    [ObservableProperty] private int _maxResults;
    [ObservableProperty] private string _blacklistText = "";
    [ObservableProperty] private string _whitelistText = "";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _scanStatus = "Drücke 'Scannen' um den Markt zu durchsuchen";
    [ObservableProperty] private string _scanModeDescription = "Sucht Paare mit starker Preisbewegung";

    public string[] TimeFrames => new[] { "M5", "M15", "M30", "H1", "H4", "D1" };
    public string[] ScanModes => new[] { "Momentum", "Reversal", "Breakout", "VolumeSurge" };

    public ObservableCollection<ScanResultItem> Results { get; } = new();

    public ScannerViewModel(ScannerSettings scannerSettings, IMarketScanner? marketScanner = null, IPublicMarketDataClient? publicClient = null)
    {
        _scannerSettings = scannerSettings;
        _marketScanner = marketScanner;
        _publicClient = publicClient;
        LoadFromSettings();
        UpdateScanModeDescription();
    }

    /// <summary>
    /// Aktualisiert die Beschreibung des Scan-Modus bei Auswahländerung.
    /// </summary>
    partial void OnSelectedScanModeChanged(string value) => UpdateScanModeDescription();

    private void UpdateScanModeDescription()
    {
        ScanModeDescription = SelectedScanMode switch
        {
            "Momentum" => "Sucht Paare mit starker Preisbewegung",
            "Reversal" => "Sucht überkaufte/überverkaufte Paare",
            "Breakout" => "Sucht Paare die aus einer Range ausbrechen",
            "VolumeSurge" => "Sucht Paare mit ungewöhnlich hohem Volumen",
            _ => ""
        };
    }

    /// <summary>
    /// Lädt die aktuellen Werte aus den ScannerSettings.
    /// </summary>
    private void LoadFromSettings()
    {
        MinVolume = _scannerSettings.MinVolume24h;
        MinPriceChange = _scannerSettings.MinPriceChange;
        SelectedTimeFrame = _scannerSettings.ScanTimeFrame.ToString();
        SelectedScanMode = _scannerSettings.Mode.ToString();
        MaxResults = _scannerSettings.MaxResults;
        BlacklistText = string.Join(", ", _scannerSettings.Blacklist);
        WhitelistText = string.Join(", ", _scannerSettings.Whitelist);
    }

    /// <summary>
    /// Schreibt die aktuellen UI-Werte zurück in die ScannerSettings.
    /// </summary>
    private ScannerSettings BuildCurrentSettings()
    {
        // UI-Werte ins Settings-Objekt übertragen
        _scannerSettings.MinVolume24h = MinVolume;
        _scannerSettings.MinPriceChange = MinPriceChange;
        _scannerSettings.MaxResults = MaxResults;

        if (Enum.TryParse<TimeFrame>(SelectedTimeFrame, out var tf))
            _scannerSettings.ScanTimeFrame = tf;

        if (Enum.TryParse<ScanMode>(SelectedScanMode, out var mode))
            _scannerSettings.Mode = mode;

        // Blacklist/Whitelist aus kommasepariertem Text parsen
        _scannerSettings.Blacklist = ParseSymbolList(BlacklistText);
        _scannerSettings.Whitelist = ParseSymbolList(WhitelistText);

        return _scannerSettings;
    }

    private static List<string> ParseSymbolList(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToList();
    }

    [RelayCommand]
    private async Task Scan()
    {
        IsScanning = true;
        ScanStatus = "Scanne BingX Perpetual-Paare...";
        Results.Clear();

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var settings = BuildCurrentSettings();

            if (_marketScanner != null)
            {
                // Echten MarketScanner verwenden (wenn Exchange-Client verfügbar)
                await foreach (var result in _marketScanner.ScanAsync(settings, _cts.Token))
                {
                    var volume = result.Indicators.GetValueOrDefault("Volume24h", 0m);
                    var priceChange = result.Indicators.GetValueOrDefault("PriceChange", 0m);
                    Results.Add(new ScanResultItem(result.Symbol, result.Score, result.SetupType, volume, priceChange));
                }
            }
            else if (_publicClient != null)
            {
                // Echte Ticker-Daten von BingX (kein API-Key nötig)
                ScanStatus = "Lade Ticker von BingX...";
                var tickers = await _publicClient.GetAllTickersAsync(_cts.Token);

                // Blacklist/Whitelist filtern
                var filtered = tickers.AsEnumerable();

                if (settings.Whitelist.Count > 0)
                    filtered = filtered.Where(t => settings.Whitelist.Any(w =>
                        t.Symbol.Contains(w, StringComparison.OrdinalIgnoreCase)));

                if (settings.Blacklist.Count > 0)
                    filtered = filtered.Where(t => !settings.Blacklist.Any(b =>
                        t.Symbol.Contains(b, StringComparison.OrdinalIgnoreCase)));

                // Volumen-Filter
                if (settings.MinVolume24h > 0)
                    filtered = filtered.Where(t => t.Volume24h >= settings.MinVolume24h);

                // Preisänderungs-Filter
                if (settings.MinPriceChange > 0)
                    filtered = filtered.Where(t => Math.Abs(t.PriceChangePercent24h) >= settings.MinPriceChange);

                // Score berechnen (einfache Heuristik basierend auf Volumen + Preisänderung)
                var scored = filtered
                    .Select(t => new ScanResultItem(
                        t.Symbol,
                        CalculateScore(t.Volume24h, t.PriceChangePercent24h, SelectedScanMode),
                        SelectedScanMode,
                        t.Volume24h,
                        t.PriceChangePercent24h))
                    .OrderByDescending(r => r.Score)
                    .Take(settings.MaxResults > 0 ? settings.MaxResults : 20);

                foreach (var item in scored)
                    Results.Add(item);
            }
            else
            {
                // Demo-Modus: Simulierte Ergebnisse (kein Client konfiguriert)
                await Task.Delay(500, _cts.Token);

                Results.Add(new("BTC-USDT", 95.5m, SelectedScanMode, 50_000_000m, 5.2m));
                Results.Add(new("ETH-USDT", 82.3m, SelectedScanMode, 30_000_000m, 3.8m));
                Results.Add(new("SOL-USDT", 71.0m, SelectedScanMode, 15_000_000m, 8.1m));
            }

            ScanStatus = Results.Count > 0
                ? $"{Results.Count} Paare gefunden die den Kriterien entsprechen"
                : "Keine Paare gefunden - versuche die Filter anzupassen";
        }
        catch (OperationCanceledException)
        {
            ScanStatus = "Abgebrochen";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Berechnet einen Score für ein Ticker-Ergebnis basierend auf Volumen und Preisänderung.
    /// </summary>
    private static decimal CalculateScore(decimal volume24h, decimal priceChange, string scanMode)
    {
        // Normalisierung: Volumen-Score (0-50) + Preisänderungs-Score (0-50)
        var volumeScore = Math.Min(volume24h / 1_000_000m, 50m);
        var changeScore = scanMode switch
        {
            "Momentum" => Math.Min(Math.Abs(priceChange) * 5m, 50m),
            "Reversal" => priceChange < -3m ? Math.Min(Math.Abs(priceChange) * 5m, 50m) : 0m,
            "Breakout" => Math.Abs(priceChange) > 5m ? Math.Min(Math.Abs(priceChange) * 4m, 50m) : 0m,
            "VolumeSurge" => Math.Min(volume24h / 500_000m, 50m),
            _ => Math.Min(Math.Abs(priceChange) * 5m, 50m)
        };

        return Math.Round(volumeScore + changeScore, 1);
    }

    [RelayCommand]
    private void StopScan()
    {
        _cts?.Cancel();
    }
}

/// <summary>
/// Ein einzelnes Scanner-Ergebnis.
/// </summary>
public record ScanResultItem(string Symbol, decimal Score, string SetupType, decimal Volume24h, decimal PriceChange);
