using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace FitnessRechner.Services;

/// <summary>
/// Begrenzt kostenlose Barcode-Scans auf 3 pro Tag.
/// Rewarded Ad gibt +5 Scans. Premium-Nutzer haben keine Begrenzung.
/// </summary>
public sealed class ScanLimitService : IScanLimitService
{
    private readonly IPurchaseService _purchaseService;
    private readonly IPreferencesService _preferences;

    private const int DailyFreeScans = 3;
    private const int AdBonusScans = 5;
    // Preference-Keys zentral in PreferenceKeys.cs

    private readonly object _lock = new();
    private int _remainingScans;

    public ScanLimitService(IPurchaseService purchaseService, IPreferencesService preferences)
    {
        _purchaseService = purchaseService;
        _preferences = preferences;
        LoadState();
    }

    public int RemainingScans
    {
        get
        {
            lock (_lock)
            {
                ResetDailyIfNeeded();
                return _remainingScans;
            }
        }
    }

    public bool CanScan
    {
        get
        {
            if (_purchaseService.IsPremium) return true;
            lock (_lock)
            {
                ResetDailyIfNeeded();
                return _remainingScans > 0;
            }
        }
    }

    public void UseOneScan()
    {
        if (_purchaseService.IsPremium) return;

        lock (_lock)
        {
            ResetDailyIfNeeded();

            if (_remainingScans > 0)
            {
                _remainingScans--;
                SaveState();
            }
        }
    }

    public void AddScans(int count)
    {
        lock (_lock)
        {
            ResetDailyIfNeeded();
            _remainingScans += count;
            SaveState();
        }
    }

    /// <summary>
    /// Prueft ob ein neuer Tag begonnen hat und setzt ggf. das Tages-Kontingent zurueck.
    /// </summary>
    private void ResetDailyIfNeeded()
    {
        var savedDate = _preferences.Get(PreferenceKeys.ScanLimitDate, "");
        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");

        if (savedDate != todayStr)
        {
            _remainingScans = DailyFreeScans;
            _preferences.Set(PreferenceKeys.ScanLimitDate, todayStr);
            _preferences.Set(PreferenceKeys.ScanLimitCount, _remainingScans);
        }
    }

    private void LoadState()
    {
        var savedDate = _preferences.Get(PreferenceKeys.ScanLimitDate, "");
        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");

        if (savedDate == todayStr)
        {
            _remainingScans = _preferences.Get(PreferenceKeys.ScanLimitCount, DailyFreeScans);
        }
        else
        {
            // Neuer Tag oder erster Start
            _remainingScans = DailyFreeScans;
            _preferences.Set(PreferenceKeys.ScanLimitDate, todayStr);
            _preferences.Set(PreferenceKeys.ScanLimitCount, _remainingScans);
        }
    }

    private void SaveState()
    {
        _preferences.Set(PreferenceKeys.ScanLimitCount, _remainingScans);
    }
}
