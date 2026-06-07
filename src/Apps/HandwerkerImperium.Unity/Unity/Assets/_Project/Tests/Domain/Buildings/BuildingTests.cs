using HandwerkerImperium.Domain.Buildings;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Buildings
{
    /// <summary>
    /// Verifiziert das portierte Buildings-Subsystem (BuildingType + Building) gegen die
    /// Original-Werte (Avalonia Models/Building.cs + Enums/BuildingType.cs).
    /// </summary>
    [TestFixture]
    public class BuildingTests
    {
        [Test]
        public void BuildingType_CostsAndUnlocks_MatchOriginal()
        {
            Assert.That(BuildingType.Canteen.GetBaseCost(), Is.EqualTo(10_000m));
            Assert.That(BuildingType.WorkshopExtension.GetBaseCost(), Is.EqualTo(100_000m));
            Assert.That(BuildingType.Canteen.GetUnlockLevel(), Is.EqualTo(5));
            Assert.That(BuildingType.WorkshopExtension.GetUnlockLevel(), Is.EqualTo(30));
            Assert.That(BuildingType.TrainingCenter.GetMaxLevel(), Is.EqualTo(5));
        }

        [Test]
        public void NextLevelCost_And_CanUpgrade_MatchOriginal()
        {
            var notBuilt = new Building { Type = BuildingType.Canteen, IsBuilt = false };
            Assert.That(notBuilt.NextLevelCost, Is.EqualTo(10_000m));
            Assert.That(notBuilt.CanUpgrade, Is.False);

            var lvl1 = new Building { Type = BuildingType.Canteen, IsBuilt = true, Level = 1 };
            Assert.That(lvl1.NextLevelCost, Is.EqualTo(20_000m)); // 10k * 2^1

            var lvl5 = new Building { Type = BuildingType.Canteen, IsBuilt = true, Level = 5 };
            Assert.That(lvl5.NextLevelCost, Is.EqualTo(0m));
            Assert.That(lvl5.CanUpgrade, Is.False);
            Assert.That(new Building { Type = BuildingType.Canteen, IsBuilt = true, Level = 4 }.CanUpgrade, Is.True);
        }

        [Test]
        public void Effects_MatchOriginal()
        {
            Assert.That(new Building { Type = BuildingType.Canteen, Level = 3 }.MoodRecoveryPerHour, Is.EqualTo(3.0m));
            Assert.That(new Building { Type = BuildingType.Canteen, Level = 3 }.RestTimeReduction, Is.EqualTo(0.60m));
            Assert.That(new Building { Type = BuildingType.Storage, Level = 2 }.MaterialCostReduction, Is.EqualTo(0.25m));
            Assert.That(new Building { Type = BuildingType.Office, Level = 3 }.ExtraOrderSlots, Is.EqualTo(4));
            Assert.That(new Building { Type = BuildingType.Showroom, Level = 4 }.DailyReputationGain, Is.EqualTo(2.0m));
            Assert.That(new Building { Type = BuildingType.TrainingCenter, Level = 3 }.TrainingSpeedMultiplier, Is.EqualTo(4.5m));
            Assert.That(new Building { Type = BuildingType.VehicleFleet, Level = 5 }.OrderRewardBonus, Is.EqualTo(0.60m));
            Assert.That(new Building { Type = BuildingType.WorkshopExtension, Level = 2 }.ExtraWorkerSlots, Is.EqualTo(3));
            // Falscher Typ liefert 0
            Assert.That(new Building { Type = BuildingType.Office, Level = 5 }.MoodRecoveryPerHour, Is.EqualTo(0m));
        }

        [Test]
        public void Create_SetsDefaults()
        {
            var b = Building.Create(BuildingType.Showroom);
            Assert.That(b.Type, Is.EqualTo(BuildingType.Showroom));
            Assert.That(b.Level, Is.EqualTo(0));
            Assert.That(b.IsBuilt, Is.False);
        }
    }
}
