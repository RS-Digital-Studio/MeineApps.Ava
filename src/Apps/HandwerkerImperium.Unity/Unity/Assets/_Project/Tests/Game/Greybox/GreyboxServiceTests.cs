using NUnit.Framework;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Game;

namespace HandwerkerImperium.Game.Tests
{
    /// <summary>
    /// P0-Verifikation der Game-§3-Service-Schicht (headless, ohne Scene/MonoBehaviour).
    /// Prueft genau das, was die Domain-Tests NICHT abdecken: korrekte Delegation an den
    /// verifizierten Idle-Kern, das <c>MoneyChanged</c>-Event-Verhalten (feuert bei Mutation,
    /// schweigt bei Fehlschlag), den geteilten Session-State ueber alle Services und den
    /// JSON-Save-Roundtrip (decimal + Stations-Liste durch Newtonsoft).
    /// </summary>
    public sealed class GreyboxServiceTests
    {
        // ── Test-Setup: alle Services auf EINER Session ────────────────────
        private sealed class Rig
        {
            public IdleBalancing Bal;
            public GreyboxSimState State;
            public GreyboxSession Session;
            public EconomyService Economy;
            public StationService Stations;
            public WorkerAutomationService Workers;
            public UpgradePadService Upgrades;
            public PlotUnlockService Plots;
            public OfflineProgressService Offline;
            public int MoneyEvents;
            public decimal LastMoneyEvent;
        }

        private static Rig NewRig(decimal startMoney = 0m)
        {
            var bal = new IdleBalancing();
            var state = GreyboxSimState.CreateNew(bal);
            state.Money = startMoney;
            var session = new GreyboxSession(bal, state);
            var economy = new EconomyService(session);
            var rig = new Rig
            {
                Bal = bal,
                State = state,
                Session = session,
                Economy = economy,
                Stations = new StationService(session),
                Workers = new WorkerAutomationService(session, economy),
                Upgrades = new UpgradePadService(session, economy),
                Plots = new PlotUnlockService(session, economy),
                Offline = new OfflineProgressService(session, economy),
            };
            economy.MoneyChanged += m => { rig.MoneyEvents++; rig.LastMoneyEvent = m; };
            return rig;
        }

        private const long TicksPerSecond = 10_000_000L;

        // ── Balancing -> State-Mapping (Vertrag fuer Scene + Save) ──────────
        [Test]
        public void CreateNew_MapsBalancing_TenStations_OnlyCarpenterOpen()
        {
            var r = NewRig();
            Assert.AreEqual(10, r.State.Stations.Count, "Balancing hat alle 10 Gewerke (GDD §6.1)");
            Assert.IsTrue(r.Stations.IsUnlocked(0), "schreiner offen (GDD §5.1: Start nur Schreinerei)");
            for (int i = 1; i < 10; i++)
                Assert.IsFalse(r.Stations.IsUnlocked(i), $"Station {i} startet gesperrt (Plot-Unlock)");
            Assert.AreEqual("schreiner", r.State.Stations[0].Id);
            Assert.AreEqual("klempner", r.State.Stations[1].Id);
            Assert.AreEqual("innovationslabor", r.State.Stations[9].Id);
        }

        // ── Geteilter Session-State ueber alle Services ────────────────────
        [Test]
        public void Services_ShareOneState()
        {
            var r = NewRig(startMoney: 1000m);
            Assert.AreEqual(1000m, r.Economy.Money);
            bool bought = r.Upgrades.Buy(UpgradeTrack.StationSpeed);
            Assert.IsTrue(bought);
            // Mutation via UpgradePadService ist sofort via EconomyService sichtbar = selber State.
            Assert.AreEqual(r.State.Money, r.Economy.Money);
            Assert.Less(r.Economy.Money, 1000m);
        }

        // ── Produktion + Aufnahme (StationService -> Idle-Kern) ────────────
        [Test]
        public void Station_Tick_Produces_Then_Pickup_Reduces()
        {
            var r = NewRig();
            r.Stations.Tick(5.0); // schreiner: 2.0s/Ware -> floor(5/2) = 2
            Assert.AreEqual(2, r.Stations.Stock(0));
            int got = r.Stations.Pickup(0, 1);
            Assert.AreEqual(1, got);
            Assert.AreEqual(1, r.Stations.Stock(0));
        }

        // ── Verkauf am Tresen feuert MoneyChanged mit neuem Stand ──────────
        [Test]
        public void Economy_Sell_CreditsAndRaises_ZeroSellNoEvent()
        {
            var r = NewRig();
            decimal earned = r.Economy.Sell(0, 3); // schreiner SellValue 5 -> 15
            Assert.AreEqual(15m, earned);
            Assert.AreEqual(15m, r.Economy.Money);
            Assert.AreEqual(1, r.MoneyEvents);
            Assert.AreEqual(15m, r.LastMoneyEvent);

            decimal none = r.Economy.Sell(0, 0); // nichts verkauft -> kein Event
            Assert.AreEqual(0m, none);
            Assert.AreEqual(1, r.MoneyEvents, "kein Event bei 0-Verkauf");
        }

        // ── Upgrade: geometrische Kosten, Kauf zieht ab + feuert genau 1x ──
        [Test]
        public void Upgrade_GeometricCost_BuyOnce_FailNoEvent()
        {
            var r = NewRig(startMoney: 1000m);
            Assert.AreEqual(0, r.Upgrades.LevelOf(UpgradeTrack.StationSpeed));
            Assert.AreEqual(50m, r.Upgrades.CostFor(UpgradeTrack.StationSpeed)); // base 50 * 1.6^0

            Assert.IsTrue(r.Upgrades.Buy(UpgradeTrack.StationSpeed));
            Assert.AreEqual(1, r.Upgrades.LevelOf(UpgradeTrack.StationSpeed));
            Assert.AreEqual(950m, r.Economy.Money);
            Assert.AreEqual(1, r.MoneyEvents);
            Assert.AreEqual(80m, r.Upgrades.CostFor(UpgradeTrack.StationSpeed)); // base 50 * 1.6^1 = 80

            r.State.Money = 10m; // unter naechster Kostenstufe
            Assert.IsFalse(r.Upgrades.Buy(UpgradeTrack.StationSpeed), "zu wenig Geld -> kein Kauf");
            Assert.AreEqual(1, r.Upgrades.LevelOf(UpgradeTrack.StationSpeed), "Level unveraendert");
            Assert.AreEqual(1, r.MoneyEvents, "kein Event bei Fehlschlag");
        }

        // ── Worker: Anstellen zieht ab + feuert; Tick automatisiert Geld ───
        [Test]
        public void Worker_Hire_Then_Tick_Automates_Money()
        {
            var r = NewRig(startMoney: 500m);
            Assert.IsFalse(r.Workers.HasWorker(0));
            Assert.AreEqual(200m, r.Workers.HireCost);

            Assert.IsTrue(r.Workers.Hire(0));
            Assert.IsTrue(r.Workers.HasWorker(0));
            Assert.AreEqual(300m, r.Economy.Money);
            Assert.AreEqual(1, r.MoneyEvents);

            r.Stations.Tick(20.0); // Stock bis StackCap 8 aufbauen
            Assert.Greater(r.Stations.Stock(0), 0);

            decimal before = r.Economy.Money;
            int stockBefore = r.Stations.Stock(0);
            decimal earned = r.Workers.Tick(5.0); // Worker bewegt Stock -> Geld
            Assert.Greater(earned, 0m, "Automatisierung erzeugt Geld");
            Assert.AreEqual(before + earned, r.Economy.Money);
            Assert.Less(r.Stations.Stock(0), stockBefore, "Worker verbraucht Stock");
            Assert.AreEqual(2, r.MoneyEvents, "Tick mit Geld-Delta feuert genau 1x zusaetzlich");
        }

        // ── Plot-Unlock: per-Station-Kosten, schaltet frei + feuert; sonst false ──
        [Test]
        public void Plot_Unlock_Succeeds_WithMoney_FailsWithout()
        {
            var ok = NewRig(startMoney: 500m);
            Assert.IsFalse(ok.Plots.IsUnlocked(1));
            Assert.AreEqual(500m, ok.Plots.UnlockCostFor(1), "klempner = erster Plot (500)");
            Assert.AreEqual(4000m, ok.Plots.UnlockCostFor(3), "maler = vierter Plot (Progression)");
            Assert.IsTrue(ok.Plots.Unlock(1));
            Assert.IsTrue(ok.Plots.IsUnlocked(1));
            Assert.IsTrue(ok.Stations.IsUnlocked(1), "StationService sieht die Freischaltung");
            Assert.AreEqual(0m, ok.Economy.Money);
            Assert.AreEqual(1, ok.MoneyEvents);

            var poor = NewRig(startMoney: 499m);
            Assert.IsFalse(poor.Plots.Unlock(1), "zu wenig Geld -> kein Unlock");
            Assert.IsFalse(poor.Plots.IsUnlocked(1));
            Assert.AreEqual(0, poor.MoneyEvents);
        }

        // ── Offline: Preview -> Claim schreibt gut + verschiebt Zeitstempel ─
        [Test]
        public void Offline_Preview_Then_Claim_Credits_And_Advances()
        {
            var r = NewRig(startMoney: 10000m);
            Assert.IsTrue(r.Workers.Hire(0)); // automatisierte Station -> Offline-Einkommen
            int eventsAfterHire = r.MoneyEvents;

            long baseTicks = 1_000_000_000_000L;
            r.Offline.MarkSeen(baseTicks);
            long now = baseTicks + 3600L * TicksPerSecond; // +1 h

            decimal preview = r.Offline.Preview(now);
            Assert.Greater(preview, 0m, "Offline-Verdienst > 0 bei automatisierter Station");

            decimal moneyBefore = r.Economy.Money;
            decimal claimed = r.Offline.Claim(now);
            Assert.AreEqual(preview, claimed, "Claim entspricht der Preview");
            Assert.AreEqual(moneyBefore + claimed, r.Economy.Money, "Betrag gutgeschrieben");
            Assert.AreEqual(now, r.State.LastSeenUtcTicks, "Zeitstempel auf 'jetzt' gesetzt");
            Assert.AreEqual(eventsAfterHire + 1, r.MoneyEvents, "Claim feuert genau 1x");

            Assert.AreEqual(0m, r.Offline.Preview(now), "nach Claim keine weitere Offline-Zeit");
        }

        // ── Save-Roundtrip: decimal + Stations-Liste durch JSON ────────────
        [Test]
        public void Save_Roundtrip_Preserves_Money_Levels_Stock_Worker_Unlock()
        {
            // WICHTIG: eigener Test-Slot — der Default-Slot ist der echte Greybox-Spielstand.
            const string slot = "hwi_greybox_save_TEST";
            GreyboxSave.Clear(slot); // sauberer Start (PlayerPrefs ist editor-persistent)
            try
            {
                var r = NewRig(startMoney: 5000m);
                r.Upgrades.Buy(UpgradeTrack.CarryCapacity);   // Level != 0
                r.Stations.Tick(6.0);                          // Stock an Station 0
                r.Workers.Hire(0);                             // HasWorker
                r.Plots.Unlock(3);                             // Station 3 offen
                r.Offline.MarkSeen(1_234_567_890L);            // Zeitstempel

                GreyboxSave.Save(r.State, slot);
                GreyboxSimState loaded = GreyboxSave.Load(slot);

                Assert.IsNotNull(loaded, "Load liefert State zurueck");
                Assert.AreEqual(r.State.Money, loaded.Money, "decimal Money exakt");
                Assert.AreEqual(r.State.CarryCapacityLevel, loaded.CarryCapacityLevel);
                Assert.AreEqual(10, loaded.Stations.Count);
                Assert.AreEqual(r.State.Stations[0].Stock, loaded.Stations[0].Stock);
                Assert.IsTrue(loaded.Stations[0].HasWorker);
                Assert.IsTrue(loaded.Stations[3].Unlocked, "Plot-Unlock ueberlebt Save");
                Assert.AreEqual(1_234_567_890L, loaded.LastSeenUtcTicks);
                Assert.AreEqual("schreiner", loaded.Stations[0].Id, "Station-Id ueberlebt");
            }
            finally
            {
                GreyboxSave.Clear(slot);
            }
        }
    }
}
