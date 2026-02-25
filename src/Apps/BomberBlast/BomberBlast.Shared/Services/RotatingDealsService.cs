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
public class RotatingDealsService : IRotatingDealsService
{
    private const string DEALS_DATA_KEY = "RotatingDealsData";

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ICardService _cardService;

    // Persistenz-Daten
    private RotatingDealsData _data;

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
                // Zufällige Rare-Karte
                var rareCards = GetRareCardTypes();
                if (rareCards.Count > 0)
                {
                    var rng = new Random(dealId.GetHashCode());
                    _cardService.AddCard(rareCards[rng.Next(rareCards.Count)]);
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
        string[] dealTypes = ["CoinPack", "GemPack", "CardPack", "UpgradeDiscount"];
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
            "GemPack" => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealGemPack",
                OriginalPrice = 4000,
                DiscountedPrice = 3000,
                DiscountPercent = 25,
                Currency = "Coins",
                RewardType = "Gems",
                RewardAmount = 15
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
        int dealType = rng.Next(4);
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
            _ => new RotatingDeal
            {
                Id = dealId,
                TitleKey = "DealPremiumBundle",
                OriginalPrice = 30,
                DiscountedPrice = 18,
                DiscountPercent = 40,
                Currency = "Gems",
                RewardType = "Coins",
                RewardAmount = 5000
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELFER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Gibt alle Rare-Karten-BombTypes zurück</summary>
    private static List<BombType> GetRareCardTypes()
    {
        var rareTypes = new List<BombType>();
        foreach (var card in CardCatalog.All)
        {
            if (card.Rarity == Rarity.Rare)
                rareTypes.Add(card.BombType);
        }
        return rareTypes;
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
