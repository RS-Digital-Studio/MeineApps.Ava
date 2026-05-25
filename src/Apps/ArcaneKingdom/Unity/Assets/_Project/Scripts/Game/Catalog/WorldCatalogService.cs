#nullable enable
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.World;
using UnityEngine;

namespace ArcaneKingdom.Game.Catalog
{
    /// <summary>
    /// Runtime-Lookup für Welt-Definitionen. Lazy-Load aus Resources/WorldCatalog.asset.
    /// </summary>
    public sealed class WorldCatalogService
    {
        private const string CatalogResourcePath = "WorldCatalog";

        private List<WorldDefinition> _worlds = new();
        private Dictionary<string, WorldDefinition> _byId = new();
        private bool _loaded;

        public IReadOnlyList<WorldDefinition> AllWorlds
        {
            get { EnsureLoaded(); return _worlds; }
        }

        public WorldDefinition? Find(string id)
        {
            EnsureLoaded();
            return _byId.TryGetValue(id, out var w) ? w : null;
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            var asset = Resources.Load<WorldCatalogAsset>(CatalogResourcePath);
            if (asset == null)
            {
                GameLogger.Warning("WorldCatalog",
                    "Resources/WorldCatalog.asset nicht gefunden — World-Map bleibt leer. " +
                    "Tools->Sync WorldCatalog ausfuehren.");
                return;
            }

            _worlds = asset.Worlds.Where(w => w != null).OrderBy(w => w.Index).ToList();
            foreach (var w in _worlds)
            {
                if (string.IsNullOrEmpty(w.Id)) continue;
                _byId[w.Id] = w;
            }

            GameLogger.Info("WorldCatalog", $"{_worlds.Count} Welten geladen.");
        }
    }
}
