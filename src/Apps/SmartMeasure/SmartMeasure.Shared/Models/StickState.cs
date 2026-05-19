namespace SmartMeasure.Shared.Models;

/// <summary>Aktueller Status des Vermessungsstabs (Rover)</summary>
public class StickState
{
    /// <summary>BLE verbunden?</summary>
    public bool IsConnected { get; set; }

    /// <summary>Akku-Stand 0-100%</summary>
    public int BatteryLevel { get; set; }

    /// <summary>RTK Fix-Quality (0=NoFix, 1=GPS, 2=DGPS, 4=RTK-Fix, 5=RTK-Float)</summary>
    public int FixQuality { get; set; }

    /// <summary>Horizontale Genauigkeit in cm</summary>
    public float HorizontalAccuracy { get; set; }

    /// <summary>Vertikale Genauigkeit in cm</summary>
    public float VerticalAccuracy { get; set; }

    /// <summary>Anzahl sichtbare Satelliten</summary>
    public int SatelliteCount { get; set; }

    /// <summary>Stab-Neigung vom Lot in Grad</summary>
    public float TiltAngle { get; set; }

    /// <summary>NTRIP-Status (0=Off, 1=Connecting, 2=Receiving, 3=Error)</summary>
    public int NtripStatus { get; set; }

    /// <summary>BNO085 Magnetometer-Accuracy (0-3)</summary>
    public int MagAccuracy { get; set; }

    /// <summary>
    /// Letzte vom Rover gemeldete Latitude in Grad (WGS84). Null wenn noch keine
    /// Position empfangen wurde. Wird via PositionUpdated-Event kontinuierlich aktualisiert.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>Letzte gemeldete Longitude in Grad (WGS84).</summary>
    public double? Longitude { get; set; }

    /// <summary>Letzte gemeldete Höhe in Meter über NN (geoid-korrigiert).</summary>
    public double? Altitude { get; set; }

    /// <summary>Komfort-Property — true wenn die letzte Position eine Höhe enthielt.</summary>
    public bool HasAltitude => Altitude.HasValue;

    /// <summary>Fix-Status als lesbarer Text</summary>
    public string FixStatusText => GetFixStatusText(FixQuality);

    /// <summary>Fix-Quality als lesbaren Text (zentrale Definition)</summary>
    public static string GetFixStatusText(int fixQuality) => fixQuality switch
    {
        4 => "RTK FIX",
        5 => "RTK FLOAT",
        2 => "DGPS",
        1 => "GPS",
        _ => "KEIN FIX"
    };
}
