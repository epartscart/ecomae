#!/usr/bin/env python3
"""Validate one fail-closed B5 Finance/GL production recovery bundle."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


REQUIRED_REFERENCE_FIELDS = {
    "productionBackup": ("reference", "checksum", "capturedAt"),
    "restoreReadback": ("reference", "capturedAt"),
    "ownershipRollback": ("reference", "nginxTest", "reload", "phpRouteSmoke"),
    "cleanup": ("reference",),
    "releaseOwnerApproval": ("reference",),
}


def require_non_empty_string(value: object, label: str) -> None:
    if not isinstance(value, str) or not value.strip():
        raise SystemExit(f"{label} must be a non-empty string")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("path")
    args = parser.parse_args()

    bundle = json.loads(Path(args.path).read_text(encoding="utf-8"))
    if bundle.get("cutoverAllowed") is not False:
        raise SystemExit("cutoverAllowed must be false")
    if bundle.get("keepPhpFallback") is not True:
        raise SystemExit("keepPhpFallback must be true")
    if bundle.get("readyForPhpRemoval") is not False:
        raise SystemExit("readyForPhpRemoval must be false")
    if bundle.get("productionEvidence") is not True:
        raise SystemExit("productionEvidence must be true")

    require_non_empty_string(bundle.get("tenant"), "tenant")
    if bundle.get("process") != "B5":
        raise SystemExit("process must be B5")

    release = bundle.get("release")
    if not isinstance(release, dict):
        raise SystemExit("release must be an object")
    for field in ("sha", "health", "routeOwnerBefore"):
        require_non_empty_string(release.get(field), f"release.{field}")
    if release["routeOwnerBefore"].lower() != "aspnet":
        raise SystemExit("release.routeOwnerBefore must be aspnet")

    for section, fields in REQUIRED_REFERENCE_FIELDS.items():
        evidence = bundle.get(section)
        if not isinstance(evidence, dict):
            raise SystemExit(f"{section} must be an object")
        for field in fields:
            require_non_empty_string(evidence.get(field), f"{section}.{field}")
        for field in ("reference", "checksum"):
            value = evidence.get(field)
            if isinstance(value, str) and "throwaway" in value.lower():
                raise SystemExit(f"{section}.{field} cannot reference throwaway evidence")

    rollback = bundle["ownershipRollback"]
    for field in ("nginxTest", "reload", "phpRouteSmoke"):
        if rollback[field].lower() != "pass":
            raise SystemExit(f"ownershipRollback.{field} must be pass")
    require_non_empty_string(rollback.get("aspnetRouteOwnerAfter"), "ownershipRollback.aspnetRouteOwnerAfter")
    if rollback["aspnetRouteOwnerAfter"].lower() != "php":
        raise SystemExit("ownershipRollback.aspnetRouteOwnerAfter must be php")

    print(f"PASS: {bundle['tenant']}/B5 recovery bundle is complete")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
