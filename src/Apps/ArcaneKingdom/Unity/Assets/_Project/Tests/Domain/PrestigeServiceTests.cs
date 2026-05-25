#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.World;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer PrestigeService + PrestigeStufeBalancing (Designplan v4 Oeko Kap. 6).
    /// </summary>
    [TestFixture]
    public sealed class PrestigeServiceTests
    {
        // --------------------------------------------------------------------
        // CanUpgrade
        // --------------------------------------------------------------------

        [Test]
        public void Upgrade_braucht_alle_Nodes_auf_3_Sternen()
        {
            var svc = new PrestigeService();
            var nodes = new Dictionary<string, int> { ["n1"] = 3, ["n2"] = 2, ["n3"] = 3 };
            var r = svc.CanUpgradePrestige(PrestigeStufe.Normal, nodes, playerGold: 1_000_000);
            Assert.IsFalse(r.IsSuccess);
            StringAssert.Contains("2 Sterne", r.ErrorMessage);
        }

        [Test]
        public void Upgrade_braucht_genug_Gold()
        {
            var svc = new PrestigeService();
            var nodes = new Dictionary<string, int> { ["n1"] = 3, ["n2"] = 3, ["n3"] = 3 };
            var r = svc.CanUpgradePrestige(PrestigeStufe.Normal, nodes, playerGold: 50_000);
            Assert.IsFalse(r.IsSuccess);
            StringAssert.Contains("Gold", r.ErrorMessage);
        }

        [Test]
        public void Upgrade_OK_wenn_Sterne_und_Gold_ausreichen()
        {
            var svc = new PrestigeService();
            var nodes = new Dictionary<string, int> { ["n1"] = 3, ["n2"] = 3 };
            var r = svc.CanUpgradePrestige(PrestigeStufe.Normal, nodes, playerGold: 100_000);
            Assert.IsTrue(r.IsSuccess);
        }

        [Test]
        public void Stufe_IV_kann_nicht_weiter_aufgewertet_werden()
        {
            var svc = new PrestigeService();
            var nodes = new Dictionary<string, int> { ["n1"] = 3 };
            var r = svc.CanUpgradePrestige(PrestigeStufe.IV, nodes, playerGold: 100_000_000);
            Assert.IsFalse(r.IsSuccess);
            StringAssert.Contains("maximale", r.ErrorMessage);
        }

        // --------------------------------------------------------------------
        // NextStufe
        // --------------------------------------------------------------------

        [Test]
        public void NextStufe_Sequenz()
        {
            var svc = new PrestigeService();
            Assert.AreEqual(PrestigeStufe.I,   svc.NextStufe(PrestigeStufe.Normal));
            Assert.AreEqual(PrestigeStufe.II,  svc.NextStufe(PrestigeStufe.I));
            Assert.AreEqual(PrestigeStufe.III, svc.NextStufe(PrestigeStufe.II));
            Assert.AreEqual(PrestigeStufe.IV,  svc.NextStufe(PrestigeStufe.III));
            Assert.AreEqual(PrestigeStufe.IV,  svc.NextStufe(PrestigeStufe.IV));
        }

        // --------------------------------------------------------------------
        // Multiplier
        // --------------------------------------------------------------------

        [Test]
        public void Multiplier_steigen_mit_Stufe()
        {
            var svc = new PrestigeService();
            var (a0, h0) = svc.ScaleEnemyStats(100, 1000, PrestigeStufe.Normal);
            var (a4, h4) = svc.ScaleEnemyStats(100, 1000, PrestigeStufe.IV);
            Assert.AreEqual(100, a0);
            Assert.AreEqual(1000, h0);
            Assert.AreEqual(250, a4);   // 2.5x
            Assert.AreEqual(2500, h4);
        }

        [Test]
        public void DailyRevenue_skaliert_korrekt()
        {
            var svc = new PrestigeService();
            Assert.AreEqual(100,    svc.CalculateDailyRevenue(100, PrestigeStufe.Normal));
            Assert.AreEqual(200,    svc.CalculateDailyRevenue(100, PrestigeStufe.I));
            Assert.AreEqual(400,    svc.CalculateDailyRevenue(100, PrestigeStufe.II));
            Assert.AreEqual(800,    svc.CalculateDailyRevenue(100, PrestigeStufe.III));
            Assert.AreEqual(1600,   svc.CalculateDailyRevenue(100, PrestigeStufe.IV));
        }

        [Test]
        public void Prestige_IV_unlockt_exklusive_Karte()
        {
            var svc = new PrestigeService();
            Assert.IsFalse(svc.UnlocksExclusiveCard(PrestigeStufe.Normal));
            Assert.IsFalse(svc.UnlocksExclusiveCard(PrestigeStufe.III));
            Assert.IsTrue(svc.UnlocksExclusiveCard(PrestigeStufe.IV));
        }

        [Test]
        public void Boss_Phasen_skalieren_mit_Stufe()
        {
            var svc = new PrestigeService();
            Assert.AreEqual(2, svc.GetBossPhaseCount(PrestigeStufe.Normal));
            Assert.AreEqual(2, svc.GetBossPhaseCount(PrestigeStufe.I));
            Assert.AreEqual(2, svc.GetBossPhaseCount(PrestigeStufe.II));
            Assert.AreEqual(3, svc.GetBossPhaseCount(PrestigeStufe.III));
            Assert.AreEqual(4, svc.GetBossPhaseCount(PrestigeStufe.IV));
        }

        // --------------------------------------------------------------------
        // Balancing-Konstanten
        // --------------------------------------------------------------------

        [Test]
        public void Upgrade_Kosten_steigen_geometrisch()
        {
            Assert.AreEqual(100_000,   PrestigeStufeBalancing.GetUpgradeGoldCost(PrestigeStufe.Normal));
            Assert.AreEqual(500_000,   PrestigeStufeBalancing.GetUpgradeGoldCost(PrestigeStufe.I));
            Assert.AreEqual(2_000_000, PrestigeStufeBalancing.GetUpgradeGoldCost(PrestigeStufe.II));
            Assert.AreEqual(5_000_000, PrestigeStufeBalancing.GetUpgradeGoldCost(PrestigeStufe.III));
            Assert.AreEqual(-1,        PrestigeStufeBalancing.GetUpgradeGoldCost(PrestigeStufe.IV));   // MAX
        }
    }
}
