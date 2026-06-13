#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Save
{
    /// <summary>
    /// P1-Save-Schema (GDD §12 / CLAUDE.md §7): schlanke, genre-eigene Slices statt Avalonia-v7-1:1.
    /// Unity-frei + Newtonsoft-serialisierbar (public Felder). Migrierbar über
    /// <see cref="SaveMigrator.CurrentSchemaVersion"/>, signiert per HMAC (<see cref="SaveSignature"/>),
    /// bei ungültiger Signatur repariert statt verworfen (<see cref="SaveSanitizer"/>).
    /// </summary>
    public sealed class GameSave
    {
        /// <summary>Schema-Version (Single-Source-of-Truth: <see cref="SaveMigrator.CurrentSchemaVersion"/>).</summary>
        public int SchemaVersion = SaveMigrator.CurrentSchemaVersion;

        /// <summary>HMAC-Signatur über die sicherheitskritischen Kernwerte (gerätegebunden).</summary>
        public string Signature = "";

        /// <summary>Letzter „gesehen"-Zeitstempel (UTC-Ticks) für Offline-Berechnung.</summary>
        public long LastSeenUtcTicks;

        public EconomySlice Economy = new EconomySlice();
        public StationsSlice Stations = new StationsSlice();
        public WorkersSlice Workers = new WorkersSlice();
        public OrdersSlice Orders = new OrdersSlice();
        public RestorationSlice Restoration = new RestorationSlice();
        public FranchiseSlice Franchise = new FranchiseSlice();
        public TownSlice Town = new TownSlice();
        public MasterySlice Mastery = new MasterySlice();
        public CosmeticsSlice Cosmetics = new CosmeticsSlice();
        public EndgameSlice Endgame = new EndgameSlice();
        public PerkboardSlice Perkboard = new PerkboardSlice();
        public CollectionSlice Collection = new CollectionSlice();
        public ProgressSlice Progress = new ProgressSlice();
    }

    /// <summary>Geld + Hartwährung.</summary>
    public sealed class EconomySlice
    {
        public decimal Money;
        public decimal Gems;
    }

    /// <summary>Stations-Produktionszustand + globale Upgrade-Stufen.</summary>
    public sealed class StationsSlice
    {
        public List<StationSaveData> Stations = new List<StationSaveData>();
        public int StationSpeedLevel;
        public int CollectRadiusLevel;
        public int CarryCapacityLevel;
    }

    public sealed class StationSaveData
    {
        public string Id = "";
        public bool Unlocked;
        public int Stock;
        /// <summary>Sichtbare Werkstatt-Ausbaustufe (GDD §6.1).</summary>
        public int BuildLevel;
    }

    /// <summary>Arbeiter-Automatisierung (getrennt von der Stations-Produktion, vgl. WorkerAutomationService).</summary>
    public sealed class WorkersSlice
    {
        public List<WorkerSaveData> Workers = new List<WorkerSaveData>();
    }

    public sealed class WorkerSaveData
    {
        public string StationId = "";
        public bool Hired;
        public int Level;
    }

    /// <summary>Kunden-/Auftrags-Aggregat (OrderQueueService, P1).</summary>
    public sealed class OrdersSlice
    {
        public long TotalServed;
        public int PendingCount;
    }

    /// <summary>Stadt-Wiederaufbau: Wahrzeichen mit Bauphasen (TownRestorationService).</summary>
    public sealed class RestorationSlice
    {
        public List<LandmarkSaveData> Landmarks = new List<LandmarkSaveData>();
    }

    public sealed class LandmarkSaveData
    {
        public string Id = "";
        public int PhasesComplete;
        public int TotalPhases;
    }

    /// <summary>Prestige/Franchise: Stadt-Index, Prestige-Zahl, permanenter Multiplikator, Prestige-Währung.</summary>
    public sealed class FranchiseSlice
    {
        public int PrestigeCount;
        public int CityIndex;
        public decimal PrestigeMultiplier = 1m;
        public decimal PrestigeCurrency;
    }

    /// <summary>Stadt-Stern-Rating (1–5, persistiert für Hysterese).</summary>
    public sealed class TownSlice
    {
        public int CurrentStar = 1;
    }

    /// <summary>Meisterschafts-Track — P1 nur Stub (kontoweit, nie reset; voller Ausbau P2).</summary>
    public sealed class MasterySlice
    {
        public int Level;
        public double Xp;
    }

    /// <summary>Besitzte Skins + aktiver Skin (Cosmetics, P1-Stub).</summary>
    public sealed class CosmeticsSlice
    {
        public List<string> OwnedSkins = new List<string>();
        public string ActiveSkin = "";
    }

    /// <summary>Endgame-Meistergrade + Renommee-Ressource (P3, nie reset).</summary>
    public sealed class EndgameSlice
    {
        public int MeistergradGrade;
        public decimal Renommee;
    }

    /// <summary>Imperium-Marken-Perkboard: verfügbare Marken + Stufen je Perk-Achse (permanent).</summary>
    public sealed class PerkboardSlice
    {
        public int AvailableMarks;
        public List<int> PerkLevels = new List<int>();
    }

    /// <summary>Sammlung: gesammelte Meisterwerkzeug-Ids (permanent).</summary>
    public sealed class CollectionSlice
    {
        public List<string> CollectedMasterTools = new List<string>();
    }

    /// <summary>Live-Ops-/Story-Fortschritts-Flags (Tagesbelohnung, abgespielte Beats, eingelöste Achievements, Tagesaufgaben).</summary>
    public sealed class ProgressSlice
    {
        public long DailyLastClaimUtcTicks;
        public int DailyStreakDay;
        public List<string> PlayedStoryBeats = new List<string>();
        public List<string> ClaimedAchievements = new List<string>();

        /// <summary>UTC-Ticks der letzten Tagesaufgaben-Ausgabe (Reset auf neuen UTC-Tag).</summary>
        public long DailyTaskRollDayUtc;
        /// <summary>Die 3 aktiven Tagesaufgaben des Tages (inkl. Basiswert + Abhol-Flag).</summary>
        public List<DailyTaskSaveData> DailyTasks = new List<DailyTaskSaveData>();
    }

    /// <summary>Eine aktive Tagesaufgabe (Metrik/Ziel/Belohnung + Basiswert bei Ausgabe + Abhol-Flag).</summary>
    public sealed class DailyTaskSaveData
    {
        public string Id = "";
        public int Metric;
        public long Target;
        public int GemReward;
        public long Baseline;
        public bool Claimed;
    }
}
