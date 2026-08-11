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
    int Closed, int Opened, int SkippedMinOrder, int FailedClose, IReadOnlySet<string> Filled,
    IReadOnlyList<Position>? FailedClosePositions = null,
    int Resized = 0);

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
        Action<Position>? onClosed = null,
        int basketSlots = 0,
        TimeSpan? closeSettleDelay = null,
        IReadOnlySet<string>? doNotOpen = null,
        IReadOnlyDictionary<string, decimal>? weights = null)
    {
        log ??= _ => { };
        // Sizing-Divisor = tatsaechliche Korbgroesse (Backtest-Paritaet: CrossSectionalMomentumEngine
        // gewichtet 1/target.Count). RiskSettings.MaxOpenPositions gehoert NICHT in den Xsec-Pfad:
        // der Scalper-Default 3 halbierte den Divisor bei 6 Ziel-Slots — jeder Slot wurde doppelt so
        // gross dimensioniert wie validiert, waehrend die Open-Schleife trotzdem ALLE Slots eroeffnete
        // (Live-Oversizing-Befund 11.08.2026). Der Drift-Refill uebergibt die Soll-Korbgroesse explizit
        // via basketSlots, weil sein target auch Fremd-Schutz-Eintraege enthaelt, die keine Korb-Slots sind.
        var slots = basketSlots > 0 ? basketSlots : target.Count;

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
        //    Settle-Retry: BingX reflektiert Market-Closes teils erst Sekunden spaeter im Positions-
        //    Snapshot (dokumentiertes Muster "Position-Retry nach Market-Order"). Ohne Warten entstehen
        //    zwei teure False-Positives: (a) die eben geschlossene Position erscheint noch → failedClose
        //    → der Hedge-Guard blockt das Ziel-Open fuer den ganzen Zyklus; (b) AvailableBalance ist noch
        //    der Vor-Close-Wert (≈0 bei vollem Korb) → perSlotMargin ≈0 → alle Opens unter Min-Order →
        //    Korb bleibt leer trotz bezahlter Close-Fees. Der Account-Snapshot fuers Sizing (Schritt 3)
        //    wird deshalb erst NACH dem stabilen Positions-Snapshot gelesen. Echte Close-Fails (Exception,
        //    z.B. TradFi ausserhalb der Handelszeiten) stehen nicht in closeAttempts — auf sie wird
        //    nicht gewartet.
        var settleDelay = closeSettleDelay ?? TimeSpan.FromSeconds(2);
        var after = await ex.GetPositionsAsync(ct).ConfigureAwait(false);
        for (var settleAttempt = 0; settleAttempt < 3 && closeAttempts.Count > 0; settleAttempt++)
        {
            var stillSettling = after.Any(p => closeAttempts.Any(c =>
                string.Equals(c.Symbol, p.Symbol, StringComparison.OrdinalIgnoreCase) && c.Side == p.Side));
            if (!stillSettling) break;
            if (settleDelay > TimeSpan.Zero)
                await Task.Delay(settleDelay, ct).ConfigureAwait(false);
            after = await ex.GetPositionsAsync(ct).ConfigureAwait(false);
        }
        var held = new HashSet<string>();
        var filled = new HashSet<string>();
        var failedClose = 0;
        var failedClosePositions = new List<Position>();
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
                failedClosePositions.Add(pos);   // Aufrufer fuehrt sie in der Fehl-Close-Retry-Liste
                log($"Rebalance: Close fehlgeschlagen — {pos.Symbol} {pos.Side} noch offen. Slot bleibt belegt, naechster Durchlauf versucht erneut.");
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
            return new RebalanceResult(closed, 0, 0, failedClose, filled, failedClosePositions);
        var perSlotMargin = equity * cfg.MarginUtilization / slots;
        var opens = target
            .Where(kv => !held.Contains($"{kv.Key}_{kv.Value}") && doNotOpen?.Contains(kv.Key) != true)
            .Select(kv => kv.Key)
            .ToList();
        // Per-Symbol-Margin: equal-weight (Default) ODER explizite Gewichte (Anteil an
        // equity×MarginUtilization; DominanceSpread: BTC 0.5, Shorts je 0.5/ShortK). Der
        // Free-Margin-Cap skaliert bei Gewichten alle Opens proportional (statt pro Slot).
        decimal MarginFor(string symbol) =>
            weights != null && weights.TryGetValue(symbol, out var w)
                ? equity * cfg.MarginUtilization * w
                : perSlotMargin;
        var capFactor = 1m;
        if (opens.Count > 0 && acc.AvailableBalance > 0m)
        {
            if (weights == null)
                perSlotMargin = Math.Min(perSlotMargin, acc.AvailableBalance * 0.95m / opens.Count);
            else
            {
                var needed = opens.Sum(MarginFor);
                if (needed > 0m) capFactor = Math.Min(1m, acc.AvailableBalance * 0.95m / needed);
            }
        }

        // 3b. Gehaltene Ziel-Positionen auf ihr Gewicht nachziehen (NUR bei expliziten Gewichten).
        //     Ohne diesen Schritt behaelt eine bereits korrekt gehaltene Position ihre ALTE Groesse:
        //     beim Wechsel Momentum → DominanceSpread schleppte der Korb eine Momentum-Position mit
        //     ~8-facher Slot-Groesse mit (live 11.08.2026: ein einziger Short trug 57 % der
        //     Short-Seite). Equal-weight (weights == null) bleibt bewusst unangetastet — dort ist
        //     jede Position per Konstruktion gleich dimensioniert.
        //     Fremd-/Schutz-Positionen (doNotOpen) werden nie angefasst: sie gehoeren dem User.
        var resized = 0;
        var topUpBudget = 0m;
        if (weights != null)
        {
            topUpBudget = Math.Max(0m, acc.AvailableBalance * 0.95m - opens.Sum(MarginFor) * capFactor);
            foreach (var pos in after)
            {
                ct.ThrowIfCancellationRequested();
                if (!target.TryGetValue(pos.Symbol, out var want) || want != pos.Side) continue;
                if (doNotOpen?.Contains(pos.Symbol) == true) continue;
                if (!weights.TryGetValue(pos.Symbol, out var w) || w <= 0m) continue;

                var price = prices.TryGetValue(pos.Symbol, out var p) && p > 0m ? p : pos.MarkPrice;
                if (price <= 0m || pos.Quantity <= 0m) continue;
                var leverage = pos.Leverage > 0m ? pos.Leverage : 1m;
                var targetQty = equity * cfg.MarginUtilization * w * leverage / price;
                if (targetQty <= 0m) continue;

                // Toleranz: kleine Abweichungen (Mark-Drift, Tick-Rundung) nicht wegtraden —
                // jede Korrektur kostet Taker-Fee und Slippage.
                var deltaQty = targetQty - pos.Quantity;
                if (Math.Abs(deltaQty) / targetQty <= ResizeTolerance) continue;

                // Die Ziel-Groesse selbst muss handelbar bleiben, sonst entstuende eine Dust-Position
                // (bzw. ein Reject) statt einer sauber gewichteten.
                if (!ex.MeetsMinimumOrder(pos.Symbol, targetQty, price)) continue;

                var adjustQty = Math.Abs(deltaQty);
                if (!ex.MeetsMinimumOrder(pos.Symbol, adjustQty, price))
                {
                    log($"Rebalance: {pos.Symbol} {pos.Side} Gewichts-Korrektur {adjustQty:F6} unter Min-Order → bleibt bei {pos.Quantity:F6}.");
                    continue;
                }

                try
                {
                    if (deltaQty < 0m)
                    {
                        await ex.ClosePartialAsync(pos.Symbol, pos.Side, adjustQty).ConfigureAwait(false);
                        log($"Rebalance: {pos.Symbol} {pos.Side} auf Zielgewicht verkleinert "
                            + $"({pos.Quantity:F6} → {targetQty:F6}).");
                    }
                    else
                    {
                        // Aufstocken bindet zusaetzliche Margin — nur aus dem Rest, der nach den
                        // geplanten Opens frei bleibt (der Account-Snapshot ist aelter als die
                        // Downsizes dieses Durchlaufs, deshalb bewusst konservativ).
                        var needed = adjustQty * price / leverage;
                        if (needed > topUpBudget)
                        {
                            log($"Rebalance: {pos.Symbol} {pos.Side} Aufstockung uebersprungen (freie Margin reicht nicht) — naechster Durchlauf.");
                            continue;
                        }
                        await ex.PlaceOrderAsync(new OrderRequest(pos.Symbol, pos.Side, OrderType.Market, adjustQty), price)
                            .ConfigureAwait(false);
                        topUpBudget -= needed;
                        log($"Rebalance: {pos.Symbol} {pos.Side} auf Zielgewicht aufgestockt "
                            + $"({pos.Quantity:F6} → {targetQty:F6}).");
                    }
                    resized++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exn)
                {
                    log($"Rebalance: Gewichts-Korrektur {pos.Symbol} {pos.Side} fehlgeschlagen ({exn.Message}) — Groesse bleibt, naechster Durchlauf erneut.");
                }
            }
        }

        // 4. Ziel-Positionen oeffnen, die noch nicht gehalten werden — ALTERNIEREND Short/Long:
        //    die fruehere Insertion-Order (alle Longs zuerst, MomentumBasketCalculator) liess bei
        //    knapper Margin (Free-Margin-Cap, Fees/Mark-Drift zwischen den Market-Orders) bevorzugt
        //    die zuletzt platzierten SHORTS scheitern — der market-neutrale Korb war netto long,
        //    ausgerechnet im Abverkauf. Alternierend trifft ein Margin-Engpass beide Seiten gleich.
        var opened = 0;
        var skippedMin = 0;
        foreach (var (symbol, side) in InterleaveBySide(target))
        {
            ct.ThrowIfCancellationRequested();
            if (held.Contains($"{symbol}_{side}")) continue;

            // Schutz-Eintraege (Fremd-Positionen, unbestaetigt fehlende Korb-Symbole): stehen im
            // Ziel nur, damit Close-vor-Open sie nicht schliesst — eroeffnet wird hier NIE.
            if (doNotOpen?.Contains(symbol) == true) continue;

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

                var notional = MarginFor(symbol) * capFactor * leverage;
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

        return new RebalanceResult(closed, opened, skippedMin, failedClose, filled, failedClosePositions, resized);
    }

    /// <summary>
    /// Relative Abweichung, ab der eine gehaltene Position auf ihr Zielgewicht nachgezogen wird
    /// (25 %). Darunter waeren Fees + Slippage teurer als der Gewichtungs-Fehler.
    /// </summary>
    private const decimal ResizeTolerance = 0.25m;

    /// <summary>
    /// Ordnet die Ziel-Eintraege alternierend Short/Long (S,L,S,L,…) statt in Dictionary-
    /// Insertion-Order (alle Longs zuerst) — Margin-Knappheit trifft so beide Seiten
    /// gleichmaessig statt systematisch die zuletzt platzierten Shorts.
    /// </summary>
    private static List<KeyValuePair<string, Side>> InterleaveBySide(IReadOnlyDictionary<string, Side> target)
    {
        var longs = new List<KeyValuePair<string, Side>>();
        var shorts = new List<KeyValuePair<string, Side>>();
        foreach (var kv in target)
            (kv.Value == Side.Sell ? shorts : longs).Add(kv);
        var result = new List<KeyValuePair<string, Side>>(target.Count);
        for (var i = 0; i < Math.Max(longs.Count, shorts.Count); i++)
        {
            if (i < shorts.Count) result.Add(shorts[i]);
            if (i < longs.Count) result.Add(longs[i]);
        }
        return result;
    }
}
