using System.Text.Json;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Filters;
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
    // Mehrfach-Tick-Bestaetigung fuer den Drift-Check: Zaehler pro Symbol — fehlt die Position
    // in einem Tick → +1, ist sie wieder da → Reset. Erst ab DriftConfirmTicks aufeinanderfolgenden
    // Fehl-Ticks gilt das Symbol als extern geschlossen. Schuetzt vor transienten API-Glitches
    // (leere/unvollstaendige Positions-Antwort → sonst Korb-Doppelaufbau mit echtem Geld) und ist
    // — anders als ein einfaches Vorgaenger-Set — robust gegen alternierende Teil-Glitches
    // ANDERER Symbole (die Bestaetigung eines echt geschlossenen Symbols wird nicht verschleppt).
    private const int DriftConfirmTicks = 2;
    private Dictionary<string, int> _driftMissCounter = new();
    // Positionen, deren Close beim Rebalance/Refill scheiterte (TradFi ausserhalb der Handels-
    // zeiten, Rate-Limit). Sie sind KEINE Fremd-Positionen: sie binden Margin doppelt und tragen
    // ungewollte Alt-Richtung — der Drift-Tick schliesst sie erneut, bis es gelingt. Persistiert
    // im State (Crash-Recovery), sonst wuerde ein Neustart sie zu geschuetzten Fremd-Positionen
    // umdeuten und bis zum naechsten Rebalance konservieren.
    private Dictionary<string, Side> _pendingCloses = new();
    // Wochenend-Verschiebungs-Log nur einmal je Verschiebung (nicht alle 30 min).
    private bool _rebalanceDeferredLogged;

    /// <summary>
    /// Per-Tick-Hard-Cap (Watchdog). Ein voller Rebalance (Klines fuer Top-50+TradFi + bis zu
    /// LongK+ShortK Close/Open-Orders) liegt typisch bei 1-3 min; 10 min laesst reichlich Luft,
    /// bleibt aber unter dem 30-min-Tick-Intervall und weit unter der 90-min-Stale-Schwelle des
    /// <c>StaleEngineDetector</c> — ein haengender Tick kostet damit ein Intervall statt Stunden.
    /// </summary>
    private static readonly TimeSpan TickTimeout = TimeSpan.FromMinutes(10);

    public bool IsRunning { get; private set; }
    public IReadOnlyDictionary<string, Side> CurrentBasket => _currentBasket;

    /// <summary>
    /// Zeitpunkt des letzten abgeschlossenen Tick-Versuchs (Success ODER Failure — Liveness des
    /// Loops, nicht Erfolg). Der Xsec-Loop hat keinen Scanner und feuert kein ScanCycleCompleted;
    /// ohne diesen Indikator waere ein stillschweigend toter Tick-Loop fuer den
    /// StaleEngineDetector unsichtbar (Watchdog-Blindheit, live diagnostiziert 12.06.2026).
    /// </summary>
    public DateTime? LastTickUtc { get; private set; }

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
        _driftMissCounter = new();
        _excludedUntilRebalance = new();
        _pendingCloses = new();
        await RestoreOrAdoptStateAsync().ConfigureAwait(false);

        IsRunning = true;
        _cts = new CancellationTokenSource();
        _eventBus.PublishBotState(BotState.Running);
        Log(LogLevel.Info, "Engine",
            IsDominanceSpread(_cfg)
                ? $"Cross-Sectional gestartet (BTC-DOMINANZ-SPREAD): Long BTC / Short Top-{_cfg.ShortK}-Volumen-Alts, "
                  + $"Rebalance alle {_cfg.RebalanceDays}d, {_cfg.LeverageCap}x, Universum Top-{_cfg.UniverseTopN}. "
                  + $"Naechster Rebalance: {(_lastRebalanceUtc + TimeSpan.FromDays(_cfg.RebalanceDays)):yyyy-MM-dd HH:mm} UTC."
                : $"Cross-Sectional gestartet: {_cfg.LongK}L-{_cfg.ShortK}S, {_cfg.LookbackCandles}-Kerzen-Momentum"
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
            var tickStart = DateTime.UtcNow;
            // Per-Tick-Hard-Cap analog TradingServiceBase.ScanIterationTimeout: ein Sub-Await, der
            // weder respondiert noch ct respektiert, fror den Loop sonst bis zum Stale-Detector ein
            // (live 25.07.2026: 13.7 h ohne Tick, danach Auto-Restart am pausierten TradFi-Symbol
            // gescheitert → Engine 40 h tot). Nach dem Cap wird der Tick hart gecancelled und im
            // naechsten Intervall neu versucht.
            using var tickCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            tickCts.CancelAfter(TickTimeout);

            try { await TickAsync(tickCts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (OperationCanceledException)
            {
                // Per-Tick-Timeout ausgeloest (tickCts hat ct nicht) — Loop lebt weiter.
                Log(LogLevel.Error, "Engine",
                    $"Cross-Sectional-Tick Timeout nach {(DateTime.UtcNow - tickStart).TotalSeconds:F0}s "
                    + $"(Hard-Cap {TickTimeout.TotalMinutes:F0} min) — Hard-Cancel, naechster Versuch im normalen Tick.");
            }
            catch (Exception ex) { Log(LogLevel.Error, "Engine", $"Cross-Sectional-Tick-Fehler: {ex.Message}"); }
            // Liveness-Marker NACH dem Tick-Versuch (Success oder Failure) — der Loop lebt.
            LastTickUtc = DateTime.UtcNow;

            try { await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _cfg.CheckIntervalMinutes)), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // 1. Heartbeat ZUERST persistieren — er ist der Liveness-Proxy fuer Watchdog/Backfill
        //    und darf nicht hinter einem fehleranfaelligen API-Call (Account/Equity) haengen.
        //    Sonst altert LastHeartbeatUtc waehrend des 21-Tage-Zyklus und der Income-Backfill
        //    rechnet nach einem Reboot mit einem riesigen Offline-Fenster.
        if (_dbService != null)
        {
            try { await _dbService.SaveLastHeartbeatAsync(DateTime.UtcNow).ConfigureAwait(false); }
            catch { /* Heartbeat ist Best-Effort — DB-Hickup darf den Tick nicht reissen. */ }
        }

        // 2. Paper: Mark-to-Market der offenen Positionen + Funding alle 8h (Live managt das selbst).
        await RefreshPaperPricingAsync(ct).ConfigureAwait(false);

        // 3. Equity-Snapshot fuer Chart/Stats — gekapselt: eine transiente Balance-Antwort
        //    (z.B. v2-Form-Glitch, live 12.06.2026) darf Rebalance/Drift-Refill nicht reissen.
        try
        {
            var acc = await _execution.GetAccountInfoAsync().ConfigureAwait(false);
            _eventBus.PublishEquity(new EquityPoint(DateTime.UtcNow, acc.Balance + acc.UnrealizedPnl));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Log(LogLevel.Warning, "Engine", $"Equity-Snapshot im Tick fehlgeschlagen ({ex.Message}) — Tick laeuft weiter."); }

        // 4. Rebalance faellig? (Wall-Clock — robust gegen Downtime.) Sonst: Korb-Drift pruefen.
        if (DateTime.UtcNow >= _lastRebalanceUtc + TimeSpan.FromDays(Math.Max(1, _cfg.RebalanceDays)))
        {
            // Wochenend-Gate: Mit IncludeTradFi traegt TradFi (~40 % des Universums) den Edge —
            // ein Rebalance am Wochenende kann TradFi weder schliessen noch oeffnen (BingX 101413):
            // Fehl-Closes blieben haengen, TradFi-Slots blieben leer, und der kaputte Korb wuerde
            // durch das gesetzte LastRebalanceUtc fuer RebalanceDays zementiert. Der Rebalance
            // wartet auf die naechste Werktags-Session; der Drift-Refill pflegt den Korb solange.
            // Der 9-Tage-Rhythmus laeuft ab dem TATSAECHLICHEN Rebalance weiter (Termin wandert —
            // gewollt, damit er nicht dauerhaft aufs Wochenende faellt).
            if (_cfg.IncludeTradFi && IsTradFiWeekendBlackout(DateTime.UtcNow))
            {
                if (!_rebalanceDeferredLogged)
                {
                    _rebalanceDeferredLogged = true;
                    Log(LogLevel.Info, "Engine",
                        "Rebalance faellig, aber TradFi-Maerkte geschlossen (Wochenende) — "
                        + "verschoben auf die naechste Werktags-Session; Drift-Refill pflegt den Korb.");
                }
                await RefillBasketDriftAsync(ct).ConfigureAwait(false);
            }
            else
            {
                _rebalanceDeferredLogged = false;
                await RebalanceAsync(ct).ConfigureAwait(false);
            }
        }
        else
            await RefillBasketDriftAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Sa/So sind alle Nicht-Forex-TradFi-Maerkte zu (Forex erst ab So 22:00 UTC) —
    /// ein voller Rebalance ist dann nicht sauber ausfuehrbar. Internal fuer Tests.</summary>
    internal static bool IsTradFiWeekendBlackout(DateTime utcNow) =>
        utcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

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
    private async Task<UniverseData?> BuildUniverseAsync(string context, CrossSectionalSettings cfg, CancellationToken ct)
    {
        var tickers = await _marketData.GetAllTickersAsync(ct).ConfigureAwait(false);
        var symbols = tickers
            .Where(t => t.Symbol.EndsWith("-USDT", StringComparison.OrdinalIgnoreCase)
                        && SymbolClassifier.IsApiTradeable(t.Symbol)
                        && (cfg.IncludeTradFi || !SymbolClassifier.IsTradFi(t.Symbol))
                        // Geschlossene Maerkte (TradFi Wochenende, Forex Fr-Abend) nicht ranken:
                        // Opens wuerden mit BingX 101413 scheitern und der Slot bliebe leer —
                        // Dauerfehlversuche pro 30-min-Tick uebers ganze Wochenende.
                        && TradingHoursFilter.IsMarketOpen(t.Symbol, DateTime.UtcNow))
            .OrderByDescending(t => t.Volume24h)
            .Take(Math.Max(cfg.LongK + cfg.ShortK, cfg.UniverseTopN))
            .Select(t => t.Symbol)
            .ToList();
        if (symbols.Count == 0) { Log(LogLevel.Warning, "Engine", $"{context}: kein Universum (keine Tickers)."); return null; }

        var navTf = ParseTf(cfg.NavTimeframe);
        var from = DateTime.UtcNow - TimeSpan.FromHours(TfHours(navTf) * (cfg.LookbackCandles + 40));
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
            if (candles.Count <= cfg.LookbackCandles) continue;
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
        // Konsistenter Settings-Snapshot fuer den GANZEN Rebalance (Schutz vor torn read mit
        // parallelem PUT /settings/xsec — der Tick liest sonst Korb-Bildung und Sizing aus
        // verschiedenen Settings-Zustaenden).
        var cfg = _cfg.Clone();
        var data = await BuildUniverseAsync("Rebalance", cfg, ct).ConfigureAwait(false);
        if (data == null) return;

        // Ziel-Korb (geteilter Calculator — identisch zum Backtest). DominanceSpread: Long BTC /
        // Short volumenstaerkste Alts statt Momentum-Ranking (Harness-Validierung 11.08.2026).
        var basket = IsDominanceSpread(cfg)
            ? MomentumBasketCalculator.ComputeDominanceBasket(data.Universe, longK: 1, shortK: cfg.ShortK)
            : MomentumBasketCalculator.ComputeBasket(
                data.Universe, cfg.LookbackCandles, cfg.LongK, cfg.ShortK, cfg.RiskAdjusted);
        if (basket.Count == 0)
        {
            Log(LogLevel.Info, "Engine", IsDominanceSpread(cfg)
                ? "Rebalance: leerer Ziel-Korb (BTC-Anker nicht im Universum?)."
                : "Rebalance: leerer Ziel-Korb (kein klares Momentum).");
        }

        // Reconciliation (Close-vor-Open, Safety) — geteilt mit Paper/Live.
        var result = await CrossSectionalRebalancer.ReconcileAsync(
            _execution, basket, data.Prices, data.Categories, cfg, _risk,
            msg => Log(LogLevel.Info, "Exit", msg), ct,
            onClosed: pos => BookLiveClose(pos, "Xsec-Rebalance"),
            weights: DominanceWeightsOrNull(cfg, basket)).ConfigureAwait(false);

        // State persistieren + Trades publishen. Exclusion-Set leeren: der volle Rebalance darf
        // extern geschlossene Symbole wieder ranken (neuer 21-Tage-Zyklus = neue Entscheidung).
        // Soll-Korb = nur tatsaechlich gefuellte Ziel-Symbole — Min-Order-Skips/Rejects wuerden
        // sonst vom Drift-Check faelschlich als "extern geschlossen" gesperrt.
        _currentBasket = basket
            .Where(kv => result.Filled.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        _excludedUntilRebalance.Clear();
        _driftMissCounter.Clear();
        // Fehl-Closes in die Retry-Liste (ersetzt die alte: was jetzt noch offen ist, steht im
        // frischen Verify-Snapshot; was wieder Ziel-Korb wurde, ist held und kein Fehl-Close mehr).
        _pendingCloses = result.FailedClosePositions?
            .ToDictionary(p => p.Symbol, p => p.Side) ?? new();
        _lastRebalanceUtc = DateTime.UtcNow;
        SaveState();
        DrainPaperTrades();

        Log(LogLevel.Trade, "Engine",
            $"Rebalance fertig: {result.Closed} geschlossen, {result.Opened} eroeffnet, "
            + $"{result.Resized} auf Zielgewicht korrigiert, "
            + $"{result.SkippedMinOrder} Min-Order-Skip, {result.FailedClose} Close-Fehler. "
            + $"Korb ({basket.Count}): {DescribeBasket(basket)}. Naechster: "
            + $"{(_lastRebalanceUtc + TimeSpan.FromDays(cfg.RebalanceDays)):yyyy-MM-dd HH:mm} UTC.");
    }

    /// <summary>
    /// Drift-Erkennung zwischen den Rebalances: Wurden Korb-Positionen extern geschlossen (manuell
    /// auf BingX/in der App), werden die freien Slots mit einem frischen Momentum-Ranking aufgefuellt —
    /// sonst laeuft der Korb bis zu RebalanceDays lang unter-investiert und verliert die
    /// Market-Neutralitaet (z.B. 3 Longs zu, nur Shorts uebrig = volles direktionales Risiko).
    /// Auch ein KOMPLETT leerer Korb (fehlgeschlagener Rebalance: Settle-Race, Insufficient Margin,
    /// Wochenende) wird hier wieder aufgebaut — vorher war das eine Sackgasse bis zum naechsten
    /// Wall-Clock-Rebalance (bis zu RebalanceDays flat, live beobachtet als "Bot verliert nur noch").
    /// Extern geschlossene Symbole werden bis zum naechsten vollen Rebalance NICHT erneut eroeffnet
    /// (User-Entscheidung respektieren); Fremd-Positionen (manuell eroeffnet) bleiben unangetastet.
    /// </summary>
    // Internal fuer Testbarkeit (InternalsVisibleTo=BingXBot.Tests).
    internal async Task RefillBasketDriftAsync(CancellationToken ct)
    {
        // Konsistenter Settings-Snapshot fuer den ganzen Refill (torn-read-Schutz, s. RebalanceAsync).
        var cfg = _cfg.Clone();

        // 1. Extern geschlossene Korb-Positionen erkennen (billig: 1 API-Call pro Tick).
        //    Mehrfach-Tick-Bestaetigung per Zaehler: erst handeln, wenn dieselbe Position in
        //    DriftConfirmTicks aufeinanderfolgenden Ticks fehlt (Schutz vor API-Glitches).
        var positions = (await _execution.GetPositionsAsync(ct).ConfigureAwait(false)).ToList();
        var openKeys = positions.Select(p => $"{p.Symbol}_{p.Side}").ToHashSet();

        // Fehl-Close-Retry: haengengebliebene Closes des letzten Rebalance/Refill erneut versuchen —
        // pro Tick, bis es gelingt. Ohne diese Liste galten die Reste als Fremd-Positionen und
        // wurden bis zum naechsten Rebalance konserviert (Ueber-Exposure + doppelt gebundene Margin).
        if (_pendingCloses.Count > 0)
        {
            var pendingChanged = false;
            foreach (var (symbol, side) in _pendingCloses.ToList())
            {
                var open = positions.FirstOrDefault(p => p.Symbol == symbol && p.Side == side);
                if (open == null)
                {
                    // Inzwischen zu (frueherer Retry hat gegriffen oder extern geschlossen).
                    _pendingCloses.Remove(symbol);
                    pendingChanged = true;
                    continue;
                }
                if (!TradingHoursFilter.IsMarketOpen(symbol, DateTime.UtcNow)) continue; // Markt zu — Versuch sinnlos.
                try
                {
                    await _execution.ClosePositionAsync(symbol, side).ConfigureAwait(false);
                    _pendingCloses.Remove(symbol);
                    positions.Remove(open);   // lokaler Snapshot: nicht mehr als Fremd-Position schuetzen
                    openKeys.Remove($"{symbol}_{side}");
                    pendingChanged = true;
                    BookLiveClose(open, "Xsec-Close-Retry");
                    Log(LogLevel.Trade, "Exit", $"Fehl-Close-Retry erfolgreich: {symbol} {side} geschlossen.");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Log(LogLevel.Warning, "Exit",
                        $"Fehl-Close-Retry {symbol} {side} erneut fehlgeschlagen ({ex.Message}) — naechster Tick.");
                }
            }
            if (pendingChanged) SaveState();
        }

        // Seiten-Flip: existiert fuer ein Korb-Symbol eine offene GEGENSEITEN-Position, hat der
        // User die Seite bewusst gedreht — sofort als extern geschlossen behandeln (kein
        // Zwei-Tick-Warten: die Gegenposition ist ein positiver Beleg, kein leere-Antwort-Glitch).
        // Ohne diese Regel stuende das Symbol mit der ALTEN Korb-Seite im Reconcile-Ziel — der
        // Rebalancer wuerde die User-Position schliessen und die alte Seite wiedereroeffnen.
        var flipped = _currentBasket
            .Where(kv => openKeys.Contains($"{kv.Key}_{(kv.Value == Side.Buy ? Side.Sell : Side.Buy)}"))
            .Select(kv => kv.Key)
            .ToList();
        if (flipped.Count > 0)
        {
            foreach (var symbol in flipped)
            {
                _currentBasket.Remove(symbol);
                _excludedUntilRebalance.Add(symbol);
                _driftMissCounter.Remove(symbol);
            }
            SaveState();
            Log(LogLevel.Warning, "Engine",
                $"Korb-Drift: {flipped.Count} Symbol(e) extern auf die Gegenseite gedreht "
                + $"({string.Join(", ", flipped)}) — Korb-Seite aufgegeben, User-Position bleibt unangetastet.");
        }
        var missingNow = _currentBasket
            .Where(kv => !openKeys.Contains($"{kv.Key}_{kv.Value}"))
            .Select(kv => kv.Key)
            .ToHashSet();
        foreach (var symbol in missingNow)
            _driftMissCounter[symbol] = _driftMissCounter.TryGetValue(symbol, out var n) ? n + 1 : 1;
        foreach (var present in _driftMissCounter.Keys.Where(k => !missingNow.Contains(k)).ToList())
            _driftMissCounter.Remove(present);
        var closedExternally = _driftMissCounter
            .Where(kv => kv.Value >= DriftConfirmTicks)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var symbol in closedExternally) _driftMissCounter.Remove(symbol);

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

        // DominanceSpread: der Korb ist EIN Paar-Trade — faellt der BTC-Anker weg (extern
        // geschlossen und gesperrt), sind die verbliebenen Alt-Shorts direktionales Net-Short.
        // Dann den Spread AUFLOESEN (restliche Korb-Shorts schliessen) statt weiterzulaufen;
        // der naechste volle Rebalance baut neu auf.
        if (IsDominanceSpread(cfg)
            && _excludedUntilRebalance.Contains(MomentumBasketCalculator.DominanceAnchorSymbol)
            && !_currentBasket.ContainsKey(MomentumBasketCalculator.DominanceAnchorSymbol)
            && _currentBasket.Count > 0)
        {
            Log(LogLevel.Warning, "Engine",
                "Dominanz-Spread: BTC-Anker extern geschlossen — loese den Spread auf "
                + $"({_currentBasket.Count} Korb-Short(s) schliessen), Neuaufbau beim naechsten Rebalance.");
            foreach (var (symbol, side) in _currentBasket.ToList())
            {
                var open = positions.FirstOrDefault(p => p.Symbol == symbol && p.Side == side);
                _currentBasket.Remove(symbol);
                _excludedUntilRebalance.Add(symbol);
                if (open == null) continue;
                try
                {
                    await _execution.ClosePositionAsync(symbol, side).ConfigureAwait(false);
                    BookLiveClose(open, "Xsec-Spread-Aufloesung");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _pendingCloses[symbol] = side;
                    Log(LogLevel.Warning, "Exit", $"Spread-Aufloesung: Close {symbol} {side} fehlgeschlagen ({ex.Message}) — Retry-Liste.");
                }
            }
            SaveState();
            DrainPaperTrades();
            return;
        }

        // 2. Freie Slots je Seite? (Korb soll LongK Longs + ShortK Shorts halten;
        //    DominanceSpread: genau 1 Long-Slot — der BTC-Anker.)
        var longTarget = IsDominanceSpread(cfg) ? 1 : cfg.LongK;
        var freeLongSlots = Math.Max(0, longTarget - _currentBasket.Count(kv => kv.Value == Side.Buy));
        var freeShortSlots = Math.Max(0, cfg.ShortK - _currentBasket.Count(kv => kv.Value == Side.Sell));
        if (freeLongSlots == 0 && freeShortSlots == 0) return;

        // 3. Frisches Ranking NUR fuer die freien Slots. Ausgeschlossen: aktueller Korb (nicht doppeln),
        //    extern geschlossene Symbole (Sperre bis Rebalance) und Symbole mit offener Fremd-Position
        //    (kein ungewollter Hedge/Doppel-Exposure).
        var data = await BuildUniverseAsync("Drift-Refill", cfg, ct).ConfigureAwait(false);
        if (data == null) return;

        var excluded = new HashSet<string>(_currentBasket.Keys);
        excluded.UnionWith(_excludedUntilRebalance);
        foreach (var pos in positions) excluded.Add(pos.Symbol);
        var candidates = data.Universe.Where(u => !excluded.Contains(u.Symbol)).ToList();

        var refill = IsDominanceSpread(cfg)
            ? MomentumBasketCalculator.ComputeDominanceBasket(
                candidates, longK: freeLongSlots > 0 ? 1 : 0, shortK: freeShortSlots)
            : MomentumBasketCalculator.ComputeBasket(
                candidates, cfg.LookbackCandles, freeLongSlots, freeShortSlots, cfg.RiskAdjusted);
        if (refill.Count == 0)
        {
            Log(LogLevel.Info, "Engine",
                $"Drift-Refill: {freeLongSlots}L/{freeShortSlots}S Slot(s) frei, aber kein Kandidat"
                + $"{(IsDominanceSpread(cfg) ? "" : " mit klarem Momentum")} — naechster Versuch in {cfg.CheckIntervalMinutes} min.");
            return;
        }

        // 4. Reconcile-Ziel = gehaltener Korb + Refill + Fremd-Positionen. Schutz-Semantik via
        //    doNotOpen: (a) unbestaetigt fehlende Korb-Symbole (Miss-Zaehler laeuft) bleiben im
        //    Ziel, damit Close-vor-Open sie nicht schliesst, falls die Position real noch offen
        //    ist — WIEDEREROEFFNET werden sie aber erst nach der Zwei-Tick-Bestaetigung NICHT
        //    (Sperrliste) bzw. gar nicht. Vorher hebelte ein einziger freier Slot die Bestaetigung
        //    aus: das unbestaetigt fehlende Symbol wurde im selben Tick wiedereroeffnet.
        //    (b) Fremd-Positionen (manuell eroeffnet) sind reiner Schutz — nie eroeffnen, auch
        //    nicht wenn sie zwischen den beiden Positions-Snapshots verschwinden.
        var target = new Dictionary<string, Side>(_currentBasket);
        var doNotOpen = new HashSet<string>();
        foreach (var symbol in _currentBasket.Keys)
            if (_driftMissCounter.ContainsKey(symbol))
                doNotOpen.Add(symbol);
        foreach (var (symbol, side) in refill) target[symbol] = side;
        foreach (var pos in positions)
        {
            // Fehl-Close-Reste sind KEINE Fremd-Positionen: nicht schuetzen — der Reconcile
            // (Close-vor-Open) versucht den Close damit automatisch erneut.
            if (_pendingCloses.TryGetValue(pos.Symbol, out var pcSide) && pcSide == pos.Side) continue;
            if (!target.ContainsKey(pos.Symbol))
            {
                target[pos.Symbol] = pos.Side;
                doNotOpen.Add(pos.Symbol);
            }
        }

        // basketSlots explizit: target enthaelt auch Fremd-Schutz-Eintraege — der Sizing-Divisor
        // muss die Soll-Korbgroesse (gehaltener Korb + Refill) sein, nicht target.Count.
        // Gewichte (nur DominanceSpread): ueber den SOLL-Korb (gehalten + Refill) — Fremd-Schutz-
        // Eintraege bekommen kein Gewicht (sie werden via doNotOpen ohnehin nie eroeffnet).
        var basketAfterRefill = new Dictionary<string, Side>(_currentBasket);
        foreach (var (symbol, side) in refill) basketAfterRefill[symbol] = side;

        var result = await CrossSectionalRebalancer.ReconcileAsync(
            _execution, target, data.Prices, data.Categories, cfg, _risk,
            msg => Log(LogLevel.Info, "Exit", msg), ct,
            onClosed: pos => BookLiveClose(pos, "Xsec-Drift-Refill"),
            basketSlots: _currentBasket.Count + refill.Count,
            doNotOpen: doNotOpen,
            weights: DominanceWeightsOrNull(cfg, basketAfterRefill)).ConfigureAwait(false);

        // 5. Nur den Korb (ohne Fremd-Schutz-Eintraege) persistieren — aufgenommen werden
        //    ausschliesslich tatsaechlich gefuellte Refill-Symbole (Min-Order-Skips/Rejects
        //    gehoeren nicht in den Soll-Korb, sonst sperrt der Drift-Check sie faelschlich).
        //    LastRebalanceUtc bleibt — ein Drift-Refill ist KEIN Rebalance, der 21-Tage-Rhythmus
        //    laeuft unveraendert weiter.
        foreach (var (symbol, side) in refill)
            if (result.Filled.Contains(symbol))
                _currentBasket[symbol] = side;
        foreach (var p in result.FailedClosePositions ?? [])
            _pendingCloses[p.Symbol] = p.Side;
        SaveState();
        DrainPaperTrades();

        Log(LogLevel.Trade, "Engine",
            $"Drift-Refill fertig: {result.Opened} eroeffnet, {result.Resized} auf Zielgewicht korrigiert, "
            + $"{result.SkippedMinOrder} Min-Order-Skip. "
            + $"Korb ({_currentBasket.Count}): {DescribeBasket(_currentBasket)}.");
    }

    // Taker-Fee-Schaetzung fuer die Live-Close-Buchung (Standard-VIP0-Rate; die echte Rate
    // weicht hoechstens minimal ab und der Income-Backfill bleibt die exakte Quelle).
    private const decimal TakerFeeEstimate = 0.0005m;

    /// <summary>
    /// Bucht einen verifizierten Live-Close des Rebalancers sofort als <see cref="CompletedTrade"/>
    /// (Event fuer Stats/SignalR/FCM + DB-Persistenz). Ohne diese Buchung erschienen Xsec-Closes
    /// erst nach bis zu 30 min als anonymer Income-Backfill ("Backfilled (Income)") — ohne
    /// Entry-Preis und Strategie-Reason. PnL/Fees sind Mark-to-Market-Naeherungen (ClosePositionAsync
    /// liefert keine Fill-Daten); der Income-Backfill dedupliziert dagegen.
    /// EntryTime: BingX liefert in der Positions-Response keine echte Open-Zeit (pos.OpenTime = UtcNow
    /// = Close-Zeit) → als konservativen Proxy den letzten Rebalance nehmen, damit die Holding-Time
    /// nicht faelschlich ~0 ist. Genau exakt ist sie nur im Income-Backfill.
    /// </summary>
    private void BookLiveClose(Position pos, string reason)
    {
        if (_paper != null) return;   // Paper bucht ueber die SimulatedExchange (DrainPaperTrades).
        var fee = pos.Quantity * (pos.EntryPrice + pos.MarkPrice) * TakerFeeEstimate;
        var entryTime = _lastRebalanceUtc != DateTime.MinValue && _lastRebalanceUtc < DateTime.UtcNow
            ? _lastRebalanceUtc
            : pos.OpenTime;
        var trade = new CompletedTrade(
            pos.Symbol, pos.Side, pos.EntryPrice, pos.MarkPrice, pos.Quantity,
            pos.UnrealizedPnl - fee, fee, entryTime, DateTime.UtcNow, reason, TradingMode.Live);
        _eventBus.PublishTrade(trade);
        if (_dbService != null)
        {
            _ = Task.Run(async () =>
            {
                try { await _dbService.SaveTradeAsync(trade).ConfigureAwait(false); }
                catch (Exception ex) { Log(LogLevel.Error, "Trade", $"Xsec-Close-Persist fehlgeschlagen ({pos.Symbol}): {ex.Message}"); }
            });
        }
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

    // ExcludedUntilRebalance/PendingCloses sind optional (null bei alten State-Dateien — abwaertskompatibel).
    private sealed record PersistedState(
        DateTime LastRebalanceUtc,
        Dictionary<string, Side> Basket,
        List<string>? ExcludedUntilRebalance = null,
        Dictionary<string, Side>? PendingCloses = null);

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
                    _pendingCloses = st.PendingCloses ?? new();
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
                _excludedUntilRebalance.Count > 0 ? _excludedUntilRebalance.ToList() : null,
                _pendingCloses.Count > 0 ? _pendingCloses : null));
            File.WriteAllText(_stateFilePath, json);
        }
        catch (Exception ex) { Log(LogLevel.Warning, "Engine", $"State speichern fehlgeschlagen: {ex.Message}"); }
    }

    // ─────────── Helpers ───────────

    /// <summary>DominanceSpread-Modus aktiv? (Unbekannte Mode-Strings = Momentum, abwaertskompatibel.)</summary>
    private static bool IsDominanceSpread(CrossSectionalSettings cfg) =>
        string.Equals(cfg.Mode, CrossSectionalSettings.ModeDominanceSpread, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Seitenweise 50/50-Gewichte des Dominanz-Spreads (Anteil an equity×MarginUtilization):
    /// BTC-Anker 0.5, jeder Short 0.5/ShortK — fehlende Short-Slots bleiben bewusst Cash
    /// (Backtest-Paritaet: kein Konzentrieren der Short-Seite auf weniger Symbole).
    /// <c>null</c> im Momentum-Modus (equal-weight-Default des Rebalancers).
    /// </summary>
    private static Dictionary<string, decimal>? DominanceWeightsOrNull(
        CrossSectionalSettings cfg, IReadOnlyDictionary<string, Side> basket)
    {
        if (!IsDominanceSpread(cfg)) return null;
        var shortSlots = Math.Max(1, cfg.ShortK);
        return basket.ToDictionary(
            kv => kv.Key,
            kv => kv.Value == Side.Buy ? 0.5m : 0.5m / shortSlots);
    }

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
