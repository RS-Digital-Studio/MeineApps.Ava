#nullable enable

namespace ArcaneKingdom.Domain.Login
{
    /// <summary>
    /// Stufen die der Login-Flow durchläuft. Wird über <see cref="System.IProgress{T}"/>
    /// an die UI gemeldet, damit der LoginScreen einen aussagekräftigen Status zeigen kann.
    /// </summary>
    public enum LoginStage
    {
        /// <summary>Vor dem ersten Schritt — UI initialisiert.</summary>
        Idle = 0,

        /// <summary>Authentifizierung gegen Backend (Firebase Auth / Stub).</summary>
        Authenticating = 1,

        /// <summary>Spielerdaten vom Server holen.</summary>
        LoadingSave = 2,

        /// <summary>Daten validieren, optional Migration ausführen.</summary>
        Validating = 3,

        /// <summary>Login abgeschlossen, UI kann zum Hub wechseln.</summary>
        Ready = 4,

        /// <summary>Login fehlgeschlagen — UI zeigt Retry-Button.</summary>
        Failed = 99
    }
}
