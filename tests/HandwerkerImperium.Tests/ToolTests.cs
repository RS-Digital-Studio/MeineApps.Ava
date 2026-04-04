using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für Tool: IsUnlocked, CanUpgrade, UpgradeCostScrews,
/// ZoneBonus, TimeBonus, RelatedMiniGame, CreateDefaults().
/// </summary>
public class ToolTests
{
    // ═══════════════════════════════════════════════════════════════════
    // IsUnlocked, CanUpgrade
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsUnlocked_Level0_IstFalse()
    {
        // Vorbereitung
        var tool = new Tool { Level = 0 };

        // Prüfung: Level 0 = noch nicht freigeschaltet
        tool.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public void IsUnlocked_Level1_IstTrue()
    {
        // Vorbereitung
        var tool = new Tool { Level = 1 };

        // Prüfung
        tool.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public void CanUpgrade_Level4_IstTrue()
    {
        // Vorbereitung: MaxLevel ist 5
        var tool = new Tool { Level = 4 };

        // Prüfung
        tool.CanUpgrade.Should().BeTrue();
    }

    [Fact]
    public void CanUpgrade_Level5_IstFalse()
    {
        // Vorbereitung: MaxLevel erreicht
        var tool = new Tool { Level = 5 };

        // Prüfung
        tool.CanUpgrade.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // UpgradeCostScrews
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 15)]
    [InlineData(2, 35)]
    [InlineData(3, 70)]
    [InlineData(4, 120)]
    [InlineData(5, 0)] // MaxLevel: kein Upgrade mehr möglich
    public void UpgradeCostScrews_AlleLevel_GibtKorrekteKosten(int level, int erwartetKosten)
    {
        // Vorbereitung
        var tool = new Tool { Level = level };

        // Prüfung
        tool.UpgradeCostScrews.Should().Be(erwartetKosten);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ZoneBonus
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ZoneBonus_Level0_IstNull()
    {
        // Vorbereitung
        var tool = new Tool { Level = 0 };

        // Prüfung: Kein Bonus ohne Freischaltung
        tool.ZoneBonus.Should().Be(0.0);
    }

    [Theory]
    [InlineData(1, 0.05)]
    [InlineData(3, 0.15)]
    [InlineData(5, 0.25)]
    public void ZoneBonus_VerschiedeneLevel_GibtKorrektenBonus(int level, double erwartetBonus)
    {
        // Vorbereitung
        var tool = new Tool { Level = level };

        // Prüfung: +5% pro Level
        tool.ZoneBonus.Should().Be(erwartetBonus);
    }

    // ═══════════════════════════════════════════════════════════════════
    // TimeBonus
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TimeBonus_Level0_IstNull()
    {
        // Vorbereitung
        var tool = new Tool { Level = 0 };

        // Prüfung
        tool.TimeBonus.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(3, 10)]
    [InlineData(5, 15)]
    public void TimeBonus_VerschiedeneLevel_GibtKorrektenBonus(int level, int erwartetBonus)
    {
        // Vorbereitung
        var tool = new Tool { Level = level };

        // Prüfung
        tool.TimeBonus.Should().Be(erwartetBonus);
    }

    // ═══════════════════════════════════════════════════════════════════
    // RelatedMiniGame
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(ToolType.Saw, MiniGameType.Sawing)]
    [InlineData(ToolType.PipeWrench, MiniGameType.PipePuzzle)]
    [InlineData(ToolType.Screwdriver, MiniGameType.WiringGame)]
    [InlineData(ToolType.Paintbrush, MiniGameType.PaintingGame)]
    [InlineData(ToolType.Hammer, MiniGameType.RoofTiling)]
    [InlineData(ToolType.SpiritLevel, MiniGameType.Blueprint)]
    [InlineData(ToolType.Magnifier, MiniGameType.Inspection)]
    [InlineData(ToolType.Compass, MiniGameType.DesignPuzzle)]
    public void RelatedMiniGame_JederToolTyp_MapptAufKorrektesSpiel(ToolType toolTyp, MiniGameType erwartetSpiel)
    {
        // Vorbereitung
        var tool = new Tool { Type = toolTyp };

        // Prüfung
        tool.RelatedMiniGame.Should().Be(erwartetSpiel);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CreateDefaults
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateDefaults_ErstelltAchtWerkzeuge()
    {
        // Ausführung
        var tools = Tool.CreateDefaults();

        // Prüfung: Ein Werkzeug pro ToolType (8 Stück)
        tools.Should().HaveCount(8);
    }

    [Fact]
    public void CreateDefaults_AlleWerkzeuge_StartenAufLevel0()
    {
        // Ausführung
        var tools = Tool.CreateDefaults();

        // Prüfung: Alle beginnen gesperrt
        tools.Should().AllSatisfy(t => t.Level.Should().Be(0));
    }

    [Fact]
    public void CreateDefaults_AlleToolTypen_SindVertreten()
    {
        // Ausführung
        var tools = Tool.CreateDefaults();
        var toolTypes = tools.Select(t => t.Type).ToHashSet();

        // Prüfung: Alle 8 ToolTypes vorhanden
        foreach (ToolType typ in Enum.GetValues<ToolType>())
        {
            toolTypes.Should().Contain(typ);
        }
    }
}
