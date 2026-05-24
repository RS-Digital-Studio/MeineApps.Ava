#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcaneKingdom.Domain.Collection
{
    /// <summary>
    /// Sammelset (DESIGN.md Kap. 5.6) — eine Liste benoetigter Material-Karten-IDs
    /// und die Karten-ID die nach erfolgreichem Exchange vergeben wird.
    /// Materialien werden NICHT verbraucht im Sinne von "weniger als 1 → loeschen",
    /// sondern als Pool-Eintrag im Inventar markiert.
    /// </summary>
    [Serializable]
    public sealed class CollectionSet
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayNameKey { get; init; } = string.Empty;
        public string DescriptionKey { get; init; } = string.Empty;
        public List<string> RequiredMaterialIds { get; init; } = new();
        public string RewardCardId { get; init; } = string.Empty;
        public string? RewardIconAddressableKey { get; init; }
    }

    /// <summary>
    /// Status eines Sets aus Spieler-Sicht.
    /// </summary>
    public readonly struct CollectionProgress
    {
        public CollectionSet Set { get; }
        public IReadOnlyList<string> OwnedMaterialIds { get; }
        public IReadOnlyList<string> MissingMaterialIds { get; }

        public CollectionProgress(CollectionSet set, IReadOnlyList<string> owned, IReadOnlyList<string> missing)
        {
            Set = set;
            OwnedMaterialIds = owned;
            MissingMaterialIds = missing;
        }

        public bool IsComplete => MissingMaterialIds.Count == 0;
        public int OwnedCount => OwnedMaterialIds.Count;
        public int TotalCount => Set.RequiredMaterialIds.Count;
    }

    /// <summary>
    /// Pure Logik fuer Set-Status. Kein Side-Effect — die Anwendung erfolgt im CollectionService.
    /// </summary>
    public static class CollectionEvaluator
    {
        public static CollectionProgress Evaluate(CollectionSet set, ISet<string> ownedMaterialIds)
        {
            var owned = new List<string>(set.RequiredMaterialIds.Count);
            var missing = new List<string>();
            foreach (var requiredId in set.RequiredMaterialIds)
            {
                if (ownedMaterialIds.Contains(requiredId)) owned.Add(requiredId);
                else missing.Add(requiredId);
            }
            return new CollectionProgress(set, owned, missing);
        }
    }
}
