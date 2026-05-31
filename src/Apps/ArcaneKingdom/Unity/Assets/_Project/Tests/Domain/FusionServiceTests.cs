#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer FusionService (Designplan v4 Kap. 5).
    /// </summary>
    [TestFixture]
    public sealed class FusionServiceTests
    {
        private static CardDefinition MakeDef(string id, Race race, Rarity rarity, bool isPremium = false)
        {
            var so = ScriptableObject.CreateInstance<CardDefinition>();
            using var sObj = new UnityEditor.SerializedObject(so);
            sObj.FindProperty("id").stringValue = id;
            sObj.FindProperty("race").enumValueIndex = (int)race;
            sObj.FindProperty("rarity").enumValueIndex = (int)rarity;
            sObj.FindProperty("isPremiumCard").boolValue = isPremium;
            sObj.ApplyModifiedPropertiesWithoutUndo();
            return so;
        }

        private static CardInstance MakeInstance(string instId, string defId)
            => new CardInstance(instId, defId, level: 0, expWithinLevel: 0, obtainedAtUtc: DateTime.UtcNow);

        // --------------------------------------------------------------------
        // Validation
        // --------------------------------------------------------------------

        [Test]
        public void Premium_Karten_koennen_nicht_fusioniert_werden()
        {
            var def = MakeDef("p_card", Race.Ritter, Rarity.Selten, isPremium: true);
            var defs = new Dictionary<string, CardDefinition> { ["p_card"] = def };
            var svc = new FusionService(defs, Array.Empty<FusionRecipe>(), seed: 1);
            var inst = MakeInstance("i1", "p_card");
            var result = svc.CanUseInFusion(inst, new FusionGuard());
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("Premium", result.ErrorMessage);
        }

        [Test]
        public void Favoriten_sind_geschuetzt()
        {
            var def = MakeDef("normal", Race.Elfen, Rarity.Gewoehnlich);
            var defs = new Dictionary<string, CardDefinition> { ["normal"] = def };
            var svc = new FusionService(defs, Array.Empty<FusionRecipe>(), seed: 1);
            var inst = MakeInstance("i1", "normal");
            var guard = new FusionGuard(favoritedInstanceIds: new[] { "i1" });
            var result = svc.CanUseInFusion(inst, guard);
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("Favorit", result.ErrorMessage);
        }

        [Test]
        public void Karten_in_aktivem_Deck_sind_geschuetzt()
        {
            var def = MakeDef("c1", Race.Tiergeister, Rarity.Selten);
            var defs = new Dictionary<string, CardDefinition> { ["c1"] = def };
            var svc = new FusionService(defs, Array.Empty<FusionRecipe>(), seed: 1);
            var inst = MakeInstance("i1", "c1");
            var guard = new FusionGuard(deckedInstanceIds: new[] { "i1" });
            var result = svc.CanUseInFusion(inst, guard);
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("Deck", result.ErrorMessage);
        }

        // --------------------------------------------------------------------
        // Letzte-Kopie
        // --------------------------------------------------------------------

        [Test]
        public void Letzte_Kopie_wird_erkannt()
        {
            var def = MakeDef("rare", Race.Ritter, Rarity.Selten);
            var defs = new Dictionary<string, CardDefinition> { ["rare"] = def };
            var svc = new FusionService(defs, Array.Empty<FusionRecipe>(), seed: 1);
            var inv = new[] { MakeInstance("i1", "rare") };
            Assert.IsTrue(svc.IsLastCopy("rare", inv));
            var inv2 = new[] { MakeInstance("i1", "rare"), MakeInstance("i2", "rare") };
            Assert.IsFalse(svc.IsLastCopy("rare", inv2));
        }

        // --------------------------------------------------------------------
        // Kategorie-Fusion
        // --------------------------------------------------------------------

        [Test]
        public void Kategorie_Fusion_braucht_gleiche_Rasse_und_Rarity()
        {
            var d1 = MakeDef("c1", Race.Ritter, Rarity.Gewoehnlich);
            var d2 = MakeDef("c2", Race.Elfen, Rarity.Gewoehnlich);
            var d3 = MakeDef("c3", Race.Ritter, Rarity.Gewoehnlich);
            var defs = new Dictionary<string, CardDefinition> { ["c1"] = d1, ["c2"] = d2, ["c3"] = d3 };
            var svc = new FusionService(defs, Array.Empty<FusionRecipe>(), seed: 1);
            var preview = svc.PreviewCategoryFusion(
                new[] { MakeInstance("i1", "c1"), MakeInstance("i2", "c2"), MakeInstance("i3", "c3") },
                new FusionGuard());
            Assert.IsFalse(preview.IsSuccess);
            StringAssert.Contains("Rasse", preview.ErrorMessage);
        }

        [Test]
        public void Kategorie_Fusion_1Star_braucht_3_Karten()
        {
            var c1 = MakeDef("c1", Race.Ritter, Rarity.Gewoehnlich);
            var u2 = MakeDef("u_a", Race.Ritter, Rarity.Ungewoehnlich);
            var defs = new Dictionary<string, CardDefinition> { ["c1"] = c1, ["u_a"] = u2 };
            var svc = new FusionService(defs, Array.Empty<FusionRecipe>(), seed: 1);

            // Nur 2 Karten = Fehler
            var p1 = svc.PreviewCategoryFusion(new[] { MakeInstance("i1", "c1"), MakeInstance("i2", "c1") }, new FusionGuard());
            Assert.IsFalse(p1.IsSuccess);
            StringAssert.Contains("3", p1.ErrorMessage);

            // 3 Karten = OK
            var p2 = svc.PreviewCategoryFusion(new[] { MakeInstance("i1", "c1"), MakeInstance("i2", "c1"), MakeInstance("i3", "c1") }, new FusionGuard());
            Assert.IsTrue(p2.IsSuccess);
            Assert.AreEqual(1_000, p2.GoldCost);
            CollectionAssert.Contains(p2.ResultPool, "u_a");
        }

        [Test]
        public void Kategorie_Fusion_Mythisch_mit_3_verschiedenen_5star_erlaubt()
        {
            // K10: 5* -> 6* via Kategorie-Fusion (3 paarweise verschiedene Legendaer, Rasse egal)
            // -> zufaellige Nicht-Goetter-Mythic + Mythischer Kern + 5 Mio Gold (Designplan v4 Kap. 5.1).
            var l1 = MakeDef("l1", Race.Ritter, Rarity.Legendaer);
            var l2 = MakeDef("l2", Race.Elfen, Rarity.Legendaer);
            var l3 = MakeDef("l3", Race.Tiergeister, Rarity.Legendaer);
            var myth = MakeDef("myth_ritter", Race.Ritter, Rarity.Mythisch);
            var defs = new Dictionary<string, CardDefinition>
            { ["l1"] = l1, ["l2"] = l2, ["l3"] = l3, ["myth_ritter"] = myth };
            var svc = new FusionService(defs, Array.Empty<FusionRecipe>(), seed: 1);
            var p = svc.PreviewCategoryFusion(new[] {
                MakeInstance("i1","l1"), MakeInstance("i2","l2"), MakeInstance("i3","l3")
            }, new FusionGuard());
            Assert.IsTrue(p.IsSuccess, p.ErrorMessage);
            Assert.AreEqual(5_000_000, p.GoldCost);
            Assert.AreEqual("mythischer_kern", p.RequiredMaterialId);
            CollectionAssert.Contains(p.ResultPool, "myth_ritter");
        }

        [Test]
        public void Mythisch_Pool_schliesst_Goetter_aus()
        {
            var l1 = MakeDef("l1", Race.Ritter, Rarity.Legendaer);
            var l2 = MakeDef("l2", Race.Elfen, Rarity.Legendaer);
            var l3 = MakeDef("l3", Race.Tiergeister, Rarity.Legendaer);
            var mRitter = MakeDef("m_ritter", Race.Ritter, Rarity.Mythisch);
            var mGott = MakeDef("m_gott", Race.Goetter, Rarity.Mythisch);
            var defs = new Dictionary<string, CardDefinition>
            { ["l1"] = l1, ["l2"] = l2, ["l3"] = l3, ["m_ritter"] = mRitter, ["m_gott"] = mGott };
            var svc = new FusionService(defs, Array.Empty<FusionRecipe>(), seed: 1);
            var p = svc.PreviewCategoryFusion(new[] {
                MakeInstance("i1","l1"), MakeInstance("i2","l2"), MakeInstance("i3","l3")
            }, new FusionGuard());
            Assert.IsTrue(p.IsSuccess, p.ErrorMessage);
            CollectionAssert.Contains(p.ResultPool, "m_ritter");
            CollectionAssert.DoesNotContain(p.ResultPool, "m_gott");
        }

        [Test]
        public void Mythisch_braucht_3_verschiedene_Karten()
        {
            var leg = MakeDef("leg", Race.Ritter, Rarity.Legendaer);
            var myth = MakeDef("myth", Race.Ritter, Rarity.Mythisch);
            var defs = new Dictionary<string, CardDefinition> { ["leg"] = leg, ["myth"] = myth };
            var svc = new FusionService(defs, Array.Empty<FusionRecipe>(), seed: 1);
            var p = svc.PreviewCategoryFusion(new[] {
                MakeInstance("i1","leg"), MakeInstance("i2","leg"), MakeInstance("i3","leg")
            }, new FusionGuard());
            Assert.IsFalse(p.IsSuccess);
            StringAssert.Contains("verschieden", p.ErrorMessage);
        }

        [Test]
        public void Mythisch_braucht_gleiche_Rarity()
        {
            var l1 = MakeDef("l1", Race.Ritter, Rarity.Legendaer);
            var l2 = MakeDef("l2", Race.Elfen, Rarity.Legendaer);
            var epic = MakeDef("e1", Race.Tiergeister, Rarity.Epic);
            var myth = MakeDef("myth", Race.Ritter, Rarity.Mythisch);
            var defs = new Dictionary<string, CardDefinition>
            { ["l1"] = l1, ["l2"] = l2, ["e1"] = epic, ["myth"] = myth };
            var svc = new FusionService(defs, Array.Empty<FusionRecipe>(), seed: 1);
            var p = svc.PreviewCategoryFusion(new[] {
                MakeInstance("i1","l1"), MakeInstance("i2","l2"), MakeInstance("i3","e1")
            }, new FusionGuard());
            Assert.IsFalse(p.IsSuccess);
            StringAssert.Contains("Seltenheit", p.ErrorMessage);
        }

        // --------------------------------------------------------------------
        // Feste Rezepte
        // --------------------------------------------------------------------

        [Test]
        public void Festes_Rezept_wird_gematcht()
        {
            var dA = MakeDef("input_a", Race.Elfen, Rarity.Gewoehnlich);
            var dB = MakeDef("input_b", Race.Elfen, Rarity.Gewoehnlich);
            var dResult = MakeDef("result", Race.Elfen, Rarity.Selten);
            var defs = new Dictionary<string, CardDefinition> { ["input_a"] = dA, ["input_b"] = dB, ["result"] = dResult };
            var recipe = new FusionRecipe(
                id: "r1", resultCardId: "result",
                requiredCardIds: new[] { "input_a", "input_b" },
                requiredMaterialIds: Array.Empty<string>(),
                goldCost: 2000,
                hintLocalizationKey: "rezept.hint");
            var svc = new FusionService(defs, new[] { recipe }, seed: 1);

            var inputs = new[] { MakeInstance("i1", "input_a"), MakeInstance("i2", "input_b") };
            var found = svc.FindMatchingRecipe(inputs);
            Assert.IsNotNull(found);
            Assert.AreEqual("r1", found!.Id);
            Assert.AreEqual("result", found.ResultCardId);
        }

        [Test]
        public void Festes_Rezept_findet_keinen_Match_bei_falschen_Karten()
        {
            var dA = MakeDef("input_a", Race.Elfen, Rarity.Gewoehnlich);
            var dWrong = MakeDef("falsche", Race.Daemonen, Rarity.Gewoehnlich);
            var dResult = MakeDef("result", Race.Elfen, Rarity.Selten);
            var defs = new Dictionary<string, CardDefinition> { ["input_a"] = dA, ["falsche"] = dWrong, ["result"] = dResult };
            var recipe = new FusionRecipe("r1", "result",
                new[] { "input_a", "input_b" }, Array.Empty<string>(), 2000, "k");
            var svc = new FusionService(defs, new[] { recipe }, seed: 1);

            var found = svc.FindMatchingRecipe(new[] { MakeInstance("i1", "input_a"), MakeInstance("i2", "falsche") });
            Assert.IsNull(found);
        }
    }
}
