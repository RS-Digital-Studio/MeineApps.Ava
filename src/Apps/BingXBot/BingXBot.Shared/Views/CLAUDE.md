# Views — AXAML-Views (Desktop + Mobile)

Jede View existiert in zwei Varianten: Desktop (`XyzView`) und Mobile (`XyzViewMobile`).
Der `ViewLocator` wählt zur Laufzeit anhand von `App.IsMobileShell` automatisch die richtige Variante.
Generische UI-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| View | Desktop | Mobile | Zweck |
|------|---------|--------|-------|
| Main | `MainView` | `MainViewMobile` | Shell mit Tab-Bar (Desktop) bzw. Bottom-Nav + More-Sheet (Android) |
| Dashboard | `DashboardView` | `DashboardViewMobile` | Balance, Positionen, Bot-Controls, Equity-Chart, SK-Ampel |
| Scanner | `ScannerView` | `ScannerViewMobile` | Live-Scan-Ergebnisse, Filter, TF-Auswahl |
| Strategy | `StrategyView` | `StrategyViewMobile` | SK-Parameter-Editor, TF-Visualisierung |
| Backtest | `BacktestView` | `BacktestViewMobile` | Historischer Test, Walk-Forward, Trade-Replay |
| TradeHistory | `TradeHistoryView` | `TradeHistoryViewMobile` | Trade-Liste, gefiltert |
| RiskSettings | `RiskSettingsView` | `RiskSettingsViewMobile` | Risiko-Parameter |
| Log | `LogView` | `LogViewMobile` | Live-Log mit Level/Kategorie-Filter |
| Settings | `SettingsView` | `SettingsViewMobile` | API-Keys, Server, Pairing, Theme, Push |
| DecisionTrail | `DecisionTrailView` | `DecisionTrailViewMobile` | Decision-Trail mit Filter |
| SettingsHistory | `SettingsHistoryView` | `SettingsHistoryViewMobile` | Settings-Audit-Trail |
| — | `MainWindow` | — | Desktop-Fenster-Host (`IClassicDesktopStyleApplicationLifetime`) |

## Pflicht-Konventionen (MVVM-Strict)

- `x:CompileBindings="True"` + `x:DataType` auf **jeder** View-Root.
- **KEIN** `DataContext = ...` im Code-Behind — `ViewLocator` setzt das.
- **KEIN** `App.Services.GetRequiredService<T>()` im View-Ctor — Android-Crash-Pattern.
- Commands per `[RelayCommand]`, keine Click-Handler.
- Bei VM-Events (z.B. `NavigationRequested`): `DataContextChanged`-Pattern — sauber an-/abmelden.

## Virtualisierung (Pflicht bei langen Listen)

`TradeHistoryView`, `LogView`, `BacktestView` (Trade-Replay-Liste) und `ScannerView` (Scan-Ergebnisse)
**müssen** `VirtualizingStackPanel` oder `RecyclingItemsPanel` verwenden. Ohne Virtualisierung
friert die UI bei großen Datensätzen (100+ Einträge) ein.

## Mobile-Navigation — More-Sheet

Auf Android zeigt `MainViewMobile` eine Bottom-Tab-Bar mit 4 Haupt-Tabs (Dashboard, Scanner,
TradeHistory, Log). Seltenere Views (Strategie, Backtest, Risiko, Settings, Diagnose) öffnet
ein Bottom-Sheet via `MainViewModel.IsMoreDrawerOpen = true`.

**Warum kein ZIndex-Overlay?** Avalonia `ZIndex` auf Grid-Kindern funktioniert auf Android
nicht für Hit-Testing — Touch-Events gehen durch. Das Sheet nutzt Content-Swap statt Overlay.

## UI-Stil

- **Monospace-Zahlen** (Consolas) für Preise, PnL und Metriken — proportionale Schrift lässt
  Spalten zittern bei Updates.
- **Dark-Mode Default** via `ThemeVariant.Dark`. Theme ist via `BotSettings.ThemePreference`
  umstellbar (Dark/Light/System).
- **Keyboard-Shortcuts** (Desktop): Ctrl+1–8 für Tab-Navigation, F5/F6/F7/F12 für Bot-Kontrolle,
  Escape navigiert zum Dashboard.
