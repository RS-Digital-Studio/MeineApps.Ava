using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Idle
{
    /// <summary>Die drei globalen Upgrade-Achsen des Greybox-Loops (P0 §2: 3 Upgrade-Pads).</summary>
    public enum UpgradeTrack
    {
        /// <summary>Produktionstempo aller Stationen.</summary>
        StationSpeed = 0,
        /// <summary>Auto-Pickup-Sammelradius des Avatars.</summary>
        CollectRadius = 1,
        /// <summary>Trag-Kapazitaet des Avatars.</summary>
        CarryCapacity = 2,
    }

    /// <summary>Laufzeit-Zustand einer einzelnen Station (Greybox).</summary>
    public sealed class StationState
    {
        public string Id = "";
        /// <summary>Ob die Station freigeschaltet ist (Station 4 erst nach Plot-Unlock).</summary>
        public bool Unlocked;
        /// <summary>Aktuell an der Station gestapelte (noch nicht abgeholte) Waren.</summary>
        public int Stock;
        /// <summary>Sekunden-Akkumulator bis zur naechsten produzierten Ware.</summary>
        public double ProduceProgressSeconds;
        /// <summary>Ob ein NPC-Arbeiter diese Station automatisiert (Tragen uebernimmt).</summary>
        public bool HasWorker;
        /// <summary>Waren-Akkumulator des Workers (fraktionales Tragen ueber Ticks).</summary>
        public double WorkerProgress;
        /// <summary>Gekaufte Worker-Tempo-Stufen (GDD §6.2: 3-5 Stufen; 0 = frisch angestellt). Persistiert.</summary>
        public int WorkerLevel;
        /// <summary>Temporaerer Tempo-Buff der Perfekt-Aktion (GDD §6.7). FLUECHTIG — bewusst
        /// nicht persistiert (Buff-Dauer ~Sekunden; Save-Schema + HMAC bleiben unberuehrt).</summary>
        public double BoostMultiplier = 1.0;
        /// <summary>Restlaufzeit des Tempo-Buffs in Sekunden (0 = kein Buff aktiv).</summary>
        public double BoostRemainingSeconds;

        public StationState() { }
        public StationState(string id, bool unlocked) { Id = id; Unlocked = unlocked; }
    }

    /// <summary>
    /// Gesamter, serialisierbarer Laufzeit-Zustand des Greybox-Prototyps (P0). Unity-frei.
    /// Wird von <see cref="GreyboxSimulation"/> deterministisch fortgeschrieben und vom
    /// Game-Layer (MonoBehaviours) nur gerendert/bedient — daher headless NUnit-testbar.
    /// </summary>
    public sealed class GreyboxSimState
    {
        public decimal Money;

        public List<StationState> Stations = new List<StationState>();

        // Globale Upgrade-Stufen (eine je UpgradeTrack)
        public int StationSpeedLevel;
        public int CollectRadiusLevel;
        public int CarryCapacityLevel;

        /// <summary>UTC-Ticks des letzten Speicherns — Basis fuer die Offline-Berechnung beim Re-Start.</summary>
        public long LastSeenUtcTicks;

        /// <summary>Liefert die Upgrade-Stufe einer Achse.</summary>
        public int GetLevel(UpgradeTrack track) => track switch
        {
            UpgradeTrack.StationSpeed => StationSpeedLevel,
            UpgradeTrack.CollectRadius => CollectRadiusLevel,
            UpgradeTrack.CarryCapacity => CarryCapacityLevel,
            _ => 0
        };

        /// <summary>Setzt die Upgrade-Stufe einer Achse.</summary>
        public void SetLevel(UpgradeTrack track, int level)
        {
            switch (track)
            {
                case UpgradeTrack.StationSpeed: StationSpeedLevel = level; break;
                case UpgradeTrack.CollectRadius: CollectRadiusLevel = level; break;
                case UpgradeTrack.CarryCapacity: CarryCapacityLevel = level; break;
            }
        }

        /// <summary>Erzeugt den Startzustand aus dem Balancing (Stationen 1:1, Stock 0, Money 0).</summary>
        public static GreyboxSimState CreateNew(IdleBalancing balancing)
        {
            var state = new GreyboxSimState { Money = 0m };
            for (int i = 0; i < balancing.Stations.Count; i++)
            {
                var sb = balancing.Stations[i];
                state.Stations.Add(new StationState(sb.Id, sb.UnlockedAtStart));
            }
            return state;
        }
    }
}
