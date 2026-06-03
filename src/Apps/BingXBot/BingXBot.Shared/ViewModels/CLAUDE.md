# ViewModels — Bot-Steuerung & UI-Logik

Alle ViewModels sind **Singleton** (in `App.axaml.cs` registriert). Kein direktes Service-Locator-Zugriff,
keine Trading-Engine-Logik — alles über Interfaces (`IBotControlService`, `IBotEventStream`, etc.).
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | Hält alle Sub-VMs. Tab-Navigation via `CurrentPageViewModel` + `ViewLocator`. Back-Press-Flow, `ExitHintRequested`-Event für Android-Toast. |
| `DashboardViewModel.cs` | Balance, offene Positionen, Bot-Start/Stop, Equity-Chart, Activity-Feed, Live/Paper-Umschaltung. |
| `ScannerViewModel.cs` | Scanner-Einstellungen, Live-Scan-Ergebnisse, aktive TF-Auswahl. |
| `StrategyViewModel.cs` | Aktive Strategie-Parameter-Anzeige (TrendFollow-Fast, H4-only). |
| `BacktestViewModel.cs` | Backtest-Start/Abort, `PerformanceReport`-Anzeige, Walk-Forward, Trade-Replay. |
| `TradeHistoryViewModel.cs` | Trade-Liste mit Filter (Symbol/TF/Modus/Zeitraum). Hört `BotEventBus.TradeCompleted`. |
| `RiskSettingsViewModel.cs` | Risiko-Parameter (Sizing, DD-Limits, Korrelation, Vol-Targeting, Cross-TF-Pyramiding). |
| `LogViewModel.cs` | Live-Log mit Level/Kategorie-Filter. Hört `BotEventBus.LogEmitted`. |
| `SettingsViewModel.cs` | API-Keys, Server-Verbindung, Pairing, Theme, Push-Notifications. |
| `SettingsHistoryViewModel.cs` | Settings-Audit-Trail (wer hat wann was geändert). |
| `ActivityFeedViewModel.cs` | Top-20-Einträge des Activity-Feeds (Live-Ereignisse). |
| `BtcTickerViewModel.cs` | BTC-USDT-Preis-Anzeige als Markt-Indikator in der Top-Bar. |
| `PositionDisplayItem.cs` | Display-Modell für eine offene Position (Preis, PnL, SL/TP-Levels). |

## Lazy-VM-Pattern (Startup-Beschleunigung)

`MainViewModel` löst das DI beim Start nur für das Dashboard auf. Alle anderen VMs werden
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

ViewModels abonnieren `ISettingsService.SettingsChanged` für Multi-Client-Sync. Beim Remote-Mode
feuert `RemoteSettingsService.RaiseChanged` dieses Event wenn ein anderer Client Settings ändert.
Neue ViewModels sollen `ISettingsPersistenceService` per DI injizieren statt `App.SaveAllSettingsAsync()`.

## BotEventBus-Abo-Pattern

ViewModels subscriben in ihrem Konstruktor und desubscriben in `Dispose()`:

```csharp
public DashboardViewModel(BotEventBus eventBus, ...)
{
    _eventBus = eventBus;
    _eventBus.BotStateChanged += OnBotStateChanged;
}

public void Dispose()
{
    _eventBus.BotStateChanged -= OnBotStateChanged;
}
```

**Alle ViewModels, die `BotEventBus` abonnieren, implementieren `IDisposable`.**

## Mobile-Shell-Besonderheiten

- Mobile-Varianten (`DashboardViewMobile`, etc.) werden via `ViewLocator` automatisch ausgewählt
  wenn `App.IsMobileShell = true` (Android). Gleiches ViewModel, andere View.
- `MainViewModel.IsMoreDrawerOpen` steuert das Bottom-Sheet auf Mobile (seltener genutzte Views:
  Strategie, Backtest, Risiko, Settings).
- **Content-Swap statt gestapelter Views**: `CurrentPageViewModel` + ein einzelnes
  `<ContentControl Content="{Binding CurrentPageViewModel}"/>` — niemals 8 Views parallel,
  das führt auf Mobile zu Crashes.

## Domain-Gotchas

- **`_tradesToday` MUSS `volatile`** — JIT kann nicht-volatile Felder bei parallelen Reads cachen.
- **ContinueWith IMMER mit `TaskScheduler.Default`** — sonst UI-Thread-Deadlock möglich.
- **OriginalQuantity IMMER die tatsächlich platzierte Menge** (nach Equity/Score-Scaling),
  nicht `riskCheck.AdjustedPositionSize`.
