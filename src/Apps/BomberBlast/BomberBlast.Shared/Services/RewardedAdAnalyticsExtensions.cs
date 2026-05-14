using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Extension fuer <see cref="IRewardedAdService"/> die Funnel-Telemetrie ergaenzt
/// (.2 .
///
/// Verwendung statt direktem <c>ShowAdAsync(placement)</c>:
/// <code>
/// var success = await _rewardedAdService.ShowAdWithTelemetryAsync(_analytics, "continue");
/// </code>
///
/// Feuert <see cref="AnalyticsEvents.RewardedAdRequest"/> VOR dem Show
/// und <see cref="AnalyticsEvents.RewardedAdCompleted"/> nach erfolgreichem Schauen.
/// Bei Abbruch / kein Fill: nur das Request-Event.
/// </summary>
public static class RewardedAdAnalyticsExtensions
{
    public static async Task<bool> ShowAdWithTelemetryAsync(
        this IRewardedAdService adService,
        IAnalyticsService? analytics,
        string placement)
    {
        // 1. Request-Event (auch wenn Ad gar nicht erscheint — fuer Funnel-Conversion-Rate).
        analytics?.LogEvent(AnalyticsEvents.RewardedAdRequest, new Dictionary<string, object>
        {
            [AnalyticsParams.Placement] = placement,
        });

        var success = await adService.ShowAdAsync(placement);

        // 2. Completed-Event nur bei erfolgreichem Watch-Through.
        if (success)
        {
            analytics?.LogEvent(AnalyticsEvents.RewardedAdCompleted, new Dictionary<string, object>
            {
                [AnalyticsParams.Placement] = placement,
            });
        }

        return success;
    }
}
