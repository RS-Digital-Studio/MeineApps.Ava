---
name: BingXBot Trading-Logik Audit April 2026
description: Analyse der 7 Trading-Strategien, Risikomanagement, Backtest-Realismus und Indikator-Logik des BingXBot
type: project
---

## BingXBot Trading-Logik Audit (05.04.2026)

9 Findings, Gesamteindruck positiv. CryptoTrendPro ist die Star-Strategie.

### Kritische Findings
1. **Backtest bildet Multi-Stage Exit nicht ab** (BacktestEngine.cs) -- CryptoTrendPro-Ergebnisse unrealistisch
2. **Drawdown-Tracking ohne Peak-Equity** (RiskManager.cs) -- Risiko wird nach Gewinnphasen unterschaetzt
3. **Korrelation auf Preis-Level statt Returns** (CorrelationChecker.cs) -- spurious correlation

### Weitere Findings
- BTC-Kontext-Scoring maximal +1 statt dokumentierte +2 (longScore < 4 Bedingung)
- TrendFollow RRR nur 1.5:1 (marginal)
- MacdStrategy Histogram-SL 1.5x ATR zu eng
- MaxNetExposurePercent 300% bei 3x Leverage nicht schuetzend
- GridStrategy Bollinger fuer Grid-Grenzen suboptimal
- PerformanceReport fehlen Calmar/Sortino/ConsecutiveLosses

### Staerken
- CryptoTrendPro Confluence-Scoring 0-12 mit Gewichtung
- Vol-adaptive SL/TP via ATR-Perzentil
- Multi-Stage Exit (TP1 50%, BE-Move, Chandelier, TP2, Time-Exit)
- BTC Health Score + Session + Funding + Cooldown Filter
- Skender.Stock.Indicators mit Struct-Cache
- SimulatedExchange mit Partial Close, Funding, Slippage
