#nullable enable
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Progression;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Progression
{
    /// <summary>
    /// Vergibt EXP an den Spieler, ermittelt Level-Ups und wendet alle Schwellen-
    /// Belohnungen an. Save wird transaktional aktualisiert.
    /// </summary>
    public sealed class ProgressionService
    {
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;

        public event System.Action<ProgressionEngine.ProgressionResult>? LevelUp;

        public ProgressionService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
        }

        public async UniTask<Result<ProgressionEngine.ProgressionResult>> AwardExpAsync(long expDelta, CancellationToken ct = default)
        {
            if (expDelta <= 0) return Result<ProgressionEngine.ProgressionResult>.Failure("EXP muss > 0 sein.");

            ProgressionEngine.ProgressionResult? result = null;
            await _save.MutateAsync(save =>
            {
                result = ProgressionEngine.ApplyExp(save.Profile, expDelta);
                save.Profile.Level = result.NewLevel;
                save.Profile.ExpTotal = result.NewExpTotal;
                ApplyRewards(save, result.EarnedRewards);
                return save;
            }, ct);

            if (result == null) return Result<ProgressionEngine.ProgressionResult>.Failure("Mutation fehlgeschlagen.");

            if (result.LeveledUp)
            {
                _analytics.Track("level_up", new Dictionary<string, object>
                {
                    ["old_level"] = result.OldLevel,
                    ["new_level"] = result.NewLevel,
                    ["exp_total"] = result.NewExpTotal,
                    ["rewards_count"] = result.EarnedRewards.Count
                });
                GameLogger.Info("Progression", $"LEVEL UP {result.OldLevel} → {result.NewLevel} (+{result.EarnedRewards.Count} Belohnungen)");
                LevelUp?.Invoke(result);
            }
            return Result<ProgressionEngine.ProgressionResult>.Success(result);
        }

        private static void ApplyRewards(PlayerSave save, IReadOnlyList<LevelUpReward> rewards)
        {
            foreach (var r in rewards)
            {
                if (r.Gold > 0) save.Currencies.AddGold(r.Gold);
                if (r.Diamonds > 0) save.Currencies.AddDiamond(r.Diamonds);
                // Packs/AvatarFrames/UnlockedFeatures werden im PlayerSave-Schema v2 als
                // Inventory-Eintraege bzw. PendingClaims persistiert — fuer den ersten Wurf
                // werden sie nur geloggt.
                if (r.CommonPacks > 0 || r.RarePacks > 0 || r.EpicPacks > 0)
                    GameLogger.Info("Progression", $"  +{r.CommonPacks} Common / +{r.RarePacks} Rare / +{r.EpicPacks} Epic Packs (PendingClaim — Schema v2)");
                if (r.UnlockedFeatureKey != null)
                    GameLogger.Info("Progression", $"  Feature freigeschaltet: {r.UnlockedFeatureKey}");
                if (r.RuneSlotUnlocked.HasValue)
                    GameLogger.Info("Progression", $"  Runen-Slot {r.RuneSlotUnlocked.Value} verfuegbar");
                if (r.AvatarFrameKey != null)
                    GameLogger.Info("Progression", $"  Avatar-Rahmen: {r.AvatarFrameKey}");
            }
        }
    }
}
