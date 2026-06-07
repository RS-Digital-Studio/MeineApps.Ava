namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Trainings-Typen für Arbeiter. 1:1-Port aus dem Avalonia-Original (Models/Enums/TrainingType.cs).
    /// Numerische Werte save-relevant.
    /// </summary>
    public enum TrainingType
    {
        /// <summary>XP → Level → +Effizienz (Standard-Training)</summary>
        Efficiency = 0,

        /// <summary>Senkt FatiguePerHour permanent (bis min 50% Reduktion)</summary>
        Endurance = 1,

        /// <summary>Senkt MoodDecayPerHour permanent (bis min 50% Reduktion)</summary>
        Morale = 2
    }
}
