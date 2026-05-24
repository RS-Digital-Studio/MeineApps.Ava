namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 7 Stufe 3: Wrapper-Interface fuer ARCore-Session, damit
/// Activity-Logik testbar wird ohne Google.AR.Core-Abhaengigkeit. AndroidArSession
/// (Android) wickelt eine echte Session, MockArSession (Tests) liefert
/// deterministische HitResults und Posen.
///
/// Minimale API — wir wrappen nur was die Activity-/AnchorManager-Logik braucht.
/// Ist noch nicht produktiv verkabelt — als Pattern-Stub fuer Folge-Iteration.</summary>
public interface IArSessionLike
{
    /// <summary>Pose-Komponenten der Kamera (Welt-Frame).</summary>
    (float tx, float ty, float tz, float qx, float qy, float qz, float qw) GetCameraPose();

    /// <summary>Hit-Test am Screen-Pixel; null wenn nichts getroffen.</summary>
    ArHitLike? HitTest(float screenX, float screenY);

    /// <summary>True wenn Tracking aktiv ist (Camera.TrackingState == Tracking).</summary>
    bool IsTracking { get; }
}

/// <summary>Plattform-unabhaengiges HitResult fuer Tests + Mocks.</summary>
public sealed record ArHitLike(
    float X, float Y, float Z,
    float DistanceMeters,
    ArHitLikeQuality Quality);

public enum ArHitLikeQuality { None, InstantPlacement, Point, Plane }
