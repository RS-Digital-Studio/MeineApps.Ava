using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Partial: Bonus-PP, Meilensteine, Speedrun-Tracking, Shop-Erweiterungen.
/// </summary>
public sealed partial class PrestigeService
{
    // Gecachte Shop-Effekte (erweitert um neue Felder)
    private decimal _cachedOrderRewardBonus;
    private decimal _cachedResearchSpeedBonus;

    public event EventHandler<PrestigeMilestoneEventArgs>? MilestoneReached;

    // ═══════════════════════════════════════════════════════════════════════
    // BONUS-PP (flat, NACH Tier-Multiplikator)
    // ═══════════════════════════════════════════════════════════════════════

    public int CalculateBonusPrestigePoints(PrestigeTier tier)
    {
        var state = _gameStateService.State;
        int bonusPp = 0;

        // Perfect Ratings: +1 PP pro 10 Perfects (max +5)
        int perfectBlocks = state.PerfectRatings / 10;
        bonusPp += Math.Min(perfectBlocks * GameBalanceConstants.BonusPpPerPerfectBlock,
                            GameBalanceConstants.BonusPpPerfectRatingsCap);

        // Volle Research-Branches: +2 PP pro komplettem Branch (max +6 bei 3 Branches)
        if (state.Researches.Count > 0)
        {
            // Branch-Gruppen durchgehen
            foreach (ResearchBranch branch in Enum.GetValues<ResearchBranch>())
            {
                bool allResearched = true;
                bool anyInBranch = false;

                for (int i = 0; i < state.Researches.Count; i++)
                {
                    var r = state.Researches[i];
                    if (r.Branch != branch) continue;
                    anyInBranch = true;
                    if (!r.IsResearched)
                    {
                        allResearched = false;
                        break;
                    }
                }

                if (anyInBranch && allResearched)
                    bonusPp += GameBalanceConstants.BonusPpFullBranch;
            }
        }

        // Alle 7 Gebäude auf Level 5: +1 PP
        if (state.Buildings.Count >= 7)
        {
            bool allMax = true;
            for (int i = 0; i < state.Buildings.Count; i++)
            {
                if (state.Buildings[i].Level < 5)
                {
                    allMax = false;
                    break;
                }
            }
            if (allMax)
                bonusPp += GameBalanceConstants.BonusPpAllBuildingsMax;
        }

        // Level-Überschuss: +0.05 PP pro Level über Tier-Minimum (max +5)
        int extraLevels = Math.Max(0, state.PlayerLevel - tier.GetRequiredLevel());
        int levelBonus = Math.Min(
            (int)(extraLevels * GameBalanceConstants.BonusPpPerExtraLevel),
            GameBalanceConstants.BonusPpExtraLevelCap);
        bonusPp += levelBonus;

        return bonusPp;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MEILENSTEINE
    // ═══════════════════════════════════════════════════════════════════════

    public int CheckAndAwardMilestones()
    {
        var prestige = _gameStateService.State.Prestige;
        int totalCount = prestige.TotalPrestigeCount;
        int totalGsAwarded = 0;

        for (int i = 0; i < GameBalanceConstants.PrestigeMilestones.Length; i++)
        {
            var (requiredCount, id, gsReward) = GameBalanceConstants.PrestigeMilestones[i];

            if (totalCount >= requiredCount && !prestige.ClaimedMilestones.Contains(id))
            {
                prestige.ClaimedMilestones.Add(id);
                _gameStateService.AddGoldenScrews(gsReward, fromPurchase: false);
                totalGsAwarded += gsReward;

                MilestoneReached?.Invoke(this, new PrestigeMilestoneEventArgs
                {
                    MilestoneId = id,
                    GoldenScrewReward = gsReward,
                    RequiredPrestigeCount = requiredCount,
                });
            }
        }

        return totalGsAwarded;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPEEDRUN-TRACKING
    // ═══════════════════════════════════════════════════════════════════════

    public TimeSpan? GetCurrentRunDuration()
    {
        var runStart = _gameStateService.State.Prestige.RunStartTime;
        return runStart > DateTime.MinValue
            ? DateTime.UtcNow - runStart
            : null;
    }

    public IReadOnlyDictionary<string, long> GetBestRunTimes()
        => _gameStateService.State.Prestige.BestRunTimes;

    // ═══════════════════════════════════════════════════════════════════════
    // CHALLENGE-ABBRUCH
    // ═══════════════════════════════════════════════════════════════════════

    public bool HasActiveChallenges =>
        _gameStateService.State.Prestige.ActiveChallenges.Count > 0;

    public int AbandonChallengeRun()
    {
        var state = _gameStateService.State;
        var prestige = state.Prestige;

        if (prestige.ActiveChallenges.Count == 0)
            return 0;

        // 50% der Basis-PP (ohne Challenge-Bonus, ohne Tier-Multiplikator)
        int basePoints = GetPrestigePoints(state.CurrentRunMoney);
        int awardedPp = Math.Max(1, basePoints / 2);

        prestige.PrestigePoints += awardedPp;
        prestige.TotalPrestigePoints += awardedPp;

        // Challenges deaktivieren — Spieler spielt ohne Modifikatoren weiter
        prestige.ActiveChallenges.Clear();

        _gameStateService.MarkDirty();
        _effectCacheDirty = true;

        return awardedPp;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SHOP-ERWEITERUNGEN (neue Effekte)
    // ═══════════════════════════════════════════════════════════════════════

    public decimal GetOrderRewardBonus()
    {
        RefreshEffectCacheIfNeeded();
        return _cachedOrderRewardBonus;
    }

    public decimal GetResearchSpeedBonus()
    {
        RefreshEffectCacheIfNeeded();
        return _cachedResearchSpeedBonus;
    }
}
