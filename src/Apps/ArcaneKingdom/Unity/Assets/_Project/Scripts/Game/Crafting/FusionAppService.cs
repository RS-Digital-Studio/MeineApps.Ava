#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Economy;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Catalog;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.Crafting
{
    /// <summary>
    /// Application-Layer-Wrapper fuer den Domain-FusionService (Designplan v4 Kap. 5).
    /// Verbindet die reine Fusions-Logik mit:
    ///   - CardCatalog (Definitionen-Lookup)
    ///   - PlayerSave-Mutation (Karten entfernen + neue hinzufuegen)
    ///   - Currency-Abzug (Gold + Scrap)
    ///   - Analytics-Tracking
    ///   - Fusion-Rezept-Laden aus Resources/Data/fusion_recipes.json
    /// </summary>
    public sealed class FusionAppService
    {
        private const string FusionRecipesResourcePath = "Data/fusion_recipes";

        private readonly CardCatalogService _catalog;
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;

        private FusionService? _fusionService;
        private IReadOnlyList<FusionRecipe>? _recipes;

        public FusionAppService(CardCatalogService catalog,
                                 ISaveService<PlayerSave> save,
                                 IAnalyticsService analytics)
        {
            _catalog = catalog;
            _save = save;
            _analytics = analytics;
        }

        private FusionService GetFusionService()
        {
            if (_fusionService != null) return _fusionService;
            var defs = _catalog.AllCards.ToDictionary(c => c.Id, c => c);
            _recipes = LoadRecipes();
            _fusionService = new FusionService(defs, _recipes);
            return _fusionService;
        }

        private IReadOnlyList<FusionRecipe> LoadRecipes()
        {
            var textAsset = Resources.Load<TextAsset>(FusionRecipesResourcePath);
            if (textAsset == null)
            {
                GameLogger.Warning("FusionAppService",
                    $"Resources/{FusionRecipesResourcePath}.json fehlt — keine festen Rezepte verfuegbar.");
                return Array.Empty<FusionRecipe>();
            }
            try
            {
                var dtos = JsonConvert.DeserializeObject<List<RecipeDto>>(textAsset.text) ?? new();
                return dtos.Select(d => new FusionRecipe(
                    id: d.id,
                    resultCardId: d.resultCardId,
                    requiredCardIds: (IReadOnlyList<string>)(d.requiredCardIds ?? new List<string>()),
                    requiredMaterialIds: (IReadOnlyList<string>)(d.requiredMaterialIds ?? new List<string>()),
                    goldCost: d.goldCost,
                    hintLocalizationKey: d.hintLocalizationKey ?? string.Empty,
                    isHidden: d.isHidden)).ToList();
            }
            catch (Exception ex)
            {
                GameLogger.Error("FusionAppService", $"Rezepte parsen fehlgeschlagen: {ex.Message}");
                return Array.Empty<FusionRecipe>();
            }
        }

        // ============================================================================
        // Public API
        // ============================================================================

        /// <summary>
        /// Vorschau der Kategorie-Fusion: zeigt Karten-Pool + Kosten ohne etwas zu mutieren.
        /// </summary>
        public FusionPreview PreviewCategoryFusion(IReadOnlyList<string> inputInstanceIds, PlayerSave save)
        {
            var guard = BuildGuard(save);
            var inputs = ResolveInstances(inputInstanceIds, save);
            return GetFusionService().PreviewCategoryFusion(inputs, guard);
        }

        /// <summary>
        /// Sucht ein passendes festes Rezept fuer die gegebenen Input-Karten.
        /// </summary>
        public FusionRecipe? FindMatchingRecipe(IReadOnlyList<string> inputInstanceIds, PlayerSave save)
        {
            var inputs = ResolveInstances(inputInstanceIds, save);
            return GetFusionService().FindMatchingRecipe(inputs);
        }

        /// <summary>
        /// Fuehrt eine Kategorie-Fusion durch: validiert, prueft Gold/Scrap-Konto, mutiert PlayerSave.
        /// </summary>
        public async UniTask<Result<string>> ApplyCategoryFusionAsync(
            IReadOnlyList<string> inputInstanceIds,
            CancellationToken ct = default)
        {
            var currentSave = await _save.LoadAsync(ct);
            if (!currentSave.IsSuccess || currentSave.Value == null)
                return Result<string>.Failure(currentSave.ErrorMessage ?? "Save nicht geladen");

            var save = currentSave.Value;
            var preview = PreviewCategoryFusion(inputInstanceIds, save);
            if (!preview.IsSuccess)
                return Result<string>.Failure(preview.ErrorMessage ?? "Preview fehlgeschlagen");

            // Gold-Konto pruefen
            if (save.Currencies.Gold < preview.GoldCost)
                return Result<string>.Failure($"Nicht genug Gold (benoetigt: {preview.GoldCost:N0}).");

            // Optional: Scrap-Konto pruefen
            if (!string.IsNullOrEmpty(preview.RequiredMaterialId))
            {
                var scrapType = ParseScrapType(preview.RequiredMaterialId);
                if (scrapType.HasValue)
                {
                    var scrapBalance = ReadScrapBalance(save.Currencies, scrapType.Value);
                    if (scrapBalance <= 0)
                        return Result<string>.Failure($"Benoetigt {preview.RequiredMaterialId} — Konto leer.");
                }
            }

            var inputs = ResolveInstances(inputInstanceIds, save);
            var rolled = GetFusionService().ApplyCategoryFusion(inputs, BuildGuard(save));
            if (!rolled.IsSuccess) return rolled;

            var resultCardId = rolled.Value!;

            // Mutiere PlayerSave: Inputs entfernen, Gold/Scrap abziehen, neue Karte hinzufuegen
            var mutationResult = await _save.MutateAsync(state =>
            {
                foreach (var instId in inputInstanceIds) state.CardInventory.Remove(instId);
                state.Currencies.SpendGold(preview.GoldCost);
                if (!string.IsNullOrEmpty(preview.RequiredMaterialId))
                {
                    var st = ParseScrapType(preview.RequiredMaterialId);
                    if (st.HasValue) state.Currencies.SpendScraps(st.Value, 1);
                }
                var newInstId = Guid.NewGuid().ToString("N");
                state.CardInventory[newInstId] = new CardInstance(
                    instanceId: newInstId,
                    cardDefinitionId: resultCardId,
                    level: 0, expWithinLevel: 0,
                    obtainedAtUtc: DateTime.UtcNow);
                return state;
            }, ct);

            if (!mutationResult.IsSuccess)
                return Result<string>.Failure(mutationResult.ErrorMessage ?? "Save-Mutation fehlgeschlagen");

            _analytics.Track("fusion_category", new Dictionary<string, object>
            {
                ["result_card"] = resultCardId,
                ["input_count"] = inputInstanceIds.Count,
                ["gold_spent"] = preview.GoldCost
            });

            return Result<string>.Success(resultCardId);
        }

        /// <summary>
        /// Fuehrt ein festes Rezept aus: validiert, prueft Gold-Konto + Materialien.
        /// </summary>
        public async UniTask<Result<string>> ApplyFixedRecipeAsync(
            FusionRecipe recipe,
            IReadOnlyList<string> inputInstanceIds,
            CancellationToken ct = default)
        {
            var currentSave = await _save.LoadAsync(ct);
            if (!currentSave.IsSuccess || currentSave.Value == null)
                return Result<string>.Failure(currentSave.ErrorMessage ?? "Save nicht geladen");
            var save = currentSave.Value;

            if (save.Currencies.Gold < recipe.GoldCost)
                return Result<string>.Failure($"Nicht genug Gold (benoetigt: {recipe.GoldCost:N0}).");

            var inputs = ResolveInstances(inputInstanceIds, save);
            var applyResult = GetFusionService().ApplyFixedRecipe(recipe, inputs, BuildGuard(save));
            if (!applyResult.IsSuccess) return applyResult;

            // Mutiere PlayerSave: Inputs entfernen, Gold abziehen, neue Karte hinzufuegen
            var mutationResult = await _save.MutateAsync(state =>
            {
                foreach (var instId in inputInstanceIds) state.CardInventory.Remove(instId);
                state.Currencies.SpendGold(recipe.GoldCost);
                // Materialien-Verbrauch (Scraps): pro Material-ID 1 Scrap-Typ wenn passt
                foreach (var matId in recipe.RequiredMaterialIds)
                {
                    var st = ParseScrapType(matId);
                    if (st.HasValue) state.Currencies.SpendScraps(st.Value, 1);
                }
                var newInstId = Guid.NewGuid().ToString("N");
                state.CardInventory[newInstId] = new CardInstance(
                    instanceId: newInstId,
                    cardDefinitionId: recipe.ResultCardId,
                    level: 0, expWithinLevel: 0,
                    obtainedAtUtc: DateTime.UtcNow);
                return state;
            }, ct);

            if (!mutationResult.IsSuccess)
                return Result<string>.Failure(mutationResult.ErrorMessage ?? "Save-Mutation fehlgeschlagen");

            _analytics.Track("fusion_recipe", new Dictionary<string, object>
            {
                ["recipe_id"] = recipe.Id,
                ["result_card"] = recipe.ResultCardId,
                ["gold_spent"] = recipe.GoldCost
            });

            return Result<string>.Success(recipe.ResultCardId);
        }

        // ============================================================================
        // Helpers
        // ============================================================================

        private FusionGuard BuildGuard(PlayerSave save)
        {
            var deckedIds = save.Decks.SelectMany(d => d.CardInstanceIds).ToHashSet();
            return new FusionGuard(
                favoritedInstanceIds: save.FavoritedCardInstanceIds,
                deckedInstanceIds: deckedIds);
        }

        private static IReadOnlyList<CardInstance> ResolveInstances(IReadOnlyList<string> ids, PlayerSave save)
        {
            var list = new List<CardInstance>(ids.Count);
            foreach (var id in ids)
            {
                if (save.CardInventory.TryGetValue(id, out var inst))
                    list.Add(inst);
            }
            return list;
        }

        private static ScrapType? ParseScrapType(string materialId) => materialId switch
        {
            "common_scrap"    => ScrapType.Common,
            "rare_scrap"      => ScrapType.Rare,
            "epic_scrap"      => ScrapType.Epic,
            "legendary_scrap" => ScrapType.Legendary,
            _                 => null
        };

        private static long ReadScrapBalance(PlayerCurrencies currencies, ScrapType type) => type switch
        {
            ScrapType.Common    => currencies.CommonScraps,
            ScrapType.Rare      => currencies.RareScraps,
            ScrapType.Epic      => currencies.EpicScraps,
            ScrapType.Legendary => currencies.LegendaryScraps,
            _                   => 0
        };

        // ============================================================================
        // DTO fuer JSON-Parsing
        // ============================================================================

        [Serializable]
        private sealed class RecipeDto
        {
            public string id = string.Empty;
            public string resultCardId = string.Empty;
            public List<string>? requiredCardIds;
            public List<string>? requiredMaterialIds;
            public long goldCost;
            public string? hintLocalizationKey;
            public bool isHidden;
        }
    }
}
