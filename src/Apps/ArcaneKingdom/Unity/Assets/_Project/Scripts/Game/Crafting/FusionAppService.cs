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
        /// Liefert true, wenn die Karte nur noch in 1 Kopie im Inventar vorhanden ist.
        /// Wird vom UI fuer die "Letzte-Kopie-Warnung" verwendet (Designplan v4 Kap. 5.2).
        /// </summary>
        public bool IsLastCopy(string cardDefinitionId, PlayerSave save)
            => GetFusionService().IsLastCopy(cardDefinitionId, save.CardInventory.Values);

        /// <summary>
        /// Fuehrt eine Kategorie-Fusion durch: validiert, prueft Gold/Scrap-Konto, mutiert PlayerSave.
        /// </summary>
        public async UniTask<Result<string>> ApplyCategoryFusionAsync(
            IReadOnlyList<string> inputInstanceIds,
            CancellationToken ct = default)
        {
            // H6: Duplikat-Input abwehren.
            if (inputInstanceIds.Distinct().Count() != inputInstanceIds.Count)
                return Result<string>.Failure("Doppelte Input-Karte angegeben.");

            var currentSave = await _save.LoadAsync(ct);
            if (!currentSave.IsSuccess || currentSave.Value == null)
                return Result<string>.Failure(currentSave.ErrorMessage ?? "Save nicht geladen");

            var save = currentSave.Value;
            var preview = PreviewCategoryFusion(inputInstanceIds, save);
            if (!preview.IsSuccess)
                return Result<string>.Failure(preview.ErrorMessage ?? "Preview fehlgeschlagen");

            var materialId = preview.RequiredMaterialId;
            var needsMaterial = !string.IsNullOrEmpty(materialId);

            // Vorab-Pruefung (UX): Gold + Material (verbindliche Pruefung erfolgt im Mutate).
            if (save.Currencies.Gold < preview.GoldCost)
                return Result<string>.Failure($"Nicht genug Gold (benoetigt: {preview.GoldCost:N0}).");
            if (needsMaterial && CountMaterial(save, materialId!) < 1)
                return Result<string>.Failure($"Benoetigt {materialId} — nicht vorhanden.");

            var inputs = ResolveInstances(inputInstanceIds, save);
            var rolled = GetFusionService().ApplyCategoryFusion(inputs, BuildGuard(save));
            if (!rolled.IsSuccess) return rolled;

            var resultCardId = rolled.Value!;

            // Mutiere PlayerSave atomar: erst re-validieren, dann konsumieren.
            string? failure = null;
            var mutationResult = await _save.MutateAsync(state =>
            {
                foreach (var instId in inputInstanceIds)
                    if (!state.CardInventory.ContainsKey(instId)) { failure = "Input-Karte nicht mehr vorhanden."; return state; }
                if (state.Currencies.Gold < preview.GoldCost) { failure = "Nicht genug Gold."; return state; }
                if (needsMaterial && CountMaterial(state, materialId!) < 1) { failure = $"Material '{materialId}' fehlt."; return state; }

                foreach (var instId in inputInstanceIds) state.CardInventory.Remove(instId);
                state.Currencies.SpendGold(preview.GoldCost);
                if (needsMaterial) ConsumeMaterial(state, materialId!, 1);

                var newInstId = Guid.NewGuid().ToString("N");
                state.CardInventory[newInstId] = new CardInstance(
                    instanceId: newInstId,
                    cardDefinitionId: resultCardId,
                    level: 0, expWithinLevel: 0,
                    obtainedAtUtc: DateTime.UtcNow);
                return state;
            }, ct);

            if (failure != null) return Result<string>.Failure(failure);
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
            // H6: Duplikat-Input abwehren (sonst wird dieselbe Karte mehrfach gezaehlt, aber nur einmal entfernt).
            if (inputInstanceIds.Distinct().Count() != inputInstanceIds.Count)
                return Result<string>.Failure("Doppelte Input-Karte angegeben.");

            var currentSave = await _save.LoadAsync(ct);
            if (!currentSave.IsSuccess || currentSave.Value == null)
                return Result<string>.Failure(currentSave.ErrorMessage ?? "Save nicht geladen");
            var save = currentSave.Value;

            var inputs = ResolveInstances(inputInstanceIds, save);
            var applyResult = GetFusionService().ApplyFixedRecipe(recipe, inputs, BuildGuard(save));
            if (!applyResult.IsSuccess) return applyResult;

            // C2: ALLE benoetigten Materialien (Gate-Materialien + Scraps) pruefen — nicht nur Gold.
            var requiredMaterials = GroupMaterials(recipe.RequiredMaterialIds);

            // Vorab-Pruefung fuer schnelles UX-Feedback (die verbindliche Pruefung erfolgt im Mutate).
            if (save.Currencies.Gold < recipe.GoldCost)
                return Result<string>.Failure($"Nicht genug Gold (benoetigt: {recipe.GoldCost:N0}).");
            foreach (var kv in requiredMaterials)
                if (CountMaterial(save, kv.Key) < kv.Value)
                    return Result<string>.Failure($"Material '{kv.Key}' fehlt (benoetigt: {kv.Value}).");

            // Mutiere PlayerSave atomar: erst ALLE Vorbedingungen re-validieren, dann konsumieren.
            string? failure = null;
            var mutationResult = await _save.MutateAsync(state =>
            {
                foreach (var instId in inputInstanceIds)
                    if (!state.CardInventory.ContainsKey(instId)) { failure = "Input-Karte nicht mehr vorhanden."; return state; }
                if (state.Currencies.Gold < recipe.GoldCost) { failure = "Nicht genug Gold."; return state; }
                foreach (var kv in requiredMaterials)
                    if (CountMaterial(state, kv.Key) < kv.Value) { failure = $"Material '{kv.Key}' fehlt."; return state; }

                // Alle Checks bestanden -> konsumieren (kann jetzt nicht mehr fehlschlagen).
                foreach (var instId in inputInstanceIds) state.CardInventory.Remove(instId);
                state.Currencies.SpendGold(recipe.GoldCost);
                foreach (var kv in requiredMaterials) ConsumeMaterial(state, kv.Key, kv.Value);

                var newInstId = Guid.NewGuid().ToString("N");
                state.CardInventory[newInstId] = new CardInstance(
                    instanceId: newInstId,
                    cardDefinitionId: recipe.ResultCardId,
                    level: 0, expWithinLevel: 0,
                    obtainedAtUtc: DateTime.UtcNow);
                return state;
            }, ct);

            if (failure != null) return Result<string>.Failure(failure);
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

        /// <summary>Gruppiert eine Material-ID-Liste zu {Material-Id : benoetigte Anzahl}.</summary>
        private static Dictionary<string, int> GroupMaterials(IReadOnlyList<string> matIds)
        {
            var dict = new Dictionary<string, int>();
            foreach (var m in matIds)
            {
                if (string.IsNullOrEmpty(m)) continue;
                dict[m] = dict.TryGetValue(m, out var c) ? c + 1 : 1;
            }
            return dict;
        }

        /// <summary>
        /// Verfuegbare Menge eines Materials. Scrap-Typen kommen aus PlayerCurrencies, alle echten
        /// Gate-Materialien (sonnenstein, mythischer_kern, ...) liegen als material.{id}-CardInstances
        /// im CardInventory (siehe MaterialDropService).
        /// </summary>
        /// <summary>Material-Id des Mythischen Kerns — wird ueber das Sternkarten-Inventar getrackt, nicht als material.*-Karte.</summary>
        private const string MythicCoreMaterialId = "mythischer_kern";

        private static int CountMaterial(PlayerSave save, string matId)
        {
            if (matId == MythicCoreMaterialId) return save.Sternkarten?.MythicCoresAvailable ?? 0;
            var scrap = ParseScrapType(matId);
            if (scrap.HasValue) return (int)Math.Min(int.MaxValue, ReadScrapBalance(save.Currencies, scrap.Value));
            var defId = "material." + matId;
            var count = 0;
            foreach (var inst in save.CardInventory.Values)
                if (inst.CardDefinitionId == defId) count++;
            return count;
        }

        /// <summary>Verbraucht <paramref name="amount"/> eines Materials (Scrap ODER material.{id}-Karten). True bei Erfolg.</summary>
        private static bool ConsumeMaterial(PlayerSave state, string matId, int amount)
        {
            if (matId == MythicCoreMaterialId)
            {
                if (state.Sternkarten == null || state.Sternkarten.MythicCoresAvailable < amount) return false;
                state.Sternkarten.MythicCoresAvailable -= amount;
                return true;
            }
            var scrap = ParseScrapType(matId);
            if (scrap.HasValue) return state.Currencies.SpendScraps(scrap.Value, amount);
            var defId = "material." + matId;
            var toRemove = new List<string>();
            foreach (var kv in state.CardInventory)
            {
                if (kv.Value.CardDefinitionId != defId) continue;
                toRemove.Add(kv.Key);
                if (toRemove.Count >= amount) break;
            }
            if (toRemove.Count < amount) return false;
            foreach (var key in toRemove) state.CardInventory.Remove(key);
            return true;
        }

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
