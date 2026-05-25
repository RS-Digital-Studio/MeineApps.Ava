#nullable enable

namespace ArcaneKingdom.Domain.Login
{
    /// <summary>
    /// Stufen die der Login-Flow durchlaeuft. Wird ueber <see cref="System.IProgress{T}"/>
    /// an die UI gemeldet, damit der LoginScreen einen aussagekraeftigen Status zeigen kann.
    /// </summary>
    public enum LoginStage
    {
        /// <summary>Vor dem ersten Schritt — UI initialisiert.</summary>
        Idle = 0,

        /// <summary>Authentifizierung gegen Backend (Firebase Auth / Stub).</summary>
        Authenticating = 1,

        /// <summary>Spielerdaten vom Server holen.</summary>
        LoadingSave = 2,

        /// <summary>Daten validieren, optional Migration ausfuehren.</summary>
        Validating = 3,

        /// <summary>Login abgeschlossen, UI kann zum Hub wechseln.</summary>
        Ready = 4,

        /// <summary>Login fehlgeschlagen — UI zeigt Retry-Button.</summary>
        Failed = 99
    }
}
