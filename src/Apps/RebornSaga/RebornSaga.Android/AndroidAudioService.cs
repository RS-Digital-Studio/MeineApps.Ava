using Android.App;
using Android.Content.Res;
using Android.Media;
using Android.OS;
using MeineApps.Core.Ava.Services;
using RebornSaga.Services;

namespace RebornSaga.Android;

/// <summary>
/// Android-Implementierung: SoundPool für SFX, MediaPlayer für BGM, Vibrator für Haptik.
/// Audio-Dateien: Assets/Sounds/*.ogg (SFX), Assets/Music/*.ogg (BGM).
/// </summary>
public sealed class AndroidAudioService : AudioService
{
    private readonly Activity _activity;
    private readonly AssetManager _assets;
    private SoundPool? _soundPool;
    private Vibrator? _vibrator;
    private readonly Dictionary<GameSfx, int> _soundIds = new();
    private bool _initialized;
    private bool _disposed;

    // BGM
    private MediaPlayer? _musicPlayer;
    private readonly object _musicLock = new();

    /// <summary>Mapping GameSfx → Asset-Dateiname (ohne Pfad/Extension).</summary>
    private static readonly Dictionary<GameSfx, string> SoundFileMap = new()
    {
        // UI
        [GameSfx.ButtonTap] = "sfx_button_tap",
        [GameSfx.MenuOpen] = "sfx_menu_open",
        [GameSfx.MenuClose] = "sfx_menu_close",
        [GameSfx.Confirm] = "sfx_confirm",
        [GameSfx.Error] = "sfx_error",

        // Dialog
        [GameSfx.TextTick] = "sfx_text_tick",
        [GameSfx.ChoiceSelect] = "sfx_choice_select",

        // Kampf
        [GameSfx.SwordSlash] = "sfx_sword_slash",
        [GameSfx.MagicCast] = "sfx_magic_cast",
        [GameSfx.HitImpact] = "sfx_hit_impact",
        [GameSfx.CriticalHit] = "sfx_critical_hit",
        [GameSfx.Dodge] = "sfx_dodge",
        [GameSfx.EnemyDefeat] = "sfx_enemy_defeat",
        [GameSfx.Block] = "sfx_block",
        [GameSfx.Heal] = "sfx_heal",
        [GameSfx.BuffApply] = "sfx_buff_apply",
        [GameSfx.DebuffApply] = "sfx_debuff_apply",

        // Fortschritt
        [GameSfx.LevelUp] = "sfx_level_up",
        [GameSfx.SkillUnlock] = "sfx_skill_unlock",
        [GameSfx.ItemPickup] = "sfx_item_pickup",
        [GameSfx.GoldCollect] = "sfx_gold_collect",
        [GameSfx.CodexDiscover] = "sfx_codex_discover",
        [GameSfx.BondUp] = "sfx_bond_up",
        [GameSfx.ChapterComplete] = "sfx_chapter_complete",

        // Spezial
        [GameSfx.GlitchSound] = "sfx_glitch",
        [GameSfx.TimeRift] = "sfx_time_rift",
        [GameSfx.KarmaShift] = "sfx_karma_shift",
        [GameSfx.UltimateActivate] = "sfx_ultimate",
    };

    /// <summary>Mapping BGM-ID → Asset-Pfad (ohne Verzeichnis-Präfix).</summary>
    private static readonly Dictionary<string, string> BgmFileMap = new()
    {
        [BgmTracks.TitleScreen] = "bgm_title.ogg",
        [BgmTracks.Village] = "bgm_village.ogg",
        [BgmTracks.Dungeon] = "bgm_dungeon.ogg",
        [BgmTracks.BossBattle] = "bgm_boss_battle.ogg",
        [BgmTracks.NormalBattle] = "bgm_normal_battle.ogg",
        [BgmTracks.Emotional] = "bgm_emotional.ogg",
        [BgmTracks.AriaSystem] = "bgm_aria_system.ogg",
        [BgmTracks.OverworldMap] = "bgm_overworld.ogg",
        [BgmTracks.Dreamworld] = "bgm_dreamworld.ogg",
        [BgmTracks.PrologBattle] = "bgm_prolog_battle.ogg",
    };

    public AndroidAudioService(Activity activity, IPreferencesService preferences)
        : base(preferences)
    {
        _activity = activity;
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
                ?.SetMaxStreams(8)
                ?.SetAudioAttributes(attributes)
                ?.Build();

            _initialized = true;

            foreach (var (sfx, filename) in SoundFileMap)
            {
                TryLoadSfx(sfx, $"Sounds/{filename}.ogg");
            }
        }
        catch
        {
            _initialized = false;
        }
    }

    private void TryLoadSfx(GameSfx sfx, string assetPath)
    {
        try
        {
            var afd = _assets.OpenFd(assetPath);
            if (afd != null && _soundPool != null)
            {
                var id = _soundPool.Load(afd, 1);
                _soundIds[sfx] = id;
                afd.Close();
            }
        }
        catch (Java.IO.IOException)
        {
            // Sound-Datei nicht vorhanden → ignorieren (wird später hinzugefügt)
        }
    }

    /// <summary>
    /// Vibrator initialisieren. API 31+ in eigene Methode ausgelagert,
    /// damit der JIT auf älteren Geräten den Typ nicht auflöst.
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
            // Vibrator nicht auf allen Geräten verfügbar
        }
    }

    private void InitializeVibratorApi31()
    {
        var vibratorManager = (VibratorManager?)_activity.GetSystemService(
            global::Android.Content.Context.VibratorManagerService);
        _vibrator = vibratorManager?.DefaultVibrator;
    }

    private void InitializeVibratorLegacy()
    {
#pragma warning disable CS0618 // VibratorService deprecated ab API 31
        _vibrator = (Vibrator?)_activity.GetSystemService(
            global::Android.Content.Context.VibratorService);
#pragma warning restore CS0618
    }

    public override void PlaySfx(GameSfx sfx)
    {
        if (!SfxEnabled || !_initialized || _soundPool == null) return;

        if (_soundIds.TryGetValue(sfx, out var soundId) && soundId > 0)
        {
            _soundPool.Play(soundId, SfxVolume, SfxVolume, 1, 0, 1.0f);
        }
    }

    public override void PlayBgm(string musicId)
    {
        if (!BgmEnabled) return;

        lock (_musicLock)
        {
            if (CurrentBgm == musicId) return; // Bereits laufend

            try
            {
                StopBgmInternal();

                if (!BgmFileMap.TryGetValue(musicId, out var filename))
                    return;

                _musicPlayer = new MediaPlayer();
                var afd = _assets.OpenFd($"Music/{filename}");
                if (afd != null)
                {
                    _musicPlayer.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
                    afd.Close();

                    _musicPlayer.Looping = true;
                    _musicPlayer.SetVolume(BgmVolume, BgmVolume);

                    // Prepare() synchron (PrepareAsync() ist void im Java-Binding)
                    _musicPlayer.Prepare();
                    _musicPlayer.Start();

                    // CurrentBgm erst nach erfolgreichem Start setzen
                    CurrentBgm = musicId;
                }
            }
            catch
            {
                StopBgmInternal();
            }
        }
    }

    public override void StopBgm()
    {
        lock (_musicLock)
        {
            StopBgmInternal();
        }
        base.StopBgm();
    }

    /// <summary>Stoppt und released den MediaPlayer. Muss innerhalb von lock(_musicLock) aufgerufen werden.</summary>
    private void StopBgmInternal()
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

    protected override void OnBgmVolumeChanged(float volume)
    {
        lock (_musicLock)
        {
            try
            {
                if (_musicPlayer is { IsPlaying: true })
                    _musicPlayer.SetVolume(volume, volume);
            }
            catch
            {
                // Volume-Änderung kann bei released Player fehlschlagen
            }
        }
    }

    /// <summary>Haptisches Feedback für Kampf-Treffer etc.</summary>
    public override void Vibrate(int durationMs = 30)
    {
        if (!VibrationEnabled) return;
        if (_vibrator == null || !_vibrator.HasVibrator) return;

        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var effect = VibrationEffect.CreateOneShot(durationMs, VibrationEffect.DefaultAmplitude);
                if (effect != null) _vibrator.Vibrate(effect);
            }
            else
            {
#pragma warning disable CS0618
                _vibrator.Vibrate(durationMs);
#pragma warning restore CS0618
            }
        }
        catch
        {
            // Vibration kann auf manchen Geräten fehlschlagen
        }
    }

    /// <summary>Gibt SoundPool, MediaPlayer und Vibrator frei.</summary>
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_musicLock)
        {
            StopBgmInternal();
        }

        try
        {
            _soundPool?.Release();
            _soundPool = null;
        }
        catch
        {
            // Release kann fehlschlagen wenn bereits disposed
        }

        _initialized = false;
        _soundIds.Clear();
    }
}
