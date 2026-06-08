using System;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.State;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Settings;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.State
{
    /// <summary>
    /// Verifiziert das portierte GameState-Root (Schicht 16) gegen die Original-Werte
    /// (Avalonia Models/GameState.cs) inkl. v7-JSON-Save-Roundtrip (Newtonsoft).
    /// </summary>
    [TestFixture]
    public class GameStateTests
    {
        [Test]
        public void CreateNew_ProducesValidDefaultState()
        {
            var s = GameState.CreateNew();
            Assert.That(s.Version, Is.EqualTo(7));
            Assert.That(GameState.CurrentStateVersion, Is.EqualTo(7));
            Assert.That(s.Money, Is.EqualTo(1000m));
            Assert.That(s.PlayerLevel, Is.EqualTo(1));
            Assert.That(s.Workshops.Count, Is.EqualTo(1));
            Assert.That(s.Workshops[0].Type, Is.EqualTo(WorkshopType.Carpenter));
            Assert.That(s.Workshops[0].Workers.Count, Is.EqualTo(2));
            Assert.That(s.Researches.Count, Is.EqualTo(72));
            Assert.That(s.Tools.Count, Is.EqualTo(8));
        }

        [Test]
        public void XpAndOfflineHours_MatchOriginal()
        {
            Assert.That(GameState.CalculateXpForLevel(1), Is.EqualTo(0));
            Assert.That(GameState.CalculateXpForLevel(2), Is.EqualTo(100));
            Assert.That(GameState.CalculateXpForLevel(10), Is.EqualTo((int)(100 * Math.Pow(9, 1.2))));
            Assert.That(new GameState().XpForNextLevel, Is.EqualTo(100));

            Assert.That(new GameState { IsPremium = false }.MaxOfflineHours, Is.EqualTo(4));
            Assert.That(new GameState { IsPremium = true }.MaxOfflineHours, Is.EqualTo(16));
            var ext = new GameState { IsPremium = false }; ext.OfflineVideoExtended = true;
            Assert.That(ext.MaxOfflineHours, Is.EqualTo(8));
        }

        [Test]
        public void WorkshopMethods_And_LegacyForwarding()
        {
            var s = GameState.CreateNew();
            var ws = s.GetOrCreateWorkshop(WorkshopType.Plumber);
            Assert.That(ws.Type, Is.EqualTo(WorkshopType.Plumber));
            Assert.That(s.Workshops.Count, Is.EqualTo(2));
            Assert.That(s.GetOrCreateWorkshop(WorkshopType.Plumber), Is.SameAs(ws)); // idempotent

            var u = new GameState { PlayerLevel = 5 };
            u.UnlockedWorkshopTypes.Add(WorkshopType.Plumber);
            Assert.That(u.IsWorkshopUnlocked(WorkshopType.Plumber), Is.True);
            Assert.That(new GameState { PlayerLevel = 4 }.IsWorkshopUnlocked(WorkshopType.Plumber), Is.False);

            // Legacy-Forwarding auf Sub-Objekte
            var fwd = new GameState();
            var t = DateTime.UtcNow;
            fwd.SpeedBoostEndTime = t;
            Assert.That(fwd.Boosts.SpeedBoostEndTime, Is.EqualTo(t));
            fwd.DailyRewardStreak = 5;
            Assert.That(fwd.DailyProgress.DailyRewardStreak, Is.EqualTo(5));
            fwd.ActiveCityThemeId = "ct_premium";
            Assert.That(fwd.Cosmetics.ActiveCityThemeId, Is.EqualTo("ct_premium"));
        }

        [Test]
        public void V7SaveRoundtrip_PreservesState()
        {
            var save = GameState.CreateNew();
            save.Money = 123_456m;
            save.PlayerLevel = 42;
            save.GoldenScrews = 99;
            save.Prestige.BronzeCount = 3;
            save.Prestige.PermanentMultiplier = 2.5m;
            save.Settings.GraphicsQuality = GraphicsQuality.Low;
            save.Statistics.TotalOrdersCompleted = 77;
            save.CurrentRunMoney = 50_000m;

            var json = JsonConvert.SerializeObject(save);
            var d = JsonConvert.DeserializeObject<GameState>(json)!;

            Assert.That(d.Money, Is.EqualTo(123_456m));
            Assert.That(d.PlayerLevel, Is.EqualTo(42));
            Assert.That(d.GoldenScrews, Is.EqualTo(99));
            Assert.That(d.Prestige.BronzeCount, Is.EqualTo(3));
            Assert.That(d.Prestige.PermanentMultiplier, Is.EqualTo(2.5m));
            Assert.That(d.Settings.GraphicsQuality, Is.EqualTo(GraphicsQuality.Low));
            Assert.That(d.Statistics.TotalOrdersCompleted, Is.EqualTo(77));
            Assert.That(d.CurrentRunMoney, Is.EqualTo(50_000m));
            Assert.That(d.Version, Is.EqualTo(7));
            Assert.That(d.Workshops.Count, Is.EqualTo(1));
            Assert.That(d.Researches.Count, Is.EqualTo(72));
            Assert.That(d.Tools.Count, Is.EqualTo(8));
        }
    }
}
