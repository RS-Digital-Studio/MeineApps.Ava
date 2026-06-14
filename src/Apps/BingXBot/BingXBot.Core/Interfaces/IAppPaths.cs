namespace BingXBot.Core.Interfaces;

/// <summary>
/// Plattform-übergreifender Zugriff auf App-spezifische Ordner und Dateien.
/// Abstrahiert Environment.SpecialFolder-Zugriffe, damit Android (Sandbox-Pfad),
/// Desktop (APPDATA/~/.config) und Server (DB-Pfad-Override) sauber getrennt sind.
/// </summary>
public interface IAppPaths
{
    /// <summary>Basis-Ordner für alle BingXBot-Dateien (plattformabhängig).</summary>
    string AppDataFolder { get; }

    /// <summary>Vollständiger Pfad zur SQLite-Datenbank.</summary>
    string DatabasePath { get; }

    /// <summary>Vollständiger Pfad zur verschlüsselten Credentials-Datei.</summary>
    string CredentialsPath { get; }

    /// <summary>Ordner in dem das Client-Server-Profil (connection.json) abgelegt wird.</summary>
    string ClientProfileFolder { get; }

    /// <summary>Vollständiger Pfad zur connection.json (Pi-Server-Verbindungs-Profil).</summary>
    string ClientProfilePath { get; }
}
