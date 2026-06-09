using NUnit.Framework;
using UnityEngine;
using HandwerkerImperium.Game;

namespace HandwerkerImperium.Game.Tests
{
    /// <summary>
    /// Verifiziert, dass das GameBalancingConfig-ScriptableObject seine spec-gegründeten Default-Werte korrekt
    /// auf das Domain-GameBalancing/IdleBalancing abbildet (float→decimal-Casts, kein Wert-Drift).
    /// </summary>
    public sealed class GameBalancingConfigTests
    {
        [Test]
        public void ToGameBalancing_MapsSpecDefaults()
        {
            var so = ScriptableObject.CreateInstance<GameBalancingConfig>();
            try
            {
                var b = so.ToGameBalancing();
                Assert.That(b.Prestige.StageMultipliers, Is.EqualTo(new[] { 3m, 4m, 5m }).AsCollection);
                Assert.That(b.Prestige.MaxPrestige, Is.EqualTo(3));
                Assert.That(b.Meistergrad.Growth, Is.EqualTo(1.5).Within(1e-4));
                Assert.That(b.Mastery.Growth, Is.EqualTo(1.15).Within(1e-3));
                Assert.That(b.Daily.LadderLength, Is.EqualTo(30));
                Assert.That(b.Referral.TierRewards, Is.EqualTo(new[] { 50, 200, 500 }).AsCollection);
                Assert.That(b.Star.Thresholds.Count, Is.EqualTo(4));
                Assert.That(b.Monetization.MigrationBonusGems, Is.EqualTo(100));
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void ToIdleBalancing_DefaultWhenNoIdleRef()
        {
            var so = ScriptableObject.CreateInstance<GameBalancingConfig>();
            try
            {
                var idle = so.ToIdleBalancing(); // kein Idle-Config gesetzt -> Default
                Assert.That(idle.Stations.Count, Is.EqualTo(10), "alle 10 Gewerke (GDD §6.1)");
                Assert.That(idle.Stations[0].UnlockedAtStart, Is.True, "Start nur Schreinerei");
                Assert.That(idle.Stations[1].UnlockCost, Is.EqualTo(500m), "Plot-Kosten-Progression beginnt bei 500");
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }
    }
}
