#nullable enable
using System;

namespace ArcaneKingdom.Domain.Hero
{
    /// <summary>
    /// Verwaltet den Cooldown der Helden-Faehigkeit waehrend eines Kampfes.
    /// </summary>
    public sealed class HeroCooldown
    {
        public int TotalCooldown { get; }
        public int RemainingTurns { get; private set; }

        public HeroCooldown(int totalCooldown, int startWithCooldown = 0)
        {
            if (totalCooldown < 1) throw new ArgumentOutOfRangeException(nameof(totalCooldown));
            TotalCooldown = totalCooldown;
            RemainingTurns = Math.Clamp(startWithCooldown, 0, totalCooldown);
        }

        public bool IsReady => RemainingTurns == 0;

        public bool TryActivate()
        {
            if (!IsReady) return false;
            RemainingTurns = TotalCooldown;
            return true;
        }

        public void TickRound() => RemainingTurns = Math.Max(0, RemainingTurns - 1);
    }
}
