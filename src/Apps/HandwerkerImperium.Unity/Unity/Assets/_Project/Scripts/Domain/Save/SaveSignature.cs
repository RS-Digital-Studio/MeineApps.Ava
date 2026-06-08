#nullable enable
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HandwerkerImperium.Domain.Save
{
    /// <summary>
    /// Anti-Cheat-Signatur (CLAUDE.md §7): gerätegebundene HMAC-SHA256 über die sicherheitskritischen
    /// Kernwerte des Saves. Verifikation lokal in konstanter Zeit; bei ungültiger Signatur wird der Save
    /// <b>repariert</b> (<see cref="SaveSanitizer"/>), nicht verworfen. Echte Ablehnung nur server-seitig
    /// für Online-Werte (post-MVP). Unity-frei, deterministisch, NUnit-testbar.
    /// </summary>
    public static class SaveSignature
    {
        /// <summary>
        /// Kanonische, kultur-invariante Nutzlast über die signierten Felder. Reihenfolge/Format sind Teil
        /// des Vertrags — jede Änderung invalidiert bestehende Signaturen (dann greift Sanitize-Reparatur).
        /// </summary>
        public static string CanonicalPayload(GameSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("v").Append(save.SchemaVersion.ToString(ci));
            sb.Append("|p").Append(save.Franchise.PrestigeCount.ToString(ci));
            sb.Append("|c").Append(save.Franchise.CityIndex.ToString(ci));
            sb.Append("|m").Append(save.Economy.Money.ToString("F2", ci));
            sb.Append("|g").Append(save.Economy.Gems.ToString("F2", ci));
            sb.Append("|o").Append(save.Orders.TotalServed.ToString(ci));
            sb.Append("|mx").Append(save.Mastery.Xp.ToString("F2", ci));
            sb.Append("|st").Append(save.Town.CurrentStar.ToString(ci));
            return sb.ToString();
        }

        /// <summary>Berechnet die HMAC-SHA256-Signatur (Hex) über die kanonische Nutzlast mit dem Geräteschlüssel.</summary>
        public static string Compute(GameSave save, string deviceKey)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(deviceKey ?? "")))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(CanonicalPayload(save)));
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Schreibt die aktuelle Signatur in den Save.</summary>
        public static void Sign(GameSave save, string deviceKey) => save.Signature = Compute(save, deviceKey);

        /// <summary>True, wenn die gespeicherte Signatur zur erwarteten passt (konstante Vergleichszeit).</summary>
        public static bool Verify(GameSave save, string deviceKey)
        {
            string expected = Compute(save, deviceKey);
            return FixedTimeEquals(expected, save.Signature ?? "");
        }

        /// <summary>Zeitkonstanter String-Vergleich (Timing-Angriffsschutz, FixedTimeEquals-Äquivalent).</summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
