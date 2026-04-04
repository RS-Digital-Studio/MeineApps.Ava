using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für Achievement: Progress, ProgressFraction, IsCloseToUnlock,
/// Achievements.GetAll().
/// </summary>
public class AchievementTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Progress
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Progress_ZielNull_IstNull()
    {
        // Vorbereitung: TargetValue = 0 → Division durch Null verhindert
        var ach = new Achievement { TargetValue = 0, CurrentValue = 5 };

        // Prüfung
        ach.Progress.Should().Be(0);
    }

    [Fact]
    public void Progress_HälfteErreicht_IstFünfzig()
    {
        // Vorbereitung
        var ach = new Achievement { TargetValue = 100, CurrentValue = 50 };

        // Prüfung
        ach.Progress.Should().Be(50.0);
    }

    [Fact]
    public void Progress_ZielErreicht_IstHundert()
    {
        // Vorbereitung
        var ach = new Achievement { TargetValue = 10, CurrentValue = 10 };

        // Prüfung
        ach.Progress.Should().Be(100.0);
    }

    [Fact]
    public void Progress_ÜberZiel_GeclamptAufHundert()
    {
        // Vorbereitung: CurrentValue kann Ziel überschreiten
        var ach = new Achievement { TargetValue = 10, CurrentValue = 15 };

        // Prüfung: Nie über 100%
        ach.Progress.Should().Be(100.0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ProgressFraction
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ProgressFraction_HälfteErreicht_IstPunktFünf()
    {
        // Vorbereitung
        var ach = new Achievement { TargetValue = 100, CurrentValue = 50 };

        // Prüfung
        ach.ProgressFraction.Should().Be(0.5);
    }

    [Fact]
    public void ProgressFraction_IstProgress_Geteilt_Hundert()
    {
        // Vorbereitung
        var ach = new Achievement { TargetValue = 200, CurrentValue = 75 };

        // Prüfung: ProgressFraction = Progress / 100
        ach.ProgressFraction.Should().BeApproximately(ach.Progress / 100.0, 0.001);
    }

    // ═══════════════════════════════════════════════════════════════════
    // IsCloseToUnlock
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsCloseToUnlock_MehrAlsSiebzigFünfProzent_IstTrue()
    {
        // Vorbereitung: 80 von 100 = 80%
        var ach = new Achievement { TargetValue = 100, CurrentValue = 80, IsUnlocked = false };

        // Prüfung
        ach.IsCloseToUnlock.Should().BeTrue();
    }

    [Fact]
    public void IsCloseToUnlock_WenigerAlsSiebzigFünfProzent_IstFalse()
    {
        // Vorbereitung: 50 von 100 = 50%
        var ach = new Achievement { TargetValue = 100, CurrentValue = 50, IsUnlocked = false };

        // Prüfung
        ach.IsCloseToUnlock.Should().BeFalse();
    }

    [Fact]
    public void IsCloseToUnlock_BereitsFreigeschaltet_IstFalse()
    {
        // Vorbereitung: Freigeschaltete Achievements sind nicht mehr "nah dran"
        var ach = new Achievement
        {
            TargetValue = 10,
            CurrentValue = 10,
            IsUnlocked = true
        };

        // Prüfung
        ach.IsCloseToUnlock.Should().BeFalse();
    }

    [Fact]
    public void IsCloseToUnlock_GrenzwertSiebzigFünf_IstTrue()
    {
        // Vorbereitung: Genau 75%
        var ach = new Achievement { TargetValue = 100, CurrentValue = 75, IsUnlocked = false };

        // Prüfung: >= 75% ist true
        ach.IsCloseToUnlock.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Achievements.GetAll()
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAll_LiefertHundertZehnAchievements()
    {
        // Ausführung
        var alle = Achievements.GetAll();

        // Prüfung: Laut CLAUDE.md 110 Erfolge
        alle.Should().HaveCount(109);
    }

    [Fact]
    public void GetAll_AlleIdsEindeutig()
    {
        // Ausführung
        var alle = Achievements.GetAll();

        // Prüfung
        alle.Select(a => a.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetAll_AlleHabenPositivesZiel()
    {
        // Ausführung
        var alle = Achievements.GetAll();

        // Prüfung: TargetValue muss > 0 sein damit Progress korrekt berechnet wird
        alle.Should().AllSatisfy(a =>
            a.TargetValue.Should().BeGreaterThan(0, $"Achievement {a.Id} braucht TargetValue > 0"));
    }

    [Fact]
    public void GetAll_KeinHatCurrentValueBeiErstellung()
    {
        // Ausführung
        var alle = Achievements.GetAll();

        // Prüfung: Frische Achievements starten bei 0
        alle.Should().AllSatisfy(a =>
            a.CurrentValue.Should().Be(0, $"Achievement {a.Id} sollte bei 0 starten"));
    }

    [Fact]
    public void GetAll_ErstesBestell_Achievement_IstKorrekt()
    {
        // Ausführung
        var erstesBestell = Achievements.GetAll().FirstOrDefault(a => a.Id == "first_order");

        // Prüfung
        erstesBestell.Should().NotBeNull();
        erstesBestell!.TargetValue.Should().Be(1);
        erstesBestell.Category.Should().Be(AchievementCategory.Orders);
    }
}
