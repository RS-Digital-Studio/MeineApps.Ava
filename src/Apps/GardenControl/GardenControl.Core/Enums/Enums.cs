namespace GardenControl.Core.Enums;

/// <summary>
/// Zustand einer Bewässerungszone
/// </summary>
public enum ZoneState
{
    /// <summary>Zone ist inaktiv</summary>
    Idle,
    /// <summary>Zone wird gerade bewässert</summary>
    Watering,
    /// <summary>Zone wartet auf Abkühlung nach Bewässerung</summary>
    Cooldown,
    /// <summary>Fehler (Sensor nicht erreichbar, Ventil klemmt)</summary>
    Error
}

/// <summary>
/// Betriebsmodus des Systems
/// </summary>
public enum SystemMode
{
    /// <summary>System aus - keine automatische Bewässerung</summary>
    Off,
    /// <summary>Manuelle Steuerung - nur über App</summary>
    Manual,
    /// <summary>Automatisch - Schwellenwert-basiert</summary>
    Automatic
}

/// <summary>
/// Auslöser einer Bewässerung
/// </summary>
public enum IrrigationTrigger
{
    /// <summary>Manuell über App ausgelöst</summary>
    Manual,
    /// <summary>Automatisch durch Schwellenwert</summary>
    Automatic,
    /// <summary>Zeitplan-basiert</summary>
    Scheduled
}

/// <summary>
/// Status der Sensorverbindung
/// </summary>
public enum SensorStatus
{
    /// <summary>Sensor liefert gültige Werte</summary>
    Ok,
    /// <summary>Sensor nicht angeschlossen oder defekt</summary>
    Disconnected,
    /// <summary>Messwert außerhalb des kalibrierten Bereichs</summary>
    OutOfRange
}
