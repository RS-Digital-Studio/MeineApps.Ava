// Plattformneutraler, deterministischer PRNG (Mulberry32) — BIT-IDENTISCHE Portierung von
// Unity/Assets/_Project/Scripts/Domain/Battle/DeterministicRng.cs.
//
// Bewusst NICHT Math.random oder ein 53-bit-Generator: derselbe Seed muss auf Client (C#)
// und Server (dieser Code) exakt dieselbe Sequenz erzeugen, sonst divergiert der Anti-Cheat-
// Replay und legitime Spieler wuerden faelschlich abgelehnt.
//
// Referenz-Formel (identisch in der C#-Datei, dort als Kommentar dokumentiert):
//   state = (state + 0x6D2B79F5) | 0
//   t = imul(state ^ (state >>> 15), 1 | state)
//   t = (t + imul(t ^ (t >>> 7), 61 | t)) ^ t
//   result = (t ^ (t >>> 14)) >>> 0
//
// Wichtig zur Determinismus-Treue:
//   - C# rechnet in `unchecked uint`. In JS bilden wir 32-bit-Wrap ueber Math.imul (32-bit-Mul)
//     und `>>> 0` (zu uint32) bzw. `| 0` (zu int32) nach.
//   - C#-`* (Multiplikation zweier uint)` ueberlaeuft modulo 2^32 — exakt das tut Math.imul.

export class DeterministicRng {
  // 32-Bit-Zustand, als JS-Number im uint32-Bereich [0, 2^32) gehalten.
  private state: number;

  /**
   * Initialisiert den Generator. Der C#-Ctor nimmt `int seed` und castet via `unchecked((uint)seed)`.
   * `seed >>> 0` bildet exakt diese Zwei-Komplement-Reinterpretation eines int32 zu uint32 nach.
   */
  constructor(seed: number) {
    // `| 0` zwingt zuerst auf int32 (entspricht dem C#-int-Parameter), `>>> 0` dann auf uint32
    // (entspricht `(uint)seed`). Damit verhalten sich negative Seeds identisch zu C#.
    this.state = (seed | 0) >>> 0;
  }

  /** Naechster 32-Bit-Wert (0 .. 2^32-1). Bit-identisch zur C#-Implementierung. */
  nextUInt(): number {
    // state += 0x6D2B79F5;  (uint-Overflow -> >>> 0)
    this.state = (this.state + 0x6d2b79f5) >>> 0;
    let t = this.state;
    // t = (t ^ (t >>> 15)) * (1 | t);
    t = Math.imul(t ^ (t >>> 15), 1 | t);
    // t = (t + (t ^ (t >>> 7)) * (61 | t)) ^ t;
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    // return t ^ (t >>> 14);   (als uint32)
    return (t ^ (t >>> 14)) >>> 0;
  }

  /**
   * Ganzzahl in [0, maxExclusive). Identische Formel wie C# (Modulo auf uint).
   * C#: `(int)(NextUInt() % (uint)maxExclusive)`.
   */
  next(maxExclusive: number): number {
    if (maxExclusive <= 0) return 0;
    // `>>> 0` haelt beide Operanden im uint32-Bereich; das Ergebnis ist < maxExclusive (<= int32-Max)
    // und damit als regulaere JS-Number exakt darstellbar.
    return (this.nextUInt() % (maxExclusive >>> 0)) | 0;
  }

  /** Gleitkommazahl in [0, 1) — fuer Wahrscheinlichkeits-Rolls. C#: NextUInt() / 2^32. */
  nextDouble(): number {
    return this.nextUInt() / 4294967296.0;
  }
}
