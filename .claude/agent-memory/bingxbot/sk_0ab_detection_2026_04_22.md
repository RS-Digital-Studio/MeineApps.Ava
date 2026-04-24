---
name: SK 0/A/B Strukturpunkt-Erkennung Audit (22.04.2026)
description: Fokus-Audit der Strukturpunkt-Erkennung je TF (4H/1H/15m) gegen alle 3 SK-Specs, mit konkreten Zusatzlogik-Stellen und Fix-Prio
type: project
---

## SK 0/A/B Punkte-Erkennung — Abgleich gegen 3 Specs (22.04.2026)

Quelle: `SK-System_ Das komplette Handbuch.docx`, `SK-System_ Technische Spezifikation.docx`, `Algorithmische Erkennung der Strukturpunkte.docx`.

### Befund in einem Satz
EIN Detektor (SequenceStateMachine) für ALLE TFs — buchtreu im Kern, aber 3 Spec-Aspekte abweichend und 8 Zusatzlogik-Stellen vorhanden.

### Je TF (Navigator-Standalone seit 15.04.2026)
- **H4 (grün)**: swing=5, minPoint0Candles=5, Default-ATR=1.5%, PipScale=1.0, SL-Buffer=12 Pips. Buchkonform.
- **H1 (grün)**: swing=5, minPoint0Candles=3, Default-ATR=0.8%, PipScale=1.0, SL-Buffer=8 Pips. Buchkonform.
- **M15 (gelb)**: Als Standalone-Navigator ist M15 Eigenkonzept. Buch sagt M15 = Trigger-TF unter H4-Setup, nicht eigenständige Sequenz. User-Entscheidung 15.04.2026.

### Kritische Spec-Abweichungen
1. **Point 0 Pivot-Check nur rechts, nicht links** (`SequenceStateMachine.cs:641-692`): Code nutzt Trailing+minPoint0Candles-Counter statt strict-Pivot-Fenster. Spec §1 verlangt beidseitige Bestätigung. Bei genug Historie funktional gleichwertig, aber nicht strict.
2. **RequireBosOnActivation default false** (`SequenzKonzeptStrategy.cs:194`): Spec §3 sagt "Ohne BOS keine SK-System-Messung" (hart), Code macht opt-in. Inkonsistent mit `RequireBosVolumeBreakout=true` (Buch-Only Strip Phase 2).
3. **Point B Trailing statt Pivot-in-Zone**: Spec §4 Phase 2 verlangt "neues Pivot Low in der Box + Box wieder verlassen". Code trailed B bis A-Break.

### Zusatzlogik (nicht im Buch/Spec)
- `_InvalidationTolerance` / `Gewarnt`-State → toter Code (Strategy setzt 0m).
- `FailedPoint0` / `PromotedToLarger` + `BiasFlip` → funktional redundant für denselben Spec-Punkt.
- Zeit-Proportions-Filter (corrBars < impulsBars*0.25) → Elliott-Einschlag, nicht in SK-Spec.
- `FibConfidence` → Info-only, harmlos.
- `CompletedGkls` max 5 → willkürliche Grenze.
- D1-BLASH-Fallback (pos<0.30=Long, >0.70=Short) → reine Heuristik.
- `EnableCounterTrendScalp` → Spec §4 erlaubt es explizit NICHT ohne "frische Gegensequenz", Code prüft das nicht hart.
- `Bckl559/Bckl618` Fib-Zahlen 11.8%/23.6% (SequenzKonzeptStrategy.cs:374) → nicht in SK-Fib-Tabelle, toter Wert.

### Toter Code (Strip-Kandidaten)
- `SequenceDetector.DetectSequence/DetectAllSequences/DetectBOS` — State-Machine übernahm, nur noch in Tests.
- `_minConfluence` Field in Strategy — wird nirgends als Gate gelesen.
- ~~`Step4_ConfluenceMarking(0)` → No-Op-Pipeline-Step.~~ ✔ Obsolet — die gesamte Pipeline wurde am 24.04.2026 entfernt (Step4 existiert nicht mehr).

### GKL-Berechnung dupliziert an 3 Stellen
- `SequenceDetector.CalculateGKL` (lastHigh/lastLow-Pivot-basiert)
- `SequenceStateMachine.ProcessAbgearbeitet` (Point0→Extension1618-basiert)
- `MultiTfGklDetector.TryDetectOnTf` (wrappt CalculateGKL)

→ Refactor-Kandidat: eine `SkFibCalculator.ComputeGkl(start, end, isLong)`-Helper.

### Bewertung
Sauber in Struktur und Benennung. 0/A/B-Erkennung algorithmisch stabil (Docht-Assertions sichern Buch-Regel). Triple-Entry (50/61.8/66.7) ist buchtreu. Aber: 2-3 Stellen zu viel Zusatzlogik für striktes Buch-Only. Phase 3 Strip würde sich anbieten.

## How to apply
Bei künftigen SK-Reviews: Zuerst prüfen ob die 3 kritischen Abweichungen (Pivot-left, RequireBosOnActivation, Point-B-Trigger) adressiert wurden. Zusatzlogik-Liste als Strip-Kandidaten-Queue für Phase 3. Wenn User fragt "ist SK sauber?" → GRÜN für H4/H1 mit Vorbehalten, GELB für M15.
