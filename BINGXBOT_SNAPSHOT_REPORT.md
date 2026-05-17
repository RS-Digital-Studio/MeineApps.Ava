# BingXBot Pi-Snapshot — Live-Analyse

**Snapshot:** `F:\Meine_Apps_Ava\bingxbot_snapshot.db` (Atomic-Backup via Python `sqlite3.backup`)
**Server-Stand:** Pi-Server seit 2026-05-15 21:36 CEST aktiv (~1d 17h Uptime beim Snapshot)
**Schema-Version:** 12
**Tools:** `tools/SkAnalytics/analyze_snapshot.py`, `tools/SkAnalytics/analyze_open_positions.py`

---

## TL;DR — drei Befunde, einer davon ein harter Bug

1. **Trade-Persistenz im Live-Pfad ist defekt — die DB sieht NULL Trades, obwohl 19 abgeschlossen sind.**
   - In **keiner** der 8 DB-Dateien auf dem Pi (Live-DB + 7 tägliche Backups) steht ein einziger Trade.
   - Im Code wird `BotDatabaseService.SaveTradeAsync` **nur** in zwei Pfaden aufgerufen:
     - `BacktestViewModel.cs:492` — Backtest-Replay-Trades
     - `BotAutoResumeService.cs:286` — Crash-Recovery-Backfill aus BingX-Income-History (nur bei Heartbeat-Drift > 5 min)
   - Der normale Live-Trade-Close-Flow läuft über `TradingServiceBase.ProcessCompletedTrade` (`TradingServiceBase.cs:1579`) — diese Methode aktualisiert RiskManager/Stats und feuert `_eventBus.PublishTrade`, **ruft aber `SaveTradeAsync` nicht auf**.
   - Die drei `TradeCompleted`-Subscriber (`TradeStatsAggregator`, `LocalBotEventStream`, `DashboardViewModel`) tun RAM-Stats / SignalR-Forward / UI-Markers — **keiner persistiert in die DB**.
   - **Konsequenz:** Die 19 Trades sieht Robert nur, weil sie per SignalR live in den App-Cache (`TradeHistoryViewModel._allTrades`) streamen. Beim nächsten App-Reload (oder Pi-Restart, oder bei einer anderen Client-Instanz) sind sie weg. `TradeStatsAggregator` lädt beim Bootstrap die letzten 10.000 Trades aus der DB — die ist leer, also sind die Stats falsch.
   - Auch `EquitySnapshots` (0 Zeilen) und `LogEntries` (0 Zeilen) sind betroffen — dieselbe Pipeline-Lücke. `DashboardViewModel.SaveEquitySnapshotAsync` läuft nur im Standalone-/Desktop-Mode, nicht auf dem Pi-Server (`UseRemoteMode=true` ist gesetzt).
   - **Das ist der harte Killer-Bug.** Bis er gefixt ist, sind ALLE folgenden Datenpunkte unzuverlässig.

2. **Hardfilter-Hypothese aus dem Strategie-Report ist verifiziert.** 7 von 7 Buch-Hardfiltern stehen auf "AUS"; die `SettingsChanges`-History zeigt, dass Robert sie selbst von **scharf** auf **AUS** umgestellt hat (Tick 639144709266130738, ~2026-05-14). Trotz lockerer Filter triggert der Bot aktuell nur **0.2 %** aller Decisions (110 von 48.326). Heißt: Filter zu lockern hat nichts gebracht, weil etwas anderes bremst — sehr wahrscheinlich der gleiche Persistenz-Pfad-Schaden plus die "state_not_activated"-Decision-Trail-Granularität.

3. **18 offene Positionen in `ExitStates`, davon 11 mit Alter 22-30 Tage.** 4 von 18 haben `ConfluenceScore = 0`, weitere 5 haben Score 1 — also **9 von 18 (50 %)** waren Low-Quality-Setups, die mit `MinConfluenceScore ≥ 2` nie aufgemacht worden wären. Drei der April-Positionen sind `IsRecovered=true` mit `OriginalQuantity=0` — das sind **23 Tage alte Pending-Limit-Orders im Limbo**, weil der `PendingLimitOrderMaxAgeHours=6`-Cleanup auf recovered Limits offenbar nicht greift. DOGE-USDT hat einen SL **über** dem Entry-Preis bei Long — Verdacht auf SL-Update-Race.

---

## 1. Datenbasis im Snapshot

| Tabelle | Zeilen | Bemerkung |
|---|---|---|
| `EvaluationDecisions` | **48.326** | Funktioniert (Decision-Trail-Pipeline schreibt). |
| `Trades` | **0** | Persistenz-Pipeline schreibt im Live-Flow nicht. |
| `EquitySnapshots` | **0** | Gleiche Lücke — nur Standalone-DashboardVM persistiert. |
| `LogEntries` | **0** | Logger schreibt nicht in DB (separate Lücke). |
| `BacktestJobs` | **0** | Keine Backtest-Jobs gelaufen. |
| `Settings` | 7 Keys | inkl. `BotSettings`-JSON, `ExitStates`, `PendingLimitOrders`, `RuntimeState` |
| `SettingsChanges` | 30 | Funktioniert. |

Verifiziert: **alle 7 Backup-DBs vom 2026-05-11 bis 2026-05-17 haben ebenfalls 0 Trades**. Es gibt keine `bot-archive-*.db`-Datei (DbArchiveService hatte noch keinen 1.-des-Monats-Trigger). Auch die lokale Windows-DB (`C:\Users\rober\AppData\Roaming\BingXBot\bot.db`) ist seit dem 24.04. nicht mehr beschrieben — `UseRemoteMode=true` ist seitdem aktiv.

`BotSettings.LastMode = Live`, `LastStrategy = "SK-System"`, `EnableDecisionTrail = true`, `WasRunningOnShutdown = true`, `EnableAutoRestartOnStale = true`.

---

## 2. Der Persistenz-Bug im Detail

### 2.1 Wo Trades entstehen (Live-Flow)

In `LiveTradingService.OrderPlacement.cs`:
- Zeile **600** (TP1-Fill via WebSocket): `var trade = new CompletedTrade(...); ProcessCompletedTrade(trade); _eventBus.PublishTrade(trade);`
- Zeile **658** (TP2-Fill / Full-Close): identisches Muster.
- Weitere Stellen für SL-Hit, manueller Close, Position-Sync — alle laufen über `ProcessCompletedTrade` + `_eventBus.PublishTrade`.

### 2.2 Was `ProcessCompletedTrade` tut (`TradingServiceBase.cs:1579-1617`)

```csharp
protected void ProcessCompletedTrade(CompletedTrade trade)
{
    _riskManager?.UpdateDailyStats(trade);     // RAM
    // Loss-Streak-Tracking ...                 // RAM
    _riskManager?.SetConsecutiveLosses(...);    // RAM
    if (_botSettings.EnableDesktopNotifications)
        _eventBus.PublishNotification(...);     // Event
    // ← KEIN _dbService.SaveTradeAsync(trade)
}
```

### 2.3 Wer `BotEventBus.TradeCompleted` abonniert

| Subscriber | Tut was |
|---|---|
| `TradeStatsAggregator` (`Stats/TradeStatsAggregator.cs:32`) | Aktualisiert RAM-Aggregate (TF × Category × Mode). |
| `LocalBotEventStream` (`Local/LocalBotEventStream.cs:69`) | Forwarded → SignalR-Hub → Clients. |
| `DashboardViewModel` (`ViewModels/DashboardViewModel.cs:375`) | Trade-Markers + Metriken-Refresh (Client-seitig). |

**Keiner ruft `_dbService.SaveTradeAsync`.** Damit ist die Pi-DB blind für Live-Trades. Die App zeigt 19 Trades nur, weil `LocalBotEventStream` sie per SignalR gepusht hat und `TradeHistoryViewModel.OnTradeClosed` sie ins RAM-`_allTrades` einfügt.

### 2.4 Was bei Bot-Restart passiert

`TradeStatsAggregator` rebuilded beim Bootstrap aus den letzten 10.000 Trades der DB. Mit `Trades=0` startet er bei Null. `TradeHistoryView` lädt `ITradeHistoryService.QueryAsync` → leer. Rolling-Metriken (`RollingWinRate`, `RollingProfitFactor`, `RollingSharpeRatio`, `LossStreakDampening`) sind alle ohne Datenbasis.

`BotAutoResumeService` rettet das **nur** bei Heartbeat-Drift > 5 min und Live-Mode: er ruft `GetIncomeHistoryAsync` für das Offline-Zeitfenster und persistiert REALIZED_PNL-Records via `SaveTradeAsync`. Ohne Heartbeat-Drift (also bei sauberem Bot-Stop) passiert kein Backfill — die zwischen Snapshot und Stop entstandenen Trades sind weg.

### 2.5 Der Mindest-Fix

```csharp
// TradingServiceBase.cs in ProcessCompletedTrade, gleich nach _riskManager?.UpdateDailyStats(trade):
_ = Task.Run(async () =>
{
    try { await _dbService.SaveTradeAsync(trade).ConfigureAwait(false); }
    catch (Exception ex)
    {
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "DB",
            $"SaveTradeAsync fehlgeschlagen: {ex.Message}", trade.Symbol));
    }
});
```

Im Live-Mode darf das nicht blockierend laufen (Fill-Pfad hat strenge Timing-Constraints), aber das Fire-and-Forget muss in einen try/catch mit Log, sonst werden DB-Fehler still verschluckt.

Analog gilt für `_eventBus.LogEmitted` → DB-Logger fehlt, und für `EquityUpdate` → Equity-HostedService auf dem Pi (CLAUDE.md erwähnt das als Lücke: "Im Remote-Mode laeuft der DashboardViewModel-Timer auf dem Pi ohnehin nicht. Remote-Equity-Kurve erfordert einen HostedService-basierten Tracker.").

---

## 3. Settings-Status — wer hat was scharfgestellt, wer entschärft?

### 3.1 Live-Werte (BotSettings-JSON am 2026-05-17)

| Setting | Live | Code-Default | Buch-Hard | Status |
|---|---|---|---|---|
| `Scanner.ImpulseAtrMultiplier` | **2.0** | 2.0 | **3.0** | weicht vom Buch ab |
| `Scanner.RequireBosCloseBreak` | **false** | false | **true** | weicht vom Buch ab |
| `Scanner.RequireBosVolumeBreakout` | **false** | false | **true** | weicht vom Buch ab |
| `Scanner.BlockLtfEntryWhenHtfInTargetZone` | **false** | false | **true** | weicht vom Buch ab |
| `Risk.RequireWickRejectionInBZone` | **false** | false | **true** | weicht vom Buch ab |
| `Risk.RequireBoxCloseOnEntry` | **false** | false | **true** | weicht vom Buch ab |
| `Risk.MinConfluenceScore` | **0** | 0 | **5** | weicht vom Buch ab |
| `Risk.MinRiskRewardRatio` | **0** | 0 | **1.0** | (CategorySettings.Crypto = 1.0 ist gesetzt) |
| `Risk.MaxRiskPercentPerTrade` | 5 | 5 | (User-Abweichung) | bewusst — bleibt |
| `Risk.MaxTotalMarginPercent` | **80** | 10 | (User-Abweichung) | massiv lockerer, bewusst |
| `Risk.HighProbabilityPositionMultiplier` | **1.0** | 2.0 | — | bewusste Reduktion |
| `Scanner.EnableBiasFlip` | **true** | true | (Audit empfiehlt false) | redundant zu FailedPoint0/PromotedToLarger |

### 3.2 Was Robert selbst geändert hat (Tick 639144709266130738, ~2026-05-14)

| Field | OldValue | NewValue |
|---|---|---|
| `Scanner.BlockLtfEntryWhenHtfInTargetZone` | true | **false** |
| `Scanner.ImpulseAtrMultiplier` | **3.0** | 2.0 |
| `Scanner.RequireBosCloseBreak` | true | **false** |
| `Risk.RequireWickRejectionInBZone` | true | **false** |
| `Risk.MaxRiskPercentPerTrade` | 3 | 5 |

Vermutlicher Grund: mit Buch-Hard kam noch weniger durch. Heißt der eigentliche Drosselfaktor liegt nicht primär an diesen Filtern.

### 3.3 Deaktivierte Daily-Caps

- `Risk.MaxDailyLossPercent = 0`
- `Risk.MaxDailyRiskPercent = 0`
- `Risk.MaxDailyDrawdownPercent = 0`
- `Risk.EnableEquityCurveScaling = false`
- `Risk.EnableVolatilityTargeting = false`

Bei defekter Persistenz und 0 als Cap kann ein schlechter Tag ungebremst durchschlagen, ohne dass der Bot sich selbst pausiert. Sobald Persistenz gefixt ist, sollten mindestens `MaxDailyLossPercent` und `MaxDailyRiskPercent` gesetzt werden.

---

## 4. Decision-Trail — was der Bot in 41 Stunden ablehnt

### 4.1 Volumen

48.326 Decisions, davon **110 (0.2 %) getriggert**, **48.216 (99.8 %) geblockt**:

| # | RejectionReason | Anzahl | % der Blocks |
|---|---|---|---|
| 1 | `state_not_activated` | 39.340 | 81.6 % |
| 2 | `other` | 8.876 | 18.4 % |

Im Code (`BingXBot.Core/Diagnostics/RejectionReasons.cs`) sind 17 Reasons definiert (`news_blackout`, `impulse_below_atr`, `no_htf_confluence`, `score_below_min`, `rrr_too_small`, `box_close_violated`, `missing_wick_rejection`, `mta_target_zone_block`, `entries_already_triggered`, `missing_strukturpunkte`, `counter_trend_inactive`, `slippage_too_high`, `tf_auto_disabled`, `correlation_limit_exceeded`, `outside_allowed_session`, `news_service_unavailable`, `other`). **Live geloggt werden aber nur zwei.** Das heißt entweder:
- Der Bot kommt nie über den `state_not_activated`-Check hinaus für 81 %, und für die restlichen 18 % schlägt vor dem granularen Check ein Catch-all-`other`-Pfad zu.
- Oder die granularen Reject-Codes werden im neuen Multi-TF-Standalone-Pfad nicht durchgereicht und alles fällt in `other`.

Verifizieren: Suche im Code, wo `RejectionReasons.Other` zugewiesen wird — das müsste eine kurze Liste an Call-Sites sein.

### 4.2 `state_not_activated` ist eigentlich kein Reject

Sample der 39.340 `state_not_activated`-Decisions:
- `SequenceState=Unknown`, `Point0/A/B=NULL`, `ConfluenceScore=0`, `CategoriesJson=[]`

Das ist eine Decision für ein Symbol, bei dem die State-Machine noch keine Sequenz aufgebaut hat. Das gehört **nicht in den Reject-Trail** — der Bot hatte gar keine Bewertungsgrundlage. 81 % des Decision-Trails ist Rauschen.

### 4.3 Confluence-Score-Verteilung der Aktivierten

| Score | Aktiviert | Triggered | Trigger-Quote |
|---|---|---|---|
| 1 | 2.013 | 42 | 2.1 % |
| 2 | 1.635 | 27 | 1.7 % |
| 3 | 931 | 12 | 1.3 % |
| 4 | 1.143 | 16 | 1.4 % |
| 5 | 175 | 12 | **6.9 %** |
| 6 | 6 | 1 | 16.7 % |

Score 5+6 triggern 4-8× häufiger — der Scorer ist intern konsistent. Aber Score 5+6 sind nur 181 von 48k Decisions; bei `MinConfluenceScore ≥ 5` käme **ein Trade alle 3 Stunden**. Das ist die scharfe Buch-Realität.

### 4.4 ZEC-Trigger-Cluster

ZEC-USDT M15 wurde **60×** für dieselbe Sequenz `ZEC-USDT_M15_486,91125_503,0901_Add` als getriggert gelogged. In `PendingLimitOrders` steht genau **eine** Order dafür. Das ist Log-Spam: Sequenz bleibt im `Aktiviert`-State, jeder Scan-Tick mit Trigger-Bedingung schreibt einen neuen Decision-Eintrag, obwohl die Order längst platziert ist. Idempotenz-Check zwischen "ich würde traden" und Decision-Log fehlt.

### 4.5 Confluence-Kategorien (Top-12)

| Kategorie | Vorkommen |
|---|---|
| `Fahrplan` | 2.656 |
| `H4-Aktiviert` | 2.053 |
| `M15-Aktiviert` | 1.511 |
| `Volume` | 1.303 |
| `H1-Aktiviert` | 1.213 |
| `D1-Aktiviert` | 1.126 |
| `GKL-D1` | 894 |
| `HighProb-Overlap: HTF-W1-GKL ∩ LTF-BC` | 527 |
| `GKL-W1` | 373 |
| `HighProb-Overlap: HTF-W1-GKL ∩ LTF-EXT161.8/200 Counter` | 42 |
| `GoldenPocket` | 12 |
| `BCKL-Bonus` | 6 |

`GoldenPocket` und `BCKL-Bonus` sind die Buch-Premium-Setups — extrem selten. Mit `MinConfluenceScore ≥ 5` plus diese Kategorie-Anforderungen geht Trade-Frequenz auf 1-3/Tag runter.

---

## 5. Die 18 offenen Positionen (`ExitStates`)

| # | Symbol | Side | Entry-Datum | Alter [d] | Qty | NavTF | Score | Recovered? | Entry | SL | TP1 | TP2 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | NCCOGOLD2USD-USDT | Long | 2026-04-16 | **30.7** | 0.00826593 | M5 | 5 | N | 4793.11 | 4785.07 | 4811.58 | 4819.44 |
| 2 | ETH-USDT | Long | 2026-04-20 | **27.5** | 0.0182677 | M15 | 4 | N | 2283.27 | 2254.13 | 2298.92 | 2313.55 |
| 3 | NCCO1OILWTI2USD-USDT | Short | 2026-04-24 | **23.0** | 0.237793 | H1 | 2 | N | 94.46 | 96.925 | 75.41 | 69.61 |
| 4 | NCCO1OILBRENT2USD-USDT | Long | 2026-04-24 | **23.0** | 0.258407 | H1 | 1 | N | 100.10 | 88.43 | 103.79 | 107.32 |
| 5 | NCFXEUR2CAD-USDT | Long | 2026-04-24 | **23.0** | 29.0984 | D1 | 1 | N | 1.6036 | 1.5741 | 1.6294 | 1.6444 |
| 6 | NCSINIKKEI2252USD-USDT | Long | 2026-04-24 | **23.0** | 0.000444 | D1 | 1 | N | 59715.9 | 51592.9 | 60374.1 | 62583.3 |
| 7 | ETC-USDT | Long | 2026-04-24 | **23.0** | 1.33595 | H4 | 3 | N | 8.521 | 8.5046 | 9.0998 | 9.3170 |
| 8 | SOL-USDT | Long | 2026-04-24 | **23.0** | 0.133492 | H1 | **0** | N | 85.937 | 85.103 | 95.277 | 98.764 |
| 9 | NCSKMSFT2USD-USDT | Short | 2026-04-24 | **23.0** | **0** | H4 | **0** | **Y** | 428.22 | 429.08 | 409.07 | 404.36 |
| 10 | NCSKAMZN2USD-USDT | Long | 2026-04-24 | **23.0** | **0** | H4 | **0** | **Y** | 249.01 | 248.52 | 266.92 | 271.35 |
| 11 | NCSKCOIN2USD-USDT | Long | 2026-04-24 | **22.9** | **0** | H4 | **0** | **Y** | 169.25 | 168.91 | 225.42 | 238.29 |
| 12 | DOGE-USDT | Long | 2026-05-17 | 0.5 | 314 | M15 | 2 | N | 0.10882 | **0.10936 (!)** | 0.11341 | 0.11468 |
| 13 | UNI-USDT | Long | 2026-05-17 | 0.2 | 12.9497 | M15 | 5 | N | 3.535 | 3.5045 | 3.5686 | 3.5891 |
| 14 | JUP-USDT | Long | 2026-05-17 | 0.1 | 172.467 | M15 | 4 | N | 0.2007 | 0.1970 | 0.2059 | 0.2081 |
| 15 | TRX-USDT | Long | 2026-05-17 | 0.1 | 97.2046 | M15 | 1 | N | 0.35594 | 0.34949 | 0.35633 | 0.35835 |
| 16 | ZEC-USDT | Long | 2026-05-17 | 0.1 | 0.0692 | M15 | 1 | N | 515.25 | 490.37 | 522.01 | 529.22 |
| 17 | ICP-USDT | Long | 2026-05-17 | 0.0 | 13.3 | M15 | 3 | N | 2.632 | 2.5480 | 2.7793 | 2.8293 |
| 18 | LTC-USDT | Long | 2026-05-17 | 0.0 | 0.6 | M15 | 3 | N | 56.04 | 55.844 | 57.275 | 57.663 |

### 5.1 Sofort-Auffälligkeiten

**a) DOGE-USDT — SL OBERHALB des Entry bei Long.** Entry 0.10882, SL **0.10936** (+0.5 %), `BreakevenSet=false`, `PartialClosed=false`. Verdacht: Bug in der SL-Update-Pipeline (Smart-Breakeven oder Pyramid-Layer-Trail wurde fälschlich ausgeführt, ohne dass TP1 erreicht ist). **Hot-Fix-Kandidat — bei Long-Position kostet ein SL über Entry sofort Geld, sobald der SL anspringt.**

**b) Drei Recovery-Limit-Orders seit 23 Tagen offen** (NCSKMSFT, NCSKAMZN, NCSKCOIN): `IsRecovered=true`, `OriginalQuantity=0`. Das sind alte Pending-Limits aus April, die nach Server-Neustart aus der BingX-API zurückgemeldet wurden, aber nie gefilled. `Risk.PendingLimitOrderMaxAgeHours=6` greift hier nicht — Cleanup-Logik prüft offenbar nur frische, nicht recoverte Pending-Limits.

**c) Vier Score-0-Positionen wurden getradet** (NCSKMSFT, NCSKAMZN, NCSKCOIN, SOL) — exakt das, was `MinConfluenceScore=0` zulässt. Bei `MinConfluenceScore=1` raus. Bei `MinConfluenceScore=2` würden weitere 5 entfallen (TRX, ZEC, NCCO1OILBRENT, NCFXEUR2CAD, NCSINIKKEI225, NCCO1OILWTI).

**d) NCSINIKKEI2252USD-USDT** bei Entry 59.716 mit SL 51.593 (**-13.6 %**) — 23 Tage offen. **NCCO1OILBRENT** bei Entry 100.10 mit SL 88.43 (-11.7 %). Beide D1-/H1-Trades auf TradFi-Perps mit extrem weiten SLs — Risiko in absoluten USDT-Beträgen ist hier nicht aus der DB ablesbar (kein aktueller Mark-Price), aber das sind die Kandidaten für die größten Buchverluste.

**e) Mehrere TP1-Targets unrealistisch eng:**
- TRX: 0.04 % über Entry — nach Fees (Maker 0.02 %, Taker 0.05 %) **negativer Net-Gewinn**.
- NCSKMSFT (Short): 0.20 % vom Entry → 0.13 % nach Maker-Fee, 0.10 % nach Taker.
- UNI: 1.0 % — knapp.
- NCCOGOLD: 0.39 %.

`Risk.MinRiskRewardRatio=0` lässt das durch. `CategorySettings.Crypto.MinRiskRewardRatio=1.0` greift offenbar nicht für TradFi-Symbole und auch nicht konsistent für Crypto.

**f) Long-Bias 16:2.** Kein klares Signal — kann an Markt-Phase liegen, kann an Asymmetrie in der Korrektur-Long- vs Korrektur-Short-Erkennung der SK-State-Machine liegen.

### 5.2 Kennzahlen

| | Wert |
|---|---|
| Total offene Positionen | 18 |
| Davon real gefilled (Qty>0) | 15 |
| Davon Recovery (Qty=0) | 3 |
| Davon ≥ 7 Tage alt | 11 |
| Davon ≥ 22 Tage alt | 11 |
| Score 0 | 4 |
| Score ≤ 1 | 9 |
| Score ≥ 5 | 2 |
| Long / Short | 16 / 2 |
| TF-Verteilung | M5: 1, M15: 8, H1: 3, H4: 4, D1: 2 |

---

## 6. Abgleich mit Roberts Aussage: 19 abgeschlossen, 2 laufend, 13 open

Die App zeigt:
- **19 abgeschlossene Trades:** kommen per SignalR-`TradeClosed`-Stream live ins `_allTrades`-RAM des `TradeHistoryViewModel`. **Nicht in der DB.** Bei nächstem App-Reload + leerer Pi-DB sind sie weg.
- **2 laufende:** das sind die echten offenen BingX-Positionen mit Qty > 0, die der Bot aktiv überwacht.
- **13 open orders:** das sind 13 offene Limit-Orders auf BingX (eingeschlossen: die 18 ExitStates mit gemixtem Pending/Position-Status — Mismatch zwischen Bot-Sicht und BingX-Sicht).

**Diskrepanz zwischen meiner DB-Sicht und Roberts App-Sicht:**

| Bot weiß (Pi-DB / ExitStates) | App zeigt (RAM) | BingX hat |
|---|---|---|
| Trades: 0 | 19 abgeschlossen | (BingX-API-Abfrage offen) |
| ExitStates: 18 | 2 laufend + 13 open | (BingX-API-Abfrage offen) |
| PendingLimitOrders: 5 | (in 13 open enthalten) | (BingX-API-Abfrage offen) |

Die 18 ExitStates dürften eine Mischung aus echten offenen Positionen, echten offenen Limits und **Stale-Einträgen** sein (April-Positionen, die auf BingX längst geschlossen wurden, aber der Bot weiß es nicht).

**Nächste Diagnose-Aktion:** BingX-API direkt befragen — `/openApi/swap/v2/user/positions` (Open-Positionen) und `/openApi/swap/v2/trade/openOrders` (Open-Orders). Vergleich gegen ExitStates → klärt, ob die 11 alten ExitStates auf BingX überhaupt noch existieren. Wenn nicht, ist die `ExitStates`-Cleanup-Logik ebenfalls defekt (Synchronisations-Bug).

---

## 7. Korrigierte Roadmap

### Phase A0 — Persistenz-Sanierung (Tage, vor allem anderen)

| Schritt | Was | Wo |
|---|---|---|
| A0.1 | `_dbService.SaveTradeAsync(trade)` in `TradingServiceBase.ProcessCompletedTrade` einbauen (Fire-and-Forget mit Log-Catch). | `src/Libraries/BingXBot.Trading/TradingServiceBase.cs:1579` |
| A0.2 | Equity-Snapshot-HostedService auf dem Pi (`DashboardViewModel.SaveEquitySnapshotAsync` läuft nur Standalone; im RemoteMode fehlt das). | Neuer `BingXBot.Server/HostedServices/EquitySnapshotService.cs` (1× pro Trade + alle 5 min) |
| A0.3 | `BotEventBus.LogEmitted` → optionaler DB-Logger (`BotDatabaseService.SaveLogAsync` existiert vermutlich noch nicht — Schema-Check), gated per Setting (sonst Spam). | `BotDatabaseService` + Hook in `LocalBotEventStream` |
| A0.4 | One-Shot-Backfill aus BingX-Income-History für die 19 schon abgeschlossenen Trades (manuell via `BotAutoResumeService.BackfillTradesFromIncomeAsync`-Pfad oder Admin-Endpoint). | Neuer REST-Endpoint `/api/v1/admin/backfill?from=2026-05-15` |
| A0.5 | ExitStates-Stale-Cleanup: ExitStates ohne Match auf BingX-Open-Positions automatisch entfernen (1× pro Reconcile-Loop). | `LiveTradingService.ReconcileLoopAsync` Erweiterung |
| A0.6 | DOGE-SL-Bug analysieren: warum ist SL **über** Entry bei Long ohne BreakevenSet+PartialClosed? Verdächtige: `SmartBreakevenService`, Pyramid-Layer-Trail, Recovery-Patch-Pfad. | `LiveTradingService.OrderPlacement.cs` + `BreakevenCalculator.cs` |
| A0.7 | Recovery-Pending-Cleanup: NCSKMSFT/NCSKAMZN/NCSKCOIN seit 23 Tagen — `PendingLimitOrderMaxAgeHours` muss auch auf `IsRecovered=true` greifen. | `LiveTradingService.ReconcileLoopAsync` / Pending-Cleanup-Pfad |

### Phase A1 — Decision-Trail-Granularität (Tage)

| Schritt | Was |
|---|---|
| A1.1 | `state_not_activated` aus dem Decision-Trail-Persist entfernen (oder hinter Toggle stellen — 81 % Rauschen). |
| A1.2 | Catch-all-`other` aufspalten — wo wird `RejectionReasons.Other` zugewiesen? Jede dieser Call-Sites bekommt einen spezifischen Code aus dem bestehenden 17er-Set. |
| A1.3 | Idempotenz-Check vor Decision-Log: wenn `Triggered=1` und Setup ist bereits als `PendingLimitOrders`-Eintrag oder `ExitStates`-Eintrag bekannt → kein neuer Log-Eintrag (verhindert ZEC-60×-Spam). |

### Phase A2 — Hardfilter behutsam scharfstellen (1 Wochenende)

Erst nach A0+A1 sinnvoll, weil sonst nicht messbar.

| Schritt | Was | Wert |
|---|---|---|
| A2.1 | `Risk.MinConfluenceScore` 0 → **2** | blockt 4 Score-0-Setups direkt; stufenweise nach 1 Woche auf 3, dann 4, dann 5 |
| A2.2 | `Risk.MinRiskRewardRatio` 0 → **1.2** | blockt TRX/NCSKMSFT-artige Mikro-TP-Trades; CategorySettings spiegeln |
| A2.3 | `Scanner.ImpulseAtrMultiplier` 2.0 → **2.5** | stufenweise, nicht direkt 3.0 |
| A2.4 | A4-A7 aus dem Strategie-Report (`RequireWickRejectionInBZone`, `RequireBoxCloseOnEntry`, `RequireBosCloseBreak`, `RequireBosVolumeBreakout`, `BlockLtfEntryWhenHtfInTargetZone`) | nach Phase A2.1-A2.3, sobald A0-Pipeline 2 Wochen Daten geliefert hat |

### Phase B (Datenbasiertes Tuning), C (ML-Filter), D (Regime), E (Portfolio)

Wie im Strategie-Report — aber **erst nach A0+A1**. Vorher gibt es schlicht keine verwertbaren Trade-Outcomes.

---

## 8. Was die Daten **nicht** zeigen

- **Realtime-PnL der 18 offenen Positionen** — kein Mark-Price im Snapshot. BingX-API-Abfrage für `/openApi/swap/v2/user/positions` notwendig.
- **Echte abgeschlossene Trades** — durch den Persistenz-Bug nicht in der DB. App-RAM ist die einzige Quelle; verloren bei Reload.
- **Win-Rate / Sharpe / Max-DD** — ohne abgeschlossene Trades in der DB nicht aggregierbar.
- **Stale-Status der April-ExitStates** — ohne BingX-API-Cross-Check nicht klar, ob die Positionen dort noch leben.

---

## 9. Sofort-To-Do für Robert (priorisierte 4-Stunden-Liste)

1. **(30 min)** BingX-Website öffnen, manuell die echten Open-Positions und Open-Orders kopieren, gegen die 18-ExitStates-Liste aus Abschnitt 5 abgleichen. Daraus ergibt sich die echte Stale-Liste.
2. **(15 min)** DOGE-USDT-Position auf BingX direkt prüfen: ist die SL-Order dort wirklich bei 0.10936 (über Entry) oder hat nur der Bot-In-Memory-Zustand das? Wenn ja → manuell SL korrigieren.
3. **(60 min)** A0.1 implementieren — `SaveTradeAsync` in `ProcessCompletedTrade` einbauen. Ein-Liner, fire-and-forget, mit Log-Catch.
4. **(30 min)** A0.4: einen Admin-One-Shot-Endpoint `POST /api/v1/admin/backfill?from=2026-05-15` einbauen, der `BackfillTradesFromIncomeAsync` aus `BotAutoResumeService` extrahiert und für das angegebene Zeitfenster aufruft. Damit kommen die 19 verlorenen Trades zurück in die DB.
5. **(60 min)** Server bauen + deployen + Service restart. Backfill auslösen. App neu laden → 19 Trades müssen jetzt aus der DB kommen (nicht mehr aus RAM).
6. **(30 min)** Erste Verifikation: nach dem nächsten echten Trade-Close prüfen, ob er in der DB landet (`sqlite3` auf Pi gegen die Live-DB, `SELECT * FROM Trades ORDER BY ExitTime DESC LIMIT 5`).

Erst danach Strategie-Diskussion.

---

## 10. Reproduktion

```powershell
# Snapshot vom Pi ziehen (sqlite3-CLI fehlt — Python-Pfad):
ssh steuerung@raspberrypi.local "python3 -c `"import sqlite3; src=sqlite3.connect('/var/lib/bingxbot/bot.db'); dst=sqlite3.connect('/tmp/bot-snapshot.db'); src.backup(dst); dst.close(); src.close()`""
scp steuerung@raspberrypi.local:/tmp/bot-snapshot.db F:/Meine_Apps_Ava/bingxbot_snapshot.db
ssh steuerung@raspberrypi.local "rm /tmp/bot-snapshot.db"

# Analyse:
py F:\Meine_Apps_Ava\tools\SkAnalytics\analyze_snapshot.py
py F:\Meine_Apps_Ava\tools\SkAnalytics\analyze_open_positions.py
```

Snapshot-Datei: `F:\Meine_Apps_Ava\bingxbot_snapshot.db` (11.6 MB, gitignored).
Hilfs-Auszüge: `bot_settings.json`, `bot_settings_pretty.json`, `pending_orders.json`, `exit_states.json` (alle gitignored).
