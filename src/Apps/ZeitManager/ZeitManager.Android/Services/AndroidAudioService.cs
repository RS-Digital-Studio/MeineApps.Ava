using Android.App;
using Android.Content;
using Android.Media;
using ZeitManager.Audio;
using ZeitManager.Models;
using ZeitManager.Services;

namespace ZeitManager.Android.Services;

/// <summary>
/// Android-spezifischer AudioService - nutzt MediaPlayer für Tonwiedergabe.
/// Unterstützt eingebaute Töne, System-Ringtones und benutzerdefinierte Sounds.
/// </summary>
public class AndroidAudioService : IAudioService
{
    private readonly object _lock = new();
    private CancellationTokenSource? _loopCts;
    private MediaPlayer? _currentPlayer;
    private string? _currentTempFile;
    private List<SoundItem>? _cachedSystemSounds;
    // Cache für AvailableSounds (wird bei Änderungen invalidiert)
    private IReadOnlyList<SoundItem>? _cachedAvailableSounds;

    /// <summary>
    /// Callback für Ringtone-Picker. Wird von MainActivity gesetzt.
    /// Gibt die gewählte URI zurück (oder null bei Abbruch).
    /// </summary>
    public static Func<Task<string?>>? PickRingtoneCallback { get; set; }

    public IReadOnlyList<SoundItem> AvailableSounds
    {
        get
        {
            if (_cachedAvailableSounds == null)
            {
                var all = new List<SoundItem>(SoundDefinitions.BuiltInSounds);
                all.AddRange(SystemSounds);
                _cachedAvailableSounds = all;
            }
            return _cachedAvailableSounds;
        }
    }

    public IReadOnlyList<SoundItem> SystemSounds
    {
        get
        {
            _cachedSystemSounds ??= LoadSystemSounds();
            return _cachedSystemSounds;
        }
    }

    public string DefaultTimerSound => "default";
    public string DefaultAlarmSound => "default";

    public async Task PlayAsync(string soundId, bool loop = false)
    {
        Stop();

        // Prüfen ob es ein System-Sound mit URI ist
        var systemSound = SystemSounds.FirstOrDefault(s => s.Id == soundId);
        if (systemSound?.Uri != null)
        {
            await PlayUriAsync(systemSound.Uri, loop);
            return;
        }

        if (loop)
        {
            CancellationTokenSource cts;
            lock (_lock)
            {
                cts = new CancellationTokenSource();
                _loopCts = cts;
            }
            var token = cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        PlayTone(soundId);
                        await Task.Delay(2000, token);
                    }
                }
                catch (OperationCanceledException) { }
            }, token);
        }
        else
        {
            await Task.Run(() => PlayTone(soundId));
        }
    }

    public async Task PlayUriAsync(string uri, bool loop = false)
    {
        Stop();

        if (loop)
        {
            CancellationTokenSource cts;
            lock (_lock)
            {
                cts = new CancellationTokenSource();
                _loopCts = cts;
            }
            var token = cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        PlayFromUri(uri);
                        await Task.Delay(2000, token);
                    }
                }
                catch (OperationCanceledException) { }
            }, token);
        }
        else
        {
            await Task.Run(() => PlayFromUri(uri));
        }
    }

    public void Stop()
    {
        // Loop stoppen
        CancellationTokenSource? oldCts;
        lock (_lock)
        {
            oldCts = _loopCts;
            _loopCts = null;
        }

        if (oldCts != null)
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }

        // Aktuellen Player stoppen und freigeben
        StopCurrentPlayer();
    }

    public async Task<SoundItem?> PickSoundAsync()
    {
        if (PickRingtoneCallback == null) return null;

        var uri = await PickRingtoneCallback();
        if (string.IsNullOrEmpty(uri)) return null;

        // Sound-Name aus URI ermitteln
        var name = GetRingtoneName(uri) ?? "Custom Sound";
        var soundId = $"ringtone_{HashHelper.StableHash(uri):X8}";

        return new SoundItem(soundId, name, IsSystem: false, Uri: uri);
    }

    private void StopCurrentPlayer()
    {
        MediaPlayer? player;
        string? tempFile;
        lock (_lock)
        {
            player = _currentPlayer;
            _currentPlayer = null;
            tempFile = _currentTempFile;
            _currentTempFile = null;
        }

        if (player != null)
        {
            try
            {
                if (player.IsPlaying) player.Stop();
                player.Release();
            }
            catch
            {
                // Player bereits freigegeben
            }
        }

        // Temp-Datei aufräumen
        if (tempFile != null)
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private void PlayTone(string soundId)
    {
        try
        {
            var (frequency, durationMs) = SoundDefinitions.GetToneParams(soundId);
            var wavData = WavGenerator.GenerateWav(frequency, durationMs);

            var tempFile = Path.Combine(
                Application.Context.CacheDir?.AbsolutePath ?? Path.GetTempPath(),
                $"zeitmanager_tone_{Guid.NewGuid():N}.wav");

            File.WriteAllBytes(tempFile, wavData);

            var player = new MediaPlayer();
            player.SetAudioAttributes(
                new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Alarm)!
                    .SetContentType(AudioContentType.Sonification)!
                    .Build()!);

            player.SetDataSource(tempFile);
            player.Prepare();

            StopCurrentPlayer();

            lock (_lock)
            {
                _currentPlayer = player;
                _currentTempFile = tempFile;
            }

            player.Completion += (_, _) =>
            {
                try
                {
                    player.Release();
                    File.Delete(tempFile);
                }
                catch { }

                lock (_lock)
                {
                    if (_currentPlayer == player)
                    {
                        _currentPlayer = null;
                        _currentTempFile = null;
                    }
                }
            };

            player.Start();
        }
        catch
        {
            // Audio-Fehler ignorieren
        }
    }

    /// <summary>
    /// Spielt einen Sound von einer content:// URI ab.
    /// </summary>
    private void PlayFromUri(string uri)
    {
        try
        {
            StopCurrentPlayer();

            var context = Application.Context;
            var player = new MediaPlayer();
            player.SetAudioAttributes(
                new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Alarm)!
                    .SetContentType(AudioContentType.Sonification)!
                    .Build()!);

            if (uri.StartsWith("content://") || uri.StartsWith("file://"))
            {
                var androidUri = global::Android.Net.Uri.Parse(uri)!;
                player.SetDataSource(context, androidUri);
            }
            else
            {
                // Lokaler Dateipfad
                player.SetDataSource(uri);
            }

            player.Prepare();

            lock (_lock)
            {
                _currentPlayer = player;
                _currentTempFile = null;
            }

            player.Completion += (_, _) =>
            {
                try { player.Release(); } catch { }
                lock (_lock)
                {
                    if (_currentPlayer == player)
                        _currentPlayer = null;
                }
            };

            player.Start();
        }
        catch
        {
            // URI-Wiedergabe fehlgeschlagen
        }
    }

    /// <summary>
    /// Lädt alle System-Ringtones (Alarm, Notification, Ringtone) via RingtoneManager.
    /// </summary>
    private static List<SoundItem> LoadSystemSounds()
    {
        var sounds = new List<SoundItem>();
        try
        {
            var context = Application.Context;
            var types = new[] { RingtoneType.Alarm, RingtoneType.Notification, RingtoneType.Ringtone };

            // Duplikate über URI vermeiden
            var seenUris = new HashSet<string>();

            foreach (var type in types)
            {
                var manager = new RingtoneManager(context);
                manager.SetType(type);

                var cursor = manager.Cursor;
                if (cursor == null) continue;

                while (cursor.MoveToNext())
                {
                    try
                    {
                        // Spalte 1 = Titel (TITLE_COLUMN_INDEX)
                        var title = cursor.GetString(1);
                        var uri = manager.GetRingtoneUri(cursor.Position);
                        if (uri == null || string.IsNullOrEmpty(title)) continue;

                        var uriStr = uri.ToString()!;
                        if (!seenUris.Add(uriStr)) continue;

                        var id = $"sys_{HashHelper.StableHash(uriStr):X8}";
                        sounds.Add(new SoundItem(id, title, IsSystem: true, Uri: uriStr));
                    }
                    catch
                    {
                        // Einzelnen Eintrag überspringen
                    }
                }
            }
        }
        catch
        {
            // System-Sound-Laden ist best-effort
        }

        return sounds;
    }

    /// <summary>
    /// Ermittelt den Namen eines Ringtones aus seiner URI.
    /// </summary>
    private static string? GetRingtoneName(string uri)
    {
        try
        {
            var context = Application.Context;
            var androidUri = global::Android.Net.Uri.Parse(uri);
            if (androidUri == null) return null;

            var ringtone = RingtoneManager.GetRingtone(context, androidUri);
            return ringtone?.GetTitle(context);
        }
        catch
        {
            return null;
        }
    }
}
