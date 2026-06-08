using System;
using System.Collections.Generic;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Offline;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Idle
{
    /// <summary>
    /// Verifiziert den Unity-freien Greybox-Loop-Kern (P0): Produktion/Cap, geometrische Upgrade-Kosten +
    /// Effekte, Spieler-Abgabe, Worker-Automatisierung, Hire/Plot-Unlock und gestaffelter Offline-Verdienst.
    /// Das ist die headless beweisbare „Loop-Mathematik" hinter dem Greybox-Prototyp.
    /// </summary>
    [TestFixture]
    public class GreyboxSimulationTests
    {
        private static IdleBalancing Bal() => new IdleBalancing();

        [Test]
        public void Production_Accrues_AndCapsAtStackCap()
        {
            var bal = Bal();
            var st = GreyboxSimState.CreateNew(bal);
            // Station 0: interval 2.0, cap 8. 16 s -> exakt 8 Waren.
            GreyboxSimulation.TickProduction(st, bal, 16.0);
            Assert.That(st.Stations[0].Stock, Is.EqualTo(8));
            // Weitere 10 s -> bleibt bei Cap.
            GreyboxSimulation.TickProduction(st, bal, 10.0);
            Assert.That(st.Stations[0].Stock, Is.EqualTo(8));
            // Gesperrte Station 4 produziert nicht.
            Assert.That(st.Stations[3].Unlocked, Is.False);
            Assert.That(st.Stations[3].Stock, Is.EqualTo(0));
        }

        [Test]
        public void UpgradeCost_Geometric_And_Buy()
        {
            var bal = Bal();
            var st = GreyboxSimState.CreateNew(bal);
            Assert.That(GreyboxSimulation.UpgradeCostFor(st, bal, UpgradeTrack.StationSpeed), Is.EqualTo(50m));   // base*1.6^0
            st.Money = 49m;
            Assert.That(GreyboxSimulation.BuyUpgrade(st, bal, UpgradeTrack.StationSpeed), Is.False);              // zu arm
            st.Money = 50m;
            Assert.That(GreyboxSimulation.BuyUpgrade(st, bal, UpgradeTrack.StationSpeed), Is.True);
            Assert.That(st.Money, Is.EqualTo(0m));
            Assert.That(st.StationSpeedLevel, Is.EqualTo(1));
            Assert.That(GreyboxSimulation.UpgradeCostFor(st, bal, UpgradeTrack.StationSpeed), Is.EqualTo(80m));   // round(50*1.6)
        }

        [Test]
        public void UpgradeEffects_SpeedRadiusCarry()
        {
            var bal = Bal();
            Assert.That(IdleEconomyFormulas.EffectiveProduceInterval(2.0, 1, 0.25), Is.EqualTo(1.6).Within(1e-9));  // 2/(1.25)
            Assert.That(IdleEconomyFormulas.EffectiveCollectRadius(2.5, 1, 0.25), Is.EqualTo(3.125).Within(1e-9));
            Assert.That(IdleEconomyFormulas.EffectiveCarryCapacity(5, 1, 0.25), Is.EqualTo(6));                     // round(6.25)
            Assert.That(IdleEconomyFormulas.EffectiveCarryCapacity(5, 2, 0.25), Is.EqualTo(8));                     // round(7.5)
        }

        [Test]
        public void PlayerDeposit_EarnsBoundedByStock()
        {
            var bal = Bal();
            var st = GreyboxSimState.CreateNew(bal);
            GreyboxSimulation.TickProduction(st, bal, 10.0); // Station 0: 5 Waren
            Assert.That(st.Stations[0].Stock, Is.EqualTo(5));
            decimal earned = GreyboxSimulation.PlayerDeposit(st, bal, 0, 3);
            Assert.That(earned, Is.EqualTo(15m));            // 3 × 5
            Assert.That(st.Stations[0].Stock, Is.EqualTo(2));
            Assert.That(st.Money, Is.EqualTo(15m));
            // Mehr angefordert als vorhanden -> nur vorhandene.
            Assert.That(GreyboxSimulation.PlayerDeposit(st, bal, 0, 10), Is.EqualTo(10m));
            Assert.That(st.Stations[0].Stock, Is.EqualTo(0));
            Assert.That(st.Money, Is.EqualTo(25m));
        }

        [Test]
        public void Workers_AutomateStockToMoney()
        {
            var bal = Bal();
            var st = GreyboxSimState.CreateNew(bal);
            st.Stations[0].HasWorker = true;
            GreyboxSimulation.TickProduction(st, bal, 10.0); // 5 Waren
            decimal earned = GreyboxSimulation.TickWorkers(st, bal, 3.0); // carrySpeed 1.0 -> 3 Waren
            Assert.That(earned, Is.EqualTo(15m));            // 3 × 5
            Assert.That(st.Stations[0].Stock, Is.EqualTo(2));
            Assert.That(st.Money, Is.EqualTo(15m));
        }

        [Test]
        public void Hire_And_UnlockPlot()
        {
            var bal = Bal();
            var st = GreyboxSimState.CreateNew(bal);
            st.Money = 199m;
            Assert.That(GreyboxSimulation.HireWorker(st, bal, 0), Is.False);
            st.Money = 200m;
            Assert.That(GreyboxSimulation.HireWorker(st, bal, 0), Is.True);
            Assert.That(st.Stations[0].HasWorker, Is.True);
            Assert.That(st.Money, Is.EqualTo(0m));

            // Plot-Unlock Station 4
            st.Money = 500m;
            Assert.That(GreyboxSimulation.UnlockPlot(st, bal, 3), Is.True);
            Assert.That(st.Stations[3].Unlocked, Is.True);
            Assert.That(st.Money, Is.EqualTo(0m));
            // Produziert jetzt.
            GreyboxSimulation.TickProduction(st, bal, 7.0); // interval 3.5 -> 2 Waren
            Assert.That(st.Stations[3].Stock, Is.EqualTo(2));
        }

        [Test]
        public void Offline_Staggered_AndCapped()
        {
            var bal = Bal();
            var st = GreyboxSimState.CreateNew(bal);
            st.Stations[0].HasWorker = true; // income/s = 5 × min(0.5,1.0) = 2.5
            Assert.That(GreyboxSimulation.TotalAutomatedIncomePerSecond(st, bal), Is.EqualTo(2.5m));

            decimal off = GreyboxSimulation.ComputeOfflineEarnings(st, bal, 3600);
            Assert.That(off, Is.EqualTo(OfflineProgressFormulas.CalculateStaggeredEarnings(2.5m, 3600m)));
            Assert.That(off, Is.EqualTo(7200m)); // 3600 × 2.5 × 0.80
            // Cap greift: 100000 s -> auf 7200 s gedeckelt.
            decimal capped = GreyboxSimulation.ComputeOfflineEarnings(st, bal, 100000);
            Assert.That(capped, Is.EqualTo(OfflineProgressFormulas.CalculateStaggeredEarnings(2.5m, 7200m)));
        }

        [Test]
        public void FullMiniLoop_Integrates()
        {
            var bal = Bal();
            var st = GreyboxSimState.CreateNew(bal);
            // 20 s aktiv produzieren + abgeben, bis genug fuer Worker.
            for (int s = 0; s < 200; s++)
            {
                GreyboxSimulation.Tick(st, bal, 1.0);
                // Spieler raeumt alle 3 Stationen ab (Trag-Kapazitaet beachtet).
                int carry = GreyboxSimulation.EffectiveCarryCapacity(st, bal);
                for (int i = 0; i < 3; i++)
                    GreyboxSimulation.PlayerDeposit(st, bal, i, carry);
            }
            Assert.That(st.Money, Is.GreaterThan(0m));
            // Genug fuer mind. ein Upgrade.
            Assert.That(st.Money, Is.GreaterThanOrEqualTo(GreyboxSimulation.UpgradeCostFor(st, bal, UpgradeTrack.StationSpeed)));
        }
    }
}
