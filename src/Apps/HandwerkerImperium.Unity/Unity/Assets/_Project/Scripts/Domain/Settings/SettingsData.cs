using System;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Settings
{
    /// <summary>
    /// Spieler-Einstellungen (Sound, Grafik, Benachrichtigungen etc.).
    /// 1:1-Port aus dem Avalonia-Original (Models/SettingsData.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class SettingsData
    {
        [JsonProperty("soundEnabled")]
        public bool SoundEnabled { get; set; } = true;

        [JsonProperty("musicEnabled")]
        public bool MusicEnabled { get; set; } = true;

        [JsonProperty("hapticsEnabled")]
        public bool HapticsEnabled { get; set; } = true;

        [JsonProperty("notificationsEnabled")]
        public bool NotificationsEnabled { get; set; } = true;

        [JsonProperty("graphicsQuality")]
        public GraphicsQuality GraphicsQuality { get; set; } = GraphicsQuality.High;

        /// <summary>ReduceMotion als eigenes Accessibility-Setting, entkoppelt von GraphicsQuality.</summary>
        [JsonProperty("reduceMotion")]
        public bool ReduceMotion { get; set; }

        /// <summary>SFX-Volume 0..1 (Default 1.0).</summary>
        [JsonProperty("sfxVolume")]
        public float SfxVolume { get; set; } = 1.0f;

        /// <summary>Music-Volume 0..1 (Default 1.0).</summary>
        [JsonProperty("musicVolume")]
        public float MusicVolume { get; set; } = 1.0f;

        /// <summary>Bildschirm während Spiel aktiv halten (Imperium-Pass-Sweetener, Default false).</summary>
        [JsonProperty("keepScreenOn")]
        public bool KeepScreenOn { get; set; }

        [JsonProperty("cloudSaveEnabled")]
        public bool CloudSaveEnabled { get; set; } = true;

        [JsonProperty("lastCloudSaveTime")]
        public DateTime LastCloudSaveTime { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; } = "";

        /// <summary>DSGVO-Consent für Analytics-Events. Default false (Opt-In).</summary>
        [JsonProperty("analyticsEnabled")]
        public bool AnalyticsEnabled { get; set; }

        /// <summary>Ob der DSGVO-Consent-Dialog bereits einmal angezeigt wurde.</summary>
        [JsonProperty("analyticsConsentShown")]
        public bool AnalyticsConsentShown { get; set; }

        /// <summary>Letzte App-Version, für die der "Was ist neu"-Dialog gezeigt wurde (SemVer).</summary>
        [JsonProperty("lastWhatsNewVersion")]
        public string LastWhatsNewVersion { get; set; } = "0.0.0";
    }
}
