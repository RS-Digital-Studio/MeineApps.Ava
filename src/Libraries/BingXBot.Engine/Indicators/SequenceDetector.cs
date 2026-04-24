using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// SK-System Sequenz-Erkennung: Identifiziert A-B-C Muster, BOS und GKL.
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
        => FindSwingPoints(candles, strength, strength);

    /// <summary>
    /// Strukturpunkte-Doku §1: Findet Pivot Highs/Lows mit asymmetrischem Fenster.
    /// Doku-Zitat: "Left_Bars = 5 bis 10, Right_Bars = 3 bis 5. Ein Pivot Low entsteht, wenn das Tief einer Kerze
    /// tiefer ist als die Tiefs der X Kerzen davor UND tiefer als die Tiefs der X Kerzen danach."
    /// Asymmetrie ist sinnvoll, weil mehr linke Kerzen die Signifikanz ("wie weit war der Move") und weniger rechte
    /// Kerzen die Reaktionszeit (frühere Erkennung) bestimmen. Doku-Defaults: left=5, right=3.
    /// </summary>
    public static List<SwingPoint> FindSwingPoints(IReadOnlyList<Candle> candles, int leftBars, int rightBars)
    {
        var swings = new List<SwingPoint>();
        if (leftBars < 1) leftBars = 1;
        if (rightBars < 1) rightBars = 1;
        if (candles.Count < leftBars + rightBars + 1) return swings;

        for (int i = leftBars; i < candles.Count - rightBars; i++)
        {
            var isHigh = true;
            var isLow = true;
            var high = candles[i].High;
            var low = candles[i].Low;

            // Linke Bars prüfen (leftBars Kerzen davor)
            for (int j = 1; j <= leftBars; j++)
            {
                if (candles[i - j].High >= high) isHigh = false;
                if (candles[i - j].Low <= low) isLow = false;
                if (!isHigh && !isLow) break;
            }
            // Rechte Bars prüfen (rightBars Kerzen danach)
            if (isHigh || isLow)
            {
                for (int j = 1; j <= rightBars; j++)
                {
                    if (candles[i + j].High >= high) isHigh = false;
                    if (candles[i + j].Low <= low) isLow = false;
                    if (!isHigh && !isLow) break;
                }
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
    // ZigZag-Kreuzvalidierung
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Kreuzvalidiert Fractal-Swing-Punkte mit Skender ZigZag.
    /// Swings die BEIDE Algorithmen finden bekommen höhere Konfidenz.
    /// </summary>
    /// <param name="candles">Candle-Daten.</param>
    /// <param name="fractalStrength">Fractal-Stärke (3-10).</param>
    /// <param name="zigZagPercent">ZigZag Prozent-Schwelle (z.B. 3.0 = 3%).</param>
    /// <param name="priceTolerancePercent">Toleranz für "gleicher Swing" in % des Preises.</param>
    public static List<(SwingPoint Point, bool IsConfirmed)> CrossValidateSwings(
        IReadOnlyList<Candle> candles,
        int fractalStrength = 5,
        decimal zigZagPercent = 3.0m,
        decimal priceTolerancePercent = 0.5m)
    {
        var fractalSwings = FindSwingPoints(candles, fractalStrength);
        var zigZagSwings = IndicatorHelper.CalculateZigZag(candles, zigZagPercent);

        if (zigZagSwings.Count == 0)
            return fractalSwings.Select(s => (s, false)).ToList();

        var result = new List<(SwingPoint Point, bool IsConfirmed)>();

        foreach (var fs in fractalSwings)
        {
            var tolerance = fs.Price * priceTolerancePercent / 100m;
            // Suche einen ZigZag-Swing in der Nähe (gleicher Typ + ähnlicher Preis)
            var confirmed = zigZagSwings.Any(zs =>
                zs.IsHigh == fs.IsHigh &&
                Math.Abs(zs.Price - fs.Price) <= tolerance &&
                Math.Abs(zs.CandleIndex - fs.CandleIndex) <= fractalStrength * 2);

            result.Add((fs, confirmed));
        }

        return result;
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
        return longSeq.Point0.CandleIndex > shortSeq.Point0.CandleIndex ? longSeq : shortSeq;
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
                        if (retLevel >= 0.382m && retLevel <= 0.786m) // Gültiger Korrekturbereich (Entry-Zone 50-66.7% wird in der Strategie geprüft)
                        {
                            var seq = BuildSequence(a, b, c, isLong, currentPrice, false, candles);
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

    // BUCH-ONLY: IKI (Interne Korrektur-Sequenz) ist kein Buch-Konzept. Die Buch-Masterclass
    // beschreibt nur B-Level, BC-Korrektur und GKL — keine sub-sequentielle Verschachtelung innerhalb
    // der B-C-Zone.

    // ═══════════════════════════════════════════════════════════════
    // BOS (Break of Structure) — Strukturbruch zur Aktivierung einer Sequenz
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Break of Structure: Erkennt ob ein signifikantes Swing-Level durchbrochen wurde.
    /// SK-Regel: Erfordert einen Candle-BODY-Close jenseits des Levels (kein Docht!).
    /// Bullisch: Close > letztes Swing-High. Bärisch: Close &lt; letztes Swing-Low.
    /// </summary>
    public static StructureBreak? DetectBOS(IReadOnlyList<Candle> candles, List<SwingPoint> swings,
        bool requireCloseBreak = true)
    {
        if (swings.Count < 2 || candles.Count == 0) return null;

        var currentClose = candles[^1].Close;
        var currentHigh = candles[^1].High;
        var currentLow = candles[^1].Low;

        // Letztes Swing-High und Swing-Low finden
        var lastHigh = swings.LastOrDefault(s => s.IsHigh);
        var lastLow = swings.LastOrDefault(s => !s.IsHigh);

        // SK-Regel: Candle-Body-Close über dem Level (kein Docht!)
        var bullBreakPrice = requireCloseBreak ? currentClose : currentHigh;
        var bearBreakPrice = requireCloseBreak ? currentClose : currentLow;

        // Bullischer Break: Close > letztes Swing-High
        if (lastHigh != null && bullBreakPrice > lastHigh.Price)
            return new StructureBreak(lastHigh, true, candles[^1].CloseTime);

        // Bärischer Break: Close < letztes Swing-Low
        if (lastLow != null && bearBreakPrice < lastLow.Price)
            return new StructureBreak(lastLow, false, candles[^1].CloseTime);

        return null;
    }

    // BUCH-ONLY: ChoCH (Change of Character) ist ein SMC-Konzept und nicht Teil des SK-Buchs.
    // Die Trend-Umkehr-Erkennung erfolgt im SK-System ausschliesslich durch das Brechen von Punkt 0
    // einer aktiven Sequenz (Invalidation + Bias-Flip in der StateMachine).

    // ═══════════════════════════════════════════════════════════════
    // SK-Buch H1 Filter: Korrektur-Ende Erkennung
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Erkennt ob eine Korrektur an Schwung verliert (Buch S.15: H1 als Filter-Chart).
    /// Long-Trade: Sucht erstes höheres Tief nach einer Serie fallender Tiefs.
    /// Short-Trade: Sucht erstes tieferes Hoch nach einer Serie steigender Hochs.
    /// </summary>
    /// <param name="candles">1H-Candle-Daten.</param>
    /// <param name="swings">Swing-Punkte der 1H-Candles.</param>
    /// <param name="isLong">True wenn übergeordnete Sequenz (4H) Long ist.</param>
    /// <returns>Ob die Korrektur endet und der Pivot-Punkt.</returns>
    public static (bool CorrectionEnding, SwingPoint? PivotPoint) DetectCorrectionEnd(
        IReadOnlyList<Candle> candles, List<SwingPoint> swings, bool isLong)
    {
        if (swings.Count < 3) return (false, null);

        if (isLong)
        {
            // Long-Trade: Korrektur = fallende Lows. Ende = erstes höheres Low.
            var lows = swings.Where(s => !s.IsHigh).TakeLast(4).ToList();
            if (lows.Count < 3) return (false, null);

            // Mindestens 2 fallende Lows (Korrektur aktiv)
            var hasFallingLows = lows[^3].Price > lows[^2].Price;
            // Letztes Low höher als vorletztes (Korrektur-Ende = Higher Low)
            var hasHigherLow = lows[^1].Price > lows[^2].Price;

            if (hasFallingLows && hasHigherLow)
                return (true, lows[^1]);
        }
        else
        {
            // Short-Trade: Korrektur = steigende Highs. Ende = erstes tieferes High.
            var highs = swings.Where(s => s.IsHigh).TakeLast(4).ToList();
            if (highs.Count < 3) return (false, null);

            var hasRisingHighs = highs[^3].Price < highs[^2].Price;
            var hasLowerHigh = highs[^1].Price < highs[^2].Price;

            if (hasRisingHighs && hasLowerHigh)
                return (true, highs[^1]);
        }

        return (false, null);
    }

    // ═══════════════════════════════════════════════════════════════
    // GKL (Gesamtkorrekturlevel)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet das Gesamtkorrekturlevel (GKL) der übergeordneten Struktur.
    /// SK-Buch: GKL = 50%, 61.8%, 66.7% Retracement der gesamten Bewegung (Golden Pocket 50-66.7%).
    /// Wird typischerweise auf HTF-Candles angewendet.
    /// </summary>
    public static (decimal Gkl500, decimal Gkl618, decimal Gkl667, bool IsUptrend, decimal SwingHigh, decimal SwingLow)?
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

        // SK-Buch: GKL-Zone = 50-66.7% (Golden Pocket), korrigiert von 55.9-66.7%
        decimal gkl500, gkl618, gkl667;
        if (isUptrend)
        {
            // Uptrend-GKL: Retracement von oben nach unten
            gkl500 = lastHigh.Price - range * 0.500m;
            gkl618 = lastHigh.Price - range * 0.618m;
            gkl667 = lastHigh.Price - range * 0.667m;
        }
        else
        {
            // Downtrend-GKL: Retracement von unten nach oben
            gkl500 = lastLow.Price + range * 0.500m;
            gkl618 = lastLow.Price + range * 0.618m;
            gkl667 = lastLow.Price + range * 0.667m;
        }

        return (gkl500, gkl618, gkl667, isUptrend, lastHigh.Price, lastLow.Price);
    }

    /// <summary>Prüft ob ein Preis im GKL-Bereich liegt (50-66.7% — SK Golden Pocket).</summary>
    public static bool IsInGKL(decimal price, decimal gkl500, decimal gkl667)
    {
        var min = Math.Min(gkl500, gkl667);
        var max = Math.Max(gkl500, gkl667);
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

        // 200% Extension: Sequenz vollständig abgearbeitet (SK-Regel)
        if (seq.HasFullyCompleted(currentPrice))
            return SequenceState.FullyCompleted;

        // 161.8% Extension: Erste Zielzone erreicht
        if (seq.HasReachedTarget(currentPrice))
            return SequenceState.TargetReached;

        // Aktiviert: B durchbrochen
        var breakPrice = useCloseBreak ? currentClose : currentPrice;
        var bBroken = seq.IsLong ? breakPrice > seq.PointA.Price : breakPrice < seq.PointA.Price;
        if (bBroken)
            return SequenceState.Active;

        // Punkt C noch nicht gebildet
        if (seq.PointB == null)
        {
            // Im Retracement-Bereich (38.2-78.6%)?
            var retLevel = GetRetracementLevel(seq.Point0.Price, seq.PointA.Price, currentPrice, seq.IsLong);
            if (retLevel >= 0.382m && retLevel <= 0.786m) // Gültiger Korrekturbereich (Entry-Zone 50-66.7% wird in der Strategie geprüft)
                return SequenceState.CorrectionZone;
            return SequenceState.Forming;
        }

        // C gebildet, wartet auf B-Break
        return SequenceState.WaitingBreak;
    }

    // ═══════════════════════════════════════════════════════════════
    // Private Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════
    // BCKL (BC-Korrekturlevel) — Re-Entry nach 100% Extension
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet das BC-Korrekturlevel: Wenn der Preis an der 100% Extension reagiert,
    /// bietet das Retracement der Aufwärtsbewegung C→Extension100 (50-66.7%) einen Re-Entry.
    /// Die Range ist C→ext100 (= A-B Range projiziert von C), NICHT B→C.
    /// </summary>
    public static (decimal Bckl500, decimal Bckl559, decimal Bckl618, decimal Bckl667)?
        CalculateBCKL(Sequence seq)
    {
        if (seq.PointB == null || seq.State != SequenceState.Active) return null;

        var ext100 = seq.Extension100;
        // BCKL-Range = Strecke C→Extension100 (die Zielbewegung, die zurückkorrigiert)
        var bcklRange = Math.Abs(ext100 - seq.PointB.Price);
        if (bcklRange <= 0) return null;

        // BCKL = Fibonacci-Retracement der Bewegung C→ext100
        // Long: ext100 ist oben, Retracement geht nach unten
        // Short: ext100 ist unten, Retracement geht nach oben
        if (seq.IsLong)
        {
            return (
                ext100 - bcklRange * 0.500m,
                ext100 - bcklRange * 0.559m,
                ext100 - bcklRange * 0.618m,
                ext100 - bcklRange * 0.667m);
        }
        else
        {
            return (
                ext100 + bcklRange * 0.500m,
                ext100 + bcklRange * 0.559m,
                ext100 + bcklRange * 0.618m,
                ext100 + bcklRange * 0.667m);
        }
    }

    /// <summary>Prüft ob ein Preis im BCKL-Bereich liegt (50-66.7% des B-C Retracements).</summary>
    public static bool IsInBCKL(decimal price, decimal bckl500, decimal bckl667)
    {
        var min = Math.Min(bckl500, bckl667);
        var max = Math.Max(bckl500, bckl667);
        return price >= min && price <= max;
    }

    // ═══════════════════════════════════════════════════════════════
    // Destabilisierung — Warnsignal wenn Korrektur-Zone nicht hält
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Erkennt Destabilisierung: Preis war in der Korrektur-Zone (50-66.7%),
    /// hat sie aber wieder verlassen OHNE Punkt B zu brechen.
    /// SK-Signal: Übergeordnete Sequenz könnte scheitern.
    /// </summary>
    public static bool IsDestabilized(IReadOnlyList<Candle> candles, Sequence seq, int lookbackCandles = 10)
    {
        if (seq.PointB == null || candles.Count < lookbackCandles + 1) return false;
        if (seq.State is SequenceState.Active or SequenceState.TargetReached or SequenceState.FullyCompleted)
            return false;

        // SK-VERIFY: [Abweichung #2] GKL-Zone = 50-66.7% (SK Golden Pocket, war 55.9-66.7%)
        var zoneMin = Math.Min(seq.Retracement500, seq.Retracement667);
        var zoneMax = Math.Max(seq.Retracement500, seq.Retracement667);

        // Prüfe ob der Preis kürzlich IN der Zone war
        var wasInZone = false;
        for (int i = candles.Count - lookbackCandles - 1; i < candles.Count - 1; i++)
        {
            if (i < 0) continue;
            if (candles[i].Close >= zoneMin && candles[i].Close <= zoneMax)
            {
                wasInZone = true;
                break;
            }
        }

        if (!wasInZone) return false;

        // Aktueller Preis hat die Zone verlassen (in Richtung Punkt A — also gegen die Sequenz)
        var currentClose = candles[^1].Close;
        if (seq.IsLong)
            return currentClose < zoneMin; // Long: Preis fällt unter die Zone
        else
            return currentClose > zoneMax; // Short: Preis steigt über die Zone
    }

    // BUCH-ONLY: SequenceType- und WaveCharacter-Klassifikation sind Eigen-Erweiterungen und
    // nicht Teil des SK-Buchs. Das Buch bewertet jede erkannte 0-A-B-C-Sequenz gleichwertig —
    // die Unterscheidung Normal/Überextendiert/Langgezogen war eine Zusatzheuristik.

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
        // A-Kandidaten (Long: Lows, Short: Highs) — von rechts nach links (neueste zuerst)
        var aSwings = swings.Where(s => isLong ? !s.IsHigh : s.IsHigh).Reverse().ToList();
        var bSwings = swings.Where(s => isLong ? s.IsHigh : !s.IsHigh).ToList();

        foreach (var a in aSwings)
        {
            // Nächsten A-Typ-Swing nach diesem A finden (begrenzt den B-Suchbereich)
            var nextA = swings.FirstOrDefault(s =>
                s.CandleIndex > a.CandleIndex && (isLong ? !s.IsHigh : s.IsHigh));

            // B suchen: Das STÄRKSTE High/Low zwischen A und dem nächsten A-Typ-Swing
            // SK-Regel: B = Ende des initialen Impulses (der signifikante Gipfel/Boden)
            var bCandidates = bSwings
                .Where(s => s.CandleIndex > a.CandleIndex &&
                            (nextA == null || s.CandleIndex < nextA.CandleIndex))
                .ToList();
            if (bCandidates.Count == 0) continue;

            // Bestes B: Höchstes High (Long) bzw. tiefstes Low (Short) — der Impuls-Gipfel
            var b = isLong
                ? bCandidates.MaxBy(s => s.Price)!
                : bCandidates.MinBy(s => s.Price)!;

            // Range-Check
            var range = Math.Abs(b.Price - a.Price);
            var midPrice = (a.Price + b.Price) / 2;
            if (midPrice == 0 || range / midPrice * 100 < minRangePercent) continue;

            // C suchen: Nächster Swing in A-Richtung NACH B, im Retracement-Bereich
            var cCandidates = swings.Where(s =>
                s.CandleIndex > b.CandleIndex &&
                (isLong ? !s.IsHigh : s.IsHigh)).ToList();

            // C-Punkt im gültigen Fibonacci-Korrekturbereich (38.2-78.6%):
            // 38.2% = Minimum für gültige Sequenz
            // 78.6% = Maximum (tiefer = fast Invalidierung)
            // Die ENTRY-Zone (50-66.7%) wird in der Strategie separat geprüft
            SwingPoint? bestC = null;
            foreach (var c in cCandidates)
            {
                var retLevel = GetRetracementLevel(a.Price, b.Price, c.Price, isLong);
                if (retLevel >= 0.382m && retLevel <= 0.786m)
                {
                    bestC = c;
                    break;
                }
            }

            var seq = BuildSequence(a, b, bestC, isLong, currentPrice, requireCloseBreak, candles);
            if (seq != null) return seq;
        }

        return null;
    }

    /// <summary>Erstellt ein Sequence-Objekt mit allen Fibonacci-Leveln.</summary>
    private static Sequence? BuildSequence(
        SwingPoint a, SwingPoint b, SwingPoint? c, bool isLong,
        decimal currentPrice, bool requireCloseBreak,
        IReadOnlyList<Candle>? candles = null)
    {
        var range = Math.Abs(b.Price - a.Price);
        if (range == 0) return null;

        // Buch-konforme Fibonacci-Level: 50/55.9/61.8/66.7/71/78.6 Retracement + 161.8/200/261.8/423.6 Extension.
        // Extension100 (100%) + Retracement702 (70.2%) bleiben als Intern-Hilfslevel fuer SL-Buffer-Projektionen.
        decimal r500, r559, r618, r667, r702, r71, r786;
        decimal ext100, ext1618, ext200, ext2618, ext4236;

        if (isLong)
        {
            // Long: A=Low, B=High → Retracement geht runter, Extension geht hoch
            r500 = b.Price - range * 0.500m;
            r559 = b.Price - range * 0.559m;
            r618 = b.Price - range * 0.618m;
            r667 = b.Price - range * 0.667m;
            r702 = b.Price - range * 0.702m;
            r71 = b.Price - range * 0.71m;
            r786 = b.Price - range * 0.786m;
            // Extensions projiziert von C (oder 50%-Retracement als Fallback)
            var cPrice = c?.Price ?? r500;
            ext100 = cPrice + range * 1.0m;
            ext1618 = cPrice + range * 1.618m;
            ext200 = cPrice + range * 2.0m;
            ext2618 = cPrice + range * 2.618m;
            ext4236 = cPrice + range * 4.236m;
        }
        else
        {
            // Short: A=High, B=Low → Retracement geht hoch, Extension geht runter
            r500 = b.Price + range * 0.500m;
            r559 = b.Price + range * 0.559m;
            r618 = b.Price + range * 0.618m;
            r667 = b.Price + range * 0.667m;
            r702 = b.Price + range * 0.702m;
            r71 = b.Price + range * 0.71m;
            r786 = b.Price + range * 0.786m;
            var cPrice = c?.Price ?? r500;
            ext100 = cPrice - range * 1.0m;
            ext1618 = cPrice - range * 1.618m;
            ext200 = cPrice - range * 2.0m;
            ext2618 = cPrice - range * 2.618m;
            ext4236 = cPrice - range * 4.236m;
        }

        // State wird NICHT in BuildSequence bestimmt — nur in UpdateState().
        // BuildSequence berechnet nur die Fibonacci-Level. Der Aufrufer (DetectSequence,
        // Evaluate) ruft UpdateState() mit dem korrekten currentClose/currentPrice auf.
        // Das verhindert den Bug wo requireCloseBreak ignoriert wurde.
        var state = c != null ? SequenceState.WaitingBreak : SequenceState.Forming;

        // Grob-Check: Wenn C existiert, prüfe ob Sequenz offensichtlich invalidiert ist
        if (c != null && (isLong ? currentPrice < a.Price : currentPrice > a.Price))
            state = SequenceState.Invalidated;

        return new Sequence
        {
            Point0 = a, PointA = b, PointB = c, IsLong = isLong, State = state,
            Retracement500 = r500, Retracement559 = r559,
            Retracement618 = r618, Retracement667 = r667, Retracement702 = r702,
            Retracement71 = r71, Retracement786 = r786,
            Extension100 = ext100,
            Extension1618 = ext1618, Extension200 = ext200,
            Extension2618 = ext2618, Extension4236 = ext4236,
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
        // SK-VERIFY: [Abweichung #2] GKL-Zone = 50-66.7% (SK Golden Pocket, war 55.9-66.7%)
        var gklMin = Math.Min(seq.Retracement500, seq.Retracement667);
        var gklMax = Math.Max(seq.Retracement500, seq.Retracement667);

        // Stabilisierung: 2+ Kerzen schließen in der GKL-Zone (50-66.7%)
        var closesInZone = last3.Count(c => c.Close >= gklMin && c.Close <= gklMax);
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

        // Hohes Volumen: Letzte Kerze hat > 1.5x SMA-20 Volumen (konsistent mit Confluence-Check)
        if (candles.Count >= 20)
        {
            var volSum = 0m;
            for (int i = candles.Count - 20; i < candles.Count; i++)
                volSum += candles[i].Volume;
            var avgVol20 = volSum / 20m;
            if (avgVol20 > 0 && lastCandle.Volume > avgVol20 * 1.5m)
                return CandleConfirmation.HighVolume;
        }

        // Preis in der erweiterten Zone (50-66.7%) aber keine besondere Bestätigung
        var zoneMin = Math.Min(seq.Retracement500, seq.Retracement667);
        var zoneMax = Math.Max(seq.Retracement500, seq.Retracement667);
        if (currentPrice >= zoneMin && currentPrice <= zoneMax)
            return CandleConfirmation.PriceTouches;

        return CandleConfirmation.None;
    }

    // BUCH-ONLY: SequenceHierarchy (Primary/Secondary/Breakout) stammt aus dem CWS-Workflow,
    // nicht aus dem SK-Buch. Das Buch kennt B-Level, BC-Korrektur und GKL — alles direkt.
}
