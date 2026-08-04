#!/usr/bin/env python3
"""Floor: product Blazor heroes/CTAs must use PHP surface color tokens."""
from __future__ import annotations
import json, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PAGES = ROOT / "aspnet/src/EcomAE.Platform/Components/Pages"
SKIP = {
    "MigrationCompareConsole.razor",
    "ZeroPhpConsole.razor",
    "ErpOnPremisesApp.razor",
    "MarketingPreviewApp.razor",
    "StorefrontPreviewApp.razor",  # multi-tile PHP home promo cards
}

ALLOWED = {
    "cp": re.compile(
        r"linear-gradient\(135deg,#0c4a6e 0%,(#2563eb 45%,#0ea5e9|#075985 55%,#0ea5e9) 100%\)",
        re.I,
    ),
    "erp": re.compile(
        r"linear-gradient\((135deg,#0c4a6e 0%,#1d4ed8 45%,#2563eb 100%|90deg,#0c4a6e,#0369a1|90deg,#1e3a8a,#2563eb|90deg,#1d4ed8,#0ea5e9|90deg,#2563eb,#38bdf8)\)",
        re.I,
    ),
    "bos": re.compile(r"linear-gradient\(135deg,#000000 0%,#0c4a6e 55%,#0ea5e9 100%\)", re.I),
    "storefront": re.compile(
        r"linear-gradient\(135deg,#111827 0%,#7f1d1d 50%,#dc2626 100%\)", re.I
    ),
}

FORBIDDEN_HERO = re.compile(
    r"linear-gradient\([^)]*(#312e81|#6366f1|#7c3aed|#a21caf|#3b0764|#14532d|#65a30d|"
    r"#9a3412|#ea580c|#5b21b6|#1e1b4b|#052e16|#431407)[^)]*\)",
    re.I,
)

hits = []
for path in sorted(PAGES.glob("*App.razor")):
    if path.name in SKIP:
        continue
    if path.name.startswith("Marketing"):
        continue
    surface = (
        "cp"
        if path.name.startswith("Cp")
        else "erp"
        if path.name.startswith("Erp")
        else "bos"
        if path.name.startswith("Bos")
        else "storefront"
        if path.name.startswith("Storefront")
        else None
    )
    if surface is None:
        continue
    text = path.read_text(encoding="utf-8")
    for m in re.finditer(r"linear-gradient\([^)]+\)", text):
        g = m.group(0)
        if FORBIDDEN_HERO.search(g):
            hits.append(f"{path.name}: forbidden palette {g[:80]}")
            continue
        if "linear-gradient(180deg" in g or "radial-gradient" in text[max(0, m.start() - 40) : m.start()]:
            continue
        # login page body gradients use radial + 180deg — allow
        if "180deg" in g:
            continue
        if not ALLOWED[surface].search(g):
            # allow command-centre / login multi-stop only if blue family
            if surface in ("cp", "erp") and (
                ("#0c4a6e" in g and ("#0ea5e9" in g or "#2563eb" in g))
                or ("#0ea5e9" in g and "#2563eb" in g)
                or ("#b91c1c" in g and "#dc2626" in g)  # CP topbar CTA red
            ):
                continue
            hits.append(f"{path.name}: non-canonical {g[:100]}")

out = {
    "role": "php-color-scheme-floor",
    "cutoverAllowed": False,
    "readyForPhpRemoval": False,
    "aspNetInteractiveComplete": 0,
    "forbiddenHits": len(hits),
    "hits": hits[:80],
    "tokens": {
        "cp": {"body": "#f0f9ff", "primary": "#2563eb", "accent": "#0ea5e9", "topnav": "white/#dc2626"},
        "erp": {"body": "#f0f9ff", "primary": "#2563eb"},
        "bos": {"body": "#f0f2f5", "primary": "#0ea5e9", "topnav": "#000000"},
        "storefront": {"primary": "#dc2626", "modex": "#ff8040"},
    },
}
out_path = ROOT / "docs/migration/evidence/presentation/php-color-scheme-floor.json"
out_path.parent.mkdir(parents=True, exist_ok=True)
out_path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"ok": len(hits) == 0, "hits": len(hits), "out": str(out_path)}, indent=2))
if hits:
    for h in hits[:40]:
        print(h)
    sys.exit(1)
