#!/usr/bin/env python3
"""Align Blazor product chrome/app colors to PHP surface tokens.

CP/ERP: blue theme (#0c4a6e / #2563eb / #0ea5e9), body #f0f9ff
BOS: black chrome + cyan #0ea5e9, body #f0f2f5
Storefront (automotive): #dc2626 / #ef4444
Never sets cutover flags.
"""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PAGES = ROOT / "aspnet/src/EcomAE.Platform/Components/Pages"
SKIP = {
    "MigrationCompareConsole.razor",
    "ZeroPhpConsole.razor",
    "ErpOnPremisesApp.razor",
    "MarketingPreviewApp.razor",
}
# Multi-tile promo grids that already mirror live PHP home cards
SKIP_GRADIENT_NORMALIZE = {
    "StorefrontPreviewApp.razor",
}

# Canonical PHP shell gradients (from epc_cp_professional / bos shell / automotive)
HERO = {
    "cp": "linear-gradient(135deg,#0c4a6e 0%,#2563eb 45%,#0ea5e9 100%)",
    "erp": "linear-gradient(135deg,#0c4a6e 0%,#1d4ed8 45%,#2563eb 100%)",
    "bos": "linear-gradient(135deg,#000000 0%,#0c4a6e 55%,#0ea5e9 100%)",
    "storefront": "linear-gradient(135deg,#111827 0%,#7f1d1d 50%,#dc2626 100%)",
}

# Primary CTA fill matching PHP shell actions
CTA = {
    "cp": "#2563eb",
    "erp": "#2563eb",
    "bos": "#0ea5e9",
    "storefront": "#dc2626",
}

# Soft chip/badge backgrounds used in KPI cards — keep muted, brand-aligned
CHIP = {
    "cp": "#e0f2fe",
    "erp": "#dbeafe",
    "bos": "#e0f2fe",
    "storefront": "#fee2e2",
}

GRADIENT_RE = re.compile(r"background:linear-gradient\([^)]+\)", re.I)
# Primary action buttons in page-local CSS (hero CTA first link)
CTA_BG_RE = re.compile(
    r"(text-decoration:none;\s*color:#fff;\s*background:)(#[0-9a-fA-F]{3,8})",
    re.I,
)
INLINE_BTN_BG_RE = re.compile(
    r"(display:inline-flex;[^\"']*?background:)(#[0-9a-fA-F]{3,8})(;color:#fff)",
    re.I,
)
# Soft pastel KPI chip backgrounds that invent purple/green/orange
SOFT_CHIP_RE = re.compile(
    r"background:(#c4b5fd|#a78bfa|#86efac|#a3e635|#fdba74|#fbbf24|#fde68a|"
    r"#99f6e4|#5eead4|#67e8f9|#7dd3fc|#bef264|#fef3c7|#ccfbf1|#e7e5e4)",
    re.I,
)


def surface_for(name: str) -> str | None:
    if name.startswith("Cp"):
        return "cp"
    if name.startswith("Erp"):
        return "erp"
    if name.startswith("Bos"):
        return "bos"
    if name.startswith("Storefront"):
        return "storefront"
    return None


def align_page(path: Path) -> bool:
    surface = surface_for(path.name)
    if surface is None or path.name in SKIP:
        return False
    text = path.read_text(encoding="utf-8")
    original = text

    hero = HERO[surface]
    cta = CTA[surface]
    chip = CHIP[surface]

    def repl_grad(m: re.Match[str]) -> str:
        g = m.group(0)
        # Keep marketing-like multi-tile storefront preview accents as-is if not hero
        # Always normalize full background:linear-gradient(...) in page styles
        return f"background:{hero}"

    # Normalize all page-local linear-gradients (heroes + banner strips in apps)
    if path.name not in SKIP_GRADIENT_NORMALIZE:
        text = GRADIENT_RE.sub(repl_grad, text)

    # Restore ERP dashboard banner variety as blue-family shades (still PHP ERP palette)
    if path.name == "ErpBosDashboardApp.razor":
        banners = [
            "linear-gradient(90deg,#ea580c,#f97316)",
            "linear-gradient(90deg,#1e3a8a,#2563eb)",
            "linear-gradient(90deg,#115e59,#0d9488)",
            "linear-gradient(90deg,#1d4ed8,#3b82f6)",
        ]
        erp_banners = [
            "linear-gradient(90deg,#0c4a6e,#0369a1)",
            "linear-gradient(90deg,#1e3a8a,#2563eb)",
            "linear-gradient(90deg,#1d4ed8,#0ea5e9)",
            "linear-gradient(90deg,#2563eb,#38bdf8)",
        ]
        # After normalize they all became HERO — rewrite banner classes specifically
        # Match style="background:HERO" on epc-erp-banner anchors in order
        parts = text.split('class="epc-erp-banner"')
        if len(parts) == 5:
            rebuilt = [parts[0]]
            for i, rest in enumerate(parts[1:]):
                rest2 = re.sub(
                    r'style="background:[^"]+"',
                    f'style="background:{erp_banners[i]}"',
                    rest,
                    count=1,
                )
                rebuilt.append(rest2)
            text = 'class="epc-erp-banner"'.join(rebuilt)

    text = CTA_BG_RE.sub(rf"\g<1>{cta}", text)
    text = INLINE_BTN_BG_RE.sub(rf"\g<1>{cta}\g<3>", text)
    text = SOFT_CHIP_RE.sub(f"background:{chip}", text)

    # Login submit for BOS must use bos-primary
    if path.name == "BosLoginApp.razor":
        text = text.replace(
            ".bos-login .epc-login-submit { width:100%; margin-top:.4rem; padding:.8rem 1rem; border:0; border-radius:.5rem; background:#2563eb;",
            ".bos-login .epc-login-submit { width:100%; margin-top:.4rem; padding:.8rem 1rem; border:0; border-radius:.5rem; background:#0ea5e9;",
        )

    # Command centre: PHP CP blue panel + quick-action tiles in blue/red brand set
    if path.name == "CpCommandCentreApp.razor":
        text = re.sub(
            r"(\.epc-cc-panel \{[^}]*?)background:linear-gradient\([^)]+\)",
            r"\1background:linear-gradient(135deg,#0c4a6e 0%,#075985 55%,#0ea5e9 100%)",
            text,
            count=1,
        )
        text = text.replace(
            'var colors = new[] { "#dc2626", "#0f766e", "#2563eb", "#7c3aed", "#db2777", "#991b1b", "#166534", "#9f1239", "#9a3412" };',
            'var colors = new[] { "#0c4a6e", "#0369a1", "#2563eb", "#0284c7", "#0ea5e9", "#1d4ed8", "#075985", "#dc2626", "#b91c1c" };',
        )

    if text != original:
        path.write_text(text, encoding="utf-8")
        return True
    return False


def main() -> int:
    changed = []
    for path in sorted(PAGES.glob("*App.razor")):
        if align_page(path):
            changed.append(path.name)
    print(f"aligned_pages={len(changed)}")
    for n in changed:
        print(f"  {n}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
