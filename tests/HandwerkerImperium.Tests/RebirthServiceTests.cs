using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für RebirthService: CanRebirth, GetRebirthCost, DoRebirth,
/// Sterne-Persistenz, ApplyStarsToWorkshops.
/// </summary>
public class RebirthServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (IGameStateService stateMock, IAudioService audioMock, IPrestigeService prestigeMock, IAscensionService ascensionMock, GameState state, RebirthService sut) ErstelleService()
    {
        var stateMock = Substitute.For<IGameStateService>();
        var audioMock = Substitute.For<IAudioService>();
        var prestigeMock = Substitute.For<IPrestigeService>();
        var ascensionMock = Substitute.For<IAscensionService>();
        var state = new GameState();
        stateMock.State.Returns(state);
        var sut = new RebirthService(stateMock, audioMock, prestigeMock, ascensionMock);
        return (stateMock, audioMock, prestigeMock, ascensionMock, state, sut);
    }

    private static Workshop ErstelleWerkstattAufMaxLevel(WorkshopType type = WorkshopType.Carpenter)
    {
        return new Workshop
        {
            Type = type,
            IsUnlocked = true,
            Level = Workshop.MaxLevel // 1000
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // CanRebirth
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CanRebirth_WorkshopAufMaxLevelKeinesSterne_GibtTrueZurueck()
    {
        // Vorbereitung
        var (_, _, _, _, state, sut) = ErstelleService();
        state.Workshops.Add(ErstelleWerkstattAufMaxLevel());
        // Keine Sterne → 0 < 5 ✓

        // Prüfung
        sut.CanRebirth(WorkshopType.Carpenter).Should().BeTrue();
    }

    [Fact]
    public void CanRebirth_WorkshopNichtAufMaxLevel_GibtFalseZurueck()
    {
        // Vorbereitung
        var (_, _, _, _, state, sut) = ErstelleService();
        state.Workshops.Add(new Workshop { Type = WorkshopType.Carpenter, IsUnlocked = true, Level = 999 });

        // Prüfung: Level < 1000 → kein Rebirth möglich
        sut.CanRebirth(WorkshopType.Carpenter).Should().BeFalse();
    }

    [Fact]
    public void CanRebirth_FuenfSterneBereitsVorhanden_GibtFalseZurueck()
    {
        // Vorbereitung
        var (_, _, _, _, state, sut) = ErstelleService();
        state.Workshops.Add(ErstelleWerkstattAufMaxLevel());
        state.WorkshopStars["Carpenter"] = 5; // Maximum

        // Prüfung: 5 Sterne = Maximum, kein weiterer Rebirth
        sut.CanRebirth(WorkshopType.Carpenter).Should().BeFalse();
    }

    [Fact]
    public void CanRebirth_WorkshopNichtVorhanden_GibtFalseZurueck()
    {
        // Vorbereitung: Kein Carpenter-Workshop
        var (_, _, _, _, state, sut) = ErstelleService();

        // Prüfung
        sut.CanRebirth(WorkshopType.Carpenter).Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetRebirthCost
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetRebirthCost_ErsterStern_Kostet100Goldschrauben()
    {
        // Vorbereitung: 0 Sterne → nächster = Stern 1
        var (_, _, _, _, state, sut) = ErstelleService();

        // Ausführung
        var (screws, _) = sut.GetRebirthCost(WorkshopType.Carpenter);

        // Prüfung: Laut RebirthCosts-Array: Stern 1 = 100 GS
        screws.Should().Be(100);
    }

    [Fact]
    public void GetRebirthCost_FuenfterStern_Kostet1000Goldschrauben()
    {
        // Vorbereitung: 4 Sterne → nächster = Stern 5
        var (_, _, _, _, state, sut) = ErstelleService();
        state.WorkshopStars["Carpenter"] = 4;

        // Ausführung
        var (screws, _) = sut.GetRebirthCost(WorkshopType.Carpenter);

        // Prüfung: Stern 5 = 1000 GS
        screws.Should().Be(1000);
    }

    [Theory]
    [InlineData(0, 0.10)]
    [InlineData(1, 0.15)]
    [InlineData(2, 0.20)]
    [InlineData(3, 0.25)]
    [InlineData(4, 0.30)]
    public void GetRebirthCost_JederStern_HatKorrektenGeldprozentsatz(int vorherigeSterne, double erwartetProzent)
    {
        // Vorbereitung
        var (_, _, _, _, state, sut) = ErstelleService();
        if (vorherigeSterne > 0)
            state.WorkshopStars["Carpenter"] = vorherigeSterne;

        // Ausführung
        var (_, geldProzent) = sut.GetRebirthCost(WorkshopType.Carpenter);

        // Prüfung
        ((double)geldProzent).Should().BeApproximately(erwartetProzent, 0.001);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DoRebirth
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DoRebirth_AlleVoraussetzungenErfuellt_GibtTrueZurueck()
    {
        // Vorbereitung
        var (stateMock, _, _, _, state, sut) = ErstelleService();
        state.Workshops.Add(ErstelleWerkstattAufMaxLevel());
        state.Money = 100_000m;
        stateMock.CanAffordGoldenScrews(100).Returns(true);
        stateMock.TrySpendGoldenScrews(100).Returns(true);

        // Ausführung
        bool result = sut.DoRebirth(WorkshopType.Carpenter);

        // Prüfung
        result.Should().BeTrue();
    }

    [Fact]
    public void DoRebirth_ErfolgreicherRebirth_SetzrLevelAufEins()
    {
        // Vorbereitung
        var (stateMock, _, _, _, state, sut) = ErstelleService();
        var ws = ErstelleWerkstattAufMaxLevel();
        state.Workshops.Add(ws);
        state.Money = 100_000m;
        stateMock.CanAffordGoldenScrews(100).Returns(true);
        stateMock.TrySpendGoldenScrews(100).Returns(true);

        // Ausführung
        sut.DoRebirth(WorkshopType.Carpenter);

        // Prüfung: Level auf 1 zurückgesetzt (Kern-Mechanik!)
        ws.Level.Should().Be(1);
    }

    [Fact]
    public void DoRebirth_ErfolgreicherRebirth_ErhoehtSternAnzahl()
    {
        // Vorbereitung
        var (stateMock, _, _, _, state, sut) = ErstelleService();
        state.Workshops.Add(ErstelleWerkstattAufMaxLevel());
        state.Money = 100_000m;
        stateMock.CanAffordGoldenScrews(100).Returns(true);
        stateMock.TrySpendGoldenScrews(100).Returns(true);

        // Ausführung
        sut.DoRebirth(WorkshopType.Carpenter);

        // Prüfung: 0 → 1 Stern
        state.WorkshopStars.GetValueOrDefault("Carpenter", 0).Should().Be(1);
    }

    [Fact]
    public void DoRebirth_ErfolgreicherRebirth_ZiehtGeldAb()
    {
        // Vorbereitung
        var (stateMock, _, _, _, state, sut) = ErstelleService();
        state.Workshops.Add(ErstelleWerkstattAufMaxLevel());
        state.Money = 100_000m;
        stateMock.CanAffordGoldenScrews(100).Returns(true);
        stateMock.TrySpendGoldenScrews(100).Returns(true);

        // Ausführung: Stern 1 kostet 10% des Geldes
        sut.DoRebirth(WorkshopType.Carpenter);

        // Prüfung: 100.000 - 10% = 90.000
        state.Money.Should().Be(90_000m);
    }

    [Fact]
    public void DoRebirth_NichtGenugGoldschrauben_GibtFalseZurueck()
    {
        // Vorbereitung
        var (stateMock, _, _, _, state, sut) = ErstelleService();
        state.Workshops.Add(ErstelleWerkstattAufMaxLevel());
        stateMock.CanAffordGoldenScrews(100).Returns(false);

        // Ausführung
        bool result = sut.DoRebirth(WorkshopType.Carpenter);

        // Prüfung
        result.Should().BeFalse();
    }

    [Fact]
    public void DoRebirth_BedingungNichtErfuellt_GibtFalseZurueck()
    {
        // Vorbereitung: Level < 1000
        var (stateMock, _, _, _, state, sut) = ErstelleService();
        state.Workshops.Add(new Workshop { Type = WorkshopType.Carpenter, IsUnlocked = true, Level = 500 });

        // Ausführung
        bool result = sut.DoRebirth(WorkshopType.Carpenter);

        // Prüfung
        result.Should().BeFalse();
    }

    [Fact]
    public void DoRebirth_ErfolgreicherRebirth_FeuertRebirthCompletedEvent()
    {
        // Vorbereitung
        var (stateMock, _, _, _, state, sut) = ErstelleService();
        state.Workshops.Add(ErstelleWerkstattAufMaxLevel());
        state.Money = 100_000m;
        stateMock.CanAffordGoldenScrews(100).Returns(true);
        stateMock.TrySpendGoldenScrews(100).Returns(true);

        WorkshopType? gefeuertFuer = null;
        sut.RebirthCompleted += (_, type) => gefeuertFuer = type;

        // Ausführung
        sut.DoRebirth(WorkshopType.Carpenter);

        // Prüfung
        gefeuertFuer.Should().Be(WorkshopType.Carpenter);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetStars
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetStars_KeineEintraege_GibtNullZurueck()
    {
        // Vorbereitung
        var (_, _, _, _, _, sut) = ErstelleService();

        // Prüfung
        sut.GetStars(WorkshopType.Carpenter).Should().Be(0);
    }

    [Fact]
    public void GetStars_SterneVorhanden_GibtKorrekteAnzahlZurueck()
    {
        // Vorbereitung
        var (_, _, _, _, state, sut) = ErstelleService();
        state.WorkshopStars["Plumber"] = 3;

        // Prüfung
        sut.GetStars(WorkshopType.Plumber).Should().Be(3);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ApplyStarsToWorkshops
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ApplyStarsToWorkshops_SterneImState_UebertraegAufWorkshopInstanzen()
    {
        // Vorbereitung
        var (_, _, _, _, state, sut) = ErstelleService();
        var ws = new Workshop { Type = WorkshopType.Carpenter, RebirthStars = 0 };
        state.Workshops.Add(ws);
        state.WorkshopStars["Carpenter"] = 3;

        // Ausführung
        sut.ApplyStarsToWorkshops();

        // Prüfung: RebirthStars auf der Workshop-Instanz muss aktualisiert sein
        ws.RebirthStars.Should().Be(3);
    }
}
