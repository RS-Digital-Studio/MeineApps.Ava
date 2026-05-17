"""
BingXBot Snapshot-Analyse — Pi-DB-Snapshot auswerten.

Liest F:\\Meine_Apps_Ava\\bingxbot_snapshot.db (Atomic-Backup vom Pi)
und erzeugt einen strukturierten Markdown-Bericht mit:

  - Aktuelle Live-Settings (Risk/Scanner) vs Code-Defaults
  - EvaluationDecisions: Volumen, Reject-Reason-Verteilung, Triggered-Anteil
  - Trades: Win-Rate, RRR, PnL — pro Timeframe, Symbol, Mode
  - Confluence-Score: Verteilung bei Rejects + Trades, Win-Rate pro Bucket
  - Equity-Verlauf, Max-DD
  - Empfehlungs-Block (Phase-A-Konkretisierung anhand echter Daten)

Aufruf:
    py tools/SkAnalytics/analyze_snapshot.py
    -> schreibt F:\\Meine_Apps_Ava\\BINGXBOT_SNAPSHOT_REPORT.md

Wegwerf-Tool fuer einmaligen Bericht. Wenn die Daten-Pipeline regelmaessig laufen
soll, gehoert das nach Phase-B-Plan in einen sauberen Tool-Ordner mit JSON-Export.
"""

from __future__ import annotations

import json
import sqlite3
import sys
from collections import Counter, defaultdict
from contextlib import closing
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from statistics import mean, median, pstdev
from typing import Any


DB_PATH = Path(r"F:\Meine_Apps_Ava\bingxbot_snapshot.db")
OUT_PATH = Path(r"F:\Meine_Apps_Ava\BINGXBOT_SNAPSHOT_REPORT.md")


# ------------- Helpers -------------

# Timeframe-Encoding (Stand BingXBot.Core/Enums/TimeFrame.cs):
# M1=0, M3=1, M5=2, M15=3, M30=4, H1=5, H2=6, H4=7, H6=8, H12=9, D1=10, W1=11, MN1=12
TF_NAMES = {
    0: "M1",
    1: "M3",
    2: "M5",
    3: "M15",
    4: "M30",
    5: "H1",
    6: "H2",
    7: "H4",
    8: "H6",
    9: "H12",
    10: "D1",
    11: "W1",
    12: "MN1",
}


def tf_label(tf: int | None) -> str:
    if tf is None:
        return "?"
    return TF_NAMES.get(tf, f"TF{tf}")


def ts_ms_to_iso(ts: int | None) -> str:
    if ts is None or ts == 0:
        return "-"
    try:
        return datetime.fromtimestamp(ts / 1000, tz=timezone.utc).isoformat(timespec="seconds")
    except (OverflowError, OSError, ValueError):
        return f"ms={ts}"


def fmt_pct(num: int, denom: int) -> str:
    if denom == 0:
        return "0.0%"
    return f"{(num / denom) * 100:.1f}%"


def fmt_avg(values: list[float]) -> str:
    if not values:
        return "n/a"
    return f"{mean(values):+.4f} (n={len(values)}, median {median(values):+.4f})"


# ------------- Settings -------------

# Code-Defaults laut Report Abschnitt 1.1 + manuelle Pruefung:
# ScannerSettings: ImpulseAtrMultiplier=2.0, RequireBosCloseBreak=false,
#                  RequireBosVolumeBreakout=false, BosVolumeMultiplier=1.5,
#                  BlockLtfEntryWhenHtfInTargetZone=false
# RiskSettings:    RequireWickRejectionInBZone=false, RequireBoxCloseOnEntry=false,
#                  MinConfluenceScore=0, RiskPerTradePercent=5.0,
#                  MaxTotalMarginPercent=10.0, LossStreakHalveThreshold=4,
#                  LossStreakPauseThreshold=7, MinRRR=1.0
CODE_DEFAULTS = {
    # Scanner (alle Namen wie im BotSettings-JSON / ScannerSettings.cs)
    "ImpulseAtrMultiplier": 2.0,
    "RequireBosCloseBreak": False,
    "RequireBosVolumeBreakout": False,
    "BosVolumeMultiplier": 1.5,
    "BlockLtfEntryWhenHtfInTargetZone": False,
    "PivotLeftBars": 5,
    "PivotRightBars": 3,
    "EnableBiasFlip": True,
    "EnableCounterTrendScalp": False,
    "AdaptiveSwingStrength": True,
    "SwingStrengthMin": 3,
    "SwingStrengthMax": 10,
    "BcklReEntryCooldownCandles": 2,
    "NavigatorMinConfluence": 3,
    # Risk (Namen wie BotSettings.Risk)
    "RequireWickRejectionInBZone": False,
    "RequireBoxCloseOnEntry": False,
    "MinConfluenceScore": 0,
    "MinRiskRewardRatio": 0.0,
    "MaxRiskPercentPerTrade": 5.0,
    "MaxTotalMarginPercent": 10.0,  # Code-Default laut Report
    "MaxPositionSizePercent": 10.0,
    "MaxMarginPerTradePercent": 5.0,
    "MaxLeverage": 10,
    "MaxOpenPositions": 10,
    "MaxOpenPositionsPerSymbol": 1,
    "LossStreakHalveAtCount": 4,
    "LossStreakPauseAtCount": 7,
    "EnableLossStreakDampening": True,
    "HighProbabilityPositionMultiplier": 2.0,
    "MaxDailyLossPercent": 0.0,
    "MaxDailyRiskPercent": 0.0,
    "MaxDailyDrawdownPercent": 0.0,
    "RequireHtfConfluenceForEntry": False,
    "BCZoneEntryStrategy": "Dual",
    "EntryMode": "Both",
    "EnableCrossTfPyramiding": False,
    "EnableEquityCurveScaling": False,
    "EnableVolatilityTargeting": False,
}

# Buch-Hard-Defaults laut Report Phase A — was schaerfer sein muesste:
BUCH_HARD = {
    "ImpulseAtrMultiplier": 3.0,
    "RequireBosCloseBreak": True,
    "RequireBosVolumeBreakout": True,
    "BlockLtfEntryWhenHtfInTargetZone": True,
    "RequireWickRejectionInBZone": True,
    "RequireBoxCloseOnEntry": True,
    "MinConfluenceScore": 5,
    "MinRiskRewardRatio": 1.0,
    "EnableBiasFlip": False,  # Audit-Empfehlung: Redundanz vermeiden
}


def parse_setting_value(raw: str) -> Any:
    """Versuche Wert sinnvoll zu typisieren."""
    if raw is None:
        return None
    s = raw.strip()
    if s.lower() in {"true", "false"}:
        return s.lower() == "true"
    try:
        if "." in s or "e" in s.lower():
            return float(s)
        return int(s)
    except ValueError:
        pass
    if s.startswith("{") or s.startswith("["):
        try:
            return json.loads(s)
        except json.JSONDecodeError:
            return s
    return s


def value_repr(v: Any) -> str:
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, float):
        return f"{v:.4g}"
    if isinstance(v, (dict, list)):
        return f"<{type(v).__name__} len={len(v)}>"
    return str(v)


@dataclass
class SettingDiff:
    key: str
    code_default: Any
    live_value: Any
    buch_hard: Any | None
    deviates_from_code: bool
    deviates_from_buch: bool


def diff_settings(live: dict[str, Any]) -> list[SettingDiff]:
    rows: list[SettingDiff] = []
    seen = set()
    for key, code_default in CODE_DEFAULTS.items():
        live_v = live.get(key, "<NICHT GESETZT>")
        seen.add(key)
        if live_v == "<NICHT GESETZT>":
            deviates_code = False  # Code-Default wirkt
            effective = code_default
        else:
            deviates_code = live_v != code_default
            effective = live_v
        buch = BUCH_HARD.get(key)
        deviates_buch = buch is not None and effective != buch
        rows.append(SettingDiff(key, code_default, live_v, buch, deviates_code, deviates_buch))
    # Zusatz: weitere Settings aus DB, die noch nicht in CODE_DEFAULTS sind
    for k, v in live.items():
        if k in seen:
            continue
        if not any(token in k for token in ("Risk", "Scanner", "Sk", "Bos", "Impulse",
                                             "Confluence", "Wick", "Box", "Pivot", "Atr",
                                             "Bias", "News", "Trend", "Margin", "Loss",
                                             "Rrr", "Probability", "HighProb")):
            continue
        rows.append(SettingDiff(k, None, v, None, False, False))
    return rows


# ------------- Analyse -------------

def load_settings(con: sqlite3.Connection) -> dict[str, Any]:
    """Liest BotSettings (JSON-Aggregat) + flache Keys.

    BingXBot speichert seit Schema v10 alle Strategie-Settings als JSON unter dem Key
    `BotSettings` mit den Sub-Objekten Risk/Scanner/Backtest. Wir flatten Risk + Scanner,
    sodass die Diff-Tabelle weiter funktioniert.
    """
    cur = con.execute("SELECT Key, Value FROM Settings ORDER BY Key")
    raw_rows = cur.fetchall()
    flat: dict[str, Any] = {}
    for key, value in raw_rows:
        flat[f"_raw:{key}"] = value  # Roh-Wert separat aufbewahren
        if key == "BotSettings" and value:
            try:
                obj = json.loads(value)
            except json.JSONDecodeError:
                continue
            for section in ("Risk", "Scanner"):
                sub = obj.get(section)
                if isinstance(sub, dict):
                    for sk, sv in sub.items():
                        flat[sk] = sv
            # Backtest-Section auch aufnehmen, aber praefigieren
            bt = obj.get("Backtest")
            if isinstance(bt, dict):
                for sk, sv in bt.items():
                    flat[f"Backtest.{sk}"] = sv
        elif key == "RuntimeState" and value:
            try:
                obj = json.loads(value)
                if isinstance(obj, dict):
                    for sk, sv in obj.items():
                        flat[f"RuntimeState.{sk}"] = sv
            except json.JSONDecodeError:
                pass
        else:
            flat[key] = parse_setting_value(value)
    return flat


def analyze_decisions(con: sqlite3.Connection) -> dict[str, Any]:
    cur = con.execute("SELECT COUNT(*), MIN(Timestamp), MAX(Timestamp) FROM EvaluationDecisions")
    total, ts_min, ts_max = cur.fetchone()
    if not total:
        return {"total": 0}
    cur = con.execute("SELECT COUNT(*) FROM EvaluationDecisions WHERE Triggered=1")
    triggered = cur.fetchone()[0]

    # Reject-Reasons (Top 25)
    cur = con.execute(
        """
        SELECT RejectionReason, COUNT(*) AS cnt
        FROM EvaluationDecisions
        WHERE Triggered=0 AND RejectionReason IS NOT NULL AND RejectionReason <> ''
        GROUP BY RejectionReason
        ORDER BY cnt DESC
        """
    )
    reject_rows = cur.fetchall()

    # Triggered pro TF
    cur = con.execute(
        """
        SELECT Tf, COUNT(*) AS total,
               SUM(CASE WHEN Triggered=1 THEN 1 ELSE 0 END) AS triggered
        FROM EvaluationDecisions
        GROUP BY Tf
        ORDER BY total DESC
        """
    )
    per_tf = cur.fetchall()

    # Triggered pro Symbol (Top 25)
    cur = con.execute(
        """
        SELECT Symbol, COUNT(*) AS total,
               SUM(CASE WHEN Triggered=1 THEN 1 ELSE 0 END) AS triggered
        FROM EvaluationDecisions
        GROUP BY Symbol
        ORDER BY total DESC
        LIMIT 25
        """
    )
    per_symbol = cur.fetchall()

    # ConfluenceScore-Verteilung
    cur = con.execute(
        """
        SELECT ConfluenceScore, COUNT(*) AS total,
               SUM(CASE WHEN Triggered=1 THEN 1 ELSE 0 END) AS triggered
        FROM EvaluationDecisions
        WHERE ConfluenceScore IS NOT NULL
        GROUP BY ConfluenceScore
        ORDER BY ConfluenceScore
        """
    )
    per_score = cur.fetchall()

    # Reject-Reason pro Timeframe (nur Top-15 Reject-Reasons)
    top_reasons = [r[0] for r in reject_rows[:15]]
    placeholders = ",".join("?" * len(top_reasons)) if top_reasons else "''"
    if top_reasons:
        cur = con.execute(
            f"""
            SELECT RejectionReason, Tf, COUNT(*) AS cnt
            FROM EvaluationDecisions
            WHERE Triggered=0 AND RejectionReason IN ({placeholders})
            GROUP BY RejectionReason, Tf
            ORDER BY RejectionReason, cnt DESC
            """,
            top_reasons,
        )
        reason_tf = cur.fetchall()
    else:
        reason_tf = []

    # Categories-Aggregation (JSON-Felder)
    cur = con.execute(
        "SELECT CategoriesJson FROM EvaluationDecisions WHERE CategoriesJson IS NOT NULL AND CategoriesJson <> ''"
    )
    category_counter: Counter[str] = Counter()
    for (cjson,) in cur.fetchall():
        try:
            obj = json.loads(cjson)
            if isinstance(obj, list):
                for c in obj:
                    category_counter[str(c)] += 1
            elif isinstance(obj, dict):
                for c, val in obj.items():
                    if val:
                        category_counter[str(c)] += 1
        except json.JSONDecodeError:
            continue

    # HardFilters-Aggregation
    cur = con.execute(
        "SELECT HardFiltersJson FROM EvaluationDecisions WHERE HardFiltersJson IS NOT NULL AND HardFiltersJson <> ''"
    )
    hard_filter_counter: Counter[str] = Counter()
    for (hjson,) in cur.fetchall():
        try:
            obj = json.loads(hjson)
            if isinstance(obj, list):
                for c in obj:
                    hard_filter_counter[str(c)] += 1
            elif isinstance(obj, dict):
                for c, val in obj.items():
                    if val:
                        hard_filter_counter[f"{c}={val}"] += 1
        except json.JSONDecodeError:
            continue

    return {
        "total": total,
        "ts_min": ts_min,
        "ts_max": ts_max,
        "triggered": triggered,
        "reject_rows": reject_rows,
        "per_tf": per_tf,
        "per_symbol": per_symbol,
        "per_score": per_score,
        "reason_tf": reason_tf,
        "category_counter": category_counter,
        "hard_filter_counter": hard_filter_counter,
    }


def analyze_trades(con: sqlite3.Connection) -> dict[str, Any]:
    cur = con.execute(
        """
        SELECT COUNT(*),
               MIN(EntryTime), MAX(EntryTime),
               SUM(CASE WHEN ExitTime IS NOT NULL AND ExitTime > 0 THEN 1 ELSE 0 END)
        FROM Trades
        """
    )
    total, t_min, t_max, closed = cur.fetchone()
    if not total:
        return {"total": 0}

    cur = con.execute(
        """
        SELECT Symbol, Side, EntryPrice, ExitPrice, Quantity, Pnl, Fee,
               EntryTime, ExitTime, Reason, Mode, FundingPaid, NavigatorTimeframe
        FROM Trades
        WHERE ExitTime IS NOT NULL AND ExitTime > 0 AND Pnl IS NOT NULL
        """
    )
    closed_rows = cur.fetchall()

    wins = [r for r in closed_rows if r[5] is not None and r[5] > 0]
    losses = [r for r in closed_rows if r[5] is not None and r[5] <= 0]

    pnls = [r[5] for r in closed_rows if r[5] is not None]
    fees = [r[6] for r in closed_rows if r[6] is not None]
    fundings = [r[11] for r in closed_rows if r[11] is not None]

    # Mode-Mapping: 0=Live, 1=Paper, 2=Backtest? -> Aus DB schauen
    mode_counter: Counter[int] = Counter(r[10] for r in closed_rows)

    # Per Navigator-Timeframe
    per_tf: dict[int, dict[str, Any]] = defaultdict(lambda: {"n": 0, "wins": 0, "pnl": 0.0, "pnls": []})
    for r in closed_rows:
        tf = r[12]
        per_tf[tf]["n"] += 1
        if r[5] is not None:
            per_tf[tf]["pnl"] += r[5]
            per_tf[tf]["pnls"].append(r[5])
            if r[5] > 0:
                per_tf[tf]["wins"] += 1

    # Per Symbol (top 15)
    per_sym: dict[str, dict[str, Any]] = defaultdict(lambda: {"n": 0, "wins": 0, "pnl": 0.0})
    for r in closed_rows:
        sym = r[0] or "?"
        per_sym[sym]["n"] += 1
        if r[5] is not None:
            per_sym[sym]["pnl"] += r[5]
            if r[5] > 0:
                per_sym[sym]["wins"] += 1

    # Reason-Verteilung (Exit-Reason)
    reason_counter: Counter[str] = Counter()
    reason_pnl: dict[str, list[float]] = defaultdict(list)
    for r in closed_rows:
        reason = r[9] or "?"
        reason_counter[reason] += 1
        if r[5] is not None:
            reason_pnl[reason].append(r[5])

    # Per Mode
    per_mode: dict[int, dict[str, Any]] = defaultdict(lambda: {"n": 0, "wins": 0, "pnl": 0.0})
    for r in closed_rows:
        m = r[10]
        per_mode[m]["n"] += 1
        if r[5] is not None:
            per_mode[m]["pnl"] += r[5]
            if r[5] > 0:
                per_mode[m]["wins"] += 1

    return {
        "total": total,
        "closed": closed,
        "t_min": t_min,
        "t_max": t_max,
        "wins": len(wins),
        "losses": len(losses),
        "win_pnls": [r[5] for r in wins],
        "loss_pnls": [r[5] for r in losses],
        "pnls": pnls,
        "fees": fees,
        "fundings": fundings,
        "per_tf": dict(per_tf),
        "per_sym": dict(per_sym),
        "reason_counter": reason_counter,
        "reason_pnl": reason_pnl,
        "per_mode": dict(per_mode),
    }


def analyze_equity(con: sqlite3.Connection) -> dict[str, Any]:
    cur = con.execute(
        "SELECT Time, Equity FROM EquitySnapshots ORDER BY Time"
    )
    rows = cur.fetchall()
    if not rows:
        return {"count": 0}
    eq_start = rows[0][1]
    eq_end = rows[-1][1]
    eq_max = max(r[1] for r in rows)
    eq_min = min(r[1] for r in rows)
    # Running Drawdown
    peak = eq_start
    max_dd_abs = 0.0
    max_dd_pct = 0.0
    for _, eq in rows:
        if eq > peak:
            peak = eq
        dd_abs = peak - eq
        dd_pct = dd_abs / peak if peak > 0 else 0.0
        if dd_abs > max_dd_abs:
            max_dd_abs = dd_abs
        if dd_pct > max_dd_pct:
            max_dd_pct = dd_pct
    return {
        "count": len(rows),
        "t_min": rows[0][0],
        "t_max": rows[-1][0],
        "eq_start": eq_start,
        "eq_end": eq_end,
        "eq_max": eq_max,
        "eq_min": eq_min,
        "pnl_total": eq_end - eq_start,
        "pnl_pct": (eq_end - eq_start) / eq_start if eq_start > 0 else 0.0,
        "max_dd_abs": max_dd_abs,
        "max_dd_pct": max_dd_pct,
    }


# ------------- Bericht -------------

def render_settings_block(live: dict[str, Any]) -> str:
    diffs = diff_settings(live)
    out = ["## 1. Settings-Status (Live vs Code-Default vs Buch-Hard)\n"]
    out.append("Quelle: `Settings`-Tabelle auf dem Pi. Live-Werte ueberschreiben Code-Defaults.\n")
    out.append("| Setting | Live | Code-Default | Buch-Hard | weicht ab? |")
    out.append("|---|---|---|---|---|")
    for d in diffs:
        live = "—" if d.live_value == "<NICHT GESETZT>" else value_repr(d.live_value)
        code = "—" if d.code_default is None else value_repr(d.code_default)
        buch = "—" if d.buch_hard is None else value_repr(d.buch_hard)
        flag_buch = "❌ vom Buch" if d.deviates_from_buch else "✅ Buch"
        flag_code = "(live)" if d.live_value != "<NICHT GESETZT>" else "(default)"
        marker = f"{flag_buch} {flag_code}"
        out.append(f"| `{d.key}` | {live} | {code} | {buch} | {marker} |")
    return "\n".join(out) + "\n"


def render_decisions_block(d: dict[str, Any]) -> str:
    if d.get("total", 0) == 0:
        return "## 2. EvaluationDecisions\n\nKeine Daten.\n"
    total = d["total"]
    triggered = d["triggered"]
    out = ["## 2. EvaluationDecisions — was lehnt der Bot ab, was triggert er?\n"]
    out.append(
        f"- Gesamtzahl Decisions: **{total:,}**".replace(",", "."))
    out.append(f"- Zeitraum: {ts_ms_to_iso(d['ts_min'])} - {ts_ms_to_iso(d['ts_max'])}")
    out.append(f"- Davon getriggert (Trade-Versuch): **{triggered:,}** ({fmt_pct(triggered, total)})".replace(",", "."))
    out.append(f"- Davon geblockt: **{total - triggered:,}** ({fmt_pct(total - triggered, total)})\n".replace(",", "."))

    # Top Reject-Reasons
    out.append("### 2.1 Top Reject-Reasons (Block-Gruende)\n")
    out.append("| # | Reason | Anzahl | % der Blocks |")
    out.append("|---|---|---|---|")
    blocked_total = total - triggered
    for i, (reason, cnt) in enumerate(d["reject_rows"][:30], 1):
        out.append(f"| {i} | `{reason}` | {cnt:,} | {fmt_pct(cnt, blocked_total)} |".replace(",", "."))

    # Per Timeframe
    out.append("\n### 2.2 Decisions pro Timeframe\n")
    out.append("| Timeframe | Total | Triggered | Trigger-Quote |")
    out.append("|---|---|---|---|")
    for tf, tot, trig in d["per_tf"]:
        out.append(f"| {tf_label(tf)} | {tot:,} | {trig:,} | {fmt_pct(trig, tot)} |".replace(",", "."))

    # Per Symbol
    out.append("\n### 2.3 Top-Symbole nach Decision-Volumen\n")
    out.append("| Symbol | Total | Triggered | Trigger-Quote |")
    out.append("|---|---|---|---|")
    for sym, tot, trig in d["per_symbol"]:
        out.append(f"| {sym} | {tot:,} | {trig:,} | {fmt_pct(trig, tot)} |".replace(",", "."))

    # Confluence-Score
    out.append("\n### 2.4 Confluence-Score-Verteilung\n")
    out.append("| Score | Total | Triggered | Trigger-Quote |")
    out.append("|---|---|---|---|")
    for sc, tot, trig in d["per_score"]:
        out.append(f"| {sc} | {tot:,} | {trig:,} | {fmt_pct(trig, tot)} |".replace(",", "."))

    # Categories
    if d["category_counter"]:
        out.append("\n### 2.5 Confluence-Kategorien (alle Decisions)\n")
        out.append("| Kategorie | Vorkommen |")
        out.append("|---|---|")
        for cat, cnt in d["category_counter"].most_common(30):
            out.append(f"| `{cat}` | {cnt:,} |".replace(",", "."))

    # HardFilters
    if d["hard_filter_counter"]:
        out.append("\n### 2.6 Hard-Filter Auspraegungen (alle Decisions)\n")
        out.append("| Filter | Vorkommen |")
        out.append("|---|---|")
        for f, cnt in d["hard_filter_counter"].most_common(30):
            out.append(f"| `{f}` | {cnt:,} |".replace(",", "."))

    return "\n".join(out) + "\n"


def render_trades_block(t: dict[str, Any]) -> str:
    if t.get("total", 0) == 0:
        return "## 3. Trades\n\nKeine Trades vorhanden.\n"

    out = ["## 3. Trades — wie hat der Bot abgeschnitten?\n"]
    total = t["total"]
    closed = t["closed"]
    out.append(f"- Gesamt: **{total:,}** Trades, davon **{closed:,}** geschlossen".replace(",", "."))
    out.append(f"- Zeitraum: {ts_ms_to_iso(t['t_min'])} - {ts_ms_to_iso(t['t_max'])}")
    pnls = t["pnls"]
    fees = t["fees"]
    fundings = t["fundings"]
    if pnls:
        total_pnl = sum(pnls)
        total_fees = sum(fees) if fees else 0.0
        total_funding = sum(fundings) if fundings else 0.0
        out.append(f"- PnL gesamt (vor Funding/nach Fees): **{total_pnl:+.4f}** USDT")
        out.append(f"- Fees gesamt: **{total_fees:+.4f}** USDT  |  Funding gesamt: **{total_funding:+.4f}** USDT")
        out.append(f"- Netto-PnL (PnL - Funding): **{total_pnl - total_funding:+.4f}** USDT")
    wins = t["wins"]
    losses = t["losses"]
    out.append(f"- Wins / Losses: **{wins} / {losses}** -> Win-Rate **{fmt_pct(wins, wins + losses)}**")
    if t["win_pnls"]:
        out.append(f"- Avg Win-PnL: {fmt_avg(t['win_pnls'])}")
    if t["loss_pnls"]:
        out.append(f"- Avg Loss-PnL: {fmt_avg(t['loss_pnls'])}")
    if t["win_pnls"] and t["loss_pnls"]:
        avg_w = mean(t["win_pnls"])
        avg_l = mean(t["loss_pnls"])
        avg_l_abs = abs(avg_l) if avg_l != 0 else 1.0
        out.append(f"- Avg-RRR (Win/Loss): **{avg_w / avg_l_abs:.2f}**")
        # Profit Factor
        gross_w = sum(t["win_pnls"])
        gross_l = abs(sum(t["loss_pnls"]))
        out.append(f"- Profit-Factor (Σ Wins / |Σ Losses|): **{gross_w / gross_l:.2f}**" if gross_l > 0 else "- Profit-Factor: ∞")

    # Per Mode
    out.append("\n### 3.1 Trades pro Mode\n")
    mode_names = {0: "Live", 1: "Paper", 2: "Backtest"}
    out.append("| Mode | n | Wins | Win-Rate | PnL Σ |")
    out.append("|---|---|---|---|---|")
    for m, info in sorted(t["per_mode"].items()):
        label = mode_names.get(m, f"Mode={m}")
        out.append(f"| {label} | {info['n']} | {info['wins']} | {fmt_pct(info['wins'], info['n'])} | {info['pnl']:+.4f} |")

    # Per Navigator-TF
    out.append("\n### 3.2 Trades pro Navigator-Timeframe\n")
    out.append("| TF | n | Wins | Win-Rate | PnL Σ | Median PnL |")
    out.append("|---|---|---|---|---|---|")
    for tf, info in sorted(t["per_tf"].items(), key=lambda x: -x[1]["n"]):
        med = median(info["pnls"]) if info["pnls"] else 0.0
        out.append(f"| {tf_label(tf)} | {info['n']} | {info['wins']} | {fmt_pct(info['wins'], info['n'])} | {info['pnl']:+.4f} | {med:+.4f} |")

    # Per Symbol
    out.append("\n### 3.3 Trades pro Symbol (Top 25)\n")
    sorted_syms = sorted(t["per_sym"].items(), key=lambda x: -x[1]["n"])[:25]
    out.append("| Symbol | n | Wins | Win-Rate | PnL Σ |")
    out.append("|---|---|---|---|---|")
    for sym, info in sorted_syms:
        out.append(f"| {sym} | {info['n']} | {info['wins']} | {fmt_pct(info['wins'], info['n'])} | {info['pnl']:+.4f} |")

    # Exit-Reasons
    out.append("\n### 3.4 Exit-Reasons (warum geschlossen?)\n")
    out.append("| Reason | n | Avg PnL | Σ PnL |")
    out.append("|---|---|---|---|")
    for reason, cnt in t["reason_counter"].most_common():
        pnls_r = t["reason_pnl"][reason]
        avg = mean(pnls_r) if pnls_r else 0.0
        s = sum(pnls_r) if pnls_r else 0.0
        out.append(f"| `{reason}` | {cnt} | {avg:+.4f} | {s:+.4f} |")

    return "\n".join(out) + "\n"


def render_equity_block(e: dict[str, Any]) -> str:
    if e.get("count", 0) == 0:
        return "## 4. Equity\n\nKeine Snapshots.\n"
    out = ["## 4. Equity-Verlauf\n"]
    out.append(f"- Snapshots: **{e['count']:,}**".replace(",", "."))
    out.append(f"- Zeitraum: {ts_ms_to_iso(e['t_min'])} - {ts_ms_to_iso(e['t_max'])}")
    out.append(f"- Start: **{e['eq_start']:.2f}**  |  End: **{e['eq_end']:.2f}**")
    out.append(f"- High: **{e['eq_max']:.2f}**  |  Low: **{e['eq_min']:.2f}**")
    out.append(f"- PnL Σ: **{e['pnl_total']:+.2f}** ({e['pnl_pct'] * 100:+.2f}%)")
    out.append(f"- Max Drawdown: **{e['max_dd_abs']:.2f}** ({e['max_dd_pct'] * 100:.2f}%)")
    return "\n".join(out) + "\n"


def render_takeaways(decisions: dict[str, Any],
                     trades: dict[str, Any],
                     settings: dict[str, Any],
                     equity: dict[str, Any]) -> str:
    out = ["## 5. Take-aways (datenbasiert, nicht vermutet)\n"]
    # Trigger-Quote
    if decisions.get("total"):
        trig_q = decisions["triggered"] / decisions["total"]
        if trig_q < 0.05:
            cls = "extrem niedrig (Bot blockt fast alles)"
        elif trig_q < 0.15:
            cls = "niedrig (Bot ist selektiv)"
        elif trig_q < 0.35:
            cls = "mittel"
        else:
            cls = "hoch (viele Setups landen als Trade-Versuch)"
        out.append(f"- **Trigger-Quote {trig_q * 100:.1f}%** ({cls}).")
    # Win-Rate
    if trades.get("wins") is not None and (trades["wins"] + trades["losses"]) > 0:
        wr = trades["wins"] / (trades["wins"] + trades["losses"])
        out.append(f"- **Live-Win-Rate {wr * 100:.1f}%** auf {trades['wins'] + trades['losses']} geschlossenen Trades.")
        if trades["win_pnls"] and trades["loss_pnls"]:
            avg_w = mean(trades["win_pnls"])
            avg_l = abs(mean(trades["loss_pnls"]))
            rrr = avg_w / avg_l if avg_l > 0 else 0.0
            out.append(f"- **Avg RRR {rrr:.2f}** (avg Win {avg_w:+.4f} vs avg Loss {avg_l:.4f}).")
    # Confluence-Score-Win-Rate-Bucket (aus Trades nicht direkt herleitbar — wir nehmen Decisions)
    # Aufruf der Bewertung passiert weiter unten; hier nur Hinweis.
    # Settings vs Buch
    deviations = []
    for k, buch in BUCH_HARD.items():
        live_v = settings.get(k, CODE_DEFAULTS.get(k))
        if live_v != buch:
            deviations.append((k, live_v, buch))
    if deviations:
        out.append("\n### 5.1 Buch-Hard-Filter, die aktuell NICHT scharf sind\n")
        out.append("| Setting | Live | Buch-Hard |")
        out.append("|---|---|---|")
        for k, live_v, buch in deviations:
            out.append(f"| `{k}` | {value_repr(live_v)} | {value_repr(buch)} |")

    return "\n".join(out) + "\n"


# ------------- Main -------------

def main() -> int:
    if not DB_PATH.exists():
        print(f"Snapshot fehlt: {DB_PATH}", file=sys.stderr)
        return 1
    with closing(sqlite3.connect(f"file:{DB_PATH}?mode=ro", uri=True)) as con:
        settings = load_settings(con)
        decisions = analyze_decisions(con)
        trades = analyze_trades(con)
        equity = analyze_equity(con)

    header = [
        "# BingXBot Snapshot-Report",
        "",
        f"**Snapshot:** `{DB_PATH}`",
        f"**Erstellt:** {datetime.now().isoformat(timespec='seconds')}",
        f"**Settings-Eintraege:** {len(settings)}",
        "",
        "---",
        "",
    ]
    blocks = [
        render_settings_block(settings),
        render_decisions_block(decisions),
        render_trades_block(trades),
        render_equity_block(equity),
        render_takeaways(decisions, trades, settings, equity),
    ]
    OUT_PATH.write_text("\n".join(header) + "\n".join(blocks), encoding="utf-8")
    print(f"Report geschrieben: {OUT_PATH}")
    print(f"Settings (Auszug): {sum(1 for k in settings if any(t in k for t in ('Risk','Scanner','Sk')))} relevante Schluessel")
    return 0


if __name__ == "__main__":
    sys.exit(main())
