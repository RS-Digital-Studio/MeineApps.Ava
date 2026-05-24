namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.16: Bewertung der aktuellen GNSS-Bedingungen vor einer
/// Vermessungs-Session. Kombiniert lokale Daten (Stab-Satelliten-Count) mit
/// NOAA Space Weather Prediction Center (geomagnetischer Kp-Index + Solar Flux
/// F10.7). Senkt Frustration bei schlechten Bedingungen — User wird vorgewarnt
/// statt im Feld zu rechnen warum die Genauigkeit heute schlecht ist.</summary>
public interface IGnssConditionService
{
    /// <summary>Aktuelle Konditionen abrufen. Mit Cache (TTL 1h) — wiederholte Aufrufe
    /// kosten kein zusaetzliches Netz. Wenn NOAA-API offline ist, liefert die Methode
    /// trotzdem ein Objekt mit lokalen Daten (Satellites/Pdop) und
    /// <see cref="GnssConditions.IonosphereLevel"/> = <see cref="GnssQuality.Unknown"/>.</summary>
    Task<GnssConditions> GetCurrentConditionsAsync(CancellationToken ct = default);
}

/// <summary>Schnappschuss der GNSS-Bedingungen zur Aufruf-Zeit.</summary>
public sealed record GnssConditions(
    int? SatelliteCount,
    double? Pdop,
    double? KpIndex,
    double? SolarFluxF107,
    GnssQuality IonosphereLevel,
    GnssQuality OverallLevel,
    string Recommendation);

/// <summary>Qualitaets-Klassifizierung pro Faktor + gesamt.</summary>
public enum GnssQuality
{
    Unknown = 0,
    /// <summary>Schlecht — Vermessung sollte verschoben werden (z.B. Kp ≥ 5 oder kein Fix).</summary>
    Poor = 1,
    /// <summary>Akzeptabel — leichte Beeintraechtigung (Kp 3-4).</summary>
    Fair = 2,
    /// <summary>Gut — Genauigkeit wie erwartet (Kp ≤ 2).</summary>
    Good = 3,
}
