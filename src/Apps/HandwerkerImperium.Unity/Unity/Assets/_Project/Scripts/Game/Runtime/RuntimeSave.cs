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

        public static void Save(GameModel model, string deviceKey)
        {
            if (model == null) return;
            var save = GameModelMapping.ToSave(model);
            SaveSignature.Sign(save, deviceKey);
            PlayerPrefs.SetString(Key, JsonConvert.SerializeObject(save));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Lädt das Modell. Kein Save → null. Beschädigtes JSON → null. Ungültige Signatur → Reparatur
        /// (Sanitize) statt Ablehnung, dann laden.
        /// </summary>
        public static GameModel Load(string deviceKey, IdleBalancing idleBalancing)
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            GameSave save;
            try { save = JsonConvert.DeserializeObject<GameSave>(PlayerPrefs.GetString(Key)); }
            catch { return null; }
            if (save == null) return null;

            SaveSanitizer.EnsureSlices(save);
            save = SaveMigrator.Migrate(save);
            if (!SaveSignature.Verify(save, deviceKey))
                SaveSanitizer.Sanitize(save); // reparieren statt wipen

            return GameModelMapping.FromSave(save, idleBalancing);
        }

        public static void Clear() => PlayerPrefs.DeleteKey(Key);
    }
}
