---
name: BingXBot Trading-Modi Review
description: Review der 3 Trading-Modi (Scalping/DayTrading/Swing), Settings-Persistenz, M15-Entry-Timing, Limit-Order Cancel
type: project
---

Review 06.04.2026: 3 Findings (2 kritisch, 1 Verbesserung), alle gefixt.

**Kritisch gefixt:**
1. LiveTradingManager.StartAsync() wendet kein Preset auf CryptoTrendProStrategy an -- Live-Trading lief immer mit Swing-Defaults egal was im UI gewaehlt war
2. App.RestoreSettingsFromDb() vergisst `LastTradingModePreset` -- nach Neustart immer Swing
3. App.SaveAllSettingsAsync() ohne Concurrency-Schutz -- parallele fire-and-forget Aufrufe aus OnSelectedStrategyChanged + OnSelectedTradingModeChanged

**Why:** Preset-Anwendung nur im Paper-Pfad (DashboardViewModel.StartPaperTradingAsync), nicht im Live-Pfad (LiveTradingManager.StartAsync). Typisches Copy-Paste-Vergessen bei zwei getrennten Start-Pfaden.

**How to apply:** Bei BingXBot immer beide Pfade (Paper + Live) pruefen wenn Logik vor Bot-Start eingefuegt wird.
