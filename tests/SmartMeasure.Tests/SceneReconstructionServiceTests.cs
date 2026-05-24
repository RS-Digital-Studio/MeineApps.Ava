using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

public class SceneReconstructionServiceTests
{
    [Fact]
    public void VoxelFilter_LeereEingabe_LiefertLeer()
    {
        var r = SceneReconstructionService.VoxelFilter([], 0.02);
        r.Should().BeEmpty();
    }

    [Fact]
    public void VoxelFilter_NegativeVoxelGroesse_LiefertLeer()
    {
        var r = SceneReconstructionService.VoxelFilter(
            new[] { (0.0, 0.0, 0.0) }, -1);
        r.Should().BeEmpty();
    }

    [Fact]
    public void VoxelFilter_EinPunkt_LiefertEinPunkt()
    {
        var r = SceneReconstructionService.VoxelFilter(
            new[] { (1.0, 2.0, 3.0) }, 0.02);
        r.Should().HaveCount(1);
        r[0].Should().Be((1.0, 2.0, 3.0));
    }

    [Fact]
    public void VoxelFilter_PunkteInGleichemBucket_WerdenZuMittelpunktVereint()
    {
        // 4 Punkte alle in 2cm-Voxel um (0,0,0) → 1 Mittelpunkt
        var input = new[]
        {
            (0.001, 0.001, 0.001),
            (0.005, 0.005, 0.005),
            (0.010, 0.010, 0.010),
            (0.015, 0.015, 0.015),
        };
        var r = SceneReconstructionService.VoxelFilter(input, 0.02);
        r.Should().HaveCount(1);
        r[0].x.Should().BeApproximately(0.00775, 0.0001);
    }

    [Fact]
    public void VoxelFilter_PunkteInDifferentBuckets_WerdenSeparatGehalten()
    {
        // 3 Punkte in 3 verschiedenen 2cm-Buckets (Abstand > 2cm)
        var input = new[]
        {
            (0.0, 0.0, 0.0),
            (0.05, 0.0, 0.0),
            (0.10, 0.0, 0.0),
        };
        var r = SceneReconstructionService.VoxelFilter(input, 0.02);
        r.Should().HaveCount(3);
    }

    [Fact]
    public void VoxelFilter_HoheDatenmenge_ReduziertSignifikant()
    {
        // 1000 Punkte in einem 1m-Wuerfel mit 5cm-Voxel — sollte deutlich reduzieren
        var rnd = new Random(42);
        var input = new List<(double, double, double)>(1000);
        for (var i = 0; i < 1000; i++)
            input.Add((rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble()));

        var r = SceneReconstructionService.VoxelFilter(input, 0.05);
        r.Count.Should().BeLessThan(input.Count);
        // Worst-case: ~20^3 = 8000 Voxel im 1m-Wuerfel, aber bei 1000 Punkten max 1000.
        r.Count.Should().BeLessThanOrEqualTo(input.Count);
    }
}
