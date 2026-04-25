using System.Diagnostics;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Zustandsautomat für SK-Sequenz-Erkennung mit Trailing Low/High.
/// Eliminiert das Fraktal-Lag-Problem: B-Punkt wird NICHT vorher fixiert,
/// sondern rutscht mit dem Preis mit bis A durchbrochen wird.
///
/// Zustände: SUCHE_0 → SUCHE_A → SUCHE_B (Trailing) → AKTIVIERT → ABGEARBEITET → SUCHE_0 (Neustart)
///
/// BUCH-REGEL DOCHT-MESSUNG (Task 4.1): Fibonacci-Punkte (Point0, PointA, PointB) werden
/// IMMER an den Kerzendochten (Wicks/Spikes) gemessen, NIE an den Kerzenkörpern oder Closes.
/// Buch-Zitat: "Das SK-System zieht die Fibonacci-Punkte (0, A, B, C) immer exakt an den
/// Spitzen der Kerzendochte (Wicks/Spikes) an, nicht an den Kerzenkörpern. Das Smart Money
/// operiert an den absoluten Extrempunkten der Liquidität."
/// Long:  Point0/PotentialB ← candle.Low,   PointA ← candle.High
/// Short: Point0/PotentialB ← candle.High,  PointA ← candle.Low
/// Debug.Assert in den Process*-Methoden sichert diese Invariante gegen zukünftige Refactorings ab.
/// </summary>
public class SequenceStateMachine
{
    /// <summary>Aktueller Zustand der State Machine.</summary>
    public SmState State { get; private set; } = SmState.Suche0;

    /// <summary>True = Long-Sequenz (0=Low, A=High, B=Low), False = Short (invertiert).</summary>
    public bool IsLong { get; private set; }

    // Erkannte Punkte
    public decimal Point0 { get; private set; }     // Ursprung (tiefstes Tief / höchstes Hoch)
    public int Point0Index { get; private set; }
    public decimal PointA { get; private set; }      // Impulsgipfel (höchstes High nach 0)
    public int PointAIndex { get; private set; }
    public decimal PotentialB { get; private set; }  // Trailing Low/High — wird ständig aktualisiert
    public int PotentialBIndex { get; private set; }
    public decimal LockedB { get; private set; }     // Eingefroren bei Aktivierung
    public int LockedBIndex { get; private set; }

    // Fibonacci-Level (berechnet bei Aktivierung)
    public decimal Extension100 { get; private set; }
    public decimal Extension1618 { get; private set; }
    public decimal Extension200 { get; private set; }
    /// <summary>261.8% Extension (Task 4.8) — Runner-Zwischenziel bei Überschießungen.</summary>
    public decimal Extension2618 { get; private set; }
    /// <summary>423.6% Extension (Task 4.8) — Runner-Hard-Cap bei Hyper-Trends.</summary>
    public decimal Extension4236 { get; private set; }
    public decimal Ret500 { get; private set; }
    public decimal Ret618 { get; private set; }
    public decimal Ret667 { get; private set; }

    // SK-VERIFY: [BC-Zone-Guard] BC-Zone-Invalid-Flag (138.2% Extension erreicht)
    /// <summary>
    /// True wenn der Preis die 138.2% Extension mindestens einmal erreicht hat. SK-Buch:
    /// Die 50-66.7% BC-Zone ist dann als Re-Entry unbrauchbar (Preis zu nahe am 161.8%-TP).
    /// State bleibt Aktiviert (laufende Trades behalten ihr TP), aber die Strategy-Schicht
    /// soll dieses Flag prüfen um keine neuen Pending-Orders gegen eine tote BC-Zone zu platzieren.
    /// Spiegelbild des Strategy-seitigen OverExtension-Guards in LiveTradingService.
    /// </summary>
    public bool IsBcZoneInvalid { get; private set; }

    // SK-VERIFY: [2.1] Trailing High/Low nach Aktivierung — für dynamische BC-Zone
    /// <summary>Höchstes High seit Aktivierung (Long). Basis für BC-Korrekturlevel.</summary>
    public decimal CurrentHigh { get; private set; }
    /// <summary>Tiefstes Low seit Aktivierung (Short). Basis für BC-Korrekturlevel.</summary>
    public decimal CurrentLow { get; private set; }

    /// <summary>Impuls-Range (0→A) in Preis-Einheiten. Für Proportions-Vergleich mit Sub-Wellen.</summary>
    public decimal ImpulseRange => Math.Abs(PointA - Point0);

    // SK-VERIFY: [3b.2] Gescheiterte → Größere Sequenz
    // Wenn eine Sequenz invalidiert wird, können deren Punkte eine größere Sequenz bilden.
    /// <summary>Point0 der letzten gescheiterten Sequenz (als möglicher Startpunkt einer größeren).</summary>
    public decimal FailedPoint0 { get; private set; }
    /// <summary>PointA der letzten gescheiterten Sequenz (als mögliches neues A einer größeren).</summary>
    public decimal FailedPointA { get; private set; }
    /// <summary>True wenn bei der letzten Invalidierung eine größere Sequenz erkannt wurde.</summary>
    public bool PromotedToLarger { get; private set; }

    /// <summary>
    /// Task 4.9 — True wenn eine aktivierte Sequenz im aktuellen Scan invalidiert wurde.
    /// Basis für automatischen Bias-Flip in der Gegenrichtung.
    /// </summary>
    public bool WasActivatedBeforeInvalidation { get; private set; }

    /// <summary>Task 4.9 — Preis bei dem die letzte aktivierte Sequenz gebrochen ist (für Bias-Flip PointA).</summary>
    public decimal LastBreakPrice { get; private set; }

    /// <summary>Task 4.9 — Candle-Index des letzten Bruchs (für Bias-Flip).</summary>
    public int LastBreakIndex { get; private set; }

    /// <summary>Task 4.9 — Altes Extrem (höchstes High Long / tiefstes Low Short) vor Invalidation, als Gegen-Sequenz-Point0.</summary>
    public decimal LastActivatedExtreme { get; private set; }

    /// <summary>Task 4.9 — Setzt den Bias-Flip-Hint zurück, damit derselbe Break nicht zweimal gefeuert wird.</summary>
    public void ResetBiasFlipHint()
    {
        WasActivatedBeforeInvalidation = false;
        LastBreakPrice = 0m;
        LastBreakIndex = -1;
        LastActivatedExtreme = 0m;
    }

    /// <summary>B-Retracement als Ratio (0.0-1.0). Elliott: Ideal 0.382-0.618, max 0.886.</summary>
    public decimal BRetracementRatio { get; private set; }

    /// <summary>
    /// Strukturpunkte-Doku §5A: Candle-Index der Aktivierungs-Kerze (Close &gt; PointA bei Long, Close &lt; PointA bei Short).
    /// Wird in <see cref="TryActivate"/> gesetzt. Strategy-Schicht prüft das Volumen dieser Kerze gegen SMA20,
    /// um Fakeouts in illiquiden Phasen hart zu blockieren (Doku-Regel "BOS_Volume ≥ SMA20 × 1.5").
    /// -1 = Sequenz noch nicht aktiviert.
    /// </summary>
    public int ActivationCandleIndex { get; private set; } = -1;

    // Strukturpunkte-Doku §2: Harte Impuls-Distanz-Schranke (|PointA - Point0| ≥ ATR_14 × N).
    /// <summary>
    /// Strukturpunkte-Doku §2: Mindest-Impuls-Distanz in absoluten Preiseinheiten.
    /// |PointA - Point0| MUSS mindestens diesen Wert betragen, sonst wird die Aktivierung verworfen
    /// und die Sequenz zurückgesetzt. Wird von der Strategy als ATR_14 × ImpulseAtrMultiplier gesetzt.
    /// 0 (Default) = Filter deaktiviert.
    /// </summary>
    public decimal MinImpulseDistance { get; set; }

    // Strukturpunkte-Doku §3: BOS-Anker (Last_Swing_High/Low VOR Point0). BOS-Gate ist per Buch-Regel
    // IMMER aktiv — "Ohne Strukturbruch (BOS) keine SK-System-Messung." Wenn der Anker 0 bleibt,
    // passiert ein graceful pass (Strategy hat zu wenig Historie geliefert).
    /// <summary>
    /// Strukturpunkte-Doku §3: Letztes markantes Pivot-High VOR Point0 (nur Long-Richtung relevant).
    /// 0 = unbekannt → graceful pass (Strategy lieferte zu wenig Historie für den Anker).
    /// Wird von der Strategy/FromCandlesBoth gesetzt, sobald Point0 bestätigt wurde.
    /// </summary>
    public decimal LastSwingHighBeforeP0 { get; set; }

    /// <summary>
    /// Strukturpunkte-Doku §3: Letztes markantes Pivot-Low VOR Point0 (nur Short-Richtung relevant).
    /// 0 = unbekannt → graceful pass.
    /// </summary>
    public decimal LastSwingLowBeforeP0 { get; set; }

    /// <summary>
    /// Strukturpunkte-Doku §3: Wenn true (Default), muss der BODY-Close die BOS-Grenze durchbrechen;
    /// wenn false reicht der Docht (High/Low). Siehe <see cref="ScannerSettings.RequireBosCloseBreak"/>.
    /// </summary>
    public bool BosRequireCloseBreak { get; set; } = true;

    /// <summary>
    /// Strukturpunkte-Doku §3: Diagnostik-Flag — true nach dem ersten BOS-Check-Fail. Reine Telemetrie
    /// für Backtest/Logs; State-Logik nutzt den Wert nicht. Wird bei erfolgreicher Aktivierung auf false zurückgesetzt.
    /// </summary>
    public bool LastActivationBlockedByBos { get; private set; }

    // SK-VERIFY: [Abweichung #6] GKL-Historie abgearbeiteter Sequenzen beibehalten
    // Im SK-System sind GKLs abgearbeiteter Sequenzen die wertvollsten Kaufzonen.
    // Ohne Historie gehen sie bei ProcessAbgearbeitet-Reset verloren.
    /// <summary>GKL-Zonen aller abgearbeiteten Sequenzen (50/66.7% von Point0 bis Extension1618).</summary>
    public List<CompletedGklEntry> CompletedGkls { get; } = new();

    // SK-VERIFY: [2.4] 100er Extension als Minimum-Gate
    /// <summary>True wenn der Preis die 100% Extension seit Aktivierung mindestens einmal erreicht hat.</summary>
    public bool Has100ExtensionReached { get; private set; }

    // SK-VERIFY: [2.2] Dynamische BC-Korrekturzone (Golden Pocket) basierend auf CurrentHigh/CurrentLow
    /// <summary>
    /// Berechnet die dynamische BC-Zone (50%-66.7% Retracement von B bis CurrentHigh/Low).
    /// Gibt (0,0) zurück wenn nicht im aktivierten Zustand.
    /// Top = näher am CurrentHigh (Entry-Einstieg), Bottom = näher am B-Punkt.
    /// </summary>
    public (decimal BcZoneTop, decimal BcZoneBottom) GetDynamicBcZone()
    {
        if (State != SmState.Aktiviert) return (0, 0);
        if (IsLong)
        {
            var range = CurrentHigh - LockedB;
            if (range <= 0) return (0, 0);
            return (CurrentHigh - range * 0.500m, CurrentHigh - range * 0.667m);
        }
        else
        {
            var range = LockedB - CurrentLow;
            if (range <= 0) return (0, 0);
            return (CurrentLow + range * 0.667m, CurrentLow + range * 0.500m);
        }
    }

    /// <summary>
    /// Elliott-Fibonacci-Confidence (0.0-1.0). Misst wie nah der B-Punkt an idealen Fib-Leveln liegt.
    /// 1.0 = exakt 61.8% (ideal), 0.0 = weit entfernt von allen Fib-Leveln.
    /// </summary>
    public decimal FibConfidence { get; private set; }

    // Konfiguration (von Strategy per ATR-basiertem Wert gesetzt)
    //
    // _minImpulsePercent (empfohlen: 1.0× ATR%):
    //   Noise-Filter für 0→A Übergang. In Kombination mit minPoint0Candles stellt dies sicher,
    //   dass Point 0 ein echtes Swing-Extrem ist. PointA trailed danach weiter →
    //   der tatsächliche 0→A Impuls wird IMMER größer als dieser Schwellenwert.
    //   1.0× ATR reicht als Gate, weil das Trailing den Rest erledigt.
    //   Höhere Werte (1.5×+) würden bei niedrig-volatilen Assets (Forex) valide Sequenzen verpassen.
    //
    // _correctionThreshold (empfohlen: 1.5× ATR%):
    //   A wird erst gelockt wenn Preis 1.5× ATR% von A zurücksetzt. Filtert Micro-Pullbacks
    //   innerhalb eines Impulses. PotentialB trailed danach weiter tiefer.
    //
    // Der echte Qualitätsfilter ist TryActivate → B-Retracement (38.2-78.6%):
    //   Unabhängig von ATR, prüft ob die Korrektur proportional zur 0→A Strecke ist.
    private readonly decimal _minImpulsePercent;  // Min. Impuls-Größe (0→A) in % vom Preis
    private readonly decimal _correctionThreshold; // Min. Korrektur von A um A zu locken (in %)
    private readonly decimal _minBRetracement;     // Min. B-Retracement (SK-Default: 0.382 = 38.2%)
    private readonly decimal _maxBRetracement;     // Max. B-Retracement (SK-Default: 0.786 = 78.6%)

    // SK-FIX: Point-0-Swing-Validierung
    // Point 0 muss ein echtes lokales Extrem sein (mindestens N Kerzen Gegenbewegung danach).
    // Ohne diese Prüfung wird jedes zufällige Low/High sofort als Point 0 akzeptiert.
    //
    // Konfigurierbar pro Timeframe, weil eine SK-Sequenz typisch ~100+ Kerzen dauert:
    //   W1: 3 (3 Wochen — Fahrplan, grob ausreichend)
    //   D1: 5 (5 Tage = 1 Handelswoche — solider Swing)
    //   H4: 5 (20h ≈ 1 Tag — echtes Tages-Extrem, ~5% einer 100-Kerzen-Sequenz)
    //   H1: 3 (3h — Filter-Timeframe)
    //   M30: 2 (1h — schnelle Entries im Trigger-Timeframe)
    private int _point0CandleCount;                // Kerzen seit letztem Point0-Update
    private readonly int _minPoint0Candles;         // Konfigurierbares Minimum pro Timeframe

    public SequenceStateMachine(decimal minImpulsePercent = 0.5m, decimal correctionThreshold = 0.3m,
        decimal minBRetracement = 0.382m, decimal maxBRetracement = 0.786m, int minPoint0Candles = 3)
    {
        _minImpulsePercent = minImpulsePercent;
        _correctionThreshold = correctionThreshold;
        _minBRetracement = minBRetracement;
        _maxBRetracement = maxBRetracement;
        _minPoint0Candles = minPoint0Candles;
    }

    /// <summary>
    /// Füttert die State Machine mit einer neuen Kerze. Wird pro Scan-Iteration aufgerufen.
    /// Gibt true zurück wenn die Sequenz gerade AKTIVIERT wurde (= Trade-Signal).
    /// </summary>
    public bool ProcessCandle(Candle candle, int index)
    {
        switch (State)
        {
            case SmState.Suche0:
                return ProcessSuche0(candle, index);
            case SmState.SucheA:
                return ProcessSucheA(candle, index);
            case SmState.SucheB:
                return ProcessSucheB(candle, index);
            case SmState.Aktiviert:
                return ProcessAktiviert(candle, index);
            case SmState.Abgearbeitet:
                return ProcessAbgearbeitet(candle, index);
            default:
                return false;
        }
    }

    /// <summary>Verarbeitet eine komplette Candle-Historie und gibt die erkannte Sequenz zurück.</summary>
    public static SequenceStateMachine? FromCandles(IReadOnlyList<Candle> candles,
        decimal minImpulsePercent = 0.5m, decimal correctionThreshold = 0.3m,
        decimal minBRetracement = 0.382m, decimal maxBRetracement = 0.786m, int minPoint0Candles = 3,
        decimal minImpulseDistance = 0m)
    {
        var (primary, _, _) = FromCandlesBoth(candles, minImpulsePercent, correctionThreshold, minBRetracement, maxBRetracement, minPoint0Candles, minImpulseDistance: minImpulseDistance);
        return primary;
    }

    /// <summary>
    /// Gibt primäre + beide Richtungs-Machines zurück.
    /// Für Sandwich-Check: Gegenrichtung direkt verfügbar ohne doppelte Berechnung.
    /// </summary>
    /// <param name="minImpulseDistance">
    /// Strukturpunkte-Doku §2: Absolute Mindest-Impuls-Distanz (|PointA - Point0|). 0 = Filter aus.
    /// Strategy übergibt ATR_14 × ImpulseAtrMultiplier (Default 3.0).
    /// </param>
    /// <param name="lastSwingHighBeforeP0">Letztes Pivot-High vor Point0 als Long-BOS-Anker (0 = unbekannt → graceful pass).</param>
    /// <param name="lastSwingLowBeforeP0">Letztes Pivot-Low vor Point0 als Short-BOS-Anker (0 = unbekannt → graceful pass).</param>
    /// <param name="bosRequireCloseBreak">True = Body-Close über Anker, False = Docht reicht (Default: true, Doku-Default).</param>
    /// <param name="bosAnchorSwingStrength">
    /// Strukturpunkte-Doku §3: Pivot-Stärke für die dynamische BOS-Anker-Suche (symmetrisch — left=right).
    /// Wenn &gt; 0 und <paramref name="bosAnchorLeftBars"/>/<paramref name="bosAnchorRightBars"/> beide ≤ 0,
    /// wird mit symmetrischem Fenster gesucht. 0 (Default) = kein dynamisches Update.
    /// </param>
    /// <param name="bosAnchorLeftBars">
    /// Strukturpunkte-Doku §1+§3 (25.04.2026): Asymmetrische BOS-Anker-Pivot-Bars links (Default 0 = inaktiv).
    /// Wenn beide (Left+Right) &gt; 0, hat das asymmetrische Paar Vorrang vor <paramref name="bosAnchorSwingStrength"/>.
    /// </param>
    /// <param name="bosAnchorRightBars">
    /// Strukturpunkte-Doku §1+§3: Asymmetrische BOS-Anker-Pivot-Bars rechts (Default 0).
    /// Steuert auch den Look-Ahead-Schutz in <see cref="RefreshBosAnchor"/>.
    /// </param>
    public static (SequenceStateMachine? primary, SequenceStateMachine longMachine, SequenceStateMachine shortMachine)
        FromCandlesBoth(IReadOnlyList<Candle> candles,
            decimal minImpulsePercent = 0.5m, decimal correctionThreshold = 0.3m,
            decimal minBRetracement = 0.382m, decimal maxBRetracement = 0.786m, int minPoint0Candles = 3,
            bool enableBiasFlip = true,
            decimal minImpulseDistance = 0m,
            decimal lastSwingHighBeforeP0 = 0m,
            decimal lastSwingLowBeforeP0 = 0m,
            bool bosRequireCloseBreak = true,
            int bosAnchorSwingStrength = 0,
            int bosAnchorLeftBars = 0,
            int bosAnchorRightBars = 0)
    {
        var longMachine = new SequenceStateMachine(minImpulsePercent, correctionThreshold, minBRetracement, maxBRetracement, minPoint0Candles)
        {
            MinImpulseDistance = minImpulseDistance,
            LastSwingHighBeforeP0 = lastSwingHighBeforeP0,
            LastSwingLowBeforeP0 = lastSwingLowBeforeP0,
            BosRequireCloseBreak = bosRequireCloseBreak
        };
        var shortMachine = new SequenceStateMachine(minImpulsePercent, correctionThreshold, minBRetracement, maxBRetracement, minPoint0Candles)
        {
            MinImpulseDistance = minImpulseDistance,
            LastSwingHighBeforeP0 = lastSwingHighBeforeP0,
            LastSwingLowBeforeP0 = lastSwingLowBeforeP0,
            BosRequireCloseBreak = bosRequireCloseBreak
        };
        longMachine.IsLong = true;
        shortMachine.IsLong = false;

        if (candles.Count < 20)
            return (null, longMachine, shortMachine);

        // Strukturpunkte-Doku §3: Pre-Pass für dynamische BOS-Anker-Auflösung (BOS-Gate ist immer aktiv).
        // Berechnung einmalig pro Aufruf (O(n × strength)) — Lookup bleibt O(log n) via binary search nach CandleIndex.
        // 25.04.2026: Asymmetrisches Pivot-Fenster (Doku §1+§3) hat Vorrang, wenn beide Werte gesetzt sind.
        List<SwingPoint>? bosSwings = null;
        int bosLookAheadRightBars = 0;
        if (bosAnchorLeftBars > 0 && bosAnchorRightBars > 0)
        {
            bosSwings = SequenceDetector.FindSwingPoints(candles, bosAnchorLeftBars, bosAnchorRightBars);
            bosLookAheadRightBars = bosAnchorRightBars;
        }
        else if (bosAnchorSwingStrength > 0)
        {
            bosSwings = SequenceDetector.FindSwingPoints(candles, bosAnchorSwingStrength);
            bosLookAheadRightBars = bosAnchorSwingStrength;
        }

        bool longActivated = false, shortActivated = false;
        int longActivatedAt = -1, shortActivatedAt = -1;
        int lastBiasFlipIndex = -100;  // Task 4.9: Ping-Pong-Schutz (3-Kerzen-Cooldown)

        for (int i = 0; i < candles.Count; i++)
        {
            // BOS-Anker pro Iteration auffrischen, bevor die Machines die Kerze verarbeiten.
            // Die Machine setzt die Anker-Felder bei Reset/Promote/BiasFlip/ProcessAbgearbeitet auf 0 — das ist das
            // Signal für uns, aus bosSwings einen neuen Anker zu ermitteln.
            if (bosSwings is { Count: > 0 })
            {
                RefreshBosAnchor(longMachine, bosSwings, i, bosLookAheadRightBars);
                RefreshBosAnchor(shortMachine, bosSwings, i, bosLookAheadRightBars);
            }

            if (longMachine.ProcessCandle(candles[i], i))
            {
                longActivated = true;
                longActivatedAt = i;
            }

            // Task 4.9 — Bias-Flip: Wenn Long gerade aktivierte Sequenz invalidiert hat
            // und Short noch in Suche0/SucheA, initialisiere Short als Bias-Flip.
            if (enableBiasFlip && longMachine.WasActivatedBeforeInvalidation
                && longMachine.LastBreakIndex == i
                && shortMachine.State <= SmState.SucheA
                && (i - lastBiasFlipIndex) >= 3)
            {
                shortMachine.InitAsBiasFlip(longMachine.LastActivatedExtreme, longMachine.LastBreakPrice, i);
                longMachine.ResetBiasFlipHint();
                lastBiasFlipIndex = i;
            }

            if (shortMachine.ProcessCandle(candles[i], i))
            {
                shortActivated = true;
                shortActivatedAt = i;
            }

            // Gegen-Richtung: Short invalidiert → Long-Bias-Flip
            if (enableBiasFlip && shortMachine.WasActivatedBeforeInvalidation
                && shortMachine.LastBreakIndex == i
                && longMachine.State <= SmState.SucheA
                && (i - lastBiasFlipIndex) >= 3)
            {
                longMachine.InitAsBiasFlip(shortMachine.LastActivatedExtreme, shortMachine.LastBreakPrice, i);
                shortMachine.ResetBiasFlipHint();
                lastBiasFlipIndex = i;
            }
        }

        // Bevorzuge die zuletzt aktivierte Sequenz
        SequenceStateMachine? primary;
        if (longActivated && shortActivated)
        {
            // Aktuell aktive Machine hat Vorrang über historisch aktivierte (die inzwischen reset wurde)
            var longAktiv = longMachine.State == SmState.Aktiviert;
            var shortAktiv = shortMachine.State == SmState.Aktiviert;
            if (longAktiv && !shortAktiv) primary = longMachine;
            else if (shortAktiv && !longAktiv) primary = shortMachine;
            // Beide aktiv oder beide nicht aktiv → neueste Aktivierung gewinnt
            else primary = longActivatedAt > shortActivatedAt ? longMachine : shortMachine;
        }
        else if (longActivated) primary = longMachine;
        else if (shortActivated) primary = shortMachine;
        else
        {
            // Keine Aktivierung — am weitesten fortgeschrittene
            if (longMachine.State > shortMachine.State) primary = longMachine;
            else if (shortMachine.State > longMachine.State) primary = shortMachine;
            else primary = longMachine; // Default: Long
        }

        return (primary, longMachine, shortMachine);
    }

    /// <summary>
    /// <summary>
    /// Strukturpunkte-Doku §3: Aktualisiert den BOS-Anker der Machine basierend auf der Pivot-Historie.
    /// Long → letztes Pivot-High mit <c>CandleIndex &lt; Point0Index</c>; Short → letztes Pivot-Low entsprechend.
    /// Idempotent: Setzt den Anker nur wenn Point0 existiert und der Anker-Slot noch 0 ist — verhindert überflüssige
    /// Updates und stellt sicher, dass Reset/Promote den Anker gezielt ungültig machen können.
    /// <para>
    /// Look-Ahead-Schutz: Ein Pivot bei Index j ist erst bei Index <c>j + rightBars</c> bestätigt — vorher wäre seine
    /// Verwendung im Backtest ein Blick in die Zukunft. Darum werden nur Swings akzeptiert, deren
    /// <c>CandleIndex + rightBars &lt;= currentIdx</c> ist. Bei Live-Trading (currentIdx = letzte Kerze der Historie)
    /// ist der Effekt neutral.
    /// </para>
    /// </summary>
    /// <param name="currentIdx">Aktuelle Candle-Position in der FromCandlesBoth-Iteration (für Look-Ahead-Filter).</param>
    /// <param name="rightBars">Rechte Pivot-Bars (= <c>bosAnchorSwingStrength</c>) — Anzahl Kerzen nach dem Pivot-Kandidaten,
    /// die für die Bestätigung benötigt wurden. Ein Swing ist erst nach <c>rightBars</c> weiteren Kerzen bekannt.</param>
    private static void RefreshBosAnchor(SequenceStateMachine sm, IReadOnlyList<SwingPoint> swings, int currentIdx, int rightBars)
    {
        if (sm.Point0 == 0 || sm.Point0Index <= 0) return;
        if (sm.IsLong)
        {
            if (sm.LastSwingHighBeforeP0 > 0) return;
            SwingPoint? latestHigh = null;
            for (var k = swings.Count - 1; k >= 0; k--)
            {
                var s = swings[k];
                if (s.CandleIndex >= sm.Point0Index) continue;
                if (s.CandleIndex + rightBars > currentIdx) continue; // Look-Ahead-Schutz
                if (!s.IsHigh) continue;
                latestHigh = s;
                break;
            }
            if (latestHigh != null) sm.LastSwingHighBeforeP0 = latestHigh.Price;
        }
        else
        {
            if (sm.LastSwingLowBeforeP0 > 0) return;
            SwingPoint? latestLow = null;
            for (var k = swings.Count - 1; k >= 0; k--)
            {
                var s = swings[k];
                if (s.CandleIndex >= sm.Point0Index) continue;
                if (s.CandleIndex + rightBars > currentIdx) continue; // Look-Ahead-Schutz
                if (s.IsHigh) continue;
                latestLow = s;
                break;
            }
            if (latestLow != null) sm.LastSwingLowBeforeP0 = latestLow.Price;
        }
    }

    /// <summary>Baut ein Sequence-Objekt aus dem aktuellen State-Machine-Zustand.</summary>
    public Sequence? ToSequence() => ToSequence(null);

    /// <summary>
    /// Baut ein Sequence-Objekt. Der Candles-Parameter bleibt zur Signatur-Kompatibilität erhalten,
    /// wird aber nicht mehr genutzt — WaveCharacter/SequenceType sind nicht Teil des SK-Buchs.
    /// </summary>
    public Sequence? ToSequence(IReadOnlyList<Candle>? candles)
    {
        if (State < SmState.SucheB) return null; // Noch kein vollständiges A-B Paar

        var a = new SwingPoint(Point0, Point0Index, DateTime.MinValue, !IsLong);
        var b = new SwingPoint(PointA, PointAIndex, DateTime.MinValue, IsLong);

        // B-Punkt: Locked wenn aktiviert (bestätigt), Potential bei SucheB (noch instabil).
        // Bei SucheB ist PotentialB ein Trailing-Wert der sich ständig ändert → PointB = null
        // um Deduplizierungsprobleme zu vermeiden (Strategy prüft PointB?.Price).
        SwingPoint? c = null;
        decimal bPrice;
        if (State >= SmState.Aktiviert)
        {
            bPrice = LockedB;
            c = new SwingPoint(LockedB, LockedBIndex, DateTime.MinValue, !IsLong);
        }
        else
        {
            bPrice = PotentialB; // Für Fib-Berechnung nötig, aber PointB bleibt null
        }

        var range = Math.Abs(PointA - Point0);
        if (range <= 0) return null;

        // Buch-konforme Fib-Levels: 50/55.9/61.8/66.7/71/78.6 Retracement, 161.8/200/261.8/423.6 Extension.
        decimal r500, r559, r618, r667, r702, r71, r786;
        decimal ext100, ext1618, ext200, ext2618, ext4236;

        if (IsLong)
        {
            r500 = PointA - range * 0.500m; r559 = PointA - range * 0.559m;
            r618 = PointA - range * 0.618m; r667 = PointA - range * 0.667m;
            r702 = PointA - range * 0.702m; r71 = PointA - range * 0.71m;
            r786 = PointA - range * 0.786m;
            ext100 = bPrice + range;
            ext1618 = bPrice + range * 1.618m; ext200 = bPrice + range * 2.0m;
            ext2618 = bPrice + range * 2.618m; ext4236 = bPrice + range * 4.236m;
        }
        else
        {
            r500 = PointA + range * 0.500m; r559 = PointA + range * 0.559m;
            r618 = PointA + range * 0.618m; r667 = PointA + range * 0.667m;
            r702 = PointA + range * 0.702m; r71 = PointA + range * 0.71m;
            r786 = PointA + range * 0.786m;
            ext100 = bPrice - range;
            ext1618 = bPrice - range * 1.618m; ext200 = bPrice - range * 2.0m;
            ext2618 = bPrice - range * 2.618m; ext4236 = bPrice - range * 4.236m;
        }

        var seqState = State switch
        {
            SmState.SucheB => SequenceState.CorrectionZone,
            SmState.Aktiviert => SequenceState.Active,
            SmState.Abgearbeitet => SequenceState.TargetReached,
            _ => SequenceState.Forming
        };

        return new Sequence
        {
            Point0 = a, PointA = b, PointB = c,
            IsLong = IsLong, State = seqState,
            Retracement500 = r500, Retracement559 = r559,
            Retracement618 = r618, Retracement667 = r667, Retracement702 = r702,
            Retracement71 = r71, Retracement786 = r786,
            Extension100 = ext100,
            Extension1618 = ext1618, Extension200 = ext200,
            Extension2618 = ext2618, Extension4236 = ext4236,
            CreatedAtCandleIndex = Point0Index,
            IsBcZoneInvalid = IsBcZoneInvalid
        };
    }

    /// <summary>
    /// Task 4.9 — Initialisiert die Machine als Bias-Flip-Startpunkt.
    /// Buch: "Wenn eine bullische Sequenz am Punkt 0 bricht, messen sie sofort die neue
    /// bärische Bewegung von oben nach unten (neue 0 nach A) und warten auf das nächste
    /// B-Level in Abwärtsrichtung."
    /// Setzt Point0 auf das alte Extrem, PointA auf den Bruch-Preis und springt direkt in SucheB.
    /// PotentialB wird initial = PointA, weil noch keine Gegen-Korrektur stattgefunden hat.
    /// </summary>
    /// <param name="oldExtreme">Altes Long-Top / Short-Bottom (wird neues Point0 der Gegen-Sequenz).</param>
    /// <param name="breakPrice">Preis bei dem die alte Sequenz gebrochen ist (wird neuer PointA).</param>
    /// <param name="breakIndex">Candle-Index des Bruchs.</param>
    public void InitAsBiasFlip(decimal oldExtreme, decimal breakPrice, int breakIndex)
    {
        Point0 = oldExtreme;
        Point0Index = Math.Max(0, breakIndex - 1);
        PointA = breakPrice;
        PointAIndex = breakIndex;
        PotentialB = breakPrice;
        PotentialBIndex = breakIndex;
        _point0CandleCount = _minPoint0Candles; // Point0 sofort bestätigt
        State = SmState.SucheB;
        PromotedToLarger = true;
        FailedPoint0 = 0;
        FailedPointA = 0;
        Has100ExtensionReached = false;
        IsBcZoneInvalid = false;
        ActivationCandleIndex = -1; // Strukturpunkte-Doku §5A: Neue Sequenz noch nicht aktiviert.
        // Strukturpunkte-Doku §3: BiasFlip verwirft den bisherigen BOS-Anker. Der Orchestrator soll beim
        // nächsten Candle-Tick für die neue Richtung einen frischen Anchor berechnen.
        LastSwingHighBeforeP0 = 0m;
        LastSwingLowBeforeP0 = 0m;
        LastActivationBlockedByBos = false;
    }

    /// <summary>Setzt die State Machine zurück.</summary>
    public void Reset()
    {
        State = SmState.Suche0;
        Point0 = 0; PointA = 0; PotentialB = 0; LockedB = 0;
        CurrentHigh = 0; CurrentLow = 0;
        Has100ExtensionReached = false;
        IsBcZoneInvalid = false; // SK-VERIFY: [BC-Zone-Guard] Flag zurücksetzen
        _point0CandleCount = 0; // SK-FIX: Counter zurücksetzen
        ActivationCandleIndex = -1; // Strukturpunkte-Doku §5A: Volume-Filter-Index auch resetten
        // Strukturpunkte-Doku §3: BOS-Anker verwerfen, damit er beim nächsten Point0-Kandidaten neu berechnet wird.
        LastSwingHighBeforeP0 = 0m;
        LastSwingLowBeforeP0 = 0m;
        LastActivationBlockedByBos = false;
    }

    /// <summary>
    /// Invalidierung in SucheB: Keine Overtracing-Toleranz (noch kein Trade).
    /// SK-VERIFY: [3b.2] Gescheiterte → Größere Sequenz.
    /// SK-FIX: Bei Invalidierung wird P0 sofort als bestätigt markiert (Promote),
    /// damit die SM schneller eine neue (möglicherweise größere) Sequenz erkennt.
    /// Das alte FailedPointA bleibt gespeichert für Analyse-Zwecke.
    /// Hinweis: Die alte Logik (direkt nach SucheB mit PotentialB=P0) war toter Code,
    /// weil die Promote-Bedingung (newExtreme > failedP0 bei Long) bei einer Invalidierung
    /// logisch unmöglich war (Trigger: candle.Low &lt; Point0 → newExtreme &lt; failedP0 IMMER).
    /// Zusätzlich hätte PotentialB=P0 immer 100% B-Retracement ergeben → TryActivate-Reject.
    /// </summary>
    private bool InvalidateAndPromoteSucheB(Candle candle, int index, decimal newExtreme, bool isLong)
    {
        FailedPoint0 = Point0;
        FailedPointA = PointA;

        // SK-Promote: Neues P0 = newExtreme. P0 gilt sofort als bestätigt
        // (war Teil einer gescheiterten Sequenz → das Extrem ist ein echtes Swing-Level).
        // Die SM startet Suche0 mit voller p0CandleCount → bei nächster passender Kerze
        // direkt Übergang zu SucheA (spart die minPoint0Candles-Wartezeit).
        Point0 = newExtreme;
        Point0Index = index;
        PointA = 0;
        _point0CandleCount = _minPoint0Candles; // SK-Promote: P0 sofort bestätigt
        PromotedToLarger = true;
        State = SmState.Suche0;
        // Strukturpunkte-Doku §3: Neues Point0 → BOS-Anker für nächste Aktivierung neu einholen.
        LastSwingHighBeforeP0 = 0m;
        LastSwingLowBeforeP0 = 0m;
        return ProcessSuche0(candle, index);
    }

    // ═══════════════════════════════════════════════════════════════
    // State-Verarbeitung
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Sucht Point 0 (Ursprung der Bewegung). BUCH-REGEL DOCHT-MESSUNG (Task 4.1):
    /// Point 0 wird IMMER am Kerzendocht gemessen — Long: candle.Low, Short: candle.High.
    /// NICHT Close/Open verwenden, da das Smart Money die Liquidität am absoluten Extrem abgreift.
    /// </summary>
    private bool ProcessSuche0(Candle candle, int index)
    {
        if (IsLong)
        {
            // Long: Suche das tiefste Tief (Punkt 0) — Docht-Extrem (Buch: "Wicks/Spikes")
            if (Point0 == 0 || candle.Low < Point0)
            {
                Point0 = candle.Low;
                Debug.Assert(Point0 == candle.Low, "SK-Buch Task 4.1: Point 0 muss aus candle.Low stammen (Docht-Messung).");
                Point0Index = index;
                _point0CandleCount = 0; // SK-FIX: Counter zurücksetzen bei neuem Extrem
            }
            else
            {
                _point0CandleCount++; // SK-FIX: Kerze ohne neues Low → Counter hoch
            }

            // SK-FIX: Point 0 muss mindestens _minPoint0Candles alt sein UND Preis signifikant gestiegen
            // Damit wird sichergestellt dass Point 0 ein echtes Swing-Low ist (Gegenbewegung bestätigt)
            if (Point0 > 0 && _point0CandleCount >= _minPoint0Candles
                && candle.High > Point0 * (1 + _minImpulsePercent / 100m))
            {
                PointA = candle.High;
                PointAIndex = index;
                State = SmState.SucheA;
            }
        }
        else
        {
            // Short: Suche das höchste Hoch (Punkt 0) — Docht-Extrem (Buch: "Wicks/Spikes")
            if (Point0 == 0 || candle.High > Point0)
            {
                Point0 = candle.High;
                Debug.Assert(Point0 == candle.High, "SK-Buch Task 4.1: Point 0 muss aus candle.High stammen (Docht-Messung).");
                Point0Index = index;
                _point0CandleCount = 0; // SK-FIX: Counter zurücksetzen bei neuem Extrem
            }
            else
            {
                _point0CandleCount++; // SK-FIX: Kerze ohne neues High → Counter hoch
            }

            // SK-FIX: Point 0 muss mindestens _minPoint0Candles alt sein UND Preis signifikant gefallen
            if (Point0 > 0 && _point0CandleCount >= _minPoint0Candles
                && candle.Low < Point0 * (1 - _minImpulsePercent / 100m))
            {
                PointA = candle.Low;
                PointAIndex = index;
                State = SmState.SucheA;
            }
        }
        return false;
    }

    /// <summary>
    /// Sucht Point A (Impulsgipfel). BUCH-REGEL DOCHT-MESSUNG (Task 4.1):
    /// PointA wird IMMER am Kerzendocht gemessen — Long: candle.High, Short: candle.Low.
    /// </summary>
    private bool ProcessSucheA(Candle candle, int index)
    {
        // SK-FIX (PointA-Locking): PointA-Update und Korrektur-Check sind mutuell
        // exklusiv pro Kerze. Wenn candle.High ein neues Hoch setzt, wissen wir nicht
        // ob innerhalb der Kerze zuerst das High oder zuerst das Low erreicht wurde
        // (OHLC-Daten enthalten diese Reihenfolge nicht). In diesem Fall KEIN Trigger
        // der B-Suche aus derselben Kerze — sonst käme ein A und ein B aus einer einzigen
        // Kerze, was semantisch kein sauberer Impuls + Korrektur ist.
        // Die B-Suche wird erst in der Folgekerze geprüft, wenn PointA stabil ist.
        if (IsLong)
        {
            // Höheres High → A-Kandidat aktualisieren (Buch: PointA = Docht-High, nicht Close)
            if (candle.High > PointA)
            {
                PointA = candle.High;
                Debug.Assert(PointA == candle.High, "SK-Buch Task 4.1: PointA muss aus candle.High stammen (Docht-Messung).");
                PointAIndex = index;
                return false;
            }

            // PointA stabil: Korrektur-Check gegen das gelockte PointA
            var correctionFromA = (PointA - candle.Low) / PointA * 100m;
            if (correctionFromA >= _correctionThreshold)
            {
                // A ist gelockt → B-Suche mit Trailing Low starten
                PotentialB = candle.Low;
                PotentialBIndex = index;
                State = SmState.SucheB;
            }
        }
        else
        {
            // Short: Tieferes Low → A-Kandidat aktualisieren (Buch: PointA = Docht-Low, nicht Close)
            if (candle.Low < PointA)
            {
                PointA = candle.Low;
                Debug.Assert(PointA == candle.Low, "SK-Buch Task 4.1: PointA muss aus candle.Low stammen (Docht-Messung).");
                PointAIndex = index;
                return false;
            }

            var correctionFromA = (candle.High - PointA) / PointA * 100m;
            if (correctionFromA >= _correctionThreshold)
            {
                PotentialB = candle.High;
                PotentialBIndex = index;
                State = SmState.SucheB;
            }
        }
        return false;
    }

    /// <summary>
    /// Sucht Point B (Korrekturende). BUCH-REGEL DOCHT-MESSUNG (Task 4.1):
    /// PotentialB wird IMMER am Kerzendocht gemessen — Long: candle.Low, Short: candle.High.
    /// </summary>
    private bool ProcessSucheB(Candle candle, int index)
    {
        if (IsLong)
        {
            // Trailing Low: B rutscht mit dem Preis nach unten mit (Buch: Docht-Low, Body ignorieren)
            if (candle.Low < PotentialB)
            {
                PotentialB = candle.Low;
                Debug.Assert(PotentialB == candle.Low, "SK-Buch Task 4.1: PotentialB muss aus candle.Low stammen (Docht-Messung).");
                PotentialBIndex = index;
            }

            // Task 4.2 — Schlusskurs-Regel nach SK-Buch Masterclass:
            // Docht-Pike unter Point0 mit Body-Close in der Korrekturbox → WickOnly (Sequenz bleibt).
            // Body-Close weit unter Box → StrongClose (Reset). Body-Close unter Point0 → Invalidierung.
            if (candle.Low < Point0)
            {
                var rangeLocal = Math.Abs(PointA - Point0);
                var ret500 = PointA - rangeLocal * 0.500m;
                var ret786 = PointA - rangeLocal * 0.786m;
                var classification = CorrectionBoxExitClassifier.Classify(
                    isLong: true, candle, boxUpper: ret500, boxLower: ret786, point0: Point0);
                if (classification == CorrectionBoxExit.FullInvalidation
                    || classification == CorrectionBoxExit.StrongClose)
                {
                    return InvalidateAndPromoteSucheB(candle, index, candle.Low, isLong: true);
                }
                // WickOnly / InBox → Sequenz bleibt aktiv, Point0-Docht wird toleriert.
            }

            // AKTIVIERUNG: Preis durchbricht A (Close über Punkt A)
            if (candle.Close > PointA)
            {
                if (TryActivate(candle, index))
                    return true;
            }
        }
        else
        {
            // Short: Trailing High (Buch: Docht-High, Body ignorieren)
            if (candle.High > PotentialB)
            {
                PotentialB = candle.High;
                Debug.Assert(PotentialB == candle.High, "SK-Buch Task 4.1: PotentialB muss aus candle.High stammen (Docht-Messung).");
                PotentialBIndex = index;
            }

            // Task 4.2 — Schlusskurs-Regel (Short-Variante).
            if (candle.High > Point0)
            {
                var rangeLocal = Math.Abs(PointA - Point0);
                var ret500 = PointA + rangeLocal * 0.500m;
                var ret786 = PointA + rangeLocal * 0.786m;
                var classification = CorrectionBoxExitClassifier.Classify(
                    isLong: false, candle, boxUpper: ret500, boxLower: ret786, point0: Point0);
                if (classification == CorrectionBoxExit.FullInvalidation
                    || classification == CorrectionBoxExit.StrongClose)
                {
                    return InvalidateAndPromoteSucheB(candle, index, candle.High, isLong: false);
                }
            }

            // AKTIVIERUNG: Close unter Punkt A
            if (candle.Close < PointA)
            {
                if (TryActivate(candle, index))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gemeinsame Aktivierungs-Logik für Long und Short.
    /// Prüft Elliott-Wellen-Regeln: Zeit-Proportion + B-Retracement-Validierung.
    /// </summary>
    private bool TryActivate(Candle candle, int index)
    {
        // Zeit-Proportions-Filter: Korrektur darf nicht zu schnell sein (Spike)
        var impulsBars = PointAIndex - Point0Index;
        var corrBars = PotentialBIndex - PointAIndex;
        if (impulsBars > 0 && corrBars < impulsBars * 0.25m)
            return false; // Spike-Korrektur: State bleibt SucheB

        // Elliott B-Retracement-Validierung:
        // B muss 38.2-88.6% der 0→A Strecke retrackieren (gemessen von A).
        // Zu flach (<38.2%) = keine echte Korrektur, nur Konsolidierung.
        // Zu tief (>88.6%) = fast Invalidierung, kein sauberer Wellencharakter.
        var range = Math.Abs(PointA - Point0);
        if (range <= 0) return false;

        // SK-FIX (B-Sanity): PotentialB muss auf der korrekten Seite von PointA liegen.
        // Long: B < A (Korrektur nach unten von Impuls-Gipfel), Short: B > A.
        // Ohne diesen Check würde Math.Abs() Datenfehler (z.B. Wick über PointA in Trailing-Phase)
        // als gültiges Retracement verschleiern und zu TryActivate-Rejects statt klarer Invalidation
        // führen. In diesem Fall: Sequenz ist nicht mehr valide, auf Suche0 zurücksetzen.
        if ((IsLong && PotentialB >= PointA) || (!IsLong && PotentialB <= PointA))
        {
            Reset();
            return false;
        }

        var bRetrace = Math.Abs(PointA - PotentialB) / range;

        // SK-FIX: B-Retracement MUSS zwischen _minBRetracement und _maxBRetracement liegen.
        // SK-System: Punkt B muss eine signifikante Korrektur sein (ideal 50-66.7%, akzeptabel 38.2-78.6%).
        // Ohne Filter wurden "Sequenzen" mit 5% Retracement (= kein echtes B) oder 90% (= fast Invalidierung) aktiviert.
        // Defaults sind jetzt SK-konform: _minBRetracement=0.382, _maxBRetracement=0.786.
        // FibConfidence bleibt als Qualitäts-Score für die Nähe zu idealen Fib-Leveln.
        if (bRetrace < _minBRetracement || bRetrace > _maxBRetracement)
        {
            // Retracement außerhalb des gültigen Bereichs → State bleibt SucheB, B wird weiter getrailed
            return false;
        }

        // Strukturpunkte-Doku §2: Harter Impuls-Distanz-Filter.
        // Doku-Zitat: "IF (Point_A - Point_0) < (ATR_14 * 3): INVALIDATE SEQUENCE.
        // Bedeutung: Die Impuls-Bewegung muss mindestens dreimal so groß sein wie eine durchschnittliche Kerze."
        // Wenn MinImpulseDistance > 0 (von Strategy gesetzt), wird die Sequenz komplett verworfen statt nur SucheB zu bleiben —
        // denn die Rauschen-Grundlage des gesamten Setups ist ungültig (Point0/PointA stammen aus toten Seitwärtsphasen).
        if (MinImpulseDistance > 0 && range < MinImpulseDistance)
        {
            Reset();
            return false;
        }

        // Strukturpunkte-Doku §3: Break-of-Structure-Filter (IMMER aktiv, Buch: "Ohne Strukturbruch keine SK-Messung").
        // Die Aktivierungs-Kerze muss zusätzlich zum PointA-Break auch das letzte markante Pivot-High/Low
        // VOR Point0 überwinden. Fehlt dieser Strukturbruch, bleibt die Sequenz in SucheB — B wird weiter
        // getrailed, bis entweder ein echter BOS stattfindet oder die Sequenz invalidiert wird.
        // Graceful: Wenn der Anchor nicht gesetzt ist (0), wird nicht blockiert (Strategy lieferte zu wenig Historie).
        {
            var breakPrice = BosRequireCloseBreak
                ? candle.Close
                : (IsLong ? candle.High : candle.Low);
            if (IsLong && LastSwingHighBeforeP0 > 0 && breakPrice <= LastSwingHighBeforeP0)
            {
                LastActivationBlockedByBos = true;
                return false;
            }
            if (!IsLong && LastSwingLowBeforeP0 > 0 && breakPrice >= LastSwingLowBeforeP0)
            {
                LastActivationBlockedByBos = true;
                return false;
            }
        }

        // Aktivierung gültig — B einfrieren und Fibonacci berechnen
        LastActivationBlockedByBos = false;
        LockedB = PotentialB;
        LockedBIndex = PotentialBIndex;
        BRetracementRatio = bRetrace;
        FibConfidence = CalculateFibConfidence(bRetrace);
        CalculateExtensions();
        // SK-VERIFY: [2.1] Trailing High/Low initialisieren (Breakout-Kerze als Startwert)
        CurrentHigh = candle.High;
        CurrentLow = candle.Low;
        // Strukturpunkte-Doku §5A: Candle-Index der Breakout-Kerze festhalten für Volume-Filter in der Strategy.
        ActivationCandleIndex = index;
        State = SmState.Aktiviert;
        return true;
    }

    /// <summary>
    /// Berechnet wie nah der B-Punkt an idealen Elliott-Fibonacci-Leveln liegt.
    /// 61.8% = ideal (Score 1.0), je weiter entfernt desto niedriger.
    /// </summary>
    private static decimal CalculateFibConfidence(decimal retracementRatio)
    {
        // Ideale Fib-Level für Welle 2 (B-Punkt): 38.2%, 50%, 61.8%, 78.6%
        decimal[] idealLevels = [0.382m, 0.500m, 0.559m, 0.618m, 0.667m, 0.786m];
        var minDist = idealLevels.Min(level => Math.Abs(retracementRatio - level));
        // Score: 1.0 bei exaktem Treffer, 0.0 bei > 10% Abweichung
        return Math.Max(0m, 1.0m - minDist / 0.10m);
    }

    private bool ProcessAktiviert(Candle candle, int index)
    {
        // SK-VERIFY: [2.1] Trailing High/Low aktualisieren (für dynamische BC-Zone)
        if (IsLong && candle.High > CurrentHigh)
            CurrentHigh = candle.High;
        if (!IsLong && candle.Low < CurrentLow)
            CurrentLow = candle.Low;

        // SK-VERIFY: [2.4] 100er Extension Tracking — für Minimum-Gate
        if (!Has100ExtensionReached)
        {
            if (IsLong && CurrentHigh >= Extension100)
                Has100ExtensionReached = true;
            if (!IsLong && CurrentLow <= Extension100)
                Has100ExtensionReached = true;
        }

        // BUCH-ONLY: Kein 138.2%-Over-Extension-Guard. Das Buch kennt nur TP1 (161.8%) und TP2 (200%).

        // Ziellevel-Check: Sequenz abgearbeitet wenn 161.8% Extension erreicht
        // SK: "Sobald Ziellevel berührt → State auf Abgearbeitet"
        if (IsLong && candle.High >= Extension1618)
        {
            State = SmState.Abgearbeitet;
            return false;
        }
        if (!IsLong && candle.Low <= Extension1618)
        {
            State = SmState.Abgearbeitet;
            return false;
        }

        // Strukturpunkte-Doku: Docht/Close unter Point0 → Sequenz SOFORT invalidiert.
        // Buch kennt keine Overtracing-Toleranz; Liquidity-Grabs durchbrechen Point0 = Sequenz ungültig.
        if (IsLong && candle.Low < Point0)
            return InvalidateAndPromote(candle, index, candle.Low, isLongInvalidation: true);
        if (!IsLong && candle.High > Point0)
            return InvalidateAndPromote(candle, index, candle.High, isLongInvalidation: false);
        return false;
    }

    /// <summary>
    /// Gemeinsame Invalidierungs-Logik nach Aktivierung.
    /// SK-VERIFY: [3b.2] Gescheiterte → Größere Sequenz.
    /// SK-Promote: P0 wird sofort als bestätigt markiert, damit die SM schneller
    /// eine neue Sequenz erkennt. FailedPoint0/FailedPointA bleiben für Analyse gespeichert.
    /// Hinweis: Die alte Logik (direkt nach SucheB mit PotentialB=P0) war toter Code,
    /// weil die Promote-Bedingung (newExtreme > failedP0 bei Long) bei einer Invalidierung
    /// logisch unmöglich war (Trigger: candle.Low &lt; Point0 → newExtreme &lt; failedP0 IMMER).
    /// Zusätzlich hätte PotentialB=P0 immer 100% B-Retracement ergeben → TryActivate-Reject.
    /// </summary>
    private bool InvalidateAndPromote(Candle candle, int index, decimal newExtreme, bool isLongInvalidation)
    {
        FailedPoint0 = Point0;
        FailedPointA = PointA > 0 ? PointA : (isLongInvalidation ? CurrentHigh : CurrentLow);

        // Task 4.9: Bias-Flip-Info festhalten BEVOR State gereset wird.
        // Eine aktivierte Sequenz die jetzt invalidiert wird = professionelle SK-Trader drehen Bias.
        WasActivatedBeforeInvalidation = true;
        LastBreakPrice = newExtreme;
        LastBreakIndex = index;
        LastActivatedExtreme = IsLong ? CurrentHigh : CurrentLow;

        // SK-Promote: Neues P0 = newExtreme, sofort bestätigt.
        // Trade ist gescheitert, aber das neue Extrem ist ein bestätigtes Swing-Level
        // (der Preis hat die gesamte alte Sequenz durchlaufen).
        Point0 = newExtreme;
        Point0Index = index;
        PointA = 0; PotentialB = 0; LockedB = 0;
        CurrentHigh = 0; CurrentLow = 0;
        Has100ExtensionReached = false;
        IsBcZoneInvalid = false; // SK-VERIFY: [BC-Zone-Guard] Flag für neue Sequenz zurücksetzen
        ActivationCandleIndex = -1; // Strukturpunkte-Doku §5A: Aktivierung der alten Sequenz ist nichtig.
        _point0CandleCount = _minPoint0Candles; // SK-Promote: P0 sofort bestätigt
        PromotedToLarger = true;
        State = SmState.Suche0;
        // Strukturpunkte-Doku §3: Neues Point0 → BOS-Anker für neue Sequenz frisch einholen.
        LastSwingHighBeforeP0 = 0m;
        LastSwingLowBeforeP0 = 0m;
        LastActivationBlockedByBos = false;
        return ProcessSuche0(candle, index);
    }

    /// <summary>
    /// Sequenz abgearbeitet (161.8% erreicht) → sofort neue Sequenzsuche starten.
    /// SK-Regel: Eine abgearbeitete Sequenz ist "verbraucht". Der Markt formt danach
    /// neue Sequenzen die wieder gehandelt werden können. Ohne diesen Reset bleibt
    /// die Machine permanent in Abgearbeitet und blockiert alle weiteren Trades.
    /// </summary>
    private bool ProcessAbgearbeitet(Candle candle, int index)
    {
        // SK-VERIFY: [Abweichung #6] GKL MERKEN bevor Reset
        // Im SK-System sind GKLs abgearbeiteter Sequenzen die wertvollsten Kaufzonen.
        var range = Math.Abs(Extension1618 - Point0);
        if (range > 0 && Extension1618 != 0)
        {
            decimal gkl500, gkl667;
            if (IsLong)
            {
                gkl500 = Extension1618 - range * 0.500m;
                gkl667 = Extension1618 - range * 0.667m;
            }
            else
            {
                gkl500 = Extension1618 + range * 0.500m;
                gkl667 = Extension1618 + range * 0.667m;
            }
            CompletedGkls.Add(new CompletedGklEntry(gkl500, gkl667, IsLong, DateTime.UtcNow));
            // Maximal 5 GKLs pro Machine behalten (älteste raus)
            while (CompletedGkls.Count > 5)
                CompletedGkls.RemoveAt(0);
        }

        // Komplett-Reset: Alle Punkte löschen, neue Suche von Grund auf
        Point0 = 0;
        Point0Index = index;
        PointA = 0;
        PotentialB = 0;
        LockedB = 0;
        CurrentHigh = 0;
        CurrentLow = 0;
        Has100ExtensionReached = false;
        IsBcZoneInvalid = false; // SK-VERIFY: [BC-Zone-Guard] Flag für neue Sequenz zurücksetzen
        ActivationCandleIndex = -1; // Strukturpunkte-Doku §5A: Aktivierung der abgearbeiteten Sequenz ist verbraucht.
        _point0CandleCount = 0; // SK-FIX: Counter zurücksetzen
        State = SmState.Suche0;
        // Strukturpunkte-Doku §3: Nach Abarbeitung frischen BOS-Anker einholen.
        LastSwingHighBeforeP0 = 0m;
        LastSwingLowBeforeP0 = 0m;
        LastActivationBlockedByBos = false;
        // Aktuelle Kerze gleich als erste Suche0-Kerze verarbeiten (keine Kerze verschwenden)
        return ProcessSuche0(candle, index);
    }

    private void CalculateExtensions()
    {
        var range = Math.Abs(PointA - Point0);
        if (IsLong)
        {
            Extension100 = LockedB + range;
            Extension1618 = LockedB + range * 1.618m;
            Extension200 = LockedB + range * 2.0m;
            Extension2618 = LockedB + range * 2.618m;
            Extension4236 = LockedB + range * 4.236m;
            Ret500 = PointA - range * 0.500m;
            Ret618 = PointA - range * 0.618m;
            Ret667 = PointA - range * 0.667m;
        }
        else
        {
            Extension100 = LockedB - range;
            Extension1618 = LockedB - range * 1.618m;
            Extension200 = LockedB - range * 2.0m;
            Extension2618 = LockedB - range * 2.618m;
            Extension4236 = LockedB - range * 4.236m;
            Ret500 = PointA + range * 0.500m;
            Ret618 = PointA + range * 0.618m;
            Ret667 = PointA + range * 0.667m;
        }
    }
}

/// <summary>Zustände der SK-Sequenz State Machine.</summary>
public enum SmState
{
    /// <summary>Sucht Punkt 0 (Ursprung der Bewegung — tiefstes Tief / höchstes Hoch).</summary>
    Suche0,
    /// <summary>Sucht Punkt A (Impulsgipfel — höchstes High nach 0). Wird gelockt bei signifikanter Korrektur.</summary>
    SucheA,
    /// <summary>Trailing Low/High: B rutscht mit dem Preis mit. Wartet auf A-Break (Aktivierung).</summary>
    SucheB,
    /// <summary>AKTIVIERT: A durchbrochen, B eingefroren. Trade läuft Richtung Ziellevel.</summary>
    Aktiviert,
    /// <summary>ABGEARBEITET: Ziellevel (161.8%) erreicht. Keine neuen Trades in diese Richtung — nur GKL/Gegensequenz.</summary>
    Abgearbeitet
}

/// <summary>
/// SK-VERIFY: [Abweichung #6] GKL-Eintrag einer abgearbeiteten Sequenz.
/// Im SK-System sind diese die wertvollsten Kaufzonen (tieferer Preis, engerer SL).
/// </summary>
public record CompletedGklEntry(
    decimal Gkl500,       // 50% Retracement der Gesamtstrecke Point0→Extension1618
    decimal Gkl667,       // 66.7% Retracement
    bool IsLong,          // Richtung der abgearbeiteten Sequenz
    DateTime CompletedAt  // Zeitpunkt der Abarbeitung
);
