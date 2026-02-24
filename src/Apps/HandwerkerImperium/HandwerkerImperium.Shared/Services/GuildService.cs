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
public class GuildService : IGuildService
{
    private const string PrefKeyPlayerName = "guild_player_name";
    private const int BaseMaxGuildMembers = 20;
    private const long DefaultWeeklyGoal = 500_000;

    private readonly IGameStateService _gameStateService;
    private readonly IFirebaseService _firebaseService;
    private readonly IPreferencesService _preferences;

    // Gecachte Forschungs-Effekte für schnellen Zugriff im GameLoop
    private GuildResearchEffects _cachedResearchEffects = new();

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

            // Prüfen ob Spieler in einer Gilde ist (Schnell-Lookup)
            var uid = _firebaseService.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            var guildId = await _firebaseService.GetAsync<string>($"player_guilds/{uid}");
            if (!string.IsNullOrEmpty(guildId))
            {
                // Gilden-Basisdaten laden für lokalen Cache
                var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
                if (guildData != null)
                {
                    UpdateLocalCache(guildId, guildData);
                    // Forschungs-Effekte laden und cachen
                    await RefreshResearchEffectsAsync(guildId);
                }
                else
                {
                    // Gilde existiert nicht mehr → lokalen Cache aufräumen
                    ClearLocalCache();
                    await _firebaseService.DeleteAsync($"player_guilds/{uid}");
                }
            }
            else
            {
                // Nicht in einer Gilde → lokalen Cache aufräumen
                ClearLocalCache();
            }
        }
        catch
        {
            // Offline → lokaler Cache bleibt bestehen
        }

        GuildUpdated?.Invoke();
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
                if (data.MemberCount >= GetMaxMembers()) continue;

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
            var uid = _firebaseService.Uid;
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
                CreatedAt = now.ToString("O")
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
                JoinedAt = now.ToString("O")
            };
            await _firebaseService.SetAsync($"guild_members/{guildId}/{uid}", member);

            // Schnell-Lookup
            await _firebaseService.SetAsync($"player_guilds/{uid}", guildId);

            // Lokalen Cache aktualisieren
            UpdateLocalCache(guildId, guildData);
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
            var uid = _firebaseService.Uid;
            if (string.IsNullOrEmpty(uid)) return false;

            var state = _gameStateService.State;
            if (state.GuildMembership != null) return false;

            // Gilden-Daten prüfen
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null) return false;
            if (guildData.MemberCount >= GetMaxMembers()) return false;

            var now = DateTime.UtcNow;

            // Mitglied eintragen
            var member = new FirebaseGuildMember
            {
                Name = PlayerName ?? "Spieler",
                Role = "member",
                Contribution = 0,
                PlayerLevel = state.PlayerLevel,
                JoinedAt = now.ToString("O")
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
            var uid = _firebaseService.Uid;
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
                    // Leere Gilde löschen
                    await _firebaseService.DeleteAsync($"guilds/{guildId}");
                    await _firebaseService.DeleteAsync($"guild_members/{guildId}");
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
            var uid = _firebaseService.Uid;
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
            var uid = _firebaseService.Uid;
            if (string.IsNullOrEmpty(uid)) return null;

            var state = _gameStateService.State;
            var membership = state.GuildMembership;
            if (membership == null) return null;

            var guildId = membership.GuildId;

            // Gilden-Daten laden
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null)
            {
                // Gilde existiert nicht mehr
                ClearLocalCache();
                await _firebaseService.DeleteAsync($"player_guilds/{uid}");
                GuildUpdated?.Invoke();
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
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GILDEN-FORSCHUNG
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<GuildResearchDisplay>> GetGuildResearchAsync()
    {
        var result = new List<GuildResearchDisplay>();

        try
        {
            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return result;

            await _firebaseService.EnsureAuthenticatedAsync();

            // Alle Forschungs-Zustände laden
            var statesRaw = await _firebaseService.GetAsync<Dictionary<string, GuildResearchState>>(
                $"guild_research/{membership.GuildId}");
            var states = statesRaw ?? new Dictionary<string, GuildResearchState>();

            // Abgeschlossene IDs sammeln für Effekt-Berechnung
            var completedIds = new HashSet<string>();
            foreach (var (id, state) in states)
            {
                if (state.Completed) completedIds.Add(id);
            }

            // Effekte berechnen und cachen
            _cachedResearchEffects = GuildResearchEffects.Calculate(completedIds);
            membership.ApplyResearchEffects(_cachedResearchEffects);
            _gameStateService.MarkDirty();

            // Definitionen mit Zuständen mergen, pro Kategorie aktive bestimmen
            var definitions = GuildResearchDefinition.GetAll();
            var categoryFirstIncomplete = new Dictionary<GuildResearchCategory, bool>();

            foreach (var def in definitions.OrderBy(d => d.Category).ThenBy(d => d.Order))
            {
                states.TryGetValue(def.Id, out var researchState);
                var isCompleted = researchState?.Completed ?? false;

                // Erste nicht-abgeschlossene pro Kategorie = aktiv
                var isActive = false;
                if (!isCompleted && !categoryFirstIncomplete.ContainsKey(def.Category))
                {
                    isActive = true;
                    categoryFirstIncomplete[def.Category] = true;
                }

                result.Add(new GuildResearchDisplay
                {
                    Id = def.Id,
                    Name = def.NameKey, // Wird im ViewModel durch lokalisierten Text ersetzt
                    Description = def.DescKey,
                    Icon = def.Icon,
                    Cost = def.Cost,
                    Progress = researchState?.Progress ?? 0,
                    Category = def.Category,
                    EffectType = def.EffectType,
                    EffectValue = def.EffectValue,
                    CategoryColor = GuildResearchDefinition.GetCategoryColor(def.Category),
                    IsCompleted = isCompleted,
                    IsActive = isActive
                });
            }
        }
        catch
        {
            // Bei Fehler leere Liste zurückgeben
        }

        return result;
    }

    public async Task<bool> ContributeToResearchAsync(string researchId, long amount)
    {
        try
        {
            var membership = _gameStateService.State.GuildMembership;
            if (membership == null || amount <= 0) return false;

            var state = _gameStateService.State;
            if (state.Money < amount) return false;

            await _firebaseService.EnsureAuthenticatedAsync();

            var guildId = membership.GuildId;
            var path = $"guild_research/{guildId}/{researchId}";

            // Aktuellen Zustand laden
            var researchState = await _firebaseService.GetAsync<GuildResearchState>(path);
            if (researchState == null)
            {
                researchState = new GuildResearchState();
            }

            if (researchState.Completed) return false;

            // Kosten der Forschung ermitteln
            var definition = GuildResearchDefinition.GetAll().FirstOrDefault(d => d.Id == researchId);
            if (definition == null) return false;

            // Beitrag berechnen (nicht mehr als nötig)
            var remaining = definition.Cost - researchState.Progress;
            var actualAmount = Math.Min(amount, remaining);
            if (actualAmount <= 0) return false;

            // Geld abziehen
            _gameStateService.AddMoney(-actualAmount);

            // Fortschritt erhöhen
            researchState.Progress += actualAmount;

            // Abschluss prüfen
            if (researchState.Progress >= definition.Cost)
            {
                researchState.Completed = true;
                researchState.CompletedAt = DateTime.UtcNow.ToString("O");
            }

            // Firebase aktualisieren
            await _firebaseService.SetAsync(path, researchState);

            // Effekte neu berechnen
            await RefreshResearchEffectsAsync(guildId);

            GuildUpdated?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public GuildResearchEffects GetResearchEffects() => _cachedResearchEffects;

    public int GetMaxMembers() => BaseMaxGuildMembers + _cachedResearchEffects.MaxMembersBonus;

    /// <summary>
    /// Lädt abgeschlossene Forschungen und aktualisiert den Effekt-Cache.
    /// </summary>
    private async Task RefreshResearchEffectsAsync(string guildId)
    {
        try
        {
            var statesRaw = await _firebaseService.GetAsync<Dictionary<string, GuildResearchState>>(
                $"guild_research/{guildId}");

            var completedIds = new HashSet<string>();
            if (statesRaw != null)
            {
                foreach (var (id, rs) in statesRaw)
                {
                    if (rs.Completed) completedIds.Add(id);
                }
            }

            _cachedResearchEffects = GuildResearchEffects.Calculate(completedIds);

            // Lokalen Cache aktualisieren
            var membership = _gameStateService.State.GuildMembership;
            if (membership != null)
            {
                membership.ApplyResearchEffects(_cachedResearchEffects);
                _gameStateService.MarkDirty();
            }
        }
        catch
        {
            // Bei Fehler alten Cache behalten
        }
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

        // Alle Mitglieder-Beiträge zurücksetzen
        var membersRaw = await _firebaseService.GetAsync<Dictionary<string, FirebaseGuildMember>>($"guild_members/{guildId}");
        if (membersRaw != null)
        {
            foreach (var (memberUid, _) in membersRaw)
            {
                await _firebaseService.UpdateAsync($"guild_members/{guildId}/{memberUid}", new Dictionary<string, object>
                {
                    ["contribution"] = 0
                });
            }
        }

        guildData.WeeklyProgress = 0;
    }

    private void UpdateLocalCache(string guildId, FirebaseGuildData guildData)
    {
        var state = _gameStateService.State;
        state.GuildMembership = new GuildMembership
        {
            GuildId = guildId,
            GuildName = guildData.Name,
            GuildLevel = guildData.Level,
            GuildIcon = guildData.Icon,
            GuildColor = guildData.Color
        };
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
