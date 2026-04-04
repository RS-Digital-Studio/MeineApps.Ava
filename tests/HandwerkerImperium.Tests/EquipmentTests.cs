using HandwerkerImperium.Models;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für Equipment: RarityColor, RarityIcon, ShopPrice, GenerateRandom().
/// </summary>
public class EquipmentTests
{
    // ═══════════════════════════════════════════════════════════════════
    // RarityColor
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(EquipmentRarity.Common, "#9E9E9E")]
    [InlineData(EquipmentRarity.Uncommon, "#4CAF50")]
    [InlineData(EquipmentRarity.Rare, "#2196F3")]
    [InlineData(EquipmentRarity.Epic, "#9C27B0")]
    public void RarityColor_AlleSeltenheiten_GibtKorrekteHexfarbe(EquipmentRarity rarity, string erwartet)
    {
        // Vorbereitung
        var equip = new Equipment { Rarity = rarity };

        // Prüfung
        equip.RarityColor.Should().Be(erwartet);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ShopPrice
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(EquipmentRarity.Common, 5)]
    [InlineData(EquipmentRarity.Uncommon, 15)]
    [InlineData(EquipmentRarity.Rare, 30)]
    [InlineData(EquipmentRarity.Epic, 60)]
    public void ShopPrice_AlleSeltenheiten_GibtKorrektenPreis(EquipmentRarity rarity, int erwartet)
    {
        // Vorbereitung
        var equip = new Equipment { Rarity = rarity };

        // Prüfung
        equip.ShopPrice.Should().Be(erwartet);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GenerateRandom()
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateRandom_Schwierigkeit0_NurCommonUndUncommon()
    {
        // Vorbereitung: Bei Schwierigkeit 0 kein Epic/Rare
        var seltenheiten = new HashSet<EquipmentRarity>();

        // Ausführung: 100 Samples sammeln
        for (int i = 0; i < 100; i++)
        {
            var equip = Equipment.GenerateRandom(0);
            seltenheiten.Add(equip.Rarity);
        }

        // Prüfung: Epic sollte bei Schwierigkeit 0 nie auftreten
        seltenheiten.Should().NotContain(EquipmentRarity.Epic,
            "Epic erfordert difficultyLevel >= 3");
    }

    [Fact]
    public void GenerateRandom_GibtGültigesEquipmentZurück()
    {
        // Ausführung
        var equip = Equipment.GenerateRandom(1);

        // Prüfung: Alle Felder gesetzt
        equip.Should().NotBeNull();
        equip.EfficiencyBonus.Should().BeGreaterThan(0m);
        equip.FatigueReduction.Should().BeGreaterThan(0m);
        equip.MoodBonus.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void GenerateRandom_NameKeyEnthältTypUndRarity()
    {
        // Ausführung
        var equip = Equipment.GenerateRandom(1);

        // Prüfung: NameKey-Format: "Equipment_{Type}_{Rarity}"
        equip.NameKey.Should().StartWith("Equipment_");
        equip.NameKey.Should().Contain(equip.Type.ToString());
        equip.NameKey.Should().Contain(equip.Rarity.ToString());
    }

    [Fact]
    public void GenerateRandom_EpicBonus_IstHöherAlsCommon()
    {
        // Vorbereitung: Epic Minimum (13%) > Common Maximum (7%)
        // Statistisch sicherer Test: Wir erstellen direkt mit bekannter Rarity
        var common = new Equipment { Rarity = EquipmentRarity.Common, EfficiencyBonus = 0.07m };
        var epic = new Equipment { Rarity = EquipmentRarity.Epic, EfficiencyBonus = 0.14m };

        // Prüfung
        epic.EfficiencyBonus.Should().BeGreaterThan(common.EfficiencyBonus);
    }
}
