using System.Text.Json;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Portfolio;
using Microsoft.Extensions.Logging;
using LogLevel = BingXBot.Core.Enums.LogLevel;

namespace BingXBot.Trading.CrossSectional;

/// <summary>
/// Paper-/Live-Hooks fuer den Cross-Sectional-Service. Bei Live alle null (echte Exchange managt Preise/Funding/Fills);
/// bei Paper setzt der Manager hier die <c>SimulatedExchange</c>-Bruecken: Preise einspeisen (Mark-to-Market),
/// Funding anwenden, abgeschlossene Trades abgreifen (fuer Event/DB/Stats).
/// </summary>
public sealed record PaperHooks(
    Action<string, decimal> SetPrice,
    Action<decimal> ApplyFunding,
    Func<IReadOnlyList<CompletedTrade>> DrainNewTrades,
    decimal FundingRate);

/// <summary>
/// Eigenstaendiger Cross-Sectional-Momentum-Trading-Service (market-neutral, periodischer Rebalance).
/// Bewusst NICHT von <see cref="TradingServiceBase"/> abgeleitet — kein per-Symbol-Scan, kein PriceTicker-SL/TP-
/// Apparat, kein Adopt-Loop (der den market-neutralen Korb kapern wuerde). Teilt nur <see cref="IExchangeClient"/>
/// (Paper=SimulatedExchange / Live=BingXRestClient), <see cref="IPublicMarketDataClient"/> (Klines/Tickers) und
/// den <see cref="BotEventBus"/>. Rebalance per Wall-Clock (robust gegen Pi-Downtime), Korb-State persistiert
/// (Crash-Recovery: bei Neustart bestehenden Korb adoptieren statt sofort neu zu ranken).
/// </summary>
public sealed class CrossSectionalTradingService : IDisposable
{
    private readonly IExchangeClient _execution;
    private readonly IPublicMarketDataClient _marketData;
    private readonly RiskSettings _risk;
    private readonly CrossSectionalSettings _cfg;
    private readonly BotEventBus _eventBus;
    private readonly ILogger _logger;
    private readonly string? _stateFilePath;
    private readonly PaperHooks? _paper;
    private readonly BotDatabaseService? _dbService;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private DateTime _lastRebalanceUtc = DateTime.MinValue;
    private DateTime _lastFundingUtc = DateTime.MinValue;
    private Dictionary<string, Side> _currentBasket = new();

    public bool IsRunning { get; private set; }
    public IReadOnlyDictionary<string, Side> CurrentBasket => _currentBasket;

    public CrossSectionalTradingService(
        IExchangeClient execution,
        IPublicMarketDataClient marketData,
        RiskSettings risk,
        CrossSectionalSettings cfg,
        BotEventBus eventBus,
        ILogger logger,
        string? stateFilePath = null,
        PaperHooks? paper = null,
        BotDatabaseService? dbService = null)
    {
        _execution = execution;
        _marketData = marketData;
        _risk = risk;
        _cfg = cfg;
        _eventBus = eventBus;
        _logger = logger;
        _stateFilePath = stateFilePath;
        _paper = paper;
        _dbService = dbService;
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;
        await RestoreOrAdoptStateAsync().ConfigureAwait(false);

        IsRunning = true;
        _cts = new CancellationTokenSource();
        _eventBus.PublishBotState(BotState.Running);
        Log(LogLevel.Info, "Engine",
            $"Cross-Sectional gestartet: {_cfg.LongK}L-{_cfg.ShortK}S, {_cfg.LookbackCandles}-Kerzen-Momentum"
            + $"{(_cfg.RiskAdjusted ? " (vol-bereinigt)" : "")}, Rebalance alle {_cfg.RebalanceDays}d, {_cfg.LeverageCap}x, "
            + $"Universum Top-{_cfg.UniverseTopN}{(_cfg.IncludeTradFi ? "+TradFi" : "")}. Naechster Rebalance: "
            + $"{(_lastRebalanceUtc + TimeSpan.FromDays(_cfg.RebalanceDays)):yyyy-MM-dd HH:mm} UTC.");
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async Task StopAsync(bool closePositions = true)
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        if (_loop != null) { try { await _loop.ConfigureAwait(false); } catch { /* loop-cancel */ } }
        if (closePositions)
        {
            try { await _execution.CloseAllPositionsAsync().ConfigureAwait(false); }
            catch (Exception ex) { Log(LogLevel.Warning, "Engine", $"Schliessen beim Stop fehlgeschlagen: {ex.Message}"); }
            DrainPaperTrades();
        }
        IsRunning = false;
        _eventBus.PublishBotState(BotState.Stopped);
        Log(LogLevel.Info, "Engine", "Cross-Sectional gestoppt.");
    }

    public async Task EmergencyStopAsync()
    {
        // Sofort stoppen + alles schliessen (gleiches Vorgehen, eigener Log).
        Log(LogLevel.Warning, "Engine", "NOT-STOPP Cross-Sectional — schliesse alle Korb-Positionen.");
        await StopAsync(closePositions: true).ConfigureAwait(false);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await TickAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log(LogLevel.Error, "Engine", $"Cross-Sectional-Tick-Fehler: {ex.Message}"); }

            try { await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _cfg.CheckIntervalMinutes)), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // 1. Paper: Mark-to-Market der offenen Positionen + Funding alle 8h (Live managt das selbst).
        await RefreshPaperPricingAsync(ct).ConfigureAwait(false);

        // 2. Equity-Snapshot fuer Chart/Stats.
        var acc = await _execution.GetAccountInfoAsync().ConfigureAwait(false);
        _eventBus.PublishEquity(new EquityPoint(DateTime.UtcNow, acc.Balance + acc.UnrealizedPnl));

        // 3. Rebalance faellig? (Wall-Clock — robust gegen Downtime.)
        if (DateTime.UtcNow >= _lastRebalanceUtc + TimeSpan.FromDays(Math.Max(1, _cfg.RebalanceDays)))
            await RebalanceAsync(ct).ConfigureAwait(false);
    }

    private async Task RefreshPaperPricingAsync(CancellationToken ct)
    {
        if (_paper == null) return; // Live: echte Exchange liefert Preise/Funding.
        var positions = await _execution.GetPositionsAsync(ct).ConfigureAwait(false);
        if (positions.Count > 0)
        {
            var tickers = await _marketData.GetAllTickersAsync(ct).ConfigureAwait(false);
            var px = tickers.ToDictionary(t => t.Symbol, t => t.LastPrice);
            foreach (var pos in positions)
                if (px.TryGetValue(pos.Symbol, out var p) && p > 0m)
                    _paper.SetPrice(pos.Symbol, p);
        }
        // Funding alle 8h auf offene Positionen (Paper-Simulation).
        if (_lastFundingUtc == DateTime.MinValue) _lastFundingUtc = DateTime.UtcNow;
        else if (DateTime.UtcNow - _lastFundingUtc >= TimeSpan.FromHours(8))
        {
            _paper.ApplyFunding(_paper.FundingRate);
            _lastFundingUtc = DateTime.UtcNow;
        }
    }

    private async Task RebalanceAsync(CancellationToken ct)
    {
        // a. Universum: Top-N nach 24h-Volumen (Live-Scanner-Spiegel), optional inkl. TradFi.
        var tickers = await _marketData.GetAllTickersAsync(ct).ConfigureAwait(false);
        var symbols = tickers
            .Where(t => t.Symbol.EndsWith("-USDT", StringComparison.OrdinalIgnoreCase)
                        && SymbolClassifier.IsApiTradeable(t.Symbol)
                        && (_cfg.IncludeTradFi || !SymbolClassifier.IsTradFi(t.Symbol)))
            .OrderByDescending(t => t.Volume24h)
            .Take(Math.Max(_cfg.LongK + _cfg.ShortK, _cfg.UniverseTopN))
            .Select(t => t.Symbol)
            .ToList();
        if (symbols.Count == 0) { Log(LogLevel.Warning, "Engine", "Rebalance: kein Universum (keine Tickers)."); return; }

        // b. Klines je Symbol (genug Vorlauf fuer den Lookback) + Preise + Kategorien.
        var navTf = ParseTf(_cfg.NavTimeframe);
        var from = DateTime.UtcNow - TimeSpan.FromHours(TfHours(navTf) * (_cfg.LookbackCandles + 40));
        var to = DateTime.UtcNow;
        var universe = new List<(string Symbol, IReadOnlyList<Candle> Candles)>();
        var prices = new Dictionary<string, decimal>();
        var categories = new Dictionary<string, MarketCategory>();
        foreach (var symbol in symbols)
        {
            ct.ThrowIfCancellationRequested();
            List<Candle> candles;
            try { candles = await _marketData.GetKlinesAsync(symbol, navTf, from, to, ct).ConfigureAwait(false); }
            catch (Exception ex) { Log(LogLevel.Debug, "Engine", $"Klines {symbol} fehlgeschlagen: {ex.Message}"); continue; }
            if (candles.Count <= _cfg.LookbackCandles) continue;
            universe.Add((symbol, candles));
            prices[symbol] = candles[^1].Close;
            categories[symbol] = SymbolClassifier.Classify(symbol);
        }
        if (universe.Count == 0) { Log(LogLevel.Warning, "Engine", "Rebalance: kein Symbol mit genug Klines."); return; }

        // c. Paper: alle Preise in die SimulatedExchange einspeisen (Sizing + Mark-to-Market).
        if (_paper != null)
            foreach (var (sym, px) in prices) _paper.SetPrice(sym, px);

        // d. Ziel-Korb (geteilter Calculator — identisch zum Backtest).
        var basket = MomentumBasketCalculator.ComputeBasket(
            universe, _cfg.LookbackCandles, _cfg.LongK, _cfg.ShortK, _cfg.RiskAdjusted);
        if (basket.Count == 0) { Log(LogLevel.Info, "Engine", "Rebalance: leerer Ziel-Korb (kein klares Momentum)."); }

        // e. Reconciliation (Close-vor-Open, Safety) — geteilt mit Paper/Live.
        var result = await CrossSectionalRebalancer.ReconcileAsync(
            _execution, basket, prices, categories, _cfg, _risk,
            msg => Log(LogLevel.Info, "Exit", msg), ct).ConfigureAwait(false);

        // f. State persistieren + Trades publishen.
        _currentBasket = new Dictionary<string, Side>(basket);
        _lastRebalanceUtc = DateTime.UtcNow;
        SaveState();
        DrainPaperTrades();

        Log(LogLevel.Trade, "Engine",
            $"Rebalance fertig: {result.Closed} geschlossen, {result.Opened} eroeffnet, "
            + $"{result.SkippedMinOrder} Min-Order-Skip, {result.FailedClose} Close-Fehler. "
            + $"Korb ({basket.Count}): {DescribeBasket(basket)}. Naechster: "
            + $"{(_lastRebalanceUtc + TimeSpan.FromDays(_cfg.RebalanceDays)):yyyy-MM-dd HH:mm} UTC.");
    }

    private void DrainPaperTrades()
    {
        if (_paper == null) return;
        var fresh = _paper.DrainNewTrades();
        foreach (var trade in fresh)
            _eventBus.PublishTrade(trade);   // Stats/SignalR/FCM-Subscriber (RAM/Push).

        // DB-Persistenz explizit: KEIN TradeCompleted-Subscriber ruft SaveTradeAsync —
        // ohne diesen Aufruf waere der Paper-Lauf nach jedem Restart unauswertbar
        // (/api/v1/trades leer, Stats-Aggregator-Rebuild findet nichts).
        // EIN Hintergrund-Task fuer alle Trades des Drains (haelt die Insert-Reihenfolge).
        if (_dbService != null && fresh.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                foreach (var t in fresh)
                {
                    try { await _dbService.SaveTradeAsync(t).ConfigureAwait(false); }
                    catch (Exception ex) { Log(LogLevel.Error, "Trade", $"Paper-Trade-Persist fehlgeschlagen ({t.Symbol}): {ex.Message}"); }
                }
            });
        }
    }

    // ─────────── State-Persistenz (Crash-Recovery) ───────────

    private sealed record PersistedState(DateTime LastRebalanceUtc, Dictionary<string, Side> Basket);

    private async Task RestoreOrAdoptStateAsync()
    {
        // Paper startet IMMER frisch: Die SimulatedExchange ist nach jedem Prozess-Start leer
        // (Positionen + PnL weg) — einen persistierten Korb zu adoptieren wuerde den Lauf bis zu
        // RebalanceDays lang einfrieren (leere Sim + Geister-Korb, kein einziger Trade).
        if (_paper != null)
        {
            _lastRebalanceUtc = DateTime.MinValue; // → erster Tick rebalanced sofort.
            Log(LogLevel.Info, "Engine", "Paper-Modus: frischer Start, Rebalance beim ersten Tick (State-Adoption nur im Live-Modus).");
            return;
        }

        // 1. Persistierten State laden (falls vorhanden) → bestehenden Korb adoptieren, NICHT sofort rebalancen.
        if (_stateFilePath != null && File.Exists(_stateFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_stateFilePath).ConfigureAwait(false);
                var st = JsonSerializer.Deserialize<PersistedState>(json);
                if (st != null)
                {
                    _lastRebalanceUtc = st.LastRebalanceUtc;
                    _currentBasket = st.Basket ?? new();
                    Log(LogLevel.Info, "Recovery",
                        $"Cross-Sectional-State geladen: Korb {_currentBasket.Count} Positionen, letzter Rebalance "
                        + $"{_lastRebalanceUtc:yyyy-MM-dd HH:mm} UTC.");
                    return;
                }
            }
            catch (Exception ex) { Log(LogLevel.Warning, "Recovery", $"State-Datei nicht lesbar ({ex.Message}) — adoptiere offene Positionen."); }
        }

        // 2. Kein State: SOFORT ranken. Offene Positionen ohne State-Datei sind mit hoher
        //    Wahrscheinlichkeit FREMDE Reste (Scalper-Wechsel, manuelle Trades) — sie blind als
        //    Korb zu adoptieren verschob den ersten Rebalance um volle RebalanceDays (live
        //    passiert 09.06.2026: 1 Rest-Position adoptiert → Rebalance erst am 30.06.).
        //    Der Rebalancer behandelt sie korrekt: Close-vor-Open schliesst alles, was nicht
        //    in den Ziel-Korb gehoert; Korb-konforme Positionen bleiben implizit erhalten.
        var positions = await _execution.GetPositionsAsync().ConfigureAwait(false);
        if (positions.Count > 0)
        {
            Log(LogLevel.Warning, "Recovery",
                $"{positions.Count} offene Position(en) ohne Xsec-State gefunden — sofortiger Rebalance "
                + "uebernimmt/schliesst sie (Close-vor-Open).");
        }
        _lastRebalanceUtc = DateTime.MinValue; // → erster Tick rebalanced sofort.
        Log(LogLevel.Info, "Engine", "Kein Xsec-State → Rebalance beim ersten Tick.");
    }

    private void SaveState()
    {
        if (_stateFilePath == null) return;
        try
        {
            var json = JsonSerializer.Serialize(new PersistedState(_lastRebalanceUtc, _currentBasket));
            File.WriteAllText(_stateFilePath, json);
        }
        catch (Exception ex) { Log(LogLevel.Warning, "Engine", $"State speichern fehlgeschlagen: {ex.Message}"); }
    }

    // ─────────── Helpers ───────────

    private static string DescribeBasket(IReadOnlyDictionary<string, Side> basket)
    {
        var longs = basket.Where(kv => kv.Value == Side.Buy).Select(kv => kv.Key);
        var shorts = basket.Where(kv => kv.Value == Side.Sell).Select(kv => kv.Key);
        return $"LONG [{string.Join(", ", longs)}] SHORT [{string.Join(", ", shorts)}]";
    }

    private static TimeFrame ParseTf(string tf) =>
        Enum.TryParse<TimeFrame>(tf, ignoreCase: true, out var parsed) ? parsed : TimeFrame.H4;

    private static double TfHours(TimeFrame tf) => tf switch
    {
        TimeFrame.M15 => 0.25,
        TimeFrame.H1 => 1,
        TimeFrame.H4 => 4,
        TimeFrame.D1 => 24,
        TimeFrame.W1 => 168,
        _ => 4,
    };

    private void Log(LogLevel level, string category, string message) =>
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, level, category, message));

    public void Dispose() => _cts?.Dispose();
}
