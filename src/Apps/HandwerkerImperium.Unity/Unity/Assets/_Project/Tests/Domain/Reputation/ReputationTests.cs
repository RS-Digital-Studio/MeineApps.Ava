using HandwerkerImperium.Domain.Reputation;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Reputation
{
    /// <summary>
    /// Verifiziert das portierte Reputations-Subsystem (CustomerReputationTier, CustomerReputation,
    /// RegularCustomer) gegen die Original-Werte (Avalonia Models/CustomerReputation.cs + Enums).
    /// </summary>
    [TestFixture]
    public class ReputationTests
    {
        [Test]
        public void Tier_Bonuses_MatchOriginal()
        {
            Assert.That(CustomerReputationTier.CityKnown.GetRegularCustomerBonus(), Is.EqualTo(0.10m));
            Assert.That(CustomerReputationTier.IndustryLegend.GetRegularCustomerBonus(), Is.EqualTo(0.35m));
            Assert.That(CustomerReputationTier.RegionStar.GetLiveOrderSpawnChance(), Is.EqualTo(0.05m));
            Assert.That(CustomerReputationTier.IndustryLegend.GetLiveOrderSpawnChance(), Is.EqualTo(0.10m));
            Assert.That(CustomerReputationTier.CityKnown.GetLiveOrderSpawnChance(), Is.EqualTo(0m));
        }

        [Test]
        public void FromScore_And_Hysteresis_MatchOriginal()
        {
            Assert.That(CustomerReputationTierExtensions.FromScore(30), Is.EqualTo(CustomerReputationTier.Beginner));
            Assert.That(CustomerReputationTierExtensions.FromScore(31), Is.EqualTo(CustomerReputationTier.CityKnown));
            Assert.That(CustomerReputationTierExtensions.FromScore(61), Is.EqualTo(CustomerReputationTier.RegionStar));
            Assert.That(CustomerReputationTierExtensions.FromScore(81), Is.EqualTo(CustomerReputationTier.IndustryLegend));

            // Hysterese: hoch bei 61, runter erst bei < 58 (kein Flackern dazwischen)
            Assert.That(CustomerReputationTierExtensions.FromScoreWithHysteresis(61, CustomerReputationTier.CityKnown),
                Is.EqualTo(CustomerReputationTier.RegionStar));
            Assert.That(CustomerReputationTierExtensions.FromScoreWithHysteresis(59, CustomerReputationTier.RegionStar),
                Is.EqualTo(CustomerReputationTier.RegionStar));
            Assert.That(CustomerReputationTierExtensions.FromScoreWithHysteresis(57, CustomerReputationTier.RegionStar),
                Is.EqualTo(CustomerReputationTier.CityKnown));
        }

        [Test]
        public void Reputation_Multipliers_And_Slots_MatchOriginal()
        {
            decimal Mul(int s) => new CustomerReputation { ReputationScore = s }.ReputationMultiplier;
            Assert.That(Mul(29), Is.EqualTo(0.7m));
            Assert.That(Mul(30), Is.EqualTo(1.0m));
            Assert.That(Mul(60), Is.EqualTo(1.2m));
            Assert.That(Mul(80), Is.EqualTo(1.5m));

            Assert.That(new CustomerReputation { ReputationScore = 69 }.ExtraOrderSlots, Is.EqualTo(0));
            Assert.That(new CustomerReputation { ReputationScore = 70 }.ExtraOrderSlots, Is.EqualTo(1));
            Assert.That(new CustomerReputation { ReputationScore = 90 }.ExtraOrderSlots, Is.EqualTo(2));
            Assert.That(new CustomerReputation { ReputationScore = 29 }.OrderQualityBonus, Is.EqualTo(-0.10m));
            Assert.That(new CustomerReputation { ReputationScore = 80 }.OrderQualityBonus, Is.EqualTo(0.20m));
        }

        [Test]
        public void AddRating_And_Decay_And_RecomputeTier()
        {
            var rep = new CustomerReputation { ReputationScore = 50 };
            rep.AddRating(5);
            Assert.That(rep.ReputationScore, Is.EqualTo(53));

            var rep2 = new CustomerReputation { ReputationScore = 50 };
            rep2.AddRating(5, 1.0m); // 3 -> ceil(3*2.0) = 6
            Assert.That(rep2.ReputationScore, Is.EqualTo(56));

            var rep3 = new CustomerReputation { ReputationScore = 50 };
            rep3.AddRating(1); // -5
            Assert.That(rep3.ReputationScore, Is.EqualTo(45));

            var decay = new CustomerReputation { ReputationScore = 60 };
            decay.DecayReputation();
            Assert.That(decay.ReputationScore, Is.EqualTo(59));
            var noDecay = new CustomerReputation { ReputationScore = 50 };
            noDecay.DecayReputation();
            Assert.That(noDecay.ReputationScore, Is.EqualTo(50));

            var tier = new CustomerReputation { ReputationScore = 61 };
            bool changed = tier.RecomputeTier(out var oldT);
            Assert.That(changed, Is.True);
            Assert.That(tier.CurrentTier, Is.EqualTo(CustomerReputationTier.RegionStar));
            Assert.That(oldT, Is.EqualTo(CustomerReputationTier.Beginner));
        }

        [Test]
        public void RegularCustomer_Defaults()
        {
            Assert.That(new RegularCustomer { PerfectOrderCount = 4 }.IsRegular, Is.False);
            Assert.That(new RegularCustomer { PerfectOrderCount = 5 }.IsRegular, Is.True);
            Assert.That(new RegularCustomer().BonusMultiplier, Is.EqualTo(1.1m));
            Assert.That(new RegularCustomer().AvatarSeed.Length, Is.EqualTo(8));
        }
    }
}
