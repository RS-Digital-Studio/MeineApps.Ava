# Views — AXAML-Views (Desktop + Mobile)

Jede View existiert in zwei Varianten: Desktop (`XyzView`) und Mobile (`XyzViewMobile`).
Der `ViewLocator` wählt zur Laufzeit anhand von `App.IsMobileShell` automatisch die richtige Variante.
Generische MVVM-/Compiled-Binding-/DI-Regeln → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| View | Desktop | Mobile | Zweck |
|------|---------|--------|-------|
| Main | `MainView` | `MainViewMobile` | Shell mit Tab-Bar (Desktop) bzw. Bottom-Nav + More-Drawer (Android) |
| Dashboard | `DashboardView` | `DashboardViewMobile` | Balance, Positionen, Bot-Controls, Equity-Chart |
| Scanner | `ScannerView` | `ScannerViewMobile` | Live-Scan-Ergebnisse, Filter, TF-Auswahl |
| Strategy | `StrategyView` | `StrategyViewMobile` | Aktive Strategie-Parameter (TrendFollow-Fast) |
| Backtest | `BacktestView` | `BacktestViewMobile` | Historischer Test, Walk-Forward, Trade-Replay |
| TradeHistory | `TradeHistoryView` | `TradeHistoryViewMobile` | Trade-Liste, gefiltert |
| RiskSettings | `RiskSettingsView` | `RiskSettingsViewMobile` | Risiko-Parameter |
| Log | `LogView` | `LogViewMobile` | Live-Log mit Level/Kategorie-Filter |
| Settings | `SettingsView` | `SettingsViewMobile` | API-Keys, Server, Pairing, Theme, Push |
| SettingsHistory | `SettingsHistoryView` | `SettingsHistoryViewMobile` | Settings-Audit-Trail |
| — | `MainWindow` | — | Desktop-Fenster-Host (`IClassicDesktopStyleApplicationLifetime`) |

## Virtualisierung (Pflicht bei langen Listen)

`TradeHistoryView`, `LogView`, `BacktestView` (Trade-Replay-Liste) und `ScannerView` (Scan-Ergebnisse)
verwenden `VirtualizingStackPanel`. Ohne Virtualisierung friert die UI bei großen Datensätzen
(100+ Einträge) ein.

## Mobile-Navigation — More-Drawer

`MainViewMobile` zeigt eine Bottom-Tab-Bar mit **5 Tabs** (Dashboard, Scanner, TradeHistory, Log,
Mehr). Der "Mehr"-Tab öffnet ein Bottom-Sheet (`DrawerSheet`) via `MainViewModel.ToggleMoreDrawerCommand`.
Das Sheet liegt als Overlay über dem `ContentControl` und enthält vier seltenere Views: Strategie,
Backtest, RiskSettings, Settings.

**Warum kein ZIndex-Overlay für die Haupt-Navigation?** Avalonia `ZIndex` auf Grid-Kindern
funktioniert auf Android nicht für Hit-Testing — Touch-Events gehen durch. Das More-Sheet umgeht
das, weil es das gesamte Overlay selbst managed (`IsVisible`-Binding + Scrim).

## UI-Stil (BingX-spezifisch)

- **Monospace-Zahlen** (Consolas) für Preise, PnL und Metriken — proportionale Schrift lässt
  Spalten zittern bei Updates.
- **Dark-Mode Default** via `ThemeVariant.Dark`. Theme ist via `BotSettings.ThemePreference`
  umstellbar (Dark/Light/System).
- **Keyboard-Shortcuts** (Desktop): Ctrl+1–8 für Tab-Navigation, F5/F6/F7/F12 für Bot-Kontrolle
  (F5=Start, F6=Pause, F7=Stop, F12=Notfall-Stop — definiert in `DashboardView`),
  Escape navigiert zum Dashboard.
