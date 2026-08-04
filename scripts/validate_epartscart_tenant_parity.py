#!/usr/bin/env python3
"""Fail-closed epartscart.com frontend + CP parity coverage gate.

Ensures every storefront surface and every CP digest-contract menu has the
required floors/samples/hybrid stubs for same-to-same PHP parity work.
Never invents cutoverAllowed=true / readyForPhpRemoval=true / RELEASE_OWNER_APPROVAL.md.
aspNetInteractiveComplete must stay 0 until live dual-samples + human approval.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

TENANT = "epartscart.com"
TENANT_BASES = (
    "https://epartscart.com",
    "https://www.epartscart.com",
)

# Digest-backed storefront surfaces that must have floor + sample + hybrid.
STOREFRONT_DIGEST_SURFACES = (
    {
        "id": "storefront-search",
        "floor": "storefront-search-item-field-floor.json",
        "sample": "storefront-search.json",
        "hybrid": "aspnet-sf-search-hybrid-ui.json",
        "phpPath": "https://www.epartscart.com/en/shop/part_search",
    },
    {
        "id": "storefront-cart",
        "floor": "storefront-cart-item-field-floor.json",
        "sample": "storefront-cart.json",
        "hybrid": "aspnet-sf-cart-hybrid-ui.json",
        "phpPath": "https://www.epartscart.com/en/shop/cart",
    },
    {
        "id": "storefront-checkout",
        "floor": "storefront-checkout-item-field-floor.json",
        "sample": "storefront-checkout.json",
        "hybrid": "aspnet-sf-checkout-hybrid-ui.json",
        "phpPath": "https://www.epartscart.com/en/shop/checkout/how_get",
    },
    {
        "id": "storefront-orders",
        "floor": "storefront-orders-item-field-floor.json",
        "sample": "storefront-orders.json",
        "hybrid": "aspnet-sf-orders-hybrid-ui.json",
        "phpPath": "https://www.epartscart.com/en/shop/orders",
    },
    {
        "id": "storefront-garage",
        "floor": "storefront-garage-item-field-floor.json",
        "sample": "storefront-garage.json",
        "hybrid": "aspnet-sf-garage-hybrid-ui.json",
        "phpPath": "https://www.epartscart.com/en/garage/login",
    },
    {
        "id": "storefront-profile",
        "floor": "storefront-profile-object-field-floor.json",
        "sample": "storefront-profile.json",
        "hybrid": "aspnet-sf-profile-hybrid-ui.json",
        "phpPath": "https://www.epartscart.com/en/users/profile",
    },
    {
        "id": "storefront-account-summary",
        "floor": "storefront-account-summary-item-field-floor.json",
        "sample": "storefront-account-summary.json",
        "hybrid": "aspnet-sf-account-summary-hybrid-ui.json",
        "phpPath": "https://www.epartscart.com/en/users/",
    },
)

# PHP chrome pages on epartscart (no dedicated digest yet) — must stay PHP-primary.
STOREFRONT_PHP_CHROME_LOCKS = (
    {"id": "storefront-home", "phpPath": "https://epartscart.com/", "probePath": "/"},
    {"id": "storefront-catalog", "phpPath": "https://www.epartscart.com/en/", "probePath": "/en/"},
    {"id": "storefront-vin_search", "phpPath": "https://epartscart.com/shop/part_search", "probePath": "/shop/part_search"},
    {"id": "storefront-returns", "phpPath": "https://epartscart.com/", "probePath": "/"},
    {"id": "storefront-payments", "phpPath": "https://epartscart.com/", "probePath": "/"},
    {"id": "storefront-support", "phpPath": "https://epartscart.com/", "probePath": "/"},
    {"id": "storefront-quotes", "phpPath": "https://www.epartscart.com/en/shop/quotes", "probePath": "/en/shop/quotes"},
    {"id": "storefront-wishlist", "phpPath": "https://www.epartscart.com/en/shop/zakladki", "probePath": "/en/shop/zakladki"},
    {"id": "storefront-compare", "phpPath": "https://www.epartscart.com/en/shop/sravneniya", "probePath": "/en/shop/sravneniya"},
    {"id": "storefront-bulk-upload", "phpPath": "https://www.epartscart.com/en/shop/bulk-upload", "probePath": "/en/shop/bulk-upload"},
    {"id": "storefront-balance", "phpPath": "https://www.epartscart.com/en/shop/balans", "probePath": "/en/shop/balans"},
)

LIVE_PRODUCT_PROBES = (
    "/",
    "/en/",
    "/CP/",
    "/cp/control",
    # Locale-prefixed shop/account paths are the live epartscart.com product chrome.
    "/en/shop/part_search",
    "/en/shop/cart",
    "/en/shop/checkout",
    "/en/shop/checkout/how_get",
    "/en/shop/orders",
    "/en/shop/quotes",
    "/en/shop/zakladki",
    "/en/shop/sravneniya",
    "/en/shop/bulk-upload",
    "/en/shop/balans",
    "/en/garage/login",
    "/en/users/",
    "/en/users/profile",
)

FORBIDDEN_ASPNET_PROBES = (
    "/cp/app",
    "/erp/app",
    "/bos/app",
    "/storefront/app",
    "/storefront/search-app",
    "/storefront/cart-app",
    "/health",
)


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def assert_cutover_false(doc: dict, rel: str, errors: list[str]) -> None:
    if doc.get("cutoverAllowed") is True:
        errors.append(f"{rel}: cutoverAllowed must be false")
    if doc.get("readyForPhpRemoval") is True or doc.get("readyToRemovePhp") is True:
        errors.append(f"{rel}: readyForPhpRemoval must be false")
    if doc.get("aspNetInteractiveComplete") not in (0, None, False):
        errors.append(f"{rel}: aspNetInteractiveComplete must stay 0")


def existing_floor_stems(sp: Path) -> set[str]:
    stems: set[str] = set()
    for p in sp.glob("*-item-field-floor.json"):
        stems.add(p.name.replace("-item-field-floor.json", ""))
    for p in sp.glob("*-object-field-floor.json"):
        stems.add(p.name.replace("-object-field-floor.json", ""))
    for p in sp.glob("*-sample-tenants-floor.json"):
        stems.add(p.name.replace("-sample-tenants-floor.json", ""))
    list_floor = sp / "list-digest-item-field-floor.json"
    if list_floor.is_file():
        stems.update(load_json(list_floor).get("stems") or [])
    return stems


def probe_url(url: str, timeout: float = 15.0) -> dict:
    req = urllib.request.Request(
        url,
        headers={"User-Agent": "ecomae-epartscart-tenant-parity/1.0"},
        method="GET",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            body = resp.read()
            final = resp.geturl()
            ctype = resp.headers.get("Content-Type", "")
            status = resp.status
    except urllib.error.HTTPError as e:
        body = e.read() if e.fp else b""
        final = url
        ctype = e.headers.get("Content-Type", "") if e.headers else ""
        status = e.code
    except Exception as exc:  # noqa: BLE001
        return {
            "url": url,
            "ok": False,
            "error": str(exc),
            "stack": "unreachable",
            "result": "fail",
        }

    text = body.decode("utf-8", errors="ignore")
    low = text.lower()
    blazor = "blazor" in low or "_framework/blazor" in low
    asp_markers = blazor or "asp-net" in low or "ecomae.platform" in low
    php_markers = (
        "epartscart" in low
        or "eparts cart" in low
        or "/epc-static.php" in low
        or "templates/nero" in low
        or "bootstrap_admin" in low
        or "pack.php" in low
    )
    if asp_markers and not php_markers:
        stack = "aspnet"
    elif php_markers:
        stack = "php-html"
    elif status in (301, 302, 303, 307, 308) and not body:
        stack = "redirect"
    elif status in (404, 403):
        stack = "absent"
    else:
        stack = "other-html" if "text/html" in ctype else "other"

    title_m = re.search(r"<title[^>]*>(.*?)</title>", text, flags=re.I | re.S)
    title = title_m.group(1).strip()[:120] if title_m else ""
    return {
        "url": url,
        "finalUrl": final,
        "httpStatus": status,
        "bytes": len(body),
        "contentType": ctype,
        "stack": stack,
        "aspnetMarkers": asp_markers,
        "phpMarkers": php_markers,
        "title": title,
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--live", action="store_true", help="Probe live epartscart.com hosts")
    ap.add_argument(
        "--out",
        default="docs/migration/evidence/tenant-safety/epartscart-frontend-cp-parity.json",
    )
    ap.add_argument(
        "--matrix-out",
        default="docs/migration/evidence/tenant-safety/epartscart-coverage-matrix.json",
    )
    args = ap.parse_args()

    root = Path(args.root).resolve()
    evidence = root / "docs/migration/evidence"
    sp = evidence / "surface-parity"
    samples = sp / "samples/migration"
    hybrid = evidence / "hybrid-ui-dual-samples"
    errors: list[str] = []
    warnings: list[str] = []

    # --- Storefront digest surfaces ---
    sf_rows = []
    for surf in STOREFRONT_DIGEST_SURFACES:
        row_errors: list[str] = []
        floor_path = sp / surf["floor"]
        sample_path = samples / surf["sample"]
        hybrid_path = hybrid / surf["hybrid"]
        if not floor_path.is_file():
            row_errors.append(f"missing floor {surf['floor']}")
        else:
            assert_cutover_false(load_json(floor_path), surf["floor"], row_errors)
        if not sample_path.is_file():
            row_errors.append(f"missing sample {surf['sample']}")
        else:
            doc = load_json(sample_path)
            assert_cutover_false(doc, surf["sample"], row_errors)
        if not hybrid_path.is_file():
            row_errors.append(f"missing hybrid {surf['hybrid']}")
        else:
            doc = load_json(hybrid_path)
            assert_cutover_false(doc, surf["hybrid"], row_errors)
            php_path = doc.get("phpAuthoritativePath") or ""
            if "epartscart.com" not in str(php_path):
                row_errors.append(f"{surf['hybrid']}: phpAuthoritativePath must point at epartscart.com")
            if doc.get("tenantChromePhp") is not True and doc.get("phpAuthoritative") is not True:
                row_errors.append(f"{surf['hybrid']}: must declare tenantChromePhp/phpAuthoritative")
        errors.extend(row_errors)
        sf_rows.append(
            {
                "id": surf["id"],
                "kind": "digest-contract",
                "phpPath": surf["phpPath"],
                "floor": surf["floor"],
                "sample": surf["sample"],
                "hybrid": surf["hybrid"],
                "status": "fail" if row_errors else "contract-complete",
                "errors": row_errors,
            }
        )

    # --- PHP chrome locks (presentation must remain PHP on tenant) ---
    chrome_rows = []
    for surf in STOREFRONT_PHP_CHROME_LOCKS:
        chrome_rows.append(
            {
                "id": surf["id"],
                "kind": "php-chrome-lock",
                "phpPath": surf["phpPath"],
                "probePath": surf["probePath"],
                "status": "php-authoritative-required",
                "note": "No ASP.NET hybrid on epartscart.com — live PHP chrome is the parity source.",
            }
        )

    # --- CP menu / field floors ---
    menu_board_path = sp / "menu-field-parity-built-vs-pending.json"
    if not menu_board_path.is_file():
        errors.append("missing menu-field-parity-built-vs-pending.json")
        menu_board = {}
    else:
        menu_board = load_json(menu_board_path)
        assert_cutover_false(menu_board, menu_board_path.name, errors)

    menus = menu_board.get("menus") or {}
    fields = menu_board.get("fieldFloors") or {}
    if int(menus.get("digestContract") or 0) < 725:
        errors.append(f"CP menus digestContract={menus.get('digestContract')} < 725")
    if int(fields.get("coveredContractStems") or 0) < int(fields.get("totalContractStems") or 0):
        errors.append(
            f"field floors incomplete: {fields.get('coveredContractStems')}/{fields.get('totalContractStems')}"
        )
    if fields.get("missingContractStems"):
        errors.append(f"missingContractStems={fields.get('missingContractStems')}")

    floor_stems = existing_floor_stems(sp)
    cp_sample_stems = sorted(
        {
            p.stem.replace("-digest", "")
            for p in samples.glob("cp-*.json")
        }
    )
    cp_missing = [s for s in cp_sample_stems if s not in floor_stems]
    if cp_missing:
        errors.append(f"CP samples without floor/list coverage: {cp_missing[:20]}")

    cp_hybrid = sorted(p.name for p in hybrid.glob("aspnet-cp-*-hybrid-ui.json"))
    if len(cp_hybrid) < 100:
        errors.append(f"CP hybrid stubs too few: {len(cp_hybrid)}")

    # Inventory honesty check
    inv_path = evidence / "module-function-parity/module-function-inventory.json"
    inv = load_json(inv_path) if inv_path.is_file() else {}
    assert_cutover_false(inv, inv_path.name, errors)
    mods = inv.get("modules") or []
    sf_mods = [m for m in mods if m.get("surface") == "storefront"]
    cp_mods = [m for m in mods if m.get("surface") == "cp"]
    php_only_cp = [m["id"] for m in cp_mods if m.get("status") == "php-only"]
    if php_only_cp:
        errors.append(f"unexpected CP php-only modules: {php_only_cp[:10]}")

    # Tenant safety floors
    for rel in (
        "tenant-safety/live-tenant-php-chrome.json",
        "tenant-safety/same-to-same-verify.json",
    ):
        path = evidence / rel
        if not path.is_file():
            errors.append(f"missing {rel}")
            continue
        doc = load_json(path)
        assert_cutover_false(doc, rel, errors)
        if doc.get("status") not in ("pass", "ok", None) and doc.get("failCount", 0) not in (0, None):
            if int(doc.get("failCount") or 0) > 0:
                errors.append(f"{rel}: failCount={doc.get('failCount')}")

    live_probes: list[dict] = []
    if args.live:
        for base in TENANT_BASES:
            for path in LIVE_PRODUCT_PROBES:
                url = base.rstrip("/") + path
                result = probe_url(url)
                # Product chrome must be PHP (or login PHP), never Blazor.
                if result.get("stack") == "aspnet" or result.get("aspnetMarkers"):
                    result["result"] = "fail"
                    errors.append(f"live ASP.NET chrome on tenant: {url}")
                elif result.get("stack") == "unreachable":
                    result["result"] = "fail"
                    errors.append(f"live unreachable: {url} ({result.get('error')})")
                elif result.get("httpStatus") in (200, 301, 302, 303, 401, 403):
                    # 401/403 still OK if not ASP.NET (auth wall)
                    if result.get("aspnetMarkers"):
                        result["result"] = "fail"
                        errors.append(f"live ASP.NET markers: {url}")
                    else:
                        result["result"] = "pass"
                else:
                    result["result"] = "warn"
                    warnings.append(f"live unexpected status {result.get('httpStatus')} for {url}")
                live_probes.append(result)

            for path in FORBIDDEN_ASPNET_PROBES:
                url = base.rstrip("/") + path
                result = probe_url(url)
                # Forbidden routes must NOT serve ASP.NET Blazor on tenant.
                if result.get("stack") == "aspnet" or (
                    result.get("aspnetMarkers") and result.get("httpStatus") == 200
                ):
                    result["result"] = "fail"
                    errors.append(f"forbidden ASP.NET shadow live on tenant: {url}")
                else:
                    result["result"] = "pass"
                result["probeKind"] = "forbidden"
                live_probes.append(result)
    else:
        warnings.append("live probe skipped (pass --live on CloudPanel / network host)")

    # Always warn: interactive + dual-sample pending
    warnings.append("aspNetInteractiveComplete=0 — live writes still PHP on epartscart.com")
    warnings.append("Authenticated dual-sample capture still required before any cutover")
    warnings.append("Never invent RELEASE_OWNER_APPROVAL.md")

    digest_complete = sum(1 for r in sf_rows if r["status"] == "contract-complete")
    matrix = {
        "role": "epartscart-frontend-cp-coverage-matrix",
        "tenant": TENANT,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "generatedAtUnix": int(time.time()),
        "storefront": {
            "digestSurfacesRequired": len(STOREFRONT_DIGEST_SURFACES),
            "digestSurfacesComplete": digest_complete,
            "phpChromeLocks": len(STOREFRONT_PHP_CHROME_LOCKS),
            "surfaces": sf_rows + chrome_rows,
        },
        "cp": {
            "menusTracked": menus.get("totalTracked"),
            "menusDigestContract": menus.get("digestContract"),
            "phpOnlyHoldout": menus.get("phpOnlyHoldout"),
            "holdoutId": menus.get("holdoutId"),
            "fieldFloorsCovered": fields.get("coveredContractStems"),
            "fieldFloorsTotal": fields.get("totalContractStems"),
            "cpSamplesWithFloorCoverage": len(cp_sample_stems) - len(cp_missing),
            "cpSamplesTotal": len(cp_sample_stems),
            "cpHybridStubs": len(cp_hybrid),
            "cpModulesInInventory": len(cp_mods),
            "phpOnlyModules": php_only_cp,
        },
        "moduleInventoryStorefrontCount": len(sf_mods),
        "built": [
            f"Storefront digest surfaces {digest_complete}/{len(STOREFRONT_DIGEST_SURFACES)} contract-complete",
            f"CP menus {menus.get('digestContract')}/{menus.get('totalTracked')} digest-contract",
            f"Field floors {fields.get('coveredContractStems')}/{fields.get('totalContractStems')}",
            f"CP hybrid stubs {len(cp_hybrid)}",
            f"PHP chrome locks declared for {len(STOREFRONT_PHP_CHROME_LOCKS)} epartscart pages",
        ],
        "pendingBeforePhpRemoval": [
            "Live authenticated dual-sample for search/cart/checkout/orders/garage/profile",
            "CP authenticated digest dual-samples for epartscart tenant cookies",
            "aspNetInteractiveComplete remains 0 until write dual-samples promote",
            "Human RELEASE_OWNER_APPROVAL.md (never invent)",
            "Exact-route shadows must not land on epartscart.com until approval",
        ],
        "ok": not errors,
        "errors": errors,
        "warnings": warnings,
        "note": (
            "epartscart.com frontend + CP must match PHP same-to-same. "
            "Contract coverage is gated here; live write cutover stays forbidden."
        ),
    }

    suite = {
        "role": "epartscart-frontend-cp-parity-suite",
        "tenant": TENANT,
        "generatedAtUnix": int(time.time()),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "readyToRemovePhp": False,
        "aspNetInteractiveComplete": 0,
        "ok": not errors,
        "errorCount": len(errors),
        "warningCount": len(warnings),
        "errors": errors,
        "warnings": warnings,
        "liveProbeCount": len(live_probes),
        "liveProbeFails": sum(1 for p in live_probes if p.get("result") == "fail"),
        "liveProbes": live_probes,
        "matrixRef": args.matrix_out,
        "phpRemovalBlockedReason": (
            "epartscart.com remains PHP-primary until live dual-sample + human approval. "
            "Never invent RELEASE_OWNER_APPROVAL.md."
        ),
        "note": "Fail-closed tenant gate for epartscart.com storefront + CP.",
    }

    out_path = root / args.out
    matrix_path = root / args.matrix_out
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(suite, indent=2) + "\n", encoding="utf-8")
    matrix_path.write_text(json.dumps(matrix, indent=2) + "\n", encoding="utf-8")

    print(
        json.dumps(
            {
                "ok": suite["ok"],
                "tenant": TENANT,
                "errors": len(errors),
                "warnings": len(warnings),
                "storefrontDigestComplete": f"{digest_complete}/{len(STOREFRONT_DIGEST_SURFACES)}",
                "cpMenus": f"{menus.get('digestContract')}/{menus.get('totalTracked')}",
                "fieldFloors": f"{fields.get('coveredContractStems')}/{fields.get('totalContractStems')}",
                "liveProbes": len(live_probes),
                "phpRemovalAllowed": False,
                "out": str(out_path),
                "matrix": str(matrix_path),
            },
            indent=2,
        )
    )
    for e in errors[:30]:
        print(f"  ERROR: {e}", file=sys.stderr)
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
