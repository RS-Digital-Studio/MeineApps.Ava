#nullable enable
using System.Collections.Generic;
using System.Reflection;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.World;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer die Mixed-Element-Welt-Logik (Designplan v4 Kap. 3.5, Brocken M14):
    /// ThemeElements/RecommendedCounterElements mit Single-Fallback + Mixed-/All-Erkennung.
    /// </summary>
    [TestFixture]
    public sealed class WorldDefinitionTests
    {
        private static WorldDefinition NewWorld(
            Element primary,
            List<Element>? themeElements = null,
            Element counter = Element.Feuer,
            List<Element>? counterElements = null)
        {
            var so = ScriptableObject.CreateInstance<WorldDefinition>();
            Set(so, "themeElement", primary);
            if (themeElements != null) Set(so, "themeElements", themeElements);
            Set(so, "recommendedCounterElement", counter);
            if (counterElements != null) Set(so, "recommendedCounterElements", counterElements);
            return so;
        }

        private static void Set(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Feld '{field}' nicht gefunden.");
            f!.SetValue(target, value);
        }

        [Test]
        public void ThemeElements_SingleWorld_FallsBackToPrimary()
        {
            var w = NewWorld(Element.Natur);
            Assert.AreEqual(1, w.ThemeElements.Count);
            Assert.AreEqual(Element.Natur, w.ThemeElements[0]);
            Assert.IsFalse(w.IsMixedElement);
            Assert.IsFalse(w.IsAllElements);
        }

        [Test]
        public void ThemeElements_MixedW8_ReturnsTwoPrimaryFirst()
        {
            var w = NewWorld(Element.Wasser, new List<Element> { Element.Wasser, Element.Dunkel });
            Assert.AreEqual(2, w.ThemeElements.Count);
            Assert.AreEqual(Element.Wasser, w.ThemeElements[0]);
            Assert.IsTrue(w.IsMixedElement);
            Assert.IsFalse(w.IsAllElements);
        }

        [Test]
        public void IsAllElements_AllSix_True()
        {
            var all = new List<Element> { Element.Feuer, Element.Wasser, Element.Natur, Element.Erde, Element.Dunkel, Element.Licht };
            var w = NewWorld(Element.Licht, all);
            Assert.IsTrue(w.IsMixedElement);
            Assert.IsTrue(w.IsAllElements);
        }

        [Test]
        public void RecommendedCounterElements_MixedEmpty_ReturnsEmpty()
        {
            // W9/W10: Mixed-Welt mit leerer Counter-Liste -> "Vielseitig" (leere Liste).
            var all = new List<Element> { Element.Feuer, Element.Wasser, Element.Natur, Element.Erde, Element.Dunkel, Element.Licht };
            var w = NewWorld(Element.Licht, all, Element.Dunkel, new List<Element>());
            Assert.AreEqual(0, w.RecommendedCounterElements.Count);
        }

        [Test]
        public void RecommendedCounterElements_SingleWorld_FallsBackToPrimary()
        {
            var w = NewWorld(Element.Natur, counter: Element.Feuer);
            Assert.AreEqual(1, w.RecommendedCounterElements.Count);
            Assert.AreEqual(Element.Feuer, w.RecommendedCounterElements[0]);
        }

        [Test]
        public void RecommendedCounterElements_W8_ReturnsNaturLicht()
        {
            var w = NewWorld(
                Element.Wasser,
                new List<Element> { Element.Wasser, Element.Dunkel },
                Element.Natur,
                new List<Element> { Element.Natur, Element.Licht });
            Assert.AreEqual(2, w.RecommendedCounterElements.Count);
            Assert.AreEqual(Element.Natur, w.RecommendedCounterElements[0]);
            Assert.AreEqual(Element.Licht, w.RecommendedCounterElements[1]);
        }
    }
}
