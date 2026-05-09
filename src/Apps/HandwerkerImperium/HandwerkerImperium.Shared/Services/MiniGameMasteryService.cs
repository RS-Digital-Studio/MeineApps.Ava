using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Default-Implementierung des Mastery-Systems (v2.0.36).
///
/// Subscribed auf <see cref="IGameStateService.PerfectRatingIncremented"/> und prueft
/// nach jedem Perfect-Rating, ob das LifetimePerfectRatingCounts-Increment ein neues
/// Tier freischaltet (Bronze 50 / Silver 200 / Gold 1000). Belohnung 5 / 15 / 50 GS.
/// </summary>
public sealed class MiniGameMasteryService : IMiniGameMasteryService, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private bool _disposed;

    public MiniGameMasteryService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
        _gameStateService.PerfectRatingIncremented += OnPerfectRatingIncrementedFromState;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gameStateService.PerfectRatingIncremented -= OnPerfectRatingIncrementedFromState;
    }

    private void OnPerfectRatingIncrementedFromState(object? sender, PerfectRatingIncrementedEventArgs e)
    {
        OnPerfectRatingRecorded(e.MiniGameType);
    }

    /// <inheritdoc />
    public event EventHandler<MasteryTierUnlockedEventArgs>? MasteryTierUnlocked;

    /// <inheritdoc />
    public MiniGameMasteryTier GetCurrentTier(MiniGameType type)
    {
        int count = GetLifetimePerfectCount(type);
        return MiniGameMasteryThresholds.GetTierForCount(count);
    }

    /// <inheritdoc />
    public int GetLifetimePerfectCount(MiniGameType type)
    {
        var dict = _gameStateService.State.LifetimePerfectRatingCounts;
        return dict != null && dict.TryGetValue((int)type, out int count) ? count : 0;
    }

    /// <inheritdoc />
    public int? GetNextTierThreshold(MiniGameType type)
    {
        var current = GetCurrentTier(type);
        return current switch
        {
            MiniGameMasteryTier.None => MiniGameMasteryThresholds.BronzeThreshold,
            MiniGameMasteryTier.Bronze => MiniGameMasteryThresholds.SilverThreshold,
            MiniGameMasteryTier.Silver => MiniGameMasteryThresholds.GoldThreshold,
            _ => null
        };
    }

    /// <inheritdoc />
    public void OnPerfectRatingRecorded(MiniGameType type)
    {
        var state = _gameStateService.State;

        // Defensive Init: ClaimedTiers-Dict kann null sein bei alten Save-Versions.
        // Lifetime-Counter wurde bereits in GameStateService.RecordPerfectRating
        // unter dem State-Lock atomar inkrementiert.
        state.ClaimedMiniGameMasteryTiers ??= new Dictionary<int, int>();

        int key = (int)type;
        int newCount = state.LifetimePerfectRatingCounts?.GetValueOrDefault(key, 0) ?? 0;

        // Aktuell hoechstes Tier basierend auf neuem Count.
        var currentTier = MiniGameMasteryThresholds.GetTierForCount(newCount);
        int currentTierInt = (int)currentTier;

        // Was hatte der Spieler bereits geclaimed?
        int claimedTier = state.ClaimedMiniGameMasteryTiers.GetValueOrDefault(key, 0);

        // Neues Tier erreicht? Falls ja, Belohnung gutschreiben + Event feuern.
        if (currentTierInt > claimedTier)
        {
            // Alle ueberschrittenen Tiers in Reihenfolge ausschuetten — falls der Spieler
            // z.B. von 0 direkt auf 200 (Silver) springt (z.B. via Cheat oder Save-Edit),
            // bekommt er Bronze + Silver Belohnungen beide.
            for (int t = claimedTier + 1; t <= currentTierInt; t++)
            {
                int reward = MiniGameMasteryThresholds.GoldenScrewRewards[t];
                _gameStateService.AddGoldenScrews(reward);
                state.ClaimedMiniGameMasteryTiers[key] = t;

                MasteryTierUnlocked?.Invoke(this, new MasteryTierUnlockedEventArgs
                {
                    MiniGameType = type,
                    Tier = (MiniGameMasteryTier)t,
                    GoldenScrewReward = reward
                });
            }
        }
    }
}
