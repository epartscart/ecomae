#!/usr/bin/env python3
"""Second-pass polish after strip_same_to_same_look_gaps.py."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PAGES = ROOT / "aspnet/src/EcomAE.Platform/Components/Pages"

SKIP = {
    "MigrationCompareConsole.razor",
    "ZeroPhpConsole.razor",
    "MarketingPreviewApp.razor",
    "ErpOnPremisesApp.razor",
}

# Incomplete hero leftovers from aggressive strip
BAD_HERO = re.compile(
    r"<p>(?:Read-only[^<]{0,80}|Batch \d+[^<]{0,80}|Manage store operations, catalogue, and partner integrations from one control panel\.|Full [^<]{0,40})</p>"
)

HERO_BY_FILE = {
    "CpOrdersApp.razor": "Open orders, track payment status, and move paid lines to ship.",
    "ErpDashboardSummaryApp.razor": "Cash, receivables, payables, stock, and approval queues for this company.",
    "ErpPurchasesApp.razor": "Purchase documents and supplier intake for this company.",
    "StorefrontLoginApp.razor": "Sign in to your parts account to view orders, garage, and saved carts.",
    "StorefrontCheckoutApp.razor": "Review your cart and continue to delivery and payment.",
    "StorefrontSearchApp.razor": "Search warehouse offers by article and brand.",
    "StorefrontCartApp.razor": "Lines in your cart ready for checkout.",
    "StorefrontPreviewApp.razor": "Storefront surfaces for browsing, account, and checkout.",
    "ErpBosDashboardApp.razor": "ERP module directory for this company workspace.",
    "CpCommandCentreApp.razor": "Control panel shortcuts and module directory.",
    "ErpLoginApp.razor": "Sign in to the company ERP workspace.",
}

SOURCE_KPI = re.compile(
    r'\s*<div class="[^"]*kpi[^"]*"><strong>@_source</strong><span>(?:Digest )?Source</span></div>\s*',
    re.I,
)
SOURCE_KPI2 = re.compile(
    r'\s*<div class="[^"]*"><strong>@_source</strong><span>Digest source</span></div>\s*',
    re.I,
)
SOURCE_EDS = re.compile(
    r'\s*<div class="epc-eds-kpi"><strong>@_s\.Source</strong><span>Digest source</span></div>\s*',
)

SECTION_PHP = [
    (re.compile(r">Executive tiles \(erp_dashboard\.php\)<"), ">Executive tiles<"),
    (re.compile(r">Command center tiles \(epc_erp_cc_kpi_tiles\)<"), ">Command center tiles<"),
    (re.compile(r">Cash / supplier ledger digests<"), ">Cash / supplier ledger<"),
]

CTA_INDENT = re.compile(
    r'(<div class="[^"]*cta[^"]*">)\n(<a href=)',
)


def polish(path: Path) -> bool:
    text = path.read_text(encoding="utf-8")
    original = text
    name = path.name

    if name in HERO_BY_FILE:
        text = re.sub(
            r"(<h1>[^<]*</h1>\s*)<p>[^<]*</p>",
            rf"\1<p>{HERO_BY_FILE[name]}</p>",
            text,
            count=1,
        )

    # Generic: replace truncated "Read-only ... from" heroes
    def fix_p(m: re.Match[str]) -> str:
        inner = m.group(1).strip()
        if inner.endswith(" from") or inner.startswith("Read-only") or "digest" in inner.lower() or "live PHP" in inner or "Wave B" in inner or "same-to-same" in inner or "ASP.NET" in inner:
            # keep file-specific if already set above; else surface default
            if name.startswith("Erp"):
                return "<p>Operational finance, inventory, and approvals for this company.</p>"
            if name.startswith("Bos"):
                return "<p>Platform fleet overview across tenants, sessions, and health.</p>"
            if name.startswith("Storefront"):
                return "<p>Browse parts, manage your garage, and track recent orders.</p>"
            return "<p>Manage store operations from the control panel.</p>"
        return m.group(0)

    text = re.sub(r"<p>([^<]*)</p>", fix_p, text)

    text = SOURCE_KPI.sub("\n", text)
    text = SOURCE_KPI2.sub("\n", text)
    text = SOURCE_EDS.sub("\n", text)

    for pat, repl in SECTION_PHP:
        text = pat.sub(repl, text)

    # Fix broken CTA indentation after removals
    text = re.sub(
        r'(<div class="[^"]*cta[^"]*">)\n(<a )',
        r"\1\n            \2",
        text,
    )
    text = re.sub(
        r'\n(<a class="ghost")',
        r"\n            \1",
        text,
    )
    text = re.sub(
        r'\n(</div>\s*\n\s*</section>)',
        r"\n        \1",
        text,
    )

    # Remaining copy leaks
    replacements = [
        (
            r"No offers for <strong>@_normalized</strong> \(@_source\)\. @_message Open PHP search for full supplier tabs\.",
            "No offers for <strong>@_normalized</strong>. Try another article or brand.",
        ),
        (
            r"Cart is empty \(@_source\)\. @_message Use PHP cart to add items\.",
            "Your cart is empty.",
        ),
        (
            r"No rows \(@_source\)\. @_message",
            "Nothing to show yet.",
        ),
        (
            r">Open live PHP ERP · @cat\.Id<",
            ">Open ERP · @cat.Id<",
        ),
        (
            r'Subtitle="Every tab deeplinks to live PHP ERP shell"',
            'Subtitle="Every ERP module and tab for this company"',
        ),
        (
            r'Subtitle="Commerce UX remains on live PHP storefront hosts — every surface linked"',
            'Subtitle="Storefront browse, account, and checkout surfaces"',
        ),
        (
            r"ASP\.NET preview reuses PHP piston CSS classes \+ industry attrs — not a broad `/` cutover\.",
            "Storefront preview uses the live theme styles and industry attributes.",
        ),
        (
            r"Same account cookies as PHP storefront digests\. Cart and checkout stay on the live PHP site\.",
            "Use your store account to continue shopping and review orders.",
        ),
        (
            r"Wave B read-only checkout scaffold\. Cart summary and step map live here; obtain mode, confirm, and payment stay on live PHP until dual-sample write parity\.",
            "Review your cart and continue to delivery and payment.",
        ),
        (
            r'@_summary\.Count line\(s\) · source <code>@_source</code>\. <a href="/storefront/cart-app">Open cart summary</a> · <a href="https://epartscart\.com/shop/cart">PHP cart</a>',
            '@_summary.Count line(s). <a href="/storefront/cart-app">Open cart</a> · <a href="https://epartscart.com/shop/cart">Checkout cart</a>',
        ),
        (
            r"Does not set <code>aspNetInteractiveComplete</code> or cutover flags\.",
            "",
        ),
        (
            r"@_note Live PHP UI remains at <a href=\"/CP/\">/CP/</a>\. Topnav uses PHP class names from desktop\.php\. Not a broad /cp cutover\.",
            "Open any module below to continue in the control panel.",
        ),
        (
            r'private string _note = "Admin session required for digest KPIs\. ";',
            'private string _note = "";',
        ),
        (
            r"Full purchases UI in PHP \(`area=purchasing&amp;tab=purchases` / <code>epc_erp_list_purchases</code>\) remains authoritative for writes — same-to-same for tenants\.",
            "Purchase documents and supplier intake for this company.",
        ),
        (
            r'<a href="/CP/">/CP/</a>',
            '<a href="/CP/">Control panel</a>',
        ),
        (
            r">PHP cart<",
            ">Cart<",
        ),
    ]
    for pat, repl in replacements:
        text = re.sub(pat, repl, text)

    # Hybrid workspace tile hrefs on command centre — point straight to PHP hrefs
    text = text.replace(
        'href="@PhpModuleCatalog.HybridWorkspaceHref("/cp/app", item.Href)"',
        'href="@item.Href"',
    )

    # Soften PhpHybridModuleDirectory subtitles that scream brochure/inventory
    text = text.replace(
        'Subtitle="@($"All {PhpModuleCatalog.CpBrochureFeatureCount} brochure features from PHP inventory")"',
        'Subtitle="@($"All {PhpModuleCatalog.CpBrochureFeatureCount} control panel modules")"',
    )

    if text != original:
        path.write_text(text, encoding="utf-8")
        return True
    return False


def main() -> int:
    changed = []
    for path in sorted(PAGES.glob("*.razor")):
        if path.name in SKIP:
            continue
        if path.name.startswith("Marketing") and "App.razor" in path.name:
            # Soften marketing legal leads without rewriting whole pages
            t = path.read_text(encoding="utf-8")
            n = re.sub(
                r"Full policy text remains on live PHP until dual-sample\.",
                "Full policy text is available from the linked policy page.",
                t,
            )
            if n != t:
                path.write_text(n, encoding="utf-8")
                changed.append(path.name)
            continue
        if polish(path):
            changed.append(path.name)
    print(f"polished={len(changed)}")
    for n in changed:
        print(f"  {n}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
