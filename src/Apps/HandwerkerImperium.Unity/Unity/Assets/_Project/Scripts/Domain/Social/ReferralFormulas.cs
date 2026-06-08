#nullable enable
using System.Text;
using HandwerkerImperium.Domain.Common;

namespace HandwerkerImperium.Domain.Social
{
    /// <summary>
    /// Referral-System (P3 §2/§4): 6-stelliger, deterministisch aus der PlayerId abgeleiteter Code + 3-Tier-Reward
    /// (50/200/500 Gems sinngemäß) + Selbst-Referral-Sperre. Reine, Unity-freie Logik; echte Einlösungs-
    /// Validierung läuft server-seitig (Anti-Cheat), hier Format/Code-Ableitung/Reward-Stufung.
    /// </summary>
    public static class ReferralFormulas
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"; // 36 Zeichen, keine Verwechsler-Filterung (Server kanonisiert)
        public const int CodeLength = 6;

        /// <summary>Leitet den stabilen 6-stelligen Referral-Code aus der PlayerId ab (gleiche Id → gleicher Code).</summary>
        public static string GenerateCode(string playerId)
        {
            uint h = StableHash.Fnv1a(playerId);
            var sb = new StringBuilder(CodeLength);
            for (int i = 0; i < CodeLength; i++)
            {
                sb.Append(Alphabet[(int)(h % (uint)Alphabet.Length)]);
                h /= (uint)Alphabet.Length;
                if (h == 0) h = StableHash.Fnv1a(playerId + i); // genug Entropie für 6 Stellen
            }
            return sb.ToString();
        }

        /// <summary>True, wenn der Code dem erwarteten Format entspricht (6 Zeichen aus A–Z/0–9).</summary>
        public static bool IsValidFormat(string? code)
        {
            if (code == null || code.Length != CodeLength) return false;
            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>True, wenn jemand seinen eigenen Code eingibt (Selbst-Referral, gesperrt). Case-insensitiv,
        /// damit die Sperre nicht durch Kleinschreibung umgangen werden kann.</summary>
        public static bool IsSelfReferral(string ownCode, string enteredCode) =>
            !string.IsNullOrEmpty(ownCode) && string.Equals(ownCode, enteredCode, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Erreichte Belohnungs-Stufe (0..3) für die Anzahl geworbener Spieler gegen aufsteigende Schwellen
        /// (z. B. {1, 5, 10}).
        /// </summary>
        public static int TierForCount(int referralCount, int[] tierThresholds)
        {
            if (tierThresholds == null) return 0;
            int tier = 0;
            for (int i = 0; i < tierThresholds.Length; i++)
                if (referralCount >= tierThresholds[i]) tier = i + 1;
            return tier;
        }

        /// <summary>Gem-Belohnung einer Stufe (1→50, 2→200, 3→500 sinngemäß; 0 = keine).</summary>
        public static int TierReward(int tier, int[] tierRewards)
        {
            if (tier <= 0 || tierRewards == null || tier > tierRewards.Length) return 0;
            return tierRewards[tier - 1];
        }
    }
}
