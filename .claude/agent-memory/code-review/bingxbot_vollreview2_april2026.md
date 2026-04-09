---
name: BingXBot Vollreview 2 April 2026
description: 11 Findings (3 krit, 3 hoch, 3 mittel, 2 niedrig). RateLimiter Semaphore-Leak, EmergencyStop ohne ATI-Save, TryClaimAutoSave Zeitstempel-Race, Multi-Mode Recovery ohne SL, SimExchange/PaperService nicht disposed, StopAsync CTS-Order, FeatureEngine stale Daten, ExitOptimizer Formel-Instabilitaet
type: project
---

Zweites Vollreview vom 07.04.2026. ~65 Dateien, ~12.000 Zeilen. Prueft Fixes aus dem vorherigen Vollreview.

**Vorherige kritische Findings GEFIXT:**
- MultiModeOrchestrator ATI-Loop: RegisterStrategies() jetzt einmal vor Schleife
- SimulatedExchange Thread-Safety: _currentAtr/_currentVolumeRatio jetzt ConcurrentDictionary
- EquityHistory Race: Dedizierter _equityLock eingefuehrt

**Neue kritische Findings (3):**
1. RateLimiter (Exchange) implementiert kein IDisposable → SemaphoreSlims leaken
2. LiveTradingManager.EmergencyStopAsync() ruft SaveAtiStateAsync() nicht auf → ATI-Lernzustand verloren bei Notfall
3. TryClaimAutoSave setzt LastAutoSaveTime BEVOR DB-Save → bei DB-Fehler wird nie wieder versucht

**Hohe Findings (3):**
4. MultiModeOrchestrator.RecoverOpenPositionsAsync setzt keinen Standard-SL fuer ungeschuetzte Positionen (im Gegensatz zu LiveTradingManager)
5. PaperTradingService: Start() erstellt neue SimulatedExchange ohne alte zu disposen → ReaderWriterLockSlim Leak
6. LiveTradingService.StopAsync: _cts.Cancel() vor CleanupUserDataStreamAsync() → DeleteListenKey kann fehlschlagen

**Mittlere Findings (3):**
7. FeatureEngine statische Felder: Stale Daten bei fehlendem BTC-Ticker im Multi-Mode
8. ExitOptimizer Verlierer-TP-Formel: Konvergiert bei extremen Werten auf Floor (0.5) → kein Lerneffekt
9. _consecutiveLosses nicht thread-safe (parallel von RunLoop, PriceTickerLoop, ProcessCompletedTrade)

**Why:** Zweites umfassendes Review prueft ob vorherige Fixes korrekt sind und findet neue Probleme v.a. in Resource-Management und Recovery-Pfaden.

**How to apply:** Kritische Findings vor dem naechsten Live-Trading fixen. Besonderer Fokus auf Emergency-Pfade (dort ist Korrektheit am wichtigsten und wird am seltensten getestet).
