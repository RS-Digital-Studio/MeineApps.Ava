#nullable enable

namespace HandwerkerImperium.Domain.Save
{
    /// <summary>
    /// Reparatur statt Ablehnung (CLAUDE.md §7): klemmt einen (ggf. manipulierten oder beschädigten) Save
    /// auf strukturell gültige Invarianten — nie ein Wipe. Hier nur <b>strukturelle</b> Grenzen
    /// (Nicht-Negativität, Stern 1–5, Prestige/Stadt 0–3, Bauphasen ≤ Total); spielbalance-abhängige
    /// Caps liegen im BalancingConfig und werden vom Game-Layer angewandt. Idempotent + Unity-frei.
    /// </summary>
    public static class SaveSanitizer
    {
        /// <summary>Maximale Prestige-Stufen (GDD §16: 4 Städte = 3 Prestige).</summary>
        public const int MaxPrestige = 3;

        public static GameSave Sanitize(GameSave save)
        {
            if (save == null) return null!;

            if (save.Economy.Money < 0m) save.Economy.Money = 0m;
            if (save.Economy.Gems < 0m) save.Economy.Gems = 0m;

            var f = save.Franchise;
            f.PrestigeCount = Clamp(f.PrestigeCount, 0, MaxPrestige);
            f.CityIndex = Clamp(f.CityIndex, 0, MaxPrestige);
            if (f.PrestigeMultiplier < 1m) f.PrestigeMultiplier = 1m;
            if (f.PrestigeCurrency < 0m) f.PrestigeCurrency = 0m;

            save.Town.CurrentStar = Clamp(save.Town.CurrentStar, 1, 5);

            if (save.Mastery.Level < 0) save.Mastery.Level = 0;
            if (save.Mastery.Xp < 0) save.Mastery.Xp = 0;

            if (save.Orders.TotalServed < 0) save.Orders.TotalServed = 0;
            if (save.Orders.PendingCount < 0) save.Orders.PendingCount = 0;

            var s = save.Stations;
            if (s.StationSpeedLevel < 0) s.StationSpeedLevel = 0;
            if (s.CollectRadiusLevel < 0) s.CollectRadiusLevel = 0;
            if (s.CarryCapacityLevel < 0) s.CarryCapacityLevel = 0;
            foreach (var st in s.Stations)
                if (st.Stock < 0) st.Stock = 0;

            foreach (var w in save.Workers.Workers)
                if (w.Level < 0) w.Level = 0;

            foreach (var lm in save.Restoration.Landmarks)
            {
                if (lm.TotalPhases < 0) lm.TotalPhases = 0;
                lm.PhasesComplete = Clamp(lm.PhasesComplete, 0, lm.TotalPhases);
            }

            return save;
        }

        private static int Clamp(int value, int lo, int hi) => value < lo ? lo : (value > hi ? hi : value);
    }
}
