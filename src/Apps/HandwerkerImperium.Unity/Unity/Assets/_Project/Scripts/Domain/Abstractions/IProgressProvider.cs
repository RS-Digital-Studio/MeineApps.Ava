namespace HandwerkerImperium.Domain.Abstractions
{
    /// <summary>
    /// Interface für Models die einen Fortschrittswert bereitstellen (DailyChallenge, WeeklyMission).
    /// 1:1-Port aus dem Avalonia-Original (Models/IProgressProvider.cs).
    /// </summary>
    public interface IProgressProvider
    {
        double Progress { get; }
    }
}
