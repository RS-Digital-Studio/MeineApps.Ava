#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Story
{
    /// <summary>
    /// Default-Story-Beats (Meister-Hans-Arc, P1 §3 / GDD §2): an Loop-Meilensteine gekoppelte Beats
    /// (Intro + erste Produktion + erster Worker + erster Plot + erste Sanierung + erstes Prestige).
    /// Reine Daten (Beat-Id + Auslöser + Reihenfolge); der Voice-/Text-Inhalt ist Content im Game-Layer/Localization.
    /// </summary>
    public static class StoryCatalog
    {
        public static List<StoryBeatDefinition> Default()
        {
            return new List<StoryBeatDefinition>
            {
                new StoryBeatDefinition("hans_intro",            StoryTrigger.GameStart,             0),
                new StoryBeatDefinition("hans_first_production", StoryTrigger.FirstStationProduce,   0),
                new StoryBeatDefinition("hans_first_worker",     StoryTrigger.FirstWorkerHired,      0),
                new StoryBeatDefinition("hans_first_plot",       StoryTrigger.FirstPlotUnlocked,     0),
                new StoryBeatDefinition("hans_first_landmark",   StoryTrigger.FirstLandmarkRestored, 0),
                new StoryBeatDefinition("hans_first_prestige",   StoryTrigger.FirstPrestige,         0),
            };
        }
    }
}
