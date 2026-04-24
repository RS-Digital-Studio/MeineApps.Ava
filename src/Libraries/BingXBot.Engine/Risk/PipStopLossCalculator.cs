using BingXBot.Core.Enums;

namespace BingXBot.Engine.Risk;

/// <summary>
/// SK-Buch Stop-Loss-Berechnung mit FIXEN Pip-Werten pro Asset-Klasse.
/// Quelle: Tradebook SK-System (Sascha Wenzel, Stefan Kassing) S.13.
/// </summary>
/// <remarks>
/// Buch-Tabelle (mit Multi-Asset-Anpassungen für BingX-Perpetuals, v1.2.6):
/// - Hauptwährungen (FX): 15/20 Pips (Cheat 37 / Cheat 49)
/// - Gold (XAU): 25/35 Pips — angehoben für M15-Noise (Standard-Buch 15/20 Pips bei Broker-1:100, BingX-Perp braucht mehr)
/// - Silber (XAG): 15/20 Pips MIT Pip-Wert 0.01 (im Buch "Hochzahl" ist bei Silber die 2. Nachkommastelle, nicht die 1.)
/// - Öl (WTI/Brent): 40 Pips
/// - Indices (DAX, Dow, NQ, SPX): 40 Pips
/// - Aktien: 40 Pips
/// - Krypto: 100 Pips
/// - GBP-Paare: +50% (Buch Cheat 46/48)
/// - Exoten (AUD/CAD/NZD-Paare): +50% (Buch)
///
/// Pip-Definition je Instrument:
/// - FX Standard: 1/10000 (4. Nachkommastelle)
/// - FX JPY: 1/100 (2. Nachkommastelle)
/// - Gold: 0.1 (Hochzahl ≙ 1. Nachkommastelle bei ~2600 USD/oz)
/// - Silber: 0.01 (Hochzahl ≙ 2. Nachkommastelle bei ~30 USD/oz, sonst SL = 5 % vom Entry)
/// - Öl: 1/100
/// - Indices: 1 Punkt
/// - Aktien: 0.01 (Cent-Niveau)
/// - Krypto: 1/10000 des Preises (skaliert mit Preis)
///
/// Globaler SL-Floor 0.15% (v1.2.6): Schutz gegen Fee+Spread (BingX Round-Trip Taker = 0.1%, Spread/Slippage 0.05%).
/// Greift wenn Pip-Cap < 0.15% vom Entry liegt — z.B. bei Gold 15 Pips (0.058%) oder kleinen Forex-Strecken.
/// Point0-Clamp bleibt harte Obergrenze (Workflow 6.9).
/// </remarks>
public static class PipStopLossCalculator
{
    /// <summary>
    /// Berechnet die SL-Distanz in Preis-Einheiten nach SK-Buch-Regel.
    /// pipScale &lt; 1.0 (z.B. M15=0.75) wird NUR auf Crypto angewandt — TradFi-Kategorien (Forex/Stock/
    /// Index/Commodity) haben bereits sehr enge Pip-Caps (15-40 Pips), eine zusätzliche Verkleinerung
    /// auf M15 würde unter Spread+Fee fallen (v1.2.6).
    /// </summary>
    public static decimal CalculateSlDistance(string symbol, MarketCategory category, decimal entryPrice, bool isSingleTrade = false, decimal pipScale = 1.0m)
    {
        var effectiveScale = (category == MarketCategory.Crypto) ? pipScale : Math.Max(1.0m, pipScale);
        var pipCount = GetPipCount(symbol, category, isSingleTrade) * effectiveScale;
        var pipValue = GetPipValue(symbol, category, entryPrice);
        return pipCount * pipValue;
    }

    /// <summary>
    /// Berechnet den absoluten SL-Preis nach SK-Buch-Regel (nur Pip-Cap, ohne 78.6er und Point0).
    /// Wird als Fallback verwendet wenn keine Sequenz verfügbar ist.
    /// </summary>
    public static decimal CalculateStopLoss(string symbol, MarketCategory category, decimal entryPrice, bool isLong, bool isSingleTrade = false, decimal pipScale = 1.0m)
    {
        var slDistance = CalculateSlDistance(symbol, category, entryPrice, isSingleTrade, pipScale);
        return isLong ? entryPrice - slDistance : entryPrice + slDistance;
    }

    /// <summary>
    /// Berechnet den SL strikt nach SK-System-Buch Masterclass:
    /// 1. Basis = 78.6% Retracement der 0-A-Range (Cheat 36 — letzte Verteidigungslinie)
    /// 2. GECAPPT bei Markt-Pips (Cheat 37/49)
    /// 3. Task 4.5: Pip-Buffer UNTER/ÜBER Point 0 (5-15 Pips je TF) — Liquidity-Grab-Schutz
    /// 4. NIEMALS über Punkt 0 hinaus (Workflow 6.9), aber inkl. Buffer-Puffer darunter zulässig
    /// </summary>
    /// <param name="fib786">78.6% Retracement-Level der 0-A-Range (Buch SL-Basis).</param>
    /// <param name="point0">Punkt 0 der Sequenz (absolute Grenze).</param>
    /// <param name="isSingleTrade">Cheat 37 (Single-Trade): 15 Pips. Cheat 49 (Multi-Trade-Additional): 20 Pips.</param>
    /// <param name="bufferPips">Task 4.5: Pip-Puffer unter Punkt 0 (Buch: 5-15 je TF). 0 = kein Buffer.</param>
    public static decimal CalculateBookStopLoss(
        string symbol, MarketCategory category,
        decimal entryPrice, bool isLong,
        decimal fib786, decimal point0,
        bool isSingleTrade = false,
        decimal pipScale = 1.0m,
        decimal bufferPips = 0m)
    {
        // 1. Basis: 78.6% Retracement (Buch Cheat 36) — letzte Verteidigungslinie
        var sl = fib786;

        // 2. Pip-Cap: SL darf vom Entry nicht weiter weg sein als Markt-Pips (Cheat 37/49)
        var pipDistance = CalculateSlDistance(symbol, category, entryPrice, isSingleTrade, pipScale);
        var pipCapSl = isLong ? entryPrice - pipDistance : entryPrice + pipDistance;
        sl = isLong ? Math.Max(sl, pipCapSl) : Math.Min(sl, pipCapSl);

        // 3. Task 4.5: Point-0-Buffer. Buch-Regel: SL "5-15 Pips unter das absolute Tief von Punkt 0".
        // Das verschiebt die Point0-Clamp um den Buffer nach außen (Long: unter Point0, Short: über).
        var pipValue = GetPipValue(symbol, category, entryPrice);
        var bufferDistance = bufferPips * pipValue;
        var effectivePoint0 = isLong ? point0 - bufferDistance : point0 + bufferDistance;

        // 4. Point0-Grenze MIT Buffer: SL darf bis effectivePoint0 (inkl. Buffer) liegen, nicht jenseits
        sl = isLong ? Math.Max(sl, effectivePoint0) : Math.Min(sl, effectivePoint0);

        // 5. Sanity: SL auf richtiger Seite (Fallback auf reinen Pip-Cap)
        if (isLong && sl >= entryPrice) sl = entryPrice - pipDistance;
        if (!isLong && sl <= entryPrice) sl = entryPrice + pipDistance;

        // 6. Fee-Floor 0.15% (BingX Round-Trip ≈ 0.1% + Spread). Zusätzliche Schutzschicht gegen
        // Fee-Erosion bei sehr engen SLs. Wirkt parallel zum Pip-Buffer, Point0-Clamp bleibt absolut.
        var minSlDistance = entryPrice * MinSlDistancePercent;
        var actualDistance = Math.Abs(entryPrice - sl);
        if (actualDistance < minSlDistance)
        {
            var floorSl = isLong ? entryPrice - minSlDistance : entryPrice + minSlDistance;
            sl = isLong ? Math.Max(floorSl, effectivePoint0) : Math.Min(floorSl, effectivePoint0);
        }

        return sl;
    }

    /// <summary>
    /// Mindest-SL-Distanz in Prozent vom Entry-Preis (Fee+Spread-Schutz).
    /// BingX Taker Round-Trip = 0.1%, Spread + Slippage ≈ 0.05% → 0.15% ist Break-Even-Floor.
    /// Wirkt zusätzlich zum Pip-Cap und Pip-Buffer (Task 4.5).
    /// </summary>
    private const decimal MinSlDistancePercent = 0.0015m;

    /// <summary>
    /// Task 3.6 — SL-Berechnung für BCKL-Re-Entry. Buch-Regel:
    /// "Bei einer BC-Korrektur: SL kommt unter Punkt B" (nicht unter Punkt 0 wie beim Primary-Entry).
    /// Punkt B ist bei BCKL die letzte verteidigte Struktur — darunter ist die BC-Welle tot.
    /// Pip-Cap und Fee-Floor bleiben aktiv, nur der Clamp wird von Point0 auf PointB verlegt.
    /// </summary>
    /// <param name="pointB">Punkt B der Sequenz (Clamp-Untergrenze für BCKL-SL statt Point0).</param>
    /// <param name="bufferPips">Task 4.5: Pip-Puffer unter Punkt B (analog zum Primary-SL-Buffer).</param>
    public static decimal CalculateBcklStopLoss(
        string symbol, MarketCategory category,
        decimal entryPrice, bool isLong,
        decimal pointB,
        bool isSingleTrade = false,
        decimal pipScale = 1.0m,
        decimal bufferPips = 0m)
    {
        // Pip-Cap: Buch sagt bei BCKL typisch 20 Pips (Cheat 49 — Multi-Trade)
        var pipDistance = CalculateSlDistance(symbol, category, entryPrice, isSingleTrade, pipScale);
        var pipCapSl = isLong ? entryPrice - pipDistance : entryPrice + pipDistance;
        var sl = pipCapSl;

        // Task 4.5: PointB-Buffer (Liquidity-Grab-Schutz unter B-Level)
        var pipValue = GetPipValue(symbol, category, entryPrice);
        var bufferDistance = bufferPips * pipValue;
        var effectivePointB = isLong ? pointB - bufferDistance : pointB + bufferDistance;

        // PointB-Clamp MIT Buffer: SL darf bis effectivePointB liegen
        sl = isLong ? Math.Max(sl, effectivePointB) : Math.Min(sl, effectivePointB);

        // Sanity: richtige Seite
        if (isLong && sl >= entryPrice) sl = entryPrice - pipDistance;
        if (!isLong && sl <= entryPrice) sl = entryPrice + pipDistance;

        // Fee-Floor 0.15% (analog zu CalculateBookStopLoss, PointB-Clamp bleibt absolut)
        var minSlDistance = entryPrice * MinSlDistancePercent;
        var actualDistance = Math.Abs(entryPrice - sl);
        if (actualDistance < minSlDistance)
        {
            var floorSl = isLong ? entryPrice - minSlDistance : entryPrice + minSlDistance;
            sl = isLong ? Math.Max(floorSl, effectivePointB) : Math.Min(floorSl, effectivePointB);
        }

        return sl;
    }

    /// <summary>
    /// 20-Pips-Buffer über dem 200er Extensionslevel (Buch Workflow 4.5).
    /// Skaliert mit Pip-Einheit des Instruments (nicht mit GBP/Exoten-Upscale).
    /// </summary>
    public static decimal Get20PipsBuffer(string symbol, MarketCategory category, decimal price)
        => 20m * GetPipValue(symbol, category, price);

    /// <summary>
    /// Anzahl Pips nach SK-Buch-Tabelle (mit Multi-Asset-Anpassung).
    /// SK-Buch Cheat Node 37: 1 Trade Strategie = Standard Stop 10-15 Pips
    /// SK-Buch Cheat Node 49: Multiple Trade Strategie = 20 Pips Stop
    /// GBP/Exoten-Paare: höherer Pip-Wert (+50%) (Cheat Node 46/48)
    /// Gold (XAU): 25/35 Pips — angehoben gegenüber Buch-Standard, weil BingX-Perp auf M15-Noise empfindlicher reagiert
    /// als Broker-Konten mit 1:100-Hebel; mit 0.15%-Floor zusammen ergibt sich ein realistisches SL-Fenster.
    /// </summary>
    private static decimal GetPipCount(string symbol, MarketCategory category, bool isSingleTrade = false)
    {
        return category switch
        {
            MarketCategory.Forex when IsGbpPair(symbol) => isSingleTrade ? 22m : 30m,
            MarketCategory.Forex when IsExoticPair(symbol) => isSingleTrade ? 22m : 30m,
            MarketCategory.Forex => isSingleTrade ? 15m : 20m,                // Cheat 37: 15, Cheat 49: 20
            MarketCategory.Commodity when IsGold(symbol) => isSingleTrade ? 25m : 35m, // Gold: angehoben (M15-Noise)
            MarketCategory.Commodity when IsSilver(symbol) => isSingleTrade ? 15m : 20m, // Silber: Buch-Standard
            MarketCategory.Commodity => 40m,                          // Öl -40 Pips
            MarketCategory.Index => 40m,                              // Indices -40 Pips
            MarketCategory.Stock => 40m,                              // Aktien wie Indices
            MarketCategory.Crypto => 100m,                            // Krypto -100 Pips
            _ => 20m
        };
    }

    /// <summary>
    /// Pip-Wert in Preis-Einheiten je nach Instrument.
    /// Gold (~2600 USD/oz): 0.1 ist die "Hochzahl" (1. Nachkommastelle).
    /// Silber (~30 USD/oz): 0.01 ist die "Hochzahl" (2. Nachkommastelle) — sonst SL = 5% vom Entry (BUG-Fix v1.2.6).
    /// Aktien: prozentualer Pip (entryPrice × 0.00005) skaliert automatisch mit Preis — robust für hochpreisige
    /// (BRK @ 600) wie auch günstige Aktien (falls BingX welche listet). 0.00005 = 50% des Crypto-Skalings,
    /// weil Aktien geringere Tagesvolatilität haben (~1-2% statt 3-5%).
    /// </summary>
    private static decimal GetPipValue(string symbol, MarketCategory category, decimal entryPrice)
    {
        return category switch
        {
            // Forex (NCFX-Perps auf BingX): prozentualer Pip wie Crypto.
            // v1.2.7 Fix — 8% WinRate auf NCFX-EUR/USD + NCFX-GBP/USD mit fixem 0.0001 Pip
            // über 5-Monate-Backtest. BingX-NCFX-Skalierung weicht von Spot-FX ab; prozentualer
            // Pip skaliert automatisch mit Preis. JPY-Sonderfall entfällt — Formel ist preis-relativ.
            MarketCategory.Forex => entryPrice * 0.0001m,
            MarketCategory.Commodity when IsGold(symbol) => 0.1m,      // Gold: 1. Nachkommastelle
            MarketCategory.Commodity when IsSilver(symbol) => 0.01m,   // Silber: 2. Nachkommastelle (v1.2.6 Bugfix)
            MarketCategory.Commodity => 0.01m,                         // Öl
            MarketCategory.Index => 1m,                                 // Indices: 1 Punkt
            MarketCategory.Stock => entryPrice * 0.00005m,             // Aktien: prozentual (v1.2.6 Defensive)
            MarketCategory.Crypto => entryPrice * 0.0001m,             // 1/10000 des Preises
            _ => 0.0001m
        };
    }

    private static bool IsGold(string symbol) =>
        symbol.Contains("GOLD", StringComparison.OrdinalIgnoreCase)
        || symbol.Contains("XAU", StringComparison.OrdinalIgnoreCase);

    private static bool IsSilver(string symbol) =>
        symbol.Contains("XAG", StringComparison.OrdinalIgnoreCase)
        || symbol.Contains("SILV", StringComparison.OrdinalIgnoreCase);

    private static bool IsGbpPair(string symbol) =>
        symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase);

    private static bool IsExoticPair(string symbol) =>
        symbol.Contains("AUD", StringComparison.OrdinalIgnoreCase)
        || symbol.Contains("CAD", StringComparison.OrdinalIgnoreCase)
        || symbol.Contains("NZD", StringComparison.OrdinalIgnoreCase);
}
