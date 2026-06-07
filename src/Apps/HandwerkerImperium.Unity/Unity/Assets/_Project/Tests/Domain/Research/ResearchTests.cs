using System;
using System.Linq;
using HandwerkerImperium.Domain.Research;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Research
{
    /// <summary>
    /// Verifiziert den portierten Research-Tree (ResearchTree/Research/ResearchEffect) gegen die
    /// Original-Werte (Avalonia Models/ResearchTree.cs + Research.cs + ResearchEffect.cs) inkl.
    /// Katalog-Integrität (Prerequisites auflösbar, Level lückenlos).
    /// </summary>
    [TestFixture]
    public class ResearchTests
    {
        [Test]
        public void Catalog_CountsPerBranch_MatchOriginal()
        {
            var all = ResearchTree.CreateAll();
            Assert.That(all.Count, Is.EqualTo(72));
            Assert.That(all.Count(r => r.Branch == ResearchBranch.Tools), Is.EqualTo(20));
            Assert.That(all.Count(r => r.Branch == ResearchBranch.Management), Is.EqualTo(20));
            Assert.That(all.Count(r => r.Branch == ResearchBranch.Marketing), Is.EqualTo(20));
            Assert.That(all.Count(r => r.Branch == ResearchBranch.Logistics), Is.EqualTo(12));
            Assert.That(all.Select(r => r.Id).Distinct().Count(), Is.EqualTo(72));
        }

        [Test]
        public void NodeValues_MatchOriginal()
        {
            var byId = ResearchTree.CreateAll().ToDictionary(r => r.Id);

            var t1 = byId["tools_01"];
            Assert.That(t1.Cost, Is.EqualTo(500m));
            Assert.That(t1.Duration, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(t1.Effect.EfficiencyBonus, Is.EqualTo(0.05m));
            Assert.That(t1.Prerequisites, Is.Empty);
            Assert.That(t1.DescriptionKey, Is.EqualTo("ResearchBetterSawsDesc"));

            Assert.That(byId["tools_04"].Effect.UnlocksAutoMaterial, Is.True);
            Assert.That(byId["tools_04"].Prerequisites, Is.EqualTo(new[] { "tools_02", "tools_03" }));
            Assert.That(byId["mgmt_10"].Effect.UnlocksSTierWorkers, Is.True);
            Assert.That(byId["mgmt_07"].Effect.UnlocksAutoAssign, Is.True);
            Assert.That(byId["logi_05"].Effect.UnlocksMarket, Is.True);
            Assert.That(byId["logi_02"].Effect.StackLimitMultiplier, Is.EqualTo(2.0m));

            var t20 = byId["tools_20"];
            Assert.That(t20.Cost, Is.EqualTo(100_000_000_000m));
            Assert.That(t20.Duration, Is.EqualTo(TimeSpan.FromHours(168)));
            Assert.That(t20.Effect.EfficiencyBonus, Is.EqualTo(0.30m));
            Assert.That(t20.Effect.AscensionPointBonus, Is.EqualTo(0.25m));
        }

        [Test]
        public void Catalog_Integrity_PrerequisitesAndLevels()
        {
            var all = ResearchTree.CreateAll();
            var byId = all.ToDictionary(r => r.Id);

            foreach (var r in all)
                foreach (var p in r.Prerequisites)
                    Assert.That(byId.ContainsKey(p), Is.True, $"Prereq fehlt: {r.Id} -> {p}");

            foreach (var br in new[] { ResearchBranch.Tools, ResearchBranch.Management, ResearchBranch.Marketing, ResearchBranch.Logistics })
            {
                var lvls = all.Where(r => r.Branch == br).Select(r => r.Level).OrderBy(x => x).ToList();
                Assert.That(lvls, Is.EqualTo(Enumerable.Range(1, lvls.Count)), $"{br} Level nicht lückenlos");
            }
        }

        [Test]
        public void InstantFinishScrewCost_MatchOriginal()
        {
            int Cost(int level) => new Research { Level = level }.InstantFinishScrewCost;
            Assert.That(Cost(7), Is.EqualTo(0));
            Assert.That(Cost(8), Is.EqualTo(15));
            Assert.That(Cost(10), Is.EqualTo(40));
            Assert.That(Cost(19), Is.EqualTo(400));
            Assert.That(Cost(20), Is.EqualTo(500));
        }

        [Test]
        public void Progress_And_RemainingTime_Compute()
        {
            Assert.That(new Research { IsResearched = true }.Progress, Is.EqualTo(100.0));
            Assert.That(new Research().RemainingTime, Is.Null);

            var active = new Research
            {
                Level = 20,
                IsActive = true,
                StartedAt = DateTime.UtcNow.AddHours(-1),
                DurationTicks = TimeSpan.FromHours(2).Ticks
            };
            Assert.That(active.CanInstantFinish, Is.True);
            Assert.That(active.Progress, Is.InRange(45.0, 55.0));
            Assert.That(active.RemainingTime, Is.Not.Null);
        }

        [Test]
        public void ResearchEffect_Combine_MatchOriginal()
        {
            var a = new ResearchEffect { EfficiencyBonus = 0.10m, ExtraWorkerSlots = 1, UnlocksMarket = true, StackLimitMultiplier = 2.0m };
            var b = new ResearchEffect { EfficiencyBonus = 0.05m, ExtraWorkerSlots = 2, UnlocksTier4 = true, StackLimitMultiplier = 5.0m };
            var c = ResearchEffect.Combine(a, b);
            Assert.That(c.EfficiencyBonus, Is.EqualTo(0.15m));
            Assert.That(c.ExtraWorkerSlots, Is.EqualTo(3));
            Assert.That(c.UnlocksMarket, Is.True);
            Assert.That(c.UnlocksTier4, Is.True);
            Assert.That(c.StackLimitMultiplier, Is.EqualTo(5.0m)); // MAX, nicht additiv
        }
    }
}
