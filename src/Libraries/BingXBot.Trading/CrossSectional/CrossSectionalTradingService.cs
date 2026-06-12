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
    // Symbole, deren Korb-Position zwischen den Rebalances extern (manuell) geschlossen wurde.
    // Der Drift-Refill eroeffnet sie NICHT erneut (User-Entscheidung respektieren) — erst der
    // naechste volle Rebalance darf sie wieder ranken. Persistiert im State (Crash-Recovery).
    private HashSet<string> _excludedUntilRebalance = new();
    // Zwei-Tick-Bestaetigung fuer den Drift-Check: Ein Symbol gilt erst als extern geschlossen,
    // wenn seine Position in ZWEI aufeinanderfolgenden Ticks fehlt. Schuetzt vor einem transienten
    // API-Glitch (leere/unvollstaendige Positions-Antwort), der sonst den ganzen Korb sperren und
    // parallel neu aufbauen wuerde (doppelte Exposure mit echtem Geld).
    private HashSet<string> _pendingDriftConfirm = new();

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
        // Drift-Tracking zuruecksetzen (Service-Instanz kann ueber Stop/Start wiederverwendet werden);
        // RestoreOrAdoptStateAsync laedt die persistierte Sperrliste danach ggf. wieder.
        _pendingDriftConfirm = new();
        _excludedUntilRebalance = new();
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

        // 3. Heartbeat persistieren — sonst altert LastHeartbeatUtc waehrend des 21-Tage-Zyklus
        //    tagelang und der Income-Backfill nach einem Reboot rechnet mit einem riesigen
        //    Offline-Fenster (Dedup faengt das, aber unnoetig viele API-Calls + Log-Rauschen).
        if (_dbService != null)
        {
            try { await _dbService.SaveLastHeartbeatAsync(DateTime.UtcNow).ConfigureAwait(false); }
            catch { /* Heartbeat ist Best-Effort — DB-Hickup darf den Tick nicht reissen. */ }
        }

        // 4. Rebalance faellig? (Wall-Clock — robust gegen Downtime.) Sonst: Korb-Drift pruefen.
        if (DateTime.UtcNow >= _lastRebalanceUtc + TimeSpan.FromDays(Math.Max(1, _cfg.RebalanceDays)))
            await RebalanceAsync(ct).ConfigureAwait(false);
        else
            await RefillBasketDriftAsync(ct).ConfigureAwait(false);
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

    private sealed record UniverseData(
        List<(string Symbol, IReadOnlyList<Candle> Candles)> Universe,
        Dictionary<string, decimal> Prices,
        Dictionary<string, MarketCategory> Categories);

    /// <summary>
    /// Baut das Momentum-Universum auf: Top-N nach 24h-Volumen (Live-Scanner-Spiegel, optional inkl.
    /// TradFi) + Klines je Symbol (genug Vorlauf fuer den Lookback) + Preise + Kategorien.
    /// Geteilt zwischen vollem Rebalance und Drift-Refill. <c>null</c> wenn kein brauchbares Universum.
    /// </summary>
    private async Task<UniverseData?> BuildUniverseAsync(string context, CancellationToken ct)
    {
        var tickers = await _marketData.GetAllTickersAsync(ct).ConfigureAwait(false);
        var symbols = tickers
            .Where(t => t.Symbol.EndsWith("-USDT", StringComparison.OrdinalIgnoreCase)
                        && SymbolClassifier.IsApiTradeable(t.Symbol)
                        && (_cfg.IncludeTradFi || !SymbolClassifier.IsTradFi(t.Symbol)))
            .OrderByDescending(t => t.Volume24h)
            .Take(Math.Max(_cfg.LongK + _cfg.ShortK, _cfg.UniverseTopN))
            .Select(t => t.Symbol)
            .ToList();
        if (symbols.Count == 0) { Log(LogLevel.Warning, "Engine", $"{context}: kein Universum (keine Tickers)."); return null; }

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
        if (universe.Count == 0) { Log(LogLevel.Warning, "Engine", $"{context}: kein Symbol mit genug Klines."); return null; }

        // Paper: alle Preise in die SimulatedExchange einspeisen (Sizing + Mark-to-Market).
        if (_paper != null)
            foreach (var (sym, px) in prices) _paper.SetPrice(sym, px);

        return new UniverseData(universe, prices, categories);
    }

    private async Task RebalanceAsync(CancellationToken ct)
    {
        var data = await BuildUniverseAsync("Rebalance", ct).ConfigureAwait(false);
        if (data == null) return;

        // Ziel-Korb (geteilter Calculator — identisch zum Backtest).
        var basket = MomentumBasketCalculator.ComputeBasket(
            data.Universe, _cfg.LookbackCandles, _cfg.LongK, _cfg.ShortK, _cfg.RiskAdjusted);
        if (basket.Count == 0) { Log(LogLevel.Info, "Engine", "Rebalance: leerer Ziel-Korb (kein klares Momentum)."); }

        // Reconciliation (Close-vor-Open, Safety) — geteilt mit Paper/Live.
        var result = await CrossSectionalRebalancer.ReconcileAsync(
            _execution, basket, data.Prices, data.Categories, _cfg, _risk,
            msg => Log(LogLevel.Info, "Exit", msg), ct).ConfigureAwait(false);

        // State persistieren + Trades publishen. Exclusion-Set leeren: der volle Rebalance darf
        // extern geschlossene Symbole wieder ranken (neuer 21-Tage-Zyklus = neue Entscheidung).
        // Soll-Korb = nur tatsaechlich gefuellte Ziel-Symbole — Min-Order-Skips/Rejects wuerden
        // sonst vom Drift-Check faelschlich als "extern geschlossen" gesperrt.
        _currentBasket = basket
            .Where(kv => result.Filled.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        _excludedUntilRebalance.Clear();
        _pendingDriftConfirm.Clear();
        _lastRebalanceUtc = DateTime.UtcNow;
        SaveState();
        DrainPaperTrades();

        Log(LogLevel.Trade, "Engine",
            $"Rebalance fertig: {result.Closed} geschlossen, {result.Opened} eroeffnet, "
            + $"{result.SkippedMinOrder} Min-Order-Skip, {result.FailedClose} Close-Fehler. "
            + $"Korb ({basket.Count}): {DescribeBasket(basket)}. Naechster: "
            + $"{(_lastRebalanceUtc + TimeSpan.FromDays(_cfg.RebalanceDays)):yyyy-MM-dd HH:mm} UTC.");
    }

    /// <summary>
    /// Drift-Erkennung zwischen den Rebalances: Wurden Korb-Positionen extern geschlossen (manuell
    /// auf BingX/in der App), werden die freien Slots mit einem frischen Momentum-Ranking aufgefuellt —
    /// sonst laeuft der Korb bis zu RebalanceDays lang unter-investiert und verliert die
    /// Market-Neutralitaet (z.B. 3 Longs zu, nur Shorts uebrig = volles direktionales Risiko).
    /// Extern geschlossene Symbole werden bis zum naechsten vollen Rebalance NICHT erneut eroeffnet
    /// (User-Entscheidung respektieren); Fremd-Positionen (manuell eroeffnet) bleiben unangetastet.
    /// </summary>
    // Internal fuer Testbarkeit (InternalsVisibleTo=BingXBot.Tests).
    internal async Task RefillBasketDriftAsync(CancellationToken ct)
    {
        if (_currentBasket.Count == 0) return;

        // 1. Extern geschlossene Korb-Positionen erkennen (billig: 1 API-Call pro Tick).
        //    Zwei-Tick-Bestaetigung: erst handeln, wenn dieselbe Position in zwei
        //    aufeinanderfolgenden Ticks fehlt (Schutz vor transienten API-Glitches).
        var positions = await _execution.GetPositionsAsync(ct).ConfigureAwait(false);
        var openKeys = positions.Select(p => $"{p.Symbol}_{p.Side}").ToHashSet();
        var missingNow = _currentBasket
            .Where(kv => !openKeys.Contains($"{kv.Key}_{kv.Value}"))
            .Select(kv => kv.Key)
            .ToHashSet();
        var closedExternally = missingNow.Where(_pendingDriftConfirm.Contains).ToList();
        _pendingDriftConfirm = missingNow;

        if (closedExternally.Count > 0)
        {
            foreach (var symbol in closedExternally)
            {
                _currentBasket.Remove(symbol);
                _excludedUntilRebalance.Add(symbol);
            }
            SaveState();
            Log(LogLevel.Warning, "Engine",
                $"Korb-Drift: {closedExternally.Count} Position(en) extern geschlossen "
                + $"({string.Join(", ", closedExternally)}) — Slots werden mit frischem Momentum-Ranking "
                + "aufgefuellt (geschlossene Symbole bleiben bis zum naechsten Rebalance gesperrt).");
        }

        // 2. Freie Slots je Seite? (Korb soll LongK Longs + ShortK Shorts halten.)
        var freeLongSlots = Math.Max(0, _cfg.LongK - _currentBasket.Count(kv => kv.Value == Side.Buy));
        var freeShortSlots = Math.Max(0, _cfg.ShortK - _currentBasket.Count(kv => kv.Value == Side.Sell));
        if (freeLongSlots == 0 && freeShortSlots == 0) return;

        // 3. Frisches Ranking NUR fuer die freien Slots. Ausgeschlossen: aktueller Korb (nicht doppeln),
        //    extern geschlossene Symbole (Sperre bis Rebalance) und Symbole mit offener Fremd-Position
        //    (kein ungewollter Hedge/Doppel-Exposure).
        var data = await BuildUniverseAsync("Drift-Refill", ct).ConfigureAwait(false);
        if (data == null) return;

        var excluded = new HashSet<string>(_currentBasket.Keys);
        excluded.UnionWith(_excludedUntilRebalance);
        foreach (var pos in positions) excluded.Add(pos.Symbol);
        var candidates = data.Universe.Where(u => !excluded.Contains(u.Symbol)).ToList();

        var refill = MomentumBasketCalculator.ComputeBasket(
            candidates, _cfg.LookbackCandles, freeLongSlots, freeShortSlots, _cfg.RiskAdjusted);
        if (refill.Count == 0)
        {
            Log(LogLevel.Info, "Engine",
                $"Drift-Refill: {freeLongSlots}L/{freeShortSlots}S Slot(s) frei, aber kein Kandidat mit klarem Momentum — naechster Versuch in {_cfg.CheckIntervalMinutes} min.");
            return;
        }

        // 4. Reconcile-Ziel = gehaltener Korb + Refill + Fremd-Positionen (nur als Schutz, damit
        //    Close-vor-Open sie zwischen den Rebalances nicht schliesst — erst der volle Rebalance darf das).
        var target = new Dictionary<string, Side>(_currentBasket);
        foreach (var (symbol, side) in refill) target[symbol] = side;
        foreach (var pos in positions)
            if (!target.ContainsKey(pos.Symbol)) target[pos.Symbol] = pos.Side;

        var result = await CrossSectionalRebalancer.ReconcileAsync(
            _execution, target, data.Prices, data.Categories, _cfg, _risk,
            msg => Log(LogLevel.Info, "Exit", msg), ct).ConfigureAwait(false);

        // 5. Nur den Korb (ohne Fremd-Schutz-Eintraege) persistieren — aufgenommen werden
        //    ausschliesslich tatsaechlich gefuellte Refill-Symbole (Min-Order-Skips/Rejects
        //    gehoeren nicht in den Soll-Korb, sonst sperrt der Drift-Check sie faelschlich).
        //    LastRebalanceUtc bleibt — ein Drift-Refill ist KEIN Rebalance, der 21-Tage-Rhythmus
        //    laeuft unveraendert weiter.
        foreach (var (symbol, side) in refill)
            if (result.Filled.Contains(symbol))
                _currentBasket[symbol] = side;
        SaveState();
        DrainPaperTrades();

        Log(LogLevel.Trade, "Engine",
            $"Drift-Refill fertig: {result.Opened} eroeffnet, {result.SkippedMinOrder} Min-Order-Skip. "
            + $"Korb ({_currentBasket.Count}): {DescribeBasket(_currentBasket)}.");
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

    // ExcludedUntilRebalance ist optional (null bei alten State-Dateien — abwaertskompatibel).
    private sealed record PersistedState(
        DateTime LastRebalanceUtc,
        Dictionary<string, Side> Basket,
        List<string>? ExcludedUntilRebalance = null);

    // Internal fuer Testbarkeit (InternalsVisibleTo=BingXBot.Tests).
    internal async Task RestoreOrAdoptStateAsync()
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
                    _excludedUntilRebalance = st.ExcludedUntilRebalance?.ToHashSet() ?? new();
                    Log(LogLevel.Info, "Recovery",
                        $"Cross-Sectional-State geladen: Korb {_currentBasket.Count} Positionen, letzter Rebalance "
                        + $"{_lastRebalanceUtc:yyyy-MM-dd HH:mm} UTC"
                        + (_excludedUntilRebalance.Count > 0
                            ? $", {_excludedUntilRebalance.Count} Symbol(e) bis zum Rebalance gesperrt."
                            : "."));
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
            var json = JsonSerializer.Serialize(new PersistedState(
                _lastRebalanceUtc, _currentBasket,
                _excludedUntilRebalance.Count > 0 ? _excludedUntilRebalance.ToList() : null));
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
