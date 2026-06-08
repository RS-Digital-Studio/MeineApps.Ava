#nullable enable
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HandwerkerImperium.Domain.Social
{
    /// <summary>Bewertungs-Kategorie eines Leaderboards (P3 §2/§4).</summary>
    public enum LeaderboardCategory { Cash = 0, Meistergrad = 1, Income = 2 }

    /// <summary>
    /// Leaderboard-Einträge (P3 §2/§4, CLAUDE.md §7): kanonische Score-Nutzlast + HMAC-Signatur. Clientseitige
    /// Werte werden NIE vertraut — die Signatur ist nur die erste Hürde; die echte Validierung läuft
    /// server-seitig (atomares PATCH + validate-Rules). PlayerId ist die stabile UUID, nicht die Firebase-UID.
    /// Reine, Unity-freie Mathematik.
    /// </summary>
    public static class LeaderboardFormulas
    {
        /// <summary>Kanonische, kultur-invariante Score-Nutzlast (Reihenfolge ist Teil des Vertrags).</summary>
        public static string CanonicalEntry(string playerId, long score, LeaderboardCategory category, long serverTimestampTicks)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("pid:").Append(playerId ?? "");
            sb.Append("|cat:").Append(((int)category).ToString(ci));
            sb.Append("|sc:").Append(score.ToString(ci));
            sb.Append("|ts:").Append(serverTimestampTicks.ToString(ci));
            return sb.ToString();
        }

        /// <summary>HMAC-SHA256 (Hex) über die Score-Nutzlast mit dem Server-Schlüssel.</summary>
        public static string Sign(string playerId, long score, LeaderboardCategory category, long serverTimestampTicks, string serverKey)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(serverKey ?? "")))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(CanonicalEntry(playerId, score, category, serverTimestampTicks)));
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Verifiziert eine Score-Signatur in konstanter Zeit; leerer Schlüssel schlägt hart fehl.</summary>
        public static bool Verify(string playerId, long score, LeaderboardCategory category, long serverTimestampTicks, string serverKey, string signature)
        {
            if (string.IsNullOrEmpty(serverKey)) return false;
            string expected = Sign(playerId, score, category, serverTimestampTicks, serverKey);
            return FixedTimeEquals(expected, signature ?? "");
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
