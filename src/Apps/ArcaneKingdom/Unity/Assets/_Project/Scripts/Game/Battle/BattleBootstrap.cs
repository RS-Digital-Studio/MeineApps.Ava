#nullable enable
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.World;
using ArcaneKingdom.Game.Catalog;

namespace ArcaneKingdom.Game.Battle
{
    /// <summary>
    /// Hilfs-Service der einen <see cref="BattleEngine"/> + <see cref="BattleAI"/> aus
    /// dem PlayerSave + optional einer NodeDefinition aufbaut.
    /// </summary>
    public sealed class BattleBootstrap
    {
        private readonly CardCatalogService _cardCatalog;

        public BattleBootstrap(CardCatalogService cardCatalog)
        {
            _cardCatalog = cardCatalog;
        }

        public sealed class Setup
        {
            public BattleEngine Engine { get; init; } = default!;
            public BattleAI Ai { get; init; } = default!;
            public Dictionary<string, CardDefinition> Definitions { get; init; } = default!;
            public Dictionary<string, CardInstance> Instances { get; init; } = default!;
        }

        /// <summary>
        /// Erzeugt Engine + AI für einen Battle. Spieler-Deck wird aus dem aktiven
        /// PlayerSave-Slot gezogen, Enemy-Deck aus der Node (oder zufällig wenn
        /// <paramref name="node"/> null ist — Test-Pfad).
        /// </summary>
        public Setup? Build(PlayerSave save, NodeDefinition? node, int seed = 0)
        {
            // 1. Spieler-Deck aus aktivem Slot
            if (save.Decks.Count == 0)
            {
                GameLogger.Warning("Battle", "Spieler hat keine Decks angelegt.");
                return null;
            }
            var activeSlot = System.Math.Clamp(save.ActiveDeckSlot, 0, save.Decks.Count - 1);
            var playerDeck = save.Decks[activeSlot];
            if (playerDeck.CardInstanceIds.Count == 0)
            {
                GameLogger.Warning("Battle", "Aktives Deck ist leer.");
                return null;
            }

            // 2. Card-Definitions & Instances zusammenstellen
            var definitions = new Dictionary<string, CardDefinition>();
            foreach (var def in _cardCatalog.AllCards)
                if (def != null) definitions[def.Id] = def;

            var instances = new Dictionary<string, CardInstance>(save.CardInventory);

            // 3. Enemy-Deck
            List<string> enemyDeckInstanceIds;
            if (node != null && node.EnemyDeckCardIds.Count > 0)
            {
                // Pro Karten-ID in der Node erstellen wir eine synthetische CardInstance
                // (Level 0, kein PlayerSave-Eintrag) und verwenden sie als enemy-only.
                enemyDeckInstanceIds = new List<string>();
                foreach (var cardId in node.EnemyDeckCardIds)
                {
                    var syntheticId = $"enemy_{cardId}_{enemyDeckInstanceIds.Count}";
                    enemyDeckInstanceIds.Add(syntheticId);
                    instances[syntheticId] = new CardInstance(
                        instanceId: syntheticId,
                        cardDefinitionId: cardId,
                        level: 0,
                        expWithinLevel: 0,
                        obtainedAtUtc: System.DateTime.UtcNow);
                }
            }
            else
            {
                // Test-Pfad: zufällige 10 Karten aus dem Catalog
                var rng = new System.Random(seed);
                var pool = _cardCatalog.AllCards.OrderBy(_ => rng.Next()).Take(10).ToList();
                enemyDeckInstanceIds = new List<string>();
                foreach (var def in pool)
                {
                    var syntheticId = $"enemy_{def.Id}_{enemyDeckInstanceIds.Count}";
                    enemyDeckInstanceIds.Add(syntheticId);
                    instances[syntheticId] = new CardInstance(
                        instanceId: syntheticId,
                        cardDefinitionId: def.Id,
                        level: 0,
                        expWithinLevel: 0,
                        obtainedAtUtc: System.DateTime.UtcNow);
                }
            }

            // 4. State + Engine + AI
            var state = new BattleState(seed != 0 ? seed : System.Environment.TickCount,
                                        playerHeroHp: 1000, enemyHeroHp: 1000);
            var engine = new BattleEngine(state, definitions, instances);
            engine.Setup(playerDeck.CardInstanceIds, enemyDeckInstanceIds);
            var ai = new BattleAI(definitions, instances);

            return new Setup
            {
                Engine = engine,
                Ai = ai,
                Definitions = definitions,
                Instances = instances
            };
        }
    }
}
