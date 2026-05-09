using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Trading.Reconciliation;

/// <summary>
/// Vergleicht den Bot-internen Trading-State (offene Signale + ExitStates) mit dem
/// tatsaechlichen Zustand auf der Exchange (offene Positionen). Liefert eine Drift-Liste
/// die der <see cref="PositionReconciler"/> dann ausfuehrt.
///
/// Zweck: Drift zwischen Bot-Wahrnehmung und Exchange-Realitaet entsteht bei
/// WebSocket-Reconnect-Luecken, Pi-Crashes, Server-Restarts ohne State-Persist und
/// manuellen User-Eingriffen auf BingX. Ohne Reconcile: doppelte Positionen beim
/// naechsten Entry oder Positionen die vergessen wurden.
///
/// Pure Funktion ohne Seiteneffekte — damit unit-testbar ohne Exchange-Mock.
/// </summary>
public static class PositionDriftAnalyzer
{
    /// <summary>
    /// Einzelner Drift-Befund zwischen Bot-State und Exchange.
    /// </summary>
    public readonly record struct DriftAction(DriftKind Kind, string Symbol, Side Side, string Reason);

    public enum DriftKind
    {
        /// <summary>Signal im Bot, aber keine Position auf Exchange → Signal entfernen.</summary>
        OrphanSignalRemove,

        /// <summary>Position auf Exchange, aber kein Signal im Bot → nur Warnung, nicht schliessen.</summary>
        UnmanagedPositionWarning,

        /// <summary>
        /// v1.5.1 Phase 3 — Position existiert, aber kein nativer STOP_MARKET-Reduce-Only-Schutz auf BingX.
        /// Aktion: Reconcile-Loop ruft <c>SetPositionSlTpAsync</c> mit dem Signal-SL erneut auf
        /// (Re-Place). Wenn kein Signal-SL bekannt: EmergencyStop fuer dieses Symbol (User-Eingriff noetig).
        /// Grace-Window 30 s nach Position-Open (verhindert Race zwischen Position-Eroeffnung und SL-Place).
        /// </summary>
        MissingStopLoss,

        /// <summary>
        /// Phase 18 / B2 — Position offen mit erwarteter TP-Konfiguration (Signal hat TakeProfit gesetzt),
        /// aber keine bot-platzierte LIMIT-Reduce-Only-Order auf der Exchange. Aktion: PlaceTpLimitOrders
        /// nachholen — analog zu Stage-3 (PendingTpRetry), aber explizit aus dem Reconcile getriggert.
        /// </summary>
        MissingTakeProfit
    }

    /// <summary>
    /// Fuehrt den Drift-Vergleich aus.
    /// </summary>
    /// <param name="exchangePositions">Alle offenen Positionen laut Exchange (REST-Snapshot).</param>
    /// <param name="botSignalKeys">Keys des Bot-internen <c>_positionSignals</c>-Dictionarys
    /// (Format <c>"{Symbol}_{Side}"</c>).</param>
    /// <param name="pendingSymbolSides">Symbol+Side-Paare deren Limit-Entry noch pending ist —
    /// diese werden als <see cref="DriftKind.OrphanSignalRemove"/> NICHT gemeldet, weil die
    /// Position noch nicht gefuellt wurde (erwartetes Verhalten).</param>
    /// <param name="graceWindow">Toleranz: Neu-angelegte Signale werden erst nach dieser Zeit
    /// als Orphan betrachtet (Verhindert Race zwischen SignalOpen und naechstem Position-Fetch).</param>
    /// <param name="signalCreatedAt">Zeitstempel wann ein Signal angelegt wurde (fuer Grace-Window).
    /// Fehlende Eintraege gelten als alt genug.</param>
    /// <returns>Liste aller gefundenen Drift-Eintraege. Kann leer sein.</returns>
    public static IReadOnlyList<DriftAction> Analyze(
        IReadOnlyList<Position> exchangePositions,
        IReadOnlyCollection<string> botSignalKeys,
        IReadOnlySet<(string Symbol, Side Side)> pendingSymbolSides,
        TimeSpan graceWindow,
        IReadOnlyDictionary<string, DateTime> signalCreatedAt,
        IReadOnlyList<Order>? openOrders = null,
        IReadOnlyDictionary<string, DateTime>? positionOpenedAt = null,
        TimeSpan? missingStopGraceWindow = null,
        IReadOnlySet<string>? signalsExpectingTakeProfit = null)
    {
        var actions = new List<DriftAction>();
        var now = DateTime.UtcNow;

        // Exchange-Side → HashSet fuer O(1)-Lookups
        var exchangeKeys = new HashSet<string>(
            exchangePositions
                .Where(p => p.Quantity > 0)
                .Select(p => $"{p.Symbol}_{p.Side}"),
            StringComparer.Ordinal);

        // 1) Bot-Signals ohne Position auf Exchange → OrphanSignalRemove
        //    Ausnahme: pending-Entry (Limit-Order-Fill steht noch aus) oder innerhalb Grace-Window.
        foreach (var key in botSignalKeys)
        {
            if (exchangeKeys.Contains(key)) continue;

            // Pending-Entry? Dann ist "keine Position" erwartbar.
            if (TryParseKey(key, out var sym, out var side) &&
                pendingSymbolSides.Contains((sym, side)))
                continue;

            // Grace: sehr junge Signale toleriert (Race zwischen OpenSignal und GetPositions)
            if (signalCreatedAt.TryGetValue(key, out var created) &&
                (now - created) < graceWindow)
                continue;

            if (!TryParseKey(key, out var orphanSym, out var orphanSide))
                continue;

            actions.Add(new DriftAction(
                DriftKind.OrphanSignalRemove,
                orphanSym,
                orphanSide,
                "Signal ohne zugehoerige Position auf Exchange (wahrscheinlich manuell geschlossen oder Fill-Event verschluckt)"));
        }

        // 2) Positionen auf Exchange ohne Bot-Signal → UnmanagedPositionWarning
        //    Nur warnen, nicht auto-schliessen — koennte manueller User-Trade, anderer Bot etc. sein.
        foreach (var pos in exchangePositions.Where(p => p.Quantity > 0))
        {
            var key = $"{pos.Symbol}_{pos.Side}";
            if (botSignalKeys.Contains(key)) continue;

            actions.Add(new DriftAction(
                DriftKind.UnmanagedPositionWarning,
                pos.Symbol,
                pos.Side,
                $"Position auf Exchange ohne Bot-Signal (Qty={pos.Quantity:F4}, Entry={pos.EntryPrice:F8}) — nicht automatisch geschlossen"));
        }

        // 3) v1.5.1 Phase 3 — Missing-StopLoss-Detection: Bot-managed Position ohne nativen
        //    STOP_MARKET-Reduce-Only-Schutz. Nur aktiv wenn openOrders + positionOpenedAt geliefert sind.
        //    Grace-Window: frisch geoeffnete Positionen (≤ 30 s) werden ausgeklammert — der SL-Place
        //    folgt im PriceTickerLoop in den ersten Sekunden, kein Drift.
        if (openOrders != null)
        {
            var stopGrace = missingStopGraceWindow ?? TimeSpan.FromSeconds(30);
            // (Symbol, ClosingSide) → hat einen nativen STOP_MARKET-Reduce-Only-Schutz
            var stopOrderSet = new HashSet<(string Symbol, Side Side)>();
            foreach (var o in openOrders)
            {
                if (o.Type != OrderType.StopMarket) continue;
                if (!o.ReduceOnly) continue;
                stopOrderSet.Add((o.Symbol, o.Side));
            }

            foreach (var pos in exchangePositions.Where(p => p.Quantity > 0))
            {
                var key = $"{pos.Symbol}_{pos.Side}";
                if (!botSignalKeys.Contains(key)) continue;  // Unmanaged → woanders behandelt
                var closingSide = pos.Side == Side.Buy ? Side.Sell : Side.Buy;
                if (stopOrderSet.Contains((pos.Symbol, closingSide))) continue;

                // Grace: Position erst seit kurzem offen → SL-Place vermutlich gerade in Arbeit.
                if (positionOpenedAt != null
                    && positionOpenedAt.TryGetValue(key, out var openedAt)
                    && (now - openedAt) < stopGrace)
                    continue;

                actions.Add(new DriftAction(
                    DriftKind.MissingStopLoss,
                    pos.Symbol,
                    pos.Side,
                    $"Position offen ohne nativen SL (Side={closingSide} STOP_MARKET ReduceOnly fehlt) — Re-Place erforderlich"));
            }

            // 4) Phase 18 / B2 — Missing-TP-Detection. Wenn das Signal ein TakeProfit erwartet
            //    (set.Contains(key)) UND keine bot-platzierte LIMIT-Reduce-Only-Order existiert →
            //    Re-Place TP nachholen. Ohne diesen Check laufen Positionen nach Bot-Restart
            //    teilweise nur mit nativem SL bis SL-Hit oder manuellem Close.
            if (signalsExpectingTakeProfit != null && signalsExpectingTakeProfit.Count > 0)
            {
                var tpOrderSet = new HashSet<(string Symbol, Side Side)>();
                foreach (var o in openOrders)
                {
                    if (o.Type != OrderType.Limit) continue;
                    if (!o.ReduceOnly) continue;
                    tpOrderSet.Add((o.Symbol, o.Side));
                }

                foreach (var pos in exchangePositions.Where(p => p.Quantity > 0))
                {
                    var key = $"{pos.Symbol}_{pos.Side}";
                    if (!signalsExpectingTakeProfit.Contains(key)) continue;
                    var closingSide = pos.Side == Side.Buy ? Side.Sell : Side.Buy;
                    if (tpOrderSet.Contains((pos.Symbol, closingSide))) continue;

                    // Grace-Window analog Missing-Stop — frisch geoeffnet, TP-Place noch in Arbeit.
                    if (positionOpenedAt != null
                        && positionOpenedAt.TryGetValue(key, out var openedAt)
                        && (now - openedAt) < (missingStopGraceWindow ?? TimeSpan.FromSeconds(30)))
                        continue;

                    actions.Add(new DriftAction(
                        DriftKind.MissingTakeProfit,
                        pos.Symbol,
                        pos.Side,
                        $"Position offen mit erwartetem TP, aber keine LIMIT-Reduce-Only-Order auf Exchange ({closingSide}) — Re-Place erforderlich"));
                }
            }
        }

        return actions;
    }

    private static bool TryParseKey(string key, out string symbol, out Side side)
    {
        // Key-Format: "{Symbol}_{Side}" (z.B. "BTC-USDT_Buy"). Symbol selbst kann Bindestriche
        // enthalten, aber keinen Underscore → letzter Underscore trennt Side.
        var idx = key.LastIndexOf('_');
        if (idx <= 0 || idx >= key.Length - 1)
        {
            symbol = "";
            side = Side.Buy;
            return false;
        }

        symbol = key.Substring(0, idx);
        return Enum.TryParse(key.Substring(idx + 1), ignoreCase: true, out side);
    }
}
