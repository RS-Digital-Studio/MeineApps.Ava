using System.Collections.Generic;
using NUnit.Framework;
using HandwerkerImperium.Domain.Progression;

namespace HandwerkerImperium.Domain.Tests.Progression
{
    /// <summary>
    /// Verifiziert die Meisterwerkzeuge: Katalog-Integrität (12 Tools, Summe +74 %), Eligibility je
    /// Bedingungs-Typ und den aggregierten Income-Bonus gesammelter Tools.
    /// </summary>
    [TestFixture]
    public class MasterToolFormulasTests
    {
        [Test]
        public void DefaultCatalog_Has12Tools_TotalBonus74Percent()
        {
            var cat = MasterToolFormulas.DefaultCatalog();
            Assert.That(cat.Count, Is.EqualTo(12));
            var allIds = new List<string>();
            foreach (var d in cat) allIds.Add(d.Id);
            Assert.That(MasterToolFormulas.TotalIncomeBonus(allIds, cat), Is.EqualTo(0.74m));
        }

        [Test]
        public void IsEligible_PerRequirementKind()
        {
            var cat = MasterToolFormulas.DefaultCatalog();
            MasterToolDefinition Tool(string id) => cat.Find(d => d.Id == id);

            Assert.That(MasterToolFormulas.IsEligible(Tool("mt_golden_hammer"), new MasterToolContext { MaxStationLevel = 75 }), Is.True);
            Assert.That(MasterToolFormulas.IsEligible(Tool("mt_golden_hammer"), new MasterToolContext { MaxStationLevel = 74 }), Is.False);
            Assert.That(MasterToolFormulas.IsEligible(Tool("mt_titanium_pliers"), new MasterToolContext { OrdersServed = 150 }), Is.True);
            Assert.That(MasterToolFormulas.IsEligible(Tool("mt_crystal_chisel"), new MasterToolContext { PrestigeCount = 1 }), Is.True);
            Assert.That(MasterToolFormulas.IsEligible(Tool("mt_master_crown"), new MasterToolContext { CollectedTools = 11 }), Is.True);
            Assert.That(MasterToolFormulas.IsEligible(Tool("mt_master_crown"), new MasterToolContext { CollectedTools = 10 }), Is.False);
        }

        [Test]
        public void TotalIncomeBonus_PartialCollection()
        {
            var cat = MasterToolFormulas.DefaultCatalog();
            var ids = new List<string> { "mt_golden_hammer", "mt_master_crown" }; // 0.02 + 0.15
            Assert.That(MasterToolFormulas.TotalIncomeBonus(ids, cat), Is.EqualTo(0.17m));
            Assert.That(MasterToolFormulas.TotalIncomeBonus(new List<string>(), cat), Is.EqualTo(0m));
            Assert.That(MasterToolFormulas.TotalIncomeBonus(null, cat), Is.EqualTo(0m));
        }
    }
}
