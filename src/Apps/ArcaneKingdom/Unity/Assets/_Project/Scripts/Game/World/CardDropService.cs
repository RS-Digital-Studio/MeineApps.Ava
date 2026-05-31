#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.World;
using ArcaneKingdom.Game.Catalog;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.World
{
    /// <summary>
    /// Vergibt nach jedem Welt-Node-Sieg eine Karte als Belohnung. Die Seltenheit ist gestuft
    /// nach Node-Typ x Sternzahl (Spielplan v5 Kap. 8.2):
    ///   Normal-Node:     1-3★ Gewöhnlich/Ungewöhnlich · 4★ (Gott) Selten
    ///   MiniBoss (N5):   1-3★ Ungewöhnlich/Selten     · 4★ (Gott) Epic
    ///   WorldBoss (N10): 1-3★ Selten                  · 4★ (Gott) Legendär
    ///
    /// Gezogen wird eine zufällige NICHT-exklusive Karte (keine Götter/Premium/Event/Prestige/
    /// Sternkarten/Saison) der Ziel-Seltenheit aus dem Standard-Sammelpool. Auf einem echten
    /// Server liefe diese Vergabe in einer Cloud Function — der Client-Trigger ist optimistisch.
    /// </summary>
    public sealed class CardDropService
    {
        private readonly ISaveService<PlayerSave> _save;
        private readonly CardCatalogService _catalog;
        private readonly IAnalyticsService _analytics;
        private readonly System.Random _random = new();

        public CardDropService(ISaveService<PlayerSave> save, CardCatalogService catalog, IAnalyticsService analytics)
        {
            _save = save;
            _catalog = catalog;
            _analytics = analytics;
        }

        /// <summary>
        /// Leitet die Drop-Seltenheit aus Node-Typ + Sternzahl ab (v5 Kap. 8.2). Ab 4 Sternen
        /// (Gott-Schwierigkeit) springt die Stufe nach oben (Boss = Epic bzw. Legendär).
        /// </summary>
        public static Rarity RollDropRarity(NodeType type, int stars, System.Random rng)
        {
            var isGott = stars >= 4;
            switch (type)
            {
                case NodeType.WorldBoss:
                    return isGott ? Rarity.Legendaer : Rarity.Selten;
                case NodeType.MiniBoss:
                    return isGott
                        ? Rarity.Epic
                        : (rng.Next(2) == 0 ? Rarity.Ungewoehnlich : Rarity.Selten);
                default: // Normal
                    return isGott
                        ? Rarity.Selten
                        : (rng.Next(2) == 0 ? Rarity.Gewoehnlich : Rarity.Ungewoehnlich);
            }
        }

        /// <summary>
        /// Würfelt die Belohnungs-Karte, legt eine neue Instanz ins Inventar und liefert die
        /// Card-Definition-ID zurück (oder null, wenn kein passender Pool existiert).
        /// </summary>
        public async UniTask<string?> RollAndAwardAsync(NodeDefinition node, int stars, CancellationToken ct = default)
        {
            if (node == null) return null;
            var rarity = RollDropRarity(node.Type, stars, _random);

            var pool = _catalog.AllCards
                .Where(c => c.Rarity == rarity && !c.IsExclusive && c.Race != Race.Goetter)
                .ToList();
            if (pool.Count == 0)
            {
                GameLogger.Warning("CardDrop", $"Kein Drop-Pool für {rarity} — Belohnung entfällt.");
                return null;
            }

            var picked = pool[_random.Next(pool.Count)];
            var instId = Guid.NewGuid().ToString("N");
            await _save.MutateAsync(save =>
            {
                save.CardInventory[instId] = new CardInstance(
                    instanceId: instId,
                    cardDefinitionId: picked.Id,
                    level: 0,
                    expWithinLevel: 0,
                    obtainedAtUtc: DateTime.UtcNow);
                return save;
            }, ct);

            _analytics.Track("card_dropped", new Dictionary<string, object>
            {
                ["node_id"] = node.Id,
                ["stars"] = stars,
                ["rarity"] = rarity.ToString(),
                ["card_id"] = picked.Id
            });
            GameLogger.Info("CardDrop", $"{node.Id} @ {stars}★ → {rarity} {picked.Id}");
            return picked.Id;
        }
    }
}
