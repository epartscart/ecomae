#!/usr/bin/env python3
"""Offline estimate of presentation scaffold razor markup depth and login chrome markers.

Writes evidence JSON with cutoverAllowed=false. Does not claim live parity pass.
"""
from __future__ import annotations

import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs/migration/evidence/presentation/presentation-scaffold-bytes-estimate.json"

FLOORS = {
    "storefront_app": 40558,
    "bos_login": 12000,
    "erp_login": 42653,
}

SOURCES = {
    "storefront_home_depth": ROOT / "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontHomeDepth.razor",
    "storefront_app": ROOT / "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPreviewApp.razor",
    "piston_banner": ROOT / "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpAspPistonBanner.razor",
    "bos_login": ROOT / "aspnet/src/EcomAE.Platform/Components/Pages/BosLoginApp.razor",
    "erp_login": ROOT / "aspnet/src/EcomAE.Platform/Components/Pages/ErpLoginApp.razor",
    "cp_login": ROOT / "aspnet/src/EcomAE.Platform/Components/Pages/CpLoginApp.razor",
}

MARKERS = {
    "bos_login": ["epc-asp-login-bos", "bosParticles", "bos-login"],
    "erp_login": ["epc-asp-login-erp", "epc-erp-portal-wrap", "erpPortalParticles"],
    "cp_login": ["epc-cp-login-hero", "epcCpParticles"],
    "storefront_home_depth": ["epc-sf-home-depth", "epc-sf-cat-grid", "epc-sf-vin-cta"],
}

CATALOG = ROOT / "aspnet/src/EcomAE.Platform/Presentation/Generated/PhpModuleCatalog.g.cs"


def strip_razor(text: str) -> str:
    text = re.sub(r"@\*.*?\*@", "", text, flags=re.S)
    text = re.sub(r"@code\b.*", "", text, flags=re.S | re.I)
    return text


def count_foreach_loops(text: str) -> int:
    return len(re.findall(r"@foreach\b", text))


def parse_catalog_counts() -> dict[str, int]:
    if not CATALOG.is_file():
        return {}
    src = CATALOG.read_text(encoding="utf-8")
    out: dict[str, int] = {}
    for key in ("ErpAreaCount", "ErpTabCount", "ErpCategoryCount", "BosModuleCount"):
        m = re.search(rf"public const int {key} = (\d+);", src)
        if m:
            out[key] = int(m.group(1))
    return out


def estimate_featured_strip_bytes(text: str) -> int:
    """PhpStorefrontHomeDepth BuildFeaturedStrips: 4 strips × 12 items."""
    m = re.search(r"for \(var s = 0; s < titles\.Length; s\+\+\)", text)
    if not m:
        return 0
    strips = 4
    items_per = 12
    sample = (
        '<a class="epc-sf-featured" href="/storefront/search-app">'
        '<span class="epc-sf-featured__badge">OEM</span>'
        '<strong>Line 48 — workshop grade component</strong>'
        '<span class="epc-sf-featured__sku">ASP-0048-OEM</span>'
        '<span class="epc-sf-featured__price">AED 858</span>'
        '<small>Dispatch lane shown on offer · cross-ref available</small></a>'
    )
    return strips * items_per * len(sample.encode("utf-8"))


def estimate_static_arrays(text: str) -> int:
    """Rough expansion for static readonly arrays in @code (categories, brands, etc.)."""
    arrays = re.findall(r"private static readonly \w+\[\] (\w+) =", text)
    bonus = 0
    name_hints = {
        "Categories": 16 * 220,
        "Brands": 24 * 90,
        "VehicleMakes": 24 * 70,
        "TrustItems": 8 * 120,
        "FaqItems": 12 * 180,
        "Testimonials": 6 * 160,
        "LoginIndustries": 11 * 140,
        "PlatformDeck": 6 * 150,
        "GuideFeatures": 8 * 80,
    }
    for arr in arrays:
        bonus += name_hints.get(arr, 0)
    return bonus


def estimate_erp_tabs_bytes(counts: dict[str, int]) -> int:
    tabs = counts.get("ErpTabCount", 154)
    sample = (
        '<a class="epc-erp-home-stat" href="/ERP/?epc_erp_shell=1&area=banking&tab=cash_bank">'
        '<strong><i class="fa fa-university"></i> Cash & bank</strong>'
        '<span>banking/cash_bank</span></a>'
    )
    areas = counts.get("ErpAreaCount", 35)
    area_card = (
        '<div class="col-md-4 col-sm-6"><div class="epc-erp-home-card">'
        '<h3><i class="fa fa-money"></i> Cash and bank management</h3>'
        '<p>Workspace area banking</p><a href="/ERP/">Open</a></div></div>'
    )
    return tabs * len(sample.encode("utf-8")) + areas * len(area_card.encode("utf-8"))


def estimate_file_bytes(key: str, path: Path, counts: dict[str, int]) -> dict:
    raw = path.read_text(encoding="utf-8")
    markup = strip_razor(raw)
    base = len(markup.encode("utf-8"))
    loops = count_foreach_loops(markup)
    loop_bonus = loops * 180
    extra = 0
    if key == "storefront_home_depth":
        extra = estimate_featured_strip_bytes(raw) + estimate_static_arrays(raw)
    if key == "erp_login":
        extra = estimate_erp_tabs_bytes(counts) + estimate_static_arrays(raw)
    if key == "bos_login":
        extra = estimate_static_arrays(raw)
    estimated = base + loop_bonus + extra
    markers = MARKERS.get(key, [])
    missing = [m for m in markers if m not in raw]
    return {
        "path": str(path.relative_to(ROOT)),
        "markupBytes": base,
        "foreachLoops": loops,
        "expansionBonusBytes": loop_bonus + extra,
        "estimatedSsrMarkupBytes": estimated,
        "markersExpected": markers,
        "markersMissing": missing,
        "markersOk": len(missing) == 0,
    }


def estimate_storefront_app_total(counts: dict[str, int]) -> int:
    parts = ["storefront_home_depth", "storefront_app", "piston_banner"]
    total = 0
    for p in parts:
        est = estimate_file_bytes(p, SOURCES[p], counts)
        total += est["estimatedSsrMarkupBytes"]
    # Chrome shell + module directory scaffold (offline constant from prior captures)
    total += 14000
    return total


def main() -> int:
    counts = parse_catalog_counts()
    files: dict[str, dict] = {}
    for key, path in SOURCES.items():
        if not path.is_file():
            files[key] = {"error": f"missing {path}"}
            continue
        files[key] = estimate_file_bytes(key, path, counts)

    storefront_est = estimate_storefront_app_total(counts)
    bos_est = files.get("bos_login", {}).get("estimatedSsrMarkupBytes", 0)
    erp_est = files.get("erp_login", {}).get("estimatedSsrMarkupBytes", 0)

    floors = {
        "storefront_app": {
            "floorBytes": FLOORS["storefront_app"],
            "estimatedCombinedBytes": storefront_est,
            "meetsFloor": storefront_est >= FLOORS["storefront_app"],
            "note": "Combined storefront/app chrome + piston + home depth offline estimate",
        },
        "bos_login": {
            "floorBytes": FLOORS["bos_login"],
            "estimatedMarkupBytes": bos_est,
            "meetsFloor": bos_est >= FLOORS["bos_login"],
            "markerClass": "epc-asp-login-bos",
            "markerOk": files.get("bos_login", {}).get("markersOk", False),
        },
        "erp_login": {
            "floorBytes": FLOORS["erp_login"],
            "estimatedMarkupBytes": erp_est,
            "meetsFloor": erp_est >= FLOORS["erp_login"],
            "markerClass": "epc-asp-login-erp",
            "markerOk": files.get("erp_login", {}).get("markersOk", False),
        },
    }

    report = {
        "role": "presentation-scaffold-bytes-estimate",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "catalogCounts": counts,
        "floors": floors,
        "files": files,
        "note": (
            "Offline razor-source estimate for scaffold depth — not a live body-byte probe. "
            "php-vs-aspnet-recheck remains soft-fail until redeploy + live compare. "
            "Never invent cutoverAllowed=true or status=pass."
        ),
    }

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    print(json.dumps({"ok": True, "out": str(OUT.relative_to(ROOT)), "floors": floors}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
