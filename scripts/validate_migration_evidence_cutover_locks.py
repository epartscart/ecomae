#!/usr/bin/env python3
"""Fail if migration evidence claims cutover/PHP removal or invents approval files.

Never invents RELEASE_OWNER_APPROVAL.md. Designed for CI / foundation checks.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

FORBIDDEN_FILES = (
    "RELEASE_OWNER_APPROVAL.md",
    "MODULE_FUNCTION_TEST_PASS.md",
    "MODULE_FUNCTION_PARITY_PASS",
    "MODULE_FUNCTION_PARITY_PASS.md",
)


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


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--evidence-root",
        type=Path,
        default=Path("docs/migration/evidence"),
    )
    ap.add_argument(
        "--docs-root",
        type=Path,
        default=Path("docs/migration"),
    )
    args = ap.parse_args()

    errors: list[str] = []
    scanned = 0

    for path in sorted(args.evidence_root.rglob("*.json")):
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            errors.append(f"{path}: invalid JSON ({ex})")
            continue
        scanned += 1
        if not isinstance(doc, dict):
            continue
        for key_path, value in walk_bools(doc):
            leaf = key_path.rsplit(".", 1)[-1]
            if leaf in {"cutoverAllowed", "readyForPhpRemoval", "readyToRemovePhp"} and value is True:
                errors.append(f"{path}: {key_path}=true (must stay false)")

    for name in FORBIDDEN_FILES:
        hits = sorted(args.docs_root.rglob(name))
        for hit in hits:
            errors.append(f"forbidden approval/pass artifact present: {hit}")

    if errors:
        print("FAIL: migration evidence cutover locks", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: scanned {scanned} evidence JSON files; "
        "cutoverAllowed/readyForPhpRemoval stay false; no invented approval files"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
