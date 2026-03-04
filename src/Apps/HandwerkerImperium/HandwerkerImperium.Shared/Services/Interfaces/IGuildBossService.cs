using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet kooperative Gilden-Bosse (wöchentlich, 6 Typen).
/// Mitglieder kämpfen gemeinsam gegen einen Boss mit Timer.
/// </summary>
public interface IGuildBossService
{
    /// <summary>Lädt den aktuell aktiven Boss mit Leaderboard-Daten.</summary>
    Task<GuildBossDisplayData?> GetActiveBossAsync();

    /// <summary>Fügt Schaden am aktiven Boss hinzu.</summary>
    Task DealDamageAsync(long damage, string source);

    /// <summary>Prüft ob der Boss besiegt oder abgelaufen ist.</summary>
    Task CheckBossStatusAsync();

    /// <summary>Spawnt einen neuen Boss falls keiner aktiv ist (wöchentlich).</summary>
    Task<bool> SpawnBossIfNeededAsync();

    /// <summary>Lädt das Schadens-Leaderboard des aktuellen Bosses.</summary>
    Task<List<BossDamageEntry>> GetLeaderboardAsync();
}
