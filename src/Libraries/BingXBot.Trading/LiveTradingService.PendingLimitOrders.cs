using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Trading;

// Partial fuer Pending-Limit-Orders-Management.
// Split-Teil von LiveTradingService (24.04.2026, P1-1 Gott-Klasse-Split).
//
// Enthaelt: Persistenz (Save/Restore zwischen App-Neustarts), Snapshot-Erstellung,
// Sequenz-Cancel (alle Siblings bei Invalidation), Stale-Sequence-Cleanup (veraltete
// Limit-Orders cancelln wenn neue Sequenz entsteht) und TP-Recovery.
//
// Der RunLoop-Pending-Reconcile-Block (~250 Zeilen) bleibt in der Hauptdatei weil er
// state-naeher mit PriceTickerLoop verwoben ist (OnBeforePriceTickerIteration).
public partial class LiveTradingService
{
    /// <summary>
    /// Fire-and-forget Persistierung der Pending-Limit-Orders (18.04.2026 v1.2.4).
    /// Wird nach jedem Platzieren/Cancellen aufgerufen damit ein Crash zwischen zwei Stop-Zyklen
    /// den State nicht verliert. Fehler werden geschluckt — best-effort, blockiert den Trading-Flow nicht.
    /// </summary>
    private async Task PersistPendingLimitOrdersAsync()
    {
        if (_dbService == null) return;
        try
        {
            var snapshot = GetPendingLimitOrdersSnapshot();
            if (snapshot.Count > 0)
                await _dbService.SavePendingLimitOrdersAsync(snapshot).ConfigureAwait(false);
            else
                await _dbService.ClearPendingLimitOrdersAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                $"PersistPendingLimitOrders fehlgeschlagen: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gibt einen Snapshot aller pending Limit-Orders für DB-Persistenz zurück.
    /// Enthält TP-Werte aus _positionSignals damit nach Recovery TP platziert werden kann.
    /// Key-Format: "{symbol}#{sequenceId}" (Sibling-Entries _Prim/_Add, Legacy _L500/_L618/_L667).
    /// </summary>
    public Dictionary<string, PendingLimitOrderState> GetPendingLimitOrdersSnapshot()
    {
        var result = new Dictionary<string, PendingLimitOrderState>();
        foreach (var kvp in _pendingLimitOrders)
        {
            var state = new PendingLimitOrderState
            {
                OrderId = kvp.Value.OrderId,
                PlacedAt = kvp.Value.PlacedAt,
                InvalidationLevel = kvp.Value.InvalidationLevel,
                IsLong = kvp.Value.IsLong,
                Symbol = kvp.Value.Symbol,
                SequenceId = kvp.Value.SequenceId,
                // TP-Werte direkt aus Tuple (unabhaengig von _positionSignals,
                // sodass Snapshot auch nach Signal-Verlust korrekt ist).
                TakeProfit = kvp.Value.TakeProfit,
                TakeProfit2 = kvp.Value.TakeProfit2,
            };

            // DisableSmartBreakeven + EntryPrice + Strategy-Felder aus zugehoerigem Signal
            // (falls noch vorhanden). Phase 0.7 (Finding 0.7): Strategy-Felder werden mit-
            // persistiert, damit nach 30s+ Signal-Rekonstruktion (Verwaist-Cleanup oder Restart)
            // A-Bruch-BE / Runner / HighProbability-Boost weiterhin korrekt arbeiten.
            var expectedSide = kvp.Value.IsLong ? Side.Buy : Side.Sell;
            var posKey = $"{kvp.Value.Symbol}_{expectedSide}";
            if (_positionSignals.TryGetValue(posKey, out var sig))
            {
                state.DisableSmartBreakeven = sig.DisableSmartBreakeven;
                state.EntryPrice = sig.EntryPrice ?? 0m;
                // Fallback fuer Legacy-Tuple-Eintraege ohne TP-Persist (sollte nach v1.2.5 nicht mehr auftreten)
                state.TakeProfit ??= sig.TakeProfit;
                state.TakeProfit2 ??= sig.TakeProfit2;

                // v1.4.0 Phase 0.7 — Strategy-Felder
                state.NavPointA = sig.NavPointA ?? 0m;
                state.IsGklSetup = sig.IsGklSetup;
                state.GklTimeframe = sig.GklTimeframe;
                state.RunnerHardCap = sig.RunnerHardCap ?? 0m;
                state.IsCounterTrendScalp = sig.IsCounterTrendScalp;
                state.PositionScaleOverride = sig.PositionScaleOverride;
            }

            result[kvp.Key] = state;
        }
        return result;
    }

    /// <summary>
    /// Stellt pending Limit-Orders aus DB-Persistenz wieder her.
    /// Wird beim Start vom LiveTradingManager aufgerufen.
    /// Toleriert Legacy-Einträge (v1.1.4): Symbol leer / SequenceId null → wird aus Key extrahiert.
    /// </summary>
    public void RestorePendingLimitOrders(Dictionary<string, PendingLimitOrderState> states)
    {
        foreach (var kvp in states)
        {
            // Legacy-Migration: v1.1.4 hatte Symbol leer + Key = Symbol. Ab v1.1.5 ist der Key
            // "{symbol}#{sequenceId}" und Symbol/SequenceId sind im State selbst persistiert.
            var symbol = !string.IsNullOrEmpty(kvp.Value.Symbol)
                ? kvp.Value.Symbol
                : ExtractSymbolFromPendingKey(kvp.Key);
            var sequenceId = kvp.Value.SequenceId; // null = Legacy

            // Bei Legacy-Einträgen den Key ins neue Format überführen damit Invalidation-Cancel funktioniert.
            var newKey = kvp.Key.Contains('#') ? kvp.Key : BuildPendingKey(symbol, sequenceId);

            // TakeProfit/TakeProfit2 persistiert — bei alten DB-Eintraegen null → Rekonstruktion
            // ohne TP (wie bisher), aber neue Eintraege behalten TP beim Restart.
            // v1.4.0 Phase 0.7 (Finding 0.7) — Strategy-Felder aus DB ins Tuple uebernehmen.
            _pendingLimitOrders[newKey] = (kvp.Value.OrderId, kvp.Value.PlacedAt,
                kvp.Value.InvalidationLevel, kvp.Value.IsLong, symbol, sequenceId,
                kvp.Value.TakeProfit, kvp.Value.TakeProfit2,
                NavPointA: kvp.Value.NavPointA,
                IsGklSetup: kvp.Value.IsGklSetup,
                GklTimeframe: kvp.Value.GklTimeframe,
                RunnerHardCap: kvp.Value.RunnerHardCap,
                IsCounterTrendScalp: kvp.Value.IsCounterTrendScalp,
                PositionScaleOverride: kvp.Value.PositionScaleOverride);

            // Signal muss auch existieren damit Fill-Detection das TP setzen kann
            var expectedSide = kvp.Value.IsLong ? Side.Buy : Side.Sell;
            var posKey = $"{symbol}_{expectedSide}";
            if (!_positionSignals.ContainsKey(posKey) && kvp.Value.TakeProfit.HasValue)
            {
                // EntryPrice persistiert seit 17.04.2026 (Limit-Preis). Bei Legacy-Einträgen
                // oder unbekanntem EntryPrice fallback auf null — im Fill werden die tatsächlichen
                // Werte in _exitStates korrigiert.
                // v1.4.0 Phase 0.7 (Finding 0.7) — Strategy-Felder restoren, damit BE-Trigger /
                // Runner / HighProb-Boost nach Restart greifen.
                decimal? entryPx = kvp.Value.EntryPrice > 0 ? kvp.Value.EntryPrice : null;
                var signal = new SignalResult(
                    kvp.Value.IsLong ? Signal.Long : Signal.Short,
                    0.5m, entryPx,
                    StopLoss: kvp.Value.InvalidationLevel,
                    TakeProfit: kvp.Value.TakeProfit,
                    Reason: "Recovery: Pending Limit-Order wiederhergestellt",
                    TakeProfit2: kvp.Value.TakeProfit2,
                    DisableSmartBreakeven: kvp.Value.DisableSmartBreakeven,
                    SequenceId: sequenceId,
                    IsGklSetup: kvp.Value.IsGklSetup,
                    GklTimeframe: kvp.Value.GklTimeframe,
                    NavPointA: kvp.Value.NavPointA > 0 ? kvp.Value.NavPointA : null,
                    RunnerHardCap: kvp.Value.RunnerHardCap > 0 ? kvp.Value.RunnerHardCap : null,
                    IsCounterTrendScalp: kvp.Value.IsCounterTrendScalp,
                    PositionScaleOverride: kvp.Value.PositionScaleOverride);
                _positionSignals[posKey] = signal;
                OnSignalCreated(posKey);

                // ExitState beim Restore direkt anlegen (v1.2.5) — vorher wurde er erst bei Fill
                // in der PriceTickerLoop-Reconciliation erzeugt. EntryPrice aus Limit-Preis
                // (Fallback auf 0 wenn kein Limit-Preis persistiert war).
                if (!_exitStates.ContainsKey(posKey))
                {
                    var epForState = kvp.Value.EntryPrice > 0 ? kvp.Value.EntryPrice : 0m;
                    _exitStates[posKey] = new PositionExitState
                    {
                        Signal = signal,
                        Symbol = symbol,
                        Side = expectedSide,
                        EntryPrice = epForState,
                        OriginalQuantity = 0m, // echte Qty erst nach Fill bekannt
                        Tp2 = kvp.Value.TakeProfit2,
                        EntryTime = kvp.Value.PlacedAt, // PlacedAt als Proxy; wird im Fill ueberschrieben
                        SequenceId = sequenceId,
                        IsRecovered = true,
                    };
                }
            }
        }
    }

    /// <summary>
    /// Cancelt ALLE pending Limit-Orders die zur gegebenen Sequenz gehören.
    /// Sibling-kritisch: Bei Invalidierung müssen alle parallel pending Entry-Orders
    /// (_Prim/_Add seit v1.2.5, Legacy _L500/_L618/_L667 aus Triple-Entry-Ära) gemeinsam entfernt
    /// werden. Triple/Quad/Hex-Entry wurden im Strip Phase 2 entfernt, die Legacy-Suffixe bleiben
    /// als Migration-Sicherheitsnetz für persistierte Orders aus älteren Versionen.
    ///
    /// Matching per SequenceId-Prefix (ohne Level-Suffix): Alle Eintraege deren
    /// SequenceId mit sequenceIdPrefix startet werden gecancelt.
    /// </summary>
    public async Task CancelAllPendingForSequenceAsync(string symbol, string sequenceIdPrefix)
    {
        if (string.IsNullOrEmpty(sequenceIdPrefix)) return;

        // Snapshot der Keys nehmen damit Entfernung während Iteration sicher ist
        var matching = _pendingLimitOrders
            .Where(kvp => kvp.Value.Symbol == symbol
                       && kvp.Value.SequenceId != null
                       && kvp.Value.SequenceId.StartsWith(sequenceIdPrefix, StringComparison.Ordinal))
            .Select(kvp => (kvp.Key, kvp.Value.OrderId, kvp.Value.SequenceId))
            .ToList();

        if (matching.Count == 0) return;

        foreach (var (key, orderId, seqId) in matching)
        {
            try
            {
                await _restClient.CancelOrderAsync(orderId, symbol).ConfigureAwait(false);
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"{symbol}: Limit-Order gecancellt — Sequenz invalidiert (SeqId={seqId}, OrderId={orderId})",
                    symbol));
            }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                    $"{symbol}: Cancel nach Invalidation schlug fehl (moeglicherweise bereits gefuellt/gecancellt): {ex.Message}",
                    symbol));
            }
            _pendingLimitOrders.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Stale-Sequence-Cleanup (19.04.2026): Wenn ein neues Signal fuer (Symbol, Seite) platziert wird
    /// und eine pending Limit-Order auf einer ANDEREN (veralteten) Sequenz existiert, wird die alte
    /// Order gecancelt bevor die neue platziert wird.
    ///
    /// Hintergrund: Wenn <see cref="BingXBot.Engine.Indicators.SequenceStateMachine"/> die Sequenz
    /// fortschreibt (PointA-Shift, neuer PointB) verschieben sich die Fib-Levels. Alte Limit-Orders
    /// haengen aber auf den ursprünglichen Preisen und wuerden bei Fill einen Trade auf veralteter
    /// Sequenz-Struktur eroeffnen (SL/TP basieren auf neuer Sequenz — Mismatch).
    ///
    /// Geschwister-Orders (gleicher kanonischer Key, Triple-Entry _L500/_L618/_L667) bleiben erhalten.
    /// </summary>
    private async Task CancelStaleSequencePendingAsync(string symbol, Side newSide, string? newSequenceId)
    {
        var newKey = GetCanonicalSequenceKey(newSequenceId);
        if (newKey == null) return;

        var newIsLong = newSide == Side.Buy;
        var cascaded = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kvp in _pendingLimitOrders.ToList())
        {
            if (!string.Equals(kvp.Value.Symbol, symbol, StringComparison.OrdinalIgnoreCase)) continue;
            if (kvp.Value.IsLong != newIsLong) continue;  // andere Richtung = eigenstaendige Gegensequenz
            var existingKey = GetCanonicalSequenceKey(kvp.Value.SequenceId);
            if (existingKey == null) continue;
            if (string.Equals(existingKey, newKey, StringComparison.Ordinal)) continue;  // Geschwister

            if (cascaded.Add(existingKey))
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"{symbol}: Stale pending Order auf veralteter Sequenz ({existingKey}) — cancelle vor Platzierung der neuen Order ({newKey})",
                    symbol));
                await CancelAllPendingForSequenceAsync(symbol, existingKey).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Platziert TP-Orders für eine bestehende Position (Recovery nach App-Neustart).
    /// Wird vom LiveTradingManager aufgerufen wenn eine Position ohne TP-Orders erkannt wird.
    /// </summary>
    public async Task RecoverTpOrdersAsync(string symbol, Side side, decimal quantity, SignalResult signal)
    {
        await PlaceTpLimitOrdersAfterFillAsync(symbol, side, quantity, signal).ConfigureAwait(false);
    }

    /// <summary>
    /// 04.05.2026 — Time-Based-Expiry für pending Limit-Orders.
    /// Cancelt alle pending Orders deren <c>PlacedAt</c> älter als
    /// <see cref="RiskSettings.PendingLimitOrderMaxAgeHours"/> ist. Wenn der Wert ≤ 0 ist,
    /// macht die Methode nichts (Backwards-Compat / Opt-out).
    ///
    /// Snapshot-Report-Fix Befund 3 / A0.7:
    ///   1. Pending-Eintraege deren OrderId NICHT mehr in <paramref name="openOrders"/> erscheint,
    ///      werden SOFORT entfernt — unabhaengig vom Alter. Schuetzt vor "23 Tage alter
    ///      Recovery-Pending im Limbo" (NCSKMSFT/NCSKAMZN/NCSKCOIN im Snapshot vom 2026-05-17).
    ///   2. Beim Entfernen wird zusaetzlich der zugehoerige <c>_exitStates</c>-Eintrag
    ///      (IsRecovered=true, OriginalQuantity=0) abgeraeumt — A0.5 wuerde ihn sonst erst nach 1 h
    ///      finden. Zusammen mit A0.5 vermeidet das das "Pending verschwunden, ExitState lebt weiter"-
    ///      Szenario.
    ///
    /// Hintergrund: Wenn ein Symbol aus der Top-100 herausfällt, wird kein neues Signal mehr
    /// generiert — <see cref="CancelStaleSequencePendingAsync"/> löst nur bei NEUEM Signal aus.
    ///
    /// Wird vom Reconcile-Loop alle 60 s aufgerufen. Internal für Testbarkeit.
    /// </summary>
    internal async Task CancelExpiredPendingLimitOrdersAsync(IReadOnlyList<Order>? openOrders = null)
    {
        if (_pendingLimitOrders.IsEmpty) return;

        var maxAgeHours = _riskSettings.PendingLimitOrderMaxAgeHours;
        var enforceMaxAge = maxAgeHours > 0m;
        var nowUtc = DateTime.UtcNow;
        var maxAge = enforceMaxAge ? TimeSpan.FromHours((double)maxAgeHours) : TimeSpan.MaxValue;

        // Open-Order-Set fuer den Existenz-Check. Wenn openOrders=null (Caller hat sie nicht),
        // ueberspringen wir den BingX-Match-Check und verlassen uns nur auf das Alter.
        HashSet<string>? openOrderIds = null;
        if (openOrders != null)
        {
            openOrderIds = new HashSet<string>(
                openOrders.Select(o => o.OrderId).Where(id => !string.IsNullOrEmpty(id)),
                StringComparer.Ordinal);
        }

        var toRemove = new List<(string Key, string OrderId, string Symbol, string? SequenceId, DateTime PlacedAt, bool IsLong, bool VanishedFromBingx, bool AgeExpired)>();

        foreach (var kvp in _pendingLimitOrders)
        {
            var age = nowUtc - kvp.Value.PlacedAt;
            var ageExpired = enforceMaxAge && age > maxAge;
            var vanished = openOrderIds != null && !openOrderIds.Contains(kvp.Value.OrderId);
            if (!ageExpired && !vanished) continue;
            toRemove.Add((kvp.Key, kvp.Value.OrderId, kvp.Value.Symbol, kvp.Value.SequenceId,
                kvp.Value.PlacedAt, kvp.Value.IsLong, vanished, ageExpired));
        }

        if (toRemove.Count == 0) return;

        foreach (var (key, orderId, symbol, sequenceId, placedAt, isLong, vanished, ageExpired) in toRemove)
        {
            var ageHours = (nowUtc - placedAt).TotalHours;
            var reasonBits = new List<string>();
            if (ageExpired) reasonBits.Add($"Alter={ageHours:F1}h > {maxAgeHours}h");
            if (vanished) reasonBits.Add("BingX-OrderId verschwunden");
            var reason = string.Join(", ", reasonBits);

            // Cancel auf BingX nur wenn die Order dort noch existieren KOENNTE — bei "vanished"
            // ist die Order eh schon weg und der Cancel-Call waere ein nutzloser API-Hit.
            if (!vanished)
            {
                try
                {
                    await _restClient.CancelOrderAsync(orderId, symbol).ConfigureAwait(false);
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                        $"{symbol}: Pending-Limit-Order abgelaufen ({reason}, SeqId={sequenceId}, OrderId={orderId}) — gecancelt",
                        symbol));
                }
                catch (Exception ex)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                        $"{symbol}: Cancel der abgelaufenen Order schlug fehl (moeglicherweise bereits gefuellt/gecancelt): {ex.Message}",
                        symbol));
                }
            }
            else
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"{symbol}: Stale-Pending-Limit entfernt ({reason}, SeqId={sequenceId}, OrderId={orderId})",
                    symbol));
            }

            _pendingLimitOrders.TryRemove(key, out _);

            // Snapshot-Report-Fix A0.7 — Zugehoerige Recovery-ExitStates (OriginalQuantity=0,
            // IsRecovered=true) sofort mit-entfernen. Ohne diesen Schritt blieb der ExitState
            // bis A0.5-Stale-Cleanup nach 1h aktiv und wurde im Dashboard als "offene Position"
            // gezaehlt.
            var posKey = $"{symbol}_{(isLong ? Side.Buy : Side.Sell)}";
            if (_exitStates.TryGetValue(posKey, out var exitState)
                && exitState.IsRecovered && exitState.OriginalQuantity == 0m)
            {
                _exitStates.TryRemove(posKey, out _);
                _positionSignals.TryRemove(posKey, out _);
                OnSignalRemoved(posKey);
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"{symbol}: Recovery-ExitState (Qty=0, IsRecovered=true) gemeinsam mit Pending entfernt",
                    symbol));
            }
        }

        await PersistPendingLimitOrdersAsync().ConfigureAwait(false);
        try { await PersistExitStatesAsync().ConfigureAwait(false); }
        catch { /* best-effort — PersistPending hat schon gelaufen */ }
    }
}
