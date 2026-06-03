using System.Globalization;
using System.Text.Json;
using BomberBlast.Models.BattlePass;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Verwaltet den Battle Pass: XP-Tracking, Tier-Fortschritt, Belohnungs-Claims, Saison-Management.
/// Persistenz via IPreferencesService (JSON).
/// </summary>
public sealed class BattlePassService : IBattlePassService
{
    private const string DATA_KEY = "BattlePassData";

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ICardService _cardService;
    private readonly Lazy<IAchievementService> _achievementService;

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

    // Gecachtes XP-Boost-Ablaufdatum (vermeidet DateTime.Parse bei jedem Aufruf)
    private DateTime? _xpBoostExpires;
    // Audit H14: Monotoner Tick-Anchor — Boost gilt zusaetzlich nur solange (now - StartTicks) < Duration.
    // Verhindert Manipulation: Datum zurueckstellen → Boost wuerde sonst laenger als 24h aktiv bleiben.
    private long _xpBoostStartTicks;
    private const long XpBoostDurationTicks = (long)XP_BOOST_DURATION_HOURS * 60 * 60 * 1000;

    public bool IsXpBoostActive
    {
        get
        {
            if (!_xpBoostExpires.HasValue) return false;
            // Klassischer DateTime-Check
            if (DateTime.UtcNow >= _xpBoostExpires.Value) return false;
            // Monotoner Tick-Check: Wenn Process laenger als Boost-Dauer laeuft seit StartTicks → abgelaufen.
            // Bei Reboot/Counter-Wrap (now < StartTicks) verlasse uns auf DateTime-Check (Reboot = neue Session).
            long now = Environment.TickCount64;
            if (_xpBoostStartTicks <= now && (now - _xpBoostStartTicks) >= XpBoostDurationTicks)
                return false;
            return true;
        }
    }

    public DateTime? XpBoostExpiresAt => IsXpBoostActive ? _xpBoostExpires : null;

    public event Action? BattlePassChanged;
    public event Action<int>? TierUpReached;

    public BattlePassService(
        IPreferencesService preferences,
        ICoinService coinService,
        IGemService gemService,
        ICardService cardService,
        Lazy<IAchievementService> achievementService)
    {
        _preferences = preferences;
        _coinService = coinService;
        _gemService = gemService;
        _cardService = cardService;
        _achievementService = achievementService;

        _data = LoadData();
        _freeRewards = BattlePassTierDefinitions.GetFreeRewards();
        _premiumRewards = BattlePassTierDefinitions.GetPremiumRewards();

        // XP-Boost-Cache initialisieren
        CacheXpBoostExpiry();

        // Prüfen ob neue Saison nötig
        CheckAndStartNewSeason();
    }

    /// <summary>Parst XpBoostExpiresAt einmalig und cacht als DateTime</summary>
    private void CacheXpBoostExpiry()
    {
        // Audit H14: Tick-Anchor aus persistierten Daten uebernehmen.
        _xpBoostStartTicks = _data.XpBoostStartTicks;

        if (string.IsNullOrEmpty(_data.XpBoostExpiresAt))
        {
            _xpBoostExpires = null;
            return;
        }
        try
        {
            var expires = DateTime.Parse(_data.XpBoostExpiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            _xpBoostExpires = DateTime.UtcNow < expires ? expires : null;
        }
        catch { _xpBoostExpires = null; }
    }

    public bool ActivateXpBoost()
    {
        if (IsXpBoostActive) return false;
        if (!_gemService.TrySpendGems(XP_BOOST_GEM_COST)) return false;

        // Ablaufdatum = jetzt + 24h. Anti-Cheat-Hybrid (Audit H14): Zusaetzlich ein monotoner
        // Tick-Anchor → bei Datum-Zurueckstellen wuerde der Tick-Check Boost beenden.
        var expiry = DateTime.UtcNow.AddHours(XP_BOOST_DURATION_HOURS);
        _data.XpBoostExpiresAt = expiry.ToString("O");
        _data.XpBoostStartTicks = Environment.TickCount64;
        _xpBoostExpires = expiry;
        _xpBoostStartTicks = _data.XpBoostStartTicks;
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

        // Premium-Veteranen-Boost (+5% kumulativ pro abgeschlossener Premium-Saison) anwenden.
        if (PremiumVeteranBoost > 1.0f)
            amount = (int)Math.Round(amount * PremiumVeteranBoost);

        int tierUps = _data.AddXp(amount);

        if (tierUps > 0)
        {
            for (int i = _data.CurrentTier - tierUps + 1; i <= _data.CurrentTier; i++)
                TierUpReached?.Invoke(i);

            // Achievement: Höchstes Battle-Pass-Tier prüfen
            _achievementService.Value.OnBattlePassTierReached(_data.CurrentTier);
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

        // v2.0.60 (B-D7): Legacy-Rewards-Fenster — Premium-Spieler mit unclaimed Rewards
        // bekommen 7 Tage nach Season-Ende noch die Möglichkeit, ihre Tier-Rewards einzulösen.
        // Bisheriges Whale-Verhalten: kompletter Reset → unclaimed Premium-Rewards weg →
        // Whale-Retention-Killer. Jetzt: Snapshot der unclaimed Tiers + Premium-Status,
        // wird im Legacy-Fenster (LegacyRewardsExpiresAt) abgerufen.
        if (_data.IsPremium && _data.CurrentTier >= 1)
        {
            _data.LegacyRewardsExpiresAt = DateTime.UtcNow.AddDays(7).ToString("O");
            _data.LegacyPremiumTier = _data.CurrentTier;
            _data.LegacyPremiumSeasonNumber = _data.SeasonNumber;
            // Persistenter Veteran-Bonus für Premium-Veteranen: +5% XP in der nächsten Saison.
            _data.PremiumVeteranSeasonsCompleted = _data.PremiumVeteranSeasonsCompleted + 1;
        }

        // Neue Saison starten
        _data = new BattlePassData
        {
            SeasonNumber = _data.SeasonNumber + 1,
            SeasonStartDate = DateTime.UtcNow.ToString("O"),
            // v2.0.60 (B-D7): Legacy-Felder ueber den Reset hinaus erhalten.
            LegacyRewardsExpiresAt = _data.LegacyRewardsExpiresAt,
            LegacyPremiumTier = _data.LegacyPremiumTier,
            LegacyPremiumSeasonNumber = _data.LegacyPremiumSeasonNumber,
            PremiumVeteranSeasonsCompleted = _data.PremiumVeteranSeasonsCompleted,
        };

        SaveData();
        BattlePassChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// v2.0.60 (B-D7): True wenn der Spieler unclaimed Premium-Rewards aus der vorherigen
    /// Saison hat und das 7-Tage-Fenster noch aktiv ist. UI zeigt einen "Legacy"-Banner.
    /// </summary>
    public bool HasUnclaimedLegacyRewards
    {
        get
        {
            if (string.IsNullOrEmpty(_data.LegacyRewardsExpiresAt)) return false;
            if (_data.LegacyPremiumTier < 1) return false;
            try
            {
                var expires = DateTime.Parse(_data.LegacyRewardsExpiresAt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                return DateTime.UtcNow < expires;
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// v2.0.60 (B-D7): Veteran-XP-Boost +5% pro abgeschlossener Premium-Saison.
    /// Wird vom XP-Anstieg-Pfad ausgewertet (jede Premium-Saison addiert kumulativen Boost).
    /// </summary>
    public float PremiumVeteranBoost
        => 1.0f + (_data.PremiumVeteranSeasonsCompleted * 0.05f);

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
                // Cosmetic-Rewards: Gems als Fallback bis Kosmetik-Items existieren
                // Tier 10 (Rare) = 15 Gems, Tier 20 (Epic) = 25 Gems, Tier 30 (Legendary) = 50 Gems
                int cosmeticGems = reward.ItemId switch
                {
                    "season_skin_rare" => 15,
                    "season_skin_epic" => 25,
                    "season_skin_legendary" => 50,
                    _ => 15
                };
                _gemService.AddGems(cosmeticGems);
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
        catch (Exception ex)
        {
            // v2.0.60 (B-E4): Corruption-Reporting für CloudSave-Cloud-Bevorzugung.
            PersistenceHealth.ReportCorruption(nameof(BattlePassService), ex);
            return new BattlePassData();
        }
    }

    private void SaveData()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(DATA_KEY, json);
        }
        catch { /* Speichern fehlgeschlagen — graceful degrade wie Kern-Services (CoinService) */ }
    }
}
