#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Achievement;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Tutorial;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Migrationen zwischen Save-Schema-Versionen.
    /// Wird vom FirebaseSaveService nach Load aufgerufen.
    /// </summary>
    public static class SaveMigrator
    {
        public const int CurrentSchemaVersion = 2;

        public static PlayerSave MigrateToCurrent(PlayerSave save)
        {
            if (save.SchemaVersion < 2) MigrateToV2(save);
            save.SchemaVersion = CurrentSchemaVersion;
            return save;
        }

        private static void MigrateToV2(PlayerSave save)
        {
            // v2 ergänzt die nullable Schema-Felder (TutorialProgress / PendingClaims /
            // PityCounters / TitlesUnlocked / Friends). Da PlayerSave die Felder mit
            // Default-Werten initialisiert, ist die Migration hier nur ein
            // No-Op-Schritt — die Felder existieren auf neu erstellten Saves bereits.
            save.SchemaVersion = 2;
        }
    }
}
