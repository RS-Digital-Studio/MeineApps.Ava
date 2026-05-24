#nullable enable
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class ElementMatchupTests
    {
        [Test]
        public void NaturStarkGegenWasser()
        {
            Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Natur, Element.Wasser));
        }

        [Test]
        public void NaturSchwachGegenFeuer()
        {
            Assert.AreEqual(ElementMatchup.WeakMultiplier, ElementMatchup.GetMultiplier(Element.Natur, Element.Feuer));
        }

        [Test]
        public void LichtStarkGegenDunkel()
        {
            Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Licht, Element.Dunkel));
        }

        [Test]
        public void DunkelSchwachGegenLicht()
        {
            Assert.AreEqual(ElementMatchup.WeakMultiplier, ElementMatchup.GetMultiplier(Element.Dunkel, Element.Licht));
        }

        [Test]
        public void LichtNeutralGegenNaturFeuerWasser()
        {
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Licht, Element.Natur));
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Licht, Element.Feuer));
            Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(Element.Licht, Element.Wasser));
        }

        [Test]
        public void GleichesElementNeutral()
        {
            foreach (Element e in System.Enum.GetValues(typeof(Element)))
                Assert.AreEqual(ElementMatchup.NeutralMultiplier, ElementMatchup.GetMultiplier(e, e), $"Element {e} gegen sich selbst sollte neutral sein.");
        }

        [Test]
        public void DreieckIstZyklisch()
        {
            // Natur > Wasser > Feuer > Natur ist ein Zyklus
            Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Natur, Element.Wasser));
            Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Wasser, Element.Feuer));
            Assert.AreEqual(ElementMatchup.StrongMultiplier, ElementMatchup.GetMultiplier(Element.Feuer, Element.Natur));
        }
    }
}
