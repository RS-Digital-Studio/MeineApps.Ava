using System.Text.Json;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Firebase;

namespace HandwerkerImperium.Services;

/// <summary>
/// GuildService — Mitgliedschaft: Browse, Create, Join, Leave (inkl. Leader-Transfer + Cleanup).
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
public sealed partial class GuildService
{
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
}
