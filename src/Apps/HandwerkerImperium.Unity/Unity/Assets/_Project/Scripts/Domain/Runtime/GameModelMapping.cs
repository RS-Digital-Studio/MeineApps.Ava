#nullable enable
using System.Collections.Generic;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.LiveOps;
using HandwerkerImperium.Domain.Restoration;
using HandwerkerImperium.Domain.Save;

namespace HandwerkerImperium.Domain.Runtime
{
    /// <summary>
    /// Übersetzt zwischen dem Laufzeit-<see cref="GameModel"/> und dem persistierten, HMAC-signierten
    /// <see cref="GameSave"/> (Slices). Trennt Laufzeit-Repräsentation (reiche Idle-/Meta-States) von der
    /// schlanken Persistenz. Reine, Unity-freie Abbildung.
    /// </summary>
    public static class GameModelMapping
    {
        /// <summary>Schreibt den Laufzeit-Zustand in ein frisches Save-Objekt (noch ohne Signatur).</summary>
        public static GameSave ToSave(GameModel m)
        {
            var s = new GameSave
            {
                SchemaVersion = SaveMigrator.CurrentSchemaVersion,
                LastSeenUtcTicks = m.Idle.LastSeenUtcTicks
            };

            s.Economy.Money = m.Idle.Money;
            s.Economy.Gems = m.Gems;

            s.Stations.StationSpeedLevel = m.Idle.StationSpeedLevel;
            s.Stations.CollectRadiusLevel = m.Idle.CollectRadiusLevel;
            s.Stations.CarryCapacityLevel = m.Idle.CarryCapacityLevel;
            foreach (var st in m.Idle.Stations)
            {
                s.Stations.Stations.Add(new StationSaveData { Id = st.Id, Unlocked = st.Unlocked, Stock = st.Stock });
                if (st.HasWorker)
                    s.Workers.Workers.Add(new WorkerSaveData { StationId = st.Id, Hired = true, Level = 0 });
            }

            s.Orders.TotalServed = m.Orders.TotalServed;
            s.Orders.PendingCount = m.Orders.PendingCustomers;

            foreach (var lm in m.Landmarks)
                s.Restoration.Landmarks.Add(new LandmarkSaveData { Id = lm.Id, PhasesComplete = lm.PhasesComplete, TotalPhases = lm.TotalPhases });

            s.Franchise.PrestigeCount = m.Meta.PrestigeCount;
            s.Franchise.CityIndex = m.Meta.CityIndex;
            s.Franchise.PrestigeMultiplier = m.Meta.PrestigeMultiplier;
            s.Franchise.PrestigeCurrency = m.Meta.PrestigeCurrency;
            s.Town.CurrentStar = m.Meta.CurrentStar;
            s.Mastery.Level = m.Meta.MasteryLevel;
            s.Mastery.Xp = m.Meta.MasteryXp;
            s.Endgame.MeistergradGrade = m.Meta.MeistergradGrade;
            s.Endgame.Renommee = m.Meta.Renommee;
            s.Perkboard.AvailableMarks = m.Meta.AvailableMarks;
            s.Perkboard.PerkLevels = new List<int>(m.PerkLevels);
            s.Collection.CollectedMasterTools = new List<string>(m.CollectedMasterTools);
            s.Cosmetics.OwnedSkins = new List<string>(m.OwnedSkins);
            s.Cosmetics.ActiveSkin = m.ActiveSkin;
            s.Progress.DailyLastClaimUtcTicks = m.DailyLastClaimUtcTicks;
            s.Progress.DailyStreakDay = m.DailyStreakDay;
            s.Progress.PlayedStoryBeats = new List<string>(m.PlayedStoryBeats);
            s.Progress.ClaimedAchievements = new List<string>(m.ClaimedAchievements);
            s.Progress.DailyTaskRollDayUtc = m.DailyTaskRollDayUtc;
            s.Progress.DailyTasks = new List<DailyTaskSaveData>();
            foreach (var dt in m.DailyTasks)
                s.Progress.DailyTasks.Add(new DailyTaskSaveData
                {
                    Id = dt.Id, Metric = (int)dt.Metric, Target = dt.Target,
                    GemReward = dt.GemReward, Baseline = dt.Baseline, Claimed = dt.Claimed
                });
            return s;
        }

        /// <summary>
        /// Baut den Laufzeit-Zustand aus einem (ggf. sanitisierten) Save: startet frisch aus dem Idle-Balancing
        /// und überlagert die persistierten Werte (Stationen per Id gematcht — robust gegen Katalog-Änderungen).
        /// </summary>
        public static GameModel FromSave(GameSave s, IdleBalancing idleBalancing)
        {
            SaveSanitizer.EnsureSlices(s);
            var m = GameModel.CreateNew(idleBalancing);

            m.Idle.Money = s.Economy.Money;
            m.Gems = s.Economy.Gems;
            m.Idle.StationSpeedLevel = s.Stations.StationSpeedLevel;
            m.Idle.CollectRadiusLevel = s.Stations.CollectRadiusLevel;
            m.Idle.CarryCapacityLevel = s.Stations.CarryCapacityLevel;
            m.Idle.LastSeenUtcTicks = s.LastSeenUtcTicks;

            foreach (var sd in s.Stations.Stations)
            {
                var st = FindStation(m.Idle, sd.Id);
                if (st != null) { st.Unlocked = sd.Unlocked; st.Stock = sd.Stock; }
            }
            foreach (var w in s.Workers.Workers)
            {
                if (w == null || !w.Hired) continue;
                var st = FindStation(m.Idle, w.StationId);
                if (st != null) st.HasWorker = true;
            }

            m.Orders.TotalServed = s.Orders.TotalServed;
            m.Orders.PendingCustomers = s.Orders.PendingCount;

            m.Landmarks.Clear();
            foreach (var lm in s.Restoration.Landmarks)
                m.Landmarks.Add(new LandmarkState(lm.Id, lm.TotalPhases) { PhasesComplete = lm.PhasesComplete });

            m.Meta.PrestigeCount = s.Franchise.PrestigeCount;
            m.Meta.CityIndex = s.Franchise.CityIndex;
            m.Meta.PrestigeMultiplier = s.Franchise.PrestigeMultiplier;
            m.Meta.PrestigeCurrency = s.Franchise.PrestigeCurrency;
            m.Meta.CurrentStar = s.Town.CurrentStar;
            m.Meta.MasteryLevel = s.Mastery.Level;
            m.Meta.MasteryXp = s.Mastery.Xp;
            m.Meta.MeistergradGrade = s.Endgame.MeistergradGrade;
            m.Meta.Renommee = s.Endgame.Renommee;
            m.Meta.AvailableMarks = s.Perkboard.AvailableMarks;
            m.PerkLevels = new List<int>(s.Perkboard.PerkLevels);
            m.CollectedMasterTools = new List<string>(s.Collection.CollectedMasterTools);
            m.OwnedSkins = new List<string>(s.Cosmetics.OwnedSkins);
            m.ActiveSkin = s.Cosmetics.ActiveSkin;
            m.DailyLastClaimUtcTicks = s.Progress.DailyLastClaimUtcTicks;
            m.DailyStreakDay = s.Progress.DailyStreakDay;
            m.PlayedStoryBeats = new List<string>(s.Progress.PlayedStoryBeats);
            m.ClaimedAchievements = new List<string>(s.Progress.ClaimedAchievements);
            m.DailyTaskRollDayUtc = s.Progress.DailyTaskRollDayUtc;
            m.DailyTasks = new List<DailyTaskRuntime>();
            foreach (var dt in s.Progress.DailyTasks)
            {
                if (dt == null) continue;
                m.DailyTasks.Add(new DailyTaskRuntime
                {
                    Id = dt.Id ?? "", Metric = (DailyTaskMetric)dt.Metric, Target = dt.Target,
                    GemReward = dt.GemReward, Baseline = dt.Baseline, Claimed = dt.Claimed
                });
            }

            // Abgeleitete Aggregat-Zähler (Stern-Scoring)
            m.Meta.RestorationPhases = RestorationFormulas.TotalPhasesComplete(m.Landmarks);
            m.Meta.OrdersServed = s.Orders.TotalServed;
            return m;
        }

        private static StationState? FindStation(GreyboxSimState idle, string id)
        {
            foreach (var st in idle.Stations)
                if (st.Id == id) return st;
            return null;
        }
    }
}
