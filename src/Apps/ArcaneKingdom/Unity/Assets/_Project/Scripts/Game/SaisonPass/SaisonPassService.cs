#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.SaisonPass;
using ArcaneKingdom.Domain.Save;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.SaisonPass
{
    /// <summary>
    /// Verwaltet den aktuellen Saison-Pass: XP vergeben, Tier-Up erkennen, Belohnungen
    /// in <c>PendingClaims</c> schreiben. Aktive Saison wird aus
    /// <c>Resources/Data/saison_pass.json</c> geladen — Production-Wechsel via Remote Config.
    /// </summary>
    public sealed class SaisonPassService
    {
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private SaisonPassDefinition? _activeSaison;

        public SaisonPassService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
            LoadActiveSaisonFromResources();
        }

        public SaisonPassDefinition? ActiveSaison => _activeSaison;
        public bool IsActive => _activeSaison != null && DateTime.UtcNow >= _activeSaison.StartedAtUtc && DateTime.UtcNow < _activeSaison.EndsAtUtc;

        public int CurrentTier(PlayerSave save) =>
            _activeSaison == null ? 0 : SaisonPassEngine.TierForXp(GetXp(save), _activeSaison);

        public int XpToNextTier(PlayerSave save) =>
            _activeSaison == null ? 0 : SaisonPassEngine.XpRemainingToNextTier(GetXp(save), _activeSaison);

        /// <summary>
        /// Vergibt XP für beliebige Trigger-Events (Quest abgeschlossen, Arena-Sieg, ...).
        /// </summary>
        public async UniTask AwardXpAsync(int xp, string sourceKey, bool premiumActive, CancellationToken ct = default)
        {
            if (xp <= 0 || _activeSaison == null) return;
            var beforeTier = 0;
            var afterTier = 0;
            IReadOnlyList<SaisonPassTierReward> earned = Array.Empty<SaisonPassTierReward>();

            await _save.MutateAsync(save =>
            {
                var current = GetXp(save);
                beforeTier = SaisonPassEngine.TierForXp(current, _activeSaison);
                var newXp = Math.Min(current + xp, _activeSaison.HardCapTier * _activeSaison.XpPerTier);
                save.SaisonPassXp[_activeSaison.Id] = newXp;
                afterTier = SaisonPassEngine.TierForXp(newXp, _activeSaison);
                earned = SaisonPassEngine.RewardsForTierRange(_activeSaison, beforeTier, afterTier, premiumActive);
                foreach (var r in earned) save.PendingClaims.Add(MakeClaim(r, _activeSaison.Id));
                return save;
            }, ct);

            if (afterTier > beforeTier)
            {
                _analytics.Track("saison_pass_tier_up", new Dictionary<string, object>
                {
                    ["from_tier"] = beforeTier,
                    ["to_tier"] = afterTier,
                    ["claims_added"] = earned.Count,
                    ["premium"] = premiumActive,
                    ["source"] = sourceKey
                });
                GameLogger.Info("SaisonPass", $"Tier-Up {beforeTier} → {afterTier} ({earned.Count} Claims).");
            }
        }

        private int GetXp(PlayerSave save) =>
            _activeSaison != null && save.SaisonPassXp.TryGetValue(_activeSaison.Id, out var v) ? v : 0;

        private static PendingClaim MakeClaim(SaisonPassTierReward r, string seasonId) => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = r.RewardKind switch
            {
                "Currency" => PendingClaimKind.Currency,
                "Scrap" => PendingClaimKind.Scrap,
                "Card" => PendingClaimKind.Card,
                _ => PendingClaimKind.Currency
            },
            SubType = r.SubType,
            Amount = r.Amount,
            SourceKey = $"saison_pass.{seasonId}.tier{r.Tier}",
            CreatedAtUtc = DateTime.UtcNow
        };

        private void LoadActiveSaisonFromResources()
        {
            var asset = Resources.Load<TextAsset>("Data/saison_pass");
            if (asset == null) { GameLogger.Warning("SaisonPass", "saison_pass.json fehlt."); return; }
            try
            {
                _activeSaison = JsonConvert.DeserializeObject<SaisonPassDefinition>(asset.text);
                if (_activeSaison != null) GameLogger.Info("SaisonPass", $"Aktive Saison: {_activeSaison.Id} (Tier-Cap {_activeSaison.HardCapTier})");
            }
            catch (Exception ex) { GameLogger.Error("SaisonPass", "Load fehlgeschlagen", ex); }
        }
    }
}
