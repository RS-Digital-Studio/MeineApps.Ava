using BomberBlast.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für MasterModeService (v2.0.48 — AAA-Audit Phase 5).
/// Validiert IsUnlocked-Gating (HighestCompletedLevel >= 100), IsActive-Setter-Guard,
/// RecordLevelCompleted-Persistenz, TotalMasterClears + TotalMaster3Stars.
/// </summary>
public class MasterModeServiceTests
{
    private static (MasterModeService Service, IProgressService Progress, ICloudSaveService Cloud)
        CreateService(int highestCompletedLevel = 0)
    {
        var prefs = new InMemoryPreferences();
        var progress = Substitute.For<IProgressService>();
        var logger = Substitute.For<IAppLogger>();
        var cloud = Substitute.For<ICloudSaveService>();

        progress.HighestCompletedLevel.Returns(highestCompletedLevel);

        var service = new MasterModeService(prefs, progress, logger, cloud);
        return (service, progress, cloud);
    }

    [Fact]
    public void IsUnlocked_HighestUnter100_LiefertFalse()
    {
        var (svc, _, _) = CreateService(highestCompletedLevel: 99);
        svc.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public void IsUnlocked_Highest100_LiefertTrue()
    {
        var (svc, _, _) = CreateService(highestCompletedLevel: 100);
        svc.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public void IsActive_NichtUnlocked_KannNichtAktiviertWerden()
    {
        var (svc, _, _) = CreateService(highestCompletedLevel: 50);

        svc.IsActive = true;

        svc.IsActive.Should().BeFalse("MasterMode kann nicht aktiviert werden wenn !IsUnlocked");
    }

    [Fact]
    public void IsActive_Unlocked_KannAktiviertWerden()
    {
        var (svc, _, _) = CreateService(highestCompletedLevel: 100);

        svc.IsActive = true;

        svc.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_Deaktivieren_GehtImmer()
    {
        var (svc, _, _) = CreateService(highestCompletedLevel: 100);
        svc.IsActive = true;

        svc.IsActive = false;

        svc.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RecordLevelCompleted_ErsterDurchlauf_LiefertTrue()
    {
        var (svc, _, _) = CreateService(highestCompletedLevel: 100);

        var firstClear = svc.RecordLevelCompleted(level: 5, stars: 3);

        firstClear.Should().BeTrue();
        svc.GetMasterStars(5).Should().Be(3);
    }

    [Fact]
    public void RecordLevelCompleted_SchlechterScoreNachBest_BehältBest()
    {
        var (svc, _, _) = CreateService(highestCompletedLevel: 100);
        svc.RecordLevelCompleted(level: 5, stars: 3);

        svc.RecordLevelCompleted(level: 5, stars: 2);

        svc.GetMasterStars(5).Should().Be(3, "Stars nur erhöhen, nicht senken");
    }

    [Fact]
    public void TotalMasterClears_ZaehltAlleLevelsMitStars()
    {
        var (svc, _, _) = CreateService(highestCompletedLevel: 100);
        svc.RecordLevelCompleted(1, 1);
        svc.RecordLevelCompleted(2, 2);
        svc.RecordLevelCompleted(3, 3);
        svc.RecordLevelCompleted(4, 0); // 0 Stars zählt nicht

        svc.TotalMasterClears.Should().Be(3);
    }

    [Fact]
    public void TotalMaster3Stars_ZaehltNur3SterneClears()
    {
        var (svc, _, _) = CreateService(highestCompletedLevel: 100);
        svc.RecordLevelCompleted(1, 1);
        svc.RecordLevelCompleted(2, 3);
        svc.RecordLevelCompleted(3, 3);
        svc.RecordLevelCompleted(4, 2);

        svc.TotalMaster3Stars.Should().Be(2);
    }

    [Fact]
    public void GetMasterStars_NichtGespielt_LiefertNull()
    {
        var (svc, _, _) = CreateService(highestCompletedLevel: 100);

        svc.GetMasterStars(99).Should().Be(0);
    }
}
