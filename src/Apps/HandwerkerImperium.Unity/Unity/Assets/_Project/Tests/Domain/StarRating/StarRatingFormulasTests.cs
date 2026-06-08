using NUnit.Framework;
using HandwerkerImperium.Domain.StarRating;

namespace HandwerkerImperium.Domain.Tests.StarRating
{
    /// <summary>
    /// P1-Verifikation des Stern-Ratings: gewichteter Aggregat-Score, rohe Schwellen-Stufung und die
    /// Hysterese (steigt sofort, fällt erst unter Schwelle minus Puffer) — das Anti-Flacker-Verhalten.
    /// </summary>
    [TestFixture]
    public class StarRatingFormulasTests
    {
        // Beispiel-Schwellen (in echt aus BalancingConfig): 2★@100, 3★@300, 4★@600, 5★@1000.
        private static readonly double[] Thresholds = { 100, 300, 600, 1000 };
        private const double Buffer = 50;

        [Test]
        public void Score_IsWeightedAggregate_NonNegative()
        {
            // 3 Werkstätten*50 + 2 Bauphasen*40 + 10 Aufträge*2 = 150 + 80 + 20 = 250
            Assert.That(StarRatingFormulas.Score(3, 2, 10, 50, 40, 2), Is.EqualTo(250).Within(1e-9));
            // negative Eingaben werden als 0 behandelt
            Assert.That(StarRatingFormulas.Score(-3, -2, -10, 50, 40, 2), Is.EqualTo(0).Within(1e-9));
        }

        [Test]
        public void RawStar_CrossesEachThreshold()
        {
            Assert.That(StarRatingFormulas.RawStar(0, Thresholds), Is.EqualTo(1));
            Assert.That(StarRatingFormulas.RawStar(99, Thresholds), Is.EqualTo(1));
            Assert.That(StarRatingFormulas.RawStar(100, Thresholds), Is.EqualTo(2));
            Assert.That(StarRatingFormulas.RawStar(300, Thresholds), Is.EqualTo(3));
            Assert.That(StarRatingFormulas.RawStar(599, Thresholds), Is.EqualTo(3));
            Assert.That(StarRatingFormulas.RawStar(600, Thresholds), Is.EqualTo(4));
            Assert.That(StarRatingFormulas.RawStar(1000, Thresholds), Is.EqualTo(5));
            Assert.That(StarRatingFormulas.RawStar(99999, Thresholds), Is.EqualTo(5), "5★ ist Maximum bei 4 Schwellen");
        }

        [Test]
        public void EvaluateStars_RisesImmediately()
        {
            // currentStar 2, Score springt auf 4★-Niveau -> sofort 4
            Assert.That(StarRatingFormulas.EvaluateStars(650, 2, Thresholds, Buffer), Is.EqualTo(4));
        }

        [Test]
        public void EvaluateStars_HoldsWithinHysteresisBuffer()
        {
            // currentStar 4 (Eintritt 600), Score fällt auf 580 -> über 600-50=550 -> Stern HALTEN (4)
            Assert.That(StarRatingFormulas.EvaluateStars(580, 4, Thresholds, Buffer), Is.EqualTo(4));
            // genau auf der Puffer-Grenze (550) -> halten (nicht strikt darunter)
            Assert.That(StarRatingFormulas.EvaluateStars(550, 4, Thresholds, Buffer), Is.EqualTo(4));
        }

        [Test]
        public void EvaluateStars_DropsWhenBelowBuffer()
        {
            // currentStar 4, Score 549 < 600-50 -> Abstieg auf rohen Stern (RawStar(549)=3)
            Assert.That(StarRatingFormulas.EvaluateStars(549, 4, Thresholds, Buffer), Is.EqualTo(3));
            // tiefer Kollaps fällt direkt auf den rohen Stern
            Assert.That(StarRatingFormulas.EvaluateStars(50, 4, Thresholds, Buffer), Is.EqualTo(1));
        }

        [Test]
        public void EvaluateStars_ClampsCurrentStar()
        {
            // currentStar 0 -> als 1 behandelt; hoher Score steigt sofort
            Assert.That(StarRatingFormulas.EvaluateStars(1000, 0, Thresholds, Buffer), Is.EqualTo(5));
            // currentStar 9 (ungueltig) -> auf max (5) geklemmt; hoher Score haelt 5
            Assert.That(StarRatingFormulas.EvaluateStars(1000, 9, Thresholds, Buffer), Is.EqualTo(5));
        }

        [Test]
        public void EvaluateStars_StaysWhenRawEqualsCurrent()
        {
            Assert.That(StarRatingFormulas.EvaluateStars(350, 3, Thresholds, Buffer), Is.EqualTo(3));
        }

        [Test]
        public void IsPrestigeReady_OnlyAtFiveStars()
        {
            Assert.That(StarRatingFormulas.IsPrestigeReady(4), Is.False);
            Assert.That(StarRatingFormulas.IsPrestigeReady(5), Is.True);
        }
    }
}
