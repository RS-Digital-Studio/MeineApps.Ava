# BingXBacktestLab — Empirischer Strategie-Vergleich

Konsolen-Tool, das BingXBot-Strategien auf **echten BingX-Klines** backtestet und vergleicht.
Nicht in der Solution (`MeineApps.Ava.sln`) — standalone via `dotnet run --project`. Diente der
datengetriebenen Entscheidung TrendFollow vs. SK-System (31.05.2026).

## Verwendung

```bash
dotnet run --project tools/BingXBacktestLab -c Release -- \
  --strategies "TrendFollow,TrendFollow-Strong,SK-System" \
  --preset may-live \            # oder --symbols "BTC-USDT,ETH-USDT,..."
  --tfs H4,H1 \
  --from 2025-11-01 --to 2026-05-31 \
  --label mein-lauf
```

| Arg | Default | Zweck |
|-----|---------|-------|
| `--strategies` | SK-System | Komma-Liste (Namen aus `StrategyFactory`) |
| `--symbols` / `--preset` | preset may-live | Explizite Liste **oder** Preset (`may-live`, `crypto-major`) |
| `--tfs` | H4,H1 | Navigator-Timeframes |
| `--from` / `--to` | 2025-11-01 / 2026-05-31 | Zeitraum (UTC) |
| `--settings` | live-settings.json | BotSettings-JSON für faire Live-Config-Validierung |
| `--label` | run | Report-Dateiname-Suffix |

Output: Console-Tabelle + `reports/report-{label}.md` + `.json`. Aggregat pro Strategie
(WinRate, PF, Expectancy/Trade, Σ PnL, RRR, MaxDD, **Long/Short-Aufschlüsselung**) + Detail pro TF.

## Architektur

- `Program.cs` — Arg-Parsing, Backtest-Matrix (Strategie × Symbol × TF), Aggregation.
- `CachingPublicClient.cs` — Decorator um `BingXPublicClient`, cached Klines als JSON in
  `.kline-cache/` (Re-Runs instant, kein Rate-Limit-Druck). Cache-Key = Symbol+TF+from+to.
- `SimpleRateLimiter` (in Program.cs) — fixes 120ms-Delay zwischen Live-Requests.
- `live-settings.json` — Snapshot der Pi-Live-Config (Risk/Scanner/Backtest) für realistische Läufe.

`.kline-cache/`, `reports/`, `bin/`, `obj/` sind gitignored (generierte Artefakte).

## Wichtige Erkenntnisse (warum es das Tool gibt)

- **Backtest-Realismus ist nicht selbstverständlich:** SK-System zeigte im Backtest 48 % WinRate /
  PF 1.2, live aber 12 % / PF 0.11. Ursache: SKs **Limit-Entry in der Korrektur-Zone** — der Backtest
  steigt Market zum Candle-Close ein, mit SL/TP für den Limit-Preis gerechnet → künstlich hohe WinRate.
  **Konsequenz:** Market-Entry-Strategien (TrendFollow) sind backtest-treu und vertrauenswürdiger;
  Limit-System-Backtests immer gegen Live validieren.
- Immer über **mehrere Marktzyklen** (z.B. 2024 + 2025) und **Long/Short getrennt** bewerten —
  ein Bullenmarkt-Long-Bias sieht sonst wie Edge aus.
