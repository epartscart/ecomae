#!/usr/bin/env python3
"""Generate design-only YARP exact-route JSON from nginx shadow allowlists.

Does NOT enable YARP. Output always sets cutoverAllowed=false.
Never invents RELEASE_OWNER_APPROVAL.md or broad catch-all routes.
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

CUTOVER_RE = re.compile(
    r"location\s+=\s+(?P<path>\S+)\s*\{(?P<body>.*?)\}",
    re.S,
)
HEADER_RE = re.compile(
    r"proxy_set_header\s+X-EcomAE-Route-Cutover\s+(?P<header>[^;]+);"
)


def slug(path: str) -> str:
    return path.strip("/").replace("/", "-").replace("?", "-") or "root"


def parse_nginx(text: str) -> list[tuple[str, str]]:
    routes: list[tuple[str, str]] = []
    for match in CUTOVER_RE.finditer(text):
        # Skip commented-out location examples (lines starting with #).
        line_start = text.rfind("\n", 0, match.start()) + 1
        if text[line_start : match.start()].lstrip().startswith("#"):
            continue
        path = match.group("path")
        body = match.group("body")
        header_match = HEADER_RE.search(body)
        header = (
            header_match.group("header").strip()
            if header_match
            else f"{slug(path)}-preview"
        )
        routes.append((path, header))
    return routes


def merge_routes(route_lists: list[list[tuple[str, str]]]) -> list[tuple[str, str]]:
    seen: set[str] = set()
    ordered: list[tuple[str, str]] = []
    for routes in route_lists:
        for path, header in routes:
            if path in seen:
                continue
            seen.add(path)
            ordered.append((path, header))
    return ordered


def build_doc(routes: list[tuple[str, str]], *, sources: list[str]) -> dict:
    yarp_routes = {}
    for path, header in routes:
        key = slug(path)
        # keep unique keys if slug collisions (shouldn't for exact paths)
        if key in yarp_routes:
            key = f"{key}-{len(yarp_routes)}"
        yarp_routes[key] = {
            "ClusterId": "ecomae-platform",
            "Match": {"Path": path},
            "Transforms": [
                {"RequestHeader": "X-EcomAE-Route-Cutover", "Set": header},
            ],
        }
    return {
        "note": (
            "DESIGN ONLY — not loaded by Program.cs. Nginx remains the CloudPanel edge. "
            "Generated from nginx exact-route shadow allowlists. "
            "Future YARP must proxy exact approved routes only; never catch-all /api, /cp, /erp, /bos, or /."
        ),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "routeCount": len(routes),
        "generatedFrom": sources,
        "ReverseProxy": {
            "Routes": yarp_routes,
            "Clusters": {
                "ecomae-platform": {
                    "Destinations": {
                        "loopback": {"Address": "http://127.0.0.1:5100/"},
                    }
                }
            },
        },
    }


def write_doc(path: Path, doc: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {path} routeCount={doc['routeCount']} cutoverAllowed=false")


def load_routes(path: Path) -> list[tuple[str, str]]:
    text = path.read_text(encoding="utf-8")
    routes = parse_nginx(text)
    if not routes:
        raise SystemExit(f"FAIL: no exact location = routes in {path}")
    return routes


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--presentation-nginx",
        type=Path,
        default=Path("deploy/aspnet/nginx-presentation-app-shadow-example.conf"),
    )
    ap.add_argument(
        "--surface-digests-nginx",
        type=Path,
        default=Path("deploy/aspnet/nginx-surface-digests-shadow-example.conf"),
    )
    ap.add_argument(
        "--storefront-digests-nginx",
        type=Path,
        default=Path("deploy/aspnet/nginx-storefront-digests-shadow-example.conf"),
    )
    ap.add_argument(
        "--catalog-glob",
        type=str,
        default="deploy/aspnet/nginx-catalog-*-shadow-example.conf",
    )
    ap.add_argument(
        "--extra-api-nginx",
        action="append",
        default=[
            "deploy/aspnet/nginx-api-shadow-example.conf",
            "deploy/aspnet/nginx-price-lookup-shadow-example.conf",
        ],
        help="Additional exact-route API nginx examples (repeatable).",
    )
    ap.add_argument(
        "--out-presentation",
        type=Path,
        default=Path("deploy/aspnet/yarp-exact-routes-example.json"),
    )
    ap.add_argument(
        "--out-surface-digests",
        type=Path,
        default=Path("deploy/aspnet/yarp-surface-digests-example.json"),
    )
    ap.add_argument(
        "--out-storefront-digests",
        type=Path,
        default=Path("deploy/aspnet/yarp-storefront-digests-example.json"),
    )
    ap.add_argument(
        "--out-catalog-api",
        type=Path,
        default=Path("deploy/aspnet/yarp-catalog-api-example.json"),
    )
    ap.add_argument("--skip-surface-digests", action="store_true")
    ap.add_argument("--skip-storefront-digests", action="store_true")
    ap.add_argument("--skip-catalog-api", action="store_true")
    args = ap.parse_args()

    presentation_routes = load_routes(args.presentation_nginx)
    write_doc(
        args.out_presentation,
        build_doc(presentation_routes, sources=[str(args.presentation_nginx)]),
    )

    if not args.skip_surface_digests:
        surface_routes = load_routes(args.surface_digests_nginx)
        write_doc(
            args.out_surface_digests,
            build_doc(surface_routes, sources=[str(args.surface_digests_nginx)]),
        )

    if not args.skip_storefront_digests:
        storefront_routes = load_routes(args.storefront_digests_nginx)
        write_doc(
            args.out_storefront_digests,
            build_doc(storefront_routes, sources=[str(args.storefront_digests_nginx)]),
        )

    if not args.skip_catalog_api:
        sources: list[str] = []
        route_lists: list[list[tuple[str, str]]] = []
        for path in sorted(Path().glob(args.catalog_glob)):
            route_lists.append(load_routes(path))
            sources.append(str(path))
        for raw in args.extra_api_nginx or []:
            path = Path(raw)
            if not path.is_file():
                continue
            route_lists.append(load_routes(path))
            sources.append(str(path))
        if not route_lists:
            raise SystemExit("FAIL: no catalog/api nginx examples found")
        catalog_routes = merge_routes(route_lists)
        write_doc(
            args.out_catalog_api,
            build_doc(catalog_routes, sources=sources),
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
