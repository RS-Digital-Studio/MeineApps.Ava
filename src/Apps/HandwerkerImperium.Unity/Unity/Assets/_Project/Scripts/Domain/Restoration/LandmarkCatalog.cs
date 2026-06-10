#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Restoration
{
    /// <summary>Definition eines Wahrzeichens (Id + Anzahl Bauphasen). Reine Daten.</summary>
    public sealed class LandmarkDefinition
    {
        public string Id = "";
        public int TotalPhases;

        public LandmarkDefinition() { }

        public LandmarkDefinition(string id, int totalPhases)
        {
            Id = id;
            TotalPhases = totalPhases;
        }
    }

    /// <summary>
    /// Akt-1-Wahrzeichen des Stadt-Wiederaufbaus (GDD §6.4): drei Restaurierungs-Ziele mit steigender
    /// Phasen-Tiefe — der sichtbare Vorher/Nachher-Bogen des Akts. Jede neue Stadt (Prestige) startet
    /// mit frischen, ruinierten Wahrzeichen; der Fortschritt speist das Stern-Rating.
    /// </summary>
    public static class LandmarkCatalog
    {
        public static IReadOnlyList<LandmarkDefinition> Default()
        {
            return new List<LandmarkDefinition>
            {
                new LandmarkDefinition("brunnen", 3),
                new LandmarkDefinition("glockenturm", 4),
                new LandmarkDefinition("stadttor", 5),
            };
        }

        /// <summary>Frische (ruinierte) Wahrzeichen-Zustände für eine neue Stadt.</summary>
        public static List<LandmarkState> CreateStates()
        {
            var list = new List<LandmarkState>();
            foreach (var def in Default())
                list.Add(new LandmarkState(def.Id, def.TotalPhases));
            return list;
        }

        /// <summary>
        /// Ergänzt fehlende Katalog-Wahrzeichen (per Id) in einem geladenen Zustand, ohne bestehenden
        /// Fortschritt anzufassen — Migration für Saves aus der Zeit vor dem Katalog.
        /// </summary>
        public static void EnsureLandmarks(List<LandmarkState> landmarks)
        {
            if (landmarks == null) return;
            foreach (var def in Default())
            {
                bool found = false;
                foreach (var lm in landmarks)
                    if (lm != null && lm.Id == def.Id) { found = true; break; }
                if (!found)
                    landmarks.Add(new LandmarkState(def.Id, def.TotalPhases));
            }
        }
    }
}
