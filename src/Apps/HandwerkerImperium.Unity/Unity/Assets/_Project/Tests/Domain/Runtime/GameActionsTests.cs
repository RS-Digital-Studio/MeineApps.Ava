using NUnit.Framework;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Restoration;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Config;
using HandwerkerImperium.Domain.Runtime;

namespace HandwerkerImperium.Domain.Tests.Runtime
{
    /// <summary>
    /// Verifiziert die Spieler-Aktions-Fläche über dem GameModel: Idle-Aktionen (Upgrade/Worker/Plot),
    /// Kunde bedienen (inkl. Eil-Bonus), Sanierungs-Investition, Perk-/Meistergrad-Kauf, Tagesbelohnung, Mastery.
    /// </summary>
    [TestFixture]
    public class GameActionsTests
    {
        private static IdleBalancing Idle() => new IdleBalancing();
        private static GameBalancing Bal() => new GameBalancing();

        [Test]
        public void IdleActions_Upgrade_Hire_Unlock()
        {
            var ib = Idle();
            var m = GameModel.CreateNew(ib);

            m.Idle.Money = 100m;
            Assert.That(GameActions.BuyUpgrade(m, ib, UpgradeTrack.StationSpeed), Is.True);
            Assert.That(m.Idle.StationSpeedLevel, Is.EqualTo(1));
            Assert.That(m.Idle.Money, Is.EqualTo(50m)); // Basis-Kosten 50

            m.Idle.Money = 200m;
            Assert.That(GameActions.HireWorker(m, ib, 0), Is.True);
            Assert.That(m.Idle.Stations[0].HasWorker, Is.True);

            m.Idle.Money = 500m;
            Assert.That(GameActions.UnlockPlot(m, ib, 3), Is.True);
            Assert.That(m.Idle.Stations[3].Unlocked, Is.True);
        }

        [Test]
        public void ServeCustomer_PaysSellValue_WithRushBonus()
        {
            var ib = Idle();
            var m = GameModel.CreateNew(ib);
            m.Orders.PendingCustomers = 2;

            decimal earned = GameActions.ServeCustomer(m, ib, 0, 1000); // schreiner SellValue 5, kein Rush
            Assert.That(earned, Is.EqualTo(5m));
            Assert.That(m.Idle.Money, Is.EqualTo(5m));
            Assert.That(m.Orders.TotalServed, Is.EqualTo(1));

            OrderQueueFormulasStartRush(m); // 3x ab now
            decimal rushed = GameActions.ServeCustomer(m, ib, 0, 1000);
            Assert.That(rushed, Is.EqualTo(15m), "5 * 3x Eil-Bonus");

            // leere Queue
            m.Orders.PendingCustomers = 0;
            Assert.That(GameActions.ServeCustomer(m, ib, 0, 1000), Is.EqualTo(0m));
        }

        [Test]
        public void InvestRestoration_CompletesPhase_GuardsMoney()
        {
            var ib = Idle();
            var b = Bal();
            var m = GameModel.CreateNew(ib);
            m.Landmarks.Add(new LandmarkState("marktplatz", 5));

            m.Idle.Money = 100m; // < PhaseBaseCost 5000
            Assert.That(GameActions.InvestRestoration(m, b, 0, 5000m), Is.EqualTo(0), "zu wenig Geld -> 0");
            Assert.That(m.Idle.Money, Is.EqualTo(100m), "Geld unveraendert");

            m.Idle.Money = 6000m;
            Assert.That(GameActions.InvestRestoration(m, b, 0, 5000m), Is.EqualTo(1), "Phase 0 (5000) abgeschlossen");
            Assert.That(m.Idle.Money, Is.EqualTo(1000m));
            Assert.That(m.Landmarks[0].PhasesComplete, Is.EqualTo(1));
        }

        [Test]
        public void BuyPerk_And_BuyMeistergrad()
        {
            var b = Bal();
            var m = GameModel.CreateNew(Idle());

            m.Meta.AvailableMarks = 10;
            Assert.That(GameActions.BuyPerk(m, b, PerkKind.GlobalTempo), Is.True); // Kosten 10
            Assert.That(m.Meta.AvailableMarks, Is.EqualTo(0));
            Assert.That(m.PerkLevels[(int)PerkKind.GlobalTempo], Is.EqualTo(1));
            Assert.That(GameActions.BuyPerk(m, b, PerkKind.GlobalTempo), Is.False, "keine Marken mehr");

            m.Meta.Renommee = 1000m;
            Assert.That(GameActions.BuyMeistergrad(m, b), Is.True);
            Assert.That(m.Meta.MeistergradGrade, Is.EqualTo(1));
        }

        [Test]
        public void ClaimDaily_OncePerDay_AndGainMastery()
        {
            var b = Bal();
            var m = GameModel.CreateNew(Idle());
            long now = 1_000_000_000_000L;

            decimal first = GameActions.ClaimDaily(m, b, 500m, 0m, now);
            Assert.That(first, Is.EqualTo(500m));
            Assert.That(m.DailyStreakDay, Is.EqualTo(1));
            Assert.That(m.Idle.Money, Is.EqualTo(500m));
            Assert.That(GameActions.ClaimDaily(m, b, 500m, 0m, now), Is.EqualTo(0m), "selber Tag -> nichts");

            Assert.That(GameActions.GainMastery(m, b, 215.0), Is.EqualTo(2)); // TotalXp(2)=215
            Assert.That(m.Meta.MasteryLevel, Is.EqualTo(2));
        }

        [Test]
        public void StartRush_Activates_AndTickBoostsEarnings()
        {
            var ib = Idle();
            var b = Bal();
            long now = 1000L;

            var s = GameModel.CreateNew(ib);
            Assert.That(GameActions.StartRush(s, b, now), Is.True);
            Assert.That(HandwerkerImperium.Domain.LiveOps.RushEventFormulas.CurrentMultiplier(s.Rush, now), Is.EqualTo(2m));
            Assert.That(GameActions.StartRush(s, b, now), Is.False, "schon aktiv");

            // gleicher Startzustand, einmal mit / einmal ohne Rush -> mit Rush mehr Verdienst
            var noRush = GameModel.CreateNew(ib); noRush.Idle.Stations[0].HasWorker = true; noRush.Idle.Stations[0].Stock = 8;
            decimal ea = GameSimulation.Tick(noRush, ib, b, 1.0, now);

            var rush = GameModel.CreateNew(ib); rush.Idle.Stations[0].HasWorker = true; rush.Idle.Stations[0].Stock = 8;
            GameActions.StartRush(rush, b, now);
            decimal eb = GameSimulation.Tick(rush, ib, b, 1.0, now);

            Assert.That(eb, Is.GreaterThan(ea), "Rush boostet den Tick-Verdienst");
        }

        private static void OrderQueueFormulasStartRush(GameModel m)
        {
            HandwerkerImperium.Domain.Orders.OrderQueueFormulas.StartRush(m.Orders, 3m, 60, 0);
        }
    }
}
