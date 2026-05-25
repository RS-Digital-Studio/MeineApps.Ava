#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Utility;

namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Pure Domain-Logik fuer das Fusions-Crafting (Designplan v4 Kap. 5).
    /// Implementiert:
    ///   - Kategorie-basiertes Crafting (Typ A): N gleiche Rasse+Rarity → 1x zufaellige hoeher
    ///   - Feste Rezepte (Typ B): spezifische Karten-Kombinationen → spezifische Karte
    ///   - Sicherheitsmechanismen: Letzte-Kopie-Warnung, Favoriten-Schutz, Premium-Sperre, Deck-Sperre
    ///
    /// Reine C#-Logik ohne Unity-Abhaengigkeiten. Wird vom Application-Layer (CraftingService in Game-Assembly)
    /// orchestriert — der Application-Layer kuemmert sich um Gold-Abzug, Inventar-Mutation, Persistenz.
    /// </summary>
    public sealed class FusionService
    {
        private readonly IReadOnlyDictionary<string, CardDefinition> _cardDefinitions;
        private readonly IReadOnlyList<FusionRecipe> _recipes;
        private readonly Random _random;

        public FusionService(
            IReadOnlyDictionary<string, CardDefinition> cardDefinitions,
            IReadOnlyList<FusionRecipe> recipes,
            int? seed = null)
        {
            _cardDefinitions = cardDefinitions;
            _recipes = recipes;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        // ============================================================================
        // Validierung
        // ============================================================================

        /// <summary>
        /// Prueft, ob eine Karten-Instanz fuer Fusion verwendet werden darf.
        /// </summary>
        public Result CanUseInFusion(CardInstance instance, FusionGuard guards)
        {
            if (!_cardDefinitions.TryGetValue(instance.CardDefinitionId, out var def))
                return Result.Failure($"Karte '{instance.CardDefinitionId}' nicht im Katalog.");

            if (!def.CanBeUsedInFusion)
                return Result.Failure($"Premium-Karte '{def.Id}' kann nicht fuer Fusion verwendet werden.");

            if (guards.IsFavorited(instance.InstanceId))
                return Result.Failure($"Karte '{def.Id}' ist als Favorit geschuetzt.");

            if (guards.IsInActiveDeck(instance.InstanceId))
                return Result.Failure($"Karte '{def.Id}' ist in einem aktiven Deck — bitte erst entfernen.");

            return Result.Success();
        }

        /// <summary>
        /// Liefert true, wenn die Karte nur noch in 1 Exemplar vorhanden ist (Letzte-Kopie-Warnung).
        /// </summary>
        public bool IsLastCopy(string cardDefinitionId, IEnumerable<CardInstance> playerInventory)
            => playerInventory.Count(i => i.CardDefinitionId == cardDefinitionId) <= 1;

        // ============================================================================
        // Kategorie-Crafting (Typ A)
        // ============================================================================

        public FusionPreview PreviewCategoryFusion(IReadOnlyList<CardInstance> inputs, FusionGuard guards)
        {
            if (inputs == null || inputs.Count == 0)
                return FusionPreview.Failure("Keine Eingabe-Karten.");

            // Alle Input-Karten muessen valide sein
            foreach (var inst in inputs)
            {
                var r = CanUseInFusion(inst, guards);
                if (!r.IsSuccess) return FusionPreview.Failure(r.ErrorMessage ?? "Karte invalide");
            }

            // Alle muessen gleiche Rasse + Rarity haben
            var firstDef = _cardDefinitions[inputs[0].CardDefinitionId];
            foreach (var inst in inputs.Skip(1))
            {
                var def = _cardDefinitions[inst.CardDefinitionId];
                if (def.Race != firstDef.Race || def.Rarity != firstDef.Rarity)
                    return FusionPreview.Failure("Alle Karten muessen die gleiche Rasse und Seltenheit haben.");
            }

            // Lookup Config
            var config = CategoryFusionRules.GetConfig(firstDef.Rarity);
            if (config == null)
                return FusionPreview.Failure($"Kein Crafting-Pfad fuer Rarity '{firstDef.Rarity}'.");

            // Anzahl pruefen
            if (inputs.Count != config.RequiredSameRaceCards)
                return FusionPreview.Failure($"Erfordert genau {config.RequiredSameRaceCards} Karten (gegeben: {inputs.Count}).");

            // Ergebnis: zufaellige Karte gleicher Rasse mit Ziel-Rarity (ausser bei Mythisch — feste Rezepte)
            if (config.ResultRarity == Rarity.Mythisch)
                return FusionPreview.Failure("6* Mythisch nur ueber feste Rezepte (siehe TryApplyFixedRecipe).");

            var candidates = _cardDefinitions.Values
                .Where(d => d.Race == firstDef.Race && d.Rarity == config.ResultRarity && !d.IsExclusive)
                .ToList();
            if (candidates.Count == 0)
                return FusionPreview.Failure($"Keine Karten von Rasse '{firstDef.Race}' und Rarity '{config.ResultRarity}'.");

            return FusionPreview.SuccessCategory(
                resultPool: candidates.Select(d => d.Id).ToList(),
                goldCost: config.GoldCost,
                materialId: config.RequiresScrapId);
        }

        public Result<string> ApplyCategoryFusion(IReadOnlyList<CardInstance> inputs, FusionGuard guards)
        {
            var preview = PreviewCategoryFusion(inputs, guards);
            if (!preview.IsSuccess) return Result<string>.Failure(preview.ErrorMessage ?? "Preview fehlgeschlagen");

            // Wir pruefen NICHT Gold/Scrap hier — das ist Aufgabe des Application-Service.
            // Wir liefern lediglich eine zufaellige Karten-ID zurueck.
            var pool = preview.ResultPool!;
            var idx = _random.Next(pool.Count);
            return Result<string>.Success(pool[idx]);
        }

        // ============================================================================
        // Feste Rezepte (Typ B)
        // ============================================================================

        /// <summary>
        /// Sucht ein festes Rezept das EXAKT mit den gegebenen Input-Karten passt (alle requiredCardIds vorhanden).
        /// Materialien-/Gold-Check ist Aufgabe des Application-Service.
        /// </summary>
        public FusionRecipe? FindMatchingRecipe(IReadOnlyList<CardInstance> inputs)
        {
            if (inputs == null || inputs.Count == 0) return null;
            var inputDefIds = inputs.Select(i => i.CardDefinitionId).ToList();

            foreach (var recipe in _recipes)
            {
                if (recipe.RequiredCardIds.Count != inputDefIds.Count) continue;
                var needed = new List<string>(recipe.RequiredCardIds);
                var matched = true;
                foreach (var defId in inputDefIds)
                {
                    if (!needed.Remove(defId)) { matched = false; break; }
                }
                if (matched && needed.Count == 0) return recipe;
            }
            return null;
        }

        public Result<string> ApplyFixedRecipe(FusionRecipe recipe, IReadOnlyList<CardInstance> inputs, FusionGuard guards)
        {
            // Sicherheitscheck: alle Inputs muessen valide sein
            foreach (var inst in inputs)
            {
                var r = CanUseInFusion(inst, guards);
                if (!r.IsSuccess) return Result<string>.Failure(r.ErrorMessage ?? "Karte invalide");
            }
            // Ergebnis-Karte muss existieren
            if (!_cardDefinitions.ContainsKey(recipe.ResultCardId))
                return Result<string>.Failure($"Ergebnis-Karte '{recipe.ResultCardId}' unbekannt.");
            return Result<string>.Success(recipe.ResultCardId);
        }
    }

    /// <summary>
    /// Sicherheits-Schicht zum Schutz vor versehentlicher Fusion (Designplan v4 Kap. 5.2).
    /// </summary>
    public sealed class FusionGuard
    {
        private readonly HashSet<string> _favoritedInstanceIds;
        private readonly HashSet<string> _deckedInstanceIds;

        public FusionGuard(IEnumerable<string>? favoritedInstanceIds = null,
                            IEnumerable<string>? deckedInstanceIds = null)
        {
            _favoritedInstanceIds = new HashSet<string>(favoritedInstanceIds ?? Array.Empty<string>());
            _deckedInstanceIds    = new HashSet<string>(deckedInstanceIds ?? Array.Empty<string>());
        }

        public bool IsFavorited(string instanceId)  => _favoritedInstanceIds.Contains(instanceId);
        public bool IsInActiveDeck(string instanceId) => _deckedInstanceIds.Contains(instanceId);
    }

    /// <summary>
    /// Read-only Vorschau einer Kategorie-Fusion. Erlaubt UI/Tests den Wahrscheinlichkeits-Pool zu sehen.
    /// </summary>
    public sealed class FusionPreview
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public IReadOnlyList<string>? ResultPool { get; }
        public long GoldCost { get; }
        public string? RequiredMaterialId { get; }

        private FusionPreview(bool isSuccess, string? errorMessage,
                               IReadOnlyList<string>? resultPool, long goldCost, string? requiredMaterialId)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            ResultPool = resultPool;
            GoldCost = goldCost;
            RequiredMaterialId = requiredMaterialId;
        }

        public static FusionPreview Failure(string error)
            => new(false, error, null, 0, null);
        public static FusionPreview SuccessCategory(IReadOnlyList<string> resultPool, long goldCost, string? materialId)
            => new(true, null, resultPool, goldCost, materialId);
    }
}
