#!/usr/bin/env python3
"""Compare ASP.NET hybrid UI samples / PHP authoritative inventory.

Does NOT authorize PHP removal or tenant chrome cutover.
Always emits cutoverAllowed=false.
Never invents RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

SKIP_NAMES = frozenset({"README.md", "compare-result.json"})
REQUIRED_AUTH = frozenset({"admin", "customer"})
REQUIRED_SURFACES = frozenset({"cp", "erp", "bos", "storefront"})


def evaluate(doc: dict, path: Path, *, contract_only: bool) -> dict:
    role = str(doc.get("role") or "")
    errors: list[str] = []
    warnings: list[str] = []

    if doc.get("readyForPhpRemoval") is True or doc.get("cutoverAllowed") is True:
        errors.append(f"{path.name}: samples must not claim readyForPhpRemoval/cutoverAllowed")

    if role == "php-hybrid-authoritative-inventory":
        if doc.get("phpAuthoritative") is not True:
            errors.append(f"{path.name}: php inventory must set phpAuthoritative=true")
        if doc.get("tenantChromePhp") is not True:
            errors.append(f"{path.name}: tenantChromePhp must be true")
        if doc.get("wwwPreviewOnly") is not True:
            errors.append(f"{path.name}: wwwPreviewOnly must be true")
        targets = doc.get("hybridUiTargets") or []
        if not isinstance(targets, list) or len(targets) < 1:
            errors.append(f"{path.name}: hybridUiTargets required")
        return {
            "file": path.name,
            "role": role,
            "ok": not errors,
            "errors": errors,
            "warnings": warnings,
        }

    if role != "aspnet-hybrid-ui-sample":
        errors.append(f"{path.name}: unknown role {role!r}")
        return {
            "file": path.name,
            "role": role,
            "ok": False,
            "errors": errors,
            "warnings": warnings,
        }

    if doc.get("phpAuthoritative") is not True:
        errors.append(f"{path.name}: phpAuthoritative must be true")
    if doc.get("wwwPreviewOnly") is not True:
        errors.append(f"{path.name}: wwwPreviewOnly must be true")
    if doc.get("tenantChromePhp") is not True:
        errors.append(f"{path.name}: tenantChromePhp must be true")

    app_route = str(doc.get("appRoute") or "")
    if not app_route.startswith(("/cp/", "/erp/", "/bos/", "/storefront/")):
        errors.append(f"{path.name}: appRoute must be a hybrid surface path, got {app_route!r}")
    if app_route not in {"/cp/orders"} and not app_route.endswith("-app"):
        errors.append(f"{path.name}: appRoute must end with -app (or be /cp/orders)")

    marker = str(doc.get("blazorMarker") or "")
    if not marker:
        errors.append(f"{path.name}: blazorMarker required")

    php_path = str(doc.get("phpAuthoritativePath") or "")
    if not php_path:
        errors.append(f"{path.name}: phpAuthoritativePath required")

    chrome = str(doc.get("chromeShell") or "")
    if not chrome.startswith("Php") or not chrome.endswith("DesktopChrome"):
        errors.append(f"{path.name}: chromeShell must be a Php*DesktopChrome layout")

    auth = str(doc.get("authKind") or "")
    if auth not in REQUIRED_AUTH:
        errors.append(f"{path.name}: authKind must be admin|customer")

    surface = str(doc.get("surface") or "")
    if surface not in REQUIRED_SURFACES:
        errors.append(f"{path.name}: surface must be cp|erp|bos|storefront")

    status = doc.get("httpStatus")
    markers_found = doc.get("markersFound") if isinstance(doc.get("markersFound"), list) else []

    if contract_only or status is None:
        # Stubs are the CI floor — do not require live HTML markers.
        if status is not None and status not in (200, 302, 401, 403):
            warnings.append(f"{path.name}: unusual stub httpStatus {status!r}")
    else:
        if status != 200:
            errors.append(f"{path.name}: live sample httpStatus must be 200, got {status!r}")
        if marker and marker not in markers_found:
            errors.append(f"{path.name}: live sample missing blazorMarker {marker!r} in markersFound")
        if not doc.get("phpDeeplinkFound"):
            warnings.append(f"{path.name}: phpDeeplinkFound=false (PHP deeplink text not seen in HTML)")

    return {
        "file": path.name,
        "role": role,
        "stem": doc.get("stem"),
        "appRoute": app_route,
        "httpStatus": status,
        "ok": not errors,
        "errors": errors,
        "warnings": warnings,
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--samples-dir",
        type=Path,
        default=Path("docs/migration/evidence/hybrid-ui-dual-samples"),
    )
    ap.add_argument("--out", type=Path, default=None)
    ap.add_argument(
        "--contract-only",
        action="store_true",
        help="Accept stubs only; still refuse cutover claims.",
    )
    args = ap.parse_args()

    samples_dir: Path = args.samples_dir
    if not samples_dir.is_dir():
        print(f"FAIL: samples dir missing: {samples_dir}", file=sys.stderr)
        return 2

    # Default to contract-only when every sample has null httpStatus (repo stubs).
    results = []
    docs: list[tuple[Path, dict]] = []
    for path in sorted(samples_dir.glob("*.json")):
        if path.name in SKIP_NAMES or path.name.startswith("compare-"):
            continue
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            results.append(
                {
                    "file": path.name,
                    "ok": False,
                    "errors": [f"invalid json: {ex}"],
                    "warnings": [],
                }
            )
            continue
        if not isinstance(doc, dict):
            results.append(
                {
                    "file": path.name,
                    "ok": False,
                    "errors": ["sample root must be object"],
                    "warnings": [],
                }
            )
            continue
        docs.append((path, doc))

    hybrid_docs = [d for _, d in docs if d.get("role") == "aspnet-hybrid-ui-sample"]
    all_stubs = bool(hybrid_docs) and all(d.get("httpStatus") is None for d in hybrid_docs)
    contract_only = bool(args.contract_only) or all_stubs

    for path, doc in docs:
        results.append(evaluate(doc, path, contract_only=contract_only))

    ui_ok = [r for r in results if r.get("role") == "aspnet-hybrid-ui-sample" and r.get("ok")]
    inventory_ok = any(r.get("role") == "php-hybrid-authoritative-inventory" and r.get("ok") for r in results)
    failed = [r for r in results if not r.get("ok")]

    out = {
        "pairsChecked": len(results),
        "hybridUiSamplesOk": len(ui_ok),
        "phpInventoryOk": inventory_ok,
        "failed": len(failed),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "contractOnly": contract_only,
        "note": (
            "Hybrid UI dual-sample harness. www exact-route Blazor previews only; "
            "live tenant/product chrome and writes remain PHP-authoritative."
        ),
        "results": results,
    }

    text = json.dumps(out, indent=2) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")
    print(text, end="")

    if failed or not ui_ok or not inventory_ok:
        print(
            f"FAIL: failed={len(failed)} hybridUiOk={len(ui_ok)} inventoryOk={inventory_ok}",
            file=sys.stderr,
        )
        return 1
    print(
        f"PASS: hybridUiSamplesOk={len(ui_ok)} phpInventoryOk=true cutoverAllowed=false",
        file=sys.stderr,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
