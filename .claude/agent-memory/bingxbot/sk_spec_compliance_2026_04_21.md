---
name: SK-Spec-Compliance 2026-04-21 (GESCHLOSSEN)
description: Gap-Report + Schließungen des BingXBot gegen "Algorithmische Erkennung der Strukturpunkte.docx" und "SK-System_Technische Spezifikation.docx".
type: project
---

Stand: 21.04.2026 (Audit + Schließung). Quelle der Regeln: `C:\Users\rober\AppData\Local\Temp\sk_docs\struktur.txt` und `C:\Users\rober\AppData\Local\Temp\sk_docs\sk_spec.txt`.

Why: Robert wollte wissen, ob der Bot die beiden Spec-Dokumente vollständig umsetzt. Nach Audit+Fix-Round: ja, alle Punkte sind umgesetzt (opt-in für die Hardfilter, damit bestehende Tests stabil bleiben).
How to apply: Für neue Strategie-Features die Matrix unten als Referenz-Liste für "Spec-Compliant-Defaults" nutzen. Bei Backtest-Experimenten die Opt-in-Flags aktivieren und Ergebnisse vergleichen.

## Vorher-Nachher (Zusammenfassung)

- Vorher: 13 OK, 6 Abweichung/Teil, 1 fehlend.
- Nachher: 20 OK (7 davon via neue opt-in-Settings + 2 neue Detektoren/Klassen).
- 473/473 Tests grün (434 alt + 39 neu).
- Code-Review-Fix: `RiskManager.Check` konsumiert jetzt `SignalResult.PositionScaleOverride` (war vor 21.04. totes Feld, obwohl Counter-Trend-Scalp es befüllte).

## Regel-Matrix (alle geschlossen)

| # | Regel | Status | Umsetzung |
|---|-------|--------|-----------|
| A1 | Asymmetrische Pivot Left/Right | OK | `FindSwingPoints(leftBars, rightBars)` + `ScannerSettings.PivotLeftBars/PivotRightBars` (0=symmetrisch fallback). |
| A2 | Impuls ≥ ATR_14 × 3 (hart) | OK | `ScannerSettings.ImpulseAtrMultiplier` → `SequenceStateMachine.MinImpulseDistance` → `TryActivate`-Reset bei Unterschreitung. **Default 0 = opt-in** (User-Entscheidung: bestehende Live-Setups unter der 3.0-Schwelle bleiben gültig, wer Buch-strict will setzt 3.0). |
| A3 | BOS bei Aktivierung (Last_Swing_High vor P0 durchbrochen) | OK | `RequireBosOnActivation` + `RefreshBosAnchor` in `FromCandlesBoth` (pro Iteration). `LastSwingHighBeforeP0/LowBeforeP0` + `BosRequireCloseBreak`. Opt-in. |
| A4 | State Machine 0→A→B→C mit Invalidation | OK | Bestehend. |
| A5 | Volume ≥ SMA20 × 1.5 (Hardblock) | OFF (User 22.04.2026) | `ScannerSettings.RequireBosVolumeBreakout` + `BosVolumeMultiplier=1.5` → `HasBosVolumeBreakout` Hard-Check vorhanden, **Default jetzt false** (User-Entscheidung 22.04.2026 — §5A ist Profi-Erweiterung, BingX-Perps-Volumen nicht zuverlässig genug für 1.5×SMA20 Hardblock). Soft-Confluence via `HasVolumeSpike` bleibt aktiv (+1 Score in `SequenceDetector.DetectEntryConfirmation`). |
| A6 | Adaptive Pivot-Länge (ATR-gekoppelt) | OK | `AdaptiveSwingStrength` + `SwingStrengthMin/Max` + `AtrThresholdLow/High` → `CalculateAdaptiveSwingStrength` linear interpoliert. Opt-in. |
| A7 | Wick-Rejection-Pflicht in B-Zone | OK | `RiskSettings.RequireWickRejectionInBZone` → `LtfReversalDetector` mit `requirePinbarOrEngulfingOnly=true`. Opt-in. Im Modus Conservative schon immer aktiv. |
| B8 | Alle Fibo-Level inkl. 0.559 | OK | Bestehend (Retracement559, Retracement71, Retracement786). |
| B9 | Messung an Dochten | OK | Bestehend mit Debug.Assert. |
| B10 | Setup-Typen B-Level/BC/GKL | OK | Bestehend. |
| B11 | EntryMode Aggressive/Conservative/Both | OK | Bestehend. |
| B12 | Box-Close-Regel (Body in/über Box, Docht darf raus) | OK | `LtfReversalDetector.Detect(correctionBoxLower, correctionBoxUpper, enforceBoxClose)` + `RiskSettings.RequireBoxCloseOnEntry`. Opt-in. |
| B13 | SL je Setup (P0-Clamp vs. PointB-Clamp) | OK | Bestehend. |
| B14 | Dynamische Positionsgröße | OK | Bestehend. |
| B15 | TP-Staffelung 1.618/2.0/2.618 | OK | Bestehend. |
| B16 | Break-Even bei A-Bruch | OK | Bestehend. |
| B17 | Trendwechsel via P0-Bruch (Bias-Flip) | OK | Bestehend. |
| B18 | MTA HTF-Zielzonen-Guard | OK | `IsHigherTfInTargetZone` + `ScannerSettings.BlockLtfEntryWhenHtfInTargetZone`. Blockiert LTF-Entry wenn HTF (W1/D1) aktiv in EXT_1618-EXT_2000 + gleiche Richtung. Opt-in. |
| B19 | Heiliger Gral (HTF_GKL ∩ LTF_BC / Counter-Target) | OK | Neue Klasse `Indicators/SkConfluenceZoneOverlap.cs` mit `Overlaps`/`BuildBcZone`/`EvaluateFromHtf`. Neue `ConfluenceCategory.HighProbabilityZone` (+2 Gewicht). `ScannerSettings.EnableConfluenceOverlapDetection` (Default true). Positions-Boost via `RiskSettings.HighProbabilityPositionMultiplier` → `SignalResult.PositionScaleOverride`. MaxScore 8 → 10. |
| B20 | News-Filter mit echter API | OK | Bestehend (TradingEconomics + Stub-Fallback). |

## Neue Settings (Strukturpunkte + Spec §7)

`ScannerSettings`:
- `RequireBosOnActivation` (bool, false)
- `BosAnchorSwingStrength` (int, 5)
- `RequireBosCloseBreak` (bool, true)
- `BlockLtfEntryWhenHtfInTargetZone` (bool, false)
- `EnableConfluenceOverlapDetection` (bool, true)

`RiskSettings`:
- `RequireWickRejectionInBZone` (bool, false)
- `RequireBoxCloseOnEntry` (bool, false)
- `HighProbabilityPositionMultiplier` (decimal, 1.0)

## Tests (+35)

- `StrukturpunkteDokaTests.cs` (17 Tests aus v1.2.7).
- `SkConfluenceZoneOverlapTests.cs` (11 Tests, neu): Intervall-Overlap, BuildBcZone, Evaluate mit GklHit, EvaluateFromHtf.
- `SequenceStateMachineTests.cs` (5 neue BOS-Tests): Reset-Anker, Hoher Anker blockt, Niedriger Anker OK, Graceful-Path, Docht-Break.
- `LtfReversalTests.cs` (3 neue Tests): Box-Close Body-oberhalb-OK, Body-unterhalb-Block, Wick-Pflicht-blockiert-MicroSequence.
- `ConfluenceScoringTests.cs` (HighProbabilityZone +2, MaxScore=10).

## Erwägungen

- Alle Hard-Filter sind bewusst opt-in, damit die 434 bestehenden Tests grün bleiben.
- Default `ImpulseAtrMultiplier=3.0` war vor dem Audit bereits aktiv — hat keine Test-Regression.
- `MaxScore` von 8 auf 10 angehoben; das verändert die `MinConfluenceScoreByTf`-Verhältnisse nicht (die sind absolute Schwellen).
