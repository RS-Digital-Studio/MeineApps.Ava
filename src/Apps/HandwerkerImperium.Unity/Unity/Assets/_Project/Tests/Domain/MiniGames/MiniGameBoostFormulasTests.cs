using NUnit.Framework;
using HandwerkerImperium.Domain.MiniGames;

namespace HandwerkerImperium.Domain.Tests.MiniGames
{
    /// <summary>
    /// Verifiziert die optionalen Tap-Timing-Boosts: Treffergüte aus dem Timing-Fehler + Buff-Stärke/-Dauer je Rating.
    /// </summary>
    [TestFixture]
    public class MiniGameBoostFormulasTests
    {
        [Test]
        public void Rate_MapsErrorToTier()
        {
            Assert.That(MiniGameBoostFormulas.Rate(0.0, 0.05, 0.15, 0.30), Is.EqualTo(TapRating.Perfect));
            Assert.That(MiniGameBoostFormulas.Rate(0.10, 0.05, 0.15, 0.30), Is.EqualTo(TapRating.Good));
            Assert.That(MiniGameBoostFormulas.Rate(-0.10, 0.05, 0.15, 0.30), Is.EqualTo(TapRating.Good), "Betrag des Fehlers");
            Assert.That(MiniGameBoostFormulas.Rate(0.25, 0.05, 0.15, 0.30), Is.EqualTo(TapRating.Ok));
            Assert.That(MiniGameBoostFormulas.Rate(0.50, 0.05, 0.15, 0.30), Is.EqualTo(TapRating.Miss));
        }

        [Test]
        public void BoostMultiplier_PerRating()
        {
            Assert.That(MiniGameBoostFormulas.BoostMultiplier(TapRating.Perfect), Is.EqualTo(2.0m));
            Assert.That(MiniGameBoostFormulas.BoostMultiplier(TapRating.Good), Is.EqualTo(1.5m));
            Assert.That(MiniGameBoostFormulas.BoostMultiplier(TapRating.Ok), Is.EqualTo(1.2m));
            Assert.That(MiniGameBoostFormulas.BoostMultiplier(TapRating.Miss), Is.EqualTo(1.0m));
        }

        [Test]
        public void BoostDuration_PerRating()
        {
            Assert.That(MiniGameBoostFormulas.BoostDurationSeconds(TapRating.Perfect, 10.0), Is.EqualTo(10.0).Within(1e-9));
            Assert.That(MiniGameBoostFormulas.BoostDurationSeconds(TapRating.Miss, 10.0), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(MiniGameBoostFormulas.BoostDurationSeconds(TapRating.Ok, 10.0), Is.GreaterThan(0.0));
        }
    }
}
