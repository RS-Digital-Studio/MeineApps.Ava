using System.Globalization;
using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// 7-Tage Login-Bonus-Zyklus mit Streak-Tracking.
/// Zyklus wiederholt sich nach Tag 7.
/// Streak bricht bei verpasstem Tag ab (Reset auf Tag 1).
/// </summary>
public class DailyRewardService : IDailyRewardService
{
    private const string DAILY_REWARD_KEY = "DailyRewardData";

    private static readonly int[] DayCoins = [500, 1000, 1500, 2000, 2500, 3000, 5000];
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    private DailyRewardData _data;

    public bool IsRewardAvailable => !IsClaimedToday();
    public int CurrentDay => _data.CurrentDay;
    public int CurrentStreak => _data.Streak;

    public DailyRewardService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _data = Load();
        CheckStreakReset();
    }

    public IReadOnlyList<DailyReward> GetRewards()
    {
        var rewards = new List<DailyReward>(7);
        for (int i = 0; i < 7; i++)
        {
            int day = i + 1;
            rewards.Add(new DailyReward
            {
                Day = day,
                Coins = DayCoins[i],
                ExtraLives = day == 5 ? 1 : 0,
                IsClaimed = day < _data.CurrentDay || (day == _data.CurrentDay && IsClaimedToday()),
                IsCurrentDay = day == _data.CurrentDay && !IsClaimedToday(),
                IsPast = day < _data.CurrentDay
            });
        }
        return rewards;
    }

    public DailyReward? ClaimReward()
    {
        if (IsClaimedToday())
            return null;

        int dayIndex = _data.CurrentDay - 1;
        if (dayIndex < 0 || dayIndex >= 7)
            dayIndex = 0;

        var reward = new DailyReward
        {
            Day = _data.CurrentDay,
            Coins = DayCoins[dayIndex],
            ExtraLives = _data.CurrentDay == 5 ? 1 : 0,
            IsClaimed = true,
            IsCurrentDay = false
        };

        _data.LastClaimDate = DateTime.UtcNow.ToString("O");
        _data.Streak++;

        // Nächster Tag oder Zyklus-Reset
        _data.CurrentDay++;
        if (_data.CurrentDay > 7)
        {
            _data.CurrentDay = 1;
        }

        Save();
        return reward;
    }

    /// <summary>
    /// Prüft ob die Streak abgebrochen ist (mehr als 1 Tag seit letztem Claim)
    /// </summary>
    private void CheckStreakReset()
    {
        if (string.IsNullOrEmpty(_data.LastClaimDate))
            return;

        try
        {
            var lastClaim = DateTime.Parse(_data.LastClaimDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var daysSinceLastClaim = (DateTime.UtcNow.Date - lastClaim.Date).Days;

            // Mehr als 1 Tag verpasst → Streak zurücksetzen
            if (daysSinceLastClaim > 1)
            {
                _data.CurrentDay = 1;
                _data.Streak = 0;
                Save();
            }
        }
        catch
        {
            // Parse-Fehler → Reset
            _data.CurrentDay = 1;
            _data.Streak = 0;
            Save();
        }
    }

    private bool IsClaimedToday()
    {
        if (string.IsNullOrEmpty(_data.LastClaimDate))
            return false;

        try
        {
            var lastClaim = DateTime.Parse(_data.LastClaimDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return lastClaim.Date == DateTime.UtcNow.Date;
        }
        catch
        {
            return false;
        }
    }

    private DailyRewardData Load()
    {
        try
        {
            string json = _preferences.Get<string>(DAILY_REWARD_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                return JsonSerializer.Deserialize<DailyRewardData>(json, JsonOptions) ?? new DailyRewardData();
            }
        }
        catch
        {
            // Fehler beim Laden → Standardwerte
        }
        return new DailyRewardData();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(DAILY_REWARD_KEY, json);
        }
        catch
        {
            // Speichern fehlgeschlagen
        }
    }

    private class DailyRewardData
    {
        public int CurrentDay { get; set; } = 1;
        public int Streak { get; set; }
        public string? LastClaimDate { get; set; }
    }
}
