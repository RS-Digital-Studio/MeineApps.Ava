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
        public int SchemaVersion;

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
}
