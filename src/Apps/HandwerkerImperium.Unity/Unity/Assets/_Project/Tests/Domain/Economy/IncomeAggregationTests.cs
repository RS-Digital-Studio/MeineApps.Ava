using NUnit.Framework;
using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Tests.Economy
{
    /// <summary>
    /// Verifiziert die Einkommens-Aggregation: multiplikative Bündelung aller permanenten Quellen + Log2-Soft-Cap
    /// auf das Aggregat (linear unter, gedämpft über der Schwelle).
    /// </summary>
    [TestFixture]
    public class IncomeAggregationTests
    {
        [Test]
        public void AggregateMultiplier_MultipliesAllSources()
        {
            // 3 (Prestige) * 1.1 * 1.05 * 1.74 * 1.5 * 1.0 = 9.04365
            decimal m = IncomeAggregation.AggregateMultiplier(3m, 0.1m, 0.05m, 0.74m, 0.5m, 0m);
            Assert.That(m, Is.EqualTo(9.04365m));
        }

        [Test]
        public void AggregateMultiplier_HandlesDegenerateInputs()
        {
            Assert.That(IncomeAggregation.AggregateMultiplier(0m, 0m, 0m, 0m, 0m, 0m), Is.EqualTo(1m), "Prestige<=0 -> 1");
            // negative Boni als 0: 2 * 1 * 1 * 1 * 1 * 1 = 2
            Assert.That(IncomeAggregation.AggregateMultiplier(2m, -0.5m, -1m, -1m, -1m, -1m), Is.EqualTo(2m));
        }

        [Test]
        public void EffectiveIncome_LinearBelowThreshold_DampenedAbove()
        {
            // Aggregat 3 < Schwelle 4 -> linear
            Assert.That(IncomeAggregation.EffectiveIncomePerSecond(100m, 3m, 4m), Is.EqualTo(300m));
            // Aggregat 9.04365 > Schwelle 4 -> gedämpft: zwischen 100*4 und 100*9.04365
            decimal eff = IncomeAggregation.EffectiveIncomePerSecond(100m, 9.04365m, 4m);
            Assert.That(eff, Is.GreaterThan(400m));
            Assert.That(eff, Is.LessThan(904.365m), "Soft-Cap dämpft den Ueberschuss");
        }
    }
}
