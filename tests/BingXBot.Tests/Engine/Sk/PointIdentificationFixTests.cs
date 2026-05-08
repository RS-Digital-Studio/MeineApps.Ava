using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>
/// Regressions-Tests für die Punkt-Identifikations-Fixes (04.05.2026):
/// <list type="number">
///   <item>Wick unter Point 0 (Long) bzw. über Point 0 (Short) in <c>SucheB</c> invalidiert die Sequenz
///         hart — auch wenn der Body-Close noch in der Korrekturbox liegt
///         (Strukturpunkte-Doku §4: "Fällt Preis unter Point_0 → sofort Reset").</item>
///   <item>Sequenz bleibt zwischen TP1 (161.8%) und TP2 (200%) im Zustand <c>Aktiviert</c>;
///         erst beim Erreichen von 200% wird sie auf <c>Abgearbeitet</c> gesetzt.</item>
///   <item>PotentialB darf strukturell nie unter Point 0 (Long) bzw. über Point 0 (Short) liegen —
///         der Sanity-Check in <c>TryActivate</c> resettet sonst.</item>
/// </list>
/// </summary>
public class PointIdentificationFixTests
{
    private static Candle C(int hour, decimal open, decimal high, decimal low, decimal close)
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(hour);
        return new Candle(time, open, high, low, close, 1_000_000m, time.AddHours(4));
    }

    /// <summary>
    /// Baut eine Kerzen-Liste die eine Long-Sequenz Point0=100 / PointA=110 / PotentialB≈105 in SucheB liefert.
    /// FromCandlesBoth verarbeitet sie und gibt die LongMachine im SucheB-State zurück.
    /// Die Box 50%-78.6% liegt zwischen 102.14 und 105.0.
    /// </summary>
    private static List<Candle> BuildLongPreSucheB()
    {
        var candles = new List<Candle>();
        // Point0 = 100 (zwei Kerzen für minPoint0Candles=1)
        candles.Add(C(0, 100m, 100m, 100m, 100m));
        candles.Add(C(1, 100m, 100.1m, 100m, 100.05m));
        // Impuls auf PointA = 110
        for (int i = 2; i <= 12; i++)
        {
            var price = 100m + (i - 1) * 1m;
            candles.Add(C(i, price - 0.5m, price + 0.05m, price - 0.55m, price));
        }
        candles.Add(C(13, 110.5m, 110.6m, 109.9m, 110m));
        // Korrektur — Trailing Low auf ≈105 (= 50% Retracement)
        candles.Add(C(14, 110m, 110m, 108m, 108.5m));
        candles.Add(C(15, 108.5m, 108.6m, 106m, 106.5m));
        candles.Add(C(16, 106.5m, 106.6m, 105m, 105.2m));
        // Buffer um auf >= 20 Kerzen zu kommen
        for (int i = 17; i < 22; i++)
            candles.Add(C(i, 105.2m, 105.3m, 105m, 105.1m));
        return candles;
    }

    private static List<Candle> BuildShortPreSucheB()
    {
        var candles = new List<Candle>();
        candles.Add(C(0, 110m, 110m, 110m, 110m));
        candles.Add(C(1, 110m, 110m, 109.95m, 109.95m));
        for (int i = 2; i <= 12; i++)
        {
            var price = 110m - (i - 1) * 1m;
            candles.Add(C(i, price + 0.5m, price + 0.55m, price - 0.05m, price));
        }
        candles.Add(C(13, 99.5m, 100.1m, 99.4m, 100m));
        candles.Add(C(14, 100m, 102m, 100m, 101.5m));
        candles.Add(C(15, 101.5m, 104m, 101.4m, 103.5m));
        candles.Add(C(16, 103.5m, 105m, 103.4m, 104.8m));
        for (int i = 17; i < 22; i++)
            candles.Add(C(i, 104.8m, 104.9m, 104.7m, 104.8m));
        return candles;
    }

    /// <summary>
    /// Vollständig aktivierte Long-Sequenz. PointA wird via Loop auf 111.05 gelockt
    /// (Kerze 12: price=111, High=111.05). Korrektur trailed bis 105 (≈55% Retracement).
    /// Aktivierung in Kerze 17 mit Close=112 strikt &gt; PointA=111.05.
    /// Range = 11.05, LockedB = 105 → Ext1618 ≈ 122.88, Ext200 = 127.10.
    /// </summary>
    private static List<Candle> BuildFullActivatedLong()
    {
        var candles = new List<Candle>();
        candles.Add(C(0, 100m, 100m, 100m, 100m));
        candles.Add(C(1, 100m, 100.1m, 100m, 100.05m));
        for (int i = 2; i <= 12; i++)
        {
            var price = 100m + (i - 1) * 1m;
            candles.Add(C(i, price - 0.5m, price + 0.05m, price - 0.55m, price));
        }
        candles.Add(C(13, 110.5m, 110.6m, 109.9m, 110m));
        candles.Add(C(14, 110m, 110m, 108m, 108.5m));
        candles.Add(C(15, 108.5m, 108.6m, 106m, 106.5m));
        candles.Add(C(16, 106.5m, 106.6m, 105m, 105.2m));
        // Aktivierung: Close strikt > PointA (=111.05).
        candles.Add(C(17, 105m, 112.5m, 105m, 112m));
        // Buffer: keine Bewegung Richtung 161.8%/200%.
        for (int i = 18; i < 40; i++)
            candles.Add(C(i, 112m, 112.2m, 111.8m, 112m));
        return candles;
    }

    /// <summary>
    /// Vollständig aktivierte Short-Sequenz. PointA wird auf 88.95 gelockt (Kerze 12: price=89, Low=88.95).
    /// Korrektur trailed nach oben bis 105 (≈55% Retracement). Aktivierung in Kerze 17 mit Close=88 strikt &lt; PointA.
    /// Range = 21.05, LockedB = 105 → Ext1618 ≈ 70.95, Ext200 ≈ 62.90.
    /// </summary>
    private static List<Candle> BuildFullActivatedShort()
    {
        var candles = new List<Candle>();
        candles.Add(C(0, 110m, 110m, 110m, 110m));
        candles.Add(C(1, 110m, 110m, 109.95m, 109.95m));
        for (int i = 2; i <= 12; i++)
        {
            var price = 110m - (i - 1) * 1m;
            candles.Add(C(i, price + 0.5m, price + 0.55m, price - 0.05m, price));
        }
        candles.Add(C(13, 99.5m, 100.1m, 99.4m, 100m));
        candles.Add(C(14, 100m, 102m, 100m, 101.5m));
        candles.Add(C(15, 101.5m, 104m, 101.4m, 103.5m));
        candles.Add(C(16, 103.5m, 105m, 103.4m, 104.8m));
        // Aktivierung: Close strikt < PointA (=88.95).
        candles.Add(C(17, 105m, 105m, 87.5m, 88m));
        for (int i = 18; i < 40; i++)
            candles.Add(C(i, 88m, 88.2m, 87.8m, 88m));
        return candles;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FIX #1 — Wick unter Point0 invalidiert die Sequenz hart (Strukturpunkte §4)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Long_WickUnterPoint0_BodyInBox_InvalidiertSequenz()
    {
        var pre = BuildLongPreSucheB();
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            pre, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        longM.State.Should().Be(SmState.SucheB);
        longM.Point0.Should().Be(100m);

        // Liquidity-Grab-Kerze: Docht piekst auf 99 (= unter Point0=100), Body-Close bei 103 (= in Box 102.14-105).
        longM.ProcessCandle(C(pre.Count, 105m, 105.2m, 99m, 103m), pre.Count);

        // Buch-strikt: Sequenz invalidiert. InvalidateAndPromoteSucheB setzt Point0 = candle.Low (99)
        // und ruft direkt ProcessSuche0 auf — der Promote-Pfad endet je nach Bedingungen in
        // Suche0/SucheA. Entscheidend ist dass die ALTE Sequenz tot ist und FailedPoint0 dokumentiert ist.
        longM.PromotedToLarger.Should().BeTrue(
            because: "Wick unter Point0 = Liquidity-Grab abgegriffen → Promote (Strukturpunkte §4)");
        longM.FailedPoint0.Should().Be(100m);
        longM.Point0.Should().Be(99m, because: "neues Point0 ist das Wick-Tief des Liquidity-Grabs");
        longM.State.Should().NotBe(SmState.SucheB,
            because: "die ALTE SucheB-Sequenz wurde invalidiert");
    }

    [Fact]
    public void Short_WickUeberPoint0_BodyInBox_InvalidiertSequenz()
    {
        var pre = BuildShortPreSucheB();
        var (_, _, shortM) = SequenceStateMachine.FromCandlesBoth(
            pre, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        shortM.State.Should().Be(SmState.SucheB);
        shortM.Point0.Should().Be(110m);

        // Liquidity-Grab: Docht piekst auf 111 (= über Point0=110), Body-Close bei 107.
        shortM.ProcessCandle(C(pre.Count, 105m, 111m, 104.8m, 107m), pre.Count);

        shortM.PromotedToLarger.Should().BeTrue();
        shortM.FailedPoint0.Should().Be(110m);
        shortM.Point0.Should().Be(111m, because: "neues Point0 ist das Wick-Hoch des Liquidity-Grabs");
        shortM.State.Should().NotBe(SmState.SucheB);
    }

    [Fact]
    public void Long_WickInBox_KeinPoint0Bruch_BleibtInSucheB()
    {
        var pre = BuildLongPreSucheB();
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            pre, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        longM.State.Should().Be(SmState.SucheB);

        // Kerze piekst auf 102 (= in Korrekturbox, klar über Point0=100). Kein Liquidity-Grab.
        longM.ProcessCandle(C(pre.Count, 105m, 105.5m, 102m, 104m), pre.Count);

        // PotentialB sollte mit dem neuen Tief getrailt haben, State bleibt SucheB.
        longM.State.Should().Be(SmState.SucheB);
        longM.PotentialB.Should().Be(102m);
        longM.PromotedToLarger.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FIX #2 — Abgearbeitet erst bei 200% (TP2), nicht bei 161.8% (TP1)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Long_TpHit161_8_BleibtAktiviert()
    {
        var candles = BuildFullActivatedLong();
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        longM.State.Should().Be(SmState.Aktiviert);
        var ext1618 = longM.Extension1618;
        var ext200 = longM.Extension200;

        // Eine Kerze trifft 161.8%, bleibt aber unter 200%.
        var hit161 = ext1618 + 0.1m;
        var below200 = ext200 - 0.5m;
        longM.ProcessCandle(C(candles.Count, below200, hit161, below200 - 0.5m, below200), candles.Count);

        longM.State.Should().Be(SmState.Aktiviert,
            because: "TP1 (161.8%) ist Teil-Exit, Sequenz läuft weiter Richtung TP2 (200%)");
        longM.Has100ExtensionReached.Should().BeTrue();
    }

    [Fact]
    public void Long_TpHit200_GehtNachAbgearbeitet()
    {
        var candles = BuildFullActivatedLong();
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        longM.State.Should().Be(SmState.Aktiviert);
        var ext200 = longM.Extension200;

        // Erste Kerze nach Aktivierung trifft Ext200 — State sollte auf Abgearbeitet wechseln.
        // ProcessCandle setzt nur den State und returned, der eigentliche Reset auf Suche0 erfolgt erst
        // beim nächsten ProcessCandle-Aufruf in ProcessAbgearbeitet.
        longM.ProcessCandle(C(candles.Count, ext200 - 0.5m, ext200 + 0.1m, ext200 - 0.6m, ext200), candles.Count);

        longM.State.Should().Be(SmState.Abgearbeitet);
    }

    [Fact]
    public void Short_TpHit161_8_BleibtAktiviert()
    {
        var candles = BuildFullActivatedShort();
        var (_, _, shortM) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        shortM.State.Should().Be(SmState.Aktiviert);
        var ext1618 = shortM.Extension1618;
        var ext200 = shortM.Extension200;

        // Short: tieferer Preis = mehr Profit. Trifft 161.8%, bleibt über 200%.
        var hit161 = ext1618 - 0.1m;
        var above200 = ext200 + 0.5m;
        shortM.ProcessCandle(C(candles.Count, above200, above200 + 0.5m, hit161, above200), candles.Count);

        shortM.State.Should().Be(SmState.Aktiviert);
    }

    [Fact]
    public void Short_TpHit200_GehtNachAbgearbeitet()
    {
        var candles = BuildFullActivatedShort();
        var (_, _, shortM) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        shortM.State.Should().Be(SmState.Aktiviert);
        var ext200 = shortM.Extension200;

        shortM.ProcessCandle(C(candles.Count, ext200 + 0.5m, ext200 + 0.6m, ext200 - 0.1m, ext200), candles.Count);

        shortM.State.Should().Be(SmState.Abgearbeitet);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FIX #2 (Mapping) — ToSequence mappt Abgearbeitet jetzt auf FullyCompleted
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Long_ToSequence_AbgearbeitetMapptAufFullyCompleted()
    {
        var candles = BuildFullActivatedLong();
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        var ext200 = longM.Extension200;
        longM.ProcessCandle(C(candles.Count, ext200 - 0.5m, ext200 + 0.1m, ext200 - 0.6m, ext200), candles.Count);
        longM.State.Should().Be(SmState.Abgearbeitet);

        var seq = longM.ToSequence();
        seq.Should().NotBeNull();
        seq!.State.Should().Be(SequenceState.FullyCompleted,
            because: "200% = TP2 = vollständig abgearbeitet (vorher fälschlicherweise TargetReached)");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FIX #2 — Direkt das vormals buggy Szenario abdecken:
    // Sequenz erreicht TP1 (161.8%), eine spätere Kerze macht V-Reversal mit Wick
    // unter Point0. Erwartung: ProcessAktiviert invalidiert die noch laufende Sequenz
    // (nicht ProcessAbgearbeitet — der Pfad würde mit der alten Schwelle einen
    // "Geister-Re-Entry" erzeugen, der jetzt eliminiert ist).
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Long_Tp1Hit_DannPoint0Wick_FuehrtZuInvalidateAndPromote_NichtZuAbgearbeitet()
    {
        var candles = BuildFullActivatedLong();
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        longM.State.Should().Be(SmState.Aktiviert);
        var ext1618 = longM.Extension1618;
        var point0Vor = longM.Point0;
        var pointAVor = longM.PointA;

        // Kerze 1: trifft 161.8% (State bleibt Aktiviert nach Fix #2).
        longM.ProcessCandle(
            C(candles.Count, ext1618 - 0.5m, ext1618 + 0.5m, ext1618 - 0.6m, ext1618 - 0.5m),
            candles.Count);
        longM.State.Should().Be(SmState.Aktiviert,
            because: "TP1 = Teil-Exit, Sequenz läuft weiter Richtung TP2");
        longM.Has100ExtensionReached.Should().BeTrue();

        // Kerze 2: V-Reversal — Wick unter Point0 → ProcessAktiviert::InvalidateAndPromote.
        // (Der Wick-Bruch ist die einzige Invalidations-Quelle in ProcessAktiviert; das ist
        // strukturell symmetrisch zum SucheB-Pfad nach Fix #1.)
        var wickUnderP0 = point0Vor - 0.5m;
        longM.ProcessCandle(
            C(candles.Count + 1, ext1618 - 0.5m, ext1618 - 0.4m, wickUnderP0, point0Vor + 0.1m),
            candles.Count + 1);

        // Promote: Bias-Flip-Hint ist gesetzt (eine AKTIVIERTE Sequenz wurde gebrochen),
        // FailedPoint0 = alter Point0, neues Point0 = Wick-Tief.
        longM.PromotedToLarger.Should().BeTrue();
        longM.WasActivatedBeforeInvalidation.Should().BeTrue(
            because: "aktivierte Sequenz wurde invalidiert → Bias-Flip-Hint für Gegen-Sequenz");
        longM.FailedPoint0.Should().Be(point0Vor);
        longM.Point0.Should().Be(wickUnderP0);
        longM.LastActivatedExtreme.Should().BeGreaterThanOrEqualTo(pointAVor,
            because: "altes Aktiv-Extrem (CurrentHigh) ist Mindestpreis für Bias-Flip-PointA");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FIX #2 (Folge) — GKL-Range zieht jetzt auf Ext200 statt Ext1618.
    // Wichtig für BCKL-Re-Entry-Pfad: CompletedGkls werden nach Abarbeitung gespeichert
    // und der Bot triggert Re-Entries in dieser Zone. Ein Wechsel der Range-Definition
    // verschiebt die GKL-Werte signifikant — Test sichert die neue Definition ab.
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Long_AbgearbeitetSpeichertGkl_AusPoint0_BisExt200()
    {
        var candles = BuildFullActivatedLong();
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        var point0 = longM.Point0;
        var ext200 = longM.Extension200;

        // Kerze 1: trifft Ext200 → State Abgearbeitet
        longM.ProcessCandle(
            C(candles.Count, ext200 - 0.5m, ext200 + 0.1m, ext200 - 0.6m, ext200),
            candles.Count);
        longM.State.Should().Be(SmState.Abgearbeitet);

        // Kerze 2: trigger ProcessAbgearbeitet → GKL wird gespeichert + Reset.
        longM.ProcessCandle(C(candles.Count + 1, ext200, ext200 + 0.1m, ext200 - 0.5m, ext200), candles.Count + 1);

        longM.CompletedGkls.Should().HaveCount(1);
        var gkl = longM.CompletedGkls[0];
        var rangeFull = ext200 - point0;
        var expected50 = ext200 - rangeFull * 0.500m;
        var expected667 = ext200 - rangeFull * 0.667m;
        gkl.Gkl500.Should().BeApproximately(expected50, precision: 0.001m,
            because: "GKL50 = 50%-Retracement der Gesamtstrecke Point0→Ext200 (TP2), nicht →Ext1618");
        gkl.Gkl667.Should().BeApproximately(expected667, precision: 0.001m);
        gkl.IsLong.Should().BeTrue();
    }

    [Fact]
    public void Short_AbgearbeitetSpeichertGkl_SymmetrischZurRange()
    {
        var candles = BuildFullActivatedShort();
        var (_, _, shortM) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);
        var point0 = shortM.Point0;
        var ext200 = shortM.Extension200;

        shortM.ProcessCandle(
            C(candles.Count, ext200 + 0.5m, ext200 + 0.6m, ext200 - 0.1m, ext200),
            candles.Count);
        shortM.State.Should().Be(SmState.Abgearbeitet);

        shortM.ProcessCandle(C(candles.Count + 1, ext200, ext200 + 0.5m, ext200 - 0.1m, ext200), candles.Count + 1);

        shortM.CompletedGkls.Should().HaveCount(1);
        var gkl = shortM.CompletedGkls[0];
        var rangeFull = point0 - ext200;
        // Short: GKL liegt OBERHALB von Ext200 (Pullback nach oben).
        var expected50 = ext200 + rangeFull * 0.500m;
        var expected667 = ext200 + rangeFull * 0.667m;
        gkl.Gkl500.Should().BeApproximately(expected50, precision: 0.001m);
        gkl.Gkl667.Should().BeApproximately(expected667, precision: 0.001m);
        gkl.IsLong.Should().BeFalse();
    }
}
