using BingXBot.Backtest.Simulation;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Exchange;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBot.Trading.CrossSectional;

/// <summary>
/// Lifecycle-Orchestrator fuer den Cross-Sectional-Modus (analog <see cref="LiveTradingManager"/>/<see cref="PaperTradingService"/>).
/// Paper: <see cref="SimulatedExchange"/> mit Live-Marktdaten (Preise/Funding/Trades via <see cref="PaperHooks"/>).
/// Live: <see cref="BingXRestClient"/> mit ZWINGENDEM Hedge-Modus (long+short gleichzeitig). Beide Pfade teilen
/// denselben <see cref="CrossSectionalTradingService"/>.
/// </summary>
public sealed class CrossSectionalManager : IDisposable
{
    private readonly ISecureStorageService? _secureStorage;
    private readonly IPublicMarketDataClient _marketData;
    private readonly RiskSettings _risk;
    private readonly CrossSectionalSettings _cfg;
    private readonly BotEventBus _eventBus;
    private readonly BotSettings _botSettings;
    private readonly string _stateFilePath;

    private CrossSectionalTradingService? _service;
    private SimulatedExchange? _paperExchange;
    private BingXRestClient? _restClient;
    private HttpClient? _httpClient;
    private RateLimiter? _rateLimiter;
    private int _lastDrainIndex;

    public bool IsRunning => _service?.IsRunning ?? false;
    public bool IsConnected => _restClient != null;
    public SimulatedExchange? PaperExchange => _paperExchange;
    public BingXRestClient? RestClient => _restClient;
    public IReadOnlyDictionary<string, Side>? CurrentBasket => _service?.CurrentBasket;

    public CrossSectionalManager(
        ISecureStorageService? secureStorage,
        IPublicMarketDataClient marketData,
        RiskSettings risk,
        CrossSectionalSettings cfg,
        BotEventBus eventBus,
        BotSettings botSettings,
        string? stateFilePath = null)
    {
        _secureStorage = secureStorage;
        _marketData = marketData;
        _risk = risk;
        _cfg = cfg;
        _eventBus = eventBus;
        _botSettings = botSettings;
        _stateFilePath = stateFilePath ?? Path.Combine(AppContext.BaseDirectory, "xsec-state.json");
    }

    /// <summary>Startet den Cross-Sectional-Modus. <paramref name="mode"/> = Paper oder Live.</summary>
    public async Task StartAsync(TradingMode mode, decimal? initialBalance = null)
    {
        if (IsRunning) return;

        IExchangeClient execution;
        PaperHooks? paper = null;

        if (mode == TradingMode.Paper)
        {
            var balance = initialBalance ?? _botSettings.PaperInitialBalance;
            _paperExchange?.Dispose();
            var fundingRate = _botSettings.SimulatedFundingRatePercent / 100m;
            _paperExchange = new SimulatedExchange(new BacktestSettings
            {
                InitialBalance = balance,
                SimulatedFundingRatePercent = _botSettings.SimulatedFundingRatePercent,
            });
            _paperExchange.SetFundingRate(fundingRate);
            _lastDrainIndex = 0;
            var sim = _paperExchange;
            paper = new PaperHooks(
                SetPrice: (s, p) => sim.SetCurrentPrice(s, p),
                ApplyFunding: r => sim.ApplyFundingRate(r),
                DrainNewTrades: () =>
                {
                    var all = sim.GetCompletedTrades();
                    var fresh = all.Skip(_lastDrainIndex).Select(t => t with { Mode = TradingMode.Paper }).ToList();
                    _lastDrainIndex = all.Count;
                    return fresh;
                },
                FundingRate: fundingRate);
            execution = sim;
            Log(LogLevel.Info, "Engine", $"Cross-Sectional PAPER (Startkapital {balance:N0} USDT).");
        }
        else
        {
            execution = await ConnectLiveAsync().ConfigureAwait(false);
        }

        _service = new CrossSectionalTradingService(
            execution, _marketData, _risk, _cfg, _eventBus, NullLogger.Instance, _stateFilePath, paper);
        await _service.StartAsync().ConfigureAwait(false);
    }

    private async Task<IExchangeClient> ConnectLiveAsync()
    {
        if (_secureStorage == null || !_secureStorage.HasCredentials)
            throw new InvalidOperationException("API-Keys nicht konfiguriert.");
        var creds = await _secureStorage.LoadCredentialsAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("API-Keys konnten nicht entschluesselt werden.");

        _httpClient ??= new HttpClient();
        _rateLimiter ??= new RateLimiter();
        _restClient = new BingXRestClient(creds.ApiKey, creds.ApiSecret, _httpClient, _rateLimiter,
            NullLogger<BingXRestClient>.Instance);

        await _restClient.SyncServerTimeAsync().ConfigureAwait(false);
        await _restClient.InitializeSymbolInfoAsync().ConfigureAwait(false);

        // Hedge-Modus ist ZWINGEND (Cross-Sectional haelt long UND short gleichzeitig).
        var isHedge = await _restClient.IsHedgeModeAsync().ConfigureAwait(false);
        if (!isHedge)
        {
            var positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);
            if (positions.Count > 0)
                throw new InvalidOperationException(
                    $"Cross-Sectional braucht Hedge-Modus, BingX steht aber auf One-Way mit {positions.Count} offenen Position(en). "
                    + "Erst alle Positionen schliessen, dann neu starten.");
            var ok = await _restClient.SetHedgeModeAsync(true).ConfigureAwait(false);
            if (!ok)
                throw new InvalidOperationException("Hedge-Modus konnte nicht aktiviert werden (BingX-API). Cross-Sectional braucht ihn (long+short).");
            Log(LogLevel.Info, "Engine", "Position-Modus auf Hedge (Zwei-Wege) umgestellt fuer Cross-Sectional.");
        }

        try
        {
            var (taker, maker) = await _restClient.GetCommissionRateAsync().ConfigureAwait(false);
            Log(LogLevel.Info, "Account", $"Fees geladen: Taker={taker:P3}, Maker={maker:P3}.");
        }
        catch (Exception ex) { Log(LogLevel.Warning, "Account", $"Commission-Rates nicht ladbar ({ex.Message}) — Standard-Fallback."); }

        var acc = await _restClient.GetAccountInfoAsync().ConfigureAwait(false);
        Log(LogLevel.Info, "Engine", $"Cross-Sectional LIVE verbunden. Balance {acc.Balance:N2} USDT, Hedge aktiv.");
        return _restClient;
    }

    public async Task StopAsync()
    {
        if (_service != null) await _service.StopAsync().ConfigureAwait(false);
    }

    public async Task EmergencyStopAsync()
    {
        if (_service != null) await _service.EmergencyStopAsync().ConfigureAwait(false);
    }

    private void Log(LogLevel level, string category, string message) =>
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, level, category, message));

    public void Dispose()
    {
        _service?.Dispose();
        _paperExchange?.Dispose();
        _httpClient?.Dispose();
    }
}
