#nullable enable
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HandwerkerImperium.Domain.Save
{
    /// <summary>
    /// Anti-Cheat-Signatur (CLAUDE.md §7): gerätegebundene HMAC-SHA256 über <b>alle</b> gameplay-/economy-
    /// kritischen Save-Werte. Verifikation lokal in konstanter Zeit; bei ungültiger Signatur wird der Save
    /// <b>repariert</b> (<see cref="SaveSanitizer"/>), nicht verworfen. Echte Ablehnung nur server-seitig
    /// für Online-Werte (post-MVP). Unity-frei, deterministisch, NUnit-testbar.
    /// </summary>
    public static class SaveSignature
    {
        /// <summary>
        /// Kanonische, kultur-invariante Nutzlast über die signierten Felder — deckt Geld/Gems, Prestige
        /// (Zahl/Stadt/Multiplikator/Währung), Mastery, Stern, alle Stations-Level + je-Station-Zustand,
        /// Worker, Sanierungs-Fortschritt und Cosmetics-Besitz ab. Reihenfolge/Format sind Teil des Vertrags;
        /// jede Änderung invalidiert bestehende Signaturen (dann greift Sanitize-Reparatur). Null-Slice-robust.
        /// </summary>
        public static string CanonicalPayload(GameSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            var ci = CultureInfo.InvariantCulture;
            var e = save.Economy ?? new EconomySlice();
            var f = save.Franchise ?? new FranchiseSlice();
            var o = save.Orders ?? new OrdersSlice();
            var m = save.Mastery ?? new MasterySlice();
            var t = save.Town ?? new TownSlice();
            var st = save.Stations ?? new StationsSlice();
            var w = save.Workers ?? new WorkersSlice();
            var r = save.Restoration ?? new RestorationSlice();
            var c = save.Cosmetics ?? new CosmeticsSlice();

            var sb = new StringBuilder();
            sb.Append("v").Append(save.SchemaVersion.ToString(ci));
            sb.Append("|p").Append(f.PrestigeCount.ToString(ci));
            sb.Append("|c").Append(f.CityIndex.ToString(ci));
            sb.Append("|pm").Append(f.PrestigeMultiplier.ToString("F4", ci));
            sb.Append("|pc").Append(f.PrestigeCurrency.ToString("F2", ci));
            sb.Append("|m").Append(e.Money.ToString("F2", ci));
            sb.Append("|g").Append(e.Gems.ToString("F2", ci));
            sb.Append("|o").Append(o.TotalServed.ToString(ci));
            sb.Append("|ml").Append(m.Level.ToString(ci));
            sb.Append("|mx").Append(m.Xp.ToString("F2", ci));
            sb.Append("|st").Append(t.CurrentStar.ToString(ci));
            sb.Append("|lv").Append(st.StationSpeedLevel.ToString(ci))
              .Append(",").Append(st.CollectRadiusLevel.ToString(ci))
              .Append(",").Append(st.CarryCapacityLevel.ToString(ci));

            sb.Append("|S");
            if (st.Stations != null)
                foreach (var s in st.Stations)
                {
                    if (s == null) continue;
                    sb.Append("[").Append(s.Id ?? "").Append(":")
                      .Append(s.Unlocked ? "1" : "0").Append(":")
                      .Append(s.Stock.ToString(ci)).Append("]");
                }

            sb.Append("|W");
            if (w.Workers != null)
                foreach (var wk in w.Workers)
                {
                    if (wk == null) continue;
                    sb.Append("[").Append(wk.StationId ?? "").Append(":")
                      .Append(wk.Hired ? "1" : "0").Append(":")
                      .Append(wk.Level.ToString(ci)).Append("]");
                }

            sb.Append("|R");
            if (r.Landmarks != null)
                foreach (var lm in r.Landmarks)
                {
                    if (lm == null) continue;
                    sb.Append("[").Append(lm.Id ?? "").Append(":")
                      .Append(lm.PhasesComplete.ToString(ci)).Append(":")
                      .Append(lm.TotalPhases.ToString(ci)).Append("]");
                }

            sb.Append("|C").Append(c.ActiveSkin ?? "");
            if (c.OwnedSkins != null)
                foreach (var sk in c.OwnedSkins)
                    sb.Append(",").Append(sk ?? "");

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

        /// <summary>
        /// True, wenn die gespeicherte Signatur zur erwarteten passt (konstante Vergleichszeit).
        /// Ein leerer/null Geräteschlüssel schlägt hart fehl (kein Leerschlüssel-Bypass).
        /// </summary>
        public static bool Verify(GameSave save, string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey) || save == null) return false;
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
