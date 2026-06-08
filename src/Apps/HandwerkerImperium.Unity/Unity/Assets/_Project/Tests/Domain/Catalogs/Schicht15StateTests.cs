using System;
using HandwerkerImperium.Domain.Settings;
using HandwerkerImperium.Domain.Boosts;
using HandwerkerImperium.Domain.Cosmetics;
using HandwerkerImperium.Domain.Guild;
using HandwerkerImperium.Domain.Onboarding;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Catalogs
{
    /// <summary>
    /// Verifiziert die portierten Schicht-15-Sammel-States (Settings, Boost, Cosmetic, GuildMembership,
    /// FtueState, StoryChapter, SeasonStoryline) gegen die Original-Werte (Avalonia Models/*).
    /// </summary>
    [TestFixture]
    public class Schicht15StateTests
    {
        [Test]
        public void GraphicsQuality_And_Settings_Defaults()
        {
            Assert.That((int)GraphicsQuality.Low, Is.EqualTo(0));
            Assert.That((int)GraphicsQuality.High, Is.EqualTo(2));

            var sd = new SettingsData();
            Assert.That(sd.SoundEnabled, Is.True);
            Assert.That(sd.GraphicsQuality, Is.EqualTo(GraphicsQuality.High));
            Assert.That(sd.SfxVolume, Is.EqualTo(1.0f));
            Assert.That(sd.LastWhatsNewVersion, Is.EqualTo("0.0.0"));

            var au = new AutomationSettings();
            Assert.That(au.AutoAcceptOnlyStandard, Is.True);
            Assert.That(au.AutoCompleteSkipLiveOrders, Is.True);
            Assert.That(au.AutoCollectDelivery, Is.False);
        }

        [Test]
        public void BoostData_And_CosmeticData()
        {
            Assert.That(new BoostData { SpeedBoostEndTime = DateTime.UtcNow.AddHours(1) }.IsSpeedBoostActive, Is.True);
            Assert.That(new BoostData().IsSpeedBoostActive, Is.False);
            Assert.That(new BoostData().IsFreeRushAvailable, Is.True);
            Assert.That(new BoostData { LastFreeRushUsed = DateTime.UtcNow }.IsFreeRushAvailable, Is.False);

            var cd = new CosmeticData();
            Assert.That(cd.UnlockedCosmetics, Is.EqualTo(new[] { "ct_default" }));
            Assert.That(cd.ActiveCityThemeId, Is.EqualTo("ct_default"));
        }

        [Test]
        public void GuildMembership_Bonus_And_ApplyEffects()
        {
            Assert.That(new GuildMembership().LeagueId, Is.EqualTo("bronze"));
            Assert.That(new GuildMembership { GuildLevel = 10 }.IncomeBonus, Is.EqualTo(0.10m));
            Assert.That(new GuildMembership { GuildLevel = 25 }.IncomeBonus, Is.EqualTo(0.20m)); // Cap

            var gm = new GuildMembership();
            gm.ApplyHallEffects(new GuildHallEffects { CraftingSpeedBonus = 0.10m, MaxMembersBonus = 6 });
            Assert.That(gm.HallCraftingSpeedBonus, Is.EqualTo(0.10m));
            Assert.That(gm.HallMaxMembersBonus, Is.EqualTo(6));
            gm.ApplyResearchEffects(new GuildResearchEffects { IncomeBonus = 0.20m, PrestigePointBonus = 0.10m });
            Assert.That(gm.ResearchIncomeBonus, Is.EqualTo(0.20m));
            Assert.That(gm.ResearchPrestigePointBonus, Is.EqualTo(0.10m));
        }

        [Test]
        public void Ftue_And_Story_Defaults()
        {
            Assert.That(new FtueState().CurrentStepIndex, Is.EqualTo(-1));
            Assert.That(new FtueState().CompletedStepIds, Is.Not.Null);
            Assert.That(new StoryChapter().Mood, Is.EqualTo("happy"));
            Assert.That(new StoryChapter().RequiredSeasonTheme, Is.Null);

            var ssl = new SeasonStoryline { ChapterIds = new[] { "c0", "c1", "c2", "c3", "c4" } };
            Assert.That(ssl.TierTriggers, Is.EqualTo(new[] { 1, 10, 25, 40, 50 }));
            Assert.That(ssl.GetChapterIdForTier(1), Is.EqualTo("c0"));
            Assert.That(ssl.GetChapterIdForTier(25), Is.EqualTo("c2"));
            Assert.That(ssl.GetChapterIdForTier(50), Is.EqualTo("c4"));
            Assert.That(ssl.GetChapterIdForTier(5), Is.Null);
        }
    }
}
