using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>Verlässlichkeit des Magnetkompass-Sensors (Android-`SensorStatus`).</summary>
public enum HeadingAccuracy { Unreliable, Low, Medium, High }

/// <summary>
/// Ausrichtungs-Messung des Geräts. <see cref="DeviceAzimuth"/> ist die geografische
/// Himmelsrichtung (true north, inkl. Missweisung) der Display-Normale (= Richtung, in die die
/// Rückseite des Geräts zeigt, wenn man es flach an eine geneigte Fläche hält).
/// <see cref="Tilt"/> ist die Neigung der Geräte-/Flächen-Ebene gegen die Horizontale (0 = flach,
/// 90 = senkrecht). <see cref="AzimuthReliable"/> ist false, wenn die Fläche zu flach liegt
/// (horizontale Projektion instabil) oder der Magnetsensor unkalibriert ist.
/// </summary>
public readonly record struct HeadingReading(
    double DeviceAzimuth,
    double MagneticAzimuth,
    double Declination,
    double Tilt,
    bool AzimuthReliable,
    HeadingAccuracy Accuracy);

/// <summary>
/// Liefert die Geräte-Ausrichtung (Azimut + Neigung) aus den Bewegungssensoren.
/// Android: Rotationsvektor (Sensor-Fusion) + Gravity + Missweisung. Desktop: Mock.
/// </summary>
public interface IHeadingService
{
    bool IsAvailable { get; }

    HeadingReading Current { get; }

    /// <summary>Feuert bei jeder neuen Messung (kann vom Sensor-Thread kommen).</summary>
    event EventHandler<HeadingReading>? Changed;

    /// <summary>Setzt die aktuelle Position für die Missweisungs-Korrektur (magnetisch -> geografisch Nord).</summary>
    void SetLocation(GeoLocation location);

    void Start();

    void Stop();
}
