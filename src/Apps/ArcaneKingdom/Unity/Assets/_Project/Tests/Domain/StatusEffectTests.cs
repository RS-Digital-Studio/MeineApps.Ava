#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Battle;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer das Status-Effekt-System (Designplan v4 Kap. 3.4 + Skills v4).
    /// </summary>
    [TestFixture]
    public sealed class StatusEffectTests
    {
        [Test]
        public void Sleep_blockt_Aktion()
        {
            var fx = new StatusEffect(StatusEffectType.Sleep, remainingTurns: 2);
            Assert.IsTrue(fx.BlocksAction);
            Assert.IsFalse(fx.BlocksSkills);
            Assert.IsFalse(fx.IsDamageOverTime);
        }

        [Test]
        public void Frozen_und_Stunned_blocken_Aktion()
        {
            Assert.IsTrue(new StatusEffect(StatusEffectType.Frozen, 1).BlocksAction);
            Assert.IsTrue(new StatusEffect(StatusEffectType.Stunned, 1).BlocksAction);
        }

        [Test]
        public void Silence_blockt_nur_Skills()
        {
            var fx = new StatusEffect(StatusEffectType.Silence, 2);
            Assert.IsFalse(fx.BlocksAction);
            Assert.IsTrue(fx.BlocksSkills);
        }

        [Test]
        public void Poisoned_und_Burning_sind_DoT()
        {
            Assert.IsTrue(new StatusEffect(StatusEffectType.Poisoned, 2, magnitude: 100).IsDamageOverTime);
            Assert.IsTrue(new StatusEffect(StatusEffectType.Burning, 2, magnitude: 80).IsDamageOverTime);
            Assert.IsFalse(new StatusEffect(StatusEffectType.Sleep, 2).IsDamageOverTime);
        }

        [Test]
        public void TickDamageOverTime_summiert_alle_DoT()
        {
            var effects = new List<StatusEffect>
            {
                new(StatusEffectType.Poisoned, 2, magnitude: 100),
                new(StatusEffectType.Burning, 2, magnitude: 80),
                new(StatusEffectType.Sleep, 2)   // kein DoT
            };
            Assert.AreEqual(180, StatusEffectHelpers.TickDamageOverTime(effects));
        }

        [Test]
        public void TickAndExpire_reduziert_Dauer_und_entfernt_abgelaufene()
        {
            var effects = new List<StatusEffect>
            {
                new(StatusEffectType.Sleep, remainingTurns: 1),
                new(StatusEffectType.Poisoned, remainingTurns: 3, magnitude: 100)
            };
            StatusEffectHelpers.TickAndExpire(effects);
            Assert.AreEqual(1, effects.Count);   // Sleep abgelaufen
            Assert.AreEqual(StatusEffectType.Poisoned, effects[0].Type);
            Assert.AreEqual(2, effects[0].RemainingTurns);
        }

        [Test]
        public void ApplyOrRefresh_fuegt_neue_Effekte_hinzu()
        {
            var effects = new List<StatusEffect>();
            StatusEffectHelpers.ApplyOrRefresh(effects, new StatusEffect(StatusEffectType.Frozen, 2));
            Assert.AreEqual(1, effects.Count);
        }

        [Test]
        public void ApplyOrRefresh_verlaengert_bestehende_Dauer_wenn_neu_laenger()
        {
            var effects = new List<StatusEffect> { new(StatusEffectType.Frozen, remainingTurns: 2) };
            StatusEffectHelpers.ApplyOrRefresh(effects, new StatusEffect(StatusEffectType.Frozen, remainingTurns: 4));
            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(4, effects[0].RemainingTurns);
        }

        [Test]
        public void ApplyOrRefresh_ignoriert_kuerzere_Dauer()
        {
            var effects = new List<StatusEffect> { new(StatusEffectType.Frozen, remainingTurns: 3) };
            StatusEffectHelpers.ApplyOrRefresh(effects, new StatusEffect(StatusEffectType.Frozen, remainingTurns: 1));
            Assert.AreEqual(3, effects[0].RemainingTurns);   // bleibt
        }

        [Test]
        public void IsBlocked_erkennt_blockende_Effekte()
        {
            var withSleep = new List<StatusEffect> { new(StatusEffectType.Sleep, 2) };
            Assert.IsTrue(StatusEffectHelpers.IsBlocked(withSleep));

            var withSilence = new List<StatusEffect> { new(StatusEffectType.Silence, 2) };
            Assert.IsFalse(StatusEffectHelpers.IsBlocked(withSilence));   // Silence blockt nur Skills

            var withPoison = new List<StatusEffect> { new(StatusEffectType.Poisoned, 2, magnitude: 50) };
            Assert.IsFalse(StatusEffectHelpers.IsBlocked(withPoison));    // DoT blockt nicht
        }

        [Test]
        public void HasEffect_findet_bestimmten_Typ()
        {
            var effects = new List<StatusEffect>
            {
                new(StatusEffectType.Burning, 2, magnitude: 100),
                new(StatusEffectType.Stunned, 1)
            };
            Assert.IsTrue(StatusEffectHelpers.HasEffect(effects, StatusEffectType.Burning));
            Assert.IsTrue(StatusEffectHelpers.HasEffect(effects, StatusEffectType.Stunned));
            Assert.IsFalse(StatusEffectHelpers.HasEffect(effects, StatusEffectType.Frozen));
        }
    }
}
