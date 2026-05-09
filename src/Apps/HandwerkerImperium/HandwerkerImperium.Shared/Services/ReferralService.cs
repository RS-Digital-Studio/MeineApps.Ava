using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// AAA-Audit P1: Friend-Invite-Reward-Loop. Verwaltet Codes + Tier-Auszahlungen.
///
/// Datenfluss:
/// - Beim ersten Start wird via <see cref="EnsureOwnCode"/> ein 6-stelliger Code generiert.
/// - Spieler kann den Code teilen (Share-Sheet via UriLauncher.ShareText).
/// - Eingeladener Spieler gibt den Code beim ersten Start ein → Server-Endpoint
///   <c>POST /referrals/{ownerCode}/claim</c> erhoeht den Counter beim Owner.
/// - Nach 24h aktiver Spielzeit des Eingeladenen: <see cref="OnReferralSucceeded"/>.
/// - <see cref="TryClaimNextTier"/> zahlt 50/200/500 GS bei 1/5/10 erfolgreichen Empfehlungen.
///
/// Aktueller Implementierungs-Status: Service-Foundation. Server-Endpoint + Anti-Cheat
/// (Geraete-Fingerprint gegen Self-Referral, IP-Limit) sind Folge-Sprint.
/// </summary>
public sealed class ReferralService : IReferralService
{
    private readonly IGameStateService _gameState;
    private readonly IAnalyticsService? _analytics;

    public event EventHandler<int>? TierUnlocked;

    public int SuccessfulReferrals => _gameState.State.Referral.SuccessfulReferrals;
    public decimal PermanentIncomeBonus => _gameState.State.Referral.PermanentIncomeBonus;

    public ReferralService(IGameStateService gameState, IAnalyticsService? analytics = null)
    {
        _gameState = gameState;
        _analytics = analytics;
    }

    public void EnsureOwnCode()
    {
        var r = _gameState.State.Referral;
        if (string.IsNullOrEmpty(r.OwnCode))
            r.OwnCode = GenerateCode();
    }

    public string GetOwnCode()
    {
        EnsureOwnCode();
        return _gameState.State.Referral.OwnCode;
    }

    public bool SubmitReferralCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6) return false;
        var r = _gameState.State.Referral;
        if (!string.IsNullOrEmpty(r.UsedReferralCode)) return false; // Bereits genutzt
        // Kein Self-Referral: own code != submitted
        if (string.Equals(r.OwnCode, code, StringComparison.OrdinalIgnoreCase)) return false;

        r.UsedReferralCode = code.ToUpperInvariant();
        _analytics?.TrackEvent("referral_code_used", new Dictionary<string, object?>
        {
            ["used_code"] = r.UsedReferralCode,
        });
        return true;
    }

    public void OnReferralSucceeded()
    {
        var r = _gameState.State.Referral;
        r.SuccessfulReferrals++;
        _analytics?.TrackEvent("referral_succeeded", new Dictionary<string, object?>
        {
            ["count"] = r.SuccessfulReferrals,
        });

        // Auto-Tier-Unlock (Spieler wird beim naechsten Open via TryClaimNextTier abgeholt)
        if (IsTierBoundary(r.SuccessfulReferrals) && !r.ClaimedTiers.Contains(r.SuccessfulReferrals))
        {
            TierUnlocked?.Invoke(this, r.SuccessfulReferrals);
        }
    }

    public int? TryClaimNextTier()
    {
        var r = _gameState.State.Referral;
        // Naechste faellige Tier finden
        int[] tiers = [1, 5, 10];
        foreach (var t in tiers)
        {
            if (r.SuccessfulReferrals >= t && !r.ClaimedTiers.Contains(t))
            {
                int rewardScrews = t switch
                {
                    1 => 50,
                    5 => 200,
                    10 => 500,
                    _ => 0
                };
                if (rewardScrews > 0)
                    _gameState.AddGoldenScrews(rewardScrews);

                r.ClaimedTiers.Add(t);
                _analytics?.TrackEvent("referral_tier_claimed", new Dictionary<string, object?>
                {
                    ["tier"] = t,
                    ["reward_screws"] = rewardScrews,
                });
                return t;
            }
        }
        return null;
    }

    private static bool IsTierBoundary(int count) => count is 1 or 5 or 10;

    private static string GenerateCode()
    {
        // 6-stelliger alphanumerischer Code (uppercase, ohne O/0/I/1 fuer Lesbarkeit)
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> buffer = stackalloc char[6];
        for (int i = 0; i < 6; i++)
            buffer[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(buffer);
    }
}
