using System.Globalization;
using System.Text.Json;
using BomberBlast.Models.BattlePass;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Verwaltet den Battle Pass: XP-Tracking, Tier-Fortschritt, Belohnungs-Claims, Saison-Management.
/// Persistenz via IPreferencesService (JSON).
/// </summary>
public class BattlePassService : IBattlePassService
{
    private const string DATA_KEY = "BattlePassData";

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ICardService _cardService;
    private IAchievementService? _achievementService;

    private BattlePassData _data;
    private readonly BattlePassReward[] _freeRewards;
    private readonly BattlePassReward[] _premiumRewards;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private const int XP_BOOST_GEM_COST = 20;
    private const int XP_BOOST_DURATION_HOURS = 24;

    public BattlePassData Data => _data;
    public bool IsSeasonActive => !_data.IsSeasonExpired;
    public bool IsPremium => _data.IsPremium;
    public int CurrentTier => _data.CurrentTier;
    public int DaysRemaining => _data.DaysRemaining;

    public bool IsXpBoostActive
    {
        get
        {
            if (string.IsNullOrEmpty(_data.XpBoostExpiresAt)) return false;
            try
            {
                var expires = DateTime.Parse(_data.XpBoostExpiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                return DateTime.UtcNow < expires;
            }
            catch { return false; }
        }
    }

    public DateTime? XpBoostExpiresAt
    {
        get
        {
            if (string.IsNullOrEmpty(_data.XpBoostExpiresAt)) return null;
            try
            {
                var expires = DateTime.Parse(_data.XpBoostExpiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                return DateTime.UtcNow < expires ? expires : null;
            }
            catch { return null; }
        }
    }

    public event Action? BattlePassChanged;
    public event Action<int>? TierUpReached;

    /// <summary>Lazy-Injection um zirkuläre DI-Abhängigkeit zu vermeiden</summary>
    public void SetAchievementService(IAchievementService achievementService) => _achievementService = achievementService;

    public BattlePassService(
        IPreferencesService preferences,
        ICoinService coinService,
        IGemService gemService,
        ICardService cardService)
    {
        _preferences = preferences;
        _coinService = coinService;
        _gemService = gemService;
        _cardService = cardService;

        _data = LoadData();
        _freeRewards = BattlePassTierDefinitions.GetFreeRewards();
        _premiumRewards = BattlePassTierDefinitions.GetPremiumRewards();

        // Prüfen ob neue Saison nötig
        CheckAndStartNewSeason();
    }

    public bool ActivateXpBoost()
    {
        if (IsXpBoostActive) return false;
        if (!_gemService.TrySpendGems(XP_BOOST_GEM_COST)) return false;

        _data.XpBoostExpiresAt = DateTime.UtcNow.AddHours(XP_BOOST_DURATION_HOURS).ToString("O");
        SaveData();
        BattlePassChanged?.Invoke();
        return true;
    }

    public int AddXp(int amount, string source = "")
    {
        if (_data.IsSeasonExpired) return 0;
        if (_data.CurrentTier >= BattlePassTierDefinitions.MaxTier) return 0;

        // 2x XP-Boost anwenden
        if (IsXpBoostActive)
            amount *= 2;

        int tierUps = _data.AddXp(amount);

        if (tierUps > 0)
        {
            for (int i = _data.CurrentTier - tierUps + 1; i <= _data.CurrentTier; i++)
                TierUpReached?.Invoke(i);

            // Achievement: Höchstes Battle-Pass-Tier prüfen
            _achievementService?.OnBattlePassTierReached(_data.CurrentTier);
        }

        SaveData();
        BattlePassChanged?.Invoke();
        return tierUps;
    }

    public BattlePassReward? ClaimReward(int tierIndex, bool isPremiumReward)
    {
        // Validierung
        if (tierIndex < 0 || tierIndex >= BattlePassTierDefinitions.MaxTier) return null;
        if (tierIndex >= _data.CurrentTier) return null; // Tier noch nicht erreicht

        if (isPremiumReward)
        {
            if (!_data.IsPremium) return null;
            if (_data.ClaimedPremiumTiers.Contains(tierIndex)) return null;

            var reward = _premiumRewards[tierIndex];
            ApplyReward(reward);
            _data.ClaimedPremiumTiers.Add(tierIndex);
            SaveData();
            BattlePassChanged?.Invoke();
            return reward;
        }
        else
        {
            if (_data.ClaimedFreeTiers.Contains(tierIndex)) return null;

            var reward = _freeRewards[tierIndex];
            ApplyReward(reward);
            _data.ClaimedFreeTiers.Add(tierIndex);
            SaveData();
            BattlePassChanged?.Invoke();
            return reward;
        }
    }

    public bool CheckAndStartNewSeason()
    {
        if (!_data.IsSeasonExpired) return false;

        // Neue Saison starten
        _data = new BattlePassData
        {
            SeasonNumber = _data.SeasonNumber + 1,
            SeasonStartDate = DateTime.UtcNow.ToString("O")
        };

        SaveData();
        BattlePassChanged?.Invoke();
        return true;
    }

    public void ActivatePremium()
    {
        if (_data.IsPremium) return;
        _data.IsPremium = true;
        SaveData();
        BattlePassChanged?.Invoke();
    }

    // === Belohnungen anwenden ===

    private void ApplyReward(BattlePassReward reward)
    {
        switch (reward.Type)
        {
            case BattlePassRewardType.Coins:
                _coinService.AddCoins(reward.Amount);
                break;
            case BattlePassRewardType.Gems:
                _gemService.AddGems(reward.Amount);
                break;
            case BattlePassRewardType.CardPack:
                // Pro Pack eine zufällige Karte droppen (Welt 5 = Mid-Game Drop-Rate)
                for (int i = 0; i < reward.Amount; i++)
                {
                    var drop = _cardService.GenerateDrop(worldNumber: 5);
                    if (drop.HasValue)
                        _cardService.AddCard(drop.Value);
                }
                break;
            case BattlePassRewardType.Cosmetic:
                // Cosmetic-Rewards werden über ItemId verwaltet
                // Temporärer Fallback bis Kosmetik-Items existieren
                _coinService.AddCoins(5000);
                break;
        }
    }

    // === Persistenz ===

    private BattlePassData LoadData()
    {
        var json = _preferences.Get(DATA_KEY, "");
        if (string.IsNullOrEmpty(json)) return new BattlePassData();

        try
        {
            return JsonSerializer.Deserialize<BattlePassData>(json, JsonOptions) ?? new BattlePassData();
        }
        catch
        {
            return new BattlePassData();
        }
    }

    private void SaveData()
    {
        var json = JsonSerializer.Serialize(_data, JsonOptions);
        _preferences.Set(DATA_KEY, json);
    }
}
