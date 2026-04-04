using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für PrestigeData: CalculatePrestigePoints(), TotalPrestigeCount,
/// CanPrestige(), GetHighestAvailableTier(), GetAllAvailableTiers(), GetBestRunTime().
/// </summary>
public class PrestigeDataTests
{
    // ═══════════════════════════════════════════════════════════════════
    // CalculatePrestigePoints (Kernformel!)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculatePrestigePoints_NullGeld_IstNull()
    {
        // Prüfung: Kein Geld = keine PP
        PrestigeData.CalculatePrestigePoints(0m).Should().Be(0);
    }

    [Fact]
    public void CalculatePrestigePoints_NegativesGeld_IstNull()
    {
        // Prüfung: Negativer Input → 0
        PrestigeData.CalculatePrestigePoints(-1000m).Should().Be(0);
    }

    [Fact]
    public void CalculatePrestigePoints_HundertTausendEuro_IstEins()
    {
        // Prüfung: floor(sqrt(100_000 / 100_000)) = floor(1.0) = 1
        PrestigeData.CalculatePrestigePoints(100_000m).Should().Be(1);
    }

    [Fact]
    public void CalculatePrestigePoints_VierHundertTausendEuro_IstZwei()
    {
        // Prüfung: floor(sqrt(400_000 / 100_000)) = floor(sqrt(4)) = floor(2) = 2
        PrestigeData.CalculatePrestigePoints(400_000m).Should().Be(2);
    }

    [Fact]
    public void CalculatePrestigePoints_EineMillionEuro_IstDrei()
    {
        // Prüfung: floor(sqrt(1_000_000 / 100_000)) = floor(sqrt(10)) = floor(3.16) = 3
        PrestigeData.CalculatePrestigePoints(1_000_000m).Should().Be(3);
    }

    [Fact]
    public void CalculatePrestigePoints_ZehnMillionEuro_IstZehn()
    {
        // Prüfung: floor(sqrt(10_000_000 / 100_000)) = floor(sqrt(100)) = 10
        PrestigeData.CalculatePrestigePoints(10_000_000m).Should().Be(10);
    }

    // ═══════════════════════════════════════════════════════════════════
    // TotalPrestigeCount
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TotalPrestigeCount_KeinePrestige_IstNull()
    {
        // Vorbereitung
        var data = new PrestigeData();

        // Prüfung
        data.TotalPrestigeCount.Should().Be(0);
    }

    [Fact]
    public void TotalPrestigeCount_SummiertAlleZähler()
    {
        // Vorbereitung
        var data = new PrestigeData
        {
            BronzeCount = 3,
            SilverCount = 2,
            GoldCount = 1,
            PlatinCount = 1
        };

        // Prüfung: 3 + 2 + 1 + 1 = 7
        data.TotalPrestigeCount.Should().Be(7);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CanPrestige()
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CanPrestige_Bronze_Level30_IstTrue()
    {
        // Vorbereitung: Bronze erfordert Level 30
        var data = new PrestigeData();

        // Prüfung
        data.CanPrestige(PrestigeTier.Bronze, 30).Should().BeTrue();
    }

    [Fact]
    public void CanPrestige_Bronze_Level29_IstFalse()
    {
        // Vorbereitung: Grenzfall - Level 29 reicht nicht
        var data = new PrestigeData();

        // Prüfung
        data.CanPrestige(PrestigeTier.Bronze, 29).Should().BeFalse();
    }

    [Fact]
    public void CanPrestige_Silver_ErfordertEinBronze()
    {
        // Vorbereitung
        var data = new PrestigeData { BronzeCount = 1 };

        // Prüfung: Silver = Level 100 + 1x Bronze
        data.CanPrestige(PrestigeTier.Silver, 100).Should().BeTrue();
    }

    [Fact]
    public void CanPrestige_Silver_OhneBronze_IstFalse()
    {
        // Vorbereitung: Kein Bronze-Prestige
        var data = new PrestigeData { BronzeCount = 0 };

        // Prüfung
        data.CanPrestige(PrestigeTier.Silver, 200).Should().BeFalse();
    }

    [Fact]
    public void CanPrestige_Platin_ErfordertZweiGold()
    {
        // Vorbereitung
        var data = new PrestigeData { BronzeCount = 1, SilverCount = 1, GoldCount = 2 };

        // Prüfung: Platin = Level 500 + 2x Gold
        data.CanPrestige(PrestigeTier.Platin, 500).Should().BeTrue();
    }

    [Fact]
    public void CanPrestige_Platin_NurEinGold_IstFalse()
    {
        // Vorbereitung: Nur 1 Gold statt 2
        var data = new PrestigeData { GoldCount = 1 };

        // Prüfung
        data.CanPrestige(PrestigeTier.Platin, 500).Should().BeFalse();
    }

    [Fact]
    public void CanPrestige_Legende_ErfordertDreiMeister()
    {
        // Vorbereitung: Alle Vorbedingungen erfüllt
        var data = new PrestigeData
        {
            BronzeCount = 1,
            SilverCount = 1,
            GoldCount = 2,
            PlatinCount = 2,
            DiamantCount = 2,
            MeisterCount = 3
        };

        // Prüfung: Legende = Level 1200 + 3x Meister
        data.CanPrestige(PrestigeTier.Legende, 1200).Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetHighestAvailableTier()
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetHighestAvailableTier_KeineVorbedingungen_IstBronze()
    {
        // Vorbereitung: Frischer Spieler ab Level 30
        var data = new PrestigeData();

        // Prüfung
        data.GetHighestAvailableTier(30).Should().Be(PrestigeTier.Bronze);
    }

    [Fact]
    public void GetHighestAvailableTier_NochKeinLevel30_IstNone()
    {
        // Vorbereitung: Zu niedriges Level
        var data = new PrestigeData();

        // Prüfung
        data.GetHighestAvailableTier(1).Should().Be(PrestigeTier.None);
    }

    [Fact]
    public void GetHighestAvailableTier_EinBronze_SilverVerfügbar()
    {
        // Vorbereitung
        var data = new PrestigeData { BronzeCount = 1 };

        // Prüfung
        data.GetHighestAvailableTier(100).Should().Be(PrestigeTier.Silver);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetBestRunTime()
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetBestRunTime_KeinEintrag_IstNull()
    {
        // Vorbereitung
        var data = new PrestigeData();

        // Prüfung
        data.GetBestRunTime(PrestigeTier.Bronze).Should().BeNull();
    }

    [Fact]
    public void GetBestRunTime_MitEintrag_GibtKorrekteZeit()
    {
        // Vorbereitung
        var bestTime = TimeSpan.FromHours(2);
        var data = new PrestigeData();
        data.BestRunTimes[PrestigeTier.Bronze.ToString()] = bestTime.Ticks;

        // Prüfung
        data.GetBestRunTime(PrestigeTier.Bronze).Should().Be(bestTime);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetAllAvailableTiers()
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAllAvailableTiers_NurBronzeVerfügbar_ListeMitEinemElement()
    {
        // Vorbereitung: Level 30, kein Prestige
        var data = new PrestigeData();

        // Ausführung
        var tiers = data.GetAllAvailableTiers(30);

        // Prüfung
        tiers.Should().ContainSingle(t => t == PrestigeTier.Bronze);
    }

    [Fact]
    public void GetAllAvailableTiers_MitBronzeUndSilver_ListeMitBeiden()
    {
        // Vorbereitung: Bronze abgeschlossen, Silver verfügbar
        var data = new PrestigeData { BronzeCount = 1 };

        // Ausführung
        var tiers = data.GetAllAvailableTiers(100);

        // Prüfung: Bronze und Silver verfügbar (Gold noch nicht, braucht 1x Silver)
        tiers.Should().Contain(PrestigeTier.Bronze);
        tiers.Should().Contain(PrestigeTier.Silver);
    }
}
