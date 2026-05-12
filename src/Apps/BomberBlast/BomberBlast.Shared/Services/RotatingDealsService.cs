using System.Globalization;
using System.Text.Json;
using BomberBlast.Models;
using BomberBlast.Models.Cards;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Generiert rotierende Deals per Seeded Random basierend auf Datum.
/// 3 Tagesdeals + 1 Wochendeal mit 20-50% Rabatt.
/// Persistiert beanspruchte Deals via JSON in IPreferencesService.
/// </summary>
public sealed class RotatingDealsService : IRotatingDealsService
{
    private const string DEALS_DATA_KEY = "RotatingDealsData";

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ICardService _cardService;

    // Persistenz-Daten
    private RotatingDealsData _data;

    // Audit M14: Pro-Spieler-Salt fuer Reward-Drops (verhindert dass alle Spieler die identische Karten-Drops bekommen).
    private const string USER_DROP_SALT_KEY = "RotatingDealsUserSalt";

    public RotatingDealsService(
        IPreferencesService preferences,
        ICoinService coinService,
        IGemService gemService,
        ICardService cardService)
    {
        _preferences = preferences;
        _coinService = coinService;
        _gemService = gemService;
        _cardService = cardService;
        _data = Load();

        // Salt einmalig erzeugen + persistieren (stabil ueber Sessions)
        if (_preferences.Get<int>(USER_DROP_SALT_KEY, 0) == 0)
        {
            _preferences.Set(USER_DROP_SALT_KEY, Random.Shared.Next(1, int.MaxValue));
        }
    }

    public List<RotatingDeal> GetTodaysDeals()
    {
        var today = DateTime.UtcNow.Date;
        var dayId = today.Year * 10000 + today.Month * 100 + today.Day;
        var rng = new Random(dayId * 31337);

        // Prüfe ob beanspruchte Deals von heute sind, sonst zurücksetzen
        CleanupExpiredClaims(today);

        var deals = new List<RotatingDeal>();
        var usedTypes = new HashSet<string>();

        // 3 verschiedene Tagesdeals generieren
        for (int i = 0; i < 3; i++)
        {
            var deal = GenerateDailyDeal(rng, dayId, i, usedTypes);
            deal.IsClaimed = _data.ClaimedDealIds.Contains(deal.Id);
            deals.Add(deal);
        }

        return deals;
    }

    public RotatingDeal? GetWeeklyDeal()
    {
        var today = DateTime.UtcNow.Date;
        // ISO-Kalenderwoche berechnen
        var cal = CultureInfo.InvariantCulture.Calendar;
        int weekNumber = cal.GetWeekOfYear(today, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        int year = today.Year;
        int weekId = year * 100 + weekNumber;
        var rng = new Random(weekId * 42069);

        var deal = GenerateWeeklyDeal(rng, weekId);
        deal.IsClaimed = _data.ClaimedDealIds.Contains(deal.Id);
        return deal;
    }

    public bool ClaimDeal(string dealId)
    {
        if (string.IsNullOrEmpty(dealId)) return false;
        if (_data.ClaimedDealIds.Contains(dealId)) return false;

        // Deal finden (täglich oder wöchentlich)
        RotatingDeal? deal = null;
        var todaysDeals = GetTodaysDeals();
        deal = todaysDeals.Find(d => d.Id == dealId);
        deal ??= GetWeeklyDeal() is { } weekly && weekly.Id == dealId ? weekly : null;

        if (deal == null || deal.IsClaimed) return false;

        // Bezahlung abziehen
        bool paymentSuccess;
        if (deal.Currency == "Gems")
        {
            paymentSuccess = _gemService.TrySpendGems(deal.DiscountedPrice);
        }
        else
        {
            paymentSuccess = _coinService.TrySpendCoins(deal.DiscountedPrice);
        }

        if (!paymentSuccess) return false;

        // Belohnung vergeben
        switch (deal.RewardType)
        {
            case "Coins":
                _coinService.AddCoins(deal.RewardAmount);
                break;
            case "Gems":
                _gemService.AddGems(deal.RewardAmount);
                break;
            case "Card":
                // Pool abhängig vom Deal-Typ: Legendary bei DealLegendaryCardDrop,
                // Epic bei DealEpicCardPack, sonst Rare. RewardAmount bestimmt Anzahl
                // der Karten-Drops (DealRareCardBundle = 3).
                var cardPool = deal.TitleKey switch
                {
                    "DealLegendaryCardDrop" => GetCardTypesByRarity(Rarity.Legendary),
                    "DealEpicCardPack" => GetCardTypesByRarity(Rarity.Epic),
                    _ => GetCardTypesByRarity(Rarity.Rare)
                };
                if (cardPool.Count > 0)
                {
                    // Audit M14: User-spezifischer Salt XOR DealId → Spieler bekommen unterschiedliche Drops.
                    var userSalt = _preferences.Get<int>(USER_DROP_SALT_KEY, 1);
                    var rng = new Random(dealId.GetHashCode() ^ userSalt);
                    int drops = Math.Max(1, deal.RewardAmount);
                    for (int i = 0; i < drops; i++)
                    {
                        _cardService.AddCard(cardPool[rng.Next(cardPool.Count)]);
                    }
                }
                break;
            case "Upgrade":
                // Coins-Rabatt → Spieler bekommt Coins-Äquivalent als Guthaben
                _coinService.AddCoins(deal.RewardAmount);
                break;
        }

        // Als beansprucht markieren
        _data.ClaimedDealIds.Add(dealId);
        _data.LastCleanupDate = DateTime.UtcNow.Date.ToString("O");
        Save();

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DEAL-GENERIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Generiert einen einzelnen Tagesdeal</summary>
    private RotatingDeal GenerateDailyDeal(Random rng, int dayId, int index, HashSet<string> usedTypes)
    {
        // Deal-Typ auswählen (ohne Wiederholung am selben Tag)
        // v2.0.34: Pool von 4 auf 7 Typen erweitert, damit eine 3-Tages-Rotation
        // sinnvoll möglich ist (3 Deals pro Tag × 3 Tage = 9 Slots, 7 unique Typen).
        string[] dealTypes =
        [
            "CoinPack", "LargeCoinPack", "CardPack", "UpgradeDiscount",
            "EpicCardPack", "GemCoinCombo", "PowerUpLuckDeal"
        ];
        string selectedType;
        do
        {
            selectedType = dealTypes[rng.Next(dealTypes.Length)];
        } while (usedTypes.Contains(selectedType) && usedTypes.Count < dealTypes.Length);
        usedTypes.Add(selectedType);

        var dealId = $"daily_{dayId}_{index}";

        return selectedType switch
        {
            "CoinPack" => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealCoinPack",
                OriginalPrice = 10,
                DiscountedPrice = 8,
                DiscountPercent = 20,
                Currency = "Gems",
                RewardType = "Coins",
                RewardAmount = 1000
            },
            "LargeCoinPack" => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealLargeCoinPack",
                OriginalPrice = 5000,
                DiscountedPrice = 3500,
                DiscountPercent = 30,
                Currency = "Coins",
                RewardType = "Coins",
                RewardAmount = 5000
            },
            "CardPack" => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealCardPack",
                OriginalPrice = 750,
                DiscountedPrice = 500,
                DiscountPercent = 33,
                Currency = "Coins",
                RewardType = "Card",
                RewardAmount = 1
            },
            "UpgradeDiscount" => GenerateUpgradeDiscount(rng, dealId),

            // v2.0.34: Drei neue Daily-Deal-Typen
            "EpicCardPack" => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealEpicCardPack",
                OriginalPrice = 50,
                DiscountedPrice = 35,
                DiscountPercent = 30,
                Currency = "Gems",
                RewardType = "Card",
                RewardAmount = 1  // Rare-Card-Pool (siehe GetRareCardTypes)
            },
            "GemCoinCombo" => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealGemCoinCombo",
                OriginalPrice = 3000,
                DiscountedPrice = 2000,
                DiscountPercent = 33,
                Currency = "Coins",
                RewardType = "Gems",
                RewardAmount = 12
            },
            "PowerUpLuckDeal" => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealPowerUpLuck",
                DescriptionKey = "UpgradePowerUpLuck",
                OriginalPrice = 7500,
                DiscountedPrice = 4500,
                DiscountPercent = 40,
                Currency = "Coins",
                RewardType = "Upgrade",
                RewardAmount = 4500  // Coins-Äquivalent als Guthaben
            },

            _ => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealCoinPack",
                OriginalPrice = 10,
                DiscountedPrice = 8,
                DiscountPercent = 20,
                Currency = "Gems",
                RewardType = "Coins",
                RewardAmount = 1000
            }
        };
    }

    /// <summary>Generiert einen Upgrade-Rabattdeal (50% auf ein zufälliges Shop-Upgrade)</summary>
    private RotatingDeal GenerateUpgradeDiscount(Random rng, string dealId)
    {
        // Upgrade-Typen mit ihren Basis-Preisen (Level-1-Preis)
        var upgradeOptions = new (string nameKey, int basePrice)[]
        {
            ("UpgradeStartBombs", 1500),
            ("UpgradeStartFire", 2000),
            ("UpgradeStartSpeed", 3000),
            ("UpgradeExtraLives", 5000),
            ("UpgradeScoreMultiplier", 4000),
            ("UpgradeTimeBonus", 3500),
            ("UpgradeShieldStart", 8000),
            ("UpgradeCoinBonus", 6000),
            ("UpgradePowerUpLuck", 7500)
        };

        var selected = upgradeOptions[rng.Next(upgradeOptions.Length)];
        int discountedPrice = selected.basePrice / 2; // 50% Rabatt

        return new RotatingDeal
        {
            Id = dealId,
            TitleKey = "DealUpgradeDiscount",
            DescriptionKey = selected.nameKey,
            OriginalPrice = selected.basePrice,
            DiscountedPrice = discountedPrice,
            DiscountPercent = 50,
            Currency = "Coins",
            RewardType = "Upgrade",
            RewardAmount = discountedPrice // Coins-Äquivalent als Guthaben
        };
    }

    /// <summary>Generiert den Wochendeal (größerer Rabatt, mehr Wert)</summary>
    private RotatingDeal GenerateWeeklyDeal(Random rng, int weekId)
    {
        var dealId = $"weekly_{weekId}";

        // Wochendeal-Typen: größere Packs mit besserem Rabatt
        // v2.0.34: Pool von 4 auf 6 erweitert für 6-Wochen-Rotation ohne Wiederholung
        int dealType = rng.Next(6);
        return dealType switch
        {
            0 => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealMegaCoinPack",
                OriginalPrice = 25,
                DiscountedPrice = 15,
                DiscountPercent = 40,
                Currency = "Gems",
                RewardType = "Coins",
                RewardAmount = 3000
            },
            1 => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealMegaGemPack",
                OriginalPrice = 10000,
                DiscountedPrice = 5000,
                DiscountPercent = 50,
                Currency = "Coins",
                RewardType = "Gems",
                RewardAmount = 40
            },
            2 => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealRareCardBundle",
                OriginalPrice = 2000,
                DiscountedPrice = 1200,
                DiscountPercent = 40,
                Currency = "Coins",
                RewardType = "Card",
                RewardAmount = 3 // 3 Karten-Drops
            },
            3 => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealPremiumBundle",
                OriginalPrice = 30,
                DiscountedPrice = 18,
                DiscountPercent = 40,
                Currency = "Gems",
                RewardType = "Coins",
                RewardAmount = 5000
            },

            // v2.0.34: Zwei neue Weekly-Deal-Typen
            4 => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealLegendaryCardDrop",
                OriginalPrice = 400,
                DiscountedPrice = 250,
                DiscountPercent = 38,
                Currency = "Gems",
                RewardType = "Card",
                RewardAmount = 1  // Wird in Claim-Logik auf Legendary-Pool begrenzt (via DealId-Marker)
            },
            _ => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealMasterBundle",
                OriginalPrice = 60,
                DiscountedPrice = 35,
                DiscountPercent = 42,
                Currency = "Gems",
                RewardType = "Coins",
                RewardAmount = 10000
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELFER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Gibt alle Rare-Karten-BombTypes zurück (Legacy-Alias für GetCardTypesByRarity(Rare)).</summary>
    private static List<BombType> GetRareCardTypes() => GetCardTypesByRarity(Rarity.Rare);

    /// <summary>Gibt alle BombTypes einer Rarität zurück (für Deal-Rewards v2.0.35).</summary>
    private static List<BombType> GetCardTypesByRarity(Rarity rarity)
    {
        var types = new List<BombType>();
        foreach (var card in CardCatalog.All)
        {
            if (card.Rarity == rarity)
                types.Add(card.BombType);
        }
        return types;
    }

    /// <summary>Bereinigt abgelaufene beanspruchte Deals (älter als gestern)</summary>
    private void CleanupExpiredClaims(DateTime today)
    {
        if (!string.IsNullOrEmpty(_data.LastCleanupDate) &&
            DateTime.TryParse(_data.LastCleanupDate, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var lastCleanup) &&
            lastCleanup.Date == today)
        {
            return; // Heute schon bereinigt
        }

        // Alte Daily-Claims entfernen (Weekly bleiben bis zur nächsten Woche)
        var todayPrefix = $"daily_{today.Year * 10000 + today.Month * 100 + today.Day}_";
        var cal = CultureInfo.InvariantCulture.Calendar;
        int currentWeek = cal.GetWeekOfYear(today, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        var weeklyPrefix = $"weekly_{today.Year * 100 + currentWeek}";

        _data.ClaimedDealIds.RemoveWhere(id =>
            !id.StartsWith(todayPrefix) && !id.StartsWith(weeklyPrefix));

        _data.LastCleanupDate = today.ToString("O");
        Save();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PERSISTENZ
    // ═══════════════════════════════════════════════════════════════════════

    private RotatingDealsData Load()
    {
        var json = _preferences.Get(DEALS_DATA_KEY, "");
        if (string.IsNullOrEmpty(json)) return new RotatingDealsData();
        try
        {
            return JsonSerializer.Deserialize<RotatingDealsData>(json) ?? new RotatingDealsData();
        }
        catch
        {
            return new RotatingDealsData();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_data);
        _preferences.Set(DEALS_DATA_KEY, json);
    }
}

/// <summary>
/// Persistenz-Daten für rotierende Deals
/// </summary>
internal class RotatingDealsData
{
    /// <summary>IDs der beanspruchten Deals</summary>
    public HashSet<string> ClaimedDealIds { get; set; } = [];

    /// <summary>Datum der letzten Bereinigung (UTC, ISO 8601)</summary>
    public string LastCleanupDate { get; set; } = "";
}
