namespace SmartMeasure.Shared.Models;

/// <summary>NTRIP-Konfiguration fuer RTK-Korrekturdaten</summary>
public class NtripConfig
{
    /// <summary>NTRIP-Server Hostname/IP</summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>NTRIP-Server Port (Standard: 2101)</summary>
    public int Port { get; set; } = 2101;

    /// <summary>Mountpoint-Name</summary>
    public string Mountpoint { get; set; } = string.Empty;

    /// <summary>Benutzername (leer bei RTK2go)</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Passwort</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Ist das die eigene Basisstation?</summary>
    public bool IsOwnBase { get; set; }

    /// <summary>Profilname fuer Anzeige</summary>
    public string ProfileName { get; set; } = string.Empty;
}
