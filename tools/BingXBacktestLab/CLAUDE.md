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

**Architektur** (`src/Apps/BingXBot/BingXBot.Backtest/Portfolio/`): `MergedTimeline` (alle H4-CloseTimes
sortiert+dedupliziert, kein Look-Ahead) · `PortfolioSymbolState` (pro Symbol: Nav-Kerzen, inkr. `navIdx`,
EIGENE Strategie-Instanz — kein geteilter Indikator-State) · `PortfolioBacktestEngine` (1 `SimulatedExchange`
+ 1 `RiskManager`, iteriert die Timeline: Tageswechsel 1×/Kalendertag konto-weit, Preise aller Symbole
setzen, NF8-OpenRisk portfolio-weit, **Exits zuerst** dann **Entries** nach 24h-Volumen absteigend, NF9-
Stream, Equity-Snapshot ~1×/Tag = alle 6 H4-Schritte). Exit/Entry teilen sich `BacktestExitProcessor`/
`BacktestEntryProcessor` mit der Single-Engine (KEINE Duplikation). **Bit-Identität-Gotcha:** Evaluate nutzt
den **pre-exit** Positions-Snapshot (wie die Single-Engine — sonst Re-Entry auf der Exit-Kerze = Look-Ahead);
ValidateTrade/Entry nutzt den **frischen** Snapshot (damit intra-Step-Entries früherer Symbole für die Gates
sichtbar sind). Der Single-Engine-Pfad bleibt unberührt (`ProcessEntryAsync`-Param `adaptLeverage=0`).

### Live-Paritaet — drei Mechaniken, die live-treu bleiben MÜSSEN (nicht reintroduzieren!)

1. **`AccountInfo.Balance` = reines Wallet (`_balance`), OHNE uPnL** (`SimulatedExchange.GetAccountInfoAsync`).
   Live liefert Wallet als `Balance` + Equity separat (`BingXRestClient.cs:1145`). Der `RiskManager` rechnet
   `equity = Balance + UnrealizedPnl` selbst — würde `Balance` schon uPnL enthalten, ergäbe das `_balance + 2×uPnL`
   (Doppelzählung): Drawdown ~verdoppelt → MaxTotalDrawdown-Freeze feuert zu früh → künstlich wenige Trades.
2. **Abgeschlossene Trades über `BacktestRiskAccounting.RecordCompletedTrade` verbuchen, nie `UpdateDailyStats`
   direkt.** Letzteres zählt jeden `Pnl<0` (inkl. Break-Even-Ausstoppung) als Verlust; live (`ProcessCompletedTrade`)
   erkennt BE-Exits (Buch 6.8) und resettet die Serie. Zusätzlich beim Tageswechsel `SetConsecutiveLosses(0)`
   (live: `TradingServiceBase.cs:454`) — sonst Dauerpause bei `LossStreakPauseAtCount`.
3. **Runner ist 3-stufig** (`BacktestExitProcessor`): TP1 schließt `Tp1CloseRatio` und setzt **TP2** als Ziel
   (kein Runner bei TP1); erst bei **TP2-Hit** + `EnableRunner` wird auf `RunnerPercent×OriginalQuantity` reduziert,
   nur dieser Rest trailt (ATR) bis Trail-Hit **oder** `RunnerHardCap` (423,6%). Spiegelt `TradingServiceBase.cs:822-847`.

**Bewusst NICHT gespiegelt:** Funding-Settlement-`CheckSession` (Live re-scannt alle 15min und steigt danach
trotzdem ein → hartes `continue` wäre untreuer) · BTC-Health-Funding (kein historischer Per-Kerze-Cache → 0) ·
Single-`BacktestEngine` hat keine Gates/Prefilter/BTC-Health/Min-Order → **nur `--portfolio` ist das Spiegelbild**.
No-Freeze-Diagnose (echte Trade-Frequenz/Edge ohne Drawdown-Bremse): `--settings pilive-nofreeze.json`.

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

**Befund (29.07.2026, 4 Jahre 2022-06..2026-06, no-freeze):** KEINE der 135 Exit-Kombis ist positiv
(beste SL3.5/BE1.5/TP1×0.3 = −73 %, Live-Baseline Rang 70/135 mit −94 %). Gegenprobe Konto-Größe:
auch mit 1000 statt 158 USDT bleibt die Baseline bei −98.5 % (PF 0.59, Sharpe −3.28, n=1014) — der
negative Edge ist **intrinsisch**, kein Min-Order-/Mini-Konto-Artefakt.

## Entry-Sweep (`--entry-sweep`) — die ENTRY-Seite auf dem EINEN Konto

Gegenstück zum `--portfolio-sweep` (Exits): Exits bleiben FIX auf Live (SL2.75/RRR1.5-3.0, BE/TP1 aus
Settings), variiert werden **Donchian/EMA/ADX + die Entry-Filter** `RequireRisingAdx` (Chop) und
`MinBreakoutAtr` (BO) — beide seit 29.07.2026 Teil von `TrendFollowParams` (additiv, Defaults neutral).
Grid `full` = 4×3×4×2×3 = 288 Kombis, `focused` = 16. Nach dem Voll-Zeitraum-Ranking werden die Top-K
(`--entry-phase-top`, Default 5; MinTrades-Guard `--entry-min-trades`, Default 100) + Baseline automatisch
über die 4 Marktphasen gegengeprüft (Anti-Bull-Overfitting). Report `reports/entry-sweep-{label}.md`/`.json`.

```bash
dotnet run --project tools/BingXBacktestLab -c Release -- \
  --entry-sweep --sweep-grid full --preset may-live --tfs H4 \
  --from 2022-06-01 --to 2026-06-01 --balance 158 --settings pilive-nofreeze.json --label entry-4y
```

**Befund (29.07.2026, 288 Kombis, 4 Jahre, no-freeze):** KEINE Entry-Kombination dreht den
TrendFollow-Fast-Portfolio-Mirror ins Plus (beste −79.6 %, Baseline Don10/EMA34/ADX18 = Rang 253/288
mit −94.3 %, alle 4 Phasen negativ bei allen Kandidaten). Konsistente Richtungs-Signale: BO 0.5×ATR
und höheres ADX (22–25) verbessern IMMER, reichen aber nie Richtung profitabel → **keine Übernahme,
Scalper-Entry-Optimierung ist ausgereizt** (deckt sich mit dem Phasen-Screen-Befund zu direktionalen
Strategien). Freeze-Hinweis: mit Live-Settings (`MaxTotalDrawdownPercent=10`) friert das 4-Jahres-Konto
nach 7 Trades dauerhaft ein — Edge-Messung daher mit `pilive-nofreeze.json` (=100; **nicht 0** — 0 heißt
seit dem RiskManager-Fix „deaktiviert", davor bedeutete es „sofortiger Dauer-Freeze").

## Phasen-Robustheit (`--phase-screen`) — Strategie in JEDER Marktphase profitabel?

Testet jede Strategie über **4 disjunkte ~1-Jahres-Phasen** (Bear/Recovery/Bull/Recent, `PhaseScreen.DefaultPhases`)
auf dem live-getreuen Portfolio-Mirror. Rangmetrik = **schlechteste Phasen-Rendite** (Anti-Overfitting: nicht
aggregiert, sondern „in welcher Phase ist die Strategie am schwächsten?"). `--strategies A,B,C` überschreibt das
Default-Set (TrendFollow-Familie + MeanReversion). Befund: **KEINE direktionale IStrategy ist in allen 4 Phasen
positiv** — 2022-Bear killt jede (absolute Richtung kippt immer irgendwo).

```bash
dotnet run --project tools/BingXBacktestLab -c Release -- \
  --phase-screen --preset may-live --balance 158.36 --settings pi-live-settings.json --label ps-existing
```

## Cross-Sectional-Momentum (`--xsec`) — strukturell phasen-robust

Eigene Engine (`CrossSectionalMomentumEngine`, kein IStrategy): long die Top-K / short die Bottom-K Symbole nach
vol-bereinigtem Momentum, periodischer Rebalance, EIN Konto. **Relativ statt absolut → funktioniert in Bull UND
Bear.** Ranking/Korb-Bildung im geteilten `BingXBot.Engine/Portfolio/MomentumBasketCalculator` (Backtest + künftig
Live nutzen DENSELBEN → Parität). `--xsec` fährt `XsecScreen.DefaultConfigs` über die 4 Phasen.

```bash
dotnet run --project tools/BingXBacktestLab -c Release -- \
  --xsec --top-coins 80 --include-tradfi true --balance 158.36 --settings pi-live-settings.json --label xsec
```

**Validierter Befund:** `L120/R126/radj/lev1` (20-Tage-Momentum, monatlicher Rebalance, market-neutral,
vol-bereinigt, **1x Leverage**) ist in ALLEN 4 Phasen positiv (Σ +64..+172% über alle Universen × Kontogrößen).
Zwei Schlüssel: (1) **Leverage ist der Killer** — lev1 Bear +10%, lev5 Bear −81% (`XsecParams.LeverageCap`).
(2) **Slot-Anzahl K skaliert mit Universums-Breite** — schmal (Top-30/80) → 3L-3S, breit (Top-150) → 5L-5S. Konto
≥1000 USDT entfernt Min-Order-Fragmentierung. `XsecParams`: Lookback/Rebalance/LongK/ShortK/RiskAdjusted/AtrStop/
LeverageCap. **Caveat:** Survivorship-Bias (Top-N = heutiges Top-N, rückwirkend) → Zahlen optimistisch verzerrt.

**Fein-Optimierung (13.06.2026, `--xsec-grid fine`/`final`):** Der eigentliche Optimum ist NICHT L120/R126,
sondern **`L60/R54/3L-3S` (10-Tage-Lookback, 9-Tage-Rebalance)** — robust (4/4) über Top-50 UND Top-80, über
K=2/3/4 UND lev1/lev2 (Plateau, kein Peak). Bei lev2: min +34.9%/+8.9% (vs Live L120/R126 = 2/4, min −40%/−68%).
Deckt sich mit der Literatur (10–14d Lookback, ~weekly Rebalance; survivorship-bias-freie AUT-Studie: 14d/7d).
**Portfolio-Vol-Targeting** (`XsecParams.VolTargetAnnualPct`, zeitvariable Gesamt-Exposure nach realisierter
Equity-Vol — NICHT zu verwechseln mit `InverseVolWeight` = within-basket): `vt30` macht L60/R54 über beide
Universen identisch robust (min +30.1% beide), dämpft die Universums-Varianz (Lit.: conditional Sharpe-Gewinn).

**Voll-Re-Validierung (29.07.2026, `--xsec-grid reval`/`unicheck`) — Juni-Befund reproduziert NICHT:**
Alle optimierten Achsen + alle verworfenen Extras erneut über die 4 Phasen, auf dem **heutigen**
Top-50-Schnitt: das Live-Profil `L60/R54/3L-3S/lev2` fällt auf 2/4 (Bear −54 %, Bull −32 %), und KEINE
der 36 Configs ist noch 4/4-positiv — obwohl die 2022er-Kerzen identisch sind. Ursache: **Universums-Drift**
(heutige Top-50 ≠ Juni-Top-50; 20/50 TradFi). Der `unicheck`-Quervergleich über 4 Schnitte
(Top-40/50/60 + may-live-Preset) zeigt: die Phasen-Ergebnisse sind snapshot-sensitiv, Parameter-Rankings
würfeln — mit EINER Ausnahme: **`L60/R42` (7-Tage-Rebalance) hat auf ALLEN 4 Schnitten die beste (bzw.
gleichauf beste) Worst-Phase** (−17..−35 % vs. Live −44..−62 %), opfert dafür Σ-PnL in der Recent-Phase
(Top-50: +185 % vs. +398 %). Neue Struktur-Mechanismen (beide im geteilten `MomentumBasketCalculator`,
Live-Parität): `ExitRankBuffer` (Rank-Hysterese gegen Turnover) und `ClusterDiversify` (max. 1 Symbol je
Asset-Cluster/Seite) — beide verbessern die Worst-Phase NICHT robust → nicht übernommen. Asymmetrische
Slots (3L-2S…4L-2S): Recovery/Recent-Booster, aber Bear schlechter → nicht übernommen. Konsequenz für
künftige Xsec-Entscheidungen: **immer über mehrere Universums-Schnitte validieren** (`unicheck`-Muster),
ein einzelner Top-N-Snapshot genügt nicht. **Fee-/Konto-Hebel quantifiziert (29.07.2026):** Maker- statt
Taker-Fees (0.02 statt 0.05 %) heben Σ nur um ~5 pp (bei ~+400 %) und lassen die Worst-Phase unverändert
→ Limit-Order-Rebalancing lohnt die Komplexität nicht. Konto-Größe 158/500/1000/5000 USDT: %-Performance
praktisch identisch → Min-Order-Fragmentierung ist beim Top-50-Profil KEIN Bremsklotz, mehr Kapital
ändert die Prozent-Zahlen nicht.

**Externe Evidenz-Recherche (30.07.2026, 25 Quellen, 122 Claims, 3-Stimmen-Verifikation je Claim →
6 überlebt; Rohdaten in `reports/research-*.json`, gitignored).** Ergebnis: KEIN Fremd-Befund
rechtfertigt eine Änderung am Live-Profil. Die drei Punkte mit Steuerungswirkung:

1. **Liquiditäts-Deflator (3-0)** — die publizierten Krypto-Querschnitts-Alphas leben im
   illiquiden/Micro-Cap-Segment; bei Marktkap-Gewichtung und realistischen Kosten verschwinden sie
   („significantly" reduziert, Autoren erklären die Persistenz selbst mit Illiquidität). Ein Top-50-
   nach-24h-Volumen-Universum filtert genau dieses Segment heraus → **die konsistenteste Erklärung
   unserer gesamten Falsifikationsserie** und ein Dämpfer für JEDEN weiteren Signal-Kandidaten.
2. **Signal-Kandidaten mit Restsubstanz, aber ohne Transfer-Nachweis:** CTREND (JFQA 2025, 2-1,
   ML-Blend aus 28 Preis-/Volumen-Signalen über mehrere Horizonte, netto 2,35–2,90 %/Woche) —
   Sample endet **Mai 2022**, also null Abdeckung für unser Falsifikationsfenster, Spot statt Perps,
   Quintile über Tausende Coins statt 6 Slots; im Illiquiditäts-Doppelsort schrumpft das Alpha von
   ~2,6 % auf 0,62 %. Residualisiertes Momentum (3-0) ist ein **Risiko-**, kein Ertrags-Effekt
   (Max-DD −93 % → −36..−55 %), belegt auf US-Aktien 1968–2022 brutto; für Perps fehlt ein
   Faktormodell zum Residualisieren.
3. **Ohne verifizierbare Evidenz (nicht widerlegt, aber kein nächster Schritt):** ML-P(Win)-Gates,
   Regime-Detection/HMM als Exposure-Schalter, On-Chain-/Order-Book-/Sentiment-Signale,
   Trailing/Zeit-Stops/Profit-Locks zwischen Rebalances, Point-in-Time-Universen für Retail.
   Time-Series- statt Cross-Sectional-Momentum (3-0 widerlegt als Alternativmodus): die 31,96 % vs.
   14,59 % p.a. sind **brutto bei täglichem** Rebalance → erhöht den Kostendrag statt ihn zu senken.
   Deep-RL (3-0): bester OOS-Agent **−34,96 %**, alle Agenten negativ — nur relativ besser als der
   −50,78 %-Benchmark, kein Ertragsnachweis.

> **Quellen-Warnung:** Drei inhaltlich hochrelevante Behauptungen (ML-Konfidenz-Sizing, gelerntes
> Regime-Trust-Gate mit AUROC 0,721, Gate+Cap-Overlay-Sharpes) stammten aus `arXiv 2603.13252` —
> 3× einstimmig verworfen, die ID ist **nicht als existierendes Paper bestätigt**. Diese Zahlen
> dürfen in keiner Folgeentscheidung auftauchen.

**Verwertbar ist genau ein Werkzeug:** der **CSCV/PBO-Overfitting-Test** (Bailey/López de Prado) als
Hypothesentest mit α = 10 % — als Selektions-Hygiene für die großen Sweeps (Lookback/Rebalance-Grid,
288×135 Entry/Exit-Kombis). Er adressiert aber nur Selektions-Overfitting über viele Trials, **nicht**
unseren Ist-Fehlermodus (Universums-Snapshot-Sensitivität). Der eigentliche Engpass bleibt ein
**Point-in-Time-Universum** (Listing-/Delisting-Historie + historische 24h-Volumen-Snapshots pro
Rebalance-Datum): ohne das ist jeder weitere Signal-Test nicht entscheidbar.

**BTC-Anker-Screen (11.08.2026, `--xsec-grid anchor`) — Seiten-Zerlegung + Dominanz-Spread:**
Ausgangspunkt war die Top-50-Struktur-Analyse (78 % der Alts underperformen BTC ueber die
Lebenszeit; Report `reports/btc-dominanz-analyse.html`). Der Screen zerlegt das Live-Profil in
seine Seiten (der Xsec-Report weist seit 11.08. **Long-/Short-PnL je Phase** aus: `LongPct`/
`ShortPct` in Zelle+JSON) und testet zwei neue Lab-Modi (`XsecMode.AnchorBtc` = Long fest BTC/
Shorts wie Live; `XsecMode.DominanceSpread` = Long BTC 50 % / Short die ShortK volumenstaerksten
Krypto-Alts, seitenweise 50/50-Gewichtung, fehlende Slots bleiben Cash). Befund ueber 4 Schnitte
(Top-40/50/60 + may-live) + 1000-USDT-Gegenprobe:
1. **Die Momentum-Short-Seite (Bottom-K) verliert in praktisch jeder Phase auf jedem Schnitt**
   (Short-only 0L-3S: Worst-Phase −82..−98 %, 0/4 bis 1/4). Der gesamte Edge des Live-Profils
   kam von der Long-Seite — die Shorts sind konstanter Ballast, auch in Bear-Phasen.
2. **Long-only (3L-0S)** traegt riesige Σ (bis +2900 % via Einzel-Moonshots), bleibt aber 2/4
   (Bear/Bull negativ) — Lotterie-Varianz, kein robustes Profil.
3. **AnchorBtc repariert das nicht** (Worst −12..−58 %, 1/4–3/4): auch mit BTC-Anker bleibt die
   Momentum-Short-Auswahl der Verlierer.
4. **DominanceSpread R180 (~monatlich)/lev1 hat auf ALLEN Schnitten die beste Worst-Phase**
   (−1,3..−9,0 % bei 158 USDT; −3,2/−6,3 % bei 1000 USDT mit 20S/30S) — die mit Abstand beste je
   im Harness gemessene Worst-Phase (Live-Profil zum Vergleich: −44..−75 %). Meist 3/4 robust,
   Σ +9..+27 % ueber 4 Jahre (1k-Konto). Verlust-Phase ist Recent — getrieben vom BTC-Long selbst
   (BTC-Rueckgang ab Ende 2025), die Alt-Short-Seite blieb dort positiv.
   **Gotchas:** breite Koerbe (≥20S) fragmentieren auf 158 USDT unter Min-Order (n=13..17,
   Shorts fehlen still → Zahlen unbrauchbar, nur ≥1000 USDT bewerten); ShortK nach Volumen
   waehlt systematisch die „heissen" Alts (konservativer als der equal-weight-Alt-Index der
   Struktur-Analyse — deren Sharpe 0.88 ueberlebt den live-treuen Harness nur teilweise).
   Offen: echte Funding-Historie (Sim = flat 0,01 %/8h; real ist Alt-Funding meist > BTC-Funding
   → Rueckenwind fuer den Spread), Point-in-Time-Universum.

**Weitere Strategie-Klassen getestet (`--xsec-grid strategies`, `--pairs`, `--funding-carry`):** Reversal,
Low-Vol-market-neutral, Inverse-Vol-Gewichtung, Skip-Period, Pairs-Trading (Distance/Gatev), Funding-Harvest —
**alle NEGATIV/nicht-robust**. **Level-Familie (13.07.2026, `--phase-screen`, Live-Settings):** S/R-Level aus
geclusterten Swing-Pivots (`Level-Bounce`/`-Bounce-Trend`/`-Retest`, `LevelStrategy` in BingXBot.Engine,
Lab-only) — alle Phasen-Renditen −5..−22 % (Bounce-Trend 1/4 positiv, Rest 0/4), Report
`reports/phase-screen-level-screen-live.md`. Auch explizites Level-Trading falsifiziert die
Keine-direktionale-Strategie-These NICHT. Long-only-Momentum/LowVol + Momentum+Carry-Combo-long-only robust, aber
survivorship-bias-verdächtig (Bear-Phase aufgebläht). Funding-Carry-Faktor (long high/short low) standalone
nicht robust. **Fazit: reines Cross-Sectional-Momentum L60/R54/3L-3S bleibt die beste bias-robuste Wahl.**

**Engines:** `CrossSectionalMomentumEngine` (XsecMode Momentum/Reversal/LowVol, InverseVolWeight, SkipCandles,
VolTargetAnnualPct), `PairsTradingEngine` (Distance/z-Score), `FundingCarryEngine` (Carry-Faktor + Momentum-Combo,
echte Funding-Historie via `FundingHistoryProvider`). Screens: `XsecScreen`, `PairsScreen`, `FundingScreen`.

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
