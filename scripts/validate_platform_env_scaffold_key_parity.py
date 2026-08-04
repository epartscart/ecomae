#!/usr/bin/env python3
"""Ensure platform.env.example documents every bool scaffold flag from options JSON.

Maps EcomAe.<Section>.<Flag> -> # EcomAe__Section__Flag=<true|false>
Never invents RELEASE_OWNER_APPROVAL.md. cutoverAllowed stays false.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


def walk_bools(obj, prefix: str = "") -> list[tuple[str, bool]]:
    items: list[tuple[str, bool]] = []
    if isinstance(obj, dict):
        for key, value in obj.items():
            path = f"{prefix}.{key}" if prefix else str(key)
            if isinstance(value, (dict, list)):
                items.extend(walk_bools(value, path))
            elif isinstance(value, bool):
                items.append((path, value))
    elif isinstance(obj, list):
        for idx, value in enumerate(obj):
            path = f"{prefix}[{idx}]"
            if isinstance(value, (dict, list)):
                items.extend(walk_bools(value, path))
            elif isinstance(value, bool):
                items.append((path, value))
    return items


def path_to_env_key(path: str) -> str | None:
    # EcomAe.OAuth.RequireMfa -> EcomAe__OAuth__RequireMfa
    parts = path.split(".")
    if not parts or parts[0] != "EcomAe":
        return None
    if any("[" in p for p in parts):
        return None
    return "__".join(parts)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--options",
        type=Path,
        default=Path("deploy/aspnet/ecomae-scaffold-options.example.json"),
    )
    ap.add_argument(
        "--env",
        type=Path,
        default=Path("deploy/aspnet/platform.env.example"),
    )
    args = ap.parse_args()

    options = json.loads(args.options.read_text(encoding="utf-8"))
    env_text = args.env.read_text(encoding="utf-8")
    errors: list[str] = []

    if options.get("cutoverAllowed") is True or options.get("readyForPhpRemoval") is True:
        errors.append("options JSON must keep cutoverAllowed/readyForPhpRemoval false")

    documented: dict[str, str] = {}
    for match in re.finditer(
        r"^\s*#\s*(EcomAe__[A-Za-z0-9_]+)=(true|false)\s*$",
        env_text,
        flags=re.M,
    ):
        documented[match.group(1)] = match.group(2)

    expected: dict[str, str] = {}
    for path, value in walk_bools(options):
        env_key = path_to_env_key(path)
        if env_key is None:
            continue
        expected[env_key] = "true" if value is True else "false"

    missing = sorted(set(expected) - set(documented))
    extra = sorted(set(documented) - set(expected))
    mismatched = sorted(
        key for key in expected.keys() & documented.keys() if expected[key] != documented[key]
    )

    if missing:
        errors.append(f"platform.env.example missing scaffold keys: {missing}")
    if extra:
        errors.append(f"platform.env.example unexpected scaffold keys: {extra}")
    if mismatched:
        detail = {k: {"options": expected[k], "env": documented[k]} for k in mismatched}
        errors.append(f"scaffold bool value mismatch: {detail}")

    if errors:
        print("FAIL: platform.env ↔ scaffold options key parity", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: documented={len(documented)} expected={len(expected)} "
        f"(cutoverAllowed=false; no invented approval)"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
