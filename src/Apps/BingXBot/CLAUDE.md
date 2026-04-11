# BingXBot - Trading Bot für BingX Perpetual Futures

Automatisierter Trading Bot mit modularem Strategie-System, Market Scanner, Backtesting und Paper-Trading.

## Status

| Eigenschaft | Wert |
|-------------|------|
| Version | v1.0.0 |
| Status | Entwicklung |
| Plattform | Desktop (Windows + Linux) |
| Exchange | BingX Perpetual Futures (USDT-M) |

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

## Strategien (8 Stück, CryptoTrendPro als Default)

| Strategie | Datei | Logik |
|-----------|-------|-------|
| **CryptoTrendPro** | CryptoTrendProStrategy.cs | **Primär-Strategie**: Supertrend + Confluence-Scoring (0-10) + Fibonacci + vol-adaptive SL/TP + Multi-Stage Exit |
| Trend-Following | TrendFollowStrategy.cs | Multi-Indikator (EMA+RSI+MACD+Volume), 5 Bedingungen, Confidence-basiert |
| EMA Cross | EmaCrossStrategy.cs | EMA-Cross + Volume + EMA200 Trend-Filter + ATR-Volatilitätsfilter |
| RSI Momentum | RsiStrategy.cs | RSI als Momentum-Indikator + Divergenz-Erkennung + Volume-Konfirmation |
| Bollinger Breakout | BollingerStrategy.cs | Squeeze-Erkennung + Breakout + Volume-Konfirmation |
| MACD | MacdStrategy.cs | Histogram-Momentum + Zero-Line-Cross + Trend-Kontext |
| Smart Grid | GridStrategy.cs | Dynamische Grenzen via Bollinger, nur in Range-Märkten (EMA+ATR Trend-Check) |

### CryptoTrendPro (Default-Strategie seit 04.04.2026)

Optimiert für Krypto-Futures 2024-2026. Confluence-Scoring statt binäre Bedingungen.

**Entry-Scoring (Long, max 10 Punkte intern):**
- +2: D1 Preis > EMA 50 (mittelfristiger Uptrend, via HTF-Candles)
- +2: H4 Supertrend(10, 3.0) bullish
- +1: H4 EMA 12 > EMA 26
- +1: H4 ADX > 20 UND steigend (+DI > -DI)
- +1: H4 RSI 45-80
- +1: H4 Volumen > 1.5x SMA(20)
- +1: HTF-Supertrend bullish (unabhängig vom D1>EMA)
- +1: Fibonacci-Retracement: Preis nahe Key-Level (38.2%/50%/61.8%, Toleranz 0.5*ATR)
- Extern (nicht im Score): Funding-Rate, Cooldown, BTC-Health via MarketFilter
- **Min. Score: 6 (Default), Large-Caps -2 Rabatt**
- Fib-Extension 161.8% als alternatives TP2 (wenn weiter als ATR-basiert)

**Pyramid Multi-Stage Exit (PositionExitState, seit 05.04.2026):**
- TP1 bei 2.5-3x ATR → **30%** Position schließen → SL auf **Smart Breakeven** (Entry + 0.5*ATR)
- TP2 bei 4.5-5x ATR → **30%** Position schließen → Rest trailing (kein TP mehr)
- Chandelier-Trailing (2.5x ATR unter Höchstpunkt) für verbleibende **40%**
- Time-Exit: 48h ohne TP1 → schließen
- **Regime-Exit**: ATI erkennt Chaotic → alle Positionen sofort schließen (PriceTickerLoop)
- ADX-Exit: ADX < 10 → Trend tot

**Volatilitäts-Adaptation (ATR-Perzentil):**
- Ruhig (<20%): SL 1.5x, TP1 2.0x, TP2 3.5x ATR
- Normal (20-75%): SL 1.8-2.0x, TP1 2.5-3.0x, TP2 4.5-5.0x ATR
- Volatil (75-90%): SL 2.5x, TP1 3.5x, TP2 6.0x ATR
- Extrem (>90%): Halbe Position, konservativere Multiplikatoren

### MarketFilter (Engine/Filters/MarketFilter.cs)

Globale Filter die VOR der Strategie-Evaluation greifen:
- **BTC Health Score** (-4 bis +4): D1>EMA50, H4 Supertrend, RSI, Funding (symmetrisch ±0.05%) → Long/Short/Position-Scale
- **Session-Filter**: 24/7 Krypto, Liquiditäts-Gewichtung (US/EU/Asia), Funding-Settlement ±5min Pause (Wrap-Around-sicher)
- **Funding-Rate**: >+0.08% blockiert Longs, <-0.05% blockiert Shorts
- **Cooldown**: 8h Pause nach Verlust-Trade
- **Max Trades/Tag**: Default 3
- **Volatilitäts-Bremse**: ATR >90. Perzentil → halbe Position

### Neue Defaults (05.04.2026, aktualisiert 11.04.2026)

| Setting | Alt | Neu |
|---------|-----|-----|
| Timeframe | H1 | **H4** |
| Scan-Intervall | 30s | **15min** (dynamisch per Timeframe) |
| Leverage | 10x | **3x** |
| Risiko/Trade | 2% | **1.5%** |
| Daily Drawdown | 5% | **0%** (deaktiviert) |
| Total Drawdown | 15% | **10%** |
| Trailing-Stop | 1.5% fix | **2.5x ATR** (Chandelier) |
| Min Volume | 10M | **20M** |
| Max Kandidaten | 10 | **100** (SK-Reversal-Screening) |
| TP1 Close | 50% | **30%** (Pyramid) |
| TP2 Close | - | **30%** (Pyramid) |
| Min RRR (global) | 1.0:1 | **0** (deaktiviert, Strategie hat eigenen Check) |
| Smart BE | Entry exakt | **Entry + 0.5*ATR** |
| Max Hold Hours | 48h | **0** (deaktiviert, SL/TP managed Exit) |
| Max Korrelation | 0.7 | **0.85** |
| Equity-Curve-Trading | An | **Aus** (Drawdowns normal bei SK) |
| Backtest Slippage | 0.05% fix | **Dynamisch** (ATR/Volume) |
| Backtest Spread | - | **0.08%** (Bid-Ask) |

Alle Strategien implementieren `IStrategy` mit `Clone()` für Multi-Symbol-Support via `StrategyManager`.

**Krypto-Optimierungen (alle Strategien):**
- Volume-Konfirmation (Signal nur bei überdurchschnittlichem Volumen)
- ATR-basierte SL/TP (angepasst an Krypto-Volatilität)
- Trend-Filter (kein Counter-Trend-Trading)
- Keine einfache Mean-Reversion (gefährlich bei Krypto-Trends)

**Strategie-Auswahl im Dashboard:**
- Dropdown im Bot-Control-Bereich
- Default: Trend-Following (beste Strategie für Krypto-Futures)
- Beschreibung wird automatisch angezeigt
- Gesperrt während Bot läuft

## Trading-Services (TradingServiceBase Architektur)

Gemeinsame Basisklasse `TradingServiceBase` enthält die gesamte Trading-Logik:
- **RunLoopAsync** (30s): Ticker → Scanner → Klines → Strategie → Risk → Order
- **PriceTickerLoopAsync** (5s): SL/TP-Check, Trailing-Stop, Preis-Updates
- Tageswechsel-Reset, Korrelations-Check, gemeinsame Signal-Verwaltung
- Abstrakte Methoden für exchange-spezifische Operationen
- Virtuelle Hooks für Live-spezifische Logik (Grace Period, 60s Fehler-Delay)
- Datei: `Services/TradingServiceBase.cs`

### PaperTradingService (erbt von TradingServiceBase)
- Nutzt `SimulatedExchange` als Backend
- ~130 Zeilen (vorher ~485 Zeilen)
- Datei: `Services/PaperTradingService.cs`

### LiveTradingService (erbt von TradingServiceBase)
- Nutzt `BingXRestClient` für echte Orders
- WebSocket User-Data-Stream (optional)
- ~280 Zeilen (vorher ~683 Zeilen)
- Datei: `Services/LiveTradingService.cs`

### LiveTradingManager (Infrastruktur-Orchestrator)
- Kapselt Live-Trading-Lifecycle: Connect, Start, Stop, EmergencyStop
- Erstellt BingXRestClient + LiveTradingService zur Laufzeit (API-Keys erst zur Laufzeit bekannt)
- ATI-State-Persistenz (Load/Save in DB)
- Position-Signal-Wiederherstellung nach App-Neustart
- Wiederverwendbarer HttpClient (vermeidet Socket-Exhaustion)
- Datei: `Services/LiveTradingManager.cs`

## Sub-ViewModels (aus DashboardViewModel extrahiert)

### BtcTickerViewModel
- Vollständig unabhängig - BTC-USDT Preis + Candlestick-Chart
- Auto-Refresh: Preis alle 10s, Candles alle 60s
- `IsEnabled` Property für abschaltbaren Ticker (via BotSettings.ShowBtcTicker)
- Datei: `ViewModels/BtcTickerViewModel.cs`

### ActivityFeedViewModel
- Vollständig unabhängig - Letzte 20 Bot-Aktionen
- Subscribet auf `BotEventBus.LogEmitted` (filtert Debug-Level aus)
- Farbcodiert: Rot=Error, Amber=Warning, Grün=Trade, Grau=Info
- Datei: `ViewModels/ActivityFeedViewModel.cs`

## Risikomanagement

- Position-Sizing: %-basiert, Kelly-Criterion, ATR-Sizing
- Drawdown-Limits: täglich + gesamt
- Max offene Positionen (global + pro Symbol)
- Korrelations-Check (Pearson)
- Trailing-Stop
- Alles konfigurierbar im UI

## UI-Views

| View | Zweck | Engine-Verdrahtung |
|------|-------|--------------------|
| Dashboard | Balance, Positionen, Bot-Controls, Strategie-Auswahl, Equity-Chart, Live-Trading | BotEventBus, StrategyManager, PaperTradingService, BotSettings, LiveTradingManager, RiskSettings, ScannerSettings, IPublicMarketDataClient?, BotDatabaseService? + Sub-VMs: BtcTickerViewModel, ActivityFeedViewModel |
| Scanner | Live-Scan mit Volumen/Momentum-Filter | BotEventBus, ScannerSettings, IMarketScanner (optional) |
| Strategie | Auswahl + dynamischer Parameter-Editor + Parameter-Rückschreibung | BotEventBus, StrategyManager, IStrategy-Instanzen |
| Backtest | Historischer Test mit PerformanceReport, publiziert Ergebnisse an TradeHistory + Log | BotEventBus, BacktestEngine, RiskManager, SimulatedExchange |
| Trade-History | Alle Trades filterbar (Modus/Symbol/Zeitraum), empfängt Trades von Bot + Backtest | BotEventBus (TradeCompleted, BacktestCompleted) |
| Risk-Settings | Risiko-Parameter konfigurieren | BotEventBus, RiskSettings (bidirektional) |
| Log | Live-Log mit Level/Kategorie-Filter, empfängt Logs von allen ViewModels | BotEventBus (LogEmitted) |
| Settings | API-Keys, Verbindung | BotEventBus, BotSettings, ISecureStorageService, IExchangeClient |

## SkiaSharp-Renderer

| Renderer | Datei | Beschreibung |
|----------|-------|--------------|
| EquityChartRenderer | Graphics/EquityChartRenderer.cs | Linien-Chart fuer Equity-Kurve (Profit/Loss-Farbgebung, Baseline) |
| BtcPriceChartRenderer | Graphics/BtcPriceChartRenderer.cs | Candlestick-Chart fuer BTC-USDT (75% Candles, 25% Volumen, Preis-Grid, Docht/Body) |

## BotEventBus (zentraler Event-Aggregator)

`BotEventBus` (Singleton) ermöglicht ViewModel-zu-ViewModel-Kommunikation ohne direkte Referenzen:

| Event | Publisher | Subscriber |
|-------|-----------|------------|
| `TradeCompleted` | DashboardVM (Bot-Trades), PaperTradingService | TradeHistoryVM |
| `BacktestCompleted` | BacktestVM | TradeHistoryVM |
| `LogEmitted` | Alle ViewModels, TradingServiceBase | LogVM, ActivityFeedViewModel |
| `BotStateChanged` | DashboardVM, PaperTradingService | MainVM (Status-Bar) |

Datei: `Services/BotEventBus.cs`

## ViewModel-DI-Verdrahtung

Alle ViewModels bekommen ihre Engine-Dependencies per Constructor Injection:

| ViewModel | DI-Parameter |
|-----------|--------------|
| MainViewModel | BotEventBus |
| DashboardViewModel | BotEventBus, StrategyManager, PaperTradingService, RiskSettings, ScannerSettings, BotSettings, LiveTradingManager, IPublicMarketDataClient?, BotDatabaseService?, AdaptiveTradingIntelligence? |
| StrategyViewModel | StrategyManager, BotEventBus |
| BacktestViewModel | RiskSettings, BotEventBus, IPublicMarketDataClient?, BotDatabaseService? |
| TradeHistoryViewModel | BotEventBus, BotDatabaseService? |
| LogViewModel | BotEventBus |
| ScannerViewModel | ScannerSettings, BotEventBus, IMarketScanner?, IPublicMarketDataClient? |
| RiskSettingsViewModel | RiskSettings, BotEventBus, BotDatabaseService? |
| SettingsViewModel | BotSettings, BotEventBus, ISecureStorageService?, IExchangeClient? |

Optionale Parameter (mit `?`) ermöglichen Demo-Modus ohne Exchange-Verbindung.

### Strategie-Parameter-Rückschreibung

StrategyViewModel schreibt UI-Parameter per Reflection zurück auf die IStrategy-Instanz:
- Convention: UI-Name "FastPeriod" wird auf privates Feld "_fastPeriod" gemappt
- Unterstützt int und decimal Parameter-Typen
- Wird bei "Aktivieren" und bei Strategie-Wechsel (wenn aktiv) angewendet

## Build

```bash
dotnet build src/Apps/BingXBot/BingXBot.Desktop
dotnet run --project src/Apps/BingXBot/BingXBot.Desktop
dotnet test tests/BingXBot.Tests
```

## DB-Persistenz (BotDatabaseService)

SQLite-basierte Persistenz für Trades, Equity-Snapshots, Logs und Settings:

| ViewModel | DB-Nutzung |
|-----------|------------|
| BacktestViewModel | Speichert Trades nach erfolgreichem Backtest |
| TradeHistoryViewModel | Lädt bestehende Trades beim Start aus DB |
| RiskSettingsViewModel | Speichert/lädt RiskSettings in/aus DB |
| DashboardViewModel | Speichert Equity-Snapshots alle 5 Minuten wenn Bot läuft |

Alle DB-Parameter sind optional (`BotDatabaseService?`), damit Tests ohne DB funktionieren.

## Bekannte Fixes (Code Review 15.03.2026)

| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Race Condition _positionSignals | PaperTradingService.cs | Dictionary -> ConcurrentDictionary (PriceTickerLoop + ScanAndTradeAsync laufen parallel) |
| Drawdown bei Gewinnen falsch | RiskManager.cs | Math.Abs(effectivePnl) zaehlt auch Gewinne als Drawdown. Fix: nur negativen PnL werten |
| EmergencyStop publiziert Trades nicht | PaperTradingService.cs | CloseAll-Trades wurden nicht an EventBus/RiskManager gemeldet |
| RiskManager im Paper-Trading nie aktualisiert | PaperTradingService.cs | UpdateDailyStats() wurde bei keinem Close aufgerufen -> Drawdown-Limits wirkungslos |
| HttpClient ohne Timeout | BingXRestClient.cs | 30s Timeout hinzugefuegt (wie BingXPublicClient) |

## Bekannte Fixes (Code Review 17.03.2026)

| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| User-Data-Stream Leak bei Stop | LiveTradingService.cs | StopAsync/EmergencyStopAsync räumten weder PeriodicTimer noch WebSocket-User-Data-Stream noch ListenKey auf. Fix: CleanupUserDataStreamAsync() als zentrale Cleanup-Methode |
| IndicatorHelper.CacheKey IndexOutOfRange | IndicatorHelper.cs | CacheKey griff auf candles[^1] zu ohne Leerheits-Prüfung. Bei leerer Liste → IndexOutOfRangeException. Fix: Guard für Count==0 |
| BeOneOf Test-Compile-Fehler | IndicatorHelperTests.cs | FluentAssertions BeOneOf(0, 1, "reason") interpretiert string als dritten int-Parameter. Fix: BeOneOf(new[] { 0, 1 }, "reason") |

## SK-System Verifikation (11.04.2026 — Vierte Re-Verifikation)

35 Regeln geprüft, alle korrekt. 273 Tests bestanden. Details: `SK_VERIFY_REPORT.md`

Letzte Fixes (Infra-Bug-Defaults):
- MaxHoldHours: 48 → 0 (deaktiviert, SL/TP managed den Exit für SK-Swing-Trades)
- MaxCorrelation: 0.7 → 0.85 (Krypto >70% korreliert in Trends)
- MinRiskRewardRatio: 1.0 → 0 (deaktiviert, Strategie hat eigenen gestuften RRR-Check)
- EnableEquityCurveTrading: true → false (Halbe Position nach Verlusten = Teufelskreis)
- MaxResults (Scanner): 50 → 100 (SK-Reversal-Setups brauchen breiteres Screening)

## Tests (273 Tests)

| Datei | Tests | Beschreibung |
|-------|-------|--------------|
| Core/ModelTests.cs | Models | Record-Erstellung, Enums |
| Core/ConfigTests.cs | Konfiguration | Settings-Defaults |
| Core/SimulatedExchangeTests.cs | SimulatedExchange | Order-Ausführung |
| Core/TimeFrameHelperTests.cs | TimeFrame-Konvertierung | IntervalString, Duration |
| Engine/EmaCrossStrategyTests.cs | EMA Cross | Signal-Generierung, Krypto-Filter |
| Engine/StrategyTests.cs | Alle 6 Strategien | Gemeinsame Tests + strategie-spezifische |
| Engine/StrategyFactoryTests.cs | StrategyFactory | Erstellung, Clone, Unknown, alte Namen |
| Engine/StrategyManagerTests.cs | StrategyManager | Multi-Symbol |
| Engine/IndicatorHelperTests.cs | Indikatoren | EMA, RSI, BB, MACD, ADX, Stochastik, HTF-Trend, Caching |
| Engine/CorrelationCheckerTests.cs | Korrelation | Pearson-Berechnung, preloadedKlines, Parallelisierung, API-Fehler-Fallback |
| Engine/MarketScannerTests.cs | Scanner | Volumen/Momentum-Filter |
| Engine/TradingEngineTests.cs | TradingEngine | Tick-Verarbeitung |
| Engine/RiskManagerTests.cs | RiskManager | Position-Sizing, Drawdown |
| Engine/TradeJournalTests.cs | TradeJournal | Record, WinRate, ProfitFactor |
| Exchange/RateLimiterTests.cs | RateLimiter | Request-Throttling |
| Exchange/BingXRestClientTests.cs | REST-Client | API-Aufrufe |
| Backtest/BacktestEngineTests.cs | BacktestEngine | Run, Demo-Candles |
| Backtest/PerformanceReportTests.cs | PerformanceReport | Metriken, Drawdown |

## Bekannte Fixes (Code Review 15.03.2026 - Dashboard Upgrade)

| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| ClosePosition bereinigt Signals nicht | DashboardViewModel.cs | Manuelles Schliessen entfernte Position aus UI, aber nicht aus _positionSignals im Service. PriceTickerLoop versuchte Position erneut zu schliessen. Fix: RemovePositionSignal() nach Close |
| TextBox decimal? Binding ohne Converter | DashboardView.axaml | TextBox.Text (string) direkt auf decimal? gebunden. Leeres Feld oder ungueltige Eingabe fuehrte zu Binding-Fehler, SL/TP konnte nie auf null gesetzt werden. Fix: NullableDecimalConverter |
| _publicClient NullForgiving (!) im Live-Start | DashboardViewModel.cs | _publicClient! erzwungen obwohl nullable. NullReferenceException wenn Client nicht verfuegbar. Fix: Expliziter Guard mit Fehlermeldung |
| PropertyChanged auf veralteten Items | DashboardViewModel.cs | SL/TP PropertyChanged-Handler feuerte auf Items die bereits aus OpenPositions entfernt waren. Fix: Guard `OpenPositions.Contains(item)` |

## Converter

| Converter | Datei | Beschreibung |
|-----------|-------|--------------|
| NullableDecimalConverter | Converters/NullableDecimalConverter.cs | decimal? in string und zurueck fuer TextBox-Bindings. Leeres Feld = null, ungueltiger Input = BindingNotification.Error, Komma+Punkt akzeptiert |

## Optimierungs-Update (17.03.2026)

### Kritische Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| RiskManager: Neues Position-Risiko einrechnen | RiskManager.cs | ValidateTrade berechnet jetzt den Worst-Case-Verlust der neuen Position (SL-Distanz * Quantity) und addiert ihn zum Drawdown. Verhindert Überschreitung des MaxDrawdown |
| Live-Trading Fees berechnen | LiveTradingService.cs | CompletedTrade enthält jetzt echte Fees (BingX Taker 0.05% pro Seite). Entry-Fee wird als initialer Verlust im RiskManager verbucht |
| REST-Client Retry mit Backoff | BingXRestClient.cs | 3 Retry-Versuche mit exponentiellem Backoff (2s, 4s, 8s) bei HTTP 429, 5xx und Netzwerkfehlern. Timestamp wird pro Versuch neu gesetzt |

### Neue Features
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| ADX Trend-Stärke-Indikator | IndicatorHelper.cs, TrendFollowStrategy.cs | ADX-Filter: Signale nur wenn ADX >= 20 (klarer Trend). Starker Trend (>40) erhöht Confidence, schwacher (20-25) reduziert |
| Multi-Timeframe Konfirmation | MarketContext.cs, PaperTradingService.cs, LiveTradingService.cs, TrendFollowStrategy.cs | 4h-Candles als HigherTimeframeCandles. EMA50 auf HTF → bullish/bearish/neutral. Gegen-Trend reduziert Confidence um 15% |
| CorrelationChecker aktiviert | CorrelationChecker.cs, PaperTradingService.cs, LiveTradingService.cs | Pearson-Korrelation gegen offene Positionen. Gated durch `RiskSettings.CheckCorrelation` (default: true, MaxCorrelation: 0.7). Nutzt IPublicMarketDataClient (funktioniert in Paper + Live) |
| Stochastik-Indikator | IndicatorHelper.cs | Neuer Indikator verfügbar: `CalculateStochastic(%K, %D)` mit konfigurierbarer Glättung |
| Indikator-Caching (Struct-Key) | IndicatorHelper.cs | ConcurrentDictionary mit `IndicatorCacheKey` Struct (statt String). Vermeidet String-Allokationen pro Lookup. IndicatorType Enum, IEquatable<T>, HashCode.Combine. ClearCache() am Ende jedes Scan-Durchlaufs |

### Sonstige Verbesserungen
| Verbesserung | Datei | Beschreibung |
|-------------|-------|--------------|
| GridStrategy TrendThreshold | GridStrategy.cs | Default von 2% auf 3.5% erhöht (Krypto-realistischer, Grid wird öfter aktiv) |
| WarmUp() implementiert | Alle 6 Strategien | Pre-Compute der benötigten Indikatoren (EMA, RSI, ATR, MACD, BB, ADX) in den IndicatorHelper-Cache |
| WebSocket User-Data-Stream | BingXWebSocketClient.cs, BingXRestClient.cs, LiveTradingService.cs | ListenKey erstellen/erneuern/löschen via REST. Separater WebSocket für ACCOUNT_UPDATE/ORDER_TRADE_UPDATE Events. Optional (Fallback: REST-Polling). ListenKey wird alle 30 Min erneuert |

## Agent-Review Fixes (17.03.2026)

### Security
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| AES zufälliger IV (Linux) | SecureStorageService.cs | `Aes.GenerateIV()` statt statischem IV. IV wird den verschlüsselten Daten vorangestellt (erste 16 Bytes). Abwärtskompatibel: Fallback auf Legacy-Format bei Decrypt-Fehler |
| HTTP-Error-Content kürzen | BingXRestClient.cs | Error-Content auf 200 Zeichen gekürzt um Info-Leaks in externen Log-Sinks zu vermeiden |
| recvWindow hinzugefügt | BingXRestClient.cs | `recvWindow=5000` in allen signierten Requests (Best Practice gegen Replay-Angriffe) |

### Performance
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| CorrelationChecker parallelisiert | CorrelationChecker.cs | `Task.WhenAll` statt sequentielle Klines-Calls pro Position. Bei 5 Positionen: ~2s statt ~12s |
| Klines nicht doppelt laden | CorrelationChecker.cs, PaperTradingService.cs, LiveTradingService.cs | Neuer optionaler Parameter `preloadedNewSymbolKlines`. Bereits geladene Candles werden übergeben → 10 API-Calls/Scan gespart |

### Debugger
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Fee-Berechnung mit Entry-Preis | LiveTradingService.cs | Entry-Fee basiert auf `pos.EntryPrice`, Exit-Fee auf aktuellem Preis (statt beide mit Ticker-Preis) |
| Verwaiste Signale bereinigen | LiveTradingService.cs | PriceTickerLoop entfernt Signale für Positionen die nicht mehr auf BingX existieren |

## Agent-Review Fixes Runde 2 (17.03.2026)

### Architektur
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Toten Code entfernt | TradingEngine.cs, BingXDataFeed.cs, TradingEngineTests.cs | 3 unbenutzte Klassen gelöscht (nie in DI registriert) |
| ScanHelper extrahiert | ScanHelper.cs (NEU) | Gemeinsame Scan-Logik: FilterCandidates, EvaluateCandidateAsync, CheckCorrelationAsync, ValidateRisk. Eliminiert ~100 Zeilen Duplikation |
| Debug.WriteLine → EventBus | PaperTradingService.cs, LiveTradingService.cs | PriceTicker-Fehler jetzt im Activity-Feed sichtbar |
| Verwaister PeriodicTimer | BingXWebSocketClient.cs | Timer + RenewListenKeyAsync entfernt (Erneuerung extern koordiniert) |

### Bugfixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| ResetDailyStats bei Tageswechsel | PaperTradingService.cs, LiveTradingService.cs | RunLoopAsync prüft UTC-Date und ruft ResetDailyStats() bei neuem Tag auf |
| Signal-Bereinigung mit Grace Period | LiveTradingService.cs | Verwaiste Signale erst nach 30s entfernt (Grace Period für BingX API-Latenz). _signalCreatedAt trackt Zeitpunkt |

## Fixes Runde 3 (17.03.2026)

### Kritische Bugfixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Ghost-Trade bei Eröffnung entfernt | LiveTradingService.cs | CompletedTrade mit Pnl=-entryFee bei Order-Eröffnung entfernt. Verfälschte PnL + doppelte Fee-Zählung. Fee wird jetzt nur beim Close eingerechnet |
| Trailing-Stop implementiert | LiveTradingService.cs, PaperTradingService.cs | Wenn EnableTrailingStop aktiv: SL wird in Gewinnrichtung nachgezogen. _extremePriceSinceEntry trackt Höchst-/Tiefstpreis pro Position. Konfigurierbar via RiskSettings (TrailingStopPercent) |
| EmergencyStop parallelisiert | LiveTradingService.cs | Task.WhenAll statt sequentiellem foreach beim Schließen aller Positionen. Reduziert Close-Zeit von N*Latenz auf 1*Latenz |

### Performance
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Positions inkrementell updaten | DashboardViewModel.cs | UpdatePositionsIncrementally() statt Clear+Add alle 5s. Bestehende Items behalten SL/TP + PropertyChanged-Handler. Nur MarkPrice/Pnl/Qty/Leverage werden aktualisiert |
| Separater RateLimiter für Live-Client | DashboardViewModel.cs | Eigener RateLimiter pro BingXRestClient statt globalen zu teilen (Public + Private API throttlen sich nicht mehr gegenseitig) |
| BingXPublicClient Retry | BingXPublicClient.cs | SendWithRetryAsync() mit 3 Versuchen + exponentiellem Backoff (2s, 4s, 8s) bei HTTP-/Netzwerkfehlern |

### Stabilität
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| WebSocket unbekannter Message-Typ | BingXWebSocketClient.cs | Nur Text und Binary (gzip) verarbeiten, unbekannte Typen überspringen |
| IndicatorHelper Cache Race | IndicatorHelper.cs | Scan-Generation im CacheKey verhindert Race Conditions bei parallelen Scan-Durchläufen |
| Debug.WriteLine → EventBus | DashboardViewModel.cs | Account-Update/BTC/Equity-Fehler jetzt im Activity-Feed sichtbar statt nur in Debug-Output |

### Cleanup
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| UseKellyCriterion/UseAtrSizing entfernt | RiskSettings.cs, RiskSettingsViewModel.cs, RiskSettingsView.axaml, BacktestViewModel.cs | Nicht implementierte Settings aus UI und Code entfernt (User-Irreführung vermeiden) |

## Security-Fixes (17.03.2026)

| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| PBKDF2 statt SHA-256 Key-Ableitung | SecureStorageService.cs | DeriveLinuxKey() nutzt jetzt Rfc2898DeriveBytes.Pbkdf2 mit 100.000 Iterationen + deterministischem Salt. Legacy SHA-256-Key als Fallback bei Decrypt fuer Migration bestehender Daten |

## Refactoring (20.03.2026)

### Architektur-Verbesserungen
| Änderung | Dateien | Beschreibung |
|----------|---------|--------------|
| TradingServiceBase | TradingServiceBase.cs (NEU), PaperTradingService.cs, LiveTradingService.cs | Gemeinsame Trading-Logik (RunLoop, PriceTickerLoop, SL/TP, Trailing-Stop) in abstrakte Basisklasse extrahiert. ~700 Zeilen Duplikation eliminiert |
| IndicatorHelper Struct-Cache | IndicatorHelper.cs | String-basierte Cache-Keys durch `IndicatorCacheKey` Struct ersetzt. IndicatorType Enum. Vermeidet ~120 String-Allokationen pro Scan-Durchlauf |
| DashboardViewModel Split | BtcTickerViewModel.cs (NEU), ActivityFeedViewModel.cs (NEU), DashboardViewModel.cs | BTC-Ticker und Activity-Feed in unabhängige Sub-ViewModels extrahiert. DashboardViewModel ~150 Zeilen reduziert |
| TradeJournal CancelledCount | TradeJournal.cs | Abgebrochene Trades (Margin/Rejected/Notfall) werden erkannt und aus WinRate-Berechnung ausgeschlossen |
| Database-Indices | BotDatabaseService.cs | CREATE INDEX für Trades (ExitTime, Mode, Symbol), Equity (Time), Logs (Timestamp, Level) |
| ScanHelper HTF-Error-Handling | ScanHelper.cs | Blankes `catch {}` durch spezifisches Exception-Handling ersetzt. OperationCanceledException wird re-thrown, Rest wird geloggt |
| BTC-Ticker abschaltbar | BotSettings.cs, BtcTickerViewModel.cs | `ShowBtcTicker` Setting + `IsEnabled` Property im BtcTickerViewModel |

## ATI - Adaptive Trading Intelligence (20.03.2026)

Selbstlernendes Trading-System mit 6 Schichten. Alle Komponenten in `BingXBot.Engine/ATI/`.

### Architektur

```
Candles → [1] FeatureEngine → [2] RegimeDetector → [3] AdaptiveEnsemble
       → [4] ConfidenceGate → [5] ExitOptimizer → [6] LearningLoop → Trade
```

### Komponenten

| Komponente | Datei | Beschreibung |
|------------|-------|--------------|
| FeatureEngine | ATI/FeatureEngine.cs | Extrahiert **25** normalisierte Features aus MarketContext (Preis, Momentum, Volatilität, Trend, Volumen, Session, BTC-Kontext, Markt-Stimmung, **Fear&Greed, Open Interest**) |
| RegimeDetector | ATI/RegimeDetector.cs | HMM-basierte Regime-Erkennung (TrendingBull/Bear, Range, Chaotic). Regelbasiert + gelernte Übergangswahrscheinlichkeiten. EMA-Glättung gegen Flackern |
| AdaptiveEnsemble | ATI/AdaptiveEnsemble.cs | Alle 6 Strategien parallel, dynamische Gewichte pro Regime (Bayesian Update). Konsens-Filter: Min 2 Strategien müssen übereinstimmen |
| ConfidenceGate | ATI/ConfidenceGate.cs | Bayesian Naive Bayes auf diskretisierten Feature-Buckets. Lernt P(Win|Features) aus eigenen Trade-Ergebnissen. Online-Lernen ab Trade 1 |
| ExitOptimizer | ATI/ExitOptimizer.cs | Adaptive SL/TP-Multiplikatoren pro Regime + Confidence. Lernt optimale Exit-Parameter aus Trade-Outcomes |
| WalkForwardOptimizer | ATI/WalkForwardOptimizer.cs | Walk-Forward Parameter-Optimierung mit GeneticSharp. Rollierende Fenster (Train:Test = 2:1) |
| AdaptiveTradingIntelligence | ATI/AdaptiveTradingIntelligence.cs | Hauptorchestrator. Verbindet alle Komponenten, erstellt Audit-Trails, verwaltet offene Trade-Kontexte |

### Core Models (in BingXBot.Core/Models/ATI/)

| Model | Beschreibung |
|-------|--------------|
| MarketRegime | Enum: TrendingBull, TrendingBear, Range, Chaotic |
| RegimeState | Regime + Confidence + 4 Wahrscheinlichkeiten |
| FeatureSnapshot | **25** normalisierte Features + Metadaten + ToFeatureArray() (inkl. Cross-Market, Fear&Greed, OpenInterest) |
| EnsembleVote | Konsens-Signal + Gewichte + Einzelstimmen |
| TradeAudit | Vollständiger Audit-Trail jeder Entscheidung |
| FeatureSnapshotEntity | DB-Entity für ML-Training (25 Features + Outcome + FromSnapshot() Factory) |

### NuGet-Pakete (neu)

| Paket | Version | Zweck |
|-------|---------|-------|
| Microsoft.ML | 5.0.0 | ML-Framework (LightGBM Phase 2) |
| Microsoft.ML.LightGbm | 5.0.0 | Gradient Boosted Trees Classifier |
| GeneticSharp | 3.1.4 | Genetischer Algorithmus für Walk-Forward |

### Integration

- TradingServiceBase: `ATI` Property, ATI-Branch in ScanAndTradeAsync, `ProcessCompletedTrade()` Methode
- PaperTradingService/LiveTradingService: `ProcessCompletedTrade()` statt `_riskManager.UpdateDailyStats()`
- DashboardViewModel: ATI per DI injiziert, Strategien beim Bot-Start ins Ensemble registriert
- BotDatabaseService: FeatureSnapshots-Tabelle + CRUD-Methoden
- App.axaml.cs: `AdaptiveTradingIntelligence` als Singleton registriert

## Profit-Optimierung (05.04.2026)

### Realistisches Backtest-Modell
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| Dynamische Slippage | SimulatedExchange.cs, BacktestSettings.cs | ATR/Volume-basierte Slippage statt fixem 0.05%. Skaliert mit Volatilität und inversem Volumen (0.02-2%) |
| Bid-Ask Spread | SimulatedExchange.cs | SpreadPercent (Default 0.08%) wird als halber Spread pro Seite aufgeschlagen. Realistischer als Slippage-Only |
| Market-Conditions pro Candle | BacktestEngine.cs → SimulatedExchange.SetMarketConditions() | ATR und Volume-Ratio werden pro Candle an SimulatedExchange übergeben |

### Risk-Management Verbesserungen
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| RRR-Validierung | RiskManager.cs, RiskSettings.cs | MinRiskRewardRatio (Default 1.5:1). Trades mit schlechtem TP/SL-Verhältnis werden rejected |
| Smart Breakeven | BacktestEngine.cs, TradingServiceBase.cs | SL nach TP1 = Entry + 0.5*ATR statt exakter Entry. Verhindert Rauswerfen bei kleinen Pullbacks |
| Regime-Exit | TradingServiceBase.cs | Bei Chaotic-Regime werden alle offenen Positionen sofort geschlossen (PriceTickerLoop) |

### Pyramid Take-Profit (30/30/40)
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| TP1: 30% Close | BacktestSettings.cs, RiskSettings.cs | Tp1CloseRatio von 0.5 auf 0.3 geändert |
| TP2: 30% Close | BacktestEngine.cs (Tp2Closed State) | Neues TP2 Partial Close: 30% bei TP2, Rest trailing ohne TP |
| Tp2CloseRatio | BacktestSettings.cs, RiskSettings.cs | Konfigurierbarer Anteil der Position bei TP2 (Default 0.3) |

### Multi-dimensionaler Scanner
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| 5D-Scoring | MarketScanner.cs | Trend (30%) + Volumen (25%) + Momentum (20%) + Volatilität (15%) + Struktur (10%) statt simples |Price%| * Volume |
| Indikator-basiert | MarketScanner.cs | Nutzt EMA, ADX, RSI, MACD, ATR, Bollinger für fundiertes Scoring. Klines werden per ExchangeClient geladen |
| Mode-Gewichtung | MarketScanner.cs | Jeder ScanMode hat eigene Gewichtungs-Verteilung der 5 Dimensionen |

### ATI Cross-Market Features
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| BTC-Kontext | FeatureEngine.cs, FeatureSnapshot.cs | BtcReturn24h, BtcTrend, BtcCorrelation, MarketSentiment als 4 neue Features (19→23) |
| Cross-Market-Pipeline | TradingServiceBase.cs | UpdateCrossMarketFeaturesAsync() berechnet BTC-Korrelation und Markt-Stimmung pro Scan-Zyklus |
| Regime CurrentRegime | RegimeDetector.cs | Neues `CurrentRegime` Property für Regime-Exit-Check in PriceTickerLoop |

### Live-Trading Verbesserungen
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| Serverseitiges SL/TP-Update | BingXRestClient.cs, LiveTradingService.cs | SetPositionSlTpAsync() aktualisiert BingX SL/TP-Orders nach TP1 Partial Close (Smart BE + TP2) |
| WalkForward im UI | BacktestViewModel.cs | RunWalkForwardCommand verdrahtet WalkForwardOptimizer im BacktestView (GA-basierte Parameter-Optimierung)

### Lernzyklus

```
Trade geschlossen → ProcessCompletedTrade()
  → AdaptiveEnsemble.RecordOutcome() → Strategie-Gewichte aktualisieren
  → ConfidenceGate.RecordOutcome() → Bayesian Buckets updaten
  → ExitOptimizer.RecordExitOutcome() → SL/TP-Multiplikatoren anpassen
```

### Persistenz (ATI-State)

- Alle Lernzustände werden beim Bot-Stop als JSON in die DB gespeichert (SettingEntity, Key: `"AtiState"`)
- Beim Bot-Start (Paper + Live) wird der Zustand aus der DB geladen
- Serialisierung pro Komponente: ConfidenceGate (BucketStats), AdaptiveEnsemble (Gewichte/Regime), ExitOptimizer (ExitStats), RegimeDetector (Übergangsmatrix)
- Korrupte Daten werden ignoriert (frischer Start mit leeren Modellen)
- `BotDatabaseService.SaveAtiStateAsync()` / `LoadAtiStateAsync()`

## Exchange-Features (20.03.2026)

### Native SL/TP-Orders
- `BingXRestClient.PlaceOrderAsync()` sendet `stopLoss` und `takeProfit` als JSON-String-Parameter
- Typ: `STOP_MARKET` (SL) / `TAKE_PROFIT_MARKET` (TP), `workingType: MARK_PRICE`
- `PlaceOrderOnExchangeAsync()` hat optionalen `SignalResult? signal` Parameter
- LiveTradingService: Signal wird durchgereicht, SL/TP nativ auf BingX gesetzt
- PaperTradingService: Signal-Parameter wird ignoriert (SimulatedExchange ohne native SL/TP)
- Bot-seitige SL/TP-Pruefung (PriceTickerLoop) bleibt als Fallback aktiv

### Dedizierter closeAllPositions Endpunkt
- `BingXRestClient.CloseAllPositionsAsync()` nutzt `/openApi/swap/v2/trade/closeAllPositions`
- Ein API-Call pro Symbol statt pro Position (effizienter bei mehreren Positionen pro Symbol)
- Parallel via `Task.WhenAll`

## Logik-Analyse Fixes (03.04.2026)

### Kritisch (5 Fixes)
| Fix | Datei(en) | Beschreibung |
|-----|-----------|--------------|
| Volume-SMA berechnete Close statt Volume | IndicatorHelper.cs, 4 Strategien | Neue `CalculateVolumeSma()` Methode. `CalculateSma()` nutzt Skender-Default (Close) — für Volume-Vergleich muss manueller SMA über Volume-Werte berechnet werden |
| Doppelter SL/TP-Trigger (Native + Bot) | LiveTradingService.cs | `OnSlTpHitAsync` prüft jetzt ob Position noch auf BingX existiert bevor Close. Signal wird VOR Close entfernt |
| Race Condition Trailing-Stop | TradingServiceBase.cs | `AddOrUpdate` mit atomarer Update-Funktion statt Read-Modify-Write. Verhindert Überschreiben von User-SL/TP-Änderungen |
| RegimeDetector Cache-Korruption | RegimeDetector.cs | `SmoothScores` gibt Kopie zurück statt gecachtes Array. `ApplyTransitionPrior` erstellt immer neue Kopie |
| CTS Dispose ohne Cancel | TradingServiceBase.cs | `_cts?.Cancel()` vor `_cts?.Dispose()` in StartBase + StopBase. Verhindert parallele Ghost-Loops |

### Hoch (8 Fixes)
| Fix | Datei(en) | Beschreibung |
|-----|-----------|--------------|
| PositionSize ignoriert StopLoss | RiskManager.cs | Risiko-basiertes Sizing: `maxLoss / slDistance` statt fixer Margin-%. Enger SL = größere Position, weiter SL = kleinere |
| ATR=0 kein Guard | 5 Strategien | Early-Return bei `atrValue <= 0` (identische OHLC bei illiquiden Assets) |
| PnL-Divergenz Paper vs Live | (dokumentiert) | SimulatedExchange nutzt Slippage, Live nicht — bekannte Inkonsistenz, Paper-Ergebnisse sind konservativer |
| EmergencyStop Race | (dokumentiert) | CTS-Cancel kann Nachlauf-Order nicht verhindern — bekanntes Restrisiko bei API-Latenz |
| ConfidenceGate Bayes-Formel | ConfidenceGate.cs | Log-Odds werden jetzt summiert (nicht gemittelt) + Prior-Term einbezogen |
| RSI Divergenz zeitlich korreliert | RsiStrategy.cs | Höchster Preis-Index bestimmt RSI-Vergleichswert (zeitlich korrelierte Pivot-Points statt unabhängiges Max/Min) |
| Non-ATI Positions-Refresh | TradingServiceBase.cs | `positions` wird nach Close-Signal aktualisiert (wie im ATI-Pfad) |
| ExitOptimizer lernt aus Verlierern | ExitOptimizer.cs | `AvgLosingSl/Tp` fließen in SL/TP-Berechnung ein (zu enger SL → weiter, zu weites TP → enger) |

### Mittel + Niedrig (10 Fixes)
| Fix | Datei(en) | Beschreibung |
|-----|-----------|--------------|
| _tickerPriceMap Thread-Safety | TradingServiceBase.cs | `Dictionary` → `ConcurrentDictionary` (PriceTickerLoop + RunLoop parallel) |
| ATI.Reset() unvollständig | AdaptiveTradingIntelligence.cs | Alle 4 Komponenten werden zurückgesetzt (nicht nur ConfidenceGate) |
| Ensemble Dissens-Strategien | AdaptiveTradingIntelligence.cs | Strategien die gegen den Konsens stimmten bekommen invertiertes Feedback |
| Quotes-Cache Kollision | IndicatorHelper.cs | FirstOpenTimeTicks im Cache-Key (verhindert Kollision bei gleicher Länge+letztem Close) |
| Ensemble bestEntry Placeholder | AdaptiveEnsemble.cs | Toter Code (unused bestEntry Variable) entfernt |

## Performance-Optimierungen (03.04.2026)

### Kritisch: Scan-Latenz + API-Calls
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Klines parallel vorladen | TradingServiceBase.cs | Alle Kandidaten-Klines+HTF parallel laden (SemaphoreSlim(5)) statt sequenziell pro Symbol. Reduziert Scan-Zeit von ~8s auf ~2s bei 20 Kandidaten |
| Account/Positions nur bei Order-Platzierung neu laden | TradingServiceBase.cs | GetAccountAsync()+GetPositionsForScanAsync() nicht mehr pro Kandidat, nur nach erfolgreichem Trade |
| Ticker-Dictionary wiederverwenden | TradingServiceBase.cs | `_tickerPriceMap` als Feld statt `tickers.ToDictionary()` alle 5s neu allokieren |

### Hoch: GC-Pressure + Allokationen
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| BotEventBus HasLogSubscribers | BotEventBus.cs | Property prüft ob LogEmitted Subscriber hat. Debug-Logs + String-Interpolation nur bei aktivem Subscriber |
| WebSocket ArrayPool Buffer | BingXWebSocketClient.cs | `ArrayPool<byte>.Shared` statt `new byte[]` pro Receive-Loop. MemoryStreams wiederverwendet (SetLength(0)), GetBuffer() statt ToArray(). Pong-Bytes gecacht |
| Quotes-Cache pro Candle-Set | IndicatorHelper.cs | `ToQuotes()` cacht Ergebnis per (Count,Close,Ticks)-Key. Vermeidet ~20.000 Quote-Allokationen pro Scan |
| CorrelationChecker ohne Array-Kopien | CorrelationChecker.cs | `CalculatePearsonFromCandles()` liest Close-Werte direkt aus Candle-Listen per Index statt ArraySegment.ToArray() |
| SimulatedExchange Positions-Cache | SimulatedExchange.cs | `_cachedPositions` + `_positionsDirty` Flag. GetPositionsAsync() gibt Cache zurück wenn kein Preis/Position sich geändert hat |
| BTC-Ticker: Klines statt alle Ticker | BtcTickerViewModel.cs | UpdateBtcPriceAsync() lädt 2 M1-Candles statt 500+ Ticker zu deserialisieren |

### Mittel: Ressource-Management
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| HttpClient wiederverwendet | DashboardViewModel.cs | `_liveHttpClient` als Feld statt `new HttpClient()` bei jedem Live-Start (Socket-Exhaustion vermeiden) |
| Account-Timer CancellationToken | DashboardViewModel.cs | `_accountUpdateCts` für sauberen Abbruch bei Stop/Emergency. Timer-Loop bekommt CancellationToken |

## UX-Verbesserungen (03.04.2026)

### Kritische Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Umlaut-Fehler | MainViewModel, DashboardViewModel, BtcTickerViewModel, DashboardView | "Laeuft"→"Läuft", "Verfuegbar"→"Verfügbar", "eroeffnet"→"eröffnet", "ausgefuehrt"→"ausgeführt" |
| Verbindungsstatus-Dot dynamisch | MainView, MainViewModel | Status-Dot grün wenn Bot läuft, rot wenn gestoppt. IsConnected Property |
| Backtest P&L Farbe | BacktestView, BacktestViewModel | Gesamt-PnL + WinRate farbkodiert. IsPnlPositive, IsWinRateGood Properties |
| API-Secret verborgen | SettingsView | RevealPassword entfernt - Secret default maskiert |

### UX-Verbesserungen
| Verbesserung | Datei | Beschreibung |
|-------------|-------|--------------|
| NumericUpDown | ScannerView, BacktestView | TextBox→NumericUpDown für numerische Eingaben (Min/Max/Increment) |
| Naming-Konsistenz | MainView | "Trade-History"→"Trade-Historie" (einheitlich deutsch) |
| Log Auto-Scroll | LogView.axaml.cs | CollectionChanged→ScrollToEnd bei neuen Einträgen |
| Scanner Progress | ScannerView | Indeterminate ProgressBar während Scan |
| RiskSettings Dirty-State | RiskSettingsView, RiskSettingsViewModel | HasUnsavedChanges Warnung bei ungespeicherten Änderungen |
| Keyboard-Shortcuts | MainView, DashboardView | Ctrl+1-8=Navigation, F5/F6/F7/F12=Bot-Kontrolle, Tooltips |
| Activity Feed expandierbar | DashboardView, DashboardViewModel | Toggle Collapsed=200px/Expanded=500px |
| Confirm-Dialoge | DashboardViewModel | Live-Start + Notfall-Stop (Live) erfordern Bestätigung |

## Architektur-Refactoring (03.04.2026)

| Änderung | Dateien | Beschreibung |
|----------|---------|--------------|
| SimulatedExchange → Backtest | SimulatedExchange.cs | Von `BingXBot.Core.Simulation` nach `BingXBot.Backtest.Simulation` verschoben. Core enthält keine konkrete Exchange-Implementierung mehr |
| LiveTradingManager extrahiert | LiveTradingManager.cs (NEU) | Live-Trading-Infrastruktur aus DashboardVM extrahiert: Connect, Start, Stop, EmergencyStop, ATI-Persistenz, Signal-Wiederherstellung. DashboardVM ~220 LOC reduziert |
| Startkapital konfigurierbar | BotSettings.cs, DashboardViewModel.cs | `BotSettings.PaperInitialBalance` statt hardcoded 10_000m an 4 Stellen |
| DB-Init Race Condition | App.axaml.cs | Synchrone DB-Initialisierung vor Fenster-Erstellung (statt fire-and-forget) |
| HttpClient wiederverwendbar | LiveTradingManager.cs | Ein HttpClient-Feld pro LiveTradingManager, wiederverwendet bei Start/Stop |
| FluentTheme DarkMode Fix | App.axaml | Veraltetes `DarkMode="True"` entfernt (wird in App.axaml.cs via RequestedThemeVariant gesetzt) |

## Risk-Management-Erweiterungen (04.04.2026)

### Neue RiskManager-Prüfungen
| Feature | Datei(en) | Beschreibung |
|---------|-----------|--------------|
| Liquidation-Preis-Check | RiskManager.cs, RiskSettings.cs | `CalculateLiquidationPrice()` berechnet Isolated-Margin-Liquidation (BingX 0.4% MMR). Kein Trade wenn Abstand < MinLiquidationDistancePercent (Default: 10%) |
| Netto-Exposure-Limit | RiskManager.cs, RiskSettings.cs | `CalculateNetExposure()` summiert alle Positionswerte. Kein Trade wenn Gesamt-Exposure > MaxNetExposurePercent (Default: 50%) |
| Funding-Rate-Filter | RiskManager.cs, RiskSettings.cs | Kein Trade gegen hohe Funding-Rate. `ConsiderFundingRate` + `MaxAdverseFundingRatePercent` (Default: 0.1%). Positive Funding schadet Longs, negative Shorts |
| IRiskManager erweitert | IRiskManager.cs | Neue Methoden: `ValidateTrade(signal, context, fundingRate)`, `CalculateLiquidationPrice()`, `CalculateNetExposure()` |

### ATI Cold-Start-Schutz
| Feature | Datei(en) | Beschreibung |
|---------|-----------|--------------|
| MinTradesBeforeLearning | ConfidenceGate.cs, AdaptiveTradingIntelligence.cs | Unter N Trades (Default: 20) gibt ConfidenceGate immer Prior zurück, filtert keine Trades. Schützt gegen schlechte Entscheidungen mit zu wenig gelernten Daten |
| ATI Auto-Save | TradingServiceBase.cs, AdaptiveTradingIntelligence.cs, BotSettings.cs | Periodische ATI-State-Persistierung (Default: 15 Min) statt nur bei Bot-Stop. Schützt gegen Datenverlust bei App-Crash |

### Funding-Rate-Simulation (Paper + Backtest)
| Feature | Datei(en) | Beschreibung |
|---------|-----------|--------------|
| SimulatedExchange.ApplyFundingRate() | SimulatedExchange.cs | Wendet Funding auf offene Positionen an: Longs zahlen bei positiver Rate, Shorts bei negativer |
| BacktestEngine Funding | BacktestEngine.cs | Funding-Rate alle 8h im Tick-Loop angewendet. Konfigurierbar via BacktestSettings.SimulatedFundingRatePercent (Default: 0.01%) |
| PaperTradingService Funding | PaperTradingService.cs | SimulatedExchange mit konfigurierter Funding-Rate aus BotSettings.SimulatedFundingRatePercent |

### Multi-Stage Exit (Backtest)
| Feature | Datei(en) | Beschreibung |
|---------|-----------|--------------|
| BacktestSettings Multi-Stage | BacktestSettings.cs | `SimulateMultiStageExit` (Default: true), `Tp1CloseRatio` (0.5), `TrailingAtrMultiplier` (2.5), `MaxHoldHoursInitial` (48), `MaxHoldHoursAfterTp1` (96) |
| BacktestExitState | BacktestEngine.cs | Innere Klasse trackt Entry, OriginalQty, PartialClosed, ExtremePriceSinceEntry, CurrentAtr pro Position |
| TP1 Partial Close | BacktestEngine.cs | Bei TP1-Hit: 50% Position via ReducePositionAsync schließen, SL auf Break-Even, TP auf TP2 verschieben |
| Chandelier-Trailing | BacktestEngine.cs | Nach TP1: SL nachziehen basierend auf Extreme-Price minus ATR*Multiplikator, nur nach vorne |
| Time-Exit | BacktestEngine.cs | Vor TP1: Schließen nach MaxHoldHoursInitial wenn nicht im Gewinn. Nach TP1: Schließen nach MaxHoldHoursAfterTp1 |

### Margin-Monitoring
| Feature | Datei(en) | Beschreibung |
|---------|-----------|--------------|
| PriceTickerLoop Liquidations-Warnung | TradingServiceBase.cs | Prüft alle 5s den Abstand zum Liquidationspreis. Warnung wenn < 2x MinLiquidationDistance. Max 1 Warnung pro 5 Min pro Position |
| BotEventBus MarginWarning | BotEventBus.cs | Neues Event: `MarginWarning` mit Symbol, aktueller Preis, Liquidationspreis, Abstand-% |

### Desktop-Benachrichtigungen
| Feature | Datei(en) | Beschreibung |
|---------|-----------|--------------|
| Trade-Notifications | TradingServiceBase.cs, BotEventBus.cs, BotSettings.cs | NotificationRequested-Event bei Trade-Close (Gewinn/Verlust, PnL, Preise). Aktivierbar via BotSettings.EnableDesktopNotifications |

### DB Schema-Versioning
| Feature | Datei(en) | Beschreibung |
|---------|-----------|--------------|
| RunMigrationsAsync() | BotDatabaseService.cs | Schema-Version in Settings-Tabelle. Automatische Migrationen bei App-Start. v2: FundingPaid-Spalte in Trades |

### UI: RiskSettings-View erweitert
| Feature | Datei(en) | Beschreibung |
|---------|-----------|--------------|
| Liquidation + Exposure Sektion | RiskSettingsView.axaml, RiskSettingsViewModel.cs | Min. Liquidations-Abstand + Max. Netto-Exposure konfigurierbar |
| Funding-Rate Sektion | RiskSettingsView.axaml, RiskSettingsViewModel.cs | Funding-Rate-Filter an/aus + Max. adverse Rate |
| Dirty-State für neue Felder | RiskSettingsViewModel.cs | Alle 4 neuen Properties lösen HasUnsavedChanges aus |

## Tests (210 Tests)

## Code-Review Fixes (05.04.2026)

### Kritische Fixes (3)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| SimulatedExchange Lock-Release Race | SimulatedExchange.cs | `ReducePositionAsync` Full-Close inline statt Lock-Release + ClosePositionAsync (Race zwischen ExitWriteLock und erneutem Lock) |
| _tradesToday Thread-Safety | TradingServiceBase.cs | `Interlocked.Increment/Exchange` statt nicht-atomarem `++`/`=0` (UI-Thread schreibt parallel via StopBase) |
| SemaphoreSlim als Klassenfeld | TradingServiceBase.cs | `_klineSemaphore` als Feld statt `new SemaphoreSlim(5)` pro ScanAndTradeAsync-Aufruf (Handle-Leak) |

### Wichtige Fixes (6)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Peak-Equity Drawdown | RiskManager.cs | Total-Drawdown relativ zu `_peakEquity` statt kumulativem `_totalPnl`. Verhindert Unterschätzung nach Gewinnphasen |
| Korrelation auf Log-Returns | CorrelationChecker.cs | `Math.Log(close[i] / close[i-1])` statt absolute Preise (vermeidet spurious Korrelation bei trending Märkten) |
| Backtest SL/TP-Bias | BacktestEngine.cs | Candle-Richtung entscheidet bei gleichzeitigem SL+TP-Treffer (statt immer SL-first) |
| BTC-Kontext-Scoring +2 | CryptoTrendProStrategy.cs | HTF-Supertrend-Bonus unabhängig vom bisherigen Score (war durch `longScore < 4` blockiert) |
| ConflueceScore Typo | 7 Dateien | `ConflueceScore` → `ConfluenceScore` (Records, Properties, Referenzen) |
| Klines ConcurrentDictionary | TradingServiceBase.cs | `Dictionary + lock` → `ConcurrentDictionary` in ScanAndTradeAsync (parallele Klines-Tasks) |

### Sicherheits-Fixes (4)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| API-Keys aus WebSocketClient | BingXWebSocketClient.cs | Unbenutzte `_apiKey`/`_apiSecret` Felder entfernt (kein Klartext-Leak bei Memory-Dump) |
| Credential-Error Logging | SettingsViewModel.cs | `{ex}` → `{ex.Message}` (kein Stacktrace mit Systempfaden) |
| Linux credentials.dat chmod 600 | SecureStorageService.cs | `File.SetUnixFileMode(UserRead|UserWrite)` nach Schreiben |
| Parameter Min/Max-Validierung | StrategyViewModel.cs | Reflection-Rückschreibung clampt Werte auf StrategyParameter.Min/Max |

### Performance-Fixes (2)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Backtest CandleSlice (Zero-Copy) | BacktestEngine.cs | `CandleSlice : IReadOnlyList<Candle>` statt `GetRange()` pro Candle (vermeidet ~5000 List-Allokationen) |
| Indikator-Cache Backtest-Limit | BacktestEngine.cs | `IndicatorHelper.ClearCache()` alle 500 Iterationen + nach Schleife (verhindert ~112 MB Cache-Wachstum) |

### Strategie-Verbesserungen (4)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| TrendFollow RRR 2:1 | TrendFollowStrategy.cs | TP-Multiplier von 3x auf 4x ATR (RRR 1.5→2.0) |
| MacdStrategy SL 2x ATR | MacdStrategy.cs | Histogram-SL von 1.5x auf 2x ATR (war zu eng für lagging MACD-Signals) |
| GridStrategy Lookback-Grenzen | GridStrategy.cs | 50-Candle High/Low als Grid-Grenzen statt Bollinger-Bänder (keine spurious Squeeze-Verengung) |
| PerformanceReport +5 Metriken | PerformanceReport.cs | CalmarRatio, SortinoRatio, RecoveryFactor, MaxConsecutiveLosses/Wins |

### Defaults-Änderungen
| Setting | Alt | Neu | Grund |
|---------|-----|-----|-------|
| MaxNetExposurePercent | 300% | 200% | Flash-Crash-Schutz bei 3x Leverage |

## Farbpalette

Dark-Trading-Theme: Primary #3B82F6, Background #1E1E2E, Profit #10B981, Loss #EF4444

## UI-Conventions (03.04.2026)

| Convention | Details |
|-----------|---------|
| Compiled Bindings | `x:CompileBindings="True"` in allen 10 Views |
| Virtualisierung | ListBox + VirtualizingStackPanel in TradeHistory, Log, Backtest, Scanner |
| Monospace-Zahlen | `FontFamily="Consolas, Courier New, monospace"` für alle Preise/PnL/Metriken |
| Keyboard-Shortcuts | Ctrl+1-8 Navigation, Escape → Dashboard |
| Tooltips | Alle Bot-Buttons, Nav-Items, Account-Karten, Risk-Settings, Backtest-Metriken |
| Farb-Palette | Alle Farben via DynamicResource aus AppPalette.axaml (keine hardcodierten Hex-Werte) |
| PnL-Farbcodierung | IsVisible-Toggle mit SuccessBrush/ErrorBrush (grün/rot) für PnL-Werte |
| Status-Indikatoren | Dynamische Farben via ViewModel-Properties (ConnectionDotColor, StatusDotColor, BotStatusColor) |
| Dark-Mode | `RequestedThemeVariant = ThemeVariant.Dark` in App.axaml.cs |
| Hover-Farben | SuccessHoverBrush, WarningHoverBrush, ErrorHoverBrush, StopHoverBrush in AppPalette |

## Professionalisierung (06.04.2026)

### Bug-Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| TP2 Partial-Close im Live/Paper | TradingServiceBase.cs, PositionExitState.cs | TP2 schloss 100% statt 30%. Neues `Tp2Closed`-Property + Partial-Close-Block analog BacktestEngine. Phase wechselt auf `Trailing` |
| FeatureSnapshotEntity 25 Features | FeatureSnapshotEntity.cs, BotDatabaseService.cs | 4 Cross-Market + 2 neue Features (FearGreed, OpenInterest) hinzugefügt. DB-Migration v2→v4. `FromSnapshot()` Factory-Methode |
| GetAdaptiveLeverage verdrahtet | TradingServiceBase.cs, LiveTradingService.cs, PaperTradingService.cs | ATR-Perzentil + Score → adaptiver Leverage vor PlaceOrder. Min(adaptiv, MaxLeverage) |
| Paper-Trading Slippage aktiv | PaperTradingService.cs | `SetMarketConditions()` wird jetzt aufgerufen. ATR aus ExitState oder 1.5% Fallback |
| Scanner parallel | MarketScanner.cs | Klines-Loading parallelisiert mit SemaphoreSlim(5) + Task.WhenAll. 20+ Symbole: ~2s statt ~20s |

### Neue Features
| Feature | Dateien | Beschreibung |
|---------|---------|--------------|
| Open Interest Feature | BingXPublicClient.cs, FeatureEngine.cs, FeatureSnapshot.cs | OI-Change als normalisiertes Feature. Steigendes OI + steigender Preis = gesunder Trend |
| Fear & Greed Index | TradingServiceBase.cs, FeatureEngine.cs | alternative.me API alle 15min. Normalisiert [0,1] als Feature. Extreme Werte = Signal-Warnung |
| Cooldown-Eskalation | RiskSettings.cs, TradingServiceBase.cs | Progressive Cooldowns: 1 Verlust=8h, 2=16h, 3+=24h. Bei 3+ Verlusten: Leverage halbiert. Max 48h Cap |
| Equity-Curve-Trading | RiskSettings.cs, TradingServiceBase.cs, RiskManager.cs | Equity unter EMA(20 Trades) → halbe Position. Automatischer Schutz vor Drawdown-Spiralen |
| Momentum-Decay | TradingServiceBase.cs, RiskSettings.cs | Erkennt wenn Preis sich >1.5x ATR vom Höchstpunkt entfernt (nach TP1). Schließt Position statt auf SL zu warten |
| Monte Carlo Simulation | MonteCarloSimulator.cs (NEU), PerformanceReport.cs | 1000 Trade-Shuffles, Konfidenz-Intervalle: MaxDD 50/95/99%, Return 5/50/95%, Ruin-Wahrscheinlichkeit |
| Rolling Live-Metriken | RiskManager.cs | Rolling 30-Trade-Window: WinRate, ProfitFactor, Sharpe. Strategy-Health-Check warnt bei Degradation |
| Regime-Backtest-Metriken | PerformanceReport.cs, CompletedTrade.cs | WinRate/PnL/ProfitFactor pro MarketRegime (TrendingBull/Bear/Range/Chaotic). CompletedTrade hat optionales Regime-Feld |
| Limit-Orders | SignalResult.cs, LiveTradingService.cs, SimulatedExchange.cs | `PreferLimitOrder` Flag im Signal. Maker-Fee 0.02% statt Taker 0.05%. SimulatedExchange hat jetzt Limit-Order-Matching in SetCurrentPrice |
| WebSocket Price-Ticker | BingXWebSocketClient.cs, LiveTradingService.cs | `SubscribeAllTickersAsync()` für Echtzeit-Preise. `TickerPriceReceived` Event. Sub-100ms Latenz für SL/TP-Monitoring |

### Neue RiskSettings (06.04.2026)
| Setting | Default | Beschreibung |
|---------|---------|--------------|
| EnableCooldownEscalation | true | Progressive Cooldown-Eskalation bei Verlusten |
| MaxCooldownHours | 48 | Maximaler Cooldown bei Eskalation |
| EnableEquityCurveTrading | true | Position reduzieren wenn Equity unter EMA |
| EquityCurvePeriod | 20 | EMA-Periode in Anzahl Trades |
| EnableMomentumDecay | true | Momentum-Decay-Detection im PriceTickerLoop |

### FeatureEngine (25 Features, vorher 23)
Neue Features: `FearGreedIndex` (Fear & Greed API), `OpenInterestChange` (BingX OI-API)

### ML Phase 2: LightGBM Classifier (06.04.2026)
- `LightGbmClassifier.cs` in `Engine/ATI/` - ML.NET LightGBM auf 25 Features + 3 Ensemble-Metadaten (28 Inputs)
- Trainiert auf gelabelten FeatureSnapshotEntity aus DB (min. 50 Samples)
- 80/20 Train/Test-Split, Metriken: Accuracy, AUC, F1, Precision, Recall
- `Predict()` gibt P(Win) zurück - kann ConfidenceGate Phase 1 (Naive Bayes) ergänzen/ersetzen
- Feature-Snapshots werden jetzt automatisch bei jedem Trade in DB gespeichert (ATI.FeatureSnapshotCompleted Event)

### CPCV - Combinatorial Purged Cross-Validation (06.04.2026)
- `CpcvValidator.cs` in `Backtest/Reports/` - 6 Blöcke, 2 Test-Blöcke pro Kombination, C(6,2)=15 Kombinationen
- Purging: 2 Trades an Block-Grenzen entfernt (verhindert Daten-Leckage)
- Ergebnis: **Probability of Backtest Overfitting (PBO)** + Degradation IS→OOS
- Automatisch im PerformanceReport bei >=30 Trades
- PBO < 30% = akzeptabel, Degradation < 30% = akzeptabel

### ConfidenceGate Buckets (06.04.2026, 16 Buckets statt 12)
- 12 Einzel-Buckets (vorher 9): +FearGreed, +OpenInterest, +BtcTrend
- 4 Kombinations-Buckets (vorher 3): +FearGreed×Regime
- Diskretisierung: FearGreed (5 Stufen), OpenInterest (3 Stufen), BtcTrend (3 Stufen)

### Regime-Tracking im Backtest (06.04.2026)
- RegimeDetector läuft im BacktestEngine mit (pro Candle-Iteration)
- CompletedTrade hat optionales `Regime?` Feld (seit 06.04.2026)
- Backtest-Trades werden mit dem Regime zum Entry-Zeitpunkt annotiert
- PerformanceReport zeigt WinRate/PnL/ProfitFactor pro MarketRegime

### ONNX-Runtime Infrastruktur (06.04.2026)
- `OnnxModelInference.cs` in `Engine/ATI/` - Lädt .onnx Dateien, Single + Batch Inference
- NuGet: `Microsoft.ML.OnnxRuntime` 1.22.0
- Workflow: Python trainiert Transformer/LSTM → `torch.onnx.export()` → C# lädt + inferiert
- Unterstützt variable Input-Shape: `[batch_size, feature_count]`
- ATI hat `OnnxModel` Property für optionale ONNX-Integration

### Auto-Training Pipeline (06.04.2026)
- `ATI.CheckAutoTraining()` trainiert LightGBM alle 10 Trades oder 24h (was zuerst kommt)
- Training im Background-Thread (blockiert nicht den Trading-Loop)
- Modell wird nur übernommen wenn AUC > 0.55 (besser als Münzwurf)
- Events: `AutoTrainingCompleted` für Logging, `FeatureSnapshotCompleted` für DB-Persistenz
- DashboardViewModel verdrahtet beides: Snapshots speichern + Auto-Training triggern

### Dashboard Rolling-Metriken (06.04.2026)
- `RollingWinRate`, `RollingSharpe`, `RollingProfitFactor` Properties im DashboardViewModel
- `StrategyHealthText` + `HasStrategyWarning` für Health-Check-Anzeige
- Aktualisierung alle 5 Min (zusammen mit Equity-Snapshots)
- RiskManager exponiert: `TotalPnl`, `RollingWinRate`, `RollingSharpeRatio`, `RollingProfitFactor`, `CheckStrategyHealth()`

### Transparenz & Logging (06.04.2026)

Alles wird im Activity-Feed und Log angezeigt:

| Feature | Log-Kategorie | Was der User sieht |
|---------|--------------|-------------------|
| Fear & Greed Index | Market | `Fear & Greed Index: 42/100 (Fear)` alle 15 Min |
| Open Interest | Market (Debug) | `BTC-USDT: OI steigend (+5.2%)` bei >3% Änderung |
| Adaptiver Leverage | Trade | `BTC-USDT: Long 0.1 @ 65000 (Lev=2x, SL=...)` |
| Equity-Curve-Scaling | Risk (Warning) | `Equity-Curve unter EMA → Position um 50% reduziert` |
| Cooldown-Eskalation | Risk (Warning) | `3 Verluste in Folge → Cooldown eskaliert auf 24h` |
| Momentum-Decay | Exit (Trade) | `Momentum-Decay: Preis 1.8x ATR vom Höchstpunkt` |
| Strategy-Health | Health (Warning) | `Rolling Sharpe 0.28 < 0.3 (degradiert)` |
| ATI-Entscheidungen | ATI | `BTC-USDT: Long AKZEPTIERT / Regime=TrendingBull, Ensemble=5/7, ML=72%` |
| ATI-Ablehnungen | ATI (Debug) | `ETH-USDT: Short ABGELEHNT / Grund: Kein Ensemble-Konsens` |
| Auto-Training | ML | `LightGBM trainiert: AUC=0.63, Acc=0.58` |
| ONNX-Modell | ML | `ONNX-Modell geladen: Inputs: [features: -1x25]` |
| WebSocket-Ticker | WebSocket | `Echtzeit-Ticker-Stream aktiv (sub-100ms Latenz)` |

Log-Filter-Kategorien: Alle, Trade, ATI, ML, Market, Risk, Health, Exit, Scanner, Engine, WebSocket, Backtest

### Limit-Orders (06.04.2026)
CryptoTrendPro setzt `PreferLimitOrder=true` bei Score >= 10/12 (starkes Signal).
Limit-Entry mit leichtem Pullback (0.1x ATR). Maker-Fee 0.02% statt Taker 0.05% = 60% Fee-Reduktion.

### ONNX Auto-Load (06.04.2026)
ATI prüft beim Start automatisch 2 Pfade:
1. `%APPDATA%/BingXBot/bingxbot_model.onnx`
2. `./bingxbot_model.onnx` (neben der .exe)

ConfidenceGate Hybrid-Gewichtung:
- Phase 1 (nur Bayesian): 100% Naive Bayes
- Phase 2 (+ LightGBM): 60% LightGBM + 40% Bayesian
- Phase 3 (+ ONNX): 50% ONNX + 30% Bayesian + 20% LightGBM

### NuGet-Pakete (aktuell)
| Paket | Version | Zweck |
|-------|---------|-------|
| Microsoft.ML | 5.0.0 | ML-Framework |
| Microsoft.ML.LightGbm | 5.0.0 | Gradient Boosted Trees |
| Microsoft.ML.OnnxRuntime | 1.22.0 | ONNX Model Inference |
| GeneticSharp | 3.1.4 | Walk-Forward Optimierung |

### BacktestView UI (06.04.2026)
- 5 Zeilen Metriken-Cards: Basis (PnL, WinRate, DD, Sharpe) + Erweitert (PF, Trades, AvgWin/Loss) + Professionell (Calmar, Sortino, Recovery, MaxConsecLosses) + Monte Carlo (DD95%, Return 5/50%, Ruin%) + CPCV (PBO, Degradation, OOS-Return) + Regime-Breakdown
- ScrollViewer für vertikales Scrollen wenn Platz nicht reicht
- Monte Carlo + CPCV nur sichtbar wenn Ergebnis vorhanden (HasMonteCarloResult/HasCpcvResult)

### DashboardView UI (06.04.2026)
- Strategy-Health-Warnung: Rote Box mit AlertCircle-Icon wenn HasStrategyWarning=true
- Rolling Live-Metriken: 3-Spalten UniformGrid mit WinRate/Sharpe/ProfitFactor (nur wenn Bot läuft)
- Aktualisierung alle 5 Min aus RiskManager Rolling-Window (30 Trades)

## Umfassender Audit + Fixes (07.04.2026)

5-Agenten-Audit (Code-Review, Security, Performance, Architektur, Health-Check).

### Kritische Fixes (2)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| ATI RegisterStrategies 3x im Loop | MultiModeOrchestrator.cs | `RegisterStrategies()` vor foreach-Schleife verschoben (rief intern `ClearStrategies()` auf → nur letzter Modus hatte Strategien) |
| SetMarketConditions Thread-Safety | SimulatedExchange.cs | `_currentAtr`/`_currentVolumeRatio` auf `ConcurrentDictionary` umgestellt (wurde ohne Lock geschrieben, während `ApplySlippage()` unter `_rwLock` las) |

### Hohe Fixes (3)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| _equityHistory Race Condition | TradingServiceBase.cs | `lock(_equityLock)` um `Add()` und `GetEquityCurveScaleFactor()` (parallele Loops) |
| WebSocket Fire-and-forget | LiveTradingService.cs | `ContinueWith(OnlyOnFaulted)` mit Error-Logging für `StartUserDataStreamAsync`/`StartTickerStreamAsync` |
| ExitOptimizer neg. TP-Multiplikator | ExitOptimizer.cs | `Math.Max(0.5f, sl/tp)` Floor-Clamp nach Verlierer-Anpassung (extreme AvgLosingTp konnte negativen TP erzeugen) |

### Mittlere Fixes (2)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| MultiModeOrchestrator Dictionaries | MultiModeOrchestrator.cs | `_services`/`_strategyManagers`/`_scannerSettings` auf `ConcurrentDictionary` (StopModeAsync parallel zu IsAnyRunning) |
| FeatureEngine statische Felder | FeatureEngine.cs | `_btcCorrelations`/`_openInterestChanges` auf `ConcurrentDictionary`, float-Felder `volatile` (Multi-Mode parallel) |

### Niedriger Fix (1)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| RiskManager Rolling-Properties Lock | RiskManager.cs | `RollingWinRate`/`ProfitFactor`/`SharpeRatio`/`RecentTrades`/`CheckStrategyHealth()` unter `lock(_lock)` (UI-Thread liest parallel) |

### Toter Code entfernt
| Was | Datei | Grund |
|-----|-------|-------|
| PaperTradingEngine | BingXBot.Backtest/PaperTradingEngine.cs | 19 Zeilen, nirgends referenziert |
| IDataFeed Interface | BingXBot.Core/Interfaces/IDataFeed.cs | Nie implementiert, nie in DI registriert. Parameter aus MarketScanner-Konstruktor entfernt |

### ATI-Pipeline Tiefenprüfung + Fixes (07.04.2026)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| RegisterOpenTrade Key-Kollision | AdaptiveTradingIntelligence.cs, TradingServiceBase.cs | `sourceId` Parameter (Timeframe) in Key aufgenommen. Verhindert Überschreibung wenn Multi-Mode-Instanzen dasselbe Symbol traden. Key: `{Symbol}_{Side}_{Timeframe}` |
| _tradesSinceLastTrain atomar | AdaptiveTradingIntelligence.cs | `Interlocked.Increment/Exchange` statt `++`/`=0`. Korrekte Trade-Zählung bei parallelen CheckAutoTraining-Aufrufen |

### ATI Deep-Dive Fixes (07.04.2026)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| **Multi-Mode ATI-Events fehlten** | DashboardViewModel.cs | **KRITISCH**: Im Multi-Mode-Pfad (Alle Modi) fehlten ALLE ATI-Event-Subscriptions: FeatureSnapshotCompleted, AutoTrainingCompleted, AuditCreated + LoadAtiStateAsync(). ATI-Lernen war im Multi-Mode komplett tot. Fix: `WireUpAtiEventsAsync()` als gemeinsame Methode für Single- und Multi-Mode |
| Multi-Mode SaveAtiState bei Stop | DashboardViewModel.cs | SaveAtiStateAsync() fehlte im Multi-Mode-Stop-Pfad (nur Single-Mode hatte es) |
| **Multi-Mode RiskPresets pro Modus** | MultiModeOrchestrator.cs | **KRITISCH**: Alle 3 Modi teilten identische RiskSettings. Scalping (MaxHold=4h) bekam Swing-Werte (MaxHold=48h). Fix: `CreateRiskSettings(mode)` erstellt pro-Modus RiskSettings mit Preset-Werten (Haltezeit, Cooldown, TP-Ratios, Leverage, RRR) |
| Multi-Mode UI-Initialisierung | DashboardViewModel.cs | Balance, HasAccountData, ShowWelcomeHint, EquitySnapshotTimer fehlten im Multi-Mode-Start |
| **Live Multi-Mode implementiert** | DashboardViewModel.cs, MultiModeOrchestrator.cs | Custom-Preset startet jetzt auch im Live-Modus 3 parallele LiveTradingServices (Scalping M15/90s + DayTrading H1/3min + Swing H4/5min). Stop, Emergency-Stop und Pause/Resume funktionieren |
| ModePrefix in Logs | PaperTradingService.cs, LiveTradingService.cs, MultiModeOrchestrator.cs | `[S]`, `[D]`, `[W]` Prefix im Multi-Mode für unterscheidbare Log-Nachrichten. Paper: `[S] BTC-USDT: Long...`, Live: `LIVE [S] BTC-USDT: Long...` |
| Orchestrator Pause/Resume | MultiModeOrchestrator.cs | `PauseAll()`, `ResumeAll()`, `IsAnyPaused` für Multi-Mode Pause-Button |
| **Paper Multi-Mode Account-Update** | DashboardViewModel.cs, MultiModeOrchestrator.cs | Account-Update nutzte `_paperService.Exchange` (Single-Mode) statt der 3 Orchestrator-Services. Fix: `GetAggregatedPaperAccountAsync()` summiert Balance/Positionen aller 3 Paper-Services |
| Paper Multi-Mode Kapitalaufteilung | MultiModeOrchestrator.cs | Startkapital wird auf 3 Modi aufgeteilt (`initialBalance / 3`) statt 3x volles Kapital |
| Paper Multi-Mode Close Position | DashboardViewModel.cs | Manuelles Schließen sucht Position in allen 3 Paper-Services statt nur im Single-Mode-Service |
| LightGBM Modell bei AUC<0.55 nicht aktiviert | LightGbmClassifier.cs | Train() setzt _predictionEngine jetzt erst NACH AUC-Check (>= 0.55). Vorher wurde schwaches Modell sofort aktiviert und steuerte 60% der ConfidenceGate-Bewertung |
| PredictionEngine Thread-Safety | LightGbmClassifier.cs | `_predictionLock` um Predict() und atomares Swap in Train(). ML.NET PredictionEngine ist nicht thread-safe |
| InvalidateModel() Methode | LightGbmClassifier.cs | Ermöglicht explizites Verwerfen des Modells (genutzt von ATI.Reset()) |
| RegimeDetector.Reset() | RegimeDetector.cs | Neue Methode: Räumt _smoothedScores, _lastRegime, _currentRegime und setzt Transitions auf Defaults. Vorher war DeserializeState("") ein No-op |
| ExitOptimizer.Reset() | ExitOptimizer.cs | Neue Methode: Räumt _exitStats. Vorher war DeserializeState("") ein No-op |
| ATI.Reset() vollständig | AdaptiveTradingIntelligence.cs | Nutzt jetzt die neuen Reset()-Methoden + LightGbm.InvalidateModel() |
| Auto-Save Dreifach-Ausführung | AdaptiveTradingIntelligence.cs, TradingServiceBase.cs | `TryClaimAutoSave()` mit Interlocked-Guard: Im Multi-Mode gewinnt nur ein Service pro Intervall statt 3x parallel zu speichern |

### ATI-Pipeline Verifiziert (07.04.2026)
- FeatureEngine: 25 Features korrekt extrahiert, ConcurrentDictionary für Cross-Market
- RegimeDetector: NormalizeInPlace-Fix verifiziert (Array-Kopien in SmoothScores + ApplyTransitionPrior)
- AdaptiveEnsemble: Strategy.Evaluate() ist pure (keine mutable state-Zugriffe), Gewichte unter Lock
- ConfidenceGate: 16 Bayesian Buckets thread-safe (ConcurrentDictionary + Lock), Cold-Start-Schutz aktiv
- ExitOptimizer: Floor-Clamp korrekt (vor Default-Mix), RecordExitOutcome unter Lock
- LearningLoop: ProcessTradeOutcome → Ensemble + ConfidenceGate + ExitOptimizer + DB alle unter Lock
- Persistenz: SerializeState/DeserializeState alle 4 Komponenten thread-safe
- Auto-Training: LightGBM nur aktiviert bei AUC >= 0.55, PredictionEngine unter Lock, Task.Run nicht-blockierend

### Security-Ergebnisse (alle OK)
- API-Keys: DPAPI (Windows) / AES-256-CBC + PBKDF2 100k (Linux)
- credentials.dat in AppData (nicht im Repo), chmod 600 auf Linux
- Keine Secrets in Logs, Keys in UI maskiert
- WebSocket-API-Key-Felder bereits entfernt (05.04.2026)

## Modi-Audit Fixes (07.04.2026)

### Kritisch (Echtgeld-Schutz)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Live Multi-Mode Position-Recovery | MultiModeOrchestrator.cs | `RecoverOpenPositionsAsync()`: Liest SL/TP aus BingX-Orders + setzt Auto-Breakeven für ALLE Services. Vorher: Keine Recovery nach App-Neustart im Multi-Mode → ungeschützte Positionen |
| Multi-Mode manueller Close | DashboardViewModel.cs | `FindServiceForPosition()` über Orchestrator statt `_liveManager.Service` (war null im Multi-Mode). Signal wird jetzt im richtigen Service entfernt |
| Multi-Mode BotState-Spam | MultiModeOrchestrator.cs, TradingServiceBase.cs | `SuppressBotStateEvents` Flag: StopAll/EmergencyStop publiziert BotState nur einmal statt 3x |

### Backtest-Genauigkeit
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Backtest ApplyPreset | BacktestViewModel.cs | `CreateStrategy()` wendet jetzt den Trading-Modus-Preset an (Scalping/DayTrading/Swing basierend auf Timeframe). Vorher: Default-Parameter unabhängig vom Timeframe |
| Backtest RiskSettings vollständig | BacktestViewModel.cs | Alle ~25 RiskSettings-Felder werden aus dem Preset übernommen. Vorher: Nur 7 Felder → kein Multi-Stage-Exit, kein MinRRR, kein Cooldown im Backtest |
| Backtest HTF-Candles | BacktestEngine.cs, BacktestSettings.cs | `HtfTimeFrame` Property: Lädt automatisch HTF-Candles für Trend-Konfirmation. Vorher: `MarketContext.HigherTimeframeCandles` immer null im Backtest |
| Backtest Preset-Sync | BacktestViewModel.cs | `BacktestSettings.Tp1/Tp2CloseRatio`, `MaxHoldHours` etc. werden aus dem Preset synchronisiert |

### HTF-Timeframe-Handling
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| HTF nicht mehr H4 hardcoded | TradingServiceBase.cs | `_scannerSettings.HtfTimeFrame` statt `TimeFrame.H4`. Scalping→H1, DayTrading→H4, Swing→D1 |
| HtfTimeFrame computed Property | ScannerSettings.cs | `HtfTimeFrame` wird automatisch aus `ScanTimeFrame` abgeleitet (M15→H1, H1→H4, H4→D1) |

### Multi-Mode UI
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| SL/TP-Anzeige Multi-Mode | DashboardViewModel.cs | `FindPositionSignal()` durchsucht alle aktiven Service-Modi (Orchestrator, Paper, Live) statt nur _paperService |
| SL/TP-Edit Multi-Mode | DashboardViewModel.cs | PropertyChanged-Handler leitet Edits an den richtigen Service über `FindServiceForPosition()` |
| Chart-Overlay Multi-Mode | DashboardViewModel.cs | `UpdateChartOverlay()` nutzt `FindPositionSignal()` statt hardcoded `_paperService` |

### Stabilität
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| _consecutiveLosses Tageswechsel | TradingServiceBase.cs | Wird bei Tageswechsel zusammen mit _tradesToday zurückgesetzt. Vorher: Verlustserie von gestern eskalierte Cooldown heute weiter |
| Cooldown-Kommentar korrigiert | TradingServiceBase.cs | Dokumentiert warum Basis-Cooldown deaktiviert ist aber Cooldown-Eskalation aktiv bleibt (Leverage-Reduktion statt Handelspause) |

## Live-Modus Code-Review Fixes (07.04.2026)

Gründliche Prüfung aller Live-Trading-Pfade auf Sicherheit, Race Conditions und Korrektheit.

### Kritische Fixes (3 — Geldverlust-Risiko)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Trailing-Stop auf BingX synchronisieren | TradingServiceBase.cs, LiveTradingService.cs | `OnTrailingStopMovedAsync()` mit Side-Parameter + async Return. LiveTradingService ruft `SetPositionSlTpAsync()` mit Throttle (max 1 Update/30s pro Symbol). Vorher: Nativer SL auf BingX wurde NIE nachgezogen → bei App-Crash ging nachgezogener Gewinn verloren |
| Doppelte Order verhindern | TradingServiceBase.cs | `_positionSignals.ContainsKey()` Check VOR `PlaceOrderOnExchangeAsync()` in beiden Pfaden (ATI + Standard). Verhindert doppelte Position wenn Limit-Order aus vorigem Scan noch pending |
| Emergency-Stop null-Safety | LiveTradingManager.cs | `_restClient` wird bei `StopAsync()` NICHT mehr genullt (PriceTickerLoop könnte noch laufen). Bei `EmergencyStopAsync()` erst nach vollständigem Service-Dispose |

### Hohe Fixes (2)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Native TP-Orders bei Trailing canceln | TradingServiceBase.cs, LiveTradingService.cs | Neuer Hook `OnEnterTrailingPhaseAsync()`. LiveTradingService cancelt alle nativen SL/TP-Orders und setzt nur SL neu. Vorher: BingX schloss Rest-40% ungewollt zum alten TP2-Preis statt Trailing laufen zu lassen |
| Recovery EntryTime Karenz | PositionExitState.cs, TradingServiceBase.cs | Neues `IsRecovered` Property. Nach Recovery: MaxHoldHours=0 für 4h Karenz, dann aktiviert. Vorher: Time-Exit-Uhr startete bei 0, echte Haltezeit nach Neustart unbekannt |

### Mittlere Fixes (3)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| WebSocket-Ticker Handler-Leak | LiveTradingService.cs | `_tickerPriceHandler` als Feld gespeichert, in `DisposeAdditional()` abgemeldet. Vorher: Lambda akkumulierte bei Start/Stop/Start |
| Partial Close Quantity | TradingServiceBase.cs | TP1-CloseQty aus `pos.Quantity` statt `exitState.OriginalQuantity`. BingX kann Quantity truncaten → closeQty war evtl. > echte Position |
| Funding-Rate pro Symbol | TradingServiceBase.cs, LiveTradingService.cs | `_fundingRates` Dictionary statt einzelner `_currentFundingRate`. Jedes offene Symbol wird separat abgefragt (Rates variieren stark zwischen Symbolen) |

### Niedriger Fix (1)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| ListenKey-Reconnect | LiveTradingService.cs | Bei 2+ fehlgeschlagenen Renewals: Neuen ListenKey erstellen + WS-Verbindung neu aufbauen. Vorher: Nur geloggt, kein Reconnect |

### Geänderte Hooks (TradingServiceBase.cs)
| Hook | Alt | Neu |
|------|-----|-----|
| `OnTrailingStopMoved` | `void(string, decimal, decimal)` | `Task OnTrailingStopMovedAsync(string, Side, decimal, decimal)` |
| `OnEnterTrailingPhaseAsync` | (neu) | `Task(string, Side, decimal?)` — nach TP2 in Trailing-Phase |

## ATI-Lernlogik Review Fixes (07.04.2026)

Gründliche Prüfung der gesamten ATI-Pipeline: FeatureEngine, RegimeDetector, AdaptiveEnsemble, ConfidenceGate, ExitOptimizer, LightGBM, ONNX.

### Hohe Fixes (3)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| ConfidenceGate Signal-Richtung | ConfidenceGate.cs | Bucket-Keys enthalten jetzt `L:`/`S:` Prefix (Long/Short). Vorher: "RSI:oversold" trackte Long+Short gemeinsam → profitable "Buy the Dip"-Muster boosteten auch Short-Confidence |
| ONNX Thread-Safety | OnnxModelInference.cs | `_sessionLock` um LoadModel (atomic swap), Predict, PredictBatch und Dispose. Vorher: TOCTOU Race wenn LoadModel die Session disposed während Predict sie nutzt |
| Ensemble-Gewichte Differenzierung | AdaptiveEnsemble.cs | EMA-Targets von 1.2/0.8 auf 1.5/0.5 geändert. 70% WinRate → Gewicht ~1.3 (vorher ~1.08), 30% → ~0.7 (vorher ~0.92). Verdoppelte Differenzierung zwischen guten und schlechten Strategien |

### Mittlerer Fix (1)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Bayesian Shrinkage | ConfidenceGate.cs | Log-Odds-Summe mit Faktor 0.5 multipliziert (Shrinkage). 16 korrelierte Buckets führten zu Overconfidence nahe 0/1. Clamp-Range auf [0.05, 0.95] verengt |

### Review-Runde Fixes (07.04.2026)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| **ExtremePriceSinceEntry=0 bei Recovery** | TradingServiceBase.cs | **KRITISCH**: Default 0 → Short-Momentum-Decay sofort (price-0=riesig). Fix: Entry-Preis als Startwert in ExitState + _extremePriceSinceEntry Dictionary |
| ConfidenceGate Bucket-Migration | ConfidenceGate.cs | Alte Keys ohne L:/S: Prefix werden bei DeserializeState 50/50 auf Long+Short aufgeteilt. Ohne Migration wären alle bisherigen ATI-Lerndaten verloren |
| PredictBatch Fehler-Return | OnnxModelInference.cs | `Array.Fill(0.5f)` statt `new float[]` (Default 0.0f). Konsistent mit Predict() Fehler-Return |
| GetModelInfo Thread-Safety | OnnxModelInference.cs | Unter `_sessionLock` (vorher ohne Lock → ObjectDisposedException möglich) |
| OnEnterTrailingPhase SL-Retry | LiveTradingService.cs | 1 Retry bei SL-Fehler nach TP-Cancel. Ohne SL wäre Position komplett ungeschützt. LogLevel Error statt Warning |

## Umfassende Logik-Analyse Fixes (07.04.2026)

4-Agenten-Review über alle Bereiche. 21 Fixes.

### Kritische Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| GetPositionScaleFactor(0)=0 | CryptoTrendProStrategy.cs | Alle 6 Nicht-CTP-Strategien konnten nie traden. Fix: `_ => 1.0m` |
| MarketFilter Funding-Schwellen 100x falsch | MarketFilter.cs | 0.08 (8%) → 0.0008 (0.08%). BingX liefert Dezimalwerte |
| Cooldown+MaxDailyTrades verdrahtet | TradingServiceBase.cs | MarketFilter-Checks waren nie aufgerufen. Jetzt in ScanAndTradeAsync |
| BTC Health positionScale bearish | MarketFilter.cs | Score<=-3 gab 1.0m → Fix: 0.65m |
| Event-Leaks in 3 ViewModels | Dashboard/TradeHistory/MainVM | IDisposable + benannte Handler + Abmeldung in Dispose() |

### Verbesserungen
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| _consecutiveLosses Interlocked Return | TradingServiceBase.cs | Rückgabewert verwenden statt re-read |
| EmergencyStopAllAsync parallel | MultiModeOrchestrator.cs | Task.WhenAll statt sequenziell |
| Dispose ruft StopAsync | MultiModeOrchestrator.cs | Cleanup für _positionSignals |
| ATI-Events benannte Felder | DashboardViewModel.cs | Saubere Abmeldung möglich |
| EquityTimer CancellationToken | DashboardViewModel.cs | Sauberer Cancel |
| BacktestViewModel IDisposable | BacktestViewModel.cs | CTS Cleanup |
| isBtc Parameter entfernt | CryptoTrendProStrategy.cs | Ungenutzter Parameter |
| SimpleScore normalisiert | MarketScanner.cs | Clamp [0,1] |
| CheckAutoTraining Race Guard | AdaptiveTradingIntelligence.cs | CAS Guard bei Multi-Mode |
| WebSocket _reconnectAttempts | BingXWebSocketClient.cs | volatile |
| DB-Migrationen spezifischer Catch | BotDatabaseService.cs | Nur "duplicate column" fangen |

## Vollaudit Fixes (07.04.2026 - Runde 2)

5-Agenten-Audit (Code-Review, Security, Performance, Architektur, Health). 226 Tests grün.

### Kritische Fixes (2)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| EmergencyStop ATI-Save | LiveTradingManager.cs | `SaveAtiStateAsync()` vor EmergencyStop aufrufen — sonst ATI-Lernzustand bei Notfall verloren |
| TryClaimAutoSave Race | AdaptiveTradingIntelligence.cs | Zeitstempel erst NACH erfolgreichem DB-Save setzen. Neue Methoden: `ConfirmAutoSave()` + `ReleaseAutoSaveClaim()`. Bei DB-Fehler wird beim nächsten Check erneut versucht |

### Hohe Fixes (2)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| SimulatedExchange IDisposable | SimulatedExchange.cs | `IDisposable` implementiert, `_rwLock.Dispose()`. Verhindert ReaderWriterLockSlim-Leak bei Start/Stop |
| PaperTradingService Dispose alte Exchange | PaperTradingService.cs | `_exchange?.Dispose()` vor Neuerstellen in `Start()` |
| LiveTradingService StopAsync CTS-Reihenfolge | LiveTradingService.cs | `CleanupUserDataStreamAsync()` VOR `_cts.Cancel()` — DeleteListenKey braucht nicht-gecancelltes Token |

### Mittlere Fixes (1)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| _consecutiveLosses Thread-Safety | TradingServiceBase.cs | `volatile int` + `Interlocked.Increment/Exchange` an allen 4 Schreibstellen (ProcessCompletedTrade, Tageswechsel, Cooldown-Read) |

### Sonstige Fixes (3)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| ExitOptimizer TP-Formel-Stabilität | ExitOptimizer.cs | Neue Formel: max 15% TP-Reduktion statt unbegrenzter Konvergenz auf Floor. Verhindert dass Lerneffekt bei extremen AvgLosingTp verloren geht |
| MarketScannerTests IDataFeed entfernt | MarketScannerTests.cs | `IDataFeed`-Mock entfernt (Interface existiert nicht mehr). Nutzt jetzt 1-Parameter-Konstruktor `MarketScanner(client, logger)` |
| ConfigTests + RiskManagerTests aktualisiert | ConfigTests.cs, RiskManagerTests.cs | Tests an aktuelle Default-Werte angepasst (MaxPositionSize 20%, CooldownHours 4, MinVolume 20M, ScanInterval 300s, MaxResults 50, MaxTradesPerDay 0) |

## Deep-Dive Audit Fixes (07.04.2026 - Runde 3)

5-Agenten Deep-Dive (Trading-Logik, ATI ML-Pipeline, Exchange+Network, ViewModel+UI). 226 Tests grün.

### Kritische Fixes (7)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| ATR-"Perzentil" war kein Perzentil | TradingServiceBase.cs | `atr/price*10000` statt echtem Perzentil 0-100 → Leverage IMMER maximal reduziert. Fix: `CalculateAtrPercentile()` |
| Bayesian Prior N+1-fach gezählt | ConfidenceGate.cs | Posterior-Odds enthielten Prior (Laplace) + nochmal addiert. Fix: Likelihood-Ratio relativ zum Prior |
| LightGBM Random-Split = Data Leakage | LightGbmClassifier.cs | Random-Shuffle bei Zeitreihendaten. Fix: Temporaler 80/20-Split |
| ATI-Events nach Stop/Start tot | DashboardViewModel.cs | `_atiEventsWired` nur in Dispose() zurückgesetzt. Fix: `UnwireAtiEvents()` in StopBot() |
| MainView PropertyChanged-Leak | MainView.axaml.cs | Handler auf Singleton nie abgemeldet. Fix: DetachedFromVisualTree |
| EquityTimer CancellationToken.None | DashboardViewModel.cs | Timer lief nach StopBot weiter. Fix: Eigener `_equityCts` |
| HTTP SendAsync ohne CancellationToken | BingXRestClient.cs | Request nicht cancellbar. Fix: ct an SendAsync/ReadAsStringAsync |

### Hohe Fixes (4)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| RegimeDetector global statt pro Symbol | RegimeDetector.cs | Multi-Mode: Letztes Symbol bestimmte Regime. Fix: `GetRegimeForSymbol()` |
| GetAdaptiveLeverage Score<=9 zu streng | CryptoTrendProStrategy.cs | Non-CTP (Score=0) immer bestraft. Fix: `score > 0 && score <= 6` |
| Sharpe sqrt(252) statt sqrt(365) | RiskManager.cs | Aktienmarkt-Wert für Krypto. Fix: `Math.Sqrt(365)` |
| BacktestVM geteilter CTS | BacktestViewModel.cs | RunBacktest + RunWalkForward teilten CTS. Fix: Separate CTS-Felder |

### Restliche Fixes (4)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Momentum-Decay Short-Threshold | TradingServiceBase.cs | Shorts: 2.5x ATR statt 1.5x (stärkere Pullbacks nach schnellem Abstieg) |
| Liquidation-Check bei <=2x Leverage deaktiviert | RiskManager.cs | Isolated-Margin-Formel zu konservativ bei Cross-Margin. Bei <=2x kein Liquidationsrisiko → return 0 |
| WalkForward Purge-Gap | WalkForwardOptimizer.cs | 12 Candles Embargo zwischen Train/Test (verhindert Label-Leakage bei offenen Trades) |
| ScannerViewModel IDisposable | ScannerViewModel.cs | CTS wird bei Dispose() gecancelt und disposed |

## Tiefenanalyse Fixes (07.04.2026 - Runde 2)

5-Agenten-Tiefenanalyse: RiskManager-Mathematik, Backtest-Pipeline, Exchange-Client, alle 7 Strategien.

### Kritische Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Liquidation-Formel korrigiert | RiskManager.cs | MMR wurde direkt addiert statt durch Leverage geteilt → Sicherheitsabstand war systematisch falsch. Korrekt: `(1 - MMR) / Leverage` |
| Net-Exposure = Brutto → Netto | RiskManager.cs | Alle Positionen positiv summiert → Hedge-Positionen doppelt bestraft. Fix: Short als negativ, `Math.Abs(net)` |
| Sharpe Population→Sample Varianz | RiskManager.cs, PerformanceReport.cs | `.Average()` (N) → `.Sum() / (N-1)`. Bei 5 Trades: 20% Unterschätzung korrigiert |
| Sortino über ALLE Returns | PerformanceReport.cs | Downside-Deviation nur über negative Returns → über alle (positive als 0). Standard-Sortino-Formel |
| Sharpe Annualisierung Trades/Jahr | PerformanceReport.cs | Fixem sqrt(252) → sqrt(TradesProJahr). Bei H4 mit 1 Trade/Woche war Sharpe massiv überschätzt |
| Limit-Order Fee-Tracking | SimulatedExchange.cs | `ExecuteOrderLocked` (Limit-Fills) setzte `_positionOpenFees` nicht → PnL bei Limit-Orders zu hoch |
| Ticker Bid/Ask korrigiert | BacktestEngine.cs | BidPrice=Candle.Low, AskPrice=Candle.High → realistischer halber Spread (SpreadPercent) |
| TP2 Division-by-Zero Guard | BacktestEngine.cs | `Tp2CloseRatio / (1 - Tp1CloseRatio)` → Guard `Tp1CloseRatio < 1m` |
| Pagination Endlos-Loop | BacktestEngine.cs | API ohne from/to liefert immer gleiche Daten → Abbruch wenn `allCandles.Count == prevCount` |
| Close blockiert Entry | TradingServiceBase.cs | Nach CloseLong/Short sofort re-evaluieren für Entry in Gegenrichtung (Supertrend-Flip ist nur 1 Candle lang) |
| TrendFollow ADX-Bonus richtungsabhängig | TrendFollowStrategy.cs | ADX-Bonus auf BEIDE Seiten → nur in DI-Richtung (+DI > -DI → Long-Bonus). Nutzt `CalculateAdxWithDi()` |
| TrendFollow Confidence Clamp | TrendFollowStrategy.cs | Confidence konnte > 1.0 werden. Fix: `Math.Clamp(0, 1)` vor Signal-Generierung |
| Retry-Delay mit CancellationToken | BingXRestClient.cs | `Task.Delay(backoff)` → `Task.Delay(backoff, ct)` — EmergencyStop wartet nicht mehr 8s |
| ContinueWith → await | BingXRestClient.cs | PlaceTpLimitOrderAsync: AggregateException-Risiko + fehlender TaskScheduler eliminiert |

### Verbesserungen
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| CTP Score-Gleichstand Tiebreaker | CryptoTrendProStrategy.cs | longScore==shortScore && beide >= minScore → Supertrend-Richtung entscheidet |
| Funding-Rate auf aktuellen Preis | SimulatedExchange.cs | ApplyFundingRate: stale MarkPrice → `GetPriceLocked()` für aktuellen Preis |
| Monte Carlo Perzentil Off-by-One | MonteCarloSimulator.cs | Index-Korrektur + Bounds-Check für Perzentil-Array-Zugriff |

## Indikator-Optimierung + Top-100 Filter (08.04.2026)

Basierend auf Recherche (2024-2026 Backtests, BingX-Doku, Krypto-Futures-Studien).

### Top-100 Market-Cap-Filter
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| OnlyTopByVolume + TopCoinsCount | ScannerSettings.cs | Nur die Top-N Coins nach 24h-Volume analysieren. Auf Futures-Börsen korreliert Volume stark mit Market Cap. Default: Top 100 |
| Volume-Ranking im Scanner | MarketScanner.cs | Vor dem Basis-Filter werden alle Ticker nach Volume sortiert und nur die Top-N behalten. Kleine/illiquide Coins ausgefiltert |
| ScannerPreset erweitert | TradingModeDefaults.cs | `OnlyTopByVolume` + `TopCoinsCount` in ScannerPreset Record |
| Multi-Mode + Dashboard | MultiModeOrchestrator.cs, DashboardViewModel.cs | Neue Properties werden bei Preset-Wechsel korrekt übernommen |

### Indikator-Optimierungen (Recherche-basiert)
| Änderung | Alt | Neu | Begründung |
|----------|-----|-----|------------|
| Volume-Multiplikator (alle Modi) | 1.0x | **1.2x** | 1.0x filtert de facto nichts (50% aller Kerzen haben über-durchschnittliches Volume). 1.2x ist echter Filter |
| CTP MinAdx (Default + Swing/DayTrading) | 18/15 | **20** | 20 ist der universelle Standard für "Trend vorhanden". Scalping bleibt bei 15 (ADX auf M15 selten >25) |
| DayTrading Supertrend-Multiplikator | 2.5 | **3.0** | Weniger Whipsaws auf H1. 2.5 erzeugt zu viele Fehlsignale bei Krypto-Volatilität. 3.0 ist Standard für alle TFs |
| Bollinger TP-Multiplikator | 2.0x | **2.5x** | Krypto-Breakouts laufen weiter als traditionelle Märkte. Backtests zeigen 2.5-3.0x ATR als optimalen BB-TP |
| EmaCross Perioden | 12/26 | **9/21** | 9/21 ist De-facto-Standard für Krypto (4H+M15). Schnellere Crosses fangen Momentum-Wechsel früher |
| CTP RSI-Range (Swing) | 40-75 / 25-60 | **42-78 / 22-58** | Krypto-Uptrends halten RSI oft bei 75+. Erweiterte Range vermeidet vorzeitige Signal-Ablehnung |

### Geänderte Defaults-Tabelle
| Setting | Scalping | DayTrading | Swing |
|---------|----------|-----------|-------|
| Volume-Multiplikator | 1.2x | 1.2x | 1.2x |
| MinAdx | 15 | **20** | **20** |
| Supertrend-Mult. | 2.0 | **3.0** | 3.0 |
| RSI Long-Range | 35-65 | 40-70 | **42-78** |
| RSI Short-Range | 35-65 | 30-60 | **22-58** |
| Top-100 Filter | aktiv | aktiv | aktiv |

## Live-Trading Review Fixes (08.04.2026)

9 Findings aus umfassendem Code-Review der Live-Trading-Logik.

### Kritische Fixes (2)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| OriginalQuantity Diskrepanz | TradingServiceBase.cs | OriginalQuantity war `riskCheck.AdjustedPositionSize` statt tatsächlich platzierte Menge. Bei Equity-Scaling oder Score-Scaling wurde TP2-Qty zu groß berechnet → `Math.Min` kappte auf gesamte Rest-Position → Trailing-Phase übersprungen. Fix: `OriginalQuantity = positionSize` (ATI) / `positionSizeStd` (Standard) |
| Multi-Mode dreifacher SL/TP-Trigger | MultiModeOrchestrator.cs | Recovery registrierte Signale in ALLEN 3 Services → 3 PriceTickerLoops versuchten gleichzeitig dieselbe Position zu schließen. Fix: Signal nur im ERSTEN Service registrieren |

### Hohe Fixes (3)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| EmergencyStop cancelt CTS vor Close | LiveTradingService.cs | `_cts?.Cancel()` vor `GetPositionsAsync` → API-Calls konnten fehlschlagen. Fix: CTS-Cancel entfernt, `StopBase()` am Ende cancelt sicher |
| MaxTradesPerDay nie geprüft | TradingServiceBase.cs | `_tradesToday` wurde gezählt aber nie gegen `RiskSettings.MaxTradesPerDay` validiert. Fix: Check in ScanAndTradeAsync nach Session-Filter (0 = unbegrenzt) |
| TP1/TP2 Limit-Order Quantity auf untruncated Basis | LiveTradingService.cs | BingX truncated Quantity auf Symbol-Precision, TP-Qty wurde aber aus Order-Menge berechnet. Fix: Nach Haupt-Order echte Position von BingX lesen, TP-Qty darauf basieren |

### Mittlere Fixes (3)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Multi-Mode StartLive ohne Recovery | MultiModeOrchestrator.cs | `StartLive()` → `StartLiveAsync()` mit integrierter Position-Recovery. Offene Positionen haben jetzt sofort SL-Schutz |
| DeleteListenKeyAsync fire-and-forget in Dispose | LiveTradingService.cs | try-catch um `_restClient.DeleteListenKeyAsync()` — ObjectDisposedException bei Shutdown möglich |
| PositionExitState Thread-Safety Dokumentation | PositionExitState.cs | Kommentar: Properties werden NUR aus PriceTickerLoop mutiert (sequentiell pro Service). ConcurrentDictionary sichert nur Add/Get/Remove |

### Geänderte Methoden-Signaturen
| Methode | Alt | Neu |
|---------|-----|-----|
| `MultiModeOrchestrator.StartLive()` | `void StartLive(restClient)` | `async Task StartLiveAsync(restClient)` — inkl. Auto-Recovery |

### Neue Gotchas
- OriginalQuantity IMMER die tatsächlich platzierte Menge verwenden (nach Equity/Score-Scaling), NICHT `riskCheck.AdjustedPositionSize`
- Recovery-Signale im Multi-Mode NUR in einem Service registrieren (sonst N-facher Close-Versuch)
- EmergencyStop: CTS NICHT vor Close-Operations canceln (API-Calls brauchen funktionierendes HTTP)
- TP-Limit-Orders: Qty aus `GetPositionsAsync()` lesen (BingX truncated auf Symbol-Precision)

## Umfassender App-Audit Fixes (08.04.2026)

4-Agenten-Audit: Trade-Persistenz, UI/UX, Codebase-Health, Profitabilität.

### Kritische Fixes (2)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Backtest-Trades fluten DB | TradeHistoryViewModel.cs | Backtest-Trades wurden bei jedem Run in DB gespeichert ohne Bereinigung → History geflutet. Fix: Backtest-Trades nicht mehr in DB persistieren (nur Paper+Live) |
| SaveTradeAsync fire-and-forget | TradeHistoryViewModel.cs | Live-Trade-Persistierung war fire-and-forget → bei DB-Fehler stiller Datenverlust. Fix: try-catch mit Error-Logging an EventBus |

### Hohe Fixes (3)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| TradeEntity verliert Regime | TradeEntity.cs, BotDatabaseService.cs | CompletedTrade.Regime wurde nie in DB persistiert. Fix: Regime-Feld + DB-Migration v6 |
| Equity beim App-Start nie geladen | DashboardViewModel.cs | Equity-Chart startete bei jedem Start bei 0 trotz DB-Daten. Fix: LoadEquityFromDbAsync() im Konstruktor (letzte 30 Tage) |
| RiskManager SL als Pflicht | RiskManager.cs | Ohne SL wurde 20% Margin als Fallback verwendet → Konto-Risiko. Fix: Trade wird ohne SL grundsätzlich abgelehnt |

### UI-Fixes (4)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Doppelte KeyBindings | MainView.axaml | Identische Ctrl+1-8 Bindings zweimal definiert → toter Code entfernt |
| AppTextMutedBrush fehlt | RiskSettingsView.axaml | 3 Texte referenzierten nicht-existierende Resource → Text unsichtbar. Fix: → `TextMutedBrush` |
| TradeHistory PnL ohne Farbe | TradeHistoryView.axaml, TradeHistoryViewModel.cs | Gesamt-PnL immer weiß statt grün/rot. Fix: TotalPnlColor Property + Binding |
| DB WAL-Modus | BotDatabaseService.cs | SQLite WAL-Modus für bessere Concurrency bei Multi-Mode (3 parallele Services) |

### Neue Gotchas
- Backtest-Trades NICHT in DB speichern — fluten sonst die History bei jedem Run
- SaveTradeAsync bei Live-Trades IMMER mit try-catch absichern — stiller Datenverlust bei DB-Fehler
- SL ist PFLICHT im RiskManager — Trade ohne SL wird grundsätzlich abgelehnt
- DB-Migration v6: Regime-Spalte in Trades + WAL-Modus

## Multi-Asset Trading: TradFi-Support (08.04.2026)

### Neue Dateien
| Datei | Beschreibung |
|-------|--------------|
| `Core/Enums/MarketCategory.cs` | Enum: Crypto, Commodity, Index, Forex, Stock |
| `Core/Helpers/SymbolClassifier.cs` | Prefix-basierte Klassifikation (NCCO/NCSI/NCFX/NCSK) |
| `Engine/Filters/TradingHoursFilter.cs` | Markt-Öffnungszeiten (Krypto 24/7, TradFi Mo-Fr) |

### BUG-FIX: Top-100-Filter fehlte in Trading-Loop
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| OnlyTopByVolume nicht angewendet | ScanHelper.cs | `FilterCandidates()` ignorierte `OnlyTopByVolume`/`TopCoinsCount` komplett → Bot handelte ALLE Symbole inkl. illiquider Meme-Coins. Fix: Top-100 für Krypto, separate TradFi-Filterung |

### TradFi-Symbole (BingX, 94 Stück)
- **Commodities** (23): `NCCOGOLD2USD-USDT`, `NCCOXAG2USD-USDT`, `NCCO1OILWTI2USD-USDT` etc.
- **Indices** (11): `NCSINASDAQ1002USD-USDT`, `NCSISP5002USD-USDT`, `NCSIDOWJONES2USD-USDT`
- **Forex** (27): `NCFXEUR2USD-USDT`, `NCFXGBP2USD-USDT` etc.
- **Stocks** (33): `NCSKTSLA2USD-USDT`, `NCSKNVDA2USD-USDT`, `NCSKAAPL2USD-USDT`
- **Gleiche API-Endpunkte** wie Krypto (Klines, Ticker, Orders)
- **Erkennung via Symbol-Prefix**: `NC` = TradFi, Rest = Krypto

### Per-Markt Risk-Settings
| Kategorie | Default-Leverage | Max-Leverage | Margin | RRR |
|-----------|-----------------|-------------|--------|-----|
| Krypto | 3x | 125x | 20% / 2% | 1.5:1 |
| Rohstoffe | 10x | 500x | 15% / 1.5% | 1.5:1 |
| Indices | 10x | 500x | 15% / 1.5% | 1.5:1 |
| Forex | 20x | 500x | 10% / 1% | 2:1 |
| Aktien | 3x | 25x | 15% / 2% | 1.5:1 |

### Strategie-Anpassungen
- **RSI-Ranges**: Krypto 42-78 (Trends pushen RSI höher), TradFi 30-70 (Standard)
- **BTC-Health/FearGreed/Funding**: Nur für Krypto aktiv, für TradFi neutral
- **ATI FeatureEngine**: 11 krypto-spezifische Features werden für TradFi auf 0/neutral gesetzt
- **ConfidenceGate**: FNG/OI/BTC Buckets nur für Krypto generiert
- **Trading-Hours**: Krypto 24/7, Forex 24/5, Commodities/Indices/Stocks Mo-Fr mit Zeiten

### UI-Änderungen
- **Dashboard**: Markt-Kategorie Checkboxen (Krypto immer an, TradFi opt-in)
- **Risk-Settings**: Per-Markt-Leverage (5 NumericUpDowns) in neuer Sektion
- **Marktspezifische Hebel**: Konfigurierbar pro Kategorie

### Neue ScannerSettings
| Property | Default | Beschreibung |
|----------|---------|--------------|
| EnableTradFi | true | TradFi-Assets aktivieren (alle Märkte per Default) |
| EnabledCategories | {Crypto,Commodity,Index,Forex,Stock} | Welche Kategorien gescannt werden |
| MinVolume24hTradFi | 1M | Eigener Volume-Filter für TradFi |

### Tests (264 gesamt, +38 neue)
| Datei | Tests | Beschreibung |
|-------|-------|--------------|
| Core/SymbolClassifierTests.cs | 25 | Prefix-Erkennung, IsTradFi, Is24x7, IsApiTradeable, DisplayName |
| Engine/TradingHoursFilterTests.cs | 8 | Wochenende, Handelszeiten, Krypto 24/7, Forex 24/5 |

### Neue ScannerSettings (09.04.2026)
| Property | Default | Beschreibung |
|----------|---------|--------------|
| MinPriceChangeTradFi | 0.1% | Eigener PriceChange-Filter für TradFi (Krypto hat 0.5%) |

### Fixes: Multi-Asset Verifizierung (09.04.2026)
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Funding-Settlement blockierte TradFi | MarketFilter.cs + TradingServiceBase.cs | `CheckSession()` blockierte ALLE Symbole 15min/Tag wegen Krypto-Funding-Settlement. Fix: `IsFundingSettlement()` extrahiert, bei gemischtem Scan nur Krypto-Kandidaten im Loop gefiltert |
| MinPriceChange fehlte für TradFi | MarketScanner.cs + ScanHelper.cs | TradFi hatte keinen PriceChange-Filter → unnötige API-Calls. Fix: `MinPriceChangeTradFi=0.1%` in ScannerSettings + im Scanner/ScanHelper angewandt |
| Stock-Handelszeiten zu restriktiv | TradingHoursFilter.cs | Stock 10:00-21:00 → 08:00-24:00 UTC. Commodity/Index: Fast 24/5 mit 1h Pause 22:00-23:00 UTC |
| Scanner ATR%-Schwellen krypto-optimiert | MarketScanner.cs | 5D-Scoring ATR% 1-4% → category-abhängig: Forex 0.05-0.5%, Stock 0.3-2%, Commodity 0.5-3%, Index 0.2-1.5%. Auch Struktur-Score-Schwellen angepasst |
| EnabledCategories-Default divergierte | MultiModeOrchestrator.cs | Alle-Modi-Modus Default war nur `{Crypto}` statt alle 5. Fix: Default konsistent mit ScannerSettings (alle 5 Kategorien) |
| SK-System MinRange für Forex zu hoch | SequenzKonzeptStrategy.cs | 0.5-1.0% filterte fast alle Forex-Sequenzen. Fix: `categoryRangeFactor` skaliert MinRange (Forex: 0.25x, Stock: 0.5x, Index: 0.4x, Commodity: 0.6x) |
| IsHedgeModeActive im Multi-Mode tot | MultiModeOrchestrator.cs | `IsHedgeModeActive` nicht gesetzt → TradFi im Alle-Modi komplett tot. Fix: Paper=true, Live=aus BotSettings |
| IsHedgeModeActive im Single-Mode Paper tot | DashboardViewModel.cs | Single-Mode Paper-Trading setzte `IsHedgeModeActive` nie → TradFi komplett ignoriert. Fix: `_scannerSettings.IsHedgeModeActive = true` vor `_paperService.Start()` |
| EnableTradFi-Fallback false im Orchestrator | MultiModeOrchestrator.cs | `_botSettings.Scanner?.EnableTradFi ?? false` → wenn BotSettings nicht gespeichert: TradFi aus. Fix: Fallback auf `true` (konsistent mit ScannerSettings-Default) |
| Large-Cap-Rabatt galt für TradFi | CryptoTrendProStrategy.cs | MinScore-2 bei >500M Volume galt auch für TradFi → schwache Signale. Fix: Nur für `MarketCategory.Crypto` |
| PriceTickerLoop ohne TradingHours | TradingServiceBase.cs | SL/TP/Trailing auf stale TradFi-Preisen bei geschlossenem Markt. Fix: `IsMarketOpen()` Check → Skip bei geschlossenem Markt |
| Scanner-Rotation Wrap-Around | ScanHelper.cs | Offset-Berechnung erzeugte bei hohem Counter potentielle Duplikate. Fix: `% remaining.Count` + Skip+Concat+Take |
| Balance v2→v3 | BingXRestClient.cs | v3 liefert Array pro Settlement-Asset, USDT-Filter für Futures-Wallet |
| Limit-Order TP bei pending Entry | LiveTradingService.cs | TP bei Limit-Entry übersprungen. Fill-Detection im PriceTickerLoop holt nach |
| Server-Zeit-Sync (Error 100421) | BingXRestClient.cs | `SyncServerTimeAsync()` berechnet Offset lokal↔Server bei Connect |

### Neue API-Features (09.04.2026, verifiziert gegen offizielle BingX API Spec)
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| Kill-Switch (Dead-Man-Switch) | BingXRestClient + LiveTradingService | `ActivateKillSwitchAsync(120s)` alle 60s. Bot-Crash → BingX cancelt nach 2min alle Orders |
| Commission-Rates von API | BingXRestClient + LiveTradingManager | Echte Maker/Taker-Fees beim Connect. VIP-Level-abhängig statt hardcoded |
| Fund-Flow (Income) | BingXRestClient | `GetIncomeHistoryAsync()` — Realized PnL, Funding-Fees, Trading-Fees |
| Order-Amendment | BingXRestClient | `AmendOrderAsync()` — Order atomar ändern (kein Cancel+Replace, keine SL-Lücke) |
| Server-Time v2 | BingXRestClient | Upgrade von v1 auf `/openApi/swap/v2/server/time` |

### Neue Gotchas
- SymbolClassifier: `NCCO`=Commodity, `NCSI`=Index, `NCFX`=Forex, `NCSK`=Stock, Rest=Crypto
- TradFi `EnableTradFi=true` Default → alle Märkte per Default aktiv, UI-Checkboxen steuern Kategorien
- Trading-Hours: TradFi am Wochenende IMMER geschlossen, auch 724-Varianten haben API gesperrt
- RSI-Ranges: 42-78 für Krypto, 30-70 für TradFi — in CryptoTrendPro.Evaluate() per context.Category
- Funding-Rate: Nur für Krypto prüfen (`category == MarketCategory.Crypto`)
- Funding-Settlement: Nur für Krypto relevant — `MarketFilter.IsFundingSettlement()` NICHT auf TradFi anwenden
- MarketContext hat jetzt `Category`-Feld (letzter Parameter, Default Crypto)
- Scanner ATR%-Schwellen MÜSSEN category-abhängig sein — TradFi hat 10-50x niedrigere ATR% als Krypto

## SK-System Vollständigkeits-Audit (09.04.2026)

Online-Recherche des originalen Stefan-Kassing SK-Systems + Abgleich mit Implementierung. 13 Findings, alle gefixt.

### Neue Models/Enums
| Element | Datei | Beschreibung |
|---------|-------|--------------|
| SequenceType Enum | Sequence.cs | Normal (Typ 1, handelbar), Overextended (Typ 2, nur Analyse), Elongated (Typ 3, nur Analyse) |
| SequenceState.FullyCompleted | Sequence.cs | 200% Extension erreicht — Sequenz vollständig abgearbeitet (SK-Regel) |
| HasFullyCompleted(price) | Sequence.cs | Prüft 200% Extension (zusätzlich zu HasReachedTarget für 161.8%) |
| IsTradeableType Property | Sequence.cs | Nur Typ 1 (Normal) darf Entries auslösen |
| DisableSmartBreakeven | SignalResult.cs | SK-Regel: SL NICHT in den Gewinn verschieben (B-C Korrekturen stoppen aus) |

### Neue SequenceDetector-Methoden
| Methode | Datei | Beschreibung |
|---------|-------|--------------|
| CalculateBCKL() | SequenceDetector.cs | BC-Korrekturlevel: 50-66.7% Retracement der B-C Welle ab 100% Extension. Für Re-Entry nach Preis-Reaktion an Extension |
| IsInBCKL() | SequenceDetector.cs | Prüft ob Preis im BCKL-Bereich liegt |
| IsDestabilized() | SequenceDetector.cs | Erkennt Destabilisierung: Preis war in Zone (50-66.7%), hat sie verlassen ohne B-Break. Warnsignal |
| ClassifySequenceType() | SequenceDetector.cs | Klassifiziert A-B-C als Typ 1/2/3 anhand B-C Impulsivität, Zeitverhältnis A-B vs B-C, Richtungskonsistenz |

### Strategie-Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Sequenztyp-Filter | SequenzKonzeptStrategy.cs | Typ 2+3 Sequenzen werden für Entry gefiltert (nur Analyse). Typ 1 = handelbar |
| 200% FullyCompleted Close | SequenzKonzeptStrategy.cs | Neues Close-Signal bei 200% Extension (Confidence 1.0). 161.8% = erste TP-Zone, 200% = abgearbeitet |
| BOS Close-Break | SequenceDetector.cs | DetectBOS prüft jetzt Candle-Body-Close (SK-Regel: kein Docht!). requireCloseBreak Parameter |
| BCKL Re-Entry | SequenzKonzeptStrategy.cs | Neuer Re-Entry-Typ: Preis im BC-Korrekturlevel nach 100% Extension. SL unter Punkt C (engerer Stop). Confidence 0.65 |
| Destabilisierung Confluence | SequenzKonzeptStrategy.cs | -1 Confluence-Abzug bei erkannter Destabilisierung (Preis verlässt Korrektur-Zone) |
| Entry-Zone auf GKL verengt | SequenceDetector.cs | StableInZone-Bestätigung prüft jetzt 55.9-66.7% (GKL) statt 50-66.7%. PriceTouches nutzt weiterhin 50-66.7% |
| Volume SMA20 konsistent | SequenceDetector.cs | HighVolume-Bestätigung nutzt jetzt SMA-20 (wie Confluence-Check) statt Durchschnitt der letzten 3 Kerzen |
| Limit-Order am Golden Ratio | SequenzKonzeptStrategy.cs | Wenn Preis außerhalb Buy/GKL-Zone → Limit-Order am 61.8% Retracement statt Marktpreis |
| Smart BE deaktivierbar | SequenzKonzeptStrategy.cs, TradingServiceBase.cs | SK-Regel "SL NICHT verschieben". DisableSmartBreakeven Flag im Signal. SK setzt true, CTP/andere false |
| ScanHelper Entry-TF | ScanHelper.cs | MarketContext bekommt jetzt M15-Candles als EntryTimeframeCandles (fehlte vorher) |

### Neue Gotchas (SK-System)
- SequenceType: Typ 2 (Überextendiert) = B-C impulsiv/schnell + >80% Richtungskonsistenz. Typ 3 (Langgezogen) = B-C > 3x A-B Dauer + flache Korrektur (<45%)
- BCKL: SL unter Punkt C (nicht A!) — engerer Stop für Re-Entry
- FullyCompleted bei 200% MUSS Positions schließen (höhere Priorität als TargetReached bei 161.8%)
- BOS: IMMER Close-Break prüfen (SK-Regel). Nur Scalping darf Wick-Break nutzen
- Smart Breakeven bei SK deaktiviert — B-C Korrekturen innerhalb der Zielbewegung können SL auf BE-Level triggern
- Destabilisierung: Lookback 10 Kerzen. Prüft ob Preis IN der Zone war und sie dann GEGEN die Sequenz verlassen hat
- ScanHelper Entry-TF: Lädt M15-Candles (24h) als 3. Ebene. Optional — bei Fehler wird auf Primary-TF zurückgefallen
- BCKL-Range: Strecke C→Extension100 (NICHT B→C!). Extension100 = C + A-B Range
- IKI-State MUSS nach Index-Mapping mit aktuellem Preis neu berechnet werden (Sub-Candle-State ist veraltet)
- TargetReached (161.8%) löst KEIN Close-Signal aus — Multi-Stage Exit handhabt TP1. Nur FullyCompleted (200%) schließt
- TP2 im SK-System ist 200% Extension (NICHT 261.8%). 200% = Sequenz abgearbeitet
- Nomenklatur: Unser PointA = SK Punkt 0, PointB = SK Punkt A, PointC = SK Punkt B. Berechnung mathematisch äquivalent
- SK SL/TP-Lebenszyklus: Entry→SL unter Punkt A→TP1 bei 161.8% (30% Partial)→TP2 bei 200% (ALLES schließen). Kein BE, kein Trailing
- Auto-Breakeven bleibt auch bei SK aktiv (Kapitalschutz) — nur Smart-BE nach TP1 ist deaktiviert
- Chandelier-Trailing prüft `DisableSmartBreakeven` — bei SK-Trades wird SL NICHT nachgezogen (bleibt unter Punkt A)
- SK TP2 = Full Close (nicht Partial 30%). `DisableSmartBreakeven` steuert auch den TP2-Modus in TradingServiceBase
- Sequenzcharakter: Jede Welle (A→B, B→C) wird als Impulsiv (I) oder Korrektiv (K) klassifiziert. IK = ideal (+1 Confluence), K* = schwach (-1 Confluence)
- Charakter-Metriken: 40% Richtungskonsistenz + 30% Body/Range-Ratio + 30% Bewegungseffizienz → Score >= 0.55 = Impulsiv
- `Sequence.CharacterPattern` zeigt z.B. "IK" (gut) oder "KI" (schlecht) im Log/Signal-Reason

## Smart Money Concepts (SMC) Integration (09.04.2026)

Institutionelle Preis-Muster als zusätzliche Confluence-Quellen für das SK-System.

### Neue Dateien
| Datei | Beschreibung |
|-------|--------------|
| `Core/Models/SmcModels.cs` | OrderBlock, FairValueGap, StructureConsistency Records + SmcZoneType Enum |
| `Engine/Indicators/SmcAnalyzer.cs` | Static class: FindOrderBlocks, FindFairValueGaps, CheckStructureConsistency |

### Confluence-Erweiterungen
| Quelle | Score | Bedingung |
|--------|-------|-----------|
| Order Block Entry | +1 | Entry in unmitigiertem bullischen OB (Long) / bärischen OB (Short) |
| OB-Widerstand | -2 | Gegenläufiger OB direkt über/unter Entry (<1% Distanz) |
| FVG-Target | +1 | TP (161.8% Extension) liegt in unmitigiertem FVG |
| Multi-TF Aligned | +1 | Alle verfügbaren TFs zeigen gleiche Richtung |
| Multi-TF Divergent | -1 | ≤1 von 3 TFs aligned |

### Weitere neue Methoden
| Methode | Datei | Beschreibung |
|---------|-------|--------------|
| CalculateZigZag | IndicatorHelper.cs | Skender ZigZag-Wrapper → SwingPoints |
| CrossValidateSwings | SequenceDetector.cs | Fractal + ZigZag Kreuzvalidierung |

### Gotchas
- OB-Mitigation: Preis muss Zone BERÜHRT haben (High/Low, nicht nur Close)
- FVG: `candles[i].Low > candles[i-2].High` (bullisch) — High/Low-basiert, nicht Close
- Order Blocks verfallen nach `maxAge=100` Kerzen
- FVG minGapPercent=0.1% filtert Micro-Gaps bei illiquiden Assets
- Alle SK-Features sind in ALLEN Modi aktiv (IKI, BCKL, FVG, OB, MTF) — SK-Regeln sind TF-unabhängig

## Live-Trading-Review Fixes (10.04.2026)

| Severity | Fix | Datei | Beschreibung |
|----------|-----|-------|--------------|
| KRITISCH | Multi-Mode EmergencyStop | MultiModeOrchestrator.cs | EmergencyStopAllAsync() rief EmergencyStop auf ALLEN 3 Services parallel → doppelte Position möglich. Fix: Live-Mode nutzt direkt `_restClient.CloseAllPositionsAsync()` (atomarer BingX-Endpoint), Services werden nur gestoppt |
| KRITISCH | TP2-Quantity | TradingServiceBase.cs | `tp2CloseQty = exitState.OriginalQuantity * Tp2CloseRatio` → `pos.Quantity * Tp2CloseRatio` (BingX truncated Quantity, OriginalQuantity stimmt nach Partial-Fill nicht mehr) |
| KRITISCH | EmergencyStop CancellationToken | LiveTradingService.cs | GetPositionsAsync/GetAllTickersAsync ohne CT → 90s Blockade bei Netzwerkproblem. Fix: Dedizierter CTS mit 10s Timeout + CT-Überladung für GetPositionsAsync |
| HOCH | User-Data-Stream Reconnect | BingXWebSocketClient.cs | UserDataReceiveLoopAsync hatte keinen Auto-Reconnect (Market-WS hatte einen). Fix: ReconnectUserDataStreamAsync() mit exponentiellem Backoff |
| HOCH | Retry-Delay ohne CT | BingXRestClient.cs | HttpRequestException/TaskCanceledException catch-Blöcke: `Task.Delay(backoff)` → `Task.Delay(backoff, ct)` |
| HOCH | Dispose Deadlock | MultiModeOrchestrator.cs | `GetAwaiter().GetResult()` → `Task.Run(() => StopAsync()).Wait(5s)` (verhindert UI-Thread Deadlock) |
| HOCH | WebSocket fire-and-forget | LiveTradingService.cs | ContinueWith → async SafeStartAsync() Wrapper (kein AggregateException-Risiko, kein TaskScheduler-Problem) |
| MITTEL | Min-Order Notional-Check | BingXRestClient.cs | Market-Orders: `checkPrice = request.Price ?? 0m` übersprung Notional-Check. Fix: `lastPrice` Parameter für aktuellen Ticker-Preis |
| MITTEL | Auto-Breakeven Puffer | TradingServiceBase.cs + MultiModeOrchestrator.cs | 0.15% fix → `max(0.15%, SmartBreakevenAtrMultiplier * ATR)` (verhindert Ausstoppen bei volatilen Coins) |
| NIEDRIG | RateLimiter disposed | RateLimiter.cs | `if (_disposed) return` → `ObjectDisposedException.ThrowIf()` (verhindert ungeschütztes Rate-Limiting nach Dispose) |

### Runde 2 (Tiefenreview)

| Severity | Fix | Datei | Beschreibung |
|----------|-----|-------|--------------|
| KRITISCH | LiveTradingManager Recovery BE-Puffer | LiveTradingManager.cs | RecoverOpenPositionsAsync nutzte noch fixen 0.15% statt ATR-basiert. Fix: `CalculateRecoveryAtrAsync()` + `max(0.15%, ATR*Mult)` — konsistent mit TradingServiceBase + MultiModeOrchestrator |
| HOCH | CloseAllPositionsAsync ohne CT | BingXRestClient.cs | Task.Run ohne CT + SendSignedRequestAsync ohne CT. Fix: CT-Überladung, direkte async Calls statt Task.Run |
| HOCH | DashboardVM doppelte Timer | DashboardViewModel.cs | 5x `_ = StartAccountUpdateAsync()` fire-and-forget → `_accountUpdateTask = ...` (Task-Referenz verhindert parallele Timer-Loops) |
| MITTEL | Recovery N API-Calls | LiveTradingManager.cs | GetOpenOrdersAsync pro Position → einmal alle Orders laden, dann per LINQ filtern |
| MITTEL | Balance=0 klare Meldung | RiskManager.cs | Explizite Prüfung `AvailableBalance <= 0` mit "Keine verfügbare Balance" statt "Position-Größe ist 0" |
| MITTEL | SK-Breakeven ATR-Puffer | TradingServiceBase.cs | SK-System Breakeven bei 2× SL-Distanz nutzte noch fixen 0.15% → ATR-basiert wie Auto-BE |

## SK-Kernlogik-Fixes (10.04.2026)

Tiefenprüfung der A-B-C Punkt-Setzung und Trade-Logik gegen das originale SK-System.

### Kritische Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| C-Punkt Validierung verschärft | SequenceDetector.cs | C nur noch bei 50-66.7% Retracement akzeptiert (war 38.2-78.6%). 38.2% = zu flach, 78.6% = fast Invalidierung. Betrifft FindBestSequence, DetectAllSequences, UpdateState |
| Entry NUR bei WaitingBreak | SequenzKonzeptStrategy.cs | CorrectionZone (C noch nicht als Swing bestätigt) blockiert jetzt Entry. SK-Regel: Erst traden wenn C sich stabilisiert hat |
| PointC=null blockiert Trade | SequenzKonzeptStrategy.cs | Ohne bestätigten C-Punkt sind Extensions Fallback-Werte (50% Retracement) → falscher TP. Jetzt NoSignal |
| BuildSequence State-Init vereinfacht | SequenceDetector.cs | B-Break-Prüfung aus BuildSequence entfernt (ignorierte requireCloseBreak). State wird nur noch von UpdateState() gesetzt — korrekt mit Close vs Wick |
| Re-Entry Reihenfolge korrigiert | SequenzKonzeptStrategy.cs | GKL (0.7) → BCKL (0.65) → IKI (0.6). Stärkstes Signal zuerst. Vorher: IKI zuerst → stärkere GKL-Signale verpasst |
| Re-Entry nur bei WaitingBreak | SequenzKonzeptStrategy.cs | IKI + GKL Re-Entry nur wenn C bestätigt (WaitingBreak). Vorher: Auch CorrectionZone → unbestätigte Re-Entries |
| DisableSmartBreakeven in Re-Entries | SequenzKonzeptStrategy.cs | Re-Entry Signale (GKL, BCKL, IKI) setzen jetzt auch DisableSmartBreakeven — vorher fehlte das Flag |
| Alle Presets einheitlich | SequenzKonzeptStrategy.cs | Scalping aktiviert jetzt IKI, BCKL, FVG (waren deaktiviert). SK-Regeln gelten auf jedem TF |

### Neue Gotchas
- C-Punkt MUSS im 50-66.7% Retracement liegen (SK-GKL). NICHT 38.2-78.6%
- Entry NIEMALS bei CorrectionZone — nur WaitingBreak (C als Swing bestätigt)
- BuildSequence berechnet NUR Fibonacci-Level, KEIN State. State kommt von UpdateState()
- Re-Entry Hierarchie: GKL (übergeordnet, stärkstes) → BCKL (BC-Level) → IKI (intern, schwächstes)

- SK-System MinRangePercent MUSS category-abhängig skaliert werden (Forex 0.25x, sonst fast keine Sequenzen)
- MultiModeOrchestrator.EnabledCategories Default MUSS alle 5 Kategorien enthalten (konsistent mit ScannerSettings)
- MultiModeOrchestrator: `IsHedgeModeActive` MUSS gesetzt werden (Paper=true, Live=aus BotSettings) — sonst TradFi komplett tot
- Single-Mode Paper: `_scannerSettings.IsHedgeModeActive = true` VOR `_paperService.Start()` — sonst TradFi auch im Single-Mode tot
- `EnableTradFi` Fallback-Werte MÜSSEN `true` sein (konsistent mit ScannerSettings.EnableTradFi Default)
- Large-Cap-Rabatt (MinScore-2) NUR für `MarketCategory.Crypto` — TradFi hat andere Stabilitäts-Dynamik
- PriceTickerLoop: TradFi-Positionen bei geschlossenem Markt überspringen (stale Preise → falsches SL/TP)
- TradingHours Commodity/Index: Nur 1h Pause 22:00-23:00 UTC (CME Maintenance), NICHT 00:00-01:00 geschlossen
- Scanner-Rotation: `_rotationOffset % remaining.Count` für sauberes Wrap-Around — NICHT `offset * count % max`
- Balance-Endpoint MUSS v3 sein (`/openApi/swap/v3/user/balance`) — v3 liefert Array, nach `asset=="USDT"` filtern
- Kill-Switch: `ActivateKillSwitchAsync()` alle 60s refreshen, bei sauberem Stop `DeactivateKillSwitchAsync()` aufrufen
- Commission-Rates: Beim Connect laden, nicht hardcoden — BingX hat VIP-Levels mit unterschiedlichen Fees
- Limit-Order TP: NICHT sofort platzieren (Position existiert noch nicht). Fill-Detection im PriceTickerLoop
- SyncServerTimeAsync: MUSS bei Connect aufgerufen werden — BingX Error 100421 bei Systemzeit-Abweichung >5s
- Fund-Flow `incomeType`: REALIZED_PNL, FUNDING_FEE, TRADING_FEE, INSURANCE_CLEAR, ADL, TRANSFER
- `_tradesToday` MUSS `volatile` sein — wird per Interlocked geschrieben, ohne Interlocked gelesen (JIT darf cachen → MaxTrades umgehbar)
- `ContinueWith` IMMER mit `TaskScheduler.Default` — ohne expliziten Scheduler nutzt es `TaskScheduler.Current` (kann UI-Thread sein → Deadlock)
- DailyPnl Dictionary: Atomarer Swap (neues Objekt zuweisen), NICHT Clear+Re-Fill (SkiaSharp-Renderer liest auf Render-Thread)
- MultiModeOrchestrator.Dispose: `Task.Run(() => StopAsync())` statt direktem `Wait()` — sonst Deadlock bei SynchronizationContext
- `_klineSemaphore` in Dispose() freigeben — SemaphoreSlim hat OS-Handles, leakt bei Start/Stop-Zyklen
- Sharpe-Ratio Annualisierung: Tatsächliche Trade-Frequenz berechnen, NICHT sqrt(365) (nimmt 1 Trade/Tag an)
- Manueller Close: `_liveManager.CommissionTakerRate` statt hardcodierter 0.0005m — sonst lernt ATI mit falschen PnL-Werten
- State Machine SucheB: INVALIDIERUNG (Low < Point0) MUSS VOR AKTIVIERUNG (Close > PointA) geprüft werden — eine Kerze kann beides gleichzeitig haben
- Short-SL Fallback: Ret500 verwenden (höchstes Level im fibLevels-Array), NICHT Ret382 (nicht im Array, liegt auf falscher Seite)
- TP2 Qty-Formel: `Tp2Ratio / (1 - Tp1Ratio)` normalisieren → 30% vom ORIGINAL statt 30% vom Rest (konsistent mit Backtest)
- WebSocket `_ws.SendAsync` ist NICHT thread-safe — SemaphoreSlim `_sendLock` für alle Send-Aufrufe (Subscribe, Unsubscribe, Pong)
- `AmendOrderAsync`: `RoundPrice`/`TruncateQuantity` anwenden — BingX lehnt Werte mit zu vielen Dezimalstellen ab
- `GetIncomeHistoryAsync`: `startTime.Value.ToUniversalTime()` — ohne UTC-Kind nutzt DateTimeOffset lokale Timezone
- `_requireCloseBreak` entfernt: Toter Code, State Machine nutzt immer Close-Break (ProcessSucheB Zeile 339)

### SK-System Redesign (10.04.2026 — Stefan Kassing Regelkonformität)

#### Holy Trinity Architektur (4H/1H/15m)
- **4H (Navigator)**: HTF-Sequenz bestimmt Richtung + GKL-Zonen. Entry nur in HTF-Richtung.
- **1H (Filter)**: Haupt-TF prüft ob Korrektur im HTF-KL an Schwung verliert. Sequenz-Erkennung hier.
- **15m (Scharfschütze)**: Entry NUR wenn Micro-Sequenz AKTIVIERT (A überschritten = Active). SL unter Micro-0.

#### SK-Regeln implementiert
| Regel | Implementation |
|-------|---------------|
| Entry nur bei WaitingBreak | CorrectionZone gesperrt (C muss Fraktal-bestätigt) |
| Richtungs-Sperre nach Abarbeitung | 20 Kerzen Cooldown (`_completedDirection`) |
| Gegensequenz-Erkennung | Nach FullyCompleted: Gegenrichtung suchen → ins GKL shorten/longen |
| Verschachtelung (Micro-SL) | SL unter 15m-0 statt Macro-A (engerer SL → CRV 1:5 bis 1:10) |
| Micro-Entry nur Active | WaitingBreak auf 15m = kein Entry (Sequenz noch nicht aktiviert) |
| Ohne Micro → kein Entry | Kein Fallback auf Macro-SL wenn 15m-Daten vorhanden |
| Over-Extension-Check | 15m schon bei 100%+ Extension → zu spät, kein Entry |
| Sandwich-Check | 15m-Long im HTF-Short-Ziellevel → KEIN Trade (größerer TF gewinnt) |
| Seitwärts-Filter | ADX < 15 → SK pausiert (Trendfolge-System) |
| HTF-Ziellevel-Blocker | HTF am 161.8%/200% → übergeordnete Bewegung läuft aus |
| Volume-Bestätigung | Low Volume bei Entry → Confidence halbiert |
| BE bei 2× SL-Distanz | SK-Kassing Original (nicht 50% zum TP) |
| SL→TP1 bei 120% Fortschritt | Gestufter Breakeven Stufe 2 |
| TP1 bei Micro-161.8% | 50% schließen, dann Break-Even (Free Ride) |
| TP2 bei HTF-Ziellevel | Rest laufen lassen bis 4H Extension 161.8% |

#### SK Gotchas
- Entry-TF (15m) Micro-Sequenz MUSS State `Aktiviert` haben (State Machine, nicht SequenceState)
- Ohne aktivierte 15m-Micro-Sequenz kein Entry (wenn M15-Daten vorhanden)
- Sandwich-Check: `FromCandlesBoth()` gibt beide Richtungen zurück → Gegenrichtung direkt für Sandwich-Check (kein doppelter FromCandles-Aufruf)
- Over-Extension: 15m schon bei 100%+ Extension → zu spät für Entry
- TP1 = 15m-Extension 161.8% (mit Min-RRR 2:1 Guard)
- TP2 = 4H-Extension 200% (Sequenz abgearbeitet). Fallback 161.8% wenn 200% zu nah
- SK-TP1-Ratio = 50% (via `Tp1CloseRatioOverride=0.5m` im Signal)
- `_completedDirection` + `_completedCooldown`: 20 Kerzen Sperre nach Sequenz-Abarbeitung
- ADX-Filter < 15 auf 4H (eigenständig vom MinAdx der CryptoTrendPro-Strategie)
- `DisableSmartBreakeven = true` → gestufter BE (2× SL, SL→TP1 bei 120%), kein Standard-Smart-BE
- Dochte vs. Bodies: Punkte (0/A/B) mit High/Low, Aktivierung mit Close, Invalidierung mit Low/High
- `SmState.Abgearbeitet` bei 161.8% Extension → `SequenceState.TargetReached` → Richtungs-Sperre
- Invalidierung in `ProcessAktiviert`: Docht (candle.Low/High), NICHT Close
- SL-Buffer: `max(1.5× ATR_15m, 0.15%)` — Liquidity-Grab-Schutz
- 15m-Sequenz Mindestgröße: 0→A Strecke >= 2× ATR_15m (filtert Rauschen)
- Flash-Crash Cooldown: 4H-Kerze > 5% Bewegung → 4 Evaluierungen pausieren. Erster Evaluate eines Klons initialisiert nur `_lastH4Close` (kein Crash-Check ohne Referenzwert)
- MTFA Anti-Deadlock: 4H GKL nur Confluence-Bonus, 1H blockiert nur bei aktiver Gegensequenz
- Gegensequenz: Nach Abarbeitung aktive Suche ins GKL der alten Sequenz (mit gespeicherten GKL-Leveln)
- CheckM15EntryTiming (RSI+Candle-Filter in TradingServiceBase) wird für SK-System ÜBERSPRUNGEN — SK hat eigenen umfassenden 15m-Filter (State Machine, ChoCH, ATR-Mindestgröße, Over-Extension). Der generische RSI>75-Check blockiert SK-Signale kontraproduktiv (Impuls-Momentum ist GEWOLLT bei Aktivierung)
- Short-SL Fallback: `Retracement382` (höchstes Level, nahe Point0/High) statt `Retracement786`. Zusätzlich Seitenprüfung: SL auf falscher Seite → ATR-Notfall-SL
- `ToSequence()` bei SucheB: PointC = null (PotentialB ist instabil/Trailing). Erst ab Aktiviert wird LockedB als PointC gesetzt
- `ToSequence(candles)`: Mit Candles-Parameter für WaveCharacter-Klassifikation (WaveAB/WaveBC + Type). Ohne Candles sind HasGoodCharacter/IsTradeableType immer false/true (tote Confluence-Punkte)
- ProcessSucheB/ProcessAktiviert: Invalidierung ruft sofort `ProcessSuche0(candle, index)` auf (keine Kerze verschwenden, konsistent mit ProcessAbgearbeitet)
- ProcessSucheB: Invalidierung wird VOR Aktivierung geprüft (Kerze mit Docht unter Point0 + Close über PointA → invalidieren, nicht aktivieren)
- Position-Sizing: SK hat eigene Score-Schwellen (100% ab Score 5, 125% ab 10). NICHT CryptoTrendPro.GetPositionScaleFactor verwenden (reduziert SK-Scores 6-7 auf 75%)
- `_lastSkStatus`: Zeigt Symbol-Name + Status des letzten nicht-trivialen Symbols (informativer als generisches "Blocked")
- `Clone()`: `ApplyPreset()` MUSS vor dem ersten Clone aufgerufen worden sein (setzt DisableSmartBreakeven)

## SK-System MTFA-Optimierung (10.04.2026)

### State Machine Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Abgearbeitet-Sackgasse | SequenceStateMachine.cs | `ProcessAbgearbeitet()`: Reset auf Suche0 statt permanent stuck. Ohne Fix: 76-84% aller Evaluierungen blockiert |
| TryActivate Methode | SequenceStateMachine.cs | Gemeinsame Aktivierungs-Logik: Zeit-Proportion + Elliott B-Retracement-Ratio + FibConfidence berechnen |
| Elliott-Properties | SequenceStateMachine.cs | `BRetracementRatio`, `FibConfidence`, `ImpulseRange` — weiche Qualitätsbewertung, kein harter Filter |

### Strategie-Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| 1H ChoCH MTFA-Deadlock | SequenzKonzeptStrategy.cs | ChoCH nur bei aktiver Korrektur prüfen (war vorher IMMER, auch bei endender Korrektur) |
| GKL State-Memory | SequenzKonzeptStrategy.cs | `_h4GklActiveCountdown`: GKL-Flag bleibt 10 Evaluierungen aktiv (15m braucht Zeit zum Drehen) |
| 4H-Sequenz Deduplizierung | SequenzKonzeptStrategy.cs | `_lastH4SeqPointA/LockedB`: Gleiche 4H-Sequenz nicht endlos handeln |
| Fibonacci-Level SL | SequenzKonzeptStrategy.cs | SL am nächsten 4H-Fib-Level (Skip(1) für Liquidity-Grab-Schutz) statt unter 15m-Punkt-0 (war 2-5%) |
| SL-Fallback ATR-Cap | SequenzKonzeptStrategy.cs | 3×ATR_15m statt 15m-Punkt-0 als Fallback (begrenzt Worst-Case-Verlust) |
| TP1 1.5x statt 2.0x | SequenzKonzeptStrategy.cs | Min-TP1 = 1.5× SL-Distanz (Gewinn kommt aus TP2, TP1 dient nur zum Risiko-Rausnehmen) |
| TP1-Ratio 30% statt 50% | SequenzKonzeptStrategy.cs | Weniger bei TP1 schließen, mehr für TP2-Run laufen lassen |
| Sandwich nur aktive SM | SequenzKonzeptStrategy.cs | Sandwich-Check nur gegen AKTIVE Gegensequenz (war: alle historischen → 25% Blockrate) |
| 1H-Filter optional | SequenzKonzeptStrategy.cs | Wenn keine 1H-Daten → 15m-Trigger entscheidet allein (Backtest-Anfang) |
| Log-Nomenklatur | SequenzKonzeptStrategy.cs | SK-Original: `4H:0=... A=... B=...` statt Sequence-Mapping `A=... B=...` |
| Elliott FibConfidence Score | SequenzKonzeptStrategy.cs | B-Punkt nahe idealem Fib-Level → +1 Confluence. Proportions-Bonus für 15m >= 10% von 4H |

### TradingServiceBase Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Klines TF-abhängig | TradingServiceBase.cs | `TimeFrame-Duration × 200` Kerzen (H4: 200 statt 25 Kerzen) |
| SK-Status vom Klon | TradingServiceBase.cs | `_lastSkStatus` vom Symbol-Klon statt Template (Template wird nie evaluiert) |
| Regime-Bypass für SK | TradingServiceBase.cs | `DisableSmartBreakeven` als Bypass-Flag (SK hat eigenen ADX-Filter) |
| M15-Timing Bypass | TradingServiceBase.cs | `CheckM15EntryTiming` übersprungen für SK (eigener 15m-Filter) |

### BacktestEngine Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| M15 Sub-Iteration | BacktestEngine.cs | Innerhalb jeder H4-Kerze alle M15-Kerzen evaluieren (sonst 15/16 Signale verpasst) |
| TF-Mappings für SK | BacktestEngine.cs + LiveBacktestRunner.cs | H1 als HTF, M15 als Entry-TF (war: D1+H1) |
| Regime-Bypass | BacktestEngine.cs | SK-Signale am Regime-Gate vorbeileiten (DisableSmartBreakeven Flag) |
| Entry-TF Zeitraum | BacktestEngine.cs | Gesamten Backtest-Zeitraum laden (war: nur -7d) |

### Exchange Fix
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Klines-Pagination | BingXPublicClient.cs | Bei <1440 Ergebnissen nicht sofort abbrechen (BingX-API liefert manchmal weniger) |

### Backtest-Ergebnisse (90 Tage, 10k Start, MinRiskRewardRatio=2.0)
| Symbol | Trades | PnL | PF | WR | Avg Win | Avg Loss |
|--------|--------|-----|----|----|---------|---------| 
| BTC | 6 | +381 | 26.3 | 83% | +99 | -8 |
| ETH | 4 | -147 | 0.1 | 25% | +24 | -57 |
| SOL | 2 | -118 | 0.2 | 50% | +32 | -149 |
| LINK | 2 | +60 | inf | 100% | +30 | 0 |
| AVAX | 4 | +62 | 2.3 | 50% | +36 | -47 |
| XRP | 2 | +11 | inf | 100% | +5 | 0 |
| DOGE | 0 | 0 | - | - | - | - |
| **Gesamt** | **20** | **+249** | - | **67%** | - | - |

### Neue Gotchas (SK-System Session 10.04.2026)
- State Machine `Abgearbeitet` MUSS auf `Suche0` resetten (sonst permanent stuck → 0 Trades)
- `FromCandles()` verarbeitet alle Kerzen von 0 → nach Reset findet sie NEUE Sequenzen (nicht die alte nochmal wenn 4H-Dedup aktiv)
- Sandwich-Check NUR gegen `SmState.Aktiviert` Gegensequenzen, NICHT gegen historische DetectAllSequences (25% Blockrate!)
- 1H-Filter ist OPTIONAL — wenn keine Daten → 15m-Trigger entscheidet allein (kein harter Block)
- Elliott B-Retracement: KEIN harter Filter in der State Machine (killt profitable Krypto-Setups). Nur FibConfidence als Score-Bonus
- MinRiskRewardRatio im RiskManager = 2.0 (TP1-basiert). Strategie hat eigenes RRR >= 3 auf TP2-Basis. RRR3 im RiskManager blockierte 6+ profitable BTC-Trades
- Fib-SL Skip(1) ist ABSICHT: Nicht das direkte nächste Level (Liquidity-Grab), sondern das übernächste
- SL-Fallback: 3×ATR_15m statt 15m-Punkt-0 (begrenzt Verluste auf ~1% statt 3-5%)
- Log zeigt SK-Original-Nomenklatur: `4H:0=Punkt0 A=PunktA B=PunktB` (nicht Sequence.PointA/B)

## SK-System Verifikation (10.04.2026)

Vollständiges Audit des SK-Systems gegen den SK-Regel-Katalog (36 Regeln in 6 Kategorien).
Report: `SK_VERIFY_REPORT.md` im Projekt-Root. Ergebnis: 17 korrekt, 14 Abweichungen, 5 fehlend → alle gefixt.

### Kritische Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Isolated Margin | LiveTradingService.cs | `SetMarginTypeAsync(symbol, Isolated)` VOR jeder Order. War nie aufgerufen → Cross Margin = gesamtes Konto exponiert |
| GKL-Berechnung | SequenzKonzeptStrategy.cs | War: 55.9/66.7% der 0→A Strecke. Korrekt: 50/66.7% der 0→Extension1618 (Gesamtstrecke). Fundamental falsche Kaufzonen für Gegensequenzen |
| ExitState-Persistenz | BotDatabaseService.cs, LiveTradingManager.cs, TradingServiceBase.cs | ExitStates + RuntimeState (TradesToday, Losses, Cooldowns) in SQLite. Ohne: TP1 nach Neustart doppelt geschlossen |
| Orphaned Orders | LiveTradingService.cs | `CancelNativeSlTpOrdersAsync` bei manuell geschlossenen Positionen (verwaiste-Signal-Erkennung) |
| SimulatedExchange | SimulatedExchange.cs | `MarginType.Cross` → `MarginType.Isolated` (Paper-Trading spiegelt jetzt Live-Modus) |

### Neue Features (SK-Regel-Konformität)
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| Trailing High/Low | SequenceStateMachine.cs | `CurrentHigh`/`CurrentLow` Properties + Tracking in ProcessAktiviert. Basis für dynamische BC-Zone |
| Dynamische BC-Zone | SequenceStateMachine.cs | `GetDynamicBcZone()` — 50-66.7% Retracement von B bis CurrentHigh/Low |
| 38.2% Mindestaktivierung | SequenzKonzeptStrategy.cs | Preis muss mindestens 38.2% Extension nach 15m-Aktivierung erreichen (zu schwache Bewegungen gefiltert) |
| Symbol-Cooldown | TradingServiceBase.cs | 4h Sperre pro Symbol nach Verlust-Trade (gegen Rache-Trades). ConcurrentDictionary + DB-Persistenz |
| 4H-ATR-Rausch-Filter | SequenzKonzeptStrategy.cs | 4H-Sequenz muss >= 2× ATR_4H sein (fehlte, nur 15m hatte den Filter) |

### Default-Anpassungen (SK-Regel-Konformität)
| Setting | Alt | Neu | Regel |
|---------|-----|-----|-------|
| MaxMarginPerTradePercent | 2% | 1% | [5.5] Max 1% Risiko pro Trade |
| MaxOpenPositions | 10 | 3 | [5.2] Max 3 offene Trades |
| ADX-Schwelle (Krypto) | 15 | 20 | [5.1] ADX < 25 = Seitwärtsmarkt |
| ADX-Schwelle (TradFi) | 12 | 15 | [5.1] ADX < 25 = Seitwärtsmarkt |

### Neue Gotchas
- GKL MUSS auf Gesamtstrecke (Point0→Extension1618) basieren, NICHT auf 0→A Strecke. Die Felder heißen jetzt `_completedGkl500`/`_completedGkl667`
- `SetMarginTypeAsync` VOR jeder Order aufrufen — BingX-Default kann Cross sein (try-catch: Fehler bei offener Position ignorieren)
- `PositionExitState` wird jetzt in DB persistiert → nach Neustart korrekte Phase (kein doppelter TP1)
- `_symbolCooldowns` ConcurrentDictionary: 4h Sperre nach Verlust, wird in ScanAndTradeAsync geprüft und in DB persistiert
- `CurrentHigh`/`CurrentLow` in SequenceStateMachine: Wird bei jeder Kerze in ProcessAktiviert aktualisiert, Reset bei Invalidierung und State-Reset
- `TryActivate` hat jetzt einen `Candle`-Parameter (für CurrentHigh/Low Initialisierung bei Aktivierung)
- Multi-Mode Live: `_isHedgeModeActive` wurde aus `_botSettings.Scanner?.IsHedgeModeActive` gelesen — ist `[JsonIgnore]` = IMMER false → TradFi komplett tot. Fix: `restClient.IsHedgeModeAsync()` direkt abfragen

## SK-System Vollständige Regelkonformität (10.04.2026)

Vollständige Verifikation aller SK-Regeln aus SK_CLAUDE_INSTRUCTIONS.md. Alle Abweichungen behoben.

### Fixes
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| TP1 50% statt 30% | SequenzKonzeptStrategy.cs | `Tp1CloseRatioOverride: 0.5m` — SK-Regel 4.5: 50% bei TP1 |
| SL ATR-primär | SequenzKonzeptStrategy.cs | SL = Point0 - max(1.5×ATR_15m, 0.15%) — SK-Regel 4.2 |
| BC-Zone als Confluence | SequenzKonzeptStrategy.cs | `GetDynamicBcZone()` gibt +1 Score wenn Preis in 50-66.7% Zone |
| ADX auf 1H | SequenzKonzeptStrategy.cs | ADX-Check auf 4H UND 1H — SK-Regel 5.1 |
| Zonen-Memory 10 Kerzen | SequenzKonzeptStrategy.cs | GKL-Flag 40h (10×4H) aktiv, nicht 10 Evaluierungen — SK-Regel 3.4 |
| EmergencyStop State-Save | LiveTradingManager.cs | ExitStates + RuntimeState bei NotfallStop speichern — SK-Regel 6.1 |
| _dbService Null-Guards | LiveTradingManager.cs | Null-Checks in Start/Stop/Emergency — SK-Regel 6.1 |
| Multi-Mode State-Persistenz | MultiModeOrchestrator.cs | ExitStates + RuntimeState in StopAllAsync — SK-Regel 6.1 |
| Gescheiterte→Größere Sequenz | SequenceStateMachine.cs | Fix M: Invalidierung prüft Upgrade auf größere Struktur |
| Mehrere Sequenzen pro Symbol | SequenzKonzeptStrategy.cs | Fix H: DetectAllSequences evaluiert parallele Kandidaten, beste RRR gewinnt |

### Neue Gotchas
- SK-System: TP1 Ratio MUSS 0.5 (50%) sein, NICHT 0.3 (CTP-Default). Über `Tp1CloseRatioOverride` im Signal
- SK-System: SL primär ATR-basiert (1.5× ATR_15m), Fib-Level nur noch Confluence. Buffer-Faktor konfigurierbar via `_slBufferPercent`
- SK-System: ADX blockiert auf BEIDEN Timeframes (4H UND 1H). ADX < 20 (Krypto) / < 15 (TradFi) = kein Entry
- SK-System: Zonen-Memory = 10 × 4H-Kerzen (40h), zeitbasiert über `_h4GklLastTouchTime` (nicht Evaluierungszähler)
- SK-System: Gescheiterte Sequenz kann größere bilden: `FailedPoint0`/`FailedPointA`/`PromotedToLarger` in SequenceStateMachine
- SK-System: `DetectAllSequences()` evaluiert parallele Kandidaten — beste RRR (>20% besser + >2:1) ersetzt SM-Sequenz
- Multi-Mode + EmergencyStop: State-Persistenz ist PFLICHT (ExitStates + RuntimeState). Ohne: TradesToday/Cooldowns/TP-Phase verloren

## SK-System Tiefenverifikation + Implementierung (11.04.2026)

5-Agenten-Tiefenaudit aller 35 SK-Regeln aus SK_CLAUDE_INSTRUCTIONS.md. 10 Abweichungen + 3 fehlende Features + 3 Bugs gefunden und behoben.

### Kritische Bugs gefixt
| Fix | Datei | Beschreibung |
|-----|-------|--------------|
| Multi-Mode State-Load | MultiModeOrchestrator.cs | `StartLiveAsync()` lädt jetzt RuntimeState + ExitStates aus DB — vorher: alles bei 0 nach Neustart |
| Multi-Mode EmergencyStop Save | MultiModeOrchestrator.cs | `EmergencyStopAllAsync()` speichert State VOR Stop — vorher: State-Verlust bei Notfall |
| SetMarginType Logging | LiveTradingService.cs | Leerer catch → differenziertes Logging bei echten Fehlern (nicht nur erwartete Ignore) |

### Fehlende Features implementiert
| Feature | Datei | Beschreibung |
|---------|-------|--------------|
| Fahrplan (MarketBias) | SequenzKonzeptStrategy.cs | EMA-200 auf 4H → Long/Short-Bias. Trades gegen Bias blockiert. Bei jedem Evaluate neu bewertet |
| Impulsive Reaktion | SequenzKonzeptStrategy.cs | Prüfung ob 15m-Aktivierung impulsiv war: >=3 Trend-Kerzen ODER Body > 1.5×ATR |
| Sequenz-IDs → Untersequenzen | SequenzKonzeptStrategy.cs | 1H-Sequenz in 4H-Zone als sekundäre Bestätigung (+2 Confluence) |

### Abweichungen behoben
| Regel | Datei | Fix |
|-------|-------|-----|
| 2.4 100er Extension Gate | SequenceStateMachine.cs | `Has100ExtensionReached` Property — BC erst valid nach 100% |
| 3.10 Wendebereiche | SequenzKonzeptStrategy.cs | GKL/SMC/ATH/ATL Check, -1 ohne validen Wendebereich |
| 3.11 Bottom-Up Feedback | SequenzKonzeptStrategy.cs | `RecordTradeOutcome()` — 3+ Verluste in Richtung pausiert |
| 3.12 Stabilisierung | SequenzKonzeptStrategy.cs | `DetectEntryConfirmation()` verdrahtet, +2 Confluence |
| 3.13 Overtracing | SequenceStateMachine.cs | `SmState.Gewarnt` + `InvalidationTolerance` (0.3×ATR) |
| 3.13 Refactoring | SequenceStateMachine.cs | `InvalidateAndPromote()` gemeinsame Methode für alle Pfade |

### Neue SM-States
| State | Beschreibung |
|-------|--------------|
| `SmState.Gewarnt` | Docht unter Point0 aber Close OK → mögliches Overtracing. Nächste Kerze entscheidet: Erholung → Aktiviert, Bestätigung → Invalidiert |

### Neue SM-Properties
| Property | Beschreibung |
|----------|--------------|
| `InvalidationTolerance` | ATR-basierte Toleranz (0.3×ATR), von Strategy gesetzt |
| `Has100ExtensionReached` | True wenn 100% Extension seit Aktivierung erreicht |

### Neue Strategy-Felder
| Feld | Beschreibung |
|------|--------------|
| `_lastFahrplanBias` | EMA-200 Bias (true=Long, false=Short, null=Neutral) |
| `_lastEma200` | Letzter EMA-200 Wert für Logging |
| `_consecutiveFailsInDirection` | Bottom-Up Counter für Verluste in Fahrplan-Richtung |

### Neue Gotchas
- Fahrplan: EMA-200 auf 4H ≈ D1-EMA-33. Wird bei jedem Evaluate berechnet. Kein Fahrplan = neutral (nicht blockieren)
- Overtracing: `SmState.Gewarnt` ist >= `SmState.Aktiviert` → `ToSequence()` mappt auf `Active` (Trade läuft weiter)
- Impulsive Reaktion: Prüft die letzten 5 Kerzen NACH 15m-Aktivierung. Ohne Impuls: kein Entry
- 100er Gate: `Has100ExtensionReached` wird bei Invalidierung zurückgesetzt (`InvalidateAndPromote`)
- Bottom-Up: Nur Verluste IN der Fahrplan-Richtung zählen. Verluste gegen Fahrplan = irrelevant
- Sekundäre Sequenz: Nur wenn Preis in 4H-GKL UND 1H-Daten vorhanden → +2 Confluence
- Wendebereiche: -1 Score (nicht harter Block). Kann unter MinConfluence fallen → effektiver Filter

## SK-System Killer-Fixes — Trade-Frequenz (11.04.2026)

8 Blocking-Point-Entschärfungen. Die 38 Blocking-Points in der Strategy töteten fast alle Signale.

### Implementierte Killer-Fixes

| Killer | Fix | Datei | Beschreibung |
|--------|-----|-------|--------------|
| K1 | 4H-Dedup → Time-Lock | SequenzKonzeptStrategy.cs | Nur blockieren wenn `_signalCooldown > 0` (≈2h), nicht permanent. Mehrere 15m-Entries in derselben 4H-Sequenz möglich |
| K2 | Impuls-Check erweitert | SequenzKonzeptStrategy.cs | Fenster 5→8 Kerzen + Methode 3: Netto-Bewegung > 1× ATR |
| K3 | 100er Extension → 138.2% | SequenzKonzeptStrategy.cs | Entry erlaubt bis 138.2% Extension (statt 100%). BC-Zone noch valid |
| K4 | RRR gestaffelt | SequenzKonzeptStrategy.cs | Score>=8: 1.5:1, >=6: 2.0:1, >=4: 2.5:1, sonst 3.0:1 |
| K5 | Bottom-Up → Confluence | SequenzKonzeptStrategy.cs | 3+ Verluste erhöhen adjustedMinConfluence statt harter Block |
| K6 | BTC Health softer | MarketFilter.cs | `AllowLong` ab Score -3 statt -1. Nur extremer Crash (-4) blockt |
| K7 | Cooldown 20→8 | SequenzKonzeptStrategy.cs | Richtungs-Sperre: 8 statt 20 Evaluierungen (≈2h statt 5h) |
| K8 | 15m FromCandlesBoth | SequenzKonzeptStrategy.cs | Direkte Richtungswahl statt "beste" Sequenz (kann falsche Richtung sein) |

### Neue Gotchas (Killer-Fixes)
- KILLER #1: `_signalCooldown` wird bei Signal-Erzeugung auf 8 gesetzt und pro Evaluate dekrementiert. Nach Ablauf darf dieselbe 4H-Sequenz neue 15m-Entries generieren
- KILLER #3: 138.2% Extension = LockedB + impulseRange × 1.382 (impulseRange = |PointA - Point0|)
- KILLER #4: RRR wird NACH dem Score berechnet (nicht davor). Score beeinflusst Min-RRR
- KILLER #5: `adjustedMinConfluence = _minConfluence + _consecutiveFailsInDirection - 1` (ab 3 Fails)
- KILLER #6: PositionScale (65-100%) deckt moderate BTC-Schwäche ab. Harter Block nur bei -4
- KILLER #8: `FromCandlesBoth()` gibt 3 Werte zurück: (primary, longMachine, shortMachine). Primary wird mit `_` verworfen

## SK-System Re-Verifikation — BuyZone/GKL-Fix (11.04.2026)

Re-Audit der SK_CLAUDE_INSTRUCTIONS.md durch 3 parallele Analyse-Agenten + manuelle Code-Verifizierung. Der vorherige Report behauptete "35/35 korrekt" — tatsächlich waren 2 kritische Fibonacci-Zonen noch FALSCH.

### Gefixte Bugs

| Bug | Datei | Alt | Neu | Auswirkung |
|-----|-------|-----|-----|------------|
| BuyZone falsch | Sequence.cs | 50-61.8% (Ret500-Ret618) | 50-66.7% (Ret500-Ret667) | +10-20% qualifizierte Signale |
| GKL-Zone falsch | Sequence.cs | 55.9-66.7% (Ret559-Ret667) | 50-66.7% (Ret500-Ret667) | GKL-Oberkante verpasst |
| IsDestabilized Zone | SequenceDetector.cs Z.520 | Ret559-Ret667 | Ret500-Ret667 | Konsistenz |
| DetectEntryConfirmation Zone | SequenceDetector.cs Z.856 | Ret559-Ret667 | Ret500-Ret667 | Konsistenz |

**Warum kritisch:** `h4Seq.IsInBuyZone(currentPrice) || h4Seq.IsInGklZone(currentPrice)` (Strategy Z.247) steuert den +2 Confluence-Bonus. Die alten Zonen verpassten Entries zwischen 61.8-66.7% (BuyZone) und 50-55.9% (GKL) — laut SK genau die Level wo der Markt am häufigsten reagiert.

**Hinweis:** `GetDynamicBcZone()` in SequenceStateMachine war bereits korrekt (50-66.7%). Die `_completedGkl500/667` Berechnung in der Strategy (Z.281-287) war ebenfalls korrekt. Nur das Sequence-Modell und SequenceDetector hatten die alten falschen Werte.

### Weitere Abweichungs-Fixes (gleiche Session)

| Abweichung | Fix | Datei | Beschreibung |
|-----------|-----|-------|--------------|
| #3 EMA-200 Fahrplan | Soft-Filter | SequenzKonzeptStrategy.cs | Hard-Block → -1 Confluence-Malus. SK: Mean Reversion erlaubt Trades gegen EMA (BLASH) |
| #5 4H-Primary Richtung | Fahrplan-Fallback | SequenzKonzeptStrategy.cs Z.190 | Wenn primary gegen Fahrplan → aligned Machine versuchen (statt Block) |
| #6 CompletedGkls | GKL-Historie | SequenceStateMachine.cs | `CompletedGkls` Liste (max 5 Einträge) + `CompletedGklEntry` Record. GKLs überleben ProcessAbgearbeitet-Reset |

### Neue Gotchas

- `Retracement559` sollte NICHT als Zone-Grenze verwendet werden, nur als Confluence-Level (Preis exakt am 55.9er → +1 Score). Zone-Grenzen sind IMMER 50% und 66.7%
- `IdealBuyZone` und `GklZone` Properties in Sequence.cs sind jetzt identisch (beide 50-66.7%). Der Unterschied ist semantisch: BuyZone = BC-Korrektur, GKL = Gesamtkorrektur
- EMA-200 ist KEIN Hard-Block mehr. `_tradeAgainstEma` Flag wird in Confluence-Score als -1 verrechnet. SK-System erlaubt bewusst Trades gegen den Trend (an Wendebereichen)
- 4H-Primary wird jetzt bei Fahrplan-Mismatch durch die aligned Machine ersetzt (wenn die mindestens SucheB hat)
- `CompletedGkls` in SequenceStateMachine speichert max 5 GKL-Zonen abgearbeiteter Sequenzen (mit Zeitstempel). NICHT in DB persistiert (bei Neustart aus Candles rekonstruiert)
- `CompletedGklEntry` Record: (Gkl500, Gkl667, IsLong, CompletedAt)
- Vollständiger Verifikationsbericht: `SK_VERIFY_REPORT.md` im Projekt-Root

## SK-System Infra-Bug-Fixes (11.04.2026 — Zweite Re-Verifikation)

Vollständige Abarbeitung der SK_CLAUDE_INSTRUCTIONS.md. 3 verbleibende Infra-Bugs identifiziert und gefixt.

### Infra-Bug-Fixes
| Bug | Datei | Beschreibung |
|-----|-------|--------------|
| Scanner Momentum statt Reversal | TradingModeDefaults.cs, ScannerSettings.cs | `ScanMode` in ScannerPreset Record ergänzt. Swing-Preset = `ScanMode.Reversal` (SK = Mean-Reversion). Multi-Mode + SingleMode übernehmen Mode aus Preset |
| MinPriceChange 0.5% | ScannerSettings.cs, TradingModeDefaults.cs | Default 0.5%→0.1%. Swing-Preset 0.3%→0.1%. SK-Stabilisierungsphasen (Prestabilisation) werden nicht mehr ausgefiltert |
| ScanMode-Anwendung | MultiModeOrchestrator.cs, DashboardViewModel.cs | Beide Pfade (Multi-Mode + SingleMode) übernehmen `ScanMode` aus dem ScannerPreset |

### Neue Gotchas
- `ScannerPreset.Mode` ist nullable (`ScanMode?`). Bei null wird `ScanMode.Momentum` als Fallback verwendet
- Swing-Preset (Default-Fall in GetScannerPreset) setzt `ScanMode.Reversal` — betrifft SK-System und alle Swing-Strategien
- MinPriceChange 0.1% + Top-100-Volume-Filter = ausreichender Spam-Schutz ohne SK-Kandidaten zu verlieren
- Reversal-Modus gewichtet Struktur 35% (statt 10%) und Trend nur 10% (statt 30%) — optimal für Mean-Reversion

### Naming-Refactoring (Abweichung #4 behoben)
| Datei | Änderung |
|-------|----------|
| Sequence.cs | Properties: PointA→Point0, PointB→PointA, PointC→PointB (SK-Nomenklatur) |
| ChartOverlay.cs | Record-Parameter: PointA→Point0, PointB→PointA, PointC→PointB |
| SequenceStateMachine.cs | ToSequence(): `Point0=a, PointA=b, PointB=c` + Kommentar-Aktualisierung |
| SequenzKonzeptStrategy.cs | Alle Sequence-Referenzen aktualisiert (Dedup, Signal, Logging) |
| DashboardViewModel.cs | SequenceOverlay-Erstellung: Point0/PointA/PointB |
| InteractiveChartRenderer.cs | Punkt-Marker: "0"/"A"/"B" statt "A"/"B"/"C" + Farben umbenannt |

### Neue Gotchas (Naming)
- Sequence.Point0 = SM.Point0 = SK's Punkt 0 (Ursprung). KONSISTENT!
- Sequence.PointA = SM.PointA = SK's Punkt A (Impulsgipfel). KONSISTENT!
- Sequence.PointB = SK's Punkt B (Korrekturende, nullable). SM.LockedB/PotentialB.
- Private Felder `_lastSignalPointA/B/C` in Strategy speichern semantisch Point0/PointA/PointB (historische Namen, intern konsistent)
