#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.World
{
    public enum NodeType
    {
        Normal = 0,
        MiniBoss = 1,    // Node 5
        WorldBoss = 2    // Node 10
    }

    /// <summary>
    /// Einzelner Welt-Node (= ein Kampf in einer Welt).
    /// </summary>
    [Serializable]
    public sealed class NodeDefinition
    {
        [SerializeField] private string id = string.Empty;       // z.B. "world_1_node_3"
        [SerializeField] private string displayNameKey = string.Empty; // z.B. "Dimension Tür 1-3"
        [SerializeField] private int nodeIndex = 1;                // 1-10
        [SerializeField] private NodeType type = NodeType.Normal;

        [Header("Gegner-Deck (Referenzen auf CardDefinition.id)")]
        [SerializeField] private List<string> enemyDeckCardIds = new();

        [Header("Belohnungen 1-4 Sterne")]
        [SerializeField] private int goldOneStar = 50;
        [SerializeField] private int goldTwoStar = 100;
        [SerializeField] private int goldThreeStar = 200;
        [SerializeField] private int goldFourStar = 500;
        [SerializeField] private int expOneStar = 10;
        [SerializeField] private int expTwoStar = 25;
        [SerializeField] private int expThreeStar = 50;
        [SerializeField] private int expFourStar = 100;

        public string Id => id;
        public string DisplayNameKey => displayNameKey;
        public int NodeIndex => nodeIndex;
        public NodeType Type => type;
        public IReadOnlyList<string> EnemyDeckCardIds => enemyDeckCardIds;

        public int GoldReward(int stars) => stars switch { 1 => goldOneStar, 2 => goldTwoStar, 3 => goldThreeStar, 4 => goldFourStar, _ => 0 };
        public int ExpReward(int stars) => stars switch { 1 => expOneStar, 2 => expTwoStar, 3 => expThreeStar, 4 => expFourStar, _ => 0 };

        public int EnergyCost => type switch
        {
            NodeType.Normal => 1,
            NodeType.MiniBoss => 2,
            NodeType.WorldBoss => 3,
            _ => 1
        };
    }
}
