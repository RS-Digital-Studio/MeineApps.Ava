---
name: BingXBot Logik-Fixes Review (April 2026)
description: Review der 18 Logik-Fixes aus Tiefenanalyse. 3 kritische Findings: BollingerStrategy ATR=0, ATI Reset loescht Strategien statt Gewichte, OnTrailingStopMoved bei fehlgeschlagenem Update.
type: project
---

Review vom 03.04.2026 ueber die Fixes aus der vorangegangenen Tiefenanalyse.

**Gut geloest:**
- RegimeDetector Array-Kopie: Exakt richtig, SmoothScores + ApplyTransitionPrior
- Trailing-Stop AddOrUpdate: Atomares CAS ueber ConcurrentDictionary
- CalculateVolumeSma: Saubere Implementierung mit Cache-Differenzierung

**Kritische Findings:**
1. OnTrailingStopMoved wird auch bei fehlgeschlagenem atomarem Update geloggt (TradingServiceBase:371+388)
2. BollingerStrategy einzige Strategie ohne ATR=0 Guard (BollingerStrategy:106+124)
3. ATI Reset() ruft _ensemble.ClearStrategies() auf — loescht registrierte Strategien statt gelernter Gewichte. Nach Reset kein Ensemble-Signal mehr moeglich.

**Verbesserungen:**
4. ExitOptimizer Verlierer-SL-Formel hat fast keinen Effekt (nur 6% Gewichtung)
5. ExitOptimizer Verlierer-TP kann bei extremen Werten unter 0.5 fallen — Min-Clamp fehlt
6. LiveTradingService entfernt Signal VOR Positions-Check — bei API-Error bleibt Position ohne SL/TP

**Why:** Fixes adressieren echte Bugs aus der Analyse, fuehren aber 3 neue kritische Probleme ein.

**How to apply:** Vor dem naechsten Commit diese 3 kritischen Findings beheben. ExitOptimizer-Formeln ueberdenken (Mathe pruefen).
