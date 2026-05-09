namespace BomberBlast.Core;

/// <summary>
/// RNG-Abstraktion (Phase 18d — AAA-Audit E4).
///
/// <para>Erlaubt Engine-Code, mit System.Random ODER <see cref="DeterministicRandom"/> zu arbeiten
/// ohne Code-Verzweigung. Variable-Timestep-Mode: System.Random-Wrapper für Performance + Bestand-Verhalten.
/// FixedTimestep-Mode: DeterministicRandom-Wrapper für Replay/Anti-Cheat-Determinismus.</para>
///
/// <para>Wird von <see cref="GameEngine"/> als Field gehalten und beim Mode-Switch zwischen
/// den Implementationen umgeschaltet. Pure Hot-Path-fähig (keine Heap-Allokation pro Roll).</para>
/// </summary>
public interface IRngProvider
{
    /// <summary>Liefert Int [0, max).</summary>
    int Next(int max);

    /// <summary>Liefert Int [min, max).</summary>
    int Next(int min, int max);

    /// <summary>Liefert double [0, 1).</summary>
    double NextDouble();
}

/// <summary>System.Random-Wrapper (Variable-Timestep-Mode, Default).</summary>
public sealed class SystemRngProvider : IRngProvider
{
    private readonly Random _random;
    public SystemRngProvider() { _random = new Random(); }
    public SystemRngProvider(int seed) { _random = new Random(seed); }
    public int Next(int max) => max <= 0 ? 0 : _random.Next(max);
    public int Next(int min, int max) => max <= min ? min : _random.Next(min, max);
    public double NextDouble() => _random.NextDouble();
}

/// <summary>
/// DeterministicRandom-Wrapper (FixedTimestep-Mode + Replay/Anti-Cheat-Pfade).
/// Bei identischem Seed liefert dieser Wrapper plattform-unabhängig dieselbe Sequenz.
/// </summary>
public sealed class DeterministicRngProvider : IRngProvider
{
    private readonly DeterministicRandom _random;
    public DeterministicRngProvider(ulong seed) { _random = new DeterministicRandom(seed); }
    public int Next(int max) => _random.Next(max);
    public int Next(int min, int max) => _random.Next(min, max);
    public double NextDouble() => _random.NextDouble();

    /// <summary>State-Snapshot für Replay-Persistenz.</summary>
    public (ulong, ulong, ulong, ulong) GetState() => _random.GetState();
    public void SetState(ulong s0, ulong s1, ulong s2, ulong s3) => _random.SetState(s0, s1, s2, s3);
}
