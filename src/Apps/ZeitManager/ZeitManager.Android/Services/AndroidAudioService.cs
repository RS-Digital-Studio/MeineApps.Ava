using Android.App;
using Android.Media;
using ZeitManager.Models;
using ZeitManager.Services;

namespace ZeitManager.Android.Services;

/// <summary>
/// Android-spezifischer AudioService - nutzt MediaPlayer fuer Tonwiedergabe.
/// Generiert WAV-Daten in-memory (gleiche Toene wie Desktop) und spielt sie via MediaPlayer ab.
/// </summary>
public class AndroidAudioService : IAudioService
{
    private readonly object _lock = new();
    private CancellationTokenSource? _loopCts;
    private MediaPlayer? _currentPlayer;

    private static readonly List<SoundItem> _sounds =
    [
        new("default", "Default Beep"),
        new("alert_high", "Alert High"),
        new("alert_low", "Alert Low"),
        new("chime", "Chime"),
        new("bell", "Bell"),
        new("digital", "Digital"),
    ];

    public IReadOnlyList<SoundItem> AvailableSounds => _sounds;
    public string DefaultTimerSound => "default";
    public string DefaultAlarmSound => "default";

    public async Task PlayAsync(string soundId, bool loop = false)
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

    private void StopCurrentPlayer()
    {
        MediaPlayer? player;
        lock (_lock)
        {
            player = _currentPlayer;
            _currentPlayer = null;
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
    }

    private void PlayTone(string soundId)
    {
        try
        {
            var (frequency, durationMs) = soundId switch
            {
                "alert_high" => (1200, 300),
                "alert_low" => (600, 500),
                "chime" => (880, 200),
                "bell" => (1000, 400),
                "digital" => (1500, 150),
                _ => (800, 300) // default
            };

            var wavData = GenerateWav(frequency, durationMs);

            // WAV in temporaere Datei schreiben und mit MediaPlayer abspielen
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

            // Alten Player stoppen
            StopCurrentPlayer();

            lock (_lock)
            {
                _currentPlayer = player;
            }

            player.Completion += (_, _) =>
            {
                try
                {
                    player.Release();
                    File.Delete(tempFile);
                }
                catch
                {
                    // Cleanup-Fehler ignorieren
                }

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
            // Audio-Fehler ignorieren
        }
    }

    /// <summary>
    /// Generiert PCM WAV-Daten in-memory mit einem Sinuston (gleich wie Desktop AudioService).
    /// </summary>
    private static byte[] GenerateWav(int frequency, int durationMs)
    {
        const int sampleRate = 44100;
        int samples = sampleRate * durationMs / 1000;
        int dataSize = samples * 2; // 16-bit mono
        int fadeOut = Math.Min(samples / 10, sampleRate / 20); // ~50ms Fade

        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms);

        // RIFF Header
        bw.Write((byte)'R'); bw.Write((byte)'I'); bw.Write((byte)'F'); bw.Write((byte)'F');
        bw.Write(36 + dataSize);
        bw.Write((byte)'W'); bw.Write((byte)'A'); bw.Write((byte)'V'); bw.Write((byte)'E');

        // fmt Sub-Chunk
        bw.Write((byte)'f'); bw.Write((byte)'m'); bw.Write((byte)'t'); bw.Write((byte)' ');
        bw.Write(16);          // Chunk-Groesse
        bw.Write((short)1);    // PCM Format
        bw.Write((short)1);    // Mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2); // Byte Rate
        bw.Write((short)2);    // Block Align
        bw.Write((short)16);   // Bits pro Sample

        // data Sub-Chunk
        bw.Write((byte)'d'); bw.Write((byte)'a'); bw.Write((byte)'t'); bw.Write((byte)'a');
        bw.Write(dataSize);

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double amplitude = 0.5;

            // Fade-Out am Ende gegen Klick-Artefakte
            if (i >= samples - fadeOut)
                amplitude *= (double)(samples - i) / fadeOut;

            short sample = (short)(Math.Sin(2 * Math.PI * frequency * t) * short.MaxValue * amplitude);
            bw.Write(sample);
        }

        return ms.ToArray();
    }
}
