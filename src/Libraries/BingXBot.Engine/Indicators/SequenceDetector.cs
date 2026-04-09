using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// SK-System Sequenz-Erkennung: Identifiziert A-B-C Muster, BOS, ChoCH, GKL und IKI.
/// Basiert auf Fractal-Swing-Erkennung und Fibonacci-Level-Validierung.
/// </summary>
public static class SequenceDetector
{
    // ═══════════════════════════════════════════════════════════════
    // Swing-Punkt-Erkennung (Fractal-basiert)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Findet alle Swing-Highs und Swing-Lows in den Candle-Daten.
    /// Ein Swing-High ist ein High das höher ist als die N Candles links UND rechts.
    /// </summary>
    public static List<SwingPoint> FindSwingPoints(IReadOnlyList<Candle> candles, int strength = 5)
    {
        var swings = new List<SwingPoint>();
        if (candles.Count < strength * 2 + 1) return swings;

        for (int i = strength; i < candles.Count - strength; i++)
        {
            var isHigh = true;
            var isLow = true;
            var high = candles[i].High;
            var low = candles[i].Low;

            for (int j = 1; j <= strength; j++)
            {
                if (candles[i - j].High >= high || candles[i + j].High >= high)
                    isHigh = false;
                if (candles[i - j].Low <= low || candles[i + j].Low <= low)
                    isLow = false;
                if (!isHigh && !isLow) break;
            }

            if (isHigh)
                swings.Add(new SwingPoint(high, i, candles[i].CloseTime, true));
            if (isLow)
                swings.Add(new SwingPoint(low, i, candles[i].CloseTime, false));
        }

        // Chronologisch sortieren (nach Candle-Index)
        swings.Sort((a, b) => a.CandleIndex.CompareTo(b.CandleIndex));
        return swings;
    }

    // ═══════════════════════════════════════════════════════════════
    // A-B-C Sequenz-Erkennung
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Erkennt die aktuellste gültige A-B-C Sequenz.
    /// Sucht von rechts (neueste) nach links durch die Swing-Punkte.
    /// </summary>
    /// <param name="candles">Candle-Daten (mindestens 50 Candles).</param>
    /// <param name="swingStrength">Fractal-Stärke (3=schnell/mehr Swings, 7=langsam/weniger).</param>
    /// <param name="minRangePercent">Minimale A→B Range in % des Preises (filtert Noise).</param>
    /// <param name="requireCloseBreak">True=Close über B für Aktivierung, False=Wick reicht.</param>
    public static Sequence? DetectSequence(
        IReadOnlyList<Candle> candles,
        int swingStrength = 5,
        decimal minRangePercent = 0.5m,
        bool requireCloseBreak = true)
    {
        if (candles.Count < 50) return null;

        var swings = FindSwingPoints(candles, swingStrength);
        if (swings.Count < 3) return null;

        var currentPrice = candles[^1].Close;

        // Von rechts nach links: Neueste Sequenz zuerst finden
        // Versuche Long- UND Short-Sequenz, nimm die bessere
        var longSeq = FindBestSequence(swings, candles, currentPrice, true, minRangePercent, requireCloseBreak);
        var shortSeq = FindBestSequence(swings, candles, currentPrice, false, minRangePercent, requireCloseBreak);

        // Beste Sequenz: Die aktuellere (neuerer Punkt A) bevorzugen
        if (longSeq == null) return shortSeq;
        if (shortSeq == null) return longSeq;
        return longSeq.PointA.CandleIndex > shortSeq.PointA.CandleIndex ? longSeq : shortSeq;
    }

    /// <summary>Erkennt ALLE gültigen Sequenzen (für verschachtelte Analyse).</summary>
    public static List<Sequence> DetectAllSequences(
        IReadOnlyList<Candle> candles,
        int swingStrength = 5,
        decimal minRangePercent = 0.3m)
    {
        var sequences = new List<Sequence>();
        if (candles.Count < 50) return sequences;

        var swings = FindSwingPoints(candles, swingStrength);
        var currentPrice = candles[^1].Close;

        // Alle möglichen A-B-C Tripel finden (Long + Short)
        foreach (var isLong in new[] { true, false })
        {
            var aSwings = swings.Where(s => isLong ? !s.IsHigh : s.IsHigh).ToList();
            var bSwings = swings.Where(s => isLong ? s.IsHigh : !s.IsHigh).ToList();

            foreach (var a in aSwings)
            {
                foreach (var b in bSwings.Where(b => b.CandleIndex > a.CandleIndex))
                {
                    var range = Math.Abs(b.Price - a.Price);
                    var midPrice = (a.Price + b.Price) / 2;
                    if (midPrice == 0 || range / midPrice * 100 < minRangePercent) continue;

                    // Punkt C suchen
                    var cSwings = swings.Where(s =>
                        s.CandleIndex > b.CandleIndex &&
                        (isLong ? !s.IsHigh : s.IsHigh)).ToList();

                    foreach (var c in cSwings)
                    {
                        var retLevel = GetRetracementLevel(a.Price, b.Price, c.Price, isLong);
                        if (retLevel >= 0.382m && retLevel <= 0.786m)
                        {
                            var seq = BuildSequence(a, b, c, isLong, currentPrice, false);
                            if (seq != null) sequences.Add(seq);
                            break; // Nur den ersten gültigen C pro A-B Paar
                        }
                    }
                }
            }
        }

        // Nach Range sortieren (größte = übergeordnete zuerst)
        sequences.Sort((a, b) => b.Range.CompareTo(a.Range));
        return sequences;
    }

    // ═══════════════════════════════════════════════════════════════
    // IKI — Interne Korrektur-Sequenz
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Sucht interne Korrektur-Sequenzen (IKI) innerhalb der B→C Korrektur einer größeren Sequenz.
    /// Kleinere Swing-Stärke (3 statt 5) um feinere Muster zu erkennen.
    /// </summary>
    public static List<Sequence> DetectIKI(
        IReadOnlyList<Candle> candles,
        Sequence parentSequence,
        int innerSwingStrength = 3)
    {
        if (parentSequence.PointC == null) return new();

        // Nur den Bereich B→C der übergeordneten Sequenz analysieren
        var startIdx = parentSequence.PointB.CandleIndex;
        var endIdx = parentSequence.PointC.CandleIndex;
        if (endIdx <= startIdx || endIdx >= candles.Count) return new();

        var subCandles = candles.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
        if (subCandles.Count < innerSwingStrength * 2 + 3) return new();

        var ikiSequences = DetectAllSequences(subCandles, innerSwingStrength, 0.2m);

        // IKI-Flag setzen und Parent-Referenz
        return ikiSequences.Select(s => new Sequence
        {
            PointA = s.PointA with { CandleIndex = s.PointA.CandleIndex + startIdx },
            PointB = s.PointB with { CandleIndex = s.PointB.CandleIndex + startIdx },
            PointC = s.PointC != null ? s.PointC with { CandleIndex = s.PointC.CandleIndex + startIdx } : null,
            IsLong = s.IsLong, State = s.State,
            Retracement382 = s.Retracement382, Retracement500 = s.Retracement500,
            Retracement559 = s.Retracement559, Retracement618 = s.Retracement618,
            Retracement667 = s.Retracement667, Retracement786 = s.Retracement786,
            Extension100 = s.Extension100, Extension1272 = s.Extension1272,
            Extension1618 = s.Extension1618, Extension200 = s.Extension200, Extension2618 = s.Extension2618,
            ParentSequence = parentSequence,
            IsIKI = true
        }).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // BOS (Break of Structure) + ChoCH (Change of Character)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Break of Structure: Erkennt ob ein signifikantes Swing-Level durchbrochen wurde.
    /// Bullisch: Preis macht ein Higher High (durchbricht letztes Swing-High).
    /// Bärisch: Preis macht ein Lower Low (durchbricht letztes Swing-Low).
    /// </summary>
    public static StructureBreak? DetectBOS(IReadOnlyList<Candle> candles, List<SwingPoint> swings)
    {
        if (swings.Count < 2 || candles.Count == 0) return null;

        var currentPrice = candles[^1].Close;
        var currentHigh = candles[^1].High;
        var currentLow = candles[^1].Low;

        // Letztes Swing-High und Swing-Low finden
        var lastHigh = swings.LastOrDefault(s => s.IsHigh);
        var lastLow = swings.LastOrDefault(s => !s.IsHigh);

        // Bullischer Break: Aktueller Preis > letztes Swing-High
        if (lastHigh != null && currentHigh > lastHigh.Price)
            return new StructureBreak(lastHigh, true, candles[^1].CloseTime);

        // Bärischer Break: Aktueller Preis < letztes Swing-Low
        if (lastLow != null && currentLow < lastLow.Price)
            return new StructureBreak(lastLow, false, candles[^1].CloseTime);

        return null;
    }

    /// <summary>
    /// Change of Character: Erkennt Trendwechsel.
    /// Bullisch→Bärisch: Erster Lower-Low nach einer Serie von Higher-Lows.
    /// Bärisch→Bullisch: Erstes Higher-High nach einer Serie von Lower-Highs.
    /// </summary>
    public static CharacterChange? DetectChoCH(List<SwingPoint> swings)
    {
        if (swings.Count < 4) return null;

        // Separate Listen für Highs und Lows
        var highs = swings.Where(s => s.IsHigh).TakeLast(5).ToList();
        var lows = swings.Where(s => !s.IsHigh).TakeLast(5).ToList();

        // Prüfe ob letzte 3+ Lows steigend waren (Uptrend) und das neueste fällt (ChoCH)
        if (lows.Count >= 3)
        {
            var isRising = true;
            for (int i = 1; i < lows.Count - 1; i++)
            {
                if (lows[i].Price <= lows[i - 1].Price) { isRising = false; break; }
            }
            // Letzes Low ist niedriger als vorletztes → ChoCH (Bullish→Bearish)
            if (isRising && lows[^1].Price < lows[^2].Price)
                return new CharacterChange(true, lows[^1], lows[^1].Time);
        }

        // Prüfe ob letzte 3+ Highs fallend waren (Downtrend) und das neueste steigt
        if (highs.Count >= 3)
        {
            var isFalling = true;
            for (int i = 1; i < highs.Count - 1; i++)
            {
                if (highs[i].Price >= highs[i - 1].Price) { isFalling = false; break; }
            }
            // Letztes High ist höher als vorletztes → ChoCH (Bearish→Bullish)
            if (isFalling && highs[^1].Price > highs[^2].Price)
                return new CharacterChange(false, highs[^1], highs[^1].Time);
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════════
    // GKL (Gesamtkorrekturlevel)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet das Gesamtkorrekturlevel (GKL) der übergeordneten Struktur.
    /// GKL = 55.9%, 61.8%, 66.7% Retracement der gesamten Bewegung.
    /// Wird typischerweise auf HTF-Candles angewendet.
    /// </summary>
    public static (decimal Gkl559, decimal Gkl618, decimal Gkl667, bool IsUptrend, decimal SwingHigh, decimal SwingLow)?
        CalculateGKL(IReadOnlyList<Candle> candles, int swingStrength = 7)
    {
        if (candles.Count < swingStrength * 2 + 10) return null;

        var swings = FindSwingPoints(candles, swingStrength);
        if (swings.Count < 2) return null;

        // Letztes signifikantes Swing-Paar finden
        var lastHigh = swings.LastOrDefault(s => s.IsHigh);
        var lastLow = swings.LastOrDefault(s => !s.IsHigh);
        if (lastHigh == null || lastLow == null) return null;

        var range = lastHigh.Price - lastLow.Price;
        if (range <= 0) return null;

        // Trend-Richtung: High kommt NACH Low = Uptrend
        var isUptrend = lastHigh.CandleIndex > lastLow.CandleIndex;

        decimal gkl559, gkl618, gkl667;
        if (isUptrend)
        {
            // Uptrend-GKL: Retracement von oben nach unten
            gkl559 = lastHigh.Price - range * 0.559m;
            gkl618 = lastHigh.Price - range * 0.618m;
            gkl667 = lastHigh.Price - range * 0.667m;
        }
        else
        {
            // Downtrend-GKL: Retracement von unten nach oben
            gkl559 = lastLow.Price + range * 0.559m;
            gkl618 = lastLow.Price + range * 0.618m;
            gkl667 = lastLow.Price + range * 0.667m;
        }

        return (gkl559, gkl618, gkl667, isUptrend, lastHigh.Price, lastLow.Price);
    }

    /// <summary>Prüft ob ein Preis im GKL-Bereich liegt (55.9-66.7%).</summary>
    public static bool IsInGKL(decimal price, decimal gkl559, decimal gkl667)
    {
        var min = Math.Min(gkl559, gkl667);
        var max = Math.Max(gkl559, gkl667);
        return price >= min && price <= max;
    }

    // ═══════════════════════════════════════════════════════════════
    // State-Update
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert den Zustand einer Sequenz basierend auf dem aktuellen Preis.
    /// </summary>
    public static SequenceState UpdateState(Sequence seq, decimal currentPrice, decimal currentClose, bool useCloseBreak)
    {
        // Invalidiert: Preis unter A (Long) oder über A (Short)
        if (seq.IsInvalidated(currentPrice))
            return SequenceState.Invalidated;

        // Zielzone erreicht
        if (seq.HasReachedTarget(currentPrice))
            return SequenceState.TargetReached;

        // Aktiviert: B durchbrochen
        var breakPrice = useCloseBreak ? currentClose : currentPrice;
        var bBroken = seq.IsLong ? breakPrice > seq.PointB.Price : breakPrice < seq.PointB.Price;
        if (bBroken)
            return SequenceState.Active;

        // Punkt C noch nicht gebildet
        if (seq.PointC == null)
        {
            // Im Retracement-Bereich (38.2-78.6%)?
            var retLevel = GetRetracementLevel(seq.PointA.Price, seq.PointB.Price, currentPrice, seq.IsLong);
            if (retLevel >= 0.382m && retLevel <= 0.786m)
                return SequenceState.CorrectionZone;
            return SequenceState.Forming;
        }

        // C gebildet, wartet auf B-Break
        return SequenceState.WaitingBreak;
    }

    // ═══════════════════════════════════════════════════════════════
    // Private Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Berechnet das Retracement-Level eines Preises relativ zu A→B (0.0 = B, 1.0 = A).</summary>
    private static decimal GetRetracementLevel(decimal a, decimal b, decimal price, bool isLong)
    {
        var range = Math.Abs(b - a);
        if (range == 0) return 0;
        return isLong
            ? (b - price) / range    // Long: B=High, Retracement geht runter
            : (price - b) / range;   // Short: B=Low, Retracement geht hoch
    }

    /// <summary>Sucht die beste A-B-C Sequenz in einer bestimmten Richtung.</summary>
    private static Sequence? FindBestSequence(
        List<SwingPoint> swings, IReadOnlyList<Candle> candles, decimal currentPrice,
        bool isLong, decimal minRangePercent, bool requireCloseBreak)
    {
        // A-Kandidaten (Long: Lows, Short: Highs) — von rechts nach links
        var aSwings = swings.Where(s => isLong ? !s.IsHigh : s.IsHigh).Reverse().ToList();
        var bSwings = swings.Where(s => isLong ? s.IsHigh : !s.IsHigh).ToList();

        foreach (var a in aSwings)
        {
            // B suchen: Nächster entgegengesetzter Swing NACH A
            var b = bSwings.FirstOrDefault(s => s.CandleIndex > a.CandleIndex);
            if (b == null) continue;

            // Range-Check
            var range = Math.Abs(b.Price - a.Price);
            var midPrice = (a.Price + b.Price) / 2;
            if (midPrice == 0 || range / midPrice * 100 < minRangePercent) continue;

            // C suchen: Nächster Swing in A-Richtung NACH B
            var cCandidates = swings.Where(s =>
                s.CandleIndex > b.CandleIndex &&
                (isLong ? !s.IsHigh : s.IsHigh)).ToList();

            SwingPoint? bestC = null;
            foreach (var c in cCandidates)
            {
                var retLevel = GetRetracementLevel(a.Price, b.Price, c.Price, isLong);
                if (retLevel >= 0.382m && retLevel <= 0.786m)
                {
                    bestC = c;
                    break; // Ersten gültigen C nehmen
                }
            }

            var seq = BuildSequence(a, b, bestC, isLong, currentPrice, requireCloseBreak);
            if (seq != null) return seq;
        }

        return null;
    }

    /// <summary>Erstellt ein Sequence-Objekt mit allen Fibonacci-Leveln.</summary>
    private static Sequence? BuildSequence(
        SwingPoint a, SwingPoint b, SwingPoint? c, bool isLong,
        decimal currentPrice, bool requireCloseBreak)
    {
        var range = Math.Abs(b.Price - a.Price);
        if (range == 0) return null;

        // Fibonacci-Level berechnen (Retracement + 5 Extension-Level)
        decimal r382, r500, r559, r618, r667, r786;
        decimal ext100, ext1272, ext1618, ext200, ext2618;

        if (isLong)
        {
            // Long: A=Low, B=High → Retracement geht runter, Extension geht hoch
            r382 = b.Price - range * 0.382m;
            r500 = b.Price - range * 0.500m;
            r559 = b.Price - range * 0.559m;
            r618 = b.Price - range * 0.618m;
            r667 = b.Price - range * 0.667m;
            r786 = b.Price - range * 0.786m;
            // Extensions projiziert von C (oder 50%-Retracement als Fallback)
            var cPrice = c?.Price ?? r500;
            ext100 = cPrice + range * 1.0m;
            ext1272 = cPrice + range * 1.272m;
            ext1618 = cPrice + range * 1.618m;
            ext200 = cPrice + range * 2.0m;
            ext2618 = cPrice + range * 2.618m;
        }
        else
        {
            // Short: A=High, B=Low → Retracement geht hoch, Extension geht runter
            r382 = b.Price + range * 0.382m;
            r500 = b.Price + range * 0.500m;
            r559 = b.Price + range * 0.559m;
            r618 = b.Price + range * 0.618m;
            r667 = b.Price + range * 0.667m;
            r786 = b.Price + range * 0.786m;
            var cPrice = c?.Price ?? r500;
            ext100 = cPrice - range * 1.0m;
            ext1272 = cPrice - range * 1.272m;
            ext1618 = cPrice - range * 1.618m;
            ext200 = cPrice - range * 2.0m;
            ext2618 = cPrice - range * 2.618m;
        }

        // State bestimmen (requireCloseBreak: true=Close über B nötig, false=Wick reicht)
        var state = SequenceState.Forming;
        if (c != null)
        {
            // Bei requireCloseBreak nutzen wir currentPrice als Close-Proxy
            // (im Backtest und Live ist currentPrice = letzter Close)
            var bBroken = isLong ? currentPrice > b.Price : currentPrice < b.Price;
            if (isLong ? currentPrice < a.Price : currentPrice > a.Price)
                state = SequenceState.Invalidated;
            else if (isLong ? currentPrice >= ext1618 : currentPrice <= ext1618)
                state = SequenceState.TargetReached;
            else if (bBroken)
                state = SequenceState.Active;
            else
                state = SequenceState.WaitingBreak;
        }
        else
        {
            // Kein C: Prüfen ob wir in der Korrektur-Zone sind
            var retLevel = GetRetracementLevel(a.Price, b.Price, currentPrice, isLong);
            state = retLevel >= 0.382m && retLevel <= 0.786m
                ? SequenceState.CorrectionZone
                : SequenceState.Forming;
        }

        return new Sequence
        {
            PointA = a, PointB = b, PointC = c, IsLong = isLong, State = state,
            Retracement382 = r382, Retracement500 = r500, Retracement559 = r559,
            Retracement618 = r618, Retracement667 = r667, Retracement786 = r786,
            Extension100 = ext100, Extension1272 = ext1272,
            Extension1618 = ext1618, Extension200 = ext200, Extension2618 = ext2618
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Entry-Bestätigung (Candlestick-Stabilisierung bei Punkt C)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Erkennt Candlestick-Bestätigung in der Entry-Zone (SK-System: "Stabilisierung bei Punkt C").
    /// Prüft ob der Preis sich in der Kaufzone stabilisiert hat (nicht nur kurz durchgelaufen).
    /// </summary>
    public static CandleConfirmation DetectEntryConfirmation(
        IReadOnlyList<Candle> candles, Sequence seq, decimal currentPrice)
    {
        if (candles.Count < 5) return CandleConfirmation.None;

        // Letzte 3 Kerzen prüfen
        var last3 = new[] { candles[^3], candles[^2], candles[^1] };
        var zoneMin = Math.Min(seq.Retracement500, seq.Retracement667);
        var zoneMax = Math.Max(seq.Retracement500, seq.Retracement667);

        // Stabilisierung: 2+ Kerzen schließen in der idealen Zone (50-66.7%)
        var closesInZone = last3.Count(c => c.Close >= zoneMin && c.Close <= zoneMax);
        if (closesInZone >= 2) return CandleConfirmation.StableInZone;

        // Hammer/Pin-Bar: Langer Docht in Sequenz-Richtung, kleiner Körper
        var lastCandle = candles[^1];
        var candleRange = lastCandle.High - lastCandle.Low;
        if (candleRange > 0)
        {
            var bodySize = Math.Abs(lastCandle.Close - lastCandle.Open);
            var bodyRatio = bodySize / candleRange;

            if (seq.IsLong)
            {
                // Long: Langer unterer Docht (Käufer wehren sich) = Hammer
                var lowerWick = Math.Min(lastCandle.Open, lastCandle.Close) - lastCandle.Low;
                if (bodyRatio < 0.35m && lowerWick / candleRange > 0.5m)
                    return CandleConfirmation.HammerOrPin;
            }
            else
            {
                // Short: Langer oberer Docht (Verkäufer wehren sich) = Inverted Hammer
                var upperWick = lastCandle.High - Math.Max(lastCandle.Open, lastCandle.Close);
                if (bodyRatio < 0.35m && upperWick / candleRange > 0.5m)
                    return CandleConfirmation.HammerOrPin;
            }

            // Engulfing: Aktuelle Kerze umschließt vorherige komplett
            var prevCandle = candles[^2];
            var prevBody = Math.Abs(prevCandle.Close - prevCandle.Open);
            if (bodySize > prevBody * 1.5m)
            {
                var isBullishEngulfing = lastCandle.Close > lastCandle.Open && prevCandle.Close < prevCandle.Open;
                var isBearishEngulfing = lastCandle.Close < lastCandle.Open && prevCandle.Close > prevCandle.Open;
                if ((seq.IsLong && isBullishEngulfing) || (!seq.IsLong && isBearishEngulfing))
                    return CandleConfirmation.Engulfing;
            }
        }

        // Hohes Volumen: Letzte Kerze hat > 1.5x durchschnittliches Volumen
        var avgVol = last3.Average(c => c.Volume);
        if (avgVol > 0 && lastCandle.Volume > avgVol * 1.5m)
            return CandleConfirmation.HighVolume;

        // Preis in der Zone aber keine besondere Bestätigung
        if (currentPrice >= zoneMin && currentPrice <= zoneMax)
            return CandleConfirmation.PriceTouches;

        return CandleConfirmation.None;
    }
}
