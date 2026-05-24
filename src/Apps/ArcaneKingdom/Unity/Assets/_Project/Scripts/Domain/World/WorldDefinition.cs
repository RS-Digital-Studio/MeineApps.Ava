#nullable enable
using System.Collections.Generic;
using UnityEngine;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.World
{
    /// <summary>
    /// Welt-Definition als ScriptableObject. Jede Welt hat 10 Nodes (DESIGN.md 8.2).
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/World/World", fileName = "World_")]
    public sealed class WorldDefinition : ScriptableObject
    {
        [Header("Identitaet")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayNameKey = string.Empty;
        [SerializeField] private int index = 1;                 // 1-9

        [Header("Thema")]
        [SerializeField] private Element themeElement = Element.Natur;
        [SerializeField, Min(1)] private int recommendedPlayerLevel = 1;
        [SerializeField] private string backgroundAddressableKey = string.Empty;
        [SerializeField] private string musicAddressableKey = string.Empty;

        [Header("Nodes")]
        [SerializeField] private List<NodeDefinition> nodes = new(10);

        public string Id => id;
        public string DisplayNameKey => displayNameKey;
        public int Index => index;
        public Element ThemeElement => themeElement;
        public int RecommendedPlayerLevel => recommendedPlayerLevel;
        public string BackgroundAddressableKey => backgroundAddressableKey;
        public string MusicAddressableKey => musicAddressableKey;
        public IReadOnlyList<NodeDefinition> Nodes => nodes;
    }
}
