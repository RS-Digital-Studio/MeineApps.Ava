using System.Globalization;
using MeineApps.Core.Ava.Services;

namespace MeineApps.Core.Premium.Ava.Services;

/// <summary>
/// Implementation of 14-day trial system using IPreferencesService
/// </summary>
public sealed class TrialService : ITrialService
{
    private const string TrialStartKey = "trial_start_date";
    private const string TrialSeenKey = "trial_offer_seen";
    private const int TrialDays = 14;

    private readonly IPreferencesService _preferences;
    private DateTime? _trialStartDate;
    private bool _hasSeenTrialOffer;

    public DateTime? TrialStartDate => _trialStartDate;
    public bool IsTrialStarted => _trialStartDate.HasValue;
    public bool HasSeenTrialOffer => _hasSeenTrialOffer;

    public int DaysRemaining
    {
        get
        {
            if (!IsTrialStarted) return 0;
            var elapsed = (DateTime.UtcNow - _trialStartDate!.Value).Days;
            return Math.Max(0, TrialDays - elapsed);
        }
    }

    public bool IsTrialActive => IsTrialStarted && DaysRemaining > 0;
    public bool IsTrialExpired => IsTrialStarted && DaysRemaining <= 0;
    public bool ShouldShowAds => !IsTrialActive;

    public event EventHandler? TrialStatusChanged;

    public TrialService(IPreferencesService preferences)
    {
        _preferences = preferences;
    }

    public void Initialize()
    {
        var storedDate = _preferences.Get(TrialStartKey, string.Empty);
        if (!string.IsNullOrEmpty(storedDate) &&
            DateTime.TryParse(storedDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
        {
            _trialStartDate = date;
        }

        _hasSeenTrialOffer = _preferences.Get(TrialSeenKey, false);

        System.Diagnostics.Debug.WriteLine(
            $"TrialService initialized: Started={IsTrialStarted}, Active={IsTrialActive}, DaysRemaining={DaysRemaining}");
    }

    public void StartTrial()
    {
        if (IsTrialStarted) return;

        _trialStartDate = DateTime.UtcNow;
        _preferences.Set(TrialStartKey, _trialStartDate.Value.ToString("o"));
        _hasSeenTrialOffer = true;
        _preferences.Set(TrialSeenKey, true);

        System.Diagnostics.Debug.WriteLine($"Trial started: {_trialStartDate}, DaysRemaining={DaysRemaining}");
        TrialStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkTrialOfferAsSeen()
    {
        _hasSeenTrialOffer = true;
        _preferences.Set(TrialSeenKey, true);
    }
}
