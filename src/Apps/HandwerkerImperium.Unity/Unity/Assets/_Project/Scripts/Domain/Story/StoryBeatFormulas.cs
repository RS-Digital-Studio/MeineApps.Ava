#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Story
{
    /// <summary>Loop-Ereignis, das einen Meister-Hans-Story-Beat auslöst (P1 §3, GDD §2).</summary>
    public enum StoryTrigger
    {
        GameStart = 0,
        FirstStationProduce = 1,
        FirstWorkerHired = 2,
        FirstPlotUnlocked = 3,
        FirstLandmarkRestored = 4,
        FirstPrestige = 5
    }

    /// <summary>Definition eines Story-Beats: Auslöser + Abspielreihenfolge (Voice-/Text-Inhalt = Content im Game-Layer).</summary>
    public sealed class StoryBeatDefinition
    {
        public string Id;
        public StoryTrigger Trigger;
        public int Order;

        public StoryBeatDefinition(string id, StoryTrigger trigger, int order)
        {
            Id = id;
            Trigger = trigger;
            Order = order;
        }
    }

    /// <summary>
    /// Story-Beats (P1 §3 / GDD §2): Hans-Intro + ~4 Beats, an Loop-Events gekoppelt („learn-by-doing"
    /// statt FTUE-Maschine). Reine, Unity-freie Auswahl-Logik: welche noch nicht gespielten Beats ein
    /// Auslöser jetzt freischaltet (in Reihenfolge). Voice-Playback + Text liegen im Game-Layer.
    /// </summary>
    public static class StoryBeatFormulas
    {
        /// <summary>
        /// Liefert die Ids der Beats, die <paramref name="trigger"/> jetzt abspielt: passender Auslöser,
        /// noch nicht in <paramref name="alreadyPlayed"/>, sortiert nach <see cref="StoryBeatDefinition.Order"/>.
        /// </summary>
        public static List<string> BeatsForTrigger(
            IReadOnlyList<StoryBeatDefinition>? catalog, StoryTrigger trigger, IReadOnlyCollection<string>? alreadyPlayed)
        {
            var matches = new List<StoryBeatDefinition>();
            if (catalog == null) return new List<string>();
            foreach (var def in catalog)
            {
                if (def == null || def.Trigger != trigger) continue;
                if (alreadyPlayed != null && Contains(alreadyPlayed, def.Id)) continue;
                matches.Add(def);
            }
            matches.Sort((a, b) => a.Order.CompareTo(b.Order));
            var result = new List<string>(matches.Count);
            foreach (var d in matches) result.Add(d.Id);
            return result;
        }

        private static bool Contains(IReadOnlyCollection<string> set, string id)
        {
            foreach (var x in set)
                if (x == id) return true;
            return false;
        }
    }
}
