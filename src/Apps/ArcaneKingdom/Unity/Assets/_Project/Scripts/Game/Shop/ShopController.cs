#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Shop;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.Shop
{
    /// <summary>
    /// Shop-Logik: Pack-Kauf, Diamant-Direkt-Items, Energie-Nachkauf.
    /// IAP (Diamanten kaufen) wird per Unity-IAP ergaenzt — diese Klasse arbeitet
    /// nach erfolgtem IAP nur mit dem Inventar.
    /// </summary>
    public sealed class ShopController
    {
        private const string PacksResourcePath = "Data/packs";

        public sealed class PackPurchaseResult
        {
            public bool Success { get; init; }
            public List<Rarity> AwardedRarities { get; init; } = new();
            public bool PityTriggered { get; init; }
            public string? Error { get; init; }
        }

        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly System.Random _rng;
        private readonly Dictionary<string, int> _pityCounters = new();   // wird im Save-Schema v2 persistiert
        private readonly List<CardPackDefinition> _availablePacks = new();

        public ShopController(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
            _rng = new System.Random();
            LoadPacksFromResources();
        }

        /// <summary>Alle bekannten Card-Packs (fuer UI-Listing).</summary>
        public IReadOnlyList<CardPackDefinition> AvailablePacks => _availablePacks;

        private void LoadPacksFromResources()
        {
            var asset = Resources.Load<TextAsset>(PacksResourcePath);
            if (asset == null)
            {
                GameLogger.Warning("Shop", $"Resources/{PacksResourcePath}.json nicht gefunden.");
                return;
            }
            try
            {
                var loaded = JsonConvert.DeserializeObject<List<CardPackDefinition>>(asset.text);
                if (loaded != null) _availablePacks.AddRange(loaded);
                GameLogger.Info("Shop", $"{_availablePacks.Count} Card-Packs geladen.");
            }
            catch (Exception ex)
            {
                GameLogger.Error("Shop", "Pack-Deserialisierung fehlgeschlagen", ex);
            }
        }

        public async UniTask<PackPurchaseResult> BuyPackAsync(CardPackDefinition pack, CancellationToken ct = default)
        {
            var saveResult = await _save.LoadAsync(ct);
            if (!saveResult.IsSuccess) return new PackPurchaseResult { Success = false, Error = saveResult.ErrorMessage };

            var save = saveResult.Value!;
            if (!save.Currencies.SpendDiamond(pack.DiamondCost))
                return new PackPurchaseResult { Success = false, Error = $"Nicht genug Diamanten ({pack.DiamondCost} benoetigt)." };

            var pity = _pityCounters.TryGetValue(pack.Id, out var p) ? p : 0;
            var roll = CardPackRoller.Roll(new CardPackRoller.RollContext { Pack = pack, PityCounter = pity, Random = _rng });
            _pityCounters[pack.Id] = roll.NewPityCounter;

            // TODO MVP: Aus Rarities konkrete Karten auswaehlen (Random aus Element/Race-Verteilung)
            //          und CardInstances im Save anlegen.
            await _save.SaveAsync(save, ct);

            _analytics.Track("pack_bought", new Dictionary<string, object>
            {
                ["pack_id"] = pack.Id,
                ["pity"] = pity,
                ["pity_triggered"] = roll.PityTriggered
            });
            return new PackPurchaseResult { Success = true, AwardedRarities = roll.Rarities, PityTriggered = roll.PityTriggered };
        }

        public async UniTask<Result> BuyEnergyAsync(int energyAmount, long diamondCost, CancellationToken ct = default)
        {
            var saveResult = await _save.LoadAsync(ct);
            if (!saveResult.IsSuccess) return Result.Failure(saveResult.ErrorMessage ?? "Save load failed");
            var save = saveResult.Value!;
            if (!save.Currencies.SpendDiamond(diamondCost))
                return Result.Failure($"Nicht genug Diamanten ({diamondCost} benoetigt).");
            save.Currencies.AddEnergyBonus(energyAmount);
            await _save.SaveAsync(save, ct);
            _analytics.Track("energy_bought", new Dictionary<string, object> { ["amount"] = energyAmount, ["diamonds"] = diamondCost });
            return Result.Success();
        }
    }
}
