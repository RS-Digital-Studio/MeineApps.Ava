using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Multiplayer-Gilden via Firebase Realtime Database.
/// Spieler erstellen/beitreten echte Gilden, arbeiten gemeinsam an Wochenzielen.
/// IncomeBonus wird lokal gecacht für GameLoop/OfflineProgress.
/// </summary>
public sealed class GuildService : IGuildService
{
    private const string PrefKeyPlayerName = "guild_player_name";
    private const int BaseMaxGuildMembers = 20;
    private const long DefaultWeeklyGoal = 500_000;

    private readonly IGameStateService _gameStateService;
    private readonly IFirebaseService _firebaseService;
    private readonly IPreferencesService _preferences;

    public event Action? GuildUpdated;
    public string? PlayerName { get; private set; }
    public bool IsOnline => _firebaseService.IsOnline;

    public GuildService(
        IGameStateService gameStateService,
        IFirebaseService firebaseService,
        IPreferencesService preferences)
    {
        _gameStateService = gameStateService;
        _firebaseService = firebaseService;
        _preferences = preferences;

        // Spielernamen aus Preferences laden
        PlayerName = _preferences.Get<string?>(PrefKeyPlayerName, null);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INIT
    // ═══════════════════════════════════════════════════════════════════════

    public async Task InitializeAsync()
    {
        try
        {
            await _firebaseService.EnsureAuthenticatedAsync();

            // Stabile Spieler-ID initialisieren (überlebt Firebase-Account-Wechsel)
            _firebaseService.InitializePlayerId(_gameStateService.State.PlayerGuid);

            var playerId = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(playerId)) return;

            // PlayerId im GameState als Backup sichern
            if (_gameStateService.State.PlayerGuid != playerId)
            {
                _gameStateService.State.PlayerGuid = playerId;
                _gameStateService.MarkDirty();
            }

            // Migration: Daten von alter Firebase-UID zu PlayerId migrieren
            await MigrateFromUidToPlayerIdAsync(playerId);

            // Prüfen ob Spieler in einer Gilde ist (Schnell-Lookup)
            var guildId = await _firebaseService.GetAsync<string>($"player_guilds/{playerId}");
            if (!string.IsNullOrEmpty(guildId))
            {
                // Gilden-Basisdaten laden für lokalen Cache
                var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
                if (guildData != null)
                {
                    UpdateLocalCache(guildId, guildData);
                }
                else if (_firebaseService.IsOnline)
                {
                    // Gilde existiert definitiv nicht mehr (Online bestätigt)
                    ClearLocalCache();
                    await _firebaseService.DeleteAsync($"player_guilds/{playerId}");
                }
                // else: Offline → lokalen Cache beibehalten
            }
            else if (_firebaseService.IsOnline)
            {
                // Definitiv nicht in einer Gilde (Online bestätigt)
                ClearLocalCache();
                await RegisterAsAvailableAsync();
            }
            // else: Offline → lokalen Cache beibehalten
        }
        catch
        {
            // Offline → lokaler Cache bleibt bestehen
        }

        GuildUpdated?.Invoke();
    }

    /// <summary>
    /// Migriert Spieler-Daten von alter Firebase-UID zu stabiler PlayerId.
    /// Einmalig beim ersten Start nach dem Update.
    /// </summary>
    private async Task MigrateFromUidToPlayerIdAsync(string playerId)
    {
        var firebaseUid = _firebaseService.Uid;
        if (string.IsNullOrEmpty(firebaseUid) || firebaseUid == playerId) return;

        // Schon unter neuer PlayerId vorhanden? → bereits migriert
        var existingGuild = await _firebaseService.GetAsync<string>($"player_guilds/{playerId}");
        if (!string.IsNullOrEmpty(existingGuild)) return;

        // Alte Daten unter Firebase-UID vorhanden?
        var oldGuildId = await _firebaseService.GetAsync<string>($"player_guilds/{firebaseUid}");
        if (string.IsNullOrEmpty(oldGuildId)) return;

        // Gilden-Zuordnung migrieren
        await _firebaseService.SetAsync($"player_guilds/{playerId}", oldGuildId);

        // Mitglieds-Eintrag migrieren
        var memberData = await _firebaseService.GetAsync<FirebaseGuildMember>(
            $"guild_members/{oldGuildId}/{firebaseUid}");
        if (memberData != null)
        {
            await _firebaseService.SetAsync($"guild_members/{oldGuildId}/{playerId}", memberData);
            await _firebaseService.DeleteAsync($"guild_members/{oldGuildId}/{firebaseUid}");
        }

        // Einladungen migrieren
        var invites = await _firebaseService.GetAsync<Dictionary<string, GuildInvitation>>(
            $"player_invites/{firebaseUid}");
        if (invites != null)
        {
            foreach (var (guildId, invite) in invites)
                await _firebaseService.SetAsync($"player_invites/{playerId}/{guildId}", invite);
            await _firebaseService.DeleteAsync($"player_invites/{firebaseUid}");
        }

        // Alte Einträge aufräumen
        await _firebaseService.DeleteAsync($"player_guilds/{firebaseUid}");
        await _firebaseService.DeleteAsync($"available_players/{firebaseUid}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BROWSE
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<GuildListItem>> BrowseGuildsAsync()
    {
        var result = new List<GuildListItem>();

        try
        {
            await _firebaseService.EnsureAuthenticatedAsync();

            // Alle Gilden laden (Firebase REST gibt Dictionary zurück)
            var guildsRaw = await _firebaseService.GetAsync<Dictionary<string, FirebaseGuildData>>("guilds");
            if (guildsRaw == null) return result;

            foreach (var (id, data) in guildsRaw)
            {
                // MaxMembers der jeweiligen Gilde verwenden (nicht eigene Forschungs-Boni)
                if (data.MemberCount >= data.MaxMembers) continue;

                result.Add(new GuildListItem
                {
                    Id = id,
                    Name = data.Name,
                    Icon = data.Icon,
                    Color = data.Color,
                    Level = data.Level,
                    MemberCount = data.MemberCount,
                    WeeklyGoal = data.WeeklyGoal,
                    WeeklyProgress = data.WeeklyProgress
                });
            }

            // Nach Level absteigend, dann MemberCount
            result.Sort((a, b) =>
            {
                var levelCompare = b.Level.CompareTo(a.Level);
                return levelCompare != 0 ? levelCompare : b.MemberCount.CompareTo(a.MemberCount);
            });
        }
        catch
        {
            // Offline
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CREATE
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> CreateGuildAsync(string name, string icon, string color)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || string.IsNullOrWhiteSpace(name)) return false;

            // Schon in einer Gilde?
            var state = _gameStateService.State;
            if (state.GuildMembership != null) return false;

            var now = DateTime.UtcNow;
            var guildId = $"g_{now:yyyyMMddHHmmss}_{uid[..Math.Min(6, uid.Length)]}";

            var guildData = new FirebaseGuildData
            {
                Name = name.Trim(),
                Icon = icon,
                Color = color,
                Level = 1,
                MemberCount = 1,
                WeeklyGoal = DefaultWeeklyGoal,
                WeeklyProgress = 0,
                WeekStartUtc = GetCurrentMonday().ToString("O"),
                CreatedBy = uid,
                CreatedAt = now.ToString("O"),
                MaxMembers = BaseMaxGuildMembers,
                LeagueId = "bronze",
                LeaguePoints = 0,
                HallLevel = 1,
                Description = ""
            };

            // Gilde erstellen
            await _firebaseService.SetAsync($"guilds/{guildId}", guildData);

            // Spieler als Leader eintragen
            var member = new FirebaseGuildMember
            {
                Name = PlayerName ?? "Spieler",
                Role = "leader",
                Contribution = 0,
                PlayerLevel = state.PlayerLevel,
                JoinedAt = now.ToString("O"),
                LastActiveAt = now.ToString("O")
            };
            await _firebaseService.SetAsync($"guild_members/{guildId}/{uid}", member);

            // Schnell-Lookup
            await _firebaseService.SetAsync($"player_guilds/{uid}", guildId);

            // Lokalen Cache aktualisieren
            UpdateLocalCache(guildId, guildData);

            // Verfügbarkeits-Registrierung entfernen (Spieler ist jetzt in einer Gilde)
            await UnregisterAvailableAsync();

            GuildUpdated?.Invoke();

            return true;
        }
        catch
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // JOIN / LEAVE
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> JoinGuildAsync(string guildId)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return false;

            var state = _gameStateService.State;
            if (state.GuildMembership != null) return false;

            // Gilden-Daten prüfen
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null) return false;
            // MaxMembers der Ziel-Gilde verwenden (nicht eigene Forschungs-Boni)
            if (guildData.MemberCount >= guildData.MaxMembers) return false;

            var now = DateTime.UtcNow;

            // Mitglied eintragen
            var member = new FirebaseGuildMember
            {
                Name = PlayerName ?? "Spieler",
                Role = "member",
                Contribution = 0,
                PlayerLevel = state.PlayerLevel,
                JoinedAt = now.ToString("O"),
                LastActiveAt = now.ToString("O")
            };
            await _firebaseService.SetAsync($"guild_members/{guildId}/{uid}", member);

            // MemberCount erhöhen
            await _firebaseService.UpdateAsync($"guilds/{guildId}", new Dictionary<string, object>
            {
                ["memberCount"] = guildData.MemberCount + 1
            });

            // Schnell-Lookup
            await _firebaseService.SetAsync($"player_guilds/{uid}", guildId);

            // Lokalen Cache aktualisieren
            guildData.MemberCount++;
            UpdateLocalCache(guildId, guildData);

            // Aus verfügbaren Spielern entfernen
            await UnregisterAvailableAsync();

            GuildUpdated?.Invoke();

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LeaveGuildAsync()
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return false;

            var state = _gameStateService.State;
            var membership = state.GuildMembership;
            if (membership == null) return false;

            var guildId = membership.GuildId;

            // Mitglied entfernen
            await _firebaseService.DeleteAsync($"guild_members/{guildId}/{uid}");

            // MemberCount verringern
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData != null)
            {
                var newCount = Math.Max(0, guildData.MemberCount - 1);
                if (newCount == 0)
                {
                    // Leere Gilde löschen (Member-Ordner ist bereits leer nach eigenem DELETE)
                    await _firebaseService.DeleteAsync($"guilds/{guildId}");

                    // Invite-Code aufräumen (falls vorhanden)
                    var code = await _firebaseService.GetAsync<string>($"guild_invite_codes/{guildId}");
                    if (!string.IsNullOrEmpty(code))
                    {
                        await _firebaseService.DeleteAsync($"invite_code_to_guild/{code}");
                        await _firebaseService.DeleteAsync($"guild_invite_codes/{guildId}");
                    }
                }
                else
                {
                    await _firebaseService.UpdateAsync($"guilds/{guildId}", new Dictionary<string, object>
                    {
                        ["memberCount"] = newCount
                    });
                }
            }

            // Schnell-Lookup entfernen
            await _firebaseService.DeleteAsync($"player_guilds/{uid}");

            // Lokalen Cache leeren
            ClearLocalCache();
            GuildUpdated?.Invoke();

            return true;
        }
        catch
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONTRIBUTE
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> ContributeAsync(decimal amount)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return false;

            var state = _gameStateService.State;
            var membership = state.GuildMembership;
            if (membership == null || amount <= 0) return false;

            // Spieler muss genug Geld haben
            if (!_gameStateService.TrySpendMoney(amount)) return false;

            var guildId = membership.GuildId;
            var contributionLong = (long)amount;

            // Aktuelle Gilden-Daten laden für atomisches Update
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null) return false;

            // Wochenziel-Fortschritt aktualisieren
            await _firebaseService.UpdateAsync($"guilds/{guildId}", new Dictionary<string, object>
            {
                ["weeklyProgress"] = guildData.WeeklyProgress + contributionLong
            });

            // Spieler-Beitrag aktualisieren
            var memberData = await _firebaseService.GetAsync<FirebaseGuildMember>($"guild_members/{guildId}/{uid}");
            if (memberData != null)
            {
                await _firebaseService.UpdateAsync($"guild_members/{guildId}/{uid}", new Dictionary<string, object>
                {
                    ["contribution"] = memberData.Contribution + contributionLong,
                    ["playerLevel"] = state.PlayerLevel
                });
            }

            _gameStateService.MarkDirty();
            GuildUpdated?.Invoke();

            return true;
        }
        catch
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // REFRESH
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<GuildDetailData?> RefreshGuildDetailsAsync()
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return null;

            var state = _gameStateService.State;
            var membership = state.GuildMembership;
            if (membership == null) return null;

            var guildId = membership.GuildId;

            // Gilden-Daten laden
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null)
            {
                if (_firebaseService.IsOnline)
                {
                    // Gilde existiert definitiv nicht mehr (Online bestätigt)
                    ClearLocalCache();
                    await _firebaseService.DeleteAsync($"player_guilds/{uid}");
                    GuildUpdated?.Invoke();
                }
                return null;
            }

            // Wöchentliches Reset prüfen
            await CheckWeeklyResetAsync(guildId, guildData);

            // Mitglieder laden
            var membersRaw = await _firebaseService.GetAsync<Dictionary<string, FirebaseGuildMember>>($"guild_members/{guildId}");
            var members = new List<GuildMemberInfo>();

            if (membersRaw != null)
            {
                foreach (var (memberUid, memberData) in membersRaw)
                {
                    members.Add(new GuildMemberInfo
                    {
                        Uid = memberUid,
                        Name = memberData.Name,
                        Role = memberData.Role,
                        Contribution = memberData.Contribution,
                        PlayerLevel = memberData.PlayerLevel,
                        IsCurrentPlayer = memberUid == uid
                    });
                }

                // Nach Beitrag sortieren (absteigend)
                members.Sort((a, b) => b.Contribution.CompareTo(a.Contribution));
            }

            // Lokalen Cache aktualisieren
            UpdateLocalCache(guildId, guildData);

            var detail = new GuildDetailData
            {
                Id = guildId,
                Name = guildData.Name,
                Icon = guildData.Icon,
                Color = guildData.Color,
                Level = guildData.Level,
                MemberCount = guildData.MemberCount,
                WeeklyGoal = guildData.WeeklyGoal,
                WeeklyProgress = guildData.WeeklyProgress,
                TotalWeeksCompleted = guildData.TotalWeeksCompleted,
                Members = members
            };

            return detail;
        }
        catch
        {
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INCOME BONUS (LOKAL)
    // ═══════════════════════════════════════════════════════════════════════

    public decimal GetIncomeBonus()
    {
        var membership = _gameStateService.State.GuildMembership;
        return membership?.IncomeBonus ?? 0m;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PLAYER NAME
    // ═══════════════════════════════════════════════════════════════════════

    public void SetPlayerName(string name)
    {
        PlayerName = name.Trim();
        _preferences.Set(PrefKeyPlayerName, PlayerName);

        // GameState synchron halten (für Chat, Friends, Gifts)
        _gameStateService.State.PlayerName = PlayerName;
        _gameStateService.MarkDirty();

        // Firebase-Member-Eintrag asynchron aktualisieren
        _ = UpdatePlayerNameInFirebaseAsync();
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
                new Dictionary<string, object> { ["name"] = PlayerName ?? "Spieler" });
        }
        catch
        {
            // Fire-and-forget: Nächster Refresh aktualisiert den Namen
        }
    }

    /// <summary>
    /// Berechnet max. Gilden-Mitglieder (20 + Forschungs-Boni aus GuildMembership-Cache).
    /// Forschungs-Effekte werden von GuildResearchService gecacht.
    /// </summary>
    public int GetMaxMembers()
    {
        var membership = _gameStateService.State.GuildMembership;
        return BaseMaxGuildMembers + (membership?.ResearchMaxMembersBonus ?? 0);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EINLADUNGS-SYSTEM
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt den Einladungs-Code der aktuellen Gilde zurück.
    /// Erstellt einen 6-stelligen Code bei Bedarf und speichert ihn in Firebase.
    /// Pfade: /guild_invite_codes/{guildId} → Code, /invite_code_to_guild/{code} → GuildId
    /// </summary>
    public async Task<string?> GetOrCreateInviteCodeAsync()
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return null;

            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return null;

            var guildId = membership.GuildId;

            // Bestehenden Code laden
            var existingCode = await _firebaseService.GetAsync<string>($"guild_invite_codes/{guildId}");
            if (!string.IsNullOrEmpty(existingCode))
                return existingCode;

            // Neuen 6-stelligen Code generieren (Kollisionsprüfung)
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
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tritt einer Gilde per 6-stelligem Einladungs-Code bei.
    /// Sucht den Code im /invite_code_to_guild/-Pfad und ruft JoinGuildAsync auf.
    /// </summary>
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

            // Prüfen ob Gilde noch existiert
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null)
                return false;

            return await JoinGuildAsync(guildId);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lädt verfügbare Spieler ohne Gilde (max 50, nach Aktivität sortiert).
    /// Pfad: /available_players/{uid} → { name, level, lastActive }
    /// </summary>
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

            // Nach Aktivität sortieren (neueste zuerst), max 50
            result.Sort((a, b) => string.Compare(b.LastActive, a.LastActive, StringComparison.Ordinal));
            if (result.Count > 50)
                result.RemoveRange(50, result.Count - 50);

            return result;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Registriert den Spieler als verfügbar für Einladungen (wenn gildelos).
    /// Wird automatisch aufgerufen wenn der Spieler keine Gilde hat.
    /// </summary>
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
                Name = PlayerName ?? "Spieler",
                Level = state.PlayerLevel,
                LastActive = DateTime.UtcNow.ToString("O")
            });
        }
        catch
        {
            // Stille Fehlerbehandlung
        }
    }

    /// <summary>
    /// Entfernt die Verfügbarkeits-Registrierung (wird bei Gilden-Beitritt aufgerufen).
    /// </summary>
    public async Task UnregisterAvailableAsync()
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return;

            await _firebaseService.DeleteAsync($"available_players/{uid}");
        }
        catch
        {
            // Stille Fehlerbehandlung
        }
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Span<char> code = stackalloc char[6];
        for (int i = 0; i < 6; i++)
            code[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EINLADUNGS-INBOX
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> SendInviteAsync(string targetUid)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(targetUid)) return false;

            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return false;

            var guildId = membership.GuildId;

            // Gilden-Daten für die Einladung laden
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null) return false;

            var invite = new GuildInvitation
            {
                GuildName = guildData.Name,
                GuildIcon = guildData.Icon,
                GuildColor = guildData.Color,
                GuildLevel = guildData.Level,
                MemberCount = guildData.MemberCount,
                InvitedBy = PlayerName ?? "Spieler",
                InvitedAt = DateTime.UtcNow.ToString("O")
            };

            // Max 10 Einladungen pro Spieler prüfen
            var existing = await _firebaseService.GetAsync<Dictionary<string, GuildInvitation>>(
                $"player_invites/{targetUid}");
            if (existing != null && existing.Count >= 10)
            {
                // Älteste Einladung löschen
                var oldest = existing.OrderBy(e => e.Value.InvitedAt).First();
                await _firebaseService.DeleteAsync($"player_invites/{targetUid}/{oldest.Key}");
            }

            await _firebaseService.SetAsync($"player_invites/{targetUid}/{guildId}", invite);
            return true;
        }
        catch
        {
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
        catch
        {
            return [];
        }
    }

    public async Task<bool> AcceptInviteAsync(string guildId)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return false;

            // Gilde beitreten
            var success = await JoinGuildAsync(guildId);
            if (!success) return false;

            // Alle Einladungen löschen
            await _firebaseService.DeleteAsync($"player_invites/{uid}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeclineInviteAsync(string guildId)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return false;

            await _firebaseService.DeleteAsync($"player_invites/{uid}/{guildId}");
            return true;
        }
        catch
        {
            return false;
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
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(targetUid)) return false;

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
        catch
        {
            return false;
        }
    }

    public async Task<bool> DemoteToMemberAsync(string targetUid)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(targetUid)) return false;

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
        catch
        {
            return false;
        }
    }

    public async Task<bool> KickMemberAsync(string targetUid)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(targetUid)) return false;
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

            // MemberCount verringern
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData != null)
            {
                var newCount = Math.Max(0, guildData.MemberCount - 1);
                await _firebaseService.UpdateAsync($"guilds/{guildId}", new Dictionary<string, object>
                {
                    ["memberCount"] = newCount
                });
            }

            GuildUpdated?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TransferLeadershipAsync(string targetUid)
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(targetUid)) return false;
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
        catch
        {
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
        catch
        {
            // Fire-and-forget: Keine Exceptions werfen
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

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob ein neuer Wochenstart ist und resettet ggf. das Wochenziel.
    /// Verteilt Belohnungen wenn das Ziel erreicht wurde.
    /// </summary>
    private async Task CheckWeeklyResetAsync(string guildId, FirebaseGuildData guildData)
    {
        var currentMonday = GetCurrentMonday();
        var weekStartParsed = DateTime.MinValue;

        if (!string.IsNullOrEmpty(guildData.WeekStartUtc))
        {
            DateTime.TryParse(guildData.WeekStartUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out weekStartParsed);
        }

        if (weekStartParsed >= currentMonday) return;

        // Wochenreset nötig
        var updates = new Dictionary<string, object>
        {
            ["weekStartUtc"] = currentMonday.ToString("O"),
            ["weeklyProgress"] = 0
        };

        // Ziel erreicht? → Level-Up + Belohnung
        if (guildData.WeeklyProgress >= guildData.WeeklyGoal)
        {
            var newLevel = guildData.Level + 1;
            updates["level"] = newLevel;
            updates["totalWeeksCompleted"] = guildData.TotalWeeksCompleted + 1;
            // Neues Wochenziel skaliert mit Level
            updates["weeklyGoal"] = (long)(DefaultWeeklyGoal * (1.0 + newLevel * 0.15));

            // Belohnung: Goldschrauben basierend auf Gilden-Level
            int screwReward = Math.Min(50, 5 + guildData.Level * 2);
            _gameStateService.AddGoldenScrews(screwReward);

            guildData.Level = newLevel;
        }

        await _firebaseService.UpdateAsync($"guilds/{guildId}", updates);

        // Eigenen Beitrag zurücksetzen (Firebase-Rules erlauben nur Schreibzugriff auf eigenen Eintrag)
        var uid = _firebaseService.PlayerId;
        if (!string.IsNullOrEmpty(uid))
        {
            await _firebaseService.UpdateAsync($"guild_members/{guildId}/{uid}", new Dictionary<string, object>
            {
                ["contribution"] = 0
            });
        }

        guildData.WeeklyProgress = 0;
    }

    private void UpdateLocalCache(string guildId, FirebaseGuildData guildData)
    {
        var state = _gameStateService.State;
        var existing = state.GuildMembership;

        // Bestehende Effekt-Caches beibehalten wenn Membership bereits existiert
        if (existing != null && existing.GuildId == guildId)
        {
            existing.GuildName = guildData.Name;
            existing.GuildLevel = guildData.Level;
            existing.GuildIcon = guildData.Icon;
            existing.GuildColor = guildData.Color;
            existing.GuildHallLevel = guildData.HallLevel;
            existing.LeagueId = guildData.LeagueId;
        }
        else
        {
            state.GuildMembership = new GuildMembership
            {
                GuildId = guildId,
                GuildName = guildData.Name,
                GuildLevel = guildData.Level,
                GuildIcon = guildData.Icon,
                GuildColor = guildData.Color,
                GuildHallLevel = guildData.HallLevel,
                LeagueId = guildData.LeagueId
            };
            // Research- und Hall-Effekte werden von den jeweiligen Services gecacht
        }

        _gameStateService.MarkDirty();
    }

    private void ClearLocalCache()
    {
        _gameStateService.State.GuildMembership = null;
        _gameStateService.MarkDirty();
    }

    private static DateTime GetCurrentMonday()
    {
        var today = DateTime.UtcNow.Date;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        return today.AddDays(-diff);
    }
}
