#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Hero;
using ArcaneKingdom.Domain.Runes;
using ArcaneKingdom.Domain.World;
using UnityEngine;

namespace ArcaneKingdom.Game.Codex
{
    /// <summary>
    /// Lese-Service fuer das in-game-Lexikon (Karten/Helden/Welten/Faehigkeiten/Runen).
    /// Daten kommen aus Resources (per DataImporter generierte ScriptableObjects).
    /// </summary>
    public sealed class CodexService
    {
        public IReadOnlyList<CardDefinition> AllCards { get; }
        public IReadOnlyList<AbilityDefinition> AllAbilities { get; }
        public IReadOnlyList<RuneDefinition> AllRunes { get; }
        public IReadOnlyList<WorldDefinition> AllWorlds { get; }
        public IReadOnlyList<HeroDefinition> AllHeroes { get; }

        public CodexService()
        {
            AllCards = Resources.LoadAll<CardDefinition>("").OrderBy(c => c.Rarity).ThenBy(c => c.Cost).ToList();
            AllAbilities = Resources.LoadAll<AbilityDefinition>("").OrderBy(a => a.Category).ThenBy(a => a.Id).ToList();
            AllRunes = Resources.LoadAll<RuneDefinition>("").OrderBy(r => r.Type).ThenBy(r => r.Rarity).ToList();
            AllWorlds = Resources.LoadAll<WorldDefinition>("").OrderBy(w => w.Index).ToList();
            AllHeroes = Resources.LoadAll<HeroDefinition>("").OrderBy(h => h.Id).ToList();
            GameLogger.Info("Codex", $"Geladen: {AllCards.Count} Karten, {AllAbilities.Count} Faehigkeiten, {AllRunes.Count} Runen, {AllWorlds.Count} Welten, {AllHeroes.Count} Helden.");
        }

        public IReadOnlyList<CardDefinition> CardsByElement(Element element) =>
            AllCards.Where(c => c.Element == element).ToList();

        public IReadOnlyList<CardDefinition> CardsByRarity(Rarity rarity) =>
            AllCards.Where(c => c.Rarity == rarity).ToList();

        public IReadOnlyList<CardDefinition> CardsByRace(ArcaneKingdom.Domain.Cards.Race race) =>
            AllCards.Where(c => c.Race == race).ToList();

        public IReadOnlyList<CardDefinition> SearchCards(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return AllCards;
            var q = query.Trim().ToLowerInvariant();
            return AllCards.Where(c =>
                c.Id.ToLowerInvariant().Contains(q) ||
                c.DisplayNameKey.ToLowerInvariant().Contains(q))
                .ToList();
        }

        public CardDefinition? FindCard(string id) =>
            AllCards.FirstOrDefault(c => c.Id == id);

        public AbilityDefinition? FindAbility(string id) =>
            AllAbilities.FirstOrDefault(a => a.Id == id);

        public WorldDefinition? FindWorld(string id) =>
            AllWorlds.FirstOrDefault(w => w.Id == id);

        public HeroDefinition? FindHero(string id) =>
            AllHeroes.FirstOrDefault(h => h.Id == id);
    }
}
