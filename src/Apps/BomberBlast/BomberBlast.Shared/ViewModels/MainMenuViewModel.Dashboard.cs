using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models.League;
using BomberBlast.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// Dashboard-Partial des MainMenuViewModel (v2.0.43, Menu-Redesign).
///
/// Aggregiert die Daten der drei Haupt-Bereiche des neuen Dashboard-Layouts:
/// <list type="bullet">
///   <item><b>Hero-Section</b> — laufender Story-Fortschritt (Welt/Stage/Stars).</item>
///   <item><b>Modi-Strip</b> — 5 sekundaere Modi (Survival/QuickPlay/Dungeon/Master/BossRush) mit Unlock-Sichtbarkeit.</item>
///   <item><b>HEUTE-Panel</b> — 5 Cards (DailyReward/DailyChallenge/Missions/LuckySpin/RotatingDeals).</item>
///   <item><b>KARRIERE-Panel</b> — 5 Cards (Liga/BattlePass/Deck/Sammlung/Sterne).</item>
///   <item><b>Saison-Banner</b> — aktives Event mit Restdauer.</item>
/// </list>
///
/// <para>Loest <see cref="DailyHubViewModel"/> ab — Inhalte werden direkt im MainMenu sichtbar,
/// keine zusaetzliche Hub-View mehr noetig.</para>
/// </summary>
public sealed partial class MainMenuViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // HERO-SECTION (Story-Fortschritt prominent)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>z. B. "Welt 5 — Stage 47" oder "Bereit zum Start"</summary>
    [ObservableProperty] private string _heroTitleText = "";

    /// <summary>z. B. "★★☆" (3 Sterne fuer letztes Level oder fuer naechstes)</summary>
    [ObservableProperty] private string _heroStarsText = "";

    /// <summary>"WEITER SPIELEN" oder "JETZT STARTEN"</summary>
    [ObservableProperty] private string _heroPrimaryButtonText = "";

    /// <summary>"Level wählen" — sekundaere Aktion</summary>
    [ObservableProperty] private string _heroSecondaryButtonText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // MODI-STRIP (5 sekundaere Modi-Tiles)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty] private bool _isMasterModeUnlocked;
    [ObservableProperty] private bool _isBossRushUnlocked;

    /// <summary>Master-Mode-Status-Text z. B. "12/100 ★"</summary>
    [ObservableProperty] private string _masterModeStatusText = "";

    /// <summary>Boss-Rush-Wochen-Best als Text "W-Best: 12.345"</summary>
    [ObservableProperty] private string _bossRushStatusText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // HEUTE-PANEL (5 Cards)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty] private string _todayPanelTitle = "";

    // Card 1: Daily Reward
    [ObservableProperty] private bool _todayRewardClaimable;
    [ObservableProperty] private string _todayRewardLabel = "";
    [ObservableProperty] private string _todayRewardSubtitle = "";

    // Card 2: Daily Challenge
    [ObservableProperty] private bool _todayChallengeAvailable;
    [ObservableProperty] private string _todayChallengeLabel = "";
    [ObservableProperty] private string _todayChallengeSubtitle = "";

    // Card 3: Daily Missions
    [ObservableProperty] private bool _todayMissionsAvailable;
    [ObservableProperty] private int _todayMissionsCompleted;
    [ObservableProperty] private int _todayMissionsTotal = 3;
    [ObservableProperty] private string _todayMissionsLabel = "";
    [ObservableProperty] private string _todayMissionsSubtitle = "";

    // Card 4: Lucky Spin
    [ObservableProperty] private bool _todaySpinAvailable;
    [ObservableProperty] private string _todaySpinLabel = "";
    [ObservableProperty] private string _todaySpinSubtitle = "";

    // Card 5: Rotating Deals
    [ObservableProperty] private int _todayDealsCount;
    [ObservableProperty] private string _todayDealsLabel = "";
    [ObservableProperty] private string _todayDealsSubtitle = "";

    // ═══════════════════════════════════════════════════════════════════════
    // KARRIERE-PANEL (5 Cards)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty] private string _careerPanelTitle = "";

    // Card 1: Liga
    [ObservableProperty] private string _careerLeagueLabel = "";
    [ObservableProperty] private string _careerLeagueSubtitle = "";
    [ObservableProperty] private string _careerLeagueColor = "#FFFFFF";

    // Card 2: Battle Pass
    [ObservableProperty] private string _careerBattlePassLabel = "";
    [ObservableProperty] private string _careerBattlePassSubtitle = "";

    // Card 3: Deck / Karten
    [ObservableProperty] private string _careerDeckLabel = "";
    [ObservableProperty] private string _careerDeckSubtitle = "";

    // Card 4: Sammlung
    [ObservableProperty] private string _careerCollectionLabel = "";
    [ObservableProperty] private string _careerCollectionSubtitle = "";

    // Card 5: Sterne (Statistik-Einstieg)
    [ObservableProperty] private string _careerStarsLabel = "";
    [ObservableProperty] private string _careerStarsSubtitle = "";

    // ═══════════════════════════════════════════════════════════════════════
    // SAISON-BANNER
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty] private bool _isEventBannerVisible;
    [ObservableProperty] private string _eventBannerName = "";
    [ObservableProperty] private string _eventBannerDescription = "";
    [ObservableProperty] private string _eventBannerAccentColor = "#FF6F00";
    [ObservableProperty] private string _eventBannerDaysLeftText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // AVATAR (TopBar Spieler-Identitaet)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty] private string _avatarColor = "#FFFFFF";
    [ObservableProperty] private string _avatarName = "";

    // ═══════════════════════════════════════════════════════════════════════
    // REFRESH-LOGIK
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aggregations-Refresh aller Dashboard-Bereiche. Wird von OnAppearing aufgerufen
    /// nachdem die Coin/Gem-Anzeige aktualisiert wurde.
    /// </summary>
    private void RefreshDashboard()
    {
        RefreshHero();
        RefreshModiStrip();
        RefreshTodayPanel();
        RefreshCareerPanel();
        RefreshEventBanner();
        RefreshAvatar();
        UpdateDashboardLocalizedTexts();
    }

    /// <summary>
    /// Lokalisierte Labels neu setzen (Sprach-Wechsel + Init).
    /// </summary>
    private void UpdateDashboardLocalizedTexts()
    {
        TodayPanelTitle = _localizationService.GetString("MenuPanelToday") ?? "TODAY";
        CareerPanelTitle = _localizationService.GetString("MenuPanelCareer") ?? "CAREER";
        TodayRewardLabel = _localizationService.GetString("DailyHubReward") ?? "Daily Reward";
        TodayChallengeLabel = _localizationService.GetString("DailyHubChallenge") ?? "Daily Challenge";
        TodayMissionsLabel = _localizationService.GetString("DailyHubMissions") ?? "Daily Missions";
        TodaySpinLabel = _localizationService.GetString("DailyHubSpin") ?? "Lucky Spin";
        TodayDealsLabel = _localizationService.GetString("DailyHubDeals") ?? "Today's Deals";
        CareerLeagueLabel = _localizationService.GetString("LeagueButton") ?? "League";
        CareerBattlePassLabel = _localizationService.GetString("BattlePassButton") ?? "Battle Pass";
        CareerDeckLabel = _localizationService.GetString("DeckButton") ?? "Deck";
        CareerCollectionLabel = _localizationService.GetString("CollectionButton") ?? "Collection";
        CareerStarsLabel = _localizationService.GetString("MenuCareerStars") ?? "Total Stars";
        HeroSecondaryButtonText = _localizationService.GetString("MenuHeroSelectLevel") ?? "Select Level";
    }

    private void RefreshHero()
    {
        int highest = _progressService.HighestCompletedLevel;
        int total = _progressService.TotalLevels;

        if (highest <= 0)
        {
            // Erstinstallation oder direkt nach Reset
            HeroTitleText = _localizationService.GetString("MenuHeroReadyToStart") ?? "Ready to start";
            HeroStarsText = "";
            HeroPrimaryButtonText = _localizationService.GetString("MenuHeroStartNow") ?? "START NOW";
        }
        else if (highest >= total)
        {
            // Alles geclear — Master-Mode-Hinweis oder einfach "Welt 10 — Stage 100"
            int world = _progressService.GetWorldForLevel(total);
            int stage = total;
            HeroTitleText = string.Format(
                _localizationService.GetString("MenuHeroWorldStage") ?? "World {0} — Stage {1}",
                world, stage);
            HeroStarsText = StarString(_progressService.GetLevelStars(total));
            HeroPrimaryButtonText = _localizationService.GetString("MenuHeroReplay") ?? "REPLAY";
        }
        else
        {
            int next = highest + 1;
            int world = _progressService.GetWorldForLevel(next);
            HeroTitleText = string.Format(
                _localizationService.GetString("MenuHeroWorldStage") ?? "World {0} — Stage {1}",
                world, next);
            HeroStarsText = StarString(_progressService.GetLevelStars(next));
            HeroPrimaryButtonText = _localizationService.GetString("MenuHeroContinue") ?? "CONTINUE";
        }
    }

    private static string StarString(int stars)
    {
        // Star = U+2605, Empty Star = U+2606
        return stars switch
        {
            3 => "★★★",
            2 => "★★☆",
            1 => "★☆☆",
            _ => "☆☆☆"
        };
    }

    private void RefreshModiStrip()
    {
        IsMasterModeUnlocked = _masterModeService.IsUnlocked;
        // BossRush-Tile sichtbar ab L20 (Dungeon-Schwelle als Mid-Game-Marker)
        IsBossRushUnlocked = _progressService.HighestCompletedLevel >= 20;

        if (IsMasterModeUnlocked)
        {
            MasterModeStatusText = string.Format(
                _localizationService.GetString("MenuMasterStatus") ?? "{0}/100 ★",
                _masterModeService.TotalMaster3Stars);
        }
        else
        {
            MasterModeStatusText = "";
        }

        if (IsBossRushUnlocked)
        {
            BossRushStatusText = _bossRushService.WeeklyBestScore > 0
                ? string.Format(
                    _localizationService.GetString("MenuBossRushBest") ?? "Best: {0}",
                    _bossRushService.WeeklyBestScore.ToString("N0"))
                : (_localizationService.GetString("MenuBossRushNew") ?? "Weekly");
        }
        else
        {
            BossRushStatusText = "";
        }
    }

    private void RefreshTodayPanel()
    {
        // Daily Reward
        TodayRewardClaimable = _dailyRewardService.IsRewardAvailable;
        TodayRewardSubtitle = TodayRewardClaimable
            ? string.Format(
                _localizationService.GetString("DailyHubRewardDay") ?? "Day {0} of 7",
                _dailyRewardService.CurrentDay)
            : (_localizationService.GetString("DailyHubRewardClaimed") ?? "Already claimed today");

        // Daily Challenge
        TodayChallengeAvailable = !_dailyChallengeService.IsCompletedToday;
        TodayChallengeSubtitle = TodayChallengeAvailable
            ? string.Format(
                _localizationService.GetString("DailyHubChallengeStreak") ?? "Streak: {0}",
                _dailyChallengeService.CurrentStreak)
            : string.Format(
                _localizationService.GetString("DailyHubChallengeBest") ?? "Best: {0}",
                _dailyChallengeService.TodayBestScore);

        // Daily Missions
        TodayMissionsCompleted = _dailyMissionService.CompletedCount;
        TodayMissionsTotal = _dailyMissionService.Missions?.Count ?? 3;
        TodayMissionsAvailable = TodayMissionsCompleted < TodayMissionsTotal;
        TodayMissionsSubtitle = string.Format(
            _localizationService.GetString("DailyHubMissionsProgress") ?? "{0} / {1} done",
            TodayMissionsCompleted, TodayMissionsTotal);

        // Lucky Spin
        TodaySpinAvailable = _luckySpinService.IsFreeSpinAvailable;
        TodaySpinSubtitle = TodaySpinAvailable
            ? (_localizationService.GetString("DailyHubSpinReady") ?? "Free spin available")
            : (_localizationService.GetString("DailyHubSpinUsed") ?? "Used today");

        // Rotating Deals
        TodayDealsCount = _rotatingDealsService.GetTodaysDeals()?.Count ?? 0;
        if (_rotatingDealsService.GetWeeklyDeal() != null) TodayDealsCount += 1;
        TodayDealsSubtitle = string.Format(
            _localizationService.GetString("DailyHubDealsCount") ?? "{0} active offers",
            TodayDealsCount);
    }

    private void RefreshCareerPanel()
    {
        // Liga
        var tier = _leagueService.CurrentTier;
        CareerLeagueColor = tier.GetColor();
        var tierName = _localizationService.GetString(tier.GetNameKey()) ?? tier.ToString();
        int rank = _leagueService.GetPlayerRank();
        CareerLeagueSubtitle = rank > 0
            ? string.Format(
                _localizationService.GetString("MenuCareerLeagueRank") ?? "{0} • #{1}",
                tierName, rank)
            : tierName;

        // Battle Pass
        if (_battlePassService.IsSeasonActive)
        {
            CareerBattlePassSubtitle = string.Format(
                _localizationService.GetString("MenuCareerBattlePassTier") ?? "Tier {0} / 30",
                _battlePassService.CurrentTier + 1);
        }
        else
        {
            CareerBattlePassSubtitle = _localizationService.GetString("MenuCareerBattlePassPaused") ?? "Off-Season";
        }

        // Deck / Karten
        int equipped = 0;
        for (int i = 0; i < _cardService.EquippedSlots.Count; i++)
            if (_cardService.EquippedSlots[i] != BomberBlast.Models.Entities.BombType.Normal) equipped++;
        int maxSlots = _cardService.IsSlot5Unlocked ? 5 : 4;
        CareerDeckSubtitle = string.Format(
            _localizationService.GetString("MenuCareerDeckSlots") ?? "{0} / {1} slots",
            equipped, maxSlots);

        // Sammlung — alle Kategorien aufsummiert
        int totalDiscovered = 0;
        int totalAll = 0;
        foreach (BomberBlast.Models.Collection.CollectionCategory cat in System.Enum.GetValues(typeof(BomberBlast.Models.Collection.CollectionCategory)))
        {
            totalDiscovered += _collectionService.GetDiscoveredCount(cat);
            totalAll += _collectionService.GetTotalCount(cat);
        }
        int percent = totalAll > 0 ? totalDiscovered * 100 / totalAll : 0;
        CareerCollectionSubtitle = string.Format(
            _localizationService.GetString("MenuCareerCollectionProgress") ?? "{0}% discovered",
            percent);

        // Sterne / Statistik-Einstieg
        int totalStars = _progressService.GetTotalStars();
        int maxStars = _progressService.TotalLevels * 3;
        CareerStarsSubtitle = string.Format(
            _localizationService.GetString("MenuCareerStarsProgress") ?? "{0} / {1}",
            totalStars, maxStars);
    }

    private void RefreshEventBanner()
    {
        var ev = _eventService.CurrentEvent;
        IsEventBannerVisible = ev != null;
        if (ev == null) return;

        EventBannerName = _localizationService.GetString(ev.NameKey) ?? ev.Type.ToString();
        EventBannerDescription = _localizationService.GetString(ev.DescriptionKey) ?? "";
        EventBannerAccentColor = ev.AccentColor;
        EventBannerDaysLeftText = FormatEventDaysLeft(ev);
    }

    /// <summary>
    /// Berechnet die Tage bis zum Event-Ende. Beruecksichtigt Jahres-Wechsel-Spans
    /// (z. B. Christmas 22.12. - 02.01.). Liefert leeren String wenn das Event bereits
    /// abgelaufen sein sollte (defensiv, sollte mit IsEventActive nicht eintreten).
    /// </summary>
    private string FormatEventDaysLeft(SeasonalEvent ev)
    {
        var now = DateTime.UtcNow.Date;
        var endYear = now.Year;
        bool spansYearChange = ev.End.month < ev.Start.month
            || (ev.End.month == ev.Start.month && ev.End.day < ev.Start.day);
        if (spansYearChange && now.Month >= ev.Start.month)
        {
            // Wir sind in der ersten Spann-Haelfte (z. B. 28.12. waehrend Christmas) → Ende im naechsten Jahr
            endYear = now.Year + 1;
        }

        DateTime endDate;
        try
        {
            endDate = new DateTime(endYear, ev.End.month, ev.End.day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return "";
        }

        var daysLeft = (int)System.Math.Ceiling((endDate - now).TotalDays);
        if (daysLeft <= 0) return "";
        return string.Format(
            _localizationService.GetString("EventBannerDaysLeft") ?? "{0} days left",
            daysLeft);
    }

    private void RefreshAvatar()
    {
        var skin = _customizationService.PlayerSkin;
        AvatarColor = $"#{skin.PrimaryColor.Red:X2}{skin.PrimaryColor.Green:X2}{skin.PrimaryColor.Blue:X2}";

        // Spielername (falls gesetzt) — sonst Default-Spielername
        AvatarName = string.IsNullOrWhiteSpace(_leagueService.PlayerName)
            ? (_localizationService.GetString("ProfileTitle") ?? "Profile")
            : _leagueService.PlayerName;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DASHBOARD-COMMANDS (Card-Taps)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>HEUTE Card 1: Daily Reward — zeigt das bestehende Popup direkt im MainMenu.</summary>
    [RelayCommand]
    private void OpenDailyReward()
    {
        if (_dailyRewardService.IsRewardAvailable)
        {
            ShowRewardPopup();
        }
    }

    /// <summary>HEUTE Card 5: Rotating Deals — Shop oeffnen (Deals werden dort angezeigt).</summary>
    [RelayCommand]
    private void OpenRotatingDeals()
    {
        MarkFeatureSeen("shop");
        NavigationRequested?.Invoke(new GoShop());
    }

    /// <summary>KARRIERE Card 4: Sammlung — Profile-Hub Sammlung-Tab.</summary>
    [RelayCommand]
    private void OpenCollection()
    {
        MarkFeatureSeen("collection");
        NavigationRequested?.Invoke(new GoCollection());
    }

    /// <summary>KARRIERE Card 5: Sterne / Statistik — Profile-Hub Statistik-Tab.</summary>
    [RelayCommand]
    private void OpenStatistics()
    {
        MarkFeatureSeen("statistics");
        NavigationRequested?.Invoke(new GoStatistics());
    }

    /// <summary>Modi-Strip: Master-Mode — LevelSelect mit aktivem Master-Toggle.</summary>
    [RelayCommand]
    private void OpenMasterMode()
    {
        if (!_masterModeService.IsUnlocked) return;
        _masterModeService.IsActive = true;
        NavigationRequested?.Invoke(new GoLevelSelect());
    }

    /// <summary>Modi-Strip: Boss-Rush — Wochen-Boss-Rush direkt einsteigen.</summary>
    [RelayCommand]
    private void OpenBossRush()
    {
        if (!IsBossRushUnlocked) return;
        NavigationRequested?.Invoke(new GoBossRush());
    }

    /// <summary>Saison-Banner: Tap → LevelSelect (Event-Welt ist Welt 6+ je nach Event).</summary>
    [RelayCommand]
    private void OpenEvent()
    {
        NavigationRequested?.Invoke(new GoLevelSelect());
    }

    /// <summary>TopBar: Coin-Tap → Shop.</summary>
    [RelayCommand]
    private void OpenCoinShop()
    {
        MarkFeatureSeen("shop");
        NavigationRequested?.Invoke(new GoShop());
    }
}
