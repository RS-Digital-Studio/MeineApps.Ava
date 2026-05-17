# BingXBot — Strategie-Analyse & Roadmap

**Stand:** 17.05.2026
**Autor:** Claude (Cowork) auf Anfrage von Robert
**Codebase-Stand:** v1.8.0, Branch `main`
**Eingabe-Frage:** "Viele Trades, aber meistens falsch gesetzt und nicht nach SK-System. Kann man das SK-System automatisieren? Sind andere Strategien evtl. besser?"

---

## TL;DR (für eilige Leser)

1. **Das Problem ist nicht SK, sondern die Konfiguration.** Die Implementierung der SK-Regeln ist korrekt und im Kern buchtreu. Aber **sieben der harten Buch-Filter sind per Default ausgeschaltet** (Opt-In), und der Confluence-Score wirkt aktuell nicht als Gate (`MinConfluenceScore = 0`). Das Resultat ist exakt das, was du beobachtest: viele Setups, viele schwache Setups, schlechte Trefferquote.
2. **Quick-Win (Tage):** Defaults auf "Buch-Hard" stellen, Confluence-Score-Gate aktivieren, eine kleine Anomalie in den BCKL-Fib-Levels entfernen. Realistisch: **30-50 % weniger Trades, deutlich besseres Win/RRR-Verhältnis**, ohne dass eine Zeile Strategie-Code neu geschrieben werden muss.
3. **Mittelfrist (Wochen):** **ML-Filter über die SK-Signale legen** (LightGBM/XGBoost-Classifier mit Probability-Threshold ≥ 55 %). Genau das, was die Web-Praxis als "good → great"-Hebel beschreibt. Du behältst SK als Setup-Erkennung und filterst die schlechten 40-50 % weg. Datenbasis ist da (`EvaluationDecisions` + `Trades`).
4. **Mittel/Langfrist:** **Regime-Detection (HMM) + Strategie-Portfolio**. SK ist ein Trend/Korrektur-System und leidet in Range-Märkten. Ein zweiter, **negativ korrelierter** Algo (Donchian-Breakout oder Mean-Reversion) ergänzt SK strukturell — die Forschungslage 2024-2026 ist da sehr eindeutig.
5. **SK ersetzen ist nicht nötig und nicht empfohlen.** SK ist auf Crypto-Perps für die Timeframes H4/H1 ein vernünftiger Edge — wenn die Filter scharf stehen. Die größeren Hebel liegen in **Disziplin der Defaults, ML-Filtering und Portfolio-Diversifikation**, nicht im Strategiewechsel.

---

## 1. Diagnose — warum die Live-Ergebnisse so aussehen, wie sie aussehen

### 1.1 Audit der SK-Implementierung (Mai 2026, Codebase v1.8.0)

Ich habe `SequenzKonzeptStrategy.cs`, `SequenceStateMachine.cs`, `RiskSettings.cs`, `ScannerSettings.cs` gelesen und gegen das Spec-Compliance-Audit vom 21.04.2026 abgeglichen (`.claude/agent-memory/bingxbot/sk_spec_compliance_2026_04_21.md`).

**Befund 1 — Hardfilter sind opt-in, Defaults sind weich:**

| Setting | Code-Default | Buch-Stand | Wirkung auf Trade-Volumen |
|---|---|---|---|
| `ScannerSettings.ImpulseAtrMultiplier` | **2.0** (Datei `ScannerSettings.cs:31`) | 3.0 (Strukturpunkte §2) | Default lässt deutlich kleinere Impulse passieren → mehr Sequenzen, aber viele aus reinem Rauschen |
| `ScannerSettings.RequireBosCloseBreak` | **false** (`ScannerSettings.cs:113`) | true (Strukturpunkte §3) | Docht-Bruch reicht statt Body-Close → viele Fake-BOS |
| `ScannerSettings.RequireBosVolumeBreakout` | **false** (`ScannerSettings.cs:41`) | true (§5A) | BOS-Kerze muss eigentlich ≥ 1.5× SMA20-Volumen haben — der Filter ist drin, aber aus |
| `RiskSettings.RequireWickRejectionInBZone` | **false** (`RiskSettings.cs:124`) | true (§5C) | Pinbar/Engulfing in B-Zone als Entry-Bestätigung — fehlt aktuell |
| `RiskSettings.RequireBoxCloseOnEntry` | **false** (`RiskSettings.cs:135`) | true (§4 / B12) | Body in/über Box muss bestätigt sein — fehlt aktuell |
| `ScannerSettings.BlockLtfEntryWhenHtfInTargetZone` | **false** (`ScannerSettings.cs:122`) | true (§7 MTA) | Buch verbietet LTF-Entry, wenn HTF in EXT_1618-2000 — Top-/Bottom-Picking-Schutz aus |
| `RiskSettings.MinConfluenceScore` | **0** (`RiskSettings.cs:162`) | — (Buch kennt keinen Score) | Mit 0 ist das Gate `if (minScore > 0 && score < minScore)` in `SequenzKonzeptStrategy.cs:630` **wirkungslos** — jedes Setup mit Score 1 oder 2 wird getradet |

Das Audit-Memo vom April hat genau diese Lage explizit dokumentiert ("Alle Hard-Filter sind bewusst opt-in, damit die 434 bestehenden Tests grün bleiben") — die Defaults wurden bewusst weich gewählt, um nicht die Test-Suite zu brechen. Das ist eine Engineering-Entscheidung, **keine Trading-Entscheidung**. In der Praxis bedeutet das: Du fährst live ein "SK-Lite", nicht das tatsächliche SK-System aus dem Buch.

**Befund 2 — Confluence-Score zählt, aber filtert nicht:**

`SkConfluenceScorer` vergibt 1-2 Punkte pro Kategorie (Price Action, Fibonacci-Golden-Pocket, Fahrplan-Alignment, HigherTfSequence, VolumeSpike, BcklReEntry, FavorableFundingRate, GklMasterZone +2, HighProbabilityZone +2). MaxScore = 10. Das wird pro Setup berechnet, in den Decision-Trail geschrieben — aber **mit Default `MinConfluenceScore = 0` blockiert es nichts**. Im Code (`SequenzKonzeptStrategy.cs:629-633`):

```csharp
var minScore = context.RiskSettings?.MinConfluenceScore ?? 0;
if (minScore > 0 && score < minScore)
{
    return Blocked(navTf, $"Hard-Gate: Confluence-Score {score} < {minScore}", ...);
}
```

Setups mit Score 1 (z. B. nur "Price Action") laufen identisch durch wie Setups mit Score 8 ("Heiliger Gral"). Die Risk-Sizing-Logik kennt den Score nicht — es gibt zwar einen `HighProbabilityPositionMultiplier`, aber kein Score-basiertes Sizing.

**Befund 3 — Kleine, aber riechende Code-Anomalie bei BCKL-Re-Entry:**

In `SequenzKonzeptStrategy.cs:431-432` (dynamische BC-Zone) werden BCKL-Fib-Levels mit `* 0.118m` und `* 0.236m` berechnet — also 11.8 % und 23.6 %. Das Audit vom 22.04.2026 (`sk_0ab_detection_2026_04_22.md`) hat das schon markiert: "11.8 %/23.6 % nicht in SK-Fib-Tabelle, toter Wert". Diese Zahlen sind weder klassische Fibonacci-Levels noch Stefan-Kassing-Tabellenwerte. Das wirkt wie ein historischer Copy-Paste-Fehler (vermutlich gemeint: 0.5 / 0.618). Wirkt zwar nur auf BCKL-Re-Entries, aber genau das ist der "IMMER-Trigger nach Aktivierung" — also kein seltener Pfad.

**Befund 4 — User-Defaults im Risk-Management bewusst lockerer als Buch:**

Hier sind die Abweichungen sauber in `src/Apps/BingXBot/CLAUDE.md` dokumentiert (Abschnitt "Bewusste User-Abweichungen vom Buch") und sind klar deine Entscheidung, kein Bug:

- Risiko pro Trade 5 % statt 1-3 %
- Margin-Anteil 10 % statt 1-3 %
- Loss-Streak-Halve bei 4 statt 3
- Loss-Streak-Pause bei 7 statt 5

Diese Werte wirken nicht auf die **Setup-Qualität** (Win-Rate), sondern auf das **Risiko-Profil** (Drawdown-Tiefe und -Geschwindigkeit). Sie sind ein zweiter Hebel und werden hier nicht zur Diskussion gestellt — du hast sie bewusst gesetzt.

### 1.2 Was ich nicht prüfen konnte

- **Pi-Daten (Live-DB):** Der Sandbox-Linux hat keinen Netzwerk-Zugriff auf dein lokales Netz (`raspberrypi.local` über mDNS/Tailscale). Ich konnte daher keinen direkten SQL-Auszug aus der `Trades`-, `Equity`- und `EvaluationDecisions`-Tabelle ziehen. Im **Anhang A** steht ein fertiger Befehl, mit dem du in 30 Sekunden einen Snapshot ziehst, den ich dann auswerte.
- **Backtest-Reports:** Keine `.json`/`.csv`-Outputs des `WalkForwardRunner` im Repo gefunden — du hast das Tool, aber offenbar keine archivierten Läufe.

### 1.2a Wichtiger Hinweis zur Default-Tabelle in 1.1

Die `RiskSettings`/`ScannerSettings`-Klassen-Defaults aus der Tabelle sind **Code-Defaults** — gelten bei frischer Installation. **Wenn du die Werte über die UI je verändert hast, liegen sie in der SQLite-DB und werden beim Start eingelesen — Code-Default wird dann überschrieben.** `appsettings.json` enthält nur Server-Config, keine Strategie-Settings.

Konsequenz: Erst der Pi-DB-Snapshot (Anhang A) zeigt mir verlässlich, welche Werte aktuell live wirken. Es ist gut möglich, dass einige der genannten Defaults bei dir bereits hochgezogen sind. Die Lücke "viele falsche Trades" zeigt aber, dass mindestens ein Teil der Filter aktuell zu locker steht — die ersten zu prüfenden Verdächtigen sind `MinConfluenceScore`, `ImpulseAtrMultiplier`, `RequireWickRejectionInBZone` und `RequireBoxCloseOnEntry`.

### 1.3 Was die Beobachtung "viele Trades, falsch gesetzt" wahrscheinlich ist

Die Diagnose-Logik:

1. Bot generiert viele Sequenzen, weil `ImpulseAtrMultiplier=2.0` (statt 3.0) bereits ~50-60 % mehr Sequenz-Kandidaten erlaubt (ATR ist eine kontinuierliche Verteilung; Schwellen-Senkung von 3 auf 2 verdoppelt grob die Menge der "qualifizierten" Impulse, weil die ATR-Verteilung auf Crypto-Perps nicht gauß-förmig ist sondern fat-tailed; mein Bauchgefühl, gerne mit Decision-Trail-Daten verifizieren).
2. Davon werden viele zu Setups, weil weder Wick-Rejection noch Box-Close noch BOS-Close-Break verlangt werden.
3. Davon scheitern viele am eigentlichen Entry-Timing, weil die B-Zone-Bestätigung fehlt.
4. Setups mit Confluence-Score 1-2 (z. B. nur "Price Action existiert") laufen identisch durch wie Score-8-Setups.
5. RRR 1.0 als Minimum (`MinRRR=1.0` per Buch) bedeutet: ein 50 %-Win-Rate-Trader fährt Breakeven minus Gebühren. Bei niedrigerer Win-Rate wird's negativ.

Das ist konsistent mit deiner Aussage. Es ist **keine** Fehlimplementierung der SK-Regeln — die Regeln sind drin. Sie sind nur abgeschaltet.

---

## 2. Lässt sich SK automatisieren? — Die ehrliche Antwort

**Ja, aber mit Einschränkungen.** Drei Aspekte trennen:

### 2.1 Was die Implementierung schon kann

- Pivot-Detection mit asymmetrischem Fenster (5 links, 3 rechts, adaptiv via ATR)
- ATR-basierter Impuls-Filter
- BOS-Anker mit Last_Swing_High/Low vor P0, mit Docht- oder Close-Break-Option
- Vollständige State-Machine 0 → SucheA → SucheB → Aktiviert → Abgearbeitet
- Triple-Entry-Staffelung (Primary @ 50 %, Additional @ 66.7 %, BCKL-Re-Entry @ Dynamic BC-Zone)
- Fibonacci-Levels inkl. 0.559 (Buch-Tabelle)
- TP-Staffelung 161.8 % / 200 % / 261.8 %
- News-Blackout (TradingEconomics-API mit Fallback-Stub)
- Counter-Trend-Scalp (opt-in, hochriskant)
- "Heiliger Gral"-Detektor (HTF GKL ∩ LTF BC-Zone Overlap)

Im Vergleich zum Buch-Wortlaut: ~95 % der dokumentierten Regeln sind implementiert. Was fehlt, ist (a) die Disziplin der Default-Konfiguration und (b) die diskretionäre Komponente, die nicht automatisierbar ist (siehe 2.3).

### 2.2 Was bei Automatisierung mehr Aufmerksamkeit braucht

- **Point-0-Pivot-Check ist im Code rechtsseitig (Trailing + minCandles-Counter), nicht streng beidseitig** (`SequenceStateMachine.cs:641-692` laut Audit). Die Spec §1 verlangt beidseitige Pivot-Bestätigung. Bei genug Historie funktional gleichwertig, aber strenggenommen ein loserer Filter.
- **Point-B-Trigger** im Code trailed bis A-Break, Spec §4 verlangt "neues Pivot Low in der Box + Box wieder verlassen". Das ist eine erkennbare Abweichung.
- **`EnableBiasFlip = true` als Default** ist laut Audit "funktional redundant" mit `FailedPoint0`/`PromotedToLarger`. Doppelte Logik für dieselbe Sache ist eine klassische Quelle für False Positives — entweder eine Variante deaktivieren.

### 2.3 Was sich grundsätzlich schlecht automatisieren lässt

SK ist im Buch als **discretionary system** geschrieben — Stefan Kassing predigt Geduld, Marktverständnis und das Erkennen "ungewöhnlich kaputter" Sequenzen, die ein Algorithmus per Definition nicht erkennt. Konkret:

- "Fahrplan"-Interpretation auf W1/D1: Im Buch eine ganzheitliche Beurteilung, im Bot eine Heuristik (Position innerhalb 30-70 % der HTF-Range). Das ist eine simplifizierende Abstraktion — funktional gut genug, aber nicht "das echte Fahrplan-Konzept".
- News-Kontext: Bot kennt nur Blackout-Fenster. Trader weiß, ob das Event "geleaked", "bull-priced" oder "neutral erwartet" ist.
- Korrelations-Cluster über Asset-Klassen: Bot hat zwar `AssetClusterClassifier`, aber kein Verständnis für aktuelle Makro-Treiber (z. B. "USD-Risk-Off heute").

Das sind aber Themen, in denen kein anderes System (auch nicht ML/RL) zaubern kann. Sie sind durch das Schwächen-Profil **aller** automatisierten Strategien.

### 2.4 Realistische Erwartung an SK-Automatisierung

Bei sauberen Defaults (Buch-Hard + Confluence-Gate ≥ 5) ist auf Crypto-Perps für H4/H1 ein Setup-Profil erreichbar, das in der Literatur als "Trend-Following mit Confirmation-Filter" einsortiert würde — typische Werte:

- Win-Rate 40-55 % (Trend-Folge-Systeme verlieren mehr Trades, gewinnen größer)
- Avg-RRR 1.5-2.5 (deine TP-Staffelung 1.618/2.0/2.618 zielt da hin)
- Sharpe 0.5-1.0 nach Gebühren auf Crypto-Perps (laut Stratzy- und Qoppac-Daten 2025)
- Max-Drawdown 20-35 % (typisch für Trend-Systeme in choppy Markets)

Wenn deine Live-Zahlen aktuell deutlich darunter liegen, sind die Default-Fixes der erste Hebel.

---

## 3. Sind andere Strategien besser? — Was die Literatur 2024-2026 sagt

### 3.1 Direkter Vergleich Trend vs. Mean-Reversion (Crypto-Futures)

| Aspekt | Trend-Following (SK, Donchian) | Mean-Reversion |
|---|---|---|
| Typischer Sharpe | 0.5-0.8 | 0.8-1.2 |
| Drawdown-Profil | 20-40 % in choppy Markets | tendenziell kleiner, aber Black-Swan-Risiko bei Trendausbrüchen |
| Funktioniert gut bei | Trends, Breakouts (4h-1d) | Range-Märkten, intraday |
| Funktioniert schlecht bei | Range, Whipsaw | starken Trends |
| Korrelation | strukturell **negativ** zu Mean-Reversion | strukturell negativ zu Trend |

Quelle: [Stratzy 2025-2026 Comparison](https://stratzy.in/blog/untitled-10/), [SetupAlpha Medium 2026](https://medium.com/@setupalpha.capital/trend-following-vs-mean-reversion-which-strategy-wins-in-2026-9513565b73f7), [Qoppac Mar 2025](https://qoppac.blogspot.com/2025/03/very-slow-mean-reversion-and-some.html).

**Key Insight:** Es gibt **keinen Gewinner** — Trend und MR sind in unterschiedlichen Regimen stark und ergänzen sich. Crypto ist überwiegend momentum-driven (favoriziert Trend), aber Range-Phasen sind regelmäßig.

### 3.2 ML-Filtering — der pragmatischste Hebel

Mehrere unabhängige Quellen (MQL5-Series 2025, Bohrium 2026, Medium-Berichte) konvergieren auf folgendes Pattern:

- Klassische regelbasierte Strategie generiert Signale (hier: SK).
- LightGBM/XGBoost-Classifier wird auf historischen Setups trainiert, Target = "war das ein Win bei Mindest-RRR von X?".
- Features: alle Inputs, die die Strategy schon kennt (ATR, Volumen, Funding, HTF-Confluence-Kategorien, Confluence-Score, Marktbedingungen) plus simple Microstructure-Features (Spread, Order-Book-Imbalance, recent volatility).
- Im Live-Betrieb: Strategy schlägt Trade vor → ML gibt P(Win) → Trade nur, wenn P ≥ 0.55 (oder höher, je nach Toleranz).

**Wirkung:** Filtert die schlechtesten 30-50 % der Setups raus. Reduziert Trade-Volumen, erhöht Win-Rate und PnL pro Trade. Eine Kombination klassisch + ML wird in der Literatur als "Industriestandard für quantitative Bots 2024-2026" beschrieben.

**Praktisch für BingXBot:** Du hast bereits den **Decision-Trail** in `EvaluationDecisions`. Damit lässt sich offline ein Trainingsdataset bauen (Reject = negative class, Trade-Win = positive class, Trade-Loss = negative class). Mit ein paar tausend Decisions ist ein sinnvolles Modell trainierbar.

Quelle: [MQL5 LightGBM-Series](https://www.mql5.com/en/articles/14926), [Stefan Jansen ML4Trading](https://stefan-jansen.github.io/machine-learning-for-trading/12_gradient_boosting_machines/), [Bohrium 2026 Comparison](https://www.bohrium.com/en/blog/tutorials/xgboost-vs-lightgbm/).

### 3.3 Reinforcement Learning — interessant, aber Vorsicht

RL-Forschung auf Crypto-Futures (PPO, A2C, DDQN) zeigt:

- A2C-basiertes Portfolio-System auf 18 Binance-Coins (Jan 2022 - Dez 2023, Daten aus MDPI-Paper 2025) erreichte **16-17 % auf Hochfrequenz, 6-7 % auf Daily** — outperformte Buy-and-Hold und Long-Only-Strategien (Annual Return, Sharpe, MaxDD).
- PPO ist meistens besser als DDQN, außer auf ETH-Märkten.
- DDQN-Agents zeigen niedrigere Returns, aber niedrigere Drawdowns (-0.98 %) und höheren Sharpe (0.21) → gute Risk-Manager.

**Honest disclaimer:** Bei RL ist die Distanz zwischen Paper-Ergebnis und Live-Performance traditionell groß. Slippage, Reward-Design, Non-Stationarity der Crypto-Märkte, Transaction Costs — die Liste der "Killer" ist lang. RL als **Position-Sizing-Layer** oder **Regime-Switcher** über klassischen Strategien ist die ehrlichere Anwendung als RL als "Black-Box-Strategie".

Quelle: [MDPI Cryptocurrency Futures Portfolio Trading System Using RL 2025](https://www.mdpi.com/2076-3417/15/17/9400), [SSRN Risk-Aware Deep RL 2025](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=5662930).

### 3.4 Regime Detection (HMM) — der hochwertige "freie Lunch"

HMM-basierte Market-Regime-Detection ist eine der besseren Investitionen der letzten zwei Jahre Algo-Forschung:

- HMM-LSTM-Hybride übertreffen statistische Forecast-Modelle auf BTC 2024-2026.
- HMM erkennt Bull/Bear/Range-/Squeeze-Regime aus Returns, Volatility, Volume.
- Ein einfacher Hebel: in High-Vol-Regimen Position halbieren oder pausieren → eliminierte in den Studien systematisch viele Verlust-Trades und hob den Sharpe deutlich.
- Multi-Modell-Ensemble (HMM + Bagging/Boosting) wurde 2024-2026 als robuster gezeigt als ein einzelnes Regime-Modell.

**Praktisch für BingXBot:** Eine schlanke HMM-Komponente auf BTC-Returns (oder TOTAL-Marketcap) als zusätzlicher **Multiplikator** auf `PositionScalingFactor` in der `RiskManager`-Pipeline. Bei "High-Vol-Bear" → 0.5×, bei "Trend" → 1.0×, bei "Squeeze/Range" → 0× (SK pausieren, weil SK in Range-Märkten leidet). Implementierungsaufwand überschaubar.

Quelle: [Preprints.org HMM Crypto 2024-2026](https://www.preprints.org/manuscript/202603.0831), [LuxAlgo HMM Indicator](https://www.luxalgo.com/library/indicator/hidden-markov-model-market-regimes/), [GitHub CryptoMarket Regime Classifier](https://github.com/akash-kumar5/CryptoMarket_Regime_Classifier).

### 3.5 Donchian-Breakout (Turtle) — der einfache Ergänzungs-Algo

- Klassische Turtle-Regeln: Entry auf 20-Tage-Donchian (System 1) oder 55-Tage (System 2), ATR-basiertes Position-Sizing, 2-ATR-Stop, Pyramiding alle 0.5 ATR bis 4 Units, Exit auf gegenläufigen Breakout.
- Moderne Crypto-Anpassungen 2025-2026: EMA-Filter (8/21/50 stacked alignment), RSI-Momentum-Check, ATR-Volatility-Stops. Eliminieren laut FundedTrading-Plus-Backtests 30-40 % der Verlust-Trades in Trend-Transitions.
- Gate Research (2025) berichtet 62.71 % Annual Return auf GT/USDT mit verbesserter Turtle-Variante — bei aller Vorsicht (Survivorship-Bias, Hindsight).

**Warum als zweites Pferd:** Donchian-Breakout ist methodologisch das **Gegenteil** von SK (kein Korrektur-Entry, sondern Breakout-Entry) und ist deshalb in den Win-/Lose-Phasen anders korreliert. Genau das ist der Punkt von Portfolio-Konstruktion.

Quelle: [TOS Indicators Modern Turtle](https://tosindicators.com/research/modern-turtle-trading-strategy-rules-and-backtest), [Gate Research Turtle Rules](https://www.gate.com/learn/articles/gate-research-turtle-trading-rules-classic-system-with-annual-returns-up-to-62-71/10867).

### 3.6 Walk-Forward + Bayesian Optimization (Optuna)

Du hast bereits `BingXBot.Backtest/WalkForwardRunner` im Repo. Was in der aktuellen Praxis funktioniert:

- **Walk-Forward statt Single-Backtest**: rolling window, IS-Optimierung + OOS-Validierung. Standard, du hast es.
- **Bayesian Optimization (Optuna/Hyperopt)** statt Grid-Search: intelligenter Sampling der Parameter-Raum. Faktor 5-10× weniger Backtest-Runs für ähnliche Resultate.
- **Purged + Embargo Cross-Validation** (López de Prado-School): verhindert Label-Leakage durch überlappende Trade-Outcomes.
- **SBuMT/FWER** (Simultaneous Backtesting under Multiple Trading rules, Family-Wise Error Rate): multiple-testing-Korrektur, weil bei N Parameter-Kombinationen ein vermeintlich starker Backtest durch reines Glück entstehen kann.

Quelle: [QuantInsti Walk-Forward](https://blog.quantinsti.com/walk-forward-optimization-introduction/), [ML4Trading Bayesian under Temporal Dependence](https://ml4trading.io/primer/bayesian-hyperparameter-optimization-under-temporal-dependence/), [TonyMa1 walk-forward-backtester](https://github.com/TonyMa1/walk-forward-backtester).

### 3.7 Strategien, die ich für BingXBot **nicht** empfehle (mit Begründung)

- **Reine Grid-Trading-Bots**: liefern in der Literatur 15-30 % auf Range-Pairs, aber bei trendenden Markets böse Drawdowns durch falsch-richtiges Akkumulieren. Mit deinem Single-Strategy-Architektur-Pattern wenig sinnvoll.
- **Funding-Rate-Arbitrage als Hauptstrategie**: ist market-neutral, braucht aber dauerhafte Spot+Perp-Positionen → konfligiert mit deiner Margin-Cap-Logik (`MaxTotalMarginPercent`) und braucht Cross-Exchange-Setup. Schon als **Bonus-Score in SK** drin, das reicht.
- **LSTM-Pure-Predictor-Strategien**: wurden 2024 mehrfach gezeigt als instabil out-of-sample auf Crypto. Wenn LSTM, dann nur als Feature in einem Ensemble.
- **HFT/Market-Making auf BingX-Perps**: BingX hat keine Maker-Rebates, das Rennen gegen co-located Market-Maker ist verloren bevor es beginnt. Nicht deine Latenz-Klasse.

---

## 4. Roadmap — was du in welcher Reihenfolge angehen solltest

### Phase A — Quick Wins (1-3 Tage, **kein** neuer Code, nur Config + Default-Änderung)

Ziel: 30-50 % weniger Setups, deutlich bessere Setup-Qualität. Keine Architektur-Änderung.

| Schritt | Datei | Aktion |
|---|---|---|
| A1 | `RiskSettings.cs:124` | `RequireWickRejectionInBZone` Default `false → true` |
| A2 | `RiskSettings.cs:135` | `RequireBoxCloseOnEntry` Default `false → true` |
| A3 | `RiskSettings.cs:162` | `MinConfluenceScore` Default `0 → 5` (bei MaxScore=10 — moderater Mid-Filter) |
| A4 | `ScannerSettings.cs:31` | `ImpulseAtrMultiplier` Default `2.0 → 3.0` (Buch-Hard) |
| A5 | `ScannerSettings.cs:41` | `RequireBosVolumeBreakout` Default `false → true` (mit `BosVolumeMultiplier=1.5`) |
| A6 | `ScannerSettings.cs:113` | `RequireBosCloseBreak` Default `false → true` (Body-Close) |
| A7 | `ScannerSettings.cs:122` | `BlockLtfEntryWhenHtfInTargetZone` Default `false → true` |
| A8 | `SequenzKonzeptStrategy.cs:431-432` | Fib `0.118m` / `0.236m` prüfen und entweder durch `0.5m` / `0.618m` ersetzen (vermutlich gemeint) oder die BCKL-Dynamic-Zone komplett auf den `SequenceDetector.CalculateBCKL`-Pfad zurückführen, der die korrekte Fib-Tabelle nutzt |

**Wichtig:** Diese Änderungen **brechen Tests** (das ist im April-Memo explizit dokumentiert: "Alle Hard-Filter sind bewusst opt-in, damit die 434 bestehenden Tests grün bleiben"). Das ist OK — der Aufwand ist, die betroffenen Tests auf die neuen Defaults anzupassen (entweder Testdaten anpassen oder explizit per-Test die alten Defaults setzen). Plane einen halben Tag dafür.

**Validierung:** `WalkForwardRunner` auf 6 Monate historische BingX-Klines für 3 repräsentative Symbole (BTC, ETH, SOL) und 2 Timeframes (H4, H1) **vor und nach** den Default-Änderungen laufen lassen. Vergleich: Trade-Count, Win-Rate, Sharpe, Max-DD, Profit-Factor. Wenn das mit den Buch-Hard-Defaults nicht klar besser aussieht — komm zurück, dann ist die Hypothese widerlegt und wir müssen tiefer graben.

### Phase B — Datenbasiertes Tuning (1-2 Wochen, bestehende Infra)

Ziel: Datenbasiert die richtigen Parameter pro Timeframe/Kategorie finden, nicht aus dem Bauch heraus.

| Schritt | Was | Wo |
|---|---|---|
| B1 | Pi-DB-Snapshot wöchentlich nach `F:\Meine_Apps_Ava\Releases\BingXBot\snapshots\` (Skript, das via `scp` zieht) | neuer Skript, z. B. `tools/PullPiSnapshot/` |
| B2 | Aus `EvaluationDecisions` + `Trades` einen Feature-Frame bauen (Pandas oder C#-LinqToDB): pro Setup-Versuch alle Kontextdaten + Outcome (Trade-Win, Trade-Loss, Reject-Reason) | neuer Konsolen-Output `tools/SkAnalytics/` |
| B3 | Decision-Trail-Analyse: Verteilung der Reject-Reasons pro TF/Symbol/Stunde — wo blockt der Bot, wo lässt er durch? (Heatmap als HTML-Output) | gleicher Tool-Ordner |
| B4 | Walk-Forward + Bayesian-Optimierung auf `MinConfluenceScore`, `ImpulseAtrMultiplier`, `BosVolumeMultiplier`, `PivotLeftBars/RightBars` **pro Timeframe** — Optuna/Hyperopt-Wrapper um `WalkForwardRunner` | Erweiterung von `BingXBot.Backtest/WalkForwardRunner` |
| B5 | Ergebnisse als Per-TF-Setting-Map zurück in Code (du hast die Strukturen schon: pro TF eigene Pivot-Defaults, ATR-Multiplier-Maps wurden im Mai 2026 entfernt — wieder einführen, aber datenbasiert) | `ScannerSettings.cs` Erweiterung |

**Aufwand:** Wochenende für B1+B3, eine Woche fokussierte Arbeit für B2+B4+B5.

### Phase C — ML-Filter-Layer (2-4 Wochen)

Ziel: Schlechte Setups vor dem Trade rausfiltern. Größter erwarteter Win-Rate-Hebel.

| Schritt | Was | Wo |
|---|---|---|
| C1 | Python-Trainings-Pipeline mit LightGBM (oder XGBoost), Features aus dem Feature-Frame aus Phase B | neuer Ordner `tools/SkMlFilter/` (Python + venv) |
| C2 | Training-Target: P(Win bei RRR ≥ 1.5) — Binary Classifier oder Regressor auf erwartetem PnL pro Trade | Python-Notebook |
| C3 | Modell-Export als ONNX, in C# via ML.NET-Bridge oder Microsoft.ML.OnnxRuntime laden | Neuer Service `BingXBot.Engine/ML/SkSignalFilter.cs` |
| C4 | `IStrategy.Evaluate`-Post-Hook: nach Confluence-Score-Pass den ML-Score berechnen, bei P < threshold blocken mit Reject-Reason `ml_filter_low_confidence` | `SequenzKonzeptStrategy.cs` Erweiterung, neue Reject-Reason |
| C5 | A/B-Test: zwei TradingServices parallel, einer mit ML-Filter, einer ohne, gleiche Settings, 4 Wochen Paper-Trading. Vergleich von Win-Rate, Trade-Count, Sharpe | Neue Infra im Paper-Modus, nutzt deine bestehende SimulatedExchange |
| C6 | Nightly Retraining (HostedService auf dem Pi, schreibt frisches `model.onnx`) | `BingXBot.Server/HostedServices/MlModelRetrainService.cs` |

**Vorbereitend nötig:** Mindestens 1000-2000 Live- oder Paper-Trades mit Decision-Trail (Win + Loss + Reject) für ein erstes sinnvolles Modell. Wenn das noch nicht da ist: erst Phase A + 4-8 Wochen Paper-Trading-Akkumulation.

### Phase D — Regime-Detection als zweiter Hebel (3-6 Wochen, kann parallel zu C)

Ziel: SK in Range-Märkten dämpfen oder pausieren — adressiert die typische Schwachstelle von Trend-Folge-Systemen.

| Schritt | Was | Wo |
|---|---|---|
| D1 | HMM auf BTC-Daily-Returns + Volatility + Volume — 3-4 Regime (Bull-Trend, Bear-Trend, Range, Squeeze) | neuer Service `BingXBot.Engine/Regime/HmmRegimeDetector.cs`, Training in Python einmalig, Inferenz in C# via einfacher Forward-Probabilities-Logik (kein Library-Lock-in nötig) |
| D2 | Pro Regime einen `PositionScalingFactor`-Multiplikator: Bull-Trend 1.0×, Bear-Trend 0.7×, Range 0.5×, Squeeze 0× (Pause) | `RiskManager.GetPositionScalingFactor` Erweiterung |
| D3 | Decision-Trail-Tracking: Regime pro Decision loggen, später analysieren | `EvaluationDecision`-Schema-Erweiterung (Migration v13) |
| D4 | Auf-Pause-Schalter für UI: Regime-Status im Dashboard sichtbar, manueller Override möglich | `DashboardView` neuer Badge |

### Phase E — Strategie-Portfolio (6-12 Wochen, größere Investition)

Ziel: SK ergänzen durch eine zweite, strukturell unkorrelierte Strategie. Ergebnis: stabilere Equity-Kurve auch in SK-feindlichen Märkten.

| Schritt | Was | Wo |
|---|---|---|
| E1 | Donchian-Breakout-Strategy parallel zur SK-Strategy: 20/55-Day Donchian, ATR-Sizing, EMA-Trend-Filter (8/21/50 stacked), RSI-Confirmation | neue Klasse `BingXBot.Engine/Strategies/DonchianBreakoutStrategy.cs`, implementiert `IStrategy` |
| E2 | `IStrategyCatalog` erweitern: Multi-Strategy-Mode, MainViewModel kann mehrere `IStrategy`-Instanzen parallel laufen lassen, jede eigenes `(symbol, side)`-Dedup | architektonische Erweiterung |
| E3 | Margin-Cap pro Strategie (z. B. SK 60 %, Donchian 30 %, Buffer 10 %) | `RiskSettings` neue Map |
| E4 | Strategy-spezifische Stats im Dashboard (Equity pro Strategie, Win-Rate pro Strategie) | `TradeStatsAggregator` Erweiterung |
| E5 | Optional Phase E2: Mean-Reversion-Strategy (Bollinger-Z-Score-Reversal) als drittes Pferd, wenn die ersten beiden stabil laufen | später |

**Disclaimer Phase E:** Das ist eine Architektur-Investition. Es lohnt sich erst, wenn die SK-Performance auf dem Niveau ist, das du erwartest (= Phase A-C abgeschlossen). Vorher ist es Vorwärts-Optimieren bei schlechter Basis.

### Phase F — Optional, ambitioniert (3-6 Monate)

- RL-Agent (PPO/A2C, Stable-Baselines3) als Meta-Layer für Position-Sizing oder Strategy-Allocation über Phase E. Forschungsmaterial reichlich vorhanden, aber Risk-Profile gehört sehr genau überlegt. Mein Bauchgefühl: **erst nach E**, wenn überhaupt.
- News-Sentiment-Features (LLM-basiert oder klassische Embeddings) als zusätzliche Inputs in den ML-Filter aus Phase C.

---

## 5. Konkrete nächste Schritte für dich

In der Reihenfolge, in der ich sie machen würde:

1. **Heute oder morgen, 30 min:** Pi-DB-Snapshot ziehen (Anhang A), nach `F:\Meine_Apps_Ava\bingxbot_snapshot.db` legen. Damit kann ich in einer Folge-Session datenbasiert die Reject-Reason-Verteilung und tatsächliche Win-Rate pro TF/Symbol/Confluence-Score analysieren. Aktuelle Aussagen über Setup-Qualität sind ohne diese Daten Modell-Vermutungen.
2. **Dieses Wochenende, halber Tag:** Phase A1-A7 als Branch `feature/buch-hard-defaults`. Tests anpassen. **NICHT direkt mergen**, sondern auf dem Branch mit `WalkForwardRunner` (6 Monate Live-Daten) gegen `main` benchmarken. Wenn klar besser → Merge. Wenn nicht → Aussage des Reports falsifiziert, dann tiefer graben.
3. **Nächste 1-2 Wochen:** Phase B1+B2+B3 (Pi-Pull-Skript + Decision-Trail-Analyse-Tool). Das ist die Daten-Grundlage für **alles andere**. Ohne diese Pipeline sind die folgenden Phasen blinder Code.
4. **Danach Entscheidung:** Phase C (ML-Filter) oder Phase D (Regime-Detection)? Empfehlung: **Phase D zuerst**, weil sie deutlich einfacher ist und das größere "billige" Risiko adressiert (Trades in falschen Regimen). Phase C ist langfristig der größere Hebel, braucht aber sauberere Daten und mehr Aufwand.

---

## 6. Was ich von dir brauche

Konkrete Punkte, an denen ich datenbasiert mehr Tiefe liefern kann:

- **Pi-DB-Snapshot** (Anhang A): Damit ich die tatsächliche Reject-Reason-Verteilung, die Win-Rate pro TF/Symbol/Confluence-Score und die typischen Setup-Outcomes pro Marktklasse berechnen kann. Erst dann lassen sich die Phase-A-Defaults wirklich datenbasiert wählen, nicht nur konsequent aus dem Buch.
- **Welcher Pfad als nächster Schritt:** Phase A (sicher), B (Datenbasis), oder C (ML)? Wenn unklar — siehe meine Empfehlung in Abschnitt 5.

---

## Anhang A — Pi-DB-Snapshot ziehen

Auf deinem Windows-Rechner in Git-Bash (oder per WSL), du brauchst `scp` (kommt mit Git for Windows):

```bash
# Snapshot vom Pi (atomic copy, weil sqlite WAL nicht direkt kopierbar ist):
ssh steuerung@raspberrypi.local "sqlite3 /var/lib/bingxbot/bot.db \".backup /tmp/bot-snapshot.db\""
scp steuerung@raspberrypi.local:/tmp/bot-snapshot.db F:/Meine_Apps_Ava/bingxbot_snapshot.db
ssh steuerung@raspberrypi.local "rm /tmp/bot-snapshot.db"
```

Wenn der Pfad zur DB anders ist, prüfe via:

```bash
ssh steuerung@raspberrypi.local "ls /var/lib/bingxbot/ && systemctl status bingxbot --no-pager | head -10"
```

Sobald die Snapshot-Datei in `F:\Meine_Apps_Ava\bingxbot_snapshot.db` liegt, sag mir Bescheid — dann werte ich aus.

---

## Anhang B — Quellenliste (Recherche-Stand 17.05.2026)

### SK-System-Quellen
- [Stefan Kassing YouTube](https://www.youtube.com/@SKTradingSystem)
- [Tradebook SK-System (Linktree-PDF)](https://ugc.production.linktr.ee/CGSqVQDSqCcRerqAZ4OA_Tradebook%20SK-System.pdf)
- [SK System ausführlich v1.02 (Scribd)](https://www.scribd.com/document/764315195/SK-System-ausfu-hrlich-v1-02)
- [Goose Tradingnetworks — Stefan Kassing Tradingsystem](https://goose-tradingnetwork.jimdofree.com/das-stefan-kassing-tradingsystem/)
- [Tradebook of SK System (Scribd)](https://www.scribd.com/document/741553862/Tradebook-of-SK-System)

### Strategy-Vergleich Trend / Mean-Reversion
- [Stratzy — Trend Following vs Mean Reversion (2025/2026)](https://stratzy.in/blog/untitled-10/)
- [Stratzy — Which Markets and Timeframes](https://stratzy.in/blog/untitled-12/)
- [Qoppac Mar 2025 — Slow Mean Reversion](https://qoppac.blogspot.com/2025/03/very-slow-mean-reversion-and-some.html)
- [SetupAlpha Medium 2026 — Trend vs MR Winner](https://medium.com/@setupalpha.capital/trend-following-vs-mean-reversion-which-strategy-wins-in-2026-9513565b73f7)
- [SSRN Padysak/Vojtko — Seasonality, Trend, MR Bitcoin](https://papers.ssrn.com/sol3/Delivery.cfm/SSRN_ID4081000_code3056836.pdf?abstractid=4081000&mirid=1)

### ML/Gradient Boosting für Trading
- [MQL5 — LightGBM/XGBoost in Trading](https://www.mql5.com/en/articles/14926)
- [Stefan Jansen — ML4Trading Gradient Boosting](https://stefan-jansen.github.io/machine-learning-for-trading/12_gradient_boosting_machines/)
- [Bohrium 2026 — XGBoost vs LightGBM](https://www.bohrium.com/en/blog/tutorials/xgboost-vs-lightgbm/)
- [LinkedIn Oscar Cruz — LightGBM Algo](https://www.linkedin.com/posts/oscar-cruz_trading-isnt-magic-its-applied-statistics-activity-7397839272302637056--aVW)

### Reinforcement Learning Crypto Futures
- [MDPI — Cryptocurrency Futures Portfolio Trading System Using RL (2025)](https://www.mdpi.com/2076-3417/15/17/9400)
- [SSRN — Risk-Aware Deep RL for Crypto Trading 2025](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=5662930)
- [arXiv — Bitcoin/Ripple DRL comparative study](https://arxiv.org/html/2505.07660v2)
- [Springer Neural Computing — DRL + Technical Analysis](https://link.springer.com/article/10.1007/s00521-023-08516-x)
- [POSTECH — A2C RL for Cryptocurrency Trading](http://dpnm.postech.ac.kr/papers/ICBC/2024/A2C%20Reinforcement%20Learning%20for%20Cryptocurrency%20Trading%20and%20Asset%20Management.pdf)

### Regime-Detection (HMM)
- [Preprints.org — HMM Crypto Regime Detection 2024-2026](https://www.preprints.org/manuscript/202603.0831)
- [QuantInsti — Regime-Adaptive Trading Python](https://blog.quantinsti.com/regime-adaptive-trading-python/)
- [LuxAlgo — HMM Market Regimes Indicator](https://www.luxalgo.com/library/indicator/hidden-markov-model-market-regimes/)
- [GitHub akash-kumar5 — CryptoMarket Regime Classifier](https://github.com/akash-kumar5/CryptoMarket_Regime_Classifier)
- [MDPI — Bitcoin Price Regime Shifts Bayesian MCMC + HMM](https://www.mdpi.com/2227-7390/13/10/1577)

### Walk-Forward & Bayesian Optimization
- [QuantInsti — Walk-Forward Optimization](https://blog.quantinsti.com/walk-forward-optimization-introduction/)
- [ML4Trading — Bayesian Hyperparameter under Temporal Dependence](https://ml4trading.io/primer/bayesian-hyperparameter-optimization-under-temporal-dependence/)
- [GitHub TonyMa1 — walk-forward-backtester (Optuna)](https://github.com/TonyMa1/walk-forward-backtester)
- [arXiv 2512.12924 — Interpretable Hypothesis-Driven Trading WFV](https://arxiv.org/html/2512.12924v1)

### Donchian / Turtle
- [TOS Indicators — Modern Turtle Trading Backtest](https://tosindicators.com/research/modern-turtle-trading-strategy-rules-and-backtest)
- [Gate Research — Turtle Trading 62.71% Annual Return](https://www.gate.com/learn/articles/gate-research-turtle-trading-rules-classic-system-with-annual-returns-up-to-62-71/10867)
- [Altrady — Turtle Trading Rules](https://www.altrady.com/blog/crypto-trading-strategies/turtle-trading-strategy-rules)
- [FundedTradingPlus — Turtle Donchian Trend Filter](https://www.fundedtradingplus.com/propiq/turtle-trading-strategy-the-classic-breakout-system-made-simple-donchian-channels-trend-filter/)

### Crypto Perpetual Futures Strategies Overview
- [CoinGape — 6 Crypto Perp Futures Strategies](https://coingape.com/blog/crypto-perpetual-futures-trading-strategies/)
- [Nurp — Top 10 Crypto Algo Strategies 2026](https://nurp.com/wisdom/top-10-strategies-to-optimize-crypto-trading/)
- [WunderTrading — Automated Crypto Strategies](https://wundertrading.com/journal/en/learn/article/how-to-automate-your-crypto-trading-strategy)
- [HyroTrader — What Actually Works in 2026](https://www.hyrotrader.com/blog/automated-crypto-trading/)
- [Blockchain-Council — Backtesting AI Crypto Strategies Safely](https://www.blockchain-council.org/cryptocurrency/backtesting-ai-crypto-trading-strategies-avoiding-overfitting-lookahead-bias-data-leakage/)

### Lokale Audit-Quellen (internes Memory)
- `F:\Meine_Apps_Ava\.claude\agent-memory\bingxbot\MEMORY.md`
- `F:\Meine_Apps_Ava\.claude\agent-memory\bingxbot\sk_spec_compliance_2026_04_21.md`
- `F:\Meine_Apps_Ava\.claude\agent-memory\bingxbot\sk_0ab_detection_2026_04_22.md`
- `F:\Meine_Apps_Ava\src\Apps\BingXBot\CLAUDE.md`
- `F:\Meine_Apps_Ava\src\Libraries\BingXBot.Engine\Strategies\SequenzKonzeptStrategy.cs`
- `F:\Meine_Apps_Ava\src\Libraries\BingXBot.Core\Configuration\ScannerSettings.cs`
- `F:\Meine_Apps_Ava\src\Libraries\BingXBot.Core\Configuration\RiskSettings.cs`
