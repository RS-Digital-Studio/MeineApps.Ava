using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für Research: RemainingTime, Progress, InstantFinishScrewCost,
/// CanInstantFinish, Duration.
/// </summary>
public class ResearchTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Duration
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Duration_KonvertiertTicksKorrekt()
    {
        // Vorbereitung
        var dauer = TimeSpan.FromHours(2);
        var research = new Research { DurationTicks = dauer.Ticks };

        // Prüfung
        research.Duration.Should().Be(dauer);
    }

    // ═══════════════════════════════════════════════════════════════════
    // RemainingTime
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RemainingTime_NichtAktiv_IstNull()
    {
        // Vorbereitung
        var research = new Research { IsActive = false };

        // Prüfung
        research.RemainingTime.Should().BeNull();
    }

    [Fact]
    public void RemainingTime_AktivOhneStartzeit_IstNull()
    {
        // Vorbereitung
        var research = new Research { IsActive = true, StartedAt = null };

        // Prüfung
        research.RemainingTime.Should().BeNull();
    }

    [Fact]
    public void RemainingTime_AktivMitZukünftigerFertigstellung_IstPositiv()
    {
        // Vorbereitung
        var research = new Research
        {
            IsActive = true,
            StartedAt = DateTime.UtcNow.AddSeconds(-30),
            DurationTicks = TimeSpan.FromMinutes(5).Ticks
        };

        // Prüfung
        research.RemainingTime.Should().NotBeNull();
        research.RemainingTime!.Value.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void RemainingTime_Abgelaufen_IstZero()
    {
        // Vorbereitung
        var research = new Research
        {
            IsActive = true,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            DurationTicks = TimeSpan.FromMinutes(30).Ticks
        };

        // Prüfung: Nie negativ
        research.RemainingTime.Should().NotBeNull();
        research.RemainingTime!.Value.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void RemainingTime_BerücksichtigtBonusSeconds()
    {
        // Vorbereitung: Gestartet vor 20s, Dauer 60s, +30s Bonus = 10s verbleibend
        var research = new Research
        {
            IsActive = true,
            StartedAt = DateTime.UtcNow.AddSeconds(-20),
            DurationTicks = TimeSpan.FromSeconds(60).Ticks,
            BonusSeconds = 30
        };

        // Prüfung: Effektive verbleibende Zeit ca. 10s
        research.RemainingTime!.Value.TotalSeconds.Should().BeApproximately(10.0, 2.0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Progress
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Progress_Erforscht_IstHundert()
    {
        // Vorbereitung
        var research = new Research { IsResearched = true };

        // Prüfung
        research.Progress.Should().Be(100.0);
    }

    [Fact]
    public void Progress_NichtAktiv_IstNull()
    {
        // Vorbereitung
        var research = new Research { IsActive = false, IsResearched = false };

        // Prüfung
        research.Progress.Should().Be(0.0);
    }

    [Fact]
    public void Progress_HälfteDerZeitVergangen_IstCaFünfzig()
    {
        // Vorbereitung
        var research = new Research
        {
            IsActive = true,
            StartedAt = DateTime.UtcNow.AddSeconds(-50),
            DurationTicks = TimeSpan.FromSeconds(100).Ticks
        };

        // Prüfung: ~50% Fortschritt
        research.Progress.Should().BeApproximately(50.0, 2.0);
    }

    [Fact]
    public void Progress_Überschritten_GeclamptAufHundert()
    {
        // Vorbereitung: Doppelt so lang wie Dauer vergangen
        var research = new Research
        {
            IsActive = true,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            DurationTicks = TimeSpan.FromMinutes(30).Ticks
        };

        // Prüfung: Progress nie über 100
        research.Progress.Should().Be(100.0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // InstantFinishScrewCost
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, 0)]   // Unter Level 8: kein Sofortabschluss
    [InlineData(7, 0)]   // Noch unter Level 8
    [InlineData(8, 15)]  // Ab Level 8: erste Kosten
    [InlineData(10, 40)]
    [InlineData(15, 180)]
    [InlineData(20, 500)]
    public void InstantFinishScrewCost_AlleLevel_GibtKorrekteKosten(int level, int erwartet)
    {
        // Vorbereitung
        var research = new Research { Level = level };

        // Prüfung
        research.InstantFinishScrewCost.Should().Be(erwartet);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CanInstantFinish
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CanInstantFinish_AktivUndLevel8_IstTrue()
    {
        // Vorbereitung
        var research = new Research
        {
            IsActive = true,
            Level = 8,
            DurationTicks = TimeSpan.FromHours(1).Ticks,
            StartedAt = DateTime.UtcNow
        };

        // Prüfung
        research.CanInstantFinish.Should().BeTrue();
    }

    [Fact]
    public void CanInstantFinish_NichtAktiv_IstFalse()
    {
        // Vorbereitung
        var research = new Research { IsActive = false, Level = 10 };

        // Prüfung
        research.CanInstantFinish.Should().BeFalse();
    }

    [Fact]
    public void CanInstantFinish_AktivAberLevel7_IstFalse()
    {
        // Vorbereitung: Level 7 hat Kosten 0 → CanInstantFinish = false
        var research = new Research { IsActive = true, Level = 7 };

        // Prüfung
        research.CanInstantFinish.Should().BeFalse();
    }
}
