using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für MasterTool: GetAllDefinitions(), GetTotalIncomeBonus(),
/// CheckEligibility(), GetValidIds().
/// </summary>
public class MasterToolTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static GameState ErzeugeStateWithWorkshopLevel(int level)
    {
        var state = GameState.CreateNew();
        state.Workshops[0].Level = level;
        return state;
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetAllDefinitions
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAllDefinitions_HatZwölfArtefakte()
    {
        // Ausführung
        var defs = MasterTool.GetAllDefinitions();

        // Prüfung: Laut CLAUDE.md 12 Artefakte
        defs.Should().HaveCount(12);
    }

    [Fact]
    public void GetAllDefinitions_AlleIdsEindeutig()
    {
        // Ausführung
        var defs = MasterTool.GetAllDefinitions();

        // Prüfung: Keine doppelten IDs
        defs.Select(d => d.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetValidIds_EnthältAlleDefinitions()
    {
        // Ausführung
        var defs = MasterTool.GetAllDefinitions();
        var validIds = MasterTool.GetValidIds();

        // Prüfung
        foreach (var def in defs)
            validIds.Should().Contain(def.Id);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetTotalIncomeBonus
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetTotalIncomeBonus_LeereKollektionsiste_IstNull()
    {
        // Ausführung
        var bonus = MasterTool.GetTotalIncomeBonus([]);

        // Prüfung
        bonus.Should().Be(0m);
    }

    [Fact]
    public void GetTotalIncomeBonus_GoldenerHammer_IstZweiProzent()
    {
        // Ausführung
        var bonus = MasterTool.GetTotalIncomeBonus(["mt_golden_hammer"]);

        // Prüfung: mt_golden_hammer = 0.02 (2%)
        bonus.Should().Be(0.02m);
    }

    [Fact]
    public void GetTotalIncomeBonus_AlleTwelveTools_IstSummeAllerBoni()
    {
        // Vorbereitung: Alle 12 IDs sammeln
        var alleIds = MasterTool.GetAllDefinitions().Select(d => d.Id).ToList();

        // Ausführung
        var gesamtBonus = MasterTool.GetTotalIncomeBonus(alleIds);

        // Prüfung: 4x 0.02 + 4x 0.03 + 3x 0.05 + 2x 0.07 + 2x 0.10 + 1x 0.15 = 0.74 (+74%)
        // Summe: 0.08 + 0.12 + 0.15 + 0.14 + 0.20 + 0.15 = 0.84? Nein laut CLAUDE.md +74%
        // Laut Definitionen: 0.02+0.02+0.03+0.03 + 0.05+0.05+0.05 + 0.07+0.07 + 0.10+0.10 + 0.15
        // = 0.10 + 0.15 + 0.14 + 0.20 + 0.15 = 0.74
        gesamtBonus.Should().Be(0.74m);
    }

    [Fact]
    public void GetTotalIncomeBonus_UnbekannteId_WirdIgnoriert()
    {
        // Ausführung
        var bonus = MasterTool.GetTotalIncomeBonus(["mt_nichtvorhanden"]);

        // Prüfung: Unbekannte IDs werden stille ignoriert
        bonus.Should().Be(0m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CheckEligibility
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckEligibility_GoldenerHammer_Level75Erfüllt()
    {
        // Vorbereitung
        var state = ErzeugeStateWithWorkshopLevel(75);

        // Prüfung
        MasterTool.CheckEligibility("mt_golden_hammer", state).Should().BeTrue();
    }

    [Fact]
    public void CheckEligibility_GoldenerHammer_Level74NichtErfüllt()
    {
        // Vorbereitung: Genau eins drunter
        var state = ErzeugeStateWithWorkshopLevel(74);

        // Prüfung: Grenzfall - Level 74 reicht nicht
        MasterTool.CheckEligibility("mt_golden_hammer", state).Should().BeFalse();
    }

    [Fact]
    public void CheckEligibility_TitaniumPliers_150AufträgeErfüllt()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.Statistics.TotalOrdersCompleted = 150;

        // Prüfung
        MasterTool.CheckEligibility("mt_titanium_pliers", state).Should().BeTrue();
    }

    [Fact]
    public void CheckEligibility_TitaniumPliers_149AufträgeNichtErfüllt()
    {
        // Vorbereitung: Grenzfall
        var state = GameState.CreateNew();
        state.Statistics.TotalOrdersCompleted = 149;

        // Prüfung
        MasterTool.CheckEligibility("mt_titanium_pliers", state).Should().BeFalse();
    }

    [Fact]
    public void CheckEligibility_BrassLevel_300MinispieleErfüllt()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.Statistics.TotalMiniGamesPlayed = 300;

        // Prüfung
        MasterTool.CheckEligibility("mt_brass_level", state).Should().BeTrue();
    }

    [Fact]
    public void CheckEligibility_CrystalChisel_ErsteBronzeErfüllt()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.Prestige.BronzeCount = 1;

        // Prüfung
        MasterTool.CheckEligibility("mt_crystal_chisel", state).Should().BeTrue();
    }

    [Fact]
    public void CheckEligibility_CrystalChisel_KeinPrestigeNichtErfüllt()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.Prestige.BronzeCount = 0;

        // Prüfung
        MasterTool.CheckEligibility("mt_crystal_chisel", state).Should().BeFalse();
    }

    [Fact]
    public void CheckEligibility_MasterCrown_ErfordertAlleAnderenTools()
    {
        // Vorbereitung: Alle 11 anderen gesammelt
        var alleIdsOhneKrone = MasterTool.GetAllDefinitions()
            .Where(d => d.Id != "mt_master_crown")
            .Select(d => d.Id)
            .ToList();
        var state = GameState.CreateNew();
        state.CollectedMasterTools = alleIdsOhneKrone;

        // Prüfung
        MasterTool.CheckEligibility("mt_master_crown", state).Should().BeTrue();
    }

    [Fact]
    public void CheckEligibility_MasterCrown_NurEinToolNichtErfüllt()
    {
        // Vorbereitung: Nur 1 Tool gesammelt
        var state = GameState.CreateNew();
        state.CollectedMasterTools = ["mt_golden_hammer"];

        // Prüfung
        MasterTool.CheckEligibility("mt_master_crown", state).Should().BeFalse();
    }

    [Fact]
    public void CheckEligibility_UnbekannteId_IstFalse()
    {
        // Vorbereitung
        var state = GameState.CreateNew();

        // Prüfung: Unbekannte ID → false
        MasterTool.CheckEligibility("mt_gar_nicht_da", state).Should().BeFalse();
    }

    [Fact]
    public void CheckEligibility_RubyBlade_ErfordertSilberPrestige()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.Prestige.SilverCount = 1;

        // Prüfung
        MasterTool.CheckEligibility("mt_ruby_blade", state).Should().BeTrue();
    }
}
