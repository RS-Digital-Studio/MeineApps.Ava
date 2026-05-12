using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementierung der <see cref="IMissionsFacade"/> — Service-Container für die fünf
/// Mission-/Goal-Subsysteme. Singleton, kein State.
/// </summary>
public sealed class MissionsFacade : IMissionsFacade
{
    public IDailyChallengeService Daily { get; }
    public IWeeklyMissionService Weekly { get; }
    public ILuckySpinService LuckySpin { get; }
    public IQuickJobService QuickJob { get; }
    public IGoalService Goal { get; }

    public MissionsFacade(
        IDailyChallengeService daily,
        IWeeklyMissionService weekly,
        ILuckySpinService luckySpin,
        IQuickJobService quickJob,
        IGoalService goal)
    {
        Daily = daily;
        Weekly = weekly;
        LuckySpin = luckySpin;
        QuickJob = quickJob;
        Goal = goal;
    }
}
