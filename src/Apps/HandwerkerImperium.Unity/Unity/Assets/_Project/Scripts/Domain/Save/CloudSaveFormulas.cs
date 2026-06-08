#nullable enable

namespace HandwerkerImperium.Domain.Save
{
    /// <summary>Auflösung eines Cloud-Save-Abgleichs.</summary>
    public enum CloudSyncResolution
    {
        /// <summary>Lokal und Cloud identisch — nichts zu tun.</summary>
        InSync = 0,
        /// <summary>Lokaler Stand ist neuer — hochladen.</summary>
        UseLocal = 1,
        /// <summary>Cloud-Stand ist neuer — Konflikt: NICHT automatisch überschreiben, Nutzer fragen (Alert).</summary>
        ConflictAlert = 2
    }

    /// <summary>
    /// Cloud-Save-Konfliktauflösung (P3 §2/§3, CLAUDE.md §7): höhere Revision der Cloud → Alert statt Overwrite
    /// (der Nutzer entscheidet, kein stiller Datenverlust). Lokale Anti-Cheat-Signatur (HMAC) bleibt unangetastet —
    /// echte Ablehnung von Online-Werten ist server-seitig. Reine, Unity-freie Entscheidungslogik.
    /// </summary>
    public static class CloudSaveFormulas
    {
        /// <summary>
        /// Entscheidet anhand der Save-Revisionen: Cloud neuer → Alert, lokal neuer → Upload, gleich → InSync.
        /// Die Revision ist eine monoton steigende Zähler-Größe (pro Speichern erhöht), nicht der Zeitstempel.
        /// </summary>
        public static CloudSyncResolution Resolve(long localRevision, long remoteRevision)
        {
            if (remoteRevision > localRevision) return CloudSyncResolution.ConflictAlert;
            if (localRevision > remoteRevision) return CloudSyncResolution.UseLocal;
            return CloudSyncResolution.InSync;
        }

        /// <summary>True, wenn ein Upload sinnvoll ist (lokal strikt neuer als Cloud; bei Gleichstand InSync).</summary>
        public static bool ShouldUpload(long localRevision, long remoteRevision) =>
            localRevision > remoteRevision;
    }
}
