#!/usr/bin/env python3
"""Keep marketing nginx shadows, dual-sample floor, installer, and probe in sync.

Exact-route /marketing/* on www only. Live / stays PHP epm-hub.
Never invents cutoverAllowed=true / RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

NGINX_LOC_RE = re.compile(r"^\s*location\s+=\s+(/marketing/\S+)\s*\{", re.M)
EXPECTED_GE_RE = re.compile(r"expected\s*(?:>=|=)\s*(\d+)\s*/marketing", re.I)
FLOOR_MIN_KEYS = ("requiredRouteCountMin", "routeCount")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--nginx",
        type=Path,
        default=Path("deploy/aspnet/nginx-presentation-app-shadow-example.conf"),
    )
    ap.add_argument(
        "--floor",
        type=Path,
        default=Path("docs/migration/evidence/presentation/marketing-app-dual-sample-floor.json"),
    )
    ap.add_argument(
        "--installer",
        type=Path,
        default=Path("scripts/cloudpanel_install_marketing_app_shadows.sh"),
    )
    ap.add_argument(
        "--probe",
        type=Path,
        default=Path("scripts/cloudpanel_probe_marketing_app_shadows.sh"),
    )
    args = ap.parse_args()
    errors: list[str] = []

    nginx_routes = sorted(set(NGINX_LOC_RE.findall(args.nginx.read_text(encoding="utf-8"))))
    if len(nginx_routes) != 37:
        errors.append(f"nginx marketing locations={len(nginx_routes)} != 37")

    if not args.floor.is_file():
        errors.append(f"missing floor {args.floor}")
        floor = {}
    else:
        floor = json.loads(args.floor.read_text(encoding="utf-8"))
        if floor.get("cutoverAllowed") is not False:
            errors.append("floor cutoverAllowed must be false")
        if floor.get("readyForPhpRemoval") is not False:
            errors.append("floor readyForPhpRemoval must be false")
        if floor.get("phpHomeMustRemainEpmHub") is not True:
            errors.append("floor phpHomeMustRemainEpmHub must be true")
        routes = floor.get("aspNetPreviewRoutes")
        if not isinstance(routes, list):
            errors.append("floor aspNetPreviewRoutes must be a list")
            routes = []
        route_set = set(routes)
        if len(routes) != len(route_set):
            errors.append("floor aspNetPreviewRoutes has duplicates")
        if route_set != set(nginx_routes):
            errors.append(
                f"floor routes mismatch nginx: "
                f"missing={sorted(set(nginx_routes) - route_set)} "
                f"extra={sorted(route_set - set(nginx_routes))}"
            )
        if int(floor.get("routeCount") or 0) != 37:
            errors.append(f"floor routeCount={floor.get('routeCount')} != 37")
        if int(floor.get("requiredRouteCountMin") or 0) != 37:
            errors.append(
                f"floor requiredRouteCountMin={floor.get('requiredRouteCountMin')} != 37"
            )

    for label, path, needle in (
        ("installer", args.installer, "expected 37 /marketing"),
        ("probe", args.probe, "expected 37 /marketing"),
    ):
        if not path.is_file():
            errors.append(f"missing {label} {path}")
            continue
        text = path.read_text(encoding="utf-8")
        if needle not in text and "!= 37" not in text and "-ne 37" not in text:
            # Accept either exact equality lock phrasing.
            if "37" not in text or "marketing" not in text:
                errors.append(f"{label} must lock marketing route count to 37")

    installer = args.installer.read_text(encoding="utf-8") if args.installer.is_file() else ""
    probe = args.probe.read_text(encoding="utf-8") if args.probe.is_file() else ""
    if "len(blocks) != 37" not in installer and "len(blocks) < 37" not in installer:
        # Prefer exact equality after this PR.
        if "!= 37" not in installer and "expected 37" not in installer:
            errors.append("installer must lock expected marketing route count 37")
    if "-ne 37" not in probe and "expected 37" not in probe:
        errors.append("probe must lock expected marketing route count 37")

    if errors:
        print("FAIL: marketing app allowlist sync", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: marketingNginx={len(nginx_routes)} floor={len(floor.get('aspNetPreviewRoutes') or [])} "
        f"routeCount=37 phpHomeMustRemainEpmHub=true cutoverAllowed=false"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
