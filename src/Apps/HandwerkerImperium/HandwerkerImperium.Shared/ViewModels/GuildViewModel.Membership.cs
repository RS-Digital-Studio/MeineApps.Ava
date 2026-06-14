using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// GuildViewModel — Mitgliedschaft: Laden, Erstellen, Beitreten, Verlassen, Beitragen.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
public sealed partial class GuildViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Navigation + Laden
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadGuildDataAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            await LoadGuildDataInternalAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// Interne Lade-Logik ohne isBusy-Guard.
    /// Wird von LoadGuildDataAsync (mit Guard) und internen Callern (bereits im isBusy-Kontext) verwendet.
    /// </summary>
    private async Task LoadGuildDataInternalAsync()
    {
        try
        {
            ViewState = GuildViewState.Loading;
            // Spielername prüfen
            if (string.IsNullOrEmpty(_facade.Guild.PlayerName))
            {
                // F-31: Welcome-Hint VOR dem NameDialog explizit pushen (idempotent — wird
                // nur einmal angezeigt). Erklaert dem Spieler, warum ein Name gebraucht wird.
                _contextualHintService?.TryShowHint(ContextualHints.GuildHint);
                ViewState = GuildViewState.NameDialog;
                return;
            }

            await _facade.Guild.InitializeAsync();

            if (!_facade.Guild.IsOnline)
            {
                // GUILD-10: Gecachte Gilden-Boni im Offline-State anzeigen
                UpdateCachedBonusInfo();
                ViewState = GuildViewState.Offline;
                return;
            }

            var membership = _gameStateService.State.GuildMembership;

            if (membership != null)
            {
                // Effekt-Caches parallel laden (sonst 0 bis Sub-Seite geöffnet wird)
                // SafeFireAndForget loggt Netzwerkfehler statt sie still zu verschlucken
                _facade.Research.RefreshResearchCacheAsync().SafeFireAndForget();
                _facade.Hall.RefreshHallCacheAsync().SafeFireAndForget();
                // War-Saison initialisieren (lädt aktive War-ID, cached War, Liga)
                _facade.WarSeason.InitializeAsync().SafeFireAndForget();

                UpdateContributionDisplay();
                await RefreshGuildDetailsAsync();
                ViewState = GuildViewState.InGuild;
            }
            else
            {
                // Spieler als verfügbar registrieren (für Einladungs-Browser)
                await _facade.Invite.RegisterAsAvailableAsync();

                // Einladungs-Inbox laden
                await LoadReceivedInvitesAsync();

                await LoadAvailableGuildsAsync();
                ViewState = GuildViewState.Browse;
            }
        }
        catch (Exception ex)
        {
            // GUILD-10: Gecachte Gilden-Boni auch bei Fehlern anzeigen
            UpdateCachedBonusInfo();

            // Nur "Offline" zeigen wenn Firebase tatsächlich nicht erreichbar ist.
            // Bei anderen Fehlern (JSON-Parsing, Logik) → Browse-State als Fallback.
            if (!_facade.Guild.IsOnline)
            {
                ViewState = GuildViewState.Offline;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Gilden-Fehler (online): {ex.Message}");
                ViewState = GuildViewState.Browse;
            }
        }
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        await LoadGuildDataAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Spielername
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ConfirmNameAsync()
    {
        if (string.IsNullOrWhiteSpace(NameInput)) return;

        _facade.Guild.SetPlayerName(NameInput.Trim());

        // Nach Namenseingabe normal laden
        await LoadGuildDataAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Gilde erstellen
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ShowCreateDialog()
    {
        CreateGuildName = "";
        SelectedIcon = "ShieldHome";
        SelectedColor = "#D97706";
        ViewState = GuildViewState.CreateDialog;
    }

    [RelayCommand]
    private void CancelCreate()
    {
        ViewState = GuildViewState.Browse;
    }

    [RelayCommand]
    private async Task CreateGuildAsync()
    {
        if (string.IsNullOrWhiteSpace(CreateGuildName)) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            ViewState = GuildViewState.Loading;
            var success = await _facade.Guild.CreateGuildAsync(CreateGuildName.Trim(), SelectedIcon, SelectedColor);
            if (success)
            {
                RefreshFromLocalState();
                await RefreshGuildDetailsAsync();
                ViewState = GuildViewState.InGuild;
                CelebrationRequested?.Invoke();

                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    _localizationService.GetString("GuildCreated") ?? "Guild created successfully!");
            }
            else
            {
                ViewState = GuildViewState.Browse;
            }
        }
        catch
        {
            ViewState = GuildViewState.Browse;
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private void SelectIcon(string icon)
    {
        SelectedIcon = icon;
    }

    [RelayCommand]
    private void SelectColor(string color)
    {
        SelectedColor = color;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Beitreten / Verlassen / Beitragen
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task JoinGuildAsync(GuildListItem? item)
    {
        if (item == null) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            ViewState = GuildViewState.Loading;
            var success = await _facade.Guild.JoinGuildAsync(item.Id);
            if (success)
            {
                RefreshFromLocalState();
                await RefreshGuildDetailsAsync();
                ViewState = GuildViewState.InGuild;
                CelebrationRequested?.Invoke();

                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    _localizationService.GetString("GuildJoined") ?? "Joined the guild!");
            }
            else
            {
                ViewState = GuildViewState.Browse;
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    _localizationService.GetString("GuildFull") ?? "Guild is full.");
            }
        }
        catch
        {
            ViewState = GuildViewState.Browse;
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task LeaveGuildAsync()
    {
        if (_isBusy) return;
        var confirmed = await _dialogService.ShowConfirmDialog(
            _localizationService.GetString("LeaveGuildTitle") ?? "Leave Guild",
            _localizationService.GetString("LeaveGuildConfirm") ?? "Do you really want to leave the guild?",
            _localizationService.GetString("Leave") ?? "Leave",
            _localizationService.GetString("Cancel") ?? "Cancel");
        if (!confirmed) return;
        _isBusy = true;
        try
        {
            ViewState = GuildViewState.Loading;
            var success = await _facade.Guild.LeaveGuildAsync();
            if (success)
            {
                Members.Clear();
                RefreshFromLocalState();
                await LoadAvailableGuildsAsync();
                ViewState = GuildViewState.Browse;

                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    _localizationService.GetString("GuildLeft") ?? "Left the guild.");
            }
            else
            {
                ViewState = GuildViewState.InGuild;
            }
        }
        catch
        {
            ViewState = GuildViewState.InGuild;
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task ContributeAsync()
    {
        if (ViewState != GuildViewState.InGuild) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var money = _gameStateService.State.Money;
            var contribution = money * (decimal)ContributionPercent / 100m;
            if (contribution < 100m || money < contribution) return;

            var success = await _facade.Guild.ContributeAsync(contribution);
            if (success)
            {
                // Slider-Anzeige aktualisieren (Geld hat sich geändert)
                UpdateContributionDisplay();

                // Gilden-Daten aktualisieren
                await RefreshGuildDetailsAsync();

                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    string.Format(
                        _localizationService.GetString("GuildContributed") ?? "€{0} beigetragen!",
                        MoneyFormatter.Format(contribution, 0)));
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    partial void OnContributionPercentChanged(double value)
    {
        UpdateContributionDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE METHODS - Mitgliedschaft / Detail-Refresh
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GUILD-10: Aktualisiert die gecachte Gilden-Bonus-Info für den Offline-State.
    /// Liest die lokal gespeicherten Research- und Hall-Boni aus der GuildMembership.
    /// </summary>
    private void UpdateCachedBonusInfo()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null)
        {
            CachedBonusInfo = "";
            return;
        }

        var parts = new List<string>();

        if (membership.IncomeBonus > 0)
            parts.Add($"+{membership.IncomeBonus * 100:F0}% {(_localizationService.GetString("Income") ?? "Income")}");
        if (membership.ResearchCostReduction > 0)
            parts.Add($"-{membership.ResearchCostReduction * 100:F0}% {(_localizationService.GetString("Costs") ?? "Costs")}");
        if (membership.ResearchXpBonus > 0)
            parts.Add($"+{membership.ResearchXpBonus * 100:F0}% XP");
        if (membership.ResearchEfficiencyBonus > 0)
            parts.Add($"+{membership.ResearchEfficiencyBonus * 100:F0}% {(_localizationService.GetString("Efficiency") ?? "Efficiency")}");

        if (parts.Count > 0)
        {
            var bonusLabel = _localizationService.GetString("GuildBonusActiveOffline")
                ?? "Guild bonuses active (offline)";
            CachedBonusInfo = $"{bonusLabel}: {string.Join(", ", parts)}";
        }
        else
        {
            CachedBonusInfo = _localizationService.GetString("GuildBonusActiveOffline")
                ?? "Guild bonuses active (offline)";
        }
    }

    private async Task RefreshGuildDetailsAsync()
    {
        var detail = await _facade.Guild.RefreshGuildDetailsAsync();
        if (detail == null)
        {
            // Gilde existiert nicht mehr
            RefreshFromLocalState();
            ViewState = GuildViewState.Browse;
            return;
        }

        CurrentGuildName = detail.Name;
        CurrentGuildIcon = detail.Icon;
        CurrentGuildColor = detail.Color;
        GuildLevel = detail.Level;
        GoalProgress = detail.WeeklyGoalProgress;
        GoalProgressDisplay = $"{MoneyFormatter.Format(detail.WeeklyProgress, 0)} / {MoneyFormatter.Format(detail.WeeklyGoal, 0)}";

        var bonus = detail.IncomeBonus;
        IncomeBonusDisplay = $"+{bonus * 100:F0}%";
        IncomeBonusDetailDisplay = $"+{bonus * 100:F0}% {(_localizationService.GetString("GuildIncomeBonus") ?? "Income Bonus")}";

        // Mitglieder aktualisieren
        var memberDisplays = new ObservableCollection<GuildMemberDisplay>();
        foreach (var m in detail.Members)
        {
            memberDisplays.Add(new GuildMemberDisplay
            {
                Name = m.Name,
                RoleDisplay = m.Role == "leader"
                    ? (_localizationService.GetString("GuildLeaderRole") ?? "Founder")
                    : (_localizationService.GetString("GuildMemberRole") ?? "Member"),
                ContributionDisplay = MoneyFormatter.Format(m.Contribution, 0),
                IsPlayer = m.IsCurrentPlayer,
                PlayerId = m.Uid // v2.1.0: PlayerId fuer Co-op-Einladungen (Uid IST die PlayerId — siehe GuildService)
            });
        }
        Members = memberDisplays;
        MembersHeaderDisplay = $"{(_localizationService.GetString("Members") ?? "Members")} ({Members.Count})";

        // Gilden-Forschungen laden
        await LoadGuildResearchAsync();

        // Chat + War laden
        await LoadChatMessagesAsync();
        await LoadWarStatusAsync();

        // Einladungs-Code laden (für Invite-Seite)
        var code = await _facade.Invite.GetOrCreateInviteCodeAsync();
        GuildInviteCode = code ?? "";
    }

    private async Task LoadAvailableGuildsAsync()
    {
        var guilds = await _facade.Guild.BrowseGuildsAsync();
        AvailableGuilds = new ObservableCollection<GuildListItem>(guilds);
        HasNoGuilds = AvailableGuilds.Count == 0;
    }

    private void UpdateContributionDisplay()
    {
        var money = _gameStateService.State.Money;
        var amount = money * (decimal)ContributionPercent / 100m;
        ContributionAmountDisplay = $"{MoneyFormatter.Format(amount, 0)} ({ContributionPercent:F0}%)";
    }
}
