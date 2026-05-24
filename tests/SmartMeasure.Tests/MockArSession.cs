using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

/// <summary>Plan-Kap. 7 Stufe 3: Deterministische IArSessionLike-Impl fuer Unit-Tests.
/// Tests koennen Pose/Tracking/HitTest setzen und das Verhalten konsumierender
/// Logik isoliert verifizieren.</summary>
public sealed class MockArSession : IArSessionLike
{
    public (float tx, float ty, float tz, float qx, float qy, float qz, float qw) CurrentPose { get; set; }
        = (0, 0, 0, 0, 0, 0, 1);

    public bool TrackingState { get; set; } = true;
    public Func<float, float, ArHitLike?> HitTestFunc { get; set; } = (_, _) => null;

    public (float tx, float ty, float tz, float qx, float qy, float qz, float qw) GetCameraPose() => CurrentPose;
    public ArHitLike? HitTest(float screenX, float screenY) => HitTestFunc(screenX, screenY);
    public bool IsTracking => TrackingState;
}
