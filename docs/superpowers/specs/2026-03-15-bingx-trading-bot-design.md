# BingX Trading Bot - Design-Dokument

## Zusammenfassung

Automatisierter Trading Bot für BingX Perpetual Futures mit modularem Strategie-System, Market Scanner, fortgeschrittenem Risikomanagement, Backtesting und Paper-Trading. Implementiert als Avalonia Desktop-App innerhalb der bestehenden MeineApps.Ava Solution.

---

## Anforderungen

- **Exchange**: BingX Perpetual Futures (USDT-M)
- **Strategien**: Modulares System mit austauschbaren Strategien (EMA-Cross, RSI, MACD, Bollinger, Grid etc.)
- **Scanner**: Automatische Erkennung von Trading-Setups über alle verfügbaren Paare
- **Risikomanagement**: Fortgeschritten, komplett konfigurierbar (Kelly, ATR, Korrelation, Drawdown-Limits)
- **Backtesting**: Vollwertig mit realistischem Slippage, Fees, Funding Rates, Equity-Kurve
- **Paper-Trading**: Live-Simulation ohne echtes Geld
- **UI**: Avalonia Desktop-App mit Dashboard, Charts (SkiaSharp), Trade-History
- **Notifications**: Logging im UI + Datei (keine externen Services)
- **Integration**: Teil der MeineApps.Ava Solution, nutzt bestehende Libraries

---

## Architektur: Layered Architecture

### Projektstruktur

```
src/
├── Libraries/
│   ├── BingXBot.Core/              # net10.0 - Domain
│   │   ├── Models/                 # Candle, CompletedTrade, Position, Order, Ticker etc.
│   │   ├── Enums/                  # Signal, Side, OrderType, TimeFrame etc.
│   │   ├── Interfaces/
│   │   │   ├── IStrategy.cs
│   │   │   ├── IExchangeClient.cs
│   │   │   ├── IRiskManager.cs
│   │   │   ├── IMarketScanner.cs
│   │   │   ├── IDataFeed.cs
│   │   │   └── ISecureStorageService.cs
│   │   ├── Simulation/
│   │   │   └── SimulatedExchange.cs  # IExchangeClient für Paper + Backtest
│   │   └── Configuration/            # Settings-Models (JSON-serialisierbar)
│   │
│   ├── BingXBot.Exchange/          # net10.0 - BingX API
│   │   ├── BingXRestClient.cs      # REST API (Orders, Account, Positions)
│   │   ├── BingXWebSocketClient.cs # WebSocket mit Auto-Reconnect + Listen-Key-Renewal
│   │   ├── BingXDataFeed.cs        # IDataFeed-Implementierung
│   │   ├── RateLimiter.cs          # Token-Bucket Rate Limiter
│   │   └── Models/                 # BingX-spezifische DTOs + Mapping
│   │
│   ├── BingXBot.Engine/            # net10.0 - Trading-Logik
│   │   ├── TradingEngine.cs        # Orchestriert alles (Zustandsmaschine)
│   │   ├── StrategyManager.cs      # Verwaltet Symbol-spezifische Strategy-Instanzen
│   │   ├── Strategies/             # Konkrete Strategien
│   │   │   ├── EmaCrossStrategy.cs
│   │   │   ├── RsiStrategy.cs
│   │   │   ├── BollingerStrategy.cs
│   │   │   ├── MacdStrategy.cs
│   │   │   └── GridStrategy.cs
│   │   ├── Scanner/
│   │   │   └── MarketScanner.cs
│   │   ├── Risk/
│   │   │   ├── RiskManager.cs      # Nutzt CorrelationChecker intern
│   │   │   └── CorrelationChecker.cs
│   │   └── Analysis/
│   │       └── TradeJournal.cs
│   │
│   └── BingXBot.Backtest/          # net10.0 - Backtesting
│       ├── BacktestEngine.cs
│       ├── PaperTradingEngine.cs
│       └── Reports/
│           └── PerformanceReport.cs
│
└── Apps/
    └── BingXBot/
        ├── BingXBot.Shared/        # Avalonia UI
        │   ├── App.axaml(.cs)
        │   ├── Themes/AppPalette.axaml
        │   ├── Services/
        │   │   ├── SecureStorageService.cs   # ISecureStorageService-Implementierung
        │   │   └── BotDatabaseService.cs     # SQLite Persistenz
        │   ├── ViewModels/
        │   │   ├── MainViewModel.cs
        │   │   ├── DashboardViewModel.cs
        │   │   ├── StrategyViewModel.cs
        │   │   ├── BacktestViewModel.cs
        │   │   ├── TradeHistoryViewModel.cs
        │   │   ├── ScannerViewModel.cs
        │   │   ├── RiskSettingsViewModel.cs
        │   │   ├── LogViewModel.cs
        │   │   └── SettingsViewModel.cs
        │   └── Views/
        │       ├── MainView.axaml
        │       ├── DashboardView.axaml
        │       ├── StrategyView.axaml
        │       ├── BacktestView.axaml
        │       ├── TradeHistoryView.axaml
        │       ├── ScannerView.axaml
        │       ├── RiskSettingsView.axaml
        │       ├── LogView.axaml
        │       └── SettingsView.axaml
        └── BingXBot.Desktop/
            └── Program.cs
```

### Abhängigkeiten

```
BingXBot.Core        ← keine Abhängigkeiten (reine Domain + SimulatedExchange)
BingXBot.Exchange    ← BingXBot.Core
BingXBot.Engine      ← BingXBot.Core (nutzt Interfaces via DI, keine direkte Exchange-Abhängigkeit)
BingXBot.Backtest    ← BingXBot.Core, BingXBot.Engine
BingXBot.Shared      ← BingXBot.Core, BingXBot.Engine, BingXBot.Exchange, BingXBot.Backtest
```

Engine hat KEINE Abhängigkeit auf Exchange. MarketScanner und TradingEngine bekommen `IDataFeed` und `IExchangeClient` per Constructor Injection. Die konkrete Implementierung (BingXRestClient oder SimulatedExchange) wird im DI-Container registriert.

`SimulatedExchange` liegt in **Core** (nicht Backtest), weil es sowohl von Paper-Trading (Engine) als auch Backtesting (Backtest) gebraucht wird.

---

## Enums

```csharp
public enum Signal { None, Long, Short, CloseLong, CloseShort }

public enum Side { Buy, Sell }

public enum OrderType { Market, Limit, StopMarket, StopLimit, TakeProfitMarket }

public enum OrderStatus { New, PartiallyFilled, Filled, Cancelled, Rejected, Expired }

public enum TimeFrame
{
    M1, M3, M5, M15, M30,     // Minuten
    H1, H2, H4, H6, H12,     // Stunden
    D1, W1, MN1               // Tag, Woche, Monat
}

public enum PositionMode { OneWay, Hedge }

public enum MarginType { Cross, Isolated }

public enum ScanMode { Momentum, Reversal, Breakout, VolumeSurge }

public enum BotState { Stopped, Starting, Running, Paused, EmergencyStop, Error }

public enum TradingMode { Live, Paper, Backtest }

public enum LogLevel { Debug, Info, Trade, Warning, Error }
```

---

## Domain-Models

### Transport-Records (für Laufzeit, nicht direkt in SQLite)

```csharp
public record Candle(
    DateTime OpenTime,
    decimal Open, decimal High, decimal Low, decimal Close,
    decimal Volume,
    DateTime CloseTime
);

public record Ticker(
    string Symbol,
    decimal LastPrice,
    decimal BidPrice, decimal AskPrice,
    decimal Volume24h,
    decimal PriceChangePercent24h,
    DateTime Timestamp
);

public record Position(
    string Symbol,
    Side Side,
    decimal EntryPrice,
    decimal MarkPrice,
    decimal Quantity,
    decimal UnrealizedPnl,
    decimal Leverage,
    MarginType MarginType,
    DateTime OpenTime
);

public record Order(
    string OrderId,
    string Symbol,
    Side Side,
    OrderType Type,
    decimal Price,
    decimal Quantity,
    decimal? StopPrice,
    DateTime CreateTime,
    OrderStatus Status
);

public record OrderRequest(
    string Symbol,
    Side Side,
    OrderType Type,
    decimal Quantity,
    decimal? Price = null,        // Limit-Orders
    decimal? StopPrice = null,    // Stop-Orders
    decimal? TakeProfit = null,
    decimal? StopLoss = null
);

public record AccountInfo(
    decimal Balance,
    decimal AvailableBalance,
    decimal UnrealizedPnl,
    decimal UsedMargin
);

public record EquityPoint(DateTime Time, decimal Equity);

// Einheitliches Trade-Model für Live, Paper und Backtest
public record CompletedTrade(
    string Symbol,
    Side Side,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    decimal Pnl,
    decimal Fee,
    DateTime EntryTime,
    DateTime ExitTime,
    string Reason,
    TradingMode Mode          // Live, Paper oder Backtest
);

public record StrategyParameter(
    string Name,
    string Description,
    string ValueType,         // "int", "decimal", "bool" - string statt Type für JSON-Serialisierung
    object DefaultValue,
    object? MinValue = null,
    object? MaxValue = null,
    object? StepSize = null   // Für UI-Slider
);

public record RiskCheckResult(
    bool IsAllowed,
    string? RejectionReason,
    decimal AdjustedPositionSize   // Ggf. reduzierte Größe
);

public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Category,          // "Trade", "Scanner", "Risk", "Engine", "WebSocket"
    string Message,
    string? Symbol = null
);

public record SignalResult(
    Signal Signal,
    decimal Confidence,       // 0.0 - 1.0
    decimal? EntryPrice,
    decimal? StopLoss,
    decimal? TakeProfit,
    string Reason
);

public record MarketContext(
    string Symbol,
    IReadOnlyList<Candle> Candles,
    Ticker CurrentTicker,
    IReadOnlyList<Position> OpenPositions,
    AccountInfo Account
);

public record ScanResult(
    string Symbol,
    decimal Score,
    string SetupType,
    Dictionary<string, decimal> Indicators
);
```

### SQLite DB-Entities (separate Klassen mit parameterlosem Konstruktor)

`sqlite-net-pcl` braucht parameterlose Konstruktoren und settbare Properties. Daher separate DB-Entities mit Mapping von/zu Records:

```csharp
[Table("Trades")]
public class TradeEntity
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Symbol { get; set; }
    public int Side { get; set; }          // Enum als int
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal Pnl { get; set; }
    public decimal Fee { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public string Reason { get; set; }
    public int Mode { get; set; }          // TradingMode als int

    public CompletedTrade ToRecord() => new(...);
    public static TradeEntity FromRecord(CompletedTrade t) => new() { ... };
}

[Table("EquitySnapshots")]
public class EquityEntity
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public DateTime Time { get; set; }
    public decimal Equity { get; set; }
}

[Table("LogEntries")]
public class LogEntity
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public int Level { get; set; }
    public string Category { get; set; }
    public string Message { get; set; }
    public string? Symbol { get; set; }
}

[Table("Settings")]
public class SettingEntity
{
    [PrimaryKey] public string Key { get; set; }
    public string Value { get; set; }     // JSON-serialisierter Inhalt
}
```

---

## Kern-Interfaces

### IStrategy

```csharp
public interface IStrategy
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<StrategyParameter> Parameters { get; }
    SignalResult Evaluate(MarketContext context);
    void WarmUp(IReadOnlyList<Candle> history);
    void Reset();

    // Factory-Methode: erstellt eine neue Instanz mit gleichem Typ und gleichen Settings
    // Benötigt für Multi-Symbol-Support (jedes Symbol braucht eigenen Indikator-State)
    IStrategy Clone();
}
```

### IExchangeClient

```csharp
public interface IExchangeClient
{
    // Orders
    Task<Order> PlaceOrderAsync(OrderRequest request);
    Task<bool> CancelOrderAsync(string orderId, string symbol);
    Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null);

    // Positionen
    Task<IReadOnlyList<Position>> GetPositionsAsync();
    Task ClosePositionAsync(string symbol, Side side);
    Task CloseAllPositionsAsync();     // Emergency-Stop

    // Account
    Task<AccountInfo> GetAccountInfoAsync();

    // Konfiguration
    Task SetLeverageAsync(string symbol, int leverage, Side side);
    Task SetMarginTypeAsync(string symbol, MarginType marginType);

    // Marktdaten (On-Demand, kleine Abfragen)
    Task<IReadOnlyList<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, int limit);
    Task<IReadOnlyList<Ticker>> GetAllTickersAsync();  // Bulk-Ticker für Scanner
    Task<decimal> GetFundingRateAsync(string symbol);
    Task<IReadOnlyList<string>> GetAllSymbolsAsync();
}
```

### IDataFeed

Zuständigkeit: **Streaming** (WebSocket) und **historische Bulk-Daten** für Backtesting.
`IExchangeClient.GetKlinesAsync` ist für kleine On-Demand-Abfragen (z.B. Indikator-Warmup).
`IExchangeClient.GetAllTickersAsync` ist für den initialen Scanner-Durchlauf (alle Symbole auf einmal).
`IDataFeed.GetHistoricalKlinesAsync` ist für große Zeiträume mit automatischer Paginierung.

```csharp
public interface IDataFeed : IAsyncDisposable
{
    IAsyncEnumerable<Candle> StreamKlinesAsync(string symbol, TimeFrame tf, CancellationToken ct);
    IAsyncEnumerable<Ticker> StreamTickerAsync(string symbol, CancellationToken ct);
    Task<IReadOnlyList<Candle>> GetHistoricalKlinesAsync(string symbol, TimeFrame tf, DateTime from, DateTime to);

    // Verbindungsstatus
    event EventHandler<bool>? ConnectionStateChanged;
    bool IsConnected { get; }
}
```

### IRiskManager

```csharp
public interface IRiskManager
{
    RiskCheckResult ValidateTrade(SignalResult signal, MarketContext context);

    // Berechnet Position-Größe basierend auf Account-Balance.
    // Wenn StopLoss null → verwendet MaxPositionSizePercent als Fallback.
    // AccountInfo wird für %-basiertes Sizing benötigt.
    decimal CalculatePositionSize(string symbol, decimal entryPrice, decimal? stopLoss, AccountInfo account);

    void UpdateDailyStats(CompletedTrade completedTrade);
    void ResetDailyStats();    // Wird um 00:00 UTC aufgerufen
}
```

### IMarketScanner

MarketScanner bekommt `IExchangeClient` (für `GetAllTickersAsync`, `GetAllSymbolsAsync`) und `IDataFeed` (für Kline-Streaming nach Vorfilterung) per Constructor Injection.

```csharp
public interface IMarketScanner
{
    IAsyncEnumerable<ScanResult> ScanAsync(ScannerSettings settings, CancellationToken ct);
}
```

### ISecureStorageService

```csharp
public interface ISecureStorageService
{
    Task SaveCredentialsAsync(string apiKey, string apiSecret);
    Task<(string ApiKey, string ApiSecret)?> LoadCredentialsAsync();
    Task DeleteCredentialsAsync();
    bool HasCredentials { get; }
}
```

---

## StrategyManager - Multi-Symbol-Support

Strategien halten internen State (Indikator-Warmup). Bei Multi-Symbol-Trading braucht jedes Symbol eine eigene Strategy-Instanz.

```csharp
public class StrategyManager
{
    private readonly Dictionary<string, IStrategy> _symbolStrategies = new();
    private IStrategy _templateStrategy;   // Wird geklont für neue Symbole

    public void SetStrategy(IStrategy strategy)
    {
        _templateStrategy = strategy;
        _symbolStrategies.Clear();
    }

    public IStrategy GetOrCreateForSymbol(string symbol)
    {
        if (!_symbolStrategies.TryGetValue(symbol, out var strategy))
        {
            strategy = _templateStrategy.Clone();
            _symbolStrategies[symbol] = strategy;
        }
        return strategy;
    }

    public void RemoveSymbol(string symbol) => _symbolStrategies.Remove(symbol);
    public void Reset() => _symbolStrategies.Clear();
}
```

---

## TradingEngine - Zustandsmaschine

```csharp
public class TradingEngine
{
    // Constructor Injection
    public TradingEngine(
        IExchangeClient exchangeClient,
        IDataFeed dataFeed,
        IMarketScanner scanner,
        IRiskManager riskManager,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        ILogger<TradingEngine> logger)

    public BotState State { get; private set; }
    public TradingMode Mode { get; }

    // Zustandsübergänge
    public Task StartAsync(TradingMode mode);  // Stopped → Starting → Running
    public Task PauseAsync();                   // Running → Paused (offene Positionen bleiben)
    public Task ResumeAsync();                  // Paused → Running
    public Task StopAsync();                    // * → Stopped (ordentliches Herunterfahren)
    public Task EmergencyStopAsync();           // * → EmergencyStop (ALLE Positionen sofort schließen)

    // Events für UI-Binding
    public event EventHandler<BotState>? StateChanged;
    public event EventHandler<Order>? OrderPlaced;
    public event EventHandler<Position>? PositionOpened;
    public event EventHandler<CompletedTrade>? TradeClosed;
    public event EventHandler<LogEntry>? LogEmitted;
    public event EventHandler<string>? ErrorOccurred;
}
```

### Modus-Wechsel im DI

```csharp
// Live-Modus: BingXRestClient als IExchangeClient, BingXDataFeed als IDataFeed
// Paper-Modus: SimulatedExchange als IExchangeClient, BingXDataFeed als IDataFeed (echte Marktdaten)
// Backtest: BacktestEngine hat eigene Loop, nutzt SimulatedExchange intern

// Wechsel im MainViewModel:
// 1. User wählt Modus (Live/Paper) im Dashboard
// 2. MainViewModel erstellt TradingEngine mit passendem IExchangeClient
// 3. Backtest: eigener Button → BacktestViewModel ruft BacktestEngine.RunAsync() direkt auf
```

### Engine-Loop (Hauptschleife)

```
StartAsync(mode):
  1. Validiere API-Verbindung (nur Live/Paper)
  2. Setze Leverage/MarginType für aktive Symbole (nur Live)
  3. Starte DataFeed (WebSocket-Verbindung)
  4. Starte Scanner-Task (Background)
  5. State = Running
  6. Loop (läuft auf Background-Task, nicht UI-Thread):
     a. Scanner liefert Kandidaten-Symbole (via GetAllTickersAsync + Filter)
     b. Für jedes Symbol: strategyManager.GetOrCreateForSymbol(symbol)
     c. strategy.Evaluate(context) → SignalResult
     d. Bei Signal != None: RiskManager.ValidateTrade()
     e. Bei Erlaubnis: IExchangeClient.PlaceOrderAsync()
     f. TradeJournal protokollieren
     g. UI-Events feuern (via Dispatcher.UIThread.Post)
     h. Trailing-Stops aktualisieren
     i. Warten auf nächsten Candle-Close oder Ticker-Update
```

### Fehlerbehandlung in der Loop

- **WebSocket-Disconnect**: Auto-Reconnect (max 5 Versuche, exponentieller Backoff). Bei Misserfolg → `State = Error`, UI-Warnung. Offene Positionen bleiben, keine neuen Trades.
- **API-Fehler (Rate Limit)**: RateLimiter wartet automatisch, kein Retry-Spam.
- **API-Fehler (Order rejected)**: Loggen, nächstes Signal abwarten.
- **Unerwartete Exception**: Loggen, Loop pausieren (nicht crashen), UI-Benachrichtigung.
- **Stale-Data-Detection**: Wenn keine neuen Ticker-Daten > 60s → Warnung, keine neuen Trades bis Daten wieder fließen.

---

## BacktestEngine

```csharp
public class BacktestEngine
{
    public BacktestEngine(IExchangeClient dataSource, ILogger<BacktestEngine> logger)

    /// Führt Backtest durch: iteriert Candle für Candle, evaluiert Strategie, simuliert Fills.
    /// Gibt PerformanceReport mit allen Metriken zurück.
    public async Task<PerformanceReport> RunAsync(
        IStrategy strategy,
        IRiskManager riskManager,
        string symbol,
        TimeFrame timeFrame,
        DateTime from,
        DateTime to,
        BacktestSettings settings,
        IProgress<int>? progress = null,       // 0-100% für UI-Fortschrittsanzeige
        CancellationToken ct = default)
    {
        // 1. Historische Daten laden via dataSource.GetKlinesAsync (paginiert)
        // 2. SimulatedExchange erstellen mit BacktestSettings (Fees, Slippage)
        // 3. strategy.WarmUp(erste N Candles)
        // 4. Für jede Candle ab Warmup-Ende:
        //    a. MarketContext bauen
        //    b. strategy.Evaluate(context)
        //    c. riskManager.ValidateTrade() + CalculatePositionSize()
        //    d. simExchange.PlaceOrderAsync() → simulierter Fill
        //    e. Trailing-Stops prüfen
        //    f. Equity-Snapshot speichern
        // 5. PerformanceReport berechnen und zurückgeben
    }
}

public class PerformanceReport
{
    public List<CompletedTrade> Trades { get; set; } = new();
    public List<EquityPoint> EquityCurve { get; set; } = new();
    public decimal TotalPnl { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal AverageRrr { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public TimeSpan AverageHoldTime { get; set; }
}
```

---

## RiskManager - CorrelationChecker Integration

```csharp
public class RiskManager : IRiskManager
{
    private readonly CorrelationChecker _correlationChecker;
    private readonly RiskSettings _settings;

    public RiskCheckResult ValidateTrade(SignalResult signal, MarketContext context)
    {
        // 1. Max offene Positionen prüfen
        // 2. Max Positionen pro Symbol prüfen
        // 3. Täglichen Drawdown prüfen → bei Überschreitung: Bot pausieren
        // 4. Gesamt-Drawdown prüfen → bei Überschreitung: Bot stoppen
        // 5. Korrelations-Check (wenn CheckCorrelation=true):
        //    _correlationChecker.IsCorrelated(newSymbol, existingPositions) → blockieren wenn > MaxCorrelation
        // 6. Position-Sizing berechnen
    }
}

public class CorrelationChecker
{
    /// Prüft ob ein neues Symbol zu stark mit bestehenden offenen Positionen korreliert.
    /// Nutzt Pearson-Korrelation auf historische Kline-Daten (letzte 100 Candles).
    public async Task<bool> IsCorrelatedAsync(
        string newSymbol,
        IReadOnlyList<Position> openPositions,
        decimal maxCorrelation,
        IExchangeClient client);
}
```

---

## WebSocket Auto-Reconnect + Listen-Key-Renewal

```csharp
public class BingXWebSocketClient : IAsyncDisposable
{
    // Automatischer Reconnect bei Verbindungsabbruch
    private int _reconnectAttempts;
    private const int MaxReconnectAttempts = 5;

    // Backoff: 1s, 2s, 4s, 8s, 16s
    private TimeSpan GetBackoff() => TimeSpan.FromSeconds(Math.Pow(2, _reconnectAttempts));

    // Nach Reconnect: Channels re-subscriben
    // Lücken-Erkennung: Letzte empfangene Candle-Zeit prüfen, fehlende per REST nachladen

    // Listen-Key für private Channels (Account Updates):
    // - POST /openApi/user/auth/userDataStream → neuen Listen-Key erstellen
    // - PUT  /openApi/user/auth/userDataStream → Listen-Key verlängern (alle 30 Minuten)
    // - PeriodicTimer erneuert Listen-Key automatisch alle 30 Min
    // - Bei Ablauf (60 Min ohne Renewal): neuen Key erstellen, private Channels re-subscriben

    public event EventHandler<bool>? ConnectionStateChanged;
}
```

---

## Rate Limiter

```csharp
public class RateLimiter
{
    // Token-Bucket: 10 Requests/Sekunde für Orders (BingX Limit)
    // Separate Buckets für: Orders (10/s), Queries (20/s)
    public async Task WaitForSlotAsync(string category, CancellationToken ct);
}
```

---

## Konfiguration

### BotSettings (Root-Config, wird in SQLite persistiert)

```csharp
public class BotSettings
{
    public RiskSettings Risk { get; set; } = new();
    public ScannerSettings Scanner { get; set; } = new();
    public BacktestSettings Backtest { get; set; } = new();
    public TradingMode LastMode { get; set; } = TradingMode.Paper;  // Default: Paper (sicher)
    public string? LastStrategyName { get; set; }
    public Dictionary<string, string> StrategyParameters { get; set; } = new();  // JSON pro Strategie
}

// Persistenz: Als JSON-Blob in SQLite `Settings`-Tabelle (Key="BotSettings", Value=JSON)
```

### RiskSettings (komplett im UI einstellbar)

```csharp
public class RiskSettings
{
    public decimal MaxPositionSizePercent { get; set; } = 2m;       // 2% des Accounts pro Trade
    public decimal MaxDailyDrawdownPercent { get; set; } = 5m;      // 5% → Bot pausiert
    public decimal MaxTotalDrawdownPercent { get; set; } = 15m;     // 15% → Bot stoppt
    public int MaxOpenPositions { get; set; } = 3;
    public int MaxOpenPositionsPerSymbol { get; set; } = 1;
    public decimal MaxLeverage { get; set; } = 10m;                 // Gilt für Live UND Backtest
    public bool UseKellyCriterion { get; set; } = false;
    public bool UseAtrSizing { get; set; } = true;
    public bool CheckCorrelation { get; set; } = true;
    public decimal MaxCorrelation { get; set; } = 0.7m;
    public bool EnableTrailingStop { get; set; } = true;
    public decimal TrailingStopPercent { get; set; } = 1.5m;
}
```

### BacktestSettings

```csharp
public class BacktestSettings
{
    public decimal InitialBalance { get; set; } = 1000m;
    public decimal MakerFee { get; set; } = 0.0002m;        // 0.02% BingX
    public decimal TakerFee { get; set; } = 0.0005m;        // 0.05% BingX
    public decimal SlippagePercent { get; set; } = 0.05m;    // 0.05%
    public bool SimulateFundingRate { get; set; } = true;
    // Leverage kommt aus RiskSettings.MaxLeverage (eine Quelle der Wahrheit)
}
```

### ScannerSettings

```csharp
public class ScannerSettings
{
    public decimal MinVolume24h { get; set; } = 10_000_000m;  // 10M USDT
    public decimal MinPriceChange { get; set; } = 1.0m;       // 1% Bewegung
    public TimeFrame ScanTimeFrame { get; set; } = TimeFrame.H1;
    public List<string> Blacklist { get; set; } = new();
    public List<string> Whitelist { get; set; } = new();      // Leer = alle Paare
    public int MaxResults { get; set; } = 10;
    public ScanMode Mode { get; set; } = ScanMode.Momentum;
}
```

---

## Persistenz (SQLite)

Nutzt `sqlite-net-pcl` (bereits in der Solution).

### Tabellen

| Tabelle | Inhalt | Entity-Klasse |
|---------|--------|----------------|
| `Trades` | Alle abgeschlossenen Trades mit `TradingMode`-Flag | `TradeEntity` |
| `EquitySnapshots` | Equity-Kurve über Zeit (alle 5min bei laufendem Bot) | `EquityEntity` |
| `Settings` | Key-Value-Store für serialisierte Settings | `SettingEntity` |
| `LogEntries` | Strukturierte Logs (Trade-bezogen für Audit-Trail) | `LogEntity` |

### Log-Rotation

- **SQLite LogEntries**: Alle 1.000 Inserts: Count prüfen. Bei > 100.000 → DELETE älteste 10.000 (ein Batch-DELETE).
- **Datei-Logs**: Rolling: max 10 Dateien × 10MB. Älteste Datei wird gelöscht wenn neue erstellt wird.

### App-Neustart

- Bot startet NICHT automatisch → User muss explizit "Start" klicken
- Letzte Settings (inkl. Modus und Strategie) werden aus SQLite geladen
- Default-Modus beim ersten Start: **Paper** (sicher)
- Offene Positionen werden beim Start von BingX API abgefragt und im UI angezeigt
- Trade-History und Equity-Kurve bleiben erhalten

---

## Logging

Strukturiertes Logging mit `Microsoft.Extensions.Logging` (bereits in der Solution).

### Ausgabe-Ziele

1. **UI-LogView**: Ringpuffer (letzte 1.000 Einträge), filterbar nach Level und Kategorie
2. **Datei**: `logs/bingxbot-{datum}.log`, max 10 Dateien × 10MB (rolling)
3. **SQLite**: Trade-bezogene Logs (nachvollziehbar warum ein Trade ausgeführt/abgelehnt wurde)

### Kategorien

- `Trade` - Order platziert/gefüllt/abgelehnt
- `Scanner` - Setup gefunden/verworfen
- `Risk` - Trade erlaubt/blockiert mit Grund
- `Engine` - Start/Stop/Pause/Fehler
- `WebSocket` - Connect/Disconnect/Reconnect
- `Backtest` - Fortschritt/Ergebnis

---

## Sicherheit

- **API-Keys Windows**: Verschlüsselt via `ProtectedData` (DPAPI), gespeichert in `%APPDATA%/BingXBot/credentials.dat`
- **API-Keys Linux**: AES-256 verschlüsselt mit Master-Passwort (User gibt es beim Start ein), gespeichert in `~/.config/BingXBot/credentials.dat`
- **Settings-Datei**: In `.gitignore`, keine Secrets im Repository
- **IP-Whitelist**: Empfohlen im BingX-Account
- **Permissions**: API-Key nur mit Trade-Berechtigung, KEIN Withdrawal

---

## Datenfluss

### Live-Trading

```
BingXWebSocket --> BingXDataFeed --> MarketScanner (via IExchangeClient.GetAllTickersAsync + IDataFeed)
                                           |
                                           v
                                    TradingEngine (IExchangeClient = BingXRestClient)
                                           |
                          +----------------+----------------+
                          v                v                v
                    StrategyManager     RiskManager     TradeJournal
                    (pro Symbol)         (+ CorrelationChecker)
                          |                |
                          v                v
                    SignalResult --> Order validieren
                                           |
                                           v
                                    BingXRestClient (via RateLimiter)
                                           |
                                           v
                               UI Update (via Events) + SQLite
```

### Paper-Trading

```
BingXWebSocket --> BingXDataFeed --> MarketScanner --> [filtert Paare]
                                           |
                                           v
                                    TradingEngine (IExchangeClient = SimulatedExchange)
                                           |
                                    [gleicher StrategyManager]
                                    [gleicher RiskManager]
                                           |
                                           v
                                    SimulatedExchange (kein echtes Geld)
                                           |
                                           v
                               UI Update + SQLite (TradingMode.Paper)
```

### Backtesting

```
Historische Daten (REST) --> BacktestEngine.RunAsync()
                                   |
                            [eigene IStrategy-Instanz]
                            [eigener RiskManager]
                            [SimulatedExchange intern]
                                   |
                                   v
                            PerformanceReport
                            (Equity, Drawdown, Sharpe, Win-Rate)
                                   |
                                   v
                            BacktestViewModel zeigt Ergebnisse
```

IStrategy und IRiskManager werden in allen drei Modi identisch verwendet. Der Unterschied liegt im Exchange-Layer: `BingXRestClient` (Live) vs. `SimulatedExchange` (Paper + Backtest). Live und Paper nutzen beide den echten WebSocket-DataFeed, Backtest iteriert über historische Daten.

---

## NuGet-Packages (zusätzlich zu bestehenden)

| Package | Version | Zweck |
|---------|---------|-------|
| `Skender.Stock.Indicators` | 2.7.1 | 150+ technische Indikatoren |

REST (`HttpClient`), WebSocket (`ClientWebSocket`), Crypto (`HMACSHA256`) und Logging (`Microsoft.Extensions.Logging`) sind in .NET built-in. Package muss in `Directory.Packages.props` registriert werden.

---

## BingX API

### REST API
- Base URL: `https://open-api.bingx.com`
- Authentifizierung: HMAC-SHA256 Signatur (API-Key + Secret + Timestamp)
- Rate Limits: 10 req/s für Orders, 20 req/s für Queries

### WebSocket
- URL: Vor Implementierung aktuelle offizielle BingX-Dokumentation prüfen (Domains ändern sich)
- Channels: Klines, Ticker, Trades, Account Updates (private, erfordert listenKey)
- Heartbeat: Ping/Pong (Intervall laut aktueller Docs)
- Auto-Reconnect bei Verbindungsabbruch mit Channel-Re-Subscribe
- Listen-Key-Renewal alle 30 Minuten für private Channels

### Wichtige Endpoints (v2/v3)
- `POST /openApi/swap/v2/trade/order` - Order platzieren
- `DELETE /openApi/swap/v2/trade/order` - Order stornieren
- `GET /openApi/swap/v2/user/positions` - Offene Positionen
- `POST /openApi/swap/v2/trade/leverage` - Leverage setzen
- `POST /openApi/swap/v2/trade/marginType` - Margin-Typ setzen
- `GET /openApi/swap/v3/quote/klines` - Historische Klines (v3)
- `GET /openApi/swap/v2/quote/ticker` - 24h Ticker (ohne Symbol = alle)
- `GET /openApi/swap/v2/user/balance` - Account Balance
- `GET /openApi/swap/v2/quote/fundingRate` - Funding Rate
- `POST /openApi/user/auth/userDataStream` - Listen-Key erstellen
- `PUT /openApi/user/auth/userDataStream` - Listen-Key verlängern

---

## UI-Konzept

| View | ViewModel | Inhalt |
|------|-----------|--------|
| **Dashboard** | DashboardViewModel | Account-Balance, offene Positionen, Live-P&L, Equity-Kurve (SkiaSharp), Bot-Status + Modus-Anzeige, Start/Pause/Stop/Emergency, Live/Paper-Toggle |
| **Scanner** | ScannerViewModel | Live-Tabelle mit gescannten Paaren, Score, Setup-Typ, Volumen, Indikatoren |
| **Strategie** | StrategyViewModel | Dropdown Strategie-Auswahl, dynamischer Parameter-Editor (aus StrategyParameter.ValueType), Aktivieren/Deaktivieren |
| **Backtest** | BacktestViewModel | Zeitraum, Strategie + Settings, RunAsync()-Button, Equity-Kurve + PerformanceReport-Statistiken |
| **Trade-History** | TradeHistoryViewModel | Alle Trades (Live + Paper + Backtest), filterbar nach Modus/Symbol/Zeitraum |
| **Risk-Settings** | RiskSettingsViewModel | Alle RiskSettings-Felder als konfigurierbare Inputs mit Validierung |
| **Log** | LogViewModel | Live-Log-Ansicht mit Level/Kategorie-Filter, Ringpuffer (1.000 Einträge) |
| **Settings** | SettingsViewModel | API-Key/Secret (via ISecureStorageService), Log-Level, Scanner-Defaults |

### Farbpalette

Trading-typisch: dunkler Hintergrund, Grün (#10B981) für Profit, Rot (#EF4444) für Loss.
Primary: #3B82F6 (Blue), Surface: #1E1E2E (Dark).

---

## Nicht im Scope (bewusst ausgelassen)

- Externe Notifications (Telegram, Discord, E-Mail)
- Multi-Exchange-Support (nur BingX)
- Mobile App / Android
- Copy-Trading
- Spot-Trading
