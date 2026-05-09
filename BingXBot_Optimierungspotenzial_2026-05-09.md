# BingXBot v1.7.0 — Optimierungspotenzial-Analyse (09.05.2026)

> **Ausgangslage**: Phasen 0-17 aus OPTIMIZATION_PLAN_2026-05 sind alle umgesetzt, 627/627 Tests grün, Pi-Deploy steht aus. Diese Analyse sucht **darüber hinaus** nach konkreten, code-belegten Lücken — keine Wiederholung dessen, was schon erledigt ist.

## Methodik

Direkte Sichtung der Kern-Dateien:
- `BingXBot.Engine/Strategies/SequenzKonzeptStrategy.cs`
- `BingXBot.Engine/Strategies/Confluence/SkConfluenceScorer.cs`
- `BingXBot.Engine/Risk/RiskManager.cs`
- `BingXBot.Trading/Resilience/OrderRetryPolicy.cs`
- `BingXBot.Server/Services/ServerHealthWatchdog.cs`

Die Beobachtungen sind zeilengenau belegt. Aufwandsschätzung: **S** (Stunden), **M** (Tag), **L** (Woche), **XL** (Sprint).

---

## 1. Top-5 Optimierungen mit größtem Trading-Edge-Hebel

### 1.1 [KRITISCHER BUG] Loss-Streak-Dampening + Equity-Scaling sind toter Code

**WO**: `BingXBot.Engine/Risk/RiskManager.cs:309-313`

```csharp
public decimal GetPositionScalingFactor(AccountInfo account)
{
    decimal factor = 1m;
    return factor;   // ❌ Doc-String verspricht Loss-Streak-Dampening + Equity-Curve-Scaling
}
```

**WAS**: Die Methode ist laut XML-Doc verantwortlich für (a) **SK-Plan 4.8 Loss-Streak-Dampening** (≥3 Verluste → 0.5×, ≥5 → 0 = Pause) und (b) **SK-Plan 5.1 Equity-Curve-Scaling** (Drawdown ab Schwelle → linear runter bis 0.5×). Implementiert ist NICHTS — die Methode gibt immer `1.0m` zurück.

**WARUM**: Das ist ein **Riskmanagement-Schutzmechanismus**, der genau in den Momenten greifen soll, in denen die Strategy degradiert (Phase 17 erkennt das auf TF-Ebene, aber nicht auf Position-Sizing-Ebene). Bei einer Verlust-Serie kommt jeder neue Trade mit voller Größe — aber die Wahrscheinlichkeit eines weiteren Verlusts ist nach 3-4 Verlusten signifikant höher (Streak-Persistence ist statistisch belegt). Außerdem ist es im Buch S.13 explizit gefordert. **Tests hätten diesen toten Pfad sofort gefunden** — das deutet auf ein Test-Coverage-Loch hin (Snapshot-Test mit hardcoded `1.0m`?).

**WIE**:
```csharp
public decimal GetPositionScalingFactor(AccountInfo account)
{
    decimal factor = 1m;
    // 4.8: Loss-Streak (Buch S.13)
    if (CurrentConsecutiveLosses >= 5) return 0m;          // Pause
    if (CurrentConsecutiveLosses >= 3) factor *= 0.5m;     // halbieren
    // 5.1: Equity-Curve (linear)
    if (_peakEquityInitialized && _peakEquity > 0)
    {
        var equity = account.Balance + account.UnrealizedPnl;
        var ddPct = (_peakEquity - equity) / _peakEquity * 100m;
        var ddThreshold = _settings.EquityCurveScalingThresholdPercent; // neu
        if (ddPct > ddThreshold)
        {
            var lerp = Math.Min(1m, (ddPct - ddThreshold) / 10m);
            factor *= 1m - 0.5m * lerp;
        }
    }
    return Math.Max(0m, factor);
}
```

**AUFWAND**: M (1 Tag inkl. Settings + 8 Tests + UI-Toggle in RiskSettingsView).
**ABHÄNGIGKEIT**: Keine — eigenständig. Komplementär zu Phase 17 (TF-Disable).

---

### 1.2 Korrelations-Filter zwischen offenen Positionen fehlt

**WO**: `RiskManager.ValidateTrade(...)` — kein Aufruf vorhanden. `MaxOpenPositions` (Zeile 64) und `MaxOpenPositionsPerSymbol` (Zeile 69) sind die einzigen Limits.

**WAS**: BTC-USDT, ETH-USDT, SOL-USDT korrelieren in Crypto historisch zu 0.7-0.9. Wenn der Bot drei Long-Positionen auf BTC/ETH/SOL gleichzeitig hält, ist das praktisch eine 3× gehebelte BTC-Position. Bei einem BTC-Crash gehen alle drei gleichzeitig in den SL.

**WARUM**: Das ist **Phase-0-Risiko-Niveau** (Konto-Schutz) und wird in keiner der Phasen 0-17 adressiert. Ein einzelner BTC-Flash-Crash-Event kann den `MaxDailyDrawdownPercent`-Circuit zwar irgendwann auslösen, aber bis dahin sind alle korrelierten SLs schon ausgelöst. Effektives Risiko = MaxRiskPerTrade × N_korrelierte_Positionen.

**WIE**:
1. Statische Asset-Cluster-Map (BTC-Major / ETH-Major / Alt-L1 / Alt-DeFi / Meme) als Default.
2. Optional dynamische Korrelations-Berechnung auf 30d-Basis (1× pro Tag refreshen, in Cache).
3. Neuer Settings-Wert `MaxCorrelatedExposurePercent` (z.B. 30 % der Wallet pro Cluster).
4. In `ValidateTrade(...)`: `var clusterExposure = OpenPositions.Where(p => SameCluster(p.Symbol, signal.Symbol)).Sum(...)`. Reject mit `RejectionReasons.CorrelationLimitExceeded`.

**AUFWAND**: L (3-5 Tage inkl. Cluster-Map + Korrelations-Service + Tests).
**ABHÄNGIGKEIT**: Neue `RejectionReasons`-Konstante (analog Phase 4-Pattern).

---

### 1.3 Volatility-Adjusted Position Sizing fehlt

**WO**: `RiskManager.CalculatePositionSize(...)` Zeilen 287-301.

```csharp
var margin = account.Balance * _settings.MaxPositionSizePercent / 100m * scaleFactor;
var qty = margin * leverage / entryPrice;
```

**WAS**: Position-Size hängt aktuell ausschließlich an `MaxPositionSizePercent` und `MaxRiskPercentPerTrade` (das den `qty` rückwärts ableitet). Die SL-Distanz fließt zwar in das Risiko-Cap ein (Zeile 165-176), aber die **inhärente Volatilität** des Symbols (ATR%) wird nicht berücksichtigt.

**WARUM**: Auf einem hochvolatilen Coin (PEPEUSDT mit 8% ATR%) wird die SL-Distanz größer und damit `posSize` automatisch kleiner — das ist gut. Aber das **erwartete R-Multiple** ist auch volatilitätsabhängig. Bei stabilen Coins (BTC mit 1% ATR%) ist der Edge bei gleicher Setup-Qualität historisch höher als bei Memecoins. **Volatility-Targeting** (jeder Trade hat denselben "Vol-Beitrag") ist Industrie-Standard für systematisches Trading und nicht implementiert.

**WIE**:
```csharp
// Optional in CalculatePositionSize:
if (_settings.EnableVolatilityTargeting && atrPercent > 0)
{
    var targetVol = _settings.VolatilityTargetPercent; // z.B. 2.0
    var volScale = Math.Min(1.5m, targetVol / atrPercent);
    qty *= volScale;
}
```

**AUFWAND**: M (1-2 Tage inkl. Backtest-Validierung).
**ABHÄNGIGKEIT**: Setting-Audit-Trail (Phase 14) übernimmt automatisch.

---

### 1.4 Time-of-Day Session-Filter fehlt

**WO**: `BingXBot.Engine/Filters/TradingHoursFilter.cs` existiert, aber im SK-Hot-Path **nicht aufgerufen** (Grep: nur Tests).

**WAS**: Crypto trades 24/7, aber Liquidität, Volatilität und Setup-Qualität schwanken stark zwischen Asia (00-08 UTC), EU (08-16 UTC) und US (13-22 UTC). Das SK-System wurde im Buch primär für Forex/EU-Session beschrieben. Auf Crypto in der **Asia-Session sind die Bewegungen oft Range-bound** und produzieren False Sequences.

**WARUM**: Phase 5 sammelt bereits Per-TF/Per-Category-Stats. Ein Zusatz "Per-Session-Stats" würde zeigen, dass die Asia-Session vermutlich systematisch schlechtere Win-Rates liefert. Phase 17 (Adaptive TF-Disable) ist die Macro-Antwort — Session-Filter wäre die Micro-Antwort darauf.

**WIE**:
1. Neuer `BotSettings.EnabledSessions` (Bitmask: Asia/EU/US/Overlap).
2. In `TradingServiceBase.RunLoopAsync` vor Strategy-Evaluate: `if (!IsSessionAllowed(now)) skip`.
3. Decision-Trail-Eintrag mit `RejectionReasons.OutsideAllowedSession`.
4. UI: 24h-Heatmap im Dashboard analog Per-TF-Stats-Card.

**AUFWAND**: M (1-2 Tage).
**ABHÄNGIGKEIT**: Phase 5 Stats-Aggregator um Session-Dimension erweitern.

---

### 1.5 Spread/Tick-Size-Awareness bei SL/TP-Platzierung fehlt

**WO**: `SequenzKonzeptStrategy.cs` — SL kommt aus `PipStopLossCalculator`, TP aus Fib-Levels (Extension161.8 / Extension200). **Tick-Size des Exchange wird im Calc nicht abgerufen.**

**WAS**: Die berechneten SL/TP-Levels werden 1:1 an BingX gesendet. BingX hat aber einen Tick-Size-Constraint (z.B. `0.001` für SOL, `0.01` für BTC). Wenn der berechnete Wert nicht auf dem Tick-Grid liegt, **rundet BingX silent** — und der echte SL kann mehrere Pips abweichen vom berechneten.

**WARUM**: Bei Memecoins mit 5-stelligen Decimals (PEPE: 0.0000018) kann ein "0.5 Pip Buffer" durch Tick-Rundung zu einem 2-Pip-Buffer werden. Bei engen Setups verfälscht das die Risk-Reward-Berechnung systematisch zu Lasten des Bots. Außerdem: Wenn SL **näher** zum aktuellen Preis gerundet wird, kann er sofort hit werden, bevor der Limit-Entry überhaupt füllt.

**WIE**:
1. `IPublicMarketDataClient.GetSymbolFiltersAsync(symbol)` für Tick-Size cachen (1× pro Symbol pro Stunde).
2. Helper: `RoundToTick(price, side, tickSize, conservative: true)` — bei SL **immer weg vom Entry runden** (Long: down, Short: up), bei TP zum Entry hin.
3. In `SignalResult`-Builder vor Rückgabe anwenden.

**AUFWAND**: M (Tag inkl. Cache + Tests). Hoher Hebel weil bei jedem Trade greifend.
**ABHÄNGIGKEIT**: Keine.

---

## 2. Top-5 Risiko / Resilience-Optimierungen

### 2.1 [BUG-Risiko] Synchroner await im Strategy-Hot-Path → Pi-Deadlock-Gefahr

**WO**: `SequenzKonzeptStrategy.cs:173-174`:

```csharp
var blackoutEvent = context.NewsBlackoutCheck(nowForNews, newsBlackoutMinutes, CancellationToken.None)
    .GetAwaiter().GetResult();
```

**WAS**: `.GetAwaiter().GetResult()` blockiert den aktuellen Thread, bis die `Task` fertig ist. Auf einem **Single-Core-Raspberry-Pi 5** mit `ConfigureAwait(true)`-Defaults (Avalonia-Kontext) ist das ein klassischer **Sync-over-Async-Deadlock-Vektor**, wenn die Task einen `await` ohne `ConfigureAwait(false)` enthält und beim Resumen den Thread will, der gerade blockiert ist.

**WARUM**: Selbst wenn es heute nicht deadlocked, ist es ein **Anti-Pattern** und blockiert den Trading-Loop für die Dauer der HTTP-Round-Trip. In einer 30-Coin-Scan-Iteration kann das einen kompletten Tick (30× HTTP-Round-Trip) ausmachen. Phase 4 war Decision-Trail — aber hier ist das **Decision-Logging selbst eine Race-Condition**.

**WIE**:
- `IStrategy.Evaluate(...)` zu `EvaluateAsync(...)` migrieren (breaking change, aber sauber).
- Oder: News-Blackout-Cache vor dem Loop (1× pro Symbol-Iteration) befüllen, sodass Strategy nur einen synchronen Cache-Lookup macht. Das ist Phase 7-Pattern (Funding-Rates) — copy/paste.

**AUFWAND**: M (Cache-Variante) bis L (Async-Migration).
**ABHÄNGIGKEIT**: Phase 7 (Funding-Cache als Vorbild).

---

### 2.2 OrderRetryPolicy ohne Idempotency-Keys → Doppel-Order-Risiko

**WO**: `BingXBot.Trading/Resilience/OrderRetryPolicy.cs:82-112` (`ExecuteAsync`).

**WAS**: Bei einem Retry nach `TaskCanceledException` (Timeout) ist nicht garantiert, dass die ursprüngliche Order **nicht** eingegangen ist — der Server kann sie geparsed und ausgeführt haben, nur die Response ging verloren. Ein Retry erzeugt dann **zwei Orders**.

**WARUM**: Phase 8 hat zwar das Retry-Pattern eingeführt, aber das CLAUDE.md erwähnt explizit: *"Idempotency-Check via Position-Existenz vor Retry-Place erforderlich, separater Schritt"*. Dieser Schritt fehlt nach wie vor. Ein doppelter Entry mit 5% Risk = 10% Risk auf einem Trade — durchbricht alle Phase-1-Caps. **Disaster-Szenario**.

**WIE**:
1. BingX unterstützt `clientOrderId` (32-Zeichen Hex). Vor dem Retry: derselbe `clientOrderId` wird wiederverwendet — BingX dedupliziert serverseitig (gibt Conflict 110204 zurück, das als Success interpretiert werden muss).
2. ODER: Vor jedem Retry `GetPositionAsync(symbol)`-Probe → wenn Position existiert mit erwarteter Side+Qty, gilt Place als erfolgreich.
3. Test-Szenario: Mock-Client der bei Attempt 1 ein `TaskCanceledException` wirft, danach `GetPositionAsync` eine bereits existente Position liefert. Retry darf KEINE neue Order auslösen.

**AUFWAND**: M (Tag inkl. 6 Tests).
**ABHÄNGIGKEIT**: Phase 8 (Erweiterung), kein Konflikt.

---

### 2.3 Clock-Drift zwischen Pi und BingX nicht überwacht

**WO**: Nirgends — `ServerHealthWatchdog` prüft nur Reachability, nicht Zeit-Sync.

**WAS**: BingX REST verlangt `timestamp` ± `recvWindow` (Default 5000 ms). Der Pi 5 ist auf NTP angewiesen — wenn `chronyd`/`systemd-timesyncd` aussetzt (häufig nach Reboot oder bei Hochlast), driftet die Pi-Uhr. Bei > 5 s Drift werden **alle Orders abgelehnt** mit `Timestamp out of recvWindow`.

**WARUM**: Das ist ein **stille Disaster-Mode** — der Bot läuft, scannt, evaluiert, aber **keine Order kommt durch**. Phase 15 (Active-Probe) erkennt nur Reachability-Probleme, nicht Auth-/Time-Window-Probleme. Der Watchdog würde grün zeigen, während intern alles abgelehnt wird.

**WIE**:
1. Im `ServerHealthWatchdog.CheckAndPushAsync`: `var serverTime = await _publicClient.GetServerTimeAsync()`. Vergleich mit `DateTime.UtcNow.ToUnixTimeMilliseconds()`. Drift > 2 s → Warning, > 4 s → `IsDegraded=true` mit Reason "Clock-Drift".
2. Alternative ohne ServerTime-Endpoint: Aus dem `Date`-Header der HTTP-Response des Tickers-Probe (existiert immer).
3. Recovery-Hint im Event: "sudo systemctl restart chronyd" für Pi-Deploy.

**AUFWAND**: S (4 Stunden).
**ABHÄNGIGKEIT**: Phase 15 (Watchdog-Erweiterung).

---

### 2.4 Disaster-Recovery: Pi-Stromausfall mit offenen Positionen

**WO**: `LiveTradingService.Reconcile.cs` (Phase 3) — Reconcile läuft beim **manuellen Resume**, aber was, wenn der Pi 4 Stunden offline war und in dieser Zeit ein TP getroffen wurde, der Bot aber den Fill verpasste?

**WAS**: Phase 3 hat Missing-Stop-Detection implementiert. Aber:
- **Missing-TP-Detection** fehlt: Wenn die TPs fehlen (z.B. weil sie reduce-only-Limit-Orders waren, die durch einen BingX-Restart gelöscht wurden), läuft die Position ohne Take-Profit weiter.
- **Phantom-Trades** beim Restart: Wenn zwischen Pi-Crash und Restart eine Position bereits geschlossen wurde, kennt der Bot das `CompletedTrade` nicht — die Statistiken (Phase 5) und der `_dailyPnl` (RiskManager Zeile 19) sind falsch.

**WARUM**: Die Stats sind die Grundlage für Phase 17 (TF-Disable). Wenn ein Trade gewonnen hat, aber nicht gezählt wird, kann eine eigentlich profitable TF fälschlicherweise disabled werden.

**WIE**:
1. **Replay-on-Resume**: Beim `BotAutoResumeService`-Start `GetUserTradesAsync(symbol, since: lastKnownTradeTime)` — alle Trades seit letztem Heartbeat in DB einspielen.
2. Letzten Heartbeat persistieren (alle 30 s in `BotState`-Tabelle, Spalte `LastHeartbeatUtc`).
3. Phase-3-Reconcile um Missing-TP-Check ergänzen (analog Missing-Stop, aber ohne Grace-Window — TPs sollen IMMER da sein).

**AUFWAND**: L (3-4 Tage inkl. Heartbeat-Schema-Migration + Tests).
**ABHÄNGIGKEIT**: Phase 3 (Reconcile-Erweiterung).

---

### 2.5 BingX Rate-Limit-Awareness fehlt

**WO**: Nirgends — `OrderRetryPolicy` reagiert nur auf `429` als Retry-Signal. Es gibt keinen **proaktiven** Token-Bucket.

**WAS**: BingX Perpetual-API hat dokumentierte Rate-Limits (z.B. 100 Requests/10s pro IP für Trade-Endpoints). Wenn der Bot 30 Symbole scannt, parallel Klines + Tickers + Funding fetcht, kann er **knapp unter dem Limit** sein. Ein zusätzliches User-Triggered-Action (Backtest-Run via UI während Live-Bot läuft) reißt das Limit. Reaktiv via 429 ist zu spät — die TP-Place-Order in dem Burst kann verloren gehen.

**WARUM**: Phase 8 ist die **reaktive** Antwort. Eine **proaktive** Drosselung wäre `IRateLimiter` (Token-Bucket pro Endpoint-Klasse). Im Zusammenspiel mit Phase 7 (Funding-Cache) und einer geplanten Klines-Cache wären die Bursts kontrollierbar.

**WIE**:
- Library-frei: einfacher `SemaphoreSlim` + `Stopwatch`-basierter Token-Bucket im `BingxRestClient`.
- Pro Request-Klasse (Public / Trade / Account) eigenes Bucket-Pair.
- Bei Token-Erschöpfung: `await _bucket.WaitAsync(ct)`, Tracing-Log.

**AUFWAND**: M (Tag).
**ABHÄNGIGKEIT**: Lebt parallel zu Phase 8.

---

## 3. Top-5 Code-Qualität / Maintainability

### 3.1 Allocation-Pressure im Hot-Path

**WO**:
- `RiskManager.cs:362` (`RecentTrades` getter): `_rollingTrades.ToList()` allokiert **bei jedem Zugriff** eine neue Liste.
- `RiskManager.cs:463`: `_rollingTrades.RemoveAt(0)` ist **O(n)** auf List<T> — Memory-Move bei jedem Trade.
- `SkConfluenceScorer.cs:35-44` (`Reasons` getter): Bei jedem Zugriff neuer `string[]` allokiert.
- `RiskManager.cs:191-192`: `OpenPositions.Sum(...)` mit Lambda allokiert Closure pro Tick.

**WAS**: Kleine Allokationen, aber im Trading-Loop bei N Symbolen × M Ticks/Sekunde × 24/7 summiert sich das zu Gen0-Pressure auf dem Pi (4 GB RAM, langsamer Garbage Collector).

**WARUM**: Pi 5 hat keine SSD-Auslagerung, GC-Pausen direkt sichtbar im Latency-Profil. Ein 50ms-GC-Pause während eines kritischen TP-Place ist eine reale Slippage-Quelle.

**WIE**:
1. `_rollingTrades` → `Queue<CompletedTrade>` (O(1) Dequeue).
2. `RecentTrades` → `IReadOnlyCollection<CompletedTrade>` direkt (mit Lock-Snapshot via `ImmutableArray.Builder` 1× pro UpdateDailyStats).
3. `Reasons` getter → einmaliger Cache pro Add-Call (lazy reset bei Add).
4. Hot-Path-Sum → for-Loop statt LINQ.

**AUFWAND**: M (1 Tag).
**ABHÄNGIGKEIT**: Keine — pure Refactoring-Wins. **Quick-Win-Kategorie**.

---

### 3.2 Magic Numbers ohne Persistenz im SK-Strategy-Hot-Path

**WO**: `SequenzKonzeptStrategy.cs`:
- `_swingStrength = 5` (Zeile 40) — als Parameter exposed, aber default hardcoded.
- `_minConfluence = 3` (Zeile 46) — laut Doc "BUCH-ONLY: Wird aktuell nicht als Hard-Threshold genutzt".
- `BcklReEntryCooldownCandles = 2` (Zeile 82) — hardcoded const.
- `_swingStrength * 2 + 20` (Zeile 158) — Magic-Formel, unklar warum.

**WAS**: Diese Werte beeinflussen direkt die Signal-Häufigkeit. Sie sind nicht in `ScannerSettings` hinterlegt, also **nicht über die UI änderbar** und **nicht im Settings-Audit-Trail (Phase 14)** erfasst.

**WARUM**: Walk-Forward-Backtest (Phase 6) kann diese Werte nicht optimieren, weil sie nicht im Settings-Space liegen. Dadurch verpasst der Bot eine wichtige Optimierungsdimension.

**WIE**: Alle vier Werte als `ScannerSettings`-Properties exposed, im Strategy-Constructor als Argument durchreichen, Walk-Forward-Runner um Param-Sweep erweitern.

**AUFWAND**: M (1 Tag).
**ABHÄNGIGKEIT**: Phase 6 + Phase 14 (Settings-Audit erfasst sie automatisch).

---

### 3.3 Partial-Class-Wildwuchs in LiveTradingService

**WO**:
- `LiveTradingService.cs` (Hauptklasse)
- `LiveTradingService.OrderPlacement.cs`
- `LiveTradingService.PendingLimitOrders.cs`
- `LiveTradingService.Reconcile.cs`
- `LiveTradingService.SlTpManager.cs`
- `LiveTradingService.WebSocket.cs`

**WAS**: 6 partial-Files für eine Klasse signalisieren "God Object". Jede hat eigenen State, eigene Felder, eigene Locks. Das Coupling wird durch die Partials nur **versteckt**, nicht aufgelöst.

**WARUM**: Tests müssen die ganze Klasse instanziieren. Refactoring eines Aspekts (z.B. SL/TP-Manager) bricht andere Aspekte (Reconcile). MVVM-Auditor + Code-Review-Agent würde das als Antipattern flaggen.

**WIE**: Strategische Extraktion zu Composition:
- `IPendingLimitOrderManager` (Interface) mit eigener Implementation.
- `IReconciler` (Interface) mit eigener Implementation.
- `LiveTradingService` orchestriert nur noch Aufrufe in fester Reihenfolge.
- Vorhandene Tests (`PendingLimitReconcileIntegrationTests`) gegen die neuen Interfaces re-testen — gibt erstmal direkten Coverage-Boost.

**AUFWAND**: XL (Sprint-Aufwand). **Aber**: niedriger Risk weil 627 Tests den Refactor absichern.
**ABHÄNGIGKEIT**: Risiko-arm wegen Test-Suite. Sollte nach Pi-Deploy + Live-Verifikation der Phase-17-Features kommen.

---

### 3.4 News-Blackout-Exception-Swallowing

**WO**: `SequenzKonzeptStrategy.cs:178-181`:

```csharp
catch
{
    // Graceful degradation: Netz-/Parse-Fehler im News-Service dürfen keine Trades blockieren.
}
```

**WAS**: Generisches `catch` ohne Exception-Variable, ohne Logging. Bei einem dauerhaften News-Service-Problem (Endpoint down, Parse-Bug, Rate-Limit) merkt **niemand** etwas — der Bot tradet einfach ohne News-Filter weiter, der User denkt aber, der Filter sei aktiv.

**WARUM**: Phase 4 Decision-Trail würde einen `RejectionReasons.NewsBlackout`-Event generieren bei aktiv blockierten News, aber **kein Event** wenn die News-Quelle selbst defekt ist. Das verstößt gegen das Phase-15-Pattern (Active-Probe meldet, wenn ein externer Service degradiert).

**WIE**:
- `_logger.LogWarning(ex, "News-Blackout-Check fehlgeschlagen, Trade ohne Filter")`.
- Counter `_newsCheckFailureCount` in `RiskManager`. Bei > 5 Failures in Folge: `IBotEventStream.NewsServiceDegraded` Event firen → UI-Banner wie ConnectionDegraded.
- Optional: Settings-Flag `RequireNewsFilter` (default false) — wenn true, Trade rejecten statt durchwinken.

**AUFWAND**: S (3 Stunden).
**ABHÄNGIGKEIT**: Phase 4 + Phase 15 (Pattern-Wiederverwendung).

---

### 3.5 ServerHealthWatchdog: 80kB-Probe alle 30s = 230 MB/Tag

**WO**: `ServerHealthWatchdog.cs:94`:

```csharp
await _publicClient.GetAllTickersAsync(probeCts.Token).ConfigureAwait(false);
```

**WAS**: Der Kommentar in Zeile 87-88 sagt es selbst: *"BingX hat keinen dedizierten ServerTime-Endpoint im IPublicMarketDataClient — Tickers ist der naechstleichte (1 GET, ~80kB Response)."* Das sind 2880 Probes/Tag × 80 kB = **230 MB/Tag** alleine für Health-Check. Auf einem Pi mit Mobile-LTE-Backup oder Flat-Volume-Cap eine reale Größe.

**WARUM**: BingX HAT einen ServerTime-Endpoint (`/openApi/swap/v2/server/time`), nur ist er nicht im `IPublicMarketDataClient`. Der Bot nutzt den Aufruf-Pfad nicht.

**WIE**:
1. Neue Methode `IPublicMarketDataClient.GetServerTimeAsync()` (~50 Bytes Response).
2. Watchdog umstellen.
3. Bonus: Kombiniert mit 2.3 (Clock-Drift-Detection) → ein Probe für zwei Zwecke.

**AUFWAND**: S (2 Stunden).
**ABHÄNGIGKEIT**: Phase 15 (Erweiterung).

---

## 4. Quick-Wins (≤ 1 Tag, klarer Nutzen)

| # | Punkt | Aufwand | Hebel |
|---|-------|---------|-------|
| QW1 | **Bug 1.1 fixen** (RiskManager.GetPositionScalingFactor) | M | Konto-Schutz |
| QW2 | **3.1 Allocation-Pressure** (Queue + Cached-Reasons) | M | Pi-Latenz |
| QW3 | **3.5 Lightweight Probe** (ServerTime statt Tickers) | S | Bandbreite |
| QW4 | **3.4 News-Service-Health-Logging** | S | Observability |
| QW5 | **2.3 Clock-Drift-Detection** | S | Disaster-Vermeidung |
| QW6 | `MaxScore = 11` Magic-Number entfernen → dynamisch berechnen aus aktiven Kategorien | S | Wartbarkeit |
| QW7 | `_lastBcklEntryCandleIndex` und `BcklReEntryCooldownCandles` in `ScannerSettings` exposed | S | Backtest-Optimierung |
| QW8 | `RemoteSettingsAutoSync` Polling-Intervall auf SignalR-Push umbauen | S | Effizienz Mobile |

---

## 5. Nice-to-Haves

### 5.1 OpenTelemetry / Prometheus-Metriken-Endpoint
**Wert**: Trace eines Signals durch Strategy → Risk → Order → Fill ist heute nur über DB-Korrelation möglich. Mit OTel hätte man Tracing per `traceId` im Decision-Trail-Event. Aufwand L, niedriger Hebel weil Phase 4 schon viel abdeckt.

### 5.2 FCM-Token-Cleanup-Job
**WO**: `BingXBot.Server/Services/FcmDeviceStore.cs`. Stale Tokens (App deinstalliert) bleiben drin, jeder Push-Send stößt 410-Errors. Kein Cleanup-Job sichtbar. Aufwand S.

### 5.3 Property-Based-Tests (FsCheck)
**Wert**: SequenceStateMachine + ConfluenceScorer sind reine Funktionen — ideale FsCheck-Kandidaten. Würde Edge-Cases finden, die der existierende Test-Setup vermutlich übersieht (z.B. extreme Candle-Sequenzen). Aufwand L.

### 5.4 Snapshot-Tests für Backtest-Reports
**Wert**: Phase 6 + 13 generieren strukturierte Reports — perfekt für Verge / ApprovalTests. Schützt vor stillen Strategy-Verhaltensänderungen bei Refactorings. Aufwand M.

### 5.5 Encryption-at-Rest für PiCredentialStore
**WO**: `BingXBot.Server/Services/PiCredentialStore.cs`. API-Keys auf Pi-Filesystem. Mit physischem Zugriff (Pi geklaut, SD-Card ausgebaut) liegen Keys offen. Aufwand M (DPAPI-Pendant für Linux: libsecret oder Dateisystem-Verschlüsselung über LUKS).

### 5.6 Bearer-Token-Rotation
**WO**: `BingXBot.Server/Auth/AuthTokenStore.cs`. Tokens haben heute kein Ablaufdatum. Bei kompromittiertem Mobile-Device gibt es kein "Logout-überall". Aufwand M.

### 5.7 Equity-Curve via SignalR-Push statt Polling
**Wert**: Mobile pulled vermutlich periodisch — bei Hintergrund-App-State eine Batterie-Belastung. SignalR-Push-Pattern ist im Bot eh etabliert. Aufwand S.

### 5.8 Funding-Bonus präziser: Threshold sollte Symbol-spezifisch sein
**WO**: `Phase 7` nutzt 0.05% global. Memecoins haben oft 0.5-1% Funding (kurze Mean-Reversion-Setups), Majors selten > 0.05%. Static-Threshold löst zu oft auf Memes aus. Symbol-Klasse als Multiplier. Aufwand M.

---

## 6. Synthese & Empfehlung

**Vorschlag für eine "Phase 18: Risk-Hardening + Edge-Recovery"** (priorisiert):

1. **QW1** — Bug-Fix `GetPositionScalingFactor` (kritisch, dürfte nicht warten).
2. **2.2** Idempotency-Keys (Disaster-Vermeidung).
3. **2.3** Clock-Drift (stille Disaster-Mode-Vermeidung).
4. **2.4** Heartbeat + Trade-Replay-on-Resume (Recovery).
5. **1.2** Korrelations-Filter (echter Edge bei Markt-Crashes).
6. **3.1** Allocation-Pressure (Pi-Stabilität).
7. **1.5** Tick-Size-Awareness (kontinuierlicher Edge).

Diese sieben Punkte zusammen sind **~2 Wochen Aufwand**, schließen die größten verbleibenden Lücken und sind alle direkt code-belegt.

**Was ich bewusst ausgelassen habe**:
- ML-Adaptive-Confluence-Weights — zu groß, ohne Daten-Pipeline kein Edge.
- Multi-Exchange-Support — Scope-Explosion.
- Cross-Margin-Algo-Optimierung — der Margin-Cap (RiskManager Zeile 193) ist konservativ und korrekt.

---

## 7. Was schon hervorragend gelöst ist (positive Beobachtungen)

- **Phase 0-17 sind exzeptionell sauber dokumentiert** im CLAUDE.md.
- **`RiskManager.RollingSharpeRatio`** (Zeile 386-415) annualisiert basierend auf **tatsächlicher** Trade-Frequenz statt fixem √365 — das ist ein Detail, das viele systematische Trader falsch machen.
- **Phase 15 Edge-Transition-Logik** in `ServerHealthWatchdog` ist als pure function (`EvaluateProbe`) extrahiert und testbar — Top-Pattern.
- **Phase 4 Rejection-Codes** sind zentral als Konstanten gepflegt — leicht erweiterbar.
- **`SkConfluenceScorer.MaxScore`** Confidence-Divisor ist dynamisch — verhindert klassische Off-by-One-Fehler beim Hinzufügen neuer Kategorien.

---

**Verfasst**: 09.05.2026
**Code-Stand**: BingXBot v1.7.0 (Phasen 0-17 Code-vollständig, Pi-Deploy ausstehend).
