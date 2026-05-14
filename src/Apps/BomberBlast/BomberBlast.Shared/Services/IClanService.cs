namespace BomberBlast.Services;

/// <summary>
/// Clan-System (.3 .
///
/// <para>
/// Asynchron, Firebase-basiert (wie League). Keine Live-Sync, kein dedizierter Server.
/// Spieler erstellt Clan (max 10 Members) oder tritt bei via 6-stelligem Code.
/// </para>
///
/// <para>
/// Features:
/// <list type="bullet">
/// <item>Clan-Wochenziel: "Sammelt 10.000 Coins gemeinsam" → Belohnung fuer alle Member</item>
/// <item>Clan-Leaderboard: Top 10 globale Clans nach Wochen-Score</item>
/// <item>Clan-Chat asynchron (alle 30s Pull, 50 letzte Messages, Profanity-Filter)</item>
/// <item>Clan-Helfen: Member kann Bomb-Card "spenden" (1x pro Tag)</item>
/// </list>
/// </para>
///
/// <para>
/// HINWEIS:.3 ist Foundation-Layer (Interface + NullImpl + Domain-Models).
/// Echte Firebase-Realtime-DB-Integration ist eigener 4-6-Wochen-Sprint inkl.
/// Security-Rules + Profanity-Filter + Rate-Limits.
/// </para>
/// </summary>
public interface IClanService
{
    /// <summary>Eigener Clan (null wenn der Spieler noch keinem beigetreten ist).</summary>
    ClanData? CurrentClan { get; }

    /// <summary>Eigene Clan-Member-ID (null wenn nicht in Clan).</summary>
    string? CurrentMemberId { get; }

    /// <summary>Erstellt einen neuen Clan und tritt ihm bei. Fehler wenn Spieler bereits in Clan.</summary>
    Task<ClanData> CreateClanAsync(string clanName, string memberDisplayName);

    /// <summary>Tritt einem Clan via 6-stelligem Code bei. Fehler wenn Code unbekannt oder Clan voll.</summary>
    Task<ClanData> JoinClanAsync(string sixDigitCode, string memberDisplayName);

    /// <summary>Verlaesst den aktuellen Clan. Fehler wenn der Spieler nicht in einem Clan ist.</summary>
    Task LeaveClanAsync();

    /// <summary>Pull der letzten 50 Chat-Messages. Wird typisch alle 30s aufgerufen.</summary>
    Task<IReadOnlyList<ClanChatMessage>> PullChatAsync();

    /// <summary>Sendet eine Chat-Message. Profanity-Filter wird serverseitig ausgewertet.</summary>
    Task SendChatAsync(string message);

    /// <summary>Globales Clan-Leaderboard (Top 10 nach Wochen-Score).</summary>
    Task<IReadOnlyList<ClanData>> GetLeaderboardAsync();

    /// <summary>Wird gefeuert wenn sich der Clan-Status aendert (Member-Beitritt, Wochenziel-Update).</summary>
    event Action? ClanChanged;
}

/// <summary>Daten eines Clans.</summary>
public sealed class ClanData
{
    public required string ClanId { get; init; }
    public required string SixDigitCode { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<ClanMember> Members { get; init; }
    public int WeeklyGoalProgress { get; init; }   // gesammelte Coins der Woche
    public int WeeklyGoalTarget { get; init; } = 10000;
    public string? CurrentBannerAssetPath { get; init; }
}

/// <summary>Daten eines Clan-Members.</summary>
public sealed class ClanMember
{
    public required string MemberId { get; init; }
    public required string DisplayName { get; init; }
    public int ContributedCoins { get; init; }
    public DateTime LastSeenUtc { get; init; }
    public ClanRole Role { get; init; } = ClanRole.Member;
}

public enum ClanRole
{
    Member = 0,
    Officer = 1,
    Leader = 2,
}

/// <summary>Eine einzelne Chat-Nachricht im Clan.</summary>
public sealed class ClanChatMessage
{
    public required string MessageId { get; init; }
    public required string SenderId { get; init; }
    public required string SenderDisplayName { get; init; }
    public required string Content { get; init; }

    /// <summary>
    /// Sendezeit als Unix-Millisekunden (Server-Timestamp via Firebase ServerValue.TIMESTAMP).
    /// Audit M05: war frueher String/DateTime (Client-Time, spoofbar). Jetzt long ms ab Epoch,
    /// serverseitig vom Firebase gesetzt — Rate-Limit/Reihenfolge nicht mehr manipulierbar.
    /// JSON-Property-Name <c>sentUtc</c> (camelCase aus JsonOptions) — passt zur Firebase-Rule.
    /// </summary>
    public required long SentUtc { get; init; }

    /// <summary>Konvertiert SentUtc (Unix-ms) in DateTime fuer Anzeige.</summary>
    public DateTime SentUtcAsDateTime => DateTimeOffset.FromUnixTimeMilliseconds(SentUtc).UtcDateTime;
}

/// <summary>NullImpl: Clan-Feature deaktiviert. Fuer Desktop oder Pre-Firebase-Setup.</summary>
public sealed class NullClanService : IClanService
{
    public ClanData? CurrentClan => null;
    public string? CurrentMemberId => null;

    public Task<ClanData> CreateClanAsync(string clanName, string memberDisplayName)
        => throw new NotSupportedException("Clan-System ist in dieser Umgebung deaktiviert.");

    public Task<ClanData> JoinClanAsync(string sixDigitCode, string memberDisplayName)
        => throw new NotSupportedException("Clan-System ist in dieser Umgebung deaktiviert.");

    public Task LeaveClanAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<ClanChatMessage>> PullChatAsync()
        => Task.FromResult<IReadOnlyList<ClanChatMessage>>(Array.Empty<ClanChatMessage>());

    public Task SendChatAsync(string message) => Task.CompletedTask;

    public Task<IReadOnlyList<ClanData>> GetLeaderboardAsync()
        => Task.FromResult<IReadOnlyList<ClanData>>(Array.Empty<ClanData>());

#pragma warning disable CS0067  // NullImpl feuert nie ClanChanged
    public event Action? ClanChanged;
#pragma warning restore CS0067
}
