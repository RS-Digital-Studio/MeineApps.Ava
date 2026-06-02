using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Helpers;
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
public sealed class GuildService : IGuildService, IDisposable
{
    private const string PrefKeyPlayerName = "guild_player_name";
    private const int BaseMaxGuildMembers = 20;
    private const long DefaultWeeklyGoal = 500_000;

    private readonly IGameStateService _gameStateService;
    private readonly IFirebaseService _firebaseService;
    private readonly IGameIntegrityService _integrityService;
    private readonly IPreferencesService _preferences;
    private readonly ILogService _log;
    // FB-H07: Beim Gilden-Verlassen wird der Forschungs-Effekt-Cache invalidiert.
    private readonly IGuildResearchService _guildResearchService;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action? GuildUpdated;
    public string? PlayerName { get; private set; }
    public bool IsOnline => _firebaseService.IsOnline;

    public GuildService(
        IGameStateService gameStateService,
        IFirebaseService firebaseService,
        IGameIntegrityService integrityService,
        IPreferencesService preferences,
        ILogService log,
        IGuildResearchService guildResearchService)
    {
        _gameStateService = gameStateService;
        _firebaseService = firebaseService;
        _integrityService = integrityService;
        _preferences = preferences;
        _log = log;
        _guildResearchService = guildResearchService;

        // Spielernamen aus Preferences laden (mit Längenbegrenzung für Legacy-Daten)
        var savedName = _preferences.Get<string?>(PrefKeyPlayerName, null);
        if (!string.IsNullOrEmpty(savedName) && savedName.Length > 30)
            savedName = savedName[..30];
        PlayerName = savedName;
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

            // auth_to_player Mapping sicherstellen (Security Rules benötigen es für Lese-Zugriff)
            // MUSS awaited werden: guild_members-Read-Rules prüfen auth_to_player/{uid}.
            // Ohne Mapping schlagen alle Gilden-Reads fehl (Permission denied).
            await _firebaseService.SyncAuthToPlayerMappingAsync();

            // PlayerId im GameState als Backup sichern
            if (_gameStateService.State.PlayerGuid != playerId)
            {
                _gameStateService.State.PlayerGuid = playerId;
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
                await RegisterAsAvailableInternalAsync();
            }
            // else: Offline → lokalen Cache beibehalten
        }
        catch (Exception ex)
        {
            _log.Error("Gilden-Initialisierung fehlgeschlagen", ex);
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

        // Mitglieds-Eintrag migrieren — Set neuer + Delete alter Eintrag atomar als
        // Multi-Path-Update. Frueher waren das zwei Operationen; bei Delete-Failure blieb der
        // alte Eintrag als Geister-Member stehen und zaehlte weiter in memberCount.
        var memberData = await _firebaseService.GetAsync<FirebaseGuildMember>(
            $"guild_members/{oldGuildId}/{firebaseUid}");
        if (memberData != null)
        {
            await _firebaseService.UpdateAsync($"guild_members/{oldGuildId}", new Dictionary<string, object>
            {
                [playerId] = memberData,
                [firebaseUid] = null!  // Firebase: null-Wert loescht den Pfad — atomar mit dem Set
            });
        }

        // Einladungen migrieren
        var invites = await _firebaseService.GetAsync<Dictionary<string, GuildInvitation>>(
            $"player_invites/{firebaseUid}");
        if (invites != null)
        {
            foreach (var (guildId, invite) in invites)
                await _firebaseService.SetAsync($"player_invites/{playerId}/{guildId}", invite);
            try { await _firebaseService.DeleteAsync($"player_invites/{firebaseUid}"); } catch { /* Nicht-kritisch */ }
        }

        // Alte Einträge aufräumen (jeweils try/catch, damit ein Fehler nicht den Rest blockiert)
        try { await _firebaseService.DeleteAsync($"player_guilds/{firebaseUid}"); } catch { /* Nicht-kritisch */ }
        try { await _firebaseService.DeleteAsync($"available_players/{firebaseUid}"); } catch { /* Nicht-kritisch */ }
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

            // Maximal 50 Gilden laden (nach Level absteigend, verhindert Überlastung bei vielen Gilden)
            var json = await _firebaseService.QueryAsync("guilds",
                "orderBy=\"level\"&limitToLast=50");

            if (string.IsNullOrEmpty(json) || json == "null")
                return result;

            var guildsRaw = JsonSerializer.Deserialize<Dictionary<string, FirebaseGuildData>>(json);
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
                    MaxMembers = data.MaxMembers,
                    Description = data.Description,
                    LeagueId = data.LeagueId,
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
        catch (Exception ex)
        {
            _log.Error("Gilden-Browse fehlgeschlagen", ex);
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CREATE
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> CreateGuildAsync(string name, string icon, string color)
    {
        var uid = _firebaseService.PlayerId;
        if (string.IsNullOrEmpty(uid) || string.IsNullOrWhiteSpace(name)) return false;

        // Sanitisierung (Play-Store-Compliance fuer User-Generated-Content):
        // 1. Trim + Unicode-Format/Control-Zeichen entfernen (Spoofing-Schutz)
        // 2. ProfanityFilter.Clean (obszoene Namen maskieren, 6 Sprachen)
        // 3. Laengen-Cap 30 Zeichen + Mindestlaenge 2
        name = new string(name.Trim().Where(c =>
            !char.IsControl(c) &&
            char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format
        ).ToArray()).Trim();
        name = ProfanityFilter.Clean(name);
        if (name.Length > 30) name = name[..30];
        if (name.Length < 2) return false;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return false;

        string? createdGuildId = null; // Tracking für Rollback im catch
        try
        {
            // Schon in einer Gilde? (Double-Check nach Lock)
            var state = _gameStateService.State;
            if (state.GuildMembership != null) return false;

            // Integritaetspruefung: Manipulierte Werte nicht an Firebase senden
            if (!VerifyIntegrityForFirebase(state)) return false;

            var now = DateTime.UtcNow;
            var guildId = $"g_{now:yyyyMMddHHmmss}_{uid[..Math.Min(6, uid.Length)]}";

            var guildData = new FirebaseGuildData
            {
                Name = name,
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
            createdGuildId = guildId;

            // Spieler als Leader eintragen
            var member = new FirebaseGuildMember
            {
                Name = PlayerName ?? "Player",
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
            await UnregisterAvailableInternalAsync();

            GuildUpdated?.Invoke();

            return true;
        }
        catch (Exception ex)
        {
            // Best-Effort Rollback: Verwaiste Firebase-Daten bereinigen
            if (createdGuildId != null)
            {
                try { await _firebaseService.DeleteAsync($"guild_members/{createdGuildId}/{uid}"); } catch { /* Best-Effort */ }
                try { await _firebaseService.DeleteAsync($"player_guilds/{uid}"); } catch { /* Best-Effort */ }
                try { await _firebaseService.DeleteAsync($"guilds/{createdGuildId}"); } catch { /* Best-Effort */ }
            }
            _log.Error("Gilde erstellen fehlgeschlagen", ex);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // JOIN / LEAVE
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> JoinGuildAsync(string guildId)
    {
        var uid = _firebaseService.PlayerId;
        if (string.IsNullOrEmpty(uid) || !FirebaseKeyValidator.IsValid(guildId)) return false;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return false;

        var memberWritten = false; // Tracking für Rollback im catch
        try
        {
            // Double-Check nach Lock: Schon in einer Gilde?
            var state = _gameStateService.State;
            if (state.GuildMembership != null) return false;

            // Integritaetspruefung: Manipulierte Werte nicht an Firebase senden
            if (!VerifyIntegrityForFirebase(state)) return false;

            // Gilden-Daten prüfen
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null) return false;

            // Tatsächliche Mitgliederzahl prüfen (Race-Condition-frei)
            var actualCount = await CountAndSyncMemberCountAsync(guildId);
            if (actualCount < 0) return false; // Netzwerkfehler → kein Join möglich
            if (actualCount >= guildData.MaxMembers) return false;

            var now = DateTime.UtcNow;
            var playerName = PlayerName ?? "Player";

            // Mitglied eintragen (ZUERST — damit wir danach als Mitglied Duplikate löschen dürfen)
            var member = new FirebaseGuildMember
            {
                Name = playerName,
                Role = "member",
                Contribution = 0,
                PlayerLevel = state.PlayerLevel,
                JoinedAt = now.ToString("O"),
                LastActiveAt = now.ToString("O")
            };
            await _firebaseService.SetAsync($"guild_members/{guildId}/{uid}", member);
            memberWritten = true;

            // Post-Join-Verification: Tatsächliche Mitgliederzahl prüfen
            // Verhindert Race Condition wenn mehrere Spieler gleichzeitig beitreten
            var newCount = await CountAndSyncMemberCountAsync(guildId);
            if (newCount < 0) newCount = guildData.MemberCount + 1; // Fallback bei Netzwerkfehler

            // Über MaxMembers? → Rollback (eigenen Eintrag wieder löschen)
            if (newCount > guildData.MaxMembers)
            {
                try { await _firebaseService.DeleteAsync($"guild_members/{guildId}/{uid}"); } catch { /* Best-Effort Rollback */ }
                await CountAndSyncMemberCountAsync(guildId); // Count neu synchronisieren
                return false;
            }

            // Schnell-Lookup
            await _firebaseService.SetAsync($"player_guilds/{uid}", guildId);

            // Lokalen Cache aktualisieren
            guildData.MemberCount = newCount;
            UpdateLocalCache(guildId, guildData);

            // Aus verfügbaren Spielern entfernen
            await UnregisterAvailableInternalAsync();

            GuildUpdated?.Invoke();

            return true;
        }
        catch (Exception ex)
        {
            // Best-Effort Rollback: Eigenen Eintrag wieder entfernen wenn bereits geschrieben
            if (memberWritten)
            {
                try { await _firebaseService.DeleteAsync($"guild_members/{guildId}/{uid}"); } catch { /* Best-Effort */ }
                try { await _firebaseService.DeleteAsync($"player_guilds/{uid}"); } catch { /* Best-Effort */ }
            }
            _log.Error("Gilde beitreten fehlgeschlagen", ex);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> LeaveGuildAsync()
    {
        var uid = _firebaseService.PlayerId;
        if (string.IsNullOrEmpty(uid)) return false;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return false;
        try
        {
            var state = _gameStateService.State;
            var membership = state.GuildMembership;
            if (membership == null) return false;

            var guildId = membership.GuildId;

            // Leader-Transfer: Wenn wir Leader sind, Führung übertragen bevor wir gehen
            var myRole = await GetMemberRoleAsync(uid);
            if (myRole == GuildRole.Leader)
            {
                await TransferLeadershipOnLeaveAsync(guildId, uid);
            }

            // Mitglied entfernen
            await _firebaseService.DeleteAsync($"guild_members/{guildId}/{uid}");

            // Boss-Damage aufräumen (eigenen Eintrag löschen)
            try { await _firebaseService.DeleteAsync($"guild_boss_damage/{guildId}/{uid}"); } catch { /* Nicht-kritisch */ }

            // MemberCount aus tatsächlicher Mitgliederzahl synchronisieren
            var newCount = await CountAndSyncMemberCountAsync(guildId);
            // Bei Netzwerkfehler (-1): Gilde NICHT löschen (Safety)
            if (newCount == 0)
            {
                // Leere Gilde komplett aufräumen
                await CleanupDeletedGuildAsync(guildId);
            }

            // Schnell-Lookup entfernen
            await _firebaseService.DeleteAsync($"player_guilds/{uid}");

            // Lokalen Cache leeren
            ClearLocalCache();
            GuildUpdated?.Invoke();

            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Gilde verlassen fehlgeschlagen", ex);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Überträgt die Führung an den ältesten Officer, sonst an das älteste Mitglied.
    /// Wird automatisch aufgerufen wenn der Leader die Gilde verlässt.
    /// </summary>
    private async Task TransferLeadershipOnLeaveAsync(string guildId, string leavingLeaderUid)
    {
        try
        {
            var membersRaw = await _firebaseService.GetAsync<Dictionary<string, FirebaseGuildMember>>(
                $"guild_members/{guildId}");
            if (membersRaw == null || membersRaw.Count <= 1) return; // Keine anderen Mitglieder

            // Kandidaten sortieren: Officers zuerst, dann nach JoinedAt (älteste zuerst)
            string? newLeaderUid = null;
            DateTime oldestJoin = DateTime.MaxValue;
            string? oldestMemberUid = null;
            DateTime oldestOfficerJoin = DateTime.MaxValue;
            string? oldestOfficerUid = null;

            foreach (var (memberUid, memberData) in membersRaw)
            {
                if (memberUid == leavingLeaderUid) continue;

                var joinDate = ParseLastActive(memberData.JoinedAt);

                if (memberData.Role == "officer" && joinDate < oldestOfficerJoin)
                {
                    oldestOfficerJoin = joinDate;
                    oldestOfficerUid = memberUid;
                }

                if (joinDate < oldestJoin)
                {
                    oldestJoin = joinDate;
                    oldestMemberUid = memberUid;
                }
            }

            // Officer hat Vorrang, sonst ältestes Mitglied
            newLeaderUid = oldestOfficerUid ?? oldestMemberUid;
            if (string.IsNullOrEmpty(newLeaderUid)) return;

            await _firebaseService.UpdateAsync($"guild_members/{guildId}/{newLeaderUid}",
                new Dictionary<string, object> { ["role"] = "leader" });
        }
        catch (Exception ex)
        {
            _log.Error("Automatischer Leader-Transfer fehlgeschlagen", ex);
        }
    }

    /// <summary>
    /// Räumt alle Firebase-Daten einer leeren Gilde auf (verwaiste Knoten verhindern).
    /// Wird aufgerufen wenn das letzte Mitglied die Gilde verlässt.
    /// </summary>
    private async Task CleanupDeletedGuildAsync(string guildId)
    {
        // Haupt-Eintrag löschen
        await _firebaseService.DeleteAsync($"guilds/{guildId}");

        // Invite-Code aufräumen (bidirektionales Mapping)
        var code = await _firebaseService.GetAsync<string>($"guild_invite_codes/{guildId}");
        if (!string.IsNullOrEmpty(code))
        {
            try { await _firebaseService.DeleteAsync($"invite_code_to_guild/{code}"); } catch { /* Nicht-kritisch */ }
            try { await _firebaseService.DeleteAsync($"guild_invite_codes/{guildId}"); } catch { /* Nicht-kritisch */ }
        }

        // Verwaiste Daten-Knoten löschen (jeweils try/catch, damit ein Fehler nicht den Rest blockiert)
        try { await _firebaseService.DeleteAsync($"guild_research/{guildId}"); } catch { /* Nicht-kritisch */ }
        try { await _firebaseService.DeleteAsync($"guild_hall/{guildId}"); } catch { /* Nicht-kritisch */ }
        try { await _firebaseService.DeleteAsync($"guild_bosses/{guildId}"); } catch { /* Nicht-kritisch */ }
        try { await _firebaseService.DeleteAsync($"guild_boss_damage/{guildId}"); } catch { /* Nicht-kritisch */ }
        try { await _firebaseService.DeleteAsync($"guild_chat/{guildId}"); } catch { /* Nicht-kritisch */ }
        try { await _firebaseService.DeleteAsync($"guild_achievements/{guildId}"); } catch { /* Nicht-kritisch */ }
        try { await _firebaseService.DeleteAsync($"guild_members/{guildId}"); } catch { /* Nicht-kritisch */ }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONTRIBUTE
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> ContributeAsync(decimal amount)
    {
        var uid = _firebaseService.PlayerId;
        if (string.IsNullOrEmpty(uid)) return false;

        var state = _gameStateService.State;
        var membership = state.GuildMembership;
        if (membership == null || amount <= 0) return false;

        // Integritaetspruefung: Manipulierte Werte nicht an Firebase senden
        if (!VerifyIntegrityForFirebase(state)) return false;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return false; // Timeout: Lock nicht erhalten
        try
        {
            // Wöchentliches Spenden-Cap prüfen (max 30% des Wochenziels pro Spieler)
            var weekKey = GetCurrentMondayUtc().ToString("yyyy-MM-dd");
            var donationPrefKey = $"guild_weekly_donation_{weekKey}_{uid}";
            var alreadyDonated = _preferences.Get(donationPrefKey, 0L);

            // Wochenziel aus Firebase laden (Fallback auf Default)
            var guildDataForCap = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{membership.GuildId}");
            var weeklyGoal = guildDataForCap?.WeeklyGoal ?? DefaultWeeklyGoal;
            var maxDonation = (long)(weeklyGoal * 0.30);
            var remaining = maxDonation - alreadyDonated;
            if (remaining <= 0) return false;

            // Betrag auf verbleibendes Cap begrenzen
            var cappedAmount = Math.Min((long)amount, remaining);
            if (cappedAmount <= 0) return false;
            amount = cappedAmount;

            // Spieler muss genug Geld haben
            if (!_gameStateService.TrySpendMoney(amount)) return false;

            var guildId = membership.GuildId;
            var contributionLong = (long)amount;

            // Aktuelle Gilden-Daten laden für atomisches Update
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null)
            {
                // Rollback: Geld zurückgeben
                _gameStateService.AddMoney(amount);
                return false;
            }

            // Wochenziel-Fortschritt ATOMAR serverseitig erhoehen (statt read-modify-write des
            // geteilten Keys) — sonst ueberschreiben gleichzeitig spendende Mitglieder ihre Beitraege
            // gegenseitig (Last-Write-Wins). Bei Fehler Rollback.
            if (!await _firebaseService.IncrementAsync($"guilds/{guildId}/weeklyProgress", contributionLong))
            {
                _gameStateService.AddMoney(amount);
                return false;
            }

            // Spenden-Tracking aktualisieren (Wöchentliches Cap)
            _preferences.Set(donationPrefKey, alreadyDonated + contributionLong);

            // Spieler-Beitrag aktualisieren (bei Fehler akzeptabel, nur Anzeige-Wert)
            var memberData = await _firebaseService.GetAsync<FirebaseGuildMember>($"guild_members/{guildId}/{uid}");
            if (memberData != null)
            {
                await _firebaseService.UpdateAsync($"guild_members/{guildId}/{uid}", new Dictionary<string, object>
                {
                    ["contribution"] = memberData.Contribution + contributionLong,
                    ["playerLevel"] = state.PlayerLevel
                });
            }

            GuildUpdated?.Invoke();

            return true;
        }
        finally
        {
            _lock.Release();
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

            // Keep-Alive: Eigene LastActiveAt aktualisieren BEVOR die Mitglieder-Liste gelesen wird.
            // Ohne diesen Call wuerde der eigene Eintrag nach 30 Tagen Inaktivitaet vom
            // IsStaleMember-Filter ausgeblendet werden (Spieler sieht sich selbst nicht mehr).
            // Fire-and-forget: Anzeige soll nicht auf Firebase-RTT warten — der folgende
            // Memory-Patch stellt sofortige Korrektheit fuer diesen Refresh-Zyklus sicher.
            UpdateLastActiveAsync().SafeFireAndForget();

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
                // Client-seitige Filterung: Duplikate (gleicher Name) und verwaiste
                // Mitglieder (>30 Tage inaktiv) aus der Anzeige ausblenden.
                // Firebase-Daten bleiben unverändert (nur Leader darf Mitglieder löschen).
                // Der eigene Spieler wird durch expliziten isSelf-Guard in beiden Filtern
                // geschuetzt — kein DTO-Patch noetig (vermeidet stille Seiteneffekte falls
                // FirebaseService je einen Response-Cache einfuehrt).
                var seenNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var duplicateIds = new HashSet<string>();

                foreach (var (memberUid, memberData) in membersRaw)
                {
                    if (seenNames.TryGetValue(memberData.Name, out var existingUid))
                    {
                        // Duplikat: Den mit dem älteren LastActiveAt aus der Anzeige filtern.
                        // ABER: Der eigene Spieler gewinnt immer (Self-Preservation gegen
                        // Name-Kollision mit anderem Spieler oder alter Account-Leiche).
                        bool currentIsSelf = memberUid == uid;
                        bool existingIsSelf = existingUid == uid;

                        if (currentIsSelf)
                        {
                            duplicateIds.Add(existingUid);
                            seenNames[memberData.Name] = memberUid;
                        }
                        else if (existingIsSelf)
                        {
                            duplicateIds.Add(memberUid);
                        }
                        else
                        {
                            var existingActive = ParseLastActive(membersRaw[existingUid].LastActiveAt);
                            var currentActive = ParseLastActive(memberData.LastActiveAt);

                            if (currentActive > existingActive)
                            {
                                duplicateIds.Add(existingUid);
                                seenNames[memberData.Name] = memberUid;
                            }
                            else
                            {
                                duplicateIds.Add(memberUid);
                            }
                        }
                    }
                    else
                    {
                        seenNames[memberData.Name] = memberUid;
                    }
                }

                foreach (var (memberUid, memberData) in membersRaw)
                {
                    // Duplikate und verwaiste Mitglieder aus der Anzeige filtern.
                    // AUSNAHME: Der eigene Spieler wird niemals gefiltert — sonst sieht man
                    // sich selbst nicht in der Mitgliederliste (gemeldeter Bug 2026-04-20).
                    bool isSelf = memberUid == uid;
                    if (!isSelf && duplicateIds.Contains(memberUid)) continue;
                    if (!isSelf && IsStaleMember(memberData)) continue;

                    members.Add(new GuildMemberInfo
                    {
                        Uid = memberUid,
                        Name = memberData.Name,
                        Role = memberData.Role,
                        Contribution = memberData.Contribution,
                        PlayerLevel = memberData.PlayerLevel,
                        IsCurrentPlayer = isSelf
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
        catch (Exception ex)
        {
            _log.Error("Gilden-Details laden fehlgeschlagen", ex);
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

    /// <summary>
    /// Berechnet max. Gilden-Mitglieder (20 + Forschungs-Boni + Hallen-Boni aus GuildMembership-Cache).
    /// Forschungs-Effekte werden von GuildResearchService, Hall-Effekte von GuildHallService gecacht.
    /// </summary>
    public int GetMaxMembers()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return BaseMaxGuildMembers;
        return BaseMaxGuildMembers + membership.ResearchMaxMembersBonus + membership.HallMaxMembersBonus;
    }

    // Einladungs-System (Codes, Spieler-Browser, Inbox) → ausgelagert nach <see cref="GuildInviteService"/>.

    // ═══════════════════════════════════════════════════════════════════════
    // VERFUEGBARKEIT (intern, vermeidet Circular DI mit GuildInviteService)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Interner Helfer: Registriert den Spieler als verfuegbar fuer Einladungen.
    /// Bewusst privat dupliziert (vgl. <see cref="GuildInviteService.RegisterAsAvailableAsync"/>),
    /// um Circular DI zu vermeiden — der GuildInviteService injiziert bereits den GuildService.
    /// </summary>
    private async Task RegisterAsAvailableInternalAsync()
    {
        try
        {
            var uid = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(uid)) return;

            var state = _gameStateService.State;
            if (state.GuildMembership != null) return;

            await _firebaseService.SetAsync($"available_players/{uid}", new AvailablePlayerInfo
            {
                Name = PlayerName ?? "Player",
                Level = state.PlayerLevel,
                LastActive = DateTime.UtcNow.ToString("O")
            });
        }
        catch (Exception ex)
        {
            _log.Error("Verfuegbarkeits-Registrierung fehlgeschlagen", ex);
        }
    }

    /// <summary>
    /// Interner Helfer: Entfernt die Verfuegbarkeits-Registrierung beim Gilden-Beitritt.
    /// Bewusst privat dupliziert (vgl. <see cref="GuildInviteService.UnregisterAvailableAsync"/>),
    /// um Circular DI zu vermeiden.
    /// </summary>
    private async Task UnregisterAvailableInternalAsync()
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

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zählt die tatsächliche Mitgliederzahl aus guild_members (Race-Condition-frei).
    /// Aktualisiert memberCount in guilds/{guildId} wenn abweichend.
    /// Gibt -1 zurück bei Netzwerkfehlern (Aufrufer muss darauf reagieren).
    /// </summary>
    private async Task<int> CountAndSyncMemberCountAsync(string guildId)
    {
        try
        {
            var json = await _firebaseService.QueryAsync($"guild_members/{guildId}", "shallow=true");
            var count = 0;
            if (!string.IsNullOrEmpty(json) && json != "null")
            {
                var members = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                count = members?.Count ?? 0;
            }

            // Count in guilds/{guildId} synchronisieren
            await _firebaseService.UpdateAsync($"guilds/{guildId}", new Dictionary<string, object>
            {
                ["memberCount"] = count
            });

            return count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GuildService] CountAndSyncMemberCountAsync Fehler: {ex.Message}");
            return -1; // Netzwerkfehler → Aufrufer darf nicht auf 0 basierte Entscheidungen treffen
        }
    }

    /// <summary>
    /// Gibt den Montag der aktuellen UTC-Woche zurück.
    /// Sonntag wird als letzter Tag der Vorwoche behandelt.
    /// </summary>
    private static DateTime GetCurrentMondayUtc()
    {
        var today = DateTime.UtcNow.Date;
        var dayOfWeek = today.DayOfWeek;
        var daysToMonday = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
        return today.AddDays(-daysToMonday);
    }

    /// <summary>
    /// Parst LastActiveAt mit RoundtripKind. Gibt DateTime.MinValue bei Fehler zurück.
    /// </summary>
    private static DateTime ParseLastActive(string? lastActiveAt)
    {
        if (string.IsNullOrEmpty(lastActiveAt)) return DateTime.MinValue;
        return DateTime.TryParse(lastActiveAt, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.MinValue;
    }

    /// <summary>
    /// Prüft ob ein Spieler unter einer anderen PlayerId bereits Mitglied ist (gleicher Name).
    /// Entsteht wenn App-Daten verloren gehen und eine neue PlayerId generiert wird.
    /// Entfernt den verwaisten Eintrag aus guild_members und player_guilds.
    /// </summary>
    /// <summary>
    /// Prüft ob ein Mitglied als verwaist gilt (>30 Tage inaktiv, kein Leader/Founder).
    /// Verwaiste Mitglieder werden aus der Anzeige gefiltert, aber nicht aus Firebase gelöscht
    /// (Firebase-Rules erlauben nur Self-Delete und Leader-Delete).
    /// </summary>
    private static bool IsStaleMember(FirebaseGuildMember memberData)
    {
        if (memberData.Role is "founder" or "leader") return false;

        var lastActive = ParseLastActive(memberData.LastActiveAt);
        return lastActive < DateTime.UtcNow.AddDays(-30) && lastActive > DateTime.MinValue;
    }

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
            // Diminishing Returns: sqrt(level) statt linear → hohe Level skalieren sanfter
            updates["weeklyGoal"] = (long)(DefaultWeeklyGoal * (1.0 + Math.Sqrt(newLevel) * 0.2));

            // Belohnung: Duplikat-Schutz via Preferences (verhindert doppelte GS bei parallelem Reset)
            var rewardKey = $"guild_weekly_reward_{currentMonday:yyyy-MM-dd}";
            if (!_preferences.Get(rewardKey, false))
            {
                int screwReward = Math.Min(50, 5 + guildData.Level * 2);
                _gameStateService.AddGoldenScrews(screwReward);
                _preferences.Set(rewardKey, true);
            }

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

    }

    private void ClearLocalCache()
    {
        _gameStateService.State.GuildMembership = null;
        // Forschungs-Effekt-Cache mit invalidieren — sonst behaelt der Spieler
        // die Gilden-Forschungs-Boni der gerade verlassenen Gilde.
        _guildResearchService.InvalidateCache();
    }

    private static DateTime GetCurrentMonday()
    {
        var today = DateTime.UtcNow.Date;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        return today.AddDays(-diff);
    }

    /// <summary>
    /// Prüft ob der GameState eine gültige Integritäts-Signatur hat.
    /// Verhindert das Senden manipulierter Werte an Firebase.
    /// </summary>
    private bool VerifyIntegrityForFirebase(GameState state)
    {
        return _integrityService.VerifySignature(state);
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
