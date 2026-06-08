#nullable enable

namespace HandwerkerImperium.Domain.Common
{
    /// <summary>
    /// Prozess-/plattformstabiler String-Hash (FNV-1a, 32-bit). <b>Pflicht</b> statt <c>string.GetHashCode()</c>,
    /// das unter IL2CPP/Mono pro Prozess randomisiert ist — wird für deterministische A/B-Buckets,
    /// Cross-Promo-Rotation und Referral-Codes gebraucht (gleicher Input → immer gleicher Hash). Unity-frei.
    /// </summary>
    public static class StableHash
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>FNV-1a-32-Hash über die UTF-16-Codeeinheiten des Strings.</summary>
        public static uint Fnv1a(string? value)
        {
            if (string.IsNullOrEmpty(value)) return FnvOffsetBasis;
            uint hash = FnvOffsetBasis;
            for (int i = 0; i < value!.Length; i++)
            {
                hash ^= value[i];
                hash *= FnvPrime;
            }
            return hash;
        }

        /// <summary>Deterministischer Bucket-Index in [0, buckets) (z. B. A/B-Test, Rotation).</summary>
        public static int Bucket(string? value, int buckets)
        {
            if (buckets <= 1) return 0;
            return (int)(Fnv1a(value) % (uint)buckets);
        }
    }
}
