using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

public class IArSessionLikeContractTests
{
    [Fact]
    public void Mock_DefaultPose_LiefertIdentity()
    {
        var s = new MockArSession();
        var p = s.GetCameraPose();
        p.tx.Should().Be(0);
        p.qw.Should().Be(1);
    }

    [Fact]
    public void Mock_TrackingStateFalse_IsTrackingFalse()
    {
        var s = new MockArSession { TrackingState = false };
        s.IsTracking.Should().BeFalse();
    }

    [Fact]
    public void Mock_HitTestFunc_WirdAufgerufen()
    {
        var s = new MockArSession
        {
            HitTestFunc = (x, y) => new ArHitLike(x, 0, y, 1.5f, ArHitLikeQuality.Plane)
        };
        var hit = s.HitTest(2, 3);
        hit.Should().NotBeNull();
        hit!.X.Should().Be(2);
        hit.Z.Should().Be(3);
        hit.Quality.Should().Be(ArHitLikeQuality.Plane);
    }
}
