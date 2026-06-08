using System.Collections.Generic;
using NUnit.Framework;
using HandwerkerImperium.Domain.Cosmetics;

namespace HandwerkerImperium.Domain.Tests.Cosmetics
{
    /// <summary>
    /// Verifiziert die Cosmetics-Logik: Besitz, Erschwinglichkeit und die Kauf-Auswertung
    /// (Erfolg / bereits besessen / zu wenig Guthaben / ungültig).
    /// </summary>
    [TestFixture]
    public class CosmeticFormulasTests
    {
        [Test]
        public void IsOwned_And_CanAfford()
        {
            var owned = new List<string> { "skin_a" };
            Assert.That(CosmeticFormulas.IsOwned(owned, "skin_a"), Is.True);
            Assert.That(CosmeticFormulas.IsOwned(owned, "skin_b"), Is.False);
            Assert.That(CosmeticFormulas.IsOwned(null, "skin_a"), Is.False);
            Assert.That(CosmeticFormulas.CanAfford(100m, 80m), Is.True);
            Assert.That(CosmeticFormulas.CanAfford(50m, 80m), Is.False);
        }

        [Test]
        public void EvaluatePurchase_AllOutcomes()
        {
            var owned = new List<string> { "skin_owned" };
            var def = new CosmeticDefinition("skin_new", CosmeticKind.AvatarSkin, CosmeticCurrency.Gems, 80m);

            Assert.That(CosmeticFormulas.EvaluatePurchase(def, 100m, owned), Is.EqualTo(CosmeticPurchaseResult.Success));
            Assert.That(CosmeticFormulas.EvaluatePurchase(def, 50m, owned), Is.EqualTo(CosmeticPurchaseResult.NotEnoughCurrency));

            var ownedDef = new CosmeticDefinition("skin_owned", CosmeticKind.AvatarSkin, CosmeticCurrency.Gems, 80m);
            Assert.That(CosmeticFormulas.EvaluatePurchase(ownedDef, 100m, owned), Is.EqualTo(CosmeticPurchaseResult.AlreadyOwned));

            Assert.That(CosmeticFormulas.EvaluatePurchase(null, 100m, owned), Is.EqualTo(CosmeticPurchaseResult.Invalid));
        }
    }
}
