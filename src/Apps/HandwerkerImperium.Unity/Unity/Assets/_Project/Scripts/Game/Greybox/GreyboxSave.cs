using Newtonsoft.Json;
using UnityEngine;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Save: simples JSON (Newtonsoft) in PlayerPrefs. Bewusst OHNE HMAC/Verschluesselung —
    /// das schlanke Genre-Save-Schema + HMAC kommt in P1 (P0-Spec §3/§8, GDD §12).
    /// </summary>
    public static class GreyboxSave
    {
        private const string Key = "hwi_greybox_save_v1";

        public static bool HasSave => PlayerPrefs.HasKey(Key);

        /// <summary>
        /// PlayerPrefs-Slot. WICHTIG: Tests MÜSSEN einen eigenen Test-Slot übergeben — der Default
        /// ist der echte Spielstand (Clear()/Save() im Test würden ihn sonst zerstören).
        /// </summary>
        public static void Save(GreyboxSimState state, string slot = Key)
        {
            if (state == null) return;
            PlayerPrefs.SetString(slot, JsonConvert.SerializeObject(state));
            PlayerPrefs.Save();
        }

        public static GreyboxSimState Load(string slot = Key)
        {
            if (!PlayerPrefs.HasKey(slot)) return null;
            try { return JsonConvert.DeserializeObject<GreyboxSimState>(PlayerPrefs.GetString(slot)); }
            catch { return null; }
        }

        public static void Clear(string slot = Key) => PlayerPrefs.DeleteKey(slot);
    }
}
