using System.Diagnostics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Bündelt alle periodischen Gilden-Checks die vom GameLoop pro Tick ausgelöst werden.
/// Extrahiert aus GameLoopService um dessen Dependencies zu reduzieren (4 Services → 1).
/// </summary>
public sealed class GuildTickService : IGuildTickService
{
    private readonly IGuildBossService? _bossService;
    private readonly IGuildHallService? _hallService;
    private readonly IGuildAchievementService? _achievementService;
    private readonly IGuildWarSeasonService? _warSeasonService;
    private readonly IWorkerAuctionService? _auctionService;

    private const int BossCheckInterval = 60;       // Offset 20
    private const int HallCheckInterval = 60;       // Offset 40
    private const int AchievementCheckInterval = 300; // Offset 250
    private const int WarSeasonCheckInterval = 300;  // Offset 260
    private const int AuctionCheckInterval = 300;    // 5min — Spawn neue Auktion (Offset 90)
    private const int AuctionBotTickInterval = 5;    // 5s — NPC-Bot-Bidding-Tick (Offset 1)

    public GuildTickService(
        IGuildBossService? bossService = null,
        IGuildHallService? hallService = null,
        IGuildAchievementService? achievementService = null,
        IGuildWarSeasonService? warSeasonService = null,
        IWorkerAuctionService? auctionService = null)
    {
        _bossService = bossService;
        _hallService = hallService;
        _achievementService = achievementService;
        _warSeasonService = warSeasonService;
        _auctionService = auctionService;
    }

    public void ProcessTick(GameState state, int tickCount)
    {
        // Nur prüfen wenn Spieler Gildenmitglied ist (spart Firebase-Calls für Solo-Spieler)
        if (state.GuildMembership?.GuildId == null) return;

        // Boss-Status prüfen (alle 60s, Offset 20)
        if (tickCount % BossCheckInterval == 20 && _bossService != null)
            CheckBossSequentialAsync().FireAndForget();

        // Hauptquartier Upgrade-Completion (alle 60s, Offset 40)
        if (tickCount % HallCheckInterval == 40 && _hallService != null)
            _hallService.CheckUpgradeCompletionAsync().FireAndForget();

        // Achievements prüfen (alle 5 Minuten, Offset 250)
        if (tickCount % AchievementCheckInterval == 250 && _achievementService != null)
            _achievementService.CheckAllAchievementsAsync().FireAndForget();

        // War-Saison Phasenwechsel + Saisonende (alle 5 Minuten, Offset 260)
        if (tickCount % WarSeasonCheckInterval == 260 && _warSeasonService != null)
            CheckWarSeasonSequentialAsync().FireAndForget();

        // v2.1.0: Worker-Auktion alle 5 Minuten refreshen (CurrentAuction-Update + Settle)
        // Offset 90 — anders als die anderen Checks, damit nicht alle gleichzeitig.
        if (tickCount % AuctionCheckInterval == 90 && _auctionService != null)
            AuctionRefreshAndSpawnAsync().FireAndForget();

        // v2.1.0: NPC-Bot-Tick alle 5s waehrend einer aktiven Auktion (Master-Client).
        // Bots bieten zufaellig hoeher — gibt Solo-Spieler Konkurrenz, stoesst auch in
        // groesseren Gilden, wenn niemand sonst bietet, das Hoechstgebot weiter hoch.
        if (tickCount % AuctionBotTickInterval == 1 && _auctionService != null)
            _auctionService.RunNpcBotTickAsync().FireAndForget();
    }

    /// <summary>
    /// Auction: Erst Refresh (Settle abgelaufene), dann Spawn (Master-Client) — sequentiell.
    /// </summary>
    private async Task AuctionRefreshAndSpawnAsync()
    {
        try
        {
            await _auctionService!.RefreshAuctionAsync();
            await _auctionService.SpawnAuctionIfMasterAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuildTick] Auction-Spawn fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Boss: Erst Status prüfen, dann ggf. Spawn (sequentiell, nicht parallel, sonst Race Condition).
    /// </summary>
    private async Task CheckBossSequentialAsync()
    {
        try
        {
            await _bossService!.CheckBossStatusAsync();
            await _bossService.SpawnBossIfNeededAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuildTick] Boss-Check fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// War: CheckPhaseTransition kann Cache nullen → CheckSeasonEnd braucht den alten Cache.
    /// </summary>
    private async Task CheckWarSeasonSequentialAsync()
    {
        try
        {
            await _warSeasonService!.CheckPhaseTransitionAsync();
            await _warSeasonService.CheckSeasonEndAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuildTick] War-Season-Check fehlgeschlagen: {ex.Message}");
        }
    }
}
