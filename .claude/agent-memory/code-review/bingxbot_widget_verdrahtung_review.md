---
name: BingXBot Widget-Verdrahtung Review
description: 4 Findings (2 kritisch gefixt): FearGreed nie gesetzt, DailyPnl Race Condition, CorrelationMatrix toter Code, CLAUDE.md veraltet
type: project
---

## Review-Ergebnis (06.04.2026)

**Scope**: DashboardView Widget-Verdrahtung, CollectionChanged-Fix, UpdateRollingMetrics, Renderer

### Gefixt

1. **FearGreedValue/FearGreedLabel nie gesetzt** (KRITISCH): BtcTickerViewModel hatte die Properties, TradingServiceBase cached den Wert in `_cachedFearGreedIndex`, aber nichts propagierte ihn ans VM. Fix: `CachedFearGreedIndex` Property auf TradingServiceBase exponiert, `GetFearGreedValueFromService()` in `UpdateRollingMetrics()` integriert, setzt BtcTicker.FearGreedValue/Label auf UI-Thread.

2. **DailyPnl Dictionary Race Condition** (KRITISCH): `UpdateDailyPnlFromTrades` lief auf Timer-Thread (via PeriodicTimer -> SaveEquitySnapshotAsync -> UpdateRollingMetrics), PnlCalendarRenderer las DailyPnl auf UI-Thread. `Dictionary<>` ist nicht thread-safe. Fix: Snapshot-Pattern - Daten auf Timer-Thread vorbereiten (BuildDailyPnlSnapshot/BuildStrategyWeightsSnapshot), Mutation nur auf UI-Thread via Dispatcher.Post.

### Offen

3. **CorrelationMatrix toter Code**: Properties + Renderer existieren, aber kein Widget in DashboardView.axaml und Properties werden nirgends befuellt. Sollte entfernt oder implementiert werden.

4. **BtcPriceChartRenderer in CLAUDE.md**: Renderer-Tabelle referenziert noch den geloeschten BtcPriceChartRenderer (ersetzt durch InteractiveChartRenderer).

### Gut geloest

- CollectionChanged benannte Handler + OnDetached Abmeldung
- Statische SKPaint/SKFont in allen Renderern (keine Frame-Allocations)
- canvas.LocalClipBounds statt e.Info.Width/Height

**Why:** Widget-Daten wurden auf falschem Thread mutiert und FearGreed-Gauge war permanent leer.
**How to apply:** Bei zukuenftigen Widget-Ergaenzungen immer pruefen: (1) Werden Daten auf UI-Thread mutiert? (2) Werden Properties tatsaechlich irgendwo gesetzt?
