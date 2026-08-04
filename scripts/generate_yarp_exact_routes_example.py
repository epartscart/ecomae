#!/usr/bin/env python3
"""Generate design-only YARP exact-route JSON from nginx presentation shadows.

Does NOT enable YARP. Output always sets cutoverAllowed=false.
Never invents RELEASE_OWNER_APPROVAL.md or broad catch-all routes.
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

LOC_RE = re.compile(r"^location\s+=\s+(\S+)\s*\{", re.M)
CUTOVER_RE = re.compile(
    r"location\s+=\s+(?P<path>\S+)\s*\{(?P<body>.*?)\}",
    re.S,
)
HEADER_RE = re.compile(
    r'proxy_set_header\s+X-EcomAE-Route-Cutover\s+(?P<header>[^;]+);'
)


def slug(path: str) -> str:
    return path.strip("/").replace("/", "-") or "root"


def parse_nginx(text: str) -> list[tuple[str, str]]:
    routes: list[tuple[str, str]] = []
    for match in CUTOVER_RE.finditer(text):
        path = match.group("path")
        body = match.group("body")
        header_match = HEADER_RE.search(body)
        header = (header_match.group("header").strip() if header_match else f"{slug(path)}-preview")
        routes.append((path, header))
    return routes


def build_doc(routes: list[tuple[str, str]]) -> dict:
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
            "Generated from deploy/aspnet/nginx-presentation-app-shadow-example.conf. "
            "Future YARP must proxy exact approved routes only; never catch-all /api, /cp, /erp, /bos, or /."
        ),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "routeCount": len(routes),
        "generatedFrom": "deploy/aspnet/nginx-presentation-app-shadow-example.conf",
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


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--nginx",
        type=Path,
        default=Path("deploy/aspnet/nginx-presentation-app-shadow-example.conf"),
    )
    ap.add_argument(
        "--out",
        type=Path,
        default=Path("deploy/aspnet/yarp-exact-routes-example.json"),
    )
    args = ap.parse_args()
    text = args.nginx.read_text(encoding="utf-8")
    routes = parse_nginx(text)
    if not routes:
        raise SystemExit(f"FAIL: no exact location = routes in {args.nginx}")
    doc = build_doc(routes)
    args.out.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {args.out} routeCount={len(routes)} cutoverAllowed=false")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
