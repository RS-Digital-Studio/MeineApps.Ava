using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

public class ScannerSettings
{
    /// <summary>
    /// Task 4.9 — Automatischer Bias-Flip nach Punkt-0-Bruch einer aktivierten Sequenz.
    /// Buch-Masterclass: "Ein gebrochener Punkt 0 einer Aufwärtssequenz markiert sehr oft den
    /// Punkt A einer neuen, frischen Abwärtssequenz. [...] Professionelle SK-Trader drehen
    /// hier oft ihre Bias." Default: true (buchtreu).
    /// </summary>
    public bool EnableBiasFlip { get; set; } = true;

    /// <summary>
    /// Task 4.10 — Counter-Trend-Scalper (Trade gegen Haupt-Trend an Extension 161.8%/200%).
    /// Buch: "Counter-Trend-Trading ist hochriskant. Default false (opt-in).
    /// </summary>
    public bool EnableCounterTrendScalp { get; set; } = false;

    // ═══════════════════════════════════════════════════════════════
    // Strukturpunkte-Doku (SK-Strukturpunkte.docx) — harte Filter-Regeln
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Strukturpunkte-Doku §2: Impuls-Distanz-Filter. |PointA - Point0| muss ≥ <see cref="ImpulseAtrMultiplier"/> × ATR_14 sein,
    /// sonst wird die Sequenz verworfen. Doku-Spanne erlaubt 2.0-3.0 — Default 2.0 (mehr handelbare
    /// Sequenzen in liquiden BingX-Perps, ohne den Rausch-Schutz aufzugeben). 3.0 bleibt für sehr
    /// strenge Filterung per UI einstellbar.
    /// </summary>
    public decimal ImpulseAtrMultiplier { get; set; } = 2.0m;

    /// <summary>
    /// Strukturpunkte-Doku §5A: Hart-Filter für BOS-Volumen. Aktivierungs-Kerze (Close > PointA bei Long,
    /// Close &lt; PointA bei Short) muss das <see cref="BosVolumeMultiplier"/>-fache des 20-Kerzen-SMA übersteigen.
    /// Doku-Zitat: "Der Impuls MUSS überdurchschnittliches Volumen aufweisen, sonst ist es ein Fakeout."
    /// Default: false (User-Entscheidung 22.04.2026). §5A ist in der Doku als "Profi-Erweiterung für
    /// 100% Perfektion" gekennzeichnet — für BingX-Perps zu scharf. Volumen bleibt als Bonus-Confluence
    /// aktiv (SequenceDetector.DetectEntryConfirmation +1 Score), blockt aber keine Sequenz mehr.
    /// </summary>
    public bool RequireBosVolumeBreakout { get; set; } = false;

    /// <summary>
    /// Strukturpunkte-Doku §5A: Volumen-Multiplikator für BOS-Kerzen. Default: 1.5 (Doku).
    /// Aktivierungs-Kerze.Volume muss ≥ SMA(Volume, 20) × <see cref="BosVolumeMultiplier"/> sein.
    /// Nur aktiv wenn <see cref="RequireBosVolumeBreakout"/> = true.
    /// </summary>
    public decimal BosVolumeMultiplier { get; set; } = 1.5m;

    /// <summary>
    /// Strukturpunkte-Doku §5B: ATR-adaptive Pivot-Länge. Wenn true: Swing-Strength wird dynamisch an die
    /// aktuelle ATR% gekoppelt — hohe Vola = mehr Kerzen links/rechts (bis 10), niedrige Vola = weniger (bis 3).
    /// Default: false (statische 5-Kerzen-Logik bleibt). Bei Aktivierung wird <see cref="SwingStrengthMin"/> und
    /// <see cref="SwingStrengthMax"/> als Range und <see cref="SwingStrengthAtrThresholdLow"/>/
    /// <see cref="SwingStrengthAtrThresholdHigh"/> als ATR%-Grenzen verwendet.
    /// </summary>
    public bool AdaptiveSwingStrength { get; set; } = true;

    /// <summary>Strukturpunkte-Doku §5B: Minimale Swing-Strength bei niedriger Vola (Doku: 3).</summary>
    public int SwingStrengthMin { get; set; } = 3;

    /// <summary>Strukturpunkte-Doku §5B: Maximale Swing-Strength bei hoher Vola (Doku: 10).</summary>
    public int SwingStrengthMax { get; set; } = 10;

    /// <summary>Strukturpunkte-Doku §5B: ATR% unter diesem Wert gilt als "niedrige Vola" (Default 0.5%).</summary>
    public decimal SwingStrengthAtrThresholdLow { get; set; } = 0.5m;

    /// <summary>Strukturpunkte-Doku §5B: ATR% über diesem Wert gilt als "hohe Vola" (Default 3.0%).</summary>
    public decimal SwingStrengthAtrThresholdHigh { get; set; } = 3.0m;

    /// <summary>
    /// Strukturpunkte-Doku §1: Asymmetrische Pivot-Fenster. Wenn beide &gt; 0, nutzt die Sequenz-Erkennung
    /// <see cref="PivotLeftBars"/> Kerzen links und <see cref="PivotRightBars"/> Kerzen rechts vom Kandidaten
    /// (Doku-Defaults 5/3). Wenn einer 0, fällt der Code auf das symmetrische Strength-Schema zurück.
    /// Gilt für die Confluence-Swing-Erkennung in der Strategie (nicht für die State-Machine).
    /// </summary>
    public int PivotLeftBars { get; set; } = 5;

    /// <summary>Strukturpunkte-Doku §1: Rechte Pivot-Bars (Doku-Bereich 3-5). 0 = symmetrisch fallback.</summary>
    public int PivotRightBars { get; set; } = 3;

    /// <summary>
    /// Strukturpunkte-Doku §3: Pivot-Stärke für BOS-Anker-Suche (Last_Swing_High/Low VOR Point0).
    /// BOS-Gate ist per Buch-Regel IMMER aktiv ("Ohne BOS keine SK-System-Messung").
    /// <para>
    /// Backward-Compat-Fallback: Wird nur ausgewertet, wenn <see cref="BosAnchorLeftBars"/> oder
    /// <see cref="BosAnchorRightBars"/> ≤ 0 sind. Bei beiden &gt; 0 zieht das asymmetrische Paar.
    /// </para>
    /// Default: 5 (Doku-Mittel der 3-10-Spanne).
    /// </summary>
    public int BosAnchorSwingStrength { get; set; } = 5;

    /// <summary>
    /// Strukturpunkte-Doku §1+§3 (25.04.2026): Asymmetrische BOS-Anker-Pivots.
    /// Linke Bars = wie weit zurück die Bestätigung reicht (Default 5; Spanne 5-10). Wenn beide
    /// (Left+Right) &gt; 0, wird der BOS-Anker mit asymmetrischem Pivot-Fenster gesucht — sonst
    /// fällt der Code auf <see cref="BosAnchorSwingStrength"/> (symmetrisch) zurück.
    /// </summary>
    public int BosAnchorLeftBars { get; set; } = 5;

    /// <summary>
    /// Strukturpunkte-Doku §1+§3 (25.04.2026): Rechte BOS-Anker-Pivot-Bars (Default 3; Spanne 3-5).
    /// Weniger als links → schnellere Erkennung bei gleicher Signifikanz. 0 = symmetrisch fallback.
    /// </summary>
    public int BosAnchorRightBars { get; set; } = 3;

    /// <summary>
    /// Strukturpunkte-Doku §3: Bei true muss der BODY-Close die Last-Swing-Grenze überschreiten (striktes BOS).
    /// Bei false reicht ein Docht darüber (loser BOS). Default: false — die Strukturpunkte-Doku formuliert
    /// die Regel nicht eindeutig als reinen Body-Close, ein Docht-Bruch reicht für die meisten Setups.
    /// Wer Buch-strikt fahren will, schaltet das per UI auf true.
    /// </summary>
    public bool RequireBosCloseBreak { get; set; } = false;

    /// <summary>
    /// Spec §7 (MTA): Blockiere LTF-Entry wenn HTF-Sequenz in ihrer Zielzone (EXT_1618-EXT_2000) steht.
    /// Doku-Zitat: "Führe Long-Trades auf dem Lower Timeframe (LTF) nur aus, wenn sich der Preis nicht in der
    /// Zielzone (EXT_1618-EXT_2000) des Higher Timeframes (HTF) befindet."
    /// Default: false — bewusste User-Lockerung: in HTF-Zielzonen finden sich häufig die besten
    /// Counter-Setups. Wer Buch-konform MTA fahren möchte, schaltet das per UI auf true.
    /// </summary>
    public bool BlockLtfEntryWhenHtfInTargetZone { get; set; } = false;

    /// <summary>
    /// Spec §7 (Confluence Engine / "Heiliger Gral"): Aktiviert den geometrischen Overlap-Check zwischen
    /// HTF-GKL-Zone und LTF-BC-Zone bzw. LTF-EXT-1.618-Gegenrichtung. Bei Treffer wird die Confluence-Kategorie
    /// <c>HighProbabilityZone</c> vergeben (+2 Gewicht, analog GKL-Masterzone).
    /// Doku-Zitat: "IF (HTF_GKL_Zone overlaps with LTF_BC_Zone) OR (HTF_GKL_Zone overlaps with LTF_Target_Zone_EXT_1618
    /// der Gegenrichtung): Markiere diese Zone als HIGH_PROBABILITY_ZONE."
    /// Default: true (das ist kein Block, sondern ein Bonus — kann nicht schaden).
    /// </summary>
    public bool EnableConfluenceOverlapDetection { get; set; } = true;

    /// <summary>
    /// v1.5.4 Phase 7 — Funding-Rate Soft-Bonus (User-Erweiterung, nicht im Buch).
    /// True (Default) = Confluence-Score bekommt +1 wenn die Funding-Rate in Trade-Richtung
    /// favorisiert (Long bei stark negativer Funding, Short bei stark positiver Funding).
    /// Schwelle 0.05 % (5 Basispunkte) — siehe <c>FundingRateBonusThresholdPercent</c>.
    /// </summary>
    public bool EnableFundingRateBonus { get; set; } = true;

    /// <summary>
    /// v1.5.4 Phase 7 — Schwelle (in %, z.B. 0.05 = 0.05 %) ab der die Funding-Rate als
    /// "favorabel" zaehlt. Default 0.05 % (= ~0.0005 als Decimal).
    /// </summary>
    public decimal FundingRateBonusThresholdPercent { get; set; } = 0.05m;

    /// <summary>
    /// v1.6.2 Phase 12 — Slippage-Guard fuer Market-Orders. Default true.
    /// Wenn aktiv: vor jedem Market-Entry wird ein OrderBook-Snapshot geholt + Slippage
    /// geschaetzt; bei Slippage > Schwelle wird die Order geblockt + Decision-Trail-Log mit
    /// Reason "slippage_too_high".
    /// </summary>
    public bool SlippageGuardEnabled { get; set; } = true;

    /// <summary>
    /// v1.6.2 Phase 12 — Globale Default-Slippage-Schwelle in % (Fallback wenn die Kategorie
    /// keinen eigenen Wert in <see cref="MaxSlippagePercentByCategory"/> hat).
    /// Crypto 0.10 %, Forex 0.05 %, Stock 0.30 % sind die Plan-Vorgaben — siehe Dictionary.
    /// </summary>
    public decimal MaxSlippagePercent { get; set; } = 0.10m;

    /// <summary>
    /// Per-Kategorie-Override für die Slippage-Schwelle (Default-Werte sind die Plan-Vorgaben
    /// aus dem Code-Kommentar zu <see cref="MaxSlippagePercent"/>). Memecoins und illiquide
    /// TradFi-Symbole haben strukturell breitere Spreads — der globale Default 0.10 % blockte
    /// diese systematisch. Wer die Schwelle global lassen möchte, leert das Dictionary.
    /// </summary>
    public Dictionary<MarketCategory, decimal> MaxSlippagePercentByCategory { get; set; } = new()
    {
        { MarketCategory.Crypto,    0.10m },
        { MarketCategory.Forex,     0.05m },
        { MarketCategory.Index,     0.20m },
        { MarketCategory.Commodity, 0.20m },
        { MarketCategory.Stock,     0.30m },
    };

    /// <summary>Liefert die Slippage-Schwelle für eine Kategorie (Fallback: globaler Wert).</summary>
    public decimal GetMaxSlippagePercent(MarketCategory category)
        => MaxSlippagePercentByCategory.TryGetValue(category, out var v) && v > 0 ? v : MaxSlippagePercent;

    // === v1.6.6 Phase 17 — Adaptive TF-Disable ===
    /// <summary>
    /// True = TFs mit schlechter WinRate werden automatisch fuer 24 h aus dem Scanner-Pfad
    /// genommen (Self-Healing). Default false — opt-in.
    /// </summary>
    public bool EnableAdaptiveTfDisable { get; set; } = false;

    /// <summary>Mindest-Sample-Size pro TF bevor Disable triggert (Default 20).</summary>
    public int AdaptiveTfMinTrades { get; set; } = 20;

    /// <summary>
    /// WinRate-Schwelle (0.0-1.0). Default 0.30 — TFs unter 30 % WinRate werden disabled.
    /// </summary>
    public decimal AdaptiveTfMinWinRate { get; set; } = 0.30m;

    /// <summary>Wie lange (Stunden) bleibt eine TF disabled, bevor Re-Probing? Default 24.</summary>
    public int AdaptiveTfDisableHours { get; set; } = 24;

    // ═══════════════════════════════════════════════════════════════
    // Multi-TF Standalone (15.04.2026) — ein Service, mehrere TFs parallel
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktive Navigator-Timeframes. Jede TF wird eigenständig pro Symbol evaluiert
    /// (eigene Sequenz, eigenes Fib-Entry-Level, eigener SL). Default: alle 4 aktiv.
    /// User schaltet via Settings-Checkbox einzelne TFs ab (z.B. nur D1+H4 am Anfang).
    /// </summary>
    public List<TimeFrame> ActiveTimeframes { get; set; } = new()
    {
        TimeFrame.D1, TimeFrame.H4, TimeFrame.H1, TimeFrame.M15
    };

    /// <summary>
    /// Per-TF Min-Volume24h-Filter für Krypto (24/7 hohe Liquidität).
    /// M15 wurde auf H1-Niveau gesenkt (10M), damit Mid-Cap-Alts (Rank 50-100) auf M15
    /// handelbar bleiben — höhere Schwelle filterte zu viele Kandidaten vor der Strategy.
    /// </summary>
    public Dictionary<TimeFrame, decimal> MinVolume24hByTf { get; set; } = new()
    {
        { TimeFrame.D1, 10_000_000m },
        { TimeFrame.H4, 10_000_000m },
        { TimeFrame.H1, 20_000_000m },
        { TimeFrame.M15, 10_000_000m },
    };

    /// <summary>
    /// Per-TF Min-Volume24h-Filter für TradFi (Features-Perps NC*-Prefix).
    /// Deutlich niedriger als Krypto: GOLD/WTI/SP500/NAS100 liegen typisch 15-500M,
    /// die meisten Aktien + Forex-Paare aber deutlich unter 10M. Default-Werte zielen
    /// darauf ab dass zumindest die großen Indices/Metalle auch auf M15 handelbar bleiben.
    /// </summary>
    public Dictionary<TimeFrame, decimal> MinVolume24hTradFiByTf { get; set; } = new()
    {
        { TimeFrame.D1, 1_000_000m },
        { TimeFrame.H4, 1_000_000m },
        { TimeFrame.H1, 2_000_000m },
        { TimeFrame.M15, 3_000_000m },
    };

    /// <summary>Per-TF Min-PriceChange-Filter (Prozent). Gilt für Krypto.</summary>
    public Dictionary<TimeFrame, decimal> MinPriceChangeByTf { get; set; } = new()
    {
        { TimeFrame.D1, 0.5m },
        { TimeFrame.H4, 0.3m },
        { TimeFrame.H1, 0.2m },
        { TimeFrame.M15, 0.15m },
    };

    /// <summary>Per-TF Min-PriceChange-Filter TradFi (niedriger: Indices/Forex bewegen weniger pro Tag).</summary>
    public Dictionary<TimeFrame, decimal> MinPriceChangeTradFiByTf { get; set; } = new()
    {
        { TimeFrame.D1, 0.2m },
        { TimeFrame.H4, 0.15m },
        { TimeFrame.H1, 0.1m },
        { TimeFrame.M15, 0.08m },
    };

    /// <summary>Per-TF MaxResults-Kandidaten (gesamt Krypto+TradFi). M15 moderat (zwischen H1 und Scalp).</summary>
    public Dictionary<TimeFrame, int> MaxResultsByTf { get; set; } = new()
    {
        { TimeFrame.D1, 30 },
        { TimeFrame.H4, 50 },
        { TimeFrame.H1, 40 },
        { TimeFrame.M15, 30 },
    };

    // BUCH-ONLY: MinConfluenceScoreByTf entfernt. Das Buch kennt keinen quantitativen
    // Confluence-Score als Hard-Threshold — Confluence wird qualitativ beschrieben
    // ("Heiliger Gral" = HTF_GKL ∩ LTF_BC).

    // ═══════════════════════════════════════════════════════════════
    // Legacy-Felder (werden weiterhin unterstützt für Backtest / UI)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Legacy-Single-TF-Volume-Filter. Bevorzugt: <see cref="MinVolume24hByTf"/> für Multi-TF.</summary>
    [Obsolete("Multi-TF ist die Wahrheit — nutze MinVolume24hByTf. Wird in v1.4.x entfernt.")]
    public decimal MinVolume24h { get; set; } = 1_000_000m;
    /// <summary>Legacy-Single-TF-Preisänderung. Bevorzugt: <see cref="MinPriceChangeByTf"/> für Multi-TF.</summary>
    [Obsolete("Multi-TF ist die Wahrheit — nutze MinPriceChangeByTf. Wird in v1.4.x entfernt.")]
    public decimal MinPriceChange { get; set; } = 0.1m;
    /// <summary>Scanner-Default-Timeframe (nur für Backtest/UI — Live-Scanner nutzt ActiveTimeframes).</summary>
    public TimeFrame ScanTimeFrame { get; set; } = TimeFrame.H4;
    public List<string> Blacklist { get; set; } = new();
    public List<string> Whitelist { get; set; } = new();
    /// <summary>Legacy-Single-TF-Result-Cap. Bevorzugt: <see cref="MaxResultsByTf"/> für Multi-TF.</summary>
    [Obsolete("Multi-TF ist die Wahrheit — nutze MaxResultsByTf. Wird in v1.4.x entfernt.")]
    public int MaxResults { get; set; } = 100;
    /// <summary>SK = Mean-Reversion (nicht Momentum).</summary>
    public ScanMode Mode { get; set; } = ScanMode.Reversal;

    /// <summary>Top-N Coins nach Volume/Market-Cap.</summary>
    public bool OnlyTopByVolume { get; set; } = true;
    /// <summary>
    /// Anzahl der Top-Coins nach Market-Cap (Fallback Volume), die in den Live-Scan einfließen.
    /// 100 schnitt rund 80 % der BingX-Perps vor den Filtern ab. Default 200 holt Mid-Caps zurück,
    /// die nachgelagerten Volume-/Volatility-Filter sortieren weiterhin schwache Kandidaten aus.
    /// </summary>
    public int TopCoinsCount { get; set; } = 200;

    /// <summary>Scan-Intervall in Sekunden. Einheitlich 60s für alle TFs (M15 erfordert regelmäßige Reaktion,
    /// langsame TFs schaden nicht durch häufigere Prüfung — Kerzen-Cache verhindert Overhead).</summary>
    public int ScanIntervalSeconds { get; set; } = 60;

    /// <summary>TradFi-Assets aktivieren (Gold, Nasdaq, Forex, Aktien).</summary>
    public bool EnableTradFi { get; set; } = true;

    /// <summary>Welche TradFi-Kategorien aktiviert sind.</summary>
    public HashSet<MarketCategory> EnabledCategories { get; set; } = new()
    {
        MarketCategory.Crypto, MarketCategory.Commodity, MarketCategory.Index,
        MarketCategory.Forex, MarketCategory.Stock
    };

    /// <summary>Legacy-Single-TF-Volume-Filter für TradFi. Bevorzugt: <see cref="MinVolume24hTradFiByTf"/>.</summary>
    [Obsolete("Multi-TF ist die Wahrheit — nutze MinVolume24hTradFiByTf. Wird in v1.4.x entfernt.")]
    public decimal MinVolume24hTradFi { get; set; } = 1_000_000m;
    /// <summary>Legacy-Single-TF-Preisänderung für TradFi. Bevorzugt: <see cref="MinPriceChangeTradFiByTf"/>.</summary>
    [Obsolete("Multi-TF ist die Wahrheit — nutze MinPriceChangeTradFiByTf. Wird in v1.4.x entfernt.")]
    public decimal MinPriceChangeTradFi { get; set; } = 0.1m;

    /// <summary>
    /// Gate für TradFi-Scanning. Default true, damit TradFi-Symbole (NC-Prefix) immer gescannt werden.
    /// Wird beim Live-Connect NICHT mehr auf false gesetzt, selbst wenn BingX im One-Way-Modus steht —
    /// LiveTradingManager versucht stattdessen BingX auf Hedge umzustellen und warnt bei Fehlschlag.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsHedgeModeActive { get; set; } = true;

    // ═══════════════════════════════════════════════════════════════
    // SK-Optimization-Plan (April 2026) — Scanner-Erweiterungen
    // ═══════════════════════════════════════════════════════════════

    // BUCH-ONLY: SequenceMaxAgeByTf entfernt. Das Buch kennt kein Sequenz-Alter-Limit —
    // eine Sequenz ist gültig bis Point 0 bricht (Invalidierung).

    // BUCH-ONLY: Per-TF-ATR-Multiplier-Maps entfernt. Das Buch kennt nur einen Impulse-Filter
    // (ATR × 3, <see cref="ImpulseAtrMultiplier"/>) — der prozentual skalierte Sekundär-Filter fällt
    // jetzt auf einen einheitlichen Default (1.0 Impulse / 1.5 Korrektur) zurück.

    // BUCH-ONLY: MinPoint0CandlesByTf entfernt. Point 0 wird via Pivot-Erkennung (leftBars/rightBars)
    // definiert — kein zusätzlicher Kerzen-Count-Filter.

    /// <summary>
    /// Migriert Legacy-M5-Einträge aus persistierten Settings (19.04.2026: M5-Navigator durch M15 ersetzt).
    /// - <see cref="ActiveTimeframes"/>: M5 → M15 (bewahrt User-Intention: die unterste aktive TF).
    /// - Per-TF-Dictionaries: Entfernt M5-Einträge, damit Code-Defaults für M15 greifen.
    ///   Migration 1:1 wäre falsch, weil M5-Werte (50M Volume, 0.5 Pip, Score 5) für Scalp kalibriert waren
    ///   und auf M15 zu streng wirken — die neuen Defaults (25M/0.75/4) sind die "richtigen" M15-Werte.
    /// Aufzurufen nach Laden aus DB und nach Deserialisierung remoter Snapshots.
    /// </summary>
    public void MigrateLegacyM5()
    {
        if (ActiveTimeframes is { Count: > 0 } && ActiveTimeframes.Contains(TimeFrame.M5))
        {
            ActiveTimeframes = ActiveTimeframes
                .Select(tf => tf == TimeFrame.M5 ? TimeFrame.M15 : tf)
                .Distinct()
                .ToList();
        }

        // Dictionary-Migration: M5-Keys verwerfen. Code-Default für M15 greift via TryGetValue-Fallback
        // bzw. Dictionary-Initializer (neue ScannerSettings-Instanz ohne DB-Overwrite).
        MinVolume24hByTf?.Remove(TimeFrame.M5);
        MinVolume24hTradFiByTf?.Remove(TimeFrame.M5);
        MinPriceChangeByTf?.Remove(TimeFrame.M5);
        MinPriceChangeTradFiByTf?.Remove(TimeFrame.M5);
        MaxResultsByTf?.Remove(TimeFrame.M5);
    }

    // === Phase 18 / D1 — Magic-Numbers aus SequenzKonzeptStrategy exposed ===
    /// <summary>
    /// Phase 18 / D1 — Swing-Stärke für Fraktal-Erkennung auf der Navigator-TF (vorher hardcoded 5
    /// in SequenzKonzeptStrategy._swingStrength). Default 5 = Buch-konform fuer H1/H4. Walk-Forward
    /// kann darueber optimieren, Settings-Audit-Trail erfasst Aenderungen.
    /// </summary>
    public int NavigatorSwingStrength { get; set; } = 5;

    /// <summary>
    /// Phase 18 / D1 — Mindest-Confluence-Fallback (vorher hardcoded 3 in
    /// SequenzKonzeptStrategy._minConfluence). BUCH-ONLY: Score ist primaer Info/Confidence,
    /// kein Hard-Threshold. Default 3.
    /// </summary>
    public int NavigatorMinConfluence { get; set; } = 3;

    /// <summary>
    /// Phase 18 / D1 — Cooldown-Kerzen zwischen BCKL-Re-Entry-Triggern (vorher const 2 in
    /// SequenzKonzeptStrategy.BcklReEntryCooldownCandles). Schuetzt vor Doppel-Trigger in
    /// derselben Kerze. Default 2.
    /// </summary>
    public int BcklReEntryCooldownCandles { get; set; } = 2;

    /// <summary>
    /// Phase 18 / D1 — Konstanter Offset fuer die Mindest-Kerzen-Anzahl beim Sequenz-Setup
    /// (vorher Magic 20 in SequenzKonzeptStrategy.cs:158: <c>navCandles.Count &lt; _swingStrength * 2 + 20</c>).
    /// Default 20 (Buch-konform).
    /// </summary>
    public int NavigatorMinCandlesOffset { get; set; } = 20;

    // === Phase 18 / F5 — Symbol-spezifischer Funding-Bonus-Threshold ===
    /// <summary>
    /// Phase 18 / F5 — Markt-Kategorie-spezifischer Multiplier auf <see cref="FundingRateBonusThresholdPercent"/>.
    /// Memecoins haben strukturell hoehere Funding-Spitzen (0.5-1 %) als Majors (selten > 0.05 %).
    /// Standard-Threshold (0.05 %) loest auf Memecoins zu oft aus → Multiplier &gt; 1.0 fuer
    /// Memecoin-Topf. Aktuell ueber MarketCategory; granular Cluster-Mapping ist Folgeschritt.
    /// Default: alle 1.0 = kein Effekt (Backwards-Compat).
    /// </summary>
    public Dictionary<MarketCategory, decimal> FundingThresholdMultiplierByCategory { get; set; } = new()
    {
        { MarketCategory.Crypto, 1.0m },
        { MarketCategory.Forex, 1.0m },
        { MarketCategory.Index, 1.0m },
        { MarketCategory.Commodity, 1.0m },
        { MarketCategory.Stock, 1.0m },
    };

    /// <summary>Phase 18 / F5 — liefert den Multiplier fuer eine Kategorie (Default 1.0).</summary>
    public decimal GetFundingThresholdMultiplier(MarketCategory category)
        => FundingThresholdMultiplierByCategory.TryGetValue(category, out var m) && m > 0 ? m : 1.0m;
}
