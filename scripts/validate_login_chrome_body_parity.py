#!/usr/bin/env python3
"""Offline gate: login Blazor pages wire PHP body classes + asset markers."""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PAGES = ROOT / "aspnet/src/EcomAE.Platform/Components/Pages"
FLOOR = ROOT / "docs/migration/evidence/presentation/login-chrome-body-parity-floor.json"

CHECKS = [
    ("CpLoginApp.razor", ["PhpChromeBodyClass", "LoginChrome=\"true\"", "epc-cp-login-hero", "epcCpParticles", "authentication"]),
    ("ErpLoginApp.razor", ["PhpChromeBodyClass", "epc-erp-portal-wrap", "epc-erp-bos-hero", "erpPortalParticles", "epc-asp-login-erp"]),
    ("BosLoginApp.razor", ["PhpChromeBodyClass", "epc-asp-login-bos", "bos-login", "bosParticles"]),
    ("StorefrontLoginApp.razor", ["PhpChromeBodyClass", "PhpStorefrontDesktopChrome", "login_form--page", "IncludeStorefrontAnalytics"]),
]

ASSET_LOCKS = [
    ("LegacyPresentationAssets.cs", ["LoginBodyClassFor", "ErpLoginStylesheets", "epc_erp_portal_inline_css_serve"]),
    ("PhpChromeBodyClass.razor", ["LoginBodyClassFor"]),
    ("epc_erp_portal_inline_css_serve.php", None),
]


def main() -> int:
    errors: list[str] = []
    for name, needles in CHECKS:
        path = PAGES / name
        if not path.is_file():
            errors.append(f"missing {path.relative_to(ROOT)}")
            continue
        text = path.read_text(encoding="utf-8")
        for needle in needles:
            if needle not in text:
                errors.append(f"{name}: missing {needle}")
        # Exactly one body-class bridge — duplicates re-assign document.body.className.
        body_class_hits = text.count("<PhpChromeBodyClass")
        if body_class_hits != 1:
            errors.append(
                f"{name}: expected exactly 1 <PhpChromeBodyClass>, found {body_class_hits}"
            )

    for rel, needles in ASSET_LOCKS:
        path = ROOT / rel if rel.endswith(".php") else ROOT / "aspnet/src/EcomAE.Platform" / (
            "Presentation/LegacyPresentationAssets.cs" if "Legacy" in rel else f"Components/Shared/{rel}"
        )
        if rel.endswith(".php"):
            path = ROOT / "content/shop/finance" / rel
        if not path.is_file():
            errors.append(f"missing {path.relative_to(ROOT)}")
            continue
        if needles:
            text = path.read_text(encoding="utf-8")
            for needle in needles:
                if needle not in text:
                    errors.append(f"{rel}: missing {needle}")

    floor = json.loads(FLOOR.read_text(encoding="utf-8"))
    floor["offlineOk"] = len(errors) == 0
    floor["errors"] = errors[:40]
    FLOOR.write_text(json.dumps(floor, indent=2) + "\n", encoding="utf-8")

    out = {"ok": len(errors) == 0, "errors": errors, "floor": str(FLOOR.relative_to(ROOT))}
    print(json.dumps(out, indent=2))
    if errors:
        for e in errors:
            print(e)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
