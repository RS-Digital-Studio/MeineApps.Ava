# CLAUDE.md — SK-System Trading Bot: Verifikation & Refactoring

## Projekt-Übersicht

Vollautomatisierter Krypto-Trading-Bot basierend auf dem SK-System (Stefan Kassing).
Der Bot erkennt 0-A-B-C Sequenzen, tradet BC-Korrekturen via BingX-API und nutzt Multi-Timeframe-Analyse (4H/1H/15m).

**Sprache:** C# / .NET
**Exchange:** BingX (Perpetual Futures)
**Margin-Modus:** Isolated

---

## Projektstruktur

```
F:\Meine_Apps_Ava\src\Apps\BingXBot\          ← Hauptanwendung (Entry Point, UI, Konfiguration)
F:\Meine_Apps_Ava\src\Libraries\              ← Engine + Core (Shared Libraries)
```

Beim ersten Start: Lies die gesamte Projektstruktur ein und erstelle eine Übersicht aller `.cs`-Dateien mit einer kurzen Beschreibung, was jede Datei tut. Melde fehlende oder leere Dateien.

---

## KRITISCH: Von der visuellen Analyse zum Algorithmus

Das SK-System wurde für das menschliche Auge entwickelt. Ein Trader sieht Swing-Highs und Swing-Lows sofort im Chart — ein Algorithmus nicht.
Die Sequenzen im SK-System sind vereinfachte Elliott-Wellen (3-wellige korrektive ABC-Struktur statt 5-welliger Impulse).
Die algorithmische Umsetzung erfordert eine mehrstufige Pipeline:

```
Rohe Kerzendaten
    ↓
[Stufe 1] ZigZag-Algorithmus → Swing-High/Low Erkennung (filtert Rauschen)
    ↓
[Stufe 2] Sequenz-Mustererkennung → 0-A-B-C Punkte identifizieren
    ↓
[Stufe 3] Fibonacci-Berechnung → Korrekturzonen & Ziellevel
    ↓
[Stufe 4] Validierung → SK-Regeln prüfen (Zeit-Proportion, ATR, etc.)
    ↓
[Stufe 5] Signal-Generierung → Entry, SL, TP
```

### Stufe 1: ZigZag als Fundament (Das "Auge" des Algorithmus)

Der ZigZag-Algorithmus ist das Kernstück, das die menschliche visuelle Wahrnehmung ersetzt.
Er filtert Marktrauschen und identifiziert nur signifikante Wendepunkte.

**Empfohlenes NuGet-Paket:** `Skender.Stock.Indicators` (GitHub: DaveSkender/Stock.Indicators)
- Enthält fertige C#-Implementierungen von: **ZigZag**, **ADX**, **ATR** und 150+ weitere Indikatoren
- ZigZag-Aufruf: `quotes.GetZigZag(EndType.HighLow, percentChange)`
- ADX-Aufruf: `quotes.GetAdx(lookbackPeriods)` 
- ATR-Aufruf: `quotes.GetAtr(lookbackPeriods)`
- NuGet: https://www.nuget.org/packages/Skender.Stock.Indicators
- Docs: https://dotnet.stockindicators.dev

**Prüfe ob das Projekt dieses Paket (oder Äquivalent) bereits nutzt.** Wenn nicht: Vorschlagen.
Wenn eigene ZigZag-Implementierung existiert: Gegen `Skender.Stock.Indicators` abgleichen auf Korrektheit.

**ZigZag-Parameter für SK-System (müssen konfigurierbar sein):**

| Parameter | Beschreibung | Empfohlener Startwert |
|-----------|-------------|----------------------|
| `EndType` | `HighLow` (Dochte) für Punkt-Erkennung, nicht `Close` | `HighLow` |
| `percentChange` | Mindest-Swing in % um Rauschen zu filtern | Timeframe-abhängig: 4H=3-5%, 1H=1.5-3%, 15m=0.5-1.5% |

**ACHTUNG:** Der ZigZag repaintet (die letzte Linie ändert sich mit neuen Daten). 
Das ist für die Punkt-Erkennung gewollt (Trailing Low für B-Punkt), aber der Code muss damit umgehen:
- Nur bestätigte (abgeschlossene) ZigZag-Punkte als 0, A, B verwenden
- Der aktuelle (unbestätigte) Punkt kann als `CurrentLowest`/`CurrentHighest` dienen

### Stufe 2: Vom ZigZag zur SK-Sequenz

Der ZigZag liefert eine Liste alternierrender Swing-Highs und Swing-Lows.
Daraus muss der Algorithmus SK-Sequenzen (0-A-B-C) erkennen:

**Bullische Sequenz:**
```
ZigZag-Punkte:  ... SwingLow₁, SwingHigh₁, SwingLow₂, [Ausbruch über SwingHigh₁] ...
SK-Mapping:          Punkt 0,    Punkt A,     Punkt B,    → Aktivierung!
```

**Bärische Sequenz (spiegelverkehrt):**
```
ZigZag-Punkte:  ... SwingHigh₁, SwingLow₁, SwingHigh₂, [Ausbruch unter SwingLow₁] ...
SK-Mapping:          Punkt 0,    Punkt A,     Punkt B,    → Aktivierung!
```

**Algorithmus-Pseudocode für die Sequenz-Erkennung:**
```
FÜR jedes neue ZigZag-Pivot:
  1. Speichere die letzten 3 bestätigten Pivots (P1, P2, P3)
  2. Prüfe ob P1→P2→P3 ein gültiges 0-A-B Muster bildet:
     - Bullisch: P1=Low, P2=High, P3=Low UND P3 > P1 (höheres Tief)
     - Bärisch: P1=High, P2=Low, P3=High UND P3 < P1 (tieferes Hoch)
  3. Validiere:
     - ATR-Filter: |P2 - P1| > 2 × ATR
     - Zeit-Filter: Kerzen(P2→P3) >= 0.25 × Kerzen(P1→P2)
  4. Speichere als Sequenz-Kandidat im Zustand "PENDING"
  5. Überwache auf Aktivierung: Close über/unter P2
  6. Bei Aktivierung: Status → "ACTIVATED", B-Punkt finalisieren
  7. Bei Invalidierung (Docht unter P1): Status → "INVALIDATED"
```

### Stufe 3: Fibonacci-Berechnungen

Nach Aktivierung berechnet der Bot die Handelszonen. Hier die exakten Formeln:

**BC-Korrekturlevel (Golden Pocket) — Bullisch:**
```csharp
// Nach Aktivierung: CurrentHigh tracken (höchstes High seit Ausbruch über A)
decimal range = CurrentHigh - PointB.Price;
decimal bcZoneTop = CurrentHigh - (range * 0.500m);    // 50.0% Retracement
decimal bcZoneBottom = CurrentHigh - (range * 0.667m);  // 66.7% Retracement
// Kaufzone: Preis fällt in [bcZoneBottom, bcZoneTop]
```

**BC-Korrekturlevel — Bärisch:**
```csharp
decimal range = PointB.Price - CurrentLow;
decimal bcZoneTop = CurrentLow + (range * 0.667m);     // 66.7% (obere Grenze)
decimal bcZoneBottom = CurrentLow + (range * 0.500m);   // 50.0% (untere Grenze)
// Verkaufszone: Preis steigt in [bcZoneBottom, bcZoneTop]
```

**Ziellevel (161.8% Extension) — Bullisch:**
```csharp
decimal impulse = PointA.Price - Point0.Price;          // Strecke 0→A
decimal target161 = PointB.Price + (impulse * 1.618m);  // Projiziert von B
```

**Ziellevel (161.8% Extension) — Bärisch:**
```csharp
decimal impulse = Point0.Price - PointA.Price;          // Strecke 0→A (positiv)
decimal target161 = PointB.Price - (impulse * 1.618m);  // Projiziert von B nach unten
```

**GKL (Gesamtkorrekturlevel) — Bullisch, wenn Target 161.8% erreicht:**
```csharp
decimal gklRange = TargetC.Price - Point0.Price;        // Gesamte Strecke 0→C
decimal gklTop = TargetC.Price - (gklRange * 0.500m);   // 50.0%
decimal gklBottom = TargetC.Price - (gklRange * 0.667m); // 66.7%
```

**GKL — Bärisch, wenn Target 161.8% erreicht:**
```csharp
decimal gklRange = Point0.Price - TargetC.Price;        // Gesamte Strecke 0→C
decimal gklBottom = TargetC.Price + (gklRange * 0.500m); // 50.0%
decimal gklTop = TargetC.Price + (gklRange * 0.667m);    // 66.7%
// Verkaufszone: Preis steigt in [gklBottom, gklTop]
```

### Stufe 3b: Erweiterte SK-Regeln (KRITISCH — oft übersehen)

Diese Regeln unterscheiden das SK-System von einer simplen Fib-Strategie:

#### 38.2%-Mindestaktivierung
Eine Sequenz wird erst als gültig betrachtet, wenn der Markt **mindestens das 38.2% Extension-Level** erreicht.
Ohne dieses Minimum ist die Bewegung zu schwach, um als Sequenz zu gelten.
```csharp
// Bullisch: Nach Aktivierung (Close > A)
decimal minTarget = PointB.Price + ((PointA.Price - Point0.Price) * 0.382m);
bool isSequenceValid = CurrentHigh >= minTarget;
// Erst wenn isSequenceValid == true → BC-Zone berechnen
```

#### Gescheiterte Sequenz → Größere Sequenz
Wenn eine kleine Sequenz scheitert (Ziel nicht erreicht, Invalidierung), bildet sich daraus oft eine größere übergeordnete Sequenz. Das ist ein Kern-Konzept im SK-System.

```
Beispiel (Bullisch):
Kleine Sequenz: 0₁-A₁-B₁ → Aktiviert → BC-Entry → SL getroffen → GESCHEITERT
                                                        ↓
Größere Sequenz: 0₁ wird neuer Punkt 0₂, das Hoch der gescheiterten wird A₂,
                 das neue Low nach dem SL wird B₂ → Neue, größere Sequenz!
```

**Algorithmus:**
```
WENN Sequenz.Status == FAILED:
  1. Prüfe ob das Low nach dem Scheitern (neues B₂) höher ist als das ursprüngliche 0₁
  2. Wenn ja: Erstelle neue Sequenz mit:
     - Point0 = ursprüngliches Point0 der gescheiterten Sequenz
     - PointA = Hoch der gescheiterten Sequenz (oder das CurrentHigh vor dem Scheitern)
     - PointB = neues Low nach dem Scheitern
  3. Validiere die neue Sequenz mit allen Standardfiltern
  4. Die neue Sequenz hat ein GRÖSSERES Korrekturlevel → attraktiverer Entry
```

**Prüfe:**
- Wird nach einem Scheitern geprüft, ob eine übergeordnete Sequenz entstanden ist?
- Wird das korrekt über Timeframes hinweg gemacht (15m scheitert → 1H-Sequenz entsteht)?

#### Re-Entry nach gescheitertem BC
Wenn der BC-Trade gestoppt wird, gibt es im SK-System noch eine letzte Einstiegsmöglichkeit:
- Das **GKL (Gesamtkorrekturlevel)** der Sequenz (Fib 50%-66.7% von Punkt 0 bis zum erreichten Hoch)
- Dieser Entry ist attraktiver (tieferer Preis, engerer SL zu Punkt 0)

```csharp
// Nach BC-Trade SL:
if (bcTradeResult == TradeResult.StoppedOut)
{
    // GKL als letzte Chance berechnen
    decimal reEntryRange = CurrentHighBeforeFail - Point0.Price;
    decimal reEntryTop = CurrentHighBeforeFail - (reEntryRange * 0.500m);
    decimal reEntryBottom = CurrentHighBeforeFail - (reEntryRange * 0.667m);
    // Neuer Limit-Buy in [reEntryBottom, reEntryTop]
}
```

#### Überlappungszonen (Confluence)
Wenn eine bullische und eine bärische Zone sich überlappen, ist das ein besonders starkes Signal.
Beispiel: Das bullische GKL eines Paares liegt genau am bärischen 161.8%-Ziellevel.

**Prüfe:**
- Werden Zonen verschiedener Sequenzen/Richtungen verglichen?
- Gibt es eine Confluence-Score oder Priority-Logik?
- Mindestens: Log wenn Überlappung erkannt wird

#### Mehrere gleichzeitige Sequenzen
Pro Symbol und Timeframe können **mehrere Sequenzen** gleichzeitig aktiv sein (z.B. eine BC-Sequenz und eine übergeordnete GKL-Sequenz).

**Prüfe:**
- Ist der Code auf eine Sequenz pro Symbol limitiert? → Muss mehrere tracken können
- Werden Sequenzen in einer Liste/Collection verwaltet (nicht als einzelne Variable)?
- Haben Sequenzen eine eindeutige ID und einen Lifecycle-Status?

### Stufe 4: Timeframe-Puzzling & Fahrplan (KERNKONZEPT)

Stefan Kassing nennt es "Timeframe-Puzzling": Das systematische Zusammensetzen der Marktrichtung von übergeordnet nach untergeordnet. **Ohne Fahrplan keine Trades** — das ist der häufigste Anfängerfehler im SK-System.

#### 4.1 Der Fahrplan (Übergeordnete Marktrichtung)

Der Fahrplan ist die ERSTE Aufgabe vor jedem Trade. Er bestimmt, in welche Richtung gehandelt wird.

**Algorithmischer Ablauf:**
```
1. MONTHLY/WEEKLY analysieren:
   - Wo steht der Preis relativ zum ATH/ATL?
   - Gibt es eine übergeordnete abgearbeitete Sequenz mit offenem GKL?
   - Gibt es aktive Sequenzen mit offenen Zielleveln?
   → Ergebnis: Grundrichtung (LONG_BIAS / SHORT_BIAS / NEUTRAL)

2. DAILY/4H analysieren:
   - Passt die aktuelle Struktur zum übergeordneten Bias?
   - Gibt es Wendebereiche (Korrekturlevel) in Richtung des Bias?
   → Ergebnis: Aktiver Fahrplan mit Zielzone

3. 1H/15m analysieren:
   - Nur Setups suchen, die zum Fahrplan passen
   - NIEMALS gegen den Fahrplan traden (Ausnahme: Hedge-Szenarien)
```

**Für den Bot bedeutet das:**
```csharp
enum MarketBias { LongBias, ShortBias, Neutral }

class Fahrplan
{
    public string Symbol { get; set; }
    public MarketBias Bias { get; set; }           // Übergeordnete Richtung
    public decimal? TargetZoneTop { get; set; }     // Wohin will der Markt?
    public decimal? TargetZoneBottom { get; set; }
    public string Timeframe { get; set; }           // Auf welchem TF basiert der Bias?
    public DateTime ValidUntil { get; set; }        // Wann muss neu bewertet werden?
    public List<string> Reasoning { get; set; }     // Warum dieser Bias? (Logging)
}
```

**Prüfe:**
- Existiert ein Fahrplan-Konzept im Code?
- Wird übergeordnet geprüft bevor untergeordnet Signale generiert werden?
- Werden Trades gegen den Fahrplan blockiert?
- Wird der Fahrplan periodisch neu bewertet (z.B. bei jeder neuen Daily-Kerze)?

#### 4.2 Timeframe-Hierarchie im Detail

```
┌──────────────────────────────────────────────────────────────────┐
│ WEEKLY / MONTHLY  (Fahrplan-Ebene)                               │
│                                                                    │
│ Aufgabe: Grundrichtung bestimmen                                  │
│ - ATH/ATL Position bewerten                                       │
│ - Abgearbeitete Sequenzen → offene GKLs identifizieren           │
│ - Aktive Sequenzen → offene Ziellevel identifizieren              │
│ - Ergebnis: LONG_BIAS oder SHORT_BIAS                             │
│                                                                    │
│ Algorithmisch: ZigZag auf Weekly (percentChange=5-10%)            │
│ Nur bei neuer Weekly-Kerze neu berechnen                          │
├──────────────────────────────────────────────────────────────────┤
│ 4H  (Struktur-Ebene / Primäre Sequenzen)                         │
│                                                                    │
│ Aufgabe: Primäre Sequenzen in Richtung des Fahrplans finden      │
│ - BC-Korrekturlevel und GKL als Kaufzonen identifizieren         │
│ - Nur Sequenzen die zum Weekly-Bias passen                        │
│ - 100er Extension muss erreicht sein für valides BC               │
│ - Prüfen: Liegt eine Überlappung mit entgegengesetztem Bereich?  │
│                                                                    │
│ Algorithmisch: ZigZag auf 4H (percentChange=3-5%)                │
│ Bei jeder geschlossenen 4H-Kerze aktualisieren                   │
│ Für Zonen-Check: Live-Preis verwenden (NICHT auf Close warten)   │
├──────────────────────────────────────────────────────────────────┤
│ 1H  (Filter-Ebene / Sekundäre Sequenzen)                         │
│                                                                    │
│ Aufgabe: Bestätigung innerhalb der 4H-Kaufzone                   │
│ - Warten bis Preis in die 4H-Zone eintritt                       │
│ - Dort: Suche nach erstem Zeichen einer Trendwende               │
│   → Höheres Tief (bullisch) / Tieferes Hoch (bärisch)           │
│   → Oder: Sekundäre Sequenz die 100er Extension erreicht         │
│ - KEIN eigenständiger Trade auf 1H — nur als Filter              │
│                                                                    │
│ Algorithmisch: ZigZag auf 1H (percentChange=1.5-3%)              │
│ Nur aktiv wenn 4H-Zone IsZoneActive == true                      │
│ Live-Preis für Zonen-Check                                        │
├──────────────────────────────────────────────────────────────────┤
│ 15m  (Trigger-Ebene / Micro-Sequenzen)                            │
│                                                                    │
│ Aufgabe: Exakten Einstieg finden mit engem Stop-Loss             │
│ - Warten auf Punkt-A-Ausbruch einer kleinen Gegensequenz         │
│ - Entry im BC-Korrekturlevel der 15m-Sequenz                     │
│ - Stop-Loss unter Punkt 0 der 15m-Sequenz (+ ATR-Buffer)        │
│ - Das gibt einen extrem engen SL → hohes CRV                     │
│                                                                    │
│ Algorithmisch: ZigZag auf 15m (percentChange=0.5-1.5%)           │
│ Nur aktiv wenn 1H-Filter bestätigt (FILTER_CONFIRMED)            │
│ Hier wird die tatsächliche Order platziert                        │
├──────────────────────────────────────────────────────────────────┤
│ 5m / 1m  (Optional: Feintuning)                                   │
│                                                                    │
│ Nur für manuelles Trading relevant                                │
│ Bot nutzt diese NICHT — zu viel Rauschen für Algorithmus          │
└──────────────────────────────────────────────────────────────────┘
```

#### 4.3 Untersequenzen (Primär, Sekundär, Breakout)

Im SK-System gibt es verschiedene Hierarchie-Ebenen von Sequenzen. Der Bot muss diese unterscheiden:

**Primäre Sequenz:**
Die übergeordnete Sequenz (typisch auf 4H). Bestimmt die Handelsrichtung und die Ziellevel.
```
Beispiel: 4H bullische Sequenz 0→A→B→C
         BC-Level = primärer Kaufbereich
         GKL = primärer Wendebereich
```

**Sekundäre Sequenz:**
Eine kleinere Sequenz innerhalb der primären, die der primären "hilft" ihre Ziele zu erreichen.
```
Beispiel: 
  4H primäre bullische Sequenz hat BC-Level bei 1.0800-1.0750
  Im 1H bildet sich INNERHALB dieses BC eine neue bullische Sequenz
  → Diese sekundäre Sequenz BESTÄTIGT den primären Kaufbereich
  → Wenn die sekundäre die 100er Extension erreicht = starkes Signal
```

**Breakout-Sequenz:**
Entsteht wenn eine bärische Struktur von den Bullen gebrochen wird (oder umgekehrt).
```
Beispiel:
  4H bärische Sequenz aktiv, Preis fällt
  1H bildet bullische Struktur die den Punkt A der bärischen Sequenz bricht
  → Bärische Sequenz ist INVALIDIERT
  → Bullische Breakout-Sequenz wird aktiviert
  → Besonders starkes Signal (Richtungswechsel)
```

**Algorithmus für Untersequenz-Erkennung:**
```
FÜR jede aktivierte primäre Sequenz auf 4H:
  1. Identifiziere den primären Kaufbereich (BC oder GKL)
  2. Wenn Preis in primären Kaufbereich eintritt:
     a. Starte Sequenz-Suche auf 1H innerhalb des Kaufbereichs
     b. Suche nach Punkt 0 (Low in der Zone) → Punkt A (erstes High) → Punkt B
     c. Wenn 1H-Sequenz aktiviert wird UND 100er Extension erreicht:
        → SEKUNDÄRE_BESTÄTIGUNG = true
     d. Starte Sequenz-Suche auf 15m innerhalb der 1H-Bestätigung
     e. 15m-Ausbruch über Punkt A = TRIGGER

FÜR jede aktive bärische Sequenz:
  1. Überwache ob eine bullische Sequenz den bärischen Punkt A bricht
  2. Wenn ja: Bärisch invalidieren, Breakout-Sequenz erstellen
  3. Breakout-Sequenz hat eigene Ziellevel (oft dynamischer als normale Sequenzen)
```

**Prüfe:**
- Unterscheidet der Code zwischen primären und sekundären Sequenzen?
- Werden sekundäre Sequenzen NUR innerhalb der primären Zone gesucht?
- Gibt es eine Breakout-Erkennung (entgegengesetzte Struktur gebrochen)?
- Werden Sequenzen mit einem `SequenceType` Enum getaggt? (PRIMARY, SECONDARY, BREAKOUT)

#### 4.4 Sequenzaufbau-Typen (IKI, III, KIK, etc.)

Stefan Kassing klassifiziert Sequenzen nach dem Charakter ihrer Teilbewegungen:
- **I** = Impulsiv (schnelle, starke Bewegung mit wenig Korrektur)
- **K** = Korrektiv (langsame, zähe Bewegung mit vielen Rücksetzern)

Die Kombination der drei Teilstrecken (0→A, A→B, B→C) ergibt den Sequenztyp:

| Typ | 0→A | A→B | B→C | Bedeutung | Algorithmus-Relevanz |
|-----|-----|-----|-----|-----------|---------------------|
| **III** | Impulsiv | Impulsiv | Impulsiv | Sehr stark, selten | Höchste Trefferquote, aggressive TP |
| **IKI** | Impulsiv | Korrektiv | Impulsiv | Standard-Setup | Normale Parameter verwenden |
| **IKK** | Impulsiv | Korrektiv | Korrektiv | Schwächelnd | Vorsicht: TP konservativ |
| **KIK** | Korrektiv | Impulsiv | Korrektiv | Unentschlossen | Confluence nötig |
| **KKK** | Korrektiv | Korrektiv | Korrektiv | Schwach | Nur mit starker Confluence traden |
| **IIK** | Impulsiv | Impulsiv | Korrektiv | Ermüdung | TP1 nehmen, Rest absichern |

**Algorithmische Erkennung (Impulsiv vs. Korrektiv):**
```csharp
enum MoveCharacter { Impulsive, Corrective }

MoveCharacter ClassifyMove(List<Candle> candles, decimal startPrice, decimal endPrice)
{
    // Methode 1: Kerzen-Verhältnis
    // Impulsiv = >60% der Kerzen in Bewegungsrichtung
    int trendCandles = candles.Count(c => 
        (endPrice > startPrice) ? c.Close > c.Open : c.Close < c.Open);
    decimal trendRatio = (decimal)trendCandles / candles.Count;
    
    // Methode 2: Retracement-Tiefe
    // Impulsiv = kein Rücksetzer > 38.2% der bisherigen Bewegung
    decimal maxRetracement = CalculateMaxRetracement(candles, startPrice, endPrice);
    
    // Methode 3: Geschwindigkeit (Preis pro Kerze)
    decimal pricePerCandle = Math.Abs(endPrice - startPrice) / candles.Count;
    decimal atr = CalculateATR(candles, 14);
    decimal speedRatio = pricePerCandle / atr;
    
    // Kombinierte Bewertung
    if (trendRatio > 0.60m && maxRetracement < 0.382m && speedRatio > 0.5m)
        return MoveCharacter.Impulsive;
    return MoveCharacter.Corrective;
}
```

**Prüfe:**
- Werden Sequenztypen klassifiziert?
- Beeinflusst der Typ die TP-Strategie oder Confluence-Score?
- Wird `III` als besonders stark gewertet?
- Wird `KKK` als Warnsignal behandelt?

#### 4.5 Entry-Regeln (Regel 1 und Regel 2)

Im SK-System gibt es zwei fundamentale Entry-Regeln:

**Regel 1: Impulsive Reaktion aus dem Wendebereich**
```
Bedingung: Der Markt kommt in einen Kaufbereich (BC/GKL) und reagiert
           mit einer IMPULSIVEN Bewegung heraus.
           
Algorithmisch:
  1. Preis betritt Kaufzone (BC oder GKL)
  2. Preis verlässt Kaufzone nach oben (bullisch)
  3. Prüfe: War die Bewegung aus der Zone impulsiv?
     → Mindestens 3 aufeinanderfolgende Kerzen in Trendrichtung
     → ODER: Eine einzelne Kerze mit Body > 1.5× ATR
  4. Wenn ja: Warte auf Korrektur dieser impulsiven Bewegung
  5. Entry im Korrekturlevel der impulsiven Bewegung (50-66.7%)
```

**Regel 2: BC nach Erreichen der 100er Extension**
```
Bedingung: Die Sequenz hat mindestens die 100% Extension erreicht.
           Erst DANN ist das BC-Korrekturlevel als Entry valid.
           
Algorithmisch:
  1. Sequenz aktiviert (Close > Punkt A)
  2. Preis steigt weiter
  3. Prüfe: Hat der Preis die 100% Extension erreicht?
     → 100% Extension = PointB + (PointA - Point0)  [bullisch]
  4. NUR wenn 100% erreicht: BC-Korrekturlevel wird valider Kaufbereich
  5. Wenn Preis die 100% NICHT erreicht und zurückfällt:
     → BC ist NICHT valid → Kein Entry
     → Aber: Prüfen ob GKL als Wendebereich funktioniert
```

**Prüfe:**
- Wird Regel 1 (impulsive Reaktion) geprüft?
- Wird die 100er Extension als Voraussetzung für valides BC geprüft?
- Werden Trades ohne 100er Extension blockiert?

#### 4.6 Dreh- und Wendebereiche

Das sind die Stellen im Chart, an denen Sequenzen ENTSTEHEN. Nicht jede Zone ist ein Wendebereich.

**Valide Wendebereiche:**
- BC-Korrekturlevel einer aktivierten, abgearbeiteten Sequenz
- GKL einer Sequenz die mindestens 161.8% erreicht hat
- Entgegengesetztes Korrekturlevel (z.B. bärisches GKL = bullischer Wendebereich)
- Überlappungszone (mehrere Level treffen sich)
- ATH/ATL-Bereiche (historische Extrempunkte)

**KEINE validen Wendebereiche:**
- Zufällige Support/Resistance-Linien
- Sequenzen mitten in einer Bewegung (ohne Bezug zum übergeordneten Chart)
- Bereiche die nur auf einem einzigen Timeframe sichtbar sind

**Algorithmus:**
```
bool IsValidWendebereich(Zone zone)
{
    // Muss aus einer abgearbeiteten oder aktiven Sequenz stammen
    if (zone.Origin == null) return false;
    
    // Muss zum übergeordneten Fahrplan passen
    if (zone.Direction != CurrentFahrplan.Bias) return false;
    
    // Muss auf mindestens 2 Timeframes sichtbar sein
    if (zone.VisibleOnTimeframes.Count < 2) return false;
    
    // Bonus: Überlappung mit entgegengesetztem Bereich
    if (HasOverlapWithOpposite(zone)) zone.ConfluenceScore += 2;
    
    return true;
}
```

**Prüfe:**
- Werden Sequenzen nur an Wendebereichen gestartet?
- Oder werden auch "wilde" Sequenzen mitten im Trend erkannt?
- Gibt es einen Wendebereich-Validator?

#### 4.7 Die 100er Extension als Validierungsschwelle

Die 100% Extension ist im SK-System eine kritische Schwelle:

```
Bullisch: 100% Extension = PointB + (PointA - Point0)
Bärisch:  100% Extension = PointB - (Point0 - PointA)
```

| Preis-Position | Bedeutung | Bot-Aktion |
|---------------|-----------|------------|
| Unter 100% | Sequenz noch nicht bestätigt | BC-Zone NICHT traden, nur beobachten |
| 100%-138.2% | Sequenz bestätigt, BC valid | BC-Korrekturlevel als Entry nutzen |
| 138.2%-161.8% | Sequenz läuft gut | BC weiterhin valid, TP-Ziel = 161.8% |
| Über 161.8% | Sequenz abgearbeitet | BC ungültig → GKL berechnen statt BC |
| Über 200% | Überextension | GKL wird primärer Kaufbereich, konservative TPs |

**Prüfe:**
- Wird die 100er Extension überhaupt berechnet?
- Wird sie als Gate für BC-Entries verwendet?
- Wird bei >161.8% korrekt auf GKL umgeschaltet?

#### 4.8 Multi-Timeframe als State Machine

Die MTFA darf NICHT als `if (4H && 1H && 15m)` implementiert werden (Deadlock-Gefahr).
Stattdessen: **Hierarchische State Machine.**

```
States pro Symbol:
┌─────────────────────────────────────────────────────────────────┐
│ FAHRPLAN_CHECK                                                    │
│   → Weekly/Daily analysieren → MarketBias setzen                 │
│   → Alle X Stunden (z.B. bei neuer Daily-Kerze) wiederholen     │
│   → Wenn Bias klar: → SCANNING                                  │
│   → Wenn Neutral: → IDLE (kein Trade)                            │
├─────────────────────────────────────────────────────────────────┤
│ SCANNING                                                          │
│   → 4H-Sequenzen in Richtung des Fahrplans suchen              │
│   → Wenn 4H eine valide Kaufzone (BC/GKL) identifiziert:       │
│     → Wechsel zu ZONE_ACTIVE                                    │
├─────────────────────────────────────────────────────────────────┤
│ ZONE_ACTIVE (Timer: 10 Kerzen des 4H = 40 Stunden)              │
│   → Live-Preis gegen 4H-Zone prüfen (NICHT auf Close warten)   │
│   → Wenn Preis in Zone: 1H-Analyse starten                     │
│   → Wenn 1H im 4H-Korrekturlevel:                              │
│     a) Impulsive Reaktion (Regel 1)? → FILTER_CONFIRMED         │
│     b) Sekundäre Sequenz mit 100er Extension? → FILTER_CONFIRMED│
│     c) Höheres Tief gebildet? → FILTER_CONFIRMED                │
│   → Wenn Timer abläuft: → Zurück zu SCANNING                   │
│   → Wenn 4H-Zone invalidiert (Punkt 0 gebrochen): → SCANNING   │
├─────────────────────────────────────────────────────────────────┤
│ FILTER_CONFIRMED                                                  │
│   → 15m-Sequenz-Suche starten (Gegensequenz)                   │
│   → Wenn 15m Punkt A einer Gegensequenz bricht:                │
│     → Entry im 15m BC-Korrekturlevel berechnen                  │
│     → SL = 15m Punkt 0 - 1.5× ATR(15m)                         │
│     → TP1 = 15m 161.8% Extension                                │
│     → TP2 = 4H Ziellevel                                        │
│     → Wechsel zu TRIGGER_READY                                  │
│   → Wenn 4H-Zone invalidiert: → Zurück zu SCANNING             │
├─────────────────────────────────────────────────────────────────┤
│ TRIGGER_READY                                                     │
│   → Limit-Order platzieren (knapp unter Live-Preis)             │
│   → Entry + SL atomar an BingX senden                           │
│   → Wechsel zu ORDER_PLACED                                     │
├─────────────────────────────────────────────────────────────────┤
│ ORDER_PLACED                                                      │
│   → Warten auf Fill (Timeout: z.B. 4 Stunden)                  │
│   → Bei Fill: → POSITION_OPEN                                   │
│   → Bei Timeout: Order canceln → SCANNING                       │
├─────────────────────────────────────────────────────────────────┤
│ POSITION_OPEN                                                     │
│   → BingX managed SL/TP                                         │
│   → Bei TP1 (15m 161.8%): 50% schließen, SL → Break-Even      │
│   → Bei TP2 (4H Ziel): Rest schließen                          │
│   → Bei SL: CancelAllOrders → Cooldown → SCANNING              │
│   → Bei SL: Prüfen ob gescheiterte → größere Sequenz entsteht  │
│   → Bei SL: Prüfen ob GKL Re-Entry möglich                     │
└─────────────────────────────────────────────────────────────────┘
```

**Prüfe:** Ist die aktuelle Implementierung eine State Machine oder eine verschachtelte if-Kette?
Wenn if-Kette → Refactoring zu State Machine vorschlagen (mit Zustandsdiagramm).

#### 4.9 Daten-Flow zwischen Timeframes

**WICHTIG:** Die Timeframes arbeiten NICHT unabhängig voneinander. Daten fließen von oben nach unten:

```
Weekly → Fahrplan (Bias + Zielzone)
           ↓ wird übergeben an
4H    → Primäre Sequenzen + Kaufzonen
           ↓ aktive Zonen werden übergeben an  
1H    → Sekundäre Bestätigung (nur innerhalb 4H-Zone)
           ↓ Bestätigung wird übergeben an
15m   → Trigger + exakter Entry (nur wenn 1H bestätigt)
```

**Und von unten nach oben (Feedback):**
```
15m   → Trade-Ergebnis (SL/TP)
           ↑ wird gemeldet an
1H    → Sequenz-Update (gescheitert? → neue Chance?)
           ↑ wird gemeldet an
4H    → Zone-Status-Update (Zone noch gültig? Sequenz abgearbeitet?)
           ↑ wird gemeldet an
Weekly → Fahrplan-Update (Ziel erreicht? Bias-Wechsel?)
```

**Prüfe:**
- Werden Daten von höheren TFs an niedrigere übergeben?
- Bekommt der 15m-Trigger die 4H-Kaufzone mitgeteilt?
- Wird nach einem Trade das Ergebnis an die höheren TFs zurückgemeldet?
- Oder arbeiten die TFs isoliert voneinander?

### Stufe 5: Feinheiten der Marktstruktur (Stabilisierung, Overtracing, B-Punkt-Qualität)

Diese drei Konzepte sind im SK-System entscheidend für die Qualität der Entries. Sie sind für das menschliche Auge leicht erkennbar, aber algorithmisch anspruchsvoll.

#### 5.1 Stabilisierungsphasen (Prestabilisation)

Im SK-System läuft der Markt SELTEN direkt aus einem Wendebereich heraus zum Ziel. Stattdessen stabilisiert sich der Preis erst — es findet ein "Kampf" zwischen Käufern und Verkäufern statt, der Liquidität aufbaut. Diese Phase ist ein QUALITÄTSMERKMAL und kein Schwächezeichen.

**Was ist Stabilisierung?**
Der Preis betritt eine Kaufzone (BC/GKL) und bewegt sich dort seitwärts mit abnehmender Volatilität, bevor er ausbricht. Visuell: Ein "Zusammenziehen" des Preises in der Zone.

**Warum ist das wichtig für den Bot?**
- Ohne Stabilisierungs-Erkennung triggert der Bot beim ersten Berühren der Zone → oft zu früh → SL
- Mit Stabilisierung: Bot wartet auf Bestätigung → weniger SL, bessere Entries
- Ein manueller Trader sieht die Seitwärtsphase sofort — der Bot muss sie mathematisch erkennen

**Algorithmische Erkennung:**
```csharp
class StabilisationDetector
{
    // Methode 1: Bollinger Band Squeeze
    // Die Bollinger Bänder ziehen sich zusammen wenn Volatilität sinkt
    bool IsSqueezing(List<Candle> candles, int period = 20)
    {
        var bbWidth = CalculateBBWidth(candles, period); // (Upper - Lower) / Middle
        var bbWidthPrev = CalculateBBWidth(candles.SkipLast(5), period);
        return bbWidth < bbWidthPrev * 0.75m; // BB-Breite um >25% geschrumpft
    }
    
    // Methode 2: Abnehmende Kerzenkörper-Größe
    // Kerzen werden kleiner = Unentschlossenheit = Stabilisierung
    bool IsBodyShrinking(List<Candle> lastNCandles, int lookback = 10)
    {
        var firstHalf = lastNCandles.Take(lookback / 2)
            .Average(c => Math.Abs(c.Close - c.Open));
        var secondHalf = lastNCandles.Skip(lookback / 2)
            .Average(c => Math.Abs(c.Close - c.Open));
        return secondHalf < firstHalf * 0.6m; // Körper >40% kleiner geworden
    }
    
    // Methode 3: Range-Kontraktion
    // High-Low Range der letzten Kerzen nimmt ab
    bool IsRangeContracting(List<Candle> candles, int lookback = 10)
    {
        var ranges = candles.TakeLast(lookback)
            .Select(c => c.High - c.Low).ToList();
        var firstHalfAvg = ranges.Take(lookback / 2).Average();
        var secondHalfAvg = ranges.Skip(lookback / 2).Average();
        return secondHalfAvg < firstHalfAvg * 0.65m;
    }
    
    // Methode 4: Höhere Tiefs + Tiefere Hochs (Symmetrisches Dreieck)
    // Preis wird von beiden Seiten eingequetscht
    bool IsFormingTriangle(List<SwingPoint> swings, int minSwings = 4)
    {
        if (swings.Count < minSwings) return false;
        var highs = swings.Where(s => s.IsHigh).Select(s => s.Price).ToList();
        var lows = swings.Where(s => !s.IsHigh).Select(s => s.Price).ToList();
        bool descendingHighs = highs.Zip(highs.Skip(1), (a, b) => b < a).All(x => x);
        bool ascendingLows = lows.Zip(lows.Skip(1), (a, b) => b > a).All(x => x);
        return descendingHighs && ascendingLows;
    }
    
    // Kombinierte Bewertung
    StabilisationScore Evaluate(List<Candle> candles, Zone kaufzone)
    {
        int score = 0;
        if (IsSqueezing(candles)) score += 2;
        if (IsBodyShrinking(candles)) score += 1;
        if (IsRangeContracting(candles)) score += 1;
        if (IsFormingTriangle(GetSwingsInZone(candles, kaufzone))) score += 2;
        
        // score >= 3 = starke Stabilisierung → Signal-Qualität erhöhen
        // score 1-2 = leichte Stabilisierung → normal handeln
        // score 0 = keine Stabilisierung → vorsichtig sein (impulsiver Entry nötig)
        return new StabilisationScore(score);
    }
}
```

**Integration in die State Machine:**
```
ZONE_ACTIVE:
  → Preis betritt Zone
  → NICHT sofort triggern!
  → StabilisationDetector starten
  → Wenn Stabilisation erkannt (Score ≥ 3):
    → Confluence-Score +2 (Qualitätsbonus)
    → Aggressiverer Entry möglich (z.B. auch ohne 15m-Trigger)
  → Wenn KEINE Stabilisation und Preis verlässt Zone:
    → Normale Entry-Logik (braucht zwingend 15m-Trigger)
  → Wenn Preis IMPULSIV aus Zone ausbricht (Regel 1):
    → Sofort handeln (Stabilisation nicht nötig bei Impuls)
```

**Prüfe:**
- Gibt es eine Erkennung von Seitwärtsphasen in Kaufzonen?
- Oder triggert der Bot bei erster Berührung der Zone?
- Wird Bollinger-Band-Squeeze oder ähnliches genutzt?
- Beeinflusst die Stabilisierung den Confluence-Score?

#### 5.2 Overtracing (Falsches Brechen von Leveln)

Overtracing ist einer der wichtigsten Begriffe im SK-System: Der Preis schießt knapp über/unter ein Level hinaus, kehrt aber sofort um. Das ist KEIN echter Bruch — es ist eine Liquiditäts-Jagd (Stop-Hunting).

**Manuell:** Ein Trader sieht sofort "der Preis hat das Level nur kurz angetippt, das war kein Ausbruch."
**Algorithmisch:** Der Bot interpretiert jeden Docht über einem Level als Bruch → falsche Invalidierungen.

**Definition Overtracing:**
```
Ein Level gilt als OVERTRACED (nicht gebrochen), wenn:
1. Ein Docht das Level durchbricht, aber die Kerze NICHT darüber/darunter SCHLIESST
2. ODER: Der Close ist nur marginal über dem Level (< Toleranz)
3. UND: Der Preis kehrt innerhalb von X Kerzen wieder zurück

Ein Level gilt als GEBROCHEN, wenn:
1. Mindestens eine Kerze ÜBER dem Level schliesst (nicht nur Docht)
2. UND: Der Close ist signifikant über dem Level (> Toleranz)
3. UND/ODER: Mehrere aufeinanderfolgende Kerzen über dem Level schliessen
```

**Algorithmische Implementierung:**
```csharp
enum LevelInteraction { NotTouched, Overtraced, Broken }

LevelInteraction EvaluateLevelBreak(
    decimal levelPrice, 
    List<Candle> recentCandles, 
    decimal atr,
    bool isResistance) // true = Preis versucht nach OBEN zu brechen
{
    // Toleranz: 0.3× ATR — alles darunter ist "marginaler Bruch" = Overtrace
    decimal tolerance = atr * 0.3m;
    
    // Confirmation-Fenster: 3 Kerzen nach dem Bruch
    int confirmationBars = 3;
    
    foreach (var candle in recentCandles)
    {
        bool wickBeyond = isResistance 
            ? candle.High > levelPrice 
            : candle.Low < levelPrice;
        
        bool closeBeyond = isResistance 
            ? candle.Close > levelPrice + tolerance
            : candle.Close < levelPrice - tolerance;
        
        if (!wickBeyond) continue; // Level nicht berührt
        
        if (!closeBeyond)
        {
            // Docht über Level, aber Close darunter = Overtrace
            return LevelInteraction.Overtraced;
        }
        
        // Close ist über Level + Toleranz → prüfe Bestätigung
        int barsAbove = CountConsecutiveClosesAbove(
            recentCandles, levelPrice, candle, confirmationBars);
        
        if (barsAbove >= 2)
            return LevelInteraction.Broken; // Echte Bestätigung
        else
            return LevelInteraction.Overtraced; // Nur kurz drüber, zurückgekehrt
    }
    
    return LevelInteraction.NotTouched;
}
```

**Wo Overtracing relevant ist (3 kritische Stellen):**

| Situation | Ohne Overtracing-Erkennung | Mit Overtracing-Erkennung |
|-----------|---------------------------|--------------------------|
| **Punkt A Ausbruch** | Docht über A = Aktivierung → oft Fehlsignal | Nur Close über A + Toleranz = Aktivierung (✅ bereits implementiert per Regel 1.1b) |
| **Punkt 0 Invalidierung** | Jeder Docht unter 0 = Sequenz tot → zu viele Verluste | Docht unter 0 = WARNUNG, Close unter 0 - Toleranz = Invalidierung |
| **Entgegengesetzter Wendebereich** | Preis bricht bärisches Level → Bot denkt Bären gewonnen → stoppt Long | Preis hat Level nur overtraced → Long bleibt gültig |

**⚠️ ACHTUNG: Punkt 0 Invalidierung vs. Overtracing-Toleranz**
Das ist ein Spannungsfeld im SK-System:
- Die strenge Regel sagt: Jeder Docht unter Punkt 0 = Sequenz tot
- Aber in der Praxis: Liquidity Grabs durchstechen Punkt 0 knapp, bevor der Preis dreht
- **Lösung:** Der SL-Buffer (1.5× ATR unter Punkt 0) fängt das bereits ab
- **Zusätzlich:** Bei Invalidierung durch Docht (nicht Close): Sequenz auf "WARNED" setzen, nicht sofort "INVALIDATED". Erst wenn Close unter Punkt 0 - Toleranz → endgültig tot.

```csharp
// Erweiterte Invalidierungs-Logik mit Overtrace-Schutz
void CheckInvalidation(Candle candle, Sequence seq)
{
    decimal tolerance = atr15m * 0.3m;
    
    if (seq.IsLong)
    {
        if (candle.Close < seq.Point0 - tolerance)
        {
            // Close deutlich unter Punkt 0 → ENDGÜLTIG invalidiert
            seq.Status = SequenceStatus.Invalidated;
        }
        else if (candle.Low < seq.Point0)
        {
            // Nur Docht unter Punkt 0 → WARNUNG (mögliches Overtrace)
            seq.Status = SequenceStatus.Warned;
            seq.WarnedAtCandle = candle;
            // Wenn nächste Kerze über Punkt 0 schließt → Warnung aufheben
        }
    }
    // Spiegelverkehrt für Short...
}
```

**Prüfe:**
- Wird zwischen Docht-Bruch und Close-Bruch unterschieden?
- Gibt es eine Toleranz bei Level-Prüfungen?
- Wird der entgegengesetzte Wendebereich auf Overtracing geprüft?
- Gibt es einen "WARNED"-Status neben "INVALIDATED"?

#### 5.3 Hohes B vs. Tiefes B (B-Punkt-Qualität)

Die Position des B-Punkts relativ zu Punkt 0 und Punkt A bestimmt die Qualität des Setups maßgeblich.

**Definition (Bullisch):**
```
Hohes B: B-Punkt liegt nahe an Punkt A (wenig korrigiert, z.B. nur 23.6% Retracement)
          → Punkt B = PointA - (PointA - Point0) * 0.236

Tiefes B: B-Punkt liegt nahe an Punkt 0 (viel korrigiert, z.B. 78.6% Retracement)
          → Punkt B = PointA - (PointA - Point0) * 0.786

Ideales B: Zwischen 50% und 66.7% Retracement (Golden Pocket der 0→A Strecke)
           → Bestes Verhältnis aus Bestätigung + Platz für Bewegung
```

**Warum das für den Bot wichtig ist:**

| B-Punkt Position | Retracement | CRV | Risiko | Bot-Aktion |
|-----------------|-------------|-----|--------|------------|
| **Sehr hohes B** | 0-23.6% | Schlecht | Hoch (weiter SL) | ⚠️ Confluence ≥ 4 nötig, kleinere Position |
| **Hohes B** | 23.6-38.2% | Mäßig | Mittel | ⚠️ Nur mit guter Confluence traden |
| **Ideales B** | 38.2-66.7% | Gut | Normal | ✅ Standard-Entry, normale Positionsgröße |
| **Tiefes B** | 66.7-78.6% | Sehr gut | Gering (enger SL) | ✅ Aggressiver traden, größere Position möglich |
| **Zu tiefes B** | >78.6% | Theoretisch super | Invalidierung nahe | ⚠️ Punkt 0 sehr nahe → schnelle Invalidierung möglich |

**Algorithmische Implementierung:**
```csharp
enum BQuality { VeryHigh, High, Ideal, Deep, TooDeep }

class BPointAnalysis
{
    public BQuality Quality { get; set; }
    public decimal RetracementPercent { get; set; }
    public decimal CrvEstimate { get; set; }
    public decimal PositionSizeMultiplier { get; set; } // 0.5 bis 1.5
    
    public static BPointAnalysis Evaluate(Sequence seq)
    {
        decimal range = Math.Abs(seq.PointA - seq.Point0);
        decimal bRetracement = Math.Abs(seq.PointA - seq.LockedB) / range;
        
        // CRV-Schätzung: BC-Zone bis Ziellevel vs. SL
        decimal bcZoneEntry = seq.IsLong
            ? seq.LockedB + range * 0.5m   // Mitte der BC-Zone
            : seq.LockedB - range * 0.5m;
        decimal target = seq.IsLong
            ? seq.LockedB + range * 1.618m  // 161.8% Extension
            : seq.LockedB - range * 1.618m;
        decimal sl = seq.IsLong
            ? seq.Point0 - atr * 1.5m       // SL unter Punkt 0
            : seq.Point0 + atr * 1.5m;
        decimal reward = Math.Abs(target - bcZoneEntry);
        decimal risk = Math.Abs(bcZoneEntry - sl);
        decimal crv = risk > 0 ? reward / risk : 0;
        
        var result = new BPointAnalysis
        {
            RetracementPercent = bRetracement * 100m,
            CrvEstimate = crv
        };
        
        if (bRetracement < 0.236m)
        {
            result.Quality = BQuality.VeryHigh;
            result.PositionSizeMultiplier = 0.5m; // Halbe Position
        }
        else if (bRetracement < 0.382m)
        {
            result.Quality = BQuality.High;
            result.PositionSizeMultiplier = 0.75m;
        }
        else if (bRetracement < 0.667m)
        {
            result.Quality = BQuality.Ideal;
            result.PositionSizeMultiplier = 1.0m; // Volle Position
        }
        else if (bRetracement < 0.786m)
        {
            result.Quality = BQuality.Deep;
            result.PositionSizeMultiplier = 1.25m; // Leicht größer
        }
        else
        {
            result.Quality = BQuality.TooDeep;
            result.PositionSizeMultiplier = 0.75m; // Vorsicht: nahe an Punkt 0
        }
        
        return result;
    }
}
```

**Zusätzlicher Effekt: Hohes B → "Knapp gedochtete 100er"**
Bei einem hohen B ist die Strecke B→C (nach Aktivierung) sehr kurz. Das bedeutet:
- Die 100er Extension wird nur knapp erreicht (oft nur ein Docht)
- Das BC-Korrekturlevel ist winzig (wenig Platz für Entry)
- Der Bot muss erkennen: "Knapp gedochtete 100er + hohes B = schwaches Setup"

```csharp
bool IsWeak100Extension(Sequence seq, decimal currentHigh)
{
    decimal ext100 = seq.IsLong
        ? seq.LockedB + (seq.PointA - seq.Point0)
        : seq.LockedB - (seq.Point0 - seq.PointA);
    
    // Wurde 100er nur knapp (< 0.2× ATR) übertraced?
    decimal overshoot = Math.Abs(currentHigh - ext100);
    bool barelyReached = overshoot < atr * 0.2m;
    
    // Kombiniert mit hohem B = schwaches Setup
    var bAnalysis = BPointAnalysis.Evaluate(seq);
    if (barelyReached && bAnalysis.Quality <= BQuality.High)
    {
        return true; // Schwach → höherer Confluence-Score nötig oder Skip
    }
    return false;
}
```

**Prüfe:**
- Wird die B-Punkt-Position analysiert (Retracement-Prozent)?
- Beeinflusst die B-Qualität die Positionsgröße oder den Confluence-Score?
- Wird ein CRV vor dem Trade berechnet?
- Wird eine "knapp gedochtete 100er" erkannt?
- Wird ein hohes B + knapp 100er als schwaches Setup behandelt?

### Zusammenfassung: Was der Code HABEN muss

| Komponente | Quelle | Status prüfen |
|-----------|--------|---------------|
| ZigZag-Algorithmus | `Skender.Stock.Indicators` oder eigen | Filtert Rauschen, liefert Swing-Pivots |
| ATR-Indikator | `Skender.Stock.Indicators` oder eigen | Für Rausch-Filter und SL-Buffer |
| ADX-Indikator | `Skender.Stock.Indicators` oder eigen | Seitwärts-Filter (< 25 = pausieren) |
| Fahrplan-Generator | Eigenentwicklung | Weekly/Daily → Bias + Zielzone |
| Sequenz-Detektor | Eigenentwicklung | Mapped ZigZag-Pivots auf 0-A-B-C |
| Sequenz-Typ-Klassifizierer | Eigenentwicklung | IKI, III, KIK etc. erkennen |
| Untersequenz-Manager | Eigenentwicklung | Primär/Sekundär/Breakout unterscheiden |
| Fibonacci-Rechner | Eigenentwicklung | BC-Level, GKL, Extensions, 100er Gate |
| Wendebereich-Validator | Eigenentwicklung | Nur valide Zonen als Entry zulassen |
| Entry-Regel-Engine | Eigenentwicklung | Regel 1 (Impuls) + Regel 2 (100er) prüfen |
| State Machine (MTFA) | Eigenentwicklung | Hierarchie ohne Deadlock, mit Fahrplan |
| Stabilisations-Detektor | Eigenentwicklung | BB-Squeeze, Range-Kontraktion, Dreieck |
| Overtracing-Handler | Eigenentwicklung | Docht vs. Close, Toleranz, WARNED-Status |
| B-Punkt-Analysator | Eigenentwicklung | Retracement-%, CRV, Positionsgröße |
| Order-Manager | BingX REST API | Atomic Orders, InvariantCulture |
| Risk-Manager | Eigenentwicklung | Limits, Cooldown, ADX-Filter |
| State-Persistenz | SQLite/JSON | Amnesie-Schutz bei Neustart |

---

## Aufgabe

Verifiziere den gesamten bestehenden Code gegen die unten definierten SK-System-Regeln.
Für jede Regel: Prüfe ob sie korrekt implementiert ist, dokumentiere Abweichungen, und schlage konkrete Fixes vor.

**Arbeitsweise:**
1. Lies zuerst ALLE relevanten Dateien, bevor du Änderungen vorschlägst
2. Erstelle pro Komponente einen Prüfbericht (✅ korrekt / ⚠️ Abweichung / ❌ fehlt)
3. Ändere Code nur nach expliziter Freigabe
4. Keine Breaking Changes ohne Rückfrage
5. Jede Änderung mit Kommentar `// SK-VERIFY: [Regel-ID]` markieren

---

## SK-System Regel-Katalog

### 1. Sequenz-Erkennung (`SequenceDetector.cs` oder Äquivalent)

#### 1.1 Punkt-Erkennung (Docht-Regel)
- **Punkte 0, A, B anlegen:** Immer absolute Extremwerte verwenden (`High` für Punkt A bei bullisch, `Low` für Punkt 0 und B bei bullisch). Umgekehrt bei bärisch.
- **Aktivierung:** Sequenz gilt erst als aktiviert, wenn eine Kerze **über Punkt A SCHLIESST** (`candle.Close > PointA.Price`), NICHT wenn nur ein Docht darüber ragt.
- **Invalidierung:** Sequenz ist sofort tot, sobald auch nur ein einzelner Docht unter Punkt 0 fällt (`candle.Low < Point0.Price` bei bullisch).

**Prüfe:**
- Wird `Close` für Aktivierung verwendet (nicht `High`)?
- Wird `Low`/`High` (Docht) für Invalidierung verwendet (nicht `Close`)?
- Werden bärische Sequenzen spiegelverkehrt korrekt behandelt?

#### 1.2 Trailing Low (Dynamischer B-Punkt)
- Nach Punkt A wird das tiefste Low kontinuierlich in einer Variable `CurrentLowest` getrackt.
- B-Punkt wird NICHT beim ersten grünen Kerzenkörper fixiert.
- Erst bei Aktivierung (Close über A) wird `CurrentLowest` rückwirkend als finaler Punkt B gesetzt.

**Prüfe:**
- Gibt es eine `CurrentLowest`-Variable (oder Äquivalent)?
- Wird B erst bei Aktivierung finalisiert?
- Wird B bei jeder neuen Kerze aktualisiert, solange keine Aktivierung stattfand?

#### 1.3 Zeit-Proportions-Filter
- Anzahl Kerzen der Korrektur (A→B) muss ≥ 25% der Kerzen des Impulses (0→A) sein.
- Wenn Korrektur zu schnell → Sequenz ignorieren (Crash-Spike-Schutz).

**Prüfe:**
- Existiert dieser Filter?
- Formel: `candlesAB >= candlesOA * 0.25`
- Wird die Sequenz tatsächlich verworfen oder nur geloggt?

#### 1.4 Mindest-Distanz (Rausch-Filter)
- Strecke 0→A muss > 2× ATR sein.
- Wenn kleiner → keine Sequenz, sondern Marktrauschen.

**Prüfe:**
- Wird ATR korrekt berechnet (welche Periode? Standard: 14)?
- Wird der Filter VOR der Aktivierung geprüft?

---

### 2. Einstiegs-Strategie (BC-Korrekturlevel)

#### 2.1 Trailing High nach Aktivierung
- Nach Ausbruch über A: `CurrentHigh` tracken (höchstes High der laufenden Bewegung).
- Wird bei jeder neuen Kerze aktualisiert.

#### 2.2 BC-Korrekturlevel (Golden Pocket)
- Fibonacci von Punkt B bis `CurrentHigh`.
- Kaufzone: 50.0% bis 66.7% Retracement dieser Strecke.
- Trade wird getriggert, wenn Kurs in diese Zone zurückfällt.

**Prüfe:**
- Fib-Berechnung: Bei bullischer Sequenz ist 50% näher an CurrentHigh, 66.7% näher an B. Stelle sicher, dass die Richtung stimmt.
- Wird die Zone dynamisch mit `CurrentHigh` aktualisiert?

#### 2.3 GKL-Wechsel (Ziellevel-Regel)
- Wenn `CurrentHigh` das **161.8% Extension** der Gesamtsequenz (0→A projiziert) erreicht:
  - BC-Korrekturlevel wird gelöscht/ungültig
  - Stattdessen: GKL berechnen (Fib von Punkt 0 bis Zielpunkt C)
  - Kaufzone wird das 50%-66.7% Retracement des GKL

**Prüfe:**
- Wird das 161.8%-Ziellevel korrekt berechnet?
- Findet der Wechsel BC→GKL tatsächlich statt?
- Wird die alte BC-Zone sauber gelöscht?

---

### 3. Multi-Timeframe-Analyse (MTFA)

#### 3.1 Rollen der Timeframes
| Timeframe | Rolle | Aufgabe |
|-----------|-------|---------|
| **4H** | Großwetterlage | Übergeordnete BC- oder GKL-Kaufzonen identifizieren |
| **1H** | Filter | Im 4H-Korrekturlevel eine Trendwende andeuten (höheres Tief) |
| **15m** | Trigger | Punkt-A-Ausbruch der kleinen Gegensequenz → enger Stop-Loss |

#### 3.2 MTFA-Deadlock vermeiden
- **KEINE harten `&&`-Bedingungen** zwischen allen 3 Timeframes.
- Die Timeframes sollen hierarchisch prüfen, NICHT gleichzeitig alle true sein müssen.

**Prüfe:**
- Gibt es eine Stelle im Code, wo `tf4H.IsValid && tf1H.IsValid && tf15m.IsValid` steht? → Refactoring nötig.
- Korrekt: 4H aktiviert Zone → 1H bestätigt → 15m triggert (sequentiell, state-basiert).

#### 3.3 Offene-Kerzen-Bug
- Bei 4H und 1H: **NICHT** auf `Candle.IsClosed` warten, um zu prüfen ob Kurs in der Zone ist.
- Stattdessen: `CurrentTick.Price` (Live-Preis) der noch offenen Kerze verwenden.

**Prüfe:**
- Wird irgendwo auf `IsClosed` gewartet, bevor der Zonen-Check passiert?
- Wird der Live-Preis für Zonen-Checks verwendet?

#### 3.4 Zonen-Memory (Toleranz)
- Wenn 4H-Kurs das Golden Pocket erreicht: `IsZoneActive = true` setzen.
- Dieser Zustand muss für mindestens **10 Kerzen** (des jeweiligen TF) bestehen bleiben.
- Damit hat der 15m-Chart Zeit, sein Setup auszubilden.

**Prüfe:**
- Existiert ein Zähler/Timer für die Zonen-Memory?
- Wird er korrekt dekrementiert?
- Was passiert bei Ablauf? (Zone muss sauber deaktiviert werden)

#### 3.5 Fahrplan (Übergeordnete Marktrichtung)
- Vor jedem Trade muss ein Fahrplan existieren (Weekly/Daily-Bias: LONG oder SHORT).
- **Ohne Fahrplan = kein Trade.** Das ist der #1 Anfängerfehler im SK-System.
- Der Fahrplan bestimmt: In welche Richtung werden Sequenzen gesucht?
- Alle Sequenzen GEGEN den Fahrplan werden ignoriert (Ausnahme: bewusste Hedges).
- Fahrplan wird bei jeder neuen Daily-Kerze neu bewertet.

**Prüfe:**
- Existiert ein Fahrplan-Konzept im Code (MarketBias, DirectionalBias o.ä.)?
- Wird der Fahrplan vor der Sequenz-Suche geprüft?
- Werden Trades gegen den Fahrplan blockiert?
- Wird der Fahrplan periodisch aktualisiert?
- Siehe Stufe 4.1 für Details.

#### 3.6 Untersequenzen (Primär / Sekundär / Breakout)
- **Primäre Sequenz** (4H): Bestimmt Handelsrichtung und Ziellevel.
- **Sekundäre Sequenz** (1H): Bestätigt den primären Kaufbereich. Entsteht INNERHALB der primären Zone. Wenn sie die 100er Extension erreicht = starkes Signal.
- **Breakout-Sequenz**: Entsteht wenn eine Sequenz der Gegenrichtung invalidiert wird (z.B. bullisch bricht bärischen Punkt A). Besonders starkes Signal.

**Prüfe:**
- Unterscheidet der Code zwischen primären, sekundären und Breakout-Sequenzen?
- Werden sekundäre Sequenzen NUR innerhalb der primären Zone gesucht?
- Gibt es ein Enum/Tag `SequenceType { Primary, Secondary, Breakout }`?
- Wird eine sekundäre Sequenz mit 100er Extension als stärkeres Signal gewertet?
- Siehe Stufe 4.3 für Details.

#### 3.7 Sequenzaufbau-Typ (IKI, III, KIK, etc.)
- Jede Teilstrecke (0→A, A→B, B→C) wird als Impulsiv (I) oder Korrektiv (K) klassifiziert.
- Der Typ beeinflusst die Trefferquote und sollte in TP-Strategie/Confluence einfließen.
- **III** = stärkster Typ (aggressive TPs), **KKK** = schwächster (nur mit Confluence).

**Prüfe:**
- Werden Sequenztypen klassifiziert?
- Beeinflusst der Typ den Confluence-Score oder die TP-Level?
- Siehe Stufe 4.4 für Algorithmus und Tabelle.

#### 3.8 Entry-Regel 1: Impulsive Reaktion
- Markt kommt in Kaufbereich → reagiert IMPULSIV heraus → Entry im Korrekturlevel dieser Reaktion.
- Impulsiv = ≥3 Kerzen in Trendrichtung ODER 1 Kerze mit Body > 1.5× ATR.

**Prüfe:**
- Wird geprüft ob die Reaktion aus der Zone impulsiv war?
- Oder reicht jede beliebige Berührung der Zone für einen Entry?
- Siehe Stufe 4.5 für Details.

#### 3.9 Entry-Regel 2: 100er Extension als Gate
- Das BC-Korrekturlevel ist erst VALID, wenn die Sequenz die 100% Extension erreicht hat.
- Unter 100%: Sequenz noch nicht bestätigt → KEIN BC-Trade.
- Über 161.8%: BC ungültig → Wechsel zu GKL.
- Die 100er ist die wichtigste Validierungsschwelle im gesamten SK-System.

**Prüfe:**
- Wird die 100% Extension berechnet?
- Wird sie als Gate vor BC-Entries verwendet?
- Werden Trades OHNE 100% Extension blockiert?
- Siehe Stufe 4.7 für Extension-Tabelle.

#### 3.10 Dreh- und Wendebereiche
- Sequenzen dürfen nur an **validen Wendebereichen** entstehen, nicht irgendwo im Chart.
- Valid: BC/GKL abgearbeiteter Sequenzen, entgegengesetztes KL, Überlappungszonen, ATH/ATL.
- Invalid: Zufällige S/R-Linien, Sequenzen mitten in der Bewegung, nur auf 1 TF sichtbar.

**Prüfe:**
- Werden Sequenzen nur an Wendebereichen gestartet oder überall?
- Gibt es einen Wendebereich-Validator?
- Wird geprüft ob der Bereich auf mindestens 2 Timeframes sichtbar ist?
- Siehe Stufe 4.6 für Details.

#### 3.11 Daten-Flow zwischen Timeframes
- Daten fließen von oben nach unten: Weekly→4H→1H→15m (Bias, Zonen, Bestätigung).
- Und von unten nach oben (Feedback): 15m→1H→4H→Weekly (Trade-Ergebnis, Status-Updates).
- Die Timeframes dürfen NICHT isoliert voneinander arbeiten.

**Prüfe:**
- Wird der 15m-Trigger über die 4H-Kaufzone informiert?
- Wird nach einem Trade das Ergebnis an die höheren TFs zurückgemeldet?
- Oder arbeiten die TFs als isolierte Instanzen?
- Siehe Stufe 4.9 für Daten-Flow-Diagramm.

#### 3.12 Stabilisierungsphasen (Prestabilisation)
- In Wendebereichen stabilisiert sich der Preis oft seitwärts bevor er dreht (Liquiditätsaufbau).
- Eine Stabilisierung ist ein QUALITÄTSMERKMAL — nicht sofort traden bei erster Berührung der Zone.
- Erkennung über: Bollinger-Band-Squeeze, abnehmende Kerzenkörper, Range-Kontraktion, Dreiecksformation.
- Stabilisierung erhöht den Confluence-Score um +2.

**Prüfe:**
- Gibt es eine Stabilisierungs-Erkennung in Kaufzonen?
- Oder triggert der Bot bei erster Berührung?
- Wird ein Squeeze oder ähnliches Muster erkannt?
- Beeinflusst Stabilisierung den Confluence-Score oder Entry-Timing?
- Siehe Stufe 5.1 für Algorithmus mit 4 Erkennungsmethoden.

#### 3.13 Overtracing (Falsches Brechen von Leveln)
- Overtracing = Preis durchbricht ein Level nur kurz (Docht), schließt aber nicht darüber/darunter.
- Das ist KEIN echter Bruch, sondern Stop-Hunting/Liquiditäts-Jagd.
- **Kritisch bei 3 Stellen:**
  1. Punkt-A-Aktivierung: Docht über A ≠ Aktivierung (✅ bereits via Close-Regel)
  2. Punkt-0-Invalidierung: Docht unter 0 = WARNUNG, nicht sofort Invalidierung
  3. Entgegengesetzter Wendebereich: Overtracing ≠ Bruch, Long bleibt gültig
- Toleranz: 0.3× ATR — Bruch erst wenn Close > Level + Toleranz UND ≥2 Kerzen bestätigen.
- Neuer Status `WARNED` zwischen `ACTIVATED` und `INVALIDATED`.

**Prüfe:**
- Wird zwischen Docht-Bruch und Close-Bruch unterschieden (außer bei Aktivierung)?
- Gibt es eine Toleranz (ATR-basiert) bei Invalidierungen?
- Gibt es einen WARNED-Status oder nur binär valid/invalid?
- Wird der entgegengesetzte Wendebereich auf Overtracing geprüft?
- Siehe Stufe 5.2 für Implementierung und Toleranz-Logik.

#### 3.14 B-Punkt-Qualität (Hohes B vs. Tiefes B)
- Die Position des B-Punkts (wie viel % der 0→A Strecke korrigiert wurde) bestimmt die Setup-Qualität.
- **Hohes B** (nahe A, <38.2% Retracement): Schlechtes CRV, weiter SL → kleinere Position oder höhere Confluence nötig.
- **Ideales B** (38.2-66.7% Retracement): Bestes Verhältnis → Standard-Entry.
- **Tiefes B** (>66.7% Retracement): Sehr gutes CRV, enger SL → größere Position möglich, aber nahe an Punkt 0.
- **Hohes B + knapp gedochtete 100er Extension** = schwaches Setup → Skip oder Confluence ≥ 4.
- Die B-Qualität beeinflusst: Positionsgröße (Multiplier 0.5-1.25), Confluence-Anforderung, TP-Strategie.

**Prüfe:**
- Wird das B-Punkt-Retracement berechnet?
- Beeinflusst es die Positionsgröße?
- Wird ein CRV vor dem Trade geschätzt?
- Wird "knapp gedochtete 100er + hohes B" als Warnsignal erkannt?
- Siehe Stufe 5.3 für BPointAnalysis-Klasse und CRV-Berechnung.

---

### 4. Order-Management (`BingXRestClient.cs` oder Äquivalent)

#### 4.1 Einstieg: Limit-Retest
- **Kein Market Buy.** Immer LIMIT BUY knapp unter Live-Preis.
- Bei 15m-Trigger: Limit-Order senden.

#### 4.2 Stop-Loss Buffer
- SL **niemals** exakt auf Punkt 0 oder B.
- Formel: `SL = Point0.Price - (1.5 * ATR_15m)` (bullisch)
- Formel: `SL = Point0.Price + (1.5 * ATR_15m)` (bärisch)

**Prüfe:**
- Wird ATR des 15m-Charts verwendet (nicht 4H oder 1H)?
- Ist der Buffer-Faktor 1.5 konfigurierbar?

#### 4.3 Atomic Order Submission
- Entry, Stop-Loss (`STOP_MARKET`) und Take-Profit (`TAKE_PROFIT_MARKET`) müssen als **ein einziges JSON-Payload** an BingX gesendet werden.
- Der Bot überwacht Trades NICHT live — die Börse managed es.

**Prüfe:**
- Werden alle 3 Orders in einem API-Call gesendet?
- Oder werden sie einzeln gesendet (Race-Condition-Risiko)?

#### 4.4 Dezimalstellen-Bug (KRITISCH)
- Preise im JSON **MÜSSEN** als String mit Punkt (`.`) formatiert werden.
- **Zwingend:** `price.ToString(CultureInfo.InvariantCulture)`
- Auf deutschen Systemen wird sonst Komma (`,`) verwendet → API-Fehler / falsche Positionsgrößen.

**Prüfe:**
- Suche nach ALLEN `ToString()`-Aufrufen für Preise/Mengen in API-relevanten Klassen.
- Suche nach String-Interpolation (`$"{price}"`) von Dezimalzahlen in API-Payloads.
- Gibt es `CultureInfo.InvariantCulture` überall wo nötig?
- Suche auch in Hilfsmethoden und Extensions.

#### 4.5 Teilverkäufe (Scaling Out)
- **TP1:** Bei 161.8% Extension des 15m-Ziellevels → 50% verkaufen, SL auf Break-Even ziehen.
- **TP2:** Restliche 50% beim 4H-Ziellevel verkaufen.

**Prüfe:**
- Wird die Position tatsächlich halbiert (nicht komplett geschlossen)?
- Wird der SL nach TP1 auf Entry-Preis verschoben?
- Wird TP2 als separate Order gesetzt?

---

### 5. Risk-Management (`RiskManager.cs` oder Äquivalent)

#### 5.1 Seitwärts-Filter (ADX/Choppiness)
- ADX-Indikator implementieren.
- ADX < 25 im 1H oder 4H → Markt richtungslos → **keine Neueinstiege**.

**Prüfe:**
- Ist ADX implementiert (nicht nur als Platzhalter)?
- Welche Periode? (Standard: 14)
- Wird auf beiden Timeframes (1H UND 4H) geprüft?

#### 5.2 Positions-Limits
- Max **1 Trade pro Symbol** (kein Positions-Kloning).
- Max **3 offene Trades** portfolio-weit.

**Prüfe:**
- Werden offene Positionen VOR neuem Entry geprüft?
- Wird gegen die BingX-API abgeglichen (nicht nur lokaler State)?

#### 5.3 Cool-Down-Timer
- Nach Stop-Loss: Symbol für **4 Stunden** (= eine 4H-Kerze) sperren.
- Keine Rache-Trades.

**Prüfe:**
- Existiert ein Cooldown-Mechanismus (z.B. `Dictionary<string, DateTime>`)?
- Wird er bei jedem neuen Signal geprüft?
- Überlebt der Cooldown einen App-Neustart (Persistenz in DB)?

#### 5.4 BingX Notional Value
- Berechnete Positionsgröße muss BingX-Minimum überschreiten (~5 USDT).
- Wenn nicht → Trade überspringen, NICHT auf Minimum aufrunden.

#### 5.5 Maximales Risiko pro Trade
- Max 1% des Kontoguthabens pro Trade.
- Positionsgröße = `(Kontoguthaben * 0.01) / (Entry - SL)` (vereinfacht).

**Prüfe:**
- Wird das aktuelle Kontoguthaben von der API abgefragt?
- Wird Leverage in die Berechnung einbezogen?

---

### 6. Infrastruktur & Ausfallsicherheit

#### 6.1 State Recovery (Amnesie-Schutz)
- Jeder Statuswechsel einer Sequenz wird in SQLite oder JSON-DB persistiert.
- Bei Neustart: DB lesen und nahtlos weitermachen.

**Prüfe:**
- Welche DB wird verwendet?
- Werden ALLE relevanten States gespeichert? (Sequenz-Status, offene Zonen, Cooldowns, Trailing-Werte, IsZoneActive-Timer)
- Gibt es einen Recovery-Pfad im Startup-Code?

#### 6.2 Orphaned Orders Cleanup
- Wenn Trade geschlossen wird (TP oder SL): **zuerst** `CancelAllOrders(Symbol)` an BingX senden.
- Verhindert Geister-Orders im Orderbuch.

**Prüfe:**
- Wird Cleanup bei JEDEM Trade-Ende aufgerufen?
- Auch bei manueller Schließung über die Börse (Webhook/Polling)?

#### 6.3 Isolated Margin
- Beim App-Start: API-Befehl senden, um ALLE Handelspaare auf `ISOLATED` zu stellen.
- Niemals Cross-Margin.

**Prüfe:**
- Passiert das beim Start automatisch?
- Was passiert bei API-Fehler? (Muss Retry oder Abort sein, nicht ignorieren)

---

## Ausgabe-Format

Erstelle nach der Analyse eine Datei `SK_VERIFY_REPORT.md` mit folgendem Format:

```markdown
# SK-System Verifikationsbericht

## Zusammenfassung
- ✅ X Regeln korrekt implementiert
- ⚠️ X Regeln mit Abweichungen
- ❌ X Regeln fehlen komplett

## Detail-Prüfung

### [Regel-ID] [Regelname]
**Status:** ✅ / ⚠️ / ❌
**Datei:** `Dateiname.cs`, Zeile XX-YY
**Befund:** ...
**Fix:** (konkreter Code-Vorschlag falls nötig)
```

---

## PRIORITÄT: Trade-Frequenz-Fix-Plan

Der Bot generiert deutlich weniger Trades als manuelles Trading. Ursache ist eine Kette von 7 Blockern, die sich gegenseitig verstärken. Dieser Plan löst sie in der richtigen Reihenfolge (Abhängigkeiten beachten).

### Warum der Bot zu wenig tradet — Die 7 Blocker

```
Rohe Signale aus dem Markt
    ↓
[Blocker 1] Statische BC-Zone          → Preis trifft Zone nie, weil sie nicht mitwandert
[Blocker 2] GKL auf falscher Basis     → Zone systematisch am falschen Ort
[Blocker 3] Nur 1 Sequenz pro Richtung → Bessere neue Sequenzen werden ignoriert
[Blocker 4] Confluence-Score ≥ 3       → Zu harter Filter, manuell reichen 1-2 Faktoren
[Blocker 5] Kein Re-Entry nach BC-Fail → Gescheiterte Trades = Symbol tot
[Blocker 6] Kein Trailing High         → BC-Zone kann gar nicht dynamisch sein
[Blocker 7] 38.2% fehlt               → Zu schwache Sequenzen aktiviert → Preis schafft BC nie
    ↓
Kaum Trades übrig
```

### Fix-Reihenfolge (STRIKT einhalten — Abhängigkeiten!)

```
Phase 1: Kontoschutz (SOFORT, vor allen anderen Änderungen)
├── Fix A: Isolated Margin setzen [Regel 6.3]
│   └── EINZEILER: SetMarginTypeAsync(symbol, Isolated) vor jeder Order
│   └── GRUND: Ohne das kann ein Bug bei den folgenden Änderungen das Konto liquidieren
│
Phase 2: Fundament reparieren (Kernlogik der Zonen)
├── Fix B: Trailing High/Low einbauen [Regel 2.1]
│   ├── CurrentHigh/CurrentLow Properties in SequenceStateMachine
│   ├── Bei jeder Kerze in ProcessAktiviert() aktualisieren
│   ├── Bei Aktivierung in TryActivate() initialisieren
│   └── KEIN Trade-Effekt allein — ist Voraussetzung für Fix C
│
├── Fix C: BC-Zone dynamisch machen [Regel 2.2] ← BRAUCHT Fix B
│   ├── GetBcZone() neu: Fib von PointB bis CurrentHigh (nicht 0→A)
│   ├── Zone-Grenzen: 50.0% bis 66.7% Retracement
│   ├── Zone muss bei jedem CurrentHigh-Update neu berechnet werden
│   ├── ERWARTETER EFFEKT: +40-60% mehr Zone-Treffer
│   └── ⚠️ VORSICHT: Alte statische IsInBuyZone() muss komplett ersetzt werden
│
├── Fix D: GKL-Berechnung korrigieren [Regel 2.3]
│   ├── Basis ändern: Strecke = Extension1618 bis Point0 (nicht A bis Point0)
│   ├── Bullisch: gklTop = Extension1618 - range*0.500, gklBottom = Extension1618 - range*0.667
│   ├── Bärisch: gklBottom = Extension1618 + range*0.500, gklTop = Extension1618 + range*0.667
│   └── ERWARTETER EFFEKT: GKL-Trades treffen jetzt die richtige Zone
│
Phase 3: Mehr Signale durchlassen
├── Fix E: 38.2%-Mindestaktivierung einbauen [Regel 3b.1]
│   ├── Nach Aktivierung prüfen: Hat Preis mindestens 38.2% Extension erreicht?
│   ├── Wenn nicht: Sequenz bleibt "PENDING", nicht "ACTIVATED"
│   ├── PARADOXER EFFEKT: Weniger Aktivierungen, aber BESSERE → mehr davon treffen BC
│   └── Filtert schwache Sequenzen raus, die sowieso nie die BC-Zone erreichen
│
├── Fix F: Re-Entry nach gescheitertem BC via GKL [Regel 3b.3]
│   ├── Wenn BC-Trade gestoppt wird: GKL der Sequenz berechnen
│   ├── GKL = Fib 50%-66.7% von Point0 bis CurrentHighBeforeFail
│   ├── Neue Limit-Order in GKL-Zone setzen (attraktiverer Preis, engerer SL)
│   └── ERWARTETER EFFEKT: Gescheiterte Trades bekommen zweite Chance
│
├── Fix G: Cooldown-Timer einbauen [Regel 5.3]
│   ├── Dictionary<string, DateTime> für Symbol-Sperren
│   ├── 4 Stunden nach SL → Symbol gesperrt
│   ├── In DB persistieren für App-Neustart
│   └── GRUND: Schützt vor Rache-Trades bei den jetzt häufigeren Signalen
│
Phase 4: Kapazität erhöhen
├── Fix H: Mehrere Sequenzen pro Symbol/Richtung [Regel 3b.5]
│   ├── SequenceStateMachine: List<Sequence> statt einzelne Variable
│   ├── Jede Sequenz braucht eindeutige ID und Lifecycle-Status
│   ├── Bei neuem ZigZag-Pivot: Alle aktiven Sequenzen aktualisieren
│   ├── AUFWAND: Groß — tiefgreifendes Refactoring der State Machine
│   └── ERWARTETER EFFEKT: +20-30% mehr gleichzeitige Opportunities
│
├── Fix I: Confluence-Score-Schwelle überprüfen [Regel 3b.4]
│   ├── Aktuell: Minimum 3 Punkte aus ~8 Kategorien
│   ├── PRÜFEN: Wie viele Signale werden NUR wegen Score < 3 blockiert?
│   │   → Logging einbauen: Log.Info($"Signal blocked: score {score}, needed 3")
│   ├── WENN >50% der Signale geblockt: Schwelle auf 2 senken
│   ├── ALTERNATIV: Score-Gewichtung anpassen (BC-Zone + 1H-Bestätigung = schon 2)
│   └── ⚠️ NICHT blind senken — erst Daten sammeln!
│
Phase 5: Feintuning (erst nach 50+ Trades mit Phase 1-4)
├── Fix J: ExitState-Persistenz [Regel 6.1]
├── Fix K: Orphaned Orders bei manueller Schließung [Regel 6.2]
├── Fix L: Default-Werte anpassen (MaxOpenPositions 10→3, Risk 2%→1%) [Regel 5.2, 5.5]
└── Fix M: Gescheiterte → Größere Sequenz [Regel 3b.2]
```

### Erwarteter Gesamt-Effekt

| Phase | Änderung | Trade-Frequenz-Effekt |
|-------|----------|----------------------|
| Phase 1 | Isolated Margin | Kein Effekt auf Frequenz, aber überlebensnotwendig |
| Phase 2 | Dynamische BC + korrektes GKL | **+50-80% mehr Trades** (Zonen werden getroffen) |
| Phase 3 | 38.2% Filter + Re-Entry + Cooldown | **+15-25% netto** (bessere Qualität + zweite Chancen) |
| Phase 4 | Mehrere Sequenzen + Score-Tuning | **+20-40% mehr Trades** (mehr Gelegenheiten parallel) |
| Phase 5 | Stabilität + Feintuning | Kein Frequenz-Effekt, aber weniger Geldverlust durch Bugs |

### Wichtige Regeln für die Umsetzung

1. **NACH JEDER PHASE: Mindestens 24h im Simulations-Modus laufen lassen** bevor die nächste Phase beginnt
2. **Logging ist Pflicht:** Jeder blockierte Trade muss geloggt werden mit GRUND
3. **Phase 2 ist das Herzstück:** Wenn Fix B+C+D korrekt sind, sollte der Bot sofort merklich mehr traden
4. **Phase 4 (Fix H) ist optional:** Nur angehen wenn Phase 2+3 nicht ausreichen
5. **Fix I (Confluence-Score) NIEMALS ohne Daten:** Erst 50+ Trades loggen, dann entscheiden
6. **Goldene Regel:** Nach Änderungen den Code in Blöcken von 50 Trades evaluieren, NICHT nach 5 Verlusten anpassen

---

## CODE-ANALYSE: 38 Blocking-Points in SequenzKonzeptStrategy.cs

Die Strategie-Datei `SequenzKonzeptStrategy.cs` (~600 Zeilen) enthält **38 `return Blocked()`-Stellen**, die ein Signal verhindern können. Zusätzlich blockiert `MarketFilter.cs` global. Jeder Blocking-Point ist für sich vielleicht sinnvoll, aber die Kette tötet fast alle Trades.

### Vollständige Blocking-Kette (in Reihenfolge der Evaluierung)

```
Signal-Eingang (jede 15m-Kerze pro Symbol)
    │
    ├── [1]  Zu wenig H4-Daten → Blocked
    ├── [2]  Flash-Crash Cooldown aktiv → Blocked (4 Evaluierungen)
    ├── [3]  4H ADX < Schwelle → Blocked (Seitwärtsmarkt)
    ├── [4]  Fahrplan: Preis unter EMA-200 bei Long → Blocked ←── KILLER
    ├── [5]  4H keine Sequenz gefunden → NoSignal
    ├── [6]  4H Sequenz nicht konstruierbar → Blocked
    ├── [7]  4H Range zu groß (Range-Markt) → Blocked
    ├── [8]  4H Sequenz zu klein (< 2× ATR) → Blocked
    ├── [9]  4H Sequenz abgearbeitet (161.8%) → Blocked + 20 Kerzen Richtungs-Sperre ←── KILLER
    ├── [10] Richtungs-Sperre aktiv + keine Gegensequenz → Blocked ←── KILLER
    ├── [11] Sequenztyp nicht handelbar → Blocked
    ├── [12] Sandwich (Entry im Ziellevel aktiver Gegensequenz) → Blocked
    ├── [13] Fahrplan gegen Bias → Blocked ←── KILLER
    ├── [14] Bottom-Up 3+ Verluste in gleicher Richtung → Blocked ←── KILLER
    ├── [15] 4H identische Sequenz bereits gehandelt → Blocked ←── KILLER #1
    │
    ├── [16] 1H ADX < Schwelle → Blocked
    ├── [17] 1H aktive Gegensequenz → Blocked
    ├── [18] 1H ChoCH gegen Richtung → Blocked
    │
    ├── [19] 15m keine Daten → Blocked
    ├── [20] 15m keine Micro-Sequenz → Blocked
    ├── [21] 15m Richtung passt nicht zu 4H → Blocked
    ├── [22] 15m nicht konstruierbar → Blocked
    ├── [23] 15m nicht aktiviert (State < Aktiviert) → Blocked
    ├── [24] 15m 38.2% Extension nicht erreicht → Blocked
    ├── [25] 15m ChoCH gegen Richtung → Blocked
    ├── [26] 15m Sequenz zu klein (< 2× ATR) → Blocked
    ├── [27] 15m über 100% Extension → Blocked (Fenster verpasst) ←── KILLER #3
    ├── [28] 15m 100% Extension noch nicht erreicht → Blocked ←── KILLER #3
    ├── [29] 15m Aktivierung nicht impulsiv → Blocked ←── KILLER #2
    │
    ├── [30] Signal bereits gesendet (Deduplizierung) → Blocked
    ├── [31] Whipsaw-Cooldown → Blocked
    │
    ├── [32] SL auf falscher Seite → Blocked/Fallback
    ├── [33] SL-Distanz zu klein → Blocked
    ├── [34] SL-Distanz zu groß → Blocked
    ├── [35] RRR < 3:1 → Blocked ←── KILLER #4
    │
    ├── [36] Confluence-Score < 3 → Blocked
    │
    │   ─── Zusätzlich VOR der Strategie (MarketFilter.cs) ───
    ├── [37] BTC Health Score ≤ -2 → Alle Longs blockiert ←── KILLER #6
    ├── [38] Funding-Settlement (5min Pause alle 8h) → Blocked
    │
    ▼
    SIGNAL (kommt fast nie an)
```

### Die 8 Killer im Detail (mit Datei + Zeile + Fix)

#### KILLER #1: 4H-Sequenz Deduplizierung
**Datei:** `SequenzKonzeptStrategy.cs`, ca. Zeile 280
**Problem:** `_lastH4SeqPointA == h4Machine.PointA && _lastH4SeqLockedB == h4Machine.LockedB` → Blocked.
Eine 4H-Sequenz kann **Tage bis Wochen** aktiv sein. Der Bot handelt sie genau EIN MAL. Danach ist sie tot — egal wie viele perfekte 15m-Entries sich darin bilden.

**Im echten SK-System:** Ein Trader nimmt in derselben 4H-Sequenz mehrere Entries. Beispiel: BC-Trade gestoppt → neuer Entry am GKL. Oder: TP1 genommen → neuer Entry im nächsten 15m-Pullback.

**Fix:**
```csharp
// ALT: Komplett-Block bei gleicher 4H-Sequenz
if (_lastH4SeqPointA == h4Machine.PointA && _lastH4SeqLockedB == h4Machine.LockedB)
    return Blocked("4H: Identische Sequenz bereits gehandelt");

// NEU: Nur blocken wenn der letzte 15m-TRIGGER identisch war
// Die 4H-Sequenz darf mehrere 15m-Entries generieren
// Deduplizierung läuft über die 15m-Punkte (A/B/C), nicht über 4H
// → Den 4H-Dedup-Check komplett entfernen oder auf Time-Lock umstellen:
if (_lastH4SeqPointA == h4Machine.PointA && _lastH4SeqLockedB == h4Machine.LockedB
    && _signalCooldown > 0)  // Nur kurzzeitig blocken (8 Evaluierungen ≈ 2h)
    return Blocked("4H: Identische Sequenz, Cooldown aktiv");
// → Nach Cooldown-Ablauf: Gleiche 4H-Sequenz darf neuen 15m-Trigger generieren
```

#### KILLER #2: Impulsive Aktivierung zu streng
**Datei:** `SequenzKonzeptStrategy.cs`, ca. Zeile 370
**Problem:** Letzten 5 Kerzen brauchen ≥3 Trend-Kerzen ODER 1 Body > 1.5× ATR.
Nach einer Stabilisierungsphase (Seitwärts in der Zone) startet der Ausbruch oft langsam — 2 Trend-Kerzen, dann 1 Gegen-Kerze, dann wieder hoch. Das ist trotzdem ein valider SK-Entry, aber der Bot blockt.

**Fix:**
```csharp
// ALT: Nur letzte 5 Kerzen prüfen — zu kurzes Fenster
var lastCandles = m15Candles.Skip(Math.Max(0, m15Candles.Count - 5)).ToList();

// NEU: Fenster auf 8 Kerzen erweitern (2h bei 15m)
// UND: Methode 3 ergänzen: Netto-Bewegung > 1× ATR
var lastCandles = m15Candles.Skip(Math.Max(0, m15Candles.Count - 8)).ToList();

// Methode 3: Netto-Bewegung der letzten 8 Kerzen > 1× ATR
if (!activationImpulsive)
{
    var nettoMove = Math.Abs(lastCandles[^1].Close - lastCandles[0].Open);
    if (nettoMove > m15AtrValue * 1.0m)
        activationImpulsive = true;
}

// Methode 4: Stabilisierung erkannt → Impuls-Check überspringen
// (Stabilisierung IST die Bestätigung, der Ausbruch danach ist per Definition valide)
if (!activationImpulsive && stabilisationScore >= 3)
    activationImpulsive = true;
```

#### KILLER #3: 100er Extension Fenster zu eng
**Datei:** `SequenzKonzeptStrategy.cs`, ca. Zeile 350+360
**Problem:** Zwei gegenläufige Checks:
- `Has100ExtensionReached == false` → Blocked ("noch nicht erreicht")
- `currentPrice > Extension100` → Blocked ("Fenster verpasst")

Bei Krypto kann der Preis die 100er in einer 15m-Kerze durchschießen und direkt darüber schließen. Der Bot sieht erst "nicht erreicht", dann in der nächsten Evaluierung "bereits darüber" → Entry-Fenster war effektiv 0 Sekunden.

**Fix:**
```csharp
// ALT: Über 100% = sofort tot
if (m15OverExt)
    return Blocked("KILL: 15m über 100% Extension — Entry-Fenster verpasst");

// NEU: Über 100% ist okay, solange unter 138.2% (BC-Zone ist noch valid!)
// Die 100er muss ERREICHT worden sein (Has100ExtensionReached),
// aber der Preis darf dort sein/darüber — das BC kommt als KORREKTUR DANACH
var m15Ext1382 = tradeIsLong
    ? m15Machine.LockedB + m15BCRange * 1.382m
    : m15Machine.LockedB - m15BCRange * 1.382m;
var tooFar = tradeIsLong
    ? currentPrice > m15Ext1382
    : currentPrice < m15Ext1382;
if (tooFar)
    return Blocked("KILL: 15m über 138.2% Extension — zu weit für BC-Entry");

// Has100ExtensionReached prüfen: Muss irgendwann ≥100% gewesen sein
// (nicht "jetzt gerade" — sondern historisch seit Aktivierung)
if (!m15Machine.Has100ExtensionReached)
    return Blocked("15m: 100% Extension noch nicht erreicht — BC-Zone noch nicht valid");
// → Kein "über 100% = verpasst" Check mehr. Das BC-Level kommt als Rücksetzer.
```

#### KILLER #4: RRR-Minimum 3:1 zu hoch
**Datei:** `SequenzKonzeptStrategy.cs`, ca. Zeile 430
**Problem:** RRR wird auf TP2 (4H Extension) berechnet. Aber wenn die 4H-Sequenz schon fortgeschritten ist (z.B. Preis hat 80% des 4H-Ziels erreicht), ist der verbleibende TP2-Weg kurz → RRR < 3 → kein Trade.

**Fix:**
```csharp
// ALT: Hartes 3:1 Minimum
if (rrr < 3m)
    return Blocked($"RRR zu klein ({rrr:F1}:1 < 3:1)");

// NEU: Gestuftes RRR basierend auf Confluence-Score
// Hoher Score = höhere Trefferquote → niedrigeres RRR akzeptabel
var minRrr = score switch
{
    >= 8 => 1.5m,  // Sehr hohe Confluence → 1.5:1 reicht
    >= 6 => 2.0m,  // Gute Confluence → 2:1
    >= 4 => 2.5m,  // Mittlere Confluence → 2.5:1
    _    => 3.0m   // Niedrige Confluence → 3:1 bleibt
};
if (rrr < minRrr)
    return Blocked($"RRR zu klein ({rrr:F1}:1 < {minRrr:F1}:1 bei Score={score})");
```

#### KILLER #5: Bottom-Up 3 Verluste = Richtungs-Tod
**Datei:** `SequenzKonzeptStrategy.cs`, ca. Zeile 270
**Problem:** `_consecutiveFailsInDirection >= 3` → KOMPLETTER Block der Richtung bis zum nächsten Gewinn. Aber Gewinne brauchen Trades → Deadlock.

Besonders problematisch: Die Variable wird NICHT persistiert (siehe Verify-Report 6.1). Bei Neustart = Reset auf 0. Also willkürliches Verhalten.

**Fix:**
```csharp
// ALT: Harter Block nach 3 Verlusten
if (_consecutiveFailsInDirection >= 3)
    return Blocked("Bottom-Up: Richtung erschöpft");

// NEU: Statt Block → Confluence-Anforderung erhöhen
// 3 Verluste: Min-Confluence von 3 auf 5 erhöhen
// 5 Verluste: Min-Confluence auf 7 (quasi unmöglich → effektiver Block)
var adjustedMinConfluence = _minConfluence;
if (_consecutiveFailsInDirection >= 3)
    adjustedMinConfluence = _minConfluence + _consecutiveFailsInDirection - 1;
// Weiter unten: score < adjustedMinConfluence statt score < _minConfluence
// → Erlaubt immer noch Trades mit extrem hoher Confluence
```

#### KILLER #6: BTC Health Score blockiert ALLES
**Datei:** `MarketFilter.cs`, Zeile ~35-75
**Problem:** `AllowLong = score >= -1`. Bei BTC-Score -2 (z.B. BTC unter EMA50 + Supertrend bärisch) sind ALLE Longs für ALLE Coins blockiert. Auch wenn SOL oder ETH eigene bullische Strukturen haben.

**Fix:**
```csharp
// ALT: Harter Block
var allowLong = score >= -1;

// NEU: BTC-Health als Confluence-Malus statt harter Block
// Score -2: Confluence -2 (statt Block)
// Score -3: Confluence -3
// Score -4: DANN harter Block (extremer BTC-Crash)
var allowLong = score >= -3; // Nur bei extremem Crash blockieren
// Im SequenzKonzeptStrategy: btcHealthScore als Confluence-Offset einbauen
// if (btcHealthScore < 0) score += btcHealthScore; // Negativ = Malus
```

#### KILLER #7: Abgearbeitet = 20 Kerzen Richtungs-Sperre
**Datei:** `SequenzKonzeptStrategy.cs`, ca. Zeile 200
**Problem:** Nach 161.8% oder 200%: `_completedCooldown = 20` + Richtungs-Sperre. Gegensequenz ins GKL wird gesucht, aber der Check ist sehr streng (Gegensequenz muss im GKL der alten landen).

20 Evaluierungen bei 15m-Scan = 5 Stunden. In dieser Zeit werden ALLE Signale in dieser Richtung für dieses Symbol blockiert.

**Fix:**
```csharp
// ALT: 20 Evaluierungen Sperre
_completedCooldown = 20;

// NEU: Zeitbasiert statt Evaluierungs-basiert (konsistenter)
// Und: Kürzere Sperre, dafür GKL als primären Entry nutzen
_completedCooldown = 8; // 8 Evaluierungen ≈ 2h (statt 5h)
// UND: Bei Ablauf der Sperre → Richtung wieder freigeben (nicht warten auf Gegensequenz)
// Die Gegensequenz-Suche ist ein BONUS, kein Gate
```

#### KILLER #8: 15m muss sofort gleiche Richtung wie 4H haben
**Datei:** `SequenzKonzeptStrategy.cs`, ca. Zeile 330
**Problem:** `m15Machine.IsLong != tradeIsLong → Blocked`. Wenn der 15m-Chart gerade eine Short-Micro-Sequenz hat während 4H Long will → sofort Blocked. Kein Warten auf die nächste 15m-Long-Sequenz.

Das ist besonders schlimm weil die 15m-State Machine nur EINE Sequenz tracked. Wenn die gerade zufällig Short ist → nächste Chance erst wenn die State Machine auf Long dreht.

**Fix:**
```csharp
// ALT: Sofort Blocked bei falscher Richtung
if (m15Machine.IsLong != tradeIsLong)
    return Blocked("15m: gegen 4H-Richtung");

// NEU: Zusätzlich nach aligned Sequenz suchen (nicht nur SM-Primärsequenz)
if (m15Machine.IsLong != tradeIsLong)
{
    // Versuche eine 15m-Sequenz in der richtigen Richtung zu finden
    var m15AltSeq = SequenceDetector.DetectSequence(
        m15Candles, _m15SwingStrength, effectiveMinRange * 0.3m, false);
    if (m15AltSeq != null && m15AltSeq.IsLong == tradeIsLong
        && m15AltSeq.State is SequenceState.Active or SequenceState.WaitingBreak)
    {
        microSeq = m15AltSeq; // Alternative verwenden
        // Weiter mit dem Rest der Evaluierung
    }
    else
    {
        return Blocked("15m: Keine Micro-Sequenz in 4H-Richtung");
    }
}
```

### Zusammenfassung: Erwarteter Effekt der Killer-Fixes

| Killer | Fix | Erwarteter Trade-Zuwachs |
|--------|-----|--------------------------|
| #1 4H-Dedup | Nur 15m-Dedup + Time-Lock statt 4H-Block | **+100-200%** (größter Einzeleffekt) |
| #2 Impuls-Check | Fenster erweitern + Stabilisierung = Bypass | +20-30% |
| #3 100er Fenster | Bis 138.2% erlauben statt nur genau 100% | +15-25% |
| #4 RRR 3:1 | Gestuftes RRR nach Confluence | +10-20% |
| #5 Bottom-Up Block | Confluence-Erhöhung statt Block | +5-10% |
| #6 BTC Health | Confluence-Malus statt Block | +10-20% (bei BTC-Bärenmärkten) |
| #7 Abgearbeitet 20 Kerzen | Auf 8 reduzieren + GKL als Entry | +10-15% |
| #8 15m Richtung | Alternative Sequenz suchen | +15-25% |

**Wichtig:** Diese Fixes müssen in die Phase 2/3 des Trade-Frequenz-Fix-Plans integriert werden. NICHT alle gleichzeitig einbauen. Reihenfolge:
1. **Killer #1** (4H-Dedup) — sofort, größter Effekt
2. **Killer #3** (100er Fenster) — sofort, logischer Bug
3. **Killer #8** (15m Alternative) — Phase 2
4. **Killer #2** (Impuls-Check) — Phase 3
5. **Killer #4, #5, #6, #7** — Phase 4 (Feintuning)

---

## CODE-ANALYSE: SK-System-Abweichungen in Sequence.cs & SequenceStateMachine.cs

Zusätzlich zu den 38 Blocking-Points gibt es strukturelle Abweichungen vom SK-System in den Kern-Modellen. Diese betreffen nicht die Trade-Frequenz, sondern die **Korrektheit der Zonen und Signale**.

### ABWEICHUNG #1: BuyZone ist 50-61.8% statt SK's 50-66.7%
**Datei:** `Sequence.cs`, `IsInBuyZone()`
**Problem:** 
```csharp
// AKTUELL:
public bool IsInBuyZone(decimal price)
{
    var (upper, lower) = IsLong
        ? (Retracement500, Retracement618)  // 50% bis 61.8%
        : (Retracement618, Retracement500);
    return price >= Math.Min(upper, lower) && price <= Math.Max(upper, lower);
}
```
SK-System definiert das Golden Pocket als **50% bis 66.7%** — NICHT 61.8%. Das 66.7er ist im SK-System DAS Schlüssel-Level. Der Bot verpasst jeden Entry der zwischen 61.8% und 66.7% liegt. Laut SK reagiert der Markt am häufigsten genau am 66.7er.

**Fix:**
```csharp
// KORREKT nach SK-System:
var (upper, lower) = IsLong
    ? (Retracement500, Retracement667)  // 50% bis 66.7%
    : (Retracement667, Retracement500);
```

### ABWEICHUNG #2: GKL-Zone beginnt bei 55.9% statt 50%
**Datei:** `Sequence.cs`, `IsInGklZone()`, `GklZone` Property
**Problem:**
```csharp
// AKTUELL:
public (decimal Upper, decimal Lower) GklZone => IsLong
    ? (Retracement559, Retracement667)  // 55.9% bis 66.7%
    : (Retracement667, Retracement559);
```
55.9% ist ein nicht-standardmäßiges Fibonacci-Level. Im SK-System ist das GKL **50% bis 66.7%** — identisch mit dem Golden Pocket der 0→A Strecke. Der obere Teil der Zone (50-55.9%) wird komplett ignoriert → Entries verpasst.

**Fix:**
```csharp
// KORREKT nach SK-System:
public (decimal Upper, decimal Lower) GklZone => IsLong
    ? (Retracement500, Retracement667)  // 50% bis 66.7%
    : (Retracement667, Retracement500);

// IsInGklZone() analog anpassen:
public bool IsInGklZone(decimal price)
{
    var min = Math.Min(Retracement500, Retracement667);
    var max = Math.Max(Retracement500, Retracement667);
    return price >= min && price <= max;
}
```

**Hinweis:** `Retracement559` kann als zusätzliches Confluence-Level beibehalten werden (z.B. Preis genau am 55.9er → Score +1), aber es darf NICHT die Zone-Grenze sein.

### ABWEICHUNG #3: Fahrplan per EMA-200 widerspricht SK-Fahrplan
**Datei:** `SequenzKonzeptStrategy.cs`, ca. Zeile 130-145
**Problem:**
```csharp
// AKTUELL:
_lastFahrplanBias = currentPrice > _lastEma200; // true=Long, false=Short
// ...
if (_lastFahrplanBias.HasValue && _lastFahrplanBias.Value != tradeIsLong)
    return Blocked("Fahrplan: gegen Bias");
```
SK's Fahrplan basiert auf **Marktstruktur**, nicht auf einem Moving Average:
- Wo steht der Preis relativ zu ATH/ATL? (BLASH: Buy Low, Sell High)
- Gibt es offene GKLs von abgearbeiteten übergeordneten Sequenzen?
- Gibt es aktive übergeordnete Sequenzen mit Zielleveln?

EMA-200 ist ein Trendfolge-Indikator. Er sagt "kaufe was steigt" (Momentum). SK sagt genau das Gegenteil: "kaufe was tief steht, verkaufe was hoch steht" (Mean Reversion an Wendebereichen).

**Konkretes Beispiel:** BTC fällt von 70k auf 30k. SK sagt: "Sehr tief, bullische Entries suchen!" EMA-200 sagt: "Preis unter EMA → Short-Bias". Bot blockiert ALLE Longs genau dann wenn SK die besten Long-Setups sieht.

**Fix:**
```csharp
// Option A: EMA-200 als SOFT-Filter (Confluence) statt HARD-Block
if (_lastFahrplanBias.HasValue && _lastFahrplanBias.Value != tradeIsLong)
{
    // Gegen EMA-Bias: Confluence-Malus statt Block
    // score -= 1; // (weiter unten im Confluence-Score)
    // NICHT blockieren — SK-System tradet bewusst gegen den Trend (an Wendebereichen)
}

// Option B: Strukturbasierter Fahrplan (korrekt nach SK)
// 1. Prüfe ob Preis in den unteren 30% der historischen Range → Long-Bias
// 2. Prüfe ob Preis in den oberen 30% → Short-Bias
// 3. Prüfe ob übergeordnete abgearbeitete Sequenz ein offenes GKL hat → Bias Richtung GKL
var historicRange = h4Candles.Max(c => c.High) - h4Candles.Min(c => c.Low);
var pricePosition = (currentPrice - h4Candles.Min(c => c.Low)) / historicRange;
// pricePosition 0.0 = ganz unten, 1.0 = ganz oben
var structuralBias = pricePosition switch
{
    < 0.30m => true,   // Tief → Long-Bias (BLASH)
    > 0.70m => false,  // Hoch → Short-Bias (BLASH)
    _ => (bool?)null    // Mitte → Neutral
};
```

### ABWEICHUNG #4: Naming-Chaos SM vs. Sequence (Bug-Risiko)
**Dateien:** `SequenceStateMachine.cs` + `Sequence.cs`
**Problem:**
```
State Machine:     Sequence.cs:      SK-System:
  Point0      →      PointA      =    Punkt 0 (Ursprung)
  PointA      →      PointB      =    Punkt A (Impulsgipfel)
  LockedB     →      PointC      =    Punkt B (Korrekturende)
```
Die Variablennamen in den zwei Klassen sind **invertiert**. `Point0` in der SM wird zu `PointA` in der Sequence. Das ist extrem verwirrend und jeder Entwickler der den Code liest wird Fehler machen.

Der Kommentar in der Strategy bestätigt das Problem:
```csharp
// Log mit SK-Original-Nomenklatur: 0=Ursprung, A=Impulsgipfel, B=Korrekturpunkt
// (Sequence.PointA=SM.Point0=SK:0, Sequence.PointB=SM.PointA=SK:A, LockedB=SK:B)
```

**Fix (langfristig):** Sequence.cs umbenennen auf SK-Nomenklatur:
```csharp
// NEU:
public required SwingPoint Point0 { get; init; }  // Ursprung (war: PointA)
public required SwingPoint PointA { get; init; }   // Impulsgipfel (war: PointB)
public SwingPoint? PointB { get; init; }           // Korrekturende (war: PointC)
```
**Achtung:** Das ist ein Breaking Change für ALLE Stellen die Sequence verwenden. Nur mit Find+Replace über das gesamte Projekt machen. Mittlerer Aufwand, aber verhindert zukünftige Bugs.

### ABWEICHUNG #5: FromCandlesBoth unterdrückt eine Richtung
**Datei:** `SequenceStateMachine.cs`, `FromCandlesBoth()`
**Problem:** Beide Richtungen (Long + Short) laufen parallel, aber nur EINE wird als `primary` zurückgegeben. In der Strategy wird dann nur `primary` weiterverarbeitet (Zeile ~170: `var h4Seq = h4Machine.ToSequence(h4Candles)`).

Wenn die Short-Machine weiter fortgeschritten ist als die Long-Machine, wird Short als primary gewählt. Die Long-Machine wird nur für den Sandwich-Check verwendet — ihre Signale werden **nie gehandelt**.

Das ist besonders schlimm wenn der Fahrplan Long sagt, aber die Short-Machine zufällig "weiter" ist (z.B. SM.State = Aktiviert vs. SucheB). Dann wird Short als primary gewählt → Fahrplan blockt Short → kein Trade. Obwohl eine valide Long-Sequenz existiert.

**Fix:**
```csharp
// In SequenzKonzeptStrategy.cs, nach FromCandlesBoth():

// AKTUELL: Nur primary nutzen
var h4Seq = h4Machine.ToSequence(h4Candles);

// NEU: Wenn primary gegen Fahrplan → andere Richtung versuchen
if (_lastFahrplanBias.HasValue && h4Machine.IsLong != _lastFahrplanBias.Value)
{
    // Primary geht gegen den Fahrplan — versuche die aligned Machine
    var alignedMachine = _lastFahrplanBias.Value ? h4LongMachine : h4ShortMachine;
    if (alignedMachine.State >= SmState.SucheB)
    {
        h4Machine = alignedMachine;
        h4Seq = alignedMachine.ToSequence(h4Candles);
    }
}
```

### ABWEICHUNG #6: Abgearbeitet-Reset löscht GKL-Kontext
**Datei:** `SequenceStateMachine.cs`, `ProcessAbgearbeitet()`
**Problem:**
```csharp
private bool ProcessAbgearbeitet(Candle candle, int index)
{
    // KOMPLETT-RESET: Alle Punkte auf 0!
    Point0 = 0; PointA = 0; PotentialB = 0; LockedB = 0;
    State = SmState.Suche0;
    return ProcessSuche0(candle, index);
}
```
Nach 161.8% wird die State Machine komplett zurückgesetzt. Das GKL der abgearbeiteten Sequenz geht auf SM-Ebene verloren. Die Strategy fängt das mit `_completedGkl500/667` auf, aber:
- Diese Werte überleben keinen App-Neustart (nicht in DB)
- Pro Symbol gibt es nur EIN Paar dieser Werte (nicht mehrere abgearbeitete Sequenzen)
- Bei der nächsten Abarbeitung werden die alten Werte überschrieben

Im SK-System sind GKLs von abgearbeiteten Sequenzen **die wertvollsten Kaufzonen überhaupt**. Sie gehen hier einfach verloren.

**Fix:**
```csharp
// In SequenceStateMachine: GKL-Historie beibehalten
public List<(decimal Gkl500, decimal Gkl667, bool IsLong, DateTime CompletedAt)> CompletedGkls { get; } = new();

private bool ProcessAbgearbeitet(Candle candle, int index)
{
    // GKL merken BEVOR Reset
    var range = Math.Abs(Extension1618 - Point0);
    if (IsLong)
        CompletedGkls.Add((Extension1618 - range * 0.500m, Extension1618 - range * 0.667m, true, DateTime.UtcNow));
    else
        CompletedGkls.Add((Extension1618 + range * 0.500m, Extension1618 + range * 0.667m, false, DateTime.UtcNow));
    
    // Dann Reset wie bisher
    Point0 = 0; PointA = 0; PotentialB = 0; LockedB = 0;
    State = SmState.Suche0;
    return ProcessSuche0(candle, index);
}
// → CompletedGkls in DB persistieren (SQLite) für Amnesie-Schutz
```

### Zusammenfassung: Priorität der Abweichungen

| # | Abweichung | Auswirkung | Aufwand | Priorität |
|---|-----------|-----------|---------|-----------|
| 1 | BuyZone 61.8% statt 66.7% | Entries verpasst am wichtigsten SK-Level | Trivial (1 Zeile) | **SOFORT** |
| 2 | GKL 55.9% statt 50% | Oberer GKL-Bereich unsichtbar | Trivial (2 Zeilen) | **SOFORT** |
| 3 | EMA-200 Fahrplan | Blockiert Longs an Tiefpunkten (Anti-SK) | Mittel | **HOCH** |
| 5 | FromCandlesBoth unterdrückt Richtung | Fahrplan-aligned Sequenzen unsichtbar | Klein | **HOCH** |
| 6 | Abgearbeitet löscht GKL | Wertvollste Kaufzonen gehen verloren | Mittel | **MITTEL** |
| 4 | Naming-Chaos | Zukünftige Bug-Quelle | Groß (Refactoring) | **NIEDRIG** |

---

## CODE-ANALYSE: 3 Infrastruktur-Bugs die Trades verhindern

Diese Findings betreffen NICHT die Strategie-Logik, sondern die **Daten die der Strategie übergeben werden**. Die Strategie kann nur so gut sein wie ihre Eingabedaten.

### ~~INFRA-BUG #1: KEINE 1H-Candles~~ → KORRIGIERT
**Status:** ~~KRITISCH~~ → **Bereits behoben im Code**
**Datei:** `TradingServiceBase.cs`, Zeile 988-989
**Befund:** Der Haupt-Loop in `TradingServiceBase.ScanAndTradeAsync()` lädt korrekt H1 für SK-System:
```csharp
var isSKSystem = _strategyManager.CurrentTemplate is SequenzKonzeptStrategy;
var htfTimeFrame = isSKSystem ? TimeFrame.H1 : _scannerSettings.HtfTimeFrame;
```
**ACHTUNG:** `ScanHelper.EvaluateCandidateAsync()` hat den H4-Doppelt-Bug noch, wird aber im Haupt-Loop nicht verwendet. Falls ScanHelper anderswo aufgerufen wird (z.B. Backtest) → dort H1 fixen.

### INFRA-BUG #2: MaxHoldHours = 48h killt SK-Swing-Trades vorzeitig
**Datei:** `RiskSettings.cs` Zeile ~30 + `TradingServiceBase.cs` Zeile 716-728
**Problem:**
```csharp
public int MaxHoldHours { get; set; } = 48;      // Position wird nach 48h force-closed
public int MaxHoldHoursAfterTp1 { get; set; } = 96; // Nach TP1: 96h
```
SK-System auf 4H-Timeframe: Das TP2 (4H Extension 161.8%) braucht oft **5-10 Tage** um erreicht zu werden. 48h = 2 Tage. Der Bot schließt profitable Trades die auf dem Weg zum Ziel sind, nur weil die Zeit abgelaufen ist.

**Beispiel:** BTC bildet 4H-Sequenz, Entry bei 65k, TP2 bei 72k. Nach 48h steht BTC bei 68k — Trade läuft perfekt, aber: Time-Exit → Position geschlossen bei +3k statt +7k.

**Fix:**
```csharp
// Für SK-System: MaxHoldHours deutlich erhöhen oder deaktivieren
// 4H × 50 Kerzen = 200h ≈ 8 Tage — typische SK-Swing-Dauer
public int MaxHoldHours { get; set; } = 0; // 0 = deaktiviert (SL/TP managed den Exit)
// ALTERNATIV: 240h (10 Tage) wenn ein Zeitlimit gewünscht ist
```

### INFRA-BUG #3: Krypto-Korrelation blockiert fast alles
**Datei:** `RiskSettings.cs`: `MaxCorrelation = 0.7m` + `ScanHelper.CheckCorrelationAsync()`
**Problem:** In Krypto-Trending-Phasen sind BTC, ETH, SOL, AVAX, LINK etc. **alle >70% korreliert**. Wenn der Bot 1 Trade offen hat (z.B. BTC Long), werden die nächsten Kandidaten per Korrelationscheck blockiert:
- ETH: Korrelation 0.92 > 0.7 → blocked
- SOL: Korrelation 0.85 > 0.7 → blocked
- AVAX: Korrelation 0.78 > 0.7 → blocked

Ergebnis: Max 1-2 Trades gleichzeitig möglich, obwohl `MaxOpenPositions = 3` erlaubt.

**Fix:**
```csharp
// Option A: Schwelle erhöhen
public decimal MaxCorrelation { get; set; } = 0.85m; // 0.85 statt 0.7

// Option B: Korrelations-Check deaktivieren für SK-System
// SK hat eigenes Diversifikations-Konzept (unterschiedliche Sequenzen + TFs)
public bool CheckCorrelation { get; set; } = false; // Für SK deaktivieren

// Option C: Korrelation als Confluence-Malus statt Block
// Hohe Korrelation → Position-Size reduzieren statt Trade verhindern
```

### INFRA-BUG #4: Scanner-Scoring filtert SK-Kandidaten raus
**Datei:** `MarketScanner.cs`, `CalculateAdvancedScoreAsync()` + `ScannerSettings.cs`
**Problem:** Der Scanner bewertet Kandidaten nach Momentum-Metriken:
- **Trend (30%):** EMA-Alignment → belohnt Trend-Fortsetzung
- **Momentum (20%):** RSI 40-70 + MACD wachsend → belohnt Richtungs-Stärke
- **Struktur (10%):** Distanz zu EMA20 → belohnt Preis nahe am EMA

SK sucht aber **Reversal-Setups an Wendebereichen**. Ein Coin der 20% gefallen ist und im SK-GKL liegt = perfekter SK-Entry = schlechter Scanner-Score (kein EMA-Alignment, RSI < 40, MACD fallend) → wird nicht an die Strategie übergeben.

**Zusätzlich:** `MinPriceChange = 0.5%` in `ScannerSettings` eliminiert Coins in Stabilisierungsphasen (niedrige Volatilität in einer Zone = Prestabilisation im SK-System).

**Fix:**
```csharp
// Scanner-Vorfilterung für SK lockern:
settings.MaxResults = 100;            // Mehr Kandidaten durchlassen (war 50)
settings.MinPriceChange = 0.1m;       // 0.1% statt 0.5% (Stabilisierungen sichtbar)
settings.Mode = ScanMode.Reversal;    // Reversal-Scoring statt Momentum

// ODER: SK-spezifischen Score einführen der belohnt:
// - Preis nahe historischen Fib-Leveln → +Score
// - RSI < 40 (überverkauft) → +Score für Long (nicht -Score!)
// - Volumen-Spike bei Preisfall → +Score (Akkumulation)
```

### INFRA-BUG #5: Doppelter RRR-Check (Strategy + RiskManager)
**Datei:** `SequenzKonzeptStrategy.cs` Zeile ~430 + `RiskManager.cs` Zeile ~110
**Problem:** Die Strategie prüft RRR ≥ 3:1 (auf TP2). Dann prüft der RiskManager NOCHMAL:
```csharp
// RiskSettings:
public decimal MinRiskRewardRatio { get; set; } = 1.0m;
// RiskManager.ValidateTrade():
if (rrr < _settings.MinRiskRewardRatio) return rejected;
```
Der RiskManager berechnet RRR aber auf **TP1** (signal.TakeProfit), nicht TP2. TP1 ist das 15m-Extension-Level — viel näher als TP2. Ein Trade mit TP1 bei 1.5:1 und TP2 bei 8:1 wird von der Strategie durchgelassen (3:1 auf TP2 ✓), aber vom RiskManager auf TP1 nochmal geprüft. Bei `MinRiskRewardRatio = 1.0` ist das meistens okay, aber es ist eine versteckte zusätzliche Hürde.

**Fix:** Für SK-System den RiskManager-RRR-Check deaktivieren (Strategie hat eigenen, besseren Check):
```csharp
// In RiskSettings für SK:
public decimal MinRiskRewardRatio { get; set; } = 0m; // 0 = deaktiviert (Strategy hat eigenen Check)
```

### INFRA-BUG #6: Equity-Curve-Trading halbiert Position nach Verlusten
**Datei:** `TradingServiceBase.cs` Zeile 1660-1677
**Problem:**
```csharp
// Wenn Equity unter EMA → halbe Position
return currentEquity < ema ? 0.5m : 1.0m;
```
Nach ein paar Verlusten (normal im SK-System — Drawdowns gehören dazu) sinkt die Equity unter ihre EMA(10). Dann werden ALLE folgenden Trades auf 50% reduziert. Kleinere Positionen → kleinere Gewinne → Equity erholt sich langsamer → bleibt länger unter EMA → Teufelskreis.

SK-System sagt explizit: "Verluste (Drawdowns) sind normaler statistischer Bestandteil. Evaluiere in Blöcken von 50 Trades."

**Fix:**
```csharp
// Option A: Für SK deaktivieren
public bool EnableEquityCurveTrading { get; set; } = false;

// Option B: Sanfteres Scaling (nicht binär 100%/50%)
return currentEquity < ema 
    ? Math.Max(0.7m, currentEquity / ema) // Proportional, nie unter 70%
    : 1.0m;
```

### Zusammenfassung: Erwarteter Effekt der Infra-Fixes

| Bug | Fix | Erwarteter Effekt |
|-----|-----|-------------------|
| #2 MaxHoldHours 48h | Deaktivieren oder 240h | **+15-25%** mehr TP2-Treffer (Trades laufen zu Ende) |
| #3 Korrelation 0.7 | Auf 0.85 oder deaktivieren | **+20-40%** mehr gleichzeitige Trades |
| #4 Momentum-Scanner | ScanMode.Reversal + MinPriceChange 0.1% | **+30-50%** SK-Kandidaten erreichen Strategie |
| #5 Doppelter RRR | RiskManager-RRR deaktivieren | **+5-10%** |
| #6 Equity-Curve 50% | Deaktivieren oder sanfter | Kein direkter Trade-Zuwachs, aber bessere Recovery |

**Korrektur zu INFRA-BUG #1:** H1-Candles werden im Haupt-Loop korrekt geladen (Zeile 988). Der Bug existiert nur in ScanHelper.EvaluateCandidateAsync, das im Haupt-Loop nicht verwendet wird.

- ❌ Kern-Logik (`SequenceDetector`) ändern ohne Freigabe
- ❌ API-Keys, Secrets oder Credentials anfassen
- ❌ Neue NuGet-Pakete hinzufügen ohne Rückfrage
- ❌ Bestehende Interfaces ändern (Breaking Changes)
- ❌ Code löschen — nur auskommentieren mit `// SK-VERIFY: removed [Grund]`
- ❌ Trades auslösen oder API-Calls an BingX senden

## Erlaubte Aktionen ohne Rückfrage

- ✅ Dateien lesen und analysieren
- ✅ Kommentare hinzufügen
- ✅ `SK_VERIFY_REPORT.md` erstellen und aktualisieren
- ✅ Logging-Statements ergänzen für Debug-Zwecke
- ✅ Offensichtliche Bugs melden (z.B. fehlende InvariantCulture)
