#!/usr/bin/env python3
"""Keep catalog/API exact-route nginx, YARP, migration goldens, and compare helpers in sync.

Covers 18 catalog routes + /api/v1/price/lookup (YARP routeCount=19).
Never invents RELEASE_OWNER_APPROVAL.md. cutoverAllowed stays false.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

NGINX_LOC_RE = re.compile(r"^\s*location\s+=\s+(/api/v1/\S+)\s*\{", re.M)

# path -> migration golden stem (price uses dedicated evidence samples)
PATH_TO_MIGRATION = {
    "/api/v1/catalog/status": "api-catalog-status",
    "/api/v1/catalog/manufacturers": "api-catalog-manufacturers",
    "/api/v1/catalog/models": "api-catalog-models",
    "/api/v1/catalog/modifications": "api-catalog-modifications",
    "/api/v1/catalog/brands": "api-catalog-brands",
    "/api/v1/catalog/suppliers": "api-catalog-suppliers",
    "/api/v1/catalog/engines": "api-catalog-engines",
    "/api/v1/catalog/analogs": "api-catalog-analogs",
    "/api/v1/catalog/article-brands": "api-catalog-article-brands",
    "/api/v1/catalog/categories": "api-catalog-categories",
    "/api/v1/catalog/products": "api-catalog-products",
    "/api/v1/catalog/engine-search": "api-catalog-engine-search",
    "/api/v1/catalog/article-links": "api-catalog-article-links",
    "/api/v1/catalog/article": "api-catalog-article",
    "/api/v1/catalog/articles": "api-catalog-articles",
    "/api/v1/catalog/engine": "api-catalog-engine",
    "/api/v1/catalog/vin": "api-catalog-vin",
    "/api/v1/catalog/brand-parts": "api-catalog-brand-parts",
}

REQUIRED_COMPARE_HELPERS = (
    "compare_catalog_status_parity.py",
    "compare_catalog_list_parity.py",
    "compare_catalog_offline_cache_parity.py",
    "compare_catalog_vin_parity.py",
    "compare_catalog_brand_parts_parity.py",
    "compare_price_lookup_parity.py",
    "compare_catalog_api_contract_floor.py",
)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--deploy-dir", type=Path, default=Path("deploy/aspnet"))
    ap.add_argument(
        "--migration-dir",
        type=Path,
        default=Path("docs/migration/evidence/surface-parity/samples/migration"),
    )
    ap.add_argument(
        "--yarp",
        type=Path,
        default=Path("deploy/aspnet/yarp-catalog-api-example.json"),
    )
    ap.add_argument("--scripts-dir", type=Path, default=Path("scripts"))
    args = ap.parse_args()
    errors: list[str] = []

    nginx_paths: list[str] = []
    for conf in sorted(args.deploy_dir.glob("nginx-catalog-*-shadow-example.conf")):
        nginx_paths.extend(NGINX_LOC_RE.findall(conf.read_text(encoding="utf-8")))
    for name in ("nginx-api-shadow-example.conf", "nginx-price-lookup-shadow-example.conf"):
        path = args.deploy_dir / name
        if path.is_file():
            nginx_paths.extend(NGINX_LOC_RE.findall(path.read_text(encoding="utf-8")))
    nginx_set = set(nginx_paths)
    if len(nginx_paths) != len(nginx_set):
        errors.append("catalog/api nginx has duplicate location = blocks")

    yarp = json.loads(args.yarp.read_text(encoding="utf-8"))
    if yarp.get("cutoverAllowed") is True or yarp.get("readyForPhpRemoval") is True:
        errors.append("YARP catalog-api design must keep cutoverAllowed/readyForPhpRemoval false")
    route_count = int(yarp.get("routeCount") or 0)
    yarp_paths = {
        route["Match"]["Path"]
        for route in (yarp.get("ReverseProxy") or {}).get("Routes", {}).values()
        if isinstance(route, dict) and isinstance(route.get("Match"), dict)
    }

    expected = set(PATH_TO_MIGRATION) | {"/api/v1/price/lookup"}
    if nginx_set != expected:
        errors.append(f"nginx paths mismatch expected: missing={sorted(expected-nginx_set)} extra={sorted(nginx_set-expected)}")
    if yarp_paths != expected:
        errors.append(f"YARP paths mismatch expected: missing={sorted(expected-yarp_paths)} extra={sorted(yarp_paths-expected)}")
    if route_count != len(expected):
        errors.append(f"YARP routeCount={route_count} != expected {len(expected)}")

    for path, stem in PATH_TO_MIGRATION.items():
        golden = args.migration_dir / f"{stem}.json"
        if not golden.is_file():
            errors.append(f"missing migration golden for {path}: {golden}")

    price_php = Path("docs/migration/evidence/price-lookup/php-baseline-sample.json")
    price_asp = Path("docs/migration/evidence/price-lookup/aspnet-output-sample.json")
    if not price_php.is_file() or not price_asp.is_file():
        errors.append("price-lookup dual samples missing under docs/migration/evidence/price-lookup/")

    for helper in REQUIRED_COMPARE_HELPERS:
        if not (args.scripts_dir / helper).is_file():
            errors.append(f"missing compare helper: scripts/{helper}")

    if errors:
        print("FAIL: catalog/API allowlist sync", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: nginx={len(nginx_set)} yarp={route_count} "
        f"catalogMigrationGoldens={len(PATH_TO_MIGRATION)} priceSamples=2"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
