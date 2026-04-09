using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Analysiert Liquiditätszonen: Wo liegen Stop-Loss-Cluster und Volume-Nodes?
/// Im SK-System werden diese Zonen zur Validierung von Entry- und Ziel-Leveln genutzt.
/// Market Maker treiben den Preis oft zu Liquiditätszonen um dort Orders zu füllen.
/// </summary>
public static class LiquidityAnalyzer
{
    /// <summary>
    /// Erkennt Liquiditätszonen basierend auf Swing-Punkten und Volume-Profil.
    /// </summary>
    /// <param name="candles">Candle-Daten für Volume-Analyse.</param>
    /// <param name="swingPoints">Erkannte Swing-Punkte (von SequenceDetector).</param>
    /// <param name="clusterTolerance">Preis-Toleranz für Cluster-Erkennung (in % des Preises).</param>
    public static List<LiquidityZone> FindLiquidityZones(
        IReadOnlyList<Candle> candles,
        List<SwingPoint> swingPoints,
        decimal clusterTolerance = 0.3m)
    {
        var zones = new List<LiquidityZone>();
        if (swingPoints.Count == 0 || candles.Count == 0) return zones;

        var currentPrice = candles[^1].Close;
        if (currentPrice <= 0) return zones;

        var tolerance = currentPrice * clusterTolerance / 100m;

        // 1. Stop-Loss-Cluster: Gruppen von Swing-Lows/Highs auf ähnlichem Preis-Level
        // Unter Swing-Lows liegen Long-Stop-Losses (Liquidation bei Break darunter)
        // Über Swing-Highs liegen Short-Stop-Losses (Liquidation bei Break darüber)
        var swingLows = swingPoints.Where(s => !s.IsHigh).Select(s => s.Price).ToList();
        var swingHighs = swingPoints.Where(s => s.IsHigh).Select(s => s.Price).ToList();

        // Cluster finden: Swing-Punkte die nahe beieinander liegen
        zones.AddRange(FindClusters(swingLows, tolerance, LiquidityType.StopLossCluster));
        zones.AddRange(FindClusters(swingHighs, tolerance, LiquidityType.StopLossCluster));

        // 2. Einzelne prominente Swings als Liquidationsziele
        // Swings die noch nicht in einem Cluster sind und nahe am aktuellen Preis liegen
        var clusterPrices = new HashSet<decimal>(zones.Select(z => z.PriceLevel));
        foreach (var swing in swingPoints)
        {
            // Nur Swings die noch nicht in einem Cluster erfasst sind
            if (clusterPrices.Any(cp => Math.Abs(cp - swing.Price) < tolerance)) continue;

            // Nur Swings in relevanter Reichweite (±10% vom aktuellen Preis)
            if (Math.Abs(swing.Price - currentPrice) / currentPrice > 0.10m) continue;

            zones.Add(new LiquidityZone(swing.Price, 0.3m, LiquidityType.SwingLiquidation));
        }

        // 3. High-Volume-Nodes: Preis-Level mit überdurchschnittlichem Handelsvolumen
        var volumeNodes = FindHighVolumeNodes(candles, tolerance);
        zones.AddRange(volumeNodes);

        // Nach Stärke sortieren (stärkste zuerst)
        zones.Sort((a, b) => b.Strength.CompareTo(a.Strength));
        return zones;
    }

    /// <summary>Prüft ob ein Preis-Level in einer Liquiditätszone liegt.</summary>
    public static bool IsInLiquidityZone(decimal price, List<LiquidityZone> zones, decimal tolerancePercent = 0.3m)
    {
        if (zones.Count == 0 || price <= 0) return false;
        var tolerance = price * tolerancePercent / 100m;
        return zones.Any(z => Math.Abs(z.PriceLevel - price) <= tolerance);
    }

    /// <summary>Gibt die Stärke der nächsten Liquiditätszone zurück (0 wenn keine in Reichweite).</summary>
    public static decimal GetNearestZoneStrength(decimal price, List<LiquidityZone> zones, decimal tolerancePercent = 0.5m)
    {
        if (zones.Count == 0 || price <= 0) return 0;
        var tolerance = price * tolerancePercent / 100m;
        var nearest = zones
            .Where(z => Math.Abs(z.PriceLevel - price) <= tolerance)
            .OrderBy(z => Math.Abs(z.PriceLevel - price))
            .FirstOrDefault();
        return nearest?.Strength ?? 0;
    }

    // ═══════════════════════════════════════════════════════════════
    // Private Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Findet Cluster von Preis-Leveln die nahe beieinander liegen.</summary>
    private static List<LiquidityZone> FindClusters(List<decimal> prices, decimal tolerance, LiquidityType type)
    {
        var clusters = new List<LiquidityZone>();
        if (prices.Count < 2) return clusters;

        var sorted = prices.OrderBy(p => p).ToList();
        var visited = new HashSet<int>();

        for (int i = 0; i < sorted.Count; i++)
        {
            if (visited.Contains(i)) continue;

            var cluster = new List<decimal> { sorted[i] };
            visited.Add(i);

            for (int j = i + 1; j < sorted.Count; j++)
            {
                if (visited.Contains(j)) continue;
                if (Math.Abs(sorted[j] - sorted[i]) <= tolerance)
                {
                    cluster.Add(sorted[j]);
                    visited.Add(j);
                }
            }

            // Cluster mit 2+ Swings = signifikante Zone
            if (cluster.Count >= 2)
            {
                var avgPrice = cluster.Average();
                // Stärke: Mehr Swings am gleichen Level = stärkere Zone (max 1.0)
                var strength = Math.Min(1.0m, cluster.Count * 0.3m);
                clusters.Add(new LiquidityZone(avgPrice, strength, type));
            }
        }

        return clusters;
    }

    /// <summary>Findet Preis-Level mit überdurchschnittlichem Handelsvolumen (Volume Profile).</summary>
    private static List<LiquidityZone> FindHighVolumeNodes(IReadOnlyList<Candle> candles, decimal tolerance)
    {
        var nodes = new List<LiquidityZone>();
        if (candles.Count < 20) return nodes;

        // Einfaches Volume-Profile: Volumen pro Preis-Level akkumulieren
        var volumeByPrice = new Dictionary<decimal, decimal>();

        foreach (var candle in candles)
        {
            // Candle-Mitte als Preis-Level (gerundet auf Toleranz)
            var mid = (candle.High + candle.Low) / 2m;
            var roundedPrice = Math.Round(mid / tolerance) * tolerance;

            if (!volumeByPrice.ContainsKey(roundedPrice))
                volumeByPrice[roundedPrice] = 0;
            volumeByPrice[roundedPrice] += candle.Volume;
        }

        if (volumeByPrice.Count == 0) return nodes;

        // Durchschnittsvolumen berechnen
        var avgVolume = volumeByPrice.Values.Average();

        // High-Volume-Nodes: > 2x Durchschnitt
        foreach (var (price, volume) in volumeByPrice)
        {
            if (volume > avgVolume * 2m)
            {
                var strength = Math.Min(1.0m, (volume / avgVolume - 1m) * 0.3m);
                nodes.Add(new LiquidityZone(price, strength, LiquidityType.HighVolumeNode));
            }
        }

        return nodes;
    }
}
