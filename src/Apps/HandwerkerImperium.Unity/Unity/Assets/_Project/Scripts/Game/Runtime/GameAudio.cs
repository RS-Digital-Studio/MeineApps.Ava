using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>Benannte SFX-Hooks des 3D-Idle-Loops (Mapping auf Clips in <see cref="GameAudio"/>).</summary>
    public enum GameSfx
    {
        ButtonTap = 0,
        MoneyEarned = 1,
        CoinCollect = 2,
        PlotUnlock = 3,
        WorkerHired = 4,
        LandmarkPhase = 5,
        LandmarkComplete = 6,
        Prestige = 7,
        OfflineEarnings = 8,
        StoryPing = 9,
        UpgradePaid = 10,
        Pickup = 11,
    }

    /// <summary>
    /// Zentraler Audio-Hub der Szene: Musik-Loop + SFX-One-Shots über einen kleinen AudioSource-Pool,
    /// mit Anti-Spam je SFX (hochfrequente Hooks wie Pickup/Coin klingen sonst wie ein Maschinengewehr).
    /// Views erreichen ihn über <see cref="GreyboxGameController.Audio"/> (Inspector-Verdrahtung,
    /// kein Service-Locator). Clips = kuratierter SoundForge-Bestand (AudioSync).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        [Header("SFX (Reihenfolge = GameSfx-Enum)")]
        [SerializeField] private AudioClip[] sfxClips = new AudioClip[12];

        [Header("Musik")]
        [SerializeField] private AudioClip musicLoop;

        [Header("Pegel")]
        [SerializeField] private float sfxVolume = 0.8f;
        [SerializeField] private float musicVolume = 0.4f;
        [SerializeField] private float minRepeatSeconds = 0.07f;

        private AudioSource _music;
        private AudioSource[] _pool;
        private int _next;
        private readonly float[] _lastPlay = new float[12];

        private void Awake()
        {
            _pool = new AudioSource[6];
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = gameObject.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
            }

            if (musicLoop != null)
            {
                _music = gameObject.AddComponent<AudioSource>();
                _music.clip = musicLoop;
                _music.loop = true;
                _music.volume = musicVolume;
                _music.playOnAwake = false;
                _music.Play();
            }
        }

        /// <summary>Spielt einen Loop-SFX (One-Shot, Anti-Spam, Pool-Rotation).</summary>
        public void Play(GameSfx sfx)
        {
            int idx = (int)sfx;
            if (sfxClips == null || idx < 0 || idx >= sfxClips.Length) return;
            var clip = sfxClips[idx];
            if (clip == null) return;
            if (Time.unscaledTime - _lastPlay[idx] < minRepeatSeconds) return;
            _lastPlay[idx] = Time.unscaledTime;

            var src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            src.PlayOneShot(clip, sfxVolume);
        }
    }
}
