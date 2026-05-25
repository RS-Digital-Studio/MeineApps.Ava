#nullable enable
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer das Doppel-Dreieck-System (Designplan v4 Kap. 3).
    ///   Physisches Dreieck:  Feuer → Natur → Wasser → Feuer
    ///   Magisches Dreieck:   Licht → Dunkel → Erde → Licht
    /// Karten verschiedener Dreiecke sind gegeneinander neutral.
    /// </summary>
    [TestFixture]
    public sealed class ElementMatchupTests
    {
        // ===========================================================================
        // Physisches Dreieck
        // ===========================================================================

        [Test]
        public void FeuerStarkGegenNatur()
            => Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Feuer, Element.Natur));

        [Test]
        public void NaturStarkGegenWasser()
            => Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Natur, Element.Wasser));

        [Test]
        public void WasserStarkGegenFeuer()
            => Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Wasser, Element.Feuer));

        [Test]
        public void NaturSchwachGegenFeuer()
            => Assert.AreEqual(ElementMatchup.WeakMultiplier, ElementMatchup.GetMultiplier(Element.Natur, Element.Feuer));

        [Test]
        public void WasserSchwachGegenNatur()
            => Assert.AreEqual(ElementMatchup.WeakMultiplier, ElementMatchup.GetMultiplier(Element.Wasser, Element.Natur));

        [Test]
        public void FeuerSchwachGegenWasser()
            => Assert.AreEqual(ElementMatchup.WeakMultiplier, ElementMatchup.GetMultiplier(Element.Feuer, Element.Wasser));

        // ===========================================================================
        // Magisches Dreieck (NEU in v4: Erde dazu, Licht/Dunkel/Erde als geschlossenes Dreieck)
        // ===========================================================================

        [Test]
        public void LichtStarkGegenDunkel()
            => Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Licht, Element.Dunkel));

        [Test]
        public void DunkelStarkGegenErde()
            => Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Dunkel, Element.Erde));

        [Test]
        public void ErdeStarkGegenLicht()
            => Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Erde, Element.Licht));

        [Test]
        public void DunkelSchwachGegenLicht()
            => Assert.AreEqual(ElementMatchup.WeakMultiplier, ElementMatchup.GetMultiplier(Element.Dunkel, Element.Licht));

        [Test]
        public void ErdeSchwachGegenDunkel()
            => Assert.AreEqual(ElementMatchup.WeakMultiplier, ElementMatchup.GetMultiplier(Element.Erde, Element.Dunkel));

        [Test]
        public void LichtSchwachGegenErde()
            => Assert.AreEqual(ElementMatchup.WeakMultiplier, ElementMatchup.GetMultiplier(Element.Licht, Element.Erde));

        // ===========================================================================
        // Cross-Dreieck: alles neutral
        // ===========================================================================

        [Test]
        public void LichtNeutralGegenPhysisch()
        {
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Licht, Element.Natur));
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Licht, Element.Feuer));
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Licht, Element.Wasser));
        }

        [Test]
        public void DunkelNeutralGegenPhysisch()
        {
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Dunkel, Element.Natur));
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Dunkel, Element.Feuer));
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Dunkel, Element.Wasser));
        }

        [Test]
        public void ErdeNeutralGegenPhysisch()
        {
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Erde, Element.Natur));
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Erde, Element.Feuer));
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Erde, Element.Wasser));
        }

        [Test]
        public void FeuerNeutralGegenMagisch()
        {
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Feuer, Element.Licht));
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Feuer, Element.Dunkel));
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Feuer, Element.Erde));
        }

        // ===========================================================================
        // Selbst- und Hilfs-Tests
        // ===========================================================================

        [Test]
        public void GleichesElementNeutral()
        {
            foreach (Element e in System.Enum.GetValues(typeof(Element)))
                Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(e, e), $"Element {e} gegen sich selbst sollte neutral sein.");
        }

        [Test]
        public void IsPhysicalErkenntDreieck()
        {
            Assert.IsTrue(ElementMatchup.IsPhysical(Element.Feuer));
            Assert.IsTrue(ElementMatchup.IsPhysical(Element.Wasser));
            Assert.IsTrue(ElementMatchup.IsPhysical(Element.Natur));
            Assert.IsFalse(ElementMatchup.IsPhysical(Element.Licht));
            Assert.IsFalse(ElementMatchup.IsPhysical(Element.Dunkel));
            Assert.IsFalse(ElementMatchup.IsPhysical(Element.Erde));
        }

        [Test]
        public void IsMagicalErkenntDreieck()
        {
            Assert.IsTrue(ElementMatchup.IsMagical(Element.Licht));
            Assert.IsTrue(ElementMatchup.IsMagical(Element.Dunkel));
            Assert.IsTrue(ElementMatchup.IsMagical(Element.Erde));
            Assert.IsFalse(ElementMatchup.IsMagical(Element.Feuer));
            Assert.IsFalse(ElementMatchup.IsMagical(Element.Wasser));
            Assert.IsFalse(ElementMatchup.IsMagical(Element.Natur));
        }

        // ===========================================================================
        // Magnituden-Spec
        // ===========================================================================

        [Test]
        public void MultipliersStimmenMitDesignplanV4()
        {
            // Designplan v4 Kap. 3.3: +10% Schaden bei "stark", -10% bei "schwach"
            Assert.AreEqual(1.10f, ElementMatchup.StrongMultiplier, 0.001f);
            Assert.AreEqual(0.90f, ElementMatchup.WeakMultiplier, 0.001f);
            Assert.AreEqual(1.00f, ElementMatchup.NeutralMultiplier, 0.001f);
        }
    }
}
