#!/usr/bin/env python3
"""Validate one fail-closed B8 tenant-country tax acceptance bundle."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


REQUIRED_SECTIONS = {
    "countryProfile": ("reference", "registeredCountry", "previewOverride"),
    "functional": ("reference", "status", "happyPath", "denialPath", "persistedReadback", "browserReadback"),
    "tenantIsolation": ("reference", "status", "tenantTwoReadback", "tenantOneNonVisibility"),
    "productionRecovery": ("reference", "status", "backupChecksum", "restoreReadback", "rollbackRouteOwner"),
    "productionSmoke": ("reference", "status", "routeOwner"),
    "releaseIdentity": ("reference", "expectedSha", "observedSha", "matches"),
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
    if bundle.get("process") != "B8":
        raise SystemExit("process must be B8")

    require_non_empty_string(bundle.get("tenant"), "tenant")
    tenant_country = bundle.get("tenantCountry")
    require_non_empty_string(tenant_country, "tenantCountry")
    if len(tenant_country.strip()) != 2:
        raise SystemExit("tenantCountry must be an ISO-style two-letter code")

    release = bundle.get("release")
    if not isinstance(release, dict):
        raise SystemExit("release must be an object")
    require_non_empty_string(release.get("sha"), "release.sha")
    require_non_empty_string(release.get("routeOwnerBefore"), "release.routeOwnerBefore")
    if release["routeOwnerBefore"].lower() != "aspnet":
        raise SystemExit("release.routeOwnerBefore must be aspnet")

    for section, fields in REQUIRED_SECTIONS.items():
        evidence = bundle.get(section)
        if not isinstance(evidence, dict):
            raise SystemExit(f"{section} must be an object")
        for field in fields:
            value = evidence.get(field)
            if field == "matches":
                if value is not True:
                    raise SystemExit("releaseIdentity.matches must be true")
            elif field == "previewOverride":
                if value is not False:
                    raise SystemExit("countryProfile.previewOverride must be false")
            else:
                require_non_empty_string(value, f"{section}.{field}")
            if isinstance(value, str) and "throwaway" in value.lower():
                raise SystemExit(f"{section}.{field} cannot reference throwaway evidence")

    country = bundle["countryProfile"]
    if country["registeredCountry"].strip().upper() != tenant_country.strip().upper():
        raise SystemExit("countryProfile.registeredCountry must match tenantCountry")
    for section in ("functional", "tenantIsolation", "productionRecovery", "productionSmoke"):
        if bundle[section]["status"].lower() != "pass":
            raise SystemExit(f"{section}.status must be pass")
    if bundle["productionRecovery"]["rollbackRouteOwner"].lower() != "php":
        raise SystemExit("productionRecovery.rollbackRouteOwner must be php")
    if bundle["productionSmoke"]["routeOwner"].lower() != "aspnet":
        raise SystemExit("productionSmoke.routeOwner must be aspnet")
    identity = bundle["releaseIdentity"]
    if identity["expectedSha"] != identity["observedSha"]:
        raise SystemExit("releaseIdentity expectedSha and observedSha must match")
    if identity["observedSha"] != release["sha"]:
        raise SystemExit("releaseIdentity.observedSha must match release.sha")

    print(f"PASS: {bundle['tenant']}/{tenant_country.upper()}/B8 acceptance bundle is complete")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
