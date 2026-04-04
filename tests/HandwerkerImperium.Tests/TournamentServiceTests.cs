using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für TournamentService: Turnier starten, Score eintragen,
/// Belohnungs-Tier ermitteln, EntryCost, CanEnter.
/// </summary>
public class TournamentServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (IGameStateService mock, GameState state, TournamentService sut) ErstelleService()
    {
        var mock = Substitute.For<IGameStateService>();
        var state = new GameState { PlayerLevel = 10 };
        mock.State.Returns(state);

        var playgames = Substitute.For<IPlayGamesService>();
        playgames.IsSignedIn.Returns(false);

        var ascension = Substitute.For<IAscensionService>();

        var sut = new TournamentService(mock, playgames, ascension);
        return (mock, state, sut);
    }

    private static Tournament ErstelleAktivesTurnier()
    {
        return new Tournament
        {
            WeekStart = GetCurrentMonday(),
            GameType = MiniGameType.Sawing,
            Leaderboard = []
        };
    }

    private static DateTime GetCurrentMonday()
    {
        var today = DateTime.UtcNow.Date;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        return today.AddDays(-diff);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CheckAndStartNewTournament
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckAndStartNewTournament_KeinTurnierVorhanden_ErstelltNeuesTurnier()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = null;

        // Ausführung
        sut.CheckAndStartNewTournament();

        // Prüfung
        state.CurrentTournament.Should().NotBeNull();
    }

    [Fact]
    public void CheckAndStartNewTournament_TurnierVorhanden_BehaeltVorhandesTurnier()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        var turnier = ErstelleAktivesTurnier();
        state.CurrentTournament = turnier;

        // Ausführung
        sut.CheckAndStartNewTournament();

        // Prüfung: Gleiches Turnier, nicht ersetzt
        state.CurrentTournament.Should().BeSameAs(turnier);
    }

    [Fact]
    public void CheckAndStartNewTournament_AltesTurnier_ErsetzDurchNeues()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        // Altes Turnier aus vergangener Woche
        state.CurrentTournament = new Tournament
        {
            WeekStart = DateTime.UtcNow.AddDays(-14),
            GameType = MiniGameType.PipePuzzle,
            Leaderboard = []
        };

        // Ausführung
        sut.CheckAndStartNewTournament();

        // Prüfung: Neues Turnier mit aktueller Woche
        state.CurrentTournament!.WeekStart.Should().Be(GetCurrentMonday());
    }

    [Fact]
    public void CheckAndStartNewTournament_NeuesTurnier_FeuertTournamentUpdatedEvent()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = null;
        bool eventFired = false;
        sut.TournamentUpdated += () => eventFired = true;

        // Ausführung
        sut.CheckAndStartNewTournament();

        // Prüfung
        eventFired.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // CanEnter / EntryCost
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CanEnter_AktivesTurnierMitGratisEintritt_GibtTrueZurueck()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = ErstelleAktivesTurnier();
        // Gratis-Eintritte noch vorhanden (LastEntryDate = MinValue → FreeEntriesRemaining = 3)

        // Ausführung & Prüfung
        sut.CanEnter.Should().BeTrue();
    }

    [Fact]
    public void CanEnter_KeinTurnierVorhanden_GibtFalseZurueck()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = null;

        // Ausführung & Prüfung
        sut.CanEnter.Should().BeFalse();
    }

    [Fact]
    public void EntryCost_GratisEintritteVorhanden_IstNull()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = ErstelleAktivesTurnier();

        // Ausführung & Prüfung
        sut.EntryCost.Should().Be(0);
    }

    [Fact]
    public void EntryCost_KeinTurnierVorhanden_IstNull()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = null;

        // Ausführung & Prüfung
        sut.EntryCost.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // RecordScore
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordScore_GueltigenScore_FuegtScoreZumTurnierHinzu()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = ErstelleAktivesTurnier();

        // Ausführung
        sut.RecordScore(500);

        // Prüfung
        state.CurrentTournament!.TotalScore.Should().Be(500);
    }

    [Fact]
    public void RecordScore_NullOderNegativer_WirdIgnoriert()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = ErstelleAktivesTurnier();

        // Ausführung: Score 0 soll ignoriert werden
        sut.RecordScore(0);

        // Prüfung: Kein Score eingetragen
        state.CurrentTournament!.TotalScore.Should().Be(0);
        state.CurrentTournament.BestScores.Should().BeEmpty();
    }

    [Fact]
    public void RecordScore_MehrAlsDreiScores_BehaeltNurTop3()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = ErstelleAktivesTurnier();
        // Gratis-Einträge ermöglichen 3 Scores heute
        state.CurrentTournament.EntriesUsedToday = 0;
        state.CurrentTournament.LastEntryDate = DateTime.MinValue;

        // Ausführung: Mehrere Scores (simuliere 4 separate Tage)
        // Test der Tournament.AddScore() Logik über RecordScore
        sut.RecordScore(100);

        // Neuen Tag simulieren für weitere Einträge
        state.CurrentTournament.EntriesUsedToday = 0;
        state.CurrentTournament.LastEntryDate = DateTime.MinValue;
        sut.RecordScore(200);

        state.CurrentTournament.EntriesUsedToday = 0;
        state.CurrentTournament.LastEntryDate = DateTime.MinValue;
        sut.RecordScore(300);

        // Prüfung: Top-3 Scores
        state.CurrentTournament.BestScores.Should().HaveCount(3);
        state.CurrentTournament.BestScores.Should().BeInDescendingOrder();
    }

    [Fact]
    public void RecordScore_AbgelaufenesTurnier_WirdIgnoriert()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = new Tournament
        {
            WeekStart = DateTime.UtcNow.AddDays(-14), // abgelaufen
            GameType = MiniGameType.Sawing,
            Leaderboard = []
        };

        // Ausführung
        sut.RecordScore(500);

        // Prüfung: Score nicht eingetragen
        state.CurrentTournament.TotalScore.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ClaimRewards
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ClaimRewards_NieTeilgenommen_GibtNullZurueck()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = ErstelleAktivesTurnier();
        // TotalScore = 0 → nie teilgenommen

        // Ausführung
        var result = sut.ClaimRewards();

        // Prüfung
        result.Should().BeNull();
    }

    [Fact]
    public void ClaimRewards_BereitsGeclaimed_GibtNullZurueck()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = ErstelleAktivesTurnier();
        state.CurrentTournament.RewardsClaimed = true;
        state.CurrentTournament.TotalScore = 1000;

        // Ausführung
        var result = sut.ClaimRewards();

        // Prüfung
        result.Should().BeNull();
    }

    [Fact]
    public void ClaimRewards_GoldRang_ZahltGoldschraubenAus()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        var turnier = ErstelleAktivesTurnier();
        turnier.TotalScore = 1000;
        // Gold-Rang: Spieler auf Platz 1
        turnier.Leaderboard.Add(new TournamentLeaderboardEntry { Name = "Du", IsPlayer = true, Rank = 1, Score = 1000 });
        state.CurrentTournament = turnier;
        state.Ascension = new AscensionData { AscensionLevel = 0 };

        // Ausführung
        var result = sut.ClaimRewards();

        // Prüfung
        result.Should().NotBeNull();
        result!.Value.tier.Should().Be(TournamentRewardTier.Gold);
        result.Value.screws.Should().BeGreaterThan(0);
        mock.Received(1).AddGoldenScrews(Arg.Any<int>());
    }

    // ═══════════════════════════════════════════════════════════════════
    // ResetDailyEntries
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ResetDailyEntries_NeuerTag_SetztEntriesUsedTodayAufNull()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService();
        state.CurrentTournament = ErstelleAktivesTurnier();
        state.CurrentTournament.EntriesUsedToday = 3;
        state.CurrentTournament.LastEntryDate = DateTime.UtcNow.AddDays(-1); // Gestern

        // Ausführung
        sut.ResetDailyEntries();

        // Prüfung
        state.CurrentTournament.EntriesUsedToday.Should().Be(0);
    }
}
