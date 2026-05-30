# BingXBot.Shared — Composition Root & UI-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält ViewModels, Views, Services und Grafik-Renderer.
Wird von `BingXBot.Android` und `BingXBot.Desktop` referenziert. Die Trading-Engine lebt in den
Backend-Libraries (`BingXBot.Trading`, `BingXBot.Engine`, etc.) — kein Trading-Code hier.

Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md).
App-Überblick + Trading-Architektur + SK-System → [../CLAUDE.md](../CLAUDE.md).

---

## Composition Root (`App.axaml.cs`)

Einziger Ort wo Services + ViewModels verdrahtet werden. Kein Service-Locator anderswo.

### `ConfigureServices(IServiceCollection)`

Registriert **alles als Singleton**. Besonderheiten:

- **`IAppPaths`**: Factory-Pattern — `AppPathsFactory?.Invoke() ?? new AppPaths()`. Android setzt
  `App.AppPathsFactory` in `MainActivity.OnCreate` VOR dem DI-Build auf `AndroidAppPaths(this)`,
  damit Sandbox-Pfade (`Context.FilesDir`) statt `Environment.SpecialFolder.UserProfile` genutzt werden.
  `Environment.SpecialFolder.UserProfile` crasht auf Android.
- **Modus-Conditional**: `IsRemoteModeEnabled()` prüft ob `client/connection.json` existiert.
  Remote-Mode → Remote-Impls (`RemoteBotControlService`, `RemoteBotEventStream`, etc.).
  Local-Mode → Local-Impls + `LocalBotEventStream` + `DecisionTrailBuffer` + `TradeStatsAggregator`.
- **`LocalBotEventStream`** wird im Remote-Mode NICHT registriert — sonst tote Subscriptions auf dem
  `BotEventBus`.
- **`ValidateOnBuild = true`** im `ServiceProviderOptions` — fängt fehlende Konstruktor-Params beim
  App-Start statt beim ersten Resolve.
- **`Lazy<T>`-Wrapper** (`LazyDiService<T>`): `services.AddTransient(typeof(Lazy<>), typeof(LazyDiService<>))`
  aktiviert `Lazy<T>`-Injection für alle Typen. Nötig weil `Microsoft.Extensions.DependencyInjection`
  `Lazy<T>` nicht out-of-the-box auflöst.

### `OnFrameworkInitializationCompleted()`

1. DI-Container synchron aufbauen (`ConfigureServices` + `BuildServiceProvider`).
2. `MarketCapRefreshHelper.Configure(new CoinGeckoMarketCapProvider())` — statischer Bridge für HTTP in Engine.
3. `BotSettings.UseRemoteMode = IsRemoteModeEnabled()`.
4. Im Remote-Mode: `ServerConnection.LoadPersistedProfile()` synchron — damit `SettingsViewModel`
   beim ersten Render die korrekte Server-URL zeigt (kein kurzes "Nicht verbunden"-Flimmern).
5. `MainViewModel` eager resolven.
6. Lifecycle-Branch:
   - `IClassicDesktopStyleApplicationLifetime` → `MainWindow { Content = mainVm }`, `IsMobileShell = false`.
   - `IActivityApplicationLifetime` → `MainViewFactory = () => ContentControl { Content = mainVm }`, `IsMobileShell = true` (Android).
   - `ISingleViewApplicationLifetime` → Fallback-Pfad (iOS, seltene Plattformen).
7. `InitializeBackgroundAsync()` als fire-and-forget — DB-Init, Settings-Restore, SignalR-Connect.

### `InitializeBackgroundAsync()`

Läuft auf Background-Thread. Exceptions nur loggen — App muss auch ohne DB/Netz starten.

**Local-Mode:**
1. `BotDatabaseService.InitializeAsync()` + `LoadSettingsAsync()`.
2. `RestoreSettingsFromDb()` auf UI-Thread (Dispatcher.UIThread.Post).
3. `DecisionTrailBuffer` auf `BotEventBus.EvaluationDecided` subscriben + DB-Persist.
4. `TradeStatsAggregator` mit den letzten 10.000 Trades aus DB rebuilden.

**Remote-Mode:**
1. `RefreshRemoteSettingsAsync()` — REST GET /settings, `RestoreSettingsFromDb` auf lokale Singletons.
2. `RemoteSettingsService.SettingsChanged`-Subscription für Multi-Client-Sync.
3. `RemoteSettingsAutoSync` eager resolven (abonniert `ConnectionChanged` für Re-Connect-Refreshes).
4. `IBotEventStream.StartAsync()` — SignalR-Verbindung aufbauen.

### `RestoreSettingsFromDb(BotSettings saved)`

Schreibt alle Settings-Blöcke aus der DB/Server auf die DI-Singletons (`RiskSettings`,
`ScannerSettings`, `BacktestSettings`, `BotSettings`). Läuft bei jedem App-Start und bei
Remote-Re-Connect. **Nicht einsparen** — die DI-Singletons sind die Binding-Quelle für alle ViewModels.

### Default-Snapshot-Sanity-Check (`LooksLikeFreshDefault`)

Verhindert, dass ein Auth-Fehler oder Race beim Remote-Settings-Refresh echte Server-Werte mit
Konstruktor-Defaults überschreibt. Prüft ob `MaxLeverage + MaxOpenPositions + MaxPositionSizePercent +
MaxTotalDrawdownPercent` gleichzeitig auf Konstruktor-Default stehen — wenn ja, Snapshot verwerfen.

---

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `BingXBot.ViewModels` |
| `Views/` | `BingXBot.Views` |
| `Graphics/` | `BingXBot.Graphics` |
| `Converters/` | `BingXBot.Converters` |
| `Services/` | `BingXBot.Services` |

---

## Unterordner

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `ViewModels/` | MainViewModel + alle Sub-VMs (Dashboard, Scanner, Backtest, etc.), Lazy-Pattern, BotEventBus-Abo | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | AXAML-Views (Desktop + Mobile-Varianten), MVVM-Strict-Regeln, More-Sheet | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Graphics/` | SkiaSharp-Renderer (Equity, Drawdown, Candlestick, PnL-Kalender, Gauge, Korrelation) | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |
| `Converters/` | NullableDecimalConverter, StaleOpacityConverter | [Converters/CLAUDE.md](Converters/CLAUDE.md) |
| `Services/` | RemoteSettingsAutoSync | [Services/CLAUDE.md](Services/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Primary `#3B82F6`),
`Resources/Strings/` (AppStrings.resx, 6 Sprachen), `Assets/`.

---

## Build

```bash
dotnet build src/Apps/BingXBot/BingXBot.Shared
```
