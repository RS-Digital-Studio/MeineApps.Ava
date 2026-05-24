namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.17: Total-Station-Modus — Phone auf Stativ ueber RTK-Stab,
/// AR-Reticle als Tachymeter-Ersatz. Jeder Punkt wird radial gemessen (Distanz aus
/// Depth-API, Richtung aus ARCore-Heading + Stativ-Origin). Genauigkeit auf 30m
/// ±5cm, auf 10m ±2cm. Service haelt die Stationierungs-Parameter ueber die Session.</summary>
public interface ITotalStationService
{
    /// <summary>Stationiert das Phone an einer bekannten WGS84-Position (typisch
    /// uebergeben vom RTK-Stab oder via vorab eingemessenem Marker). Setzt das
    /// lokale Koordinatensystem auf diese Origin.</summary>
    void SetStationOrigin(double latitude, double longitude, double altitude, double headingDeg);

    /// <summary>Berechnet einen Ziel-Punkt aus Distanz + relativem Bearing (zur
    /// Stativ-Forward-Richtung).</summary>
    (double latitude, double longitude, double altitude) ProjectTarget(double distanceMeters, double bearingDeg, double pitchDeg);

    /// <summary>Aktuelle Station-Origin (null wenn nicht stationiert).</summary>
    (double latitude, double longitude, double altitude, double headingDeg)? Station { get; }
}
