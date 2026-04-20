---
name: SmartMeasure AR-Praezisions-Phase2 Review Apr 2026
description: 3 neue Dateien + ArCaptureActivity-Erweiterung. 14 Findings (4 krit): Bowditch wird von RefreshAnchors sofort ueberschrieben, Sampling haengt bei Tracking-Verlust, _activeSampler Thread-Race, Magnetometer-Pfad de facto tot weil Listener nach 1s beendet aber Finalize nach 5s laeuft
type: project
---

# SmartMeasure AR-Praezisions-Upgrade Phase 2 Review (17.04.2026)

Review fuer Phase-2-Features (Depth-API, Ground-Plane, ARCore-Heading, Bowditch, Quality-Score).

## Dateien
- ArPrecisionHelpers.cs (NEU, 260 Zeilen)
- ArAnchorManager.cs (NEU, 312 Zeilen - enthaelt ArAnchorManager, ArPoseSampler, ArStabilityMonitor)
- ArOverlayState.cs (NEU, 102 Zeilen)
- ArCaptureActivity.cs (erweitert, 2480 Zeilen)

## Kritische Bugs (4)

### K1. Bowditch-Correction wird von Anchor-Refresh sofort ueberschrieben
`CloseActiveContour` ruft `ApplyBowditchCorrection` auf. Naechster Frame ruft `RefreshAllAnchors` → `ArAnchorManager.RefreshAnchors` schreibt `point.X/Y/Z = anchor.Pose.Tx()/Ty()/Tz()`. Bowditch verpufft nach 16ms.
**Fix**: Vor Bowditch alle Anchors der Kontur-Punkte detachen. Alternative: Anchor-Pose "umbuchen" auf korrigierte Welt-Position (aber ARCore-API erlaubt kein Re-Pose).

### K2. Sampling haengt fest wenn Tracking waehrend 800ms verloren geht
`UpdateSamplingIfActive()` wird nur innerhalb `if (isTracking)` aufgerufen. Tracking-Verlust waehrend Sampling → `_activeSampler` bleibt fuer immer != null → alle folgenden Taps zeigen "Sample laeuft, bitte warten".
**Fix**: Auch im else-Zweig aufrufen, dort mit Timeout-Check finalisieren + ShowTransientHint.

### K3. `_activeSampler` / `_samplesCollected` nicht thread-safe
Schreiben auf UI-Thread (PlaceNewPoint), Lesen+Mutieren auf GL-Thread (UpdateSamplingIfActive). Keine Lock/Barrier. Race-Potenzial fuer Partial-Reads und verpasste Increments.
**Fix**: Alles auf GL-Thread via `_glSurfaceView.QueueEvent`, oder Lock um `_activeSampler`/Counter/`_sampleTargetX/Y`.

### K4. BuildOverlayState ruft ValidatePreMeasureConditions 2x pro Frame
Jeder Aufruf iteriert alle Trackables (JNI). Zusaetzlich separater planeCount-Loop. 3x Plane-Iteration pro Frame.
**Fix**: Einmal aufrufen, Tuple in lokale Variable, zweimal nutzen.

### K5. Magnetometer-Listener nur 1s aktiv, Finalize aber nach 5s
`CaptureSensorData` meldet HeadingListener nach 1000ms ab. `CollectInitialSensorSamples` finalisiert nach 5000ms. HeadingSampleTargetCount=20, aber bei SensorDelay.Normal (~200ms) kommen in 1s nur ~5 Samples. `FinalizeArCoreHeading` braucht >=5 und greift IMMER → Magnetometer-Fallback ist de facto tot.
**Fix**: Listener bis 5s aktiv lassen oder SensorDelay.Fastest + 2s Fenster.

## Hohe Bugs (6)

### H1. Bowditch: letzter Punkt nicht exakt auf erstem (Floating-Point-Rundung)
Konsistenz-Check am Ende der Methode kommentiert nur, setzt aber nicht explizit `last.X = first.X`. Shoelace-Rechnung koennte 0-Flaeche-Segmente erzeugen.
**Fix**: Explizit setzen oder letzten Punkt entfernen.

### H2. Thread-Safety bei `_magneticAccuracy`, `_lowMagAccuracyWarned`, `_groundPlaneY`, `_frameCountTracking/Total`
Keine volatile/Interlocked. JIT-Register-Caching moeglich, Races bei Warn-Dialog-Flag.

### H3. ExtractHeadingFromCameraPose bei Kamera-nach-unten (Garten!) = Noise
atan2(fx, -fz) liefert bei steil geneigter Kamera (Garten-Vermessung) instabile 0-360 Werte weil fx und fz beide ~0. Noise landet in Samples und verfaelscht Heading-Final.
**Fix**: Pitch pruefen, bei >53° Sample verwerfen und null zurueckgeben.

### H4. `_reticleHitDistance` stale fuer Depth-Sanity
FinalizeSampling nutzt _reticleHitDistance (= letzter Mitte-Frame), aber Target-Pixel ist (_sampleTargetX/Y) ausserhalb Mitte. Depth-Multiplier vergleicht falsche Referenz-Distanz.
**Fix**: In PlaceNewPoint Distanz zum Sample-Hit speichern.

### H5. Bowditch skip bei < 4 Punkten statt < 3
Dreieck ist valides Polygon. Grenze 4 willkuerlich.
**Fix**: < 3 oder < 2.

### H6. FindGroundPlaneY iteriert alle Trackables alle 30 Frames = 2x/s
Bei 50+ Planes teuer. Kein Guard-Break wenn schon stabile Plane gefunden.

## Mittlere Bugs (3)

### M1. DepthSanityMultiplier: ByteBuffer.Order nicht explizit gesetzt (zufaellig korrekt)
### M2. ArStabilityMonitor: Score bei jedem Event neu berechnet (ineffizient, kein Bug)
### M3. _captureMode kann zwischen PlaceNewPoint und FinalizeSampling wechseln → Punkt landet woanders als User tippte
### M4. _groundUpdateFrameCounter wird bei Tracking-Verlust nicht resettet
### M5. _lowMagAccuracyWarned nie resettet → keine neue Warnung bei erneut schlechtem Kompass
### M6. ComputeTrackingQualityScore: Default-Werte (Stability 1.0, Mag 3) geben +25 bevor Sensoren gemessen haben → Anfangs faelschlich hoher Score

## Positives
- CloseActiveContour haelt Lock sauber ueber Bowditch + Add
- ArAnchorManager kapselt Lifecycle inkl. Hard-Limit + try-catch um Detach
- Circular-Median via sin/cos korrekt implementiert (keine 359/1 Mittelungs-Falle)
- Xamarin-Bindings korrekt: AcquireDepthImage16Bits gibt Android.Media.Image, q[0..3] = x,y,z,w
- Recovery-Pattern + Haptic-Feedback gekapselt mit Defensive try-catch

## Garten-Use-Case Priority
H3 (Kamera-nach-unten-Heading-Noise) ist fuer den eigentlichen Use-Case fast so wichtig wie ein K-Finding — bei Garten-Vermessung schaut der User fast immer nach unten, dort liefert ExtractHeadingFromCameraPose Noise.
