namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Fähigkeit eines Vorarbeiters/Managers.
    /// 1:1-Port aus dem Avalonia-Original (Models/Manager.cs). Enum-Reihenfolge = Persistenz-Integer.
    /// </summary>
    public enum ManagerAbility
    {
        AutoCollectOrders,
        EfficiencyBoost,
        FatigueReduction,
        MoodBoost,
        IncomeBoost,
        TrainingSpeedUp
    }
}
