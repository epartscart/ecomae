#!/usr/bin/env python3
"""Keep surface/storefront digest nginx, capture ROUTES, compare contracts, and YARP in sync.

Also requires /cp/orders-digest (presentation shadow) in digest dual-sample contracts.
Never invents RELEASE_OWNER_APPROVAL.md. cutoverAllowed stays false.
"""
from __future__ import annotations

import argparse
import ast
import json
import re
import sys
from pathlib import Path

NGINX_LOC_RE = re.compile(r"^\s*location\s+=\s+(/(?:cp|erp|bos|storefront)/\S+)\s*\{", re.M)
ROUTE_ASSIGN_RE = re.compile(r'\[([a-z0-9-]+)\]="([^"]+)"')


def path_to_stem(path: str) -> str:
    return path.strip("/").replace("/", "-")


def load_compare_stems(compare_path: Path) -> set[str]:
    tree = ast.parse(compare_path.read_text(encoding="utf-8"), filename=str(compare_path))
    stems: set[str] = set()
    for node in tree.body:
        if not isinstance(node, ast.Assign):
            continue
        for target in node.targets:
            if not isinstance(target, ast.Name):
                continue
            if target.id not in {"SUMMARY_CONTRACTS", "LIST_CONTRACTS", "OBJECT_CONTRACTS"}:
                continue
            if isinstance(node.value, ast.Dict):
                for key in node.value.keys:
                    if isinstance(key, ast.Constant) and isinstance(key.value, str):
                        stems.add(key.value)
    return stems


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
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
    ap.add_argument(
        "--capture",
        type=Path,
        default=Path("scripts/cloudpanel_capture_digest_dual_samples.sh"),
    )
    ap.add_argument(
        "--compare",
        type=Path,
        default=Path("scripts/compare_digest_dual_samples.py"),
    )
    ap.add_argument(
        "--migration-dir",
        type=Path,
        default=Path("docs/migration/evidence/surface-parity/samples/migration"),
    )
    ap.add_argument(
        "--yarp-surface",
        type=Path,
        default=Path("deploy/aspnet/yarp-surface-digests-example.json"),
    )
    ap.add_argument(
        "--yarp-storefront",
        type=Path,
        default=Path("deploy/aspnet/yarp-storefront-digests-example.json"),
    )
    ap.add_argument(
        "--surface-installer",
        type=Path,
        default=Path("scripts/cloudpanel_install_surface_digest_shadows.sh"),
    )
    ap.add_argument(
        "--inventory",
        type=Path,
        default=Path("docs/migration/evidence/surface-parity/surface-digest-exact-routes.json"),
    )
    ap.add_argument("--scripts-dir", type=Path, default=Path("scripts"))
    args = ap.parse_args()
    errors: list[str] = []

    OPERATOR_PROBE_SCRIPTS = (
        "probe_live_surface_stack.sh",
        "run_php_decommission_area_tests.sh",
        "verify_pre_php_removal_parity.sh",
    )
    INVENTORY_MARKER = "surface-parity/surface-digest-exact-routes.json"

    surface_paths = NGINX_LOC_RE.findall(args.surface_nginx.read_text(encoding="utf-8"))
    storefront_paths = NGINX_LOC_RE.findall(args.storefront_nginx.read_text(encoding="utf-8"))
    surface_set = set(surface_paths)
    storefront_set = set(storefront_paths)
    if len(surface_paths) != len(surface_set):
        errors.append("surface digest nginx has duplicate location = blocks")
    if len(storefront_paths) != len(storefront_set):
        errors.append("storefront digest nginx has duplicate location = blocks")

    # Presentation-only digest also covered by dual-sample contracts/capture.
    extra_presentation = {"/cp/orders-digest"}
    all_digest_paths = surface_set | storefront_set | extra_presentation
    expected_stems = {path_to_stem(p) for p in all_digest_paths}

    capture_text = args.capture.read_text(encoding="utf-8")
    capture_routes = {stem: path.split("?", 1)[0] for stem, path in ROUTE_ASSIGN_RE.findall(capture_text)}
    capture_stems = set(capture_routes)

    compare_stems = load_compare_stems(args.compare)

    mig_stems = {
        p.stem
        for p in args.migration_dir.glob("*.json")
        if not p.stem.startswith("api-")
    }

    yarp_surface = json.loads(args.yarp_surface.read_text(encoding="utf-8"))
    yarp_storefront = json.loads(args.yarp_storefront.read_text(encoding="utf-8"))
    for label, doc in (("surface", yarp_surface), ("storefront", yarp_storefront)):
        if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
            errors.append(f"YARP {label} digests must keep cutoverAllowed/readyForPhpRemoval false")

    surface_count = int(yarp_surface.get("routeCount") or 0)
    storefront_count = int(yarp_storefront.get("routeCount") or 0)
    if surface_count != len(surface_set):
        errors.append(f"YARP surface routeCount={surface_count} != nginx={len(surface_set)}")
    if storefront_count != len(storefront_set):
        errors.append(
            f"YARP storefront routeCount={storefront_count} != nginx={len(storefront_set)}"
        )

    installer = args.surface_installer.read_text(encoding="utf-8")
    if "expected 127 digest locations" not in installer and "!= 127" not in installer:
        # Accept either style used by installer.
        if "32" not in installer:
            errors.append("surface digest installer does not lock expected count 32")

    missing_capture = sorted(expected_stems - capture_stems)
    extra_capture = sorted(capture_stems - expected_stems)
    missing_compare = sorted(expected_stems - compare_stems)
    extra_compare = sorted(compare_stems - expected_stems)
    missing_mig = sorted(expected_stems - mig_stems)

    if missing_capture:
        errors.append(f"capture ROUTES missing stems: {missing_capture}")
    if extra_capture:
        errors.append(f"capture ROUTES unexpected stems: {extra_capture}")
    if missing_compare:
        errors.append(f"compare contracts missing stems: {missing_compare}")
    if extra_compare:
        errors.append(f"compare contracts unexpected stems: {extra_compare}")
    if missing_mig:
        errors.append(f"migration goldens missing stems: {missing_mig}")

    # Capture path must match stem mapping.
    for stem, path in sorted(capture_routes.items()):
        if path_to_stem(path) != stem:
            errors.append(f"capture route stem/path mismatch: {stem} -> {path}")

    # Checked-in inventory must mirror digest exact-route floor (35).
    if not args.inventory.is_file():
        errors.append(f"missing surface-digest exact-route inventory: {args.inventory}")
    else:
        try:
            inventory = json.loads(args.inventory.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            errors.append(f"{args.inventory}: invalid JSON ({ex})")
            inventory = None
        if isinstance(inventory, dict):
            if inventory.get("cutoverAllowed") is not False:
                errors.append(f"{args.inventory}: cutoverAllowed must be explicitly false")
            if inventory.get("readyForPhpRemoval") is not False:
                errors.append(f"{args.inventory}: readyForPhpRemoval must be explicitly false")
            inv_routes = inventory.get("routes")
            if not isinstance(inv_routes, list):
                errors.append(f"{args.inventory}: routes must be a list")
            else:
                inv_set = set(inv_routes)
                if len(inv_routes) != len(inv_set):
                    errors.append(f"{args.inventory}: duplicate routes")
                if inv_set != all_digest_paths:
                    errors.append(
                        f"{args.inventory} routes mismatch digest floor: "
                        f"missing={sorted(all_digest_paths - inv_set)} "
                        f"extra={sorted(inv_set - all_digest_paths)}"
                    )
                if int(inventory.get("routeCount") or 0) != len(all_digest_paths):
                    errors.append(
                        f"{args.inventory} routeCount={inventory.get('routeCount')} "
                        f"!= digest floor {len(all_digest_paths)}"
                    )

    for script_name in OPERATOR_PROBE_SCRIPTS:
        script_path = args.scripts_dir / script_name
        if not script_path.is_file():
            errors.append(f"missing operator probe script: scripts/{script_name}")
            continue
        text = script_path.read_text(encoding="utf-8")
        if INVENTORY_MARKER not in text:
            errors.append(
                f"{script_name} must reference surface-digest exact-route inventory "
                f"({INVENTORY_MARKER})"
            )

    if errors:
        print("FAIL: surface/storefront digest allowlist sync", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: surfaceNginx={len(surface_set)} storefrontNginx={len(storefront_set)} "
        f"ordersDigest=1 capture={len(capture_stems)} compare={len(compare_stems)} "
        f"migrationGoldens={len(expected_stems)} yarp={surface_count}+{storefront_count} "
        f"inventory={len(all_digest_paths)} operatorProbes={len(OPERATOR_PROBE_SCRIPTS)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
