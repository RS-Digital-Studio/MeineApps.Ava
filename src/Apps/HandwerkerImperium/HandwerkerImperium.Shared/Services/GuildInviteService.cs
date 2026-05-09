using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementierung der <see cref="IGuildInviteService"/>: Invite-Codes,
/// "Verfuegbare-Spieler"-Browser und Direkt-Einladungs-Inbox.
/// Beitritts-Operationen (JoinByInviteCode, AcceptInvite) delegieren an
/// <see cref="IGuildService.JoinGuildAsync"/>, damit nur ein Service den globalen
/// Beitritts-Lock und die Integritaets-Checks haelt.
/// </summary>
public sealed class GuildInviteService : IGuildInviteService, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IFirebaseService _firebaseService;
    private readonly IGuildService _guildService;
    private readonly ILogService _log;

    /// <summary>
    /// Eigener Lock fuer die Code-Generierungs-Schleife (Kollisionspruefung).
    /// Verhindert, dass zwei parallele <see cref="GetOrCreateInviteCodeAsync"/>-Aufrufe
    /// denselben Code zweimal anlegen oder zwei verschiedene Codes fuer dieselbe Gilde erzeugen.
    /// Der Beitritts-Lock liegt im GuildService — diesen Service aergert das nicht.
    /// </summary>
    private readonly SemaphoreSlim _inviteLock = new(1, 1);

    public GuildInviteService(
        IGameStateService gameStateService,
        IFirebaseService firebaseService,
        IGuildService guildService,
        ILogService log)
    {
        _gameStateService = gameStateService;
        _firebaseService = firebaseService;
        _guildService = guildService;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INVITE-CODES (6-stellig, bidirektionales Mapping)
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<string?> GetOrCreateInviteCodeAsync()
    {
        var uid = _firebaseService.PlayerId;
        if (string.IsNullOrEmpty(uid)) return null;

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return null;

        if (!await _inviteLock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return null;
        try
        {
            var guildId = membership.GuildId;

            // Bestehenden Code laden
            var existingCode = await _firebaseService.GetAsync<string>($"guild_invite_codes/{guildId}");
            if (!string.IsNullOrEmpty(existingCode))
                return existingCode;

            // Neuen 6-stelligen Code generieren (Kollisionspruefung)
            string code;
            int attempts = 0;
            do
            {
                code = GenerateInviteCode();
                var existing = await _firebaseService.GetAsync<string>($"invite_code_to_guild/{code}");
                if (string.IsNullOrEmpty(existing)) break;
                attempts++;
            } while (attempts < 5);

            // Code speichern (bidirektionales Mapping)
            await _firebaseService.SetAsync($"guild_invite_codes/{guildId}", code);
            await _firebaseService.SetAsync($"invite_code_to_guild/{code}", guildId);

            return code;
        }
        catch (Exception ex)
        {
            _log.Error("Einladungscode erstellen fehlgeschlagen", ex);
            return null;
        }
        finally
        {
            _inviteLock.Release();
        }
    }

    public async Task<bool> JoinByInviteCodeAsync(string code)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
                return false;

            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return false;

            // Code→GuildId Lookup
            var guildId = await _firebaseService.GetAsync<string>(
                $"invite_code_to_guild/{code.ToUpperInvariant()}");
            if (string.IsNullOrEmpty(guildId))
                return false;

            // Pruefen ob Gilde noch existiert
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null)
                return false;

            return await _guildService.JoinGuildAsync(guildId);
        }
        catch (Exception ex)
        {
            _log.Error("Beitritt per Einladungscode fehlgeschlagen", ex);
            return false;
        }
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Span<char> code = stackalloc char[6];
        for (int i = 0; i < 6; i++)
            code[i] = chars[System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, chars.Length)];
        return new string(code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VERFUEGBARE SPIELER (gildelose Spieler)
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<AvailablePlayerInfo>> BrowseAvailablePlayersAsync()
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return [];

            var playersRaw = await _firebaseService.GetAsync<Dictionary<string, AvailablePlayerInfo>>(
                "available_players");

            if (playersRaw == null || playersRaw.Count == 0)
                return [];

            var result = new List<AvailablePlayerInfo>();
            foreach (var (playerUid, info) in playersRaw)
            {
                // Eigenen Spieler ausblenden
                if (playerUid == uid) continue;

                info.Uid = playerUid;
                result.Add(info);
            }

            // Nach Aktivitaet sortieren (neueste zuerst), max 50
            result.Sort((a, b) => string.Compare(b.LastActive, a.LastActive, StringComparison.Ordinal));
            if (result.Count > 50)
                result.RemoveRange(50, result.Count - 50);

            return result;
        }
        catch (Exception ex)
        {
            _log.Error("Verfuegbare Spieler laden fehlgeschlagen", ex);
            return [];
        }
    }

    public async Task RegisterAsAvailableAsync()
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return;

            var state = _gameStateService.State;
            if (state.GuildMembership != null) return; // Hat bereits eine Gilde

            await _firebaseService.SetAsync($"available_players/{uid}", new AvailablePlayerInfo
            {
                Name = _guildService.PlayerName ?? "Player",
                Level = state.PlayerLevel,
                LastActive = DateTime.UtcNow.ToString("O")
            });
        }
        catch (Exception ex)
        {
            _log.Error("Verfuegbarkeits-Registrierung fehlgeschlagen", ex);
        }
    }

    public async Task UnregisterAvailableAsync()
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return;

            await _firebaseService.DeleteAsync($"available_players/{uid}");
        }
        catch (Exception ex)
        {
            _log.Error("Verfuegbarkeits-Abmeldung fehlgeschlagen", ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EINLADUNGS-INBOX (Direkt-Einladungen pro Spieler)
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> SendInviteAsync(string targetUid)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || !FirebaseKeyValidator.IsValid(targetUid)) return false;

            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return false;

            var guildId = membership.GuildId;

            // Gilden-Daten fuer die Einladung laden
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null) return false;

            var invite = new GuildInvitation
            {
                GuildName = guildData.Name,
                GuildIcon = guildData.Icon,
                GuildColor = guildData.Color,
                GuildLevel = guildData.Level,
                MemberCount = guildData.MemberCount,
                InvitedBy = _guildService.PlayerName ?? "Player",
                InvitedAt = DateTime.UtcNow.ToString("O")
            };

            // Max 10 Einladungen pro Spieler pruefen
            var existing = await _firebaseService.GetAsync<Dictionary<string, GuildInvitation>>(
                $"player_invites/{targetUid}");
            if (existing != null && existing.Count >= 10)
            {
                // Aelteste Einladung loeschen
                var oldest = existing.OrderBy(e => e.Value.InvitedAt).First();
                await _firebaseService.DeleteAsync($"player_invites/{targetUid}/{oldest.Key}");
            }

            await _firebaseService.SetAsync($"player_invites/{targetUid}/{guildId}", invite);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Einladung senden fehlgeschlagen", ex);
            return false;
        }
    }

    public async Task<List<(string guildId, GuildInvitation invite)>> GetReceivedInvitesAsync()
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return [];

            await _firebaseService.EnsureAuthenticatedAsync();

            var invitesRaw = await _firebaseService.GetAsync<Dictionary<string, GuildInvitation>>(
                $"player_invites/{uid}");
            if (invitesRaw == null || invitesRaw.Count == 0) return [];

            var result = new List<(string guildId, GuildInvitation invite)>();
            foreach (var (guildId, invite) in invitesRaw)
            {
                result.Add((guildId, invite));
            }

            // Nach Datum sortieren (neueste zuerst)
            result.Sort((a, b) => string.Compare(b.invite.InvitedAt, a.invite.InvitedAt, StringComparison.Ordinal));
            return result;
        }
        catch (Exception ex)
        {
            _log.Error("Einladungen laden fehlgeschlagen", ex);
            return [];
        }
    }

    public async Task<bool> AcceptInviteAsync(string guildId)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || !FirebaseKeyValidator.IsValid(guildId)) return false;

            // Gilde beitreten (delegiert an GuildService — haelt Beitritts-Lock + Integritaets-Checks)
            var success = await _guildService.JoinGuildAsync(guildId);
            if (!success) return false;

            // Alle Einladungen loeschen
            await _firebaseService.DeleteAsync($"player_invites/{uid}");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Einladung annehmen fehlgeschlagen", ex);
            return false;
        }
    }

    public async Task<bool> DeclineInviteAsync(string guildId)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || !FirebaseKeyValidator.IsValid(guildId)) return false;

            await _firebaseService.DeleteAsync($"player_invites/{uid}/{guildId}");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Einladung ablehnen fehlgeschlagen", ex);
            return false;
        }
    }

    public void Dispose()
    {
        _inviteLock.Dispose();
    }
}
