using SQLite;

namespace GardenControl.Core.Models;

/// <summary>
/// Key-Value Systemkonfiguration, persistiert in SQLite.
/// </summary>
[Table("SystemConfig")]
public class SystemConfigEntry
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Bekannte Konfigurationsschlüssel
/// </summary>
public static class ConfigKeys
{
    /// <summary>Sensor-Abfrageintervall in Sekunden (Standard: 30)</summary>
    public const string PollIntervalSeconds = "poll_interval_seconds";

    /// <summary>Betriebsmodus: Off, Manual, Automatic</summary>
    public const string SystemMode = "system_mode";

    /// <summary>GPIO-Pin für die Pumpe (BCM, Standard: 23)</summary>
    public const string PumpGpioPin = "pump_gpio_pin";

    /// <summary>Maximale Verlaufsdaten in Tagen (Standard: 30)</summary>
    public const string HistoryRetentionDays = "history_retention_days";

    /// <summary>Minimale Pause zwischen zwei automatischen Bewässerungen einer Zone in Minuten</summary>
    public const string MinPauseBetweenWateringMinutes = "min_pause_minutes";
}
