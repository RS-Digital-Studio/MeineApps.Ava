#nullable enable
using ArcaneKingdom.Domain.World;
using UnityEngine;

namespace ArcaneKingdom.Game.Catalog
{
    /// <summary>
    /// ScriptableObject-Container der alle <see cref="WorldDefinition"/>-Assets
    /// für Runtime-Lookup buendelt. Liegt unter Assets/_Project/Resources/WorldCatalog.asset
    /// und wird vom WorldCatalogSyncTool gefüllt.
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/Catalog/World Catalog",
                     fileName = "WorldCatalog")]
    public sealed class WorldCatalogAsset : ScriptableObject
    {
        [SerializeField] private WorldDefinition[] worlds = System.Array.Empty<WorldDefinition>();

        public WorldDefinition[] Worlds => worlds;

#if UNITY_EDITOR
        public void SetWorlds(WorldDefinition[] all) => worlds = all;
#endif
    }
}
