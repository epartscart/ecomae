#!/usr/bin/env python3
"""Field-by-field recursive compare of PHP/ASP.NET surface payload samples.

Usage:
  python3 scripts/compare_surface_payload_parity.py \\
    --left docs/migration/evidence/surface-parity/samples/cp-dashboard-php.json \\
    --right docs/migration/evidence/surface-parity/samples/cp-dashboard-aspnet.json \\
    --require users,adminSessions,portalTenants,activePortalTenants,source,message \\
    --path summary
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any


DEFAULT_IGNORE = {
    "note",
    "message",
    "session",
    "presentation",
    "hint",
}


def load(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def dig(payload: Any, dotted: str | None) -> Any:
    if not dotted:
        return payload
    cur = payload
    for part in dotted.split("."):
        if not isinstance(cur, dict) or part not in cur:
            raise KeyError(f"missing path {dotted!r} at {part!r}")
        cur = cur[part]
    return cur


def compare(left: Any, right: Any, path: str, ignore: set[str], failures: list[str]) -> None:
    if isinstance(left, dict) and isinstance(right, dict):
        left_keys = {k for k in left.keys() if k not in ignore}
        right_keys = {k for k in right.keys() if k not in ignore}
        missing = sorted(left_keys - right_keys)
        extra = sorted(right_keys - left_keys)
        for key in missing:
            failures.append(f"{path}.{key}: missing on right")
        for key in extra:
            failures.append(f"{path}.{key}: unexpected on right")
        for key in sorted(left_keys & right_keys):
            compare(left[key], right[key], f"{path}.{key}", ignore, failures)
        return

    if isinstance(left, list) and isinstance(right, list):
        if len(left) != len(right):
            failures.append(f"{path}: list length left={len(left)} right={len(right)}")
            return
        for idx, (a, b) in enumerate(zip(left, right)):
            compare(a, b, f"{path}[{idx}]", ignore, failures)
        return

    if left != right:
        failures.append(f"{path}: left={left!r} right={right!r}")


def require_fields(payload: Any, fields: list[str], path: str, failures: list[str]) -> None:
    if not isinstance(payload, dict):
        failures.append(f"{path}: expected object for required fields")
        return
    if fields and isinstance(next(iter(payload.values()), None), list):
        # list digest container — validate first item when present
        for key, value in payload.items():
            if isinstance(value, list):
                if not value:
                    return
                item = value[0]
                if not isinstance(item, dict):
                    failures.append(f"{path}.{key}[0]: expected object")
                    return
                for field in fields:
                    if field not in item:
                        failures.append(f"{path}.{key}[0].{field}: missing required field")
                return
    for field in fields:
        if field not in payload:
            failures.append(f"{path}.{field}: missing required field")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--left", required=True, type=Path)
    parser.add_argument("--right", required=True, type=Path)
    parser.add_argument("--path", default="", help="dotted path inside both payloads (e.g. summary)")
    parser.add_argument("--require", default="", help="comma-separated required fields at --path")
    parser.add_argument(
        "--ignore",
        default=",".join(sorted(DEFAULT_IGNORE)),
        help="comma-separated keys ignored during recursive compare",
    )
    parser.add_argument("--contract-only", action="store_true", help="only check required fields on right")
    args = parser.parse_args()

    left = load(args.left)
    right = load(args.right)
    ignore = {part.strip() for part in args.ignore.split(",") if part.strip()}
    require = [part.strip() for part in args.require.split(",") if part.strip()]
    failures: list[str] = []

    try:
        left_node = dig(left, args.path or None)
        right_node = dig(right, args.path or None)
    except KeyError as exc:
        print(f"SURFACE PARITY FAILED: {exc}")
        return 1

    if require:
        require_fields(right_node if args.contract_only else left_node, require, args.path or "$", failures)
        if args.contract_only:
            require_fields(right_node, require, args.path or "$", failures)

    if not args.contract_only:
        compare(left_node, right_node, args.path or "$", ignore, failures)

    if failures:
        print("SURFACE PARITY FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    mode = "contract" if args.contract_only else "field-by-field"
    print(f"SURFACE PARITY PASSED ({mode}): {args.left.name} == {args.right.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
