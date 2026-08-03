#!/usr/bin/env python3
"""Compare ASP.NET catalog miss samples / PHP fill inventory (Batch 5).

Does NOT authorize PHP removal. Always emits cutoverAllowed=false.
Never invents RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

MISS_CODES = frozenset({"cache_miss", "vin_cache_miss"})
SKIP_NAMES = frozenset(
    {
        "README.md",
        "compare-result.json",
        # Separate dry-run evidence family (not a dual-sample pair).
        "miss-fill-dry-run-report.json",
    }
)


def evaluate(doc: dict, path: Path) -> dict:
    role = str(doc.get("role") or "")
    errors: list[str] = []
    warnings: list[str] = []

    if doc.get("readyForPhpRemoval") is True or doc.get("cutoverAllowed") is True:
        errors.append(f"{path.name}: samples must not claim readyForPhpRemoval/cutoverAllowed")

    if role == "php-umapi-fill-inventory":
        if doc.get("phpAuthoritative") is not True:
            errors.append(f"{path.name}: php inventory must set phpAuthoritative=true")
        fills = doc.get("liveFillPaths") or []
        if not fills:
            errors.append(f"{path.name}: liveFillPaths required")
        always = {str(a) for a in (doc.get("alwaysLiveActions") or [])}
        if "articles" not in always or "engine" not in always:
            warnings.append(f"{path.name}: expected alwaysLiveActions to include articles+engine")
        return {
            "file": path.name,
            "role": role,
            "ok": not errors,
            "errors": errors,
            "warnings": warnings,
        }

    if role != "aspnet-catalog-miss-sample":
        errors.append(f"{path.name}: unknown role {role!r}")
        return {
            "file": path.name,
            "role": role,
            "ok": False,
            "errors": errors,
            "warnings": warnings,
        }

    status = doc.get("httpStatus")
    if status not in (401, 404):
        errors.append(f"{path.name}: httpStatus must be 401 or 404 for miss samples, got {status!r}")

    if doc.get("ok") is True:
        errors.append(f"{path.name}: miss sample must have ok=false")

    err = doc.get("error") if isinstance(doc.get("error"), dict) else {}
    code = str(err.get("code") or "")
    if status == 404 and code not in MISS_CODES:
        errors.append(f"{path.name}: 404 miss requires error.code in {sorted(MISS_CODES)}, got {code!r}")
    if status == 401 and code and code not in {"unauthorized", "auth_required", "missing_api_key"}:
        warnings.append(f"{path.name}: unexpected 401 code {code!r}")

    if doc.get("phpAuthoritative") is not True:
        errors.append(f"{path.name}: phpAuthoritative must be true")

    route = str(doc.get("route") or "")
    if not route.startswith("/api/v1/catalog/"):
        errors.append(f"{path.name}: route must be under /api/v1/catalog/")

    return {
        "file": path.name,
        "role": role,
        "action": doc.get("action"),
        "httpStatus": status,
        "errorCode": code or None,
        "ok": not errors,
        "errors": errors,
        "warnings": warnings,
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--samples-dir",
        type=Path,
        default=Path("docs/migration/evidence/catalog-miss-umapi"),
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

    results = []
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
        results.append(evaluate(doc, path))

    miss_ok = [r for r in results if r.get("role") == "aspnet-catalog-miss-sample" and r.get("ok")]
    inventory_ok = any(r.get("role") == "php-umapi-fill-inventory" and r.get("ok") for r in results)
    failed = [r for r in results if not r.get("ok")]

    out = {
        "pairsChecked": len(results),
        "missSamplesOk": len(miss_ok),
        "phpInventoryOk": inventory_ok,
        "failed": len(failed),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "contractOnly": bool(args.contract_only),
        "note": "Batch 5 miss harness only. Live UMAPI fill remains PHP-authoritative.",
        "results": results,
    }

    text = json.dumps(out, indent=2) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")
    print(text, end="")

    if failed or not miss_ok or not inventory_ok:
        print(
            f"FAIL: failed={len(failed)} missOk={len(miss_ok)} inventoryOk={inventory_ok}",
            file=sys.stderr,
        )
        return 1
    print(
        f"PASS: missSamplesOk={len(miss_ok)} phpInventoryOk=true cutoverAllowed=false",
        file=sys.stderr,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
