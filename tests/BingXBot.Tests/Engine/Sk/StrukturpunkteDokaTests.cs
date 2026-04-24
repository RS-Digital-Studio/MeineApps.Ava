using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>
/// Regressions-Tests fuer die 4 Anforderungen aus "Algorithmische Erkennung der Strukturpunkte.docx":
///   §1 Asymmetrische Pivot-Fenster (Left_Bars 5-10, Right_Bars 3-5)
///   §2 Harte Impuls-Distanz-Schranke (|PointA - Point0| &gt;= ATR_14 x 3)
///   §5A Volumen-Anomaly-Filter (BOS_Vol &gt;= SMA20 x 1.5 als Hard-Block)
///   §5B Adaptive Pivot-Laenge (ATR-gekoppelt 3-10 Kerzen)
/// </summary>
public class StrukturpunkteDokaTests
{
    private static Candle C(int hour, decimal open, decimal high, decimal low, decimal close, decimal volume = 1_000_000m)
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(hour);
        return new Candle(time, open, high, low, close, volume, time.AddHours(4));
    }

    // ═══════════════════════════════════════════════════════════════
    // §1: Asymmetrische Pivot-Fenster
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FindSwingPoints_Asymmetrisch_ErkenntPivotMitWenigerRechtenBars()
    {
        // Baut eine Spitze bei Index 7 (Left=5, Right=3)
        var candles = new List<Candle>();
        for (int i = 0; i < 15; i++)
        {
            var price = 100m + Math.Abs(i - 7) * -1m; // Peak bei i=7 (Preis 100)
            candles.Add(C(i, price, price, price, price));
        }
        // Ueberhoehe den Peak, damit er eindeutig erkennbar ist
        candles[7] = C(7, 101m, 101m, 101m, 101m);

        var swings = SequenceDetector.FindSwingPoints(candles, leftBars: 5, rightBars: 3);
        swings.Should().Contain(s => s.IsHigh && s.CandleIndex == 7);
    }

    [Fact]
    public void FindSwingPoints_AsymmetrischLeftGroesser_FiltertSchwacheSwings()
    {
        // Nur 2 Kerzen links, 5 rechts waere zu strikt — sym. Version findet Peak, asym nicht mehr
        var candles = new List<Candle>();
        for (int i = 0; i < 15; i++)
        {
            candles.Add(C(i, 100m, 100m, 100m, 100m));
        }
        // Zwei Peaks dicht beieinander (bei 5 und 7)
        candles[5] = C(5, 101m, 101m, 101m, 101m);
        candles[7] = C(7, 101m, 101m, 101m, 101m); // Gleiche Hoehe → gegenseitige Disqualifikation mit Left=5

        var symmetric = SequenceDetector.FindSwingPoints(candles, strength: 5);
        // Beide Peaks werden bei Left=5/Right=5 wegen der >= - Disqualifikation ausgefiltert
        symmetric.Should().NotContain(s => s.IsHigh && (s.CandleIndex == 5 || s.CandleIndex == 7));

        // Bei Left=1, Right=1 sollten beide Peaks als Swing-High erkannt werden
        var asymmetric = SequenceDetector.FindSwingPoints(candles, leftBars: 1, rightBars: 1);
        asymmetric.Should().Contain(s => s.IsHigh && s.CandleIndex == 5);
        asymmetric.Should().Contain(s => s.IsHigh && s.CandleIndex == 7);
    }

    [Fact]
    public void FindSwingPoints_AsymmetrischMitUngueltigenParametern_Graceful()
    {
        var candles = new List<Candle>();
        for (int i = 0; i < 10; i++) candles.Add(C(i, 100m, 100m, 100m, 100m));

        // Negative Parameter werden auf 1 geklemmt, darf nicht crashen
        var swings = SequenceDetector.FindSwingPoints(candles, leftBars: -3, rightBars: 0);
        swings.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // §2: Harte Impuls-Distanz-Schranke (ATR_14 x 3)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MinImpulseDistance_ZuKleinerImpuls_WirdVerworfen()
    {
        var candles = BuildSmallImpulseLongSequence();

        // Ohne Filter: Sequenz wuerde aktivieren
        var (_, longMachineOhne, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.05m, correctionThreshold: 0.05m, minPoint0Candles: 1,
            minImpulseDistance: 0m);
        longMachineOhne.State.Should().Be(SmState.Aktiviert);

        // Mit Filter (riesige Mindest-Distanz): Sequenz verworfen
        var (_, longMachineMit, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.05m, correctionThreshold: 0.05m, minPoint0Candles: 1,
            minImpulseDistance: 100m); // Unerreichbar gross
        longMachineMit.State.Should().NotBe(SmState.Aktiviert);
    }

    [Fact]
    public void MinImpulseDistance_AusreichenderImpuls_Aktiviert()
    {
        var candles = BuildStandardLongSequence();

        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            minImpulseDistance: 1m); // 1 Preis-Einheit, Impuls ist 8 → erfuellt
        longMachine.State.Should().Be(SmState.Aktiviert);
    }

    [Fact]
    public void MinImpulseDistance_NullDeaktiviert_KeinBlock()
    {
        var candles = BuildStandardLongSequence();

        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            minImpulseDistance: 0m);
        longMachine.State.Should().Be(SmState.Aktiviert);
    }

    // ═══════════════════════════════════════════════════════════════
    // §3: Break of Structure (BOS) als harte Aktivierungs-Schranke
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Bos_BreakKerzeUnterLastSwingHigh_Blockiert()
    {
        // Standard-Long-Sequenz mit PointA um 108.1. Wenn BOS-Anker = 200 (unerreichbar) → keine Aktivierung.
        var candles = BuildStandardLongSequence();
        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            lastSwingHighBeforeP0: 200m); // Anker weit oberhalb der realistischen Break-Kerze

        longMachine.State.Should().NotBe(SmState.Aktiviert);
        longMachine.LastActivationBlockedByBos.Should().BeTrue();
    }

    [Fact]
    public void Bos_BreakKerzeUeberLastSwingHigh_Aktiviert()
    {
        // Standard-Long-Sequenz, Breakout-Close ~108.5 → Anker 105 (erreichbar) lässt Aktivierung zu.
        var candles = BuildStandardLongSequence();
        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            lastSwingHighBeforeP0: 105m); // Anker erreichbar (Breakout-Kerze hat Close > 108)

        longMachine.State.Should().Be(SmState.Aktiviert);
        longMachine.LastActivationBlockedByBos.Should().BeFalse();
    }

    [Fact]
    public void Bos_OhneAnker_GracefulPass()
    {
        var candles = BuildStandardLongSequence();
        // Anker = 0 (unbekannt) → Doku-Graceful: BOS-Check übersprungen, Aktivierung normal.
        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            lastSwingHighBeforeP0: 0m, lastSwingLowBeforeP0: 0m);

        longMachine.State.Should().Be(SmState.Aktiviert);
    }

    [Fact]
    public void Bos_WickBreakNurMitBosRequireCloseBreakFalse()
    {
        var candles = BuildStandardLongSequence();
        // Anker knapp unter Docht-High (108.6 ~ high der Break-Kerze = pointA + 0.5 = 108.6),
        // aber über dem Close (108.5 ~ pointA + 0.4).
        // bosRequireCloseBreak=true: blockiert (Close < Anker)
        var (_, lmClose, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            lastSwingHighBeforeP0: 108.55m, bosRequireCloseBreak: true);
        lmClose.State.Should().NotBe(SmState.Aktiviert);

        // bosRequireCloseBreak=false: akzeptiert (Docht-High > Anker)
        var (_, lmWick, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            lastSwingHighBeforeP0: 108.55m, bosRequireCloseBreak: false);
        lmWick.State.Should().Be(SmState.Aktiviert);
    }

    // ═══════════════════════════════════════════════════════════════
    // §5A: Aktivierungs-Kerze-Index wird gesetzt (Volumen-Filter-Anker)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ActivationCandleIndex_WirdNachAktivierungGesetzt()
    {
        var candles = BuildStandardLongSequence();

        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);

        longMachine.State.Should().Be(SmState.Aktiviert);
        longMachine.ActivationCandleIndex.Should().BeGreaterThanOrEqualTo(0);
        longMachine.ActivationCandleIndex.Should().BeLessThan(candles.Count);
    }

    [Fact]
    public void ActivationCandleIndex_OhneAktivierung_MinusEins()
    {
        // Zu wenige Kerzen → State bleibt Suche0, Index = -1
        var candles = new List<Candle>();
        for (int i = 0; i < 5; i++) candles.Add(C(i, 100m, 100m, 100m, 100m));

        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(candles);
        longMachine.ActivationCandleIndex.Should().Be(-1);
    }

    [Fact]
    public void Reset_SetztActivationCandleIndexAufMinusEins()
    {
        var candles = BuildStandardLongSequence();
        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        longMachine.State.Should().Be(SmState.Aktiviert);
        longMachine.ActivationCandleIndex.Should().BeGreaterThanOrEqualTo(0);

        longMachine.Reset();
        longMachine.ActivationCandleIndex.Should().Be(-1);
    }

    // ═══════════════════════════════════════════════════════════════
    // §5B: Adaptive Pivot-Laenge — Helper-Methoden-Indirekt-Test
    // (Die direkte Methode ist private in SequenzKonzeptStrategy, wir testen die oeffentliche FindSwingPoints-Facette)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FindSwingPoints_SymmetricUndAsymmetric_VerhaltenKonsistent()
    {
        var candles = BuildStandardLongSequence();

        // strength=5 muss identisch sein zu left=5,right=5
        var sym = SequenceDetector.FindSwingPoints(candles, strength: 5);
        var asymIdent = SequenceDetector.FindSwingPoints(candles, leftBars: 5, rightBars: 5);
        asymIdent.Count.Should().Be(sym.Count);
        for (int i = 0; i < sym.Count; i++)
        {
            asymIdent[i].CandleIndex.Should().Be(sym[i].CandleIndex);
            asymIdent[i].Price.Should().Be(sym[i].Price);
            asymIdent[i].IsHigh.Should().Be(sym[i].IsHigh);
        }
    }

    [Fact]
    public void FindSwingPoints_ZuWenigCandles_ReturnsEmpty()
    {
        var candles = new List<Candle>();
        for (int i = 0; i < 5; i++) candles.Add(C(i, 100m, 100m, 100m, 100m));

        // left=5 + right=5 + 1 = 11 Kerzen benoetigt, nur 5 vorhanden → leer
        var swings = SequenceDetector.FindSwingPoints(candles, leftBars: 5, rightBars: 5);
        swings.Should().BeEmpty();
    }

    // Hinweis: Spec §7 HighProbabilityZone-Tests liegen in ConfluenceScoringTests.cs (Scorer-Unit).

    // ═══════════════════════════════════════════════════════════════
    // §2 + §3 Short-Richtungs-Parität (Compliance-Audit-Nachzug)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MinImpulseDistance_ShortSequenz_WirdBeiZuKleinemImpulsVerworfen()
    {
        var candles = BuildStandardShortSequence();

        var (_, _, shortMachineOhne) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            minImpulseDistance: 0m);
        shortMachineOhne.State.Should().Be(SmState.Aktiviert);

        var (_, _, shortMachineMit) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            minImpulseDistance: 100m);
        shortMachineMit.State.Should().NotBe(SmState.Aktiviert);
    }

    [Fact]
    public void Bos_ShortSequenzMitUnerreichbaremAnchor_Blockiert()
    {
        var candles = BuildStandardShortSequence();

        var (_, _, shortMachine) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            lastSwingLowBeforeP0: 1m); // Anker weit unter realistischem Break-Close → unerreichbar

        shortMachine.State.Should().NotBe(SmState.Aktiviert);
        shortMachine.LastActivationBlockedByBos.Should().BeTrue();
    }

    [Fact]
    public void Bos_ShortSequenzMitErreichbaremAnchor_Aktiviert()
    {
        var candles = BuildStandardShortSequence();

        var (_, _, shortMachine) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            lastSwingLowBeforeP0: 95m); // Anker über dem zu erwartenden Breakout-Close (~91.5)

        shortMachine.State.Should().Be(SmState.Aktiviert);
        shortMachine.LastActivationBlockedByBos.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper: Candle-Sequenzen
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Long-Sequenz mit kleinem Impuls (0 -> 100.5, also nur 0.5 Preis-Einheiten Impuls).
    /// Mit sehr niedrigem minImpulsePercent=0.05 waere die Sequenz ohne Filter aktivierbar,
    /// aber minImpulseDistance=100 schlaegt das ab.
    /// </summary>
    private static List<Candle> BuildSmallImpulseLongSequence()
    {
        var candles = new List<Candle>();
        candles.Add(C(0, 100m, 100m, 100m, 100m));
        candles.Add(C(1, 100m, 100m, 100m, 100m));
        // Kleiner Impuls 100 → 100.5
        for (int i = 2; i <= 10; i++)
        {
            var p = 100m + (i - 1) * 0.05m;
            candles.Add(C(i, p, p + 0.01m, p - 0.01m, p + 0.01m));
        }
        // Korrektur auf 50% Retracement (100.25)
        var pointA = 100.5m;
        for (int i = 11; i <= 20; i++)
        {
            var p = pointA - (pointA - 100.25m) * (i - 10) / 10m;
            candles.Add(C(i, p, p + 0.01m, p - 0.01m, p));
        }
        // Breakout
        candles.Add(C(21, pointA + 0.01m, pointA + 0.1m, pointA, pointA + 0.1m));
        for (int i = 22; i < 30; i++)
            candles.Add(C(i, pointA + 0.1m, pointA + 0.1m, pointA + 0.05m, pointA + 0.1m));
        return candles;
    }

    /// <summary>Standard-Short-Sequenz (invers zu BuildStandardLongSequence): 100 → 92.</summary>
    private static List<Candle> BuildStandardShortSequence()
    {
        var candles = new List<Candle>();
        candles.Add(C(0, 100m, 100m, 100m, 100m));
        candles.Add(C(1, 100m, 100m, 99.9m, 99.95m));
        for (int i = 2; i <= 10; i++)
        {
            var p = 100m - (i - 1) * 0.9m;
            candles.Add(C(i, p, p + 0.05m, p - 0.1m, p - 0.05m));
        }
        var pointA = 91.9m;
        var range = 100m - pointA;
        var target = pointA + range * 0.5m;
        for (int i = 11; i <= 20; i++)
        {
            var progress = (i - 10) / 10m;
            var p = pointA + (target - pointA) * progress;
            candles.Add(C(i, p, p + 0.05m, p - 0.05m, p));
        }
        // Short-Breakout: Close unter PointA
        candles.Add(C(21, pointA - 0.1m, pointA, pointA - 0.5m, pointA - 0.4m));
        for (int i = 22; i < 30; i++)
            candles.Add(C(i, pointA - 0.4m, pointA - 0.3m, pointA - 0.5m, pointA - 0.4m));
        return candles;
    }

    /// <summary>Standard-Long-Sequenz mit ausreichendem Impuls (100 → 108).</summary>
    private static List<Candle> BuildStandardLongSequence()
    {
        var candles = new List<Candle>();
        candles.Add(C(0, 100m, 100m, 100m, 100m));
        candles.Add(C(1, 100m, 100.1m, 100m, 100.05m));
        for (int i = 2; i <= 10; i++)
        {
            var p = 100m + (i - 1) * 0.9m;
            candles.Add(C(i, p, p + 0.1m, p - 0.05m, p + 0.05m));
        }
        var pointA = 108.1m;
        var range = pointA - 100m;
        var target = pointA - range * 0.5m;
        for (int i = 11; i <= 20; i++)
        {
            var progress = (i - 10) / 10m;
            var p = pointA - (pointA - target) * progress;
            candles.Add(C(i, p, p + 0.05m, p - 0.05m, p));
        }
        candles.Add(C(21, pointA + 0.1m, pointA + 0.5m, pointA, pointA + 0.4m));
        for (int i = 22; i < 30; i++)
            candles.Add(C(i, pointA + 0.4m, pointA + 0.5m, pointA + 0.3m, pointA + 0.4m));
        return candles;
    }
}
