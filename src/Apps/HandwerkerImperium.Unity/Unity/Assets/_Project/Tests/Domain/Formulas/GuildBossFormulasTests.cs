using System;
using System.Collections.Generic;
using HandwerkerImperium.Domain.Guild;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Formulas
{
    /// <summary>
    /// Verifiziert den Service-Formel-Extrakt GuildBossFormulas (aus GuildBossService) gegen die
    /// Original-Mathematik: Schadens-Multiplikator nach Quelle, HP-Skalierung nach Mitgliederzahl
    /// (Code max(0.5, members/5.0)), Wochen-Rotation, Belohnung nach Rang, Rang-Berechnung, Rest-HP.
    /// </summary>
    [TestFixture]
    public class GuildBossFormulasTests
    {
        [Test]
        public void EffectiveDamage_SourceMultiplier()
        {
            GuildBossDefinition craftBoss = null, orderBoss = null;
            foreach (var d in GuildBossDefinition.GetAll())
            {
                if (craftBoss == null && d.CraftingDamageMultiplier > 1m) craftBoss = d;
                if (orderBoss == null && d.OrderDamageMultiplier > 1m) orderBoss = d;
            }
            Assert.That(craftBoss, Is.Not.Null, "Kein Boss mit Crafting-Multiplikator");
            Assert.That(orderBoss, Is.Not.Null, "Kein Boss mit Order-Multiplikator");

            long expCraft = (long)(100 * craftBoss.CraftingDamageMultiplier);
            Assert.That(GuildBossFormulas.CalculateEffectiveDamage(100, craftBoss, "crafting"), Is.EqualTo(expCraft));
            Assert.That(GuildBossFormulas.CalculateEffectiveDamage(100, craftBoss, "CRAFTING"), Is.EqualTo(expCraft));
            Assert.That(GuildBossFormulas.CalculateEffectiveDamage(100, craftBoss, "xyz"), Is.EqualTo(100));

            long expOrder = (long)(100 * orderBoss.OrderDamageMultiplier);
            Assert.That(GuildBossFormulas.CalculateEffectiveDamage(100, orderBoss, "order"), Is.EqualTo(expOrder));
            Assert.That(GuildBossFormulas.CalculateEffectiveDamage(100, orderBoss, "orders"), Is.EqualTo(expOrder));
        }

        [Test]
        public void WeekRotation_And_HpScaling()
        {
            Assert.That(GuildBossFormulas.GetBossIndexForWeek(7, 6), Is.EqualTo(1));
            Assert.That(GuildBossFormulas.GetBossIndexForWeek(6, 6), Is.EqualTo(0));
            Assert.That(GuildBossFormulas.GetBossIndexForWeek(13, 6), Is.EqualTo(1));

            Assert.That(GuildBossFormulas.CalculateScaledBossHp(1000, 1), Is.EqualTo(500L));
            Assert.That(GuildBossFormulas.CalculateScaledBossHp(1000, 5), Is.EqualTo(1000L));
            Assert.That(GuildBossFormulas.CalculateScaledBossHp(1000, 10), Is.EqualTo(2000L));
            Assert.That(GuildBossFormulas.CalculateScaledBossHp(1000, 0), Is.EqualTo(500L));   // mc -> 1
            Assert.That(GuildBossFormulas.CalculateScaledBossHp(1000, 20), Is.EqualTo(4000L));  // Code: 20/5=4x
        }

        [Test]
        public void Reward_And_Rank_And_Hp()
        {
            Assert.That(GuildBossFormulas.CalculateBossReward(1), Is.EqualTo(30));
            Assert.That(GuildBossFormulas.CalculateBossReward(2), Is.EqualTo(20));
            Assert.That(GuildBossFormulas.CalculateBossReward(3), Is.EqualTo(20));
            Assert.That(GuildBossFormulas.CalculateBossReward(4), Is.EqualTo(10));

            Assert.That(GuildBossFormulas.CalculateRank(50, new long[] { 100, 60, 50, 30 }), Is.EqualTo(3));
            Assert.That(GuildBossFormulas.CalculateRank(100, new long[] { 100, 60, 50 }), Is.EqualTo(1));

            Assert.That(GuildBossFormulas.CalculateCurrentHp(1000, 300), Is.EqualTo(700L));
            Assert.That(GuildBossFormulas.CalculateCurrentHp(1000, 1200), Is.EqualTo(0L));
            Assert.That(GuildBossFormulas.IsDefeated(1000, 1000), Is.True);
            Assert.That(GuildBossFormulas.IsDefeated(1000, 999), Is.False);
        }
    }
}
