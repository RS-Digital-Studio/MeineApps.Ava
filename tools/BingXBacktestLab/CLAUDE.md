# BingXBacktestLab — Empirischer Strategie-Vergleich

Konsolen-Tool, das BingXBot-Strategien auf **echten BingX-Klines** backtestet und vergleicht.
Nicht in der Solution (`MeineApps.Ava.sln`) — standalone via `dotnet run --project`. Dient der
datengetriebenen Entscheidung zwischen Strategien und Parametern.

## Verwendung

```bash
dotnet run --project tools/BingXBacktestLab -c Release -- \
  --strategies "TrendFollow,TrendFollow-Strong,SK-System" \
  --preset may-live \            # oder --symbols "BTC-USDT,ETH-USDT,..."
  --tfs H4,H1 \
  --from 2025-11-01 --to 2026-05-31 \
  --label mein-lauf
```

| Arg | Default | Zweck |
|-----|---------|-------|
| `--strategies` | SK-System | Komma-Liste (Namen aus `StrategyFactory`) |
| `--symbols` / `--preset` | preset may-live | Explizite Liste **oder** Preset (`may-live`, `crypto-major`) |
| `--tfs` | H4,H1 | Navigator-Timeframes |
| `--from` / `--to` | 2025-11-01 / 2026-05-31 | Zeitraum (UTC) |
| `--settings` | live-settings.json | BotSettings-JSON für faire Live-Config-Validierung |
| `--label` | run | Report-Dateiname-Suffix |

Output: Console-Tabelle + `reports/report-{label}.md` + `.json`. Aggregat pro Strategie
(WinRate, PF, Expectancy/Trade, Σ PnL, RRR, MaxDD, **Long/Short-Aufschlüsselung**) + Detail pro TF.

## Portfolio-Modus (`--portfolio`) — Spiegelbild des Live-Bots

Der Default-Matrix-Pfad fährt **pro Symbol eine eigene `BacktestEngine` mit eigenem 1000-USDT-Konto**
und summiert nur die PnLs. Dadurch feuern die **konto-weiten** Risk-Gates NIE (MaxOpenPositions,
MaxTotalMargin, Korrelations-Cluster, Daily-Loss/Drawdown), und das risk-basierte Sizing tradet jedes
Symbol mit „frischen" 1000 USDT. `--portfolio` fährt stattdessen **EIN gemeinsames Konto über alle
Symbole, zeitlich gemergt** (`PortfolioBacktestEngine`) → die Gates greifen und das Sizing teilt sich
die eine (sinkende/steigende) Equity. So wird der Backtest zum Spiegelbild des Live-Bots.

```bash
dotnet run --project tools/BingXBacktestLab -c Release -- \
  --portfolio --preset may-live --tfs H4 \
  --from 2022-06-01 --to 2026-06-01 --balance 158 --label portfolio-smoke
```

| Arg | Default | Zweck |
|-----|---------|-------|
| `--portfolio` | — | aktiviert den Portfolio-Pfad (beendet danach) |
| `--balance` | 158 | Start-Balance des EINEN Kontos → `Backtest.InitialBalance` |
| `--tfs` | (erstes Element) | nur die erste TF wird als Nav-TF genutzt (H4-only, TrendFollow-Fast) |
| `--strategies` | (erstes Element) | nur die erste Strategie (Live: TrendFollow-Fast) |
| `--scanner-filter` | true | GAP 11: Live-Scanner-Vorfilter (`Backtest.EnableScannerPrefilter`) → `false` zum Abschalten (Diagnose) |
| `--btc-health` | true | GAP 4: BTC-Health-Positionsskalierung + SK-Score-Scale (`Backtest.EnableBtcHealthScale`) → `false` zum Abschalten |

**Live-Spiegel-Vorfilter (GAP 11 + GAP 4)** — im `--portfolio`-Modus **standardmäßig AN** ("alles wie in live"),
per `--scanner-filter false` / `--btc-health false` abschaltbar. Die Console-Ausgabe zeigt den aktiven Status.
Beide Pfade wirken **nur** im `PortfolioBacktestEngine` (Default `false` in `BacktestSettings` → Single-Engine +
bestehende Portfolio-Läufe ohne Flags bleiben bit-identisch).

- **Scanner-Vorfilter** (`PassesScannerFilter`): pro Symbol/H4-Kerze einen synthetischen 24h-Ticker bauen
  (`Volume24h` = Σ Kerzen-Volumen×Close der letzten 6 H4-Kerzen = Quote-Volumen USDT; `PriceChangePercent24h` =
  Δ über 6 Kerzen) und gegen `MinVolume24hByTf`/`MinPriceChangeByTf` (kategorie-spezifisch Crypto/TradFi, wie
  `ScanHelper.FilterCandidatesForTimeframe` — nachgebaut, da `BingXBot.Trading` vom Backtest nicht referenzierbar)
  prüfen, plus `TradingHoursFilter.IsMarketOpen` (TradFi-Marktstunden) + `IsSessionAllowed` (Crypto-Session-Bitmask).
  Symbol/Kerze, die den Filter nicht passiert, erzeugt keinen Entry.
- **BTC-Health** (`MarketFilter.CalculateBtcHealth`): BTC-USDT D1+H4 separat vorladen (ab `from.AddDays(-120)` für
  D1-Warmup ≥55), pro Zeitschritt inkrementeller Slice → harter Block bei Crypto + `!AllowLong/AllowShort`, sonst
  `PositionScale`-Multiplikation. SK-Score-Scale (`ConfluenceScore` ≥10→1.25/≥5→1.0/_→0.75) ist an dies Flag
  gekoppelt. Skalierung wirkt im `BacktestEntryProcessor` auf `AdjustedPositionSize` VOR PlaceOrder; die platzierte
  (skalierte) Menge wird auch als `OriginalQuantity` gespeichert (TP1/TP2-50/50-Proportionen live-treu). Funding=0
  (kein historischer Per-Kerze-Funding-Cache) → der Score liegt mit neutralem Funding-Bonus zwischen −2 und +4.

Fokus **TrendFollow-Fast (H4-only)**: `RequiresHigherTimeframeContext=false`, kein Entry-TF-Sub-Loop →
ein `MarketContext` pro H4-Kerze (Direktpfad). Output: `reports/portfolio-{label}.md`/`.json` mit Σ PnL,
echter **Konto-MaxDD%** (aus der Equity-Curve), WinRate, PF, Long/Short-Split, Trade-Anzahl + **pro-Symbol-
Breakdown**. Lädt `BingXSymbolInfoProvider` (Min-Order/Min-Notional spiegeln die Live-Reject-Semantik).

**Architektur** (`src/Libraries/BingXBot.Backtest/Portfolio/`): `MergedTimeline` (alle H4-CloseTimes
sortiert+dedupliziert, kein Look-Ahead) · `PortfolioSymbolState` (pro Symbol: Nav-Kerzen, inkr. `navIdx`,
EIGENE Strategie-Instanz — kein geteilter Indikator-State) · `PortfolioBacktestEngine` (1 `SimulatedExchange`
+ 1 `RiskManager`, iteriert die Timeline: Tageswechsel 1×/Kalendertag konto-weit, Preise aller Symbole
setzen, NF8-OpenRisk portfolio-weit, **Exits zuerst** dann **Entries** nach 24h-Volumen absteigend, NF9-
Stream, Equity-Snapshot ~1×/Tag = alle 6 H4-Schritte). Exit/Entry teilen sich `BacktestExitProcessor`/
`BacktestEntryProcessor` mit der Single-Engine (KEINE Duplikation). **Bit-Identität-Gotcha:** Evaluate nutzt
den **pre-exit** Positions-Snapshot (wie die Single-Engine — sonst Re-Entry auf der Exit-Kerze = Look-Ahead);
ValidateTrade/Entry nutzt den **frischen** Snapshot (damit intra-Step-Entries früherer Symbole für die Gates
sichtbar sind). Der Single-Engine-Pfad bleibt unberührt (`ProcessEntryAsync`-Param `adaptLeverage=0`).

## Portfolio-Sweep (`--portfolio-sweep`) — Parameter-Variation auf dem EINEN Konto

Spannt ein Grid über die Strategie-/Risk-Stellschrauben (**SL / BE / TP-RRR / TP1-Split**) auf und fährt für
**jede Kombi einen vollen `PortfolioBacktestEngine`-Lauf über alle Symbole auf EINEM gemeinsamen Konto** (alle
Gates aktiv = live-treu). Beantwortet die Frage: *Dreht IRGENDEINE Parameter-Kombination das live-getreue
Portfolio-Ergebnis ins Plus?* — im Gegensatz zum Single-Symbol-`--sweep`, der auf isolierten 1000-USDT-Konten
pro Symbol läuft (Gates feuern nie → unrealistisch). **Donchian/EMA/ADX bleiben FIX auf Live (10/34/18)**, weil
der Live-Bot diese nicht variiert; nur SL/BE/RRR/TP1-Split werden gedreht.

```bash
dotnet run --project tools/BingXBacktestLab -c Release -- \
  --portfolio-sweep --settings pi-live-settings.json --preset may-live --tfs H4 \
  --from 2022-06-01 --to 2026-06-01 --balance 158 --sweep-grid full --label psweep-mayl-4y
```

| Arg | Default | Zweck |
|-----|---------|-------|
| `--portfolio-sweep` | — | aktiviert den Portfolio-Sweep-Pfad (beendet danach) |
| `--sweep-grid` | full | `full` = 5×3×3×3 = **135 Kombis** (SL{2.0,2.5,2.75,3.0,3.5} × RRR{1.5/3.0,2.0/4.0,1.5/4.0} × BE{1.5,2.0,2.5} × TP1{0.3,0.5,0.7}) · `focused` = 3×2×2×2 = 24 Kombis (Schnelldurchlauf). Baseline-Kombi immer enthalten. |
| `--balance` | 158 | Start-Balance des EINEN Kontos → `Backtest.InitialBalance` |
| `--scanner-filter` / `--btc-health` | true | Live-Spiegel-Vorfilter (GAP 11 / GAP 4), wie `--portfolio` → `false` zum Abschalten |
| `--sweep-parallel` | CPU-Kerne | Kombis laufen parallel (`Parallel.ForEachAsync`); Klines werden via `MemoryKlineCache` einmal vorab warmgeladen (sequenzieller Baseline-Lauf), dann teilen alle Threads den RAM-Cache. |

Jede Kombi ist teuer (ein Voll-Lauf über alle Symbole), daher das fokussierte Grid statt des vollen
Don/EMA/ADX-Kreuzprodukts. SL/RRR gehen über `PortfolioBacktestEngine.RunAsync(trendFollowOverride: …)`
(`TrendFollowParams`-Struct → frische `TrendFollowStrategy` pro Symbol), BE über `Risk.BreakevenTriggerRMultiple`,
TP1-Split über **`Backtest.Tp1CloseRatio`** (NICHT `RiskSettings` — wie beim Single-Sweep). Die Baseline lebt in
`PortfolioSweep.Baseline` (SL2.75/RRR1.5-3.0/BE2.0/TP1×0.5). Report `reports/portfolio-sweep-{label}.md`/`.json`:
alle Kombis nach Σ PnL absteigend, Baseline markiert + ihr Rang, klare Aussage (schlägt beste Kombi Baseline?
dreht irgendeine ins Plus?). Top-10 in der Console.

**Engine-Override (`trendFollowOverride`):** `PortfolioBacktestEngine.RunAsync` nimmt optional `TrendFollowParams?`.
Priorität: explizite `strategyFactory` (Tests) > `trendFollowOverride` (Sweep) > `StrategyFactory.Create` (Default).
Ohne Override unverändert → bestehende `--portfolio`-Läufe bit-identisch (`PortfolioVsSingleRegressionTest`).

## Parameter-Sweep & Walk-Forward (`--sweep` / `--full` / `--compare` / `--axis`)

Vier Modi finden datengetrieben bessere Parameter (statt manuell `settings.json` zu variieren). Alle nutzen
einen In-Memory-Kline-Cache (`MemoryKlineCache`) vor dem Disk-Cache + parallele Ausführung (`--sweep-parallel`,
Default = CPU-Kerne). Backtests sind deterministisch (SimulatedExchange-RNG seed 42 → parallel-sicher).

| Modus | Zweck | Kern-Args |
|-------|-------|-----------|
| `--sweep` | Grid über TrendFollow-Achsen (Don/EMA/ADX/SL/RRR + BE + TP1-Split), Walk-Forward Train→OOS-Test | `--sweep-grid focused\|extended\|sl-fine`, `--train-split 0.65`, `--sweep-top 20`, `--sweep-min-trades 50`, `--sweep-rank expectancy\|pf\|totalpnl` |
| `--axis` | **Isolierter OFAT-Sweep EINER Stellschraube** (`sl`/`be`/`tp`/`tp1split`) durchgehend über den GANZEN Zeitraum, alle anderen Achsen = Live-Baseline. Ehrlichster Einzeleffekt ohne Achsen-Kopplung | `--axis be`, `--axis-values "0,1.0,1.5,2.0,2.5,3.0"` (bei `tp` RRR-Paare: `"1.5/3.0,2.0/4.0"`) |
| `--full` | Mehrere SL-Werte (sonst Live-Default) durchgehend über den GANZEN Zeitraum (alle Phasen, kein Split) | `--compare-sl "2.5,2.75,3.0,3.25"` |
| `--compare` | Dieselben SL-Werte über rollierende, überlappende Fenster — Konsistenz/Robustheit pro Phase | `--compare-sl …`, `--window-days 180`, `--step-days 60` |

`--axis` ist der Schwester-Modus zu `--full` (das nur SL kann) für BE/TP/TP1-Split. Die Live-Baseline lebt
zentral in `Sweep.Baseline` (spiegelt `StrategyFactory.Create("TrendFollow-Fast")` + RiskSettings-Defaults:
Don10/EMA34/ADX18/**SL×2.75**/RRR1.5-3.0/BE2.0/TP1-Split50%) — bei Live-Parameter-Änderungen mitziehen.
**Phasen-Gotcha:** Der durchgehende Lauf bevorzugt bei TP/TP1-Split die weiteren Ziele (Gewinner-laufen-lassen),
weil das 2-Jahres-ΣPnL von der jüngsten Bull-Phase dominiert wird. Immer phasenweise (3 disjunkte Fenster)
gegenprüfen — weite TPs verlieren in Bärenphasen ~2× mehr (Bull-Overfitting). Reports: `reports/axis-*.md`.

**Scoring:** `--sweep` rankt nach **Worst-of-both** (`min(Train, Test)`) — bestraft Overfitting (Train≫Test)
*und* Test-Glück (Test≫Train). TrendFollow-Parameter sind Strategie-Konstruktor-Argumente (direkt instanziiert),
BE + TP1-Split kommen aus den Settings — **Achtung: den TP1-Split liest der Backtest aus
`BacktestSettings.Tp1CloseRatio`, NICHT aus `RiskSettings`** (sonst tunt der Sweep ins Leere).

**Methodik-Gotcha Train/Test-Split:** Ein Train/Test-Split kann einen Parameter über einen Train-Peak-Artefakt
fälschlich favorisieren. Der durchgehende `--full`-Lauf über mehrere Jahre (= alle Phasen in einem Fenster) ist
bei wenigen offenen Achsen die ehrlichere Entscheidungsbasis. Reports: `reports/sweep-*.md`,
`full-*.md`, `compare-*.md` (+ `.json`, gitignored).

## Architektur

- `Program.cs` — Arg-Parsing, Backtest-Matrix (Strategie × Symbol × TF), Aggregation.
- `Sweep.cs` — `Sweep.RunAsync` (Walk-Forward-Sweep), `Sweep.FullAsync` (Voll-Zeitraum), `Sweep.CompareAsync` (rollierender Vergleich).
- `CachingPublicClient.cs` — Decorator um `BingXPublicClient`, cached Klines als JSON in
  `.kline-cache/` (Re-Runs instant, kein Rate-Limit-Druck). Cache-Key = Symbol+TF+from+to (SHA1-Hash).
- `MemoryKlineCache.cs` — In-Memory-Decorator vor `CachingPublicClient` für den Sweep (spart Disk-JSON-Deserialisierung bei tausenden Wiederholungen derselben Klines, thread-safe via `ConcurrentDictionary`).
- `SimpleRateLimiter` (in `Program.cs`) — fixes 120ms-Delay zwischen Live-Requests.
- `live-settings.json` — Snapshot der Pi-Live-Config (Risk/Scanner/Backtest) für realistische Läufe.

`.kline-cache/`, `reports/`, `bin/`, `obj/` sind gitignored (generierte Artefakte).

## Wichtige Erkenntnisse (warum es das Tool gibt)

- **Backtest-Realismus ist nicht selbstverständlich:** SK-System zeigte im Backtest 48 % WinRate /
  PF 1.2, live aber 12 % / PF 0.11. Ursache: SKs **Limit-Entry in der Korrektur-Zone** — der Backtest
  steigt Market zum Candle-Close ein, mit SL/TP für den Limit-Preis gerechnet → künstlich hohe WinRate.
  **Konsequenz:** Market-Entry-Strategien (TrendFollow) sind backtest-treu und vertrauenswürdiger;
  Limit-System-Backtests immer gegen Live validieren.
- Immer über **mehrere Marktzyklen** (z.B. 2024 + 2025) und **Long/Short getrennt** bewerten —
  ein Bullenmarkt-Long-Bias sieht sonst wie Edge aus.
