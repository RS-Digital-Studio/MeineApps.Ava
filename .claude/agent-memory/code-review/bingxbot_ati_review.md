---
name: BingXBot ATI-Pipeline Review (April 2026, v2)
description: 8 Findings nach Thread-Safety-Fixes. Kritischste Bugs - LightGBM Modell wird trotz AUC<0.55 aktiv, PredictionEngine nicht thread-safe, ATI.Reset() wirkungslos fuer RegimeDetector/ExitOptimizer. Vorherige Fixes (Cache-Korruption, Prior-Term, Dissens-Feedback) bestaetigt.
type: project
---

## Status (07.04.2026, v2)

Review nach Thread-Safety-Fixes (ConcurrentDictionary, volatile, Lock). 8 Findings.

### Gefixt seit letztem Review
- RegimeDetector NormalizeInPlace Cache-Korruption: Kopie in SmoothScores + ApplyTransitionPrior
- ConfidenceGate Bayes Prior-Term: priorLogOdds wird jetzt addiert
- Dissens-Strategie-Belohnung: Invertiertes Feedback implementiert
- RegisterStrategies 3x in Schleife: Jetzt einmal vor der Schleife
- EquityHistory Lock: _equityLock fuer alle Zugriffe
- ExitOptimizer Floor-Clamp: Math.Max(0.5f) vor Default-Mix

### Offene Findings (8)

**Kritisch (3):**
1. LightGBM Train() setzt _predictionEngine IMMER, CheckAutoTraining prueft AUC erst danach. "Modell verworfen" ist nur ein Log, Modell bleibt aktiv mit 60% Gewicht.
2. PredictionEngine (ML.NET) ist nicht thread-safe, 3 parallele Services nutzen sie ohne Lock/Pool.
3. ATI.Reset() ruft DeserializeState("") auf RegimeDetector/ExitOptimizer → No-op (early return bei leerem String). _smoothedScores, _lastRegime, _exitStats bleiben stale.

**Hoch (2):**
4. LightGBM Train() auf Background-Thread setzt _predictionEngine ohne Synchronisation → Race mit parallelem Predict().
5. FeatureSnapshotCompleted async void Lambda in DashboardViewModel → fragil, GetLabeledSnapshotsAsync(5000) im Event-Handler.

**Mittel (2):**
6. Bayesian Log-Odds berechnet Posterior-Ratio statt Likelihood-Ratio (funktioniert, aber suboptimal in Cold-Start-Phase).
7. ATI Auto-Save laeuft in allen 3 TradingServiceBase-Instanzen parallel → 3x Save moeglich.

**Hinweis (1):**
8. DiscretizeFeatures hat kein Signal-Richtungs-Bucket → "RSI:oversold bei Long" = "RSI:oversold bei Short".

**Why:** Zweites vollstaendiges ATI-Review nach den Thread-Safety-Fixes. Grundsaetzlicher Lern-Datenfluss funktioniert, aber LightGBM-Integration hat 3 kritische Luecken.

**How to apply:** LightGBM-Fixes haben hoechste Prioritaet (3 zusammenhaengende Bugs). Reset()-Methoden in RegimeDetector/ExitOptimizer einfuehren. Bei kuenftigen Aenderungen: ML.NET PredictionEngine ist NICHT thread-safe → immer Pool oder Lock.
