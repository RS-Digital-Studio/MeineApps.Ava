using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Trading.CrossSectional;

/// <summary>
/// Ergebnis eines Rebalance-Durchlaufs (fuer Logging/Events/Tests). <see cref="Filled"/> enthaelt
/// die Ziel-Symbole, die nach dem Durchlauf tatsaechlich gehalten werden (bereits korrekt offen
/// ODER erfolgreich eroeffnet) — Min-Order-Skips und Rejects fehlen. Der Aufrufer baut seinen
/// Soll-Korb daraus, statt die Exchange erneut zu fragen (frische Market-Orders erscheinen in
/// GetPositions teils erst Sekunden spaeter — eine Nachfrage waere ein Race).
/// </summary>
public sealed record RebalanceResult(
    int Closed, int Opened, int SkippedMinOrder, int FailedClose, IReadOnlySet<string> Filled);

/// <summary>
/// Fuehrt den Cross-Sectional-Rebalance gegen einen <see cref="IExchangeClient"/> aus: bringt die offenen
/// Positionen auf den Ziel-Korb (Symbol → Side). Bewusst exchange-agnostisch — Paper (SimulatedExchange)
/// und Live (BingXRestClient) nutzen DENSELBEN Reconciliation-Code.
///
/// Sicherheits-Regeln (Real-Money):
/// <list type="number">
/// <item><b>Close-vor-Open</b> — erst alle abgewaehlten Positionen schliessen, dann neue oeffnen
///   (sonst kurzzeitige Margin-Ueberschreitung → neue Orders rejected, alte noch offen).</item>
/// <item><b>Close verifizieren</b> — nach dem Schliessen erneut <c>GetPositions</c>; was offen bleibt + nicht
///   ins Ziel passt, ist ein fehlgeschlagener Close → Slot bleibt belegt, naechster Rebalance versucht erneut.</item>
/// <item><b>Kein ungewollter Hedge</b> — solange die Gegenseite eines Symbols noch offen ist, wird die
///   Ziel-Seite NICHT eroeffnet (sonst Long+Short auf demselben Symbol = doppelte Fees, neutralisierte Exposure).</item>
/// <item><b>Min-Order/Leverage</b> — pro Slot <see cref="IExchangeClient.MeetsMinimumOrder"/> pruefen,
///   Leverage per <see cref="CrossSectionalSettings.LeverageCap"/> kappen.</item>
/// </list>
/// </summary>
public static class CrossSectionalRebalancer
{
    public static async Task<RebalanceResult> ReconcileAsync(
        IExchangeClient ex,
        IReadOnlyDictionary<string, Side> target,
        IReadOnlyDictionary<string, decimal> prices,
        IReadOnlyDictionary<string, MarketCategory> categories,
        CrossSectionalSettings cfg,
        RiskSettings risk,
        Action<string>? log = null,
        CancellationToken ct = default,
        Action<Position>? onClosed = null)
    {
        log ??= _ => { };
        var slots = Math.Min(cfg.LongK + cfg.ShortK, risk.MaxOpenPositions);

        // 1. Close-vor-Open: Positionen schliessen, die nicht (mehr) zum Ziel passen (Symbol raus ODER Seite gedreht).
        //    Geschlossene Positionen (Pre-Close-Snapshot) merken — nach der Verifikation meldet
        //    onClosed sie dem Aufrufer (Live: CompletedTrade-Buchung, sonst nur Income-Backfill nach 30 min).
        var positions = await ex.GetPositionsAsync(ct).ConfigureAwait(false);
        var closed = 0;
        var closeAttempts = new List<Position>();
        foreach (var pos in positions)
        {
            ct.ThrowIfCancellationRequested();
            if (!target.TryGetValue(pos.Symbol, out var want) || want != pos.Side)
            {
                // Pro Symbol gekapselt: ein fehlgeschlagener Close (z.B. TradFi am Wochenende →
                // BingX 101413 "non-trading hours", oder 100410 Rate-Limit) darf NICHT den ganzen
                // Rebalance abbrechen — sonst baut der Korb gar nicht auf. Die Position bleibt offen
                // (failedClose im Verify-Schritt), der naechste Durchlauf versucht erneut.
                try
                {
                    await ex.ClosePositionAsync(pos.Symbol, pos.Side).ConfigureAwait(false);
                    closeAttempts.Add(pos);
                    closed++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exn)
                {
                    log($"Rebalance: Close {pos.Symbol} {pos.Side} fehlgeschlagen ({exn.Message}) — Position bleibt, naechster Durchlauf erneut.");
                }
            }
        }

        // 2. Verifizieren: erneut abfragen; bereits korrekt gehaltene merken, fehlgeschlagene Closes zaehlen.
        var after = await ex.GetPositionsAsync(ct).ConfigureAwait(false);
        var held = new HashSet<string>();
        var filled = new HashSet<string>();
        var failedClose = 0;
        foreach (var pos in after)
        {
            if (target.TryGetValue(pos.Symbol, out var want) && want == pos.Side)
            {
                held.Add($"{pos.Symbol}_{pos.Side}");   // schon korrekt → kein Re-Open
                filled.Add(pos.Symbol);
            }
            else
            {
                failedClose++;
                log($"Rebalance: Close fehlgeschlagen — {pos.Symbol} {pos.Side} noch offen. Slot bleibt belegt, naechster Rebalance versucht erneut.");
            }
        }

        // Verifizierte Closes melden (Position im after-Snapshot verschwunden = Close hat gegriffen).
        if (onClosed != null)
        {
            var stillOpen = after.Select(p => $"{p.Symbol}_{p.Side}").ToHashSet();
            foreach (var pos in closeAttempts.Where(p => !stillOpen.Contains($"{p.Symbol}_{p.Side}")))
            {
                try { onClosed(pos); }
                catch (Exception exn) { log($"Rebalance: onClosed-Hook fehlgeschlagen ({pos.Symbol}): {exn.Message}"); }
            }
        }

        // 3. Sizing: Equity-gleichgewichtet ueber die Slots. Zusaetzlich gegen die FREIE Margin
        //    deckeln: beim Drift-Refill binden die gehaltenen Korb- und Fremd-Positionen bereits
        //    Margin — ohne Cap wuerden die neuen Slots so dimensioniert, als waere der Korb leer,
        //    und BingX lehnte die Orders mit Insufficient Margin ab (Slot bliebe dauerhaft leer).
        var acc = await ex.GetAccountInfoAsync().ConfigureAwait(false);
        var equity = acc.Balance + acc.UnrealizedPnl;
        if (equity <= 0m || slots <= 0)
            return new RebalanceResult(closed, 0, 0, failedClose, filled);
        var perSlotMargin = equity * cfg.MarginUtilization / slots;
        var opensNeeded = target.Count(kv => !held.Contains($"{kv.Key}_{kv.Value}"));
        if (opensNeeded > 0 && acc.AvailableBalance > 0m)
            perSlotMargin = Math.Min(perSlotMargin, acc.AvailableBalance * 0.95m / opensNeeded);

        // 4. Ziel-Positionen oeffnen, die noch nicht gehalten werden.
        var opened = 0;
        var skippedMin = 0;
        foreach (var (symbol, side) in target)
        {
            ct.ThrowIfCancellationRequested();
            if (held.Contains($"{symbol}_{side}")) continue;

            // Gegenseite noch offen (fehlgeschlagener Close) → NICHT die Ziel-Seite oeffnen (kein Hedge derselben Exposure).
            if (after.Any(pp => pp.Symbol == symbol && pp.Side != side))
            {
                log($"Rebalance: {symbol} Gegenseite noch offen → {side}-Open uebersprungen.");
                continue;
            }

            if (!prices.TryGetValue(symbol, out var price) || price <= 0m) continue;

            var cat = categories.TryGetValue(symbol, out var c) ? c : MarketCategory.Crypto;
            var catLev = (int)risk.GetCategorySettings(cat).MaxLeverage;
            var leverage = Math.Max(1, cfg.LeverageCap > 0 ? Math.Min(catLev, cfg.LeverageCap) : catLev);

            // Pro Slot gekapselt: ein Fehlschlag (z.B. 100410 ohne Retry bei Order-Eroeffnung,
            // Insufficient Margin) darf die restlichen Slots nicht abbrechen — der Slot bleibt
            // leer und der naechste Durchlauf (Rebalance/Drift-Refill) versucht es erneut.
            try
            {
                await ex.SetLeverageAsync(symbol, leverage, side).ConfigureAwait(false);

                var notional = perSlotMargin * leverage;
                var qty = notional / price;
                if (qty <= 0m) continue;
                if (!ex.MeetsMinimumOrder(symbol, qty, price))
                {
                    skippedMin++;
                    log($"Rebalance: {symbol} {side} unter Min-Order (qty {qty:F6} @ {price:F4}) → Slot leer.");
                    continue;
                }

                var order = await ex.PlaceOrderAsync(new OrderRequest(symbol, side, OrderType.Market, qty), price).ConfigureAwait(false);
                if (order.Status == OrderStatus.Rejected)
                {
                    skippedMin++;
                    log($"Rebalance: {symbol} {side} Order abgelehnt ({order.RejectionReason ?? "?"}).");
                    continue;
                }
                opened++;
                filled.Add(symbol);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exn)
            {
                skippedMin++;
                log($"Rebalance: {symbol} {side} Order fehlgeschlagen ({exn.Message}) → Slot leer, naechster Durchlauf versucht erneut.");
            }
        }

        return new RebalanceResult(closed, opened, skippedMin, failedClose, filled);
    }
}
