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
public sealed class AndroidAudioService : IAudioService, IDisposable
{
    private readonly Activity _activity;
    private readonly AssetManager _assets;
    private SoundPool? _soundPool;
    private Vibrator? _vibrator;
    private readonly Dictionary<GameSound, int> _soundIds = new();
    private bool _initialized;

    // Hintergrundmusik (MediaPlayer für Loop-Streaming)
    private MediaPlayer? _musicPlayer;
    private MediaPlayer? _crossfadeOldPlayer;     // P2.3: Alter Player waehrend Crossfade
    private MusicTrack _currentTrack = MusicTrack.None;
    private readonly object _musicLock = new();
    private System.Timers.Timer? _crossfadeTimer; // 50ms-Tick fuer Volume-Ramp
    private const float MusicMaxVolume = 0.5f;
    private const int CrossfadeDurationMs = 800;
    private DateTime _crossfadeStart;
    private bool _crossfadeFadingOut;             // true = StopMusic mit Fade
    private float _preFocusVolume = MusicMaxVolume; // P2.3: Volume vor AudioFocus-Loss
    // Code-Review-Fix [Finding 7]: Aktueller Player-Volume mitfuehren, damit PauseMusic
    // nach Duck-Modus nicht wieder auf Full springt. MediaPlayer hat keine GetVolume-API.
    private float _currentMusicVolume = MusicMaxVolume;

    /// <summary>P2.3: MusicTrack -> Asset-Dateiname (Loop-File in <c>Assets/Music/</c>).</summary>
    private static readonly Dictionary<MusicTrack, string> MusicFileMap = new()
    {
        [MusicTrack.IdleWorkshop] = "music_idle_workshop.ogg",
        [MusicTrack.BossOrTournament] = "music_boss_tournament.ogg",
        [MusicTrack.Celebration] = "music_celebration.ogg",
    };

    // P2.3: AudioFocus — bei Telefonanruf / Bluetooth-Switch wird die Musik gemutet.
    private AudioManager? _audioManager;
    private AudioFocusRequestClass? _audioFocusRequest;
    private AudioFocusListener? _audioFocusListener;

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
        get => _gameStateService.State.Settings.SoundEnabled;
        set
        {
            _gameStateService.State.Settings.SoundEnabled = value;
            // State wird via AutoSave (30s) persistiert
        }
    }

    public bool MusicEnabled
    {
        get => _gameStateService.State.Settings.MusicEnabled;
        set
        {
            _gameStateService.State.Settings.MusicEnabled = value;
            // State wird via AutoSave (30s) persistiert
            if (!value) StopMusic();
        }
    }

    /// <summary>F-19: SFX-Volume 0..1 — wird beim Play auf SoundPool angewandt.</summary>
    public float SfxVolume
    {
        get => _gameStateService.State.Settings.SfxVolume;
        set => _gameStateService.State.Settings.SfxVolume = System.Math.Clamp(value, 0f, 1f);
    }

    /// <summary>F-19: Music-Volume 0..1 — wird sofort an den MediaPlayer durchgereicht.</summary>
    public float MusicVolume
    {
        get => _gameStateService.State.Settings.MusicVolume;
        set
        {
            var clamped = System.Math.Clamp(value, 0f, 1f);
            _gameStateService.State.Settings.MusicVolume = clamped;
            // Sofort auf aktuell laufenden Track anwenden, damit der Slider Live-Feedback gibt.
            try
            {
                lock (_musicLock)
                {
                    var effective = MusicMaxVolume * clamped;
                    _musicPlayer?.SetVolume(effective, effective);
                    _currentMusicVolume = effective;
                }
            }
            catch { /* MediaPlayer-State-Change-Fehler still ignorieren */ }
        }
    }

    public AndroidAudioService(Activity activity, IGameStateService gameStateService)
    {
        _activity = activity;
        _gameStateService = gameStateService;
        _assets = activity.Assets!;
        InitializeSoundPool();
        InitializeVibrator();
        InitializeAudioFocus();
    }

    /// <summary>
    /// P2.3: AudioFocus-Listener registrieren. Bei Loss → Pause, bei Gain → Resume.
    /// API 26+ via <see cref="AudioFocusRequestClass"/>, davor Legacy-Pfad.
    /// </summary>
    private void InitializeAudioFocus()
    {
        try
        {
            _audioManager = (AudioManager?)_activity.GetSystemService(Context.AudioService);
            if (_audioManager == null) return;

            _audioFocusListener = new AudioFocusListener(this);

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                var attrs = new AudioAttributes.Builder()
                    ?.SetUsage(AudioUsageKind.Game)
                    ?.SetContentType(AudioContentType.Music)
                    ?.Build();

                _audioFocusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                    ?.SetAudioAttributes(attrs!)
                    ?.SetAcceptsDelayedFocusGain(false)
                    ?.SetOnAudioFocusChangeListener(_audioFocusListener)
                    ?.Build();
            }
        }
        catch
        {
            // AudioFocus ist Best-Effort — wenn das nicht klappt, spielt die Musik trotzdem
        }
    }

    /// <summary>P2.3: AudioFocus anfordern. true = bekommen, false = von System verweigert.</summary>
    private bool RequestAudioFocus()
    {
        try
        {
            if (_audioManager == null) return true;

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                if (_audioFocusRequest == null) return true;
                var result = _audioManager.RequestAudioFocus(_audioFocusRequest);
                return result == AudioFocusRequest.Granted;
            }
#pragma warning disable CA1422 // Legacy-Pfad fuer API < 26
            var legacyResult = _audioManager.RequestAudioFocus(_audioFocusListener, global::Android.Media.Stream.Music, AudioFocus.Gain);
#pragma warning restore CA1422
            return legacyResult == AudioFocusRequest.Granted;
        }
        catch
        {
            return true; // Best-Effort
        }
    }

    private void AbandonAudioFocus()
    {
        try
        {
            if (_audioManager == null) return;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                if (_audioFocusRequest != null) _audioManager.AbandonAudioFocusRequest(_audioFocusRequest);
            }
            else
            {
#pragma warning disable CA1422
                _audioManager.AbandonAudioFocus(_audioFocusListener);
#pragma warning restore CA1422
            }
        }
        catch { }
    }

    /// <summary>P2.3: Listener-Klasse fuer AudioFocus-Aenderungen.</summary>
    private sealed class AudioFocusListener : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        private readonly AndroidAudioService _service;
        public AudioFocusListener(AndroidAudioService service) { _service = service; }

        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            switch (focusChange)
            {
                case AudioFocus.Loss:
                case AudioFocus.LossTransient:
                    _service.PauseMusic();
                    break;
                case AudioFocus.LossTransientCanDuck:
                    // Kurz andere App ueber uns — Volume halbieren statt pausieren
                    _service.SetMusicVolume(MusicMaxVolume * 0.3f);
                    break;
                case AudioFocus.Gain:
                    _service.ResumeMusic();
                    _service.SetMusicVolume(MusicMaxVolume);
                    break;
            }
        }
    }

    /// <summary>P2.3: Volume direkt setzen (ohne Crossfade) — fuer Duck-Modus.</summary>
    private void SetMusicVolume(float volume)
    {
        lock (_musicLock)
        {
            try
            {
                _musicPlayer?.SetVolume(volume, volume);
                _currentMusicVolume = volume; // Code-Review-Fix [Finding 7]
            }
            catch { }
        }
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
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
                InitializeVibratorApi31();
            else
                InitializeVibratorLegacy();
        }
        catch
        {
            // Vibrator nicht verfügbar auf manchen Geräten
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
    private void InitializeVibratorApi31()
    {
        var vibratorManager = (VibratorManager?)_activity.GetSystemService(Context.VibratorManagerService);
        _vibrator = vibratorManager?.DefaultVibrator;
    }

    private void InitializeVibratorLegacy()
    {
#pragma warning disable CA1422 // VibratorService ist deprecated ab API 31, aber Fallback nötig
        _vibrator = (Vibrator?)_activity.GetSystemService(Context.VibratorService);
#pragma warning restore CA1422
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
        if (!MusicEnabled) return Task.CompletedTask;

        try
        {
            lock (_musicLock)
            {
                StopMusicInternal();

                _musicPlayer = new MediaPlayer();
            }

            var afd = _assets.OpenFd($"Music/{musicFile}");
            if (afd != null)
            {
                _musicPlayer.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
                afd.Close();

                _musicPlayer.Looping = true;
                _musicPlayer.SetVolume(MusicMaxVolume, MusicMaxVolume);
                _currentMusicVolume = MusicMaxVolume; // Code-Review-Fix [Finding 7]

                // Prepare() synchron (PrepareAsync() ist void im Java-Binding, kein Task)
                _musicPlayer.Prepare();
                RequestAudioFocus();
                _musicPlayer.Start();
            }
        }
        catch
        {
            // Musik-Wiedergabe kann fehlschlagen (fehlende Datei, Codec-Problem)
            lock (_musicLock)
            {
                StopMusicInternal();
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>P2.3: Track-basierte API mit Crossfade (~800ms).</summary>
    public Task PlayMusicAsync(MusicTrack track, bool crossfade = true)
    {
        if (!MusicEnabled || track == MusicTrack.None)
        {
            StopMusic(fadeOut: crossfade);
            return Task.CompletedTask;
        }
        if (_currentTrack == track && _musicPlayer != null && _musicPlayer.IsPlaying) return Task.CompletedTask;
        if (!MusicFileMap.TryGetValue(track, out var fileName)) return Task.CompletedTask;

        try
        {
            MediaPlayer? newPlayer = null;
            lock (_musicLock)
            {
                if (crossfade && _musicPlayer != null)
                {
                    // Alten Player als Crossfade-Out, neuen Player aufbauen
                    _crossfadeOldPlayer?.Release();
                    _crossfadeOldPlayer = _musicPlayer;
                    _musicPlayer = null;
                }
                else
                {
                    StopMusicInternal();
                }
                newPlayer = new MediaPlayer();
                _musicPlayer = newPlayer;
            }

            var afd = _assets.OpenFd($"Music/{fileName}");
            if (afd == null) return Task.CompletedTask;

            newPlayer.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
            afd.Close();
            newPlayer.Looping = true;
            newPlayer.SetVolume(crossfade ? 0f : MusicMaxVolume, crossfade ? 0f : MusicMaxVolume);
            _currentMusicVolume = crossfade ? 0f : MusicMaxVolume; // Code-Review-Fix [Finding 7]
            newPlayer.Prepare();
            RequestAudioFocus();
            newPlayer.Start();
            _currentTrack = track;

            if (crossfade) StartCrossfade(fadingOut: false);
        }
        catch
        {
            lock (_musicLock) { StopMusicInternal(); }
        }
        return Task.CompletedTask;
    }

    public void StopMusic(bool fadeOut = false)
    {
        if (fadeOut && _musicPlayer != null)
        {
            lock (_musicLock)
            {
                _crossfadeOldPlayer?.Release();
                _crossfadeOldPlayer = _musicPlayer;
                _musicPlayer = null;
            }
            StartCrossfade(fadingOut: true);
            return;
        }
        lock (_musicLock)
        {
            StopMusicInternal();
            AbandonAudioFocus();
            _currentTrack = MusicTrack.None;
        }
    }

    public void PauseMusic()
    {
        lock (_musicLock)
        {
            try
            {
                if (_musicPlayer != null && _musicPlayer.IsPlaying)
                {
                    // Code-Review-Fix [Finding 7]: Aktuellen (ggf. geduckten) Volume merken,
                    // damit Resume nicht wieder auf Full springt.
                    _preFocusVolume = _currentMusicVolume;
                    _musicPlayer.Pause();
                }
            }
            catch { }
        }
    }

    public void ResumeMusic()
    {
        if (!MusicEnabled) return;
        lock (_musicLock)
        {
            try
            {
                if (_musicPlayer != null && !_musicPlayer.IsPlaying)
                {
                    _musicPlayer.SetVolume(_preFocusVolume, _preFocusVolume);
                    _musicPlayer.Start();
                }
            }
            catch { }
        }
    }

    /// <summary>P2.3: Startet den Volume-Ramp-Timer fuer Crossfade.</summary>
    private void StartCrossfade(bool fadingOut)
    {
        StopCrossfadeTimer();
        _crossfadeStart = DateTime.UtcNow;
        _crossfadeFadingOut = fadingOut;
        _crossfadeTimer = new System.Timers.Timer(50);
        _crossfadeTimer.Elapsed += OnCrossfadeTick;
        _crossfadeTimer.AutoReset = true;
        _crossfadeTimer.Start();
    }

    private void StopCrossfadeTimer()
    {
        if (_crossfadeTimer == null) return;
        _crossfadeTimer.Stop();
        _crossfadeTimer.Elapsed -= OnCrossfadeTick;
        _crossfadeTimer.Dispose();
        _crossfadeTimer = null;
    }

    private void OnCrossfadeTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _crossfadeStart).TotalMilliseconds;
        var t = (float)Math.Clamp(elapsed / CrossfadeDurationMs, 0, 1);

        lock (_musicLock)
        {
            try
            {
                _crossfadeOldPlayer?.SetVolume(MusicMaxVolume * (1f - t), MusicMaxVolume * (1f - t));
                if (!_crossfadeFadingOut && _musicPlayer != null)
                {
                    _musicPlayer.SetVolume(MusicMaxVolume * t, MusicMaxVolume * t);
                    _currentMusicVolume = MusicMaxVolume * t; // Code-Review-Fix [Finding 7]
                }
            }
            catch { }

            if (t >= 1f)
            {
                StopCrossfadeTimer();
                try { _crossfadeOldPlayer?.Stop(); _crossfadeOldPlayer?.Release(); } catch { }
                _crossfadeOldPlayer = null;
                if (_crossfadeFadingOut)
                {
                    AbandonAudioFocus();
                    _currentTrack = MusicTrack.None;
                }
            }
        }
    }

    /// <summary>Stoppt und released den MediaPlayer. Muss innerhalb von lock(_musicLock) aufgerufen werden.</summary>
    private void StopMusicInternal()
    {
        try
        {
            if (_musicPlayer != null)
            {
                if (_musicPlayer.IsPlaying)
                    _musicPlayer.Stop();

                _musicPlayer.Release();
                _musicPlayer = null;
            }
        }
        catch
        {
            _musicPlayer = null;
        }
    }

    public void Vibrate(VibrationType type)
    {
        if (_vibrator == null || !_vibrator.HasVibrator) return;

        try
        {
            var (pattern, amplitudes) = GetVibrationPattern(type);

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
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
#pragma warning disable CA1422 // Vibrate(long) deprecated ab API 26 (Legacy-Pfad)
                _vibrator.Vibrate(pattern[0]);
#pragma warning restore CA1422
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

    private bool _disposed;

    /// <summary>Released Crossfade-Timer + AudioFocus-Listener + SoundPool.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Crossfade-Timer stoppen + freigeben
        StopCrossfadeTimer();

        // Musik fade-frei stoppen (Cleanup-Pfad — kein User mehr da)
        lock (_musicLock)
        {
            try { _crossfadeOldPlayer?.Release(); } catch { }
            _crossfadeOldPlayer = null;
            StopMusicInternal();
        }

        // AudioFocus zurueckgeben
        AbandonAudioFocus();
        try { _audioFocusListener?.Dispose(); } catch { }
        try { _audioFocusRequest?.Dispose(); } catch { }
        _audioFocusListener = null;
        _audioFocusRequest = null;
        _audioManager = null;

        // SoundPool freigeben (releast alle SFX-Puffer auf einen Schlag)
        try { _soundPool?.Release(); } catch { }
        try { _soundPool?.Dispose(); } catch { }
        _soundPool = null;
        _soundIds.Clear();

        // Vibrator referenziert System-Service — kein eigener Dispose noetig
        _vibrator = null;
    }
}
