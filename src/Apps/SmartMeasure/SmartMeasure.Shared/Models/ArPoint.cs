namespace SmartMeasure.Shared.Models;

/// <summary>Ein 3D-Punkt aus der AR-Kamera-Erfassung (lokale Meter-Koordinaten)</summary>
public class ArPoint
{
    /// <summary>Position X in Metern (lokal, relativ zum AR-Session-Start, rechts positiv)</summary>
    public float X { get; set; }

    /// <summary>Position Y in Metern (lokal, relativ zum AR-Session-Start, oben positiv)</summary>
    public float Y { get; set; }

    /// <summary>Position Z in Metern (lokal, relativ zum AR-Session-Start, nach hinten positiv)</summary>
    public float Z { get; set; }

    /// <summary>ARCore Anchor-ID (fuer stabile Positionierung in der Welt)</summary>
    public string? AnchorId { get; set; }

    /// <summary>Konfidenz des Hit-Tests (0.0 - 1.0)</summary>
    public float Confidence { get; set; }

    /// <summary>Optionales Label ("Ecke Terrasse", "Beetkante")</summary>
    public string? Label { get; set; }

    /// <summary>Zeitpunkt der Erfassung (UTC)</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Euklidischer Abstand zu einem anderen Punkt in Metern</summary>
    public float DistanceTo(ArPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>2D-Abstand (nur X/Z, ohne Hoehe) in Metern</summary>
    public float Distance2DTo(ArPoint other)
    {
        var dx = X - other.X;
        var dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
