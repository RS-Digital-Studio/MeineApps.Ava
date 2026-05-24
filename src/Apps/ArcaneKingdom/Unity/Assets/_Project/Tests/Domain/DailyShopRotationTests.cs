#nullable enable
using System;
using ArcaneKingdom.Domain.DailyShop;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class DailyShopRotationTests
    {
        [Test]
        public void RotationHatGenauSechsSlots()
        {
            var slots = DailyShopRotation.RotationForDay(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(DailyShopRotation.SlotsPerDay, slots.Count);
        }

        [Test]
        public void GleicherTagErgibtGleicheRotation()
        {
            var day = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
            var a = DailyShopRotation.RotationForDay(day);
            var b = DailyShopRotation.RotationForDay(day);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].SubType, b[i].SubType);
                Assert.AreEqual(a[i].PriceAmount, b[i].PriceAmount);
            }
        }

        [Test]
        public void AndererTagErgibtAndereRotation()
        {
            var a = DailyShopRotation.RotationForDay(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
            var b = DailyShopRotation.RotationForDay(new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
            var different = false;
            for (var i = 0; i < a.Count; i++)
            {
                if (a[i].SubType != b[i].SubType || a[i].PriceAmount != b[i].PriceAmount) { different = true; break; }
            }
            Assert.IsTrue(different, "Verschiedene UTC-Tage sollten unterschiedliche Rotationen erzeugen.");
        }

        [Test]
        public void GenauEinSlotProTagIstRabatiert()
        {
            var slots = DailyShopRotation.RotationForDay(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
            var discounted = 0;
            foreach (var s in slots) if (s.DiscountedFromDaily) discounted++;
            Assert.AreEqual(1, discounted);
        }
    }
}
