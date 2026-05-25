#nullable enable
using System;
using ArcaneKingdom.Domain.Economy;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer SternkartenService + LoginTracker (Designplan v4 Oeko Kap. 5).
    /// </summary>
    [TestFixture]
    public sealed class SternkartenServiceTests
    {
        // --------------------------------------------------------------------
        // Sternkarten-Inventar + Berechnung Sternpunkte
        // --------------------------------------------------------------------

        [Test]
        public void Sternpunkte_werden_aus_Karten_summiert()
        {
            var inv = new SternkartenInventory { Bronze = 22, Silber = 2, Gold = 2, Platin = 1 };
            // 22*1 + 2*5 + 2*15 + 1*50 = 22 + 10 + 30 + 50 = 112
            Assert.AreEqual(112, inv.AvailableSternpunkte);
        }

        [Test]
        public void Verbrauchte_Sternpunkte_werden_abgezogen()
        {
            var inv = new SternkartenInventory { Bronze = 10, SternpunkteSpent = 5 };
            Assert.AreEqual(5, inv.AvailableSternpunkte);
        }

        [Test]
        public void Sternkarten_hinzufuegen_aktualisiert_Inventar()
        {
            var svc = new SternkartenService();
            var inv = new SternkartenInventory();
            svc.AddSternkarte(inv, SternkartenStufe.Bronze, 3);
            svc.AddSternkarte(inv, SternkartenStufe.Gold, 1);
            Assert.AreEqual(3, inv.Bronze);
            Assert.AreEqual(1, inv.Gold);
            Assert.AreEqual(3 * 1 + 1 * 15, inv.AvailableSternpunkte);
        }

        // --------------------------------------------------------------------
        // Tempel-Eintausch
        // --------------------------------------------------------------------

        [Test]
        public void Eintausch_OK_bei_genug_Sternpunkten()
        {
            var svc = new SternkartenService();
            var inv = new SternkartenInventory { Bronze = 50 };
            var r = svc.Exchange(inv, SternkartenWerte.CostRandom2Star);   // 30
            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(50 - 30, inv.AvailableSternpunkte);
        }

        [Test]
        public void Eintausch_fehlschlagen_bei_zu_wenig()
        {
            var svc = new SternkartenService();
            var inv = new SternkartenInventory { Bronze = 10 };
            var r = svc.Exchange(inv, SternkartenWerte.CostChosen3Star);   // 80
            Assert.IsFalse(r.IsSuccess);
            StringAssert.Contains("Sternpunkte", r.ErrorMessage);
            Assert.AreEqual(10, inv.AvailableSternpunkte);                  // unveraendert
        }

        [Test]
        public void Mythic_Fragment_Eintausch()
        {
            var svc = new SternkartenService();
            // 500 Sternpunkte fuer 1 Fragment
            var inv = new SternkartenInventory { Platin = 10 };   // 500 Sternpunkte
            var r = svc.ExchangeForMythicFragment(inv);
            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(1, r.Value);
            Assert.AreEqual(1, inv.MythicCoreFragments);
            Assert.AreEqual(0, inv.AvailableSternpunkte);
        }

        [Test]
        public void Mythic_Core_aus_3_Fragmenten()
        {
            var svc = new SternkartenService();
            var inv = new SternkartenInventory { MythicCoreFragments = 3 };
            Assert.IsTrue(svc.CanCraftMythicCore(inv));
            var r = svc.CraftMythicCore(inv);
            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(1, r.Value);
            Assert.AreEqual(0, inv.MythicCoreFragments);
        }

        [Test]
        public void Mythic_Core_fehlschlaegt_bei_zu_wenig_Fragmenten()
        {
            var svc = new SternkartenService();
            var inv = new SternkartenInventory { MythicCoreFragments = 2 };
            Assert.IsFalse(svc.CanCraftMythicCore(inv));
            var r = svc.CraftMythicCore(inv);
            Assert.IsFalse(r.IsSuccess);
        }

        // --------------------------------------------------------------------
        // LoginTracker
        // --------------------------------------------------------------------

        [Test]
        public void LoginTracker_NextDayInCycle_startet_bei_1()
        {
            var t = new LoginTracker();
            Assert.AreEqual(1, t.NextDayInCycle);
        }

        [Test]
        public void LoginTracker_zaehlt_hoch()
        {
            var t = new LoginTracker();
            for (var i = 0; i < 5; i++) t.MarkClaimed(DateTime.UtcNow);
            Assert.AreEqual(5, t.DaysClaimedThisCycle);
            Assert.AreEqual(6, t.NextDayInCycle);
        }

        [Test]
        public void LoginTracker_resetet_nach_30_Tagen()
        {
            var t = new LoginTracker();
            for (var i = 0; i < 30; i++) t.MarkClaimed(DateTime.UtcNow);
            Assert.AreEqual(30, t.DaysClaimedThisCycle);
            t.MarkClaimed(DateTime.UtcNow);
            Assert.AreEqual(1, t.DaysClaimedThisCycle);   // Zyklus von vorne
        }

        [Test]
        public void LoginTracker_CanClaim_pro_Tag_einmal()
        {
            var t = new LoginTracker();
            var heute = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(t.CanClaimToday(heute));
            t.MarkClaimed(heute);
            Assert.IsFalse(t.CanClaimToday(heute));
            var morgen = heute.AddDays(1);
            Assert.IsTrue(t.CanClaimToday(morgen));
        }

        // --------------------------------------------------------------------
        // Konstanten gegen Designplan v4 Spec
        // --------------------------------------------------------------------

        [Test]
        public void Sternpunkt_Werte_aus_Designplan_v4()
        {
            Assert.AreEqual(1,  SternkartenWerte.GetSternpunkte(SternkartenStufe.Bronze));
            Assert.AreEqual(5,  SternkartenWerte.GetSternpunkte(SternkartenStufe.Silber));
            Assert.AreEqual(15, SternkartenWerte.GetSternpunkte(SternkartenStufe.Gold));
            Assert.AreEqual(50, SternkartenWerte.GetSternpunkte(SternkartenStufe.Platin));
        }

        [Test]
        public void Tempel_Kosten_aus_Designplan_v4_Oeko_5_3()
        {
            Assert.AreEqual(30,  SternkartenWerte.CostRandom2Star);
            Assert.AreEqual(80,  SternkartenWerte.CostChosen3Star);
            Assert.AreEqual(150, SternkartenWerte.CostExclusive3Star);
            Assert.AreEqual(350, SternkartenWerte.CostExclusive4Star);
            Assert.AreEqual(100, SternkartenWerte.CostLegendaryScrap);
            Assert.AreEqual(500, SternkartenWerte.CostMythicFragment);
            Assert.AreEqual(3,   SternkartenWerte.MythicFragmentsPerCore);
        }

        [Test]
        public void Designplan_v4_Beispiel_30_Tage_perfekter_Login()
        {
            // 22x Bronze + 2x Silber + 2x Gold + 1x Platin = 112 Sternpunkte (Designplan v4 Kap. 5.2)
            var inv = new SternkartenInventory { Bronze = 22, Silber = 2, Gold = 2, Platin = 1 };
            Assert.AreEqual(112, inv.AvailableSternpunkte);
        }
    }
}
