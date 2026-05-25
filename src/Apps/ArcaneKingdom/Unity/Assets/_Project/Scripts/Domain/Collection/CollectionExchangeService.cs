#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;

namespace ArcaneKingdom.Domain.Collection
{
    /// <summary>
    /// Service fuer das Eintauschen vollstaendiger Sammlungen (Spielplan v5 Kap. 5.6).
    /// Spieler sammelt Material-Karten (ATK 1 / HP 1) bis ein Set komplett ist,
    /// druckt Exchange und erhaelt die Belohnungs-Karte. Die Material-Karten werden konsumiert.
    /// </summary>
    public sealed class CollectionExchangeService
    {
        private readonly IReadOnlyDictionary<string, CardDefinition> _cardDefinitions;

        public CollectionExchangeService(IReadOnlyDictionary<string, CardDefinition> cardDefinitions)
        {
            _cardDefinitions = cardDefinitions;
        }

        /// <summary>
        /// Vorab-Pruefung: Welche Material-Card-Instances aus dem Inventar matchen das Set?
        /// Pro Required-Material-ID wird genau 1 Instance benoetigt (eindeutig pro Position).
        /// </summary>
        public CollectionExchangePreview Preview(CollectionSet set, IReadOnlyCollection<CardInstance> inventory)
        {
            if (set is null) throw new ArgumentNullException(nameof(set));
            if (inventory is null) throw new ArgumentNullException(nameof(inventory));

            // Map: defId -> InstanceId-Liste (eine Instance pro Required-Slot verbrauchen)
            var byDefId = inventory
                .GroupBy(i => i.CardDefinitionId)
                .ToDictionary(g => g.Key, g => g.Select(i => i.InstanceId).ToList());

            var instancesToConsume = new List<string>();
            var missingDefIds = new List<string>();

            foreach (var requiredDefId in set.RequiredMaterialIds)
            {
                if (byDefId.TryGetValue(requiredDefId, out var instances) && instances.Count > 0)
                {
                    instancesToConsume.Add(instances[0]);
                    instances.RemoveAt(0);
                }
                else
                {
                    missingDefIds.Add(requiredDefId);
                }
            }

            return new CollectionExchangePreview(set, instancesToConsume, missingDefIds);
        }

        /// <summary>
        /// Fuehrt den Tausch durch: validiert, verbraucht Material-Karten, gibt Reward-Karte zurueck.
        /// Mutiert das PlayerSave direkt (Inventory + Rewards).
        /// </summary>
        public Result<string> ApplyExchange(CollectionSet set, PlayerSave save)
        {
            if (set is null) return Result<string>.Failure("Set ist null");
            if (save is null) return Result<string>.Failure("Save ist null");
            if (!_cardDefinitions.ContainsKey(set.RewardCardId))
                return Result<string>.Failure($"Reward-Card '{set.RewardCardId}' nicht im Katalog.");

            var preview = Preview(set, save.CardInventory.Values);
            if (!preview.IsComplete)
                return Result<string>.Failure($"Sammlung unvollstaendig: {preview.MissingDefIds.Count} fehlende Material-Karten.");

            // Material-Instanzen entfernen
            foreach (var instId in preview.InstanceIdsToConsume)
            {
                save.CardInventory.Remove(instId);
            }

            // Belohnungs-Karte hinzufuegen
            var newInstId = Guid.NewGuid().ToString("N");
            save.CardInventory[newInstId] = new CardInstance(
                instanceId: newInstId,
                cardDefinitionId: set.RewardCardId,
                level: 0,
                expWithinLevel: 0,
                obtainedAtUtc: DateTime.UtcNow);

            // Set als gesammelt markieren (kann pro Save nur 1x ausgeloest werden)
            if (!save.ClaimedCollectionSetIds.Contains(set.Id))
                save.ClaimedCollectionSetIds.Add(set.Id);

            return Result<string>.Success(set.RewardCardId);
        }
    }

    /// <summary>
    /// Ergebnis einer Tausch-Vorschau: Welche Instances werden konsumiert, welche Materialien fehlen.
    /// </summary>
    public sealed class CollectionExchangePreview
    {
        public CollectionSet Set { get; }
        public IReadOnlyList<string> InstanceIdsToConsume { get; }
        public IReadOnlyList<string> MissingDefIds { get; }

        public CollectionExchangePreview(CollectionSet set,
                                          IReadOnlyList<string> instanceIdsToConsume,
                                          IReadOnlyList<string> missingDefIds)
        {
            Set = set;
            InstanceIdsToConsume = instanceIdsToConsume;
            MissingDefIds = missingDefIds;
        }

        public bool IsComplete => MissingDefIds.Count == 0;
    }
}
