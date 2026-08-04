#!/usr/bin/env python3
"""Keep storefront digest nginx, presentation apps, hybrid TARGETS, and floor in sync.

www exact-route only. Live ePartsCart/tenant storefront chrome stays PHP.
Never invents cutoverAllowed=true / RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

NGINX_LOC_RE = re.compile(r"^\s*location\s+=\s+(/storefront/\S+)\s*\{", re.M)
HYBRID_ROW_RE = re.compile(
    r'\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"'
)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--digest-nginx",
        type=Path,
        default=Path("deploy/aspnet/nginx-storefront-digests-shadow-example.conf"),
    )
    ap.add_argument(
        "--presentation-nginx",
        type=Path,
        default=Path("deploy/aspnet/nginx-presentation-app-shadow-example.conf"),
    )
    ap.add_argument(
        "--hybrid-capture",
        type=Path,
        default=Path("scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh"),
    )
    ap.add_argument(
        "--floor",
        type=Path,
        default=Path(
            "docs/migration/evidence/presentation/storefront-shadow-dual-sample-floor.json"
        ),
    )
    ap.add_argument(
        "--digest-installer",
        type=Path,
        default=Path("scripts/cloudpanel_install_storefront_digest_shadows.sh"),
    )
    ap.add_argument(
        "--digest-probe",
        type=Path,
        default=Path("scripts/cloudpanel_probe_storefront_digest_shadows.sh"),
    )
    args = ap.parse_args()
    errors: list[str] = []

    digests = sorted(set(NGINX_LOC_RE.findall(args.digest_nginx.read_text(encoding="utf-8"))))
    if len(digests) != 7:
        errors.append(f"digest nginx storefront locations={len(digests)} != 7")
    if "/storefront/checkout" not in digests:
        errors.append("digest nginx missing /storefront/checkout")

    apps = sorted(
        {
            p
            for p in NGINX_LOC_RE.findall(args.presentation_nginx.read_text(encoding="utf-8"))
            if p.endswith("-app") or p in {"/storefront/app", "/storefront/login"}
        }
    )
    expected_apps = {
        "/storefront/app",
        "/storefront/login",
        "/storefront/search-app",
        "/storefront/cart-app",
        "/storefront/checkout-app",
        "/storefront/orders-app",
        "/storefront/garage-app",
        "/storefront/profile-app",
        "/storefront/account-summary-app",
    }
    if set(apps) != expected_apps:
        errors.append(
            f"presentation storefront apps mismatch: "
            f"missing={sorted(expected_apps - set(apps))} "
            f"extra={sorted(set(apps) - expected_apps)}"
        )
    if "/storefront/checkout-app" not in apps:
        errors.append("presentation nginx missing /storefront/checkout-app")

    hybrid_text = args.hybrid_capture.read_text(encoding="utf-8")
    hybrid_sf = {
        (stem, app)
        for stem, surface, app, digest in HYBRID_ROW_RE.findall(hybrid_text)
        if surface == "storefront"
    }
    if len(hybrid_sf) != 7:
        errors.append(f"hybrid storefront TARGETS={len(hybrid_sf)} != 7")
    hybrid_apps = {app for _stem, app in hybrid_sf}
    expected_hybrid_apps = {
        "/storefront/search-app",
        "/storefront/cart-app",
        "/storefront/checkout-app",
        "/storefront/orders-app",
        "/storefront/garage-app",
        "/storefront/profile-app",
        "/storefront/account-summary-app",
    }
    if hybrid_apps != expected_hybrid_apps:
        errors.append(
            f"hybrid storefront apps mismatch: "
            f"missing={sorted(expected_hybrid_apps - hybrid_apps)} "
            f"extra={sorted(hybrid_apps - expected_hybrid_apps)}"
        )

    installer = args.digest_installer.read_text(encoding="utf-8")
    probe = args.digest_probe.read_text(encoding="utf-8")
    if "!= 7" not in installer and "expected 7 storefront digest" not in installer:
        errors.append("storefront digest installer must lock expected count 7")
    if "-ne 7" not in probe and "expected 7" not in probe:
        errors.append("storefront digest probe must lock expected count 7")

    if not args.floor.is_file():
        errors.append(f"missing floor {args.floor}")
    else:
        floor = json.loads(args.floor.read_text(encoding="utf-8"))
        if floor.get("cutoverAllowed") is not False:
            errors.append("floor cutoverAllowed must be false")
        if floor.get("readyForPhpRemoval") is not False:
            errors.append("floor readyForPhpRemoval must be false")
        if floor.get("tenantStorefrontPhp") is not True:
            errors.append("floor tenantStorefrontPhp must be true")
        floor_digests = floor.get("digestExactRoutes") or []
        floor_apps = floor.get("presentationExactRoutes") or []
        if set(floor_digests) != set(digests):
            errors.append(
                f"floor digestExactRoutes mismatch nginx: "
                f"missing={sorted(set(digests) - set(floor_digests))} "
                f"extra={sorted(set(floor_digests) - set(digests))}"
            )
        if set(floor_apps) != expected_apps:
            errors.append("floor presentationExactRoutes mismatch expected storefront apps")
        if int(floor.get("digestRouteCount") or 0) != 7:
            errors.append(f"floor digestRouteCount={floor.get('digestRouteCount')} != 7")
        if int(floor.get("presentationRouteCount") or 0) != 9:
            errors.append(
                f"floor presentationRouteCount={floor.get('presentationRouteCount')} != 9"
            )
        if int(floor.get("hybridTargetCount") or 0) != 7:
            errors.append(f"floor hybridTargetCount={floor.get('hybridTargetCount')} != 7")

    if errors:
        print("FAIL: storefront shadow allowlist sync", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: storefrontDigests=7 presentationApps=9 hybridTargets=7 "
        f"checkoutDigest+App present tenantStorefrontPhp=true cutoverAllowed=false"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
