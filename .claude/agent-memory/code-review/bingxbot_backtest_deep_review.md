---
name: BingXBot Backtest+SimExchange Tiefes Review April 2026
description: 10 Findings (5 krit): Limit-Order Fee fehlt (noch offen), Pagination-Loop gleiche Daten, Ticker Bid/Ask=Candle Range, Funding auf MarkPrice statt Current, TP2 DivByZero
type: project
---

Tiefes Review vom 07.04.2026 ueber BacktestEngine, SimulatedExchange, PerformanceReport, Monte Carlo, CPCV.

**5 Kritische Findings:**
1. ExecuteOrderLocked (Limit-Fill) setzt _positionOpenFees nicht -- Opening-Fee fehlt im CompletedTrade, PnL zu optimistisch. (Bestaetigung aus vorigem Review, noch nicht gefixt)
2. LoadFromExchangeClientAsync Pagination: GetKlinesAsync(symbol, tf, batchSize) ohne Offset/Cursor -- jeder Batch liefert dieselben Daten. Loop laeuft batchCount-mal ohne neuen Fortschritt.
3. Ticker im Backtest: BidPrice=Candle.Low, AskPrice=Candle.High -- falsch, das ist der Candle-Range (1-5%), nicht der Spread (0.01-0.1%). Doppelte Spread-Beruecksichtigung wenn Strategie Ticker-Spread einkalkuliert.
4. ApplyFundingRate rechnet auf pos.MarkPrice statt aktuellem Preis -- MarkPrice ist veraltet (letzter Fill oder letztes GetPositionsAsync), verzerrt Funding-Kosten.
5. TP2-Quantity: Division durch (1-Tp1CloseRatio) ohne Guard -- DivByZero bei Tp1CloseRatio=1.0.

**3 Verbesserungen:**
6. Sharpe Ratio annualisiert mit sqrt(252) statt sqrt(TradesPerYear) -- falsch bei Intraday-TF.
7. Sortino Downside-Varianz nur ueber negative Returns statt alle Returns -- unterschaetzt Sortino.
8. positionRegimes nicht pro Trade gespeichert sondern per Key-Lookup -- falsches Regime bei mehreren Trades mit gleichem Symbol+Side.

**2 Hinweise:**
9. OrderRejectionPercent und MaxLatencyMs in Settings definiert aber nirgends verwendet.
10. Smart Breakeven ist eigentlich Profit-Lock (kein Bug, irrefuehrender Name).

**Why:** Tiefes Backtest-Review weil kein Trading-Bot profitabel sein kann wenn der Backtest falsch rechnet. Fehler in Fee-Tracking, Metriken und Markt-Simulation verfaelschen jedes Optimierungsergebnis.

**How to apply:**
- Limit-Fee: _positionOpenFees auch in ExecuteOrderLocked setzen
- Pagination: GetKlinesAsync mit from/to-Overload oder Offset verwenden
- Ticker: Realistischen Spread berechnen statt Candle-Range
- Funding: GetPriceLocked(pos.Symbol) statt pos.MarkPrice
- TP2: Guard gegen Tp1CloseRatio >= 1.0
- Sharpe: sqrt(TradesPerYear) statt sqrt(252)
- Sortino: Downside-Varianz ueber alle Returns
