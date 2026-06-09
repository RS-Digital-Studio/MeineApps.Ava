using System.Collections.Generic;
using NUnit.Framework;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Restoration;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Config;
using HandwerkerImperium.Domain.Runtime;

namespace HandwerkerImperium.Domain.Tests.Runtime
{
    /// <summary>
    /// Integrations-Beweis des Spiel-Orchestrators: der Tick treibt Produktion/Worker/Orders; das effektive
    /// Einkommen bündelt alle permanenten Quellen (Soft-Cap); der Stern steigt mit Fortschritt; Prestige resettet
    /// den Idle-Loop und behält die Meta; Offline rechnet mit dem effektiven Einkommen — alles über EINEM Modell.
    /// </summary>
    [TestFixture]
    public class GameSimulationTests
    {
        private static List<MasterToolDefinition> Catalog() => MasterToolFormulas.DefaultCatalog();

        [Test]
        public void Tick_Produces_Automates_AndSpawnsCustomers()
        {
            var idleBal = new IdleBalancing();
            var bal = new GameBalancing();
            var m = GameModel.CreateNew(idleBal);
            m.Idle.Stations[0].HasWorker = true; // automatisiert

            GameSimulation.Tick(m, idleBal, bal, 20.0, 1000);

            Assert.That(m.Idle.Money, Is.GreaterThan(0m), "Worker automatisiert Stock -> Geld");
            Assert.That(m.Orders.PendingCustomers, Is.GreaterThan(0), "Kunden treffen ein");
        }

        [Test]
        public void EffectiveIncome_AggregatesPermanentSources_WithSoftCap()
        {
            var idleBal = new IdleBalancing();
            var bal = new GameBalancing();
            var cat = Catalog();
            var m = GameModel.CreateNew(idleBal);
            m.Idle.Stations[0].HasWorker = true;

            decimal baseInc = GreyboxSimulation.TotalAutomatedIncomePerSecond(m.Idle, idleBal);
            Assert.That(baseInc, Is.GreaterThan(0m));

            decimal eff0 = GameSimulation.EffectiveIncomePerSecond(m, idleBal, bal, cat);
            Assert.That(eff0, Is.EqualTo(baseInc), "ohne Boni: Aggregat = 1");

            m.Meta.PrestigeMultiplier = 3m;
            m.Meta.MasteryLevel = 10; // +10 %
            decimal eff1 = GameSimulation.EffectiveIncomePerSecond(m, idleBal, bal, cat);
            Assert.That(eff1, Is.GreaterThan(eff0), "permanente Boni erhoehen das Einkommen");
        }

        [Test]
        public void EvaluateStar_RisesWithWorkshops_Restoration_Volume()
        {
            var bal = new GameBalancing();
            var m = GameModel.CreateNew(new IdleBalancing()); // 3 offene Werkstaetten
            m.Orders.TotalServed = 50;
            m.Landmarks.Add(new LandmarkState("marktplatz", 5) { PhasesComplete = 5 });

            // score = 3*50 + 5*40 + 50*2 = 450 -> 3★ (>=300, <600), Stadt 0 (scale 1)
            Assert.That(GameSimulation.EvaluateStar(m, bal), Is.EqualTo(3));
        }

        [Test]
        public void Prestige_ResetsIdleLoop_PersistsMeta()
        {
            var idleBal = new IdleBalancing();
            var bal = new GameBalancing();
            var m = GameModel.CreateNew(idleBal);
            m.Idle.Money = 1_000_000m;
            m.Idle.Stations[0].Stock = 5;
            m.Orders.TotalServed = 99;
            m.Meta.MasteryLevel = 7;
            m.Meta.CurrentStar = 5;

            Assert.That(GameSimulation.CanPrestige(m, bal), Is.True);
            Assert.That(GameSimulation.TryPrestige(m, idleBal, bal), Is.True);

            // Idle-Loop akt-intern zurueckgesetzt
            Assert.That(m.Idle.Money, Is.EqualTo(0m));
            Assert.That(m.Idle.Stations[0].Stock, Is.EqualTo(0));
            Assert.That(m.Orders.TotalServed, Is.EqualTo(0));
            Assert.That(m.Meta.CurrentStar, Is.EqualTo(1));
            // Permanent behalten
            Assert.That(m.Meta.MasteryLevel, Is.EqualTo(7));
            Assert.That(m.Meta.PrestigeCount, Is.EqualTo(1));
            Assert.That(m.Meta.PrestigeMultiplier, Is.EqualTo(3m));
            Assert.That(m.Meta.CityIndex, Is.EqualTo(1));
        }

        [Test]
        public void ComputeOffline_UsesEffectiveIncome_PremiumBoosts()
        {
            var idleBal = new IdleBalancing();
            var bal = new GameBalancing();
            var cat = Catalog();
            var m = GameModel.CreateNew(idleBal);
            m.Idle.Stations[0].HasWorker = true;

            decimal off = GameSimulation.ComputeOffline(m, idleBal, bal, cat, 3600.0);
            Assert.That(off, Is.GreaterThan(0m));

            m.IsPremium = true;
            decimal offPremium = GameSimulation.ComputeOffline(m, idleBal, bal, cat, 3600.0);
            Assert.That(offPremium, Is.GreaterThan(off), "Premium erhoeht Offline (Einkommen + Multiplikator)");
        }
    }
}
