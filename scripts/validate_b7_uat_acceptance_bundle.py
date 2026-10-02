#!/usr/bin/env python3
"""Validate one fail-closed B7 Treasury UAT and production bundle."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


REQUIRED_REFERENCES = {
    "uat": ("reference", "status", "happyPath", "denialPath", "browserReadback", "persistedReadback", "tenantIsolation"),
    "productionSmoke": ("reference", "status", "routeOwner"),
    "releaseIdentity": ("reference", "expectedSha", "observedSha", "matches"),
    "recoveryRollback": ("reference", "status", "validatedBundle"),
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

    require_non_empty_string(bundle.get("tenant"), "tenant")
    if bundle.get("process") != "B7":
        raise SystemExit("process must be B7")

    release = bundle.get("release")
    if not isinstance(release, dict):
        raise SystemExit("release must be an object")
    require_non_empty_string(release.get("sha"), "release.sha")
    require_non_empty_string(release.get("routeOwnerBefore"), "release.routeOwnerBefore")
    if release["routeOwnerBefore"].lower() != "aspnet":
        raise SystemExit("release.routeOwnerBefore must be aspnet")

    for section, fields in REQUIRED_REFERENCES.items():
        evidence = bundle.get(section)
        if not isinstance(evidence, dict):
            raise SystemExit(f"{section} must be an object")
        for field in fields:
            value = evidence.get(field)
            if field in ("status", "routeOwner", "validatedBundle"):
                require_non_empty_string(value, f"{section}.{field}")
            elif field == "matches":
                if value is not True:
                    raise SystemExit("releaseIdentity.matches must be true")
            else:
                require_non_empty_string(value, f"{section}.{field}")
            if isinstance(value, str) and "throwaway" in value.lower():
                raise SystemExit(f"{section}.{field} cannot reference throwaway evidence")

    for section in ("uat", "productionSmoke", "recoveryRollback"):
        if bundle[section]["status"].lower() != "pass":
            raise SystemExit(f"{section}.status must be pass")
    if bundle["productionSmoke"]["routeOwner"].lower() != "aspnet":
        raise SystemExit("productionSmoke.routeOwner must be aspnet")
    if bundle["recoveryRollback"]["validatedBundle"].lower() != "pass":
        raise SystemExit("recoveryRollback.validatedBundle must be pass")
    if bundle["releaseIdentity"]["expectedSha"] != bundle["releaseIdentity"]["observedSha"]:
        raise SystemExit("releaseIdentity expectedSha and observedSha must match")
    if bundle["releaseIdentity"]["observedSha"] != release["sha"]:
        raise SystemExit("releaseIdentity.observedSha must match release.sha")

    print(f"PASS: {bundle['tenant']}/B7 UAT and production bundle is complete")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
