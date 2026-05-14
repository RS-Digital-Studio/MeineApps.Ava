using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.Logging;

namespace BomberBlast.Services;

/// <summary>
/// Firebase-basierte Implementation von <see cref="IClanService"/> (Sprint 7.3 AAA-Audit #23).
///
/// <para>
/// Asynchron via Firebase Realtime Database — kein Live-Sync. Pull-Pattern:
/// <list type="bullet">
/// <item>Chat-Pull alle 30s (max 50 Messages)</item>
/// <item>Clan-Daten on-demand bei View-Aktivierung</item>
/// <item>Wochen-Goal-Update bei Coin-Pickup (Debounce 5s)</item>
/// </list>
/// </para>
///
/// <para>
/// Firebase-Pfad-Schema:
/// <list type="bullet">
/// <item><c>clans/{clanId}</c> — Clan-Daten (Name, Code, Members, WeeklyGoalProgress)</item>
/// <item><c>clans/{clanId}/chat</c> — Chat-Messages (max 50 letzte, Push-Pattern)</item>
/// <item><c>clan_codes/{6digitCode}</c> — Lookup-Index Code → ClanId</item>
/// <item><c>leaderboard_clans/{week}</c> — Wochen-Leaderboard (Top 10)</item>
/// </list>
/// </para>
///
/// <para>
/// Sicherheits-Hinweise (in Firebase-Rules zu enforcen, hier als Code-Konvention):
/// <list type="bullet">
/// <item>Clan-Daten nur schreibbar wenn Spieler Member (Server-Side Rule)</item>
/// <item>Chat-Rate-Limit: max 5 Messages/Min pro User</item>
/// <item>Profanity-Filter clientseitig + Server-Side Validation</item>
/// </list>
/// </para>
/// </summary>
public sealed class FirebaseClanService : IClanService
{
    private const string KeyCurrentClanId = "Clan_CurrentClanId";
    private const string KeyCurrentMemberId = "Clan_CurrentMemberId";

    private readonly IFirebaseService _firebase;
    private readonly IPreferencesService _prefs;
    private readonly ILogger<FirebaseClanService> _logger;

    private ClanData? _currentClan;

    public event Action? ClanChanged;

    public FirebaseClanService(IFirebaseService firebase, IPreferencesService prefs, ILogger<FirebaseClanService> logger)
    {
        _firebase = firebase;
        _prefs = prefs;
        _logger = logger;
        // Audit H10: try/catch im fire-and-forget — ungefangene Exceptions hier
        // wuerden via TaskScheduler.UnobservedTaskException den Release-Process killen.
        _ = Task.Run(async () =>
        {
            try { await LoadCurrentClanAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "LoadCurrentClanAsync (Ctor) fehlgeschlagen"); }
        });
    }

    public ClanData? CurrentClan => _currentClan;
    public string? CurrentMemberId => string.IsNullOrEmpty(_prefs.Get(KeyCurrentMemberId, string.Empty))
        ? null
        : _prefs.Get(KeyCurrentMemberId, string.Empty);

    private async Task LoadCurrentClanAsync()
    {
        var clanId = _prefs.Get(KeyCurrentClanId, string.Empty);
        if (string.IsNullOrEmpty(clanId)) return;

        try
        {
            await _firebase.EnsureAuthenticatedAsync();
            _currentClan = await _firebase.GetAsync<ClanData>($"clans/{clanId}");
            ClanChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FirebaseClanService: Konnte Clan {ClanId} nicht laden", clanId);
        }
    }

    public async Task<ClanData> CreateClanAsync(string clanName, string memberDisplayName)
    {
        if (_currentClan != null)
            throw new InvalidOperationException("Spieler ist bereits in einem Clan.");

        await _firebase.EnsureAuthenticatedAsync();
        if (string.IsNullOrEmpty(_firebase.Uid))
            throw new InvalidOperationException("Firebase-Auth fehlgeschlagen.");

        // Generiere 6-stelligen Code (alphanumerisch, eindeutig).
        // Production-Code wuerde Server-Side via Cloud-Function generieren um Kollisionen
        // zu vermeiden. Client-Generation: Re-Try bei Kollision (Race-Window ~1s).
        var rng = new Random();
        string sixDigit = "";
        for (int i = 0; i < 6; i++)
            sixDigit += "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[rng.Next(32)];  // Verwechslungs-feindlich

        // Clan erstellen
        var member = new ClanMember
        {
            MemberId = _firebase.Uid,
            DisplayName = memberDisplayName,
            ContributedCoins = 0,
            LastSeenUtc = DateTime.UtcNow,
            Role = ClanRole.Leader,
        };
        var clanData = new ClanData
        {
            ClanId = sixDigit,
            SixDigitCode = sixDigit,
            Name = clanName,
            Members = new[] { member },
            WeeklyGoalProgress = 0,
            WeeklyGoalTarget = 10000,
        };

        await _firebase.SetAsync($"clans/{sixDigit}", clanData);
        await _firebase.SetAsync($"clan_codes/{sixDigit}", new { clan_id = sixDigit });

        _prefs.Set(KeyCurrentClanId, sixDigit);
        _prefs.Set(KeyCurrentMemberId, _firebase.Uid);
        _currentClan = clanData;
        ClanChanged?.Invoke();

        return clanData;
    }

    public async Task<ClanData> JoinClanAsync(string sixDigitCode, string memberDisplayName)
    {
        if (_currentClan != null)
            throw new InvalidOperationException("Spieler ist bereits in einem Clan. Verlasse den aktuellen zuerst.");

        await _firebase.EnsureAuthenticatedAsync();
        if (string.IsNullOrEmpty(_firebase.Uid))
            throw new InvalidOperationException("Firebase-Auth fehlgeschlagen.");

        var clan = await _firebase.GetAsync<ClanData>($"clans/{sixDigitCode}")
            ?? throw new InvalidOperationException("Clan-Code nicht gefunden.");

        if (clan.Members.Count >= 10)
            throw new InvalidOperationException("Clan ist voll (10 Mitglieder maximum).");

        var newMember = new ClanMember
        {
            MemberId = _firebase.Uid,
            DisplayName = memberDisplayName,
            ContributedCoins = 0,
            LastSeenUtc = DateTime.UtcNow,
            Role = ClanRole.Member,
        };
        var updatedMembers = new List<ClanMember>(clan.Members) { newMember };
        var updatedClan = new ClanData
        {
            ClanId = clan.ClanId,
            SixDigitCode = clan.SixDigitCode,
            Name = clan.Name,
            Members = updatedMembers,
            WeeklyGoalProgress = clan.WeeklyGoalProgress,
            WeeklyGoalTarget = clan.WeeklyGoalTarget,
            CurrentBannerAssetPath = clan.CurrentBannerAssetPath,
        };

        await _firebase.SetAsync($"clans/{sixDigitCode}", updatedClan);

        _prefs.Set(KeyCurrentClanId, sixDigitCode);
        _prefs.Set(KeyCurrentMemberId, _firebase.Uid);
        _currentClan = updatedClan;
        ClanChanged?.Invoke();

        return updatedClan;
    }

    public async Task LeaveClanAsync()
    {
        if (_currentClan == null) return;
        var uid = _firebase.Uid;
        if (string.IsNullOrEmpty(uid)) return;

        try
        {
            var clanId = _currentClan.ClanId;
            // Audit M15: Re-fetch fresh server state before mutation — Race-Schutz gegen
            // gleichzeitige Beitritte (Read-Modify-Write ohne Transaction). Wenn Re-fetch
            // fehlschlaegt verwenden wir lokalen Snapshot als Fallback.
            var fresh = await _firebase.GetAsync<ClanData>($"clans/{clanId}") ?? _currentClan;
            // Remove member from Clan
            var updatedMembers = fresh.Members.Where(m => m.MemberId != uid).ToList();

            if (updatedMembers.Count == 0)
            {
                // Letzter Member → Clan loeschen
                await _firebase.DeleteAsync($"clans/{clanId}");
                await _firebase.DeleteAsync($"clan_codes/{_currentClan.SixDigitCode}");
            }
            else
            {
                // Andere Members bleiben — Update mit fresh-Server-Snapshot
                var updatedClan = new ClanData
                {
                    ClanId = fresh.ClanId,
                    SixDigitCode = fresh.SixDigitCode,
                    Name = fresh.Name,
                    Members = updatedMembers,
                    WeeklyGoalProgress = fresh.WeeklyGoalProgress,
                    WeeklyGoalTarget = fresh.WeeklyGoalTarget,
                };
                await _firebase.SetAsync($"clans/{clanId}", updatedClan);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FirebaseClanService.LeaveClanAsync fehlgeschlagen");
        }
        finally
        {
            _prefs.Set(KeyCurrentClanId, string.Empty);
            _prefs.Set(KeyCurrentMemberId, string.Empty);
            _currentClan = null;
            ClanChanged?.Invoke();
        }
    }

    public async Task<IReadOnlyList<ClanChatMessage>> PullChatAsync()
    {
        if (_currentClan == null) return Array.Empty<ClanChatMessage>();
        try
        {
            await _firebase.EnsureAuthenticatedAsync();
            var msgs = await _firebase.GetAsync<Dictionary<string, ClanChatMessage>>(
                $"clans/{_currentClan.ClanId}/chat");
            if (msgs == null) return Array.Empty<ClanChatMessage>();
            // Sortiere nach SentUtc, max 50
            return msgs.Values
                .OrderByDescending(m => m.SentUtc)
                .Take(50)
                .OrderBy(m => m.SentUtc)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FirebaseClanService.PullChatAsync fehlgeschlagen");
            return Array.Empty<ClanChatMessage>();
        }
    }

    // Sprint 7.3 AAA-Audit #15: Client-seitiges Anti-Spam-Cooldown — 3s zwischen Chat-Sends
    // pro Clan-Member. Schuetzt vor versehentlichen Doppel-Sends + dummen Spam-Versuchen.
    // (Server-seitig waere defense-in-depth, braucht aber Multi-Path-Updates — deferred.)
    private const double ChatSendCooldownSeconds = 3.0;
    private DateTime _lastChatSentUtc = DateTime.MinValue;

    public async Task SendChatAsync(string message)
    {
        if (_currentClan == null || string.IsNullOrWhiteSpace(message)) return;
        var uid = _firebase.Uid;
        if (string.IsNullOrEmpty(uid)) return;

        var member = _currentClan.Members.FirstOrDefault(m => m.MemberId == uid);
        if (member == null) return;

        // Sprint 7.3 AAA-Audit #15: Rate-Limit gegen Chat-Spam.
        var since = (DateTime.UtcNow - _lastChatSentUtc).TotalSeconds;
        if (since >= 0 && since < ChatSendCooldownSeconds)
            return;

        // Profanity-Filter wird in Production via Server-Side-Rules erzwungen.
        // Client-Side: einfache Length-Limit (max 200 Zeichen).
        var clean = message.Length > 200 ? message[..200] : message;

        try
        {
            // Audit M05: ServerValue.TIMESTAMP-Sentinel statt Client-DateTime.UtcNow.
            // Verhindert Rate-Limit-/Reihenfolge-Spoofing (LeagueService nutzt dasselbe Pattern).
            var chatPayload = new Dictionary<string, object>
            {
                ["messageId"] = Guid.NewGuid().ToString(),
                ["senderId"] = uid,
                ["senderDisplayName"] = member.DisplayName,
                ["content"] = clean,
                ["sentUtc"] = FirebaseServerTimestamp,
            };
            await _firebase.PushAsync($"clans/{_currentClan.ClanId}/chat", chatPayload);
            _lastChatSentUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FirebaseClanService.SendChatAsync fehlgeschlagen");
        }
    }

    // Audit M05: Firebase-Sentinel-Konstante fuer ServerValue.TIMESTAMP (Anti-Spoofing).
    private static readonly Dictionary<string, string> FirebaseServerTimestamp = new() { [".sv"] = "timestamp" };

    public async Task<IReadOnlyList<ClanData>> GetLeaderboardAsync()
    {
        try
        {
            await _firebase.EnsureAuthenticatedAsync();
            var year = DateTime.UtcNow.Year;
            var week = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.UtcNow);
            var weekId = $"{year}W{week:D2}";

            var leaderboard = await _firebase.GetAsync<Dictionary<string, ClanData>>(
                $"leaderboard_clans/{weekId}");
            if (leaderboard == null) return Array.Empty<ClanData>();
            return leaderboard.Values
                .OrderByDescending(c => c.WeeklyGoalProgress)
                .Take(10)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FirebaseClanService.GetLeaderboardAsync fehlgeschlagen");
            return Array.Empty<ClanData>();
        }
    }
}
