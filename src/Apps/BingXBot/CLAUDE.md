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
BingXBot.Core        <- Domain (Models, Enums, Interfaces, SimulatedExchange, DB-Entities)
BingXBot.Exchange    <- BingX REST + WebSocket API Client
BingXBot.Engine      <- Trading-Logik (Strategien, Scanner, Risk, Indikatoren mit Struct-Cache)
BingXBot.Backtest    <- Backtesting + Paper-Trading
BingXBot.Shared      <- Avalonia UI (ViewModels inkl. Sub-VMs, Views, Services mit TradingServiceBase)
BingXBot.Desktop     <- Desktop Entry-Point
```

## Strategien (6 Stück, alle Krypto-optimiert)

| Strategie | Datei | Logik |
|-----------|-------|-------|
| Trend-Following | TrendFollowStrategy.cs | Multi-Indikator (EMA+RSI+MACD+Volume), 5 Bedingungen, Confidence-basiert |
| EMA Cross | EmaCrossStrategy.cs | EMA-Cross + Volume + EMA200 Trend-Filter + ATR-Volatilitätsfilter |
| RSI Momentum | RsiStrategy.cs | RSI als Momentum-Indikator + Divergenz-Erkennung + Volume-Konfirmation |
| Bollinger Breakout | BollingerStrategy.cs | Squeeze-Erkennung + Breakout + Volume-Konfirmation |
| MACD | MacdStrategy.cs | Histogram-Momentum + Zero-Line-Cross + Trend-Kontext |
| Smart Grid | GridStrategy.cs | Dynamische Grenzen via Bollinger, nur in Range-Märkten (EMA+ATR Trend-Check) |

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
| Dashboard | Balance, Positionen, Bot-Controls, Strategie-Auswahl, Equity-Chart, Live-Trading | BotEventBus, StrategyManager, PaperTradingService, RiskSettings, ScannerSettings, IPublicMarketDataClient?, BotDatabaseService?, ISecureStorageService? + Sub-VMs: BtcTickerViewModel, ActivityFeedViewModel |
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
| DashboardViewModel | BotEventBus, StrategyManager, PaperTradingService, RiskSettings, ScannerSettings, IPublicMarketDataClient?, BotDatabaseService?, ISecureStorageService? |
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

## Tests (180 Tests)

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
| FeatureEngine | ATI/FeatureEngine.cs | Extrahiert 19 normalisierte Features aus MarketContext (Preis, Momentum, Volatilität, Trend, Volumen, Session) |
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
| FeatureSnapshot | 19 normalisierte Features + Metadaten + ToFeatureArray() |
| EnsembleVote | Konsens-Signal + Gewichte + Einzelstimmen |
| TradeAudit | Vollständiger Audit-Trail jeder Entscheidung |
| FeatureSnapshotEntity | DB-Entity für ML-Training (19 Features + Outcome) |

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

## Tests (201 Tests)

## Farbpalette

Dark-Trading-Theme: Primary #3B82F6, Background #1E1E2E, Profit #10B981, Loss #EF4444
