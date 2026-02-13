namespace ZeitManager.Audio;

/// <summary>
/// Generiert PCM WAV-Daten in-memory. Gemeinsam genutzt von Desktop und Android AudioService.
/// </summary>
public static class WavGenerator
{
    /// <summary>
    /// Generiert eine PCM WAV-Datei im Speicher mit einem Sinuston.
    /// </summary>
    public static byte[] GenerateWav(int frequency, int durationMs)
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
        bw.Write(16);
        bw.Write((short)1);    // PCM
        bw.Write((short)1);    // Mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);

        // data Sub-Chunk
        bw.Write((byte)'d'); bw.Write((byte)'a'); bw.Write((byte)'t'); bw.Write((byte)'a');
        bw.Write(dataSize);

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double amplitude = 0.5;

            if (i >= samples - fadeOut)
                amplitude *= (double)(samples - i) / fadeOut;

            short sample = (short)(Math.Sin(2 * Math.PI * frequency * t) * short.MaxValue * amplitude);
            bw.Write(sample);
        }

        return ms.ToArray();
    }
}
