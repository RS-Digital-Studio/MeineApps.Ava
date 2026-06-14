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
/// <remarks>
/// Partial-Split (v2.1.4) — Firebase-Gilden-CRUD, aufgeteilt nach Sub-Bereichen. Alle Schreib-Pfade
/// (Create/Join/Leave/Contribute) teilen denselben <see cref="_lock"/>; geteilte private Helfer
/// bleiben in dieser Kern-Datei:
/// <list type="bullet">
/// <item>GuildService.cs — Felder, Lock, Konstruktor, Dispose, geteilte Helfer (Cache, Member-Count, Verfügbarkeit, Datum, Integrität).</item>
/// <item>GuildService.Lifecycle.cs — InitializeAsync + UID→PlayerId-Migration.</item>
/// <item>GuildService.Membership.cs — Browse, Create, Join, Leave (Leader-Transfer, Cleanup).</item>
/// <item>GuildService.Contribution.cs — Wochenziel-Beitrag + wöchentliches Reset/Reward.</item>
/// <item>GuildService.Details.cs — Detail-Refresh (Mitgliederliste, Dedup, Stale-Filter) + Bonus-Lookups.</item>
/// <item>GuildService.Roles.cs — Rollen-Management, Keep-Alive, Spielername.</item>
/// </list>
/// </remarks>
public sealed partial class GuildService : IGuildService, IDisposable
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
    // HELPERS (geteilt über alle Partials)
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
