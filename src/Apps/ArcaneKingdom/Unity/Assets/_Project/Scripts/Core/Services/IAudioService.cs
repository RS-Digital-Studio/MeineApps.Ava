#nullable enable
using ArcaneKingdom.Core.Utility;

namespace ArcaneKingdom.Core.Services
{
    /// <summary>
    /// Audio-System: BGM (Loop, Crossfade) + SFX (One-Shot mit Pooling).
    /// </summary>
    public interface IAudioService
    {
        float MasterVolume { get; set; }
        float MusicVolume { get; set; }
        float SfxVolume { get; set; }

        void PlayMusic(string addressableKey, float fadeInSeconds = 1.0f);
        void StopMusic(float fadeOutSeconds = 1.0f);
        void PlaySfx(string addressableKey, float volume = 1.0f);
        void StopAllSfx();
    }
}
