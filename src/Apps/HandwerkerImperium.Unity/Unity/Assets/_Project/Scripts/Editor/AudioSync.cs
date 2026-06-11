using System.IO;
using UnityEditor;
using UnityEngine;

namespace HandwerkerImperium.Editor
{
    /// <summary>
    /// Synchronisiert das kuratierte Audio-Set aus dem Avalonia-HWI-Bestand (SoundForge-generiert,
    /// versioniert unter <c>HandwerkerImperium.Shared/Assets</c>) in das Unity-Projekt — Wiederverwenden
    /// statt Duplizieren: die Unity-Kopien sind lokal (gitignored) und jederzeit per Menü reproduzierbar.
    /// Menü: HandwerkerImperium ▸ Runtime ▸ Sync Audio von Avalonia.
    /// </summary>
    public static class AudioSync
    {
        private const string SourceRoot = "../../HandwerkerImperium/HandwerkerImperium.Shared/Assets"; // relativ zum Unity-Projektordner
        private const string SfxDir = "Assets/_Project/Audio/Sfx";
        private const string MusicDir = "Assets/_Project/Audio/Music";

        /// <summary>Kuratiertes Set für den 3D-Idle-Loop (Quelle hat 60+ — nur die genutzten Hooks).</summary>
        private static readonly string[] Sfx =
        {
            "sfx_button_tap", "sfx_money_earned", "sfx_coin_collect", "sfx_building_complete",
            "sfx_intern_ready", "sfx_hammering", "sfx_milestone_major", "sfx_prestige_complete",
            "sfx_offline_earnings", "sfx_news_ping", "sfx_costs_paid", "sfx_drop_common",
        };

        private static readonly string[] Music = { "music_idle_workshop" };

        [MenuItem("HandwerkerImperium/Runtime/Sync Audio von Avalonia")]
        public static void Sync()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath); // .../Unity
            string source = Path.GetFullPath(Path.Combine(projectRoot, SourceRoot));
            if (!Directory.Exists(source))
            {
                Debug.LogError("[AudioSync] Avalonia-Asset-Quelle nicht gefunden: " + source);
                return;
            }

            Directory.CreateDirectory(SfxDir);
            Directory.CreateDirectory(MusicDir);
            int copied = 0;
            foreach (var name in Sfx)
                copied += CopyIfNewer(Path.Combine(source, "Sounds", name + ".ogg"), Path.Combine(SfxDir, name + ".ogg")) ? 1 : 0;
            foreach (var name in Music)
                copied += CopyIfNewer(Path.Combine(source, "Music", name + ".ogg"), Path.Combine(MusicDir, name + ".ogg")) ? 1 : 0;

            AssetDatabase.Refresh();
            Debug.Log($"[AudioSync] {copied} Audio-Dateien synchronisiert ({Sfx.Length} SFX + {Music.Length} Musik kuratiert).");
        }

        private static bool CopyIfNewer(string from, string to)
        {
            if (!File.Exists(from))
            {
                Debug.LogWarning("[AudioSync] Quelle fehlt: " + from);
                return false;
            }
            if (File.Exists(to) && File.GetLastWriteTimeUtc(to) >= File.GetLastWriteTimeUtc(from)) return false;
            File.Copy(from, to, true);
            return true;
        }
    }
}
