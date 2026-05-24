#nullable enable
using ArcaneKingdom.Game.Arena;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer die statische Rang-Berechnung (ArenaController.CalculateRankChange).
    /// Liegt in Domain.Tests, weil die Methode pure C# ist und keine Unity-API braucht.
    /// </summary>
    [TestFixture]
    public sealed class ArenaRankCalcTests
    {
        [Test]
        public void SiegGegenGleichRangigenGibt25Punkte()
        {
            Assert.AreEqual(25, ArenaController.CalculateRankChange(100, 100, ArenaController.MatchOutcome.Win));
        }

        [Test]
        public void SiegGegenSchwaecherenGibtMaximal25()
        {
            var change = ArenaController.CalculateRankChange(100, 50, ArenaController.MatchOutcome.Win);
            Assert.LessOrEqual(change, 25);
            Assert.GreaterOrEqual(change, 10);
        }

        [Test]
        public void SiegGegenStaerkerenGibtMehrPunkte()
        {
            var change = ArenaController.CalculateRankChange(100, 200, ArenaController.MatchOutcome.Win);
            Assert.GreaterOrEqual(change, 30);
            Assert.LessOrEqual(change, 50);
        }

        [Test]
        public void NiederlageGegenGleichRangigenVerliert20()
        {
            Assert.AreEqual(-20, ArenaController.CalculateRankChange(100, 100, ArenaController.MatchOutcome.Loss));
        }

        [Test]
        public void NiederlageGegenSchwaecherenIstSchmerzhafter()
        {
            var change = ArenaController.CalculateRankChange(100, 50, ArenaController.MatchOutcome.Loss);
            Assert.Less(change, -20);
        }

        [Test]
        public void DisconnectIstStrafe()
        {
            Assert.AreEqual(-50, ArenaController.CalculateRankChange(100, 100, ArenaController.MatchOutcome.Disconnect));
        }
    }
}
