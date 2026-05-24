#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.World
{
    /// <summary>
    /// Pure-C#-Drop-Roller. Deterministisch mit gegebenem <see cref="Random"/>.
    /// </summary>
    public static class MaterialDropResolver
    {
        public static IReadOnlyList<string> RollDrops(NodeMaterialDropTable table, int stars, Random rng)
        {
            if (stars < 1 || stars > 4) throw new ArgumentOutOfRangeException(nameof(stars), "Stars must be 1..4.");
            var result = new List<string>();
            foreach (var drop in table.Drops)
            {
                var chance = stars switch
                {
                    1 => drop.ChanceOneStar,
                    2 => drop.ChanceTwoStar,
                    3 => drop.ChanceThreeStar,
                    4 => drop.ChanceFourStar,
                    _ => 0f
                };
                if (chance <= 0f) continue;
                if (rng.NextDouble() <= chance) result.Add(drop.MaterialId);
            }
            return result;
        }
    }
}
