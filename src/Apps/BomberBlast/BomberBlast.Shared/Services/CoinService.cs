using System.Text.Json;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Persistente Coin-Verwaltung via IPreferencesService
/// </summary>
public class CoinService : ICoinService
{
    private const string COIN_DATA_KEY = "CoinData";
    private const int DAILY_BONUS = 500;
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    private CoinData _data;

    public int Balance => _data.Balance;
    public int TotalEarned => _data.TotalEarned;
    public int DailyBonusAmount => DAILY_BONUS;

    public bool IsDailyBonusAvailable =>
        _data.LastDailyBonusDate == null ||
        _data.LastDailyBonusDate.Value.Date < DateTime.Today;

    public event EventHandler? BalanceChanged;

    public CoinService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _data = Load();
    }

    public bool TryClaimDailyBonus()
    {
        if (!IsDailyBonusAvailable)
            return false;

        _data.LastDailyBonusDate = DateTime.Today;
        AddCoins(DAILY_BONUS);
        return true;
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        _data.Balance += amount;
        _data.TotalEarned += amount;
        Save();
        BalanceChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || _data.Balance < amount)
            return false;

        _data.Balance -= amount;
        Save();
        BalanceChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool CanAfford(int amount)
    {
        return _data.Balance >= amount;
    }

    private CoinData Load()
    {
        try
        {
            string json = _preferences.Get<string>(COIN_DATA_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                return JsonSerializer.Deserialize<CoinData>(json, JsonOptions) ?? new CoinData();
            }
        }
        catch
        {
            // Fehler beim Laden → Standardwerte
        }
        return new CoinData();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(COIN_DATA_KEY, json);
        }
        catch
        {
            // Speichern fehlgeschlagen
        }
    }

    private class CoinData
    {
        public int Balance { get; set; }
        public int TotalEarned { get; set; }
        public DateTime? LastDailyBonusDate { get; set; }
    }
}
