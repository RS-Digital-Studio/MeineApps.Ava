using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// XP- und Level-Operationen.
/// </summary>
public sealed partial class GameStateService
{
    // ===================================================================
    // XP/LEVEL-OPERATIONEN
    // ===================================================================

    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        int oldLevel;
        int levelUps;
        int totalXp, currentXp, xpForNext, newLevel;

        lock (_stateLock)
        {
            // Bei Max-Level keine XP mehr addieren (verhindert int.MaxValue Overflow)
            if (_state.PlayerLevel >= LevelThresholds.MaxPlayerLevel)
                return;

            oldLevel = _state.PlayerLevel;

            // XP-Boost aus DailyReward (2x)
            if (_state.IsXpBoostActive)
                amount *= 2;

            // Prestige-Shop XP-Multiplikator
            var xpBonus = GetPrestigeXpBonus();
            if (xpBonus > 0)
                amount = (int)(amount * (1m + xpBonus));

            _state.CurrentXp += amount;
            _state.TotalXp += amount;

            levelUps = 0;
            while (_state.CurrentXp >= _state.XpForNextLevel && _state.PlayerLevel < LevelThresholds.MaxPlayerLevel)
            {
                _state.PlayerLevel++;
                levelUps++;
            }

            totalXp = _state.TotalXp;
            currentXp = _state.CurrentXp;
            xpForNext = _state.XpForNextLevel;
            newLevel = _state.PlayerLevel;
        }

        XpGained?.Invoke(this, new XpGainedEventArgs(amount, totalXp, currentXp, xpForNext));

        if (levelUps > 0)
        {
            var newlyUnlocked = new List<WorkshopType>();
            foreach (WorkshopType type in Enum.GetValues<WorkshopType>())
            {
                int unlockLevel = type.GetUnlockLevel();
                if (unlockLevel > oldLevel && unlockLevel <= newLevel)
                {
                    newlyUnlocked.Add(type);
                }
            }

            LevelUp?.Invoke(this, new LevelUpEventArgs(oldLevel, newLevel, newlyUnlocked));
        }
    }

    /// <summary>
    /// Gibt den gecachten XP-Bonus zurück (refresht bei Bedarf).
    /// </summary>
    private decimal GetPrestigeXpBonus()
    {
        RefreshPrestigeBonusCacheIfNeeded();
        return _cachedXpBonus;
    }
}
