using System.Globalization;
using Avalonia.Threading;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerRechner.Services;

/// <summary>
/// Verwaltet temporaeren Premium-Zugang nach Rewarded Ad.
/// Speichert Ablaufzeit persistent in Preferences.
/// </summary>
public class PremiumAccessService : IPremiumAccessService, IDisposable
{
    private const string PrefKey = "PremiumAccessExpiry";
    private const string ExtendedHistoryPrefKey = "ExtendedHistoryExpiry";
    private const int HistoryLimitFree = 5;
    private const int HistoryLimitExtended = 30;

    private readonly IPurchaseService _purchaseService;
    private readonly IPreferencesService _preferencesService;
    private readonly DispatcherTimer _checkTimer;
    private bool _disposed;

    public event EventHandler? AccessExpired;

    public DateTime? AccessExpiresAt { get; private set; }

    public bool HasAccess =>
        _purchaseService.IsPremium ||
        (AccessExpiresAt.HasValue && AccessExpiresAt.Value > DateTime.UtcNow);

    public int RemainingMinutes
    {
        get
        {
            if (_purchaseService.IsPremium) return int.MaxValue;
            if (!AccessExpiresAt.HasValue) return 0;
            var remaining = AccessExpiresAt.Value - DateTime.UtcNow;
            return remaining.TotalMinutes > 0 ? (int)Math.Ceiling(remaining.TotalMinutes) : 0;
        }
    }

    public PremiumAccessService(IPurchaseService purchaseService, IPreferencesService preferencesService)
    {
        _purchaseService = purchaseService;
        _preferencesService = preferencesService;

        // Gespeicherte Ablaufzeit laden
        LoadSavedExpiry();

        // Timer prueft alle 30s ob Zugang abgelaufen
        _checkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _checkTimer.Tick += OnCheckTimerTick;
        if (AccessExpiresAt.HasValue && HasAccess)
            _checkTimer.Start();
    }

    public void GrantTemporaryAccess(TimeSpan duration)
    {
        AccessExpiresAt = DateTime.UtcNow.Add(duration);
        // Persistent speichern (ISO 8601 mit "O" Format)
        _preferencesService.Set(PrefKey, AccessExpiresAt.Value.ToString("O"));
        _checkTimer.Start();
    }

    private void LoadSavedExpiry()
    {
        var saved = _preferencesService.Get<string>(PrefKey, "");
        if (!string.IsNullOrEmpty(saved) &&
            DateTime.TryParse(saved, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var expiry))
        {
            if (expiry > DateTime.UtcNow)
                AccessExpiresAt = expiry;
            else
                _preferencesService.Set(PrefKey, ""); // Abgelaufen, aufraumen
        }
    }

    private void OnCheckTimerTick(object? sender, EventArgs e)
    {
        if (!HasAccess && AccessExpiresAt.HasValue)
        {
            AccessExpiresAt = null;
            _preferencesService.Set(PrefKey, "");
            _checkTimer.Stop();
            AccessExpired?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool HasExtendedHistory
    {
        get
        {
            if (_purchaseService.IsPremium) return true;
            var saved = _preferencesService.Get<string>(ExtendedHistoryPrefKey, "");
            if (string.IsNullOrEmpty(saved)) return false;
            if (DateTime.TryParse(saved, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var expiry))
            {
                return expiry > DateTime.UtcNow;
            }
            return false;
        }
    }

    public void GrantExtendedHistory()
    {
        var expiry = DateTime.UtcNow.AddHours(24);
        _preferencesService.Set(ExtendedHistoryPrefKey, expiry.ToString("O"));
    }

    public int GetHistoryLimit()
    {
        return HasExtendedHistory ? HistoryLimitExtended : HistoryLimitFree;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _checkTimer.Stop();
        _checkTimer.Tick -= OnCheckTimerTick;
        _disposed = true;
    }
}
