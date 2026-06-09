using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Restoration;
using HandwerkerImperium.Domain.Runtime;
using HandwerkerImperium.Domain.Save;

namespace HandwerkerImperium.Domain.Tests.Runtime
{
    /// <summary>
    /// Verifiziert die vollständige Persistenz-Kette: GameModel → GameSave → HMAC-Signatur → JSON-Roundtrip →
    /// Signatur-Verifikation → GameModel zurück, mit Werte-Gleichheit über alle Slices (inkl. P2/P3-Endgame/Perks/Sammlung).
    /// </summary>
    [TestFixture]
    public class GameModelMappingTests
    {
        private const string Key = "device-key-runtime";

        [Test]
        public void Roundtrip_Model_To_SignedSave_To_Model()
        {
            var idleBal = new IdleBalancing();
            var m = GameModel.CreateNew(idleBal);
            m.Idle.Money = 5000m;
            m.Gems = 42m;
            m.Idle.Stations[0].Stock = 3;
            m.Idle.Stations[0].HasWorker = true;
            m.Idle.StationSpeedLevel = 2;
            m.Idle.Stations[3].Unlocked = true;
            m.Idle.LastSeenUtcTicks = 999L;
            m.Orders.TotalServed = 80;
            m.Orders.PendingCustomers = 3;
            m.Landmarks.Add(new LandmarkState("marktplatz", 5) { PhasesComplete = 2 });
            m.Meta.PrestigeCount = 1; m.Meta.CityIndex = 1; m.Meta.PrestigeMultiplier = 3m; m.Meta.PrestigeCurrency = 5m;
            m.Meta.CurrentStar = 4; m.Meta.MasteryLevel = 6; m.Meta.MasteryXp = 1234.5;
            m.Meta.MeistergradGrade = 2; m.Meta.Renommee = 333m; m.Meta.AvailableMarks = 7;
            m.PerkLevels = new List<int> { 1, 0, 3 };
            m.CollectedMasterTools.Add("mt_golden_hammer");
            m.OwnedSkins.Add("premium"); m.ActiveSkin = "premium";
            m.DailyStreakDay = 4; m.DailyLastClaimUtcTicks = 123L;
            m.ClaimedAchievements.Add("orders_10");

            var save = GameModelMapping.ToSave(m);
            SaveSignature.Sign(save, Key);
            string json = JsonConvert.SerializeObject(save);
            var loadedSave = JsonConvert.DeserializeObject<GameSave>(json);
            Assert.That(SaveSignature.Verify(loadedSave, Key), Is.True, "HMAC ueberlebt Model->Save->JSON");

            var m2 = GameModelMapping.FromSave(loadedSave!, idleBal);
            Assert.That(m2.Idle.Money, Is.EqualTo(5000m));
            Assert.That(m2.Gems, Is.EqualTo(42m));
            Assert.That(m2.Idle.Stations[0].Stock, Is.EqualTo(3));
            Assert.That(m2.Idle.Stations[0].HasWorker, Is.True);
            Assert.That(m2.Idle.StationSpeedLevel, Is.EqualTo(2));
            Assert.That(m2.Idle.Stations[3].Unlocked, Is.True);
            Assert.That(m2.Idle.LastSeenUtcTicks, Is.EqualTo(999L));
            Assert.That(m2.Orders.TotalServed, Is.EqualTo(80));
            Assert.That(m2.Landmarks[0].PhasesComplete, Is.EqualTo(2));
            Assert.That(m2.Meta.PrestigeMultiplier, Is.EqualTo(3m));
            Assert.That(m2.Meta.CurrentStar, Is.EqualTo(4));
            Assert.That(m2.Meta.MasteryLevel, Is.EqualTo(6));
            Assert.That(m2.Meta.MeistergradGrade, Is.EqualTo(2));
            Assert.That(m2.Meta.AvailableMarks, Is.EqualTo(7));
            Assert.That(m2.PerkLevels, Is.EqualTo(new[] { 1, 0, 3 }).AsCollection);
            Assert.That(m2.CollectedMasterTools[0], Is.EqualTo("mt_golden_hammer"));
            Assert.That(m2.ActiveSkin, Is.EqualTo("premium"));
            Assert.That(m2.DailyStreakDay, Is.EqualTo(4));
            // abgeleitete Aggregate
            Assert.That(m2.Meta.RestorationPhases, Is.EqualTo(2));
            Assert.That(m2.Meta.OrdersServed, Is.EqualTo(80));
        }
    }
}
