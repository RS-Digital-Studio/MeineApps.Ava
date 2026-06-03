# ViewModels — Bot-Steuerung & UI-Logik

Alle ViewModels sind **Singleton** (in `App.axaml.cs` registriert). Kein Service-Locator,
keine Trading-Engine-Logik — alles über Interfaces (`IBotControlService`, `IBotEventStream`,
`ISettingsService`, etc.) oder direkt über den `BotEventBus`.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
Composition Root, DI-Registrierung, Modus-Conditional → [../CLAUDE.md](../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | Hält alle Sub-VMs. Tab-Navigation via `CurrentPageViewModel` + `ViewLocator`. Back-Press-Flow, `ExitHintRequested`-Event für Android-Toast. |
| `DashboardViewModel.cs` | Balance, offene Positionen, Bot-Start/Stop, Rolling-Metriken, Stats-Breakdown-Card, Live/Paper-Umschaltung. Hält `BtcTickerViewModel` + `ActivityFeedViewModel` als Sub-VMs. |
| `ScannerViewModel.cs` | Scanner-Einstellungen, Live-Scan-Ergebnisse, TF-Auswahl. Hört `ISettingsService.SettingsChanged`. |
| `StrategyViewModel.cs` | Aktive Strategie-Parameter-Anzeige (TrendFollow-Fast, H4-only). |
| `BacktestViewModel.cs` | Backtest-Start/Abort, `PerformanceReport`-Anzeige, Walk-Forward, Trade-Replay. |
| `TradeHistoryViewModel.cs` | Trade-Liste mit Filter (Symbol/TF/Modus/Zeitraum). Hört `IBotEventStream.TradeClosed` + `.BacktestCompleted`. |
| `RiskSettingsViewModel.cs` | Risiko-Parameter (Sizing, DD-Limits, Korrelation, Vol-Targeting, Cross-TF-Pyramiding). Hört `ISettingsService.SettingsChanged`. |
| `LogViewModel.cs` | Live-Log mit Level/Kategorie-Filter. Hört `IBotEventStream.LogEmitted`. Ringpuffer 1000 Einträge (Queue statt List — Dequeue O(1)). |
| `SettingsViewModel.cs` | API-Keys, Server-Verbindung, Pairing, Theme, Push-Notifications. Hört `ISettingsService.SettingsChanged`. |
| `SettingsHistoryViewModel.cs` | Settings-Audit-Trail (wer hat wann was geändert). |
| `ActivityFeedViewModel.cs` | Letzte 50 Einträge des Activity-Feeds (via `IBotEventStream.ActivityFeed`). |
| `BtcTickerViewModel.cs` | BTC-USDT-Preis-Anzeige als Markt-Indikator in der Top-Bar. |
| `PositionDisplayItem.cs` | Display-Modell (Record) für eine offene Position (Preis, PnL, SL/TP-Levels). |

## Lazy-VM-Pattern (Startup-Beschleunigung)

`MainViewModel` löst beim Start nur `DashboardViewModel` eager auf. Alle anderen VMs werden
über `Lazy<T>` (via `LazyDiService<T>`) erst beim ersten Navigations-Aufruf initialisiert.

```csharp
// RICHTIG: Lazy — Scanner-VM wird erst bei Navigation zum Scanner-Tab aufgelöst
private readonly Lazy<ScannerViewModel> _scanner;
public ScannerViewModel Scanner => _scanner.Value;

// Tab-Highlighting ohne unnötigen Lazy-Trigger
public bool IsScannerActive => _scanner.IsValueCreated
    && ReferenceEquals(CurrentPageViewModel, _scanner.Value);
```

## Settings-Sync-Pattern

ViewModels, die Settings anzeigen, abonnieren `ISettingsService.SettingsChanged` für
Multi-Client-Sync. Beim Remote-Mode feuert `RemoteSettingsService.RaiseChanged` dieses
Event wenn ein anderer Client Settings ändert. Neue ViewModels sollen
`ISettingsPersistenceService` per DI injizieren statt `App.SaveAllSettingsAsync()`.

## Event-Abo-Pattern

ViewModels subscriben im Konstruktor und desubscriben in `Dispose()`. Wer `IBotEventStream`
direkt abonniert (TradeHistory, Log, ActivityFeed), und wer den `BotEventBus` nutzt
(Dashboard, Backtest, RiskSettings, Settings, Strategy, MainVM), hängt davon ab ob die
Daten auch im Remote-Mode über SignalR fließen müssen — `IBotEventStream` ist die
Remote-fähige Abstraktion.

```csharp
// ViewModels mit BotEventBus-Abo (Local-only):
public DashboardViewModel(BotEventBus eventBus, ...)
{
    _eventBus = eventBus;
    _eventBus.BotStateChanged += OnBotStateChanged;
}
public void Dispose() => _eventBus.BotStateChanged -= OnBotStateChanged;

// ViewModels mit IBotEventStream-Abo (Local + Remote):
public LogViewModel(IBotEventStream eventStream)
{
    _eventStream = eventStream;
    _eventStream.LogEmitted += OnLogEmitted;
}
public void Dispose() => _eventStream.LogEmitted -= OnLogEmitted;
```

**Alle ViewModels, die Events abonnieren, implementieren `IDisposable`.**

## Mobile-Shell-Besonderheiten

- Mobile-Varianten (`DashboardViewMobile`, etc.) werden via `ViewLocator` automatisch
  ausgewählt wenn `App.IsMobileShell = true` (Android). Gleiches ViewModel, andere View.
- `MainViewModel.IsMoreDrawerOpen` steuert das Bottom-Sheet auf Mobile.
- **Content-Swap statt gestapelter Views**: `CurrentPageViewModel` + ein einzelnes
  `<ContentControl Content="{Binding CurrentPageViewModel}"/>` — niemals alle Views
  parallel instanziieren, das führt auf Mobile zu Crashes.

## Domain-Gotchas

- **`ContinueWith` IMMER mit `TaskScheduler.Default`** — sonst UI-Thread-Deadlock möglich.
- **`OriginalQuantity` IMMER die tatsächlich platzierte Menge** (nach Equity/Score-Scaling),
  nicht `riskCheck.AdjustedPositionSize`.
