#nullable enable
using System;

namespace ArcaneKingdom.Domain.World
{
    /// <summary>
    /// Eine Material-Karte (Sammelset-Komponente), die ein Welt-Node mit gegebener
    /// Wahrscheinlichkeit pro Sterne-Stufe droppt. Pure-C# Logik in
    /// <see cref="MaterialDropResolver"/>.
    /// </summary>
    [Serializable]
    public sealed class MaterialDropEntry
    {
        public string MaterialId { get; init; } = string.Empty;
        public float ChanceOneStar { get; init; }
        public float ChanceTwoStar { get; init; }
        public float ChanceThreeStar { get; init; }
        public float ChanceFourStar { get; init; }
    }

    [Serializable]
    public sealed class NodeMaterialDropTable
    {
        public string NodeId { get; init; } = string.Empty;
        public MaterialDropEntry[] Drops { get; init; } = Array.Empty<MaterialDropEntry>();
    }
}
