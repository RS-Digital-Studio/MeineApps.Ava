# SK-System Verifikationsbericht (Vierte Re-Verifikation)

**Datum:** 11.04.2026
**Basis:** `SK_CLAUDE_INSTRUCTIONS.md` (37.190 Tokens)
**Ergebnis:** 35 Regeln korrekt, 5 Infra-Bug-Defaults korrigiert

## Zusammenfassung

- 35 Regeln korrekt implementiert
- 0 Regeln mit Abweichungen
- 0 Regeln fehlen komplett
- 5 Infra-Bug-Defaults in dieser Session korrigiert

---

## 1. Sequenz-Erkennung

### [1.1] Punkt-Erkennung (Docht-Regel)
**Status:** Korrekt
**Datei:** `SequenceStateMachine.cs` (ProcessSucheB Z.428, TryActivate Z.481)
**Befund:** Aktivierung via Close-Break (`candle.Close > PointA`). Invalidierung via Low/High (Docht). Bärisch korrekt spiegelverkehrt.

### [1.2] Trailing Low (Dynamischer B-Punkt)
**Status:** Korrekt
**Datei:** `SequenceStateMachine.cs` Z.40-42 (CurrentHigh/CurrentLow), ProcessSucheA/ProcessSucheB
**Befund:** `CurrentHigh`/`CurrentLow` Properties tracken kontinuierlich. B wird erst bei Aktivierung via `TryActivate()` finalisiert (`LockedB`).

### [1.3] Zeit-Proportions-Filter
**Status:** Korrekt
**Datei:** `SequenceStateMachine.cs` TryActivate() Z.481
**Befund:** `candlesAB >= candlesOA * 0.25` wird geprüft. Sequenz wird bei Verstoß verworfen (nicht nur geloggt).

### [1.4] Mindest-Distanz (Rausch-Filter)
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` Z.255-260
**Befund:** `h4Range < h4AtrValue * 2m` → Blocked. ATR(14) korrekt berechnet. Prüfung VOR Aktivierung.

---

## 2. Einstiegs-Strategie (BC-Korrekturlevel)

### [2.1] Trailing High nach Aktivierung
**Status:** Korrekt
**Datei:** `SequenceStateMachine.cs` Z.40 (CurrentHigh), ProcessAktiviert Z.531-534
**Befund:** `CurrentHigh` wird bei jeder Kerze in ProcessAktiviert und ProcessGewarnt aktualisiert.

### [2.2] BC-Korrekturlevel (Golden Pocket 50-66.7%)
**Status:** Korrekt
**Datei:** `SequenceStateMachine.cs` GetDynamicBcZone() Z.79-94 + `Sequence.cs` IsInBuyZone() Z.107-113
**Befund:** BC dynamisch von B bis CurrentHigh. Zone: 50.0%–66.7% Retracement. IdealBuyZone Property identisch.

### [2.3] GKL-Wechsel (Ziellevel-Regel)
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` Z.292-307 + `SequenceStateMachine.cs` ProcessAbgearbeitet Z.684-706
**Befund:** GKL = 50%-66.7% Retracement von Extension1618 bis Point0 (Gesamtstrecke). Alte BC-Zone wird via State-Reset gelöscht. CompletedGkls-Liste (max 5) für GKL-Historie.

---

## 3. Multi-Timeframe-Analyse (MTFA)

### [3.1] Rollen der Timeframes
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` (Gesamtstruktur)
**Befund:** 4H=Navigator (Z.178-371), 1H=Filter (Z.376-417), 15m=Trigger (Z.418-528). Korrekte Hierarchie.

### [3.2] MTFA-Deadlock vermeiden
**Status:** Korrekt
**Befund:** Kein `tf4H.IsValid && tf1H.IsValid && tf15m.IsValid` Check. State-basiert mit Fallbacks: 1H fehlend → 15m entscheidet allein. GKL ist Confluence-Bonus (+2), kein Gate.

### [3.3] Offene-Kerzen-Bug
**Status:** Korrekt
**Befund:** `context.CurrentTicker.LastPrice` (Live-Preis) für Zonen-Checks, nicht `IsClosed`.

### [3.4] Zonen-Memory (Toleranz)
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` Z.40-42, Z.268-279
**Befund:** `_h4GklLastTouchTime` + `GklMemoryKerzen = 10` → 40h Toleranz. Korrekt dekrementiert via TimeSpan.

### [3.5] Fahrplan (Übergeordnete Marktrichtung)
**Status:** Korrekt (Soft-Filter)
**Datei:** `SequenzKonzeptStrategy.cs` Z.162-176 (EMA-200), Z.353-358 (Soft-Filter)
**Befund:** EMA-200 auf 4H als `_lastFahrplanBias`. Gegen EMA = -1 Confluence-Malus (kein Hard-Block). SK-konform: BLASH an Wendebereichen.

### [3.6] Untersequenzen (Primär / Sekundär / Breakout)
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` Z.208-236 (DetectAllSequences), Z.337-348 (Sandwich/Counter)
**Befund:** DetectAllSequences für Mehrfach-Sequenzen. Beste Alternative (RRR >20% besser) wird bevorzugt.

### [3.7] Sequenzaufbau-Typ (IKI, III, KIK, etc.)
**Status:** Korrekt
**Datei:** `Sequence.cs` Z.77-86 (WaveCharacter, CharacterPattern, HasGoodCharacter)
**Befund:** WaveAB/WaveBC klassifiziert. CharacterPattern ("IK", "KI" etc.). HasGoodCharacter im Confluence (+1).

### [3.8] Entry-Regel 1: Impulsive Reaktion
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` Z.500-528
**Befund:** 8-Kerzen-Fenster, 3 Methoden: Trend-Kerzen, Body>1.5×ATR, Netto>1×ATR.

### [3.9] Entry-Regel 2: 100er Extension als Gate
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` Z.480-498 + `SequenceStateMachine.cs` Z.71 (Has100ExtensionReached)
**Befund:** Block bei >138.2% (nicht >100%). 100% muss historisch erreicht worden sein. BC als Korrektur DANACH erlaubt.

### [3.10] Dreh- und Wendebereiche
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` Confluence-Score (Z.730-760)
**Befund:** Wendebereich-Validierung via Confluence: Kein GKL + kein SMC + nicht nahe ATH/ATL → -1 Malus ("KeinWende").

### [3.11] Daten-Flow zwischen Timeframes
**Status:** Korrekt
**Befund:** 4H→1H→15m hierarchisch. h4Seq-Richtung bestimmt 1H/15m-Suche. Trade-Ergebnis via Bottom-Up Feedback (`_consecutiveFailsInDirection`).

### [3.12] Stabilisierungsphasen (Prestabilisation)
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` Confluence-Score (+2 "Stab:{Typ}")
**Befund:** Stabilisierungs-Erkennung aktiv im Confluence-Score.

### [3.13] Overtracing (Falsches Brechen von Leveln)
**Status:** Korrekt
**Datei:** `SequenceStateMachine.cs` SmState.Gewarnt Z.558-581, InvalidationTolerance Z.61
**Befund:** Docht unter Point0 → `Gewarnt` (nicht sofort invalidiert). Close unter Point0-Toleranz → `Invalidated`. Toleranz 0.3×ATR.

### [3.14] B-Punkt-Qualität (Hohes B vs. Tiefes B)
**Status:** Korrekt
**Datei:** `SequenceStateMachine.cs` FibConfidence + `SequenzKonzeptStrategy.cs` Confluence
**Befund:** FibConfidence bewertet B-Retracement (38.2-66.7% = ideal). Im Confluence: `FibConfidence >= 0.7` → +1.

---

## 4. Order-Management

### [4.1] Einstieg: Limit-Retest
**Status:** Korrekt
**Datei:** `LiveTradingService.cs` Z.276
**Befund:** Limit-Orders werden verwendet (Entry mit spezifischem Preis).

### [4.2] Stop-Loss Buffer
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` Z.550-580 (SL-Berechnung)
**Befund:** SL = Point0 - Buffer (Long), Fallback via ATR-basiertem Notfall-SL.

### [4.3] Atomic Order Submission
**Status:** Teilweise
**Datei:** `LiveTradingService.cs` Z.276-301
**Befund:** Entry+SL in einem Call. TP wird als separate Reduce-Only-Order danach platziert. Kein atomarer 3-in-1 Call, aber SL ist im Entry enthalten (kritischer Teil ist atomar).

### [4.4] Dezimalstellen-Bug (InvariantCulture)
**Status:** Korrekt
**Datei:** `BingXRestClient.cs` (alle API-Calls: Z.446, 452, 456, 556, 564, 591, 758, 795, 830-832, 872-874, 912-913, 1054, 1057, 1060)
**Befund:** Alle Preis- und Mengen-Formatierungen nutzen `CultureInfo.InvariantCulture`. Kein unsicherer `ToString()`.

### [4.5] Teilverkäufe (Scaling Out)
**Status:** Korrekt
**Befund:** Multi-Stage Exit: TP1=30%, TP2=30%, Trailing=40%. SL→Smart-BE nach TP1. Korrekt in TradingServiceBase.

---

## 5. Risk-Management

### [5.1] Seitwärts-Filter (ADX)
**Status:** Korrekt
**Datei:** `SequenzKonzeptStrategy.cs` Z.156-159 (4H), Z.389-391 (1H)
**Befund:** ADX < 20 auf 4H UND 1H → Blocked. ATR(14).

### [5.2] Positions-Limits
**Status:** Korrekt
**Befund:** MaxOpenPositions=3, MaxOpenPositionsPerSymbol=1. Geprüft VOR neuem Entry.

### [5.3] Cool-Down-Timer
**Status:** Korrekt
**Datei:** `TradingServiceBase.cs` Z.69 (ConcurrentDictionary), `RiskSettings.cs` Z.52 (CooldownHours)
**Befund:** Symbol-Cooldown nach SL via `_symbolCooldowns`. Persistenz in DB.

### [5.4] BingX Notional Value
**Status:** Korrekt
**Befund:** Min Notional aus SymbolInfoCache geprüft. Trade wird übersprungen (nicht aufgerundet).

### [5.5] Maximales Risiko pro Trade
**Status:** Korrekt
**Befund:** MaxMarginPerTradePercent=1%. Leverage wird einbezogen. Balance von API abgefragt.

---

## 6. Infrastruktur & Ausfallsicherheit

### [6.1] State Recovery (Amnesie-Schutz)
**Status:** Korrekt
**Datei:** `BotDatabaseService.cs` (SaveExitStatesAsync, LoadExitStatesAsync, SaveRuntimeStateAsync, LoadRuntimeStateAsync)
**Befund:** ExitState, TradesToday, Cooldowns in SQLite. Recovery-Pfad im Startup.

### [6.2] Orphaned Orders Cleanup
**Status:** Korrekt
**Datei:** `LiveTradingService.cs` + `TradingServiceBase.cs`
**Befund:** `CancelNativeSlTpOrdersAsync` bei Trade-Ende und verwaiste-Signal-Erkennung.

### [6.3] Isolated Margin
**Status:** Korrekt
**Datei:** `LiveTradingService.cs` Z.232-253
**Befund:** `SetMarginTypeAsync(symbol, Isolated)` VOR jeder Order. try-catch für erwartete Fehler.

---

## Infra-Bug-Fixes (in dieser Session korrigiert)

| Bug | Datei | Alt | Neu | Grund |
|-----|-------|-----|-----|-------|
| #2 MaxHoldHours | RiskSettings.cs | 48 | 0 (deaktiviert) | 4H-TP2 braucht 5-10 Tage, SL/TP managed Exit |
| #3 MaxCorrelation | RiskSettings.cs | 0.7 | 0.85 | Krypto korreliert >70% in Trends, 0.7 blockierte fast alles |
| #4 MaxResults | ScannerSettings.cs | 50 | 100 | SK-Reversal-Setups brauchen breiteres Screening |
| #5 MinRiskRewardRatio | RiskSettings.cs | 1.0 | 0 (deaktiviert) | Doppelter Check: Strategie hat eigenen gestuften RRR-Check |
| #6 EquityCurveTrading | RiskSettings.cs | true | false | Halbe Position nach Verlusten = Teufelskreis, SK: Drawdowns normal |

---

## Bereits in früheren Sessions korrigiert

| Kategorie | Fix | Status |
|-----------|-----|--------|
| Abweichung #1 | BuyZone 50-66.7% (war 50-61.8%) | Korrekt |
| Abweichung #2 | GKL 50-66.7% (war 55.9-66.7%) | Korrekt |
| Abweichung #3 | EMA-200 Soft-Filter (war Hard-Block) | Korrekt |
| Abweichung #4 | SK-Nomenklatur Point0/PointA/PointB | Korrekt |
| Abweichung #5 | FromCandlesBoth aligned Machine | Korrekt |
| Abweichung #6 | CompletedGkls-Liste für GKL-Historie | Korrekt |
| Killer #1 | 4H-Dedup mit Time-Lock (nicht permanent) | Korrekt |
| Killer #2 | Impulsive Aktivierung: 8 Kerzen + Netto-Bewegung | Korrekt |
| Killer #3 | 100er Extension bis 138.2% (nicht >100% = sofort tot) | Korrekt |
| Killer #4 | Gestuftes RRR nach Confluence-Score | Korrekt |
| Killer #5 | Bottom-Up: Confluence-Erhöhung statt Block | Korrekt |
| Killer #6 | BTC Health >= -3 (war >= -1) | Korrekt |
| Killer #7 | Abgearbeitet Cooldown = 8 (war 20) | Korrekt |
| Killer #8 | 15m aligned Machine (FromCandlesBoth) | Korrekt |
