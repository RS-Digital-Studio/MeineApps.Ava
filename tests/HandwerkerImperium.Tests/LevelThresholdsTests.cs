using HandwerkerImperium.Models;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für LevelThresholds: Alle Feature-Unlock-Level, Automatisierungs-Schwellen,
/// Tab-Freischaltungen und Reputations-Schwellenwerte.
/// Diese Tests sichern die Spielbalance ab - eine Änderung hier fällt sofort auf.
/// </summary>
public class LevelThresholdsTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Progressive Disclosure - UI-Sichtbarkeit
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void BannerStrip_IstLevel3()
    {
        // Prüfung: BannerStrip (Events/Boosts) ab Level 3 sichtbar
        LevelThresholds.BannerStrip.Should().Be(3);
    }

    [Fact]
    public void QuickJobs_IstLevel5()
    {
        // Prüfung: QuickJobs-Tab ab Level 5
        LevelThresholds.QuickJobs.Should().Be(5);
    }

    [Fact]
    public void CraftingResearch_IstLevel8()
    {
        // Prüfung: Crafting + Forschung ab Level 8
        LevelThresholds.CraftingResearch.Should().Be(8);
    }

    [Fact]
    public void ManagerSection_IstLevel10()
    {
        // Prüfung: Vorarbeiter ab Level 10
        LevelThresholds.ManagerSection.Should().Be(10);
    }

    [Fact]
    public void MasterToolsSection_IstLevel20()
    {
        // Prüfung: Meisterwerkzeuge ab Level 20
        LevelThresholds.MasterToolsSection.Should().Be(20);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Automatisierung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AutoCollect_IstLevel15()
    {
        // Prüfung: Auto-Collect ab Level 15 (laut CLAUDE.md)
        LevelThresholds.AutoCollect.Should().Be(15);
    }

    [Fact]
    public void AutoAccept_IstLevel25()
    {
        // Prüfung: Auto-Accept ab Level 25 (laut CLAUDE.md)
        LevelThresholds.AutoAccept.Should().Be(25);
    }

    [Fact]
    public void AutoAssign_IstLevel50()
    {
        // Prüfung: Auto-Assign ab Level 50 (laut CLAUDE.md)
        LevelThresholds.AutoAssign.Should().Be(50);
    }

    [Fact]
    public void AutomatisierungReihenfolge_AutoCollectVorAutoAccept()
    {
        // Prüfung: Sinnvolle Reihenfolge der Freischaltungen
        LevelThresholds.AutoCollect.Should().BeLessThan(LevelThresholds.AutoAccept);
        LevelThresholds.AutoAccept.Should().BeLessThan(LevelThresholds.AutoAssign);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Tab-Freischaltungen
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TabUnlockLevels_HatFuenfEintraege()
    {
        // Prüfung: 5 Tabs (Werkstatt, Imperium, Missionen, Gilde, Shop)
        LevelThresholds.TabUnlockLevels.Should().HaveCount(5);
    }

    [Fact]
    public void TabUnlockLevels_WerkstattLevel1()
    {
        // Prüfung: Tab 0 (Werkstatt) ist ab Level 1 verfügbar
        LevelThresholds.TabUnlockLevels[0].Should().Be(1);
    }

    [Fact]
    public void TabUnlockLevels_ImperiumLevel5()
    {
        // Prüfung: Tab 1 (Imperium) ab Level 5
        LevelThresholds.TabUnlockLevels[1].Should().Be(5);
    }

    [Fact]
    public void TabUnlockLevels_MissionenLevel8()
    {
        // Prüfung: Tab 2 (Missionen) ab Level 8
        LevelThresholds.TabUnlockLevels[2].Should().Be(8);
    }

    [Fact]
    public void TabUnlockLevels_GildeLevel15()
    {
        // Prüfung: Tab 3 (Gilde) ab Level 15
        LevelThresholds.TabUnlockLevels[3].Should().Be(15);
    }

    [Fact]
    public void TabUnlockLevels_ShopLevel3()
    {
        // Prüfung: Tab 4 (Shop) ab Level 3
        LevelThresholds.TabUnlockLevels[4].Should().Be(3);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Kontextuelle Hints
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void HintLevels_SindKonsistentMitFeatureLevels()
    {
        // Prüfung: Hints erscheinen beim selben Level wie das Feature
        LevelThresholds.HintWorkerUnlock.Should().BeGreaterThan(0);
        LevelThresholds.HintQuickJobs.Should().Be(LevelThresholds.QuickJobs);
        LevelThresholds.HintCrafting.Should().Be(LevelThresholds.CraftingResearch);
        LevelThresholds.HintManagerUnlock.Should().Be(LevelThresholds.ManagerSection);
        LevelThresholds.HintAutomation.Should().Be(LevelThresholds.AutoCollect);
        LevelThresholds.HintMasterTools.Should().Be(LevelThresholds.MasterToolsSection);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Reputations-Schwellenwerte
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ReputationWarningThreshold_IstFuenfzig()
    {
        // Prüfung: Warnung unter 50 Reputation
        LevelThresholds.ReputationWarningThreshold.Should().Be(50);
    }

    [Fact]
    public void ReputationHighlightThreshold_IstAchtzig()
    {
        // Prüfung: Highlight ab 80 Reputation
        LevelThresholds.ReputationHighlightThreshold.Should().Be(80);
    }

    [Fact]
    public void ReputationWarning_IstKleinerAlsHighlight()
    {
        // Prüfung: Warnschwelle muss unter Highlight-Schwelle liegen
        LevelThresholds.ReputationWarningThreshold.Should().BeLessThan(
            LevelThresholds.ReputationHighlightThreshold);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature-Gates
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void PrestigeShopUnlock_IstLevel500()
    {
        // Prüfung: Prestige-Shop ab Level 500
        LevelThresholds.PrestigeShopUnlock.Should().Be(500);
    }

    [Fact]
    public void MaxPlayerLevel_IstFuenfzehnhundert()
    {
        // Prüfung: Hard-Cap bei Level 1500
        LevelThresholds.MaxPlayerLevel.Should().Be(1500);
    }

    [Fact]
    public void MinPlayerLevel_IstEins()
    {
        // Prüfung: Spieler startet bei Level 1
        LevelThresholds.MinPlayerLevel.Should().Be(1);
    }

    [Fact]
    public void WorkshopCeremonyThreshold_IstFuenfzig()
    {
        // Prüfung: Workshop-Level-Meilensteine ab Level 50 gefeiert
        LevelThresholds.WorkshopCeremonyThreshold.Should().Be(50);
    }

    [Fact]
    public void TutorialHintMaxLevel_IstDrei()
    {
        // Prüfung: Tutorial-Hinweise nur unter Level 3 anzeigen
        LevelThresholds.TutorialHintMaxLevel.Should().Be(3);
    }

    [Fact]
    public void AlleLevelWerte_SindPositiv()
    {
        // Prüfung: Kein Schwellenwert darf 0 oder negativ sein
        LevelThresholds.BannerStrip.Should().BePositive();
        LevelThresholds.QuickJobs.Should().BePositive();
        LevelThresholds.CraftingResearch.Should().BePositive();
        LevelThresholds.ManagerSection.Should().BePositive();
        LevelThresholds.MasterToolsSection.Should().BePositive();
        LevelThresholds.AutoCollect.Should().BePositive();
        LevelThresholds.AutoAccept.Should().BePositive();
        LevelThresholds.AutoAssign.Should().BePositive();
    }

    [Fact]
    public void ProgressiveDisclosureReihenfolge_IstLogisch()
    {
        // Prüfung: Features werden in sinnvoller Reihenfolge freigeschaltet
        // BannerStrip(3) < QuickJobs(5) < CraftingResearch(8) < ManagerSection(10) < AutoCollect(15) < MasterTools(20)
        LevelThresholds.BannerStrip.Should().BeLessThan(LevelThresholds.QuickJobs);
        LevelThresholds.QuickJobs.Should().BeLessThan(LevelThresholds.CraftingResearch);
        LevelThresholds.CraftingResearch.Should().BeLessThan(LevelThresholds.ManagerSection);
        LevelThresholds.ManagerSection.Should().BeLessThan(LevelThresholds.AutoCollect);
        LevelThresholds.AutoCollect.Should().BeLessThan(LevelThresholds.MasterToolsSection);
    }
}
