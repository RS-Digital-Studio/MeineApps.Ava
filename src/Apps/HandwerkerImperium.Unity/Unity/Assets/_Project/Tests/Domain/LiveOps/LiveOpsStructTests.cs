using System;
using HandwerkerImperium.Domain.Events;
using HandwerkerImperium.Domain.LiveOps;
using HandwerkerImperium.Domain.Guild;
using HandwerkerImperium.Domain.Persistence;
using HandwerkerImperium.Domain.Statistics;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.LiveOps
{
    /// <summary>
    /// Verifiziert die portierten Effekt-/Reward-Structs + blattlosen Daten (Schicht 11) gegen die
    /// Original-Werte (Avalonia Models/*). Inkl. Friend-Determinismus (Random→Instanz) und
    /// GuildMegaProject-Katalog.
    /// </summary>
    [TestFixture]
    public class LiveOpsStructTests
    {
        [Test]
        public void GameEventEffect_Defaults_MatchOriginal()
        {
            var e = new GameEventEffect();
            Assert.That(e.IncomeMultiplier, Is.EqualTo(1.0m));
            Assert.That(e.CostMultiplier, Is.EqualTo(1.0m));
            Assert.That(e.RewardMultiplier, Is.EqualTo(1.0m));
            Assert.That(e.MarketRestriction, Is.Null);
            Assert.That(e.AffectedWorkshop, Is.Null);
        }

        [Test]
        public void Friend_GiftAmount_And_DeterministicSimulation()
        {
            Assert.That(new Friend { FriendshipLevel = 1 }.GiftAmount, Is.EqualTo(1));
            Assert.That(new Friend { FriendshipLevel = 3 }.GiftAmount, Is.EqualTo(2));
            Assert.That(new Friend { FriendshipLevel = 5 }.GiftAmount, Is.EqualTo(3));

            var a = Friend.CreateSimulatedFriends(new Random(42));
            var b = Friend.CreateSimulatedFriends(new Random(42));
            Assert.That(a.Count, Is.EqualTo(5));
            Assert.That(a[0].Name, Is.EqualTo("MaxBuilder"));
            Assert.That(a[4].Name, Is.EqualTo("OttoMeister"));
            Assert.That(a[0].Level, Is.EqualTo(b[0].Level)); // deterministisch bei gleichem Seed
            Assert.That(a.TrueForAll(f => f.Level >= 5 && f.Level < 50), Is.True);
        }

        [Test]
        public void ReferralProgress_PermanentBonus()
        {
            var rp = new ReferralProgress();
            Assert.That(rp.PermanentIncomeBonus, Is.EqualTo(0m));
            rp.ClaimedTiers.Add(10);
            Assert.That(rp.PermanentIncomeBonus, Is.EqualTo(0.05m));
        }

        [Test]
        public void WelcomeBackOffer_Expiry()
        {
            Assert.That(new WelcomeBackOffer { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) }.IsExpired, Is.True);
            var future = new WelcomeBackOffer { ExpiresAt = DateTime.UtcNow.AddHours(1) };
            Assert.That(future.IsExpired, Is.False);
            Assert.That(future.TimeRemaining, Is.GreaterThan(TimeSpan.Zero));
        }

        [Test]
        public void CloudSaveMetadata_ParsesIso()
        {
            var iso = "2026-06-07T12:00:00.0000000Z";
            Assert.That(new CloudSaveMetadata { SavedAtIso = iso }.SavedAtUtc.Year, Is.EqualTo(2026));
            Assert.That(new CloudSaveMetadata().SavedAtUtc, Is.EqualTo(DateTime.MinValue));
        }

        [Test]
        public void GuildMegaProject_Catalog_MatchOriginal()
        {
            var req = GuildMegaProjectTemplates.GetRequirements(GuildMegaProjectType.Cathedral);
            Assert.That(req["villa"], Is.EqualTo(1));
            Assert.That(req["luxury_furniture"], Is.EqualTo(50));
            var reqHq = GuildMegaProjectTemplates.GetRequirements(GuildMegaProjectType.Headquarters);
            Assert.That(reqHq["skyscraper"], Is.EqualTo(1));
            Assert.That(reqHq["villa"], Is.EqualTo(2));

            var rew = GuildMegaProjectTemplates.GetReward(GuildMegaProjectType.Cathedral);
            Assert.That(rew.CraftingSpeedBonus, Is.EqualTo(0.05m));
            Assert.That(rew.BonusWarehouseSlots, Is.EqualTo(3));
            var rewHq = GuildMegaProjectTemplates.GetReward(GuildMegaProjectType.Headquarters);
            Assert.That(rewHq.AutoSellPriceBonus, Is.EqualTo(0.20m));
            Assert.That(GuildMegaProjectTemplates.AbandonmentSunsetDays, Is.EqualTo(30));
        }

        [Test]
        public void MiniGameStats_And_BattlePassReward_Defaults()
        {
            Assert.That(MiniGameStats.RollingWindowSize, Is.EqualTo(20));
            Assert.That(new MiniGameStats().RollingResults, Is.Not.Null);
            Assert.That(new BattlePassReward().RewardType, Is.EqualTo(BattlePassRewardType.Standard));
        }
    }
}
