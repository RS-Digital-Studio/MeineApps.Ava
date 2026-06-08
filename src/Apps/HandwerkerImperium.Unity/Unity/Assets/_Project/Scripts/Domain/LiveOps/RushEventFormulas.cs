#nullable enable

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>Zustand des Rush-Events (zeitbegrenzte 2×-Phase mit Cooldown).</summary>
    public sealed class RushEventState
    {
        public bool Active;
        public long EndsAtUtcTicks;
        public long CooldownUntilUtcTicks;
        public decimal Multiplier = 2m;
    }

    /// <summary>
    /// Rush-Event (P2 §2/§3, GDD §9.1/§10): kurze, alle Stationen umfassende 2×-Tempo/Reward-Phase,
    /// 1×/Tag gratis per Ad startbar, danach Cooldown. Reine, Unity-freie Zeit-Mathematik (UTC-Ticks).
    /// </summary>
    public static class RushEventFormulas
    {
        private const long TicksPerSecond = 10_000_000L;

        /// <summary>True, wenn gerade kein Rush läuft und der Cooldown abgelaufen ist.</summary>
        public static bool CanStart(RushEventState state, long nowUtcTicks) =>
            state != null && !IsActive(state, nowUtcTicks) && nowUtcTicks >= state.CooldownUntilUtcTicks;

        /// <summary>Startet einen Rush (Multiplikator/Laufzeit/Cooldown), wenn erlaubt. Liefert true bei Erfolg.</summary>
        public static bool Start(RushEventState state, decimal multiplier, double durationSeconds, double cooldownSeconds, long nowUtcTicks)
        {
            if (!CanStart(state, nowUtcTicks)) return false;
            if (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds <= 0) return false;
            if (double.IsNaN(cooldownSeconds) || double.IsInfinity(cooldownSeconds) || cooldownSeconds < 0) cooldownSeconds = 0;
            state.Active = true;
            state.Multiplier = multiplier < 1m ? 1m : multiplier;
            state.EndsAtUtcTicks = nowUtcTicks + (long)(durationSeconds * TicksPerSecond);
            state.CooldownUntilUtcTicks = nowUtcTicks + (long)(cooldownSeconds * TicksPerSecond);
            return true;
        }

        /// <summary>True, wenn der Rush aktiv und noch nicht abgelaufen ist.</summary>
        public static bool IsActive(RushEventState state, long nowUtcTicks) =>
            state != null && state.Active && nowUtcTicks < state.EndsAtUtcTicks;

        /// <summary>True, solange der Cooldown nach einem Rush läuft.</summary>
        public static bool IsOnCooldown(RushEventState state, long nowUtcTicks) =>
            state != null && nowUtcTicks < state.CooldownUntilUtcTicks;

        /// <summary>Beendet einen abgelaufenen Rush. Liefert true, wenn er gerade beendet wurde.</summary>
        public static bool ExpireIfDue(RushEventState state, long nowUtcTicks)
        {
            if (state == null || !state.Active || nowUtcTicks < state.EndsAtUtcTicks) return false;
            state.Active = false;
            return true;
        }

        /// <summary>Aktueller Multiplikator (Rush-Bonus, sonst 1).</summary>
        public static decimal CurrentMultiplier(RushEventState state, long nowUtcTicks) =>
            IsActive(state, nowUtcTicks) ? state!.Multiplier : 1m;
    }
}
