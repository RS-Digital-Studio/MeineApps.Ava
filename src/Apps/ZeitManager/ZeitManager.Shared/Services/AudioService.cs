using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ZeitManager.Audio;
using ZeitManager.Models;

namespace ZeitManager.Services;

public class AudioService : IAudioService
{
    private readonly object _lock = new();
    private CancellationTokenSource? _loopCts;

    // Desktop hat keine System-Ringtones
    private static readonly IReadOnlyList<SoundItem> EmptySystemSounds = [];
    // Benutzerdefinierte Sounds (über FilePicker hinzugefügt)
    private readonly List<SoundItem> _customSounds = [];
    // Cache für AvailableSounds (wird bei Änderungen invalidiert)
    private IReadOnlyList<SoundItem>? _cachedAvailableSounds;

    public IReadOnlyList<SoundItem> AvailableSounds
    {
        get
        {
            if (_cachedAvailableSounds == null)
            {
                var all = new List<SoundItem>(SoundDefinitions.BuiltInSounds);
                all.AddRange(_customSounds);
                _cachedAvailableSounds = all;
            }
            return _cachedAvailableSounds;
        }
    }

    public IReadOnlyList<SoundItem> SystemSounds => EmptySystemSounds;
    public string DefaultTimerSound => "default";
    public string DefaultAlarmSound => "default";

    public async Task PlayAsync(string soundId, bool loop = false)
    {
        Stop();

        // Prüfen ob es ein benutzerdefinierter Sound mit URI ist
        var custom = _customSounds.FirstOrDefault(s => s.Id == soundId);
        if (custom?.Uri != null)
        {
            await PlayUriAsync(custom.Uri, loop);
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

        if (!File.Exists(uri)) return;

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
                        PlaySoundFile(uri);
                        await Task.Delay(2000, token);
                    }
                }
                catch (OperationCanceledException) { }
            }, token);
        }
        else
        {
            await Task.Run(() => PlaySoundFile(uri));
        }
    }

    public void Stop()
    {
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

        if (OperatingSystem.IsWindows())
        {
            PlaySound(null, IntPtr.Zero, 0);
        }
    }

    public async Task<SoundItem?> PickSoundAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel == null) return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Sound auswählen",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Audio") { Patterns = ["*.wav", "*.mp3", "*.ogg"] }
                ]
            });

            if (files.Count == 0) return null;

            var file = files[0];
            var filePath = file.Path.LocalPath;
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // In App-Data kopieren für Persistenz
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ZeitManager", "Sounds");
            Directory.CreateDirectory(appDataDir);

            var destPath = Path.Combine(appDataDir, Path.GetFileName(filePath));
            if (filePath != destPath)
                File.Copy(filePath, destPath, overwrite: true);

            var soundId = $"custom_{fileName}_{Path.GetExtension(filePath).TrimStart('.')}";
            var sound = new SoundItem(soundId, fileName, IsSystem: false, Uri: destPath);

            // Duplikat vermeiden
            _customSounds.RemoveAll(s => s.Id == soundId);
            _customSounds.Add(sound);
            _cachedAvailableSounds = null; // Cache invalidieren

            return sound;
        }
        catch
        {
            return null;
        }
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime single)
            return TopLevel.GetTopLevel(single.MainView);
        return null;
    }

    private static void PlayTone(string soundId)
    {
        try
        {
            var (frequency, durationMs) = SoundDefinitions.GetToneParams(soundId);
            var wavData = WavGenerator.GenerateWav(frequency, durationMs);

            if (OperatingSystem.IsWindows())
            {
                PlaySound(wavData, IntPtr.Zero, SND_MEMORY | SND_SYNC | SND_NODEFAULT);
            }
            else if (OperatingSystem.IsLinux())
            {
                PlaySoundLinux(wavData);
            }
        }
        catch
        {
            // Audio-Fehler ignorieren
        }
    }

    private static void PlaySoundFile(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows: PlaySound mit Dateiname
                PlaySoundFile(filePath, IntPtr.Zero, SND_FILENAME | SND_SYNC | SND_NODEFAULT);
            }
            else if (OperatingSystem.IsLinux())
            {
                PlayFileLinux(filePath);
            }
        }
        catch
        {
            // Audio-Fehler ignorieren
        }
    }

    // Windows: PlaySound from winmm.dll
    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(byte[]? data, IntPtr hmod, uint fdwSound);

    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool PlaySoundFile(string? pszSound, IntPtr hmod, uint fdwSound);

    private const uint SND_SYNC = 0x0000;
    private const uint SND_MEMORY = 0x0004;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint SND_FILENAME = 0x00020000;

    // Linux: WAV in Temp-Datei und mit aplay/paplay abspielen
    private static void PlaySoundLinux(byte[] wavData)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"zeitmanager_tone_{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(tempFile, wavData);
        PlayFileLinux(tempFile);
        try { File.Delete(tempFile); } catch { }
    }

    private static void PlayFileLinux(string filePath)
    {
        string[] players = ["paplay", "aplay", "ffplay"];
        foreach (var player in players)
        {
            try
            {
                var args = player == "ffplay" ? $"-nodisp -autoexit \"{filePath}\"" : filePath;
                var psi = new ProcessStartInfo
                {
                    FileName = player,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(5000);
                    return;
                }
            }
            catch
            {
                continue;
            }
        }
    }
}
