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

            // DisableSmartBreakeven + EntryPrice aus zugehoerigem Signal (falls noch vorhanden).
            var expectedSide = kvp.Value.IsLong ? Side.Buy : Side.Sell;
            var posKey = $"{kvp.Value.Symbol}_{expectedSide}";
            if (_positionSignals.TryGetValue(posKey, out var sig))
            {
                state.DisableSmartBreakeven = sig.DisableSmartBreakeven;
                state.EntryPrice = sig.EntryPrice ?? 0m;
                // Fallback fuer Legacy-Tuple-Eintraege ohne TP-Persist (sollte nach v1.2.5 nicht mehr auftreten)
                state.TakeProfit ??= sig.TakeProfit;
                state.TakeProfit2 ??= sig.TakeProfit2;
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
            _pendingLimitOrders[newKey] = (kvp.Value.OrderId, kvp.Value.PlacedAt,
                kvp.Value.InvalidationLevel, kvp.Value.IsLong, symbol, sequenceId,
                kvp.Value.TakeProfit, kvp.Value.TakeProfit2);

            // Signal muss auch existieren damit Fill-Detection das TP setzen kann
            var expectedSide = kvp.Value.IsLong ? Side.Buy : Side.Sell;
            var posKey = $"{symbol}_{expectedSide}";
            if (!_positionSignals.ContainsKey(posKey) && kvp.Value.TakeProfit.HasValue)
            {
                // EntryPrice persistiert seit 17.04.2026 (Limit-Preis). Bei Legacy-Einträgen
                // oder unbekanntem EntryPrice fallback auf null — im Fill werden die tatsächlichen
                // Werte in _exitStates korrigiert.
                decimal? entryPx = kvp.Value.EntryPrice > 0 ? kvp.Value.EntryPrice : null;
                var signal = new SignalResult(
                    kvp.Value.IsLong ? Signal.Long : Signal.Short,
                    0.5m, entryPx,
                    StopLoss: kvp.Value.InvalidationLevel,
                    TakeProfit: kvp.Value.TakeProfit,
                    Reason: "Recovery: Pending Limit-Order wiederhergestellt",
                    TakeProfit2: kvp.Value.TakeProfit2,
                    DisableSmartBreakeven: kvp.Value.DisableSmartBreakeven,
                    SequenceId: sequenceId);
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
}
