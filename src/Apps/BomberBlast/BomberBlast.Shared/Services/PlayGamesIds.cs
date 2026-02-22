namespace BomberBlast.Services;

/// <summary>
/// Mapping von lokalen Achievement-/Leaderboard-IDs auf Google Play Games Services IDs.
/// IDs aus der Google Play Console (Projekt 353652455692).
/// </summary>
public static class PlayGamesIds
{
    // ═══════════════════════════════════════════════════════════════════════
    // LEADERBOARDS
    // ═══════════════════════════════════════════════════════════════════════

    public const string LeaderboardTotalStars = "CgkIjPLQuqUKEAIQGQ";

    // ═══════════════════════════════════════════════════════════════════════
    // ACHIEVEMENTS → GPGS-ID MAPPING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt die GPGS-Achievement-ID für eine lokale Achievement-ID zurück.
    /// Gibt null zurück wenn kein Mapping existiert.
    /// </summary>
    public static string? GetGpgsAchievementId(string localId)
    {
        return localId switch
        {
            // Fortschritt
            "first_victory" => "CgkIjPLQuqUKEAIQAQ",
            "world1" => "CgkIjPLQuqUKEAIQGg",
            "world2" => "CgkIjPLQuqUKEAIQEA",
            "world3" => "CgkIjPLQuqUKEAIQAg",
            "world4" => "CgkIjPLQuqUKEAIQAw",
            "world5" => "CgkIjPLQuqUKEAIQBA",
            "daily_streak7" => "CgkIjPLQuqUKEAIQBQ",
            "daily_complete30" => "CgkIjPLQuqUKEAIQEQ",

            // Meisterschaft
            "stars_50" => "CgkIjPLQuqUKEAIQFQ",
            "stars_100" => "CgkIjPLQuqUKEAIQBg",
            "stars_150" => "CgkIjPLQuqUKEAIQBw",

            // Kampf
            "kills_100" => "CgkIjPLQuqUKEAIQCA",
            "kills_500" => "CgkIjPLQuqUKEAIQCQ",
            "kills_1000" => "CgkIjPLQuqUKEAIQCg",
            "kick_master" => "CgkIjPLQuqUKEAIQEg",
            "power_bomber" => "CgkIjPLQuqUKEAIQCw",

            // Geschick
            "no_damage" => "CgkIjPLQuqUKEAIQDA",
            "efficient" => "CgkIjPLQuqUKEAIQFg",
            "speedrun" => "CgkIjPLQuqUKEAIQDQ",
            "combo3" => "CgkIjPLQuqUKEAIQDg",
            "combo5" => "CgkIjPLQuqUKEAIQEw",
            "curse_survivor" => "CgkIjPLQuqUKEAIQFw",

            _ => null
        };
    }
}
