#nullable enable
using HandwerkerImperium.Domain.Common;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Remote-Config-Live-Ops (P3 §2/§3): deterministische A/B-Bucket-Zuordnung + Feature-Flag-Rollout aus der
    /// stabilen PlayerId (kein Server-Roundtrip für die Zuordnung; Server liefert nur die Werte). Reine,
    /// Unity-freie Mathematik über <see cref="StableHash"/>.
    /// </summary>
    public static class RemoteConfigFormulas
    {
        /// <summary>Deterministischer A/B-Bucket in [0, bucketCount) aus PlayerId + Experiment-Schlüssel.</summary>
        public static int AbBucket(string playerId, string experimentKey, int bucketCount)
        {
            if (bucketCount <= 1) return 0;
            return StableHash.Bucket(playerId + "|" + experimentKey, bucketCount);
        }

        /// <summary>
        /// True, wenn der Spieler im prozentualen Rollout eines Feature-Flags liegt (deterministisch,
        /// 0..100). 0 % → nie, ≥100 % → immer.
        /// </summary>
        public static bool IsInRollout(string playerId, string flagKey, int rolloutPercent)
        {
            if (rolloutPercent <= 0) return false;
            if (rolloutPercent >= 100) return true;
            return StableHash.Bucket(playerId + "|" + flagKey, 100) < rolloutPercent;
        }
    }
}
