using System.Text.Json;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Persistente Coin-Verwaltung via IPreferencesService
/// </summary>
public sealed class CoinService : ICoinService
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
        _data.LastDailyBonusDate.Value.Date < DateTime.UtcNow.Date;

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

        _data.LastDailyBonusDate = DateTime.UtcNow.Date;
        AddCoins(DAILY_BONUS);
        return true;
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        // Overflow-Guard: Bei >2.147 Mrd Coins clamped auf int.MaxValue statt silent Negativ.
        // Relevant bei Survival-Mega-Streaks oder kumulierten Premium-Boni.
        long newBalance = (long)_data.Balance + amount;
        long newTotal = (long)_data.TotalEarned + amount;
        _data.Balance = newBalance > int.MaxValue ? int.MaxValue : (int)newBalance;
        _data.TotalEarned = newTotal > int.MaxValue ? int.MaxValue : (int)newTotal;
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
        string json = _preferences.Get<string>(COIN_DATA_KEY, "");
        if (string.IsNullOrEmpty(json))
            return new CoinData();

        CoinData data;
        try
        {
            data = JsonSerializer.Deserialize<CoinData>(json, JsonOptions) ?? new CoinData();
        }
        catch (Exception ex)
        {
            // Corrupt JSON: Meldung an PersistenceHealth → CloudSaveService bevorzugt Cloud-Pull.
            PersistenceHealth.ReportCorruption(nameof(CoinService), ex);
            return new CoinData();
        }

        // Negative-Balance-Defense: Pre-v2.0.30 Overflows oder manuelle Preferences-Edits
        // koennten negative Werte hinterlassen. Clamp auf 0 + Corruption-Flag → Cloud-Pull.
        if (data.Balance < 0 || data.TotalEarned < 0)
        {
            PersistenceHealth.ReportCorruption(nameof(CoinService));
            if (data.Balance < 0) data.Balance = 0;
            if (data.TotalEarned < 0) data.TotalEarned = 0;
        }
        return data;
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
