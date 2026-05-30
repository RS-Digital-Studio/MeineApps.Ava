#nullable enable

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Plattformneutraler, deterministischer PRNG (Mulberry32). Bewusst NICHT System.Random:
    /// dessen Algorithmus ist weder ueber .NET-Versionen garantiert stabil noch in JavaScript
    /// reproduzierbar. Mulberry32 ist ein klar definierter 32-Bit-Generator, der in C# und in der
    /// serverseitigen TypeScript-Portierung (Cloud-Function Anti-Cheat-Replay) BIT-IDENTISCH
    /// implementiert ist — gleicher Seed ergibt auf Client und Server exakt dieselbe Sequenz.
    ///
    /// Referenz-Implementierung (muss mit Server/CloudFunctions/src/battle/deterministicRng.ts uebereinstimmen):
    ///   state = (state + 0x6D2B79F5) | 0
    ///   t = imul(state ^ (state >>> 15), 1 | state)
    ///   t = (t + imul(t ^ (t >>> 7), 61 | t)) ^ t
    ///   result = (t ^ (t >>> 14)) >>> 0
    /// </summary>
    public sealed class DeterministicRng
    {
        private uint _state;

        public DeterministicRng(int seed)
        {
            _state = unchecked((uint)seed);
        }

        /// <summary>Naechster 32-Bit-Wert (0 .. 2^32-1).</summary>
        public uint NextUInt()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                uint t = _state;
                t = (t ^ (t >> 15)) * (1u | t);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                return t ^ (t >> 14);
            }
        }

        /// <summary>Ganzzahl in [0, maxExclusive). Identische Formel auf C#- und TS-Seite (Modulo).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        /// <summary>Gleitkommazahl in [0, 1) — fuer Wahrscheinlichkeits-Rolls.</summary>
        public double NextDouble()
        {
            return NextUInt() / 4294967296.0;   // / 2^32
        }
    }
}
