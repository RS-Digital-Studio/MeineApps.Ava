#nullable enable

namespace HandwerkerImperium.Domain.Save
{
    /// <summary>
    /// Save-Migrations-Infrastruktur (CLAUDE.md §7): Single-Source-of-Truth für die Schema-Version.
    /// P1 startet bei v1 (kein struktureller Vorgänger im neuen Schema); zukünftige Migrationen werden hier
    /// versionsweise verkettet, sodass alte Saves verlustfrei auf das aktuelle Schema gehoben werden.
    /// </summary>
    public static class SaveMigrator
    {
        /// <summary>Aktuelle Schema-Version des P1-Save-Modells.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>
        /// Hebt einen geladenen Save auf <see cref="CurrentSchemaVersion"/>. Unbekannte/ältere Versionen
        /// werden schrittweise migriert; ein bereits aktueller Save bleibt unverändert.
        /// </summary>
        public static GameSave Migrate(GameSave save)
        {
            if (save == null) return null!;

            // v0 (ungesetzt) / Vorversionen -> v1: rein struktureller Lift, keine Feld-Transformation nötig.
            if (save.SchemaVersion < 1)
                save.SchemaVersion = 1;

            // Künftige Migrationen:
            // if (save.SchemaVersion == 1) { /* v1 -> v2 ... */ save.SchemaVersion = 2; }

            if (save.SchemaVersion > CurrentSchemaVersion)
                save.SchemaVersion = CurrentSchemaVersion; // Save aus neuerer App-Version -> klemmen

            return save;
        }

        /// <summary>True, wenn der Save auf der aktuellen Schema-Version liegt.</summary>
        public static bool IsCurrent(GameSave save) => save != null && save.SchemaVersion == CurrentSchemaVersion;
    }
}
