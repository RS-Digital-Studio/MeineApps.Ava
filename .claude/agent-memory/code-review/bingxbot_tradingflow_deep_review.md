---
name: BingXBot Trading-Flow + Backtest Deep Review
description: 10 Findings (3 krit): TP2-Qty-Formel-Divergenz, Chaotic-Regime-Divergenz, Backtest TP1 OriginalQuantity
type: project
---

## Deep Review Trading-Flow + Backtest-Engine (10.04.2026)

10 Findings (3 kritisch, 2 hoch, 3 mittel, 2 niedrig). Fokus auf Divergenzen Live/Paper/Backtest.

### Kritische Divergenzen

1. **Backtest TP1 closeQty aus OriginalQuantity** (BacktestEngine.cs:338): Backtest nutzt `exitState.OriginalQuantity`, Live nutzt `pos.Quantity`. Nach Partial-Fills divergieren die Ergebnisse. Backtest schliesst zu viel bei TP1.

2. **Chaotic-Regime: Backtest schliesst, Live nur warnt** (BacktestEngine.cs:539 vs TradingServiceBase.cs:393): Fundamentale Verhaltensdivergenz. Backtest-PnL systematisch besser als Live.

3. **TP2-Qty-Formel divergiert** (BacktestEngine.cs:499 vs TradingServiceBase.cs:632): Backtest normalisiert auf Original (`Tp2CloseRatio / (1 - Tp1CloseRatio)`), Live nicht (`pos.Quantity * Tp2CloseRatio`). Live laesst 70% trailing, Backtest 57%.

### Bekannte Divergenzen Paper vs Live

- Paper setzt Preis exakt auf SL/TP-Level (kein Slippage bei SL-Trigger), Live nutzt Market-Order (Slippage moeglich)
- SimulatedExchange Limit-Order-Fills ohne Margin-Check (koennen negative Balance erzeugen)

**Why:** Jede Divergenz zwischen Backtest und Live bedeutet dass Backtest-Optimierung auf falschen Daten basiert. Trading-Entscheidungen die auf Backtest-Ergebnissen beruhen koennen im Live-Betrieb anders ausfallen.

**How to apply:** Bei JEDER Aenderung an der Exit-Logik IMMER alle 3 Codepfade pruefen: BacktestEngine, TradingServiceBase, SimulatedExchange.
