"""Analyse der offenen ExitStates (laufende Trades / Recovery-Positionen)."""

from __future__ import annotations
import json
import sqlite3
from contextlib import closing
from datetime import datetime, timezone
from pathlib import Path

DB = Path(r"F:\Meine_Apps_Ava\bingxbot_snapshot.db")
TF_NAMES = {0: "M1", 1: "M3", 2: "M5", 3: "M15", 4: "M30",
            5: "H1", 6: "H2", 7: "H4", 8: "H6", 9: "H12",
            10: "D1", 11: "W1", 12: "MN1"}


def main():
    with closing(sqlite3.connect(f"file:{DB}?mode=ro", uri=True)) as con:
        cur = con.execute("SELECT Value FROM Settings WHERE Key='ExitStates'")
        row = cur.fetchone()
        if not row or not row[0]:
            print("Keine ExitStates")
            return
        obj = json.loads(row[0])

    import re
    now = datetime.now(timezone.utc)
    states = []
    for k, s in obj.items():
        raw = s["EntryTime"]
        # .NET liefert teils 7-stellige Mikrosekunden — Python akzeptiert max 6.
        raw = re.sub(r"\.(\d{6})\d+", r".\1", raw)
        raw = raw.replace("Z", "+00:00")
        entry = datetime.fromisoformat(raw)
        age = (now - entry).total_seconds() / 86400.0
        side = "Long" if s.get("Side", 0) == 0 else "Short"
        sig = s.get("Signal") or {}
        states.append({
            "key": k,
            "symbol": s["Symbol"],
            "side": side,
            "entry_time": entry.isoformat(timespec="minutes"),
            "age_days": round(age, 1),
            "entry_price": s.get("EntryPrice", 0),
            "quantity": s.get("OriginalQuantity", 0),
            "nav_tf": TF_NAMES.get(s.get("NavigatorTimeframe", -1), f"TF{s.get('NavigatorTimeframe')}"),
            "phase": s.get("Phase", 0),
            "is_recovered": s.get("IsRecovered", False),
            "is_gkl": (sig.get("IsGklSetup") or False),
            "confluence_score": sig.get("ConfluenceScore", 0),
            "stop_loss": sig.get("StopLoss", 0),
            "tp1": sig.get("TakeProfit", 0),
            "tp2": sig.get("TakeProfit2", 0),
            "reason": sig.get("Reason", ""),
            "tp1_order_id": s.get("Tp1LimitOrderId"),
            "tp2_order_id": s.get("Tp2LimitOrderId"),
            "tp_managed_by_exchange": s.get("IsTpManagedByExchange", False),
            "partial_closed": s.get("PartialClosed", False),
            "breakeven_set": s.get("BreakevenSet", False),
        })

    states.sort(key=lambda x: x["entry_time"])
    print(f"Total ExitStates: {len(states)}")
    real_qty = [s for s in states if s["quantity"] and s["quantity"] > 0]
    recovered = [s for s in states if s["is_recovered"]]
    print(f"  davon mit Quantity>0 (real gefuellt): {len(real_qty)}")
    print(f"  davon IsRecovered=True: {len(recovered)}")

    print()
    print(f"{'Symbol':<28} {'Side':<5} {'EntryTime':<17} {'Age_d':<6} "
          f"{'Qty':<10} {'NavTF':<5} {'Phase':<5} {'Score':<5} {'Rec':<3} {'TpExch':<6} "
          f"{'EntryPx':<12} {'SL':<12} {'TP1':<12} {'TP2':<12}")
    print("-" * 165)
    for s in states:
        print(f"{s['symbol']:<28} {s['side']:<5} {s['entry_time']:<17} "
              f"{s['age_days']:<6.1f} {s['quantity']:<10.6g} {s['nav_tf']:<5} "
              f"{s['phase']:<5} {s['confluence_score']:<5} "
              f"{'Y' if s['is_recovered'] else 'N':<3} "
              f"{'Y' if s['tp_managed_by_exchange'] else 'N':<6} "
              f"{s['entry_price']:<12.6g} {s['stop_loss']:<12.6g} "
              f"{s['tp1']:<12.6g} {s['tp2']:<12.6g}")


if __name__ == "__main__":
    main()
