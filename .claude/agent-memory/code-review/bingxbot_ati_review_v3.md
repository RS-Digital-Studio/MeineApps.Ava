---
name: BingXBot ATI-Pipeline Review v3 (April 2026)
description: 8 Findings (2 krit, 2 hoch, 3 mittel, 1 niedrig). Alle v2-Findings gefixt. Neue Erkenntnisse - Bayesian Prior wird N+1-fach gezaehlt (Over-Rejection), OnnxModel nicht thread-safe, ConfidenceGate ohne Signal-Richtung, ExitOptimizer TP-Aufschaukelung, Ensemble-Gewichte konvergieren zu schwach.
type: project
---

## Status (07.04.2026, v3)

Review nach v2-Fixes. 8 neue/vertieft analysierte Findings. Alle 8 v2-Findings sind gefixt.

### Gefixt seit v2
- LightGBM AUC-Gate: Modell wird nur bei AUC>=0.55 aktiviert (Zeile 137)
- PredictionEngine Lock: _predictionLock fuer alle Zugriffe
- ATI.Reset(): RegimeDetector.Reset() + ExitOptimizer.Reset() + LightGbm.InvalidateModel()
- Train() atomarer Swap unter _predictionLock
- Auto-Save TryClaimAutoSave mit Interlocked-Guard

### Aktuelle Findings (8)

**Kritisch (2):**
1. Bayesian Naive Bayes: smoothedWinRate enthaelt bereits den Prior (Laplace), aber priorLogOdds wird nochmals addiert → Prior N+1-fach gezaehlt. Bei 10 Buckets und 40% WinRate wird Confidence systematisch zu stark gedrueckt → Over-Rejection nach Verlustphasen.
2. OnnxModelInference.Predict() ohne Lock, LoadModel() disposed _session concurrent → selbes Pattern das bei LightGBM gefixt wurde.

**Hoch (2):**
3. FeatureSnapshotCompleted async void Lambda: Unbehandelte Exceptions crashen App, kein graceful Shutdown bei Training.
4. ExitOptimizer TP-Anpassung kann bei grossen AvgLosingTp-Werten zum Floor 0.5 konvergieren → unrealistisch enge TPs.

**Mittel (3):**
5. Bucket-Explosion: 5.3 Mio moegliche Keys, meiste erreichen nie MinBucketSamples=5 → Lernen effektiv nur auf wenigen Buckets.
6. Ensemble-Gewichte konvergieren bei 50% WinRate auf ~1.0, Differenzierung zwischen Strategien minimal (Ratio 1.35x bei 70/30% WinRate).
7. ConfidenceGate Buckets ohne Signal-Richtung: "RSI:oversold" bei Long und Short im selben Bucket → Long/Short-spezifisches Lernen unmoeglich.
8. TryClaimAutoSave: LastAutoSaveTime non-volatile DateTime auf x86 nicht atomar (nur theoretisch, x64 Desktop).

**Niedrig (1):**
9. FeatureSnapshotEntity Kommentar sagt "23 Features", sind aber 25.

**Why:** Dritte ATI-Review-Iteration. Die groben Bugs (Thread-Safety, AUC-Gate, Reset) sind alle gefixt. Die verbleibenden Findings betreffen die mathematische Korrektheit des Lernens und langfristige Konvergenz-Effekte.

**How to apply:** Bayesian-Formel hat hoechste Prio (beeinflusst jede Trade-Entscheidung). OnnxModel-Lock ist Copy-Paste vom LightGBM-Fix. Signal-Richtung in Buckets ist der groesste Hebel fuer besseres Lernen.
