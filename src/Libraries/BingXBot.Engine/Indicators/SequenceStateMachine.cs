using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Zustandsautomat für SK-Sequenz-Erkennung mit Trailing Low/High.
/// Eliminiert das Fraktal-Lag-Problem: B-Punkt wird NICHT vorher fixiert,
/// sondern rutscht mit dem Preis mit bis A durchbrochen wird.
///
/// Zustände: SUCHE_0 → SUCHE_A → SUCHE_B (Trailing) → AKTIVIERT
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

    // Konfiguration
    private readonly decimal _minImpulsePercent;  // Min. Impuls-Größe (0→A) in % vom Preis
    private readonly decimal _correctionThreshold; // Min. Korrektur von A um A zu locken (in %)

    public SequenceStateMachine(decimal minImpulsePercent = 0.5m, decimal correctionThreshold = 0.3m)
    {
        _minImpulsePercent = minImpulsePercent;
        _correctionThreshold = correctionThreshold;
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
            default:
                return false;
        }
    }

    /// <summary>Verarbeitet eine komplette Candle-Historie und gibt die erkannte Sequenz zurück.</summary>
    public static SequenceStateMachine? FromCandles(IReadOnlyList<Candle> candles,
        decimal minImpulsePercent = 0.5m, decimal correctionThreshold = 0.3m)
    {
        if (candles.Count < 20) return null;

        // Versuche Long UND Short, nimm die mit Aktivierung (oder die aktuellere)
        var longMachine = new SequenceStateMachine(minImpulsePercent, correctionThreshold);
        var shortMachine = new SequenceStateMachine(minImpulsePercent, correctionThreshold);
        longMachine.IsLong = true;
        shortMachine.IsLong = false;

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
        if (longActivated && shortActivated)
            return longActivatedAt > shortActivatedAt ? longMachine : shortMachine;
        if (longActivated) return longMachine;
        if (shortActivated) return shortMachine;

        // Keine Aktivierung — gib die am weitesten fortgeschrittene zurück
        if (longMachine.State > shortMachine.State) return longMachine;
        if (shortMachine.State > longMachine.State) return shortMachine;
        return longMachine; // Default: Long
    }

    /// <summary>Baut ein Sequence-Objekt aus dem aktuellen State-Machine-Zustand.</summary>
    public Sequence? ToSequence()
    {
        if (State < SmState.SucheB) return null; // Noch kein vollständiges A-B Paar

        var a = new SwingPoint(Point0, Point0Index, DateTime.MinValue, !IsLong);
        var b = new SwingPoint(PointA, PointAIndex, DateTime.MinValue, IsLong);

        // B-Punkt: Locked wenn aktiviert, sonst Potential
        var bPrice = State == SmState.Aktiviert ? LockedB : PotentialB;
        var bIndex = State == SmState.Aktiviert ? LockedBIndex : PotentialBIndex;
        var c = new SwingPoint(bPrice, bIndex, DateTime.MinValue, !IsLong);

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
            _ => SequenceState.Forming
        };

        return new Sequence
        {
            PointA = a, PointB = b, PointC = c,
            IsLong = IsLong, State = seqState,
            Retracement382 = r382, Retracement500 = r500, Retracement559 = r559,
            Retracement618 = r618, Retracement667 = r667, Retracement786 = r786,
            Extension100 = ext100, Extension1272 = ext1272,
            Extension1618 = ext1618, Extension200 = ext200, Extension2618 = ext2618
        };
    }

    /// <summary>Setzt die State Machine zurück.</summary>
    public void Reset()
    {
        State = SmState.Suche0;
        Point0 = 0; PointA = 0; PotentialB = 0; LockedB = 0;
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
            }
            // Wenn Preis signifikant steigt → Punkt 0 gefunden, suche A
            if (Point0 > 0 && candle.High > Point0 * (1 + _minImpulsePercent / 100m))
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
            }
            if (Point0 > 0 && candle.Low < Point0 * (1 - _minImpulsePercent / 100m))
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

            // AKTIVIERUNG: Preis durchbricht A (Close über Punkt A)
            if (candle.Close > PointA)
            {
                LockedB = PotentialB;
                LockedBIndex = PotentialBIndex;
                CalculateExtensions();
                State = SmState.Aktiviert;
                return true; // SIGNAL!
            }

            // INVALIDIERUNG: Preis fällt unter Punkt 0
            if (candle.Low < Point0)
            {
                // Sequenz zerstört → neu starten mit aktuellem Low als Punkt 0
                Point0 = candle.Low;
                Point0Index = index;
                PointA = 0;
                State = SmState.Suche0;
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

            // AKTIVIERUNG: Close unter Punkt A
            if (candle.Close < PointA)
            {
                LockedB = PotentialB;
                LockedBIndex = PotentialBIndex;
                CalculateExtensions();
                State = SmState.Aktiviert;
                return true;
            }

            // INVALIDIERUNG: Preis steigt über Punkt 0
            if (candle.High > Point0)
            {
                Point0 = candle.High;
                Point0Index = index;
                PointA = 0;
                State = SmState.Suche0;
            }
        }
        return false;
    }

    private bool ProcessAktiviert(Candle candle, int index)
    {
        // Invalidierung: Preis fällt unter Punkt 0 (Long) oder steigt über Punkt 0 (Short)
        // → Sequenz zerstört, State Machine resettet
        if (IsLong && candle.Close < Point0)
        {
            Point0 = candle.Low;
            Point0Index = index;
            PointA = 0; PotentialB = 0; LockedB = 0;
            State = SmState.Suche0;
        }
        else if (!IsLong && candle.Close > Point0)
        {
            Point0 = candle.High;
            Point0Index = index;
            PointA = 0; PotentialB = 0; LockedB = 0;
            State = SmState.Suche0;
        }
        return false;
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
    Aktiviert
}
