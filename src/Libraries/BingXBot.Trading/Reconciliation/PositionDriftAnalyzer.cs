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
        UnmanagedPositionWarning
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
        IReadOnlyDictionary<string, DateTime> signalCreatedAt)
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
