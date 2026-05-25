#nullable enable
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using UnityEngine;

namespace ArcaneKingdom.Game.Services
{
    /// <summary>
    /// Basis-Audio mit zwei AudioSources (BGM + SFX-Pool). Sound-Assets über Addressables.
    ///
    /// SKELETT: Implementierung von Crossfade / SFX-Pooling / Addressables-Loading folgt
    /// in der MVP-Phase. Aktuell nur Volume-Kontrolle und Logging.
    /// </summary>
    public sealed class UnityAudioService : MonoBehaviour, IAudioService
    {
        [SerializeField] private AudioSource? musicSource;
        [SerializeField] private AudioSource? sfxSource;

        private float _masterVolume = 1f;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;

        public float MasterVolume { get => _masterVolume; set { _masterVolume = Mathf.Clamp01(value); ApplyVolumes(); } }
        public float MusicVolume { get => _musicVolume; set { _musicVolume = Mathf.Clamp01(value); ApplyVolumes(); } }
        public float SfxVolume { get => _sfxVolume; set { _sfxVolume = Mathf.Clamp01(value); ApplyVolumes(); } }

        public void PlayMusic(string addressableKey, float fadeInSeconds = 1.0f)
        {
            GameLogger.Info("Audio", $"PlayMusic({addressableKey}) (TODO: Addressables-Load + Crossfade)");
            // TODO MVP: Addressables.LoadAssetAsync<AudioClip>(addressableKey), Crossfade über 2 AudioSources.
        }

        public void StopMusic(float fadeOutSeconds = 1.0f)
        {
            GameLogger.Info("Audio", $"StopMusic (fade {fadeOutSeconds}s)");
            if (musicSource != null) musicSource.Stop();
        }

        public void PlaySfx(string addressableKey, float volume = 1.0f)
        {
            GameLogger.Verbose("Audio", $"PlaySfx({addressableKey}, vol {volume:F2})");
            // TODO MVP: SFX-Pool aus 8 AudioSources, LRU-Cache für geladene Clips.
        }

        public void StopAllSfx()
        {
            if (sfxSource != null) sfxSource.Stop();
        }

        private void ApplyVolumes()
        {
            if (musicSource != null) musicSource.volume = _musicVolume * _masterVolume;
            if (sfxSource != null) sfxSource.volume = _sfxVolume * _masterVolume;
        }
    }
}
