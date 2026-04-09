---
name: BingXBot Live-Trailing + ATI-Lernlogik Review
description: Review von 13 Fixes (Live-Trading-Modus + ATI-Lernlogik). 7 Findings (2 kritisch): ExtremePriceSinceEntry=0 bei Recovery, Bucket-Key-Migration fehlt
type: project
---

Review der 13 Fixes in 2 Runden (Live-Trading + ATI-Lernlogik), 07.04.2026.

**2 Kritische Findings:**
1. `RestorePositionSignal` setzt `ExtremePriceSinceEntry` nicht → bei Short-Recovery bleibt Wert auf 0 → falscher Trailing-SL und sofortiger Momentum-Decay-Exit
2. ConfidenceGate Bucket-Keys jetzt mit "L:"/"S:" Prefix → alte serialisierte Daten werden nie mehr gelesen → Cold-Start

**Weitere Findings:**
- OnnxModelInference.IsModelLoaded TOCTOU (entschaerft durch internen null-Check in Predict)
- PredictBatch: 0.0f statt 0.5f bei Fehler
- GetModelInfo() nicht unter Lock
- OnEnterTrailingPhaseAsync: Kein Retry nach CancelNativeSlTpOrders + fehlgeschlagenem SL-Setzen
- Recovery-Karenz: EntryTime ist App-Neustart-Zeit, nicht echte Eroeffnungszeit

**Positives:**
- Trailing-Stop-Sync auf BingX korrekt mit Throttle
- ONNX Atomic Swap sauber implementiert
- Recovery-Karenz-Logik durchdacht

**How to apply:** Bei naechsten BingXBot-Reviews diese Patterns beachten:
- Recovery-Pfad (RestorePositionSignal) IMMER auf alle PositionExitState-Felder pruefen
- Serialisierung/Deserialisierung: Key-Format-Aenderungen brauchen Migration
- ONNX Thread-Safety: Alle oeffentlichen Methoden muessen unter Lock sein
