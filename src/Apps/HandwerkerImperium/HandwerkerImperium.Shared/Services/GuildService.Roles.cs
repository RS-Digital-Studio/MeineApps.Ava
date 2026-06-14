using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services;

/// <summary>
/// GuildService — Rollen-Management (Promote/Demote/Kick/Transfer), Keep-Alive, Spielername.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
public sealed partial class GuildService
{
    // ═══════════════════════════════════════════════════════════════════════
    // PLAYER NAME
    // ═══════════════════════════════════════════════════════════════════════

    public void SetPlayerName(string name)
    {
        // Sicherheit: Name trimmen und auf max. 30 Zeichen begrenzen
        name = name.Trim();

        // Zero-Width-Characters und Format-Zeichen entfernen
        name = new string(name.Where(c =>
            !char.IsControl(c) &&
            char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format
        ).ToArray()).Trim();

        // Play-Store-Compliance: Obszoene Namen maskieren (6 Sprachen via ProfanityFilter)
        name = ProfanityFilter.Clean(name);

        if (name.Length > 30) name = name[..30];
        if (string.IsNullOrWhiteSpace(name)) return;

        PlayerName = name;
        _preferences.Set(PrefKeyPlayerName, PlayerName);

        // GameState synchron halten (für Chat, Friends, Gifts)
        _gameStateService.State.PlayerName = PlayerName;

        // Firebase-Member-Eintrag asynchron aktualisieren
        UpdatePlayerNameInFirebaseAsync().SafeFireAndForget();
    }

    private async Task UpdatePlayerNameInFirebaseAsync()
    {
        try
        {
            var playerId = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(playerId)) return;

            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return;

            await _firebaseService.UpdateAsync(
                $"guild_members/{membership.GuildId}/{playerId}",
                new Dictionary<string, object> { ["name"] = PlayerName ?? "Player" });
        }
        catch (Exception ex)
        {
            _log.Error("Spielername in Firebase aktualisieren fehlgeschlagen", ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ROLLEN-MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> PromoteToOfficerAsync(string targetUid)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || !FirebaseKeyValidator.IsValid(targetUid)) return false;

            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return false;

            // Nur Leader darf befördern
            var myRole = await GetMemberRoleAsync(uid);
            if (myRole != GuildRole.Leader) return false;

            var guildId = membership.GuildId;
            await _firebaseService.UpdateAsync($"guild_members/{guildId}/{targetUid}", new Dictionary<string, object>
            {
                ["role"] = "officer"
            });

            GuildUpdated?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Beförderung zum Officer fehlgeschlagen", ex);
            return false;
        }
    }

    public async Task<bool> DemoteToMemberAsync(string targetUid)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || !FirebaseKeyValidator.IsValid(targetUid)) return false;

            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return false;

            // Nur Leader darf degradieren
            var myRole = await GetMemberRoleAsync(uid);
            if (myRole != GuildRole.Leader) return false;

            var guildId = membership.GuildId;
            await _firebaseService.UpdateAsync($"guild_members/{guildId}/{targetUid}", new Dictionary<string, object>
            {
                ["role"] = "member"
            });

            GuildUpdated?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Degradierung zum Member fehlgeschlagen", ex);
            return false;
        }
    }

    public async Task<bool> KickMemberAsync(string targetUid)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || !FirebaseKeyValidator.IsValid(targetUid)) return false;
            if (uid == targetUid) return false; // Sich selbst kann man nicht kicken

            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return false;

            var guildId = membership.GuildId;

            // Rollen-Check: Leader darf alle kicken, Officer nur Members
            var myRole = await GetMemberRoleAsync(uid);
            if (myRole == GuildRole.Member) return false;

            var targetRole = await GetMemberRoleAsync(targetUid);
            if (myRole == GuildRole.Officer && targetRole != GuildRole.Member) return false;

            // Mitglied aus guild_members entfernen
            await _firebaseService.DeleteAsync($"guild_members/{guildId}/{targetUid}");

            // Schnell-Lookup des Ziels entfernen
            await _firebaseService.DeleteAsync($"player_guilds/{targetUid}");

            // Boss-Damage aufräumen (gekicktes Mitglied)
            try { await _firebaseService.DeleteAsync($"guild_boss_damage/{guildId}/{targetUid}"); } catch { /* Nicht-kritisch */ }

            // MemberCount aus tatsächlicher Mitgliederzahl synchronisieren
            await CountAndSyncMemberCountAsync(guildId);

            GuildUpdated?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Mitglied kicken fehlgeschlagen", ex);
            return false;
        }
    }

    public async Task<bool> TransferLeadershipAsync(string targetUid)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || !FirebaseKeyValidator.IsValid(targetUid)) return false;
            if (uid == targetUid) return false; // Keine Übertragung an sich selbst

            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return false;

            // Nur Leader darf Führung übertragen
            var myRole = await GetMemberRoleAsync(uid);
            if (myRole != GuildRole.Leader) return false;

            var guildId = membership.GuildId;

            // Ziel zum Leader machen
            await _firebaseService.UpdateAsync($"guild_members/{guildId}/{targetUid}", new Dictionary<string, object>
            {
                ["role"] = "leader"
            });

            // Sich selbst zum Officer degradieren
            await _firebaseService.UpdateAsync($"guild_members/{guildId}/{uid}", new Dictionary<string, object>
            {
                ["role"] = "officer"
            });

            GuildUpdated?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Führung übertragen fehlgeschlagen", ex);
            return false;
        }
    }

    public async Task UpdateLastActiveAsync()
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return;

            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return;

            await _firebaseService.UpdateAsync(
                $"guild_members/{membership.GuildId}/{uid}",
                new Dictionary<string, object>
                {
                    ["lastActiveAt"] = DateTime.UtcNow.ToString("O")
                });
        }
        catch (Exception ex)
        {
            _log.Error("LastActive aktualisieren fehlgeschlagen", ex);
        }
    }

    /// <summary>
    /// Liest die Rolle eines Mitglieds aus Firebase.
    /// Fallback auf Member wenn nicht gefunden.
    /// </summary>
    private async Task<GuildRole> GetMemberRoleAsync(string uid)
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return GuildRole.Member;

        var roleStr = await _firebaseService.GetAsync<string>(
            $"guild_members/{membership.GuildId}/{uid}/role");

        return roleStr switch
        {
            "leader" => GuildRole.Leader,
            "officer" => GuildRole.Officer,
            _ => GuildRole.Member
        };
    }
}
