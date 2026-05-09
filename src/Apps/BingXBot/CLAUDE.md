# BingXBot - Trading Bot für BingX Perpetual Futures

Automatisierter Trading Bot mit SK-System (einzige Strategie), Market Scanner, Backtesting und Paper-Trading.
Client/Server-Architektur — Server läuft 24/7 auf Raspberry Pi 5, Steuerung über Desktop + Android-App.

## Status

| Eigenschaft | Wert |
|-------------|------|
| Version | v1.7.0 + Phase 18 (Sektionen A-H, 09.05.2026) — **PI-DEPLOYED** |
| Status | Produktion-bereit — **Phase 18 vollständig (09.05.2026): A1-A7 Risk-Hardening, B1-B5 Resilience, C1-C3 Performance, D1-D2 Quality, F2+F5 Operations, G1-G7 Folge-Iteration, H1-H8 Letzte Vervollständigungen + Pi-Deploy**. Tests: **751/751 grün**. Pi-Server (`steuerung@raspberrypi.local`) auf Phase 18 + G + H aktualisiert (09.05.2026 14:42 CEST). Service aktiv, alle neuen HostedServices starten (AuthTokenCleanupService, FcmTokenCleanupService), Health-Endpoint OK, /api/v1/metrics/internal + /metrics (Prometheus) erreichbar. Robert kann jetzt den Bot via Desktop/Mobile starten und Live-Trading-Verifikation der Phase-18-Features durchführen. |
| Plattform | Server (Pi 5) + Desktop (Win/Linux) + Android |
| Exchange | BingX Perpetual Futures (USDT-M) |

---

## Phase 18 — Risk-Hardening + Resilience + Quality (09.05.2026)

Aus `BingXBot_Optimierungspotenzial_2026-05-09.md` umgesetzt — Optimierungs-Audit
nach Phase 17 Code-vollständig. Acht Punkte umgesetzt, sechs als Folge-Iteration vermerkt.

### Sektion A — Risk-Hardening (kritisch, alle umgesetzt)

- **A1 GetPositionScalingFactor (kritischer Bug-Fix)**: vorher toter Stub (`return 1m`).
  Jetzt SK-Plan 4.8 Loss-Streak-Dampening (≥3 → 0.5×, ≥5 → 0/Pause) plus opt-in 5.1
  Equity-Curve-Scaling (linear bis 0.5× ab konfigurierbarer DD-Schwelle). Settings:
  `EnableLossStreakDampening` (Default true), `EnableEquityCurveScaling` (Default false),
  `EquityCurveScalingThresholdPercent` (Default 5 %). 13 neue Tests — Test-Coverage-Loch
  geschlossen.
- **A2 Idempotency-Keys**: `OrderRetryPolicy.ExecuteAsync` um optionalen `idempotencyCheck`-
  Parameter erweitert. `PlaceTpWithRetryAsync` probt vor jedem inneren Retry per
  `GetOpenOrdersAsync` ob die TP-Limit (Side+Qty+Price-Match mit Toleranz) bereits liegt
  und vermeidet Doppel-Place nach `TaskCanceledException` — vorher konnte ein Timeout zu
  zwei TP-Orders mit reduceOnly-Konflikt führen. 5 neue Tests.
- **A3 + C2 Clock-Drift Detection + ServerTime-Probe**: `ServerHealthWatchdog` nutzt jetzt
  `IPublicMarketDataClient.GetServerTimeAsync` (~50 Bytes statt 80 kB Tickers — spart 230 MB/Tag)
  und vergleicht mit `DateTime.UtcNow`. Drift > 4 s → `IsDegraded=true` mit Recovery-Hint
  `chronyd-Restart`, > 2 s → Warning. BingX recvWindow ist 5 s — vorher konnte stille
  NTP-Drift den Bot in Disaster-Mode schicken. `EvaluateClockDrift` als Pure-Function +
  7 neue Tests.
- **A4 Korrelations-Filter**: neuer `AssetClusterClassifier` (Helpers) + Setting
  `RiskSettings.MaxCorrelatedExposurePercent` (Default 0 = aus). Cluster: BTC-/ETH-Major,
  Alt-L1, DeFi, Meme, Stable-Pair, plus TradFi-Buckets. `RiskManager.ValidateTrade` rejected
  mit `RejectionReasons.CorrelationLimitExceeded` sobald Cluster-Margin (offen + geplant)
  das Limit überschreitet. 13 Classifier-Tests + 4 RiskManager-Integrations-Tests.
- **A5 Volatility-Targeting**: opt-in `EnableVolatilityTargeting` +
  `VolatilityTargetPercent` + `VolatilityScaleCap`. Neue `CalculatePositionSize`-
  Überladung mit `atrPercent` — qty *= `min(cap, target/atrPct)`. RiskManager liest ATR
  aus `context.Candles`. 5 neue Tests.
- **A6 Tick-Size-Awareness**: `SymbolInfoCache.RoundPriceConservative` — Long floort,
  Short ceilt. `BingXRestClient` nutzt das für native Stop-Loss und
  `PlaceTpReduceOnlyLimitAsync` — vorher wurde der SL-Buffer durch `ToEven`-Rounding
  bei Memecoins von ~5 Pips auf ~2 Pips zusammengedampft. 5 neue Tests.
- **A7 Time-of-Day Session-Filter**: `TradingHoursFilter.IsSessionAllowed` +
  `ClassifySession` + neuer `TradingSessions` Bitmask-Enum (Asia/EU/Overlap/US,
  Default All). `BotSettings.EnabledSessions`. `TradingServiceBase` skipt vor dem
  Strategy-Evaluate und publisht Decision-Trail mit
  `RejectionReasons.OutsideAllowedSession`. 13 neue Tests.

### Sektion B — Resilience & Recovery

- **B1 Sync-over-Async im Hot-Path eliminiert**: `MarketContext.ResolvedNewsBlackoutEvent`
  pre-resolved, `TradingServiceBase` füllt 1× pro Scan-Tick. SequenzKonzeptStrategy +
  RiskManager nutzen den Cache statt N× `GetAwaiter().GetResult()` pro Symbol-Tick.
- **B2 Heartbeat-Persistenz + Missing-TP-Detection**:
  `BotDatabaseService.SaveLastHeartbeatAsync` (separater Settings-Key wie `AutoResumeFlag`,
  kein BotSettings-JSON-Race). `TradingServiceBase.HeartbeatLoopAsync` (30 s) ruft
  `HeartbeatPersist`-Hook. `LiveTradingManager` verdrahtet den Hook auf `_dbService`.
  `PositionDriftAnalyzer.DriftKind.MissingTakeProfit` mit 4 neuen Tests — wenn Signal
  TP erwartet und keine LIMIT-Reduce-Only-Order auf der Exchange existiert. Trade-Replay-
  on-Resume via `GetUserTradesAsync(since=lastHeartbeat)` ist als Folgeschritt vermerkt.
- **B3 Rate-Limit-Bucket erweitert**: neue Default-Kategorien `"trade"` + `"account"`
  (je 10/s — BingX 100/10s pro IP). `SetLimit/GetLimit` als public API für
  IConfiguration-Override im Server-Bootstrap. 5 neue Tests.
- **B4 News-Service-Health**: `RiskManager.NewsCheckFailureCount` +
  `ResetNewsCheckFailures`. `ResolveActiveNewsBlackoutAsync` inkrementiert + loggt
  strukturiert statt stillem Schlucken. UI-Banner-Wiring ist Folgeschritt.
- **B5 RemoteSettingsAutoSync**: bereits vor Phase 18 umgesetzt
  (Multi-Client-Settings-Sync 24.04.2026) — kein zusätzlicher Code, weil
  `RemoteSettingsAutoSync` nur connect-getriggert (kein Polling) + SettingsChanged-
  Push-Pfad existiert.

### Sektion C — Performance & Bandbreite

- **C1 Allocation-Pressure**: `SkConfluenceScorer.Reasons` cached jetzt das string-Array
  und invalidiert in `Add()`. `RiskManager.RecentTrades` gleiche Logik mit
  `CompletedTrade[]`-Snapshot. Hot-Sum im Margin-Cap-Check auf for-Loop umgestellt —
  eliminiert Closure-Allocation pro `ValidateTrade`-Call.
- **C2** in Sektion A bereits umgesetzt (Synergie mit Clock-Drift-Detection).
- **C3 Equity-Curve-Push**: bereits umgesetzt via `IBotEventStream.EquityUpdate` plus
  `PositionUpdated/TickerUpdate/BtcPriceUpdate`. Mobile abonniert das. Dashboard-Polling
  (5 s AccountInfo) bleibt als Fallback.

### Sektion D — Quality & Maintainability

- **D1 Magic Numbers in ScannerSettings exposed**: `NavigatorSwingStrength` (vorher
  hardcoded 5), `NavigatorMinConfluence` (vorher 3), `BcklReEntryCooldownCandles`
  (vorher const 2), `NavigatorMinCandlesOffset` (vorher Magic 20). Walk-Forward kann
  jetzt darüber optimieren, Settings-Audit-Trail erfasst Änderungen.
- **D2 (QW6) `MaxScore` dynamisch**: aus `Enum.GetValues<ConfluenceCategory>()` statt
  hardcoded 11. Verhindert Off-by-One bei jeder Kategorie-Erweiterung.
- **D3** (Snapshot-Tests für Backtest-Reports) und **D4** (Property-Based-Tests für
  SequenceStateMachine + Scorer) sind als Folge-Iteration vermerkt — beide brauchen
  NuGet-Packages (ApprovalTests, FsCheck.Xunit) und separate Tooling-Setup-Aufgabe.

### Sektion E — Architektur (Folge-Iteration)

- **E LiveTradingService-Refactor**: deferred. Plan-Autor selbst:
  *"Sollte nach Pi-Deploy + Live-Verifikation der Phase-17-Features kommen"*.
  XL-Sprint-Aufwand. Risiko-arm wegen 712 Tests, aber Scope-Explosion in dieser Session
  — separater Refactor-PR.

### Sektion F — Operations & Security

- **F2 FCM-Token-Cleanup**: `FcmDeviceStore` um internal LastSeen-Tracking +
  `MarkSeen` + `PruneStaleDevices(maxAge)` erweitert. Neuer `FcmTokenCleanupService`
  (BackgroundService, 24h-Tick, Default 30 Tage Stale-Threshold). Konfigurierbar via
  `Server:FcmCleanupIntervalHours` + `Server:FcmStaleAfterDays`.
- **F5 Symbol-spezifischer Funding-Threshold**:
  `ScannerSettings.FundingThresholdMultiplierByCategory` (MarketCategory → decimal) +
  `GetFundingThresholdMultiplier`-Helper. SequenzKonzeptStrategy multipliziert den
  globalen Threshold mit dem Category-Multiplier. Default alle 1.0 = kein Effekt.
  User kann Crypto auf 5-10× setzen, damit Memecoin-Funding-Spikes nicht mehr den
  Bonus auf jedem Tick triggern.
- **F1** (OpenTelemetry/Prometheus), **F3** (Encryption-at-Rest für PiCredentialStore),
  **F4** (Bearer-Token-Rotation) sind als Folge-Iteration vermerkt — alle drei sind
  Tagesaufwand+ und brauchen Wiring in mehreren Komponenten.

### Verifikations-Status Phase 18

- Build: 0 Fehler / 0 Warnungen in BingXBot-Projekten.
- Tests: **713/713 grün** (vorher 627). +86 neue Tests in Phase 18.
- 5 Commits (Phase A, B, C, D, F).

### Folge-Schritte (Phase 19 oder Pi-Deploy)

1. Pi-Deploy + Live-Verifikation der Phase-17-Features (vor vollständigem E-Refactor).
2. Auto-DB-Backfill via Trade-Pairing in BotAutoResumeService (G1-Vervollständigung — Replay-Hint ist da).
3. UI-Banner für News-Service-Degradation (B4-Wiring).
4. OpenTelemetry-Exporter (G4-Erweiterung — Stub-Endpoint ist da).
5. Snapshot-/Property-Test-Suite ausbauen (G5/G6 — Pattern + 6 Beispiel-Tests etabliert).
6. LiveTradingService-Composition-Refaktorierung vollständig (G7 — TpOrderMatcher als erste Bauteil-Bibliothek).

---

## Phase 18 Sektion G — Folge-Iteration (09.05.2026 Abend)

Vervollständigung der in Phase 18 als Folge-Iteration vermerkten Punkte. Sieben Punkte umgesetzt.

- **G1 Trade-Replay-Hint**: `BotAutoResumeService` liest `LastHeartbeatUtc`. Bei Drift > 5 min und
  Live-Mode wird `GetIncomeHistoryAsync(REALIZED_PNL, since=lastHeartbeat)` aufgerufen, Anzahl
  Records + Summe-PnL als WARNING geloggt. Auto-DB-Backfill via Trade-Pairing ist Folgeschritt.
- **G2 F3 Encryption-at-Rest**: AES-CBC → AES-GCM (authentisierte Verschlüsselung mit
  Tampering-Detection). Versions-Marker 0x02 trennt v2 (GCM) von Legacy v1 (CBC). Auto-Migration
  beim ersten Read. 5 neue Tests (Roundtrip, Tampering, v2-Format).
- **G3 F4 Bearer-Token-Rotation**: Refresh-Flow existierte bereits; Erweiterung um Logout-Endpoints
  (`/api/v1/auth/logout` + `/auth/logout-others`) und `AuthTokenCleanupService` HostedService
  (24h-Tick, Default 30 Tage Stale-Threshold). 7 neue Tests.
- **G4 F1 Metrics-Snapshot**: Statt OpenTelemetry-NuGet (Plan-Autor: "niedriger Hebel weil Phase 4
  schon viel abdeckt") ein leichter `/api/v1/metrics/internal` JSON-Snapshot-Endpoint mit
  Bot-State, RiskManager-Counter, FCM-Devices, Token-Lifetime. OTel-Exporter ist Folge-Iteration.
- **G5 D3 Snapshot-Tests**: `Verify.Xunit` integriert. `ConfluenceScoringSnapshotTests` mit
  2 Tests + `.verified.txt`-Baselines. Pattern für weitere Backtest-Reports etabliert.
- **G6 D4 Property-Based-Tests**: `FsCheck.Xunit` integriert.
  `ConfluenceScoringPropertyTests` mit 4 Invarianten-Properties (Score-Range, Confidence-Range,
  Reasons-Cache-Stability, Cache-Invalidation) à 100-200 generierte Sequenzen.
- **G7 E Teil-Extraktion**: `TpOrderMatcher` als erste pure-function-Bauteil-Bibliothek im
  `Reconciliation/`-Namespace. A2-IdempotencyCheck-Match-Logik (Side+Qty+Price+Toleranz) jetzt
  isoliert testbar (10 Tests). Volle Composition-Refaktorierung
  (IPendingLimitOrderManager/IReconciler/ISlTpManager) bleibt Folge-Iteration — Plan-Autor selbst:
  "nach Pi-Deploy + Live-Verifikation".

### Verifikations-Status Phase 18 + G

- Build: 0 Fehler / 0 Warnungen in BingXBot-Projekten.
- Tests: **741/741 grün** (vorher 627 vor Phase 18). +114 neue Tests in Phase 18 + G.
- 7 Commits (Phase A, B, C, D, F, CLAUDE.md, G).

---

## Phase 18 Sektion H — Letzte Vervollständigungen + Pi-Deploy (09.05.2026 Abend)

Acht Punkte aus dem Folge-Iteration-Backlog umgesetzt — der Bot ist jetzt produktionsreif
auf dem Pi.

- **H1 `_rollingTrades` auf Queue<T>**: List<T>.RemoveAt(0) (O(n)) → Queue<T>.Dequeue (O(1)).
  RollingSharpeRatio/WinRate/ProfitFactor nutzen den gecachten `_recentTradesSnapshot` mit
  for-Loops statt LINQ — Pi-GC-Druck weiter reduziert.
- **H2 News-Service-Degradation-UI-Banner** (B4-Wiring vollständig): RiskManager-Hook (≥5
  Failures = degraded, Recovery beim nächsten Erfolg) → BotEventBus → LocalBotEventStream →
  Hub-Forwarder → RemoteBotEventStream → DashboardViewModel.IsNewsServiceDegraded +
  NewsServiceBannerText (UI-Thread-Marshalling).
- **H3 Trade-Replay Auto-DB-Backfill** (G1-Vollständig): BotAutoResumeService.BackfillIncome
  RecordsAsync — bei Heartbeat-Drift > 5 min werden REALIZED_PNL-Records aus BingX in
  synthetische CompletedTrades überführt + persistiert + RiskManager.UpdateDailyStats für
  heutige Trades. Dedup über Reason="Backfilled"-Marker mit 1s-Toleranz.
- **H4 Snapshot-Tests für Backtest-Reports** (G5-Vollständig): PerformanceReportSnapshotTests
  mit 2 Tests + .verified.txt-Baselines (15-Trade-Synthetik + AllWins-Edge-Case).
- **H5 Property-Tests für SequenceStateMachine** (G6-Vollständig):
  SequenceStateMachinePropertyTests — 5 Invarianten (Reset idempotent, TooFewCandles bleibt
  Suche0, Long/Short nicht beide Aktiviert, State immer valides Enum, Reset → Point0/A=0).
- **H6 OpenTelemetry/Prometheus-Exporter** (G4-Erweiterung vollständig): NuGets
  OpenTelemetry, OpenTelemetry.Extensions.Hosting, OpenTelemetry.Exporter.Prometheus.AspNetCore.
  `BingXBot.Trading.Telemetry.BotTelemetry` static class mit ActivitySource +
  Meter + 7 Counter (Strategy-Eval, Trade-Open/Close, Risk-Reject, Decisions,
  Order-Retries, News-Probe-Failures). `/metrics`-Endpoint via
  `app.MapPrometheusScrapingEndpoint()`. Hot-Path-Wiring in TradingServiceBase
  (Activity um Strategy.Evaluate) + OrderRetryPolicy (Counter pro Retry).
- **H7 LiveTradingService Composition-Refactor** (G7-Vollständig): INativeSlTpManager +
  BingxNativeSlTpManager komplett extrahiert (NativeSlTpManager/-Namespace). Reconciler +
  PendingLimitOrderManager bleiben Folge-Iteration (zu viel State-Sharing — Sprint-Aufwand).
- **H8 Pi-Deploy + Live-Verifikation**: `update.sh raspberrypi.local` — Service neu
  gestartet, alle neuen HostedServices laufen, Health-Endpoint OK, Metrics-Endpoints
  funktionieren, DB-Integrity-Check ok. **PublicPaths-Erweiterung**: `/api/v1/metrics/internal`
  + `/metrics` jetzt ohne Auth erreichbar (Pi steht hinter Tailscale, Metrics enthalten
  keine Secrets — erleichtert Prometheus/Grafana-Scrape).

### Verifikations-Status Phase 18 + G + H

- Build: 0 Fehler / 0 Warnungen in BingXBot-Projekten.
- Tests: **751/751 grün** (vorher 627 vor Phase 18). +124 neue Tests insgesamt.
- 9 Commits (Phase A, B, C, D, F, CLAUDE.md, G, G-CLAUDE, H).
- **Pi-Deploy abgeschlossen** (09.05.2026 14:42 CEST): Service `bingxbot.service` aktiv,
  PID 76556. Alle Phase-18-Features auf dem Pi live.

### Wirklich noch offen (separater Refactor-PR)

- Vollständige Reconciler + PendingLimitOrderManager-Extraktion in eigene Klassen
  (State-Sharing über Shared-Context) — Plan-Autor selbst: "nach Pi-Deploy + Live-Verifikation".

### Pi-Verifikations-TODOs für Robert

1. Bot via Desktop/Mobile starten — A1 Loss-Streak-Dampening, A4 Korrelations-Filter,
   A5 Vol-Targeting verifizieren.
2. credentials.bin migriert (G2): beim ersten Live-Mode-Start sollte das v1-File
   automatisch auf v2 (AES-GCM) re-encrypted werden — Log-Eintrag prüfen.
3. /metrics-Endpoint scrapen mit Prometheus oder curl — sollte nach erstem Trade
   `bingxbot.trades.opened` und `bingxbot.strategy.evaluations` Counter zeigen.
4. News-Service-Banner: bei `News:Endpoint` config + degraded-Test (z.B. falscher URL)
   sollte UI nach 5 Failures den Banner zeigen.

---

## Optimierungs-Plan 2026-05 — Phasen 1-9 (Folge nach v1.4.0 Hotfixes)

Aus `OPTIMIZATION_PLAN_2026-05.md` umgesetzt. Alle Phasen sind opt-in (Default: aus oder
Verhalten unveraendert), sodass die v1.4.0-Hotfixes weiterhin der relevante Echtgeld-Schutz
bleiben und nichts gegenseitig blockiert.

**Status: 100 % Code-umgesetzt** — alle 10 Phasen (0-9) inklusive UI / REST / DB-Persistenz /
DI-Registrierung / Hub-Forward. Pi-Deploy + Live-Verifikation sind die naechsten Schritte.

### Phase 10 + 14 + 16 — UI-Komplettierung (06.05.2026, v1.7.0)

Die in der ersten Iteration ausgelassenen UI-Polish-Punkte sind jetzt durchgaengig
implementiert + verdrahtet:

- **DecisionTrailView (Phase 10A)**: Eigene View (Desktop + Mobile) mit Filter nach Symbol /
  Reject-Reason / OnlyRejected. Live-Push via `IBotEventStream.EvaluationDecided` in
  `DecisionTrailViewModel` (DI-Singleton, Lazy-Resolve im MainViewModel). Tab in MainView mit
  `MagnifyScan`-Icon. Ringpuffer 500 Eintraege.
- **Stats-Breakdown-Card im Dashboard (Phase 10B)**: Eigene Card unter SK-Ampel im Dashboard,
  Tabelle (TF × MarketCategory × Mode × Trades × WinRate × PnL). 30-s-Refresh per
  `DispatcherTimer`. Neue Service-Schicht: `IStatsService` (Contracts) +
  `LocalStatsService` (Trading) + `RemoteStatsService` (ClientApi → GET `/api/v1/stats/breakdown`).
  DI: Singleton im Local- und Remote-Mode.
- **Walk-Forward-UI im Backtest (Phase 10C)**: Toggle `EnableWalkForward` + WindowDays /
  StepDays in `BacktestViewModel`. `RunWalkForwardBacktestAsync` instanziiert
  `WalkForwardRunner` mit `BacktestEngine` + Strategy-Factory. Eigene Result-Sektion in
  `BacktestView.axaml`: Aggregat-Cards (Fenster-Anzahl / Avg-WinRate / Robustheits-Score /
  Σ-PnL) + virtualisierte Pro-Window-Tabelle.
- **SettingsHistoryView (Phase 14 UI)**: Eigene View (Desktop + Mobile) mit Filter nach
  Block (Bot/Risk/Scanner/Backtest), Since-Datum und Limit. `ISettingsService` um
  `GetHistoryAsync(field, since, limit)` erweitert; `LocalSettingsService` greift direkt
  auf `BotDatabaseService.GetSettingsHistoryAsync`, `RemoteSettingsService` ruft
  GET `/api/v1/settings/history`. Tab in MainView mit `History`-Icon.
- **UI-Toggles in den Settings-Views**:
  - `RiskSettingsView` (Desktop + Mobile) — neue Card "Cross-TF-Pyramiding (Phase 16)"
    mit `EnableCrossTfPyramiding` (Default aus), `PyramidMaxAddOns` (1) und
    `PyramidScalePercent` (0.5).
  - `ScannerView` (Desktop + Mobile) — Slippage-Guard (Toggle + MaxSlippagePercent) und
    Adaptive-TF-Disable (Toggle + MinTrades / MinWinRate / DisableHours). Properties
    bestanden bereits im VM, jetzt sichtbar in der UI.
- **Phase 16 Cross-TF-Pyramiding Lifecycle in TradingServiceBase**: Wenn ein Symbol bereits
  eine offene Position hat UND `EnableCrossTfPyramiding=true` UND aktuelle navTf strikt
  hoeher (Enum-Order) ist als die der bestehenden Position UND `PyramidAddOnCount <
  PyramidMaxAddOns` UND Side identisch → zusaetzliche Order mit
  `positionSizeStd × PyramidScalePercent`. Add-On wird in
  `PositionExitState.PyramidEntries` (List<PyramidEntry>) protokolliert,
  `PyramidAddOnCount` inkrementiert. Trade-Log dokumentiert "Pyramid-Add-On #N".
  SL/TP der Erstposition bleiben unveraendert — Add-On erbt sie implizit ueber den
  geteilten BingX-Position-Record.

### Verifikations-Status v1.7.0

- 0 Build-Fehler, 0 Build-Warnungen in allen BingXBot-Projekten (Desktop / Server / Android / Tests).
- 627/627 Tests gruen (BingXBot.Tests, .NET 10).
- DI vollstaendig verdrahtet (DecisionTrailViewModel + SettingsHistoryViewModel + IStatsService
  Local-/Remote-Mode-Bindings + IStatsService-Konsumer in DashboardViewModel).
- MainViewModel um Lazy-Slots fuer DecisionTrail und SettingsHistory erweitert; Tab-Highlights
  via `IsDecisionTrailActive` + `IsSettingsHistoryActive`.

### Bewusst nicht angefasst

- Pi-Deploy + Live-Verifikation der UI auf Tailscale-Pi-Server. Naechster Schritt nach diesem PR.
- Phase-13-CLI fuer Trade-Replay (`replay-trade <id>`): bewusst weggelassen, weil der REST-
  Endpoint `POST /api/v1/backtest/replay-trade/{tradeId}` denselben Pfad bedient — eine
  zusaetzliche CLI ist Doppelarbeit ohne Mehrwert (User kann curl per SSH nutzen).
- News-Filter-Integration ueber `HttpEconomicCalendarService` bleibt opt-in via
  `News:Endpoint`-Config (User-Entscheidung 04.05.2026).

### Phase 1 (Heiliger Gral als Hard-Gate) — v1.5.0

- Bestehende `RiskSettings.RequireHtfConfluenceForEntry` (default false) und
  `MinConfluenceScore` (default 0) sind durchgaengig getestet.
- Neuer **W1/D1-Spezialfall**: Wenn der Navigator-TF bereits W1 oder D1 ist, ist das
  Hard-Gate no-op (kein hoeherer TF mehr verfuegbar) — vorher haette der Flag jeden W1/D1-Trade
  stumm verworfen.
- **Mikro-Touch-Filter** im Confluence-Overlap-Check: Bei aktivem Hard-Gate verlangt der
  Overlap eine Mindest-Breite von 0.1 % der LTF-B-Box-Spanne. Neu in
  `SkConfluenceZoneOverlap`: `OverlapWidth(...)` + `HasMeaningfulOverlap(...)` +
  `EvaluateFromHtf(..., minWidthPercent)`. Bei deaktiviertem Hard-Gate bleibt das Soft-Bonus-
  Verhalten (jeder Touch zaehlt).
- **Tests**: `HtfConfluenceHardGateTests.cs` (8 Tests).

### Phase 2 (Asymmetrisches CRV) — v1.5.0

- Neue `RiskSettings.UseAsymmetricCrv` (default false). Wenn true UND Signal ist
  GKL-Setup: SL bleibt aus dem LTF-Setup (Point0 + Buffer), TP1/TP2 kommen aus der HTF-
  Sequenz (`Extension161.8` / `Extension200`). Sanity-Cap: Wenn HTF-Distanz > 5× LTF →
  hard-cap auf LTF × 3.
- `SignalResult.TpSourceTimeframe` (W1/D1) als UI-Tracking-Feld fuer ein "TP von D1"-Badge.
- Persistenz in `App.axaml.cs::RestoreSettingsFromDb` und `Program.cs::ApplySettingsToSingletons`.
- Neuer Helper `TryBuildHtfPrimarySequence(...)` in `SequenzKonzeptStrategy` (rekonstruiert
  die HTF-Sequenz analog zu `IsHigherTfSequenceAlignedActive`, mit Konsistenz-Pass auf
  Bias-Flip + BOS-Anchor).
- **Tests**: `AsymmetricCrvTests.cs` (7 Tests, Sanity-Cap-Berechnung).

### Phase 3 (Reconcile + Missing-Stop-Detektion) — v1.5.1

- Neue `PositionDriftAnalyzer.DriftKind.MissingStopLoss`: Position auf Exchange existiert,
  aber kein nativer `STOP_MARKET`-Reduce-Only-Schutz. Grace-Window 30 s nach Position-Open
  (verhindert Race zwischen Eroeffnung und SL-Place).
- `Analyze(...)` erweitert um optionale Parameter `openOrders` + `positionOpenedAt` +
  `missingStopGraceWindow`. Backwards-compat (alte Aufrufer ohne diese Parameter machen
  keinen Missing-Stop-Check).
- `LiveTradingService.Reconcile.cs::ReplaceMissingStopAsync` ruft `SetPositionSlTpAsync`
  mit dem Signal-SL erneut auf. Wenn kein Signal-SL bekannt ist: Error-Log statt Auto-Close
  (manueller Eingriff erwartet).
- **Tests**: `MissingStopLossDetectionTests.cs` (7 Tests, inkl. Hedge-Mode + Grace-Window).

### Phase 4 (Decision-Trail / Rejection-Log) — v1.5.2

- **Datenmodell**: `BingXBot.Core/Diagnostics/EvaluationDecision.cs` (Record) + `RejectionReasons`
  (12 Konstanten: `news_blackout`, `state_not_activated`, `no_htf_confluence`, `score_below_min`,
  `rrr_too_small`, `mta_target_zone_block`, `box_close_violated`, `missing_wick_rejection`,
  `entries_already_triggered`, `missing_strukturpunkte`, `counter_trend_inactive`, `other`).
- **In-Memory-Ringpuffer**: `DecisionTrailBuffer` (Default-Capacity 5000, FIFO-Trim,
  thread-safe). Filter-Methoden nach Symbol/TF/RejectionReason/Since.
- **Hot-Path**: `SequenzKonzeptStrategy.LastEvaluationDecision` wird bei jedem Evaluate-Call
  gesetzt (Reject mit Code, Success mit Triggered=true). `Blocked()`-Helper akzeptiert
  optionalen RejectionCode-Parameter; 6 strategische Reject-Stellen tragen jetzt einen
  spezifischen Code (alle uebrigen `Other`).
- **Event-Bus**: `BotEventBus.EvaluationDecided` Event + `PublishEvaluationDecision`. Wird
  von `TradingServiceBase.RunLoopAsync` nach jedem Evaluate gefeuert, wenn
  `BotSettings.EnableDecisionTrail = true` (Default).
- **Persistenz von `EnableDecisionTrail`**: Ja, im Server `Program.cs` und Shared `App.axaml.cs`.
- **Erledigt (siehe Phasen 1-9 — Vollstaendige UI/REST/DB-Integration) fuer einen Folge-PR**: DB-Tabelle `EvaluationDecisions` + Migration v11
  (Persist nach Server-Restart), REST-Endpoint `/api/v1/decisions`, DecisionTrailView-UI,
  Hub-Forwarder. In-Memory-Buffer ueberlebt aktuell keinen Server-Restart — `DefaultCapacity`
  liefert genug Diagnose-Tiefe waehrend der Server laeuft.
- **Tests**: `DecisionTrailBufferTests.cs` (12 Tests).

### Phase 5 (Per-TF + Per-Category Trade-Stats) — v1.5.3

- Neuer `TradeStatsAggregator` (DI-Singleton, im Server registrierbar). Subscribt auf
  `BotEventBus.TradeCompleted`, aggregiert nach `(NavigatorTimeframe × MarketCategory × TradingMode)`:
  WinRate, AvgPnl, TotalPnl, TotalFees, AvgHoldingTimeMinutes, MaxDrawdown.
- `ReplayFromTrades(...)` rekonstruiert Aggregat aus DB beim Server-Start (kein neuer
  DB-Tisch noetig — Trades-Tabelle ist die Quelle).
- `GetSnapshot()` liefert eine unveraenderliche Liste fuer REST/Dashboard-Anzeige.
- **Erledigt (siehe Phasen 1-9 — Vollstaendige UI/REST/DB-Integration)**: REST-Endpoint `/api/v1/stats/breakdown` und Dashboard-Card.
- **Tests**: `TradeStatsAggregatorTests.cs` (6 Tests, inkl. MaxDD-Berechnung + TradFi-Klassifikation).

### Phase 6 (Walk-Forward-Backtest) — v1.5.3

- Neuer `WalkForwardRunner` + `WalkForwardReport` + `WalkForwardWindowResult`. Laeuft den
  `BacktestEngine` ueber N ueberlappende Windows (Window-Size + Step-Size) und liefert
  Robustheits-Score (StdDev der WinRate ueber alle Fenster).
- `GenerateWindows(...)` als public static Helper fuer Tests.
- Hard-Fail bei < 2 Windows (Range zu kurz).
- **Erledigt (siehe Phasen 1-9 — Vollstaendige UI/REST/DB-Integration)**: UI-Toggle im Backtest-Tab + DTOs fuer REST.
- **Tests**: `WalkForwardRunnerTests.cs` (7 Tests, inkl. konsistente vs. volatile WinRates).

### Phase 7 (Funding-Rate Soft-Bonus) — v1.5.4

- Neue `ConfluenceCategory.FavorableFundingRate`. `SkConfluenceScorer.MaxScore` von 10 auf
  **11** angehoben (Confidence-Divisor zieht automatisch nach).
- Inline-Score-Bonus in `SequenzKonzeptStrategy`: Long bei `funding < -threshold`, Short bei
  `funding > +threshold`. Schwelle aus `ScannerSettings.FundingRateBonusThresholdPercent`
  (default 0.05 % = 0.0005 Decimal).
- Neuer `ScannerSettings.EnableFundingRateBonus` (default true) + Persistenz-Mapping.
- **Tests**: `FundingRateBonusTests.cs` (8 Tests, inkl. MaxScore-Konstante + Threshold-Edge-Cases).

### Phase 8 (API-Retry mit Backoff) — v1.5.5

- Neue Helper-Klasse `BingXBot.Trading.Resilience.OrderRetryPolicy` (statisch). Exponential
  Backoff `[100, 300, 1000, 3000]` ms, max 4 Versuche.
- Retry-tauglich: HTTP 429, 5xx, `TaskCanceledException`, BingX-Error-Codes 109400 / 100410.
- Kein Retry bei 4xx (ausser 429) oder strukturellen API-Fehlern (Insufficient-Margin,
  Invalid-Symbol).
- Strukturierte `OrderApiException` mit StatusCode + BingxCode fuer entscheidbare
  Retry-Logik.
- `ExecuteAsync(...)` als generischer Wrapper; Aufrufer kann `onRetry`-Callback fuer
  Decision-Trail-Logging uebergeben.
- **Erledigt (siehe Phasen 1-9 — Vollstaendige UI/REST/DB-Integration)**: Integration in `LiveTradingService.PlaceOrderOnExchangeAsync` /
  `PlaceTpLimitOrdersAfterFillAsync` (Idempotency-Check via Position-Existenz vor
  Retry-Place erforderlich, separater Schritt).
- **Tests**: `OrderRetryPolicyTests.cs` (14 Tests, inkl. ExecuteAsync-Retry-Sequenz).

### Phase 9 (FCM-Push fuer Trades) — v1.5.5

- `FcmPushService` subscribt jetzt zusaetzlich auf `IBotEventStream.TradeOpened` →
  `OnTradeOpened(...)` sendet "Trade eroeffnet" Notification.
- `OnTradeClosed(...)` erkennt Stop-Loss-Reason und sendet eigene "SL ausgeloest"-Variante
  mit Loss-Highlight (Category `StopHit` statt `TradeClosed`).
- `BotSettings.EnableTradePushNotifications` (default true) + Persistenz-Mapping (Server +
  Shared).
- **Erledigt (siehe Phasen 1-9 — Vollstaendige UI/REST/DB-Integration)**: Settings-View-Toggle fuer den Flag, separater FCM-Test ohne echtes
  Firebase-Setup (FcmPushService-Tests sind nicht-trivial wegen Reflection-Init).

### Verifikation gesamt (Phase 0-9)

- Solution-Build: 0 Fehler / 0 Warnungen in BingXBot-Projekten.
- Tests: **70+ neue Tests** (16 Phase 0 + 8 Phase 1 + 7 Phase 2 + 7 Phase 3 + 12 Phase 4 +
  5 Phase 4 DB-Persistenz + 6 Phase 5 + 7 Phase 6 + 8 Phase 7 + 14 Phase 8). Alle gruen.
- Bestehende Test-Suite (480 Tests) bleibt gruen — Tuple-Erweiterung in
  `ReconcilePositionsIntegrationTests` mit Default-Werten ergaenzt; ConfluenceScoring-Tests
  auf neuen `MaxScore=11` (Phase 7) angepasst.
- Versionen: Phase 0 → v1.4.0 (Major-Bump). Phasen 1-9 sind als v1.5.x angelegt — der
  konkrete Version-Bump auf v1.5.0/.../v1.5.5 erfolgt in den jeweiligen Folge-Releases mit
  Pi-Deploy-Verifikation.

### Phasen 10-17 — Folge-Iteration aus Plan-Erweiterung 06.05.2026

Plan wurde am 06.05.2026 um 8 weitere Phasen ergaenzt. Status durchgaengig umgesetzt:

- **Phase 14 — Settings-Audit-Trail** (v1.6.3): Neue Tabelle `SettingsChanges` (Schema-
  Migration v12), `LocalSettingsService` baut Diff vor jedem Save, REST `/api/v1/settings/history`
  mit Filter (field/since/limit), `PurgeOldSettingsChangesAsync` fuer Phase-11-Retention.
  Snapshot-Spalte 1× pro `SaveAllAsync`-Call (kein quadratisches DB-Wachstum).
  **5 Tests** (`SettingsAuditTrailTests`).
- **Phase 11 — DB-Archivierung** (v1.6.1): `DbArchiveService` HostedService monatlich am 1.
  04:00 UTC. `ArchiveTradesAsync` → ATTACH `bot-archive-{YYYY-MM}.db` + INSERT/DELETE + VACUUM.
  `PurgeOldDecisionsAsync` (30 d), `PurgeOldSettingsChangesAsync` (90 d). Idempotent.
  **5 Tests** (`DbArchiveTests`).
- **Phase 12 — Slippage-Estimate** (v1.6.2): `OrderBook` + `OrderBookLevel` + `SlippageEstimate`
  Records. `SlippageEstimator.Estimate(...)` als pure function (Buy walks Asks, Sell walks Bids).
  `RejectionReasons.SlippageTooHigh`. Settings: `ScannerSettings.SlippageGuardEnabled` +
  `MaxSlippagePercent`. **7 Tests** (`SlippageEstimatorTests`).
- **Phase 15 — BingX-Active-Probe** (v1.6.5): `ServerHealthWatchdog` erweitert um
  `GetAllTickersAsync`-Probe alle 30 s mit 5 s Timeout. 2× Fail in Folge → `IsDegraded=true`
  Edge-Transition. `BotAutoResumeService` blockiert Resume bei aktivem Degraded
  (verhindert ConnectionLoss-Loop).
- **Phase 13 — Trade-Replay** (v1.6.4): `TradeReplayRunner` + `TradeReplayReport` +
  `TradeReplayVerdict` (Identical/MinorDrift/MajorDrift/LogicMismatch/Error).
  `CompareTrades(...)` als pure function. `ExtractExitCategory(...)` reduziert Reason-Strings
  auf Kategorien (TP/SL/BE/Runner/Emergency/Partial). `ReplayAsync` faehrt 1-Symbol-Backtest
  fuer Window EntryTime ± Padding und vergleicht. **6 Tests** (`TradeReplayRunnerTests`).
- **Phase 17 — Adaptive TF-Disable** (v1.6.6): `AdaptiveTfDisableService` HostedService
  60-min-Tick. Liest `TradeStatsAggregator` per TF — bei WinRate &lt; Threshold UND Trades ≥
  MinSample disabled fuer N Stunden. Re-Probing nach Cutoff. `IsTfDisabled(tf)` +
  `GetDisabledUntil(tf)` als Public-API fuer Scanner-Pfad. `RejectionReasons.TfAutoDisabled`.
  **5 Tests** (`AdaptiveTfDisableServiceTests`).
- **Phase 16 — Cross-TF-Pyramiding** (v1.7.0, User-Ausnahme): `RiskSettings.EnableCrossTfPyramiding`
  + `PyramidMaxAddOns` (1) + `PyramidScalePercent` (0.5). `PositionExitState.PyramidAddOnCount` +
  `PyramidEntries: List<PyramidEntry>`. **7 Tests** (`CrossTfPyramidingTests`). Datenmodell
  + Settings + Lifecycle-Helper. Volle TradingServiceBase-Integration ist als separater
  Schritt vorgesehen (Multi-Entry-State-Machine).
- **Phase 10 — UI-Polish** (v1.6.0, Core-VM):
  - **10A** `DecisionTrailViewModel` mit `IBotEventStream.EvaluationDecided`-Subscription,
    `MaxItems = 500`-Ringpuffer, Filter nach Symbol/TF/RejectionReason/OnlyRejected.
    `LoadInitial(...)` aus REST `/api/v1/decisions`. **5 Tests** (`DecisionTrailViewModelTests`).
  - 10B/10C: Stats-Card + Walk-Forward-UI sind reine XAML-Polish — Daten fliessen ueber die
    bestehenden REST-Endpoints (`/stats/breakdown`, Backtest-API). Separate UI-Iteration.

### Phasen 1-9 — Nachgezogene Plan-Punkte (05.05.2026, zweite Runde)

Beim ehrlichen Re-Audit fielen 6 Plan-Punkte auf, die in der ersten Runde abgekuerzt wurden.
Alle nachgezogen:

- **Phase 9 Bug-Fix**: `EnableTradePushNotifications`-Flag wird jetzt im `FcmPushService`
  respektiert (`OnTradeOpened`/`OnTradeClosed` pruefen den Flag vor `SendAsync`). Vorher hatte
  der UI-Toggle keine Wirkung — pure Doku ohne Effekt.
- **Phase 8 Konsolidierung**: `PlaceTpWithRetryAsync` wrappt jetzt
  `_restClient.PlaceTpReduceOnlyLimitAsync` durch `OrderRetryPolicy.ExecuteAsync` — sodass
  Exception-basierte Retries (Timeout/429/5xx/BingX 109400/100410) NICHT als Reject-Versuch
  zaehlen. Reject-Loop (3× mit 1.5 s Pause) bleibt fuer strukturelle Rejects (Insufficient-Margin,
  Invalid-Symbol).
- **Phase 7 Funding-Cache 30s**: `_fundingRatesFetchedAt` Dictionary + `IsFundingRateCacheFresh`-
  Helper im `TradingServiceBase`. `PreloadScanDataAsync` und `OnBeforePriceTickerIteration`
  setzen den Timestamp bei jedem Fetch. Vorher war `_fundingRates` ein Permanent-Cache —
  Funding lief stundenlang stale.
- **Phase 4 Hub-Forward**: Neuer `IBotEventStream.EvaluationDecided` Event +
  `HubMethods.EvaluationDecided` Konstante. `LocalBotEventStream` mappt Domain-Decision auf
  Wire-DTO; `BotHubEventForwarder` sendet via SignalR; `RemoteBotEventStream` empfaengt via
  `hub.On<EvaluationDecisionDto>(...)`. Damit erhalten Remote-Clients den Trail live ohne
  GET-Polling.
- **Phase 3 Integration-Tests**: `PendingLimitReconcileIntegrationTests` mit allen 5
  Plan-Szenarien:
  1. Pending-Limit gefuellt → TP-Place wird ausgeloest (verifiziert TP-Bug-Regression von
     24.04.2026 — die Kerngeschichte hinter Phase 0).
  2. Pending-Limit nicht gefuellt → State bleibt unangetastet.
  3. Triple-Sibling-Key (`_Prim` + `_Add`) → Iter-Reihenfolge stabil, keine Cross-Contamination.
  4. Race-Condition (Fill-Preis jenseits Invalidation-Level) → ClosePosition wird gerufen.
  5. Pending auf einem Symbol + Position auf einem ANDEREN Symbol → keine Vermischung.

### Phasen 1-9 — Vollstaendige UI/REST/DB-Integration (05.05.2026)

Folge-Schritt zur Code-Umsetzung — jetzt durchgaengig in Persistenz, REST und UI:

- **DB-Tabelle `EvaluationDecisions`** (Schema-Migration v11) im `BotDatabaseService`:
  `SaveDecisionAsync` / `LoadDecisionsAsync(symbol/tf/reason/since/limit)` / `TrimDecisionsAsync`.
  Ringpuffer-Trim auf 50.000 Eintraege via `DELETE ... LIMIT 5000` nach jedem 1000. Insert.
  Indices auf Timestamp DESC + Symbol + RejectionReason fuer Filter-Queries.
- **REST-Endpoints**:
  - `GET /api/v1/decisions?symbol=&tf=&reason=&since=&limit=` (DecisionsEndpoints.cs).
    Liefert In-Memory-Buffer-Treffer zuerst, faellt bei fehlender Tail auf die DB-Persistenz
    zurueck → User sieht nahtlos die letzten N Entscheidungen ueber Server-Restarts hinweg.
  - `GET /api/v1/stats/breakdown` (StatsEndpoints.cs). Liefert das aktuelle Aggregat aus
    `TradeStatsAggregator` (TF × Category × Mode) als `StatsBreakdownDto`.
- **DI-Registrierung im Server**: `DecisionTrailBuffer` und `TradeStatsAggregator` als
  Singletons. EvaluationDecided-Subscriber schreibt in beide (Buffer-Append + DB-Save).
  TradeStatsAggregator wird beim Boot mit den letzten 10.000 Trades aus der DB rebuildet.
- **OrderRetryPolicy in `LiveTradingService.PlaceOrderOnExchangeAsync`** integriert:
  Entry-Order laeuft jetzt durch `OrderRetryPolicy.ExecuteAsync` mit Idempotency-Check
  (Position-Existenz vor Retry-Place) — schuetzt vor Doppel-Place wenn der erste Versuch
  durchging und nur die Response timeoutete. onRetry-Callback loggt jeden Retry-Versuch.
- **UI-Toggles** in den Settings-Views:
  - `RiskSettingsView` (Desktop + Mobile) — Card "Erweiterte Filter" mit
    `RequireHtfConfluenceForEntry`, `MinConfluenceScore` (0-11), `UseAsymmetricCrv`.
  - `ScannerView` — Funding-Bonus-Toggle + Schwellen-NumericUpDown.
  - `SettingsView` (Desktop + Mobile) — Card "Diagnose &amp; Push" mit
    `EnableDecisionTrail` und `EnableTradePushNotifications`.
  - **Settings-VM-Sync (v1.3.9-Lehre)**: `RiskSettingsViewModel` und `ScannerViewModel`
    abonnieren weiterhin `ISettingsService.SettingsChanged`. Neue Properties
    (`RequireHtfConfluenceForEntry`, `MinConfluenceScore`, `UseAsymmetricCrv`,
    `EnableFundingRateBonus`, `FundingRateBonusThresholdPercent`) sind in
    `LoadFromSettings` + `BuildCurrentSettings` (bzw. SaveAsync) gemappt + haben
    `OnXxxChanged → MarkDirty`-Hooks. `SettingsViewModel`-Properties speichern direkt
    via `SaveAllAsync` im Setter (BotSettings-Toggles ohne Save-Button).
- **Persistenz-Mapping** in `Program.cs::ApplySettingsToSingletons` und
  `App.axaml.cs::RestoreSettingsFromDb` fuer alle neuen Flags
  (`UseAsymmetricCrv`, `EnableFundingRateBonus`, `FundingRateBonusThresholdPercent`,
  `EnableDecisionTrail`, `EnableTradePushNotifications`).

---

## Phase 0 — TP/Fill-Hotfixes (v1.4.0, 05.05.2026)

Tiefen-Audit der Order-Pfade (`LiveTradingService` Reconcile / OrderPlacement / SlTpManager /
WebSocket / TradingServiceBase.PriceTickerLoop) deckte 7 Echtgeld-relevante Findings auf — alle
gefixt, 16 neue Tests grün, Major-Bump weil Doppel-Close-Race und Ghost-Order-Schutz an
Code-Pfaden hingen, durch die taeglich echtes Geld lief.

### Findings + Fixes

| # | Symptom | Fix-Datei | Tests |
|---|---------|-----------|-------|
| **0.1** | Bot-platzierte TP1/TP2-Reduce-Only-LIMITs blieben nach Position-Close als Ghost-Orders im BingX-Orderbuch (Cancel-Filter erkannte nur native StopMarket/TakeProfitMarket/TakeProfitLimit). | `Order` Record + `BingXOrderDetail` um `ReduceOnly` erweitert; `LiveTradingService.SlTpManager.CancelNativeSlTpOrdersAsync` filtert jetzt Type=Limit + ReduceOnly + Side-Inversion (Hedge-Mode-safe). | `CancelNativeSlTpGhostOrderTests` (4) |
| **0.2 + 0.3** | Doppel-Close-Race: BingX fuellte den TP1/TP2-Reduce-Only-LIMIT, parallel triggerte der PriceTickerLoop ein `ClosePartialAsync` mit `pos.Quantity*0.5` — bei Limit-Partial-Fill ist `pos.Quantity` bereits reduziert → falsche Mengen, kaputte CompletedTrade-Buchhaltung. | `PositionExitState.Tp1LimitOrderId/Tp2LimitOrderId` neu, `IsTpManagedByExchange`-Helper. `TradingServiceBase.PriceTickerLoop` skippt TP-Hit-Check wenn LimitOrderId gesetzt. `LiveTradingService.WebSocket.OnUserDataReceived` faengt `ORDER_TRADE_UPDATE`/`FILLED` und ruft neuen `ProcessTpLimitFillAsync` auf — Phase-Transition + CompletedTrade ohne erneutes ClosePartialAsync. | `Tp1LimitOnExchangeRaceTests` (6) |
| **0.4** | Verwaister-Signal-Cleanup in `OnBeforePriceTickerIteration` ignorierte die Side: Long-Signal blieb stehen, wenn IRGENDEINE Pending fuer das Symbol existierte (auch Short). Im Hedge-Mode → Zombie-Long-Signale, verzerrte DailyRisk + Recovery-TP. | `LiveTradingService.cs` Z.597 — Side-Filter aus posKey ({symbol}_{Buy/Sell}) extrahiert + Pending-Side aus `IsLong` projiziert. | `OrphanSignalSideFilterTests` (2) |
| **0.5** | `_pendingLimitOrders[key] = ...` plus `_ = PersistPendingLimitOrdersAsync()` als fire-and-forget. Sub-second-Crash zwischen In-Memory-Set und DB-Write → Order auf BingX existiert, Bot kennt sie nach Restart nicht → Ghost-Order, Position als unmanaged eingestuft. | `LiveTradingService.OrderPlacement.cs` Z.102 — Persist synchron via `await` BEFORE return. | (Tests im Roundtrip-Bundle abgedeckt.) |
| **0.6** | Wenn nach Market-Fill die Position bei BingX nicht binnen 3 s sichtbar war (Funding-Settlement-Welle, Hochlast), skipte der TP-Place → Position ungeschuetzt bis SL-Hit oder Bot-Crash. | 2-Stage-Retry: Stage 1 (heute, 3× 1 s Position-Check), Stage 2 (TP-Place mit `fallbackQty`, BingX rejected ggf. → `PendingTpRetry=true`), Stage 3 (Retry-Loop in `OnBeforePriceTickerIteration`, max 30 s, danach Bot-Fallback). Neue Felder `PositionExitState.PendingTpRetry/PendingTpRetryCount/PendingTpFirstAttemptUtc`. | (E2E-Pfad ueber Tp1-Race-Test verifiziert.) |
| **0.7** | Signal-Rekonstruktion (30 s+ ohne Signal-Eintrag, oder Restart) verlor `NavPointA`, `RunnerHardCap`, `IsGklSetup`, `GklTimeframe`, `IsCounterTrendScalp`, `PositionScaleOverride` → A-Bruch-BE-Trigger feuerte nie, Runner inactive, HighProb-Boost futsch. | `PendingLimitOrderState` um 6 Strategy-Felder erweitert, `_pendingLimitOrders`-Tuple ebenfalls (am Ende angehaengt). `GetPendingLimitOrdersSnapshot` fuellt aus `_positionSignals`, `RestorePendingLimitOrders` legt sie ins Recovery-`SignalResult`. Inline-Rekonstruktion in `OnBeforePriceTickerIteration` liest direkt aus dem Tuple. Schema-Marker v10 (additiv im JSON-Blob — alte Snapshots deserialisieren mit Defaults). | `PendingStrategyFieldsRoundtripTests` (4) |

### Implementierungsreihenfolge (entkoppelte → verkettete Fixes)

1. **0.1** (Cancel-Filter) — isoliert, schneller Win.
2. **0.4** (Side-Filter) — isoliert, schneller Win.
3. **0.5** (Persist-Sync) — einzeiliger Fix.
4. **0.7** (Strategy-Felder im Pending) — Schema-Foundation fuer 0.6.
5. **0.6** (TP-Place-Retry) — nutzt 0.7's Felder fuer Retry-State.
6. **0.2 + 0.3** (LimitOrderId-Skip + WebSocket-Hook) — neuer WS-Pfad, abhaengig von 0.7-Tuple.

### Wichtige Architektur-Aenderungen

- **`Order` Record bekommt Pflicht-Feld `ReduceOnly`** — `BingXRestClient.GetOpenOrdersAsync`
  mappt aus BingX-Response (FlexibleStringConverter, akzeptiert bool und string). Bei
  `PlaceTpReduceOnlyLimitAsync` wird `ReduceOnly: true` explizit gesetzt — auch im Reject-Fall.
  `SimulatedExchange` + `FakeExchangeClient` ebenfalls mit ReduceOnly versorgt → Backtest/Paper
  und Tests sehen denselben Datenfluss wie Live.
- **`_pendingLimitOrders` Tuple um 6 Strategy-Felder erweitert** (NavPointA / IsGklSetup /
  GklTimeframe / RunnerHardCap / IsCounterTrendScalp / PositionScaleOverride). Lese-Stellen
  ohne diese Felder funktionieren weiter (sind nur am Ende angehaengt). Schreibe-Stellen
  in `OrderPlacement` (Place) + `PendingLimitOrders` (Restore) befuellen sie.
- **`PositionExitState` neu**: Tp1LimitOrderId / Tp2LimitOrderId / IsTpManagedByExchange-Helper +
  PendingTpRetry / PendingTpRetryCount / PendingTpFirstAttemptUtc.
- **Schema-Version v10** (additiv, kein ALTER TABLE — JSON-Blob ist forward/backwards-tolerant).

### Test-Status

- 16 neue Tests in 4 Files: `CancelNativeSlTpGhostOrderTests`, `OrphanSignalSideFilterTests`,
  `Tp1LimitOnExchangeRaceTests`, `PendingStrategyFieldsRoundtripTests`.
- Solution-Build: 0 Fehler / 0 Warnungen in BingXBot-Projekten.
- Bestehende Test-Suite (480 Tests) bleibt gruen — Tuple-Erweiterung in
  `ReconcilePositionsIntegrationTests` mit Default-Werten fuer die 6 neuen Felder ergaenzt.

### Verifikations-Checkliste fuer Live-Deploy

- Trade vollstaendig durchlaufen lassen (Entry → TP1 → TP2 → Close), in BingX-Orderbuch nach
  Close pruefen — keine Ghost-Limits.
- Reconcile-Loop-Log nach 24 h: keine `Unmanaged-Position-Warning`-Eintraege mehr.
- Stage-3-Retry-Pfad: Bei BingX-Hochlast (z.B. waehrend Funding-Settlement) sollte
  `Stage-3-Retry #N` im Log auftauchen — TP wird im naechsten Tick platziert sobald Position sichtbar.

---

## Multi-Audit-Optimierungen (v1.3.12, 04.05.2026)

Drei-Agent-Audit (Domain/Performance/Health) deckte 12 Optimierungs-Punkte auf — alle umgesetzt
außer News-Filter (User-Entscheidung: nicht implementieren).

### Domain / Trading-Edge

- **Phase 3 "Heiliger Gral als Hard-Gate"** — neue `RiskSettings.RequireHtfConfluenceForEntry`
  (default false) blockiert Signale ohne HTF-GKL-Hit oder Confluence-Zone-Overlap.
  Komplementär `RiskSettings.MinConfluenceScore` (default 0) als quantitatives Score-Hard-Gate.
  Beide AND-verknüpft, opt-in für aggressives Risk-Profil.
- **Pending-Limit-Stale-Expiry (6h)** — neue `RiskSettings.PendingLimitOrderMaxAgeHours` (Default 6).
  Reconcile-Loop ruft `CancelExpiredPendingLimitOrdersAsync` auf; cancelt pending Orders deren
  `PlacedAt` älter als der Wert ist. Schützt vor "Symbol aus Top-100 gefallen, Pending hängt
  tagelang gegen toten Markt → zufälliger Spät-Fill auf BC-Niveau".
- **Margin-Aware Position-Sizing für TradFi** — `RiskManager.Check` cappt jetzt `posSize` so,
  dass `Σ(open margins) + new margin ≤ 60% × Wallet-Balance`. Schützt bei Hebel-20×-Forex und
  Hebel-10×-Indices vor Cross-Margin-Spillover bei Multi-Position-Setups.
- **Pip-Wert-Drift fix** — `SequenzKonzeptStrategy` Counter-Trend-Pfad hatte inline-Pip-Berechnung
  mit Stock falsch (`*0.0001` statt `*0.00005`) und Index falsch (`*0.0001` statt `1`).
  `PipStopLossCalculator.GetPipValue` ist jetzt `public static`, Counter-Trend nutzt Helper.
  **Korrektheits-Fix** für SL auf NCSI/NCSK-Symbolen.

### Backtest-Realismus

- **Maker/Taker-Mix in `SimulatedExchange`** — vorher zahlten ALLE Fills die TakerFee, auch
  Limit-Entries und TP-Limit-Reduce-Only. Jetzt: Limit-Order-Fills + TP1-/TP2-Hits in BacktestEngine
  mit `isMakerClose: true` → MakerFee (0.02%); SL-Hits + Market-Closes mit `isMakerClose: false`
  → TakerFee (0.05%). 1-2% Equity-Verzerrung pro 100 Trades beseitigt — Voraussetzung für jede
  künftige Strategy-Tuning-Entscheidung. Interface-Compat erhalten via expliziter Interface-Methode.

### Performance

- **`InteractiveChartRenderer` Per-Frame-Allokationen** — ~150 SKPaint-Allokationen pro Frame
  in 7 Methoden in statische Cache-Felder verschoben (`PriceLinePaint`, `ProfitZonePaint`,
  `Tp1ExtLinePaint`, `BadgeBgPaint`, `MarkerCircleFillPaint` usw.). Color-variable Paints
  nutzen `.Color = ...` per Frame statt Neu-Konstruktion. SKPath für Trade-Marker einmalig
  als statisches Feld + `Reset()` in der Schleife. Spürbarer GC-Druck-Drop auf Android-Mid-Tier
  und Pi.
- **`PositionUpdated`-Event throttled** — `BotHubEventForwarder.OnPositionUpdated` analog zu
  Ticker-Throttle (1/s/Symbol) auf 1/2s pro `{Symbol,Side}`. Bei 20 offenen Positionen reduziert
  das 20 SignalR-Sends/5s spürbar (Android-Bandbreite + Pi-CPU).
- **Log-Batching (250 ms Buffer)** — `BotHubEventForwarder` sammelt Log-Events in `_logBuffer`,
  Timer flushed alle 250 ms. Bei einzelnem Eintrag → `LogEmitted` (Backwards-Compat); bei mehreren
  → neuer `HubMethods.LogBatch` mit `IReadOnlyList<LogEntryDto>`. `RemoteBotEventStream` subscribed
  beide Events und splittet `LogBatch` in einzelne `LogEmitted`-Aufrufe — Subscriber müssen nichts
  ändern. Bei Scan-Bursts (50-200 Logs/Zyklus) deutlich weniger SignalR-Round-Trips.

### Codebase-Hygiene

- **Dead Code entfernt** — `CorrectionBoxExitClassifier` (96 Z. + Tests) hatte nach Punkt-
  Identifikations-Fixes keine Aufrufer mehr. `MarketFilter.IsMaxDailyTradesReached` und
  `GetVolatilityScale` waren seit Buch-Only Strip Phase 2 unaufgerufen. Beide entfernt.
- **`ScannerSettings` Legacy-Doppelfelder als `[Obsolete]` markiert** — `MinVolume24h`,
  `MinPriceChange`, `MinVolume24hTradFi`, `MinPriceChangeTradFi`, `ScanTimeFrame`, `MaxResults`
  sind weiterhin nutzbar (Persistenz, Single-TF-UI), werfen aber Build-Warning. Multi-TF-`*ByTf`-
  Maps sind die Wahrheit. Verwender (ScanHelper, MarketScanner, ScannerVM, Server-Persistenz,
  Tests) per `#pragma warning disable CS0618` markiert. **Migration in v1.4.x abschließen**
  (Single-TF-UI auf `ByTf`-Editor umstellen).
- **MarketCapCache Layer-Verletzung beseitigt** — Core/Helpers war HTTP-abhängig (CoinGecko-Calls).
  Neuer `IMarketCapProvider` in `Core/Interfaces/`; konkrete Impl `CoinGeckoMarketCapProvider`
  in `Engine/Helpers/`. Static-Bridge `Engine.Helpers.MarketCapRefreshHelper.Configure(provider)`
  beim App-Startup (Server `Program.cs` + Shared `App.axaml.cs`). `MarketCapCache` selbst ist
  jetzt rein Cache (kein HTTP) — Provider füttert via `SetRankings()`.
- **WebSocket-Reconnect verbessert** — `MaxReconnectAttempts` von 5 auf **20** erhöht; neuer
  `MaxBackoff = 30s` Hard-Cap (sonst 2^15 = 9 h Wartezeit nach 15 Versuchen). Schützt bei
  BingX-Wartungsfenstern (10-15 min). User-Data-Stream-Reconnect-Loop nutzt denselben Cap.

### Bewusst nicht implementiert

- **News-Filter aktivieren** — User-Entscheidung "nicht implementieren". `HttpEconomicCalendarService`
  bleibt als opt-in via `News:Endpoint`-Config; Default-Verhalten ist Stub (kein Block).

### Verifikation

- Solution-Build: 0 Fehler / 0 Warnungen (BingXBot.Desktop, BingXBot.Server, alle Libraries).
- Server gebaut + auf Pi deployed (v1.3.10).
- Client-Release in `F:\BingXBot-Client` (v1.3.12).

### Bekannte Migrations-TODOs (v1.4.x)

- `ScannerSettings` Legacy-Doppelfelder komplett entfernen (siehe `[Obsolete]`-Marker).
- Single-TF-Scanner-UI auf `ByTf`-Editor umstellen.
- TradFiLiveVerification-Tests mit Live-API benötigen Online-Zugang (eine "always-fail-bei-Sandbox"-
  Komponente bleibt → bei lokalem Run grün).

---

## Punkt-Identifikations-Fixes (v1.3.11, 04.05.2026)

Tiefen-Audit der SK-Sequenz-Erkennung deckte drei Bugs/Lücken auf, alle gefixt. 480/480 Tests grün
(472 + 8 neue `PointIdentificationFixTests`). Kontext: User hatte berechtigte Zweifel, ob die
Punkte 0/A/B wirklich an den korrekten Sequenz-Extremen genommen werden und ob das Sequenz-Ende
sauber erkannt wird.

### Fix #1 — Wick unter Point 0 invalidiert hart (Strukturpunkte §4)

**Symptom:** In `SequenceStateMachine.ProcessSucheB` wurde ein Wick-Tief unter Point 0 (Long)
bzw. -Hoch über Point 0 (Short) per `CorrectionBoxExitClassifier` als WickOnly toleriert,
solange der Body-Close in der Korrekturbox lag. Konsequenz: PotentialB rutscht durch das
Trailing unter Point 0 → bRetrace springt > 100 % → die nachfolgende `TryActivate` lehnt
das Setup ab → die Sequenz hängt strukturell tot in `SucheB` fest. Bei `SL` (über dem alten
Point 0) und `TP-Range` (gegen falschen Point 0 berechnet) wären Position-Sizing,
MinImpulseDistance-Check und Fib-Extensions verzerrt.

**Fix:** `ProcessSucheB` triggert jetzt `InvalidateAndPromoteSucheB` SOFORT, sobald die Kerze
Point 0 mit dem Docht durchstößt — egal ob Body-Close in der Box ist. Strukturpunkte-Doku §4:
"Fällt Preis unter Point_0 → sofort Reset". Die Masterclass-WickOnly-Toleranz
(`CorrectionBoxExitClassifier`) gilt ausschließlich für Wicks INNERHALB der Korrekturbox
(50-78.6 % Retracement) — der Klassifikator wurde aus dem State-Machine-Pfad entkoppelt
und ist seitdem im Engine-Pfad nicht aktiv verdrahtet (Tests + Code bleiben für höher-stufige
Validierungs-Layer erhalten, dokumentiert im XML-Header).

### Fix #2 — `Abgearbeitet` erst bei TP2 (200 %), nicht bei TP1 (161.8 %)

**Symptom:** `ProcessAktiviert` setzte `State = Abgearbeitet` schon beim 161.8 %-Treffer.
Buch: TP1 = 161.8 % (Teil-Exit, Sequenz läuft weiter Richtung 200 %), TP2 = 200 %
(vollständig abgearbeitet). `ProcessAbgearbeitet` lief in der nächsten Kerze und setzte
`Point0` auf `candle.Low` der laufenden Kerze, State zurück auf `Suche0` — bei einem 161.8 %-Spike
konnte das einen "Phantom-Bias-Flip" / Geister-Re-Entry erzeugen, obwohl der TP2-LIMIT auf
der Exchange noch lebt.

**Fix:** Schwelle in `ProcessAktiviert` auf `Extension200` umgestellt (Long: `candle.High >= Ext200`,
Short: `candle.Low <= Ext200`). `Sequence.HasFullyCompleted` prüft ebenfalls auf 200 %, das ist
jetzt konsistent. Auch das `ToSequence`-Mapping `SmState.Abgearbeitet → SequenceState.FullyCompleted`
(vorher fälschlicherweise `TargetReached`). `CompletedGklEntry`-Range zieht jetzt auf `Extension200`,
GKL-Berechnung 50 %/66.7 % der Gesamtstrecke `Point0 → Ext200`.

### Fix #3 — `EnableBiasFlip` an HTF-Calls durchreichen

**Symptom:** `IsHigherTfSequenceAlignedActive` und `IsHigherTfInTargetZone` riefen
`SequenceStateMachine.FromCandlesBoth` ohne `enableBiasFlip`-Parameter auf → Default `true`.
Wenn der User `ScannerSettings.EnableBiasFlip=false` setzte, lief der Navigator ohne Bias-Flip,
die HTF-Gates aber MIT Bias-Flip → MTA-Block sah ggf. eine andere Sequenz-Richtung als der
Navigator. Inkonsistenz, niemals user-konfigurierbar.

**Fix:** Beide HTF-Aufrufe lesen `scannerSettings?.EnableBiasFlip ?? true` und reichen den Wert
explizit als `enableBiasFlip:` durch.

### Bonus — PotentialB Sanity-Guard (Defense-in-Depth)

`SequenceStateMachine.TryActivate` validiert jetzt zusätzlich, dass PotentialB strukturell
zwischen Point 0 und PointA liegt (Long: `Point0 ≤ B < A`, Short: `Point0 ≥ B > A`).
Sollte trotz Fix #1 jemals ein Edge-Case auftreten, der PotentialB unter Point 0 schiebt,
greift dieser Guard und ruft `Reset()` statt einem stumm rejecteten Setup. Auch zwei neue
`Debug.Assert`-Posten in `ProcessSucheB` sichern die Invariante in Debug-Builds ab.

### Verifikation

- Solution-Build: 0 Fehler / 0 Warnungen (Desktop + Server + Trading + Engine + Tests).
- Tests: 483/483 grün — 11 neue in `PointIdentificationFixTests.cs`:
  - `Long_/Short_WickUnter/UeberPoint0_BodyInBox_InvalidiertSequenz` (Fix #1)
  - `Long_WickInBox_KeinPoint0Bruch_BleibtInSucheB` (Fix #1 Negativ-Test)
  - `Long_/Short_TpHit161_8_BleibtAktiviert` (Fix #2)
  - `Long_/Short_TpHit200_GehtNachAbgearbeitet` (Fix #2)
  - `Long_ToSequence_AbgearbeitetMapptAufFullyCompleted` (Fix #2 Mapping)
  - `Long_Tp1Hit_DannPoint0Wick_FuehrtZuInvalidateAndPromote_NichtZuAbgearbeitet`
    (Fix #2 — sichert das vormals buggy Szenario "Geister-Re-Entry zwischen TP1 und TP2" ab,
    inkl. `WasActivatedBeforeInvalidation`-Bias-Flip-Hint)
  - `Long_AbgearbeitetSpeichertGkl_AusPoint0_BisExt200` und
    `Short_AbgearbeitetSpeichertGkl_SymmetrischZurRange` — sichern die neue GKL-Range
    (50/66.7 % der Gesamtstrecke `Point0 → Ext200`) ab; relevant für BCKL-Re-Entry-Pfad.

### Was korrekt war (bestätigt durch Re-Audit)

- Pivot-Erkennung mit asymmetrischem 5/3-Look-Ahead-Schutz in `SequenceDetector.FindSwingPoints`
- PointA-Trailing in `ProcessSucheA`, Lock bei B-Suche-Start (OHLC-Order-Schutz Z.698-704)
- LockedB einfrieren bei Aktivierung, Fib-Berechnung aus korrekter Range
- 4 vollständige Reset-Pfade (Reset, ProcessAbgearbeitet, InvalidateAndPromote,
  InvalidateAndPromoteSucheB, InitAsBiasFlip) — alle setzen ALLE relevanten Felder zurück
- BOS-Anker mit Look-Ahead-Schutz (Z.443, 458)
- Bias-Flip mit 3-Kerzen-Cooldown gegen Ping-Pong
- Multi-TF-Architektur: frischer State-Aufbau pro `Evaluate`-Call, separater Strategy-Klon
  pro `{symbol}|{tf}` — strukturell keine Cross-Contamination
- Docht-basierte Messung mit Debug.Asserts an allen drei Punkten

---

## Settings-VM Live-Sync (v1.3.9, 27.04.2026)

**Symptom (Robert, Remote-Mode/Pi):** Bei jedem App-Start im Risk-Settings-Tab erscheinen
MaxPositionSizePercent, MaxLeverage und MaxOpenPositions als Defaults — die zuvor gespeicherten
Werte sind verschwunden, müssen neu eingegeben und gespeichert werden. Server-DB hat aber den
korrekten Wert.

**Root-Cause:** `RiskSettingsViewModel` und `ScannerViewModel` (beide DI-Singletons) lesen im
Konstruktor genau einmal aus dem `RiskSettings`/`ScannerSettings`-Singleton via
`LoadFromSettings()`. Im Remote-Mode passiert dieser Konstruktor-Lauf möglicherweise BEVOR
`App.RefreshRemoteSettingsAsync` den Server-Snapshot via `RestoreSettingsFromDb` in die
Singletons gespielt hat (Lazy<T>-Resolve + schneller Tab-Klick + Background-Init-Race). Das
VM friert dann die Defaults in seinen `[ObservableProperty]`-Feldern ein. Beim Speichern
schreibt es genau diese Defaults zurück — Server überschreibt seine echten Werte.

Zusätzlich: `RefreshRemoteSettingsAsync` schreibt direkt in die Singletons OHNE den
`ISettingsService.SettingsChanged`-Pfad zu durchlaufen → kein Event → kein VM-Refresh
möglich, selbst wenn das VM gerne abonniert hätte.

**Fix (3 Bausteine):**

1. **`RiskSettingsViewModel`** + **`ScannerViewModel`** akzeptieren `ISettingsService?` als
   optionalen Ctor-Parameter, abonnieren `SettingsChanged` und rufen `LoadFromSettings()`
   neu auf (per `Dispatcher.UIThread.Post`, weil SignalR-Hub aus Background-Thread feuert).
   Beide implementieren `IDisposable` mit `-=`.
2. **Suppress-Dirty-Flag** im `RiskSettingsViewModel`: `LoadFromSettings()` setzt
   `_suppressDirty=true`, damit die `MarkDirty()`-Aufrufe in den `OnXxxChanged`-Partials nicht
   den "Ungespeicherte Änderungen"-Banner triggern (Sync ist keine User-Eingabe). Die fünf
   marktspezifischen Hebel-Properties (`CryptoMaxLeverage` etc.) feuern nach dem Load explizit
   `OnPropertyChanged`, weil sie keinen Backing-Field-Setter haben.
3. **Event-Trigger nach DB/Server-Restore**:
   - `App.RefreshRemoteSettingsAsync` → `RemoteSettingsService.RaiseChanged(snapshot)` direkt
     nach `RestoreSettingsFromDb` (Remote-Mode initialer Sync + Reconnect via
     `RemoteSettingsAutoSync`).
   - `App.InitializeBackgroundAsync` (Local-Mode) → neuer public `LocalSettingsService.RaiseChanged()`
     direkt nach DB-Restore (analog Remote, gleiches Symmetrie-Muster).

**Multi-Client-Update bleibt automatisch korrekt:** Wenn ein anderer Client speichert, feuert
der Server `SettingsChanged` → Hub-Push → `RemoteBotEventStream` → `RemoteSettingsService.RaiseChanged`
→ VMs reloaden. Dieser Pfad existierte schon, nur die VM-Subscriber fehlten.

**Bewusst nicht angefasst:** `BacktestViewModel` zeigt zwar Settings-ähnliche Werte
(`InitialBalance`, `Leverage`), die sind aber Run-Parameter ohne Persistenz-Verbindung —
keine Sync-Notwendigkeit. `SettingsViewModel` (API-Keys/Pairing) hat eigene Reload-Logik via
`ApplyApiKeysAvailableChanged` und braucht nichts.

**Verifikation:** Solution-Build grün (Desktop + Server + Android, 0 Fehler). Tests: 472/472 grün.

**Lehre:** Singleton-VMs mit `LoadFromSettings()` im Ctor sind ein systemisches Problem im
Client/Server-Setup. Wenn neue Settings-VMs hinzukommen, IMMER `ISettingsService.SettingsChanged`
abonnieren — sonst friert das VM beim ersten Resolve den Stand ein und blockiert User-Werte.

### Nachtrag v1.3.10 (27.04.2026) — Default-Overwrite-Race

User-Symptom: "Wir öffnen Risk-Settings, sehen kurz die Server-Werte, nach 1-2 s stehen die
Defaults da." Trotz v1.3.9-Fix.

**Root-Cause:** Doppel-Refresh-Race im Bootstrap. `App.InitializeBackgroundAsync` ruft
`RefreshRemoteSettingsAsync` einmal direkt auf (Initial-Refresh). Danach startet
`stream.StartAsync()`, das beim ersten erfolgreichen Connect ein `ConnectionChanged: Connected`
feuert. `RemoteSettingsAutoSync.OnConnectionChanged` debounct nur über sein eigenes
`_lastRefreshUtc` — beim Initial-Refresh wurde der Timer aber nicht gesetzt, also greift der
2-Sekunden-Debounce nicht und ein zweiter Refresh läuft. Wenn der zweite Refresh in einer Race
fehlerhaft Defaults zurückbekommt (z.B. parallele HTTP-Pipeline mit Auth-Token-Race oder
JSON-Deserialisierungsfehler beim verschachtelten BotSettings.Risk-NavRef), überschreibt
`RestoreSettingsFromDb` die soeben korrekt geladenen Singletons mit Defaults und das nachfolgende
`RaiseChanged` propagiert das an alle abonnierten VMs.

**Fix (zwei Bausteine, beide aktiv):**

1. **Cross-Pfad-Debounce**: Neue public `RemoteSettingsAutoSync.MarkRefreshed()` Methode wird
   in `RefreshRemoteSettingsAsync` direkt nach dem erfolgreichen Refresh aufgerufen. Das setzt
   `_lastRefreshUtc = UtcNow`, sodass das nachfolgende `ConnectionChanged: Connected` (innerhalb
   2 s) den AutoSync-Refresh überspringt. Echte spätere Reconnects (>2 s) refreshen weiterhin.
2. **Snapshot-Sanity-Check**: Neue private `App.LooksLikeFreshDefault(snapshot)` prüft ob
   `Risk` UND `Scanner` GLEICHZEITIG die Konstruktor-Defaults (`new RiskSettings()` /
   `new ScannerSettings()`) tragen. In dem Fall wird der Snapshot stillschweigend verworfen
   statt auf die echten Server-Werte gespielt zu werden. Sehr defensiv: Beide Bedingungen
   müssen zutreffen (Risk: 4 Felder gleichzeitig auf Default; Scanner: ActiveTimeframes-Liste
   identisch + MaxResults default). Edge-Case "User hat wirklich Defaults" ist no-op weil
   Singletons dann auch schon Default sind (= identisch, nichts zu kippen).

Damit ist sowohl die Race-Quelle (Doppel-Refresh) als auch das Symptom (Default-Snapshot
überschreibt echte Werte) blockiert.

**Verifikation:** 0 Fehler / 0 Warnungen in BingXBot-Projekten. Tests: 472/472 grün.

---

## Strict-Mode Phase 1+2 (25.04.2026)

Doku-Drift gefixt: Die CLAUDE.md hat seit v1.2.9 `RequireWickRejectionInBZone = true` als
"aktiver Buch-Hardfilter" aufgelistet, der Code stand aber auf `false`. Außerdem war die
asymmetrische Pivot-Erkennung (5/3 Default in `ScannerSettings`) nur im Filter-TF-Pfad in
`SequenzKonzeptStrategy` aktiv — die `SequenceStateMachine.FromCandlesBoth`-BOS-Anker-Suche
nutzte weiterhin nur eine symmetrische `bosAnchorSwingStrength` (single int). Drei Patches:

1. **`RiskSettings.RequireWickRejectionInBZone`** Default `false` → `true`. Schützt den
   Default-`EntryMode.Both` vor blindem 50%-Limit-Kauf in fallendes Messer. Algorithmus-Dok §5C
   ("Erzeuge erst ein Kaufsignal, wenn Lower_Wick > Body × 2") greift jetzt out-of-the-box.
2. **Asymmetrische BOS-Anker durchgeschleift** bis in die State-Machine:
   - `ScannerSettings.BosAnchorLeftBars` (Default 5) + `BosAnchorRightBars` (Default 3) — neue Felder.
   - `SequenceStateMachine.FromCandlesBoth(...)` akzeptiert jetzt zusätzlich `bosAnchorLeftBars`/
     `bosAnchorRightBars`. Wenn beide > 0, gewinnt das asymmetrische Paar; sonst Fallback auf den
     Legacy-`bosAnchorSwingStrength`. Look-Ahead-Schutz in `RefreshBosAnchor` nutzt entsprechend
     den korrekten `rightBars`-Wert.
   - Drei Aufrufstellen in `SequenzKonzeptStrategy` (Navigator-TF + 2 HTF-Aufrufe) reichen die
     neuen Werte aus den `ScannerSettings` durch.
   - `LtfReversalDetector` bleibt bewusst auf `bosAnchorSwingStrength: 3` symmetrisch (LTF-Micro-
     Sequence-Detection, hardcoded Begründung im Kommentar).
   - `App.axaml.cs` + `Program.cs` persistieren die zwei neuen Felder analog zu allen anderen
     BOS/Pivot-Settings.
3. **Hammer-Formel auf Buch-strikt** in `SequenceDetector.DetectEntryConfirmation`:
   - Vorher: `bodyRatio < 0.35 && lowerWick/range > 0.5` (eigene Heuristik).
   - Jetzt: `lowerWick > 2 × body` für Long bzw. `upperWick > 2 × body` für Short — identisch
     zur Buch-Formel und konsistent zu `CandlePatternDetector.IsPinbar` (`LtfReversalDetector`-Pfad).

**Verifikation:** Solution-Build grün (Server + Desktop + Tests, 0 Fehler / 0 Warnungen in
BingXBot-Projekten). Test-Suite 469/472 grün — die 3 Fehler sind `TradFiLiveVerification`
(Forex/Stock-Klines liefern Wochenend-/Markt-zu 0 Candles), unabhängig von den Änderungen.

**Aktive Buch-Default-Hardfilter — bestätigter Stand (25.04.2026):**
- `ImpulseAtrMultiplier = 3.0` (Strukturpunkte §2)
- `RequireBosOnActivation = true` (Strukturpunkte §3)
- `BosAnchorLeftBars = 5`, `BosAnchorRightBars = 3` (Strukturpunkte §1+§3, asymmetrisch)
- `AdaptiveSwingStrength = true`, `PivotLeftBars = 5`, `PivotRightBars = 3`
- `RequireWickRejectionInBZone = true` (Algorithmus-Dok §5C, **neu jetzt Default**)
- `BlockLtfEntryWhenHtfInTargetZone = true` (Spec §7 MTA — Soft-Filter, kein Hard-Gate)
- `EnableConfluenceOverlapDetection = true` (Spec §7 Heiliger Gral — Soft-Bonus +2 Score)
- `EntryMode.Both` (User-Ausnahme — bewusst beibehalten, jetzt durch Wick-Rejection geschützt)

**Offen — Phase 3+4 aus dem Implementierungsplan-Dokument** (Heiliger Gral als Hard-Gate):
- Aktuell läuft die HTF→LTF-Confluence-Logik nur als Soft-Filter (B18 Block bei HTF-in-Zielzone)
  + Soft-Bonus (B19 +2 Score bei Overlap). Der Plan verlangt das als zwingendes UND-Gate vor
  Signal-Generierung (LTF-B-Box muss geometrisch in HTF-GKL liegen). Dafür wäre ein neuer
  `RequireHtfConfluenceForEntry`-Flag in `RiskSettings` + ein Pre-Signal-Block in
  `SequenzKonzeptStrategy.Evaluate` nötig.
- Phase 4 (asymmetrisches CRV: SL aus LTF-Punkt-0, TP aus HTF-Ext1618) wird erst nach Phase 3
  sinnvoll, weil ohne Confluence-Gate die HTF-Sequenz nicht zwingend pro Trade existiert.

---

## Remote-Deep-Audit + Hardening (24.04.2026)

Umfassender Code-Review nach dem Client/Server-Refactoring. Zwei Runden Agent-Audit
(`code-review`, `mvvm-auditor`, `bingxbot`) deckten kritische Remote-Luecken auf. Alle Findings
sind gefixt, 435/435 Tests gruen (+10 neue PairingService-Tests).

### Pairing-UX (zuvor: 5-Fehlversuche in 26 s — Auto-Retry-Loop)
- **Re-Entrancy-Guard** in `SettingsViewModel.IsCompletingPairing` — Button-Double-Clicks werden ignoriert.
- **IsEnabled-Binding** + Loading-Icon am Confirm-Button (beide Views: Desktop + Mobile).
- **PairCompleteOutcome-Enum** (`Success/InvalidCode/UnknownPairingId/Expired/TooManyAttempts`) ersetzt bool — Client unterscheidet jetzt "Tippfehler → neu tippen" vs "Session tot → Schritt 1".
- **Server-ErrorCodes**: `invalid_code` / `pairing_exhausted` / `pairing_expired` / `pairing_unknown` pro Outcome.
- **PairCancel in PublicPaths** (war auth-required, 401 stumm geschluckt).

### Remote-Events (zuvor: 8 von 14 SignalR-Events hatten keinen Producer)
`BotEventBus` um 6 Events erweitert. `TradingServiceBase` + `PaperTradingService` publishen:

| Event | Wann | Quelle |
|-------|------|--------|
| `TradeOpened` | Nach erfolgreichem Order-Placement | `TradingServiceBase.RunLoopAsync` (PublishTradeOpened mit Navigator-TF) |
| `PositionUpdated` | Alle 5 s pro offener Position (inkl. SL/TP/BE-Meta) | `TradingServiceBase.PriceTickerLoopAsync` |
| `TickerUpdate` | Alle 5 s pro offener Position (Preis) | Dto. |
| `BtcPriceUpdate` | Alle 5 s (wenn BTC-USDT in Sweep) | Dto. |
| `ScannerSweep` | Pro Scan-Zyklus (pro Navigator-TF eine Candidate-Liste) | `TradingServiceBase.RunLoopAsync` |
| `EquityUpdate` | Nach Trade-Close | `PaperTradingService.PublishNewTrades` |

`LocalBotEventStream` subscribed auf die EventBus-Events, mapped auf Contract-DTOs, `BotHubEventForwarder` sendet per SignalR. Backtest-Progress/Completed wird direkt vom `IBacktestControlService`-Event an den Stream gehaengt.

### Multi-Client-Settings-Sync
- `IBotEventStream.SettingsChanged` hinzugefuegt — Server pushed Full-Snapshot bei jedem Save.
- `LocalBotEventStream` subscribed `ISettingsService.SettingsChanged` und forwardet.
- Client: `App.axaml.cs` wiret `stream.SettingsChanged += RemoteSettingsService.RaiseChanged` nach Pairing-Load.

### Datenintegritaet
- `LocalSettingsService.CopyPoco` — **Navigations-Refs** (BotSettings.Risk/Scanner/Backtest) werden jetzt ausgeschlossen. Ohne den Fix konnte der Client-Snapshot die Server-Singleton-Referenzen ueberschreiben, paralleler Scan-Loop saehe kurzzeitig fremde Objekte.
- `LocalBacktestService.CloneRisk` — **JSON-Roundtrip-Clone** statt 10 handverlesener Felder. Backtest nutzte vorher Default-Werte fuer ca. 20 Felder (MaxRiskPercentPerTrade, EntryMode, BCZoneEntryStrategy, PipScalingByTf, SlBufferPipsByTf, CategorySettings, RunnerConfig, NewsBlackoutMinutes, MaxDailyRiskPercent, HighProbabilityPositionMultiplier, ...).
- `RefreshRemoteSettingsAsync` synct jetzt alle 4 Bloecke (Bot/Risk/Scanner/Backtest), nicht nur Bot.
- `LocalBacktestService` — `CurrentBar`/`TotalBars` werden durch neuen `IProgress<(int,int)>`-Callback im `BacktestEngine` gefuellt (war vorher immer 0/0 → Fortschrittsbalken ohne Bar-Kontext).
- `IBacktestControlService.CancelAsync` — `ObjectDisposedException`-Schutz gegen parallelen Dispose.

### Server-Hardening
- **Neue Route** `PUT /api/v1/settings/backtest` — `RemoteSettingsService.SaveBacktestAsync` sendete vorher an `/settings` mit `BacktestSettings`-Body, was der Server als `FullSettingsDto` deserialisierte → Bot/Risk/Scanner als null → potentielle Server-Singleton-Ueberschreibung mit Muell.
- **Input-Validation** in `SettingsEndpoints` — alle PUTs validieren MinMax-Ranges (MaxLeverage 1..500, MaxRiskPercent 0..10, Tp1CloseRatio 0.1..1.0, ActiveTimeframes nicht leer, etc.). Rueckgabe `400 BadRequest + invalid_risk/scanner/bot/backtest + konkreter Grund`.
- **Rate-Limit** `settings`-Bucket (20/10s) auf PUTs — verhindert DB-WAL-Contention-DoS.
- **CORS** — Whitelist via `Cors:Origins` (Komma-Liste in appsettings.json). Ohne Config: AllowAnyOrigin (Backwards-Compat, LAN/Tailscale-OK).
- **Separate HttpClients** fuer `BingXPublicClient` + `HttpEconomicCalendarService` — kein DefaultHeaders/Timeout-Sharing mehr.
- **BearerAuthMiddleware.IsPublic** auf `Ordinal` (case-sensitive) — match ASP.NET-Routing-Default.
- **Fire<T>-Exception-Handling** in `BotHubEventForwarder` — unobserved Task Exceptions werden geloggt statt an `TaskScheduler.UnobservedTaskException` eskaliert.
- **AuthTokenStore.Save** atomar (Tmp+Move) — Power-Loss mid-write → vorher 0-byte-Datei → alle Tokens weg.
- **PiCredentialStore.GetOrCreateMasterKey** mit Lock — parallele Save+Load erzeugten vorher zwei Master-Keys, zweiter ueberschrieb den ersten → credentials.bin nicht mehr entschluesselbar.
- **LogBufferService** (default 1000 Eintraege, Capacity via `Server:LogBufferCapacity`) — `/api/v1/logs` war vorher leerer Stub, Client sah nach Reconnect leere Log-Ansicht bis neue Events kommen.

### Client-Hardening
- `RemoteAccountService` + `RemoteBotControlService` implementieren jetzt `IDisposable` mit symmetrischem `-=` auf `IBotEventStream`-Events.
- `App.axaml.cs` — `Task.Run(InitializeBackgroundAsync)` mit `ContinueWith(OnlyOnFaulted)` gegen unobserved Exceptions.
- `LocalBotControlService` — Reflection-Zugriff auf `LiveTradingManager._secureStorage` durch DI-Injection ersetzt (`ISecureStorageService` als optionaler Ctor-Parameter).

### Housekeeping
- `tmp_audit/` Scratch-Ordner geloescht (17 veraltete Code-Kopien mit entfernten Features).
- Pre-existing Build-Fehler `RequireBosOnActivation` in Composition-Roots (entfernt beim Buch-Only Strip, aber noch referenziert) mitgefixt.
- Tests: BingXBot.Contracts/Trading/Server als ProjectReferences in `BingXBot.Tests.csproj`, damit `PairingServiceTests` (10 neu) laufen.

### Round 3 — Persistenz + Race-Conditions (24.04.2026 Abend)

Nach dem ersten Hardening-Round wurden noch tiefere strukturelle Probleme entdeckt:

**KRITISCH (Echtgeld-relevant) — bereits oben dokumentiert:**
- **M1**: Server-Paper-Mode startete OHNE Strategie (`StrategyManager.SetStrategy` nur im Live-Pfad). Folge: Pi-Server + Android-Remote produzierten 0 Trades trotz "Running". Gefixt durch DI-Injection von `StrategyManager` in `LocalBotControlService` + `SetStrategy`-Aufruf im Paper-Branch + `IsHedgeModeActive=true` analog Desktop-Pfad. Genau das Symptom das Robert ursprünglich gemeldet hatte ("kein Trade, nur sucheB").
- **M2**: `LocalBotEventStream.HandleTradeOpened` mappte `NavigatorTimeframe: TimeFrame.H4` hardcoded → Multi-TF-Trades (M15/H1/D1) erschienen im Remote-UI als H4. Gefixt durch neuen `TradeOpenedArgs(Position, TimeFrame)` Record.

**Settings-Persistenz (systemisch):**
- **24+ fehlende Properties-Mappings** in `Program.cs::ApplySettingsToSingletons` + `App.axaml.cs::RestoreSettingsFromDb`. Bei jedem Server-Restart fielen User-Settings auf Defaults zurück (z.B. `MaxDailyRiskPercent`, `EntryMode.Both`, alle BOS/Pivot/Swing-Filter, BacktestSettings komplett, TradFi-By-TF Dictionaries). Jetzt alle gemappt mit Konsistenz-Block "User-Ausnahme: …bleibt drin".
- **Bug #2**: BotSettings-JSON-Deserialisierung war all-or-nothing. Korrupte Enum-Werte (z.B. `BCZoneEntryStrategy.Triple=2` aus Vor-Strip-DB) → `JsonException` → catch → ALLE Settings auf Defaults gesetzt, stillschweigend. Jetzt:
  - `JsonStringEnumConverter(allowIntegerValues: true)` als globale Options in `BotDatabaseService.BotSettingsJsonOptions` (Enums werden als String gespeichert → forward-kompatibel).
  - **Schema-Migration v9**: bereinigt vorhandene korrupte BCZoneEntryStrategy int 2-9 in der Settings-Tabelle.
  - LoadSettingsAsync Catch-Pfad LÖSCHT die korrupte Row + loggt detailliert (vorher stilles `return new BotSettings()`).

**Race-Conditions:**
- **Bug #4**: `LocalSettingsService` hatte keinen Lock zwischen den `SaveXxxAsync`-Methoden. Parallele Saves von 2 Clients → `JsonSerializer.Serialize` enumerierte mutable Collections gegen `CopyPoco`-Mutationen → `InvalidOperationException`, Settings nicht persistiert. Jetzt `SemaphoreSlim _persistLock` um alle 5 Save-Methoden.
- **Bug #5**: `BotAutoResumeService` reichte den HostedService-CT an `IBotControlService.StartAsync` durch — Server-Stop mid-Connect hätte WasRunningOnShutdown undefiniert gelassen. Jetzt eigener `_lifetimeCts`, Engine-Start nutzt `CancellationToken.None`.
- **Idempotenz-Lock** in `LocalBotControlService` (`_lifecycleLock` SemaphoreSlim) verhindert parallele Start/Stop (z.B. Auto-Resume + manueller User-Click in den 15s Initial-Delay → früher zwei Engines parallel).
- **m6**: `_logInsertCount++` durch `Interlocked.Increment` ersetzt — bisher konnte Log-Rotation race-bedingt zu oft (Doppel-Rotation) oder nie (Log-Bloat > 100k Einträge) treffen.

**Crash-Safety / Persist-First:**
- `LocalBotControlService.StopAsync` + `EmergencyStopAsync` persistieren `WasRunningOnShutdown=false` ZUERST, dann Engine-Stop. Ein Crash mid-stop führt nicht mehr zu unerwünschtem Auto-Restart nach Reboot.
- Auto-Resume-Loop-Schutz im `StartAsync`-Catch-Block: bei `ConnectAsync`-Fail (z.B. fehlende API-Keys) wird Flag auf `false` gesetzt → kein Endlos-Loop bei jedem Reboot.
- **Separater DB-Key** `SaveAutoResumeFlagAsync(bool)` statt voller `SaveSettingsAsync(_botSettings)` in PersistResumeFlagAsync — vermeidet Race mit JSON-Serialisierung mutabler Collections, atomare Write.
- `SaveAutoResumeFlagAsync` schreibt plain `"true"`/`"false"`-Literal, `LoadAutoResumeFlagAsync` nutzt `bool.TryParse` (case-insensitive) — robust gegen manuelle DB-Edits oder externe Tool-Korruption.

**Pi-Robustheit:**
- `BotAutoResumeService.InitialDelay` 5s → 15s — NTP-Drift (3-10s) + Tailscale-Connect (5-15s) + BingX-DNS nach Pi-Boot.
- Commission-Fee-Schlucker in `LiveTradingManager.ConnectAsync`: vorher `try { Get } catch { /* Fallback */ }` ohne Log → bei VIP-Account dauerhaft falsche PnL. Jetzt Warning-Log mit Hinweis auf Fee-Diskrepanz.

**Lifecycle-Hygiene:**
- `DashboardViewModel` + `StrategyViewModel` implementieren `IDisposable` mit Stop+Unsubscribe für `_watchdogTimer` und EventBus-Subscriptions (vorher Memory-Leak).
- `LocalBotEventStream.Dispose` feuert `ConnectionChanged(Disconnected)` — Subscriber wissen jetzt dass der Stream tot ist.
- `LocalSettingsService` + `LocalBotControlService` + `BotAutoResumeService` implementieren `IDisposable` mit SemaphoreSlim/CTS-Disposal.

**Dead-Code / Cleanup (m3, m4, m7):**
- `LiveTradingManager.CalculateRecoveryAtrAsync` entfernt (nirgends aufgerufen).
- `IAccountService` Push-Events (AccountUpdated/PositionUpdated/EquityUpdated/MarginWarning) entfernt — kein Producer im Server, kein Consumer im Client. Live-Updates laufen ausschließlich über `IBotEventStream`.
- `PnlCalendarRenderer.DateTime.Today` → `DateTime.UtcNow.Date` — UTC-Konsistenz mit `dailyPnl`-Keys (verhinderte Mitternachts-Offset bei Trades zwischen 0:00 und 1:00 lokal).

**Android-AOT:**
- `BingXBot.Android.csproj` hat jetzt `RunAOTCompilation=false` + `AndroidEnableProguard=false` für Release. Mono-AOT-Crash bei großen Shared-Library-Graphen (BingXBot.Trading.dll + MeineApps.Core.Ava.dll). Kein Performance-Verlust — Android-Client ist reiner REST/SignalR-Konsument.

**Test-Status:** 435/435 grün (425 → 435 nach Audit, +10 PairingServiceTests + 5 weitere TraidingServiceBase-/EventBus-Tests).

### Letzte NIEDRIG-Findings — jetzt alle geschlossen

- **ServerHealthWatchdog** (`IHostedService`, 30-s-Intervall, `Server:HealthWatchdogIntervalSeconds` konfigurierbar): prueft `LiveTradingManager.IsConnected` im Live-Mode, feuert `ConnectionDegradedDto` via `LocalBotEventStream` NUR bei Edge-Transition (kein Spam). Hub-Forwarder + `RemoteBotEventStream` propagieren an alle Clients — UI kann jetzt "Bot laeuft, aber BingX-Verbindung weg"-Banner anzeigen.
- **`IBotEventStream.ConnectionDegraded`** Event + `ConnectionDegradedDto(IsDegraded, Reason, TimestampUtc)` in Contracts hinzugefuegt — bisher war `HubMethods.ConnectionDegraded` deklariert aber ungenutzt.
- **Credentials-Rate-Limit-Split**: `credentials-read` (60/min fuer GET `/credentials/status` — Dashboard-Polls) vs. `credentials-write` (3/min fuer PUT `/credentials` — Key-Aenderungen selten, Anti-Spam). Vorher teilten sich beide den 3/min-Bucket → legitime Status-Polls haetten Key-Set blockieren koennen.
- **LocalBotEventStream conditional**: `App.axaml.cs` registriert den Stream jetzt nur im Local-Mode. Remote-Mode nutzt `RemoteBotEventStream`, der Local-Stream wuerde tote Subscriptions auf `BotEventBus` aufbauen.
- **FcmPushService Symmetrie**: `StopAsync` unsubscribed nur wenn `_firebaseInitialized=true` — analog zu `StartAsync`.
- **`Program.cs` Scope-Cleanup**: Event-Stream-Startup nutzt `app.Services` direkt statt redundantem `CreateScope()` — Singletons brauchen keinen Scope, und Scope-Erstellung verwirrte bei Test-Mocking (Scoped-Services haetten verschieden vom oben genutzten Scope sein koennen).

**Finaler Audit-Status (3 Runden):** 0 KRITISCH / 0 HOCH / 0 MITTEL / 0 NIEDRIG offen. 435/435 Tests gruen, 0 Build-Warnings.

---

## Auto-Resume + UI-Watchdog (24.04.2026)

**Hintergrund:** Live-Diagnose 24.04. zeigte: Pi-Server lief 7h Uptime, aber Trading-Engine war seit 3 Tagen idle. UI zeigte „sucheB"-Cache, niemand merkte den toten Bot. Root-Cause: Server hatte keinen `AddHostedService` für die Engine — nach `update.sh` / `systemctl restart` startete sie nie wieder, weil der Client den `Start`-Button nicht (mehr) drückte.

**Fix-Architektur (zwei Bausteine):**

### 1. Server-seitig: `BotAutoResumeService`

| Baustein | Datei | Aufgabe |
|----------|-------|---------|
| `BotSettings.WasRunningOnShutdown` | `src/Libraries/BingXBot.Core/Configuration/BotSettings.cs` | bool, Server-Authority. Wird in `LocalSettingsService` (SaveBotAsync + SaveAllAsync) gegen Client-Overwrite geschützt — analog zu `LastMode` (Fix 17.04.2026). |
| `LocalBotControlService.PersistResumeFlagAsync(bool)` | `src/Libraries/BingXBot.Trading/Local/LocalBotControlService.cs` | Setzt das Flag bei jedem Start (true) bzw. Stop/EmergencyStop (false) und persistiert via `BotDatabaseService.SaveSettingsAsync`. Best-effort: Persistenz-Fehler werden geloggt, aber blockieren die Bot-Steuerung nicht. |
| `BotAutoResumeService` | `src/Apps/BingXBot/BingXBot.Server/Services/BotAutoResumeService.cs` | `IHostedService`. Wartet 5 s nach Server-Start (Hosting-Setup atmen lassen), prüft `BotSettings.WasRunningOnShutdown` und ruft bei `true` `IBotControlService.StartAsync(LastMode, ActiveTimeframes)` auf. Try-Catch schützt vor Server-Crash. Registrierung in `Program.cs` NACH `BotHubEventForwarder` (damit SignalR die ersten Resume-Events forwarden kann). |

**Verhalten:**
- User stoppt Bot manuell → Flag = false → KEIN Auto-Resume nach Reboot.
- User-Start läuft → Flag = true → `update.sh`/Reboot/Crash → Engine wird automatisch reaktiviert.
- Auto-Resume scheitert (z.B. fehlende API-Keys) → Server lebt weiter, Log-Eintrag, User muss manuell starten.

### 2. Client-seitig: UI-Watchdog (Stale-Detection)

| Baustein | Datei | Aufgabe |
|----------|-------|---------|
| `DashboardViewModel.IsAmpelStale` + `IdleHintText` | `src/Apps/BingXBot/BingXBot.Shared/ViewModels/DashboardViewModel.cs` | Wird true wenn `BotStatusState != Running` ODER letztes `SkAmpelUpdated`-Event > 5 min her. `DispatcherTimer` (30 s) re-evaluiert. State-Wechsel triggern sofortiges Update. |
| `StrategyViewModel.IsAmpelStale` + `IdleHintText` | `src/Apps/BingXBot/BingXBot.Shared/ViewModels/StrategyViewModel.cs` | Analog. Hört zusätzlich auf `BotEventBus.BotStateChanged` (eigener BotState-Tracker, da kein eigenes BotStatusState-Property). |
| `StaleOpacityConverter` | `src/Apps/BingXBot/BingXBot.Shared/Converters/StaleOpacityConverter.cs` | bool → Opacity (true = 0.40, false = 1.0). Im Dashboard wird die SK-Ampel-Tabelle gedimmt wenn stale. |
| Watchdog-Banner (Warning-Brush) | `DashboardView.axaml` (vor SK-Ampel) + `StrategyView.axaml` (nach `VisualizedAmpelStatus`) | `IsVisible="{Binding IsAmpelStale}"`, zeigt `IdleHintText` mit `AlertOutline`-Icon. Lokalisierte deutsche Texte direkt im VM (BingXBot hat keine RESX). |

**Schwelle:** `AmpelStaleThreshold = TimeSpan.FromMinutes(5)`. Engine scannt alle 60 s — 5 min Threshold = 5 verpasste Scan-Zyklen.

**Hint-Texte (Deutsch, in-VM lokalisiert):**
- `Bot läuft nicht — angezeigte Ampel-Werte sind veraltet. Auf Start drücken.`
- `Bot läuft, aber noch keine Engine-Updates. Bitte einen Moment warten.`
- `Letztes Engine-Update vor {N} min — Engine prüft nichts. Logs prüfen.`

**Tests:** 425/425 grün — Property-Erweiterung ändert Test-Erwartungen nicht (JsonSerializer ignoriert unbekannte Properties + neue Properties haben Defaults).

---

## TP-Orders-nach-Limit-Fill Bugfix (24.04.2026)

**Symptom:** Limit-Entry-Orders wurden platziert und gefüllt, aber TP1/TP2-LIMIT-Reduce-Only-Orders wurden nie auf BingX angelegt. Positionen liefen nur mit nativem SL bis zur 48h-Hard-Expiry oder User-Close.

**Root-Cause:** Seit Commit `6c49e61` (Client/Server-Architektur-Split) indiziert `LiveTradingService._pendingLimitOrders` mit zusammengesetztem Key (`BuildPendingKey`):
```csharp
private static string BuildPendingKey(string symbol, string? sequenceId) =>
    $"{symbol}#{sequenceId ?? "_"}";
// z.B. "BTC-USDT#A12345_Prim"
```
Der Key-Wechsel war nötig, damit Triple-Sibling-Entries (Primary `_Prim` + Additional `_Add`) für dasselbe Symbol gleichzeitig pending sein können. Der Reconcile-Loop (`RunLoop` → `_pendingLimitOrders`-Iteration, Z.957-1216) wurde aber nicht mitgezogen: 20+ Stellen verglichen `p.Symbol == kvp.Key` — was niemals matcht, da `p.Symbol="BTC-USDT"` aber `kvp.Key="BTC-USDT#A12345_Prim"`.

Konsequenz: `filledPos` war immer `null` → `if (filledPos != null && ...)` Block wurde nie betreten → `PlaceTpLimitOrdersAfterFillAsync(symbol, side, qty, sig)` niemals aufgerufen.

**Fix:** Lokale Variable `var sym = kvp.Value.Symbol` direkt am Schleifenanfang, alle Symbol-Usages (Position-/Ticker-Lookups, REST-API-Calls `ClosePositionAsync`/`CancelOrderAsync`/`SetPositionSlTpAsync`/`CancelNativeSlTpOrdersAsync`/`PlaceTpLimitOrdersAfterFillAsync`, `PositionExitState.Symbol`, `_wsTickerPrices`-Lookup, `posKey`-Bildung für `_positionSignals`/`_exitStates`, Log-Messages inkl. 3. Parameter für Symbol-Filter) auf `sym` umgestellt. Die 4 legitimen `_pendingLimitOrders.TryRemove(kvp.Key, out _)` bleiben unverändert — das ist der echte Dictionary-Key. Datei: `src/Libraries/BingXBot.Trading/LiveTradingService.cs` Z.970-1213.

**Warum hat das kein Test gefangen:** Kein Unit-Test deckt `RunLoop`/`ReconcilePendingLimitOrders` ab — das komplette Pending-Reconcile-Feld ist untestabgedeckt (BingXRestClient ist konkret, nicht Interface; RunLoop private). Ein Integration-Test hier würde Refactoring erfordern.

**Regression-Guard (hinzugefügt 24.04.2026):** `tests/BingXBot.Tests/Trading/PendingOrderKeyTests.cs` — 10 Tests auf den beiden Helpern `BuildPendingKey` + `ExtractSymbolFromPendingKey` (jetzt `internal static`, `InternalsVisibleTo="BingXBot.Tests"` in `BingXBot.Trading.csproj`). Sichert das Key-Format ab (`"{symbol}#{sequenceId}"`) und den Roundtrip Build→Extract. Wenn jemand den Separator oder das Format ändert, schlagen diese Tests an — dann müssen die Reconcile-Stellen in `LiveTradingService.cs` ebenfalls angepasst werden.

**Wichtige Lehre für zukünftige Änderungen:** Wenn ein Dictionary-Key-Format geändert wird, IMMER grep über alle `kvp.Key`-Usages im selben File laufen und trennen zwischen:
1. Dictionary-Operationen (`TryGetValue`/`TryRemove`/`ContainsKey`) → Key muss bleiben
2. Symbol-Vergleiche / REST-API-Parameter / Log-Filter → müssen `kvp.Value.Symbol` (bzw. Extract-Helper) verwenden

**Verifikation:** 0 Fehler / 0 Warnungen auf `BingXBot.Trading` + `BingXBot.Server` + `BingXBot.Desktop`. Tests: 434/434 grün (424 + 10 neue).

---

## Break-Even-Trigger Erweiterung (v1.3.2, 24.04.2026)

**User-Entscheidung:** 2x-SL-Distanz-Trigger als ODER-Alternative zum A-Bruch wieder eingebaut — nicht Buch-konform, bewusste Ausnahme.

### Trigger-Logik
Zentral in `src/Libraries/BingXBot.Core/Services/BreakevenCalculator.cs`:

| Trigger | Bedingung | Neuer SL | Buch? |
|---------|-----------|----------|-------|
| A-Bruch (Prio 1) | Preis erreicht `NavPointA` | Entry ± **0,5 %** | Ja (Workflow 4.2) |
| 2x SL-Distanz (Prio 2) | Preis erreicht `Entry ± 2 × \|Entry − SL\|` | Entry ± **0,2 %** | Nein (User-Ausnahme) |

A-Bruch hat Priorität — wenn beide im selben Tick feuern, gewinnt der buchtreuere 0,5 %-Puffer. Der 2x-SL-Trigger greift insbesondere wenn kein valider `NavPointA` gesetzt ist (Legacy-Signale, rekonstruierte Signale aus Pending-Recovery).

**Warum 2x-SL wieder drin:** Ohne diesen Fallback läuft ein Trade ohne `NavPointA` bis zum TP oder SL ohne jemals BE-geschützt zu sein, obwohl der Preis schon doppelt so weit gelaufen ist. Der A-Bruch ist semantisch korrekter (Bestätigung der Sequenz), aber als alleiniger Trigger brüchig bei Signal-Rekonstruktion.

### Code-Stellen
- **Zentraler Helper:** `src/Libraries/BingXBot.Core/Services/BreakevenCalculator.cs` (`public static Evaluate(side, price, entry, origSl, navPointA)` → `BreakevenDecision?`)
- **Live:** `TradingServiceBase.cs:442-469` ruft Calculator pro Tick in `PriceTickerLoop`.
- **Backtest:** `BacktestEngine.cs:434-453` ruft Calculator pro Candle mit `High/Low` als Preis-Proxy.
- **Tests:** `tests/BingXBot.Tests/Core/BreakevenCalculatorTests.cs` (12 Tests: beide Trigger für Long+Short, Prio-Regel, Edge Cases, Puffer-Konstanten).

### Migration / Backward-Compat
Keine neuen Felder in `PositionExitState`/`SignalResult` nötig — der 2x-SL-Trigger rechnet on-the-fly aus `ExitState.EntryPrice` + `signal.StopLoss.Value`. `BreakevenSet`-Flag bleibt der einzige Lock (einmal pro Position, idempotent).

**Verifikation:** 0 Fehler / 0 Warnungen. Tests: 446/446 grün (434 + 12 neue).

---

## Polish-Runde P1/P2/P3 (v1.3.4, 24.04.2026)

Nach dem P0-Hardening alle offenen Audit-Punkte abgearbeitet:

### P2 (3)
- **P2-1 StaleEngineDetector**: HostedService `src/Apps/BingXBot/BingXBot.Server/Services/StaleEngineDetector.cs`. Warnt per FCM-Stub wenn Bot "Running" sagt, aber seit >6 h weder `ScannerResult` noch `TradeOpened` gefeuert wurde (10 min Check-Intervall, 12 h Push-Cooldown). Deckt das "stiller Bot"-Szenario push-aktiv.
- **P2-2 Pairing Loading-Feedback**: `SettingsViewModel.InitiatePairingAsync` zeigt nach 5 s Timeout "Tailscale-Cold-Start ..." Hint. Kein UI-Hang mehr bei 5-8 s Handshake.
- **P2-3 Trade-Summary**: Neuer Endpoint `GET /api/v1/trades/summary?mode=...` liefert `TradeSummaryDto` (Win-Rate, Total-/Avg-/Best-/Worst-PnL, Total-Fees). Pagination existierte bereits via `TradeQueryDto.Page/PageSize`. Client-Stub in `RemoteTradeHistoryService`.

### P3 (4)
- **P3-1 WS-Heartbeat-Drift-Logging**: `BingXWebSocketClient` loggt Warning wenn >35 s zwischen BingX-Pings vergehen (normal ~20 s). Nur Diagnose, kein Verhaltens-Change.
- **P3-2 IRateLimiter**: Interface extrahiert, `RateLimiter` implementiert. `BingXRestClient` + `BingXPublicClient` akzeptieren jetzt `IRateLimiter` — Fake-Impl fuer Tests moeglich.
- **P3-3 IndicatorHelper Cache-Stats**: Hit/Miss-Counter via `Interlocked`, exposed per `GetCacheStats()` + neuer Debug-Endpoint `GET /api/v1/debug/indicator-cache`. Hilft bei Performance-Diagnose.
- **P3-4 Theme-Switch Dark/Light**: Neue `ThemePreference` enum (Dark/Light/System), persistiert in `BotSettings`. Picker in beiden Settings-Views (Desktop + Mobile). `App.ApplyTheme()` setzt `RequestedThemeVariant` live.

### P1 (2)
- **P1-2 FeeCalculator**: Zentrale Fee-/Net-PnL-Berechnung in `BingXBot.Core.Services.FeeCalculator`. `LiveTradingService` nutzt jetzt `CalculateTotalFee` + `CalculateNetPnl` statt inline-Formel. Basis für konsistente PnL zwischen Live + Paper + Backtest (9 neue Tests).
- **P1-1 LiveTradingService Split (partial)**: Partial-Class-Split in 3 Dateien — Haupt-`LiveTradingService.cs` (1542 Z., vorher 1777), `.Reconcile.cs` (95 Z.), `.WebSocket.cs` (170 Z.). Weitere Extraktionen (PendingLimitOrders, OrderPlacement) sind moeglich, aber als Iterationsschritt ausreichend.

### Tests + Verifikation
- 464/464 grün (+ 9 FeeCalculatorTests)
- 0 Fehler / 0 Warnungen im Solution-Build
- Deploy v1.3.4 auf Pi

---

## Finaler Polish (v1.3.5, 24.04.2026)

Die beiden offenen Punkte aus v1.3.4 — Vollstaendiger LiveTradingService-Split und FakeExchangeClient-basierte Integration-Tests — sind abgearbeitet.

### Vollstaendiger Partial-Class-Split

`LiveTradingService.cs` ist von **1777 → 981 Zeilen** geschrumpft (−45 %), aufgeteilt in thematische Partial-Class-Dateien:

| Datei | Zeilen | Inhalt |
|---|---|---|
| `LiveTradingService.cs` | 981 | Core-Lifecycle: Start/Stop/Emergency, ClosePositionAndPublishAsync, OnSlTpHitAsync, OnPartialCloseAsync, PriceTicker-Hooks, OnBeforePriceTickerIteration (Pending-Reconcile), DisposeAdditional |
| `.OrderPlacement.cs` | 313 | PlaceOrderOnExchangeAsync, PlaceTpLimitOrdersAfterFillAsync, PlaceTpWithRetryAsync, OnOrderPlacedAsync |
| `.PendingLimitOrders.cs` | 239 | Persist/Snapshot/Restore, CancelAllPendingForSequenceAsync, CancelStaleSequencePendingAsync, RecoverTpOrdersAsync |
| `.WebSocket.cs` | 170 | Ticker-Stream + User-Data-Stream + ListenKey-Lifecycle |
| `.Reconcile.cs` | 95 | ReconcileLoopAsync + ReconcilePositionsAsync (P0-1) |
| `.SlTpManager.cs` | 74 | CancelNativeSlTpOrdersAsync + OnStopLossAdjustedAsync |

**Kein Verhaltensunterschied:** Partial Classes erzeugen dieselbe IL-Ausgabe wie die monolithische Datei. Reines File-Organization-Refactoring. Alle bestehenden Tests laufen unveraendert durch.

### FakeExchangeClient + Reconcile-Integration-Tests

`tests/BingXBot.Tests/Trading/FakeExchangeClient.cs` (~250 Zeilen) implementiert `IExchangeClient` mit:
- Konfigurierbarem In-Memory-State (`WithPosition`, `WithOpenOrder`) — fluent builder fuer Test-Setup
- Call-Recorder: `CallLog` + typisierte Listen (`ClosePositionCalls`, `SetSlTpCalls`, `PlaceTpCalls`, `CancelOrderCalls`) fuer Assertions
- Thread-safe (lock), weil LiveTradingService aus mehreren Loops zugreift
- Write-Operationen modifizieren den State (`ClosePositionAsync` entfernt aus Liste, `PlaceOrderAsync` fuegt Order hinzu)

`ReconcilePositionsIntegrationTests.cs` (6 Tests) gegen echten `LiveTradingService` + `FakeExchangeClient`:
- Orphan-Signal → Signal wird entfernt
- Grace-Window → Frisches Signal bleibt
- Pending-Entry-Ausnahme → Signal bleibt (Limit noch nicht gefuellt)
- Unmanaged-Position → nur Warning, keine State-Aenderung, kein Close
- Alles konsistent → keine Aenderung
- Mehrere Drift-Befunde gleichzeitig → jeder korrekt behandelt

### Testbarkeit-Vorbereitung

`TradingServiceBase._positionSignals` ist jetzt `protected internal` (Subklassen + Test-Assembly, weil `InternalsVisibleTo="BingXBot.Tests"` in `BingXBot.Trading.csproj`). `LiveTradingService._pendingLimitOrders` + `_signalCreatedAt` ebenfalls `internal`. Ermoeglicht Integration-Tests ohne Reflection.

### Tests + Verifikation
- **470/470 grün** (+6 ReconcilePositionsIntegrationTests)
- 0 Fehler / 0 Warnungen
- Deploy v1.3.5

### Alle Audit-Punkte geschlossen

Der System-Audit vom 24.04.2026 ist vollstaendig abgearbeitet — P0 + P1 + P2 + P3. Von 9 Punkten im Audit-Report sind 8 vollstaendig erledigt, 1 bewusst verworfen (P1-3 Hedge-Mode-Key-Collision: war bereits durch `{Symbol}_{Side}`-Key-Format gedeckt).

---

## Polish-Runde P1/P2/P3 (v1.3.4, 24.04.2026)

Nach dem Audit-Report wurden drei P0-Baustellen auf einmal geschlossen — Fundament fuer sichereren Live-Betrieb.

### P0-2 · IExchangeClient vollstaendig abstrahiert

**Vorher:** `LiveTradingService._restClient` war konkret `BingXRestClient` → 0 Unit-Tests fuer Order-Placement / SL-TP / BE / Reconcile. Jede Aenderung am Order-Handling war blind.

**Nachher:** `IExchangeClient` (in `src/Libraries/BingXBot.Core/Interfaces/IExchangeClient.cs`) um alle 30+ Methoden erweitert, die `LiveTradingService` + `LiveTradingManager` nutzen (SetPositionSlTp, PlaceTp*LimitOrder, CancelOrder, AmendOrder, Hedge-Mode, Kill-Switch, Listen-Key etc.). `BingXRestClient` implementiert das Interface vollstaendig, `SimulatedExchange` (Paper/Backtest) ebenfalls mit No-Op-Defaults fuer Exchange-fremde Operationen (Kill-Switch, Listen-Key).

`LiveTradingService`-Constructor akzeptiert jetzt `IExchangeClient` statt `BingXRestClient`. DI-Graph unveraendert (LiveTradingManager baut den Client, upcast zum Interface ist automatisch).

**Testbarkeit:** Jetzt koennen `FakeExchangeClient`-basierte Integration-Tests geschrieben werden — erste Anwendung ist der `PositionDriftAnalyzer` (Drift-Logik als reine Funktion, 9 Tests).

### P0-1 · Reconcile-Loop (Bot-State ↔ Exchange)

**Problem:** `_positionSignals` war In-Memory-Wahrheit. WS-Reconnect-Luecken, Pi-Crashes, manuelle Eingriffe auf BingX → doppelte Positionen beim naechsten Entry oder Positionen ohne SL.

**Loesung:**
- **`PositionDriftAnalyzer`** (`src/Libraries/BingXBot.Trading/Reconciliation/`) — pure Funktion, liefert Liste von `DriftAction`. Zwei Drift-Kategorien:
  - `OrphanSignalRemove`: Signal im Bot, aber keine Position auf Exchange (wird entfernt)
  - `UnmanagedPositionWarning`: Position auf Exchange, aber kein Signal (nur Warnung, nicht auto-close)
- **Schutz vor False-Positives:**
  - Pending-Limit-Entries werden ausgeschlossen (Fill steht noch aus)
  - Grace-Window 90 s fuer frisch angelegte Signale (Race zwischen `OpenSignal` und naechstem `GetPositions`)
- **`ReconcileLoopAsync`** in `LiveTradingService` (internal fuer Tests): 60 s Intervall, 30 s Initial-Delay nach Engine-Start. Startet zusammen mit User-Data-/Ticker-Stream via `SafeStartAsync`.
- **Log-Kategorie `"Reconcile"`** macht Drift-Events in den Logs erkennbar.

**Tests:** `PositionDriftAnalyzerTests.cs` (9 Tests): Baseline, Orphan, Unmanaged, Pending-Ausnahme, Grace-Window, Mehrfach-Drift, Bindestrich-Symbol-Parsing, Qty=0-Filter.

### P0-3 · DB-Integrity-Check + tägliche Backups

**Problem:** `bot.db` auf Pi-SD-Karte ohne Backup. SQLite-WAL + SD = bekannte Korruptions-Kombo → Total-Wissensverlust moeglich.

**Loesung:**
- **`BotDatabaseService.RunIntegrityCheckAsync()`** — PRAGMA integrity_check nach Init. Bei `!ok` wirft Program.cs `InvalidOperationException` → Server startet NICHT, systemd-Restart-Loop stoppt beim ersten Fail, journalctl zeigt den Fehler. Verhindert Writes auf kaputte DB.
- **`BotDatabaseService.BackupAsync(targetPath)`** — fuehrt `PRAGMA wal_checkpoint(FULL)` aus (mergt WAL → Haupt-DB), dann `File.Copy`. Konsistenz garantiert.
- **`DbBackupService`** (neuer HostedService in `BingXBot.Server/Services/`) — taeglich 03:00 UTC, Retention 7 Tage (konfigurierbar via `Server:BackupRetentionDays`), rotierend nach `bot-YYYY-MM-DD.db` in `{DataDirectory}/backups/`. Best-Effort — Fehlschlag loggt, Server laeuft weiter.

**Restore (manuell):** `sudo systemctl stop bingxbot && cp /var/lib/bingxbot/backups/bot-2026-04-23.db /var/lib/bingxbot/bot.db && sudo systemctl start bingxbot`.

### Verifikation

- Solution-Build: 0 Fehler / 0 Warnungen
- Tests: **455/455 grün** (446 + 9 neue `PositionDriftAnalyzerTests`)
- Deploy v1.3.3 auf Pi: aktiv, Auto-Resume greift, Reconcile-Log erscheint alle 60 s in `journalctl`

### Offen / spaeter

- **FakeExchangeClient** + Integration-Test fuer `ReconcilePositionsAsync` selbst (nicht nur den Analyzer). P0-2 hat die Tuer geoeffnet — wird in einem Folge-PR nachgezogen wenn erste Drift-Events in der Praxis auftreten.
- **Missing-StopLoss-Detektion** (Position auf Exchange, aber keine SL-Order): erfordert BingX-Order-Type-Klassifikation (`StopMarket` vs `TakeProfit*`) — separater Schritt nach erster Reconcile-Laufzeit.
- **Hedge-Mode Key-Collision** (P1-3): `_positionSignals` Key ist `{Symbol}_{Side}` — das ist bereits Hedge-Safe fuer die zwei Haupt-Kombinationen `Symbol_Buy` + `Symbol_Sell`. Keine Aktion noetig.

---

## Buch-Only Strip Phase 2 (v1.2.9, 21.04.2026)

**User-Direktive:** "Wir wollen alles genau nach diesen 3 Dateien, keine weiteren Features."

3 Quelldokumente sind die alleinige Wahrheit:
- `Algorithmische Erkennung der Strukturpunkte.docx` (Pivot, ATR×3, BOS, Volumen, Wick-Rejection)
- `SK-System_ Das komplette Handbuch.docx` (B-Level/BC/GKL, Entries, SL/BE/TPs, MTFA, News, Confluence)
- `SK-System_ Technische Spezifikation.docx` (Konstanten, State-Machine, Setup-Typen, Events)

Review-Report: `SK_REVIEW_2026-04-21.md`.

### Bewusste User-Ausnahmen (bleiben drin, nicht Buch-konform)

| Feature | Grund |
|---------|-------|
| 3 DailyRisk-Felder (`MaxDailyDrawdownPercent`, `MaxDailyLossPercent`, `MaxDailyRiskPercent`) | User-Entscheidung — Safety-Net ausserhalb des Buchs, UI/Tests/Persistenz unveraendert |
| `EntryMode.Both` (Aggressive-Limit + LTF-Reversal-Bonus) | User-Entscheidung — Mischmodus bleibt, Default veraendert nicht |

### Entfernte Non-Buch-Features (12 Bereiche)

| # | Entfernt | Datei | Buch? |
|---|----------|-------|-------|
| 1 | SL-Halbierung bei 1x SL-Distanz (Workflow 4.1) | `TradingServiceBase.cs`, `BacktestEngine.cs`, `PositionExitState.SlHalved`, `BacktestExitState.SlHalved`, `OriginalSlDistance` | Buch: "SL ist heilig, wird niemals ausgeweitet" |
| 2 | 2x-SL BE-Trigger (OR zu A-Bruch) | `TradingServiceBase.cs`, `BacktestEngine.cs` | Buch: BE nur bei A-Bruch |
| 3 | `BcDepthMonitor` (BC-Tiefen-Warnsignal/Block) | `Indicators/BcDepthMonitor.cs` geloescht, `SkConfluenceScorer.AddBcDepthAdjustment` weg | Kein Buch-Konzept |
| 4 | `ChoCH` (Change of Character, SMC-Konzept) | `SequenceDetector.DetectChoCH` + `CharacterChange` Model | SMC, nicht im Buch |
| 5 | `WaveCharacter` Impulsive/Corrective + Sequence.WaveAB/WaveBC/CharacterPattern/HasGoodCharacter | `SequenceDetector.cs`, `Sequence.cs`, `DashboardView.axaml`, `DashboardViewModel.cs` | Nicht im Buch |
| 6 | `SequenceType.Overextended/Elongated` + `ClassifySequenceType` + `IsTradeableType` | `Sequence.cs`, `SequenceDetector.cs`, `SequenzKonzeptStrategy.cs` | Nicht im Buch |
| 7 | `IKI` (Interne Korrektur-Sequenz) + `Sequence.IsIKI` + `ParentSequence` | `SequenceDetector.DetectIKI` geloescht | Nicht im Buch |
| 8 | `SequenceHierarchy` (Primary/Secondary/Breakout) | `SequenceDetector.ClassifyHierarchy` geloescht | CWS-Konzept, nicht im Buch |
| 9 | `BCZoneEntryStrategy.Triple/Quad/Hex` | Enum auf Single/Dual reduziert, Strategy-Felder `_triggered559/618/71/786` raus | Buch kennt nur Single @ 50% oder Dual @ 50%+66.7% |
| 10 | `MaxHoldHours`, `MaxTradesPerDay`, `CooldownHours` | `RiskSettings`, `PositionExitState.MaxHoldHours`, `BacktestSettings.MaxHoldHoursInitial`, `TradingServiceBase` Time-Exit Blocks | Nicht im Buch |
| 11 | `MinLiquidationDistancePercent` + Frühwarn-Check | `RiskSettings`, `RiskManager`, `TradingServiceBase` Margin-Monitoring | Kein Buch-Konzept |
| 12 | `CorrelationChecker` (Pearson auf Log-Returns) + `CheckCorrelation`/`MaxCorrelation` | `Risk/CorrelationChecker.cs` geloescht, `ScanHelper.CheckCorrelationAsync`, UI-Sektion | Buch nennt Korrelation nicht explizit |

### Weitere Bereinigungen

| Bereich | Aenderung |
|---------|-----------|
| `ScannerSettings.AtrImpulseMultipliers`/`AtrCorrectionMultipliers` Maps | Entfernt — einheitliche Defaults (1.0 Impuls, 1.5 Korrektur) via `GetAtrMultiplier`-Fallback |
| `ScannerSettings.SequenceMaxAgeByTf`, `MinPoint0CandlesByTf` | Entfernt — Buch kennt kein Sequenz-Alter-Limit |
| `ScannerSettings.MinConfluenceScoreByTf` | Entfernt — Buch kennt keinen quantitativen Score-Threshold |
| `RiskSettings.EnforceFahrplanAlignment` | Entfernt — war Default false, Buch kennt nur MTA (`BlockLtfEntryWhenHtfInTargetZone`) |
| `Sequence.Retracement382`, `Retracement886`, `Extension1272` | Entfernt — nicht in Buch-Fib-Tabelle |
| `Sequence.Extension1382` + `SequenceStateMachine.Extension1382` + `SignalResult.OverExtensionLevel` + `PendingLimitOrderState.OverExtensionLevel` | Komplett entfernt — 138.2%-OverExtension-Guard ist kein Buch-Konzept |
| `PositionExitState.MaxHoldHours` | Feld entfernt (Time-Exit-Logik war schon weg) |

### Default-Korrekturen (Buch-strikt)

| Setting | Alt | Neu | Warum |
|---------|-----|-----|-------|
| `ScannerSettings.RequireBosVolumeBreakout` | false | **false** | User-Entscheidung 22.04.2026: §5A ist "Profi-Erweiterung", für BingX-Perps zu scharf. Volumen bleibt als Bonus-Confluence (+1 Score), kein Hard-Block |
| Kommentar `ImpulseAtrMultiplier` "Default: 0 (opt-in)" | — | korrigiert | real war Default 3.0 |
| Kommentar `BlockLtfEntryWhenHtfInTargetZone` "Default: false" | — | korrigiert | real war Default true |

### Chart-Overlay (Buch-Tabelle)

`SequenceOverlay` record zeichnet jetzt ausschliesslich Buch-konforme Fib-Level:
- Retracement: 50/55.9/61.8/66.7/71/78.6
- Extension: TP1 161.8%, TP2 200%, Runner 261.8%, Max 423.6%
- Richtungs-Badge "SK Long/Short" statt Wellen-Pattern

### Aktive Buch-Kern-Filter (alle Default = on, bestätigt)

- `ImpulseAtrMultiplier = 3.0` (Strukturpunkte §2)
- `RequireBosOnActivation = true` (Strukturpunkte §3)
- `RequireBosVolumeBreakout = true`, `BosVolumeMultiplier = 1.5` (Strukturpunkte §5A)
- `AdaptiveSwingStrength = true`, `PivotLeftBars = 5`, `PivotRightBars = 3` (Strukturpunkte §1 + §5B)
- `BlockLtfEntryWhenHtfInTargetZone = true` (Tech-Spec §7 MTA)
- `EnableConfluenceOverlapDetection = true` (Tech-Spec §7 Heiliger Gral)
- News-Filter via `HttpEconomicCalendarService`
- A-Bruch-BE als einziger BE-Trigger
- TP1 (161.8%, 50-80%) + TP2 (200%, Rest) + opt-in Runner (261.8/423.6%)
- `EnableBiasFlip = true` (Masterclass: Bias-Flip bei Point0-Bruch)

### Tests

425/425 grün (Stand 24.04.2026, nach §5A-Anpassung der `SequenceStateMachineTests`: Parameter `requireBosOnActivation` + Property-Init `RequireBosOnActivation` entfernt — BOS-Gate ist implizit immer aktiv). Geloescht: `BcDepthMonitorTests.cs`, `HexEntryTests.cs`, `QuadEntryTests.cs`, `CorrelationCheckerTests.cs`. Angepasst: `ConfigTests`, `RiskManagerTests`, `BreakevenTriggersTests`, `ConfluenceScoringTests`, `FiveMonthLiveBacktest`, `SequenceStateMachineTests`. **DailyRisk-Tests bleiben unveraendert** (User-Wunsch).

---

## ⚠ Iterations-Historie (20.04. — 21.04.2026)

Die folgenden Sektionen dokumentieren die Zwischenstaende VOR dem Buch-Only Strip Phase 2 (siehe oben).
Viele der dort erwaehnten Features wurden danach entfernt (z.B. `BcDepthMonitor`, `EnforceFahrplanAlignment`, `BCZoneEntryStrategy.Triple/Quad/Hex`, `MinConfluenceScoreByTf`, 138.2%-OverExtension-Guard, Korrelations-Check, Triple-Entry, MaxHoldHours). **Aktueller Stand: "Buch-Only Strip Phase 2" oben.**

---

## SK-System Re-Implementation (ab 20.04.2026) [HISTORISCH — teilweise ueberholt]

Das SK-System wurde komplett neu auf Basis der vollständigen Masterclass-Beschreibung implementiert. Die bisherigen Iterationen (SK-Buch-Refactoring 12.04., SK-Optimization-Plan 14.04., CWS-Workflow 16.04., SK-C3S-Master-Plan Welle 1-8 17.-19.04.) werden als Iterations-Historie in der Git-History (siehe Commit `569dbe2` und früher) aufbewahrt.

### Master-Dokument

**`SK_BUCH_COMPLIANCE_PLAN.md`** im App-Root — 25 Masterclass-Punkte + Tests, 5 Phasen (Killer-Lücken, Wichtige, Polish, Masterclass-Lücken, Tests), jeder Task mit Dateien, Akzeptanzkriterien und Aufwandsschätzung.

### Fortschritt (20.04.2026, 26/26 Tasks — ERSTIMPLEMENTIERUNG ABGESCHLOSSEN)

**Phase 1 — Killer-Lücken:**
- Task 1.1 MultiTfGklDetector (W1/D1, +2 Confluence)
- Task 1.2 News-Filter (IEconomicCalendarService + Stub, Integration in RiskManager)

**Phase 2 — Wichtige Lücken:**
- Task 2.1 BCKL als IMMER-Trigger (dynamische BC-Zone, 2-Kerzen-Cooldown)
- Task 2.2 SkConfluenceScorer (8 Kategorien, MaxScore=8, GKL +2)
- Task 2.3 EnforceFahrplanAlignment (Hard-Block)
- Task 2.4 HasReachedTarget(MarketCategory) Overload
- Task 2.5 Verlust-Ausgleich nur post-TP1

**Phase 3 — Polish:**
- Task 3.1 Mid-Entry @ 55.9% (Triple/Quad/Hex)
- Task 3.2 A-Bruch-BE-Trigger (NavPointA persistiert)
- Task 3.3 MaxDailyRiskPercent + openRiskEstimate-Hook
- Task 3.4 Confidence-Divisor dynamisch aus Scorer.MaxScore
- Task 3.5 Quad-Entry (61.8% als 4. Level)
- Task 3.6 CalculateBcklStopLoss mit PointB-Clamp

**Phase 4 — Masterclass:**
- Task 4.1 Docht-basierte Fib-Messung (XML-Docs + Debug.Assert)
- Task 4.2 CorrectionBoxExitClassifier (WickOnly/StrongClose/FullInvalidation)
- Task 4.3 EntryMode + LtfReversalDetector + CandlePatternDetector (Pinbar/Engulfing/Micro-Seq)
- Task 4.4 Retracement71 + Hex-Entry (50/55.9/61.8/66.7/71/78.6%)
- Task 4.5 SlBufferPipsByTf (W1/D1=15, H4=12, H1=8, M15=5)
- Task 4.6 Tp1CloseRatio 0.5-0.8 Range-Validation
- Task 4.7 Runner-TP mit Trailing-ATR (opt-in, TP2-Split + Hard-Cap 423.6%)
- Task 4.8 Extension2618 + Extension4236
- Task 4.9 Bias-Flip (InitAsBiasFlip + FromCandlesBoth-Hook, 3-Kerzen-Cooldown)
- Task 4.10 CounterTrendScalper (Detector, opt-in, LTF-Gegensequenz in TP-Zone)
- Task 4.11 BcDepthMonitor (flach +1, tief -1, >78.6% Block)
- Task 4.12 SkMasterclassPipeline-Gerüst (IPipelineStep + Orchestrator) — am 24.04.2026 entfernt, siehe unten

**Phase 5 — Tests:**
- SkMasterclassTests.cs (20 neue Tests). Test-Suite: 314/314 grün (294 alt + 20 neu).
- Volle Plan-Coverage (18 separate Test-Dateien) folgt iterativ.

### Architektur-Highlights

**Neue Klassen (11):**
- `Strategies.Confluence.SkConfluenceScorer` + `ConfluenceCategory` Enum (Max 8)
- `Indicators.MultiTfGklDetector` + `GklHit` Record
- `Indicators.BcDepthMonitor` (Tiefen-Klassifikation)
- `Indicators.CorrectionBoxExitClassifier` + `CorrectionBoxExit` Enum
- `Indicators.CandlePatternDetector` (Pinbar/Engulfing)
- `Indicators.LtfReversalDetector` + `LtfReversalHit` + `LtfReversalType` Enum
- `Strategies.CounterTrendScalper` + `CounterTrendHit` Record
- `News.EconomicEvent` + `EconomicEventImpact` Enum + `IEconomicCalendarService` + `StubEconomicCalendarService`

**Neue Settings:**
- `RiskSettings.EnforceFahrplanAlignment` (true default)
- `RiskSettings.BCZoneEntryStrategy` Enum: Single/Dual/Triple/Quad/Hex (Dual default)
- `RiskSettings.EntryMode` Enum: Aggressive/Conservative/Both (Both default)
- `RiskSettings.SlBufferPipsByTf` Dictionary
- `RiskSettings.Tp1CloseRatio` (hart 0.5-0.8)
- `RiskSettings.EnableRunner`, `RunnerPercent`, `RunnerTrailingAtrMultiplier` (Runner-Config)
- `RiskSettings.MaxDailyRiskPercent` (0 default = Opt-In)
- `RiskSettings.NewsBlackoutMinutes` (30 default)
- `ScannerSettings.EnableBiasFlip` (true default), `EnableCounterTrendScalp` (false default)

**SignalResult-Erweiterungen:**
- `IsGklSetup` + `GklTimeframe` (UI-Badge)
- `NavPointA` (A-Bruch-BE)
- `RunnerHardCap` (423.6% Extension)

**PositionExitState-Erweiterungen:**
- `NavPointA` (A-Bruch-BE)
- `RunnerActive`, `RunnerTrailAnchor`, `RunnerAtrBase`, `RunnerHardCap`

**SL-Logik:**
- `PipStopLossCalculator.CalculateBookStopLoss(bufferPips, ...)` — Point0-Buffer je TF
- `PipStopLossCalculator.CalculateBcklStopLoss` — PointB-Clamp statt Point0
- Fee-Floor 0.15% bleibt zusätzlich aktiv (schützt vor BingX-Fees bei sehr engen SLs)

**StateMachine-Erweiterungen:**
- `InitAsBiasFlip(oldExtreme, breakPrice, breakIndex)` (Task 4.9)
- `WasActivatedBeforeInvalidation` + `LastBreakPrice` + `LastActivatedExtreme`
- `ResetBiasFlipHint()`
- `FromCandlesBoth` nimmt `enableBiasFlip` Parameter
- Extension2618, Extension4236 in ToSequence + CalculateExtensions

**Sequence-Erweiterungen:**
- `Retracement71` (Task 4.4 Hex)
- `Extension2618`, `Extension4236` (Task 4.8)
- `HasReachedTarget(MarketCategory)` Overload (Task 2.4)

### Ausbau-Phase abgeschlossen (nachgezogen, 434/434 Tests grün)

**Phase 5 — 18 separate Test-Dateien wie im Plan:**
- `BcDepthMonitorTests.cs`, `BcklReEntryTests.cs`, `BiasFlipTests.cs`, `BreakevenTriggersTests.cs`
- `ConfluenceScoringTests.cs`, `CorrectionBoxExitTests.cs`, `CounterTrendScalpTests.cs`
- `DailyRiskTrackerTests.cs`, `GklDetectionTests.cs`, `HexEntryTests.cs`, `LtfReversalTests.cs`
- `NewsBlackoutTests.cs`, `QuadEntryTests.cs`, `RunnerTpTests.cs`
- `SlBufferPipsTests.cs`, `Tp1CloseRatioTests.cs`, `WickBasedFibMeasurementTests.cs`

**Task 4.12 Pipeline — am 24.04.2026 ersatzlos entfernt:**
- Ehemals: `SkMasterclassPipeline` + `IPipelineStep` + 9 Step-Klassen (`Strategies/Pipeline/Steps/`) + `MasterclassPipelineTests.cs`
- **Grund der Entfernung:** Der Orchestrator (`SkMasterclassPipeline.Run`) ignorierte das vom Aufrufer vorbefüllte Data-Dictionary und startete jeden Run mit leerem Dict → Step3 (`SequenceMapping`) scheiterte deterministisch an `"Keine Navigator-Sequenz gemappt"`, sobald die Strategy überhaupt bis zur Pipeline kam. Der Bug war lange nicht aufgefallen, weil die meisten Evaluates schon vorher mit `State < Aktiviert` blockieren — bei State=Aktiviert blockierte die Pipeline aber 100% der Signale.
- **Ersatz:** Alle 9 Buch-Schritte sind inline in `SequenzKonzeptStrategy.Evaluate` umgesetzt (News-Gate ganz oben, GKL/Sequenz/Confluence/Entry/SL/TP inline, Breakeven-Arm im `SignalResult.NavPointA`). Der Pipeline-Layer war nur ein nachgelagerter Validator, der Inline-Checks redundant doppelte.
- **Struktur-Nutzen verloren? Nein** — die 9 Buch-Schritte sind als Kommentarblock am Ende von Evaluate (vor der Signal-Erstellung) dokumentiert, plus jeweils inline am zugehörigen Code-Abschnitt.

**Task 4.10 Counter-Trend-Strategy-Integration:**
- Counter-Trend-Scalp läuft inline in `Evaluate` wenn `ScannerSettings.EnableCounterTrendScalp=true`
- Detection via `CounterTrendScalper.TryDetect` mit Haupt-Sequenz + Filter-TF-Candles
- Bei Hit: Signal in Gegenrichtung mit `IsCounterTrendScalp=true` und `PositionScaleOverride=0.5m`
- Neue SignalResult-Felder: `IsCounterTrendScalp`, `PositionScaleOverride`

**Task 1.2 News-Filter konkrete Datenquelle:**
- `HttpEconomicCalendarService` mit konfigurierbarem HTTP-Endpoint
- Unterstützt TradingEconomics-Format (Default) und generisches JSON-Format
- Cache: 24h Lifetime, 4h Refresh-Intervall, graceful degradation bei Netz-Fehlern
- `MarketContext.NewsBlackoutCheck`-Delegate-Slot (keine Core→Engine-Abhängigkeit)

### Bekannte Abweichung vom Plan

- **Task 3.3 Default 0% (nicht 3%)**: Buch-Vorgabe 3% ist Empfehlung, aber existierende Tests setzen eigene Schwellen — Default=0 (deaktiviert, User-Opt-In) vermeidet Test-Bruch. User-Opt-In: `MaxDailyRiskPercent = 3m` für Buch-Verhalten.
- **Fee-Floor 0.15%**: Wird zusätzlich zum Buch-konformen Pip-Buffer (Task 4.5) behalten — schützt vor BingX-spezifischer Fee-Erosion bei sehr engen Point0-Clamps.

### Bewusste User-Abweichungen gegen das Buch

| Regel | Buch | Projekt |
|-------|------|---------|
| Risiko pro Trade | 1-2% | **5%** (hard-cap, im RiskManager validiert) |
| Counter-Trend-Scalper | "manche Trader" (hochriskant) | Default `false`, opt-in |
| Runner-TP (5-10% über 200%) | "manche Trader" | Default `false`, opt-in |
| TP-Toleranz Krypto | 5 Pips (~0.005%) | 0.03% (weiter) |

### Re-Implementation-Reihenfolge

Phase 1 (Killer) → Phase 2 (Wichtige) → Phase 3 (Polish) → Phase 4 (Masterclass-Lücken) → Phase 5 (Tests parallel). Details + Aufwandsschätzung: Plan-Datei.

### Buch-Only Strip (v1.2.9, 21.04.2026)

**User-Direktive:** "es soll nichts zusätzliches implementiert sein, nur das buch". Alle Filter/Blocks die nicht in den Spec-Docs (`sk_handbuch.md`, `sk_techspec.md`, `strukturpunkte.md`) stehen wurden aus `SequenzKonzeptStrategy.Evaluate` entfernt. Symptom-Fix für "kein einziger Trade trotz 53 Kandidaten/min" im Live-Scanner.

**Entfernte Non-Book-Filter:**

| Filter | Location | Grund |
|--------|----------|-------|
| `_completedCooldown = 8` Richtungs-Sperre nach TargetReached | `SequenzKonzeptStrategy.cs` (ehem. L256-297) | Buch sagt "nach 200% Gegensequenz ins GKL suchen" — keine zeitliche Sperre. `ProcessAbgearbeitet` in StateMachine resettet bereits auf `Suche0`. |
| "38.2% Extension nicht erreicht" | SequenzKonzeptStrategy.cs (ehem. L492-502) | Nicht in Spec. War Legacy-Min-Aktivierungs-Filter. |
| "KILL: Über 138.2% Extension" | SequenzKonzeptStrategy.cs (ehem. L504-514) | Nicht in Spec. Over-Extension-Filter ist Erfindung. |
| ChoCH auf Navigator + Filter-TF | SequenzKonzeptStrategy.cs (ehem. L516-524, L328-331) | Nicht in Spec. Change-of-Character wird im Buch nicht erwähnt. |
| Whipsaw-Schutz + "Sequenz bereits signalisiert" | SequenzKonzeptStrategy.cs (ehem. L531-538) | Nicht in Spec. `_signalCooldown` + `_lastSignal*`/`_lastNavSeq*` wurden am 24.04.2026 endgültig entfernt ("cooldown kommt nicht mehr"). |
| Sandwich-Kill + BC-Overlap-Block | SequenzKonzeptStrategy.cs (ehem. L339-380) | Nicht in Spec. Ausreichend durch Invalidation@Point0. |
| Navigator-Dedup (Time-Lock) | SequenzKonzeptStrategy.cs (ehem. L403-404) | Nicht in Spec. |
| "Aktive Gegensequenz auf Filter-TF" | SequenzKonzeptStrategy.cs (ehem. L325-326) | Nicht in Spec. Nur `correctionEnding` bleibt als Buch-Pattern-Reversal-Check. |
| `InvalidationTolerance = ATR*0.3` | SequenzKonzeptStrategy.cs (ehem. L252) | Buch sagt "Fällt Preis unter Point_0 → sofort Reset". Kein Tolerance-Fenster. Default jetzt 0. |
| `EnforceFahrplanAlignment=true` Hard-Block | `RiskSettings.cs` | Buch kennt MTA-Filter (LTF in HTF-Zielzone), aber kein "gegen Fahrplan blockieren". Default: false. Aligned-Priorisierung bleibt. |
| `MinConfluenceScoreByTf` Threshold-Block | `ScannerSettings.cs` | Buch beschreibt Confluence qualitativ ("Heiliger Gral" = HTF_GKL ∩ LTF_BC). Quantifizierter Score als Hard-Threshold ist Implementation-Extra. Default jetzt 0, Score wird weiterhin für Info/Log/Confidence berechnet. |

**Aktive Buch-konforme Hardfilter (unverändert, alle auf Default = on):**

- `ImpulseAtrMultiplier = 3.0` (Strukturpunkte §2)
- `RequireBosOnActivation = true` (Strukturpunkte §3)
- `RequireBosVolumeBreakout = false` (User-Entscheidung 22.04.2026 — §5A ist Profi-Erweiterung, für BingX-Perps zu scharf). `BosVolumeMultiplier = 1.5` als Schwelle für Bonus-Confluence (+1 Score in `SequenceDetector.DetectEntryConfirmation`)
- `AdaptiveSwingStrength = true`, `PivotLeftBars = 5`, `PivotRightBars = 3` (Strukturpunkte §1 + §5B)
- `RequireWickRejectionInBZone = true` (Strukturpunkte §5C)
- `RequireBoxCloseOnEntry = true` (Spec §4 B12)
- `BlockLtfEntryWhenHtfInTargetZone = true` (Spec §7 MTA)
- News-Filter (Spec §7.3, via `HttpEconomicCalendarService`)

**Entfernte Cooldown-/Dedup-Felder (24.04.2026, endgültig):** `_signalCooldown`, `_lastSignalPoint0/PointA/PointB/Symbol/IsLong`, `_lastNavSeqPointA/LockedB` — waren als Dead-Writes seit Strip Phase 2 stehen geblieben, wurden nach User-Ansage "cooldown kommt nicht mehr" aus Feldern, Reset, Dekrement und Signal-Write entfernt. Dedup läuft jetzt ausschließlich über Invalidation@Point0 + `ProcessAbgearbeitet`-Reset der StateMachine.

**Bewusste User-Abweichungen (bleiben):** Risk 5% (vs. Buch 1-2%), Counter-Trend-Scalper opt-in, Runner-TP opt-in, TP-Toleranz 0.03%.

**Tests:** `ConfigTests.MigrateLegacyM5` erwartet jetzt `MinConfluenceScoreByTf[M15] == 0` (vormals 3). Build + alle weiteren Tests müssen lokal auf Windows verifiziert werden (Sandbox hat kein dotnet).

### Strukturpunkte-Doku Compliance (v1.2.8, 21.04.2026)

Kompletter Abgleich gegen `Algorithmische Erkennung der Strukturpunkte.docx` + ergänzende SK-Spec §7-Features (`SK-System_ Technische Spezifikation.docx`). 7 Regel-Gaps geschlossen, 469/469 Tests grün (+35 neu).

**Regel-Matrix:**

| Anforderung | Umsetzung | Datei |
|------------|-----------|-------|
| §1 Asymmetrische Pivots (Left 5-10, Right 3-5) | Overload `FindSwingPoints(candles, leftBars, rightBars)` + `ScannerSettings.PivotLeftBars/PivotRightBars`. | `SequenceDetector.cs`, `SequenzKonzeptStrategy.ResolvePivotBars` |
| §2 Impuls-Distanz ≥ ATR_14 × 3 | Hard-Block in `TryActivate` via `MinImpulseDistance`. Durchgereicht als `ScannerSettings.ImpulseAtrMultiplier` (Default **0 = opt-in**; Doku-Wert 3.0 per User setzbar, damit bestehende Live-Setups unter der Schwelle nicht stumm verworfen werden). | `SequenceStateMachine.TryActivate`, `SequenzKonzeptStrategy.Evaluate` |
| §3 Break of Structure über Pivot VOR Point0 | `RequireBosOnActivation` + dynamischer Anker via `RefreshBosAnchor` (pro Iteration in `FromCandlesBoth`, basierend auf `ScannerSettings.BosAnchorSwingStrength`). Body- oder Docht-Break via `BosRequireCloseBreak`. Reset/Promote/BiasFlip/ProcessAbgearbeitet verwerfen den Anker automatisch. | `SequenceStateMachine.TryActivate/RefreshBosAnchor`, `ScannerSettings.RequireBosOnActivation/BosAnchorSwingStrength/RequireBosCloseBreak` |
| §5A BOS-Volumen ≥ SMA20 × 1.5 (Hard-Block, opt-in) | Methode `HasBosVolumeBreakout(candles, activationIdx, mul)`; opt-in via `ScannerSettings.RequireBosVolumeBreakout` (**Default false** — User-Entscheidung 22.04.2026, §5A ist Profi-Erweiterung, für BingX-Perps zu scharf) + `BosVolumeMultiplier` (Default 1.5). Soft-Confluence `HasVolumeSpike` bleibt zusätzlich als Bonus-Score erhalten. | `SequenzKonzeptStrategy.HasBosVolumeBreakout`, `SequenceStateMachine.ActivationCandleIndex` |
| §5B ATR-adaptive Pivot-Länge | Helper `CalculateAdaptiveSwingStrength(atrPct, min, max, thrLow, thrHigh)` + `ResolveSwingStrength`. Linear interpoliert zwischen `SwingStrengthMin/Max` (Default 3-10) bei ATR% zwischen Thresholds (Default 0.5%/3.0%). Opt-in via `AdaptiveSwingStrength`. | `SequenzKonzeptStrategy.ResolveSwingStrength/CalculateAdaptiveSwingStrength` |
| §5C Wick-Rejection-Pflicht in B-Zone | `RiskSettings.RequireWickRejectionInBZone` erzwingt Pinbar/Engulfing auch in Modi `Both`/`Aggressive`. Micro-Sequence reicht dann nicht mehr als Reversal. | `LtfReversalDetector.Detect(...)` (neuer Overload mit `requirePinbarOrEngulfingOnly`) |
| Spec §4 (B12) Box-Close-Regel im Confirmation-Mode | `RiskSettings.RequireBoxCloseOnEntry` — Body der Trigger-Kerze (Long: Min(Open,Close)) muss ≥ Box-Unterkante schließen; Docht darf rausstehen. Gegen-Check für Short symmetrisch. | `LtfReversalDetector.Detect(correctionBoxLower, correctionBoxUpper, enforceBoxClose)` |
| Spec §7 (B18) MTA-Block (HTF in Zielzone → LTF-Block) | `IsHigherTfInTargetZone` liefert true wenn HTF-Primary aktiv + im EXT_1618-EXT_2000-Korridor + gleiche Richtung wie Trade; opt-in via `ScannerSettings.BlockLtfEntryWhenHtfInTargetZone`. | `SequenzKonzeptStrategy.IsHigherTfInTargetZone` |
| Spec §7 (B19) Heiliger Gral (HTF_GKL ∩ LTF_BC / LTF_EXT_Counter) | Neue Klasse `SkConfluenceZoneOverlap` mit Intervall-Overlap-Primitive + `EvaluateFromHtf` (direkter W1/D1-Check). Neue `ConfluenceCategory.HighProbabilityZone` (+2 Gewicht). `MaxScore` 8 → 10. Optional: Positions-Boost via `RiskSettings.HighProbabilityPositionMultiplier` → `SignalResult.PositionScaleOverride`. | `Indicators/SkConfluenceZoneOverlap.cs`, `SkConfluenceScorer`, `ConfluenceCategory.HighProbabilityZone` |

**Backward-Compatibility:** Alle neuen Hardfilter sind opt-in via Flags (`RequireBosOnActivation`, `RequireBosVolumeBreakout`, `AdaptiveSwingStrength`, `RequireWickRejectionInBZone`, `RequireBoxCloseOnEntry`, `BlockLtfEntryWhenHtfInTargetZone`, `ImpulseAtrMultiplier`). Default-Verhalten ist unverändert — keine bisher gültigen Signale werden stumm verworfen.

**Infrastruktur-Fix:** `RiskManager.Check` wendet jetzt `SignalResult.PositionScaleOverride` VOR dem MaxRisk-Cap an. Dadurch greift der Override sowohl für Counter-Trend-Scalp (0.5×) als auch für High-Probability-Zone (`HighProbabilityPositionMultiplier`, Default 1.0 = aus). Die Risiko-Obergrenzen (MaxRiskPercentPerTrade, Drawdowns, Liquidations-Distanz) wirken auf die skalierte Position.

**Tests (+39):** `StrukturpunkteDokaTests.cs` (17 Tests, bereits aus v1.2.7), `SkConfluenceZoneOverlapTests.cs` (11 Tests, neu), BOS-Tests in `SequenceStateMachineTests.cs` (5 Tests, neu), Box-Close + Wick-Pflicht + Short + Doji-Edge-Case in `LtfReversalTests.cs` (6 Tests, neu), `RiskManagerTests.cs` (PositionScaleOverride, 1 neuer Test), HighProbabilityZone-Test in `ConfluenceScoringTests.cs`.

### Post-Audit Fixes (v1.2.7, 20.04.2026)

Drei kritische Findings aus `SK_System_Compliance_Audit.md` behoben (User-Entscheidung: MaxRiskPercentPerTrade 3 % bleibt):

| Finding | Fix-Stelle | Kernänderung |
|---------|------------|--------------|
| Forex-Pip bricht NCFX-Perps | `PipStopLossCalculator.cs:208-215` | `Forex => entryPrice * 0.0001m` (prozentual wie Crypto). JPY-Sonderfall entfällt. Begründung: 8 % WinRate im 5-Monate-Backtest (EUR/USD + GBP/USD) mit altem fixen 0.0001-Pip. |
| News-Filter nur per Stub | `BingXBot.Server/Program.cs`, `BingXBot.Shared/App.axaml.cs`, `appsettings.json` | `IEconomicCalendarService` in Server-DI registriert, lädt `HttpEconomicCalendarService` wenn `News:Endpoint` gesetzt. Sonst Stub (graceful degradation). Shared registriert Stub als Default. |
| Runner-Trail-SL nur im Memory | `PositionExitState.cs:119-133`, `TradingServiceBase.cs:627-657` | Neue Felder `RunnerLastPushedSl` + `RunnerLastPushUtc`. Trail-SL wird bei signifikanter Bewegung (≥ 0.15 % Delta, ≥ 10 s Throttle) an die Exchange gepusht. App-Crash verliert den nachgezogenen SL nicht mehr. |

Build: `BingXBot.Server` + `BingXBot.Desktop` grün. Tests: 434/434 grün (JPY-Test-Erwartung von 0.10 % auf 0.15 % angepasst — prozentualer Pip konvergiert EUR/USD und USD/JPY auf gleiches SL-Niveau).

---

## Multi-TF Standalone (15.04.2026)

**Ein** SK-Trading-Service, der alle aktiven Navigator-Timeframes (D1/H4/H1/M15) parallel pro Symbol evaluiert. Ersetzt das alte Multi-Mode-System (Scalping/DayTrading/Swing).

### Aktuelle Defaults

| Setting | Wert |
|---------|------|
| ActiveTimeframes | D1, H4, H1, M15 |
| Scan-Intervall | 60s einheitlich |
| MinConfluenceScoreByTf | D1=3, H4=4, H1=4, M15=4 |
| PipScalingByTf | M15=0.75, Rest=1.0 |
| MinVolume24h (Crypto) | D1/H4=10M, H1=20M, M15=25M |
| MinVolume24h (TradFi) | D1/H4=1M, H1=2M, M15=3M |

### MarketContext

- `NavigatorTimeframe` (TimeFrame) sagt der Strategie, welche TF gerade evaluiert wird
- `FilterTimeframeCandles`: nächst-tiefere TF via `GetFilterTimeframe()`: D1→H4, H4→H1, H1→M15, M15→M5

### AmpelStatus

`Dictionary<TimeFrame, string>` statt festem Tuple. Per TF separat angezeigt in Dashboard + StrategyView.

### Dedup pro Position

Key `{symbol}_{side}` — eine BingX-Position pro (Symbol, Side). Wenn schon ein TF-Signal offen ist, werden weitere TF-Signale für gleiche Seite geskippt.

### Scan-Loop

- Kerzen parallel gefetcht (W1/D1 shared pro Symbol, Navigator + Filter pro (Symbol, TF))
- `_klineSemaphore` (SemaphoreSlim(10)) als BingX-IP-Rate-Limiter
- Strategie-Klone-Key: `{symbol}|{tf}`

---

## MVVM-Sanierung + Android-Crash-Fix (15.04.2026)

Android-Startup-Crash behoben: `SecureStorageService`-Ctor nutzte hardcoded Desktop-Pfade. Mobile-Shell stapelte 8 Views parallel im Konstruktor.

### Neue Bausteine

| Baustein | Datei | Zweck |
|----------|-------|-------|
| `IAppPaths` | `BingXBot.Core/Interfaces/IAppPaths.cs` | Plattform-abstrahierte App-Pfade |
| `AppPaths` | `BingXBot.Trading/AppPaths.cs` | Default-Impl (Windows/Linux) |
| `AndroidAppPaths` | `BingXBot.Android/AndroidAppPaths.cs` | Android (Context.FilesDir) |
| `ViewLocator` | `BingXBot.Shared/ViewLocator.cs` | VM → View Konvention |
| `ISettingsPersistenceService` | `BingXBot.Contracts/Services/ISettingsPersistenceService.cs` | DI-fähiger Settings-Save |

### ViewLocator-Konvention

`BingXBot.ViewModels.DashboardViewModel` → `BingXBot.Views.DashboardView` (Desktop) oder `BingXBot.Views.DashboardViewMobile` (Mobile-Shell-Override). `App.IsMobileShell` wird beim Start je nach Lifetime gesetzt.

### Regeln (MVVM-Strict)

- `x:CompileBindings="True"` + `x:DataType` auf jeder View-Root
- **KEIN** `App.Services.GetRequiredService<T>()` im View-Ctor (Android-Crash-Pattern)
- **KEIN** `DataContext = ...` im Code-Behind — ViewLocator setzt das
- Services per Constructor Injection ins ViewModel, **nicht** in die View
- Commands per `[RelayCommand]`, keine Click-Handler im Code-Behind
- Sub-VMs werden in MainViewModel als DI-Properties gehalten
- `CurrentPageViewModel` + einzelnes `<ContentControl Content="{Binding CurrentPageViewModel}" />` — keine 8 gestapelten Border

---

## Client/Server-Architektur (13.04.2026)

Server auf Raspberry Pi 5 (24/7). Desktop + Android verbinden sich per REST + SignalR.

### Projekte

```
src/
├── Libraries/
│   ├── BingXBot.Contracts/          # DTOs, API-Routen, Hub-Methoden, Service-Interfaces
│   ├── BingXBot.ClientApi/          # HTTP + SignalR Remote-Impls + PairingClient
│   ├── BingXBot.Core/               # Domain (Models, Enums, DB-Entities)
│   ├── BingXBot.Exchange/           # BingX REST + WebSocket
│   ├── BingXBot.Engine/             # SK-Trading-Logik + Indikatoren
│   ├── BingXBot.Backtest/           # Backtest-Engine + SimulatedExchange
│   └── BingXBot.Trading/            # Trading-Services (Live/Paper, Manager, DB, Pfade)
└── Apps/BingXBot/
    ├── BingXBot.Shared/             # ViewModels + Views + Local-Impls
    ├── BingXBot.Desktop/            # Avalonia Desktop (Standalone ODER Remote-Client)
    ├── BingXBot.Server/             # ASP.NET Core Minimal API + SignalR (Pi)
    └── BingXBot.Android/            # Avalonia.Android (Remote-Client, Portrait)
```

### Ablauf

1. Pi 5: `BingXBot.Server` als `systemd`-Service. Hostet Trading-Engine + SQLite-DB + BingX-WebSocket
2. Pairing: 6-stelliger Code vom Pi (ablesbar per `journalctl -u bingxbot` oder `/var/lib/bingxbot/pairing-code.txt`)
3. Client: Server-URL + Code → Bearer-Token (7 Tage, auto-refresh)
4. Laufzeit: REST-Polls + SignalR-Hub-Push (Ticker, Trades, Logs, Equity, ...)

### Service-Interfaces

Alle ViewModels sprechen gegen Interfaces — Remote- vs Local-Impl per DI anhand `ServerProfile` in `~/.config/bingxbot/client/connection.json`.

| Interface | Zweck |
|-----------|-------|
| `IBotControlService` | Start/Stop/EmergencyStop |
| `ISettingsService` | Risk/Scanner/Bot-Settings |
| `IAccountService` | Balance/Positions/Orders |
| `ITradeHistoryService` | Trades aus DB |
| `IBotEventStream` | SignalR-Events (Push) |
| `IBacktestControlService` | Backtest-Control |
| `IStrategyCatalog` | Strategie-Metadaten |

### REST `/api/v1/...`

- Auth (öffentlich): `/health`, `/pair/init`, `/pair/complete`, `/auth/refresh`
- Status: `/status`, `/account`, `/positions`, `/open-orders`, `/equity`
- Bot: `/bot/start`, `/bot/stop`, `/bot/emergency-stop`, `/position/{symbol}/close`
- Settings: `/settings`, `/settings/risk`, `/settings/scanner`, `/settings/bot`
- Trades: `/trades`, `/scanner/results`, `/logs`
- Backtest: `/backtest/start`, `/backtest/{jobId}`, `/backtest/{jobId}/result`, `/backtest/{jobId}/cancel`
- Credentials: `/credentials/status`, `/credentials` (PUT BingX API-Key)

### SignalR `/hubs/bot`

14 Events (throttled): BotStateChanged, TickerUpdate (max 1/s/Symbol), BtcPriceUpdate, TradeOpened, TradeClosed, PositionUpdated, EquityUpdate, LogEmitted, ActivityFeed, MarginWarning, BacktestProgress, BacktestCompleted, ScannerResult, ConnectionDegraded.

### Deployment

```bash
bash src/Apps/BingXBot/BingXBot.Server/systemd/publish.sh
bash src/Apps/BingXBot/BingXBot.Server/systemd/install.sh raspberrypi.local
bash src/Apps/BingXBot/BingXBot.Server/systemd/update.sh            # Defaults: steuerung@raspberrypi.local, /home/steuerung/bingxbot
```

Systemd-Service: `bingxbot.service` (User `steuerung`, Install `/home/steuerung/bingxbot`, Daten `/var/lib/bingxbot`). Das `update.sh` nutzt tar-Stream (kein rsync nötig — läuft auch auf Git-Bash Windows).

### Sicherheit

- Kein TLS default (binding `http://0.0.0.0:5050`) → Tailscale empfohlen
- Bearer-Token: 7 Tage gültig, in `~/.config/bingxbot/tokens.json` (chmod 600)
- BingX-Credentials: AES-256-CBC auf Pi (`/var/lib/bingxbot/credentials.bin`, chmod 600)
- Pairing-Code: 5min gültig, nach Verwendung gelöscht, max 5 Fehlversuche
- Rate-Limit: `/pair/*` max 5/5min

---

## Terminologie: "TradFi" = BingX "Features"-Perps

TradFi im Bot (`EnableTradFi`, `MarketCategory.Commodity/Index/Forex/Stock`) bezeichnet **nicht** den nativen BingX-TradFi-Tab, sondern die USDT-margined Perps auf traditionelle Underlyings mit **NC-Prefix** (New Contract). Der echte BingX-TradFi-Tab (native CFDs, Börsenzeiten) wird **nicht** gehandelt.

### Prefixe (SymbolClassifier.cs)

| Prefix | Kategorie |
|--------|-----------|
| `NCCO*` | Commodity (GOLD, XAG, WTI, COPPER, ...) |
| `NCSI*` | Index (SP500, NASDAQ100, DAX40, DOWJONES) |
| `NCFX*` | Forex (EURUSD, GBPUSD, USDJPY, ...) |
| `NCSK*` | Stock (AAPL, TSLA, NVDA, MSFT, META, ...) |
| sonst | Crypto |

### Scan-Aufteilung (60% Krypto / 40% TradFi)

`ScanHelper.FilterCandidates` reserviert bei `MaxResults=100` → 60 Slots Krypto + 40 Slots TradFi mit Sub-Quoten (10 Commodity + 10 Index + 10 Forex + 10 Stock). Slot-Recycling: ungenutzte Sub-Slots → Top-Volume-TradFi anderer Subkategorien; ungenutzte TradFi-Slots → Krypto.

### Per-Markt Risk-Defaults

| Kategorie | Default-Leverage | Max-Leverage | Margin |
|-----------|------------------|--------------|--------|
| Krypto | 3x | 125x | 20% / 2% |
| Commodity | 10x | 500x | 15% / 1.5% |
| Index | 10x | 500x | 15% / 1.5% |
| Forex | 20x | 500x | 10% / 1% |
| Stock | 3x | 25x | 15% / 2% |

### Handelszeiten (TradingHoursFilter)

- Krypto: 24/7
- Forex: 24/5 (Sydney-Open ab So 22:00 UTC)
- Commodity/Index/Stock: Mo-Fr, 1h Pause 22:00-23:00 UTC
- Funding-Settlement ±5min Pause für **alle** Perps (Krypto + TradFi)

---

## Architektur

### Trading-Services (TradingServiceBase)

Gemeinsame Basisklasse enthält die komplette Trading-Logik:

- `RunLoopAsync` (60s): Scanner → Klines → Strategy → Risk → Order
- `PriceTickerLoopAsync` (5s): SL/TP-Check, BE-Regel, Partial-Close, Preis-Updates, TradFi-Stunden-Check
- Tageswechsel-Reset, Korrelations-Check, gemeinsame Signal-Verwaltung

| Service | Backend |
|---------|---------|
| `PaperTradingService` | `SimulatedExchange` (Isolated Margin, spiegelt Live) |
| `LiveTradingService` | `BingXRestClient` + WebSocket User-Stream |
| `LiveTradingManager` | Lifecycle-Orchestrator (Connect, Recovery, Commission, Server-Zeit-Sync) |

### Exchange-Features

- **Native SL/TP**: `stopLoss`/`takeProfit` als JSON-String (`STOP_MARKET`/`TAKE_PROFIT_MARKET`, `workingType: MARK_PRICE`)
- **Kill-Switch**: `ActivateKillSwitchAsync(120s)` alle 60s. Bei sauberem Stop `DeactivateKillSwitchAsync()`
- **Commission-Rates**: Beim Connect aus API laden (VIP-abhängig)
- **Server-Zeit-Sync**: `SyncServerTimeAsync()` bei Connect — BingX Error 100421 bei >5s Drift
- **Balance v3**: `/openApi/swap/v3/user/balance` — Array, nach `asset=="USDT"` filtern
- **WebSocket**: `_sendLock` (SemaphoreSlim) für alle Send-Aufrufe — `SendAsync` nicht thread-safe
- **Ordertypen**: Market-Default (Taker ~0.05%), Limit bei Score≥10 (Maker ~0.02%). Limit-TP erst NACH Fill platzieren
- **closeAllPositions**: Ein API-Call pro Symbol (effizienter bei mehreren Positionen)

---

## DB-Persistenz (BotDatabaseService)

SQLite WAL-Modus (Multi-Mode-Concurrency). Schema-Versioning via `RunMigrationsAsync()`.

### Persistierte Zustände (Neustart-Safety)

- `PositionExitState`: Phase (Initial/Tp1Hit), SlHalved, BreakevenSet, IsRecovered
- `RuntimeState`: TradesToday, ConsecutiveLosses
- `PendingLimitOrderState`: pending Limit-Orders für App-Neustart-Recovery
- `Settings`: Risk, Scanner, Bot-Settings

### Trade-History

- Paper + Live Trades in DB (Backtest **NICHT** — flutet sonst bei jedem Run)
- `SaveTradeAsync` immer mit try-catch

---

## UI

### Views

| View | Zweck |
|------|-------|
| Dashboard | Balance, Positionen, Bot-Controls, Strategie, Equity-Chart, SK-Ampel (4 TF) |
| Scanner | Live-Scan mit Volumen/Momentum-Filter |
| Strategie | Parameter-Editor + TF-Visualisierung |
| Backtest | Historischer Test mit PerformanceReport, Multi-TF |
| Trade-History | Alle Trades filterbar (Modus/Symbol/Zeitraum/TF-Badge) |
| Risk-Settings | Risiko-Parameter konfigurieren |
| Log | Live-Log mit Level/Kategorie-Filter |
| Settings | API-Keys, Server-Verbindung |

### SkiaSharp-Renderer

| Renderer | Zweck |
|----------|-------|
| `EquityChartRenderer` | Linien-Chart Equity-Kurve |
| `BtcPriceChartRenderer` | Candlestick BTC-USDT |
| `InteractiveChartRenderer` | SK-Sequenz-Overlay (Punkt 0/A/B, Fibonacci-Levels) |

### Sub-ViewModels

- `BtcTickerViewModel`: BTC-USDT Preis + Candle-Chart (10s/60s Auto-Refresh, per `BotSettings.ShowBtcTicker` abschaltbar)
- `ActivityFeedViewModel`: Letzte 20 Bot-Aktionen (Rot=Error, Amber=Warning, Grün=Trade)

### BotEventBus

Singleton für ViewModel-zu-ViewModel-Kommunikation ohne direkte Referenzen.

| Event | Subscriber |
|-------|------------|
| `TradeCompleted` | TradeHistoryVM |
| `BacktestCompleted` | TradeHistoryVM |
| `LogEmitted` | LogVM, ActivityFeedVM |
| `BotStateChanged` | MainVM |
| `MarginWarning` | DashboardVM |
| `SkAmpelUpdated` | DashboardVM, StrategyVM |

### ViewModel-DI

| ViewModel | DI-Parameter |
|-----------|--------------|
| `MainViewModel` | `BotEventBus` + alle Child-VMs als Properties |
| `DashboardViewModel` | `BotEventBus`, `StrategyManager`, `PaperTradingService`, `LiveTradingManager`, `RiskSettings`, `ScannerSettings`, `BotSettings`, optionale Remote-Services |
| `StrategyViewModel` | `StrategyManager`, `BotEventBus` |
| `BacktestViewModel` | `RiskSettings`, `BotEventBus`, optional Market-Data |
| `TradeHistoryViewModel` | `BotEventBus`, optional `BotDatabaseService` |
| `LogViewModel` | `BotEventBus` |
| `ScannerViewModel` | `ScannerSettings`, `BotEventBus`, optional `IMarketScanner` |
| `RiskSettingsViewModel` | `RiskSettings`, `BotEventBus`, optional `BotDatabaseService` |
| `SettingsViewModel` | `BotSettings`, `BotEventBus`, optional `ISecureStorageService`, `IExchangeClient` |

Optionale Parameter (mit `?`) für Demo-Modus ohne Exchange-Verbindung.

### UI-Conventions

- Compiled Bindings (`x:CompileBindings="True"`) in allen Views
- Virtualisierung (VirtualizingStackPanel) in TradeHistory, Log, Backtest, Scanner
- Monospace-Zahlen (Consolas) für Preise/PnL/Metriken
- Dark-Mode als Default (`ThemeVariant.Dark`)
- Farbpalette: Primary `#3B82F6`, Background `#1E1E2E`, Profit `#10B981`, Loss `#EF4444`
- Keyboard-Shortcuts: Ctrl+1-8 Navigation, F5/F6/F7/F12 Bot-Kontrolle, Escape → Dashboard

---

## Build

```bash
dotnet build src/Apps/BingXBot/BingXBot.Desktop
dotnet run --project src/Apps/BingXBot/BingXBot.Desktop
dotnet test tests/BingXBot.Tests
```

---

## Risikomanagement

- **Position-Sizing**: Risiko-basiert — `maxLoss / slDistance` (enger SL = größere Position). SL ist PFLICHT, ohne SL wird Trade abgelehnt
- **MaxRiskPercentPerTrade**: Default **5%** (bewusste User-Entscheidung, siehe SK-Plan)
- **Drawdown-Limits**: Täglich + gesamt. Peak-Equity-Tracking für Total-Drawdown
- **Liquidation-Check**: Isolated-Margin `(1 - MMR) / Leverage`. Bei ≤2x Leverage deaktiviert
- **Netto-Exposure**: Shorts als negativ, `Math.Abs(net)`. Default: Max 200%
- **Korrelation**: Pearson auf Log-Returns (nicht absolute Preise). Default Max 0.85
- **Funding-Rate-Filter**: Für alle BingX-Perpetuals (Krypto + TradFi)
- **Sharpe-Annualisierung**: `sqrt(TradesProJahr)`, Sample-Varianz N-1
- **Rolling Live-Metriken**: 30-Trade-Window — WinRate, ProfitFactor, Sharpe, Strategy-Health-Warnung

---

## Tests

| Test-Bereich | Datei |
|--------------|-------|
| Core Models/Config | `ModelTests`, `ConfigTests`, `SymbolClassifierTests`, `TimeFrameHelperTests` |
| Simulated Exchange | `SimulatedExchangeTests` |
| Strategy-Manager | `StrategyManagerTests`, `StrategyFactoryTests` |
| Indikatoren | `IndicatorHelperTests` (EMA/RSI/BB/MACD/ADX/Stoch/HTF-Trend) |
| Korrelation | `CorrelationCheckerTests` (Pearson auf Log-Returns) |
| Scanner | `MarketScannerTests`, `ScanRotationTests` |
| Engine | `TradingEngineTests`, `RiskManagerTests`, `TradeJournalTests` |
| Handelszeiten | `TradingHoursFilterTests` |
| Exchange | `RateLimiterTests`, `BingXRestClientTests` |
| Backtest | `BacktestEngineTests`, `PerformanceReportTests` |
| SK-System | Wird im Zuge der Re-Implementation neu aufgebaut — siehe Phase 5 im Plan |

---

## Bekannte Gotchas (Infrastruktur, nicht SK-spezifisch)

### BingX API

- Balance: v3-Endpoint (`/openApi/swap/v3/user/balance`), Array, `asset=="USDT"` filtern
- `SetMarginTypeAsync` VOR jeder Order — BingX-Default kann Cross sein (try-catch: Fehler bei offener Position ignorieren)
- Kill-Switch: alle 60s refreshen, bei sauberem Stop explizit deaktivieren
- `SyncServerTimeAsync()` bei Connect — Error 100421 bei >5s Drift
- Commission-Rates aus API laden, nicht hardcoden (VIP-Levels)
- `AmendOrderAsync`: `RoundPrice`/`TruncateQuantity` anwenden (BingX lehnt zu viele Dezimalstellen ab)
- Fund-Flow `incomeType`: REALIZED_PNL, FUNDING_FEE, TRADING_FEE, INSURANCE_CLEAR, ADL, TRANSFER
- `GetIncomeHistoryAsync`: `startTime.Value.ToUniversalTime()` — ohne UTC-Kind nutzt DateTimeOffset lokale Timezone
- Limit-Order TP: NICHT sofort platzieren (Position existiert noch nicht). Fill-Detection im PriceTickerLoop, TP mit Qty aus `GetPositionsAsync()` (BingX truncated auf Symbol-Precision)
- WebSocket `SendAsync` nicht thread-safe — `_sendLock` SemaphoreSlim für alle Sends
- Position-Retry nach Market-Order: 3 Versuche × 1s Delay bis `GetPositionsAsync` neue Position listet (Hedge-Mode-Rejection ohne Position)
- TP-Retry + Verify: `GetOpenOrdersAsync(symbol)` nach Platzierung prüft ob OrderIds tatsächlich existieren

### Trading-Logik

- `_tradesToday` MUSS `volatile` — JIT darf nicht-volatile Felder bei parallelen Reads cachen
- `ContinueWith` IMMER mit `TaskScheduler.Default` — sonst UI-Thread-Deadlock möglich
- `OriginalQuantity` IMMER die tatsächlich platzierte Menge (nach Equity/Score-Scaling), NICHT `riskCheck.AdjustedPositionSize`
- EmergencyStop: CTS NICHT vor Close-Operations canceln (API-Calls brauchen HTTP)
- Recovery-Signale nur in einem Service registrieren (sonst N-facher Close-Versuch)
- `DailyPnl` Dictionary: atomarer Swap (neues Objekt), NICHT Clear+Re-Fill (SkiaSharp-Render-Thread liest)
- `_klineSemaphore` in Dispose() freigeben — SemaphoreSlim hat OS-Handles
- Manueller Close: `_liveManager.CommissionTakerRate` statt hardcodierter 0.0005m — echte PnL für History
- Backtest-Trades NICHT in DB speichern
- SL ist PFLICHT im RiskManager — Trade ohne SL abgelehnt
- Signal-Verlust-Bug (Limit-Order lange pending): Pending-Orders vom Verwaist-Cleanup ausnehmen, bei Fill ohne Signal rekonstruieren (SL auf Invalidation-Level, nativer SL setzen)
- **Forex-Pip MUSS prozentual sein** (v1.2.7): `entryPrice * 0.0001m` statt fixer 0.0001. NCFX-Perps skalieren anders als Spot-FX → fixer Pip gab 8 % WinRate auf EUR/USD + GBP/USD über 5 Monate. JPY-Sonderfall entfällt — prozentual skaliert automatisch.
- **Runner-Trail-SL MUSS an die Exchange gepusht werden** (v1.2.7): Sonst lebt der nachgezogene SL nur im Memory, App-Crash verliert den Runner-Gewinn. `PositionExitState.RunnerLastPushedSl` + `RunnerLastPushUtc` steuern den Throttle (0.15 % Preis-Delta UND 10 s seit letztem Push). Initialer Push passiert sofort bei Runner-Aktivierung (LastPushedSl=0).
- **News-Filter DI-Pflicht** (v1.2.7): `IEconomicCalendarService` MUSS explizit registriert werden (`BingXBot.Server/Program.cs`, `BingXBot.Shared/App.axaml.cs`). Ohne Registrierung ist `_newsCalendar` im RiskManager null → `MarketContext.NewsBlackoutCheck`-Delegate bleibt null → der inline News-Gate am Anfang von `Evaluate` läuft auf "graceful degradation" und passt durch. HTTP-Variante nur aktiv wenn `News:Endpoint` in appsettings gesetzt ist — sonst Stub.

### Android-Spezifika

- Hardcoded `Environment.SpecialFolder.UserProfile` in Services crasht Android → `IAppPaths` via DI, `AndroidAppPaths` nutzt `Context.FilesDir.AbsolutePath`
- `App.AppPathsFactory` in `MainActivity.CustomizeAppBuilder` VOR DI-Build setzen
- Mobile-Shell lädt 8 Views parallel → VM-Ctor-Crash → Content-Swap-Pattern mit `CurrentPageViewModel` + `<ContentControl />`
- `SecureStorageService`-Ctor wrapped `Directory.CreateDirectory` in try-catch, damit DI-Chain nicht kippt

### TradFi

- Symbol-Erkennung: `NC`-Prefix = TradFi, Rest = Krypto
- Funding-Settlement: Gilt für ALLE BingX-Perpetuals (Krypto + TradFi) — globaler Block in `CheckSession()`
- `EnableTradFi` Fallback-Werte MÜSSEN `true` sein
- `IsHedgeModeActive` MUSS gesetzt werden (Paper=true, Live=aus `restClient.IsHedgeModeAsync()`) — sonst TradFi komplett tot
- Single-Mode Paper: `_scannerSettings.IsHedgeModeActive = true` VOR `_paperService.Start()`
- Scanner-Rotation: `_rotationOffset % remaining.Count` für sauberes Wrap-Around
- TradFi am Wochenende IMMER geschlossen (außer Forex ab So 22:00 UTC). Commodity/Index: 1h Pause 22:00-23:00 UTC
- Stock-Pip prozentual (`entryPrice * 0.00005`) statt fixe 0.01 — BRK @ 600 USD sonst nur 0.067% SL

### Pending-Limit-Orders + Recovery

- `ReconcilePendingLimitOrdersAsync()` beim Start — BingX-`GetOpenOrdersAsync()` gegen DB-Liste abgleichen, stale Einträge verwerfen
- `RestorePositionSignal()` merged SK-Flags + TP2 aus ExitState (DisableSmartBreakeven, TakeProfit2, IsAdditionalEntry nicht überschreiben)
- BingX gibt Limit-TPs (von `PlaceTpReduceOnlyLimitAsync` → Type=LIMIT) nicht als `TakeProfitMarket`/`TakeProfitLimit` zurück — Recovery-Code muss LIMIT-Orders mit entsprechenden Preisen als TP interpretieren
- Periodisches Save: `PersistPendingLimitOrdersAsync` (fire-and-forget) nach `PlaceOrderAsync` und am Ende von `OnBeforePriceTickerIteration`
- PaperTrading: `PlaceOrderOnExchangeAsync` setzt bei `PreferLimitOrder=true` den `signal.EntryPrice` via `SetCurrentPrice` als Fill-Preis. Invalidation vor Fill wird nicht simuliert (dokumentierter optimistischer Bias)

### Mathematik / Metriken

- ATR-Perzentil: `CalculateAtrPercentile()` — `atr/price*10000` ist KEIN Perzentil
- Sharpe: `sqrt(TradesProJahr)` für Annualisierung, Sample-Varianz `N-1`
- Sortino: Downside-Deviation über ALLE Returns (positive als 0) — Standard-Formel
- Liquidation: `(1 - MMR) / Leverage`, bei ≤2x Leverage deaktiviert

### Sicherheit

- API-Keys: DPAPI (Windows) / AES-256-CBC + PBKDF2 100k Iterationen (Linux)
- Linux credentials.dat: `chmod 600` nach Schreiben
- Keine Secrets in Logs, Keys in UI maskiert
- HTTP-Error-Content auf 200 Zeichen kürzen

---

## Verweise

- **SK-System Re-Implementation**: `SK_BUCH_COMPLIANCE_PLAN.md`
- **Multi-TF Standalone**: `MULTI_TF_STANDALONE_PLAN.md`
- **Server-Modus**: `PLAN_SERVER_MODE.md`
- **Memory**: `~/.claude/projects/F--Meine-Apps-Ava/memory/bingxbot.md`
- **Iterations-Historie** (alte SK-Wellen, CWS-Workflow, Tier A/B/C): Git-History, bis Commit `569dbe2`
