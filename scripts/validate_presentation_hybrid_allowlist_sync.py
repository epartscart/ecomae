#!/usr/bin/env python3
"""Keep presentation nginx shadows, hybrid TARGETS, routes, installer, and YARP in sync.

Shells/logins/auth are nginx-only; hybrid TARGETS cover module *-app (+ /cp/orders) previews.
Never enables cutover. Never invents RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

NGINX_LOC_RE = re.compile(r"^\s*location\s+=\s+(\S+)\s*\{", re.M)
ROUTE_CONST_RE = re.compile(
    r'public const string \w+\s*=\s*"(/(?:cp|erp|bos|storefront)/[^"]+|/(?:auth/login/admin))";'
)
EXPECTED_RE = re.compile(r"^\s*expected\s*=\s*(\d+)\b", re.M)
HYBRID_ROUTE_RE = re.compile(r'\(\s*"[^"]+"\s*,\s*"[^"]+"\s*,\s*"(/[^"]+)"')
HYBRID_ROW_RE = re.compile(
    r'\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*,\s*"([^"]+)"'
)

# Presentation nginx entries that are not hybrid UI dual-sample TARGETS.
SHELLS_LOGINS_AUTH = frozenset(
    {
        "/cp/app",
        "/erp/app",
        "/bos/app",
        "/storefront/app",
        "/cp/login",
        "/erp/login",
        "/bos/login",
        "/storefront/login",
        "/auth/login/admin",
        "/cp/orders-digest",
    }
)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--nginx",
        type=Path,
        default=Path("deploy/aspnet/nginx-presentation-app-shadow-example.conf"),
    )
    ap.add_argument(
        "--hybrid-capture",
        type=Path,
        default=Path("scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh"),
    )
    ap.add_argument(
        "--routes",
        type=Path,
        default=Path("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"),
    )
    ap.add_argument(
        "--installer",
        type=Path,
        default=Path("scripts/cloudpanel_install_presentation_app_shadows.sh"),
    )
    ap.add_argument(
        "--yarp",
        type=Path,
        default=Path("deploy/aspnet/yarp-exact-routes-example.json"),
    )
    ap.add_argument(
        "--surface-nginx",
        type=Path,
        default=Path("deploy/aspnet/nginx-surface-digests-shadow-example.conf"),
    )
    ap.add_argument(
        "--storefront-nginx",
        type=Path,
        default=Path("deploy/aspnet/nginx-storefront-digests-shadow-example.conf"),
    )
    args = ap.parse_args()
    errors: list[str] = []

    nginx_text = args.nginx.read_text(encoding="utf-8")
    nginx_paths = NGINX_LOC_RE.findall(nginx_text)
    nginx_set = set(nginx_paths)
    if len(nginx_paths) != len(nginx_set):
        errors.append("nginx presentation shadow has duplicate location = blocks")

    hybrid_text = args.hybrid_capture.read_text(encoding="utf-8")
    hybrid_routes = HYBRID_ROUTE_RE.findall(hybrid_text)
    # HYBRID_ROUTE_RE may also catch digestRoute empties poorly; restrict to TARGETS block.
    targets_block = hybrid_text
    if "TARGETS = [" in hybrid_text:
        targets_block = hybrid_text.split("TARGETS = [", 1)[1].split("]", 1)[0]
    hybrid_routes = HYBRID_ROUTE_RE.findall(f"TARGETS = [{targets_block}]")
    hybrid_set = set(hybrid_routes)

    routes_text = args.routes.read_text(encoding="utf-8")
    route_consts = set(ROUTE_CONST_RE.findall(routes_text))
    # Presentation-relevant route constants: *App paths, /cp/orders, logins, auth.
    presentation_route_consts = {
        p
        for p in route_consts
        if p.endswith("-app")
        or p.endswith("/app")
        or p.endswith("/login")
        or p in {"/cp/orders", "/auth/login/admin", "/cp/orders-digest"}
    }

    installer_text = args.installer.read_text(encoding="utf-8")
    expected_m = EXPECTED_RE.search(installer_text)
    if not expected_m:
        errors.append("installer missing expected = N")
        expected = -1
    else:
        expected = int(expected_m.group(1))

    yarp = json.loads(args.yarp.read_text(encoding="utf-8"))
    if yarp.get("cutoverAllowed") is True or yarp.get("readyForPhpRemoval") is True:
        errors.append("YARP design example must keep cutoverAllowed/readyForPhpRemoval false")
    route_count = int(yarp.get("routeCount") or 0)

    if expected != len(nginx_set):
        errors.append(
            f"installer expected={expected} != nginx location count={len(nginx_set)}"
        )
    if route_count != len(nginx_set):
        errors.append(
            f"YARP routeCount={route_count} != nginx location count={len(nginx_set)}"
        )

    missing_nginx = sorted(hybrid_set - nginx_set)
    if missing_nginx:
        errors.append(f"hybrid TARGETS missing from nginx: {missing_nginx}")

    accounted = hybrid_set | SHELLS_LOGINS_AUTH
    nginx_extra = sorted(nginx_set - accounted)
    if nginx_extra:
        errors.append(
            f"nginx locations not in hybrid TARGETS or shells/logins/auth allowlist: {nginx_extra}"
        )

    missing_shells = sorted(SHELLS_LOGINS_AUTH - nginx_set)
    if missing_shells:
        errors.append(f"expected shells/logins/auth missing from nginx: {missing_shells}")

    # Every hybrid appRoute should have a matching EcomAeRoutes constant when it is an *-app or /cp/orders.
    route_app_paths = {p for p in presentation_route_consts if p.endswith("-app") or p == "/cp/orders"}
    hybrid_without_route = sorted(hybrid_set - route_app_paths)
    if hybrid_without_route:
        errors.append(f"hybrid TARGETS missing EcomAeRoutes constants: {hybrid_without_route}")

    route_apps_without_nginx = sorted(route_app_paths - nginx_set)
    if route_apps_without_nginx:
        errors.append(f"EcomAeRoutes *App paths missing from nginx: {route_apps_without_nginx}")

    # Cross-lock: hybrid digestRoute values must exist on digest nginx (or presentation for orders-digest).
    digest_nginx = set(NGINX_LOC_RE.findall(args.surface_nginx.read_text(encoding="utf-8")))
    digest_nginx |= set(NGINX_LOC_RE.findall(args.storefront_nginx.read_text(encoding="utf-8")))
    digest_nginx |= {"/cp/orders-digest"}
    hybrid_digest_routes = {
        digest for _stem, _surface, _app, digest, _php in HYBRID_ROW_RE.findall(targets_block) if digest
    }
    # Explicit exceptions: search/cart Blazor previews have no JSON digest exact-route.
    missing_digest_shadows = sorted(hybrid_digest_routes - digest_nginx)
    if missing_digest_shadows:
        errors.append(
            f"hybrid digestRoute missing from surface/storefront/orders-digest nginx: {missing_digest_shadows}"
        )

    if errors:
        print("FAIL: presentation/hybrid allowlist sync", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: nginx={len(nginx_set)} hybridTargets={len(hybrid_set)} "
        f"shellsLoginsAuth={len(SHELLS_LOGINS_AUTH)} hybridDigestRoutes={len(hybrid_digest_routes)} "
        f"expected={expected} yarpRouteCount={route_count}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
