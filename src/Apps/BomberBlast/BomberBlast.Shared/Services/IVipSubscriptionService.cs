using System.Globalization;

namespace BomberBlast.Services;

/// <summary>
/// VIP-Subscription (Phase 23b — AAA-Audit M2).
///
/// <para>Royal-Match-Pattern: 4,99€/Monat als Subscription mit täglichen Boni:</para>
/// <list type="bullet">
///   <item>+100 Gems täglich (1× pro UTC-Tag, automatisch bei App-Start gutgeschrieben).</item>
///   <item>Permanent +25% Coin-Multiplier.</item>
///   <item>VIP-Skin + VIP-Trail (Reward-Tier).</item>
///   <item>Continue-Cooldown halbiert (30s statt 60s).</item>
/// </list>
///
/// <para>Code-Foundation — Subscription-IAP-Setup (Product-ID <c>vip_monthly</c>) + Server-Validation
/// vom User nachgereicht (Google Play Subscriptions API). Bis dahin ist <see cref="IsActive"/> false.</para>
/// </summary>
public interface IVipSubscriptionService
{
    /// <summary>True wenn Subscription aktuell aktiv (nicht abgelaufen).</summary>
    bool IsActive { get; }

    /// <summary>Datum bis wann die Subscription gültig ist (UTC, ISO 8601).</summary>
    DateTime? ExpiresAtUtc { get; }

    /// <summary>Coin-Multiplier wenn VIP aktiv (1.25 = +25%).</summary>
    float CoinMultiplier { get; }

    /// <summary>Tägliche Gem-Auszahlung (bei VIP aktiv).</summary>
    int DailyGems { get; }

    /// <summary>Continue-Cooldown-Sekunden (60 Standard, 30 mit VIP).</summary>
    int ContinueCooldownSeconds { get; }

    /// <summary>True wenn die heutige Daily-Gems-Auszahlung noch nicht abgeholt wurde.</summary>
    bool CanClaimDailyGems { get; }

    /// <summary>
    /// Aktiviert die Subscription bis zum gegebenen Ablaufdatum (vom IAP-Validation-Pfad gerufen).
    /// </summary>
    void Activate(DateTime expiresAtUtc);

    /// <summary>Beendet die Subscription manuell (für Tests / Cancel-Flow).</summary>
    void Deactivate();

    /// <summary>
    /// Markiert die Daily-Gems als heute eingelöst. Nächster Anspruch ab UTC-Mitternacht.
    /// </summary>
    void MarkDailyGemsClaimed();
}

/// <summary>
/// Default-Implementation persistiert via Preferences.
/// </summary>
public sealed class VipSubscriptionService : IVipSubscriptionService
{
    private const string KeyExpires = "Vip_ExpiresAtUtc";
    private const string KeyDailyClaimed = "Vip_DailyClaimedDate";

    private readonly MeineApps.Core.Ava.Services.IPreferencesService _prefs;

    public VipSubscriptionService(MeineApps.Core.Ava.Services.IPreferencesService prefs)
    {
        _prefs = prefs;
    }

    public DateTime? ExpiresAtUtc
    {
        get
        {
            var raw = _prefs.Get(KeyExpires, string.Empty);
            if (string.IsNullOrEmpty(raw)) return null;
            try { return DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind); }
            catch { return null; }
        }
    }

    public bool IsActive
    {
        get
        {
            var exp = ExpiresAtUtc;
            return exp.HasValue && exp.Value > DateTime.UtcNow;
        }
    }

    public float CoinMultiplier => IsActive ? 1.25f : 1.0f;
    public int DailyGems => 100;
    public int ContinueCooldownSeconds => IsActive ? 30 : 60;

    public bool CanClaimDailyGems
    {
        get
        {
            if (!IsActive) return false;
            var lastRaw = _prefs.Get(KeyDailyClaimed, string.Empty);
            if (string.IsNullOrEmpty(lastRaw)) return true;
            try
            {
                var last = DateTime.Parse(lastRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                return last.Date < DateTime.UtcNow.Date;
            }
            catch { return true; }
        }
    }

    public void Activate(DateTime expiresAtUtc)
    {
        _prefs.Set(KeyExpires, expiresAtUtc.ToString("O"));
    }

    public void Deactivate()
    {
        _prefs.Set(KeyExpires, string.Empty);
    }

    public void MarkDailyGemsClaimed()
    {
        _prefs.Set(KeyDailyClaimed, DateTime.UtcNow.ToString("O"));
    }
}
