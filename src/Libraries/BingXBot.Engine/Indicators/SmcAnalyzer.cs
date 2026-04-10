using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Smart Money Concepts (SMC) Analyse: Order Blocks, Fair Value Gaps, Multi-TF Konsistenz.
/// Ergänzt den SequenceDetector um institutionelle Preis-Muster.
/// Alle Methoden sind static und thread-safe (kein State).
/// </summary>
public static class SmcAnalyzer
{
    // ═══════════════════════════════════════════════════════════════
    // Order Blocks
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Erkennt Order Blocks: Letzte Gegenkerze vor einem Break of Structure (BOS).
    /// Bullish OB = letzte bärische Kerze vor bullischem BOS (Preis durchbricht Swing-High).
    /// Bearish OB = letzte bullische Kerze vor bärischem BOS (Preis durchbricht Swing-Low).
    /// </summary>
    public static List<OrderBlock> FindOrderBlocks(
        IReadOnlyList<Candle> candles,
        List<SwingPoint> swings,
        int maxAge = 100)
    {
        var blocks = new List<OrderBlock>();
        if (candles.Count < 10 || swings.Count < 3) return blocks;

        var currentPrice = candles[^1].Close;
        var startIdx = Math.Max(0, candles.Count - maxAge);

        // Swing-Highs und Swing-Lows getrennt durchgehen
        var highs = swings.Where(s => s.IsHigh && s.CandleIndex >= startIdx).ToList();
        var lows = swings.Where(s => !s.IsHigh && s.CandleIndex >= startIdx).ToList();

        // Bullische Order Blocks: Suche wo Preis ein vorheriges Swing-High durchbricht
        foreach (var high in highs)
        {
            // Prüfe ob spätere Candles dieses High durchbrechen (BOS)
            var bosIdx = -1;
            for (int i = high.CandleIndex + 1; i < candles.Count; i++)
            {
                if (candles[i].Close > high.Price)
                {
                    bosIdx = i;
                    break;
                }
            }
            if (bosIdx < 0) continue;

            // Letzte bärische Kerze VOR dem BOS finden (= Order Block)
            for (int i = bosIdx - 1; i >= high.CandleIndex && i >= startIdx; i--)
            {
                if (candles[i].Close < candles[i].Open) // Bärische Kerze
                {
                    var ob = CreateOrderBlock(candles, i, SmcZoneType.Bullish, currentPrice,
                        high.Price, candles[bosIdx].Close);
                    if (ob != null) blocks.Add(ob);
                    break;
                }
            }
        }

        // Bärische Order Blocks: Suche wo Preis ein vorheriges Swing-Low durchbricht
        foreach (var low in lows)
        {
            var bosIdx = -1;
            for (int i = low.CandleIndex + 1; i < candles.Count; i++)
            {
                if (candles[i].Close < low.Price)
                {
                    bosIdx = i;
                    break;
                }
            }
            if (bosIdx < 0) continue;

            // Letzte bullische Kerze VOR dem BOS finden
            for (int i = bosIdx - 1; i >= low.CandleIndex && i >= startIdx; i--)
            {
                if (candles[i].Close > candles[i].Open) // Bullische Kerze
                {
                    var ob = CreateOrderBlock(candles, i, SmcZoneType.Bearish, currentPrice,
                        low.Price, candles[bosIdx].Close);
                    if (ob != null) blocks.Add(ob);
                    break;
                }
            }
        }

        // Nach Aktualität sortieren (neueste zuerst), Duplikate entfernen
        blocks = blocks
            .GroupBy(b => b.CandleIndex)
            .Select(g => g.First())
            .OrderByDescending(b => b.CandleIndex)
            .ToList();

        return blocks;
    }

    /// <summary>Gibt den aktiven (unmitigierten) Order Block zurück in dem der Preis liegt.</summary>
    public static OrderBlock? GetActiveOrderBlock(decimal price, List<OrderBlock> blocks, SmcZoneType type)
    {
        return blocks.FirstOrDefault(b =>
            b.Type == type && !b.IsMitigated &&
            price >= b.ZoneLow && price <= b.ZoneHigh);
    }

    /// <summary>Prüft ob ein gegenläufiger OB nahe am Entry-Preis liegt (Widerstand).</summary>
    public static OrderBlock? GetBlockingOrderBlock(decimal entryPrice, List<OrderBlock> blocks,
        bool isLong, decimal proximityPercent = 1.0m)
    {
        if (entryPrice <= 0) return null;
        var tolerance = entryPrice * proximityPercent / 100m;

        // Long: Bärischer OB ÜBER dem Entry = Widerstand
        // Short: Bullischer OB UNTER dem Entry = Unterstützung (blockiert Short)
        return blocks.FirstOrDefault(b =>
            !b.IsMitigated &&
            ((isLong && b.Type == SmcZoneType.Bearish && b.ZoneLow > entryPrice && b.ZoneLow - entryPrice < tolerance) ||
             (!isLong && b.Type == SmcZoneType.Bullish && b.ZoneHigh < entryPrice && entryPrice - b.ZoneHigh < tolerance)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Fair Value Gaps
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Erkennt Fair Value Gaps (Imbalance-Zonen): 3-Kerzen-Muster mit Preislücke.
    /// Bullish FVG: Kerze[i].Low > Kerze[i-2].High → Gap nach oben (wird oft gefüllt).
    /// Bearish FVG: Kerze[i].High &lt; Kerze[i-2].Low → Gap nach unten.
    /// </summary>
    public static List<FairValueGap> FindFairValueGaps(
        IReadOnlyList<Candle> candles,
        decimal minGapPercent = 0.1m,
        int maxAge = 100)
    {
        var gaps = new List<FairValueGap>();
        if (candles.Count < 3) return gaps;

        var startIdx = Math.Max(2, candles.Count - maxAge);
        var currentPrice = candles[^1].Close;

        for (int i = startIdx; i < candles.Count; i++)
        {
            var prev2 = candles[i - 2];
            var current = candles[i];

            // Bullish FVG: Aktuelle Kerze Low > 2-Kerzen-vorher High
            if (current.Low > prev2.High)
            {
                var gapSize = current.Low - prev2.High;
                var midPrice = (current.Low + prev2.High) / 2m;
                if (midPrice > 0 && gapSize / midPrice * 100m >= minGapPercent)
                {
                    // Mitigation prüfen: Wurde der Gap nachträglich gefüllt?
                    var mitigated = false;
                    for (int j = i + 1; j < candles.Count; j++)
                    {
                        if (candles[j].Low <= prev2.High) // Preis berührt den Gap-Bereich
                        {
                            mitigated = true;
                            break;
                        }
                    }

                    gaps.Add(new FairValueGap(current.Low, prev2.High, SmcZoneType.Bullish,
                        i - 1, candles[i - 1].CloseTime, mitigated, gapSize));
                }
            }

            // Bearish FVG: Aktuelle Kerze High < 2-Kerzen-vorher Low
            if (current.High < prev2.Low)
            {
                var gapSize = prev2.Low - current.High;
                var midPrice = (prev2.Low + current.High) / 2m;
                if (midPrice > 0 && gapSize / midPrice * 100m >= minGapPercent)
                {
                    var mitigated = false;
                    for (int j = i + 1; j < candles.Count; j++)
                    {
                        if (candles[j].High >= prev2.Low)
                        {
                            mitigated = true;
                            break;
                        }
                    }

                    gaps.Add(new FairValueGap(prev2.Low, current.High, SmcZoneType.Bearish,
                        i - 1, candles[i - 1].CloseTime, mitigated, gapSize));
                }
            }
        }

        return gaps;
    }

    /// <summary>Gibt den aktiven (unmitigierten) FVG zurück in dem der Preis oder ein Ziellevel liegt.</summary>
    public static FairValueGap? GetActiveFvg(decimal price, List<FairValueGap> gaps, SmcZoneType type)
    {
        return gaps.FirstOrDefault(g =>
            g.Type == type && !g.IsMitigated &&
            price >= g.ZoneBottom && price <= g.ZoneTop);
    }

    /// <summary>Prüft ob ein Zielpreis (TP/Extension) in einem unmitigierten FVG liegt.</summary>
    public static bool IsTargetInFvg(decimal targetPrice, List<FairValueGap> gaps)
    {
        return gaps.Any(g => !g.IsMitigated &&
            targetPrice >= g.ZoneBottom && targetPrice <= g.ZoneTop);
    }

    // ═══════════════════════════════════════════════════════════════
    // Multi-TF Struktur-Konsistenz
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob alle Zeitebenen die gleiche Marktrichtung zeigen.
    /// Im SK-System sollen HTF, Primary und Entry-TF aligned sein.
    /// </summary>
    public static StructureConsistency CheckStructureConsistency(
        IReadOnlyList<Candle>? htfCandles,
        IReadOnlyList<Candle> primaryCandles,
        IReadOnlyList<Candle>? entryCandles,
        int htfSwingStrength = 7,
        int primarySwingStrength = 5,
        int entrySwingStrength = 3)
    {
        // Richtung pro Zeitebene bestimmen (via Sequenz-Erkennung)
        bool? htfDir = null;
        bool? primaryDir = null;
        bool? entryDir = null;

        if (htfCandles is { Count: > 30 })
        {
            var htfSeq = SequenceDetector.DetectSequence(htfCandles, htfSwingStrength, 0.5m, true);
            if (htfSeq != null)
                htfDir = htfSeq.IsLong;
            else
            {
                // Fallback: GKL-Trend
                var gkl = SequenceDetector.CalculateGKL(htfCandles, htfSwingStrength);
                if (gkl.HasValue) htfDir = gkl.Value.IsUptrend;
            }
        }

        if (primaryCandles.Count > 20)
        {
            var primarySeq = SequenceDetector.DetectSequence(primaryCandles, primarySwingStrength, 0.5m, true);
            if (primarySeq != null) primaryDir = primarySeq.IsLong;
        }

        if (entryCandles is { Count: > 15 })
        {
            var entrySeq = SequenceDetector.DetectSequence(entryCandles, entrySwingStrength, 0.3m, false);
            if (entrySeq != null) entryDir = entrySeq.IsLong;
        }

        // Konsistenz berechnen: Wie viele TFs zeigen in die gleiche Richtung?
        var directions = new[] { htfDir, primaryDir, entryDir }.Where(d => d.HasValue).ToList();
        if (directions.Count == 0)
            return new StructureConsistency(htfDir, primaryDir, entryDir, 0, 0, false);

        var bullCount = directions.Count(d => d!.Value);
        var bearCount = directions.Count(d => !d!.Value);
        var aligned = Math.Max(bullCount, bearCount);
        var fullyAligned = aligned == directions.Count;

        return new StructureConsistency(htfDir, primaryDir, entryDir, aligned, directions.Count, fullyAligned);
    }

    // ═══════════════════════════════════════════════════════════════
    // Sequenz-Ineinandergreifen (SK-Matroschka)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// SK-System "Ineinandergreifen": Prüft ob das Ziellevel der aktuellen Sequenz
    /// im Kaufbereich (GKL 50-66.7%) einer übergeordneten Sequenz liegt.
    /// Wenn ja: Extrem starkes Signal — Market Maker werden den Preis dorthin treiben.
    /// </summary>
    /// <param name="primarySequence">Aktuelle Sequenz auf dem Primary-TF.</param>
    /// <param name="htfCandles">Candles des übergeordneten Timeframes.</param>
    /// <param name="htfSwingStrength">Swing-Stärke für HTF-Sequenz-Erkennung.</param>
    /// <returns>True wenn das Ziellevel der Primary-Sequenz im HTF-Kaufbereich liegt.</returns>
    public static bool CheckSequenceInterlock(
        Sequence primarySequence,
        IReadOnlyList<Candle>? htfCandles,
        int htfSwingStrength = 7)
    {
        if (htfCandles is not { Count: > 30 }) return false;

        // Alle HTF-Sequenzen finden (nicht nur die neueste — Matroschka!)
        var htfSequences = SequenceDetector.DetectAllSequences(htfCandles, htfSwingStrength, 0.5m);
        if (htfSequences.Count == 0) return false;

        // Ziellevel der Primary-Sequenz (161.8% Extension)
        var targetPrice = primarySequence.Extension1618;

        // Prüfe ob dieses Ziellevel im Kaufbereich einer HTF-Sequenz liegt
        foreach (var htfSeq in htfSequences)
        {
            // Nur Sequenzen in gleicher Richtung prüfen
            if (htfSeq.IsLong != primarySequence.IsLong) continue;

            // Ziellevel der Primary-Sequenz muss im GKL (50-66.7%) der HTF-Sequenz liegen
            if (htfSeq.IsInBuyZone(targetPrice) || htfSeq.IsInGklZone(targetPrice))
                return true;
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // Private Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════

    private static OrderBlock? CreateOrderBlock(IReadOnlyList<Candle> candles, int obIndex,
        SmcZoneType type, decimal currentPrice, decimal swingPrice, decimal bosClosePrice)
    {
        if (obIndex < 0 || obIndex >= candles.Count) return null;

        var obCandle = candles[obIndex];
        var zoneHigh = obCandle.High;
        var zoneLow = obCandle.Low;

        // Doji-Kerzen (High == Low) ergeben keine sinnvolle Zone → filtern
        var obSize = zoneHigh - zoneLow;
        if (obSize <= 0) return null;

        // Stärke: BOS-Distanz relativ zur OB-Größe + Alter
        var bosDistance = Math.Abs(bosClosePrice - swingPrice);
        var strength = Math.Min(1.0m, bosDistance / obSize * 0.3m);

        // Alter-Bonus: Neuere OBs sind relevanter
        var age = candles.Count - obIndex;
        strength *= Math.Max(0.3m, 1.0m - age / 200m);

        // Mitigation prüfen: Hat der Preis die Zone berührt oder durchstochen?
        var mitigated = false;
        for (int i = obIndex + 1; i < candles.Count; i++)
        {
            if (type == SmcZoneType.Bullish)
            {
                // Bullisch: Preis berührt oder durchsticht die Zone (Low <= ZoneHigh)
                if (candles[i].Low <= zoneHigh)
                {
                    mitigated = true;
                    break;
                }
            }
            else
            {
                // Bärisch: Preis berührt oder durchsticht die Zone (High >= ZoneLow)
                if (candles[i].High >= zoneLow)
                {
                    mitigated = true;
                    break;
                }
            }
        }

        return new OrderBlock(zoneHigh, zoneLow, type, obIndex, obCandle.CloseTime,
            mitigated, Math.Round(strength, 3));
    }
}
