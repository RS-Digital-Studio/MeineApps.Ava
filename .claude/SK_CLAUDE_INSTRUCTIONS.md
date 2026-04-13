# CLAUDE.md — SK-System Trading Bot

## Projekt

SK-System (Stefan Kassing) auf BingX Perpetual Futures, Isolated Margin. C# / .NET, Avalonia UI.
Handelt: **Krypto, Forex, Rohstoffe (Gold/Silber/Öl), Indices (DAX/Nasdaq), Aktien.**

```
F:\Meine_Apps_Ava\src\Apps\BingXBot\                     ← App (UI, Services)
F:\Meine_Apps_Ava\src\Libraries\BingXBot.Core\            ← Models, Config, Interfaces, Enums
F:\Meine_Apps_Ava\src\Libraries\BingXBot.Engine\           ← Strategies, Indicators, Scanner, Risk
F:\Meine_Apps_Ava\src\Libraries\BingXBot.Exchange\         ← BingX REST/WebSocket
```

---

# TEIL 1: Das SK-System (strikt nach Tradebook)

Quelle: Tradebook SK-System, Sascha Wenzel (mit Vorwort Stefan Kassing), Cheat v1.03, Workflow v1.05

## 1.1 Grundprinzip (S.15)

Das SK-System ist ein reproduzierbares System, das auf dem Erkennen bestimmter Marktbewegungen (Price Action) basiert und die Fibonacci-Folge nutzt, um relevante Preis-Bereiche zu definieren. Es besitzt eine überdurchschnittlich hohe Trefferquote.

Grundprinzip: **BLASH — Buy Low And Sell High** (S.20)

## 1.2 Chart-Analyse-Hierarchie (S.15)

Der Markt wird **von oben nach unten** analysiert:

1. **Übergeordnet** (Monats-/Wochen-Chart bis H1): Richtung und Fahrplan bestimmen
2. **Untergeordnet** (M30): Setup finden und Trade platzieren

> "Wenn die Erkenntnisse aus der übergeordneten Analyse für unser Tradingsystem weiterhin interessant sind, dann gehen wir in den untergeordneten Chart und analysieren diesen."

**Cheat Node 6:** Übergeordnete Marktanalyse = Weekly bis H1
**Cheat Node 7:** Wechsel in M30

**Bot-Mapping:** Weekly → Daily → H4 → H1 (übergeordnet) → M30 (untergeordnet/Trigger)

## 1.3 Sequenz — 0, A, B, C (S.15-16)

Eine Sequenz ist eine trendbasierte Bewegung im Markt. Wir fokussieren uns auf eine **3er Sequenz** mit 4 Punkten: 0, A, B, C.

**Zwei Arten:**
- **Erfolgreich abgearbeitete Sequenz (valide)**: Ziellevel wurde erreicht
- **Nicht erfolgreich abgearbeitete Sequenz (invalide)**: Ziellevel wurde nicht erreicht

**Die 4 Punkte:**
- **Punkt 0**: Start der Bewegung (absolutes High oder Low) (Workflow 3)
- **Punkt A**: Ende des initialen Impulses — "Punkt A ist erst dann A wenn der Markt die Bewegung 0-A anfängt zu korrigieren" (Workflow 4)
- **Punkt B**: Korrektur im Fibonacci-Bereich 50-66.7% (Entry-Zone)
- **Punkt C**: Ziellevel bei 161.8-200% Extension

**Sequenzfindung (Workflow S.35, Anmerkungen):**
1. Eine Sequenz kann grundsätzlich nur am Anfang bzw. Ende einer Bewegung angelegt werden
2. Eine Sequenz darf nicht mittig einer Bewegung angelegt werden, es sei denn sie resultiert aus einer bereits aktiven größeren Sequenz
3. Ein Punkt 0 stellt meistens den Start der Bewegung dar, oftmals als absolutes High oder Low
4. Punkt A ist erst dann A wenn der Markt die Bewegung 0-A anfängt zu korrigieren und zu unseren Entrylevel läuft
5. Sequenz ist beim Über- bzw. Unterschreiten von A einfach aktiviert, so dass die Bullen/Bären sich weiterhin durchgesetzt haben und die Wahrscheinlichkeit höher ist, dass die Sequenz ins Ziellevel kommt

## 1.4 Korrekturlevel / Entry-Zone (S.16, Cheat Node 50)

Das Korrekturlevel (KL) liegt am Punkt B im Fibonacci-Bereich:

| Level | Fibonacci |
|-------|-----------|
| 50.0% | 0.500 |
| 55.9% | 0.559 |
| 61.8% | 0.618 |
| 66.7% | 0.667 |

Das Antragen des KL erfolgt mit dem **Fib-Retracement** vom Punkt 0→A.
Dieser Bereich ist die **Entry-Zone** für Trades.

**Wichtig (S.16):** Wenn der Markt das 0.382 Level NICHT überschreitet → keine Trades, keine Sequenz (Workflow Node 8/9).

## 1.5 Ziellevel / Take Profit (S.16)

Das Ziellevel liegt im Fibonacci-Bereich **161.8 – 200%** Extension.
Der TP wird mit den **trendbasierten Extensionsleveln** definiert (Extension wird von 0-A-B angelegt).

**Zielmöglichkeiten (Cheat Node 38-40, Workflow 3):**
1. CRV (min. 1:1)
2. 100% Korrektur (Bullen-/Bärenkorrekturlevel)
3. 100% Extension
4. 161.8-200% Extension (Hauptzielbereich)
5. Dailyrange

**Single Trade TP (Workflow 4.5):**
> "SL bei Single Trade beim C-Zielbereich: 20 Pips über dem 200er Extensionslevel!"
→ Exit/TP wird 20 Pips über dem 200er Extensionslevel gesetzt.

## 1.6 100er Extension als Richtungsweiser (S.16-17)

Die 100er Extension zeigt an, ob die Sequenz funktioniert oder nicht:

- **Übersteigt**: Wahrscheinlichkeit groß, dass der Markt das Ziellevel erreicht
- **Abprallt**: Retracement der B-C Welle bilden → neues Korrekturlevel (BCKL) → Re-Entry möglich (S.17)

**Workflow 6.6:** "Korrektur der BC Bewegung stellt IMMER einen Reentry dar (bei aktivierter Sequenz)"

## 1.7 Retracement (S.16)

Das "Zurückziehen" des Marktes nach einer Bewegung. Der Markt korrigiert seine vorherige Bewegung.

## 1.8 Gap (S.17)

Die Lücke zwischen den Kursen wenn der Markt schließt und wieder öffnet. Entsteht durch globale Zeitverschiebungen. Relevant für Forex (Mo-Fr), Aktien, Indices — NICHT für Krypto (24/7).

## 1.9 Stop Loss (S.12-13, Cheat Node 36/37/49, Workflow 4)

**A) SL-Position (Cheat Node 36):**
> "SL knapp oberhalb/unterhalb 78.6er"

Der SL liegt am 78.6% Retracement der 0→A Range.

**B) SL-Größe — unterschiedlich nach Trade-Strategie (Cheat Node 35/37/49):**

| Strategie | Markt | SL |
|-----------|-------|-----|
| **1 Trade Strategie** (Node 35→37) | Standard (Hauptwährungen) | **10-15 Pips** |
| **Multiple Trade Strategie** (Node 51→49) | Standard (Hauptwährungen) | **20 Pips** |
| Beide | Indices (DAX) | 40 Punkte (Node 42) |
| Beide | Öl | 40 Pips (Node 44) |
| Beide | Kryptowährungen (BTC) | 100 Pips (Node 45) |

**Seite 13 nennt die allgemeinen SL-Maxima:**
- Hauptwährungen und Metalle: 20 Pips
- Indices und Öl: 40 Pips
- Kryptowährungen: 100 Pips

**C) SL-Grenze (Workflow 6.9):**
> "Aus sequenz-technischer Sicht darf der Markt bis zum Punkt 0 (sofern B-C Bewegung dominant, impulsiv) aus Entrysicht zählt nur das 50-66.7er KL"

Punkt 0 ist die absolute Grenze. Darüber hinaus = Sequenz invalid.

**Zusammen:** SL am 78.6er platzieren → gecappt bei marktspezifischen Pips → nie über Punkt 0.

**Zusätzliche SL-Regeln (Workflow 4):**
- 4.1: Risiko reduzieren = SL halbieren / max 5 Pips SL
- 4.4: Spread wird NICHT zum SL addiert (aber zum Entry, s. 1.17)

## 1.10 Breakeven (Cheat Node 53, Workflow 4.2-4.3, S.18)

**Cheat Node 53:** "auf BE ziehen spätestens wenn Markt Bären/Bullenkorrekturlevel erreicht"

**Workflow 4.2:** "Auf BE ziehen wenn der Trade in einem Bull/Bären Korrekturlevel vor dem doppelten Profit liegt"

**Workflow 4.3:** "SL wird NICHT nachgezogen (außer auf BE) — Trade läuft dann BE oder ins Ziel"

**S.18 Spread bei BE:** Bei BE den Spread einberechnen — SL nicht pipgenau auf Entry, sondern Entry + Spread setzen. Sonst wird man mit der Gebühr (Spread) ausgestoppt.

→ **Zusammenfassung:** BE wird EINMAL gesetzt (spätestens bei entgegengesetztem KL / bei doppeltem Profit), danach KEIN Trailing. Trade läuft bis zum TP oder wird auf BE ausgestoppt.

## 1.11 Entry-Regeln (Cheat Node 9/34/35/50/51, Workflow 5)

**Bestätigungen (Cheat Node 9):** Mindestens 3, schöner 4 Bestätigungsgruppen bevor Entry.

**Price-Action-Behaviour prüfen (Cheat Node 8)** → mind. 3-4 Bestätigungen (Node 9) → dann:
- Correction High (Node 10): Lower High + Lower Low → Short (Node 11→12→13)
- Correction Low (Node 14): Higher Low + Higher High → Long (Node 15→16→17)

### 1 Trade Strategie (Cheat Node 35)
- Ein Entry am besten verfügbaren Fib-Level im KL
- SL: 10-15 Pips Standard (Node 37), am 78.6er (Node 36)
- TP: 200er Extension + 20 Pips Buffer (Workflow 4.5)

### Multiple Trade Strategie (Cheat Node 50/51)
- 4 Limit-Orders an den Fib-Leveln: 50% / 55.9% / 61.8% / 66.7%
- SL: 20 Pips (Node 49)
- **Jeder Entry mit eigenem SL** — SL darf nächsten Entry nicht überdecken (Workflow 5.4)
- Der letzte Entry (66.7%) hat den SL am 78.6er

**Workflow 5.4:** "Wenn SL vom 50er Entry alle Bereiche (50-66.7) überdeckt → 50er Entry SL verkleinern um am 66.7er einen validen Entry platzieren zu können."

**Weitere Entry-Regeln (Workflow 5):**
- 5.1: Spread bei Entry berücksichtigen
- 5.2: Die Einstiegsbereiche sollten sich nicht mit dem SL des vorherigen Levels überschneiden
- 5.3: Der Entry wird solange getradet wie er valide ist

## 1.12 Valide / Invalide (S.22)

Gütekriterium für Sequenzen und Trades. Dabei können unterschiedliche Kombinationen auftreten — **ein Trade kann invalide sein, während die Sequenz noch valide bleibt.**

- **Trade invalid:** Wenn man im KL einsteigt und dieses deutlich überschritten wird (Workflow Node 13/14, 23/24)
- **Sequenz valide:** Solange der Preis vor Punkt 0 bleibt (Workflow Node 17)
- **Sequenz invalid:** Erst nach dem Überschreiten von Punkt 0 (Workflow Node 15→16)
- **Sequenz invalid:** Preis überschreitet das 200% Level im Zielbereich — Punkt C (S.22, Workflow Node 33)

**Workflow Node 34:** "Trades invalid, Sequenz valid" — wenn die 2.000 überschritten wird aber die Gesamtsequenz noch intakt ist.

## 1.13 Pull Back (S.22)

Ein Pullback ist KEINE klassische Korrektur. Es ist eine schnelle, impulsive Bewegung über ein sehr kurzes Zeitintervall, während eine Korrektur eher träge und über ein längeres Zeitintervall agiert. Im 4H-Chart ist eine Kerze ein Pullback, im 15-Minuten-Chart bilden ca. 40 Kerzen (= 10h) eine Korrektur.

## 1.14 Trademanagement — Workflow-Regeln (S.35)

**Risiko (Workflow 1):**

| Regel | Beschreibung |
|-------|-------------|
| 1.1 | Maximal 1-3% pro Trade riskieren — bei kleinem Konto nicht 1-3% pro Trade, sondern dividieren! |

**StopLoss (Workflow 4):**

| Regel | Beschreibung |
|-------|-------------|
| 4.1 | Risiko reduzieren = SL halbieren / max 5 Pips SL |
| 4.2 | Auf BE ziehen wenn der Trade in einem Bull/Bären Korrekturlevel vor dem doppelten Profit liegt |
| 4.3 | SL wird NICHT nachgezogen (außer auf BE) — Trade läuft dann BE oder ins Ziel |
| 4.4 | Spread wird NICHT berücksichtigt (= nicht zum SL addiert) |
| 4.5 | SL bei Single Trade beim C-Zielbereich: 20 Pips über dem 200er Extensionslevel |

**Entry (Workflow 5):**

| Regel | Beschreibung |
|-------|-------------|
| 5.1 | Spread berücksichtigen |
| 5.2 | Einstiegsbereiche sollen sich nicht mit dem SL des vorherigen Levels überschneiden |
| 5.3 | Entry wird solange getradet wie er valide ist |
| 5.4 | Wenn SL vom 50er Entry alle Bereiche (50-66.7) überdeckt → 50er SL verkleinern um am 66.7er validen Entry platzieren zu können |

**Allgemeines (Workflow 6):**

| Regel | Beschreibung |
|-------|-------------|
| 6.1 | Wenn x Trades in SL und Möglichkeit besteht mit einem Trade die Verluste auszugleichen → TP! |
| 6.2 | Gewinne sollten am gleichen Tag realisiert werden |
| 6.3 | Mit der Sequenz handeln ist weniger riskant als gegen die Sequenz |
| 6.4 | Die C Welle darf theoretisch unendlich lang gehen |
| 6.5 | Zielbereich gilt als abgearbeitet bei 5 Pips Toleranz vor dem 161.8er |
| 6.6 | Korrektur der BC Bewegung stellt IMMER einen Reentry dar (bei aktivierter Sequenz) |
| 6.7 | Reaktion am 200er Ziellevel mit gefolgter Abarbeitung des Korrekturlevels (0)→(C): Ziellevel gibt keine Entrys mehr |
| 6.8 | Wird Trade nach BE Setzung ausgestoppt → gleich wieder einsteigen |
| 6.9 | Aus sequenz-technischer Sicht darf der Markt bis zum Punkt 0 (sofern B-C Bewegung dominant, impulsiv) — aus Entrysicht zählt nur das 50-66.7er KL |

**Diversifikation (Workflow 2):**

| Regel | Beschreibung |
|-------|-------------|
| 2.1 | Alle Märkte traden, wenn sie einen validen Entry geben |
| 2.2 | Idealerweise ergeben sich Hedge-Situationen |

## 1.15 Risikomanagement (S.13-14)

- **1-3% der Kontoeinlage pro Tag** — bei mehreren Trades aufteilen (S.13)
- **CRV mindestens 1:1** — je höher desto besser (S.13)
- **Lotsize** = Geldwert pro Trade / (Pips zwischen Entry und SL × Pipwert) (S.14)
- Niemals traden ohne einen Stop Loss gesetzt zu haben (S.12)

## 1.16 Price Action (S.20)

Die Fähigkeit, Marktbewegungen zu verstehen. Marktbewegungen basieren auf dem Zusammenhang zwischen **Angebot und Nachfrage** — zwischen Verkäufern und Käufern.

- Lows werden generiert um Up-Targets (Verkaufsziele) zu erreichen
- Highs werden generiert um Down-Targets (Kaufziele) zu erreichen
- **Correction High** (Cheat Node 10-12): Lower High + Lower Low → Short-Richtung
- **Correction Low** (Cheat Node 14-16): Higher Low + Higher High → Long-Richtung

## 1.17 Spread (S.18)

Die Differenz zwischen dem niedrigsten (Ask) und höchsten (Bid) Preis.

- Spread wird **NICHT zum SL addiert** (Workflow 4.4)
- Spread wird **zum Einstiegspreis addiert** bei Kaufpositionen (S.18: z.B. EURUSD Entry bei 1.11103, Spread 2 Pips → tatsächlicher Einstieg: 1.11123)
- Bei BE: Spread einberechnen — SL auf Entry + Spread, nicht pipgenau auf Entry (S.18)
- Exotische Paare = höherer Spread
- Volatile Zeiten / News = höherer Spread
- Haupthandelszeiten = niedrigster Spread

## 1.18 Liquiditätsgewinnung (S.19)

Orderfüllung an den Lows und Highs einer Seitwärtsbewegung innerhalb des Support & Resistance Bereiches bis zum Break Out. Der Markt generiert Kraft und bricht dann aus. Große Instanzen setzen Liquidität frei und suggerieren den kleineren Marktteilnehmern eine Richtung, um ihre eigenen Positionen gefüllt zu bekommen.

## 1.19 Support and Resistance (S.21)

Wird im Tradebook nur der Vollständigkeit halber aufgeführt und ist im Kontext zu der Liquiditätsgewinnung zu betrachten.

- **Support** (Unterstützung): Unterer Punkt eines Abwärtstrends
- **Resistance** (Widerstand): Oberer Punkt eines Aufwärtstrends

Im SK-System kein eigenständiges Werkzeug — dient als Kontext für Liquiditätsgewinnung.

## 1.20 Risikodiversifikation / Hedging (S.19-20)

- Risiko minimieren durch breite Streuung über viele Märkte (sowohl positiv als auch negativ korrelierend)
- **Hedging** (S.20): Eine Währung geht in einem Markt short und in einem Markt long, wenn es das Setup zulässt

## 1.21 Emotionen (Cheat Node 31)

> "Kein Platz für Emotionen" (S.30)

- Emotionale Aktivitäten haben keine feste Variable
- Ein Gefühl ist abhängig von so vielen äußeren Faktoren, dass diese Aktivitäten immer wieder gleich auszuführen sind
- Emotionale Aktivitäten sind aufgrund dieser Erkenntnis einfach nicht reproduzierbar
- Professionelle Trader zeichnen sich dadurch aus, dass sie ein System haben mit festen Regeln und somit rationale Entscheidungen treffen können

**Cheat Node 30:** "streng eingehalten"
**Cheat Node 32:** "keinen Ermessensspielraum"

## 1.22 Cheat-Flowchart Kurzfassung (S.23, Cheat v1.03)

```
System (1) → 100% Vertrauen (2) → positive Trefferquote (3)
  ↓
Aufbau (4): Price Action (5) + 3er Sequenzen (18)
  ↓
Übergeordnete Marktanalyse Weekly-H1 (6)
  ↓
Wechsel in M30 (7) → Price-Action-Behaviour prüfen (8)
  ↓
Mind. 3, schöner 4 Bestätigungsgruppen (9)
  ↓
Correction High (10→11→12→13=Short)
  ODER
Correction Low (14→15→16→17=Long)
  ↓
Aufwärtssequenz (28) / Abwärtssequenz (29)
  ↓
Kein Platz für Emotionen (31) | streng eingehalten (30) | kein Ermessensspielraum (32)
  ↓
System gibt grünes Licht (33) → Entry mit nächster Korrektur/KV-Bereich (34)
  ↓
SL knapp oberhalb/unterhalb 78.6er (36)
  ↓
1 Trade Strategie (35):          Multiple Trade Strategie (51):
  Standard Stop 10-15 Pips (37)    50/55.9/61.8/66.7 (50)
  DAX 40 Punkte (42)               20 Pips Stop (49)
  Öl 40 Pips (44)
  BTC 100 Pips (45)
  ↓
Zielmöglichkeiten (41):
  Mindestziel 100% Korrekturbewegung (38)
  Bullen-/Bärenkorrekturlevel (39)
  161.8er Extension (40)
  ↓
Trade / Position → Tradeverwaltung (52)
  ↓
auf BE ziehen spätestens wenn Markt Bären/Bullenkorrekturlevel erreicht (53)
  ↓
Treffer (54) / kein Treffer (55) → Fehleranalyse (56)
```

**Pip-Value-Gruppen (Cheat):**
- Paare mit höherem Pip Value: GBP (Node 46)
- Metalle: Gold, Silber (Node 47)
- Exoten: AUD, CAD, NZD (Node 48)

## 1.23 Workflow-Ablauf Kurzfassung (S.34-35, Workflow v1.05)

**Hauptprozess:** Price Action (1) → Sequenzfindung (2) → System (3) → Trademanagement (4)

**Sequenzfindung:**
```
Markt generiert High/Low = Punkt 0 (5)
  ↓
Markt generiert Low/High = Punkt A (6)
  ↓
Markt korrigiert = Punkt B (7)
  ↓
Überschreitet 0.382? → NEIN: Keine Trades (9)
                      → JA (10): Mögliche Einstiege 50/55.9/61.8/66.7 (11)
                                  → Einstieg Trademanagement (12)
                                  → KL deutlich überschritten? → Trades invalid (14)
```

**Nach Entry — Richtung Ziellevel:**
```
Markt bewegt sich zum Ziellevel (18)
  ↓
Reagiert vor Punkt 0? (17) → JA: Sequenz noch valide
Überschreitet Punkt 0? (15) → Sequenz invalid (16)
  ↓
Markt erreicht Ziellevel NICHT (19):
  → Markt korrigiert B→C (20) → neue Einstiege 50-66.7 (21/22)
  → Markt reagiert vor Punkt B (27) → neue Sequenz möglich (28)
  → Markt überschreitet Punkt B (25) → Sequenz invalid (26)
  ↓
Markt ERREICHT Ziellevel (30):
  → Mögliche Einstiege 1.618/1.809/2.000 (31)
  → Markt überschreitet 2.000 (33) → Trades invalid, Sequenz valid (34)
  ↓
Markt korrigiert gesamte Bewegung 0→C (35):
  → Überschreitet 0.382? → NEIN: Keine Trades (37)
                          → JA (39): Einstiege 50-66.7 (40/41)
  → Reagiert vor Punkt 0 (46) → Neue Sequenz (47/48)
  → Überschreitet Punkt 0 (44) → Sequenz invalid (45)
```

**Punkt C darf unendlich extendieren (Node 36/Workflow 6.4)**

---

# TEIL 2: Abweichungen im Bot (Fixes)

## 🔴 FIX 1: Multi-Tier ENTFERNEN

**Tradebook (S.15):** EIN linearer Flow (Übergeordnet Weekly-H1 → Untergeordnet M30)
**Bot:** 3 parallele EvaluateTier-Aufrufe (T1/T2/T3)
**Fix:** Zurück zu EINER Evaluate-Methode. EvaluateTier, TradingTier-Enum, Tier-Settings, M5/M1-Loading → ENTFERNEN.

## 🔴 FIX 2: BE-Regel implementieren

**Tradebook (Cheat 53):** "auf BE ziehen spätestens wenn Markt Bären/Bullenkorrekturlevel erreicht"
**Tradebook (Workflow 4.2):** "Auf BE ziehen wenn der Trade in einem Bull/Bären Korrekturlevel vor dem doppelten Profit liegt"
**Tradebook (Workflow 4.3):** SL wird NICHT nachgezogen (außer auf BE)
**Tradebook (S.18):** BE = Entry + Spread
**Bot:** `DisableSmartBreakeven = true` → Bot zieht NIE auf BE
**Fix:** BE einmal setzen wenn Preis entgegengesetztes KL erreicht UND Trade vor doppeltem Profit steht. Danach NICHT nachziehen. BE = Entry + Spread.

## 🔴 FIX 3: Re-Entry nach BE-Stop

**Tradebook (Workflow 6.8):** "Wird Trade nach BE Setzung ausgestoppt → gleich wieder einsteigen"
**Bot:** `_failedSeqPending` nur bei SL-Hit
**Fix:** Bei BE-Stop (PnL ≈ 0) sofort Re-Entry, kein Cooldown.

## 🔴 FIX 4: Entry an Fib-Leveln statt M30-PointA

**Tradebook (S.16, Cheat 50):** Limit-Orders an 50/55.9/61.8/66.7% der Sequenz
**Bot:** `EntryPrice: m30Machine.PointA` (Micro-Sequenz Breakout)
**Fix:** Entry direkt an den Fibonacci-Leveln. Bei Multiple Trade: Jeder Entry mit eigenem SL VOR dem nächsten Level (Workflow 5.4).

## 🔴 FIX 5: SL nach Tradebook (78.6er + Pip-Cap)

**Tradebook (Cheat 36):** SL am 78.6er
**Tradebook (Cheat 37/49):** 1 Trade = 10-15 Pips Standard, Multiple = 20 Pips Standard
**Tradebook (S.13):** Max 20 Pips (Hauptwährungen), 40 (Indices/Öl), 100 (Krypto)
**Tradebook (Workflow 6.9):** nie über Punkt 0
**Bot:** SL = Point0 ± 1.5×ATR (unter Punkt 0, ohne 78.6er, ohne Pip-Cap)
**Fix:** SL am 78.6% Retracement → gecappt bei Markt-Pips (10-15 für 1 Trade / 20 für Multiple bei Standard-Paaren) → nie über Punkt 0.

## 🔴 FIX 6: RiskSettings-Defaults

**Tradebook (Workflow 1.1):** "Maximal 1-3% pro Trade riskieren — bei kleinem Konto dividieren"
**Tradebook (S.13):** 1-3% der Kontoeinlage pro Tag, aufgeteilt auf Trades
**Bot:** MaxOpenPositions=10, MaxMarginPerTradePercent=10%
**Fix:** MaxOpenPositions=3, MaxMarginPerTradePercent=1%, MaxDailyRiskPercent=2%

## 🔴 FIX 7: TP = 200% + Buffer

**Tradebook (Workflow 4.5):** "20 Pips über dem 200er Extensionslevel"
**Bot:** TP2 = 161.8% Extension + Partial Close 30% bei TP1
**Fix:** TP = 200% Extension + 20 Pips Buffer. KEIN Partial Close (nicht im Tradebook).

## 🟡 FIX 8: Ziellevel-Toleranz

**Tradebook (Workflow 6.5):** "Zielbereich gilt als abgearbeitet bei 5 Pips Toleranz vor dem 161.8er"
**Fix:** In SequenceStateMachine: HasFullyCompleted mit 5 Pips / 0.03% Toleranz.

## 🟡 FIX 9: BCKL als eigenständiger Re-Entry-Trigger

**Tradebook (Workflow 6.6):** "Korrektur der BC Bewegung stellt IMMER einen Reentry dar (bei aktivierter Sequenz)"
**Bot:** BCKL nur als Confluence (+1 Score)
**Fix:** BCKL als eigenständiger Entry-Trigger. SL unter BC-B, TP bei Hauptsequenz-Ziellevel.

## 🟡 FIX 10: Verlust-Ausgleichs-TP

**Tradebook (Workflow 6.1):** "Wenn x Trades in SL und dann Möglichkeit besteht mit einem Trade die Verluste auszugleichen → TP!"
**Fix:** Wenn laufender Trade Tagesverluste kompensieren kann → automatisch TP setzen.

## 🟡 FIX 11: Nach 200er → GKL kein Entry mehr

**Tradebook (Workflow 6.7):** "Reaktion am 200er Ziellevel mit gefolgter Abarbeitung des Korrekturlevels (0)→(C): Ziellevel gibt keine Entrys mehr"
**Fix:** Wenn 200er erreicht UND GKL der 0→C Strecke abgearbeitet → keine weiteren Entries in dieser Sequenz.

---

# TEIL 3: Implementierungs-Reihenfolge

| Prio | Fix | Beschreibung |
|------|-----|-------------|
| 🔴 1 | Multi-Tier ENTFERNEN | Zurück zu einer Evaluate-Methode |
| 🔴 2 | BE-Regel | Bei entgegengesetztem KL + doppeltem Profit, einmal, nicht nachziehen |
| 🔴 3 | Re-Entry nach BE-Stop | Sofort, kein Cooldown (6.8) |
| 🔴 4 | Entry an Fib-Leveln | Limit-Orders an 50/55.9/61.8/66.7 mit gestaffeltem SL (5.4) |
| 🔴 5 | SL nach Tradebook | 78.6er + Pip-Cap (10-15/20 je Strategie) + Punkt-0-Grenze |
| 🔴 6 | RiskSettings | MaxPos=3, Risk=1%, DailyRisk=2% |
| 🔴 7 | TP = 200% + Buffer | Kein Partial Close bei 161.8% |
| 🟡 8 | Ziellevel-Toleranz | 5 Pips vor 161.8er = abgearbeitet |
| 🟡 9 | BCKL Re-Entry | Eigenständiger Trigger, nicht nur Confluence (6.6) |
| 🟡 10 | Verlust-Ausgleichs-TP | Tagesverluste kompensieren (6.1) |
| 🟡 11 | Post-200er GKL-Block | Keine Entries nach 200er + GKL-Abarbeitung (6.7) |

---

# TEIL 4: Externe Daten (Zusatz, nicht im Tradebook)

BinancePublicClient.cs mit 9 Endpoints als Confluence-Score (±1 bis ±3, keine harten Blocks):
Open Interest, Long/Short Ratio, Top-Trader Ratio, Taker Buy/Sell Volume, Funding Rate,
Liquidation Orders, Kline Orderflow, BTC-Dominanz, Fear & Greed Index.

Widerspricht dem Tradebook nicht — zusätzliche Bestätigung (passt zu Cheat Node 9: "mindestens 3, schöner 4 Bestätigungsgruppen").

---

# TEIL 5: Regeln (strikt nach Tradebook)

**VERBOTEN:**
- ❌ Multi-Tier / parallele Strategien (S.15: EIN Flow von übergeordnet nach untergeordnet)
- ❌ Entry am M30-PointA statt an Fib-Leveln (S.16: Entry im KL 50-66.7)
- ❌ Partial Close bei 161.8% (nicht im Tradebook)
- ❌ TP bei 161.8% bei Single Trade (Workflow 4.5: "über dem 200er")
- ❌ SL unter Punkt 0 (Workflow 6.9: Punkt 0 = absolute Grenze)
- ❌ SL ohne 78.6er-Platzierung (Cheat Node 36: "SL am 78.6er")
- ❌ SL ohne Markt-Pip-Cap (S.13, Cheat Node 37/42/44/45/49)
- ❌ DisableSmartBreakeven = true (Cheat 53 + Workflow 4.2: "auf BE ziehen")
- ❌ SL nachziehen über BE hinaus (Workflow 4.3: "nur auf BE")
- ❌ Mehr als 1-3% Risiko pro Trade (Workflow 1.1), bei kleinem Konto dividieren
- ❌ MaxOpenPositions = 10 oder MaxMarginPerTrade = 10%
- ❌ Trading gegen die Sequenz ohne besonderen Grund (Workflow 6.3)
- ❌ API-Keys/Secrets anfassen

**PFLICHT:**
- ✅ EIN Top-Down-Flow: Weekly→Daily→H4→H1 (übergeordnet) → M30 (untergeordnet) (S.15)
- ✅ Entry an Fibonacci-Leveln 50/55.9/61.8/66.7 im KL (S.16, Cheat 50)
- ✅ Bei Multiple Trade: Jeder Entry mit eigenem SL VOR dem nächsten Fib-Level (Workflow 5.4)
- ✅ SL am 78.6er, gecappt bei Markt-Pips, nie über Punkt 0 (Cheat 36, S.13, Workflow 6.9)
- ✅ 1 Trade SL: 10-15 Pips Standard (Cheat 37) | Multiple Trade SL: 20 Pips (Cheat 49)
- ✅ TP = 200% Extension + 20 Pips Buffer (Workflow 4.5)
- ✅ BE bei entgegengesetztem KL / doppeltem Profit, einmal setzen, nicht nachziehen (Cheat 53, Workflow 4.2+4.3)
- ✅ BE = Entry + Spread (S.18)
- ✅ Nach BE-Stop sofort Re-Entry (Workflow 6.8)
- ✅ BC-Korrektur = IMMER Re-Entry bei aktivierter Sequenz (Workflow 6.6)
- ✅ 5 Pips Toleranz vor 161.8er = Zielbereich abgearbeitet (Workflow 6.5)
- ✅ Nach 200er + GKL-Abarbeitung → keine Entrys mehr (Workflow 6.7)
- ✅ Verlust-Ausgleichs-TP (Workflow 6.1)
- ✅ Mind. 3-4 Bestätigungen vor Entry (Cheat Node 9)
- ✅ 100er Extension als Richtungsweiser (S.16-17)
- ✅ CRV mindestens 1:1 (S.13)
- ✅ Risikodiversifikation über viele Märkte (S.19, Workflow 2.1+2.2)
- ✅ Spread berücksichtigen bei Entry (Workflow 5.1), NICHT beim SL (Workflow 4.4)
- ✅ Kein Platz für Emotionen — System streng eingehalten, kein Ermessensspielraum (Cheat 30-32)
