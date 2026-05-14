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
        int perfectBlocks = state.Statistics.PerfectRatings / 10;
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

        // Tier-skalierender Bonus-PP. Vorher war der flat-Bonus
        // (max +16) bei Legende-Runs (×9 Tier-Multi) nur +18% Uplift — unerheblich.
        // Bei Bronze (×1.2) waren die gleichen +16 noch +133% Uplift. Design bestrafte Vielspieler.
        // Loesung: Bonus-PP werden mit einem Tier-Faktor multipliziert, sodass die relative Wirkung
        // erhalten bleibt. Tier-Faktor = sqrt(TierIndex+1) — sanft ansteigend, kein Inflation-Run.
        //   None=×1.0, Bronze=×1.41, Silver=×1.73, Gold=×2.0, Platin=×2.24, Diamant=×2.45,
        //   Meister=×2.65, Legende=×2.83 → max ~45 PP Uplift bei Legende (vs. 16 vorher).
        int tierIndex = (int)tier;
        double tierFactor = Math.Sqrt(tierIndex + 1);
        bonusPp = (int)Math.Round(bonusPp * tierFactor);

        return bonusPp;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MEILENSTEINE
    // ═══════════════════════════════════════════════════════════════════════

    public int CheckAndAwardMilestones()
    {
        // State-Mutation (ClaimedMilestones, PrestigesSinceLastWeeklyReward) laeuft
        // unter dem zentralen State-Lock — race-frei gegen den Background-Serializer.
        // AddGoldenScrews (nimmt erneut den Lock + feuert Event) und das MilestoneReached-Event
        // werden gesammelt und bewusst NACH dem Lock abgearbeitet.
        var pending = new List<PrestigeMilestoneEventArgs>();
        int totalGsAwarded = 0;

        _gameStateService.ExecuteWithLock(() =>
        {
            var prestige = _gameStateService.State.Prestige;
            int totalCount = prestige.TotalPrestigeCount;

            for (int i = 0; i < GameBalanceConstants.PrestigeMilestones.Length; i++)
            {
                var (requiredCount, id, gsReward) = GameBalanceConstants.PrestigeMilestones[i];

                if (totalCount >= requiredCount && !prestige.ClaimedMilestones.Contains(id))
                {
                    prestige.ClaimedMilestones.Add(id);
                    totalGsAwarded += gsReward;

                    pending.Add(new PrestigeMilestoneEventArgs
                    {
                        MilestoneId = id,
                        GoldenScrewReward = gsReward,
                        RequiredPrestigeCount = requiredCount,
                    });
                }
            }

            // v2.0.37: Wiederholbarer Wochen-Meilenstein — alle 7 Prestiges +5 GS.
            // Der Counter wird im DoPrestige-Pfad (ApplyPrestige) hochgezaehlt;
            // diese Methode resettet ihn, wenn 7 erreicht ist.
            if (prestige.PrestigesSinceLastWeeklyReward >= 7)
            {
                const int weeklyReward = 5;
                prestige.PrestigesSinceLastWeeklyReward -= 7;
                totalGsAwarded += weeklyReward;

                pending.Add(new PrestigeMilestoneEventArgs
                {
                    MilestoneId = "pm_weekly",
                    GoldenScrewReward = weeklyReward,
                    RequiredPrestigeCount = 7,
                });
            }
        });

        // GS-Gutschrift + MilestoneReached-Event AUSSERHALB des Locks abarbeiten.
        foreach (var args in pending)
        {
            _gameStateService.AddGoldenScrews(args.GoldenScrewReward, fromPurchase: false);
            MilestoneReached?.Invoke(this, args);
        }

        return totalGsAwarded;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPEEDRUN-TRACKING
    // ═══════════════════════════════════════════════════════════════════════

    public TimeSpan? GetCurrentRunDuration()
    {
        var runStart = _gameStateService.Prestige.RunStartTime;
        return runStart > DateTime.MinValue
            ? DateTime.UtcNow - runStart
            : null;
    }

    public IReadOnlyDictionary<string, long> GetBestRunTimes()
        => _gameStateService.Prestige.BestRunTimes;

    // ═══════════════════════════════════════════════════════════════════════
    // CHALLENGE-ABBRUCH
    // ═══════════════════════════════════════════════════════════════════════

    public bool HasActiveChallenges =>
        _gameStateService.Prestige.ActiveChallenges.Count > 0;

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
