#nullable enable
using System.Collections.Generic;
using HandwerkerImperium.Domain.Progression;

namespace HandwerkerImperium.Domain.Save
{
    /// <summary>
    /// Reparatur statt Ablehnung (CLAUDE.md §7): klemmt einen (ggf. manipulierten oder beschädigten) Save
    /// auf strukturell gültige Invarianten — nie ein Wipe, nie ein Crash. Stellt zuerst alle Slices/Listen
    /// sicher (deserialisierte <c>null</c>-Slices), dann klemmt es strukturelle Grenzen (Nicht-Negativität,
    /// Stern 1–5, Prestige 0–3, Stadt 0–3, Multiplikator-Bereich, Bauphasen ≤ Total). Idempotent + Unity-frei.
    /// </summary>
    public static class SaveSanitizer
    {
        /// <summary>Höchster Stadt-Index (4 Städte → 0..3). Eigene Größe, NICHT an MaxPrestige gekoppelt.</summary>
        public const int MaxCityIndex = 3;

        /// <summary>Großzügige Obergrenze des permanenten Prestige-Multiplikators gegen absurde Manipulation.</summary>
        public const decimal MaxPrestigeMultiplier = 1000m;

        /// <summary>
        /// Stellt sicher, dass alle Slices und Listen instanziiert sind (Newtonsoft lässt fehlende/explizit
        /// <c>null</c> gesetzte Slices auf <c>null</c>). MUSS vor Signatur-Verifikation/Sanitize laufen.
        /// </summary>
        public static GameSave EnsureSlices(GameSave save)
        {
            if (save == null) return null!;
            save.Economy ??= new EconomySlice();
            save.Stations ??= new StationsSlice();
            save.Stations.Stations ??= new List<StationSaveData>();
            save.Workers ??= new WorkersSlice();
            save.Workers.Workers ??= new List<WorkerSaveData>();
            save.Orders ??= new OrdersSlice();
            save.Restoration ??= new RestorationSlice();
            save.Restoration.Landmarks ??= new List<LandmarkSaveData>();
            save.Franchise ??= new FranchiseSlice();
            save.Town ??= new TownSlice();
            save.Mastery ??= new MasterySlice();
            save.Cosmetics ??= new CosmeticsSlice();
            save.Cosmetics.OwnedSkins ??= new List<string>();
            save.Endgame ??= new EndgameSlice();
            save.Perkboard ??= new PerkboardSlice();
            save.Perkboard.PerkLevels ??= new List<int>();
            save.Collection ??= new CollectionSlice();
            save.Collection.CollectedMasterTools ??= new List<string>();
            save.Progress ??= new ProgressSlice();
            save.Progress.PlayedStoryBeats ??= new List<string>();
            save.Progress.ClaimedAchievements ??= new List<string>();
            save.Progress.DailyTasks ??= new List<DailyTaskSaveData>();
            return save;
        }

        public static GameSave Sanitize(GameSave save)
        {
            if (save == null) return null!;
            EnsureSlices(save);

            if (save.Economy.Money < 0m) save.Economy.Money = 0m;
            if (save.Economy.Gems < 0m) save.Economy.Gems = 0m;

            var f = save.Franchise;
            f.PrestigeCount = Clamp(f.PrestigeCount, 0, PrestigeFormulas.MaxPrestige);
            f.CityIndex = Clamp(f.CityIndex, 0, MaxCityIndex);
            if (f.PrestigeMultiplier < 1m) f.PrestigeMultiplier = 1m;
            if (f.PrestigeMultiplier > MaxPrestigeMultiplier) f.PrestigeMultiplier = MaxPrestigeMultiplier;
            if (f.PrestigeCurrency < 0m) f.PrestigeCurrency = 0m;

            save.Town.CurrentStar = Clamp(save.Town.CurrentStar, 1, 5);

            if (save.Mastery.Level < 0) save.Mastery.Level = 0;
            if (!(save.Mastery.Xp >= 0)) save.Mastery.Xp = 0; // fängt negativ UND NaN

            if (save.Orders.TotalServed < 0) save.Orders.TotalServed = 0;
            if (save.Orders.PendingCount < 0) save.Orders.PendingCount = 0;

            var s = save.Stations;
            if (s.StationSpeedLevel < 0) s.StationSpeedLevel = 0;
            if (s.CollectRadiusLevel < 0) s.CollectRadiusLevel = 0;
            if (s.CarryCapacityLevel < 0) s.CarryCapacityLevel = 0;
            foreach (var st in s.Stations)
                if (st != null && st.Stock < 0) st.Stock = 0;

            foreach (var w in save.Workers.Workers)
                if (w != null && w.Level < 0) w.Level = 0;

            foreach (var lm in save.Restoration.Landmarks)
            {
                if (lm == null) continue;
                if (lm.TotalPhases < 0) lm.TotalPhases = 0;
                lm.PhasesComplete = Clamp(lm.PhasesComplete, 0, lm.TotalPhases);
            }

            if (save.Endgame.MeistergradGrade < 0) save.Endgame.MeistergradGrade = 0;
            if (save.Endgame.Renommee < 0m) save.Endgame.Renommee = 0m;
            if (save.Perkboard.AvailableMarks < 0) save.Perkboard.AvailableMarks = 0;
            for (int i = 0; i < save.Perkboard.PerkLevels.Count; i++)
                if (save.Perkboard.PerkLevels[i] < 0) save.Perkboard.PerkLevels[i] = 0;
            if (save.Progress.DailyStreakDay < 0) save.Progress.DailyStreakDay = 0;

            return save;
        }

        private static int Clamp(int value, int lo, int hi) => value < lo ? lo : (value > hi ? hi : value);
    }
}
