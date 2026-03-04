namespace HandwerkerImperium.Models;

// ═══════════════════════════════════════════════════════════════════════
// GILDEN-ENUMS (zentral für alle Gilden-Features)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Rolle eines Spielers innerhalb einer Gilde.
/// </summary>
public enum GuildRole
{
    /// <summary>Normales Mitglied ohne Sonderrechte.</summary>
    Member,

    /// <summary>Offizier mit Einlade- und Kick-Rechten.</summary>
    Officer,

    /// <summary>Gildenleiter mit vollen Administrationsrechten.</summary>
    Leader
}

/// <summary>
/// Liga-Stufe einer Gilde im Saison-System.
/// Bestimmt Matchmaking und Belohnungen.
/// </summary>
public enum GuildLeague
{
    /// <summary>Einstiegs-Liga für neue Gilden.</summary>
    Bronze,

    /// <summary>Mittlere Liga nach ersten Erfolgen.</summary>
    Silver,

    /// <summary>Fortgeschrittene Liga für erfahrene Gilden.</summary>
    Gold,

    /// <summary>Elite-Liga für die besten Gilden.</summary>
    Diamond
}

/// <summary>
/// Phase eines Gilden-Kriegs innerhalb einer Saison-Woche.
/// </summary>
public enum WarPhase
{
    /// <summary>Angriffsphase: Mitglieder sammeln Angriffspunkte.</summary>
    Attack,

    /// <summary>Verteidigungsphase: Gegner greift an, Verteidigungsboni zählen.</summary>
    Defense,

    /// <summary>Auswertungsphase: Ergebnis wird berechnet.</summary>
    Evaluation,

    /// <summary>Krieg abgeschlossen, Belohnungen verteilt.</summary>
    Completed
}

/// <summary>
/// Status eines kooperativen Gilden-Bosses.
/// </summary>
public enum BossStatus
{
    /// <summary>Boss ist aktiv und kann angegriffen werden.</summary>
    Active,

    /// <summary>Boss wurde von der Gilde besiegt.</summary>
    Defeated,

    /// <summary>Boss-Timer abgelaufen ohne Sieg.</summary>
    Expired
}

/// <summary>
/// Gebäude-IDs für das Gilden-Hauptquartier.
/// Jedes Gebäude hat eigene Boni und Upgrade-Stufen.
/// </summary>
public enum GuildBuildingId
{
    /// <summary>Werkstatt: Erhöht Crafting-Geschwindigkeit.</summary>
    Workshop,

    /// <summary>Forschungslabor: Reduziert Forschungszeit.</summary>
    ResearchLab,

    /// <summary>Handelsposten: Erhöht Einkommen.</summary>
    TradingPost,

    /// <summary>Schmiede: Erhöht Auftragsbelohnungen.</summary>
    Smithy,

    /// <summary>Wachturm: Erhöht Kriegs-Punkte.</summary>
    Watchtower,

    /// <summary>Versammlungshalle: Erhöht maximale Mitgliederzahl.</summary>
    AssemblyHall,

    /// <summary>Schatzkammer: Erhöht Wochenziel-Belohnungen.</summary>
    Treasury,

    /// <summary>Festung: Erhöht Verteidigungsbonus im Krieg.</summary>
    Fortress,

    /// <summary>Trophäenhalle: Zeigt Gilden-Achievements an.</summary>
    TrophyHall,

    /// <summary>Meisterthron: Universalbonus auf alles.</summary>
    MasterThrone
}

/// <summary>
/// Typen kooperativer Gilden-Bosse.
/// Jeder Boss hat eigene HP-Skalierung und Schadensregeln.
/// </summary>
public enum GuildBossType
{
    /// <summary>Standard-Boss. Alle Schadensquellen gleich.</summary>
    StoneGolem,

    /// <summary>Crafting-Schaden zählt doppelt.</summary>
    IronTitan,

    /// <summary>Auftrags-Schaden zählt doppelt.</summary>
    MasterArchitect,

    /// <summary>Mini-Game-Schaden zählt doppelt.</summary>
    RustDragon,

    /// <summary>Geldspenden zählen dreifach als Schaden.</summary>
    ShadowTrader,

    /// <summary>24h-Timer, alle Schadensquellen 1.5x. Härtester Boss.</summary>
    ClockworkColossus
}

/// <summary>
/// Kategorien für Gilden-Achievements.
/// </summary>
public enum GuildAchievementCategory
{
    /// <summary>Kooperations-Achievements (Geld, Forschung, Mitglieder).</summary>
    StrongerTogether,

    /// <summary>Kriegs-Achievements (Siege, Saisons, Liga).</summary>
    WarHeroes,

    /// <summary>Boss-Achievements (Kills, MVPs, Speedkills).</summary>
    DragonSlayers,

    /// <summary>Gebäude-Achievements (Upgrades, Hallen-Level).</summary>
    Builders
}

/// <summary>
/// Stufe eines Gilden-Achievements (Bronze/Silver/Gold).
/// Höhere Stufen geben bessere Belohnungen.
/// </summary>
public enum AchievementTier
{
    /// <summary>Einstiegsstufe: 5 Goldschrauben.</summary>
    Bronze,

    /// <summary>Mittlere Stufe: 15 Goldschrauben + Banner-Kosmetik.</summary>
    Silver,

    /// <summary>Höchste Stufe: 30 Goldschrauben + Emblem-Kosmetik.</summary>
    Gold
}
