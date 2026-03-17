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
BingXBot.Engine      <- Trading-Logik (Strategien, Scanner, Risk, TradingEngine)
BingXBot.Backtest    <- Backtesting + Paper-Trading
BingXBot.Shared      <- Avalonia UI (ViewModels, Views)
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

## Paper-Trading (PaperTradingService)

Echter Paper-Trading-Service mit REST-Polling (implementiert IDisposable):
- Alle 30 Sekunden: Ticker holen, Scanner filtern, Klines laden
- Strategie evaluieren, RiskManager pruefen, Order auf SimulatedExchange platzieren
- Account-Update im Dashboard alle 5 Sekunden
- Pause/Resume: Loop laeuft weiter, ueberspringt Scans bei Pause
- EmergencyStopAsync: Async statt blockierendem GetAwaiter().GetResult()
- CancellationTokenSource wird korrekt disposed (Start, Stop, Dispose)
- Events ueber BotEventBus (Trades, Logs, Account-Updates)
- Datei: `Services/PaperTradingService.cs`

## Live-Trading (v2.0 - Voll funktional)

Live-Trading-Service handelt automatisch mit echtem Geld über BingXRestClient:
- `LiveTradingService` (analog zu PaperTradingService, aber mit echtem BingXRestClient)
- BingXRestClient wird zur Laufzeit mit gespeicherten API-Keys erstellt
- Verbindungstest: GetAccountInfoAsync() beim Start
- Echte Balance + Positionen werden alle 5 Sekunden aktualisiert
- **Scan-Loop alle 30s**: Ticker holen, Scanner filtern, Klines laden, Strategie evaluieren, echte Orders platzieren
- **SL/TP-Loop alle 5s**: Prüft offene Positionen gegen gespeicherte SL/TP-Levels, schließt bei Hit
- **Normaler Stop**: Positionen bleiben offen (User entscheidet manuell)
- **Notfall-Stop**: Schließt ALLE echten Positionen sofort über BingXRestClient
- **Sicherheitsmaßnahmen**: Roter Status-Banner, "LIVE ORDER:" Prefix im Activity-Feed, 60s Pause bei API-Fehlern
- SettingsViewModel feuert `ApiKeysAvailableChanged` Event bei Save/Delete
- `IsLiveActive` Property für roten UI-Rahmen im Dashboard
- Datei: `Services/LiveTradingService.cs`

## Activity-Feed (Dashboard)

Live-Feed der letzten 20 Bot-Aktionen direkt im Dashboard:
- Subscribet auf `BotEventBus.LogEmitted` (filtert Debug-Level aus)
- ActivityItem Record: Time, Category, Message, Level, Symbol
- Farbcodiert: Rot=Error, Amber=Warning, Gruen=Trade, Grau=Info
- Max 200px Höhe, scrollbar
- Zeigt "Bot ist gestoppt" wenn nicht aktiv

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
| Dashboard | Balance, Positionen, Activity-Feed, Bot-Controls, Strategie-Auswahl, Equity-Chart, BTC-Live-Candlestick-Chart, Live-Trading (voll funktional) | BotEventBus, StrategyManager, PaperTradingService, LiveTradingService, RiskSettings, ScannerSettings, IPublicMarketDataClient, ISecureStorageService, BingXRestClient (Live), Auto-Refresh |
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
| `LogEmitted` | Alle ViewModels, PaperTradingService | LogVM, DashboardVM (Activity-Feed) |
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
| Indikator-Caching | IndicatorHelper.cs | ConcurrentDictionary-Cache: Gleiche Candle-Daten + Indikator-Parameter → gecachtes Ergebnis. ClearCache() am Ende jedes Scan-Durchlaufs. Spart ~60 List-Allokationen/Scan |

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

## Tests (198 Tests)

## Farbpalette

Dark-Trading-Theme: Primary #3B82F6, Background #1E1E2E, Profit #10B981, Loss #EF4444
