---
name: BingXBot Cross-Cutting Review April 2026
description: Gründliches Review über 6 Libraries. 11 Findings (4 kritisch, 3 hoch). MultiMode Recovery ohne Standard-SL, RateLimiter uncancellable, FeatureEngine Stale-State, Partial-Close falsche Quantity
type: project
---

Review vom 07.04.2026. Cross-Cutting über alle 6 Libraries + Desktop-App.

**Kritisch (4):**
1. MultiModeOrchestrator.RecoverOpenPositionsAsync setzt KEINEN Standard-SL bei fehlendem SL/TP — loggt nur "KEIN SL" + Position bleibt ungeschützt. LiveTradingManager.RecoverOpenPositionsAsync berechnet ATR-basierte SL und setzt sie. Multi-Mode-Recovery ist damit gefährlicher.
2. SendSignedRequestAsync übergibt CancellationToken.None an RateLimiter — EmergencyStop wartet unbegrenzt wenn Rate-Limit-Slot blockiert, obwohl _cts bereits gecancelt ist.
3. LiveTradingService.OnSlTpHitAsync: Bot-seitig erkannter SL/TP → Prüft "Position noch da?" → Wenn Ja: CancelNativeSlTpOrders + ClosePosition. Zwischen CancelNativeSlTpOrders und ClosePosition gibt es kein Retry. Wenn ClosePosition API-Exception wirft, sind native SL/TP gelöscht UND Position offen und ungeschützt.
4. Partial-Close TP2-Quantity-Berechnung: tp2CloseQty = remainingQty * (Tp2CloseRatio / (1 - Tp1CloseRatio)). Bei Default-Werten 0.3/(1-0.3) = 0.428..., bei remainingQty=70% → Ergebnis 0.428*70% ≈ 30% korrekt. ABER wenn Tp1 teilweise gefüllt wurde (echte remainingQty ≠ 70%), ist die Formel falsch — sie schliesst zu viel oder zu wenig.

**Hoch (3):**
5. FeatureEngine: 6 statische volatile Felder (_btcReturn24h, _btcTrend, _marketSentiment, _fearGreedIndex, _btcCorrelations, _openInterestChanges) werden von TradingServiceBase.UpdateCrossMarketFeaturesAsync gesetzt. Im Multi-Mode setzen 3 Services parallel verschiedene Werte → Regime-Erkennung sieht inkonsistenten Zustand (BTC-Return von Service A, Sentiment von Service B).
6. RateLimiter implementiert NICHT IDisposable, obwohl pro Kategorie SemaphoreSlim-Instanzen allokiert werden. LiveTradingManager.Dispose() disposed _rateLimiter nicht (ist null-setzbar aber ohne Dispose). Bei mehrfachem Connect/Disconnect entstehen verwaiste SemaphoreSlim.
7. EmergencyStopAsync in LiveTradingService: Liest Positionen + Tickers mit normalen API-Calls BEVOR Positionen geschlossen werden. Wenn GetPositionsAsync oder GetAllTickersAsync durch den RateLimiter blockiert (CancellationToken.None!), dauert der Notfall-Stop möglicherweise Sekunden statt sofort.

**Mittel (3):**
8. CalculateStandardSlAsync Fallback: fallbackPercent = 0.03/leverage. Bei Leverage 3 → 1% SL. Bei volatilen Coins mit Spread >1% kann der SL sofort getriggert werden. Mindestabstand fehlt.
9. ExitOptimizer Verlierer-TP-Formel: tp = tp * 0.8f + (tp - (AvgLosingTp - tp) * 0.2f) * 0.2f. Wenn AvgLosingTp = 2*tp → tp * 0.8 + (tp - tp*0.2) * 0.2 = 0.96*tp (minimal). Wenn AvgLosingTp = 10*tp → tp * 0.8 + (tp - 9*tp*0.2) * 0.2 = 0.8tp + (-0.8tp)*0.2 = 0.64tp. Floor-Clamp 0.5 schützt, aber 0.5x ATR als TP ist extrem eng.
10. OnBreakevenSetAsync hat keinen Retry bei API-Fehler — anders als OnTrailingStopMovedAsync (3 Retries) und OnEnterTrailingPhaseAsync (2 Retries). Position verliert Breakeven-Schutz bei transientem API-Fehler.

**Niedrig (1):**
11. RateLimiter WaitForSlotAsync: Nach dem Warten (Task.Delay) wird der älteste Timestamp dequeued, aber der neue Timestamp wird NACH dem Semaphore-Release enqueued. Eigentlich wird er VOR dem Release enqueued (im finally-Block Release), aber logisch: Wenn während des Waits ein anderer Thread ebenfalls wartet, könnten beide denselben Slot beanspruchen da der Dequeue + Enqueue nicht atomar ist. In der Praxis unkritisch wegen Semaphore-Schutz.

**Positives:**
- Trailing-Stop per AddOrUpdate auf ConcurrentDictionary korrekt atomarisiert (früherer Fix)
- ATI RegisterStrategies nur 1x pro StartPaper/StartLive (früherer Fix)
- Nativer SL auf BingX als Sicherheitsnetz bei App-Crash — durchgängig implementiert
- Recovery-Karenz für wiederhergestellte Positionen (4h bevor Time-Exit greift)
- Retry-Logik in OnTrailingStopMovedAsync (3 Versuche) und OnEnterTrailingPhaseAsync (2 Versuche)

**Why:** Cross-Cutting Review mit Fokus auf Live-Trading-Sicherheit. Die 4 kritischen Findings können direkt zu Geldverlust führen.

**How to apply:**
1. MultiMode Recovery MUSS Standard-SL berechnen (wie LiveTradingManager)
2. CancellationToken durch die gesamte API-Chain durchreichen
3. ClosePosition Retry nach fehlgeschlagenem Cancel
4. OnBreakevenSetAsync braucht Retry-Logik
