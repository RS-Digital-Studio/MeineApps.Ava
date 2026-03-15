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
BingXBot.Core        ← Domain (Models, Enums, Interfaces, SimulatedExchange, DB-Entities)
BingXBot.Exchange    ← BingX REST + WebSocket API Client
BingXBot.Engine      ← Trading-Logik (Strategien, Scanner, Risk, TradingEngine)
BingXBot.Backtest    ← Backtesting + Paper-Trading
BingXBot.Shared      ← Avalonia UI (ViewModels, Views)
BingXBot.Desktop     ← Desktop Entry-Point
```

## Strategien

| Strategie | Datei | Logik |
|-----------|-------|-------|
| EMA Cross | EmaCrossStrategy.cs | Fast/Slow EMA Kreuzung |
| RSI | RsiStrategy.cs | Oversold/Overbought |
| Bollinger | BollingerStrategy.cs | Band-Touch Mean-Reversion |
| MACD | MacdStrategy.cs | MACD/Signal-Linie Cross |
| Grid | GridStrategy.cs | Range-Trading mit Levels |

Alle Strategien implementieren `IStrategy` mit `Clone()` für Multi-Symbol-Support via `StrategyManager`.

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
| Dashboard | Balance, Positionen, Bot-Controls, Equity-Chart, BTC-Live-Candlestick-Chart | IPublicMarketDataClient (Klines), Auto-Refresh 60s |
| Scanner | Live-Scan mit Volumen/Momentum-Filter | ScannerSettings, IMarketScanner (optional) |
| Strategie | Auswahl + dynamischer Parameter-Editor | StrategyManager, IStrategy-Instanzen |
| Backtest | Historischer Test mit PerformanceReport | BacktestEngine, RiskManager, SimulatedExchange |
| Trade-History | Alle Trades filterbar | - |
| Risk-Settings | Risiko-Parameter konfigurieren | RiskSettings (bidirektional) |
| Log | Live-Log mit Level/Kategorie-Filter | - |
| Settings | API-Keys, Verbindung | BotSettings, ISecureStorageService, IExchangeClient |

## SkiaSharp-Renderer

| Renderer | Datei | Beschreibung |
|----------|-------|--------------|
| EquityChartRenderer | Graphics/EquityChartRenderer.cs | Linien-Chart fuer Equity-Kurve (Profit/Loss-Farbgebung, Baseline) |
| BtcPriceChartRenderer | Graphics/BtcPriceChartRenderer.cs | Candlestick-Chart fuer BTC-USDT (75% Candles, 25% Volumen, Preis-Grid, Docht/Body) |

## ViewModel-DI-Verdrahtung

Alle ViewModels bekommen ihre Engine-Dependencies per Constructor Injection:

| ViewModel | DI-Parameter |
|-----------|--------------|
| StrategyViewModel | StrategyManager |
| BacktestViewModel | RiskSettings |
| ScannerViewModel | ScannerSettings, IMarketScanner? (optional) |
| RiskSettingsViewModel | RiskSettings (lädt/speichert bidirektional) |
| SettingsViewModel | BotSettings, ISecureStorageService?, IExchangeClient? (optional) |

Optionale Parameter (mit `?`) ermöglichen Demo-Modus ohne Exchange-Verbindung.

## Build

```bash
dotnet build src/Apps/BingXBot/BingXBot.Desktop
dotnet run --project src/Apps/BingXBot/BingXBot.Desktop
dotnet test tests/BingXBot.Tests
```

## Farbpalette

Dark-Trading-Theme: Primary #3B82F6, Background #1E1E2E, Profit #10B981, Loss #EF4444
