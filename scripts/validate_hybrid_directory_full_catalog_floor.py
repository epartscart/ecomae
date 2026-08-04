#!/usr/bin/env python3
"""Lock hybrid Blazor shells to the full PHP module catalog directories.

Never invents RELEASE_OWNER_APPROVAL.md. cutoverAllowed stays false.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

FLOORS = {
    "erpCategories": 9,
    "erpAreas": 35,
    "erpTabs": 154,
    "bosSections": 11,
    "bosModules": 99,
    "cpBrochureFeatures": 405,
    "storefrontSurfaces": 12,
}
MIN_TOTAL = sum(FLOORS.values())

SHELL_REQUIREMENTS = {
    "aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor": [
        "PhpModuleCatalog.CpBrochureFeatures",
        "PhpHybridModuleDirectory",
    ],
    "aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor": [
        "PhpModuleCatalog.ErpCategories",
        "PhpModuleCatalog.ErpAreas",
        "PhpModuleCatalog.ErpTabs",
        "PhpHybridModuleDirectory",
    ],
    "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetApp.razor": [
        "PhpModuleCatalog.BosSections",
        "PhpModuleCatalog.BosModules",
        "PhpHybridModuleDirectory",
    ],
    "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPreviewApp.razor": [
        "PhpModuleCatalog.StorefrontSurfaces",
        "PhpHybridModuleDirectory",
    ],
}


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--catalog",
        type=Path,
        default=ROOT
        / "aspnet/src/EcomAE.Platform/Presentation/Generated/php_module_catalog.json",
    )
    ap.add_argument(
        "--summary-cs",
        type=Path,
        default=ROOT / "aspnet/src/EcomAE.Platform/Presentation/PhpModuleCatalog.cs",
    )
    ap.add_argument(
        "--evidence-out",
        type=Path,
        default=ROOT
        / "docs/migration/evidence/hybrid-ui-dual-samples/hybrid-directory-full-catalog-floor.json",
    )
    args = ap.parse_args()
    errors: list[str] = []

    catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
    counts = catalog.get("counts") if isinstance(catalog.get("counts"), dict) else {}
    for key, floor in FLOORS.items():
        try:
            value = int(counts.get(key))
        except (TypeError, ValueError):
            errors.append(f"counts.{key} missing")
            continue
        if value < floor:
            errors.append(f"counts.{key}={value} < {floor}")

    total = sum(int(counts.get(k) or 0) for k in FLOORS)
    if total < MIN_TOTAL:
        errors.append(f"totalTracked={total} < {MIN_TOTAL}")

    summary_cs = args.summary_cs.read_text(encoding="utf-8")
    for needle in (
        "directoryCoverage",
        "ErpCategories+ErpAreas+ErpTabs",
        "BosSections+BosModules",
        "fullCatalogFloor",
        "omittedKinds",
    ):
        if needle not in summary_cs:
            errors.append(f"PhpModuleCatalog.BuildSummary missing {needle!r}")

    shell_ok: dict[str, bool] = {}
    for rel, needles in SHELL_REQUIREMENTS.items():
        path = ROOT / rel
        if not path.is_file():
            errors.append(f"missing shell {rel}")
            shell_ok[rel] = False
            continue
        text = path.read_text(encoding="utf-8")
        missing = [n for n in needles if n not in text]
        shell_ok[rel] = not missing
        if missing:
            errors.append(f"{rel} missing {missing}")

    out = {
        "role": "hybrid-directory-full-catalog-floor",
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "totalTracked": total,
        "floors": FLOORS,
        "counts": {k: int(counts.get(k) or 0) for k in FLOORS},
        "shellRequirements": {
            rel: {"required": needles, "ok": shell_ok.get(rel, False)}
            for rel, needles in SHELL_REQUIREMENTS.items()
        },
        "ok": not errors,
        "errors": errors,
        "note": (
            "Hybrid Blazor shells must expose the full PHP catalog directories "
            "(CP features, ERP categories/areas/tabs, BOS modules, storefront surfaces). "
            "Never invents RELEASE_OWNER_APPROVAL.md."
        ),
    }
    args.evidence_out.parent.mkdir(parents=True, exist_ok=True)
    args.evidence_out.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")

    if errors:
        print("FAIL: hybrid directory full catalog floor", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: totalTracked={total} shells={len(SHELL_REQUIREMENTS)} "
        f"cutoverAllowed=false aspNetInteractiveComplete=0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
