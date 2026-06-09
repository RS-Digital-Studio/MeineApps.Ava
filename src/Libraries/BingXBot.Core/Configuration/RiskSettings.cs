using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

/// <summary>Marktspezifische Risk-Overrides. Fehlende Kategorien nutzen globale RiskSettings-Defaults.</summary>
public record MarketCategorySettings
{
    public decimal MaxLeverage { get; init; } = 3m;
    /// <summary>Margin-Anteil je Trade in % der Balance. Default 10 % (bewusste User-Lockerung).</summary>
    public decimal MaxPositionSizePercent { get; init; } = 10m;
    /// <summary>Anzeige-Wert max. Margin pro Trade. Default 5 % (aliged mit MaxRiskPercentPerTrade).</summary>
    public decimal MaxMarginPerTradePercent { get; init; } = 5m;
    /// <summary>Minimales RRR pro Kategorie. SK-Buch S.13 fordert 1:1 (Buch-strikt).</summary>
    public decimal MinRiskRewardRatio { get; init; } = 1.0m;
}

/// <summary>
/// Risk-Settings strikt nach SK-Buch (Tradebook Sascha Wenzel / Stefan Kassing).
/// Alle Non-Buch-Features entfernt — der Bot implementiert ausschliesslich die Buch-Regeln.
/// </summary>
public class RiskSettings
{
    /// <summary>
    /// Max. Margin (Risikokapital) pro Trade in % der Balance. SK-Buch: 1-3 % konservativ.
    /// Bewusste User-Lockerung: 10 % als Default, damit das 5 %-Risk-Profil bei moderatem Leverage
    /// genug Margin-Spielraum hat. UI kann zurück auf Buch-Default 3 % stellen.
    /// </summary>
    public decimal MaxPositionSizePercent { get; set; } = 10m;
    /// <summary>
    /// UI-Anzeige-Wert für max. Margin pro Trade (in % der Balance). Wird in der Engine nicht direkt
    /// als Cap angewendet — der echte Risiko-Cap kommt aus <see cref="MaxRiskPercentPerTrade"/>.
    /// Default 5 % — aliged mit MaxRiskPercentPerTrade (bewusste User-Abweichung vom Buch).
    /// </summary>
    public decimal MaxMarginPerTradePercent { get; set; } = 5m;
    /// <summary>Max. Tages-Drawdown in %. 0 = deaktiviert (Buch-konform: MaxDailyLossPercent übernimmt).</summary>
    public decimal MaxDailyDrawdownPercent { get; set; } = 0m;
    /// <summary>Max. Total-Drawdown in % (Safety-Net gegen Bot-Crash).</summary>
    public decimal MaxTotalDrawdownPercent { get; set; } = 10m;
    /// <summary>Max. gleichzeitig offene Positionen. SK-Buch Workflow: max. 3 Trades parallel.</summary>
    public int MaxOpenPositions { get; set; } = 3;
    public int MaxOpenPositionsPerSymbol { get; set; } = 1;

    /// <summary>Max. Leverage-Faktor.</summary>
    public decimal MaxLeverage { get; set; } = 10m;

    // BUCH-ONLY: Kein Pearson-Korrelations-Check. Das Buch kennt keine quantitative Korrelations-
    // Berechnung. Der Cluster-Filter (Phase 18 / A4) ist eine bewusste User-Ausnahme — er ersetzt
    // keine Buch-Regel, sondern adressiert ein Phase-0-Risiko (Konto-Schutz beim Flash-Crash) das
    // im Buch nicht abgedeckt ist (Buch ist Forex-zentriert, Crypto-Cluster-Korrelation ist eigene Risk-Klasse).

    /// <summary>
    /// Phase 18 / A5 — Volatility-Targeting fuer Position-Sizing (opt-in, default false).
    /// Wenn true: Position-Quantity wird um <c>min(VolScaleCap, VolatilityTargetPercent / atrPct)</c>
    /// skaliert. Bei stabilen Coins (BTC, ATR ~1 %) wird die Position gegenueber dem nominalen Sizing
    /// hochgeschraubt; bei Memecoins (ATR ~8 %) heruntergeschraubt. Industriestandard fuer
    /// systematisches Trading. Wirkt VOR dem MaxRisk-Cap — die finale Risiko-Obergrenze bleibt erhalten.
    /// </summary>
    public bool EnableVolatilityTargeting { get; set; } = false;

    /// <summary>Phase 18 / A5 — Ziel-ATR in Prozent (z.B. 2.0 = 2 % je Periode). Default 2 %.</summary>
    public decimal VolatilityTargetPercent { get; set; } = 2.0m;

    /// <summary>Phase 18 / A5 — Obergrenze fuer den Skalierungs-Multiplikator (Default 1.5×).</summary>
    public decimal VolatilityScaleCap { get; set; } = 1.5m;

    /// <summary>
    /// Phase 18 / A4 — Cluster-Korrelations-Filter (opt-in, default 0 = aus).
    /// Wenn &gt; 0: Vor jedem Trade wird die Summe der Margins aller offenen Positionen im selben
    /// <see cref="AssetCluster"/> berechnet. Wenn (Σ + neue Margin) &gt; <c>X %</c> der Wallet-Balance,
    /// wird der Trade abgelehnt mit <see cref="RejectionReasons.CorrelationLimitExceeded"/>.
    /// Empfehlung: 30 %. Schuetzt vor "3× BTC durch BTC/ETH/SOL parallel"-Disasters bei Flash-Crashes.
    /// </summary>
    public decimal MaxCorrelatedExposurePercent { get; set; } = 0m;

    /// <summary>
    /// Cross-Asset-Netto-Direktions-Limit (opt-in, default 0 = aus). Wenn &gt; 0: Vor jedem Trade
    /// wird das gleichgerichtete Netto-Notional ALLER offenen Positionen (Crypto + TradFi) plus
    /// das geplante Notional gegen <c>X %</c> der Wallet-Balance geprueft. Schuetzt vor einem
    /// einseitigen Gesamt-Buch (z.B. 8 Shorts ueber Indizes/Aktien/Rohstoffe/Crypto = 159 %
    /// Net-Short), das ein einzelner Markt-Rebound komplett gleichzeitig trifft. Notional-basiert,
    /// damit Hochhebel-Positionen (Forex 20x) nicht unterschaetzt werden. Richtwert: 100-150.
    /// </summary>
    public decimal MaxNetDirectionalExposurePercent { get; set; } = 0m;

    // === v1.7.0 Phase 16 — Cross-TF-Position-Pyramidisierung (User-Ausnahme, NICHT im Buch) ===
    /// <summary>
    /// Bewusste Buch-Abweichung. Wenn aktiv: ein offenes Long/Short kann durch ein zweites
    /// Long/Short auf hoeherer TF um <see cref="PyramidScalePercent"/> der Original-Groesse
    /// erweitert werden. Default false. Pro-Trader-Praxis fuer Trend-Confirmation-Adds.
    /// </summary>
    public bool EnableCrossTfPyramiding { get; set; } = false;

    /// <summary>Anzahl zusaetzlicher Pyramid-Adds pro Position (Default 1).</summary>
    public int PyramidMaxAddOns { get; set; } = 1;

    /// <summary>
    /// Skalierung der Add-On-Position relativ zur Original-Quantitaet (Default 0.5 = 50 %).
    /// </summary>
    public decimal PyramidScalePercent { get; set; } = 0.5m;

    /// <summary>
    /// Task 4.7 — Runner-TP opt-in. Buch: "manche Trader lassen am 200%-Level noch 5-10%
    /// als Runner für mögliche Überschießungen (261.8% oder 423.6%) laufen und trailen den
    /// Stop-Loss aggressiv nach." Default false (Buch: opt-in, nicht Standard).
    /// </summary>
    public bool EnableRunner { get; set; } = false;
    /// <summary>Task 4.7 — Anteil der Position der als Runner weiterläuft (Buch: 5-10%, Default 10%).</summary>
    public decimal RunnerPercent { get; set; } = 0.10m;
    /// <summary>Task 4.7 — ATR-Multiplikator für Trailing-Stop des Runners (Buch: "aggressiv nachtrailen").</summary>
    public decimal RunnerTrailingAtrMultiplier { get; set; } = 2.0m;

    /// <summary>
    /// Break-Even-Trigger als Vielfaches der SL-Distanz (R-Multiple). Sobald der Preis
    /// <c>Entry ± BreakevenTriggerRMultiple × |Entry − SL|</c> in Profit-Richtung erreicht, zieht der
    /// SL auf Break-Even (siehe <see cref="BingXBot.Core.Services.BreakevenCalculator"/>). Default 2.0
    /// (= BE bei 2R, historisch). 1.5 ≈ BE sobald das TP1-Level (1.5R bei TrendFollow) erreicht ist —
    /// die Rest-Position nach der TP1-Teilschliessung laeuft dann risikofrei. 0 deaktiviert den
    /// 2x-SL-BE-Trigger (nur der A-Bruch-Trigger bleibt aktiv, fuer SK relevant).
    /// </summary>
    public decimal BreakevenTriggerRMultiple { get; set; } = 2.0m;

    // BUCH-ONLY: EnforceFahrplanAlignment entfernt. Das Buch kennt keinen "Trade gegen Fahrplan
    // blockieren"-Filter — die einzige Multi-TF-Regel ist die MTA in ScannerSettings
    // (BlockLtfEntryWhenHtfInTargetZone).

    // === Partial Close (TP1 + TP2) — Buch S.16 Zielbereich 161.8-200% ===
    /// <summary>
    /// Anteil der Position bei TP1 (161.8% Extension) geschlossen. Buch Masterclass (Task 4.6):
    /// "Schließe hier 50% bis 80% deiner Position ab." Range 0.5-0.8 (bei Set automatisch geklemmt).
    /// Default 0.5 = konservative Buch-Variante. 0.8 = aggressive Gewinnsicherung.
    /// </summary>
    public decimal Tp1CloseRatio
    {
        get => _tp1CloseRatio;
        set => _tp1CloseRatio = Math.Clamp(value, 0.5m, 0.8m);
    }
    private decimal _tp1CloseRatio = 0.5m;
    /// <summary>
    /// Anteil der Position bei TP2 (200%) geschlossen. Bei aktivem Runner (Task 4.7):
    /// TP2 = 1 - Tp1CloseRatio - RunnerPercent. Default 0.5 = kompletter Rest ohne Runner.
    /// </summary>
    public decimal Tp2CloseRatio { get; set; } = 0.5m;

    /// <summary>
    /// Globales Minimum-RRR als Fallback (Strategy + CategorySettings haben eigenen Check).
    /// 0 = deaktiviert (Strategy-Check + CategorySettings genügen).
    /// </summary>
    public decimal MinRiskRewardRatio { get; set; } = 0m;

    /// <summary>
    /// Hard-Cap für Risiko pro Trade (|Entry-SL|*Qty / Equity). SK-Buch: 1-3 %. 0 = deaktiviert.
    /// Bewusste User-Abweichung vom Buch: 5 % (aggressivere Sizing-Wahl, dokumentiert in
    /// <c>src/Apps/BingXBot/CLAUDE.md</c>). MaxDailyRiskPercent dient als Tages-Gesamtschirm.
    /// </summary>
    public decimal MaxRiskPercentPerTrade { get; set; } = 5m;

    /// <summary>
    /// Max Daily Loss Circuit-Breaker in % der Equity. Nach Überschreitung keine neuen Entries bis UTC-00:00.
    /// Default: 0 = deaktiviert (Buch-konform, User-Entscheidung). Empfehlung: 5%.
    /// </summary>
    public decimal MaxDailyLossPercent { get; set; } = 0m;

    // === Phase 18 / SK-Plan 4.8 + 5.1 — Adaptive Position-Scaling ===
    /// <summary>
    /// SK-Plan 4.8 (Buch S.13) — Loss-Streak-Dampening.
    /// Wenn aktiv: ab <see cref="LossStreakHalveAtCount"/> Verlusten in Folge → Position auf 50 %
    /// skalieren, ab <see cref="LossStreakPauseAtCount"/> → Pause (Faktor 0). Wirkt in
    /// <see cref="GetPositionScalingFactor"/> als Multiplikator vor dem MaxRisk-Cap.
    /// Default true — Buch-empfohlener Standard-Schutz.
    /// </summary>
    public bool EnableLossStreakDampening { get; set; } = true;

    /// <summary>
    /// Schwelle (in aufeinanderfolgenden Verlust-Trades), ab der die Position halbiert wird.
    /// Wirkt nur wenn <see cref="EnableLossStreakDampening"/> = true.
    /// Buch S.13 nennt 3; bei 5 %-Risk lockern wir auf 4 (mehr Recovery-Trades).
    /// </summary>
    public int LossStreakHalveAtCount { get; set; } = 4;

    /// <summary>
    /// Schwelle (in aufeinanderfolgenden Verlust-Trades), ab der eine Trading-Pause greift
    /// (Position-Scaling-Faktor 0). Wirkt nur wenn <see cref="EnableLossStreakDampening"/> = true.
    /// Buch S.13 nennt 5; bei 5 %-Risk lockern wir auf 7 (Bot-Pause erst bei schwerer Drawdown-Phase).
    /// </summary>
    public int LossStreakPauseAtCount { get; set; } = 7;

    /// <summary>
    /// Margin-Cap (Σ aller offenen Margins + neue Margin) in % der Wallet-Balance.
    /// Schützt vor Cross-Margin-Liquidation bei mehreren parallelen Trades mit hohem Hebel.
    /// Default 80 % (bewusste Lockerung gegenüber dem früheren Hardcode 60 %). 0 = deaktiviert.
    /// Bei Überschreitung wird die Position-Size reduziert; bei vollständig genutztem Cap wird
    /// der Trade abgelehnt mit Reason "Margin-Cap erreicht".
    /// </summary>
    public decimal MaxTotalMarginPercent { get; set; } = 80m;

    /// <summary>
    /// Minimaler Rest-Anteil der Position-Size nach dem MaxRiskPercentPerTrade-Cap, unter dem das
    /// Setup komplett verworfen wird (statt mit Mini-Position zu traden). Range 0..1.
    /// Default 0.1 (= 10 % der originalen Größe). Früherer Hardcode war 0.01 (= 1 %), was bei
    /// weiten SLs zu vielen unnötigen Rejects führte. 0 = Reject-Schwelle deaktiviert (Trade
    /// läuft mit beliebig kleiner Größe weiter).
    /// </summary>
    public decimal MinPositionSizeRetentionPercent { get; set; } = 0.1m;

    /// <summary>
    /// SK-Plan 5.1 — Equity-Curve-Scaling (opt-in).
    /// Wenn aktiv: Bei laufendem Drawdown vom Peak (in % der Equity) wird die Position-Size
    /// ab <see cref="EquityCurveScalingThresholdPercent"/> linear bis auf 50 % gedämpft.
    /// Default false — komplementär, aber konservativer als Loss-Streak-Dampening.
    /// </summary>
    public bool EnableEquityCurveScaling { get; set; } = false;

    /// <summary>
    /// SK-Plan 5.1 — Schwelle (Drawdown vom Peak in %), ab der das Equity-Curve-Scaling greift.
    /// Bei (Schwelle + 10%) Drawdown ist der Faktor bei 0.5×.
    /// Default 5 % (z.B. von 10.000 USDT auf 9.500 USDT).
    /// </summary>
    public decimal EquityCurveScalingThresholdPercent { get; set; } = 5m;

    /// <summary>
    /// Task 3.3 — Max. kumuliertes Tagesrisiko in % der Equity (Buch S.13: "1-3% pro Tag insgesamt").
    /// Summiert realisierte Verluste + geplante Trade-Risiken (Entry-SL-Distanz × Qty).
    /// Default: 0 = deaktiviert (User-Opt-In). Empfehlung laut SK-Buch: 3% (Max).
    /// Wirkt zusätzlich zu <see cref="MaxRiskPercentPerTrade"/> — einzelner Trade hart-cap,
    /// Tagesbudget als Gesamtgrenze.
    /// </summary>
    public decimal MaxDailyRiskPercent { get; set; } = 0m;

    /// <summary>
    /// Multi-TF Standalone (15.04.2026, auf M15 umgestellt 19.04.2026): Pip-SL-Skalierung pro Navigator-TF.
    /// Default: 1.0 für die swing-orientierten TFs, M15 = 0.75 (moderater Pip-Cap für das untere Ende).
    /// Die SL-Struktur greift primär über Next-Level + Pip-Buffer (Task 4.5) — Pip-Scaling ist nur noch Safety-Net.
    /// </summary>
    public Dictionary<TimeFrame, decimal> PipScalingByTf { get; set; } = new()
    {
        { TimeFrame.D1, 1.0m },
        { TimeFrame.H4, 1.0m },
        { TimeFrame.H1, 1.0m },
        { TimeFrame.M15, 0.75m },
    };

    /// <summary>
    /// Task 4.5 — SL-Buffer in Pips unter (Long) bzw. über (Short) Punkt 0. Buch-Zitat:
    /// "Gib dem SL immer ein wenig Luft für den Broker-Spread und Liquiditätsabgriffe (Spikes/Wicks).
    /// Setze ihn z.B. 5-15 Pips (je nach Zeiteinheit) unter das absolute Tief von Punkt 0."
    /// Höhere TFs brauchen mehr Puffer, M15 am wenigsten.
    /// Pip-Wert wird per Asset-Klasse berechnet (PipStopLossCalculator.GetPipValue).
    /// </summary>
    public Dictionary<TimeFrame, decimal> SlBufferPipsByTf { get; set; } = new()
    {
        { TimeFrame.W1, 15m },
        { TimeFrame.D1, 15m },
        { TimeFrame.H4, 12m },
        { TimeFrame.H1, 8m },
        { TimeFrame.M15, 5m },
    };

    // === Per-Markt Risk-Overrides ===
    /// <summary>
    /// Marktspezifische Risk-Settings. MinRRR = 1:1 (Buch S.13).
    /// MaxPositionSizePercent/MaxMarginPerTradePercent sind an die globalen User-Defaults (10 %/5 %) angeglichen;
    /// MaxLeverage bleibt asset-typisch unterschiedlich (Crypto 5×, Forex 20×, …).
    /// </summary>
    public Dictionary<MarketCategory, MarketCategorySettings> CategorySettings { get; set; } = new()
    {
        { MarketCategory.Crypto,    new() { MaxLeverage = 5m,  MaxPositionSizePercent = 10m, MaxMarginPerTradePercent = 5m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Commodity, new() { MaxLeverage = 10m, MaxPositionSizePercent = 10m, MaxMarginPerTradePercent = 5m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Index,     new() { MaxLeverage = 10m, MaxPositionSizePercent = 10m, MaxMarginPerTradePercent = 5m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Forex,     new() { MaxLeverage = 20m, MaxPositionSizePercent = 10m, MaxMarginPerTradePercent = 5m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Stock,     new() { MaxLeverage = 5m,  MaxPositionSizePercent = 10m, MaxMarginPerTradePercent = 5m, MinRiskRewardRatio = 1.0m } },
    };

    public MarketCategorySettings GetCategorySettings(MarketCategory category)
        => CategorySettings.GetValueOrDefault(category, new MarketCategorySettings
        {
            MaxLeverage = MaxLeverage,
            MaxPositionSizePercent = MaxPositionSizePercent,
            MaxMarginPerTradePercent = MaxMarginPerTradePercent,
            MinRiskRewardRatio = MinRiskRewardRatio
        });

    /// <summary>
    /// Migriert Legacy-M5-Einträge aus persistierten Settings (19.04.2026: M5-Navigator durch M15 ersetzt).
    /// Entfernt M5-Key aus <see cref="PipScalingByTf"/>; der Code-Default (Fallback im Strategy-Switch) deckt M15 mit 0.75 ab.
    /// </summary>
    public void MigrateLegacyM5()
    {
        PipScalingByTf?.Remove(TimeFrame.M5);
    }
}
