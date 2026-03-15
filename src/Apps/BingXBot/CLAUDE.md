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

## Live-Trading (v1.0 - Nur Anzeige)

Live-Modus verbindet sich mit echtem BingX-Account:
- BingXRestClient wird zur Laufzeit mit gespeicherten API-Keys erstellt
- Verbindungstest: GetAccountInfoAsync() beim Start
- Echte Balance + Positionen werden alle 5 Sekunden aktualisiert
- **v1.0: NUR Anzeige, KEIN automatisches Handeln** (zu riskant ohne ausreichendes Testing)
- Notfall-Stop trennt Verbindung, schliesst aber KEINE echten Positionen (manuell auf BingX)
- SettingsViewModel feuert `ApiKeysAvailableChanged` Event bei Save/Delete

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
| Dashboard | Balance, Positionen, Activity-Feed, Bot-Controls, Strategie-Auswahl, Equity-Chart, BTC-Live-Candlestick-Chart, Live-Trading (nur Anzeige) | BotEventBus, StrategyManager, PaperTradingService, IPublicMarketDataClient, ISecureStorageService, BingXRestClient (Live), Auto-Refresh |
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
| DashboardViewModel | BotEventBus, StrategyManager, PaperTradingService, IPublicMarketDataClient?, BotDatabaseService?, ISecureStorageService? |
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

## Tests (165 Tests)

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
| Engine/IndicatorHelperTests.cs | Indikatoren | EMA, RSI, BB, MACD |
| Engine/CorrelationCheckerTests.cs | Korrelation | Pearson-Berechnung |
| Engine/MarketScannerTests.cs | Scanner | Volumen/Momentum-Filter |
| Engine/TradingEngineTests.cs | TradingEngine | Tick-Verarbeitung |
| Engine/RiskManagerTests.cs | RiskManager | Position-Sizing, Drawdown |
| Engine/TradeJournalTests.cs | TradeJournal | Record, WinRate, ProfitFactor |
| Exchange/RateLimiterTests.cs | RateLimiter | Request-Throttling |
| Exchange/BingXRestClientTests.cs | REST-Client | API-Aufrufe |
| Backtest/BacktestEngineTests.cs | BacktestEngine | Run, Demo-Candles |
| Backtest/PerformanceReportTests.cs | PerformanceReport | Metriken, Drawdown |

## Farbpalette

Dark-Trading-Theme: Primary #3B82F6, Background #1E1E2E, Profit #10B981, Loss #EF4444
