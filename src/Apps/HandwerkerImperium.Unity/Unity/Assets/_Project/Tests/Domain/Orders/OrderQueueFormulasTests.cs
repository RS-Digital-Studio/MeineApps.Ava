using NUnit.Framework;
using HandwerkerImperium.Domain.Orders;

namespace HandwerkerImperium.Domain.Tests.Orders
{
    /// <summary>
    /// Verifiziert die Kunden-Queue (Zustrom/Bedienen, Queue-Cap) und den Eil-Auftrag
    /// (Start/Aktiv/Ablauf/Ad-Verlängerung/Belohnungs-Multiplikator).
    /// </summary>
    [TestFixture]
    public class OrderQueueFormulasTests
    {
        private const long TicksPerSecond = 10_000_000L;

        [Test]
        public void Tick_SpawnsCustomers_UpToMax()
        {
            var s = new OrderQueueState();
            // Intervall 2s, dt 5s -> floor(5/2)=2 Kunden
            Assert.That(OrderQueueFormulas.Tick(s, 5.0, 2.0, 10), Is.EqualTo(2));
            Assert.That(s.PendingCustomers, Is.EqualTo(2));
            // Queue-Cap 3: weiterer grosser dt fuellt nur bis 3
            OrderQueueFormulas.Tick(s, 100.0, 2.0, 3);
            Assert.That(s.PendingCustomers, Is.EqualTo(3));
        }

        [Test]
        public void Serve_ReducesQueue_AndCountsTotal()
        {
            var s = new OrderQueueState { PendingCustomers = 5 };
            Assert.That(OrderQueueFormulas.Serve(s, 3), Is.EqualTo(3));
            Assert.That(s.PendingCustomers, Is.EqualTo(2));
            Assert.That(s.TotalServed, Is.EqualTo(3));
            // mehr anfordern als vorhanden -> nur vorhandene
            Assert.That(OrderQueueFormulas.Serve(s, 10), Is.EqualTo(2));
            Assert.That(s.PendingCustomers, Is.EqualTo(0));
            Assert.That(s.TotalServed, Is.EqualTo(5));
            Assert.That(OrderQueueFormulas.Serve(s, 1), Is.EqualTo(0)); // leere Queue
        }

        [Test]
        public void Rush_Start_Active_Expire()
        {
            var s = new OrderQueueState();
            long now = 1_000_000_000_000L;
            OrderQueueFormulas.StartRush(s, 3m, 60, now);
            Assert.That(OrderQueueFormulas.IsRushActive(s, now), Is.True);
            Assert.That(OrderQueueFormulas.CurrentRewardMultiplier(s, now), Is.EqualTo(3m));

            long afterExpiry = now + 61L * TicksPerSecond;
            Assert.That(OrderQueueFormulas.IsRushActive(s, afterExpiry), Is.False);
            Assert.That(OrderQueueFormulas.ExpireRushIfDue(s, afterExpiry), Is.True);
            Assert.That(s.Rush.Active, Is.False);
            Assert.That(OrderQueueFormulas.CurrentRewardMultiplier(s, afterExpiry), Is.EqualTo(1m));
        }

        [Test]
        public void Rush_ExtendByAd_KeepsActive()
        {
            var s = new OrderQueueState();
            long now = 1_000_000_000_000L;
            OrderQueueFormulas.StartRush(s, 2m, 30, now);
            OrderQueueFormulas.ExtendRush(s, 30); // +30s
            long at45 = now + 45L * TicksPerSecond;
            Assert.That(OrderQueueFormulas.IsRushActive(s, at45), Is.True, "nach Verlaengerung bei 45s noch aktiv");
        }

        [Test]
        public void Rush_RewardMultiplier_FloorOne()
        {
            var s = new OrderQueueState();
            OrderQueueFormulas.StartRush(s, 0.5m, 10, 0); // < 1 wird auf 1 geklemmt
            Assert.That(s.Rush.RewardMultiplier, Is.EqualTo(1m));
        }
    }
}
