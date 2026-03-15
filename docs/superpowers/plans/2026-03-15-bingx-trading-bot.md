# BingX Trading Bot - Implementierungsplan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development or superpowers:executing-plans.

**Goal:** BingX Perpetual Futures Trading Bot als Avalonia Desktop-App.

**Architecture:** Layered: Core, Exchange, Engine, Backtest + Avalonia UI.

**Tech Stack:** .NET 10, Avalonia 11.3, SkiaSharp, CommunityToolkit.Mvvm, sqlite-net-pcl, Skender.Stock.Indicators 2.7.1

**Spec:** docs/superpowers/specs/2026-03-15-bingx-trading-bot-design.md

## Chunk 1: Scaffolding + Core

### Task 1: Projekt-Scaffolding
- [ ] Skender.Stock.Indicators in Directory.Packages.props
- [ ] 7 csproj-Dateien erstellen (Core, Exchange, Engine, Backtest, Shared, Desktop, Tests)
- [ ] dotnet sln add + Build + Commit

### Task 2: Core Enums
- [ ] 11 Enums (Signal, Side, OrderType, OrderStatus, TimeFrame, MarginType, PositionMode, ScanMode, BotState, TradingMode, LogLevel)
- [ ] Build + Commit

### Task 3: Core Models (TDD)
- [ ] Tests + 14 Records (Candle, Ticker, Position, Order, OrderRequest, AccountInfo, EquityPoint, CompletedTrade, StrategyParameter, RiskCheckResult, LogEntry, SignalResult, MarketContext, ScanResult)
- [ ] Commit

### Task 4: Core Interfaces
- [ ] IStrategy, IExchangeClient, IDataFeed, IRiskManager, IMarketScanner, ISecureStorageService
- [ ] Commit

### Task 5: Core Configuration (TDD)
- [ ] RiskSettings, BacktestSettings, ScannerSettings, BotSettings mit Defaults
- [ ] Commit

### Task 6: SimulatedExchange (TDD)
- [ ] IExchangeClient mit internem State, SetCurrentPrice, Fees, Slippage
- [ ] Commit

### Task 7: DB-Entities
- [ ] TradeEntity, EquityEntity, LogEntity, SettingEntity mit Mapping
- [ ] Commit

## Chunk 2: Exchange Library

### Task 8: RateLimiter (TDD) - Token-Bucket 10/s Orders, 20/s Queries
### Task 9: BingXRestClient Grundstruktur (TDD) - HMAC-SHA256, GetAccountInfo
### Task 10: BingXRestClient Endpoints - Trading + Marktdaten
### Task 11: BingXWebSocketClient - Auto-Reconnect, Listen-Key-Renewal
### Task 12: BingXDataFeed - IDataFeed Streaming + Historisch

## Chunk 3: Engine Library

### Task 13: IndicatorHelper (TDD) - Skender Wrapper
### Task 14: EmaCrossStrategy (TDD) - Fast/Slow EMA Cross
### Task 15: RSI, Bollinger, MACD, Grid Strategien (TDD)
### Task 16: StrategyManager (TDD) - Multi-Symbol Clone-Pattern
### Task 17: RiskManager + CorrelationChecker (TDD)
### Task 18: MarketScanner (TDD) - Volumen/Blacklist Filter, Scoring
### Task 19: TradeJournal - Protokollierung + Statistiken
### Task 20: TradingEngine (TDD) - Zustandsmaschine + Background-Loop

## Chunk 4: Backtest Library

### Task 21: PerformanceReport - Metriken-Berechnung
### Task 22: BacktestEngine (TDD) - Candle-Iteration + SimulatedExchange
### Task 23: PaperTradingEngine - Live-Daten + Simulation

## Chunk 5: UI Grundgeruest

### Task 24: App.axaml + Dark-Trading-Theme + DI + Program.cs
### Task 25: BotDatabaseService - SQLite CRUD + Log-Rotation
### Task 26: SecureStorageService - DPAPI/AES-256
### Task 27: MainView - Sidebar-Navigation + Status-Bar
### Task 28: SettingsView - API-Key-Verwaltung

## Chunk 6: Feature-Views

### Task 29: DashboardView - Balance, Positionen, Equity-Chart, Bot-Controls
### Task 30: RiskSettingsView - Konfigurierbare Risiko-Parameter
### Task 31: StrategyView - Dropdown + dynamischer Parameter-Editor
### Task 32: ScannerView - Live-Ergebnis-Tabelle + Filter
### Task 33: BacktestView - Run + Progress + PerformanceReport + Chart
### Task 34: TradeHistoryView - Filter + farbcodiertes P&L
### Task 35: LogView - Ringpuffer + Farbcodierung
### Task 36: CLAUDE.md + Finaler Build
