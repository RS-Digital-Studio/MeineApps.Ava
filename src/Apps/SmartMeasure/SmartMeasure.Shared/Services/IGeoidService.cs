namespace SmartMeasure.Shared.Services;

/// <summary>
/// Konvertiert zwischen WGS84-Ellipsoid-Höhe (was GPS liefert) und Geoid-Höhe (NN, was User erwartet).
///
/// Hintergrund: Der ZED-F9P liefert in NAV-PVT sowohl heightEllipsoid als auch heightMSL.
/// Die App bekommt aktuell heightEllipsoid — in Deutschland ist der Geoid ca. +45 bis +50m
/// über dem Ellipsoid. Ohne Korrektur wäre "210m NN" in Wirklichkeit 210-48 = 162m NN.
///
/// Zwei Strategien:
/// - Client-seitige Korrektur via EGM96 (diese Implementierung): Offline-fähig, Rechen-billig.
/// - Firmware-seitig: ESP32 sendet bereits heightMSL. Dann darf die App NICHT nochmal korrigieren
///   → Setting <see cref="IsClientCorrectionEnabled"/> steuert das.
/// </summary>
public interface IGeoidService
{
    /// <summary>Geoid-Undulation N (Meter): h_ellipsoid = h_geoid + N. Für Deutschland +45 bis +50m.</summary>
    double GetUndulation(double latitude, double longitude);

    /// <summary>Ellipsoid-Höhe → Geoid-Höhe (NN). Wenn IsClientCorrectionEnabled=false, pass-through.</summary>
    double EllipsoidToGeoid(double latitude, double longitude, double ellipsoidAltitude);

    /// <summary>Geoid-Höhe → Ellipsoid-Höhe. Wenn IsClientCorrectionEnabled=false, pass-through.</summary>
    double GeoidToEllipsoid(double latitude, double longitude, double geoidAltitude);

    /// <summary>Gibt an ob Client-seitige Korrektur angewendet wird. Wenn false,
    /// nimmt die App an dass die Höhe bereits Geoid/MSL ist (z.B. F9P mit heightMSL-Config).</summary>
    bool IsClientCorrectionEnabled { get; set; }
}
