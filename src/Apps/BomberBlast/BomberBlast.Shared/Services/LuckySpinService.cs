using System.Globalization;
using System.Text.Json;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Glücksrad-Service: 1x gratis pro Tag, gewichtete Belohnungen
/// 9 Segmente mit Coins und Gems
/// </summary>
public sealed class LuckySpinService : ILuckySpinService
{
    private readonly IPreferencesService _preferences;
    private SpinData _data;
    // Audit M06: Random.Shared statt new Random() — thread-safe, kein Time-Tick-Seed-Problem
    // (mehrere new Random() in derselben Millisekunde lieferten identische Reihen).

    private static readonly SpinReward[] _rewards =
    [
        new() { Index = 0, NameKey = "SpinReward150",   Coins = 150,  Weight = 22 },
        new() { Index = 1, NameKey = "SpinReward100",   Coins = 100,  Weight = 20 },
        new() { Index = 2, NameKey = "SpinReward250",   Coins = 250,  Weight = 18 },
        new() { Index = 3, NameKey = "SpinReward500",   Coins = 500,  Weight = 15 },
        new() { Index = 4, NameKey = "SpinReward100",   Coins = 100,  Weight = 20 },
        new() { Index = 5, NameKey = "SpinReward750",   Coins = 750,  Weight = 10 },
        new() { Index = 6, NameKey = "SpinReward250",   Coins = 250,  Weight = 18 },
        // BAL-33 (20.04.2026): Weight 8 -> 12. Gem-Segment-Chance von 5.9% auf 8.6%,
        // Gem-Erwartungswert/Spin von 0.66 auf 0.79 (+20%). Gratis-Gems waren schwach.
        new() { Index = 7, NameKey = "SpinReward5Gems", Gems = 5,     Weight = 12 },
        new() { Index = 8, NameKey = "SpinRewardJackpot", Coins = 3000, Gems = 10, Weight = 5, IsJackpot = true },
    ];

    private static readonly int _totalWeight = _rewards.Sum(r => r.Weight);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public LuckySpinService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _data = Load();
    }

    public bool IsFreeSpinAvailable
    {
        get
        {
            if (string.IsNullOrEmpty(_data.LastFreeSpinDate))
                return true;

            var lastDate = DateTime.Parse(_data.LastFreeSpinDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return lastDate.Date < DateTime.UtcNow.Date;
        }
    }

    public int TotalSpins => _data.TotalSpins;
    public int SpinsSinceLastJackpot => _data.SpinsSinceLastJackpot;
    // v2.0.60 (B-B14): Pity 50→25, plus Soft-Pity ab Spin 15. 50 Tage bis Jackpot-Garantie
    // war zu lang für Casual (1× free Spin/Tag = 7 Wochen). Soft-Pity-Curve macht Late-Game-Spins
    // psychologisch fairer ohne Compliance-Verletzung (Drop-Rate steigt graduell, kommuniziert).
    public int JackpotPityThreshold => 25;

    public IReadOnlyList<SpinReward> GetRewards() => _rewards;

    public IReadOnlyList<(int RewardIndex, float ProbabilityPercent)> GetDropRates()
    {
        var list = new List<(int, float)>(_rewards.Length);
        for (int i = 0; i < _rewards.Length; i++)
        {
            float pct = _rewards[i].Weight * 100f / _totalWeight;
            list.Add((i, pct));
        }
        return list;
    }

    /// <summary>
    /// v2.0.60 (B-B14): Soft-Pity-Boost ab Spin 15 — Jackpot-Wahrscheinlichkeit
    /// steigt schrittweise an. Spin 15-19: 8.5%, Spin 20-24: 25%, Spin 25+ Hard-Pity 100%.
    /// </summary>
    private int GetSoftPityBoostWeight()
    {
        int spinsSince = _data.SpinsSinceLastJackpot;
        return spinsSince switch
        {
            >= 25 => 0,                       // Wird durch Hard-Pity behandelt
            >= 20 => 25 * _totalWeight / 100, // 25% Jackpot-Chance
            >= 15 => 9 * _totalWeight / 100,  // ~8.5% Jackpot-Chance
            _ => 0
        };
    }

    public int Spin()
    {
        int jackpotIndex = -1;
        for (int i = 0; i < _rewards.Length; i++)
            if (_rewards[i].IsJackpot) { jackpotIndex = i; break; }

        // Phase 23 — Pity-Counter (Lootbox-Compliance UK/China):
        // Nach JackpotPityThreshold Spins ohne Jackpot ist der naechste Spin garantiert Jackpot.
        // v2.0.60 (B-B14): Threshold von 50 auf 25. Plus Soft-Pity-Boost ab Spin 15.
        if (jackpotIndex >= 0 && _data.SpinsSinceLastJackpot >= JackpotPityThreshold)
        {
            _data.TotalSpins++;
            _data.SpinsSinceLastJackpot = 0;
            Save();
            return jackpotIndex;
        }

        // v2.0.60 (B-B14): Soft-Pity erhöht effektives Gewicht des Jackpot-Segments.
        int softPityBoost = jackpotIndex >= 0 ? GetSoftPityBoostWeight() : 0;
        int effectiveTotalWeight = _totalWeight + softPityBoost;

        // Gewichtete Zufallsauswahl
        int roll = Random.Shared.Next(effectiveTotalWeight);
        int cumulative = 0;
        for (int i = 0; i < _rewards.Length; i++)
        {
            int weight = _rewards[i].Weight + (i == jackpotIndex ? softPityBoost : 0);
            cumulative += weight;
            if (roll < cumulative)
            {
                _data.TotalSpins++;
                if (i == jackpotIndex)
                    _data.SpinsSinceLastJackpot = 0;
                else
                    _data.SpinsSinceLastJackpot++;
                Save();
                return i;
            }
        }
        return 0; // Fallback
    }

    public void ClaimFreeSpin()
    {
        _data.LastFreeSpinDate = DateTime.UtcNow.ToString("O");
        Save();
    }

    private SpinData Load()
    {
        var json = _preferences.Get("LuckySpinData", "");
        if (string.IsNullOrEmpty(json))
            return new SpinData();
        try
        {
            return JsonSerializer.Deserialize<SpinData>(json, JsonOptions) ?? new SpinData();
        }
        catch
        {
            return new SpinData();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_data, JsonOptions);
        _preferences.Set("LuckySpinData", json);
    }

    private class SpinData
    {
        public string? LastFreeSpinDate { get; set; }
        public int TotalSpins { get; set; }
        /// <summary>Phase 23 — Pity-Counter für Lootbox-Compliance.</summary>
        public int SpinsSinceLastJackpot { get; set; }
    }
}
