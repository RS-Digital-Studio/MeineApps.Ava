# BingXBot - Trading Bot für BingX Perpetual Futures

Automatisierter Trading Bot mit modularem Strategie-System, Market Scanner, Backtesting und Paper-Trading.

## Status

| Eigenschaft | Wert |
|-------------|------|
| Version | v1.0.0 |
| Status | Entwicklung |
| Plattform | Desktop (Windows + Linux) |
| Exchange | BingX Perpetual Futures (USDT-M) |

---

## SK-System Buch-Refactoring (12.04.2026) — STRIKT 1:1 TRADEBOOK-KONFORM

Vollständiges Refactoring des SK-Systems auf reine Buch-Konformität nach "Tradebook SK-System" (Sascha Wenzel, Stefan Kassing). Alle Bot-spezifischen Erweiterungen entfernt. Dies ist der neue Source of Truth für das SK-System.

### Architektur (Buch-konform)

```
Chart-Hierarchie (Buch S.15):
  Übergeordnet: Weekly → Daily → H4 → H1 (Marktanalyse)
  Untergeordnet: M30 (Primär-Entry-Chart)

Sequenz: Punkt 0 → Punkt A (Impulsende) → Punkt B (Korrektur 50-66.7%) → Punkt C (161.8-200% Ext.)
```

### Entfernt (nicht im Buch)
- **Holy Trinity Multi-Tier** (Tier 2/3): komplett raus, nur 1 Tier
- **M5/M1-Candles**: nicht mehr geladen
- **Externe Confluence**: BinancePublicClient, ExternalMarketData, Funding, OI, Long-Short, Liquidationen, BTC-Dominanz, CoinGecko — alles gelöscht
- **BTC-Health-Score**: MarketFilter.CalculateBtcHealth nicht mehr aufgerufen
- **20+ Confluence-Quellen** → reduziert auf 3-5 Buch-Bestätigungen
- **`_consecutiveFailsInDirection`** Bottom-Up Feedback: raus
- **B-Punkt Position-Scale**, **SK Score-Bonus-Scaling**: raus
- **Gestaffeltes Min-RRR** (1.5/2.0/2.5/3.0): → nur min 1:1 (Buch)
- **ATR-dynamische SL**: → feste Pip-Werte
- **Asset-Kategorie-Multiplikatoren**: einheitlich

### Implementiert (Buch-Features)
- **M30 Primär-Entry-Chart**: `ScanHelper`, `TradingServiceBase` lädt M30 statt M15 für SK
- **Weekly-Fahrplan**: `MarketContext.WeeklyCandles`, `DetermineFahrplanBias()` prüft W1-GKL zuerst
- **Entry an Fib-Leveln (Buch S.16, Cheat 50)**: `ComputeFibEntry()` wählt bestes Fib-Level der H4-NavSeq (50/55.9/61.8/66.7). Primary: nächstes Level im Retracement-Richtung; Additional: 66.7% (Deep-GKL)
- **SL strikt nach Buch (Cheat 36, S.13, Workflow 6.9)**: `CalculateBookStopLoss()` — 78.6% Retracement der 0→A-Range, gecappt bei Markt-Pips (20/40/100), niemals über Punkt 0 hinaus
- **Feste Pip-SL-Cap**: `PipStopLossCalculator.cs` — Hauptwährungen/Metalle -20, Indices/Öl -40, Krypto -100, GBP/Exoten +50%
- **TP = 200% + 20 Pips Buffer** (Workflow 4.5): TP2 = `navSeq.Extension200 ± Get20PipsBuffer()`. TP1 (Partial 50%) bei 161.8% (Buch-Priorität 4)
- **5-Pips-Toleranz vor 161.8er** (Workflow 6.5): `Sequence.HasReachedTarget()` nutzt 0.03% Toleranz
- **BE-Regel nach Buch** (Cheat 53, Workflow 4.1-4.3, S.18): SL halbieren bei 1× SL-Distanz (4.1), BE einmal bei 2× SL-Distanz mit Entry+Spread (4.2+S.18), KEIN weiteres Nachziehen (4.3)
- **Verlust-Ausgleichs-TP** (Workflow 6.1+6.2): Wenn Unrealized-Gewinn ≥ Tagesverluste → Auto-Close (Gewinne am gleichen Tag)
- **BCKL als eigenständiger Re-Entry** (Workflow 6.6): Nach 100er-Extension-Touch eigener Entry am BCKL-Level + SL unter M30-PointB
- **Multi-Entry Staffelung**: 50er voll + 66.7er halb (via `IsAdditionalEntry` im Signal)
- **BE-Exit → sofortiger Re-Entry** (Workflow 6.8): BE-Ausstoppung erkannt, KEIN 4h-Cooldown
- **Limit-Order-Expiry Buch-konform** (Workflow 5.3): Kein 5-Min-Timeout mehr — Order läuft bis Preis den Invalidation-Level (= SL ≈ Point0) erreicht. 48h Hard-Expiry als Safety-Net bei Daten-Ausfall

### Geänderte Dateien
| Datei | Änderung |
|-------|----------|
| `BingXBot.Engine/Strategies/SequenzKonzeptStrategy.cs` | **Komplett neu** (~500 Zeilen statt 1358), strikt Buch-konform |
| `BingXBot.Engine/Risk/PipStopLossCalculator.cs` | NEU — feste Pip-SL pro Asset-Klasse |
| `BingXBot.Core/Models/MarketContext.cs` | M5/M1/ExternalData raus, WeeklyCandles ergänzt |
| `BingXBot.Core/Models/SignalResult.cs` | TradingTier + Tp1CloseRatioOverride + PositionScaleOverride + SourceTier raus, IsAdditionalEntry neu |
| `BingXBot.Core/Configuration/RiskSettings.cs` | MaxMarginPerTradePercent 10→1 (Buch: 1%), Tier-Positionen raus |
| `BingXBot.Core/Configuration/ScannerSettings.cs` | EnableTier2/3, UseM15EntryTiming raus |
| `BingXBot.Core/Configuration/TradingModeDefaults.cs` | UseM15EntryTiming raus, Swing-Preset SK-konform |
| `BingXBot.Core/Models/PositionExitState.cs` | SlHalved Flag neu |

### Gelöschte Dateien
- `BingXBot.Engine/External/` (ganzer Ordner) — BinancePublicClient
- `BingXBot.Core/Models/ExternalMarketData.cs`
- `BingXBot.Engine/Strategies/BollingerStrategy.cs`, `BreakoutPullbackStrategy.cs`, `CryptoTrendProStrategy.cs`, `EmaCrossStrategy.cs`, `GridStrategy.cs`, `MacdStrategy.cs`, `RsiStrategy.cs`, `TrendFollowStrategy.cs` — alle Non-SK-Strategien entfernt
- `tests/BingXBot.Tests/Backtest/LiveBacktestRunner.cs` — Integration-Test gegen CryptoTrendPro mit Reflection-Fields, die nicht mehr existieren

### Cleanup (12.04.2026, 8. Verifikation — strikt 1:1 Tradebook)
- **ATI-Infrastruktur komplett entfernt** (~5700 LOC): `BingXBot.Engine/ATI/` (Ensemble, ConfidenceGate, ExitOptimizer, RegimeDetector, LightGBM, ONNX, WalkForwardOptimizer, FeatureEngine, AdaptiveTradingIntelligence), `BingXBot.Core/Models/ATI/` (EnsembleVote, FeatureSnapshot, MarketRegime, TradeAudit), `FeatureSnapshotEntity`, `AtiLearningRenderer`
- **Non-Buch RiskSettings-Felder entfernt**: UseAdaptiveLeverage, EnableTrailingStop, TrailingStopPercent, EnableMultiStageExit, MaxHoldHoursAfterTp1, SmartBreakevenAtrMultiplier, EnableCooldownEscalation, MaxCooldownHours, EnableEquityCurveTrading, EquityCurvePeriod, EnableMomentumDecay, MaxNetExposurePercent, ConsiderFundingRate, MaxAdverseFundingRatePercent
- **TradingServiceBase Non-Buch-Logik entfernt**: Chandelier-Trailing, Momentum-Decay-Exit, Auto-Breakeven auf Leverage-Basis, Smart-BE mit ATR-Puffer, Fear & Greed Fetch, BTC-Korrelation, Open-Interest-Tracking, `CheckM15EntryTiming`, Cross-Market-Features, EquityCurveScaleFactor, Symbol-Cooldowns (Buch 6.8: sofort Re-Entry)
- **Walk-Forward-Optimizer UI** aus BacktestViewModel raus
- **Regime-Hintergrund** aus InteractiveChartRenderer (RegimeZone, DrawRegimeBackground)
- **UI-Widgets entfernt**: ATI-Reset-Button, Strategy-Weights-Widget, ATI-Lernfortschritt-Widget, Fear & Greed Gauge, Regime-Breakdown im Backtest, Trailing-Stop-Sektion in RiskSettingsView
- **PositionExitState Zombie-Felder raus**: ExtremePriceSinceEntry, TrailingAtrMultiplier, CurrentAtr, ConfluenceScore, Tp2Closed, ExitPhase.Trailing
- **BotSettings Ati-Felder raus**: AtiMinTradesBeforeLearning, AtiAutoSaveIntervalMinutes
- **Engine.csproj**: Microsoft.ML, Microsoft.ML.LightGbm, Microsoft.ML.OnnxRuntime, GeneticSharp PackageReferences entfernt
- **DB-Migration v7**: DROP TABLE FeatureSnapshots + DELETE AtiState-Key (Cleanup alter Installs)
- Kommentare "SK Holy Trinity" → "SK-System" (Buch-konformer Name)

### TP-Recovery nach App-Neustart (12.04.2026)
**Problem**: `_pendingLimitOrders` war nur In-Memory → App-Neustart zwischen Limit-Order-Platzierung und Fill → Fill-Detection verloren → TP-Limit-Orders nie platziert → Position lief ohne TP bis SL.

**Fix**: 3-Schichten-Lösung:
1. **`PendingLimitOrderState`** (BingXBot.Core/Models): Persistierbarer Zustand mit OrderId, TP1/TP2, InvalidationLevel
2. **DB-Persistenz** (BotDatabaseService): `SavePendingLimitOrdersAsync`/`LoadPendingLimitOrdersAsync` — wird bei Stop/EmergencyStop gespeichert
3. **Recovery** (LiveTradingManager):
   - `RestorePendingLimitOrders()` beim Start → stellt `_pendingLimitOrders` + `_positionSignals` wieder her
   - `RecoverMissingTpOrdersAsync()` nach Position-Recovery → prüft ob gefüllte Positionen ohne TP-Orders existieren → platziert TP1/TP2 nach
   - Pending-Orders in DB werden nach erfolgreicher Recovery gelöscht

**Geänderte Dateien**:
| Datei | Änderung |
|-------|----------|
| `BingXBot.Core/Models/PendingLimitOrderState.cs` | NEU — Persistierbares Model für pending Limit-Orders |
| `BingXBot.Shared/Services/BotDatabaseService.cs` | Save/Load/Clear für PendingLimitOrders |
| `BingXBot.Shared/Services/LiveTradingService.cs` | `GetPendingLimitOrdersSnapshot()`, `RestorePendingLimitOrders()`, `RecoverTpOrdersAsync()` |
| `BingXBot.Shared/Services/LiveTradingManager.cs` | Pending-Orders bei Start laden, bei Stop speichern, `RecoverMissingTpOrdersAsync()` |
| `BingXBot.Shared/Services/MultiModeOrchestrator.cs` | Pending-Orders bei Stop/EmergencyStop speichern/clearen |

### Pending-Order-Abgleich + SK-Recovery-Flags (13.04.2026)

**Problem 1 (Pending-Orders)**: Beim Start wurden alle DB-gespeicherten Pending-Orders blind wiederhergestellt — auch wenn sie auf BingX längst gefüllt/gecancelt/expired waren. Resultat: "15 Pending-Orders wiederhergestellt" ohne dass eine davon noch auf BingX existierte. Invalidierungs-Cleanup im PriceTickerLoop räumt zwar nach ~5s auf, aber UI + Logs zeigen irreführende Info und bei Race-Conditions können TP-Orders für bereits geschlossene Positionen platziert werden.

**Fix 1**: `LiveTradingManager.ReconcilePendingLimitOrdersAsync()` — liest `GetOpenOrdersAsync()` VOR dem Restore, vergleicht OrderIds und verwirft stale Einträge. DB wird mit bereinigter Liste überschrieben. Bei API-Fehler: Fallback auf ungefilterte Liste (best-effort, Invalidierung greift im PriceTickerLoop).

**Problem 2 (SK-Recovery-Flags)**: `RestorePositionSignal()` überschrieb das aus DB geladene Original-Signal mit einem neuen Recovery-Signal (aus BingX-Orders). Dabei gingen `DisableSmartBreakeven`, `TakeProfit2` und `IsAdditionalEntry` verloren. Konsequenz: SK-BE-Regel (Workflow 4.1/4.2) griff nach Neustart nicht mehr, TP2 war weg.

**Zusätzlich**: BingX gibt Limit-TPs (von `PlaceTpReduceOnlyLimitAsync` → Type=LIMIT) nicht als `TakeProfitMarket`/`TakeProfitLimit` zurück. Recovery-Code ignorierte sie also und setzte `tpPrice=null`.

**Fix 2**: `RestorePositionSignal()` prüft ob bereits ein ExitState mit Original-Signal geladen wurde und übernimmt `TakeProfit ?? original.TakeProfit`, `TakeProfit2 ?? original.TakeProfit2`, `DisableSmartBreakeven`, `IsAdditionalEntry`. Bei vorhandenem ExitState wird nur die Signal-Referenz aktualisiert (SL kommt aus BingX = Ground-Truth, Flags bleiben erhalten).

**Geänderte Dateien**:
| Datei | Änderung |
|-------|----------|
| `BingXBot.Shared/Services/LiveTradingManager.cs` | `ReconcilePendingLimitOrdersAsync()` — BingX-Abgleich VOR RestorePendingLimitOrders |
| `BingXBot.Shared/Services/TradingServiceBase.cs` | `RestorePositionSignal()` merged SK-Flags + TP2 aus ExitState, aktualisiert `existingState.Signal` |

### TP-Platzierung robust + verifiziert (13.04.2026)

**Problem**: Log zeigte "TP1 Limit platziert" aber auf BingX waren keine TP-Orders zu sehen. Mehrere Ursachen möglich:
1. **Position nicht registriert**: Nach Market-Order braucht BingX 1-3s bis `GetPositionsAsync()` die neue Position listet → `actualQty=fallbackQty` → im Hedge-Mode kann `positionSide=LONG` + `side=SELL` rejected werden weil Position noch nicht existiert
2. **Stumme API-Erfolge**: `PlaceTpReduceOnlyLimitAsync` gibt OrderId zurück, aber Order taucht nicht im Orderbuch auf (unwahrscheinlich, aber möglich bei Race)
3. **Einmal-Rejection ohne Retry**: API-Fehler oder Race-Conditions führen zu Rejection die nicht erneut versucht wird
4. **Log-Verwirrung bei Limit-Entry**: Trade-Log zeigt "TP1=... | TP2=..." direkt nach Entry-Platzierung, aber bei Limit-Entry wird TP erst NACH Fill platziert → User sucht vergeblich im BingX-Orderbuch

**Fix**:
1. **Position-Retry** in `PlaceTpLimitOrdersAfterFillAsync`: 3 Versuche mit 1s Delay bis Position bei BingX verfügbar. Nach 3s keine Position → Warning + Abbruch (PriceTickerLoop übernimmt als Bot-seitiger Fallback)
2. **`PlaceTpWithRetryAsync`**: Einzelne TP-Platzierung mit 3 Retries (1.5s Delay zwischen Versuchen). Detailliertes Error-Log bei Rejection inkl. OrderId nach Erfolg
3. **Verify nach Platzierung**: `GetOpenOrdersAsync(symbol)` liest Orderbuch, prüft ob TP1/TP2 OrderIds tatsächlich existieren. Bei Abweichung: Error-Log mit Hinweis auf Bot-seitigen Fallback
4. **Log-Klarheit bei Limit-Pending**: Entry-Log zeigt jetzt explizit "TP1=..., TP2=... werden erst NACH Fill auf BingX platziert (Maker-Fee, nicht jetzt sichtbar im Orderbuch)" — verhindert Miss-Interpretation

**Geänderte Datei**:
| Datei | Änderung |
|-------|----------|
| `BingXBot.Shared/Services/LiveTradingService.cs` | Position-Retry + TP-Retry + Verify + klarere Limit-Pending-Logs |

### Signal-Verlust-Bug bei langen Limit-Orders (13.04.2026) — KRITISCH

**Problem (User-Report mit Screenshot)**: Position auf BingX ohne SL/TP sichtbar, Log zeigt endlos:
```
LIVE: PUMP-USDT Limit gefüllt @ 0,00179200, aber Signal noch nicht registriert — retry nächster Tick
```

**Root-Cause**: Verwaist-Signal-Cleanup in `OnBeforePriceTickerIteration` entfernte Signale nach 30s wenn keine Position existierte. Limit-Orders können aber Minuten/Stunden pending bleiben (Preis erreicht Limit nicht sofort).

**Ablauf**:
1. `t=0s`: Limit-Entry platziert → `_positionSignals[key]` + `_pendingLimitOrders[symbol]` gesetzt
2. `t=0-30s`: Limit-Order pending, keine Position bei BingX
3. `t>30s`: Verwaist-Cleanup entfernt Signal (keine Position da)
4. `t=N Minuten`: Limit-Order gefüllt → Position existiert
5. PriceTickerLoop: Fill erkannt + `_pendingLimitOrders`-Eintrag → Signal-Check → nicht da → "retry" endlos
6. TP wird nie platziert, Position läuft ohne Schutz bis SL

**Fix 1 — Pending-Orders vom Verwaist-Cleanup ausnehmen**:
```csharp
var symbol = key.Split('_')[0];
if (_pendingLimitOrders.ContainsKey(symbol))
    continue; // Signal NICHT entfernen wenn Limit-Order pending
```

**Fix 2 — Signal-Rekonstruktion nach 30s bei Fill ohne Signal**:
- Signal rekonstruieren: `StopLoss = InvalidationLevel`, `TakeProfit = null`, `DisableSmartBreakeven = true`
- Nativen SL auf BingX setzen (`SetPositionSlTpAsync`)
- Warning loggen (TP unbekannt, manuelle Überwachung)
- `_pendingLimitOrders` entfernen

**Limitation**: `_pendingLimitOrders`-Tuple speichert keine TP-Werte — nur `(OrderId, PlacedAt, InvalidationLevel, IsLong)`. Bei Signal-Verlust geht der TP verloren. Mittelfristig: Tuple durch `PendingLimitOrderState` ersetzen das TP1/TP2 trägt.

**Geänderte Datei**:
| Datei | Änderung |
|-------|----------|
| `BingXBot.Shared/Services/LiveTradingService.cs` | Pending-Ausnahme im Verwaist-Cleanup + Signal-Rekonstruktion bei Fill ohne Signal |

### Buch-Parameter-Tabelle (einheitlich für alle Assets)
| Parameter | Wert | Quelle |
|-----------|------|--------|
| Entry-Chart | M30 | Buch S.15 |
| Navigator-Chart | H4 | Buch S.15 (H1-Analyse) |
| Filter-Chart | H1 | Buch S.15 |
| Golden Pocket | 50-66.7% | Buch S.16 |
| Min-Aktivierung | 38.2% Extension | Buch Workflow |
| Ziel | 161.8-200% | Buch S.16 |
| SL Hauptwährungen/Metalle | -20 Pips | Buch S.13 |
| SL Indices/Öl | -40 Pips | Buch S.13 |
| SL Krypto | -100 Pips | Buch S.13 |
| SL GBP/Exoten | +50% (je 30 Pips) | Buch S.13 |
| Min CRV | 1:1 | Buch S.13 |
| Max Risiko/Trade | 1% | Buch S.13 (1-3%) |
| Max offene Positionen | 3 | Buch Workflow |
| TP1 Close-Ratio | 50% | Buch-Praxis |
| TP2 Close-Ratio | 50% (Rest) | Buch |
| Confluence-Bestätigungen | 3-4 | Buch S.23 |

### Ausnahmen (explizit nicht implementiert nach User-Entscheidung)
- **Tages-Risiko-Limit** (Buch: 1-3%/Tag): `MaxDailyDrawdownPercent = 0` (deaktiviert)
- **Gleicher-Tag-Exit als Zeit-Regel** (Buch: Gewinne am gleichen Tag realisieren): `MaxHoldHours = 0` (unbegrenzt). Workflow 6.2 wird stattdessen indirekt via Verlust-Ausgleichs-TP (Workflow 6.1) umgesetzt

### Plan-Datei
Vollständiger Plan: `SK_BUCH_REFACTOR_PLAN.md` im App-Root.

---

## Architektur

4 Libraries + Desktop-App:

```
BingXBot.Core        <- Domain (Models, Enums, Interfaces, DB-Entities)
BingXBot.Exchange    <- BingX REST + WebSocket API Client
BingXBot.Engine      <- Trading-Logik (Strategien, Scanner, Risk, Indikatoren mit Struct-Cache)
BingXBot.Backtest    <- Backtesting + Paper-Trading + SimulatedExchange (unter Simulation/)
BingXBot.Shared      <- Avalonia UI (ViewModels inkl. Sub-VMs, Views, Services mit TradingServiceBase)
BingXBot.Desktop     <- Desktop Entry-Point
```

---

## Strategien

Nach Buch-Refactoring (12.04.2026) ist das **SK-System die einzige Strategie**. Alle vorherigen Strategien (CryptoTrendPro, TrendFollow, EMA Cross, RSI, Bollinger, MACD, Grid, Breakout-Pullback) wurden entfernt.

| Strategie | Datei | Logik |
|-----------|-------|-------|
| **SK-System** | SequenzKonzeptStrategy.cs | Stefan-Kassing-Trading-System (Buch-konform): M30 Entry-Chart, H4 Navigator, H1 Filter, Weekly Fahrplan, feste Pip-SL, 3-4 Confluence-Bestätigungen. Details siehe "SK-System Buch-Refactoring" oben. |

### MarketFilter (Engine/Filters/MarketFilter.cs)

Globale Filter die VOR der Strategie-Evaluation greifen:
- **Session-Filter**: 24/7 Krypto, Funding-Settlement ±5min Pause für ALLE BingX-Perpetuals (Krypto + TradFi haben Funding)
- **Cooldown**: Symbol-spezifischer 4h-Cooldown nach Verlust-Trade (nicht bei BE-Exit — Buch-Regel 6.8)
- **Max Trades/Tag**: Default 0 (unbegrenzt)
- **BTC-Health-Score-Blocking**: ENTFERNT (war nicht Buch-konform, blockierte zu viele Setups)

### Aktuelle Defaults

| Setting | Wert |
|---------|------|
| Timeframe | H4 |
| Scan-Intervall | 15min (dynamisch per Timeframe) |
| Leverage | 3x |
| Risiko/Trade | 1% (SK: Buch S.13) |
| Daily Drawdown | 0% (deaktiviert) |
| Total Drawdown | 10% |
| Min Volume | 20M |
| Max Kandidaten | 100 (SK-Reversal-Screening) |
| TP1 Close | 50% (SK-Buch) |
| TP2 Close | 50% Rest (SK-Buch) |
| Min RRR | 1:1 (SK-Buch S.13, Strategy hat eigenen Check) |
| BE-Regel | SK-Buch Workflow 4.1/4.2 (SL halbieren → BE einmal → kein Nachziehen) |
| Max Hold Hours | 0 (deaktiviert, SL/TP managed Exit) |
| Max Korrelation | 0.85 |
| Backtest Slippage | Dynamisch (ATR/Volume) |
| Backtest Spread | 0.08% (Bid-Ask) |

---

## Trading-Services (TradingServiceBase Architektur)

Gemeinsame Basisklasse `TradingServiceBase` enthält die gesamte Trading-Logik:
- **RunLoopAsync** (15min): Ticker → Scanner → Klines → Strategie → Risk → Order
- **PriceTickerLoopAsync** (5s): SL/TP-Check, SK-BE-Regel (Workflow 4.1/4.2), TP1-Partial-Close, Verlust-Ausgleichs-TP, Preis-Updates, TradFi-Stunden-Check
- Tageswechsel-Reset, Korrelations-Check, gemeinsame Signal-Verwaltung
- `_klineSemaphore` als Klassenfeld (SemaphoreSlim(5), kein Handle-Leak)
- `_tickerPriceMap` als ConcurrentDictionary (PriceTickerLoop + RunLoop parallel)
- Datei: `Services/TradingServiceBase.cs`

### PaperTradingService (erbt von TradingServiceBase)
- Nutzt `SimulatedExchange` (BingXBot.Backtest.Simulation) als Backend
- `MarginType.Isolated` (spiegelt Live-Modus)
- Datei: `Services/PaperTradingService.cs`

### LiveTradingService (erbt von TradingServiceBase)
- Nutzt `BingXRestClient` für echte Orders
- WebSocket User-Data-Stream mit Auto-Reconnect (exponentieller Backoff)
- `SetMarginTypeAsync(symbol, Isolated)` VOR jeder Order (try-catch: BingX-Fehler bei offener Position ignorieren)
- Kill-Switch: `ActivateKillSwitchAsync(120s)` alle 60s. Bei sauberem Stop: `DeactivateKillSwitchAsync()`
- Datei: `Services/LiveTradingService.cs`

### LiveTradingManager (Infrastruktur-Orchestrator)
- Kapselt Live-Trading-Lifecycle: Connect, Start, Stop, EmergencyStop
- Position-Signal-Wiederherstellung nach App-Neustart (ExitStates + RuntimeState aus DB)
- Commission-Rates beim Connect aus BingX-API laden (VIP-Level-abhängig)
- Server-Zeit-Sync (`SyncServerTimeAsync()`) bei Connect — BingX Error 100421 bei >5s Abweichung
- Datei: `Services/LiveTradingManager.cs`

### MultiModeOrchestrator
- Verwaltet 3 parallele Trading-Modi: Scalping (M15), DayTrading (H1), Swing (H4)
- Pro-Modus RiskSettings via `CreateRiskSettings(mode)` (Haltezeit, Leverage, Cooldown)
- `GetAggregatedPaperAccountAsync()` summiert Balance/Positionen aller 3 Paper-Services
- Recovery-Signale NUR im ERSTEN Service registrieren (sonst N-facher Close-Versuch)
- EmergencyStopAllAsync: Live-Mode ruft direkt `_restClient.CloseAllPositionsAsync()` (atomarer BingX-Endpoint)
- `Task.Run(() => StopAsync())` statt direktem `Wait()` in Dispose — sonst Deadlock bei SynchronizationContext
- ExitStates + RuntimeState werden in StopAllAsync und EmergencyStopAllAsync persistiert
- Datei: `Services/MultiModeOrchestrator.cs`

---

## Exchange-Features

### BingX API
- **Native SL/TP**: `stopLoss`/`takeProfit` als JSON-String, Typ `STOP_MARKET`/`TAKE_PROFIT_MARKET`, `workingType: MARK_PRICE`
- **Kill-Switch**: `ActivateKillSwitchAsync(120s)` alle 60s. Bot-Crash → BingX cancelt nach 2min alle Orders
- **Commission-Rates**: Beim Connect aus BingX-API laden (nicht hardcoden — VIP-Levels)
- **Fund-Flow**: `GetIncomeHistoryAsync()` — REALIZED_PNL, FUNDING_FEE, TRADING_FEE
- **Order-Amendment**: `AmendOrderAsync()` — atomar ändern (kein Cancel+Replace, keine SL-Lücke). `RoundPrice`/`TruncateQuantity` anwenden
- **Server-Zeit-Sync**: `SyncServerTimeAsync()` bei Connect. `startTime.Value.ToUniversalTime()` für API-Calls
- **Balance v3**: `/openApi/swap/v3/user/balance` — Array, nach `asset=="USDT"` filtern
- **Trailing-Stop-Sync**: `OnTrailingStopMovedAsync()` mit Throttle (max 1 Update/30s pro Symbol)
- **WebSocket**: `_sendLock` (SemaphoreSlim) für alle Send-Aufrufe — `SendAsync` ist NICHT thread-safe

### Ordertypen
- **Market-Order**: Default. Taker-Fee 0.05% (VIP-abhängig)
- **Limit-Order**: `PreferLimitOrder=true` bei Score >= 10. Maker-Fee 0.02%. TP NICHT sofort platzieren — Fill-Detection im PriceTickerLoop
- **closeAllPositions**: Ein API-Call pro Symbol (effizienter bei mehreren Positionen)

---

## Multi-Asset Trading / TradFi-Support

### Neue Dateien (seit 08.04.2026)
| Datei | Beschreibung |
|-------|--------------|
| `Core/Enums/MarketCategory.cs` | Enum: Crypto, Commodity, Index, Forex, Stock |
| `Core/Helpers/SymbolClassifier.cs` | Prefix-basierte Klassifikation: `NCCO`=Commodity, `NCSI`=Index, `NCFX`=Forex, `NCSK`=Stock, Rest=Crypto |
| `Engine/Filters/TradingHoursFilter.cs` | Markt-Öffnungszeiten: Krypto 24/7, Forex 24/5, Commodity/Index Mo-Fr mit 1h Pause 22:00-23:00 UTC |

### TradFi-Symbole (BingX, 103 Stück, verifiziert 13.04.2026 via Live-API)
- **Commodities** (23): `NCCOGOLD2USD-USDT`, `NCCOXAG2USD-USDT`, `NCCO1OILWTI2USD-USDT` etc.
- **Indices** (11): `NCSINASDAQ1002USD-USDT`, `NCSISP5002USD-USDT`, `NCSIDOWJONES2USD-USDT`
- **Forex** (27): `NCFXEUR2USD-USDT`, `NCFXGBP2USD-USDT` etc.
- **Stocks** (42): `NCSKTSLA2USD-USDT`, `NCSKNVDA2USD-USDT`, `NCSKAAPL2USD-USDT`, `NCSKMSFT2USD-USDT`, `NCSKMETA2USD-USDT`
- Gleiche API-Endpunkte wie Krypto (Klines, Ticker, Orders)
- Top-Liquidität: GOLD (494M Volume), WTI Öl (43M+33M), SP500 (22M), NASDAQ100 (18M)

### Scan-Aufteilung (60% Krypto / 40% TradFi mit Sub-Quoten, ab 13.04.2026)
- `ScanHelper.FilterCandidates()` reserviert bei `MaxResults=100` → 60 Slots Krypto + 40 Slots TradFi
- **Sub-Quoten innerhalb TradFi**: 10 Rohstoffe + 10 Indices + 10 Forex + 10 Aktien (25% pro Subkategorie)
  - Ohne Sub-Quoten dominierten Aktien (22/40 = 55%) und Indices verschwanden (2/40 = 5%)
  - Mit Sub-Quoten Live-Verteilung: 10 Rohstoffe / 9 Indices / 11 Forex / 10 Aktien (gleichverteilt)
- **Slot-Recycling**: Ungenutzte Sub-Slots (z.B. Indices-Pool nur 9) gehen an Top-Volume-TradFi der anderen Subkategorien
- **Cross-Recycling**: Ungenutzte TradFi-Slots fallen an Krypto zurück (und umgekehrt)
- TradFi sortiert nach Volume24h pro Subkategorie, Krypto Fisher-Yates-Shuffle (faire Rotation)
- Verifiziert via `tests/BingXBot.Tests/Integration/TradFiLiveVerification.cs` (9 Tests, alle grün)

### Per-Markt Risk-Settings
| Kategorie | Default-Leverage | Max-Leverage | Margin | RRR |
|-----------|-----------------|-------------|--------|-----|
| Krypto | 3x | 125x | 20% / 2% | 1.5:1 |
| Rohstoffe | 10x | 500x | 15% / 1.5% | 1.5:1 |
| Indices | 10x | 500x | 15% / 1.5% | 1.5:1 |
| Forex | 20x | 500x | 10% / 1% | 2:1 |
| Aktien | 3x | 25x | 15% / 2% | 1.5:1 |

### Strategie-Anpassungen
- **Scanner ATR%-Schwellen**: Category-abhängig — Forex 0.05-0.5%, Stock 0.3-2%, Krypto 1-4%
- **SK MinRangePercent**: Category-abhängig — Forex 0.25x, Stock 0.5x, Index 0.4x, Commodity 0.6x
- **PriceTickerLoop**: TradFi-Positionen bei geschlossenem Markt überspringen (stale Preise → falsches SL/TP)
- **Funding-Settlement**: Gilt für ALLE BingX-Perpetuals (Krypto + TradFi) — globaler Block in `MarketFilter.CheckSession()`

### ScannerSettings (TradFi-relevante)
| Property | Default | Beschreibung |
|----------|---------|--------------|
| EnableTradFi | true | TradFi-Assets aktivieren |
| EnabledCategories | {Crypto,Commodity,Index,Forex,Stock} | Welche Kategorien gescannt werden |
| MinVolume24hTradFi | 1M | Eigener Volume-Filter für TradFi |
| MinPriceChangeTradFi | 0.1% | Eigener PriceChange-Filter für TradFi |
| OnlyTopByVolume | true | Nur Top-N Coins nach 24h-Volume (Krypto) |
| TopCoinsCount | 100 | Anzahl Top-Coins |

---

## DB-Persistenz (BotDatabaseService)

SQLite-basierte Persistenz (WAL-Modus für Multi-Mode Concurrency):

| Bereich | DB-Nutzung |
|---------|------------|
| BacktestViewModel | Trades NICHT in DB (fluten sonst History bei jedem Run) |
| TradeHistoryViewModel | Lädt Paper+Live-Trades beim Start aus DB. `SaveTradeAsync` immer mit try-catch |
| RiskSettingsViewModel | Speichert/lädt RiskSettings in/aus DB |
| DashboardViewModel | Equity-Snapshots alle 5 Min, ExitStates, RuntimeState |
| BotDatabaseService | Schema-Versioning: `RunMigrationsAsync()`. Aktuelle Version: v6 (Regime-Spalte + WAL-Modus) |

### Persistierte Zustände (für Neustart-Safety)
- `PositionExitState`: Phase (Initial/Tp1Hit), SlHalved, BreakevenSet, IsRecovered — kein doppelter TP1 nach Neustart
- `RuntimeState`: TradesToday, ConsecutiveLosses

---

## UI-Views

| View | Zweck | Engine-Verdrahtung |
|------|-------|--------------------|
| Dashboard | Balance, Positionen, Bot-Controls, Strategie-Auswahl, Equity-Chart, Live-Trading | BotEventBus, StrategyManager, PaperTradingService, BotSettings, LiveTradingManager, RiskSettings, ScannerSettings, IPublicMarketDataClient?, BotDatabaseService? + Sub-VMs: BtcTickerViewModel, ActivityFeedViewModel |
| Scanner | Live-Scan mit Volumen/Momentum-Filter | BotEventBus, ScannerSettings, IMarketScanner? |
| Strategie | Auswahl + dynamischer Parameter-Editor | BotEventBus, StrategyManager, IStrategy-Instanzen |
| Backtest | Historischer Test mit PerformanceReport | BotEventBus, BacktestEngine, RiskManager, SimulatedExchange |
| Trade-History | Alle Trades filterbar (Modus/Symbol/Zeitraum) | BotEventBus (TradeCompleted, BacktestCompleted) |
| Risk-Settings | Risiko-Parameter konfigurieren | BotEventBus, RiskSettings |
| Log | Live-Log mit Level/Kategorie-Filter | BotEventBus (LogEmitted) |
| Settings | API-Keys, Verbindung | BotEventBus, BotSettings, ISecureStorageService?, IExchangeClient? |

### SkiaSharp-Renderer
| Renderer | Datei | Beschreibung |
|----------|-------|--------------|
| EquityChartRenderer | Graphics/EquityChartRenderer.cs | Linien-Chart für Equity-Kurve |
| BtcPriceChartRenderer | Graphics/BtcPriceChartRenderer.cs | Candlestick-Chart für BTC-USDT |
| InteractiveChartRenderer | Graphics/InteractiveChartRenderer.cs | SK-Sequenz-Overlay (Punkt 0/A/B, Fibonacci) |

### Sub-ViewModels
- **BtcTickerViewModel**: BTC-USDT Preis + Candlestick-Chart. Auto-Refresh: Preis 10s, Candles 60s. Abschaltbar via `BotSettings.ShowBtcTicker`
- **ActivityFeedViewModel**: Letzte 20 Bot-Aktionen. Subscribet auf `BotEventBus.LogEmitted`. Farbcodiert: Rot=Error, Amber=Warning, Grün=Trade

---

## BotEventBus

`BotEventBus` (Singleton) ermöglicht ViewModel-zu-ViewModel-Kommunikation ohne direkte Referenzen:

| Event | Publisher | Subscriber |
|-------|-----------|------------|
| `TradeCompleted` | DashboardVM, PaperTradingService | TradeHistoryVM |
| `BacktestCompleted` | BacktestVM | TradeHistoryVM |
| `LogEmitted` | Alle ViewModels, TradingServiceBase | LogVM, ActivityFeedViewModel |
| `BotStateChanged` | DashboardVM, PaperTradingService | MainVM (Status-Bar) |
| `MarginWarning` | TradingServiceBase (PriceTickerLoop) | DashboardVM |

Datei: `Services/BotEventBus.cs`

---

## ViewModel-DI-Verdrahtung

| ViewModel | DI-Parameter |
|-----------|--------------|
| MainViewModel | BotEventBus |
| DashboardViewModel | BotEventBus, StrategyManager, PaperTradingService, RiskSettings, ScannerSettings, BotSettings, LiveTradingManager, MultiModeOrchestrator, IPublicMarketDataClient?, BotDatabaseService?, ISecureStorageService? |
| StrategyViewModel | StrategyManager, BotEventBus |
| BacktestViewModel | RiskSettings, BotEventBus, IPublicMarketDataClient?, BotDatabaseService? |
| TradeHistoryViewModel | BotEventBus, BotDatabaseService? |
| LogViewModel | BotEventBus |
| ScannerViewModel | ScannerSettings, BotEventBus, IMarketScanner?, IPublicMarketDataClient? |
| RiskSettingsViewModel | RiskSettings, BotEventBus, BotDatabaseService? |
| SettingsViewModel | BotSettings, BotEventBus, ISecureStorageService?, IExchangeClient? |

Optionale Parameter (mit `?`) ermöglichen Demo-Modus ohne Exchange-Verbindung.

**Strategie-Parameter-Rückschreibung**: StrategyViewModel schreibt UI-Parameter per Reflection zurück. Convention: UI-Name "FastPeriod" → privates Feld "_fastPeriod". Unterstützt int und decimal.

---

## Risikomanagement

- **Position-Sizing**: Risiko-basiert — `maxLoss / slDistance` (enger SL = größere Position). SL ist PFLICHT, ohne SL wird Trade abgelehnt
- **Drawdown-Limits**: Täglich + gesamt. Peak-Equity-Tracking für Total-Drawdown
- **Liquidation-Check**: Isolated-Margin-Formel: `(1 - MMR) / Leverage`. Bei <=2x Leverage deaktiviert
- **Netto-Exposure**: Shorts als negativ, `Math.Abs(net)`. Default: Max 200%
- **Korrelation**: Pearson auf Log-Returns (nicht absolute Preise — verhindert spurious Korrelation). Default: Max 0.85
- **Funding-Rate-Filter**: RiskManager-Ebene. Für alle BingX-Perpetuals (Krypto + TradFi)
- **Sharpe-Annualisierung**: `sqrt(TradesProJahr)` — NICHT sqrt(365) oder sqrt(252)
- **Rolling Live-Metriken**: 30-Trade-Window: WinRate, ProfitFactor, Sharpe. Strategy-Health-Warnung bei Degradation

---

## Tests

| Datei | Tests | Beschreibung |
|-------|-------|--------------|
| Core/ModelTests.cs | Models | Record-Erstellung, Enums |
| Core/ConfigTests.cs | Konfiguration | Settings-Defaults |
| Core/SimulatedExchangeTests.cs | SimulatedExchange | Order-Ausführung |
| Core/SymbolClassifierTests.cs | SymbolClassifier | Prefix-Erkennung, IsTradFi |
| Core/TimeFrameHelperTests.cs | TimeFrame-Konvertierung | IntervalString, Duration |
| Engine/EmaCrossStrategyTests.cs | EMA Cross | Signal-Generierung |
| Engine/StrategyTests.cs | Alle Strategien | Gemeinsame + strategie-spezifische Tests |
| Engine/StrategyFactoryTests.cs | StrategyFactory | Erstellung, Clone |
| Engine/StrategyManagerTests.cs | StrategyManager | Multi-Symbol |
| Engine/IndicatorHelperTests.cs | Indikatoren | EMA, RSI, BB, MACD, ADX, Stochastik, HTF-Trend |
| Engine/CorrelationCheckerTests.cs | Korrelation | Pearson, Log-Returns, Parallelisierung |
| Engine/MarketScannerTests.cs | Scanner | Volumen/Momentum-Filter |
| Engine/TradingEngineTests.cs | TradingEngine | Tick-Verarbeitung |
| Engine/RiskManagerTests.cs | RiskManager | Position-Sizing, Drawdown |
| Engine/TradingHoursFilterTests.cs | TradingHoursFilter | Wochenende, Handelszeiten, Krypto 24/7 |
| Engine/ScanRotationTests.cs | Scanner-Rotation | Wrap-Around-sicher |
| Engine/TradeJournalTests.cs | TradeJournal | Record, WinRate, ProfitFactor |
| Exchange/RateLimiterTests.cs | RateLimiter | Request-Throttling |
| Exchange/BingXRestClientTests.cs | REST-Client | API-Aufrufe |
| Backtest/BacktestEngineTests.cs | BacktestEngine | Run, Demo-Candles |
| Backtest/PerformanceReportTests.cs | PerformanceReport | Metriken, Drawdown |

---

## Build

```bash
dotnet build src/Apps/BingXBot/BingXBot.Desktop
dotnet run --project src/Apps/BingXBot/BingXBot.Desktop
dotnet test tests/BingXBot.Tests
```

---

## UI-Conventions

| Convention | Details |
|-----------|---------|
| Compiled Bindings | `x:CompileBindings="True"` in allen Views |
| Virtualisierung | ListBox + VirtualizingStackPanel in TradeHistory, Log, Backtest, Scanner |
| Monospace-Zahlen | `FontFamily="Consolas, Courier New, monospace"` für alle Preise/PnL/Metriken |
| Keyboard-Shortcuts | Ctrl+1-8 Navigation, F5/F6/F7/F12 Bot-Kontrolle, Escape → Dashboard |
| Tooltips | Alle Bot-Buttons, Nav-Items, Account-Karten, Risk-Settings |
| Farb-Palette | Alle Farben via DynamicResource aus AppPalette.axaml |
| PnL-Farbcodierung | IsVisible-Toggle mit SuccessBrush/ErrorBrush für PnL-Werte |
| Dark-Mode | `RequestedThemeVariant = ThemeVariant.Dark` in App.axaml.cs |
| Farbpalette | Primary #3B82F6, Background #1E1E2E, Profit #10B981, Loss #EF4444 |

---

## Aktuelle Gotchas (konsolidiert)

### BingX API
- Balance-Endpoint: MUSS v3 (`/openApi/swap/v3/user/balance`) — v3 liefert Array, nach `asset=="USDT"` filtern
- `SetMarginTypeAsync` VOR jeder Order — BingX-Default kann Cross sein (try-catch: Fehler bei offener Position ignorieren)
- Kill-Switch: `ActivateKillSwitchAsync()` alle 60s refreshen, bei sauberem Stop `DeactivateKillSwitchAsync()` aufrufen
- `SyncServerTimeAsync()` MUSS bei Connect aufgerufen werden — BingX Error 100421 bei Systemzeit-Abweichung >5s
- Commission-Rates beim Connect laden, nicht hardcoden — BingX hat VIP-Levels
- `AmendOrderAsync`: `RoundPrice`/`TruncateQuantity` anwenden — BingX lehnt Werte mit zu vielen Dezimalstellen ab
- Fund-Flow `incomeType`: REALIZED_PNL, FUNDING_FEE, TRADING_FEE, INSURANCE_CLEAR, ADL, TRANSFER
- `GetIncomeHistoryAsync`: `startTime.Value.ToUniversalTime()` — ohne UTC-Kind nutzt DateTimeOffset lokale Timezone
- Limit-Order TP: NICHT sofort platzieren (Position existiert noch nicht). Fill-Detection im PriceTickerLoop
- TP-Limit-Orders: Qty aus `GetPositionsAsync()` lesen (BingX truncated auf Symbol-Precision)
- WebSocket `_ws.SendAsync` ist NICHT thread-safe — SemaphoreSlim `_sendLock` für alle Send-Aufrufe

### Trading-Logik
- `_tradesToday` MUSS `volatile` sein — JIT darf nicht-volatile Felder bei parallelen Reads cachen
- `ContinueWith` IMMER mit `TaskScheduler.Default` — ohne expliziten Scheduler kann UI-Thread-Deadlock entstehen
- `OriginalQuantity` IMMER die tatsächlich platzierte Menge verwenden (nach Equity/Score-Scaling), NICHT `riskCheck.AdjustedPositionSize`
- EmergencyStop: CTS NICHT vor Close-Operations canceln (API-Calls brauchen funktionierendes HTTP)
- Recovery-Signale im Multi-Mode NUR in einem Service registrieren (sonst N-facher Close-Versuch)
- `DailyPnl` Dictionary: Atomarer Swap (neues Objekt zuweisen), NICHT Clear+Re-Fill (SkiaSharp-Renderer liest auf Render-Thread)
- `_klineSemaphore` in Dispose() freigeben — SemaphoreSlim hat OS-Handles
- Manueller Close: `_liveManager.CommissionTakerRate` statt hardcodierter 0.0005m — echte PnL für TradeHistory
- Backtest-Trades NICHT in DB speichern — fluten sonst History bei jedem Run
- SL ist PFLICHT im RiskManager — Trade ohne SL wird grundsätzlich abgelehnt
- `MultiModeOrchestrator.Dispose`: `Task.Run(() => StopAsync())` statt direktem `Wait()` — sonst Deadlock

### SK-System
- Sequenz-Nomenklatur: `Sequence.Point0` = SK Punkt 0, `Sequence.PointA` = SK Punkt A, `Sequence.PointB` = SK Punkt B (Nullable)
- C-Punkt MUSS im 50-66.7% Retracement liegen (SK-GKL). NICHT 38.2-78.6%
- Entry NIEMALS bei CorrectionZone — nur WaitingBreak (C als Swing bestätigt)
- `Retracement559` NUR als Confluence-Level, NICHT als Zone-Grenze. Zonen-Grenzen sind immer 50% und 66.7%
- BuyZone und GklZone in Sequence.cs sind identisch (50-66.7%). Semantisch unterschiedlich: BuyZone = BC-Korrektur, GKL = Gesamtkorrektur
- `CompletedGkls` speichert max 5 GKL-Zonen abgearbeiteter Sequenzen (mit Zeitstempel, NICHT in DB persistiert)
- GKL MUSS auf Gesamtstrecke (Point0→Extension1618) basieren — `_completedGkl500`/`_completedGkl667`
- State Machine `Abgearbeitet` MUSS auf `Suche0` resetten (sonst permanent stuck → 0 Trades)
- `ProcessSucheB`: Invalidierung VOR Aktivierung prüfen (eine Kerze kann beides haben)
- BOS: IMMER Close-Break prüfen (SK-Regel). BuildSequence berechnet NUR Fibonacci-Level, KEIN State
- `FromCandlesBoth()` gibt (primary, longMachine, shortMachine) zurück — Primary mit `_` verwerfen, direkte Richtungswahl
- TP1 = NavSeq Extension 161.8%, TP2 = NavSeq Extension 200% + 20 Pips Buffer. TP1-Ratio = 50% (Buch-Praxis)
- `DisableSmartBreakeven = true` bei SK-Trades → eigene BE-Regel (Workflow 4.1/4.2)
- `_signalCooldown` wird bei Signal auf 8 gesetzt und pro Evaluate dekrementiert — nach Ablauf neue M30-Entries in gleicher H4-Sequenz möglich
- Entry erlaubt bis 138.2% Extension (NICHT 100%). BC-Zone noch valid bis 138.2%
- Sandwich-Check NUR gegen aktive Gegensequenz (`SmState.Aktiviert`), NICHT historische

### TradFi
- Symbol-Erkennung: `NC`-Prefix = TradFi, Rest = Krypto
- Funding-Settlement: Gilt für ALLE BingX-Perpetuals (Krypto + TradFi) — globaler Block in `CheckSession()`
- `EnableTradFi` Fallback-Werte MÜSSEN `true` sein (konsistent mit ScannerSettings.EnableTradFi Default)
- `IsHedgeModeActive` MUSS gesetzt werden (Paper=true, Live=aus `restClient.IsHedgeModeAsync()`) — sonst TradFi komplett tot
- Single-Mode Paper: `_scannerSettings.IsHedgeModeActive = true` VOR `_paperService.Start()`
- Scanner-Rotation: `_rotationOffset % remaining.Count` für sauberes Wrap-Around
- TradFi am Wochenende IMMER geschlossen. Commodity/Index: 1h Pause 22:00-23:00 UTC

### Mathematik / Metriken
- ATR-Perzentil: `CalculateAtrPercentile()` verwenden — `atr/price*10000` ist KEIN Perzentil
- Sharpe-Ratio: `sqrt(TradesProJahr)` für Annualisierung, Sample-Varianz `N-1`
- Sortino: Downside-Deviation über ALLE Returns (positive als 0) — Standard-Formel

### Sicherheit
- API-Keys: DPAPI (Windows) / AES-256-CBC + PBKDF2 100k Iterationen (Linux)
- Linux credentials.dat: `chmod 600` nach Schreiben
- Keine Secrets in Logs, Keys in UI maskiert
- HTTP-Error-Content auf 200 Zeichen kürzen

---

## Historische Fix-Zusammenfassung

Vor dem 12.04.2026 Buch-Refactoring gab es 10+ Verifikations-Sessions (09.03. bis 11.04.2026) mit insgesamt mehreren Hundert Einzel-Fixes — vollständige Details in der Git-History. Die wichtigsten Erkenntnisse sind in den obigen Abschnitten und in den Gotchas verankert. Kurz-Zusammenfassung der relevantesten Lessons:

- **Race Conditions**: ConcurrentDictionary für `_positionSignals`, `_tickerPriceMap`, `_fundingRates`. `volatile` für `_tradesToday`, `_consecutiveLosses`. Interlocked für Zähler
- **Thread-Safety**: `SemaphoreSlim` für WebSocket-SendAsync, IndicatorHelper-Cache, Klines-Parallel-Load. Locks für RollingMetrics
- **Ressource-Management**: HttpClient als Feld wiederverwenden. `_klineSemaphore` in Dispose. SimulatedExchange IDisposable. PaperTradingService disposed alte Exchange vor Neuerstellen
- **Backtest-Korrektheit**: Temporaler 80/20-Split (kein Random-Shuffle). CandleSlice statt GetRange (Zero-Copy). IndicatorHelper.ClearCache() alle 500 Iterationen. HTF-Candles für Trend-Konfirmation laden
- **Live-Trading-Sicherheit**: `SetMarginTypeAsync` VOR jeder Order. Kill-Switch alle 60s refreshen. Commission-Rates aus API. BE-Änderung auf BingX synchronisieren
- **EmergencyStop**: CTS NICHT vor Close-Operations canceln. `CloseAllPositionsAsync()` (atomarer BingX-Endpoint) statt N-faches paralleles Close
- **Multi-Mode**: Pro-Modus RiskSettings. Recovery-Signale nur in einem Service. `SuppressBotStateEvents` gegen Log-Spam
- **SK-System**: State Machine Abgearbeitet→Suche0 Reset war der gravierendste Bug (0 Trades ohne Fix). Sandwich-Check nur gegen aktive Sequenzen
- **Mathematik-Korrekturen**: Sharpe sqrt(TradesProJahr), Sample-Varianz N-1, Sortino über alle Returns, Liquidation-Formel `(1-MMR)/Leverage`
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        