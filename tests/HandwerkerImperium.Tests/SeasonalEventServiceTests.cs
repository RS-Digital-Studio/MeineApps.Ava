using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.Models.Events;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für SeasonalEventService: Währung hinzufügen, Items kaufen,
/// Event-Aktivierung, Dispose/Event-Abmeldung.
/// </summary>
public class SeasonalEventServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (IGameStateService stateMock, IWorkerService workerMock, ICraftingService craftMock, GameState state) ErstelleMocks()
    {
        var stateMock = Substitute.For<IGameStateService>();
        var workerMock = Substitute.For<IWorkerService>();
        var craftMock = Substitute.For<ICraftingService>();
        var state = new GameState();
        stateMock.State.Returns(state);
        return (stateMock, workerMock, craftMock, state);
    }

    private static SeasonalEvent ErstelleAktivesEvent(Season saison = Season.Spring)
    {
        return new SeasonalEvent
        {
            Season = saison,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            Currency = 0,
            TotalPoints = 0
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // IsEventActive
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsEventActive_AktivesEvent_GibtTrueZurueck()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = ErstelleAktivesEvent();
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Prüfung
        sut.IsEventActive.Should().BeTrue();
    }

    [Fact]
    public void IsEventActive_KeinEvent_GibtFalseZurueck()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = null;
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Prüfung
        sut.IsEventActive.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // AddSeasonalCurrency
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddSeasonalCurrency_AktivesEvent_ErhoehtWaehrung()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = ErstelleAktivesEvent();
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Ausführung
        sut.AddSeasonalCurrency(10);

        // Prüfung
        state.CurrentSeasonalEvent!.Currency.Should().Be(10);
        state.CurrentSeasonalEvent.TotalPoints.Should().Be(10);
    }

    [Fact]
    public void AddSeasonalCurrency_KeinEvent_TutNichts()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = null;
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Ausführung & Prüfung: Kein Crash
        var act = () => sut.AddSeasonalCurrency(10);
        act.Should().NotThrow();
    }

    [Fact]
    public void AddSeasonalCurrency_NulloderNegativ_TutNichts()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = ErstelleAktivesEvent();
        state.CurrentSeasonalEvent.Currency = 50;
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Ausführung
        sut.AddSeasonalCurrency(0);
        sut.AddSeasonalCurrency(-5);

        // Prüfung: Keine Änderung
        state.CurrentSeasonalEvent!.Currency.Should().Be(50);
    }

    [Fact]
    public void AddSeasonalCurrency_AktivesEvent_FeuertSeasonalEventChangedEvent()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = ErstelleAktivesEvent();
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);
        bool eventFired = false;
        sut.SeasonalEventChanged += () => eventFired = true;

        // Ausführung
        sut.AddSeasonalCurrency(5);

        // Prüfung
        eventFired.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuySeasonalItem
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void BuySeasonalItem_GenugWaehrung_GibtTrueZurueck()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = ErstelleAktivesEvent(Season.Spring);
        state.CurrentSeasonalEvent.Currency = 100;
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Ausführung: XP-Pack kostet 30 SP
        bool result = sut.BuySeasonalItem("spring_xp_pack");

        // Prüfung
        result.Should().BeTrue();
    }

    [Fact]
    public void BuySeasonalItem_GenugWaehrung_ReduzierteWaehrung()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = ErstelleAktivesEvent(Season.Spring);
        state.CurrentSeasonalEvent.Currency = 100;
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Ausführung: XP-Pack kostet 30 SP
        sut.BuySeasonalItem("spring_xp_pack");

        // Prüfung
        state.CurrentSeasonalEvent!.Currency.Should().Be(70);
    }

    [Fact]
    public void BuySeasonalItem_NichtGenugWaehrung_GibtFalseZurueck()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = ErstelleAktivesEvent(Season.Spring);
        state.CurrentSeasonalEvent.Currency = 5; // Zu wenig für 30 SP
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Ausführung
        bool result = sut.BuySeasonalItem("spring_xp_pack");

        // Prüfung
        result.Should().BeFalse();
    }

    [Fact]
    public void BuySeasonalItem_BereitsGekauft_GibtFalseZurueck()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = ErstelleAktivesEvent(Season.Spring);
        state.CurrentSeasonalEvent.Currency = 200;
        state.CurrentSeasonalEvent.PurchasedItems.Add("spring_xp_pack");
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Ausführung
        bool result = sut.BuySeasonalItem("spring_xp_pack");

        // Prüfung: Doppelkauf verhindert
        result.Should().BeFalse();
    }

    [Fact]
    public void BuySeasonalItem_UnbekannteItemId_GibtFalseZurueck()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = ErstelleAktivesEvent(Season.Spring);
        state.CurrentSeasonalEvent.Currency = 200;
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Ausführung
        bool result = sut.BuySeasonalItem("nicht_existierendes_item");

        // Prüfung
        result.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetShopItems (statisch)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(Season.Spring)]
    [InlineData(Season.Summer)]
    [InlineData(Season.Autumn)]
    [InlineData(Season.Winter)]
    public void GetShopItems_JedeSaison_Gibt6ItemsZurueck(Season saison)
    {
        // Ausführung
        var items = SeasonalEventService.GetShopItems(saison);

        // Prüfung: 4 Basis + 2 einzigartige = 6 Items pro Saison
        items.Should().HaveCount(6, $"Saison {saison} muss 6 Items haben (4 Basis + 2 einzigartige)");
    }

    [Fact]
    public void GetShopItems_AlleItemsHabenEindeutigeId()
    {
        // Ausführung
        foreach (Season saison in Enum.GetValues<Season>())
        {
            var items = SeasonalEventService.GetShopItems(saison);
            // Prüfung: Alle IDs müssen eindeutig sein
            items.Select(i => i.Id).Should().OnlyHaveUniqueItems($"in Saison {saison}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Dispose
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Dispose_WirdAufgerufen_MeldetEventHandlerAb()
    {
        // Vorbereitung
        var (stateMock, workerMock, craftMock, state) = ErstelleMocks();
        state.CurrentSeasonalEvent = ErstelleAktivesEvent();
        var sut = new SeasonalEventService(stateMock, workerMock, craftMock);

        // Ausführung
        sut.Dispose();

        // Prüfung: Zweifaches Dispose darf nicht crashen
        var act = () => sut.Dispose();
        act.Should().NotThrow();
    }
}
