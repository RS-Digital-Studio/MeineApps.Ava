# BingXBot — Trading Bot für BingX Perpetual Futures

Automatisierter SK-System-Trading-Bot mit Client/Server-Architektur. Server läuft 24/7 auf
Raspberry Pi 5, Steuerung über Desktop (Windows/Linux) und Android-App. Handel auf BingX
USDT-margined Perpetual Futures (Crypto + TradFi-Perps via NC-Prefix).

| Aspekt | Wert |
|--------|------|
| Topologie | Pi-Server (Engine, 24/7) + Desktop/Android Remote-Clients |
| Strategie | **TrendFollow-Fast** (Donchian-Breakout, Live-Default seit 31.05.2026, H4-only) + SK-System (Reversal, optional) — Multi-TF Standalone |
| Exchange | BingX Perpetual Futures (USDT-M) |
| Pi-Server | `steuerung@raspberrypi.local` (systemd-Service `bingxbot.service`) |

Für generische Build-Befehle, Conventions und Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Modus-Conditional, Settings-Restore | `App.axaml.cs`, Service-Registrierungen, Local/Remote-Branch | [BingXBot.Shared](BingXBot.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, AppPaths-Factory, Manifest, Back-Button | [BingXBot.Android](BingXBot.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs`, Standalone vs. Remote-Client-Modus | [BingXBot.Desktop](BingXBot.Desktop/CLAUDE.md) |
| ViewModels (alle Sub-VMs, Lazy-Pattern, BotEventBus-Abo) | MainVM, DashboardVM, ScannerVM, BacktestVM, … | [Shared/ViewModels](BingXBot.Shared/ViewModels/CLAUDE.md) |
| Views (Desktop + Mobile-Varianten, MVVM-Strict, More-Sheet) | Alle AXAML-Views, ViewLocator-Konvention | [Shared/Views](BingXBot.Shared/Views/CLAUDE.md) |
| SkiaSharp-Renderer (Equity, Drawdown, Candlestick, Gauge) | Alle 6 Renderer, Paint-Cache-Strategie | [Shared/Graphics](BingXBot.Shared/Graphics/CLAUDE.md) |
| Converters (NullableDecimal, StaleOpacity) | XAML-Wert-Konverter | [Shared/Converters](BingXBot.Shared/Converters/CLAUDE.md) |
| Services (RemoteSettingsAutoSync) | Client-seitige Service-Impls | [Shared/Services](BingXBot.Shared/Services/CLAUDE.md) |

---

## Build & Zielframework

| Projekt | Framework | Befehl |
|---------|-----------|--------|
| `BingXBot.Core` | `net10.0` | `dotnet build src/Libraries/BingXBot.Core` |
| `BingXBot.Contracts` | `net10.0` | `dotnet build src/Libraries/BingXBot.Contracts` |
| `BingXBot.Exchange` | `net10.0` | `dotnet build src/Libraries/BingXBot.Exchange` |
| `BingXBot.Engine` | `net10.0` | `dotnet build src/Libraries/BingXBot.Engine` |
| `BingXBot.Backtest` | `net10.0` | `dotnet build src/Libraries/BingXBot.Backtest` |
| `BingXBot.Trading` | `net10.0` | `dotnet build src/Libraries/BingXBot.Trading` |
| `BingXBot.ClientApi` | `net10.0` | `dotnet build src/Libraries/BingXBot.ClientApi` |
| `BingXBot.Server` | `net10.0` | `dotnet run --project src/Apps/BingXBot/BingXBot.Server` |
| `BingXBot.Shared` | `net10.0` | `dotnet build src/Apps/BingXBot/BingXBot.Shared` |
| `BingXBot.Desktop` | `net10.0` | `dotnet run --project src/Apps/BingXBot/BingXBot.Desktop` |
| `BingXBot.Android` | `net10.0-android` | `dotnet build src/Apps/BingXBot/BingXBot.Android` |

Pi-Server-Deploy via Skill `/server-deploy` (siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)).

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `BingXBot.Core/Models/` | `BingXBot.Core.Models` |
| `BingXBot.Core/Diagnostics/` | `BingXBot.Core.Diagnostics` |
| `BingXBot.Contracts/Dtos/` | `BingXBot.Contracts.Dtos` |
| `BingXBot.Engine/Indicators/` | `BingXBot.Engine.Indicators` |
| `BingXBot.Trading/Services/` | `BingXBot.Trading.Services` |
| `BingXBot.Server/Api/` | `BingXBot.Server.Api` |
| `BingXBot.Server/Services/` | `BingXBot.Server.Services` |
| `BingXBot.Shared/ViewModels/` | `BingXBot.ViewModels` |
| `BingXBot.Shared/Views/` | `BingXBot.Views` |
| `BingXBot.Shared/Services/` | `BingXBot.Services` |

---

## Projekt-Struktur

```
src/
├── Libraries/
│   ├── BingXBot.Core/         # Domain (Models, Enums, DB-Entities, Interfaces, Helpers, Diagnostics)
│   ├── BingXBot.Contracts/    # DTOs, REST-Routen, Hub-Methoden, Service-Interfaces
│   ├── BingXBot.Exchange/     # BingXRestClient + WebSocket-Client, RateLimiter, SymbolInfoCache
│   ├── BingXBot.Engine/       # SK-Strategie + Indikatoren + Confluence-Scoring + News-Service
│   ├── BingXBot.Backtest/     # BacktestEngine + SimulatedExchange + PerformanceReport + WalkForwardRunner
│   ├── BingXBot.Trading/      # TradingServiceBase + Live/Paper/Manager + DB-Service + Telemetry
│   └── BingXBot.ClientApi/    # HTTP- + SignalR-Remote-Impls + PairingClient (für Desktop/Android)
└── Apps/BingXBot/
    ├── BingXBot.Shared/       # ViewModels + Views + Local-Service-Impls (DI-zentriert)
    ├── BingXBot.Desktop/      # Avalonia Desktop (Standalone- ODER Remote-Client)
    ├── BingXBot.Server/       # ASP.NET Core Minimal API + SignalR-Hub (Pi)
    └── BingXBot.Android/      # Avalonia.Android (Remote-Client, Portrait)
```

**Trennungs-Pattern**: Server kennt KEINE Avalonia/Shared/Desktop-Referenzen — Server-Image
auf dem Pi ist headless. Client-Apps kennen Server-internals NUR über `BingXBot.Contracts`
(Service-Interfaces + DTOs), niemals direkt.

---

## Client/Server-Architektur

### Modi

| Modus | Wer hostet die Engine | UI |
|-------|----------------------|-----|
| **Standalone** | Desktop-App selbst | Lokal |
| **Remote-Client** | Pi-Server | Desktop oder Android verbindet sich |
| **Server** | Pi (24/7) | Keine UI — REST + SignalR |

Modus wird per `ServerProfile` in `~/.config/bingxbot/client/connection.json` gewählt.
Wenn `ServerProfile` gesetzt ist, registriert die DI Remote-Service-Impls; sonst Local-Impls.

### Pairing-Flow

1. Pi-Server startet, generiert 6-stelligen Pairing-Code (5 min gültig, max 5 Fehlversuche).
2. Code ablesen via `journalctl -u bingxbot` oder `/var/lib/bingxbot/pairing-code.txt`.
3. Client gibt Server-URL + Code ein.
4. Server liefert Bearer-Token + Refresh-Token (Token 7d, Refresh 30d, beide rotierend).
5. Token wird im Client persistiert: Linux/Mac `~/.config/bingxbot/tokens.json`,
   Windows `%APPDATA%\bingxbot\tokens.json`, Android intern (`Context.FilesDir`).

### Service-Interfaces (alle ViewModels sprechen NUR gegen diese)

| Interface | Zweck | Local-Impl | Remote-Impl |
|-----------|-------|-----------|-------------|
| `IBotControlService` | Start/Stop/EmergencyStop | `LocalBotControlService` | `RemoteBotControlService` |
| `ISettingsService` | Risk/Scanner/Bot/Backtest-Settings | `LocalSettingsService` | `RemoteSettingsService` |
| `ISettingsPersistenceService` | Persistiert alle Settings-Blöcke (SaveAllAsync, Semaphore-geschützt) | `SettingsPersistenceService` | — (nur Local benötigt) |
| `IAccountService` | Balance/Positions/Orders | `LocalAccountService` | `RemoteAccountService` |
| `ITradeHistoryService` | Trades aus DB | `LocalTradeHistoryService` | `RemoteTradeHistoryService` |
| `IBotEventStream` | SignalR-Events (Push) | `LocalBotEventStream` | `RemoteBotEventStream` |
| `IBacktestControlService` | Backtest-Control | `LocalBacktestService` | `RemoteBacktestService` |
| `IStrategyCatalog` | Strategie-Metadaten | `LocalStrategyCatalog` | — (kein Remote-Impl) |
| `IStatsService` | Stats-Breakdown (TF × Category × Mode) | `LocalStatsService` | `RemoteStatsService` |

### REST-Routen (`/api/v1/...`)

| Gruppe | Endpoints |
|--------|-----------|
| Public (Auth-frei) | `/health`, `/pair/init`, `/pair/complete`, `/pair/cancel`, `/auth/refresh`, `/metrics/internal`, `/metrics` |
| Auth | `/auth/logout`, `/auth/logout-others` |
| Status | `/status`, `/account`, `/positions`, `/open-orders`, `/equity` |
| Bot-Control | `/bot/start`, `/bot/stop`, `/bot/emergency-stop`, `/position/{symbol}/close` |
| Admin | `/admin/backfill-trades` (POST, Trade-Backfill aus BingX-Income, Body `{ fromUtc, toUtc? }`) |
| Settings | `/settings`, `/settings/risk`, `/settings/scanner`, `/settings/bot`, `/settings/backtest`, `/settings/history` |
| Trades & Logs | `/trades`, `/trades/summary`, `/scanner/results`, `/logs` |
| Backtest | `/backtest/start`, `/backtest/{jobId}`, `/backtest/{jobId}/result`, `/backtest/{jobId}/cancel`, `/backtest/replay-trade/{tradeId}` |
| Credentials | `/credentials/status`, `/credentials` (PUT) |
| Decisions | `/decisions` (Decision-Trail-Query) |
| Stats | `/stats/breakdown` |
| Devices | `/devices/fcm` (FCM-Token-Registrierung) |

`PublicPaths`-Liste in `BearerAuthMiddleware` — Metrics-Endpoints sind public weil
Pi hinter Tailscale steht und Prometheus/Grafana-Scraper scrapen müssen.

### SignalR-Hub `/hubs/bot`

Server pusht Events (throttled wo nötig):

| Event | Throttle | Inhalt |
|-------|----------|--------|
| `BotStateChanged` | — | Engine-State (Running/Stopped/Paused) |
| `TickerUpdate` | 1/s/Symbol | Symbol-Preis-Snapshot |
| `BtcPriceUpdate` | 5s | BTC-USDT als Markt-Indikator |
| `TradeOpened` | — | Neuer Trade mit Navigator-TF |
| `TradeClosed` | — | CompletedTrade mit Reason |
| `PositionUpdated` | 2s/(Symbol,Side) | Mark-Price/PnL/SL/TP-Refresh |
| `EquityUpdate` | nach Trade-Close | Equity-Curve-Punkt |
| `LogEmitted` | — | Einzel-Log-Eintrag |
| `LogBatch` | 250ms-Buffer | List<LogEntry> für Scan-Bursts |
| `ActivityFeed` | — | User-relevante Aktion (Top-20-Liste) |
| `MarginWarning` | — | Liquidation-Approach-Warnung |
| `BacktestProgress` | per Step | Backtest-Fortschritt |
| `BacktestCompleted` | — | Backtest-Ergebnis |
| `ScannerResult` | per Sweep | Scanner-Symbol-Liste pro TF |
| `ConnectionDegraded` | Edge-Transition | BingX-Connection-Status |
| `EvaluationDecided` | per Evaluate | Decision-Trail-Eintrag |
| `NewsServiceDegraded` | Edge-Transition | News-Service-Health-Status |
| `SettingsChanged` | bei Save | Multi-Client-Sync |

Client-Invokes: `SubscribeSymbol`, `UnsubscribeSymbol`, `SetLogFilter`, `Ping`.

### Deployment

```bash
# Linux-arm64 self-contained Pi-Bundle bauen:
bash src/Apps/BingXBot/BingXBot.Server/systemd/publish.sh

# Erstinstallation auf dem Pi (systemd-Service installieren):
bash src/Apps/BingXBot/BingXBot.Server/systemd/install.sh raspberrypi.local

# Update bestehender Installation (tar-Stream, läuft auch auf Git-Bash Windows):
bash src/Apps/BingXBot/BingXBot.Server/systemd/update.sh raspberrypi.local
```

Systemd-Unit: `bingxbot.service`. User: `steuerung`. Install-Pfad: `/home/steuerung/bingxbot`.
Daten-Pfad: `/var/lib/bingxbot`. SSH-Passwort `qwer`.

### Server-Sicherheit

- **Kein TLS by default** (`http://0.0.0.0:5050`) → Tailscale empfohlen für Remote-Zugriff.
- **Bearer-Token**: 7d gültig, in `~/.config/bingxbot/tokens.json` (chmod 600). Refresh-Token 30d.
  Cleanup periodisch über `AuthTokenCleanupService` (24h-Tick).
- **BingX-Credentials**: AES-256-GCM auf Pi (`/var/lib/bingxbot/credentials.bin`, chmod 600).
  Auto-Migration aus altem AES-CBC-Format (Versions-Marker 0x02). Master-Key in `.masterkey`
  (chmod 400, per-Installation Random).
- **Pairing-Code**: 5min gültig, nach Verwendung gelöscht, max 5 Fehlversuche.
- **Rate-Limit**: `/pair/*` max 5/5min, `/credentials-write` max 3/min, andere granular per
  RateLimiter-Konfiguration in `Program.cs`.
- **Logout**: `/api/v1/auth/logout` revoked aktuelles Token, `/auth/logout-others` revoked
  alle ausser dem aktuellen (Logout-überall nach Mobile-Diebstahl).

### HostedServices (Pi-Bootstrap)

| Service | Tick | Zweck |
|---------|------|-------|
| `BotAutoResumeService` | Once @ Start (15s) + Backfill-Loop (30 min) | Reaktiviert Engine nach Crash/Reboot wenn `WasRunningOnShutdown=true`. **Periodischer Trade-Backfill (30 min, letzte 3 h)** fängt native SL/TP-Closes bei laufendem Bot ein (Dedup gegen alle Live-Trades per Symbol+ExitTime). Public `BackfillFromBingxAsync` für Admin-Endpoint |
| `BotHubEventForwarder` | Event-getrieben | Forward `IBotEventStream` → SignalR-Hub mit Log-Batching (250ms) und Throttling |
| `EquitySnapshotService` | 5 min + Event | Persistiert Equity-Snapshot periodisch + sofort nach Trade-Close (2 s Debounce). Publiziert `EquityUpdate` an SignalR-Clients |
| `DbLogPersistenceService` | 250 ms-Batch | Subscribed `LogEmitted`, schreibt im Batch in `LogEntries`. Toggle `EnableDbLogPersistence`, MinLevel-Filter |
| `ServerHealthWatchdog` | 30s | BingX `GetServerTimeAsync`-Probe + Clock-Drift-Detection (warn 2s, degrade 4s — recvWindow 5s) |
| `DbBackupService` | Daily @ 03:00 UTC | SQLite VACUUM + File.Copy nach `{DataDir}/backups/bot-YYYY-MM-DD.db`, 7-Tage-Retention |
| `DbArchiveService` | Monthly @ 1. 04:00 UTC | Trades > 90d in `bot-archive-{YYYY-MM}.db`, Decisions-/SettingsChanges-Purge |
| `AdaptiveTfDisableService` | 60min | Auto-disable TFs mit WinRate < Threshold und ≥ MinSample, Re-Probing nach Cutoff |
| `StaleEngineDetector` | 10min | FCM-Push wenn Bot Running aber > 6h ohne Scanner/Trade-Aktivität |
| `FcmPushService` | Event-getrieben | Trade-Push (TradeOpened/Closed/SL-Hit) an Mobile via Firebase-Admin-SDK |
| `FcmTokenCleanupService` | 24h | Stale FCM-Devices > 30d entfernen |
| `AuthTokenCleanupService` | 24h | Expired Bearer-/Refresh-Tokens entfernen |

---

## Trading-Architektur

### Service-Hierarchie

```
TradingServiceBase (abstrakt)
├── PaperTradingService    → SimulatedExchange (Isolated Margin, spiegelt Live)
└── LiveTradingService     → BingXRestClient + WebSocket User-Stream
                              ↑
                          LiveTradingManager (Lifecycle: Connect, Recovery, Commission, Server-Zeit-Sync)
```

### `TradingServiceBase`-Loops

| Loop | Intervall | Aufgaben |
|------|-----------|----------|
| `RunLoopAsync` | 60s | Scanner → Klines (parallel) → Strategy.Evaluate → RiskManager.ValidateTrade → Order-Placement |
| `PriceTickerLoopAsync` | 5s | SL/TP-Hit-Check, BE-Trigger, Partial-Close, Preis-Updates, TradFi-Stunden-Check, Pending-Limit-Reconcile, Stage-3-PendingTpRetry |
| `HeartbeatLoopAsync` | 30s | `BotDatabaseService.SaveLastHeartbeatAsync` (für Crash-Recovery + Trade-Replay-Backfill) |
| `ReconcileLoopAsync` | 60s (Live nur) | **Adoption unmanaged/unvollstaendiger Positionen** (`AdoptUnmanagedPositionsAsync`, s.u.) → `PositionDriftAnalyzer` → Orphan/Unmanaged/MissingStop/MissingTp + Pending-Limit-Order-Stale-Expiry |

**Jede offene Position wird jeden Reconcile-Durchgang voll abgesichert** (`AdoptUnmanagedPositionsAsync`,
laeuft VOR dem Analyzer): Positionen ohne Bot-Signal — oder mit unvollstaendigem Recovery-Signal
(`TakeProfit=null`/`DisableSmartBreakeven=false`, wie `RecoverOpenPositions` sie beim Start anlegt) — werden
adoptiert. Notfall-SL (`max(1.5%, 0.03/Leverage)`) wenn nativ keiner liegt, dann ein vollstaendiges Signal
(SL+TP1+TP2+BE). TP aus vorhandenen Limit-Orders oder SL-Distanz × RRR 1.5/3.0. Vervollstaendigung per direktem
`with`-Update (NICHT `RestorePositionSignal` — dessen Fallback uebernaehme sonst das alte `DisableSmartBreakeven=false`).

- **`ReplaceMissingStop` setzt nur den SL** (`SetPositionSlTpAsync(…, null)`) — der TP laeuft ausschliesslich
  ueber den reduce-only-LIMIT-Pfad (`ReplaceMissingTakeProfit`/`PlaceTpLimitOrdersAfterFill`). Ein nativer
  `TAKE_PROFIT_MARKET` wuerde bei TP1 die ganze Position schliessen und die 50/50-Teilschliessung aushebeln.
- **Min-Qty-aware TP-Split** (`SplitTpQuantity` + `IExchangeClient.MeetsMinimumOrder`): faellt eine 50/50-
  Teilmenge unter die Symbol-Min-Qty (z.B. ETH 0.01 / Min-Qty 0.01 → 0.005), wird KEIN Split gemacht — ein
  Full-TP bei TP1, TP2 wird aus dem Signal entfernt (verhindert BingX-Reject + Endlos-Re-Place). `MeetsMinimumOrder`
  ist eine Default-Interface-Methode (`=> true`); nur `BingXRestClient` delegiert an den `SymbolInfoCache`.

### Multi-TF Standalone

**Ein** SK-Strategy-Service evaluiert pro Symbol alle aktiven Navigator-Timeframes parallel.

| Aktive TF | Default-Konfig |
|-----------|----------------|
| W1 | Fahrplan-Bias (übergeordnete Marktanalyse) |
| D1 | Navigator + Fahrplan |
| H4 | Navigator + Fahrplan |
| H1 | Navigator (Filter-TF: M15) |
| M15 | Navigator (Filter-TF: M5) |

Pro Symbol-Evaluate-Aufruf wird die `MarketContext.NavigatorTimeframe` gesetzt; die
Strategie greift auf Navigator-Candles + Filter-TF-Candles zu.

`AmpelStatus` ist `Dictionary<TimeFrame, string>` — pro TF separater Status für UI.

**Dedup pro Position**: Key `{symbol}_{side}` — eine BingX-Position pro (Symbol, Side).
Wenn ein TF-Signal offen ist, werden weitere TF-Signale für gleiche Seite geskippt.

**Strategie-Kloning**: Pro `{symbol}|{tf}` ein eigener `SequenceStateMachine`-Instance,
um Cross-Contamination zwischen Symbolen + TFs zu vermeiden.

### Order-Pfad (Live)

```
Scanner.Filter → Strategy.Evaluate → RiskManager.ValidateTrade
    ↓
LiveTradingService.PlaceOrderOnExchangeAsync
    ↓
OrderRetryPolicy.ExecuteAsync (Retry mit IdempotencyCheck via TpOrderMatcher)
    ↓
BingxNativeSlTpManager (native SL/TP-Cancel + Update)
    ↓
PendingLimitOrders-Persist → Stage-3-Retry-Loop bei BingX-Hochlast
```

### News-Blackout (Pre-Resolved)

`TradingServiceBase` ruft 1× pro Scan-Tick `RiskManager.ResolveActiveNewsBlackoutAsync`
und füllt `MarketContext.ResolvedNewsBlackoutEvent`. Die `SequenzKonzeptStrategy` und
der zweite RiskManager-Check lesen den Cache (kein Sync-over-Async pro Symbol-Tick).

Bei News-Service-Failure ≥ 5× → `NewsServiceHealthChanged`-Event → UI-Banner
"News-Filter degradiert".

### Composition-Bibliotheken

Aus `LiveTradingService` extrahierte pure-function- und Manager-Klassen:

| Klasse | Pfad | Zweck |
|--------|------|-------|
| `TpOrderMatcher` | `BingXBot.Trading/Reconciliation/` | Side+Qty+Price-Match mit Toleranz für Idempotency-Probe |
| `BingxNativeSlTpManager` | `BingXBot.Trading/NativeSlTpManager/` | Native SL/TP-Cancel + SL-Update mit Retry |
| `PositionDriftAnalyzer` | `BingXBot.Trading/Reconciliation/` | Pure-function-Drift-Erkennung (Orphan/Unmanaged/MissingStop/MissingTp) |
| `OrderRetryPolicy` | `BingXBot.Trading/Resilience/` | Exponential-Backoff-Retry mit IdempotencyCheck |
| `BreakevenCalculator` | `BingXBot.Core/Services/` | A-Bruch + 2x-SL-Trigger-Logik (zentral, Live + Backtest) |
| `FeeCalculator` | `BingXBot.Core/Services/` | Fee + Net-PnL (konsistent zwischen Live + Paper + Backtest) |

---

## Strategien (`StrategyFactory`)

Alle Strategien implementieren `IStrategy` (`Evaluate(MarketContext) → SignalResult`). Die
Order-Pipeline (Scanner → Evaluate → RiskManager → Order) ist strategie-agnostisch; die aktive
Strategie wählt `BotSettings.LastStrategyName`. Empirisch verglichen über das Backtest-Lab
(`tools/BingXBacktestLab`) auf echten BingX-Klines.

**`IStrategy.RequiresHigherTimeframeContext`** (Default true): Steuert ob der Scan W1/D1-Fahrplan-
Kerzen pro Symbol lädt. SK = true (nutzt Fahrplan), TrendFollow = false (reiner H4-Navigator → spart
2 Klines-Calls/Symbol). D1 für BTC wird unabhängig geladen (BTC-Health).

**Break-Even-Gate** (`TradingServiceBase` PriceTickerLoop): Der BE-Block (A-Bruch ODER 2x-SL-Distanz,
`BreakevenCalculator`) ist über `signal.DisableSmartBreakeven` aktiviert — historisch invertiert
benannt (der frühere ATR-Smart-BE wurde im Buch-Strip entfernt; `true` = "nutze A-Bruch/2x-SL-BE").
**Jede neue Strategie, die Break-Even will, MUSS `DisableSmartBreakeven: true` setzen** (SK + TrendFollow
tun das; mit `NavPointA=0` greift der Distanz-Trigger). Der Distanz-Trigger ist konfigurierbar via
`RiskSettings.BreakevenTriggerRMultiple` (Default 2.0 = BE bei 2R; 0 = nur A-Bruch). Empirisch (Backtest-Lab,
TrendFollow-Fast, 3 Marktphasen) ist 2R optimal — aggressiveres BE schneidet Gewinner zu frueh ab.

| Strategie | Typ | H4-PF (3 Marktphasen, gefixt) | Status |
|-----------|-----|-------------------------------|--------|
| **TrendFollow-Fast** | Donchian(10)-Breakout in Trend-Richtung, EMA(34)+ADX/DMI, Market-Entry, ATR-SL, RRR 1.5/3.0 | **5.4 / 2.0 / 2.8** (alle 3 profitabel) | **Live-Default, H4-only** |
| TrendFollow (Standard, Don 20/EMA 50) | wie Fast, traegere Parameter | 5.3 / **0.95** / 1.7 (Mittelphase verlierend) | optional |
| TrendFollow-Wide / -Strong | TrendFollow-Varianten | gemischt (Strong Mittelphase 0.78) | optional |
| SkTrend | SK-Sequenz-Trigger + Trend-Filter + Market-Entry + ATR-SL ("reparierte SK") | OOS verlierend (PF 0.78) | nicht empfohlen |
| SK-System | Reversal nach Stefan Kassing (Limit-Entry in Korrektur-Zone) | H4 backtest-ok, aber Limit-Entry → **live unrentabel** (12 % WR live) | optional |

**Warum TrendFollow-Fast (H4-only) Live-Default ist:** Das SK-Reversal-System lieferte live nur ~12 % WinRate /
PF 0.11 (enge SL ~0.3 %, RRR 1.2, kaputter Short-Pfad, Loss-Streak-Pause → Dauerstillstand). TrendFollow handelt
**mit** dem Markt, weite ATR-SL, hohes RRR, **Market-Entry** (backtest-treu — minimaler Backtest-Live-Gap, anders
als SKs Limit-Entry mit 48 %-Backtest-vs-12 %-Live-Gap). Empirisch (gefixtes Lab, echte Klines, 3 disjunkte
Marktphasen): **H1 ist konsistent unprofitabel** (PF 0.4–0.99 ueber alle Strategien) → nur H4. **TrendFollow-Fast**
ist die einzige Variante, die auf H4 in allen 3 Phasen profitabel ist. Realistischer Live-PF ~2 (NICHT die
ungefixten 2.5+). Longs liefen 2023–2026 schwaecher (28–45 % WR) als Shorts — markphasen-abhaengig, offener
Verbesserungs-Hebel. Umgeht den TradFi-Pip-Bug (ATR statt fixe Pips). Details + Lab → `tools/BingXBacktestLab/CLAUDE.md`.

`RuntimeState` (TradesToday, ConsecutiveLosses) wird mit dem Strategie-Namen getaggt:
`LiveTradingManager` setzt die Loss-Streak bei Strategiewechsel zurueck. **Zusaetzlich** (Audit-Fix 31.05.):
der UTC-Tageswechsel ruft `RiskManager.SetConsecutiveLosses(0)` — ohne das blieb der RiskManager-Counter
bei ≥ PauseAtCount stehen → Scaling 0 → kein Trade → selbsterhaltende Dauerpause (die SK-Falle).

## SK-System (Sequenz-Konzept Strategie)

Implementierung von Stefan Kassings Sequenz-Konzept (SK-Buch + Algorithmische Strukturpunkte
+ Technische Spezifikation, alle drei DOCX in `src/Apps/BingXBot/`). Multi-TF Standalone
seit Frühjahr 2026. **Hinweis:** Seit 31.05.2026 nicht mehr Live-Default (siehe Strategien-Tabelle oben) —
bleibt als optionale, vollständig implementierte Strategie erhalten.

### Sequenz-Phasen (`SmState`)

```
Suche0 (Origin)
   ↓ Pivot erkannt + ATR-Impuls + BOS
SucheA (Impuls-Ende lokalisieren, Trailing High/Low)
   ↓ Korrektur startet (50-78.6% Retracement)
SucheB (Korrektur-Ende lokalisieren, Box-Logik)
   ↓ Trigger-Kerze (Wick-Rejection / Pinbar / Engulfing)
Aktiviert (Position kann eröffnet werden, BC-Zone primär @ 50% / 66.7%)
   ↓ Preis erreicht Extension200 (TP2)
Abgearbeitet (Sequenz fertig, Reset auf Suche0 oder Bias-Flip)
```

### Confluence-Scoring (`SkConfluenceScorer`)

`MaxScore` wird dynamisch aus `Enum.GetValues<ConfluenceCategory>()` berechnet (keine Magic).
GklMasterZone und HighProbabilityZone zählen +2, alle anderen +1.

| Kategorie | Gewicht |
|-----------|---------|
| `PriceAction`, `FibonacciGoldenPocket`, `FahrplanAlignment`, `HigherTfSequence`, `VolumeSpike`, `BcklReEntry`, `FavorableFundingRate` | 1 |
| `GklMasterZone`, `HighProbabilityZone` | 2 |

### Aktive Buch-Hardfilter (Default-Stand)

| Setting | Default | Buch-Stand | Bemerkung |
|---------|---------|------------|-----------|
| `ImpulseAtrMultiplier` | `2.0` | 3.0 | Doku-Spanne erlaubt 2.0; mehr Sequenzen in liquiden Perps |
| `RequireBosOnActivation` | `true` | true | BOS bleibt Pflicht (Strukturpunkte §3) |
| `RequireBosCloseBreak` | `false` | true (§3) | Docht-Bruch reicht; Buch-strikter Body-Close per UI aktivierbar |
| `AdaptiveSwingStrength` / `PivotLeftBars=5` / `PivotRightBars=3` | `true/5/3` | §5B | Asymmetrische Pivots aktiv |
| `RequireWickRejectionInBZone` | `false` | true (§5C) | Buch §7 lässt drei gleichwertige Reversal-Wege zu |
| `BlockLtfEntryWhenHtfInTargetZone` | `false` | true (Spec §7) | MTA als opt-in; Counter-Setups in HTF-Targets bleiben handelbar |
| `EnableConfluenceOverlapDetection` | `true` | true | "Heiliger Gral" Bonus-Confluence |
| News-Filter (`HttpEconomicCalendarService`) | opt-in | empfohlen | Via `News:Endpoint`-Config aktivieren |

### Bewusste User-Abweichungen vom Buch

| Regel | Buch | Projekt |
|-------|------|---------|
| Risiko pro Trade | 1-3 % | **5 %** (`MaxRiskPercentPerTrade`, RiskManager-Cap) |
| Margin-Anteil je Trade | 1-3 % | **10 %** (`MaxPositionSizePercent`) |
| Loss-Streak-Halve | 3 Losses | **4 Losses** (`LossStreakHalveAtCount`) |
| Loss-Streak-Pause | 5 Losses | **7 Losses** (`LossStreakPauseAtCount`) |
| Margin-Cap (Σ aller offenen Margins) | — | **80 %** (`MaxTotalMarginPercent`, 0 = aus) |
| Conservative-Mode darf in SucheB einsteigen | nicht spezifiziert | aktiv bei LTF-Reversal |
| Counter-Trend-Scalper | "manche Trader" (hochriskant) | Default `false`, opt-in |
| Runner-TP (5-10 % über 200 %) | "manche Trader" | Default `false`, opt-in |
| TP-Toleranz Krypto | 5 Pips (~0.005 %) | 0.03 % |
| `EntryMode.Both` (Aggressive-Limit + LTF-Bonus) | Strikt entweder/oder | Mischmodus |
| `MaxDailyDrawdownPercent`/`MaxDailyLossPercent`/`MaxDailyRiskPercent` | Nicht im Buch | User-Safety-Net, beibehalten |
| Cross-TF-Pyramiding (`EnableCrossTfPyramiding`) | Nicht im Buch | Default false, opt-in |
| Per-Kategorie-Slippage (`MaxSlippagePercentByCategory`) | Nicht im Buch | Crypto 0.10 % / Forex 0.05 % / Stock 0.30 % / Index+Commodity 0.20 % |

### Plan-Dokumente

- `src/Apps/BingXBot/SK_BUCH_COMPLIANCE_PLAN.md` — 25 Masterclass-Punkte mit Tests
- `src/Apps/BingXBot/MULTI_TF_STANDALONE_PLAN.md` — Multi-TF-Architektur
- `src/Apps/BingXBot/PLAN_SERVER_MODE.md` — Client/Server-Architektur

---

## RiskManager (Risikomanagement)

### Aktive Schutz-Mechanismen

- **Position-Sizing**: Risiko-basiert — `maxLoss / slDistance` (enger SL = größere Position).
  SL ist **Pflicht**, ohne SL wird Trade abgelehnt.
- **`MaxRiskPercentPerTrade`**: Default **5%** (User-Entscheidung, siehe SK-Plan).
- **Drawdown-Limits**: Täglich + gesamt. Peak-Equity-Tracking für Total-Drawdown.
- **Liquidation-Check**: Isolated-Margin-Formel `(1 - MMR) / Leverage`. Bei ≤ 2x Leverage
  deaktiviert (kein Liquidations-Risiko).
- **Margin-Cap**: Σ aller offenen Margins + neue Margin ≤ `MaxTotalMarginPercent`
  (Default 80 %) der Wallet-Balance — TradFi-Schutz bei Hebel 20×/10×. 0 = Filter aus.
- **Daily-Loss-Circuit**: Nach Überschreitung keine neuen Entries bis UTC-00:00.
- **Daily-Risk-Budget**: Realisierte + offene + geplante Risiken ≤ `MaxDailyRiskPercent`
  (SK-Buch S.13: 1-3%).
- **StopLossSanityGuard** (`BingXBot.Core/Services/`): Pure-Function-Validator vor jedem
  SL-Push (BE-Trigger, Runner-Trail, Partial-Close-SL/TP, Recovery-BE,
  `BingxNativeSlTpManager.UpdateNativeStopLossAsync`). Reject = WARNING + Push verweigert.
  Schuetzt vor "Long-SL ueber Entry" (DOGE-Bug aus dem Snapshot vom 2026-05-17).
  Buffer `MaxBreakevenBufferPercent=0.5 %` ueber/unter Entry erlaubt — Werte darueber nur
  mit `RunnerActive=true`.

### Adaptive Position-Scaling (`GetPositionScalingFactor`)

- **Loss-Streak-Dampening** (Default on, Schwellen einstellbar):
  ≥ `LossStreakHalveAtCount` (Default 4) Verluste → 0.5×, ≥ `LossStreakPauseAtCount`
  (Default 7) → 0 (Pause). Buch S.13 nennt 3/5; User-Default 4/7 lockert bewusst.
- **Equity-Curve-Scaling** (opt-in): Drawdown vom Peak ab `EquityCurveScalingThresholdPercent`
  → linear runter bis 0.5×.
- **Position-Retention-Cap**: Wenn das `MaxRiskPercentPerTrade`-Cap die Position unter
  `MinPositionSizeRetentionPercent` (Default 10 %) der Original-Größe drückt, wird der
  Trade verworfen statt mit Mini-Position einzusteigen.
- Alle Faktoren multiplizieren sich.

### Korrelations-Filter (`AssetClusterClassifier`)

Opt-in via `MaxCorrelatedExposurePercent` (Default 0 = aus). Schützt vor "3× BTC durch
parallele BTC/ETH/SOL"-Disasters bei Flash-Crashes. Cluster:

| Cluster | Mitglieder |
|---------|-----------|
| `CryptoBtcMajor` | BTC, WBTC, BTCB, BTCDOM |
| `CryptoEthMajor` | ETH, WETH, STETH, RETH, CBETH, WSTETH |
| `CryptoAltL1` | SOL, AVAX, ADA, DOT, NEAR, ATOM, ALGO, FTM, TRX, TON, APT, SUI, INJ, SEI, TIA |
| `CryptoAltDefi` | UNI, AAVE, LINK, MKR, SNX, COMP, CRV, LDO, GMX, DYDX, 1INCH, BAL, SUSHI |
| `CryptoMeme` | DOGE, SHIB, PEPE, FLOKI, WIF, BONK, MEME, BABYDOGE, POPCAT, TURBO, BOME |
| `CryptoStablePair` | USDC, DAI, BUSD, TUSD, FDUSD, USDP |
| `CryptoOther` | Rest (no-op im Filter) |
| `TradFiForex/Index/Commodity/Stock` | Per `SymbolClassifier.Classify` |

### Volatility-Targeting (opt-in)

Wenn `EnableVolatilityTargeting = true`: `qty *= min(VolatilityScaleCap, VolatilityTargetPercent / atrPct)`.
Stabile Coins (BTC, ATR ~1%) bekommen mehr Größe, Memecoins (ATR ~8%) weniger.

### Tick-Size-Awareness (`SymbolInfoCache.RoundPriceConservative`)

Long: floort den Preis. Short: ceilt. Stellt sicher dass Tick-Rounding den geplanten
SL-Buffer NIE auffrisst (Memecoins mit 5-Pip-Buffer würden sonst auf 2 Pips
zusammengedampft).

### Time-of-Day Session-Filter (Crypto, opt-in)

`BotSettings.EnabledSessions` als `TradingSessions`-Bitmask:

| Session | UTC-Zeitraum |
|---------|--------------|
| `Asia` | 00:00–08:00 + 22:00–24:00 |
| `Eu` | 08:00–13:00 |
| `EuUsOverlap` | 13:00–16:00 |
| `Us` | 16:00–22:00 |

Default `All` (= no-op). TradFi-Symbole haben separate Markt-Stunden in `TradingHoursFilter`.

### Rolling Live-Metriken (30-Trade-Window, `Queue<T>`)

`RollingWinRate`, `RollingProfitFactor`, `RollingSharpeRatio` (annualisiert mit
`sqrt(TradesProJahr)`, Sample-Varianz N-1). `CheckStrategyHealth` warnt bei Sharpe < 0.3
oder WinRate < 25% oder ≥ 5 Verlusten in Folge.

---

## DB-Persistenz (`BotDatabaseService`)

SQLite WAL-Modus für Multi-Mode-Concurrency. Schema-Versioning via `RunMigrationsAsync()`.

### Persistierte Tabellen

| Tabelle | Inhalt |
|---------|--------|
| `Trades` | Live + Paper-Trades (Backtest **nicht** — flutet sonst) |
| `Equity` | Equity-Curve-Punkte (EquityEntity) |
| `Logs` | Log-Einträge (LogEntity) |
| `Settings` | JSON-blob pro Settings-Block (Risk/Scanner/Bot/Backtest) + `AutoResumeFlag` + `LastHeartbeatUtc` als separate Keys |
| `BacktestJobs` | Backtest-Job-Metadaten (BacktestJobEntity) |
| `EvaluationDecisions` | Decision-Trail (Migration v11), 50000-Eintrag-Trim mit Index auf Timestamp/Symbol/Reason |
| `SettingsChanges` | Audit-Trail für Settings-Diffs (Migration v12) |
| `RuntimeState` | TradesToday, ConsecutiveLosses, ExitStates, PendingLimitOrders (JSON-Blobs für Crash-Recovery) |

### Schema-Migration

`RunMigrationsAsync` läuft bei Start. Aktuelle Versionen-Marker in CLAUDE.md NICHT
dokumentiert (sind Code, nicht Doku). Schema-Änderungen sind additiv (kein `ALTER TABLE`,
JSON-Blob-Toleranz).

### Backup + Archiv

- `DbBackupService`: Daily 03:00 UTC, `PRAGMA wal_checkpoint(FULL)` + `File.Copy` →
  `{DataDir}/backups/bot-YYYY-MM-DD.db`. 7-Tage-Retention.
- `DbArchiveService`: Monthly 1. 04:00 UTC, Trades > 90d in `bot-archive-{YYYY-MM}.db`.
  Decisions > 30d und SettingsChanges > 90d werden gepurged.
- `RunIntegrityCheckAsync`: PRAGMA integrity_check beim Bootstrap. Bei `!ok` startet der
  Server NICHT (`InvalidOperationException` → systemd-Restart-Loop stoppt mit klarem Fehler).

### Recovery-Flow nach Crash/Restart

1. `BotDatabaseService.LoadLastHeartbeatAsync` liest letzten Heartbeat.
2. `BotAutoResumeService` prüft `WasRunningOnShutdown`-Flag. Wenn true:
3. Bei Heartbeat-Drift > 5 min und Live-Mode: `GetIncomeHistoryAsync` für Offline-Zeitfenster,
   REALIZED_PNL-Records → synthetische `CompletedTrade`-Backfills via `SaveTradeAsync` +
   `RiskManager.UpdateDailyStats` für heutige Trades.
4. `LiveTradingManager` ruft beim Live-Start `RiskManager.RestoreStats(dailyPnl, totalPnl, peakEquity)`
   aus den persistierten Trades + Equity-Snapshots auf — ohne diese Rehydration starten Daily-Loss-Circuit
   und Total-Drawdown-Schutz nach jedem Restart amnesisch (bei 0).
5. `IBotControlService.StartAsync` reaktiviert Engine im zuletzt aktiven Mode.

### Live-Trade-Persistenz (Pflicht-Pfad)

`TradingServiceBase.ProcessCompletedTrade` ruft Fire-and-Forget
`_dbService.SaveTradeAsync(trade)` auf — **alle** TP1/TP2/SL/Manual-Close-Pfade laufen
darüber. Ohne diesen Hook (vor Mai 2026) sah die DB **keine** Live-Trades; SignalR-Push
hielt sie nur im Client-RAM. Bei Fehler wird `LogLevel.Error` mit Trade-Kontext geloggt.

`PostTradePersistHook` (optional) wird nach erfolgreichem `SaveTradeAsync` aufgerufen —
verwendet für `EquitySnapshotService` (separater Equity-Punkt nach Close).

### Server-HostedServices fuer Persistenz (Remote-Mode)

| Service | Was |
|---------|-----|
| `EquitySnapshotService` | Schreibt Equity-Snapshot alle 5 min (`Server:EquitySnapshotIntervalMinutes`) + sofort nach jedem Trade-Close (2 s Debounce). Publiziert `BotEventBus.EquityUpdate` für SignalR-Clients. Vorher schrieb nur `DashboardViewModel`-Timer (lief im Remote-Mode nicht). |
| `DbLogPersistenceService` | Subscribed `BotEventBus.LogEmitted`, schreibt im 250-ms-Batch in `LogEntries`. Settings-gated via `BotSettings.EnableDbLogPersistence` (Default true) + `DbLogPersistenceMinLevel` (Default Info). Queue-Hard-Cap 10.000 schützt vor DB-Slowness. |
| `BotAutoResumeService` (erweitert) | Public `BackfillFromBingxAsync(fromUtc, toUtc?)`-Methode für `POST /api/v1/admin/backfill-trades` — Admin-Endpoint zum Nachholen verlorener Trades aus BingX-Income-History. Dedup-aware (Reason="Backfilled (Pi offline)"). |

### ExitStates-Stale-Cleanup (`ReconcileLoopAsync`)

`LiveTradingService.CleanupStaleExitStatesAsync` läuft pro Reconcile-Tick und entfernt
ExitStates, die weder einer offenen BingX-Position noch einem Pending-Limit noch einer
Reduce-Only-Order (Tp1/Tp2-OrderId in OpenOrders) zugeordnet sind. Grace-Window:
Recovery 1 h, normal 5 min. Bei fehlenden OpenOrders (Rate-Limit) konservativ 24 h.
`PersistExitStatesAsync` schreibt jetzt auch leere Snapshots — sonst hielt Restart die
gerade entfernten States in der DB fest.

### Pending-Limit-Cleanup (verschaerft)

`CancelExpiredPendingLimitOrdersAsync(openOrders?)` entfernt Pending-Limits nicht nur
nach `PendingLimitOrderMaxAgeHours` (Default 6 h), sondern **sofort** wenn die OrderId
nicht mehr in den BingX-Open-Orders erscheint. Zusätzlich wird der zugehörige
Recovery-ExitState (`IsRecovered=true, OriginalQuantity=0`) im selben Schritt entfernt.

---

## Telemetry & Monitoring

### `BotTelemetry` (`BingXBot.Trading/Telemetry/`)

Zentrale `ActivitySource "BingXBot.Trading"` + `Meter` für OpenTelemetry-Integration.

| Counter | Tags | Zweck |
|---------|------|-------|
| `bingxbot.strategy.evaluations` | symbol, tf | Strategy-Evaluate-Aufrufe |
| `bingxbot.trades.opened` | — | Geöffnete Trades |
| `bingxbot.trades.closed` | reason | Geschlossene Trades |
| `bingxbot.risk.rejects` | reason | RiskManager-Ablehnungen |
| `bingxbot.decisions.logged` | — | Decision-Trail-Eintraege |
| `bingxbot.orders.retries` | ex (Exception-Type) | Order-Retry-Versuche |
| `bingxbot.news.probe_failures` | — | News-Service-Probe-Fehler |

`BotTelemetry.StartActivity(name)` startet Activity nur wenn ein OTel-Listener aktiv ist
(no-op overhead in Standalone-/Test-Setups).

### Endpoints

| Endpoint | Format | Zweck |
|----------|--------|-------|
| `/api/v1/health` | JSON | Liveness-Check für systemd + Tailscale-Probe |
| `/api/v1/metrics/internal` | JSON-Snapshot | Bot-State, RiskManager-Metriken, FCM-Devices, Token-Lifetime — für Grafana JSON-API-Plugin |
| `/metrics` | Prometheus-Text | Counter-Werte für Prometheus-Scrape |

Beide Metrics-Endpoints sind in `PublicPaths` (kein Auth nötig — Pi steht hinter Tailscale).

### Decision-Trail (`DecisionTrailBuffer`)

5000-Einträge-Ringpuffer im RAM, parallel persistiert in `EvaluationDecisions`-Tabelle.
Pro `Strategy.Evaluate` ein Record (Reject mit Code aus `RejectionReasons` oder Success).
UI-View `DecisionTrailView` mit Filter nach Symbol/TF/Reject-Reason/OnlyRejected.

`RejectionReasons`-Konstanten:

| Code | Auslöser |
|------|----------|
| `news_blackout` | News-Blackout-Fenster aktiv |
| `state_not_activated` | SK-State noch nicht `Aktiviert` |
| `impulse_below_atr` | Impuls-Distanz unter ATR × Multiplier |
| `no_htf_confluence` | HTF-Hard-Gate: weder GKL noch Zone-Overlap |
| `score_below_min` | Confluence-Score unter `MinConfluenceScore` |
| `rrr_too_small` | RRR < 1.0 |
| `box_close_violated` | Wick-Rejection in B-Zone fehlt (`RequireBoxCloseOnEntry`) |
| `missing_wick_rejection` | Pinbar/Engulfing fehlt |
| `mta_target_zone_block` | HTF in Zielzone, LTF-Block |
| `entries_already_triggered` | Beide Entry-Levels bereits getriggert |
| `missing_strukturpunkte` | Point0/PointA fehlt |
| `counter_trend_inactive` | Counter-Trend-Scalp deaktiviert |
| `slippage_too_high` | Order-Book-Slippage > Threshold |
| `tf_auto_disabled` | TF wegen schlechter WinRate auto-disabled |
| `correlation_limit_exceeded` | Cluster-Margin > Limit |
| `outside_allowed_session` | Trade ausserhalb erlaubter Session |
| `news_service_unavailable` | News-Service degradiert + RequireNewsFilter aktiv |
| `insufficient_data` | Nicht genug Candles im Navigator-TF |
| `no_sequence` | State &lt; SucheB — typisch in Seitwärts-Phasen |
| `sequence_not_constructable` | ToSequence liefert null (defensiv) |
| `bos_volume_below_threshold` | BOS-Kerze unter Volumen-SMA (`RequireBosVolumeBreakout`) |
| `sl_geometry_error` | SL- oder TP-Geometrie falsch |
| `sl_distance_zero` | SL-Distanz = 0 (Position-Sizing-Schutz) |
| `other` | Generischer Reject — sollte nach A1.2 kaum noch auftreten |

### Decision-Trail-Hygiene (Snapshot-Report A1.x)

`TradingServiceBase` filtert vor `PublishEvaluationDecision`:
- `BotSettings.DecisionTrailIncludeNotActivated` (Default false) blockt
  `state_not_activated`-Eintraege (vorher 81 % Rauschen).
- `BotSettings.DecisionTrailDeduplicateTriggers` (Default true) loggt Triggered-Decisions
  fuer dieselbe Sequenz nur einmal pro Bot-Laufzeit (verhindert ZEC-60×-Cluster).
  Dedup-Key = `Symbol|TF|SequenceState|Point0|PointA|PointB`. Set wird bei Stop/Start
  geleert.

### Trade-Stats (`TradeStatsAggregator`)

Aggregiert `BotEventBus.TradeCompleted`-Events nach `(NavigatorTimeframe × MarketCategory × TradingMode)`:
WinRate, AvgPnl, TotalPnl, TotalFees, AvgHoldingTime, MaxDrawdown. Beim Server-Bootstrap
mit den letzten 10000 Trades aus DB rebuildet. Endpoint `/api/v1/stats/breakdown`.

---

## UI-Architektur (`BingXBot.Shared`)

### Views (`BingXBot.Shared/Views/`)

| View | Zweck |
|------|-------|
| `DashboardView` | Balance, Positionen, Bot-Controls, Strategie, Equity-Chart, SK-Ampel pro TF, News-Service-Banner |
| `ScannerView` | Live-Scan mit Volumen/Momentum-Filter + Scanner-Settings (Multi-TF, Filter, Slippage, Adaptive-TF) |
| `StrategyView` | Parameter-Editor + TF-Visualisierung |
| `BacktestView` | Historischer Test mit `PerformanceReport` + Walk-Forward + Trade-Replay |
| `TradeHistoryView` | Alle Trades filterbar (Modus/Symbol/Zeitraum/TF-Badge) |
| `RiskSettingsView` | Risiko-Parameter (Risk/Margin/DD/Korrelation/Vol-Targeting/Pyramiding) |
| `LogView` | Live-Log mit Level/Kategorie-Filter |
| `SettingsView` | API-Keys, Server-Verbindung, Pairing, Theme, Push-Notifications, Decision-Trail |
| `DecisionTrailView` | Decision-Trail mit Filter |
| `SettingsHistoryView` | Settings-Audit-Trail |

Mobile-Variante via `ViewLocator`-Konvention: `DashboardView` → `DashboardViewMobile`,
ausgewählt zur Laufzeit über `App.IsMobileShell`.

### SkiaSharp-Renderer

| Renderer | Zweck |
|----------|-------|
| `EquityChartRenderer` | Linien-Chart Equity-Kurve |
| `DrawdownChartRenderer` | Drawdown-Visualisierung (Underwater-Chart) |
| `InteractiveChartRenderer` | SK-Sequenz-Overlay (Punkt 0/A/B + Fibonacci-Levels + Trade-Marker) |
| `PnlCalendarRenderer` | Tages-PnL-Heatmap |
| `FearGreedGaugeRenderer` | Gauge-Anzeige für Fear & Greed / Markt-Sentiment |
| `CorrelationMatrixRenderer` | Korrelations-Matrix für Cluster-Visualisierung |

### `BotEventBus` (Singleton, ViewModel-zu-ViewModel)

| Event | Subscriber |
|-------|-----------|
| `TradeCompleted` | TradeHistoryVM |
| `TradeOpened` | LocalBotEventStream → SignalR |
| `BacktestCompleted` | TradeHistoryVM |
| `NotificationRequested` | Desktop-Notification-Service |
| `LogEmitted` | LogVM, ActivityFeedVM |
| `BotStateChanged` | MainVM |
| `MarginWarning` | DashboardVM |
| `TradingModeChanged` | MainVM (Statusleiste Paper/Live) |
| `SkAmpelUpdated` | DashboardVM, StrategyVM |
| `EvaluationDecided` | DecisionTrailBuffer + LocalBotEventStream |
| `NewsServiceHealthChanged` | LocalBotEventStream → SignalR → UI-Banner |
| `PositionUpdated`, `EquityUpdate`, `TickerUpdate`, `BtcPriceUpdate`, `ScannerSweep` | LocalBotEventStream → SignalR |

### ViewModel-DI-Pattern

`MainViewModel` als DI-Singleton, hält Child-VMs als Properties (Lazy<T> für spät-genutzte
VMs wie DecisionTrail/SettingsHistory zur Bootstrap-Beschleunigung).

Alle Child-VMs bekommen `IBotEventStream` (Local oder Remote) und ggf. weitere Service-
Interfaces per Constructor-Injection. Optionale Parameter mit `?` für Demo-Modus ohne
Exchange-Verbindung.

### UI-Conventions

- Compiled Bindings (`x:CompileBindings="True"` + `x:DataType`) auf JEDER View-Root
- Virtualisierung (`VirtualizingStackPanel`) in TradeHistory, Log, Backtest, Scanner
- Monospace-Zahlen (Consolas) für Preise/PnL/Metriken
- Dark-Mode Default (`ThemeVariant.Dark`), via `BotSettings.ThemePreference` umstellbar
- Farbpalette: Primary `#3B82F6`, Background `#1E1E2E`, Profit `#10B981`, Loss `#EF4444`
- Keyboard-Shortcuts: Ctrl+1–8 Navigation, F5/F6/F7/F12 Bot-Kontrolle, Escape → Dashboard

---

## MVVM-Regeln (Strict, sonst Android-Crash)

- `x:CompileBindings="True"` + `x:DataType` auf jeder View-Root
- **KEIN** `App.Services.GetRequiredService<T>()` im View-Ctor — Android-Crash-Pattern
- **KEIN** `DataContext = ...` im Code-Behind — `ViewLocator` setzt das
- Services per Constructor-Injection ins ViewModel, **nicht** in die View
- Commands per `[RelayCommand]`, keine Click-Handler im Code-Behind
- Sub-VMs als DI-Properties im MainViewModel
- `CurrentPageViewModel` + einzelnes `<ContentControl Content="{Binding CurrentPageViewModel}"/>`
  — keine 8 gestapelten Border (Mobile-Shell-Crash)
- Komplexe Views mit VM-Events: `DataContextChanged`-Pattern (sauber ab/anmelden)

Bei Verdacht: Agent `mvvm-auditor` oder Skill `mvvm-check`.

---

## Settings-Architektur

### Settings-Klassen

- `RiskSettings` — Position-Sizing, DD-Limits, Korrelation, Vol-Targeting, Cluster, Equity-Scaling, Cross-TF-Pyramiding, BC-Zone-Entry-Strategie, Entry-Mode, SL-Buffer, TP-Ratios, Runner
- `ScannerSettings` — ActiveTimeframes, Volume/PriceChange-Filter pro TF, BOS-Anker-Bars, Pivot-Bars, Confluence-Settings, Adaptive-TF-Disable, Slippage-Guard, Funding-Bonus + Multiplier, Magic-Number-Properties (NavigatorSwingStrength, BcklReEntryCooldownCandles)
- `BotSettings` — UseRemoteMode, ServerUrl, LastMode, WasRunningOnShutdown, Theme, PaperInitialBalance, Decision-Trail-Toggle, Trade-Push-Toggle, EnabledSessions

### Settings-Sync (Multi-Client)

- `LocalSettingsService` persistiert in DB + feuert `SettingsChanged`-Event.
- `LocalBotEventStream` forwarded an `IBotEventStream.SettingsChanged`.
- `BotHubEventForwarder` pusht via SignalR an alle Clients.
- `RemoteSettingsService.RaiseChanged` ruft alle abonnierten ViewModels die ihren `LoadFromSettings`-Pfad neu durchlaufen.
- Audit-Trail: `LocalSettingsService` baut Diff vor jedem Save → `SettingsChanges`-Tabelle.

### Settings-Persistenz-Race-Schutz

- `SemaphoreSlim _persistLock` um alle 5 `SaveXxxAsync`-Methoden (verhindert parallele
  Save-Race mit `JsonSerializer`-Mutation auf `_botSettings`-Collections).
- Server-Authority-Properties: `LastMode` und `WasRunningOnShutdown` werden in
  `LocalSettingsService.SaveBotAsync`/`SaveAllAsync` gegen Client-Overwrite geschützt
  (Client darf nur seine eigenen Felder überschreiben, Server-Felder bleiben).
- `BotSettings`-JSON-Deserialisierung: Korrupte Enum-Werte werden via
  `JsonStringEnumConverter(allowIntegerValues: true)` toleriert; bei JsonException wird
  die korrupte Row gelöscht + detailliert geloggt (statt stillschweigend Defaults).

---

## Terminologie & Conventions

### "TradFi" = BingX "Features"-Perps mit NC-Prefix

TradFi im Bot bezeichnet **NICHT** den nativen BingX-TradFi-Tab (CFDs mit Börsenzeiten),
sondern USDT-margined Perps auf traditionelle Underlyings mit `NC`-Prefix (New Contract).

| Prefix | Kategorie |
|--------|-----------|
| `NCCO*` | Commodity (GOLD, XAG, WTI, COPPER) |
| `NCSI*` | Index (SP500, NASDAQ100, DAX40, DOWJONES) |
| `NCFX*` | Forex (EURUSD, GBPUSD, USDJPY) |
| `NCSK*` | Stock (AAPL, TSLA, NVDA, MSFT, META) |
| sonst | Crypto |

`SymbolClassifier.Classify(symbol)` ist case-insensitive (BingX liefert mixed-case wie
"Ncco1Oilwti2USD-USDT").

### Scan-Aufteilung (`ScanHelper.FilterCandidates`)

Bei `MaxResults = 100`: 60 Krypto + 40 TradFi (10 Commodity + 10 Index + 10 Forex + 10 Stock).
Slot-Recycling: ungenutzte Sub-Slots → Top-Volume-TradFi anderer Subkategorien;
ungenutzte TradFi-Slots → Krypto.

### Per-Markt Risk-Defaults

| Kategorie | Default-Leverage | Max-Leverage | MaxPositionSize / MaxMarginPerTrade |
|-----------|------------------|--------------|--------------------------------------|
| Krypto | 3× | 125× (BingX), 5× (User-Default) | 3% / 1% |
| Commodity | 10× | 500× | 3% / 1% |
| Index | 10× | 500× | 3% / 1% |
| Forex | 20× | 500× | 3% / 1% |
| Stock | 3× | 25× | 3% / 1% |

MinRRR per Kategorie: 1.0 (SK-Buch S.13).

### Handelszeiten (`TradingHoursFilter`)

- Krypto: 24/7
- Forex: 24/5 (Sydney-Open ab So 22:00 UTC, Schluss Fr 22:00 UTC)
- Commodity/Index/Stock: Mo-Fr (sonst geschlossen)
- 7×24-Varianten (`Symbol.Contains("724")`): immer offen
- Funding-Settlement ±5min Pause für ALLE Perps (Krypto + TradFi)

### DateTime-Pattern

- Persistenz: IMMER `DateTime.UtcNow` (NIE `DateTime.Now`)
- Format: ISO 8601 `"O"` → `dt.ToString("O", CultureInfo.InvariantCulture)`
- Parse: IMMER `DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)`
- BingX `incomeType`: `REALIZED_PNL`, `FUNDING_FEE`, `TRADING_FEE`, `INSURANCE_CLEAR`, `ADL`, `TRANSFER`
- BingX-API: `startTime.Value.ToUniversalTime()` für `DateTimeOffset` (sonst lokale Timezone)

### Pip-Werte (`PipStopLossCalculator.GetPipValue`)

| Kategorie | Pip-Berechnung |
|-----------|---------------|
| Crypto | `entryPrice * 0.0001` (prozentual) |
| Forex | `entryPrice * 0.0001` (prozentual — NCFX-Perps skalieren anders als Spot-FX, fixer 0.0001 gab 8% WinRate) |
| Stock | `entryPrice * 0.00005` (prozentual — BRK @ 600 USD sonst nur 0.067% SL) |
| Index | `entryPrice * 0.0001` |
| Commodity | `entryPrice * 0.0001` |

### Naming Conventions

| Element | Convention | Beispiel |
|---------|-----------|----------|
| ViewModel | Suffix `ViewModel` | `DashboardViewModel` |
| View | Suffix `View` (oder `ViewMobile`) | `DashboardView`, `DashboardViewMobile` |
| Service Interface | `I{Name}Service` | `IBotControlService` |
| Service Implementation | `Local{Name}Service` / `Remote{Name}Service` / `{Name}Service` | `LocalBotControlService` |
| Events | PascalCase EventHandler oder Action-Delegate | `TradeCompleted`, `NavigationRequested` |

---

## Bekannte Gotchas (Patterns die regelmaessig brauchen)

### BingX-API

- **Balance v3-Endpoint**: `/openApi/swap/v3/user/balance` — Array-Response, nach `asset == "USDT"` filtern
- **`SetMarginTypeAsync` VOR jeder Order** — BingX-Default kann Cross sein, try-catch (Fehler bei offener Position ignorieren)
- **Kill-Switch**: alle 60s refreshen (`ActivateKillSwitchAsync(120s)`), bei sauberem Stop explizit `DeactivateKillSwitchAsync()`
- **Server-Zeit-Sync**: `SyncServerTimeAsync()` bei Connect — Error 100421 bei > 5s Drift
- **`AmendOrderAsync`**: `RoundPrice`/`TruncateQuantity` anwenden (BingX lehnt zu viele Dezimalstellen ab)
- **Limit-Order TP**: NICHT sofort platzieren (Position existiert noch nicht). Fill-Detection im PriceTickerLoop, TP mit Qty aus `GetPositionsAsync` (BingX truncated auf Symbol-Precision)
- **WebSocket `SendAsync` nicht thread-safe** — `_sendLock` SemaphoreSlim für alle Sends
- **WebSocket User-Data-Reconnect mit ListenKey-Refresh**: `BingXWebSocketClient.ListenKeyRefresher`-Callback (Default `RestClient.CreateListenKeyAsync`) → bei Reconnect wird ein frischer Key geholt. Vorher: Reconnect nutzte den alten Key, der bei Server-Side-Disconnect oft schon abgelaufen war → bis 10 min User-Data-Stream tot
- **Position-Retry nach Market-Order**: 3 Versuche × 1s Delay bis `GetPositionsAsync` neue Position listet (Hedge-Mode-Rejection ohne Position)
- **TP-Retry + Verify**: `GetOpenOrdersAsync(symbol)` nach Platzierung prüft ob OrderIds tatsächlich existieren
- **Idempotency-Check vor Retry**: `TpOrderMatcher.FindMatchingTpOrder` prüft mit Toleranz, ob die TP-Limit bereits liegt — verhindert Doppel-Place nach `TaskCanceledException`
- **OrderTypes**: BingX gibt Bot-Limit-TPs (`Type=Limit + ReduceOnly=true`) NICHT als `TakeProfitMarket`/`TakeProfitLimit` zurück. Cancel-Filter in `BingxNativeSlTpManager` und Recovery-Logik berücksichtigen das
- **`SetPositionSlTpAsync` ohne Position-Qty**: Wenn `GetPositionsAsync` keine Position liefert (z.B. gerade geschlossen), wird ein `InvalidOperationException` geworfen statt `closePosition=true` mit `quantity=0` (BingX V2 ignoriert das unzuverlaessig → Position konnte sonst still ohne SL bleiben)

### Trading-Logik

- **`_tradesToday` MUSS `volatile`** — JIT darf nicht-volatile Felder bei parallelen Reads cachen
- **`ContinueWith` IMMER mit `TaskScheduler.Default`** — sonst UI-Thread-Deadlock möglich
- **`OriginalQuantity` IMMER die tatsächlich platzierte Menge** (nach Equity/Score-Scaling), NICHT `riskCheck.AdjustedPositionSize`
- **EmergencyStop**: CTS NICHT vor Close-Operations canceln (API-Calls brauchen HTTP)
- **Recovery-Signale** nur in EINEM Service registrieren (sonst N-facher Close-Versuch)
- **`DailyPnl` Dictionary**: atomarer Swap (neues Objekt), NICHT Clear+Re-Fill (SkiaSharp-Render-Thread liest)
- **`_klineSemaphore` in Dispose() freigeben** — SemaphoreSlim hat OS-Handles
- **Manueller Close**: `_liveManager.CommissionTakerRate` statt hardcodierter 0.0005m — echte PnL für History
- **Backtest-Trades NIEMALS in DB speichern** — flutet sonst bei jedem Run
- **SL ist PFLICHT**: `RiskManager.ValidateTrade` lehnt Trade ohne SL ab
- **Sync-over-Async im Hot-Path verboten** — News-Blackout per `MarketContext.ResolvedNewsBlackoutEvent` pre-resolved 1× pro Scan-Tick
- **Margin-Aware-Cap (TradFi)**: bei Hebel 20×/10× würde 5%-Risk-Trade fast gesamte verfügbare Margin binden — `RiskManager` cappt Σ(Margins) ≤ 60% × Wallet-Balance
- **Runner-Trail-SL MUSS an Exchange gepusht werden**: `PositionExitState.RunnerLastPushedSl` + `RunnerLastPushUtc` (0.15% Delta UND 10s seit letztem Push), sonst lebt SL nur im Memory und Crash verliert Runner-Gewinn
- **ExitState-Persist nach kritischen Mutationen**: `PersistExitStatesAsync`-Hook in `TradingServiceBase` (no-op) + Override in `LiveTradingService` (SQLite-WAL-Write). Aufruf-Punkte: TP1/TP2-LimitOrderId set/null (Stage-1+Stage-2+WS-Fill), Phase Initial→Tp1Hit, RunnerActive=true, BreakevenSet=true. Vorher: Persist nur in `LiveTradingManager.StopAsync`/`EmergencyStopAsync` — Hot-Crash zwischen TP-Place und Stop verlor die OrderId-Zuordnung
- **Limit-Distance-Guard analog SlippageGuard**: `ScannerSettings.LimitDistanceGuardEnabled` + `MaxLimitOrderDistanceByCategory` (Crypto 5 %, Forex 1.5 %, Stock 3 %, Index 2 %, Commodity 2.5 %). Limit-Preis weiter als Schwelle vom aktuellen Markt → Order geblockt (Schutz vor stale Fib-Levels nach grossen Bewegungen zwischen Signal und Place)
- **OpenRisk-Estimate pro Scan-Tick auffrischen**: `_riskManager.SetOpenRiskEstimate(Σ |Entry-SL|×Qty)` in `ScanAndTradeAsync` + im Backtest pro Iteration. Vorher: `_openRiskEstimate` nie aktualisiert → `MaxDailyRiskPercent`-Check (Buch S.13: 1-3 %) ignorierte offene Positionen, nutzte nur realisierte Verluste
- **MissingTakeProfit-Recovery im Reconcile**: `signalsExpectingTakeProfit`-Set wird aus `_positionSignals` aufgebaut und an `PositionDriftAnalyzer.Analyze` weitergereicht. `ReplaceMissingTakeProfitAsync` ruft `PlaceTpLimitOrdersAfterFillAsync` mit echter Position-Qty. Vorher Dead-Code (Set wurde nicht übergeben + Switch hatte kein `case MissingTakeProfit`) → Bot-Restart konnte Positionen ohne TP-Order hängen lassen
- **Recovery-Signale via OnSignalCreated tracken**: `RestorePositionSignal` ruft am Ende `OnSignalCreated(key)` → `_signalCreatedAt`/`_positionOpenTimes` werden gesetzt → Reconcile-Grace-Windows (Orphan 90 s, MissingStop 30 s) greifen auch für recovered Positionen
- **Backtest streamt RiskManager-Updates pro Tick**: `UpdateDailyStats` läuft während der Iteration über neu abgeschlossene Trades (`lastCompletedTradeCount`-Tracker), Tageswechsel-Reset über `currentCandle.CloseTime.Date`. Vorher: `UpdateDailyStats` nur final-Loop → `LossStreakDampening`, `EquityCurveScaling` und `MaxDailyLoss-Circuit` haben im Backtest nie gefeuert, Ergebnisse waren systematisch zu optimistisch

### Pending-Limit-Orders + Recovery

- **Persist synchron VOR `return`**: `await PersistPendingLimitOrdersAsync` (NICHT fire-and-forget) — sonst kann Sub-second-Crash zwischen In-Memory-Set und DB-Write zu Ghost-Orders führen
- **PendingTpRetry-Stage-2/3**: bei BingX-Hochlast (Funding-Settlement) Position erst nach Sekunden sichtbar — Stage 2 platziert mit `fallbackQty`, Stage 3 retried in PriceTickerLoop max 30s
- **Pending-Symbol-Side im Reconcile**: `PositionDriftAnalyzer` schliesst Pending-Limit-Entries vom OrphanSignal-Check aus
- **Stale Pending-Cleanup**: `RiskSettings.PendingLimitOrderMaxAgeHours` (Default 6h) — schützt gegen Symbol-aus-Top-100-gefallen + Pending hängt tagelang gegen toten Markt
- **Triple-Sibling-Key** (`_Prim` + `_Add`): `BuildPendingKey` baut `{symbol}#{sequenceId}` — Reconcile-Loop nutzt `kvp.Value.Symbol` für REST-API-Calls, NICHT `kvp.Key` (Key enthält SequenceId-Suffix)
- **Strategy-Felder im Pending**: NavPointA, IsGklSetup, GklTimeframe, RunnerHardCap, IsCounterTrendScalp, PositionScaleOverride werden im Pending persistiert + bei Recovery rekonstruiert (sonst BE-Trigger feuert nie nach Restart)

### Mathematik / Metriken

- **ATR-Perzentil**: `CalculateAtrPercentile()` — `atr/price*10000` ist KEIN Perzentil
- **Sharpe**: `sqrt(TradesProJahr)` für Annualisierung (NICHT `sqrt(252)`), Sample-Varianz `N-1`
- **Sortino**: Downside-Deviation über ALLE Returns (positive als 0) — Standard-Formel
- **Liquidation**: `(1 - MMR) / Leverage`, bei ≤ 2× Leverage deaktiviert (kein Risiko)
- **Confluence-MaxScore**: dynamisch aus `Enum.GetValues<ConfluenceCategory>()` (verhindert Off-by-One bei neuer Kategorie)

### Android-Spezifika

- Hardcoded `Environment.SpecialFolder.UserProfile` in Services crasht Android → `IAppPaths` via DI, `AndroidAppPaths` nutzt `Context.FilesDir.AbsolutePath`
- `App.AppPathsFactory` in `MainActivity.CustomizeAppBuilder` VOR DI-Build setzen
- Mobile-Shell darf NICHT 8 Views parallel im Konstruktor laden → Content-Swap-Pattern mit `CurrentPageViewModel` + `<ContentControl />`
- `SecureStorageService`-Ctor wrapped `Directory.CreateDirectory` in try-catch, damit DI-Chain nicht kippt
- AOT komplett aus (`RunAOTCompilation=false` + `AndroidEnableProguard=false`) — Mono-AOT-Bug auf .NET 10 mit großen Shared-Library-Graphen

### Sicherheit

- **API-Keys auf Pi**: AES-256-GCM (authentisiert) mit Auto-Migration aus altem AES-CBC-Format. Master-Key per-Installation, chmod 400.
- **DPAPI auf Windows** (Desktop-Standalone) — User-Scope, an Windows-Login gebunden
- **Linux credentials.bin**: chmod 600 nach Schreiben, Atomic-Write via Tmp + Move
- **Keine Secrets in Logs**, Keys in UI maskiert
- **HTTP-Error-Content** auf 200 Zeichen kürzen (vermeidet Token-Leak in Stack-Traces)
- **`PiCredentialStore.GetOrCreateMasterKey`**: Lock um Check+Read+Write (sonst zwei parallele Save/Load erzeugen zwei Master-Keys, zweiter überschreibt → credentials.bin nicht mehr entschlüsselbar)
- **`AuthTokenStore.Save`**: Atomic-Write (Tmp + Move) — Power-Loss mid-write würde sonst 0-byte-Datei = alle Tokens weg

### Scan-Loop Watchdog (Silent-Death-Schutz)

- **Per-Iteration-Timeout**: `TradingServiceBase.RunLoopAsync` umschliesst `ScanAndTradeAsync` mit `LinkedTokenSource.CancelAfter(4 min)`. Bei Hang in HTTP/Klines/Task.WhenAll wird die Iteration hart gecancelled → catch → naechster Versuch. Verhindert das beobachtete „Silent Death"-Pattern (Bot meldet Running, Loop tot, Heartbeat-DB-Writes laufen weiter)
- **`BotEventBus.ScanCycleCompleted`**: feuert pro `RunLoopAsync`-Iteration (Success oder Failure) mit `(UtcTimestamp, Success, ErrorMessage, DurationSeconds)`. Zuverlaessiger Activity-Indikator — feuert auch bei leeren Scans (im Gegensatz zu ScannerResult/TradeOpened)
- **Diagnose-Properties** in `TradingServiceBase`: `LastSuccessfulScanUtc`, `LastScanError`, `LastScanErrorUtc`, `CurrentScanStartedUtc`, `ScanIterationInProgress` — public Getter fuer Watchdog + UI-Debug
- **`StaleEngineDetector` subscribed `ScanCycleCompleted`**: erkennt jetzt auch Hangs zwischen Scanner-Events. Logge `LastScanCycle` + `LastScanError` im Stale-Alert
- **Auto-Restart**: `BotSettings.EnableAutoRestartOnStale` (Default true) + `AutoRestartAfterStaleAlertCount` (Default 2). Nach N×Stale-Alert wird die Engine via `IBotControlService.Stop+Start` automatisch neu gestartet. Reset nach erfolgreicher Recovery

### Server-Bootstrap-Race

- **`BotAutoResumeService.InitialDelay = 15s`**: NTP-Drift (3-10s) + Tailscale-Connect (5-15s) + BingX-DNS nach Pi-Boot brauchen Zeit
- **`PersistResumeFlagAsync` ZUERST, dann Engine-Stop**: Crash mid-Stop führt sonst zu unerwünschtem Auto-Restart nach Reboot
- **Separater DB-Key `AutoResumeFlag`** (NICHT BotSettings-JSON): vermeidet Race mit Collection-Mutation
- **Idempotenz-Lock `_lifecycleLock`** in `LocalBotControlService`: verhindert parallele Start/Stop (z.B. Auto-Resume + manueller Click in Initial-Delay-Fenster)

---

## Build / Test / Deploy

```bash
# Build (Solution)
dotnet build src/Apps/BingXBot/BingXBot.Desktop
dotnet build src/Apps/BingXBot/BingXBot.Server
dotnet build src/Apps/BingXBot/BingXBot.Android

# Standalone-Run (Desktop ohne Pi-Server)
dotnet run --project src/Apps/BingXBot/BingXBot.Desktop

# Tests (gesamte Suite, ohne Live-API-Tests)
dotnet test tests/BingXBot.Tests --filter "FullyQualifiedName!~TradFiLiveVerification"

# Live-API-Tests separat (brauchen Internet + BingX-Public-Endpoint)
dotnet test tests/BingXBot.Tests --filter "FullyQualifiedName~TradFiLiveVerification"

# Pi-Deploy
bash src/Apps/BingXBot/BingXBot.Server/systemd/publish.sh
bash src/Apps/BingXBot/BingXBot.Server/systemd/update.sh raspberrypi.local

# Client-Releases (nach F:\BingXBot-Client)
dotnet publish src/Apps/BingXBot/BingXBot.Desktop -c Release -r win-x64 --self-contained true
dotnet publish src/Apps/BingXBot/BingXBot.Desktop -c Release -r linux-x64 --self-contained true
dotnet publish src/Apps/BingXBot/BingXBot.Android -c Release   # AAB für Play Console
```

---

## Verweise

| Datei | Zweck |
|-------|-------|
| `BingXBot.Shared/CLAUDE.md` | Composition Root, DI-Registrierungen, Namespace-Konvention |
| `BingXBot.Android/CLAUDE.md` | Android-Host, AndroidApp, MainActivity, Manifest |
| `BingXBot.Desktop/CLAUDE.md` | Desktop-Host, Standalone vs. Remote-Client |
| `BingXBot.Shared/ViewModels/CLAUDE.md` | Alle Sub-ViewModels, Lazy-Pattern, BotEventBus |
| `BingXBot.Shared/Views/CLAUDE.md` | AXAML-Views (Desktop + Mobile), MVVM-Strict |
| `BingXBot.Shared/Graphics/CLAUDE.md` | SkiaSharp-Renderer, Paint-Cache |
| `BingXBot.Shared/Converters/CLAUDE.md` | NullableDecimalConverter, StaleOpacityConverter |
| `BingXBot.Shared/Services/CLAUDE.md` | RemoteSettingsAutoSync |
| `src/Apps/BingXBot/SK_BUCH_COMPLIANCE_PLAN.md` | SK-System Master-Plan (25 Punkte, 5 Phasen) |
| `src/Apps/BingXBot/MULTI_TF_STANDALONE_PLAN.md` | Multi-TF-Architektur-Plan |
| `src/Apps/BingXBot/PLAN_SERVER_MODE.md` | Client/Server-Architektur-Plan |
| `src/Apps/BingXBot/Algorithmische Erkennung der Strukturpunkte.docx` | Strukturpunkte-Doku (Pivot, ATR×3, BOS, Volumen, Wick) |
| `src/Apps/BingXBot/SK-System_ Das komplette Handbuch.docx` | SK-Buch (Stefan Kassing) |
| `src/Apps/BingXBot/SK-System_ Technische Spezifikation.docx` | Technische Spec (Konstanten, State-Machine, Setup-Typen) |
| `~/.claude/projects/F--Meine-Apps-Ava/memory/bingxbot.md` | Memory: Trading-Domain-Notizen |
| `~/.claude/projects/F--Meine-Apps-Ava/memory/reference_pi_ssh.md` | Memory: Pi-SSH-Zugang |
| `~/.claude/projects/F--Meine-Apps-Ava/memory/feedback_bingxbot_dailyrisk_bleibt.md` | Memory: User-Ausnahmen vom SK-Buch |
| `Releases/BingXBot/CHANGELOG_*.md` | Release-Notes (Geschichte) |
| `F:\BingXBot-Client\` | Aktuelle Client-Releases (Desktop-Win + Desktop-Linux + Android-AAB) |
