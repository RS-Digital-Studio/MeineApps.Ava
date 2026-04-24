using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

/// <summary>
/// Task 3.1 — Entry-Staffelung im BC-Zone-Korrekturbereich.
/// Bestimmt wie viele parallele Entry-Levels ausgelöst werden.
/// </summary>
/// <summary>
/// Task 4.3 — Entry-Modus nach SK-Buch Masterclass.
/// Buch: Aggressiv (Limit-Order direkt) oder konservativ (auf LTF-Reversal warten).
/// </summary>
public enum EntryMode
{
    /// <summary>Aggressiv: Limit-Order direkt an 50/55.9/66.7% — wie bisher.</summary>
    Aggressive,
    /// <summary>Konservativ: Signal NUR wenn Preis in Korrekturbox UND LTF-Reversal bestätigt.</summary>
    Conservative,
    /// <summary>Beide: Aggressiv-Limits + zusätzlich LTF-Trigger als Bonus (Default).</summary>
    Both
}

public enum BCZoneEntryStrategy
{
    /// <summary>Nur Primary-Entry bei 50% Retracement (aggressivste Buch-Variante).</summary>
    Single,
    /// <summary>Primary @ 50% + Additional @ 66.7% — Buch-Standard (Anker an beiden Box-Enden).</summary>
    Dual
}

/// <summary>Marktspezifische Risk-Overrides. Fehlende Kategorien nutzen globale RiskSettings-Defaults.</summary>
public record MarketCategorySettings
{
    public decimal MaxLeverage { get; init; } = 3m;
    public decimal MaxPositionSizePercent { get; init; } = 3m;
    /// <summary>SK-Buch S.13: 1% Risiko pro Trade (1-3% Tagesrisiko, bei mehreren Trades aufteilen).</summary>
    public decimal MaxMarginPerTradePercent { get; init; } = 1m;
    /// <summary>Minimales RRR pro Kategorie. SK-Buch S.13 fordert 1:1 (Buch-strikt).</summary>
    public decimal MinRiskRewardRatio { get; init; } = 1.0m;
}

/// <summary>
/// Risk-Settings strikt nach SK-Buch (Tradebook Sascha Wenzel / Stefan Kassing).
/// Alle Non-Buch-Features entfernt — der Bot implementiert ausschliesslich die Buch-Regeln.
/// </summary>
public class RiskSettings
{
    /// <summary>Max. Margin pro Trade in % der Balance. SK-Buch: 1-3%, konservativ 1%.</summary>
    public decimal MaxPositionSizePercent { get; set; } = 3m;
    /// <summary>Max. Risiko (Verlust bei SL-Hit) pro Trade in % der Balance. SK-Buch S.13: 1%.</summary>
    public decimal MaxMarginPerTradePercent { get; set; } = 1m;
    /// <summary>Max. Tages-Drawdown in %. 0 = deaktiviert (Buch-konform: MaxDailyLossPercent übernimmt).</summary>
    public decimal MaxDailyDrawdownPercent { get; set; } = 0m;
    /// <summary>Max. Total-Drawdown in % (Safety-Net gegen Bot-Crash).</summary>
    public decimal MaxTotalDrawdownPercent { get; set; } = 10m;
    /// <summary>Max. gleichzeitig offene Positionen. SK-Buch Workflow: max. 3 Trades parallel.</summary>
    public int MaxOpenPositions { get; set; } = 3;
    public int MaxOpenPositionsPerSymbol { get; set; } = 1;

    /// <summary>Max. Leverage-Faktor.</summary>
    public decimal MaxLeverage { get; set; } = 10m;

    // BUCH-ONLY: Kein Korrelations-Check. Das Buch kennt keine Pearson-Korrelation zwischen offenen
    // Positionen — Risiko-Diversifikation passiert ueber Risk-Per-Trade + Positionsgroesse.

    /// <summary>
    /// Task 3.1 — Entry-Staffelung im BC-Zone-Korrekturbereich.
    /// Dual = Buch-Standard (Primary @ 50% + Additional @ 66.7%).
    /// Triple ergänzt Mid @ 55.9%, Quad + Hex erweitern auf 4/6 Levels.
    /// </summary>
    public BCZoneEntryStrategy BCZoneEntryStrategy { get; set; } = BCZoneEntryStrategy.Dual;

    /// <summary>
    /// Task 4.3 — Entry-Modus (aggressiv mit Limit-Order oder konservativ mit LTF-Reversal-Bestätigung).
    /// Default <see cref="EntryMode.Both"/>: Aggressive-Limits werden platziert, zusätzlich LTF-Trigger
    /// als bonus-Confluence. Conservative-Only: Signal NUR bei bestätigtem LTF-Reversal.
    /// </summary>
    public EntryMode EntryMode { get; set; } = EntryMode.Both;

    /// <summary>
    /// Strukturpunkte-Doku §5C: Wenn true wird auch in Modus <see cref="EntryMode.Both"/> und
    /// <see cref="EntryMode.Aggressive"/> eine Pinbar- ODER Engulfing-Bestätigung in der B-Zone erzwungen
    /// und Micro-Sequence als Reversal-Trigger verworfen.
    /// Default: false (Buch §7 Konservativer Einstieg erlaubt ausdrücklich Pinbar ODER Engulfing ODER
    /// eine kleine 0-A-B-C-Umkehrsequenz — alle drei sind gleichwertig). Der frühere True-Default hat den
    /// dritten Trigger stumm rausgeschnitten. Wer ausschließlich Docht-Rejection akzeptieren will, dreht das hoch.
    /// Doku-Zitat Algorithmus-Dok §5C: "Erzeuge erst ein Kaufsignal, wenn Lower_Wick > (Body * 2)." —
    /// explizit als Profi-Erweiterung beschrieben, nicht als Basis-Regel.
    /// </summary>
    public bool RequireWickRejectionInBZone { get; set; } = false;

    /// <summary>
    /// Spec §4 Confirmation-Mode: Die Trigger-Kerze muss mit dem Body INNERHALB oder ÜBER der Korrekturbox
    /// (Long: >= Box-Unterkante; Short: &lt;= Box-Oberkante) schließen; der Docht darf rausstehen.
    /// Wenn true wird diese Regel zusätzlich zum Reversal-Pattern erzwungen (Block bei Body-Close unter/über der Box).
    /// Default: false — Buch-Masterclass §8 "Kerzen im Korrekturbereich" sagt: "Solange der Kerzenkörper
    /// innerhalb oder oberhalb der Box schließt, ist das Setup valide" — also ist die Regel ein Filter auf
    /// starke negative Closes, nicht ein zusätzliches Muss-Gate für den Entry. Der frühere True-Default
    /// hat zu viele valide Setups blockiert (Box-Poke + Reversal).
    /// </summary>
    public bool RequireBoxCloseOnEntry { get; set; } = false;

    /// <summary>
    /// Spec §7 ("Heiliger Gral"): Positions-Multiplikator wenn der Overlap-Check eine <c>HighProbabilityZone</c>
    /// markiert (HTF-GKL überlappt mit LTF-BC oder LTF-EXT-1.618-Gegenrichtung). Skaliert die Basis-Positionsgröße.
    /// 1.0 = kein Boost (Default), &gt;1.0 = mehr Risiko in High-Probability-Zonen.
    /// Wirksame Obergrenze: <see cref="MaxPositionSizePercent"/> greift weiterhin.
    /// </summary>
    public decimal HighProbabilityPositionMultiplier { get; set; } = 1.0m;

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
    /// Hard-Cap für Risiko pro Trade (|Entry-SL|*Qty / Equity). SK-Buch: 1-3%. 0 = deaktiviert.
    /// Wirkt ZUSÄTZLICH zu MaxMarginPerTradePercent als Schutz gegen unerwartete Sizing-Kombinationen.
    /// </summary>
    public decimal MaxRiskPercentPerTrade { get; set; } = 3m;

    /// <summary>
    /// Max Daily Loss Circuit-Breaker in % der Equity. Nach Überschreitung keine neuen Entries bis UTC-00:00.
    /// Default: 0 = deaktiviert (Buch-konform, User-Entscheidung). Empfehlung: 5%.
    /// </summary>
    public decimal MaxDailyLossPercent { get; set; } = 0m;

    /// <summary>
    /// Task 1.2 — News-Blackout-Fenster in Minuten um High-Impact-Events (FOMC/NFP/CPI/ECB/BoE/BoJ).
    /// Buch-Masterclass Step 1: "Vor extrem wichtigen Ereignissen solltest du keine Limit-Orders
    /// offen in Korrekturboxen liegen haben." Default 30min (±30 um Event-Zeit).
    /// 0 = deaktiviert (wenn kein EconomicCalendarService verfügbar auch ignoriert).
    /// </summary>
    public int NewsBlackoutMinutes { get; set; } = 30;

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
    /// <summary>Marktspezifische Risk-Settings. SK-Buch: einheitlich 1% Risiko für alle Kategorien.</summary>
    public Dictionary<MarketCategory, MarketCategorySettings> CategorySettings { get; set; } = new()
    {
        // SK-Buch S.13: MinRRR = 1:1 fuer alle Kategorien (Buch-konform).
        { MarketCategory.Crypto,    new() { MaxLeverage = 5m,  MaxPositionSizePercent = 3m, MaxMarginPerTradePercent = 1m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Commodity, new() { MaxLeverage = 10m, MaxPositionSizePercent = 3m, MaxMarginPerTradePercent = 1m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Index,     new() { MaxLeverage = 10m, MaxPositionSizePercent = 3m, MaxMarginPerTradePercent = 1m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Forex,     new() { MaxLeverage = 20m, MaxPositionSizePercent = 3m, MaxMarginPerTradePercent = 1m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Stock,     new() { MaxLeverage = 5m,  MaxPositionSizePercent = 3m, MaxMarginPerTradePercent = 1m, MinRiskRewardRatio = 1.0m } },
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
