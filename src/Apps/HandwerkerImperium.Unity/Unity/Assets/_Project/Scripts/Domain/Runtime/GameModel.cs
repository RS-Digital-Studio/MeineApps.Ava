#nullable enable
using System.Collections.Generic;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Orders;
using HandwerkerImperium.Domain.LiveOps;
using HandwerkerImperium.Domain.Restoration;

namespace HandwerkerImperium.Domain.Runtime
{
    /// <summary>
    /// Der unifizierte Laufzeit-Zustand des Spiels — komponiert den verifizierten P0-Idle-Loop
    /// (<see cref="Idle"/>) mit der Meta-Progression (<see cref="Meta"/>) und allen Runtime-Sub-Zuständen
    /// (Kunden-Queue, Rush, Wahrzeichen, Perks, Sammlung, Daily, Cosmetics, Gems, Premium). Unity-frei;
    /// der Game-Layer hält EINE Instanz, die Services/Views lesen + mutieren, und persistiert sie via
    /// <see cref="GameModelMapping"/> ins HMAC-signierte Save.
    /// </summary>
    public sealed class GameModel
    {
        /// <summary>Aktiver Idle-Loop (Stationen, Geld, Upgrade-Stufen, Worker) — verifizierter P0-Core.</summary>
        public GreyboxSimState Idle = new GreyboxSimState();

        /// <summary>Meta-Progression (Prestige, Mastery, Meistergrad, Stern, Renommee, Marken).</summary>
        public MetaState Meta = new MetaState();

        public OrderQueueState Orders = new OrderQueueState();
        public RushEventState Rush = new RushEventState();
        public List<LandmarkState> Landmarks = new List<LandmarkState>();

        public decimal Gems;
        public bool IsPremium; // aus dem Store wiederhergestellt (nicht im Save autoritativ)

        public List<string> CollectedMasterTools = new List<string>();
        public List<int> PerkLevels = new List<int>();
        public List<string> OwnedSkins = new List<string>();
        public string ActiveSkin = "";

        public long DailyLastClaimUtcTicks;
        public int DailyStreakDay;
        public List<string> PlayedStoryBeats = new List<string>();
        public List<string> ClaimedAchievements = new List<string>();

        /// <summary>UTC-Ticks der letzten Tagesaufgaben-Ausgabe (Reset auf neuen UTC-Tag).</summary>
        public long DailyTaskRollDayUtc;
        /// <summary>Die 3 aktiven Tagesaufgaben des Tages (Laufzeit-Repräsentation, persistiert via Mapping).</summary>
        public List<DailyTaskRuntime> DailyTasks = new List<DailyTaskRuntime>();

        /// <summary>Frischer Start aus dem Idle-Balancing (Stationen 1:1, Stock 0, Geld 0); Meta = Akt 1, Stadt 0;
        /// Wahrzeichen ruiniert aus dem Katalog (Stadt-Wiederaufbau, GDD §6.4).</summary>
        public static GameModel CreateNew(IdleBalancing idleBalancing)
        {
            var m = new GameModel
            {
                Idle = GreyboxSimState.CreateNew(idleBalancing),
                Landmarks = LandmarkCatalog.CreateStates()
            };
            return m;
        }
    }

    /// <summary>Laufzeit-Zustand einer Tagesaufgabe: Ziel/Belohnung + Basiswert bei Ausgabe + Abhol-Flag.</summary>
    public sealed class DailyTaskRuntime
    {
        public string Id = "";
        public LiveOps.DailyTaskMetric Metric;
        public long Target;
        public int GemReward;
        public long Baseline;
        public bool Claimed;
    }
}
