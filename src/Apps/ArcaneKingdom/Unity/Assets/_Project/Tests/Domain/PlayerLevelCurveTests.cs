#nullable enable
using ArcaneKingdom.Domain.Battle;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class PlayerLevelCurveTests
    {
        [Test]
        public void Level1AufLevel2BrauchtUngefaehr1080Exp()
        {
            var needed = PlayerLevelCurve.ExpRequiredFromLevel(1);
            Assert.Greater(needed, 1000);
            Assert.Less(needed, 1500);
        }

        [Test]
        public void ExpRequiredSteigtMonoton()
        {
            for (var lv = 1; lv < 100; lv++)
            {
                var a = PlayerLevelCurve.ExpRequiredFromLevel(lv);
                var b = PlayerLevelCurve.ExpRequiredFromLevel(lv + 1);
                Assert.GreaterOrEqual(b, a, $"EXP fuer LV {lv + 1} sollte >= EXP fuer LV {lv} sein.");
            }
        }

        [Test]
        public void LevelForExpRoundtripsKumulierteSumme()
        {
            for (var target = 2; target <= 50; target++)
            {
                var totalExp = PlayerLevelCurve.ExpCumulativeForLevel(target);
                var derived = PlayerLevelCurve.LevelForExp(totalExp);
                Assert.AreEqual(target, derived, $"Target-Level {target} sollte aus kumulierter EXP rekonstruierbar sein.");
            }
        }

        [Test]
        public void SoftCapBeiLevel150()
        {
            Assert.AreEqual(150, PlayerLevelCurve.LevelForExp(long.MaxValue / 2));
        }
    }
}
