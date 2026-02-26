using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Media;
using Android.OS;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Android;

/// <summary>
/// Android-Implementierung für Audio-Wiedergabe und Haptik.
/// Nutzt SoundPool für SFX (Assets/Sounds/*.ogg) und Vibrator für Haptik.
/// </summary>
public class AndroidAudioService : IAudioService
{
    private readonly Activity _activity;
    private readonly AssetManager _assets;
    private SoundPool? _soundPool;
    private Vibrator? _vibrator;
    private readonly Dictionary<GameSound, int> _soundIds = new();
    private bool _initialized;

    /// <summary>Mapping GameSound → Dateiname (ohne Pfad/Extension)</summary>
    private static readonly Dictionary<GameSound, string> SoundFileMap = new()
    {
        [GameSound.ButtonTap] = "sfx_button_tap",
        [GameSound.MoneyEarned] = "sfx_money_earned",
        [GameSound.LevelUp] = "sfx_level_up",
        [GameSound.Upgrade] = "sfx_upgrade",
        [GameSound.WorkerHired] = "sfx_worker_hired",
        [GameSound.Perfect] = "sfx_perfect",
        [GameSound.Good] = "sfx_good",
        [GameSound.Miss] = "sfx_miss",
        [GameSound.OrderComplete] = "sfx_order_complete",
        [GameSound.Sawing] = "sfx_sawing",
        [GameSound.Hammering] = "sfx_hammering",
        [GameSound.Drilling] = "sfx_drilling",
        [GameSound.Countdown] = "sfx_countdown",
        [GameSound.CoinCollect] = "sfx_coin_collect",
        [GameSound.ComboHit] = "sfx_combo_hit",
    };

    private readonly IGameStateService _gameStateService;

    public bool SoundEnabled
    {
        get => _gameStateService.State.SoundEnabled;
        set
        {
            _gameStateService.State.SoundEnabled = value;
            _gameStateService.MarkDirty();
        }
    }

    public bool MusicEnabled
    {
        get => _gameStateService.State.MusicEnabled;
        set
        {
            _gameStateService.State.MusicEnabled = value;
            _gameStateService.MarkDirty();
            if (!value) StopMusic();
        }
    }

    public AndroidAudioService(Activity activity, IGameStateService gameStateService)
    {
        _activity = activity;
        _gameStateService = gameStateService;
        _assets = activity.Assets!;
        InitializeSoundPool();
        InitializeVibrator();
    }

    private void InitializeSoundPool()
    {
        try
        {
            var attributes = new AudioAttributes.Builder()
                ?.SetUsage(AudioUsageKind.Game)
                ?.SetContentType(AudioContentType.Sonification)
                ?.Build();

            _soundPool = new SoundPool.Builder()
                ?.SetMaxStreams(6)
                ?.SetAudioAttributes(attributes)
                ?.Build();

            _initialized = true;

            // Alle Sound-Assets aus Assets/Sounds/ laden
            foreach (var (sound, filename) in SoundFileMap)
            {
                TryLoadSfx(sound, $"Sounds/{filename}.ogg");
            }
        }
        catch
        {
            // SoundPool-Initialisierung kann auf manchen Geräten fehlschlagen
            _initialized = false;
        }
    }

    /// <summary>Versucht eine SFX-Datei aus Assets in SoundPool zu laden</summary>
    private void TryLoadSfx(GameSound sound, string assetPath)
    {
        try
        {
            var afd = _assets.OpenFd(assetPath);
            if (afd != null && _soundPool != null)
            {
                var id = _soundPool.Load(afd, 1);
                _soundIds[sound] = id;
                afd.Close();
            }
        }
        catch (Java.IO.IOException)
        {
            // Sound-Datei nicht vorhanden → ignorieren
        }
    }

    /// <summary>
    /// Vibrator initialisieren. API 31+ Code in eigene Methode ausgelagert,
    /// damit der JIT auf älteren Geräten (API 28/29) nicht VibratorManager auflöst
    /// → sonst nativer Mono-Abort (abort_application) weil der Java-Typ nicht existiert.
    /// </summary>
    private void InitializeVibrator()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                InitializeVibratorApi31();
            else
                InitializeVibratorLegacy();
        }
        catch
        {
            // Vibrator nicht verfügbar auf manchen Geräten
        }
    }

    private void InitializeVibratorApi31()
    {
        var vibratorManager = (VibratorManager?)_activity.GetSystemService(Context.VibratorManagerService);
        _vibrator = vibratorManager?.DefaultVibrator;
    }

    private void InitializeVibratorLegacy()
    {
#pragma warning disable CS0618 // VibratorService ist deprecated ab API 31, aber Fallback nötig
        _vibrator = (Vibrator?)_activity.GetSystemService(Context.VibratorService);
#pragma warning restore CS0618
    }

    public Task PlaySoundAsync(GameSound sound)
    {
        if (!SoundEnabled || !_initialized || _soundPool == null) return Task.CompletedTask;

        if (_soundIds.TryGetValue(sound, out var soundId) && soundId > 0)
        {
            _soundPool.Play(soundId, 1.0f, 1.0f, 1, 0, 1.0f);
        }

        return Task.CompletedTask;
    }

    public Task PlayMusicAsync(string musicFile)
    {
        // TODO: Hintergrundmusik implementieren wenn gewünscht
        return Task.CompletedTask;
    }

    public void StopMusic()
    {
        // TODO: Hintergrundmusik stoppen
    }

    public void Vibrate(VibrationType type)
    {
        if (_vibrator == null || !_vibrator.HasVibrator) return;

        try
        {
            var (pattern, amplitudes) = GetVibrationPattern(type);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                if (pattern.Length == 1)
                {
                    // Einmalige Vibration
                    var effect = VibrationEffect.CreateOneShot(pattern[0], VibrationEffect.DefaultAmplitude);
                    if (effect != null) _vibrator.Vibrate(effect);
                }
                else
                {
                    // Vibrations-Muster (timings mit 0ms Start-Pause)
                    var timings = new long[pattern.Length * 2];
                    for (int i = 0; i < pattern.Length; i++)
                    {
                        timings[i * 2] = i == 0 ? 0 : 30; // Pause zwischen Vibrationen
                        timings[i * 2 + 1] = pattern[i];
                    }
                    var effect = VibrationEffect.CreateWaveform(timings, -1);
                    if (effect != null) _vibrator.Vibrate(effect);
                }
            }
            else
            {
#pragma warning disable CS0618 // Vibrate(long) deprecated ab API 26
                _vibrator.Vibrate(pattern[0]);
#pragma warning restore CS0618
            }
        }
        catch
        {
            // Vibration kann auf manchen Geräten fehlschlagen
        }
    }

    /// <summary>
    /// Vibrations-Muster je nach Typ: (Dauer in ms, Amplituden)
    /// </summary>
    private static (long[] pattern, int[] amplitudes) GetVibrationPattern(VibrationType type) => type switch
    {
        VibrationType.Light => ([20], [128]),
        VibrationType.Medium => ([40], [192]),
        VibrationType.Heavy => ([80], [255]),
        VibrationType.Success => ([30, 50], [192, 255]),
        VibrationType.Error => ([50, 30, 50], [255, 128, 255]),
        VibrationType.LevelUp => ([40, 60, 80], [128, 192, 255]),
        VibrationType.MiniGameHit => ([15], [160]),
        _ => ([30], [128])
    };
}
