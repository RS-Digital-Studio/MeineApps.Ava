namespace BomberBlast.Core;

/// <summary>
/// Deterministischer Random-Generator (Phase 18b — AAA-Audit E4).
///
/// <para>Implementierung: <see href="https://prng.di.unimi.it/xoshiro256plus.c">xoshiro256+</see> —
/// Public Domain (Sebastiano Vigna). Schneller, statistisch hochwertiger PRNG mit 256-Bit-State.
/// Voraussetzung für Replay/Anti-Cheat/Async-PvP — gleicher Seed → identische Ausgabe-Sequenz auf
/// allen Plattformen (im Gegensatz zu .NET's <see cref="Random"/> der je nach Runtime variieren kann).</para>
///
/// <para>Performance: ~3-5× schneller als <see cref="Random"/> für int-Range. Kein Heap-Allocation
/// pro Roll. Thread-unsicher per Default — pro Sim-Tick eine eigene Instanz reicht.</para>
///
/// <para>Determinismus-Garantie: Bei identischem Seed liefert der Generator auf x64/ARM64 dieselben
/// 64-Bit-Outputs Bit für Bit. Wird durch <see cref="DeterministicRandomTests"/> verifiziert.</para>
/// </summary>
public sealed class DeterministicRandom
{
    // 256-Bit-State (vier 64-Bit-Worte)
    private ulong _s0, _s1, _s2, _s3;

    /// <summary>Erzeugt einen neuen Generator mit dem gegebenen Seed.</summary>
    public DeterministicRandom(ulong seed)
    {
        // SplitMix64 als Seed-Expander — verhindert dass kleine Seeds (z.B. 0/1)
        // zu schwachen Initial-States führen.
        _s0 = SplitMix64(ref seed);
        _s1 = SplitMix64(ref seed);
        _s2 = SplitMix64(ref seed);
        _s3 = SplitMix64(ref seed);
    }

    /// <summary>Aktueller Seed-State (für Replay-Speicherung).</summary>
    public (ulong s0, ulong s1, ulong s2, ulong s3) GetState() => (_s0, _s1, _s2, _s3);

    /// <summary>Setzt den State explizit (für Replay-Wiederherstellung).</summary>
    public void SetState(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;
    }

    /// <summary>SplitMix64 — Public-Domain-Seed-Mischer.</summary>
    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        var z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Liefert einen 64-Bit-Output (xoshiro256+).</summary>
    public ulong NextUInt64()
    {
        // xoshiro256+ ist die "+"-Variante (statt "**"). Bei "+" ist der return-Wert _s0 + _s3,
        // wobei die niedrigsten 3 Bits etwas schwächer sind — wir verwerfen sie für int-Range nicht,
        // aber für sensible bit-Tests sollte man die oberen 53 Bits nehmen.
        var result = _s0 + _s3;

        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;

        _s2 ^= t;
        _s3 = (_s3 << 45) | (_s3 >> 19); // rotl(_s3, 45)

        return result;
    }

    /// <summary>Liefert einen Int im Bereich [0, max). Für Random-Choices in Engine-Logik.</summary>
    public int Next(int max)
    {
        if (max <= 0) return 0;
        // Verwende obere 53 Bits für statistische Qualität.
        var top = NextUInt64() >> 11;  // 53 Bits
        return (int)(top % (ulong)max);
    }

    /// <summary>Liefert einen Int im Bereich [min, max).</summary>
    public int Next(int min, int max)
    {
        if (max <= min) return min;
        return min + Next(max - min);
    }

    /// <summary>Liefert ein double in [0.0, 1.0).</summary>
    public double NextDouble()
    {
        // Standard-Pattern für x64-Double: nutze obere 53 Bits + 2^-53.
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>Liefert ein float in [0.0, 1.0).</summary>
    public float NextSingle() => (float)NextDouble();

    /// <summary>Coin-Flip — true mit Wahrscheinlichkeit 0.5.</summary>
    public bool NextBool() => (NextUInt64() & 1UL) != 0;
}
