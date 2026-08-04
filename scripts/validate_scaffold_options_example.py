#!/usr/bin/env python3
"""Validate consolidated Enterprise BOS scaffold options stay disabled.

Fails if cutover/removal is claimed or dangerous Replace/Allow/Register flags are true.
Never invents RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

# Bool flags that must remain false in the example file.
MUST_BE_FALSE_SUFFIXES = (
    "Enabled",
    "ReplaceMysqlBridge",
    "ReplacePhpSessionCookies",
    "AllowPublish",
    "ReplacePhpSearch",
    "ReplaceLocalFilePaths",
    "ReplaceEnvFileSecrets",
    "ReplacePhpCookieBridge",
    "ReplaceBlazorHybridPresentation",
    "RegisterExporters",
    "RegisterPipelines",
    "ExposePublicEndpoint",
    "RequireMfa",
    "UseAsBusinessSourceOfRecord",
    "ReplaceLegacyApiClientThrottle",
    "RequireForPlatformHost",
    "AllowBusinessWrites",
    "AllowWorkerWrites",
)

# Explicit allowlist for true values in scaffolding examples.
MAY_BE_TRUE = {
    "AllowIsolatedServiceEvaluation",
}


def walk(obj, prefix: str = "") -> list[tuple[str, object]]:
    items: list[tuple[str, object]] = []
    if isinstance(obj, dict):
        for key, value in obj.items():
            path = f"{prefix}.{key}" if prefix else str(key)
            if isinstance(value, (dict, list)):
                items.extend(walk(value, path))
            else:
                items.append((path, value))
    elif isinstance(obj, list):
        for idx, value in enumerate(obj):
            path = f"{prefix}[{idx}]"
            if isinstance(value, (dict, list)):
                items.extend(walk(value, path))
            else:
                items.append((path, value))
    return items


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--path",
        type=Path,
        default=Path("deploy/aspnet/ecomae-scaffold-options.example.json"),
    )
    args = ap.parse_args()
    doc = json.loads(args.path.read_text(encoding="utf-8"))

    errors: list[str] = []
    if doc.get("cutoverAllowed") is True:
        errors.append("cutoverAllowed must be false")
    if doc.get("readyForPhpRemoval") is True:
        errors.append("readyForPhpRemoval must be false")

    for path, value in walk(doc):
        key = path.rsplit(".", 1)[-1]
        if not isinstance(value, bool):
            continue
        if key in MAY_BE_TRUE:
            continue
        if key.endswith(MUST_BE_FALSE_SUFFIXES) or key in MUST_BE_FALSE_SUFFIXES:
            if value is True:
                errors.append(f"{path} must be false")

    if errors:
        print(f"FAIL: {args.path}", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(f"PASS: {args.path} cutoverAllowed=false dangerous flags disabled")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
