using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet globale Community-Bounties via Firebase.
/// Alle Spieler arbeiten gemeinsam an einem Ziel.
/// Bounties rotieren alle 3 Tage.
/// </summary>
public class BountyService : IBountyService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Bounty-Typen mit Zielen
    private static readonly (string Type, string DisplayKey, long Target, int Reward)[] BountyTypes =
    [
        ("orders", "BountyOrders", 10_000, 10),
        ("minigames", "BountyMiniGames", 5_000, 10),
        ("upgrades", "BountyUpgrades", 20_000, 10),
        ("crafting", "BountyCrafting", 3_000, 10),
        ("prestige", "BountyPrestige", 100, 10)
    ];

    private const int TopContributorBonus = 20; // Extra Goldschrauben für Top-10

    // Cache
    private BountyDisplayData? _cachedBounty;
    private DateTime _lastBountyCheck = DateTime.MinValue;
    private static readonly TimeSpan BountyCheckCooldown = TimeSpan.FromMinutes(2);

    public BountyService(
        IFirebaseService firebase,
        IGameStateService gameStateService,
        ISaveGameService saveGameService)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
    }

    public async Task<BountyDisplayData?> GetActiveBountyAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Cache prüfen
            if (_cachedBounty != null && DateTime.UtcNow - _lastBountyCheck < BountyCheckCooldown)
                return _cachedBounty;

            var bountyId = GetCurrentBountyId();

            // Bounty laden
            var bounty = await _firebase.GetAsync<CommunityBounty>($"bounties/{bountyId}");

            if (bounty == null)
            {
                // Neue Bounty erstellen
                bounty = CreateNewBounty(bountyId);
                await _firebase.SetAsync($"bounties/{bountyId}", bounty);
            }

            // Eigenen Beitrag laden
            long ownContribution = 0;
            var uid = _firebase.Uid;
            if (!string.IsNullOrEmpty(uid))
            {
                var contribution = await _firebase.GetAsync<BountyContribution>(
                    $"bounties/{bountyId}/contributions/{uid}");
                ownContribution = contribution?.Amount ?? 0;
            }

            var endDate = DateTime.TryParse(bounty.EndDate, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var ed) ? ed : DateTime.UtcNow.AddDays(3);

            // DisplayKey aus Typ bestimmen
            var typeInfo = BountyTypes.FirstOrDefault(bt => bt.Type == bounty.Type);

            _cachedBounty = new BountyDisplayData
            {
                BountyId = bountyId,
                Type = bounty.Type,
                TypeDisplayKey = typeInfo.DisplayKey ?? "BountyOrders",
                Target = bounty.Target,
                Current = bounty.Current,
                OwnContribution = ownContribution,
                Reward = bounty.Reward,
                EndDate = endDate,
                IsCompleted = bounty.Status == "completed" || bounty.Current >= bounty.Target
            };

            _lastBountyCheck = DateTime.UtcNow;
            return _cachedBounty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in GetActiveBountyAsync: {ex.Message}");
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ContributeAsync(string bountyType, long amount)
    {
        if (amount <= 0) return;

        try
        {
            var bountyId = GetCurrentBountyId();
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            // Eigenen Beitrag aktualisieren
            var existing = await _firebase.GetAsync<BountyContribution>(
                $"bounties/{bountyId}/contributions/{uid}");
            var newAmount = (existing?.Amount ?? 0) + amount;

            await _firebase.SetAsync($"bounties/{bountyId}/contributions/{uid}",
                new BountyContribution
                {
                    Amount = newAmount,
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                });

            // Globalen Counter aktualisieren (Read-Modify-Write)
            var bounty = await _firebase.GetAsync<CommunityBounty>($"bounties/{bountyId}");
            if (bounty != null && bounty.Status == "active")
            {
                var newCurrent = bounty.Current + amount;
                var updates = new Dictionary<string, object> { ["current"] = newCurrent };

                // Prüfe ob Ziel erreicht
                if (newCurrent >= bounty.Target)
                {
                    updates["status"] = "completed";
                }

                await _firebase.UpdateAsync($"bounties/{bountyId}", updates);
            }

            // Cache invalidieren
            _cachedBounty = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in ContributeAsync: {ex.Message}");
        }
    }

    public async Task CheckAndFinalizeBountyAsync()
    {
        try
        {
            var bountyId = GetCurrentBountyId();
            var bounty = await _firebase.GetAsync<CommunityBounty>($"bounties/{bountyId}");
            if (bounty == null) return;

            // Prüfe ob Ziel erreicht und noch nicht belohnt
            if (bounty.Current >= bounty.Target || bounty.Status == "completed")
            {
                var uid = _firebase.Uid;
                if (string.IsNullOrEmpty(uid)) return;

                // Prüfe ob Spieler beigetragen hat
                var contribution = await _firebase.GetAsync<BountyContribution>(
                    $"bounties/{bountyId}/contributions/{uid}");

                if (contribution != null && contribution.Amount > 0)
                {
                    // Basis-Belohnung für alle Beitragenden
                    _gameStateService.AddGoldenScrews(bounty.Reward);
                    await _saveGameService.SaveAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in CheckAndFinalizeBountyAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// Generiert eine Bounty-ID basierend auf dem aktuellen Datum (alle 3 Tage).
    /// </summary>
    private static string GetCurrentBountyId()
    {
        var now = DateTime.UtcNow;
        // Alle 3 Tage eine neue Bounty (basierend auf Tag des Jahres)
        var period = now.DayOfYear / 3;
        return $"b_{now.Year}_{period:D3}";
    }

    /// <summary>
    /// Erstellt eine neue Bounty basierend auf der Rotation.
    /// </summary>
    private static CommunityBounty CreateNewBounty(string bountyId)
    {
        // Typ rotiert basierend auf Bounty-ID Hash
        var typeIndex = Math.Abs(bountyId.GetHashCode()) % BountyTypes.Length;
        var (type, _, target, reward) = BountyTypes[typeIndex];

        var now = DateTime.UtcNow;
        return new CommunityBounty
        {
            Type = type,
            Target = target,
            Current = 0,
            Reward = reward,
            StartDate = now.ToString("O"),
            EndDate = now.AddDays(3).ToString("O"),
            Status = "active"
        };
    }
}
