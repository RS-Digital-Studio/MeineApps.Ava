using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

// ═══════════════════════════════════════════════════════════════════════
// FIREBASE-MODELS (Gilden-Boss-System)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Firebase-Daten eines aktiven Gilden-Bosses.
/// Pfad: guild_bosses/{guildId}/{bossId}
/// </summary>
public class FirebaseGuildBoss
{
    /// <summary>Eindeutige ID dieser Boss-Instanz.</summary>
    [JsonPropertyName("bossId")]
    public string BossId { get; set; } = "";

    /// <summary>Maximale HP des Bosses (bossHpPerLevel * bossLevel).</summary>
    [JsonPropertyName("bossHp")]
    public long BossHp { get; set; }

    /// <summary>Aktuelle HP des Bosses (0 = besiegt).</summary>
    [JsonPropertyName("currentHp")]
    public long CurrentHp { get; set; }

    /// <summary>Level des Bosses (skaliert HP und Belohnungen).</summary>
    [JsonPropertyName("bossLevel")]
    public int BossLevel { get; set; } = 1;

    /// <summary>Wann der Boss gestartet wurde (UTC ISO 8601).</summary>
    [JsonPropertyName("startedAt")]
    public string StartedAt { get; set; } = "";

    /// <summary>Wann der Boss abläuft (UTC ISO 8601).</summary>
    [JsonPropertyName("expiresAt")]
    public string ExpiresAt { get; set; } = "";

    /// <summary>Status: "active", "defeated", "expired".</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";
}

/// <summary>
/// Schaden eines einzelnen Spielers an einem Boss.
/// Pfad: guild_bosses/{guildId}/{bossId}/damage/{playerId}
/// </summary>
public class GuildBossDamage
{
    /// <summary>Gesamt-Schaden des Spielers an diesem Boss.</summary>
    [JsonPropertyName("totalDamage")]
    public long TotalDamage { get; set; }

    /// <summary>Anzahl Angriffe des Spielers.</summary>
    [JsonPropertyName("hits")]
    public int Hits { get; set; }

    /// <summary>Zeitpunkt des letzten Angriffs (UTC ISO 8601).</summary>
    [JsonPropertyName("lastHitAt")]
    public string LastHitAt { get; set; } = "";
}

// ═══════════════════════════════════════════════════════════════════════
// DEFINITION (statisch, 6 Boss-Typen)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Statische Definition eines Boss-Typs.
/// Bestimmt HP-Skalierung, Timer und Schadens-Multiplikatoren.
/// </summary>
public class GuildBossDefinition
{
    /// <summary>Boss-Typ-Enum.</summary>
    public GuildBossType BossType { get; init; }

    /// <summary>Lokalisierungs-Key für den Boss-Namen.</summary>
    public string NameKey { get; init; } = "";

    /// <summary>Lokalisierungs-Key für die Boss-Beschreibung.</summary>
    public string DescKey { get; init; } = "";

    /// <summary>GameIconKind-Name fuer den Boss.</summary>
    public string Icon { get; init; } = "";

    /// <summary>HP pro Boss-Level.</summary>
    public long HpPerLevel { get; init; }

    /// <summary>Timer-Dauer in Stunden.</summary>
    public int DurationHours { get; init; } = 48;

    /// <summary>Multiplikator für Crafting-Schaden.</summary>
    public decimal CraftingDamageMultiplier { get; init; } = 1.0m;

    /// <summary>Multiplikator für Auftrags-Schaden.</summary>
    public decimal OrderDamageMultiplier { get; init; } = 1.0m;

    /// <summary>Multiplikator für Mini-Game-Schaden.</summary>
    public decimal MiniGameDamageMultiplier { get; init; } = 1.0m;

    /// <summary>Multiplikator für Geldspenden-Schaden.</summary>
    public decimal MoneyDonationDamageMultiplier { get; init; } = 1.0m;

    /// <summary>Farbe für UI-Darstellung (Hex).</summary>
    public string Color { get; init; } = "#888888";

    /// <summary>
    /// Gibt alle 6 Boss-Definitionen zurück (gecacht).
    /// </summary>
    private static readonly List<GuildBossDefinition> _allDefinitions =
    [
        // Steingolem: Standard-Boss, alle Schadensquellen gleich
        new()
        {
            BossType = GuildBossType.StoneGolem,
            NameKey = "GuildBoss_StoneGolem",
            DescKey = "GuildBossDesc_StoneGolem",
            Icon = "Wall",
            HpPerLevel = 5_000,
            DurationHours = 48,
            Color = "#78716C"
        },

        // Eisentitan: Crafting-Schaden zählt doppelt
        new()
        {
            BossType = GuildBossType.IronTitan,
            NameKey = "GuildBoss_IronTitan",
            DescKey = "GuildBossDesc_IronTitan",
            Icon = "ShieldSword",
            HpPerLevel = 7_500,
            DurationHours = 48,
            CraftingDamageMultiplier = 2.0m,
            Color = "#475569"
        },

        // Meisterarchitekt: Auftrags-Schaden zählt doppelt
        new()
        {
            BossType = GuildBossType.MasterArchitect,
            NameKey = "GuildBoss_MasterArchitect",
            DescKey = "GuildBossDesc_MasterArchitect",
            Icon = "HardHat",
            HpPerLevel = 6_000,
            DurationHours = 48,
            OrderDamageMultiplier = 2.0m,
            Color = "#D97706"
        },

        // Rostdrache: Mini-Game-Schaden zählt doppelt
        new()
        {
            BossType = GuildBossType.RustDragon,
            NameKey = "GuildBoss_RustDragon",
            DescKey = "GuildBossDesc_RustDragon",
            Icon = "Fire",
            HpPerLevel = 8_000,
            DurationHours = 48,
            MiniGameDamageMultiplier = 2.0m,
            Color = "#DC2626"
        },

        // Schattenhändler: Geldspenden zählen dreifach
        new()
        {
            BossType = GuildBossType.ShadowTrader,
            NameKey = "GuildBoss_ShadowTrader",
            DescKey = "GuildBossDesc_ShadowTrader",
            Icon = "Ninja",
            HpPerLevel = 5_500,
            DurationHours = 48,
            MoneyDonationDamageMultiplier = 3.0m,
            Color = "#6D28D9"
        },

        // Uhrwerk-Koloss: Härtester Boss, 24h, alle 1.5x
        new()
        {
            BossType = GuildBossType.ClockworkColossus,
            NameKey = "GuildBoss_ClockworkColossus",
            DescKey = "GuildBossDesc_ClockworkColossus",
            Icon = "CogSync",
            HpPerLevel = 10_000,
            DurationHours = 24,
            CraftingDamageMultiplier = 1.5m,
            OrderDamageMultiplier = 1.5m,
            MiniGameDamageMultiplier = 1.5m,
            MoneyDonationDamageMultiplier = 1.5m,
            Color = "#0E7490"
        }
    ];

    public static List<GuildBossDefinition> GetAll() => _allDefinitions;

    /// <summary>
    /// Berechnet die maximalen HP für ein bestimmtes Boss-Level.
    /// </summary>
    public long CalculateHp(int level) => HpPerLevel * Math.Max(1, level);
}

// ═══════════════════════════════════════════════════════════════════════
// DISPLAY (UI-Daten für ViewModel)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Aufbereitete Anzeige-Daten für den aktuellen Gilden-Boss.
/// </summary>
public class GuildBossDisplayData
{
    /// <summary>Typ des Bosses.</summary>
    public GuildBossType BossType { get; set; }

    /// <summary>Lokalisierter Boss-Name.</summary>
    public string BossName { get; set; } = "";

    /// <summary>Maximale HP.</summary>
    public long MaxHp { get; set; }

    /// <summary>Aktuelle HP.</summary>
    public long CurrentHp { get; set; }

    /// <summary>Ablaufzeitpunkt (UTC ISO 8601).</summary>
    public string ExpiresAt { get; set; } = "";

    /// <summary>Status des Bosses.</summary>
    public BossStatus Status { get; set; } = BossStatus.Active;

    /// <summary>Eigener Gesamt-Schaden.</summary>
    public long OwnDamage { get; set; }

    /// <summary>Eigene Anzahl Angriffe.</summary>
    public int OwnHits { get; set; }

    /// <summary>Eigener Rang im Schadens-Leaderboard.</summary>
    public int OwnRank { get; set; }

    /// <summary>Top-Schadens-Leaderboard.</summary>
    public List<BossDamageEntry> Leaderboard { get; set; } = [];

    /// <summary>HP-Prozent (0.0 - 1.0).</summary>
    [JsonIgnore]
    public double HpPercent => MaxHp > 0
        ? Math.Clamp((double)CurrentHp / MaxHp, 0.0, 1.0) : 0.0;

    /// <summary>Verbleibende Zeit bis zum Ablauf.</summary>
    [JsonIgnore]
    public TimeSpan TimeRemaining
    {
        get
        {
            if (string.IsNullOrEmpty(ExpiresAt)) return TimeSpan.Zero;
            if (DateTime.TryParse(ExpiresAt, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var expires))
            {
                var remaining = expires - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            return TimeSpan.Zero;
        }
    }
}

/// <summary>
/// Eintrag im Boss-Schadens-Leaderboard.
/// </summary>
public class BossDamageEntry
{
    /// <summary>Spieler-ID (stabile Identität, überlebt Namensänderungen).</summary>
    public string PlayerId { get; set; } = "";

    /// <summary>Spieler-Name (nur für Anzeige).</summary>
    public string PlayerName { get; set; } = "";

    /// <summary>Gesamt-Schaden am Boss.</summary>
    public long Damage { get; set; }

    /// <summary>Anzahl Angriffe.</summary>
    public int Hits { get; set; }
}
