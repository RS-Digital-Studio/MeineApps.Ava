namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Worker-Persönlichkeiten mit Gameplay-Effekt. Jede gibt einen einzigartigen Bonus + Nachteil.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/WorkerPersonality.cs). Reine Spiellogik —
    /// Icon/Lokalisierungs-Methoden leben in der Unity-UI-Schicht. Numerische Werte save-relevant.
    /// </summary>
    public enum WorkerPersonality
    {
        /// <summary>Ausgeglichen, keine Spezial-Boni/-Mali</summary>
        Steady = 0,

        /// <summary>+20% Effizienz, Stimmung sinkt 50% schneller</summary>
        Perfectionist = 1,

        /// <summary>Stimmung sinkt 50% langsamer, -10% Effizienz</summary>
        Cheerful = 2,

        /// <summary>+25% XP-Gewinn, Müdigkeit 25% schneller</summary>
        Ambitious = 3,

        /// <summary>Müdigkeit 30% langsamer, -15% Effizienz</summary>
        Relaxed = 4,

        /// <summary>+15% Spezialisierungs-Bonus, geringerer Stimmungseinfluss bei Transfers</summary>
        Specialist = 5
    }

    /// <summary>
    /// Extension-Methoden für <see cref="WorkerPersonality"/> (reine Spiellogik-Multiplikatoren).
    /// </summary>
    public static class WorkerPersonalityExtensions
    {
        /// <summary>Effizienz-Multiplikator aus der Persönlichkeit.</summary>
        public static decimal GetEfficiencyMultiplier(this WorkerPersonality personality) => personality switch
        {
            WorkerPersonality.Steady => 1.0m,
            WorkerPersonality.Perfectionist => 1.20m,
            WorkerPersonality.Cheerful => 0.90m,
            WorkerPersonality.Ambitious => 1.0m,
            WorkerPersonality.Relaxed => 0.85m,
            WorkerPersonality.Specialist => 1.0m,
            _ => 1.0m
        };

        /// <summary>Stimmungs-Verfall-Multiplikator (höher = schnellerer Verfall).</summary>
        public static decimal GetMoodDecayMultiplier(this WorkerPersonality personality) => personality switch
        {
            WorkerPersonality.Steady => 1.0m,
            WorkerPersonality.Perfectionist => 1.5m,
            WorkerPersonality.Cheerful => 0.5m,
            WorkerPersonality.Ambitious => 1.0m,
            WorkerPersonality.Relaxed => 1.0m,
            WorkerPersonality.Specialist => 1.0m,
            _ => 1.0m
        };

        /// <summary>Müdigkeits-Multiplikator (höher = ermüdet schneller).</summary>
        public static decimal GetFatigueMultiplier(this WorkerPersonality personality) => personality switch
        {
            WorkerPersonality.Steady => 1.0m,
            WorkerPersonality.Perfectionist => 1.0m,
            WorkerPersonality.Cheerful => 1.0m,
            WorkerPersonality.Ambitious => 1.25m,
            WorkerPersonality.Relaxed => 0.70m,
            WorkerPersonality.Specialist => 1.0m,
            _ => 1.0m
        };

        /// <summary>XP-Gewinn-Multiplikator.</summary>
        public static decimal GetXpMultiplier(this WorkerPersonality personality) => personality switch
        {
            WorkerPersonality.Ambitious => 1.25m,
            _ => 1.0m
        };

        /// <summary>Spezialisierungs-Bonus-Multiplikator (zusätzlich zu Basis +15%).</summary>
        public static decimal GetSpecializationBonus(this WorkerPersonality personality) => personality switch
        {
            WorkerPersonality.Specialist => 0.15m, // Gesamt +30% mit Basis +15%
            _ => 0.0m
        };
    }
}
