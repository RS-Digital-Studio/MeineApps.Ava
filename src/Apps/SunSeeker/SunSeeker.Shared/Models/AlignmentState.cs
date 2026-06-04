namespace SunSeeker.Shared.Models;

/// <summary>Bewertung der Ausricht-Genauigkeit fuer eine Ampel-Anzeige.</summary>
public enum AlignmentQuality { Excellent, Good, Fair, Poor }

/// <summary>
/// Live-Bewertung der aktuellen Panel-Ausrichtung gegen die Empfehlung und die aktuelle Sonne.
/// <see cref="AzimuthError"/> und <see cref="TiltError"/> sind vorzeichenbehaftet
/// (Ist minus Soll). <see cref="DirectGainFactor"/> ist cos(Einfallswinkel), also der Anteil
/// der Direktstrahlung, der bei der aktuellen Ausrichtung die Modulebene trifft (0..1).
/// </summary>
public readonly record struct AlignmentState(
    double PanelAzimuth,
    double PanelTilt,
    double AzimuthError,
    double TiltError,
    double AngleOfIncidence,
    double DirectGainFactor,
    bool SunBehindPanel,
    AlignmentQuality Quality);
