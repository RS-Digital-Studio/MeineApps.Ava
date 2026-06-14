using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// GuildViewModel — Einladungs-System: Invite-Code, Spieler-Browser, Einladungs-Inbox.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
public sealed partial class GuildViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Einladungs-Code-Eingabe (Browse-State)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ToggleInviteCodeInput()
    {
        IsInviteCodeInputVisible = !IsInviteCodeInputVisible;
        if (IsInviteCodeInputVisible)
            JoinCodeInput = "";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Einladungs-System
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ShareInviteCode()
    {
        if (string.IsNullOrEmpty(GuildInviteCode)) return;

        var shareText = $"{(_localizationService.GetString("InviteCode") ?? "Invite Code")}: {GuildInviteCode}";
        UriLauncher.ShareText(shareText, _localizationService.GetString("ShareCode") ?? "Share Code");

        MessageRequested?.Invoke(
            _localizationService.GetString("Guild") ?? "Guild",
            $"{(_localizationService.GetString("CodeCopied") ?? "Code copied")}: {GuildInviteCode}");
    }

    [RelayCommand]
    private async Task JoinByCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(JoinCodeInput)) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var success = await _facade.Invite.JoinByInviteCodeAsync(JoinCodeInput.Trim());
            if (success)
            {
                JoinCodeInput = "";
                CelebrationRequested?.Invoke();
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    _localizationService.GetString("GuildJoined") ?? "Joined the guild!");
                await LoadGuildDataInternalAsync();
            }
            else
            {
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    _localizationService.GetString("GuildCodeInvalid") ?? "Invalid code");
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadAvailablePlayersAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var players = await _facade.Invite.BrowseAvailablePlayersAsync();
            var inviteText = _localizationService.GetString("InvitePlayer") ?? "Invite Player";
            var invitedText = _localizationService.GetString("InvitedBadge") ?? "Invited";
            var displays = new ObservableCollection<AvailablePlayerDisplay>();
            foreach (var p in players)
            {
                displays.Add(new AvailablePlayerDisplay
                {
                    Uid = p.Uid,
                    Name = p.Name,
                    LevelDisplay = $"Lv.{p.Level}",
                    LastActiveDisplay = FormatLastActive(p.LastActive),
                    IsInvited = false,
                    InviteButtonText = inviteText,
                    InvitedText = invitedText
                });
            }
            AvailablePlayers = displays;
            HasNoAvailablePlayers = displays.Count == 0;
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task InvitePlayerAsync(AvailablePlayerDisplay? player)
    {
        if (player == null || player.IsInvited) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            // Direkte Einladung an den Spieler senden (Firebase-basiert)
            var success = await _facade.Invite.SendInviteAsync(player.Uid);
            if (success)
            {
                player.IsInvited = true;
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    _localizationService.GetString("InviteSent") ?? "Invite sent");
            }
            else
            {
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    _localizationService.GetString("InviteFailed") ?? "Invite failed");
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Einladungs-Inbox
    // ═══════════════════════════════════════════════════════════════════════

    private async Task LoadReceivedInvitesAsync()
    {
        try
        {
            var invites = await _facade.Invite.GetReceivedInvitesAsync();
            var maxMembers = _facade.Guild.GetMaxMembers();
            var displays = new ObservableCollection<GuildInvitationDisplay>();
            foreach (var (guildId, invite) in invites)
            {
                displays.Add(new GuildInvitationDisplay
                {
                    GuildId = guildId,
                    GuildName = invite.GuildName,
                    GuildIcon = invite.GuildIcon,
                    GuildColor = invite.GuildColor,
                    GuildLevel = invite.GuildLevel,
                    MemberDisplay = $"{invite.MemberCount}/{maxMembers}",
                    InvitedByDisplay = $"{_localizationService.GetString("InvitedByPrefix") ?? "Invited by:"} {invite.InvitedBy}"
                });
            }
            ReceivedInvites = displays;
            HasReceivedInvites = displays.Count > 0;
            ReceivedInviteCount = displays.Count;
        }
        catch
        {
            ReceivedInvites = [];
            HasReceivedInvites = false;
            ReceivedInviteCount = 0;
        }
    }

    [RelayCommand]
    private async Task AcceptInviteAsync(GuildInvitationDisplay? invite)
    {
        if (invite == null || _isBusy) return;
        _isBusy = true;
        try
        {
            var success = await _facade.Invite.AcceptInviteAsync(invite.GuildId);
            if (success)
            {
                CelebrationRequested?.Invoke();
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    _localizationService.GetString("GuildJoined") ?? "Joined the guild!");
                await LoadGuildDataInternalAsync();
            }
            else
            {
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Guild",
                    _localizationService.GetString("GuildFull") ?? "Guild is full.");
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeclineInviteAsync(GuildInvitationDisplay? invite)
    {
        if (invite == null || _isBusy) return;
        _isBusy = true;
        try
        {
            await _facade.Invite.DeclineInviteAsync(invite.GuildId);
            ReceivedInvites.Remove(invite);
            HasReceivedInvites = ReceivedInvites.Count > 0;
            ReceivedInviteCount = ReceivedInvites.Count;
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// Formatiert den LastActive-Zeitstempel als lesbaren relativen Text.
    /// </summary>
    private string FormatLastActive(string lastActiveIso)
    {
        if (string.IsNullOrEmpty(lastActiveIso)) return "";

        if (!DateTime.TryParse(lastActiveIso, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var lastActive))
            return "";

        var diff = DateTime.UtcNow - lastActive;

        if (diff.TotalHours < 24)
            return _localizationService.GetString("Today") ?? "Today";
        if (diff.TotalHours < 48)
            return _localizationService.GetString("Yesterday") ?? "Yesterday";

        var days = (int)diff.TotalDays;
        var template = _localizationService.GetString("DaysAgo") ?? "{0} days ago";
        return string.Format(template, days);
    }
}
