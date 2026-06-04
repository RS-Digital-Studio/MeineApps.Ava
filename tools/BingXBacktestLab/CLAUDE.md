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
