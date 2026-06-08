using Newtonsoft.Json;
using UnityEngine;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game.Greybox
{
    /// <summary>
    /// P0-Save: simples JSON (Newtonsoft) in PlayerPrefs. Bewusst OHNE HMAC/Verschluesselung —
    /// das schlanke Genre-Save-Schema + HMAC kommt in P1 (P0-Spec §3/§8, GDD §12).
    /// </summary>
    public static class GreyboxSave
    {
        private const string Key = "hwi_greybox_save_v1";

        public static bool HasSave => PlayerPrefs.HasKey(Key);

        public static void Save(GreyboxSimState state)
        {
            if (state == null) return;
            PlayerPrefs.SetString(Key, JsonConvert.SerializeObject(state));
            PlayerPrefs.Save();
        }

        public static GreyboxSimState Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            try { return JsonConvert.DeserializeObject<GreyboxSimState>(PlayerPrefs.GetString(Key)); }
            catch { return null; }
        }

        public static void Clear() => PlayerPrefs.DeleteKey(Key);
    }
}
