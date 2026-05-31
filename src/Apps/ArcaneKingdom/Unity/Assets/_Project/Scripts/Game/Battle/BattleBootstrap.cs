#nullable enable
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.World;
using ArcaneKingdom.Domain.Runes;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.Game.Codex;
using ArcaneKingdom.Game.Hero;

namespace ArcaneKingdom.Game.Battle
{
    /// <summary>
    /// Hilfs-Service der einen <see cref="BattleEngine"/> + <see cref="BattleAI"/> aus
    /// dem PlayerSave + optional einer NodeDefinition aufbaut.
    /// </summary>
    public sealed class BattleBootstrap
    {
        private readonly CardCatalogService _cardCatalog;
        private readonly HeroService? _heroService;
        private readonly CodexService? _codex;

        // HeroService + CodexService sind optional, damit Test-Code BattleBootstrap weiterhin ohne
        // sie bauen kann; im DI-Build (VContainer) werden die registrierten Singletons injiziert.
        // CodexService liefert die RuneDefinitions fuer die Deck-Runen-Aggregation (K12).
        public BattleBootstrap(CardCatalogService cardCatalog, HeroService? heroService = null, CodexService? codex = null)
        {
            _cardCatalog = cardCatalog;
            _heroService = heroService;
            _codex = codex;
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
        /// <param name="difficulty">
        /// Sterne-Schwierigkeit (Spielplan v5 Kap. 8.3). Beeinflusst Gegner-Stats-Multiplier
        /// (Classic 1.0x, Amateur 1.25x, Profi 1.6x, Gott 2.2x) und aktiviert bei Gott-Stufe
        /// die Phasen-Boss-Mechanik. Default = Classic.
        /// </param>
        public Setup? Build(PlayerSave save, NodeDefinition? node, int seed = 0,
                             NodeDifficulty difficulty = NodeDifficulty.Classic)
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
            // Enemy-HP skaliert mit Difficulty (Spielplan v5 Kap. 8.3)
            var enemyMultiplier = difficulty.EnemyStatMultiplier();
            var scaledEnemyHp = (int)(1000 * enemyMultiplier);
            // Seed MUSS deterministisch sein (Replay/Anti-Cheat). Kein Environment.TickCount:
            // wenn der Aufrufer keinen Seed liefert (seed==0), aus Deck+Node stabil ableiten.
            var effectiveSeed = seed != 0 ? seed : ComputeDeterministicSeed(playerDeck.CardInstanceIds, node);
            var state = new BattleState(effectiveSeed, playerHeroHp: 1000, enemyHeroHp: scaledEnemyHp);
            // Schwierigkeits-Multiplier wird beim Einsetzen jeder Gegner-Karte angewandt (BattleEngine.PlayCard).
            state.EnemyStatMultiplier = enemyMultiplier;

            // Helden-Passiv des Spielers verdrahten (Designplan v4 Kap. 2.1): an die gewaehlte Rasse
            // gekoppelt. MUSS vor engine.Setup gesetzt werden, damit Setup den Rudelbund-Tiergeist-
            // Zaehler (BeastSpiritCountInDeck) vorberechnet. Ohne diesen Schritt blieben alle Passivs
            // (Koenigliche Aura, Goettlicher Segen, Waldlaeufer, Rudelbund, Lebensraub) im Kampf wirkungslos.
            if (_heroService != null)
            {
                var chosenRace = save.Story.ChosenRace;
                var hero = _heroService.AvailableHeroes.FirstOrDefault(h => h.Race == chosenRace);
                if (hero != null)
                    state.PlayerHeroPassiv = new HeroPassivContext(hero.FaehigkeitsTyp, hero.Magnitude);
            }

            // Deck-Runen aggregieren (Spielplan v5 Kap. 7.2, K12). MUSS vor engine.Setup gesetzt
            // werden, damit Setup die Hero-HP-/Start-Mana-Runen anwenden kann. Geteilte Logik mit
            // dem RuneScreen. Nur freigeschaltete Slots zaehlen (RuneLoadoutBuilder prueft das).
            if (_codex != null)
                state.PlayerRuneLoadout = RuneLoadoutBuilder.Build(
                    playerDeck, save,
                    runeId => _codex.FindRune(runeId),
                    cardId => _cardCatalog.Find(cardId));

            // Boss-Encounter aktivieren wenn Mini-/World-Boss-Node + Gott-Stufe
            if (node != null && difficulty.ActivatesBossPhases(node.Type))
            {
                state.IsBossEncounter = true;
                state.BossPhase2PassiveKey = "boss.phase2.allcards_atk_buff";
                // 2-3 Verstaerkungs-Karten aus Enemy-Deck-Pool (Plan-Vorgabe)
                foreach (var cardId in node.EnemyDeckCardIds.Take(3))
                    state.BossPhase2ReinforcementCardIds.Add(cardId);
            }

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

        /// <summary>
        /// Erzeugt einen stabilen, reproduzierbaren Seed aus den Deck-Instanz-IDs und der Node.
        /// Nutzt FNV-1a (NICHT string.GetHashCode, das pro Prozess randomisiert ist), damit
        /// derselbe Kampf bei Replay/Server-Nachrechnung denselben Seed ergibt.
        /// </summary>
        private static int ComputeDeterministicSeed(IReadOnlyList<string> playerDeckInstanceIds, NodeDefinition? node)
        {
            unchecked
            {
                const uint fnvOffset = 2166136261;
                const uint fnvPrime = 16777619;
                var hash = fnvOffset;
                void Mix(string? s)
                {
                    if (string.IsNullOrEmpty(s)) return;
                    foreach (var c in s!) { hash ^= c; hash *= fnvPrime; }
                    hash ^= (uint)'|'; hash *= fnvPrime;
                }
                if (node != null) Mix(node.Id);
                for (var i = 0; i < playerDeckInstanceIds.Count; i++) Mix(playerDeckInstanceIds[i]);
                // 0 ist ein gueltiger Seed, aber wir vermeiden ihn, da Aufrufer 0 als "nicht gesetzt" deuten.
                var seed = (int)hash;
                return seed != 0 ? seed : 1;
            }
        }
    }
}
