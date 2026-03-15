using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet globale Community-Bounties via Firebase.
/// Alle Spieler arbeiten gemeinsam an einem Ziel.
/// Bounties rotieren alle 3 Tage.
/// </summary>
public sealed class BountyService : IBountyService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;
    private readonly IPreferencesService _preferences;
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
        ISaveGameService saveGameService,
        IPreferencesService preferences)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
        _preferences = preferences;
    }

    public async Task<BountyDisplayData?> GetActiveBountyAsync()
    {
        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)))
            return _cachedBounty; // Timeout: gecachtes Ergebnis zurückgeben
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

            // Gesamtbeitrag aller Spieler aggregieren (Race-Condition-frei)
            var (totalContributions, ownContribution) = await AggregateContributionsAsync(bountyId);

            // Nutze aggregierten Wert statt des potenziell veralteten bounty.Current
            var current = Math.Max(bounty.Current, totalContributions);

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
                Current = current,
                OwnContribution = ownContribution,
                Reward = bounty.Reward,
                EndDate = endDate,
                IsCompleted = bounty.Status == "completed" || current >= bounty.Target
            };

            _lastBountyCheck = DateTime.UtcNow;
            return _cachedBounty;
        }
        catch
        {
            // Netzwerkfehler still behandelt
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
            var uid = _firebase.PlayerId;
            if (string.IsNullOrEmpty(uid)) return;

            // Eigenen Beitrag aktualisieren (Race-Condition-frei: jeder Spieler hat eigenen Eintrag)
            var existing = await _firebase.GetAsync<BountyContribution>(
                $"bounties/{bountyId}/contributions/{uid}");
            var newAmount = (existing?.Amount ?? 0) + amount;

            await _firebase.SetAsync($"bounties/{bountyId}/contributions/{uid}",
                new BountyContribution
                {
                    Amount = newAmount,
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                });

            // Gesamtfortschritt aggregieren und Status prüfen
            var bounty = await _firebase.GetAsync<CommunityBounty>($"bounties/{bountyId}");
            if (bounty != null && bounty.Status == "active")
            {
                var (totalContributions, _) = await AggregateContributionsAsync(bountyId);
                if (totalContributions >= bounty.Target)
                {
                    await _firebase.UpdateAsync($"bounties/{bountyId}",
                        new Dictionary<string, object>
                        {
                            ["status"] = "completed",
                            ["current"] = totalContributions
                        });
                }
            }

            // Cache invalidieren
            _cachedBounty = null;
        }
        catch
        {
            // Netzwerkfehler still behandelt
        }
    }

    public async Task CheckAndFinalizeBountyAsync()
    {
        try
        {
            var bountyId = GetCurrentBountyId();
            var bounty = await _firebase.GetAsync<CommunityBounty>($"bounties/{bountyId}");
            if (bounty == null) return;

            // Aggregierten Fortschritt prüfen (Race-Condition-frei)
            var (totalContributions, _) = await AggregateContributionsAsync(bountyId);
            var isCompleted = bounty.Status == "completed" || totalContributions >= bounty.Target;

            if (isCompleted)
            {
                // Duplikat-Schutz: Pro Bounty nur einmal belohnen
                var rewardKey = $"bounty_rewarded_{bountyId}";
                if (_preferences.Get(rewardKey, false)) return;

                var uid = _firebase.PlayerId;
                if (string.IsNullOrEmpty(uid)) return;

                // Prüfe ob Spieler beigetragen hat
                var contribution = await _firebase.GetAsync<BountyContribution>(
                    $"bounties/{bountyId}/contributions/{uid}");

                if (contribution != null && contribution.Amount > 0)
                {
                    // Basis-Belohnung für alle Beitragenden (einmalig)
                    _gameStateService.AddGoldenScrews(bounty.Reward);
                    _preferences.Set(rewardKey, true);
                    await _saveGameService.SaveAsync();
                }
            }
        }
        catch
        {
            // Netzwerkfehler still behandelt
        }
    }

    /// <summary>
    /// Aggregiert alle Beiträge einer Bounty (Race-Condition-frei, wie GuildBossService-Pattern).
    /// Gibt (Gesamt, eigener Beitrag) zurück.
    /// </summary>
    private async Task<(long Total, long Own)> AggregateContributionsAsync(string bountyId)
    {
        try
        {
            var json = await _firebase.QueryAsync($"bounties/{bountyId}/contributions", "");
            if (string.IsNullOrEmpty(json) || json == "null")
                return (0, 0);

            var contributions = JsonSerializer.Deserialize<Dictionary<string, BountyContribution>>(json);
            if (contributions == null || contributions.Count == 0)
                return (0, 0);

            long total = 0;
            long own = 0;
            var uid = _firebase.PlayerId;

            foreach (var (playerId, contribution) in contributions)
            {
                total += contribution.Amount;
                if (playerId == uid)
                    own = contribution.Amount;
            }

            return (total, own);
        }
        catch
        {
            return (0, 0);
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
