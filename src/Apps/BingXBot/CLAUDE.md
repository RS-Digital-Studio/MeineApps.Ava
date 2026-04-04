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

## Strategien (7 Stück, CryptoTrendPro als Default)

| Strategie | Datei | Logik |
|-----------|-------|-------|
| **CryptoTrendPro** | CryptoTrendProStrategy.cs | **Primär-Strategie**: Supertrend + Confluence-Scoring (0-12) + vol-adaptive SL/TP + Multi-Stage Exit |
| Trend-Following | TrendFollowStrategy.cs | Multi-Indikator (EMA+RSI+MACD+Volume), 5 Bedingungen, Confidence-basiert |
| EMA Cross | EmaCrossStrategy.cs | EMA-Cross + Volume + EMA200 Trend-Filter + ATR-Volatilitätsfilter |
| RSI Momentum | RsiStrategy.cs | RSI als Momentum-Indikator + Divergenz-Erkennung + Volume-Konfirmation |
| Bollinger Breakout | BollingerStrategy.cs | Squeeze-Erkennung + Breakout + Volume-Konfirmation |
| MACD | MacdStrategy.cs | Histogram-Momentum + Zero-Line-Cross + Trend-Kontext |
| Smart Grid | GridStrategy.cs | Dynamische Grenzen via Bollinger, nur in Range-Märkten (EMA+ATR Trend-Check) |

### CryptoTrendPro (Default-Strategie seit 04.04.2026)

Optimiert für Krypto-Futures 2024-2026. Confluence-Scoring statt binäre Bedingungen.

**Entry-Scoring (Long, 0-12 Punkte):**
- +2: D1 Preis > EMA 50 (mittelfristiger Uptrend)
- +2: H4 Supertrend(10, 3.0) bullish
- +1: H4 EMA 12 > EMA 26
- +1: H4 ADX > 20 UND steigend (+DI > -DI)
- +1: H4 RSI 45-80
- +1: H4 Volumen > 1.5x SMA(20)
- +2: BTC-Kontext (D1 > EMA50 + HTF Supertrend)
- +1: Funding-Rate günstig
- +1: Cooldown respektiert
- **Min. Score: 8/12 für Trade**

**Multi-Stage Exit (PositionExitState):**
- TP1 bei 2.5-3x ATR → 50% Position schließen → SL auf Break-Even
- Chandelier-Trailing (2.5x ATR unter Höchstpunkt) nach TP1
- TP2 bei 4.5-5x ATR → Rest schließen
- Time-Exit: 48h ohne TP1 → schließen
- Regime-Exit: Supertrend-Flip → sofort schließen
- ADX-Exit: ADX < 15 → Trend tot

**Volatilitäts-Adaptation (ATR-Perzentil):**
- Ruhig (<20%): SL 1.5x, TP1 2.0x, TP2 3.5x ATR
- Normal (20-75%): SL 1.8-2.0x, TP1 2.5-3.0x, TP2 4.5-5.0x ATR
- Volatil (75-90%): SL 2.5x, TP1 3.5x, TP2 6.0x ATR
- Extrem (>90%): Halbe Position, konservativere Multiplikatoren

### MarketFilter (Engine/Filters/MarketFilter.cs)

Globale Filter die VOR der Strategie-Evaluation greifen:
- **BTC Health Score** (-4 bis +4): D1>EMA50, H4 Supertrend, RSI, Funding → Long/Short/Position-Scale
- **Session-Filter**: US (13-21 UTC) = 100%, EU = 90%, Asia = 75%, Wochenende = 0%
- **Funding-Rate**: >+0.08% blockiert Longs, <-0.05% blockiert Shorts
- **Cooldown**: 8h Pause nach Verlust-Trade
- **Max Trades/Tag**: Default 3
- **Volatilitäts-Bremse**: ATR >90. Perzentil → halbe Position

### Neue Defaults (04.04.2026)

| Setting | Alt | Neu |
|---------|-----|-----|
| Timeframe | H1 | **H4** |
| Scan-Intervall | 30s | **15min** (dynamisch per Timeframe) |
| Leverage | 10x | **3x** |
| Risiko/Trade | 2% | **1.5%** |
| Daily Drawdown | 5% | **3%** |
| Total Drawdown | 15% | **10%** |
| Trailing-Stop | 1.5% fix | **2.5x ATR** (Chandelier) |
| Min Volume | 10M | **50M** |
| Max Kandidaten | 10 | **5** |

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
