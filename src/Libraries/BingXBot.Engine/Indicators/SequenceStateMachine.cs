using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Zustandsautomat für SK-Sequenz-Erkennung mit Trailing Low/High.
/// Eliminiert das Fraktal-Lag-Problem: B-Punkt wird NICHT vorher fixiert,
/// sondern rutscht mit dem Preis mit bis A durchbrochen wird.
///
/// Zustände: SUCHE_0 → SUCHE_A → SUCHE_B (Trailing) → AKTIVIERT → ABGEARBEITET → SUCHE_0 (Neustart)
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
    public decimal Ret500 { get; private set; }
    public decimal Ret618 { get; private set; }
    public decimal Ret667 { get; private set; }

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

    /// <summary>B-Retracement als Ratio (0.0-1.0). Elliott: Ideal 0.382-0.618, max 0.886.</summary>
    public decimal BRetracementRatio { get; private set; }

    // SK-VERIFY: [3.13] Overtracing-Toleranz für Punkt-0-Invalidierung
    /// <summary>ATR-basierte Toleranz für Overtracing (0.3× ATR). Von Strategy gesetzt vor FromCandles().</summary>
    public decimal InvalidationTolerance { get; set; }

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
            case SmState.Gewarnt:
                return ProcessGewarnt(candle, index);
            case SmState.Abgearbeitet:
                return ProcessAbgearbeitet(candle, index);
            default:
                return false;
        }
    }

    /// <summary>Verarbeitet eine komplette Candle-Historie und gibt die erkannte Sequenz zurück.</summary>
    public static SequenceStateMachine? FromCandles(IReadOnlyList<Candle> candles,
        decimal minImpulsePercent = 0.5m, decimal correctionThreshold = 0.3m,
        decimal minBRetracement = 0.382m, decimal maxBRetracement = 0.786m, int minPoint0Candles = 3)
    {
        var (primary, _, _) = FromCandlesBoth(candles, minImpulsePercent, correctionThreshold, minBRetracement, maxBRetracement, minPoint0Candles);
        return primary;
    }

    /// <summary>
    /// Gibt primäre + beide Richtungs-Machines zurück.
    /// Für Sandwich-Check: Gegenrichtung direkt verfügbar ohne doppelte Berechnung.
    /// </summary>
    public static (SequenceStateMachine? primary, SequenceStateMachine longMachine, SequenceStateMachine shortMachine)
        FromCandlesBoth(IReadOnlyList<Candle> candles,
            decimal minImpulsePercent = 0.5m, decimal correctionThreshold = 0.3m,
            decimal minBRetracement = 0.382m, decimal maxBRetracement = 0.786m, int minPoint0Candles = 3)
    {
        var longMachine = new SequenceStateMachine(minImpulsePercent, correctionThreshold, minBRetracement, maxBRetracement, minPoint0Candles);
        var shortMachine = new SequenceStateMachine(minImpulsePercent, correctionThreshold, minBRetracement, maxBRetracement, minPoint0Candles);
        longMachine.IsLong = true;
        shortMachine.IsLong = false;

        if (candles.Count < 20)
            return (null, longMachine, shortMachine);

        bool longActivated = false, shortActivated = false;
        int longActivatedAt = -1, shortActivatedAt = -1;

        for (int i = 0; i < candles.Count; i++)
        {
            if (longMachine.ProcessCandle(candles[i], i))
            {
                longActivated = true;
                longActivatedAt = i;
            }
            if (shortMachine.ProcessCandle(candles[i], i))
            {
                shortActivated = true;
                shortActivatedAt = i;
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

    /// <summary>Baut ein Sequence-Objekt aus dem aktuellen State-Machine-Zustand.</summary>
    public Sequence? ToSequence() => ToSequence(null);

    /// <summary>
    /// Baut ein Sequence-Objekt mit optionaler WaveCharacter-Klassifikation.
    /// Mit Candles: WaveAB/WaveBC + Type werden gesetzt → HasGoodCharacter + IsTradeableType funktionieren.
    /// Ohne Candles: WaveAB/WaveBC = Unknown, Type = Normal (Default).
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

        // Fibonacci berechnen
        decimal r382, r500, r559, r618, r667, r786;
        decimal ext100, ext1272, ext1618, ext200, ext2618;

        if (IsLong)
        {
            r382 = PointA - range * 0.382m; r500 = PointA - range * 0.500m;
            r559 = PointA - range * 0.559m; r618 = PointA - range * 0.618m;
            r667 = PointA - range * 0.667m; r786 = PointA - range * 0.786m;
            ext100 = bPrice + range; ext1272 = bPrice + range * 1.272m;
            ext1618 = bPrice + range * 1.618m; ext200 = bPrice + range * 2.0m;
            ext2618 = bPrice + range * 2.618m;
        }
        else
        {
            r382 = PointA + range * 0.382m; r500 = PointA + range * 0.500m;
            r559 = PointA + range * 0.559m; r618 = PointA + range * 0.618m;
            r667 = PointA + range * 0.667m; r786 = PointA + range * 0.786m;
            ext100 = bPrice - range; ext1272 = bPrice - range * 1.272m;
            ext1618 = bPrice - range * 1.618m; ext200 = bPrice - range * 2.0m;
            ext2618 = bPrice - range * 2.618m;
        }

        var seqState = State switch
        {
            SmState.SucheB => SequenceState.CorrectionZone,
            SmState.Aktiviert => SequenceState.Active,
            SmState.Gewarnt => SequenceState.Active, // Gewarnt zählt als aktiv (Trade läuft noch)
            SmState.Abgearbeitet => SequenceState.TargetReached,
            _ => SequenceState.Forming
        };

        var seq = new Sequence
        {
            // SK-VERIFY: Abweichung #4 — SK-Nomenklatur (Point0=Ursprung, PointA=Impulsgipfel, PointB=Korrekturende)
            Point0 = a, PointA = b, PointB = c,
            IsLong = IsLong, State = seqState,
            Retracement382 = r382, Retracement500 = r500, Retracement559 = r559,
            Retracement618 = r618, Retracement667 = r667, Retracement786 = r786,
            Extension100 = ext100, Extension1272 = ext1272,
            Extension1618 = ext1618, Extension200 = ext200, Extension2618 = ext2618
        };

        // WaveCharacter + Type klassifizieren (wenn Candles verfügbar)
        // Ohne diese Klassifikation sind HasGoodCharacter/IsTradeableType immer false/true (Default)
        if (candles is { Count: > 0 })
        {
            var (waveAB, waveBC) = SequenceDetector.ClassifySequenceCharacter(seq, candles);
            seq.WaveAB = waveAB;
            seq.WaveBC = waveBC;
        }

        return seq;
    }

    /// <summary>Setzt die State Machine zurück.</summary>
    public void Reset()
    {
        State = SmState.Suche0;
        Point0 = 0; PointA = 0; PotentialB = 0; LockedB = 0;
        CurrentHigh = 0; CurrentLow = 0;
        Has100ExtensionReached = false;
        _point0CandleCount = 0; // SK-FIX: Counter zurücksetzen
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
        return ProcessSuche0(candle, index);
    }

    // ═══════════════════════════════════════════════════════════════
    // State-Verarbeitung
    // ═══════════════════════════════════════════════════════════════

    private bool ProcessSuche0(Candle candle, int index)
    {
        if (IsLong)
        {
            // Long: Suche das tiefste Tief (Punkt 0)
            if (Point0 == 0 || candle.Low < Point0)
            {
                Point0 = candle.Low;
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
            // Short: Suche das höchste Hoch (Punkt 0)
            if (Point0 == 0 || candle.High > Point0)
            {
                Point0 = candle.High;
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

    private bool ProcessSucheA(Candle candle, int index)
    {
        if (IsLong)
        {
            // Höheres High → A-Kandidat aktualisieren
            if (candle.High > PointA)
            {
                PointA = candle.High;
                PointAIndex = index;
            }
            // A wird gelockt wenn Preis signifikant korrigiert (zurückfällt)
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
            if (candle.Low < PointA)
            {
                PointA = candle.Low;
                PointAIndex = index;
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

    private bool ProcessSucheB(Candle candle, int index)
    {
        if (IsLong)
        {
            // Trailing Low: B rutscht mit dem Preis nach unten mit
            if (candle.Low < PotentialB)
            {
                PotentialB = candle.Low;
                PotentialBIndex = index;
            }

            // INVALIDIERUNG VOR AKTIVIERUNG: SK-Regel — sobald ein Docht Punkt 0 unterschreitet,
            // ist die Struktur zerstört. Muss VOR der Aktivierung geprüft werden, da eine Kerze
            // sowohl Low < Point0 als auch Close > PointA haben kann (langer Docht + starker Close).
            // Hinweis: In SucheB gibt es keinen WARNED-State (nur bei Aktiviert), da vor der Aktivierung
            // noch kein Trade läuft und Overtracing-Toleranz nicht sinnvoll ist.
            if (candle.Low < Point0)
                return InvalidateAndPromoteSucheB(candle, index, candle.Low, isLong: true);

            // AKTIVIERUNG: Preis durchbricht A (Close über Punkt A)
            if (candle.Close > PointA)
            {
                if (TryActivate(candle, index))
                    return true;
            }
        }
        else
        {
            // Short: Trailing High
            if (candle.High > PotentialB)
            {
                PotentialB = candle.High;
                PotentialBIndex = index;
            }

            // INVALIDIERUNG VOR AKTIVIERUNG: Preis steigt über Punkt 0 → Struktur zerstört
            if (candle.High > Point0)
                return InvalidateAndPromoteSucheB(candle, index, candle.High, isLong: false);

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

        // Aktivierung gültig — B einfrieren und Fibonacci berechnen
        LockedB = PotentialB;
        LockedBIndex = PotentialBIndex;
        BRetracementRatio = bRetrace;
        FibConfidence = CalculateFibConfidence(bRetrace);
        CalculateExtensions();
        // SK-VERIFY: [2.1] Trailing High/Low initialisieren (Breakout-Kerze als Startwert)
        CurrentHigh = candle.High;
        CurrentLow = candle.Low;
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

        // SK-VERIFY: [3.13] Overtracing-Toleranz: Docht unter Point0 → GEWARNT (nicht sofort invalidiert)
        // Toleranz = 0.3× ATR (von Strategy gesetzt). Liquidity-Grabs durchspiessen Point0 oft knapp.
        if (IsLong && candle.Low < Point0)
        {
            // Prüfe ob Close noch über der Toleranzgrenze liegt
            if (InvalidationTolerance > 0 && candle.Close >= Point0 - InvalidationTolerance)
            {
                // Nur Docht — mögliches Overtracing. Warnung, nicht Invalidierung.
                State = SmState.Gewarnt;
                return false;
            }
            // Close deutlich unter Point0 → ENDGÜLTIG invalidiert
            return InvalidateAndPromote(candle, index, candle.Low, isLongInvalidation: true);
        }
        if (!IsLong && candle.High > Point0)
        {
            if (InvalidationTolerance > 0 && candle.Close <= Point0 + InvalidationTolerance)
            {
                State = SmState.Gewarnt;
                return false;
            }
            return InvalidateAndPromote(candle, index, candle.High, isLongInvalidation: false);
        }
        return false;
    }

    /// <summary>
    /// SK-VERIFY: [3.13] Gewarnt-State: Docht hat Point0 durchbrochen, Close war noch OK.
    /// Nächste Kerze(n) entscheiden: Erholung → zurück zu Aktiviert. Bestätigung → Invalidiert.
    /// </summary>
    private bool ProcessGewarnt(Candle candle, int index)
    {
        // Trailing High/Low weiter aktualisieren (Position könnte noch laufen)
        if (IsLong && candle.High > CurrentHigh)
            CurrentHigh = candle.High;
        if (!IsLong && candle.Low < CurrentLow)
            CurrentLow = candle.Low;

        // 100er Extension Tracking auch im Gewarnt-State
        if (!Has100ExtensionReached)
        {
            if (IsLong && CurrentHigh >= Extension100)
                Has100ExtensionReached = true;
            if (!IsLong && CurrentLow <= Extension100)
                Has100ExtensionReached = true;
        }

        // Erholung: Close über/unter Point0 → Overtracing bestätigt, zurück zu Aktiviert
        if (IsLong && candle.Close > Point0)
        {
            State = SmState.Aktiviert;
            return false;
        }
        if (!IsLong && candle.Close < Point0)
        {
            State = SmState.Aktiviert;
            return false;
        }

        // Bestätigung der Invalidierung: Close unter/über Point0 ± Toleranz
        if (IsLong && candle.Close < Point0 - InvalidationTolerance)
            return InvalidateAndPromote(candle, index, candle.Low, isLongInvalidation: true);
        if (!IsLong && candle.Close > Point0 + InvalidationTolerance)
            return InvalidateAndPromote(candle, index, candle.High, isLongInvalidation: false);

        // Noch im Gewarnt-State — weitere Kerze abwarten
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

        // SK-Promote: Neues P0 = newExtreme, sofort bestätigt.
        // Trade ist gescheitert, aber das neue Extrem ist ein bestätigtes Swing-Level
        // (der Preis hat die gesamte alte Sequenz durchlaufen).
        Point0 = newExtreme;
        Point0Index = index;
        PointA = 0; PotentialB = 0; LockedB = 0;
        CurrentHigh = 0; CurrentLow = 0;
        Has100ExtensionReached = false;
        _point0CandleCount = _minPoint0Candles; // SK-Promote: P0 sofort bestätigt
        PromotedToLarger = true;
        State = SmState.Suche0;
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
        _point0CandleCount = 0; // SK-FIX: Counter zurücksetzen
        State = SmState.Suche0;
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
            Ret500 = PointA - range * 0.500m;
            Ret618 = PointA - range * 0.618m;
            Ret667 = PointA - range * 0.667m;
        }
        else
        {
            Extension100 = LockedB - range;
            Extension1618 = LockedB - range * 1.618m;
            Extension200 = LockedB - range * 2.0m;
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
    /// <summary>SK-VERIFY: [3.13] GEWARNT: Docht hat Point0 durchbrochen, aber Close noch OK.
    /// Mögliches Overtracing (Liquidity-Grab). Nächste Kerze entscheidet:
    /// Close über Point0 → zurück zu Aktiviert, Close unter Point0-Toleranz → Invalidiert.</summary>
    Gewarnt,
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
