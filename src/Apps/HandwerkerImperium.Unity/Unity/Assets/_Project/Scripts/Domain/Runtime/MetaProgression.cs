#nullable enable
using System.Collections.Generic;
using HandwerkerImperium.Domain.Progression;

namespace HandwerkerImperium.Domain.Runtime
{
    /// <summary>
    /// Orchestriert die Meta-Progression über den reinen Formeln (PROGRESSION_BALANCING §2-§5): Prestige als
    /// Akt-Finale (Reset akt-intern, Persist permanent + Multiplikator + Marken), Meisterschafts-XP-Gewinn,
    /// Endgame-Meistergrad-Kauf. Reine, Unity-freie Zustands-Übergänge — der Game-Layer ruft sie auf Loop-Events.
    /// </summary>
    public static class MetaProgression
    {
        /// <summary>
        /// Führt ein Prestige aus, wenn erlaubt (5★ + Limit nicht erreicht): erhöht Prestige-Zahl/Stadt, setzt den
        /// kumulativen Multiplikator, schreibt PP-Marken + Perkboard-Marken gut und <b>resettet die akt-internen</b>
        /// Werte (Stern/Aufträge/Sanierung). Stations-/Geld-/Worker-Reset des Idle-Loops macht der Game-Layer.
        /// Liefert true bei Erfolg.
        /// </summary>
        public static bool TryPrestige(MetaState s, decimal currentRunMoney,
            IReadOnlyList<decimal> stageMultipliers, int marksPerPrestige, int maxPrestige)
        {
            if (s == null || !PrestigeFormulas.CanPrestige(s.CurrentStar, s.PrestigeCount, maxPrestige))
                return false;

            int pp = PrestigeFormulas.PrestigePoints(currentRunMoney);
            s.PrestigeCount += 1;
            s.CityIndex = s.PrestigeCount <= maxPrestige ? s.PrestigeCount : maxPrestige; // 0=Hansstadt … 3=Metropole
            s.PrestigeMultiplier = PrestigeFormulas.CityMultiplier(s.PrestigeCount, stageMultipliers);
            s.PrestigeCurrency += pp;
            s.AvailableMarks += PerkboardFormulas.MarksFromPrestige(1, marksPerPrestige);

            // Akt-intern zurücksetzen (PROGRESSION §3) — Permanent bleibt unangetastet.
            s.CurrentStar = 1;
            s.OrdersServed = 0;
            s.RestorationPhases = 0;
            return true;
        }

        /// <summary>
        /// Schreibt Meisterschafts-XP gut und hebt das Level entsprechend der Gesamt-XP an.
        /// Liefert die Anzahl gewonnener Level.
        /// </summary>
        public static int GainMasteryXp(MetaState s, double xp, double baseXp, double growth)
        {
            if (s == null || xp <= 0) return 0;
            s.MasteryXp += xp;
            int newLevel = MasteryFormulas.LevelForTotalXp(s.MasteryXp, baseXp, growth);
            if (newLevel <= s.MasteryLevel) return 0;
            int gained = newLevel - s.MasteryLevel;
            s.MasteryLevel = newLevel;
            return gained;
        }

        /// <summary>
        /// Kauft (in der Endstadt) den nächsten Meistergrad, wenn genug Renommee da ist. Liefert true bei Erfolg.
        /// </summary>
        public static bool TryBuyMeistergrad(MetaState s, decimal renommeeBaseCost, double growth)
        {
            if (s == null || !MeistergradFormulas.CanPurchase(s.Renommee, s.MeistergradGrade, renommeeBaseCost, growth))
                return false;
            s.Renommee -= MeistergradFormulas.RenommeeCost(s.MeistergradGrade, renommeeBaseCost, growth);
            s.MeistergradGrade += 1;
            return true;
        }
    }
}
