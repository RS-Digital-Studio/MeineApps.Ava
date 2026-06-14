using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// GuildViewModel — Kampf/Hub: Gildenkrieg, Quick-Status, Achievements, Sub-Seiten-Navigation.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
public sealed partial class GuildViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Sub-Seiten Navigation
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void NavigateToResearch() => NavigationRequested?.Invoke("guild_research");

    [RelayCommand]
    private void NavigateToMembers() => NavigationRequested?.Invoke("guild_members");

    [RelayCommand]
    private void NavigateToInvite() => NavigationRequested?.Invoke("guild_invite");

    [RelayCommand]
    private void NavigateBack() => NavigationRequested?.Invoke("..");

    [RelayCommand]
    private void NavigateToChat() => NavigationRequested?.Invoke("guild_chat");

    [RelayCommand]
    private void NavigateToWar() => NavigationRequested?.Invoke("guild_war");

    [RelayCommand]
    private void NavigateToWarSeason() => NavigationRequested?.Invoke("guild_war_season");

    [RelayCommand]
    private void NavigateToBoss() => NavigationRequested?.Invoke("guild_boss");

    [RelayCommand]
    private void NavigateToHall() => NavigationRequested?.Invoke("guild_hall");

    [RelayCommand]
    private void NavigateToAchievements() => NavigationRequested?.Invoke("guild_achievements");

    /// <summary>V7 (, Plan Section 3.9): Mega-Projekt-Bauplatz.</summary>
    [RelayCommand]
    private void NavigateToBuildSite() => NavigationRequested?.Invoke("guild_build_site");

    [RelayCommand]
    private void DismissTip()
    {
        HasActiveTip = false;
        _facade.Tip.MarkTipSeen("guild_hub");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Gildenkrieg
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ContributeToWarAsync()
    {
        if (ActiveWar == null || !ActiveWar.IsActive) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            // Beitrag: 10% des aktuellen Spieler-Levels als Score-Punkte
            var level = _gameStateService.PlayerLevel;
            var points = Math.Max(1, level * 10);
            await _facade.WarSeason.ContributeScoreAsync(points, "manual");
            await LoadWarStatusAsync();

            MessageRequested?.Invoke(
                _localizationService.GetString("GuildWarTitle") ?? "Guild War",
                string.Format(
                    _localizationService.GetString("WarContribute") ?? "+{0} Punkte beigetragen!",
                    points));
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// Laedt den aktuellen Kriegs-Status.
    /// </summary>
    public async Task LoadWarStatusAsync()
    {
        try
        {
            var seasonData = await _facade.WarSeason.GetCurrentWarDataAsync();
            var isActive = seasonData != null && !string.IsNullOrEmpty(seasonData.WarId)
                           && !string.IsNullOrEmpty(seasonData.OpponentName);

            // GuildWarDisplayData für die einfache GuildWarView befüllen
            var membership = _gameStateService.State.GuildMembership;
            ActiveWar = seasonData != null ? new GuildWarDisplayData
            {
                OwnGuildName = membership?.GuildName ?? "",
                OpponentGuildName = seasonData.OpponentName ?? "",
                OwnScore = seasonData.OwnScore,
                OpponentScore = seasonData.OpponentScore,
                EndDate = DateTime.TryParse(seasonData.PhaseEndsAt, CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var ed) ? ed : DateTime.UtcNow,
                IsActive = isActive
            } : null;

            HasActiveWar = isActive;

            if (isActive && seasonData != null)
            {
                var remaining = ActiveWar!.EndDate - DateTime.UtcNow;
                WarTimeRemaining = remaining > TimeSpan.Zero
                    ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}min"
                    : (_localizationService.GetString("WarResult") ?? "Result is in");
                WarStatusText = $"{MoneyFormatter.Format(seasonData.OwnScore, 0)} vs {MoneyFormatter.Format(seasonData.OpponentScore, 0)}";
                WarSubtitle = WarStatusText;
            }
            else
            {
                HasActiveWar = false;
                WarSubtitle = _localizationService.GetString("NoActiveWar") ?? "No active war";
            }
        }
        catch
        {
            HasActiveWar = false;
            WarSubtitle = _localizationService.GetString("NoActiveWar") ?? "No active war";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS - Quick-Status (Hub-Übersicht) + Achievements
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert die Quick-Status-Anzeigen im Guild-Hub.
    /// Wird nach dem Laden der Gilden-Daten aufgerufen.
    /// </summary>
    public async Task RefreshQuickStatusAsync()
    {
        try
        {
            // War Quick-Status
            WarQuickStatus = WarSeasonViewModel.GetQuickStatus();

            // Boss Quick-Status
            BossQuickStatus = BossViewModel.GetQuickStatus();

            // Liga Quick-Status
            var league = _facade.WarSeason.GetCurrentLeague();
            LeagueQuickStatus = league switch
            {
                GuildLeague.Bronze => _localizationService.GetString("LeagueBronze") ?? "Bronze League",
                GuildLeague.Silver => _localizationService.GetString("LeagueSilver") ?? "Silver League",
                GuildLeague.Gold => _localizationService.GetString("LeagueGold") ?? "Gold League",
                GuildLeague.Diamond => _localizationService.GetString("LeagueDiamond") ?? "Diamond League",
                _ => "Bronze"
            };

            // Research Quick-Status
            var research = await _facade.Research.GetGuildResearchAsync();
            var completed = research.Count(r => r.IsCompleted);
            ResearchQuickStatus = $"{completed}/{research.Count}";

            // Hall Quick-Status
            HallQuickStatus = HallViewModel.GetQuickStatus();

            // Tipp prüfen
            var tip = _facade.Tip.GetTipForContext("guild_hub");
            HasActiveTip = tip != null;
            ActiveTipText = tip ?? "";
        }
        catch
        {
            // Quick-Status-Fehler sind nicht kritisch
        }
    }

    /// <summary>
    /// Lädt die Gilden-Achievements (für die Achievements-Sub-Seite).
    /// </summary>
    public async Task LoadGuildAchievementsAsync()
    {
        try
        {
            var achievements = await _facade.Achievement.GetAchievementsAsync();
            // RESX-Keys in lokalisierte Texte auflösen
            foreach (var a in achievements)
            {
                a.Name = _localizationService.GetString(a.Name) ?? a.Name;
                a.Description = _localizationService.GetString(a.Description) ?? a.Description;
            }
            GuildAchievements = new ObservableCollection<GuildAchievementDisplay>(achievements);
            HasGuildAchievements = GuildAchievements.Count > 0;
        }
        catch
        {
            // Fehler beim Laden der Achievements ist nicht kritisch
        }
    }

    private void OnGuildAchievementCompleted(GuildAchievementDisplay achievement)
    {
        CelebrationRequested?.Invoke();
        MessageRequested?.Invoke(
            _localizationService.GetString("GuildAchievementUnlocked") ?? "Guild Achievement Unlocked!",
            $"{achievement.Name} (+{achievement.GoldenScrewReward} GS)");
    }
}
