using System.Globalization;

namespace BomberBlast.Services;

/// <summary>
/// Wochen-Content-Drop-Pipeline (.4 .
///
/// <para>
/// Liefert deterministische Wochen-spezifische Inhalte ohne App-Update:
/// Wochen-Mutator-Kombi, Wochen-Hand-Layout, Wochen-Lucky-Spin-Reward, Wochen-Boss-Modifier-Set.
/// </para>
///
/// <para>
/// Determinismus via ISO-Wochen-Seed: gleiche Woche = gleiche Inhalte fuer alle Spieler weltweit.
/// Per Remote-Config kann der Wochen-Plan ueberschrieben werden — Pipeline-Code
/// bleibt unveraendert, neue Inhalte kommen per RemoteConfig + JSON-Definition.
/// </para>
///
/// <para>
/// Aufwand: Initial 2 Wochen (Pipeline + Hand-Layouts), dann 2-4h pro Woche (1 Map + Modifier-Pick).
/// </para>
/// </summary>
public interface IWeeklyContentService
{
    /// <summary>Aktuelle ISO-Wochen-Nummer + Jahr (kombiniert als yyyyW01).</summary>
    string CurrentIsoWeekId { get; }

    /// <summary>Mutator-Kombi der aktuellen Woche (z.B. "Ice + SpeedDrain"). Null bei "kein Wochen-Modifier".</summary>
    WeeklyModifier? GetCurrentWeekModifier();

    /// <summary>
    /// Lucky-Spin-Bonus-Reward der aktuellen Woche (z.B. limitiertes Cosmetic).
    /// Null = nur Standard-Belohnungen.
    /// </summary>
    WeeklyReward? GetCurrentWeekLuckySpinBonus();

    /// <summary>
    /// Wochen-Boss-Modifier-Pool. Aus diesem Pool wird der Boss-Encounter der Woche
    /// modifiziert (siehe.1 BossModifier).
    /// </summary>
    string[] GetCurrentWeekBossModifiers();
}

/// <summary>Wochen-Modifier-Definition.</summary>
public sealed class WeeklyModifier
{
    public required string Id { get; init; }
    public required string NameKey { get; init; }
    public required string DescKey { get; init; }
}

/// <summary>Wochen-Reward (typisch: limitiertes Cosmetic).</summary>
public sealed class WeeklyReward
{
    public required string Id { get; init; }
    public required string NameKey { get; init; }
    public required string IconAssetPath { get; init; }
}

/// <summary>
/// Default-Implementation: ISO-Wochen-Seed mit Round-Robin durch Modifier/Reward-Pools.
/// </summary>
public sealed class WeeklyContentService : IWeeklyContentService
{
    /// <summary>Pool aus 8 Wochen-Modifier — rotiert deterministisch pro ISO-Woche.</summary>
    private static readonly WeeklyModifier[] ModifierPool =
    {
        new() { Id = "wm_ice_speed", NameKey = "WeeklyModifierIceSpeedName", DescKey = "WeeklyModifierIceSpeedDesc" },
        new() { Id = "wm_double_bombs", NameKey = "WeeklyModifierDoubleBombsName", DescKey = "WeeklyModifierDoubleBombsDesc" },
        new() { Id = "wm_phantom_walls", NameKey = "WeeklyModifierPhantomWallsName", DescKey = "WeeklyModifierPhantomWallsDesc" },
        new() { Id = "wm_no_timer", NameKey = "WeeklyModifierNoTimerName", DescKey = "WeeklyModifierNoTimerDesc" },
        new() { Id = "wm_mirror_controls", NameKey = "WeeklyModifierMirrorName", DescKey = "WeeklyModifierMirrorDesc" },
        new() { Id = "wm_giant_blasts", NameKey = "WeeklyModifierGiantName", DescKey = "WeeklyModifierGiantDesc" },
        new() { Id = "wm_quick_curse", NameKey = "WeeklyModifierQuickCurseName", DescKey = "WeeklyModifierQuickCurseDesc" },
        new() { Id = "wm_double_coins", NameKey = "WeeklyModifierDoubleCoinsName", DescKey = "WeeklyModifierDoubleCoinsDesc" },
    };

    /// <summary>Pool aus 4 Wochen-Lucky-Spin-Rewards (rotiert deterministisch).</summary>
    private static readonly WeeklyReward[] RewardPool =
    {
        new() { Id = "wr_neon_trail", NameKey = "WeeklyRewardNeonTrailName", IconAssetPath = "trails/neon_pulse" },
        new() { Id = "wr_gold_frame", NameKey = "WeeklyRewardGoldFrameName", IconAssetPath = "frames/gold_seasonal" },
        new() { Id = "wr_emerald_burst", NameKey = "WeeklyRewardEmeraldName", IconAssetPath = "victories/emerald_burst" },
        new() { Id = "wr_phoenix_aura", NameKey = "WeeklyRewardPhoenixName", IconAssetPath = "auras/phoenix" },
    };

    public string CurrentIsoWeekId
    {
        get
        {
            var now = DateTime.UtcNow;
            int week = ISOWeek.GetWeekOfYear(now);
            int year = ISOWeek.GetYear(now);
            return $"{year}W{week:D2}";
        }
    }

    public WeeklyModifier? GetCurrentWeekModifier()
    {
        // Deterministisch: ISO-Woche × 7 + Jahr-mod-Bias als Pool-Index
        var now = DateTime.UtcNow;
        int week = ISOWeek.GetWeekOfYear(now);
        int year = ISOWeek.GetYear(now);
        int index = ((year * 7) + week) % ModifierPool.Length;
        return ModifierPool[index];
    }

    public WeeklyReward? GetCurrentWeekLuckySpinBonus()
    {
        var now = DateTime.UtcNow;
        int week = ISOWeek.GetWeekOfYear(now);
        int year = ISOWeek.GetYear(now);
        int index = ((year * 5) + week) % RewardPool.Length;  // anderer Bias als Modifier
        return RewardPool[index];
    }

    public string[] GetCurrentWeekBossModifiers()
    {
        // Deterministische Auswahl von 3 Modifiern aus den 8 BossModifier-Werten.
        var now = DateTime.UtcNow;
        int week = ISOWeek.GetWeekOfYear(now);
        int year = ISOWeek.GetYear(now);
        int seed = (year * 11) + week;
        var allBossModifiers = new[]
        {
            "Shielded", "Fast", "Healing", "Summoner",
            "Frenzy", "Berserk", "Reflective", "Burning",
        };
        var rng = new Random(seed);
        // Fisher-Yates-Shuffle, dann erste 3 nehmen.
        var copy = (string[])allBossModifiers.Clone();
        for (int i = copy.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return new[] { copy[0], copy[1], copy[2] };
    }
}
