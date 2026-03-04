using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

// ═══════════════════════════════════════════════════════════════════════
// FIREBASE-MODELS (Saison-System)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Saison-Daten für das Gilden-Krieg-System.
/// Pfad: guild_war_seasons/{seasonId}
/// Eine Saison läuft mehrere Wochen mit wöchentlichen Kriegs-Matchups.
/// </summary>
public class GuildWarSeasonData
{
    [JsonPropertyName("startDate")]
    public string StartDate { get; set; } = "";

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; } = "";

    /// <summary>Status der Saison: "active", "completed", "upcoming".</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    /// <summary>Aktuelle Woche innerhalb der Saison (1-basiert).</summary>
    [JsonPropertyName("week")]
    public int Week { get; set; } = 1;
}

/// <summary>
/// Liga-Eintrag einer Gilde innerhalb einer Saison.
/// Pfad: guild_war_seasons/{seasonId}/leagues/{leagueId}/{guildId}
/// </summary>
public class GuildLeagueEntry
{
    /// <summary>Gesammelte Liga-Punkte in dieser Saison.</summary>
    [JsonPropertyName("points")]
    public int Points { get; set; }

    /// <summary>Anzahl gewonnener Kriege.</summary>
    [JsonPropertyName("wins")]
    public int Wins { get; set; }

    /// <summary>Anzahl verlorener Kriege.</summary>
    [JsonPropertyName("losses")]
    public int Losses { get; set; }

    /// <summary>Aktueller Rang innerhalb der Liga.</summary>
    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}

/// <summary>
/// Individueller Spieler-Score in einem Gilden-Krieg.
/// Pfad: guild_wars/{warId}/scores/{guildId}/{playerId}
/// </summary>
public class GuildWarPlayerScore
{
    /// <summary>Punkte aus der Angriffsphase.</summary>
    [JsonPropertyName("attackScore")]
    public long AttackScore { get; set; }

    /// <summary>Punkte aus der Verteidigungsphase.</summary>
    [JsonPropertyName("defenseScore")]
    public long DefenseScore { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";

    /// <summary>Gesamtpunktzahl (Angriff + Verteidigung).</summary>
    [JsonIgnore]
    public long TotalScore => AttackScore + DefenseScore;
}

/// <summary>
/// Log-Eintrag für Kriegs-Ereignisse (Punkte, Phasenwechsel, Ergebnisse).
/// Pfad: guild_wars/{warId}/log/{entryId}
/// </summary>
public class GuildWarLogEntry
{
    /// <summary>Typ des Eintrags: "score", "phase_change", "bonus", "result".</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("guildId")]
    public string GuildId { get; set; } = "";

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; } = "";

    [JsonPropertyName("points")]
    public long Points { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

/// <summary>
/// Bonus-Mission innerhalb eines Kriegs für Extrapunkte.
/// Pfad: guild_wars/{warId}/bonus_missions/{missionId}
/// </summary>
public class WarBonusMission
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Lokalisierungs-Key für den Namen.</summary>
    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = "";

    /// <summary>Lokalisierungs-Key für die Beschreibung.</summary>
    [JsonPropertyName("descKey")]
    public string DescKey { get; set; } = "";

    /// <summary>Zielwert zum Abschließen der Mission.</summary>
    [JsonPropertyName("target")]
    public int Target { get; set; }

    /// <summary>Aktueller Fortschritt.</summary>
    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    /// <summary>Bonuspunkte bei Abschluss.</summary>
    [JsonPropertyName("bonusPoints")]
    public int BonusPoints { get; set; }

    /// <summary>Ob die Mission abgeschlossen ist.</summary>
    [JsonIgnore]
    public bool IsCompleted => Progress >= Target;
}

// ═══════════════════════════════════════════════════════════════════════
// DISPLAY (UI-Daten für ViewModel)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Aufbereitete Anzeige-Daten für die Kriegs-Saison-Übersicht.
/// Kombiniert Saison-, Liga- und aktuelle Kriegs-Daten.
/// </summary>
public class WarSeasonDisplayData
{
    /// <summary>Firebase-ID der aktuellen Saison.</summary>
    public string SeasonId { get; set; } = "";

    /// <summary>Fortlaufende Saison-Nummer (1, 2, 3, ...).</summary>
    public int SeasonNumber { get; set; }

    /// <summary>Aktuelle Woche innerhalb der Saison.</summary>
    public int WeekNumber { get; set; }

    /// <summary>Eigene Liga-Stufe.</summary>
    public GuildLeague OwnLeague { get; set; } = GuildLeague.Bronze;

    /// <summary>Firebase-ID des aktuellen Kriegs (leer wenn Bye-Week).</summary>
    public string WarId { get; set; } = "";

    /// <summary>Name der gegnerischen Gilde.</summary>
    public string OpponentName { get; set; } = "";

    /// <summary>Level der gegnerischen Gilde.</summary>
    public int OpponentLevel { get; set; }

    /// <summary>Eigene Kriegs-Punkte.</summary>
    public long OwnScore { get; set; }

    /// <summary>Gegnerische Kriegs-Punkte.</summary>
    public long OpponentScore { get; set; }

    /// <summary>Aktuelle Phase des Kriegs.</summary>
    public WarPhase CurrentPhase { get; set; } = WarPhase.Attack;

    /// <summary>Wann die aktuelle Phase endet (UTC ISO 8601).</summary>
    public string PhaseEndsAt { get; set; } = "";

    /// <summary>Bonus-Missionen für Extrapunkte.</summary>
    public List<WarBonusMission> BonusMissions { get; set; } = [];

    /// <summary>Name des MVPs (meiste Punkte diese Woche).</summary>
    public string MvpName { get; set; } = "";

    /// <summary>Punktzahl des MVPs.</summary>
    public long MvpScore { get; set; }

    /// <summary>Ob die eigene Gilde in Führung liegt.</summary>
    [JsonIgnore]
    public bool IsLeading => OwnScore > OpponentScore;

    /// <summary>Ob diese Woche ein Freilos ist (kein Gegner).</summary>
    [JsonIgnore]
    public bool IsByeWeek => string.IsNullOrEmpty(WarId);
}
