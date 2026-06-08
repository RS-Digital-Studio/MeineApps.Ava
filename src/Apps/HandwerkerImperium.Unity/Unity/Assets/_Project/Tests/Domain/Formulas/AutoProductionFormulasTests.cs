using System;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.State;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Formulas
{
    /// <summary>
    /// Verifiziert den Service-Formel-Extrakt AutoProductionFormulas (aus AutoProductionService) gegen die
    /// Original-Mathematik: Intervalle (60/120/180), Produkt-Mapping, Freischaltung (Lv50), Offline-Staffelung
    /// (80/35/15/5) und Offline-Produktion inkl. Stack-Limit-Cap.
    /// </summary>
    [TestFixture]
    public class AutoProductionFormulasTests
    {
        [Test]
        public void Interval_And_Mapping_And_Unlock()
        {
            Assert.That(AutoProductionFormulas.GetProductionInterval(WorkshopType.MasterSmith), Is.EqualTo(60));
            Assert.That(AutoProductionFormulas.GetProductionInterval(WorkshopType.InnovationLab), Is.EqualTo(120));
            Assert.That(AutoProductionFormulas.GetProductionInterval(WorkshopType.Carpenter), Is.EqualTo(180));

            Assert.That(AutoProductionFormulas.GetTier1ProductId(WorkshopType.Carpenter), Is.EqualTo("planks"));
            Assert.That(AutoProductionFormulas.GetTier1ProductId(WorkshopType.InnovationLab), Is.EqualTo("prototype"));
            foreach (WorkshopType t in (WorkshopType[])Enum.GetValues(typeof(WorkshopType)))
                Assert.That(AutoProductionFormulas.GetTier1ProductId(t), Is.Not.Null, "WorkshopType " + t + " nicht gemappt");

            Assert.That(AutoProductionFormulas.IsAutoProductionUnlocked(new Workshop { Type = WorkshopType.Carpenter, Level = 49 }), Is.False);
            Assert.That(AutoProductionFormulas.IsAutoProductionUnlocked(new Workshop { Type = WorkshopType.Carpenter, Level = 50 }), Is.True);
        }

        [Test]
        public void EffectiveOfflineSeconds_Staggered()
        {
            Assert.That(AutoProductionFormulas.CalculateEffectiveOfflineSeconds(3600), Is.EqualTo(2880).Within(1e-6));   // 1h: 3600*0.8
            Assert.That(AutoProductionFormulas.CalculateEffectiveOfflineSeconds(28800), Is.EqualTo(10440).Within(1e-6)); // 8h
        }

        [Test]
        public void OfflineProduction_Amount_And_Cap()
        {
            var st = new GameState();
            st.Workshops.Clear();
            var ws = new Workshop { Type = WorkshopType.Carpenter, Level = 50 };
            ws.Workers.Add(new Worker { AssignedWorkshop = WorkshopType.Carpenter });
            st.Workshops.Add(ws);

            // 2h -> effektiv 5760s / 180 = 32 planks
            Assert.That(AutoProductionFormulas.CalculateOfflineProduction(st, 7200, 50).GetValueOrDefault("planks", 0), Is.EqualTo(32));

            // Cap durch Stack-Limit: current 45, limit 50 -> 5
            st.CraftingInventory["planks"] = 45;
            Assert.That(AutoProductionFormulas.CalculateOfflineProduction(st, 7200, 50).GetValueOrDefault("planks", 0), Is.EqualTo(5));

            // offlineSeconds <= 0 -> leer
            Assert.That(AutoProductionFormulas.CalculateOfflineProduction(st, 0, 50).Count, Is.EqualTo(0));
        }

        [Test]
        public void OfflineProduction_NotUnlocked_Empty()
        {
            var st = new GameState();
            st.Workshops.Clear();
            var ws = new Workshop { Type = WorkshopType.Carpenter, Level = 49 };
            ws.Workers.Add(new Worker { AssignedWorkshop = WorkshopType.Carpenter });
            st.Workshops.Add(ws);
            Assert.That(AutoProductionFormulas.CalculateOfflineProduction(st, 7200, 50).Count, Is.EqualTo(0));
        }
    }
}
