using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Spec §7 — "Heiliger Gral" / Confluence Engine:
/// "IF (HTF_GKL_Zone overlaps with LTF_BC_Zone) OR (HTF_GKL_Zone overlaps with LTF_Target_Zone_EXT_1618 der Gegenrichtung):
///  Markiere diese Zone als HIGH_PROBABILITY_ZONE."
///
/// Geometrisches Intervall-Overlap-Test zwischen einer HTF-GKL-Zone (aus <see cref="MultiTfGklDetector"/>)
/// und der LTF-BC-Korrekturzone (50-66.7% Retracement der LTF-Sequenz) bzw. der LTF-Zielzone der Gegenrichtung.
/// Preis-zu-Zone-Checks (Punkt-in-Intervall) beantworten nur "ist der Preis gerade dort", geometrisches
/// Overlap beantwortet "liegt eine ganze Zone in einer anderen Zone" — das ist das Buch-Kriterium.
/// </summary>
public static class SkConfluenceZoneOverlap
{
    /// <summary>Geschlossenes Intervall [Low, High] im Preisraum.</summary>
    public readonly record struct PriceZone(decimal Low, decimal High)
    {
        public bool IsValid => High >= Low && High > 0m;
    }

    /// <summary>
    /// True wenn zwei Intervalle sich überlappen (klassisches Overlap-Kriterium:
    /// <c>max(a.Low, b.Low) &lt;= min(a.High, b.High)</c>).
    /// </summary>
    public static bool Overlaps(PriceZone a, PriceZone b)
    {
        if (!a.IsValid || !b.IsValid) return false;
        return Math.Max(a.Low, b.Low) <= Math.Min(a.High, b.High);
    }

    /// <summary>
    /// Berechnet die Breite des Overlap-Bereichs zweier <see cref="PriceZone"/>.
    /// Gibt 0 zurueck wenn kein Overlap besteht.
    /// </summary>
    public static decimal OverlapWidth(PriceZone a, PriceZone b)
    {
        if (!a.IsValid || !b.IsValid) return 0m;
        var lo = Math.Max(a.Low, b.Low);
        var hi = Math.Min(a.High, b.High);
        return hi >= lo ? hi - lo : 0m;
    }

    /// <summary>
    /// v1.5.0 Phase 1 — Mikro-Touch-Schwelle fuer das HTF-Confluence-Hard-Gate.
    /// Pruet ob der Overlap-Bereich zwischen <paramref name="reference"/> und <paramref name="other"/>
    /// mindestens <paramref name="minWidthPercent"/> der <paramref name="reference"/>-Spanne ausmacht.
    /// Damit werden 0-1-Tick-Beruehrungen ("Mikro-Touch") nicht als Confluence gewertet — nur ein
    /// echter geometrischer Schnitt mit nennenswerter Breite gilt.
    /// </summary>
    /// <param name="reference">Referenz-Zone (typisch: LTF-B-Box) — ihre Spanne ist die Bezugsgroesse.</param>
    /// <param name="other">Andere Zone (typisch: HTF-GKL).</param>
    /// <param name="minWidthPercent">Minimal-Breite des Overlaps in Prozent der Referenz-Spanne (z.B. 0.1m = 0.1 %).</param>
    public static bool HasMeaningfulOverlap(PriceZone reference, PriceZone other, decimal minWidthPercent)
    {
        if (!reference.IsValid || !other.IsValid) return false;
        if (minWidthPercent <= 0m) return Overlaps(reference, other);
        var span = reference.High - reference.Low;
        if (span <= 0m) return false;
        var width = OverlapWidth(reference, other);
        return width >= span * (minWidthPercent / 100m);
    }

    /// <summary>
    /// Baut eine <see cref="PriceZone"/> aus zwei Preis-Grenzen. Reihenfolge egal — das Intervall wird sortiert.
    /// </summary>
    public static PriceZone MakeZone(decimal a, decimal b) =>
        new(Math.Min(a, b), Math.Max(a, b));

    /// <summary>
    /// Baut die LTF-BC-Zone aus einer Sequenz (50%-66.7% Retracement der Impuls-Strecke 0→A).
    /// Gibt null zurück wenn die Sequenz noch keine gültigen Retracement-Level hat.
    /// </summary>
    public static PriceZone? BuildBcZone(Sequence seq)
    {
        if (seq.Retracement500 == 0m || seq.Retracement667 == 0m) return null;
        return MakeZone(seq.Retracement500, seq.Retracement667);
    }

    /// <summary>
    /// Baut die "Zielzone der Gegenrichtung" EXT_1618-EXT_200 aus einer Sequenz der GEGENRICHTUNG.
    /// Die Spec: "HTF_GKL_Zone overlaps with LTF_Target_Zone_EXT_1618 der Gegenrichtung" — also die Zone,
    /// in der eine potenzielle Umkehr-Sequenz ihr TP-Ziel hätte, ist ebenfalls ein High-Probability-Pool.
    /// </summary>
    public static PriceZone? BuildTargetZoneOfOpposite(Sequence counterSeq)
    {
        if (counterSeq.Extension1618 == 0m || counterSeq.Extension200 == 0m) return null;
        return MakeZone(counterSeq.Extension1618, counterSeq.Extension200);
    }

    /// <summary>
    /// Spec §7 — "Heiliger Gral"-Check mit externem <see cref="GklHit"/>. Nützlich wenn der Aufrufer
    /// bereits einen Hit (Preis in GKL) vorliegen hat. Für reine Geometrie-Prüfung ohne Preis-Filter bitte
    /// <see cref="EvaluateFromHtf"/> verwenden, das eine beliebige HTF-GKL-Zone akzeptiert.
    /// </summary>
    public static (bool HasOverlap, string Reason, TimeFrame? HtfTf, OverlapKind Kind) Evaluate(
        GklHit? htfGkl,
        Sequence? ltfSeq,
        Sequence? ltfCounterSeq)
    {
        if (htfGkl == null) return (false, string.Empty, null, OverlapKind.None);

        var gklZone = MakeZone(htfGkl.Gkl500, htfGkl.Gkl667);

        // Variante 1: HTF-GKL überlappt mit LTF-BC-Zone.
        if (ltfSeq != null)
        {
            var bcZone = BuildBcZone(ltfSeq);
            if (bcZone.HasValue && Overlaps(gklZone, bcZone.Value))
                return (true, $"HighProb-Overlap: HTF-{htfGkl.Tf}-GKL ∩ LTF-BC", htfGkl.Tf, OverlapKind.GklAndBc);
        }

        // Variante 2: HTF-GKL überlappt mit LTF-Zielzone der Gegenrichtung.
        if (ltfCounterSeq != null && ltfCounterSeq.IsLong != (ltfSeq?.IsLong ?? !ltfCounterSeq.IsLong))
        {
            var targetZone = BuildTargetZoneOfOpposite(ltfCounterSeq);
            if (targetZone.HasValue && Overlaps(gklZone, targetZone.Value))
                return (true, $"HighProb-Overlap: HTF-{htfGkl.Tf}-GKL ∩ LTF-EXT161.8/200 Counter",
                    htfGkl.Tf, OverlapKind.GklAndCounterTarget);
        }

        return (false, string.Empty, null, OverlapKind.None);
    }

    /// <summary>Klassifikation für Logs/Telemetrie — welches Overlap-Muster getroffen hat.</summary>
    public enum OverlapKind
    {
        None = 0,
        /// <summary>HTF-GKL überlappt mit LTF-BC-Korrekturzone (50-66.7%).</summary>
        GklAndBc,
        /// <summary>HTF-GKL überlappt mit LTF-EXT_1.618/2.0-Zielzone der Gegenrichtung.</summary>
        GklAndCounterTarget
    }

    /// <summary>
    /// Spec §7 — Geometrie-Check der ohne <see cref="GklHit"/> (d.h. ohne Preis-in-Zone-Filter) auskommt.
    /// Berechnet die HTF-GKL-Zone aus Weekly-/Daily-Kerzen direkt via <see cref="SequenceDetector.CalculateGKL"/>
    /// und prüft Overlap mit der LTF-BC-Zone bzw. der EXT_1.618-Zielzone einer optionalen Gegensequenz.
    /// Bevorzugt W1 (Master-Zone), Fallback D1.
    /// </summary>
    public static (bool HasOverlap, string Reason, TimeFrame? HtfTf, OverlapKind Kind) EvaluateFromHtf(
        IReadOnlyList<Candle>? weekly,
        IReadOnlyList<Candle>? daily,
        Sequence? ltfSeq,
        Sequence? ltfCounterSeq,
        decimal minWidthPercent = 0m)
    {
        PriceZone? gkl = null;
        TimeFrame? htfTf = null;
        if (weekly is { Count: >= 20 })
        {
            var w = SequenceDetector.CalculateGKL(weekly, swingStrength: 7);
            if (w.HasValue)
            {
                gkl = MakeZone(w.Value.Gkl500, w.Value.Gkl667);
                htfTf = TimeFrame.W1;
            }
        }
        if (gkl == null && daily is { Count: >= 20 })
        {
            var d = SequenceDetector.CalculateGKL(daily, swingStrength: 7);
            if (d.HasValue)
            {
                gkl = MakeZone(d.Value.Gkl500, d.Value.Gkl667);
                htfTf = TimeFrame.D1;
            }
        }
        if (gkl is null) return (false, string.Empty, null, OverlapKind.None);

        var gklZone = gkl.Value;
        if (ltfSeq != null)
        {
            var bcZone = BuildBcZone(ltfSeq);
            if (bcZone.HasValue
                && (minWidthPercent > 0m
                    ? HasMeaningfulOverlap(bcZone.Value, gklZone, minWidthPercent)
                    : Overlaps(gklZone, bcZone.Value)))
                return (true, $"HighProb-Overlap: HTF-{htfTf}-GKL ∩ LTF-BC", htfTf, OverlapKind.GklAndBc);
        }
        if (ltfCounterSeq != null && (ltfSeq == null || ltfCounterSeq.IsLong != ltfSeq.IsLong))
        {
            var targetZone = BuildTargetZoneOfOpposite(ltfCounterSeq);
            if (targetZone.HasValue
                && (minWidthPercent > 0m
                    ? HasMeaningfulOverlap(targetZone.Value, gklZone, minWidthPercent)
                    : Overlaps(gklZone, targetZone.Value)))
                return (true, $"HighProb-Overlap: HTF-{htfTf}-GKL ∩ LTF-EXT161.8/200 Counter",
                    htfTf, OverlapKind.GklAndCounterTarget);
        }
        return (false, string.Empty, null, OverlapKind.None);
    }
}
