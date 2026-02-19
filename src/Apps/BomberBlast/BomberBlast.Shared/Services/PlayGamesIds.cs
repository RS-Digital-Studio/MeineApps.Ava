namespace BomberBlast.Services;

/// <summary>
/// Mapping von lokalen Achievement-/Leaderboard-IDs auf Google Play Games Services IDs.
/// TODO: Echte IDs aus der Play Console eintragen.
/// </summary>
public static class PlayGamesIds
{
    // ═══════════════════════════════════════════════════════════════════════
    // LEADERBOARDS
    // ═══════════════════════════════════════════════════════════════════════

    public const string LeaderboardArcadeHighscore = "TODO_LEADERBOARD_ARCADE";
    public const string LeaderboardTotalStars = "TODO_LEADERBOARD_STARS";

    // ═══════════════════════════════════════════════════════════════════════
    // ACHIEVEMENTS → GPGS-ID MAPPING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt die GPGS-Achievement-ID für eine lokale Achievement-ID zurück.
    /// Gibt null zurück wenn kein Mapping existiert oder die ID ein TODO-Platzhalter ist.
    /// </summary>
    public static string? GetGpgsAchievementId(string localId)
    {
        var gpgsId = localId switch
        {
            // Fortschritt
            "first_victory" => "TODO_ACH_FIRST_VICTORY",
            "world1" => "TODO_ACH_WORLD1",
            "world2" => "TODO_ACH_WORLD2",
            "world3" => "TODO_ACH_WORLD3",
            "world4" => "TODO_ACH_WORLD4",
            "world5" => "TODO_ACH_WORLD5",
            "daily_streak7" => "TODO_ACH_DAILY_STREAK7",
            "daily_complete30" => "TODO_ACH_DAILY_COMPLETE30",

            // Meisterschaft
            "stars_50" => "TODO_ACH_STARS_50",
            "stars_100" => "TODO_ACH_STARS_100",
            "stars_150" => "TODO_ACH_STARS_150",

            // Kampf
            "kills_100" => "TODO_ACH_KILLS_100",
            "kills_500" => "TODO_ACH_KILLS_500",
            "kills_1000" => "TODO_ACH_KILLS_1000",
            "kick_master" => "TODO_ACH_KICK_MASTER",
            "power_bomber" => "TODO_ACH_POWER_BOMBER",

            // Geschick
            "no_damage" => "TODO_ACH_NO_DAMAGE",
            "efficient" => "TODO_ACH_EFFICIENT",
            "speedrun" => "TODO_ACH_SPEEDRUN",
            "combo3" => "TODO_ACH_COMBO3",
            "combo5" => "TODO_ACH_COMBO5",
            "curse_survivor" => "TODO_ACH_CURSE_SURVIVOR",

            // Arcade
            "arcade_10" => "TODO_ACH_ARCADE_10",
            "arcade_25" => "TODO_ACH_ARCADE_25",

            _ => null
        };

        // Platzhalter-IDs überspringen (noch nicht in Play Console konfiguriert)
        if (gpgsId != null && gpgsId.StartsWith("TODO_"))
            return null;

        return gpgsId;
    }
}
