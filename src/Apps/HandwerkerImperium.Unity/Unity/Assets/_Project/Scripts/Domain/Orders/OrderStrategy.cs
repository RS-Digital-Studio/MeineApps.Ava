namespace HandwerkerImperium.Domain.Orders
{
    /// <summary>
    /// Strategie, mit der ein Auftrag angegangen wird (Risk/Reward).
    /// Orthogonal zur Auftrags-Schwierigkeit — Difficulty ist fest, Strategy wählt der Spieler vor dem MiniGame.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/OrderStrategy.cs). Reine Spiellogik —
    /// Lokalisierungs-Keys leben in der Unity-UI-Schicht. Numerische Werte save-relevant.
    /// </summary>
    public enum OrderStrategy
    {
        /// <summary>Sicher: Leichter, weniger Reward.</summary>
        Safe = 0,

        /// <summary>Standard: Ausgewogen (Baseline).</summary>
        Standard = 1,

        /// <summary>Risiko: Knallhart, viel Reward — Miss = 0 Reward + Reputation-Penalty.</summary>
        Risk = 2
    }

    /// <summary>
    /// Extension-Methoden für <see cref="OrderStrategy"/> — Multiplikatoren für Reward,
    /// MiniGame-Parameter und Penalty-Werte bei Miss.
    /// </summary>
    public static class OrderStrategyExtensions
    {
        /// <summary>Reward-Multiplikator. Wirkt multiplikativ auf <c>BaseReward * Difficulty * OrderType</c>.</summary>
        public static decimal GetRewardMultiplier(this OrderStrategy strategy) => strategy switch
        {
            OrderStrategy.Safe => 0.75m,        // 25% weniger
            OrderStrategy.Standard => 1.0m,
            OrderStrategy.Risk => 2.0m,          // verdoppelt — bei Perfect
            _ => 1.0m
        };

        /// <summary>XP-Multiplikator analog zum Reward-Multiplikator.</summary>
        public static decimal GetXpMultiplier(this OrderStrategy strategy) => strategy switch
        {
            OrderStrategy.Safe => 0.75m,
            OrderStrategy.Standard => 1.0m,
            OrderStrategy.Risk => 1.75m,
            _ => 1.0m
        };

        /// <summary>
        /// Multiplikator auf die "Perfect/Good/OK"-Zonen im MiniGame.
        /// &gt;1.0 = Zonen werden breiter (leichter), &lt;1.0 = schmaler (schwerer).
        /// </summary>
        public static double GetToleranceMultiplier(this OrderStrategy strategy) => strategy switch
        {
            OrderStrategy.Safe => 1.5,          // +50% breiter (deutlich leichter)
            OrderStrategy.Standard => 1.0,
            OrderStrategy.Risk => 0.5,           // -50% schmaler (sehr schwer)
            _ => 1.0
        };

        /// <summary>
        /// Multiplikator auf die MiniGame-Geschwindigkeit (Marker, Timer, Fall-Rate).
        /// &gt;1.0 = schneller (schwerer), &lt;1.0 = langsamer (leichter).
        /// </summary>
        public static double GetSpeedMultiplier(this OrderStrategy strategy) => strategy switch
        {
            OrderStrategy.Safe => 0.7,
            OrderStrategy.Standard => 1.0,
            OrderStrategy.Risk => 1.3,
            _ => 1.0
        };

        /// <summary>
        /// Zeitmultiplikator für MiniGames mit festem Timer (Pipe-Puzzle, Wiring, Invent, etc.).
        /// &gt;1.0 = mehr Zeit, &lt;1.0 = weniger Zeit.
        /// </summary>
        public static double GetTimeMultiplier(this OrderStrategy strategy) => strategy switch
        {
            OrderStrategy.Safe => 1.3,           // +30% mehr Zeit
            OrderStrategy.Standard => 1.0,
            OrderStrategy.Risk => 0.7,           // -30% Zeit
            _ => 1.0
        };

        /// <summary>
        /// Ob bei dieser Strategie ein Miss den Auftrag komplett scheitern lässt.
        /// Nur <see cref="OrderStrategy.Risk"/> hat diese harte Regel.
        /// </summary>
        public static bool HasHardFail(this OrderStrategy strategy) => strategy == OrderStrategy.Risk;

        /// <summary>
        /// Reputations-Malus bei einem Miss (nur bei Risk aktiv).
        /// Wird als negativer Reputation-Wert angewendet (z.B. -10 bedeutet Reputation sinkt um 10).
        /// </summary>
        public static int GetReputationPenaltyOnMiss(this OrderStrategy strategy) => strategy switch
        {
            OrderStrategy.Risk => -10,           // Spürbar, aber nicht ruinös
            _ => 0
        };
    }
}
