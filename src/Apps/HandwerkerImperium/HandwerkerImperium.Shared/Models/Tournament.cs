using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Belohnungsstufe im Turnier.
/// </summary>
public enum TournamentRewardTier
{
    None,
    Bronze,
    Silver,
    Gold
}

/// <summary>
/// Ein Eintrag in der Turnier-Bestenliste.
/// </summary>
public class TournamentLeaderboardEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("isPlayer")]
    public bool IsPlayer { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}

/// <summary>
/// Ein wöchentliches MiniGame-Turnier.
/// </summary>
public class Tournament
{
    [JsonPropertyName("weekStart")]
    public DateTime WeekStart { get; set; }

    [JsonPropertyName("gameType")]
    public MiniGameType GameType { get; set; }

    /// <summary>
    /// Die 3 besten Ergebnisse des Spielers.
    /// </summary>
    [JsonPropertyName("bestScores")]
    public List<int> BestScores { get; set; } = [];

    [JsonPropertyName("totalScore")]
    public int TotalScore { get; set; }

    [JsonPropertyName("entriesUsedToday")]
    public int EntriesUsedToday { get; set; }

    [JsonPropertyName("lastEntryDate")]
    public DateTime LastEntryDate { get; set; } = DateTime.MinValue;

    [JsonPropertyName("rewardsClaimed")]
    public bool RewardsClaimed { get; set; }

    /// <summary>
    /// Bestenliste (echte Play Games Einträge oder Fallback-Simulation).
    /// </summary>
    [JsonPropertyName("leaderboard")]
    public List<TournamentLeaderboardEntry> Leaderboard { get; set; } = [];

    /// <summary>
    /// Ob das Leaderboard von echten Play Games Daten stammt.
    /// </summary>
    [JsonPropertyName("isRealLeaderboard")]
    public bool IsRealLeaderboard { get; set; }

    [JsonIgnore]
    public bool IsExpired => DateTime.UtcNow > WeekStart.AddDays(7);

    [JsonIgnore]
    public TimeSpan TimeRemaining => IsExpired ? TimeSpan.Zero : WeekStart.AddDays(7) - DateTime.UtcNow;

    /// <summary>
    /// Freie Teilnahmen heute (3 pro Tag).
    /// </summary>
    [JsonIgnore]
    public int FreeEntriesRemaining
    {
        get
        {
            if (LastEntryDate.Date < DateTime.UtcNow.Date)
                return 3;
            return Math.Max(0, 3 - EntriesUsedToday);
        }
    }

    /// <summary>
    /// Bestimmt die Belohnungsstufe basierend auf dem Rang.
    /// EVENT-2: Erweitert damit fast alle Teilnehmer belohnt werden (nur Rang 10 = None).
    /// </summary>
    public TournamentRewardTier GetRewardTier()
    {
        if (Leaderboard.Count == 0) return TournamentRewardTier.None;
        var playerEntry = Leaderboard.FirstOrDefault(e => e.IsPlayer);
        if (playerEntry == null) return TournamentRewardTier.None;

        return playerEntry.Rank switch
        {
            >= 1 and <= 3 => TournamentRewardTier.Gold,
            >= 4 and <= 6 => TournamentRewardTier.Silver,
            >= 7 and <= 9 => TournamentRewardTier.Bronze,
            _ => TournamentRewardTier.None
        };
    }

    /// <summary>
    /// Fügt einen Score hinzu und aktualisiert die Top-3.
    /// </summary>
    public void AddScore(int score)
    {
        BestScores.Add(score);
        BestScores.Sort((a, b) => b.CompareTo(a));
        if (BestScores.Count > 3)
            BestScores.RemoveRange(3, BestScores.Count - 3);
        TotalScore = BestScores.Sum();

        // Tages-Entry zählen
        if (LastEntryDate.Date < DateTime.UtcNow.Date)
            EntriesUsedToday = 1;
        else
            EntriesUsedToday++;
        LastEntryDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Generiert simulierte Gegner skaliert nach Spieler-Level.
    /// </summary>
    public static List<TournamentLeaderboardEntry> GenerateSimulatedOpponents(int playerLevel)
    {
        var names = new[]
        {
            "HandwerkerMax", "BaumeisterPro", "WerkstattKing", "MeisterFritz",
            "HammerHans", "SchrauberLisa", "ProfiAnna", "BaustelleKurt",
            "WerkzeugOtto"
        };

        var entries = new List<TournamentLeaderboardEntry>();
        int baseScore = Math.Max(100, playerLevel * 15);

        for (int i = 0; i < 9; i++)
        {
            // Scores skalieren mit Spieler-Level (immer erreichbar)
            double factor = 0.4 + Random.Shared.NextDouble() * 1.2;
            int score = (int)(baseScore * factor);
            entries.Add(new TournamentLeaderboardEntry
            {
                Name = names[i],
                Score = score,
                IsPlayer = false
            });
        }

        return entries;
    }
}
