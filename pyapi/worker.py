"""pyapi background worker — order-alert + low-stock push dispatcher.

Phase 3 of the migration: replaces per-request/cron PHP polling with a single
long-lived Python loop. Safe to run alongside PHP (read-only polling + FCM send;
no schema mutation beyond the ops setup).

Run:
    python -m pyapi.worker                 # loop forever
    python -m pyapi.worker --once          # single pass (cron-friendly)

Interval + low-stock cadence via env:
    PYAPI_WORKER_INTERVAL_SEC   (default 30)
    PYAPI_LOW_STOCK_EVERY_SEC   (default 3600)

State (last seen order id, last low-stock ping) is kept in a small file so
restarts do not re-notify the whole backlog.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
from pathlib import Path

from . import db, push
from .ops.push_setup import ensure_schema

STATE_FILE = Path(os.environ.get("PYAPI_WORKER_STATE", "/tmp/pyapi_worker_state.json"))


def _load_state() -> dict:
    try:
        return json.loads(STATE_FILE.read_text())
    except Exception:
        return {}


def _save_state(state: dict) -> None:
    try:
        STATE_FILE.write_text(json.dumps(state))
    except Exception:
        pass


def _max_order_id() -> int:
    try:
        row = db.fetch_one("SELECT MAX(`id`) AS m FROM `shop_orders`")
        return int(row["m"]) if row and row.get("m") is not None else 0
    except Exception:
        return 0


def run_once(state: dict, low_stock_every: int) -> dict:
    now = int(time.time())

    # First run: baseline to "now" so we don't blast the entire order history.
    if "last_order_id" not in state:
        state["last_order_id"] = _max_order_id()

    order_res = push.notify_new_orders(int(state.get("last_order_id", 0)), app="cp")
    if order_res.get("status") and "last_id" in order_res:
        state["last_order_id"] = order_res["last_id"]

    low_res = {"skipped": True}
    if now - int(state.get("last_low_stock_ts", 0)) >= low_stock_every:
        low_res = push.notify_low_stock(app="cp")
        state["last_low_stock_ts"] = now

    _save_state(state)
    return {"orders": order_res, "low_stock": low_res, "state": state}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="pyapi push worker")
    parser.add_argument("--once", action="store_true", help="single pass then exit")
    args = parser.parse_args(argv)

    interval = int(os.environ.get("PYAPI_WORKER_INTERVAL_SEC", "30"))
    low_stock_every = int(os.environ.get("PYAPI_LOW_STOCK_EVERY_SEC", "3600"))

    try:
        ensure_schema()
    except Exception as exc:
        print(f"[pyapi.worker] schema ensure failed: {exc}", file=sys.stderr)

    state = _load_state()

    if args.once:
        result = run_once(state, low_stock_every)
        print(json.dumps(result))
        return 0

    print(f"[pyapi.worker] started (interval={interval}s, low_stock_every={low_stock_every}s)")
    while True:
        try:
            run_once(state, low_stock_every)
        except Exception as exc:  # never let the loop die
            print(f"[pyapi.worker] pass error: {exc}", file=sys.stderr)
        time.sleep(interval)


if __name__ == "__main__":
    raise SystemExit(main())
