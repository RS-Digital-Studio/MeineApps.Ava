using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models.League;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für die Profil- und Statistik-Seite.
/// Zeigt Spielerprofil (Rahmen, Liga, Währungen) und aggregierte Statistiken.
/// </summary>
public partial class StatisticsViewModel : ObservableObject
{
    private readonly IProgressService _progressService;
    private readonly IHighScoreService _highScoreService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly IAchievementService _achievementService;
    private readonly IDailyChallengeService _dailyChallengeService;
    private readonly IWeeklyChallengeService _weeklyChallengeService;
    private readonly IDailyMissionService _dailyMissionService;
    private readonly ILuckySpinService _luckySpinService;
    private readonly ILeagueService _leagueService;
    private readonly ICustomizationService _customizationService;
    private readonly IDungeonService _dungeonService;
    private readonly ICardService _cardService;
    private readonly ILocalizationService _localizationService;

    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROFIL-HEADER
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _profileTitle = "Profil";

    [ObservableProperty]
    private string _playerName = "Bomber";

    // Liga
    [ObservableProperty]
    private string _leagueTierName = "Bronze";

    [ObservableProperty]
    private string _leagueTierColor = "#CD7F32";

    [ObservableProperty]
    private string _leagueRankText = "#1";

    [ObservableProperty]
    private string _leaguePointsText = "0";

    [ObservableProperty]
    private string _leagueSeasonText = "Saison 1";

    // Rahmen
    [ObservableProperty]
    private string _activeFrameName = "";

    [ObservableProperty]
    private string _frameOuterColor = "Transparent";

    [ObservableProperty]
    private string _frameInnerColor = "Transparent";

    [ObservableProperty]
    private bool _hasActiveFrame;

    // Währungen
    [ObservableProperty]
    private string _coinsText = "0";

    [ObservableProperty]
    private string _gemsText = "0";

    // Schnell-Übersicht
    [ObservableProperty]
    private string _achievementProgressText = "0 / 0";

    [ObservableProperty]
    private int _achievementProgressPercent;

    [ObservableProperty]
    private string _totalStarsOverview = "0 / 300";

    [ObservableProperty]
    private string _cardsOwnedText = "0 / 14";

    [ObservableProperty]
    private string _dungeonBestFloorText = "0";

    // ═══════════════════════════════════════════════════════════════════════
    // TITEL
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _statsTitle = "Statistiken";

    // ═══════════════════════════════════════════════════════════════════════
    // KATEGORIE: FORTSCHRITT (#4CAF50 grün)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _progressLabel = "Fortschritt";

    [ObservableProperty]
    private string _levelsCompletedText = "0 / 100";

    [ObservableProperty]
    private string _levelsCompletedLabel = "Level abgeschlossen";

    [ObservableProperty]
    private string _totalStarsText = "0 / 300";

    [ObservableProperty]
    private string _totalStarsLabel = "Sterne gesammelt";

    [ObservableProperty]
    private string _starsPercentText = "0%";

    [ObservableProperty]
    private string _starsPercentLabel = "Sterne %";

    // ═══════════════════════════════════════════════════════════════════════
    // KATEGORIE: KAMPF (#F44336 rot)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _combatLabel = "Kampf";

    [ObservableProperty]
    private string _totalKillsText = "0";

    [ObservableProperty]
    private string _totalKillsLabel = "Gegner besiegt";

    [ObservableProperty]
    private string _bombsKickedText = "0";

    [ObservableProperty]
    private string _bombsKickedLabel = "Bomben getreten";

    [ObservableProperty]
    private string _powerBombsText = "0";

    [ObservableProperty]
    private string _powerBombsLabel = "Power-Bomben eingesetzt";

    // ═══════════════════════════════════════════════════════════════════════
    // KATEGORIE: HERAUSFORDERUNGEN (#FF9800 orange)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _challengesLabel = "Herausforderungen";

    [ObservableProperty]
    private string _dailyStreakText = "0";

    [ObservableProperty]
    private string _dailyStreakLabel = "Daily Challenge Streak";

    [ObservableProperty]
    private string _longestStreakText = "0";

    [ObservableProperty]
    private string _longestStreakLabel = "Längster Streak";

    [ObservableProperty]
    private string _dailyChallengesCompletedText = "0";

    [ObservableProperty]
    private string _dailyChallengesCompletedLabel = "Daily Challenges abgeschlossen";

    [ObservableProperty]
    private string _weeksCompletedText = "0";

    [ObservableProperty]
    private string _weeksCompletedLabel = "Wochenmissionen abgeschlossen";

    [ObservableProperty]
    private string _dailyMissionDaysText = "0";

    [ObservableProperty]
    private string _dailyMissionDaysLabel = "Tage mit täglichen Missionen";

    // ═══════════════════════════════════════════════════════════════════════
    // KATEGORIE: WIRTSCHAFT (#FFD700 gold)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _economyLabel = "Wirtschaft";

    [ObservableProperty]
    private string _coinBalanceText = "0";

    [ObservableProperty]
    private string _coinBalanceLabel = "Aktuelle Coins";

    [ObservableProperty]
    private string _totalEarnedText = "0";

    [ObservableProperty]
    private string _totalEarnedLabel = "Gesamt verdient";

    [ObservableProperty]
    private string _achievementsText = "0 / 0";

    [ObservableProperty]
    private string _achievementsLabel = "Achievements";

    [ObservableProperty]
    private string _totalSpinsText = "0";

    [ObservableProperty]
    private string _totalSpinsLabel = "Glücksrad-Drehungen";

    // ═══════════════════════════════════════════════════════════════════════
    // KATEGORIE: DUNGEON (#9C27B0 lila)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _dungeonLabel = "Dungeon";

    [ObservableProperty]
    private string _dungeonRunsText = "0";

    [ObservableProperty]
    private string _dungeonRunsLabel = "Dungeon Runs";

    [ObservableProperty]
    private string _dungeonBestText = "0";

    [ObservableProperty]
    private string _dungeonBestLabel = "Bester Floor";

    [ObservableProperty]
    private string _dungeonCoinsText = "0";

    [ObservableProperty]
    private string _dungeonCoinsLabel = "Dungeon Coins";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public StatisticsViewModel(
        IProgressService progressService,
        IHighScoreService highScoreService,
        ICoinService coinService,
        IGemService gemService,
        IAchievementService achievementService,
        IDailyChallengeService dailyChallengeService,
        IWeeklyChallengeService weeklyChallengeService,
        IDailyMissionService dailyMissionService,
        ILuckySpinService luckySpinService,
        ILeagueService leagueService,
        ICustomizationService customizationService,
        IDungeonService dungeonService,
        ICardService cardService,
        ILocalizationService localizationService)
    {
        _progressService = progressService;
        _highScoreService = highScoreService;
        _coinService = coinService;
        _gemService = gemService;
        _achievementService = achievementService;
        _dailyChallengeService = dailyChallengeService;
        _weeklyChallengeService = weeklyChallengeService;
        _dailyMissionService = dailyMissionService;
        _luckySpinService = luckySpinService;
        _leagueService = leagueService;
        _customizationService = customizationService;
        _dungeonService = dungeonService;
        _cardService = cardService;
        _localizationService = localizationService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        UpdateLocalizedTexts();
        UpdateProfileData();
        UpdateStatistics();
    }

    private void UpdateProfileData()
    {
        // Liga
        var tier = _leagueService.CurrentTier;
        LeagueTierName = _localizationService.GetString(tier.GetNameKey()) ?? tier.ToString();
        LeagueTierColor = tier.GetColor();
        LeagueRankText = $"#{_leagueService.GetPlayerRank()}";
        LeaguePointsText = _leagueService.CurrentPoints.ToString("N0");
        LeagueSeasonText = $"{_localizationService.GetString("LeagueSeason") ?? "Saison"} {_leagueService.SeasonNumber}";

        // Rahmen
        var frame = _customizationService.ActiveFrame;
        HasActiveFrame = frame != null && frame.Id != "frame_none";
        if (HasActiveFrame && frame != null)
        {
            ActiveFrameName = _localizationService.GetString(frame.NameKey) ?? frame.Id;
            FrameOuterColor = $"#{frame.PrimaryColor.Red:X2}{frame.PrimaryColor.Green:X2}{frame.PrimaryColor.Blue:X2}";
            FrameInnerColor = $"#{frame.SecondaryColor.Red:X2}{frame.SecondaryColor.Green:X2}{frame.SecondaryColor.Blue:X2}";
        }
        else
        {
            ActiveFrameName = _localizationService.GetString("FrameNone") ?? "Kein Rahmen";
            FrameOuterColor = "Transparent";
            FrameInnerColor = "Transparent";
        }

        // Währungen
        CoinsText = _coinService.Balance.ToString("N0");
        GemsText = _gemService.Balance.ToString("N0");

        // Schnell-Übersicht
        var unlocked = _achievementService.UnlockedCount;
        var total = _achievementService.TotalCount;
        AchievementProgressText = $"{unlocked:N0} / {total:N0}";
        AchievementProgressPercent = total > 0 ? unlocked * 100 / total : 0;

        var stars = _progressService.GetTotalStars();
        TotalStarsOverview = $"{stars:N0} / 300";

        var ownedCards = _cardService.OwnedCards.Count;
        CardsOwnedText = $"{ownedCards} / 14";

        var dungeonStats = _dungeonService.Stats;
        DungeonBestFloorText = dungeonStats.BestFloor.ToString("N0");
    }

    private void UpdateStatistics()
    {
        // Fortschritt
        var completed = _progressService.HighestCompletedLevel;
        var totalLevels = _progressService.TotalLevels;
        var totalStars = _progressService.GetTotalStars();
        const int maxStars = 300;
        var starsPercent = maxStars > 0 ? totalStars * 100 / maxStars : 0;

        LevelsCompletedText = $"{completed:N0} / {totalLevels:N0}";
        TotalStarsText = $"{totalStars:N0} / {maxStars:N0}";
        StarsPercentText = $"{starsPercent}%";

        // Kampf
        TotalKillsText = _achievementService.TotalEnemyKills.ToString("N0");
        BombsKickedText = _achievementService.TotalBombsKicked.ToString("N0");
        PowerBombsText = _achievementService.TotalPowerBombs.ToString("N0");

        // Herausforderungen
        DailyStreakText = _dailyChallengeService.CurrentStreak.ToString("N0");
        LongestStreakText = _dailyChallengeService.LongestStreak.ToString("N0");
        DailyChallengesCompletedText = _dailyChallengeService.TotalCompleted.ToString("N0");
        WeeksCompletedText = _weeklyChallengeService.TotalWeeksCompleted.ToString("N0");
        DailyMissionDaysText = _dailyMissionService.TotalDaysCompleted.ToString("N0");

        // Wirtschaft
        CoinBalanceText = _coinService.Balance.ToString("N0");
        TotalEarnedText = _coinService.TotalEarned.ToString("N0");
        AchievementsText = $"{_achievementService.UnlockedCount:N0} / {_achievementService.TotalCount:N0}";
        TotalSpinsText = _luckySpinService.TotalSpins.ToString("N0");

        // Dungeon
        var ds = _dungeonService.Stats;
        DungeonRunsText = ds.TotalRuns.ToString("N0");
        DungeonBestText = ds.BestFloor.ToString("N0");
        DungeonCoinsText = ds.TotalCoinsEarned.ToString("N0");
    }

    public void UpdateLocalizedTexts()
    {
        ProfileTitle = _localizationService.GetString("ProfileTitle") ?? "Profil";
        StatsTitle = _localizationService.GetString("StatsTitle") ?? "Statistiken";

        // Fortschritt
        ProgressLabel = _localizationService.GetString("StatsProgress") ?? "Fortschritt";
        LevelsCompletedLabel = _localizationService.GetString("StatsLevelsCompleted") ?? "Level abgeschlossen";
        TotalStarsLabel = _localizationService.GetString("StatsTotalStars") ?? "Sterne gesammelt";
        StarsPercentLabel = _localizationService.GetString("StatsStarsPercent") ?? "Sterne %";

        // Kampf
        CombatLabel = _localizationService.GetString("StatsCombat") ?? "Kampf";
        TotalKillsLabel = _localizationService.GetString("StatsTotalKills") ?? "Gegner besiegt";
        BombsKickedLabel = _localizationService.GetString("StatsBombsKicked") ?? "Bomben getreten";
        PowerBombsLabel = _localizationService.GetString("StatsPowerBombs") ?? "Power-Bomben eingesetzt";

        // Herausforderungen
        ChallengesLabel = _localizationService.GetString("StatsChallenges") ?? "Herausforderungen";
        DailyStreakLabel = _localizationService.GetString("StatsDailyStreak") ?? "Daily Challenge Streak";
        LongestStreakLabel = _localizationService.GetString("StatsLongestStreak") ?? "Längster Streak";
        DailyChallengesCompletedLabel = _localizationService.GetString("StatsDailyChallengesCompleted") ?? "Daily Challenges abgeschlossen";
        WeeksCompletedLabel = _localizationService.GetString("StatsWeeksCompleted") ?? "Wochenmissionen abgeschlossen";
        DailyMissionDaysLabel = _localizationService.GetString("StatsDailyMissionDays") ?? "Tage mit täglichen Missionen";

        // Wirtschaft
        EconomyLabel = _localizationService.GetString("StatsEconomy") ?? "Wirtschaft";
        CoinBalanceLabel = _localizationService.GetString("StatsCoinBalance") ?? "Aktuelle Coins";
        TotalEarnedLabel = _localizationService.GetString("StatsTotalEarned") ?? "Gesamt verdient";
        AchievementsLabel = _localizationService.GetString("StatsAchievements") ?? "Achievements";
        TotalSpinsLabel = _localizationService.GetString("StatsTotalSpins") ?? "Glücksrad-Drehungen";

        // Dungeon
        DungeonLabel = _localizationService.GetString("StatsDungeon") ?? "Dungeon";
        DungeonRunsLabel = _localizationService.GetString("StatsDungeonRuns") ?? "Dungeon Runs";
        DungeonBestLabel = _localizationService.GetString("StatsDungeonBest") ?? "Bester Floor";
        DungeonCoinsLabel = _localizationService.GetString("StatsDungeonCoins") ?? "Dungeon Coins";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void Back() => NavigationRequested?.Invoke("..");
}
