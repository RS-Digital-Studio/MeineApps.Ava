using System.Diagnostics;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using NAudio.Vorbis;
using NAudio.Wave;

namespace HandwerkerImperium.Desktop;

/// <summary>
/// Cross-Platform-Audio fuer Desktop (Windows/Linux/macOS).
///
/// Architektur:
/// - <b>Windows:</b> Volle Audio-Wiedergabe via NAudio + NAudio.Vorbis (OGG-Decoder).
///   Mehrere SFX-Voices parallel (max 6), Music-Loop mit Crossfade, Volume-Control.
/// - <b>Linux/macOS:</b> Process-Spawn-Fallback mit <c>ffplay</c> (FFMPEG). Best-Effort —
///   wenn ffplay nicht im PATH ist, wird Audio still verworfen (kein Crash).
///
/// Sounds liegen unter <c>Assets/Sounds/{name}.ogg</c> bzw. <c>Assets/Music/{name}.ogg</c>
/// im Output-Verzeichnis (kopiert via AvaloniaResource → bin/...).
///
/// Behebt Befund "Desktop-AudioService ist Stub" (P2).
/// </summary>
public sealed class DesktopAudioService : IAudioService, IDisposable
{
    private readonly IGameStateService _gameStateService;

    // Windows-NAudio-Pfad
    private readonly List<WaveOutEvent> _activeSfxVoices = [];
    private readonly object _sfxLock = new();
    private const int MaxConcurrentSfx = 6;

    // Music-State
    private WaveOutEvent? _musicOut;
    private VorbisWaveReader? _musicReader;
    private LoopStream? _musicLoop;
    private MusicTrack _currentTrack = MusicTrack.None;
    private readonly object _musicLock = new();
    private const float MusicMaxVolume = 0.5f;
    // Interne SFX-Mix-Decke. Der vom Nutzer steuerbare Faktor kommt aus Settings.SfxVolume (Property).
    private const float SfxMixLevel = 0.85f;

    // Crossfade
    private System.Timers.Timer? _crossfadeTimer;
    private DateTime _crossfadeStart;
    private const int CrossfadeDurationMs = 800;

    // ffplay-Fallback-State (Linux/macOS)
    private readonly bool _ffplayAvailable;
    private Process? _musicProcess;

    /// <summary>SFX-Datei-Mapping fuer den Enum-Pfad (lokal — verhindert Doppelpflege).</summary>
    private static readonly Dictionary<GameSound, string> SfxFileMap = new()
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

    private static readonly Dictionary<MusicTrack, string> MusicFileMap = new()
    {
        [MusicTrack.IdleWorkshop] = "music_idle_workshop",
        [MusicTrack.BossOrTournament] = "music_boss_tournament",
        [MusicTrack.Celebration] = "music_celebration",
    };

    public bool SoundEnabled
    {
        get => _gameStateService.State.Settings.SoundEnabled;
        set => _gameStateService.State.Settings.SoundEnabled = value;
    }

    public bool MusicEnabled
    {
        get => _gameStateService.State.Settings.MusicEnabled;
        set
        {
            _gameStateService.State.Settings.MusicEnabled = value;
            if (!value) StopMusic();
        }
    }

    /// <summary>F-19: SFX-Volume 0..1 (persistiert in Settings, Default 1.0). Skaliert die NAudio-SFX-Voices.</summary>
    public float SfxVolume
    {
        get => _gameStateService.State.Settings.SfxVolume;
        set => _gameStateService.State.Settings.SfxVolume = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>F-19: Music-Volume 0..1 (persistiert in Settings, Default 1.0). Wirkt sofort auf laufende Musik (ausser waehrend Crossfade).</summary>
    public float MusicVolume
    {
        get => _gameStateService.State.Settings.MusicVolume;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            _gameStateService.State.Settings.MusicVolume = clamped;
            // Sofort auf laufende NAudio-Musik anwenden — ausser ein Crossfade rampt gerade selbst die Lautstaerke.
            if (OperatingSystem.IsWindows() && _crossfadeTimer == null)
            {
                lock (_musicLock)
                {
                    try { if (_musicOut != null) _musicOut.Volume = EffectiveMusicVolume; } catch { }
                }
            }
        }
    }

    /// <summary>Effektiver Music-Pegel: interne Decke × nutzergesteuerter <see cref="MusicVolume"/>.</summary>
    private float EffectiveMusicVolume => MusicMaxVolume * MusicVolume;

    public DesktopAudioService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
        _ffplayAvailable = !OperatingSystem.IsWindows() && DetectFfplay();
    }

    private static bool DetectFfplay()
    {
        try
        {
            using var probe = Process.Start(new ProcessStartInfo
            {
                FileName = "ffplay",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            probe?.WaitForExit(2000);
            return probe is { ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // SFX
    // ─────────────────────────────────────────────────────────────────────

    public Task PlaySoundAsync(GameSound sound)
    {
        if (!SoundEnabled) return Task.CompletedTask;
        if (!SfxFileMap.TryGetValue(sound, out var fileName)) return Task.CompletedTask;
        return PlaySoundFileAsync(fileName);
    }

    /// <summary>Spielt eine OGG-Datei aus <c>Assets/Sounds/</c> ohne Enum-Round-Trip.</summary>
    public Task PlaySoundFileAsync(string fileName)
    {
        if (!SoundEnabled) return Task.CompletedTask;
        var path = ResolveAssetPath("Sounds", fileName + ".ogg");
        if (path == null) return Task.CompletedTask;

        if (OperatingSystem.IsWindows())
            PlaySfxWithNAudio(path);
        else if (_ffplayAvailable)
            PlayWithFfplayDetached(path, loop: false);

        return Task.CompletedTask;
    }

    private void PlaySfxWithNAudio(string path)
    {
        try
        {
            // NAudio.Vorbis: VorbisWaveReader fuer OGG-Vorbis.
            var reader = new VorbisWaveReader(path);
            var waveOut = new WaveOutEvent { Volume = SfxMixLevel * SfxVolume };
            waveOut.Init(reader);

            // Cleanup wenn fertig
            waveOut.PlaybackStopped += (_, _) =>
            {
                lock (_sfxLock) _activeSfxVoices.Remove(waveOut);
                try { waveOut.Dispose(); reader.Dispose(); } catch { }
            };

            lock (_sfxLock)
            {
                // Limit auf 6 parallele Voices (analog SoundPool-Pattern).
                if (_activeSfxVoices.Count >= MaxConcurrentSfx)
                {
                    var oldest = _activeSfxVoices[0];
                    _activeSfxVoices.RemoveAt(0);
                    try { oldest.Stop(); oldest.Dispose(); } catch { }
                }
                _activeSfxVoices.Add(waveOut);
            }

            waveOut.Play();
        }
        catch
        {
            // Datei fehlt / NAudio-Init-Fehler — still verwerfen.
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Music
    // ─────────────────────────────────────────────────────────────────────

    public Task PlayMusicAsync(string musicFile)
    {
        if (!MusicEnabled) return Task.CompletedTask;
        // Legacy: Asset-Dateiname direkt
        var fileName = musicFile.EndsWith(".ogg") ? musicFile : musicFile + ".ogg";
        var path = ResolveAssetPath("Music", fileName);
        if (path == null) return Task.CompletedTask;
        StartMusicTrack(path, MusicTrack.None, crossfade: false);
        return Task.CompletedTask;
    }

    public Task PlayMusicAsync(MusicTrack track, bool crossfade = true)
    {
        if (!MusicEnabled || track == MusicTrack.None)
        {
            StopMusic(fadeOut: crossfade);
            return Task.CompletedTask;
        }
        if (_currentTrack == track) return Task.CompletedTask;
        if (!MusicFileMap.TryGetValue(track, out var fileName)) return Task.CompletedTask;

        var path = ResolveAssetPath("Music", fileName + ".ogg");
        if (path == null) return Task.CompletedTask;

        StartMusicTrack(path, track, crossfade);
        return Task.CompletedTask;
    }

    private void StartMusicTrack(string path, MusicTrack track, bool crossfade)
    {
        if (OperatingSystem.IsWindows())
            StartMusicNAudio(path, track, crossfade);
        else if (_ffplayAvailable)
        {
            StopMusicProcessIfRunning();
            _musicProcess = PlayWithFfplayDetached(path, loop: true);
            _currentTrack = track;
        }
    }

    private void StartMusicNAudio(string path, MusicTrack track, bool crossfade)
    {
        try
        {
            lock (_musicLock)
            {
                StopMusicNAudioInternal();

                var reader = new VorbisWaveReader(path);
                _musicLoop = new LoopStream(reader);
                _musicOut = new WaveOutEvent { Volume = crossfade ? 0f : EffectiveMusicVolume };
                _musicOut.Init(_musicLoop);
                _musicReader = reader;
                _musicOut.Play();
                _currentTrack = track;
            }

            if (crossfade) StartCrossfade();
        }
        catch
        {
            lock (_musicLock) StopMusicNAudioInternal();
        }
    }

    public void StopMusic(bool fadeOut = false)
    {
        if (OperatingSystem.IsWindows())
        {
            lock (_musicLock) StopMusicNAudioInternal();
        }
        else
        {
            StopMusicProcessIfRunning();
        }
        _currentTrack = MusicTrack.None;
    }

    public void PauseMusic()
    {
        if (OperatingSystem.IsWindows())
        {
            try { _musicOut?.Pause(); } catch { }
        }
        else if (_musicProcess != null && !_musicProcess.HasExited)
        {
            // Pausieren via Process — nicht trivial, einfach stoppen.
            StopMusicProcessIfRunning();
        }
    }

    public void ResumeMusic()
    {
        if (!MusicEnabled) return;
        if (OperatingSystem.IsWindows())
        {
            try { _musicOut?.Play(); } catch { }
        }
    }

    private void StopMusicNAudioInternal()
    {
        try { _musicOut?.Stop(); } catch { }
        try { _musicOut?.Dispose(); } catch { }
        try { _musicLoop?.Dispose(); } catch { }
        try { _musicReader?.Dispose(); } catch { }
        _musicOut = null;
        _musicLoop = null;
        _musicReader = null;
    }

    private void StopMusicProcessIfRunning()
    {
        try
        {
            if (_musicProcess is { HasExited: false })
                _musicProcess.Kill(entireProcessTree: true);
            _musicProcess?.Dispose();
        }
        catch { }
        _musicProcess = null;
    }

    private void StartCrossfade()
    {
        StopCrossfadeTimer();
        _crossfadeStart = DateTime.UtcNow;
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
        try
        {
            if (_musicOut != null) _musicOut.Volume = EffectiveMusicVolume * t;
        }
        catch { }
        if (t >= 1f) StopCrossfadeTimer();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Spawnt ffplay nicht-blockierend. Returnt den Process fuer optionales Stop.</summary>
    private static Process? PlayWithFfplayDetached(string path, bool loop)
    {
        try
        {
            var args = loop
                ? $"-nodisp -autoexit -loglevel quiet -loop 0 \"{path}\""
                : $"-nodisp -autoexit -loglevel quiet \"{path}\"";
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffplay",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loest einen Asset-Pfad gegen das Output-Verzeichnis auf (AvaloniaResource liefert Assets
    /// nach <c>bin/Debug/.../Assets/Sounds/...</c>).
    /// </summary>
    private static string? ResolveAssetPath(string subDir, string fileName)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var full = Path.Combine(baseDir, "Assets", subDir, fileName);
        return File.Exists(full) ? full : null;
    }

    public void Vibrate(VibrationType type)
    {
        // Desktop hat kein Haptik-System — bewusst NoOp.
    }

    public void Dispose()
    {
        StopCrossfadeTimer();
        lock (_musicLock) StopMusicNAudioInternal();
        StopMusicProcessIfRunning();
        lock (_sfxLock)
        {
            foreach (var voice in _activeSfxVoices)
            {
                try { voice.Stop(); voice.Dispose(); } catch { }
            }
            _activeSfxVoices.Clear();
        }
    }
}

/// <summary>
/// Endlos-Loop-Wrapper fuer einen WaveStream — NAudio liefert das nicht out-of-the-box.
/// Resettet die Position auf 0 wenn das Ende erreicht ist.
/// </summary>
internal sealed class LoopStream(WaveStream source) : WaveStream
{
    private readonly WaveStream _source = source;

    public override WaveFormat WaveFormat => _source.WaveFormat;
    public override long Length => _source.Length;
    public override long Position
    {
        get => _source.Position;
        set => _source.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var bytesRead = _source.Read(buffer, offset + totalRead, count - totalRead);
            if (bytesRead == 0)
            {
                if (_source.Position == 0) break; // Datei leer
                _source.Position = 0;             // Loop-Reset
                continue;
            }
            totalRead += bytesRead;
        }
        return totalRead;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _source.Dispose();
        base.Dispose(disposing);
    }
}
