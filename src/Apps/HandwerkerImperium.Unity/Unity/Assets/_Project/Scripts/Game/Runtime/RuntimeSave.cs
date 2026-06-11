using Newtonsoft.Json;
using UnityEngine;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Runtime;
using HandwerkerImperium.Domain.Save;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Persistenz des Vollspiels: serialisiert den <see cref="GameModel"/> über das schlanke, HMAC-signierte
    /// <see cref="GameSave"/>-Schema nach PlayerPrefs (JSON). Gerätegebundener Schlüssel; bei ungültiger Signatur
    /// wird der Save <b>repariert</b> (Sanitize), nicht verworfen (CLAUDE.md §7). Plattform-IO im Game-Layer,
    /// die Abbildung + Krypto im Domain.
    /// </summary>
    public static class RuntimeSave
    {
        private const string Key = "hwi_game_save_v1";

        /// <summary>Gerätegebundener HMAC-Schlüssel (Anti-Cheat-Bindung an das Gerät).</summary>
        public static string DeviceKey => SystemInfo.deviceUniqueIdentifier;

        public static bool HasSave => PlayerPrefs.HasKey(Key);

        /// <summary>
        /// PlayerPrefs-Slot. WICHTIG: Tests MÜSSEN einen eigenen Test-Slot übergeben — der Default
        /// ist der echte Spielstand (Clear()/Save() im Test würden ihn sonst zerstören; teuer gelernt).
        /// </summary>
        public static void Save(GameModel model, string deviceKey, string slot = Key)
        {
            if (model == null) return;
            var save = GameModelMapping.ToSave(model);
            SaveSignature.Sign(save, deviceKey);
            PlayerPrefs.SetString(slot, JsonConvert.SerializeObject(save));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Lädt das Modell. Kein Save → null. Beschädigtes JSON → null. Ungültige Signatur → Reparatur
        /// (Sanitize) statt Ablehnung, dann laden.
        /// </summary>
        public static GameModel Load(string deviceKey, IdleBalancing idleBalancing, string slot = Key)
        {
            string corruptBackupKey = slot + "_corrupt";
            if (!PlayerPrefs.HasKey(slot)) return null;
            string raw = PlayerPrefs.GetString(slot);
            GameSave save;
            try { save = JsonConvert.DeserializeObject<GameSave>(raw); }
            catch (System.Exception ex)
            {
                // Nie still wipen: nicht-parsebaren Save sichern (Diagnose/Recovery), DANN frisch starten.
                Debug.LogWarning("[RuntimeSave] Save nicht parsebar (" + ex.GetType().Name + "): " + ex.Message + " — Raw in Backup-Slot gesichert.");
                PlayerPrefs.SetString(corruptBackupKey, raw);
                PlayerPrefs.Save();
                return null;
            }
            if (save == null)
            {
                Debug.LogWarning("[RuntimeSave] Save deserialisiert zu null — Raw in Backup-Slot gesichert.");
                PlayerPrefs.SetString(corruptBackupKey, raw);
                PlayerPrefs.Save();
                return null;
            }

            SaveSanitizer.EnsureSlices(save);
            save = SaveMigrator.Migrate(save);
            if (!SaveSignature.Verify(save, deviceKey))
            {
                Debug.LogWarning("[RuntimeSave] Signatur ungueltig — Save wird repariert (Sanitize), nicht verworfen.");
                SaveSanitizer.Sanitize(save); // reparieren statt wipen
            }

            return GameModelMapping.FromSave(save, idleBalancing);
        }

        public static void Clear(string slot = Key) => PlayerPrefs.DeleteKey(slot);
    }
}
