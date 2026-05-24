#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Collection;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.Collection
{
    /// <summary>
    /// Verwaltet Material-Karten-Sammelsets (DESIGN.md Kap. 5.6).
    /// Sets werden aus <c>Resources/Data/collections.json</c> geladen, der Spieler-
    /// Materialien-Pool wird aus dem PlayerSave abgeleitet (Karten-Inventar mit
    /// "material.*" Praefix-Konvention).
    /// </summary>
    public sealed class CollectionService
    {
        private const string MaterialIdPrefix = "material.";

        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly List<CollectionSet> _sets = new();

        public CollectionService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
            LoadSetsFromResources();
        }

        public IReadOnlyList<CollectionSet> AllSets => _sets;

        /// <summary>Status aller Sets fuer die aktuelle Spieler-Sammlung.</summary>
        public async UniTask<List<CollectionProgress>> EvaluateAllAsync(CancellationToken ct = default)
        {
            var loadResult = await _save.LoadAsync(ct);
            if (!loadResult.IsSuccess) return new List<CollectionProgress>();
            var owned = ExtractMaterialIds(loadResult.Value!);
            return _sets.Select(s => CollectionEvaluator.Evaluate(s, owned)).ToList();
        }

        /// <summary>
        /// Tauscht ein vollstaendiges Set gegen die Belohnungs-Karte. Verbraucht die
        /// Material-Karten aus dem Inventar.
        /// </summary>
        public async UniTask<Result<string>> ExchangeAsync(string setId, CancellationToken ct = default)
        {
            var set = _sets.FirstOrDefault(s => s.Id == setId);
            if (set == null) return Result<string>.Failure($"Set '{setId}' unbekannt.");

            string? newCardId = null;
            string? error = null;

            await _save.MutateAsync(save =>
            {
                var owned = ExtractMaterialIds(save);
                var progress = CollectionEvaluator.Evaluate(set, owned);
                if (!progress.IsComplete)
                {
                    error = $"Set unvollstaendig: {progress.OwnedCount}/{progress.TotalCount}.";
                    return save;
                }

                // Material-Karten verbrauchen (eine pro requiredId)
                var toRemove = new List<string>();
                foreach (var requiredMaterial in set.RequiredMaterialIds)
                {
                    var fullId = MaterialIdPrefix + requiredMaterial;
                    var first = save.CardInventory
                        .Where(kv => kv.Value.CardDefinitionId == fullId)
                        .Select(kv => kv.Key)
                        .FirstOrDefault();
                    if (first != null) toRemove.Add(first);
                }
                foreach (var key in toRemove) save.CardInventory.Remove(key);

                // Belohnungs-Karte als CardInstance hinzufuegen
                newCardId = System.Guid.NewGuid().ToString("N");
                save.CardInventory[newCardId] = new Domain.Cards.CardInstance(
                    instanceId: newCardId,
                    cardDefinitionId: set.RewardCardId,
                    level: 0,
                    expWithinLevel: 0,
                    obtainedAtUtc: System.DateTime.UtcNow);
                return save;
            }, ct);

            if (error != null) return Result<string>.Failure(error);
            _analytics.Track("collection_exchanged", new Dictionary<string, object>
            {
                ["set_id"] = setId, ["reward_card_id"] = set.RewardCardId
            });
            GameLogger.Info("Collection", $"Set '{setId}' getauscht → Karte '{set.RewardCardId}'.");
            return Result<string>.Success(newCardId!);
        }

        private static ISet<string> ExtractMaterialIds(PlayerSave save)
        {
            var set = new HashSet<string>();
            foreach (var kv in save.CardInventory)
            {
                var defId = kv.Value.CardDefinitionId;
                if (defId.StartsWith(MaterialIdPrefix))
                    set.Add(defId.Substring(MaterialIdPrefix.Length));
            }
            return set;
        }

        private void LoadSetsFromResources()
        {
            var asset = Resources.Load<TextAsset>("Data/collections");
            if (asset == null)
            {
                GameLogger.Warning("Collection", "Resources/Data/collections.json nicht gefunden.");
                return;
            }
            try
            {
                var loaded = JsonConvert.DeserializeObject<List<CollectionSet>>(asset.text);
                if (loaded != null) _sets.AddRange(loaded);
                GameLogger.Info("Collection", $"{_sets.Count} Sammelsets geladen.");
            }
            catch (System.Exception ex)
            {
                GameLogger.Error("Collection", "Sets-Deserialisierung fehlgeschlagen", ex);
            }
        }
    }
}
