#nullable enable
using ArcaneKingdom.Domain.Player;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Migrationen zwischen Save-Schema-Versionen.
    /// Wird vom FirebaseSaveService nach Load aufgerufen.
    /// </summary>
    public static class SaveMigrator
    {
        public const int CurrentSchemaVersion = 4;

        public static PlayerSave MigrateToCurrent(PlayerSave save)
        {
            if (save.SchemaVersion < 2) MigrateToV2(save);
            if (save.SchemaVersion < 3) MigrateToV3(save);
            if (save.SchemaVersion < 4) MigrateToV4(save);
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

        private static void MigrateToV3(PlayerSave save)
        {
            // v3 (Designplan v4) ergänzt:
            //   - Prestige (PrestigeSaveSlice)
            //   - Sternkarten (SternkartenSaveSlice)
            //   - Story (StorySaveSlice — chosen Race + Memory-Fragments)
            //   - Events (EventSaveSlice — Saison-Event-Punkte)
            //   - FavoritedCardInstanceIds (Schutz vor versehentlicher Fusion)
            //
            // Wie v2: PlayerSave-Konstruktor initialisiert die Felder mit Default-Werten.
            // Beim Laden eines v2-Saves (vor Schema-Upgrade) sind diese Felder null oder
            // verwenden Default-Konstruktor-Werte — also setzen wir hier explizit non-null.

            if (save.Prestige == null)              save.Prestige = new PrestigeSaveSlice();
            if (save.Sternkarten == null)           save.Sternkarten = new SternkartenSaveSlice();
            if (save.Story == null)                 save.Story = new StorySaveSlice();
            if (save.Events == null)                save.Events = new EventSaveSlice();
            if (save.FavoritedCardInstanceIds == null) save.FavoritedCardInstanceIds = new System.Collections.Generic.HashSet<string>();

            save.SchemaVersion = 3;
        }

        private static void MigrateToV4(PlayerSave save)
        {
            // v4 ergaenzt die persistierte Quest-Slice. Aeltere Saves hatten keinen Quest-Zustand;
            // null-safe initialisieren, damit Fortschritt ab jetzt erhalten bleibt.
            if (save.Quests == null) save.Quests = new QuestSaveSlice();
            save.SchemaVersion = 4;
        }
    }
}
