using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

public class ArPoseSamplerTests
{
    [Fact]
    public void ComputeRobustMedian_UnterDreiSamples_LiefertNull()
    {
        var s = new ArPoseSampler();
        s.Add(1f, 0f, 0f, 3);
        s.Add(1.1f, 0f, 0f, 3);
        s.ComputeRobustMedian().Should().BeNull();
    }

    [Fact]
    public void ComputeRobustMedian_EngerCluster_LiefertMittelwert()
    {
        // 5 Samples sehr nah um (1, 0, -2). Erwartet: x ~= 1, z ~= -2, kleine StdDev.
        var s = new ArPoseSampler();
        s.Add(0.99f, 0.001f, -2.01f, 3);
        s.Add(1.00f, 0.000f, -2.00f, 3);
        s.Add(1.01f, -0.001f, -2.00f, 3);
        s.Add(1.00f, 0.002f, -1.99f, 2);
        s.Add(1.00f, 0.000f, -2.01f, 3);

        var r = s.ComputeRobustMedian();
        r.Should().NotBeNull();
        r!.Value.x.Should().BeApproximately(1.00f, 0.02f);
        r.Value.z.Should().BeApproximately(-2.00f, 0.02f);
        r.Value.stdDev.Should().BeLessThan(0.05f);
        r.Value.validCount.Should().BeGreaterThan(3);
        r.Value.hitQuality.Should().Be(3); // Median der Hit-Qualities [3,3,3,2,3]
    }

    [Fact]
    public void ComputeRobustMedian_MitOutlier_OutlierWirdGefiltert()
    {
        // 10 dicht beieinander liegende Samples (Streuung ~5cm) + 1 grober Outlier
        // (1m weg). Bei einem 11-Sample-Set blaeht ein einzelner Outlier die StdDev
        // nicht stark genug auf, um selbst innerhalb 3σ zu rutschen — er wird gefiltert.
        var s = new ArPoseSampler();
        for (var i = 0; i < 10; i++)
        {
            s.Add(1.0f + i * 0.005f, 0f, -2.0f + (i % 2) * 0.005f, 3);
        }
        s.Add(2.0f, 0f, -3.0f, 1); // 1m diagonal vom Cluster weg

        var r = s.ComputeRobustMedian();
        r.Should().NotBeNull();
        // Cluster-Mittel sollte erhalten bleiben
        r!.Value.x.Should().BeApproximately(1.025f, 0.05f);
        r.Value.z.Should().BeApproximately(-2.0f, 0.05f);
        r.Value.validCount.Should().Be(10); // Outlier raus, alle Cluster-Samples drin
    }

    [Fact]
    public void ComputeRobustMedian_ZweiterPassFiltertNachgezogenenOutlier()
    {
        // 10 enge Cluster-Samples (x=1.0) + grober Outlier (x=1.5) + moderater Outlier (x=1.25).
        // Pass 1 (gegen den Median) fängt nur den groben; der moderate zieht das Mittel und wird
        // erst vom zweiten Pass (gegen das Mittel) entfernt. Ohne den zweiten Pass läge das
        // Ergebnis bei ~1.023 statt 1.0.
        var s = new ArPoseSampler();
        for (var i = 0; i < 10; i++)
            s.Add(1.0f, 0f, -2.0f, 3);
        s.Add(1.5f, 0f, -2.0f, 1);  // grober Outlier
        s.Add(1.25f, 0f, -2.0f, 1); // moderater Outlier

        var r = s.ComputeRobustMedian();
        r.Should().NotBeNull();
        r!.Value.x.Should().BeApproximately(1.0f, 0.01f); // beide Outlier raus
        r.Value.validCount.Should().Be(10);
    }

    [Fact]
    public void Clear_LeertSampleBuffer()
    {
        var s = new ArPoseSampler();
        s.Add(0f, 0f, 0f, 3);
        s.Add(1f, 0f, 0f, 3);
        s.Count.Should().Be(2);
        s.Clear();
        s.Count.Should().Be(0);
    }
}
