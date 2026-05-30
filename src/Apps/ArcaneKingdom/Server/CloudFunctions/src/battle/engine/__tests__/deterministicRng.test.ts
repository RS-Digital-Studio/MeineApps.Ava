// Regressions-Tests fuer den Mulberry32-PRNG (deterministicRng.ts).
//
// Zweck: sicherstellen, dass die TS-Portierung BIT-IDENTISCH zur C#-Referenz arbeitet. Der RNG
// ist das Fundament des gesamten Replays — weicht er ab, divergiert alles.
//
// Strategie:
//   1. Eine UNABHAENGIGE Inline-Referenz (referenceMulberry32) bildet exakt die im C#-Kommentar
//      dokumentierte Formel ab. Der Test prueft, dass die Produktiv-Klasse mit dieser Referenz
//      ueber viele Seeds/Iterationen uebereinstimmt — fixiert also die Formel gegen Regression.
//   2. Konkrete erwartete Werte sind zusaetzlich als Anker hartkodiert (aus der Formel hergeleitet).
//      Diese Anker MUESSEN identisch sein zu dem, was die C#-DeterministicRng fuer dieselben Seeds
//      liefert (Cross-Test gegen C# steht noch aus — siehe Abschlussbericht).

import { DeterministicRng } from "../deterministicRng";

/**
 * Unabhaengige Referenz-Implementierung der im C#-Kommentar dokumentierten Mulberry32-Formel.
 * Bewusst eigenstaendig (kein Import der Produktiv-Logik), damit der Test echte Aussagekraft hat.
 */
function referenceMulberry32(seed: number): () => number {
  let state = (seed | 0) >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), 1 | t);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return (t ^ (t >>> 14)) >>> 0;
  };
}

describe("DeterministicRng (Mulberry32)", () => {
  const seeds = [0, 1, 12345, -1, 2147483647, -2147483648, 999999937];

  test("stimmt ueber viele Seeds/Iterationen mit der Referenz-Formel ueberein", () => {
    for (const seed of seeds) {
      const rng = new DeterministicRng(seed);
      const ref = referenceMulberry32(seed);
      for (let i = 0; i < 1000; i++) {
        expect(rng.nextUInt()).toBe(ref());
      }
    }
  });

  test("liefert die fixierten Regressions-Anker (aus der C#-Referenz-Formel hergeleitet)", () => {
    // Diese Werte MUESSEN identisch zu C#-DeterministicRng(seed).NextUInt()-Sequenzen sein.
    const anchors: Record<number, number[]> = {
      0: [1144304738, 1416247, 958946056, 627933444, 2007157716],
      1: [2693262067, 11749833, 2265367787, 4213581821, 4159151403],
      12345: [4207900869, 1317490944, 2079646450, 3513001552, 2187978186],
      [-1]: [3850105811, 813802916, 3073704848, 4054706436, 3630262831],
      2147483647: [1842962257, 546041740, 1654754255, 1702490205, 513796057],
    };

    for (const [seedStr, expected] of Object.entries(anchors)) {
      const rng = new DeterministicRng(Number(seedStr));
      const actual = Array.from({ length: expected.length }, () => rng.nextUInt());
      expect(actual).toEqual(expected);
    }
  });

  test("nextUInt liefert ausschliesslich uint32-Werte (0 .. 2^32-1)", () => {
    const rng = new DeterministicRng(0xc0ffee);
    for (let i = 0; i < 5000; i++) {
      const v = rng.nextUInt();
      expect(Number.isInteger(v)).toBe(true);
      expect(v).toBeGreaterThanOrEqual(0);
      expect(v).toBeLessThanOrEqual(0xffffffff);
    }
  });

  test("next(maxExclusive) bleibt im Bereich und ist deterministisch", () => {
    const rng = new DeterministicRng(42);
    // Aus der Formel hergeleitete Anker fuer next(6).
    expect([0, 1, 2, 3, 4, 5, 6, 7].map(() => rng.next(6))).toEqual([0, 4, 0, 5, 0, 3, 4, 5]);

    // next(0) und next(-1) muessen 0 liefern (C#: maxExclusive <= 0 -> 0).
    const rng2 = new DeterministicRng(7);
    expect(rng2.next(0)).toBe(0);
    expect(rng2.next(-5)).toBe(0);
  });

  test("nextDouble liegt in [0, 1)", () => {
    const rng = new DeterministicRng(2024);
    for (let i = 0; i < 5000; i++) {
      const d = rng.nextDouble();
      expect(d).toBeGreaterThanOrEqual(0);
      expect(d).toBeLessThan(1);
    }
  });

  test("identischer Seed erzeugt identische Sequenz (Reproduzierbarkeit)", () => {
    const a = new DeterministicRng(13371337);
    const b = new DeterministicRng(13371337);
    for (let i = 0; i < 200; i++) {
      expect(a.nextUInt()).toBe(b.nextUInt());
    }
  });
});
