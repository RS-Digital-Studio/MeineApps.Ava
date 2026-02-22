using System.Globalization;
using System.Text.Json;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Glücksrad-Service: 1x gratis pro Tag, gewichtete Belohnungen
/// 9 Segmente mit Coins und Gems
/// </summary>
public class LuckySpinService : ILuckySpinService
{
    private readonly IPreferencesService _preferences;
    private SpinData _data;
    private readonly Random _random = new();

    private static readonly SpinReward[] _rewards =
    [
        new() { Index = 0, NameKey = "SpinReward50",    Coins = 50,   Weight = 25 },
        new() { Index = 1, NameKey = "SpinReward100",   Coins = 100,  Weight = 20 },
        new() { Index = 2, NameKey = "SpinReward250",   Coins = 250,  Weight = 18 },
        new() { Index = 3, NameKey = "SpinReward500",   Coins = 500,  Weight = 15 },
        new() { Index = 4, NameKey = "SpinReward100",   Coins = 100,  Weight = 20 },
        new() { Index = 5, NameKey = "SpinReward750",   Coins = 750,  Weight = 10 },
        new() { Index = 6, NameKey = "SpinReward250",   Coins = 250,  Weight = 18 },
        new() { Index = 7, NameKey = "SpinReward5Gems", Gems = 5,     Weight = 8 },
        new() { Index = 8, NameKey = "SpinReward1500",  Coins = 1500, Weight = 5, IsJackpot = true },
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

    public IReadOnlyList<SpinReward> GetRewards() => _rewards;

    public int Spin()
    {
        // Gewichtete Zufallsauswahl
        int roll = _random.Next(_totalWeight);
        int cumulative = 0;
        for (int i = 0; i < _rewards.Length; i++)
        {
            cumulative += _rewards[i].Weight;
            if (roll < cumulative)
            {
                _data.TotalSpins++;
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
    }
}
