#nullable enable
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Orders;
using HandwerkerImperium.Domain.Restoration;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.LiveOps;
using HandwerkerImperium.Domain.Config;

namespace HandwerkerImperium.Domain.Runtime
{
    /// <summary>
    /// Spieler-Aktionen über dem <see cref="GameModel"/> (was der Spieler aktiv auslöst — im Gegensatz zum
    /// per-Frame-<see cref="GameSimulation.Tick"/>): Idle-Loop-Aktionen (Upgrade/Worker/Plot, delegiert an den
    /// verifizierten <see cref="GreyboxSimulation"/>) + Meta-Aktionen (Kunde bedienen, Sanierung, Perk, Meistergrad,
    /// Daily, Mastery). Reine, Unity-freie Logik; der Game-Layer ruft sie auf Klick/Trigger.
    /// </summary>
    public static class GameActions
    {
        // ── Idle-Loop (auf model.Idle) ─────────────────────────────────────
        public static bool BuyUpgrade(GameModel m, IdleBalancing idleBal, UpgradeTrack track) =>
            GreyboxSimulation.BuyUpgrade(m.Idle, idleBal, track);

        public static bool HireWorker(GameModel m, IdleBalancing idleBal, int stationIndex) =>
            GreyboxSimulation.HireWorker(m.Idle, idleBal, stationIndex);

        public static bool UnlockPlot(GameModel m, IdleBalancing idleBal, int stationIndex) =>
            GreyboxSimulation.UnlockPlot(m.Idle, idleBal, stationIndex);

        // ── Kunden-Queue ───────────────────────────────────────────────────
        /// <summary>Bedient EINEN Kunden an der Station (Verkaufswert + Eil-Bonus -> Geld, Volumen +1). Liefert den Erlös.</summary>
        public static decimal ServeCustomer(GameModel m, IdleBalancing idleBal, int stationIndex, long nowUtcTicks)
        {
            if (OrderQueueFormulas.Serve(m.Orders, 1) <= 0) return 0m;
            decimal baseEarn = GreyboxSimulation.SellCarried(m.Idle, idleBal, stationIndex, 1); // base bereits in Money
            decimal mult = OrderQueueFormulas.CurrentRewardMultiplier(m.Orders, nowUtcTicks);
            if (mult > 1m)
            {
                decimal bonus = baseEarn * (mult - 1m);
                m.Idle.Money += bonus;
                return baseEarn + bonus;
            }
            return baseEarn;
        }

        // ── Sanierung ──────────────────────────────────────────────────────
        /// <summary>Investiert Geld in ein Wahrzeichen (schaltet Bauphasen frei). Liefert abgeschlossene Phasen.</summary>
        public static int InvestRestoration(GameModel m, GameBalancing bal, int landmarkIndex, decimal amount)
        {
            if (landmarkIndex < 0 || landmarkIndex >= m.Landmarks.Count || amount <= 0m || m.Idle.Money < amount) return 0;
            m.Idle.Money -= amount;
            return RestorationFormulas.Invest(m.Landmarks[landmarkIndex], amount, bal.Restoration.PhaseBaseCost, bal.Restoration.PhaseGrowth);
        }

        // ── Imperium-Marken-Perkboard ──────────────────────────────────────
        public static bool BuyPerk(GameModel m, GameBalancing bal, PerkKind kind)
        {
            int idx = (int)kind;
            EnsurePerkSlots(m, idx);
            int level = m.PerkLevels[idx];
            if (!PerkboardFormulas.CanBuy(m.Meta.AvailableMarks, level, bal.Perkboard.DefaultMaxLevel, bal.Perkboard.MarkCostBase, bal.Perkboard.MarkCostGrowth))
                return false;
            m.Meta.AvailableMarks -= PerkboardFormulas.MarkCost(level, bal.Perkboard.MarkCostBase, bal.Perkboard.MarkCostGrowth);
            m.PerkLevels[idx] = level + 1;
            return true;
        }

        // ── Endgame-Meistergrad ────────────────────────────────────────────
        public static bool BuyMeistergrad(GameModel m, GameBalancing bal) =>
            MetaProgression.TryBuyMeistergrad(m.Meta, bal.Meistergrad.RenommeeBaseCost, bal.Meistergrad.Growth);

        // ── Tagesbelohnung ─────────────────────────────────────────────────
        /// <summary>Holt die Tagesbelohnung ab (einkommens-skaliert), wenn fällig. Liefert den Geld-Betrag.</summary>
        public static decimal ClaimDaily(GameModel m, GameBalancing bal, decimal baseMoney, decimal netIncomePerSecond, long nowUtcTicks)
        {
            var c = DailyRewardFormulas.Evaluate(m.DailyStreakDay, m.DailyLastClaimUtcTicks, nowUtcTicks, bal.Daily.LadderLength);
            if (!c.CanClaim) return 0m;
            decimal money = DailyRewardFormulas.GetScaledMoney(baseMoney, c.Day, netIncomePerSecond);
            m.Idle.Money += money;
            m.DailyStreakDay = c.Day;
            m.DailyLastClaimUtcTicks = nowUtcTicks;
            return money;
        }

        // ── Meisterschaft ──────────────────────────────────────────────────
        public static int GainMastery(GameModel m, GameBalancing bal, double xp) =>
            MetaProgression.GainMasteryXp(m.Meta, xp, bal.Mastery.BaseXp, bal.Mastery.Growth);

        // ── Rush-Event (alle Stationen kurz 2×) ────────────────────────────
        /// <summary>Startet das Rush-Event (per Ad), wenn nicht aktiv und Cooldown abgelaufen. Liefert true bei Erfolg.</summary>
        public static bool StartRush(GameModel m, GameBalancing bal, long nowUtcTicks) =>
            RushEventFormulas.Start(m.Rush, bal.Rush.Multiplier, bal.Rush.DurationSeconds, bal.Rush.CooldownSeconds, nowUtcTicks);

        private static void EnsurePerkSlots(GameModel m, int index)
        {
            while (m.PerkLevels.Count <= index)
                m.PerkLevels.Add(0);
        }
    }
}
