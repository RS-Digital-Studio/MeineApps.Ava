using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Firebase;

namespace HandwerkerImperium.Services;

/// <summary>
/// GuildService — Detail-Refresh (Mitgliederliste, Dedup, Stale-Filter) + lokale Bonus-Lookups.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
public sealed partial class GuildService
{
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
}
