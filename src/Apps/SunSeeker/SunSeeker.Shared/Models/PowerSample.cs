namespace SunSeeker.Shared.Models;

/// <summary>Ein Solar-Leistungs-Messwert zu einem Zeitpunkt (PV-Eingang der Powerstation, Watt).</summary>
public readonly record struct PowerSample(DateTime TimestampUtc, double SolarWatts);
