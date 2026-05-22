using System.Text;
using System.Text.Json;
using BomberBlast.Models.League;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Implementation von <see cref="IDataExportService"/> (Phase 25 — DSGVO Art. 20).
///
/// <para>Sammelt alle relevanten Spieler-Daten aus den existierenden Services. Kein direkter
/// Preferences-Zugriff — die Services kapseln die Daten-Domains und liefern strukturierte Werte.
/// Format-Strategie: Einzelne Domains werden in einem typsicheren <c>ExportSnapshot</c> gesammelt
/// und mit System.Text.Json serialisiert (kein Reflection auf interne Felder).</para>
/// </summary>
public sealed class DataExportService : IDataExportService
{
    private readonly IPreferencesService _prefs;
    private readonly ICoinService _coins;
    private readonly IGemService _gems;
    private readonly IProgressService _progress;
    private readonly IAchievementService _achievements;
    private readonly IShopService _shop;
    private readonly ILeagueService _league;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public DataExportService(
        IPreferencesService prefs,
        ICoinService coins,
        IGemService gems,
        IProgressService progress,
        IAchievementService achievements,
        IShopService shop,
        ILeagueService league)
    {
        _prefs = prefs;
        _coins = coins;
        _gems = gems;
        _progress = progress;
        _achievements = achievements;
        _shop = shop;
        _league = league;
    }

    public async Task<string> ExportAsJsonAsync()
    {
        var snapshot = await BuildSnapshotAsync();
        return JsonSerializer.Serialize(snapshot, JsonOpts);
    }

    public async Task<string> ExportAsHumanReadableAsync()
    {
        var snapshot = await BuildSnapshotAsync();
        var sb = new StringBuilder();
        sb.AppendLine("=== BomberBlast — Meine Daten ===");
        sb.AppendLine($"Stand: {snapshot.ExportedAtUtc:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine($"Spielername: {snapshot.PlayerName}");
        sb.AppendLine();
        sb.AppendLine("--- Fortschritt ---");
        sb.AppendLine($"  Höchstes Level: {snapshot.HighestCompletedLevel} / 100");
        sb.AppendLine($"  Sterne gesamt: {snapshot.TotalStars}");
        sb.AppendLine();
        sb.AppendLine("--- Wirtschaft ---");
        sb.AppendLine($"  Münzen: {snapshot.Coins:N0}");
        sb.AppendLine($"  Edelsteine: {snapshot.Gems:N0}");
        sb.AppendLine($"  Münzen verdient (lifetime): {snapshot.TotalCoinsEarned:N0}");
        sb.AppendLine();
        sb.AppendLine("--- Liga ---");
        sb.AppendLine($"  Tier: {snapshot.LeagueTier}");
        sb.AppendLine($"  Punkte: {snapshot.LeaguePoints}");
        sb.AppendLine();
        sb.AppendLine("--- Achievements ---");
        sb.AppendLine($"  Freigeschaltet: {snapshot.AchievementsUnlocked} / {snapshot.AchievementsTotal}");
        sb.AppendLine();
        sb.AppendLine("--- Datenschutz-Zustimmungen ---");
        sb.AppendLine($"  Analytics: {(snapshot.AnalyticsConsent ? "Aktiv" : "Deaktiviert")}");
        return sb.ToString();
    }

    private Task<ExportSnapshot> BuildSnapshotAsync()
    {
        var snap = new ExportSnapshot
        {
            ExportedAtUtc = DateTime.UtcNow,
            PlayerName = _league.PlayerName ?? string.Empty,

            HighestCompletedLevel = _progress.HighestCompletedLevel,
            TotalStars = _progress.GetTotalStars(),

            Coins = _coins.Balance,
            TotalCoinsEarned = _coins.TotalEarned,
            Gems = _gems.Balance,

            LeagueTier = _league.CurrentTier.ToString(),
            LeagueSubTier = _league.CurrentTier.GetSubTier(_league.CurrentPoints).ToString(),
            LeaguePoints = _league.CurrentPoints,

            AchievementsUnlocked = _achievements.UnlockedCount,
            AchievementsTotal = _achievements.TotalCount,

            AnalyticsConsent = _prefs.Get("AnalyticsConsent", false),

            ShopUpgradeLevels = BuildShopSummary(),
        };
        return Task.FromResult(snap);
    }

    private Dictionary<string, int> BuildShopSummary()
    {
        // Shop-Upgrade-Zustand als Key/Value-Dictionary für Export.
        // Wir lesen die Standard-Upgrade-Keys direkt aus Preferences (Defensiv: 0 wenn fehlt).
        var keys = new[]
        {
            "Upgrade_StartBombs", "Upgrade_StartFire", "Upgrade_StartSpeed",
            "Upgrade_ExtraLives", "Upgrade_ScoreMultiplier", "Upgrade_TimeBonus",
            "Upgrade_ShieldStart", "Upgrade_CoinBonus", "Upgrade_PowerUpLuck",
        };
        var result = new Dictionary<string, int>();
        foreach (var k in keys)
            result[k] = _prefs.Get(k, 0);
        return result;
    }

    /// <summary>
    /// Strukturierter Snapshot der Spieler-Daten. Wird via System.Text.Json serialisiert.
    /// Properties sind public damit der JsonSerializer ohne Reflection-Hacks arbeiten kann.
    /// </summary>
    public sealed class ExportSnapshot
    {
        public DateTime ExportedAtUtc { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int HighestCompletedLevel { get; set; }
        public int TotalStars { get; set; }
        public int Coins { get; set; }
        public int TotalCoinsEarned { get; set; }
        public int Gems { get; set; }
        public string LeagueTier { get; set; } = string.Empty;
        public string LeagueSubTier { get; set; } = string.Empty;
        public int LeaguePoints { get; set; }
        public int AchievementsUnlocked { get; set; }
        public int AchievementsTotal { get; set; }
        public bool AnalyticsConsent { get; set; }
        public Dictionary<string, int> ShopUpgradeLevels { get; set; } = new();
    }
}
