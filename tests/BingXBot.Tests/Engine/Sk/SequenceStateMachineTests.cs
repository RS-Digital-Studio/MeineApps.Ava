using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>
/// Regressions-Tests für die SK-State-Machine-Fixes (19.04.2026):
/// 1. PointA-Locking in ProcessSucheA
/// 2. IsInvalidated mit Overtracing-Toleranz
/// 3. BRetracementRatio Sanity-Check (PotentialB auf richtiger Seite von PointA)
///
/// Alle Tests nutzen FromCandlesBoth() (realer Pfad) mit gebauten Candle-Sequenzen.
/// </summary>
public class SequenceStateMachineTests
{
    private static Candle C(int hour, decimal open, decimal high, decimal low, decimal close)
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(hour);
        return new Candle(time, open, high, low, close, 1_000_000m, time.AddHours(4));
    }

    /// <summary>
    /// Baut eine Long-Sequenz die aktiviert wird:
    /// Point0=100, Impuls auf ~108 (PointA), Korrektur auf ~104 (PointB, 50% Retracement),
    /// dann Breakout über PointA → Aktivierung. Extension1618≈112.87, Extension1382≈109.528.
    /// Gesamt: 30 Candles (genügt für minPoint0Candles=1 und 20-Kerzen-Mindestanforderung).
    /// </summary>
    private static List<Candle> BuildActivatedLongSequence(decimal retracementRatio = 0.5m)
    {
        var candles = new List<Candle>();
        // : Point0-Bildung (Kerzen 0-1, Low = 100)
        candles.Add(C(0, 100m, 100m, 100m, 100m));
        candles.Add(C(1, 100m, 100.1m, 100m, 100.05m));
        // : Impuls bis PointA=108 (Kerzen 2-10)
        for (int i = 2; i <= 10; i++)
        {
            var p = 100m + (i - 1) * 0.9m;
            candles.Add(C(i, p, p + 0.1m, p - 0.05m, p + 0.05m));
        }
        // : Korrektur auf Retracement-Level (Kerzen 11-20)
        var pointA = 108.1m; // ~ letzte High in 
        var range = pointA - 100m;
        var target = pointA - range * retracementRatio;
        for (int i = 11; i <= 20; i++)
        {
            var progress = (i - 10) / 10m;
            var p = pointA - (pointA - target) * progress;
            candles.Add(C(i, p, p + 0.05m, p - 0.05m, p));
        }
        // : Breakout (Kerze 21)
        candles.Add(C(21, pointA + 0.1m, pointA + 0.5m, pointA, pointA + 0.4m));
        // Buffer
        for (int i = 22; i < 30; i++)
            candles.Add(C(i, pointA + 0.4m, pointA + 0.5m, pointA + 0.3m, pointA + 0.4m));

        return candles;
    }

    // ═══════════════════════════════════════════════════════════════
    // FIX 1: PointA-Locking in ProcessSucheA
    // Verifiziert über den Aktivierungs-Pfad: Sauberes PointA-Lock
    // führt zu validen Sequenzen bei Standard-Konfiguration.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FromCandlesBoth_LongSequenzMit50ProzentRetracement_WirdAktiviert()
    {
        var candles = BuildActivatedLongSequence(retracementRatio: 0.5m);
        var (primary, longMachine, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);

        primary.Should().NotBeNull();
        longMachine.State.Should().Be(SmState.Aktiviert);
        longMachine.IsLong.Should().BeTrue();
        longMachine.BRetracementRatio.Should().BeInRange(0.382m, 0.786m);
    }

    // ═══════════════════════════════════════════════════════════════
    // IsInvalidated — Buch: Point0-Bruch = sofort ungültig, keine Toleranz
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsInvalidated_StrikterWickVergleich()
    {
        var seqLong = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.UtcNow, false),
            PointA = new SwingPoint(110m, 10, DateTime.UtcNow, true),
            IsLong = true
        };
        var seqShort = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.UtcNow, true),
            PointA = new SwingPoint(90m, 10, DateTime.UtcNow, false),
            IsLong = false
        };

        seqLong.IsInvalidated(100m).Should().BeFalse();
        seqLong.IsInvalidated(99.99m).Should().BeTrue();
        seqLong.IsInvalidated(101m).Should().BeFalse();

        seqShort.IsInvalidated(100m).Should().BeFalse();
        seqShort.IsInvalidated(100.01m).Should().BeTrue();
        seqShort.IsInvalidated(99m).Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════
    // FIX 3: BRetracementRatio Sanity-Check
    // Getestet indirekt: Sequenzen mit absurden B-Retracements
    // führen nicht zu falschen Aktivierungen.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FromCandlesBoth_MitZuFlachemRetracement_AktiviertNicht()
    {
        // Nur 20% Retracement — unter _minBRetracement=0.382
        var candles = BuildActivatedLongSequence(retracementRatio: 0.2m);
        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);

        longMachine.State.Should().NotBe(SmState.Aktiviert);
    }

    [Fact]
    public void FromCandlesBoth_MitZuTiefemRetracement_AktiviertNicht()
    {
        // 90% Retracement — über _maxBRetracement=0.786
        var candles = BuildActivatedLongSequence(retracementRatio: 0.9m);
        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1);

        longMachine.State.Should().NotBe(SmState.Aktiviert);
    }

    // BUCH-ONLY: Tests für 138.2%-OverExtension-Guard entfernt (kein Buch-Konzept).

    // ═══════════════════════════════════════════════════════════════
    // Strukturpunkte-Doku §3 (A3) — BOS-Hardfilter bei Aktivierung
    // ═══════════════════════════════════════════════════════════════

    // Hinweis (24.04.2026): Das BOS-Gate ist seit dem §5A-Refactor immer aktiv
    // (FromCandlesBoth Z. 315 "BOS-Gate ist immer aktiv"). Der frühere Toggle-Parameter
    // requireBosOnActivation und die Property RequireBosOnActivation wurden entfernt —
    // die Tests wurden entsprechend auf den impliziten Aktivzustand umgestellt.

    [Fact]
    public void RequireBos_OhneAnker_AktiviertWieZuvor()
    {
        // Graceful-Path: BOS-Gate aktiv, aber kein Anker (LastSwingHigh=0 aus Pre-Pass nicht gefunden).
        // Die Machine darf trotzdem aktivieren — fehlender Anker blockiert nicht (Doku: "graceful pass-through").
        var candles = BuildActivatedLongSequence(retracementRatio: 0.5m);
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            bosAnchorSwingStrength: 5);

        // Bei dieser simplen Test-Sequenz findet der Pre-Pass kein Pivot-High VOR Point0 (= Index 0/1).
        longM.State.Should().Be(SmState.Aktiviert);
        longM.LastActivationBlockedByBos.Should().BeFalse();
    }

    [Fact]
    public void RequireBos_MitZuHohemAnker_BlockiertAktivierung()
    {
        // Explizit einen hohen Anker setzen, der die Aktivierungs-Kerze (Close ~108.5) überragt → Block.
        var candles = BuildActivatedLongSequence(retracementRatio: 0.5m);
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            lastSwingHighBeforeP0: 200m,     // absichtlich viel höher als jeder Candle-Close
            bosRequireCloseBreak: true);

        // Close der Breakout-Kerze (108.5) < Anker 200 → BOS scheitert → State bleibt SucheB.
        longM.State.Should().Be(SmState.SucheB);
        longM.LastActivationBlockedByBos.Should().BeTrue();
    }

    [Fact]
    public void RequireBos_MitNiedrigemAnker_AktivierungBleibtErhalten()
    {
        // Anker unter dem Activation-Close → BOS passiert → Aktivierung wie immer.
        var candles = BuildActivatedLongSequence(retracementRatio: 0.5m);
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            lastSwingHighBeforeP0: 105m,     // unter Break-Close 108.5
            bosRequireCloseBreak: true);

        longM.State.Should().Be(SmState.Aktiviert);
        longM.LastActivationBlockedByBos.Should().BeFalse();
    }

    [Fact]
    public void RequireBos_DochtErlaubt_WennCloseBreakFalse()
    {
        // bosRequireCloseBreak=false: Der Docht (High) genügt. Sollte trotz hohem Anker aktivieren,
        // wenn irgendwann die High über dem Anker liegt.
        var candles = BuildActivatedLongSequence(retracementRatio: 0.5m);
        // Setze einen Anker, der höher als Close (108.5) aber niedriger als High (108.6) liegt.
        // Die Breakout-Kerze im Helper schließt bei ~108.5 mit High=108.6.
        var anchor = 108.55m;
        var (_, longM, _) = SequenceStateMachine.FromCandlesBoth(
            candles, minImpulsePercent: 0.3m, correctionThreshold: 0.3m, minPoint0Candles: 1,
            lastSwingHighBeforeP0: anchor,
            bosRequireCloseBreak: false);

        longM.State.Should().Be(SmState.Aktiviert);
    }

    [Fact]
    public void RequireBos_Reset_VerwirftAnker()
    {
        // Nach Reset muss der Anker auf 0 zurückgesetzt sein.
        var sm = new SequenceStateMachine()
        {
            LastSwingHighBeforeP0 = 150m,
            LastSwingLowBeforeP0 = 50m
        };
        sm.Reset();
        sm.LastSwingHighBeforeP0.Should().Be(0m);
        sm.LastSwingLowBeforeP0.Should().Be(0m);
        sm.LastActivationBlockedByBos.Should().BeFalse();
    }
}
