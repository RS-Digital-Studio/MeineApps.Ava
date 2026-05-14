namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Bounded-Context "Missions": Alle Daily/Weekly/Goal-Loops + Random-Reward-Loops.
/// Service-Sprawl-Reduction (12.05.2026).
///
/// Bündelt die fünf Mission-Subsysteme die heute einzeln im MainViewModel injiziert sind.
/// MissionsFeatureViewModel könnte diese Facade nutzen statt fünf Einzel-Dependencies.
/// </summary>
public interface IMissionsFacade
{
    /// <summary>Tägliche Herausforderungen (3 pro Tag, Score-Mapping aus Mini-Game-Rating).</summary>
    IDailyChallengeService Daily { get; }

    /// <summary>Wöchentliche Missionen (10 pro Woche, Reward-Track).</summary>
    IWeeklyMissionService Weekly { get; }

    /// <summary>Glücksrad (1x gratis pro Tag + Ad-Spin).</summary>
    ILuckySpinService LuckySpin { get; }

    /// <summary>Schnellaufträge (rotierende Quick-Jobs, kein Mini-Game).</summary>
    IQuickJobService QuickJob { get; }

    /// <summary>"Nächstes Ziel"-Banner — kontextuelle Empfehlungen.</summary>
    IGoalService Goal { get; }
}
