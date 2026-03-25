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

    /// <summary>Fix-Status als lesbarer Text</summary>
    public string FixStatusText => FixQuality switch
    {
        4 => "RTK FIX",
        5 => "RTK FLOAT",
        2 => "DGPS",
        1 => "GPS",
        _ => "KEIN FIX"
    };
}
