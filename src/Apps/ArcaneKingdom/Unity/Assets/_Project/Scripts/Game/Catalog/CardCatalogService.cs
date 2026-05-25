#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using UnityEngine;

namespace ArcaneKingdom.Game.Catalog
{
    /// <summary>
    /// Runtime-Lookup fuer Karten-Definitionen. Laedt <see cref="CardCatalogAsset"/>
    /// aus Resources beim ersten Zugriff (Lazy) und cached die ID-&gt;Definition-Map.
    /// </summary>
    public sealed class CardCatalogService
    {
        private const string CatalogResourcePath = "CardCatalog";
        private readonly Dictionary<string, CardDefinition> _byId = new();
        private bool _loaded;

        public IReadOnlyCollection<CardDefinition> AllCards
        {
            get { EnsureLoaded(); return _byId.Values; }
        }

        public CardDefinition? Find(string id)
        {
            EnsureLoaded();
            return _byId.TryGetValue(id, out var card) ? card : null;
        }

        public bool TryFind(string id, out CardDefinition card)
        {
            EnsureLoaded();
            return _byId.TryGetValue(id, out card!);
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            var asset = Resources.Load<CardCatalogAsset>(CatalogResourcePath);
            if (asset == null)
            {
                GameLogger.Warning("CardCatalog",
                    $"Resources/{CatalogResourcePath}.asset nicht gefunden — Karten-UI bleibt leer. " +
                    "Tools->Sync CardCatalog ausfuehren.");
                return;
            }

            foreach (var card in asset.Cards)
            {
                if (card == null) continue;
                if (string.IsNullOrEmpty(card.Id))
                {
                    GameLogger.Warning("CardCatalog", $"Karte ohne ID uebersprungen: {card.name}");
                    continue;
                }
                if (_byId.ContainsKey(card.Id))
                {
                    GameLogger.Warning("CardCatalog", $"Duplikat-ID '{card.Id}' uebersprungen.");
                    continue;
                }
                _byId[card.Id] = card;
            }

            GameLogger.Info("CardCatalog", $"{_byId.Count} Karten-Definitionen geladen.");
        }
    }
}
