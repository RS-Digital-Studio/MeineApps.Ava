using Google.AR.Core;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Android.Ar;

/// <summary>Plan-Kap. 7 Stufe 3: Wrapper der Google.AR.Core.Session auf das
/// plattform-unabhaengige <see cref="IArSessionLike"/>-Interface. Damit kann Logik
/// die nur Pose/HitTest braucht ohne ARCore-Abhaengigkeit getestet werden.</summary>
public sealed class AndroidArSession : IArSessionLike
{
    private readonly Func<Frame?> _frameProvider;

    /// <summary>Frame-Provider statt fester Session-Referenz, weil die Activity das
    /// aktuelle Frame lazy via <c>_frameLock</c> bereitstellt.</summary>
    public AndroidArSession(Func<Frame?> frameProvider)
    {
        _frameProvider = frameProvider;
    }

    public (float tx, float ty, float tz, float qx, float qy, float qz, float qw) GetCameraPose()
    {
        var frame = _frameProvider();
        var pose = frame?.Camera?.Pose;
        if (pose == null) return (0, 0, 0, 0, 0, 0, 1);
        var q = pose.GetRotationQuaternion();
        return (pose.Tx(), pose.Ty(), pose.Tz(),
            q?[0] ?? 0, q?[1] ?? 0, q?[2] ?? 0, q?[3] ?? 1);
    }

    public ArHitLike? HitTest(float screenX, float screenY)
    {
        var frame = _frameProvider();
        if (frame == null) return null;
        try
        {
            var hits = frame.HitTest(screenX, screenY);
            if (hits == null || hits.Count == 0) return null;
            var best = hits[0];
            var pose = best.HitPose;
            if (pose == null) return null;
            var quality = best.Trackable switch
            {
                Plane => ArHitLikeQuality.Plane,
                Google.AR.Core.Point => ArHitLikeQuality.Point,
                _ => ArHitLikeQuality.InstantPlacement,
            };
            return new ArHitLike(pose.Tx(), pose.Ty(), pose.Tz(), best.Distance, quality);
        }
        catch
        {
            return null;
        }
    }

    public bool IsTracking
    {
        get
        {
            var frame = _frameProvider();
            return frame?.Camera?.TrackingState == TrackingState.Tracking;
        }
    }
}
