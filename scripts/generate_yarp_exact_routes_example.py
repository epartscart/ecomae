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
    return path.strip("/").replace("/", "-") or "root"


def parse_nginx(text: str) -> list[tuple[str, str]]:
    routes: list[tuple[str, str]] = []
    for match in CUTOVER_RE.finditer(text):
        # Skip commented-out location examples (lines starting with #).
        line_start = text.rfind("\n", 0, match.start()) + 1
        if text[line_start:match.start()].lstrip().startswith("#"):
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
    seen: dict[str, str] = {}
    ordered: list[tuple[str, str]] = []
    for routes in route_lists:
        for path, header in routes:
            if path in seen:
                continue
            seen[path] = header
            ordered.append((path, header))
    return ordered


def build_doc(routes: list[tuple[str, str]], *, sources: list[str]) -> dict:
    yarp_routes = {}
    for path, header in routes:
        key = slug(path)
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
        "--skip-surface-digests",
        action="store_true",
        help="Only regenerate presentation YARP JSON.",
    )
    args = ap.parse_args()

    presentation_text = args.presentation_nginx.read_text(encoding="utf-8")
    presentation_routes = parse_nginx(presentation_text)
    if not presentation_routes:
        raise SystemExit(f"FAIL: no exact location = routes in {args.presentation_nginx}")

    write_doc(
        args.out_presentation,
        build_doc(
            presentation_routes,
            sources=[str(args.presentation_nginx)],
        ),
    )

    if not args.skip_surface_digests:
        digests_text = args.surface_digests_nginx.read_text(encoding="utf-8")
        digest_routes = parse_nginx(digests_text)
        if not digest_routes:
            raise SystemExit(f"FAIL: no exact location = routes in {args.surface_digests_nginx}")
        write_doc(
            args.out_surface_digests,
            build_doc(
                digest_routes,
                sources=[str(args.surface_digests_nginx)],
            ),
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
