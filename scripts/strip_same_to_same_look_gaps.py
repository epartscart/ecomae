#!/usr/bin/env python3
"""Strip stack-revealing look gaps from CP/ERP/BOS/storefront Blazor product apps.

Goal: tenant-facing UI must not advertise ASP.NET digests, PHP cutover, Hybrid
workspace, or "Open PHP". Product chrome only — same-to-same with live hubs.

Skips migration/operator consoles. Never invents cutoverAllowed=true.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PAGES = ROOT / "aspnet/src/EcomAE.Platform/Components/Pages"

SKIP_NAMES = {
    "MigrationCompareConsole.razor",
    "ZeroPhpConsole.razor",
    "ErpOnPremisesApp.razor",  # on-prem installer surface — operator-facing
}

# CTA / link text rewrites (order matters — longer first)
LINK_REWRITES = [
    (re.compile(r">Open full PHP OMS<"), ">Open orders<"),
    (re.compile(r">Open PHP Control Panel<"), ">Open control panel<"),
    (re.compile(r">Open PHP ERP dashboard<"), ">Open dashboard<"),
    (re.compile(r">Open PHP platform health<"), ">Open platform health<"),
    (re.compile(r">Open PHP orders<"), ">Open orders<"),
    (re.compile(r">Open PHP delivery methods<"), ">Manage methods<"),
    (re.compile(r">Open PHP[^<]*<"), ">Open<"),
    (re.compile(r">Open in PHP<"), ">Open<"),
    (re.compile(r">PHP orders<"), ">Orders<"),
]

HYBRID_LINK = re.compile(
    r'\s*<a class="ghost" href="@PhpModuleCatalog\.HybridWorkspaceHref\([^)]+\)">Hybrid workspace</a>\s*\n?'
)
COMMAND_CENTRE = re.compile(
    r'\s*<a class="ghost" href="/(?:cp|erp|bos)/app">(?:Command centre|ERP shell|Fleet command)</a>\s*\n?'
)
COMMAND_CENTRE_PLAIN = re.compile(
    r'\s*<a class="ghost" href="/cp/app">Command centre</a>\s*\n?'
)

# Footer note blocks that advertise digests/cutover — remove entire div when migration-flavored
NOTE_DIV = re.compile(
    r'\n\s*<div class="[^"]*note[^"]*">[\s\S]*?</div>\s*(?=\n</Php|\n</Storefront|\n@code|\Z)',
    re.MULTILINE,
)

HERO_LEAK_PATTERNS = [
    (re.compile(r"Read-only [^.]{0,80} from <code>/[^<]+</code>\.\s*"), ""),
    (re.compile(r"Read-only [^.]{0,120}digest[^.]{0,160}\.\s*", re.I), ""),
    (re.compile(r"Batch \d+ read-only [^.]{0,200}\.\s*", re.I), ""),
    (re.compile(r"Full OMS console, filters, and writes stay on live PHP\.\s*", re.I), ""),
    (re.compile(r"Interactive widgets stay on live PHP\.\s*", re.I), ""),
    (re.compile(r"Live health checks stay on PHP[^.]*\.\s*", re.I), ""),
    (re.compile(r"Full order detail, reorder, and returns stay on live PHP[^.]{0,80}\.\s*", re.I), ""),
    (re.compile(r"under PHP (?:CP|ERP|BOS|storefront) desktop chrome\.\s*", re.I), ""),
    (re.compile(r"matching PHP <code>[^<]+</code>[^.]*\.\s*", re.I), ""),
    (re.compile(r"\s*native <code>/BOS/</code> session still required for full module UX\.\s*", re.I), ""),
    (re.compile(r"Credentials and checkout writes stay on live PHP\.\s*", re.I), ""),
    (re.compile(r"Rate edits and shipments stay on live PHP\.\s*", re.I), ""),
    (re.compile(r"Secrets and delivery payloads stay off this digest\.\s*", re.I), ""),
    (re.compile(r"Channel config stays on live PHP\.\s*", re.I), ""),
    (re.compile(r"Configure handlers and pickup stays on live PHP\.\s*", re.I), ""),
    (re.compile(r"Sensitive JSON packs stay on live PHP\.\s*", re.I), ""),
]

EMPTY_ROW_LEAK = re.compile(
    r"(No [^<]{0,80} in digest \(@_source\)\.\s*@_message)"
)
EMPTY_ROW_FIX = "Nothing to show yet."

# Empty hero <p> after stripping — fill with product copy by surface
DEFAULT_HERO = {
    "cp": "Manage store operations, catalogue, and partner integrations from one control panel.",
    "erp": "Operational finance, inventory, and approvals for this company.",
    "bos": "Platform fleet overview across tenants, sessions, and health.",
    "storefront": "Browse parts, manage your garage, and track recent orders.",
}


def surface_of(name: str) -> str:
    lower = name.lower()
    if lower.startswith("erp"):
        return "erp"
    if lower.startswith("bos"):
        return "bos"
    if lower.startswith("storefront"):
        return "storefront"
    return "cp"


def strip_note_divs(text: str) -> str:
    def should_drop(block: str) -> bool:
        markers = (
            "JSON digest",
            "Not a broad",
            "cutover",
            "same-to-same",
            "remains authoritative",
            "Source:",
            "tenant product chrome",
            "$_SESSION",
            "Digest JSON",
            "Read-only preview",
            "phpAuthoritative",
            "ASP.NET",
            "aspnet",
        )
        return any(m in block for m in markers)

    out = text
    for m in list(NOTE_DIV.finditer(text)):
        if should_drop(m.group(0)):
            out = out.replace(m.group(0), "\n")
    return out


def soften_hero_paragraphs(text: str, surface: str) -> str:
    def repl(m: re.Match[str]) -> str:
        inner = m.group(1)
        original = inner
        for pat, repl_s in HERO_LEAK_PATTERNS:
            inner = pat.sub(repl_s, inner)
        inner = re.sub(r"\s{2,}", " ", inner).strip()
        # Drop leftover code route crumbs in hero
        inner = re.sub(r"\s*<code>/[^<]+</code>\s*", " ", inner)
        inner = re.sub(r"\s{2,}", " ", inner).strip()
        if not inner or len(inner) < 24 or "digest" in inner.lower() or "live PHP" in inner:
            inner = DEFAULT_HERO[surface]
        if inner == original:
            # Still leaky?
            if any(x in original for x in ("digest", "live PHP", "Batch ", "cutover", "JSON")):
                inner = DEFAULT_HERO[surface]
        return f"<p>{inner}</p>"

    return re.sub(r"<p>([\s\S]*?)</p>", repl, text, count=3)


def transform(text: str, name: str) -> str:
    surface = surface_of(name)
    original = text

    text = HYBRID_LINK.sub("\n", text)
    text = COMMAND_CENTRE.sub("\n", text)
    text = COMMAND_CENTRE_PLAIN.sub("\n", text)

    for pat, repl in LINK_REWRITES:
        text = pat.sub(repl, text)

    text = strip_note_divs(text)
    text = soften_hero_paragraphs(text, surface)
    text = EMPTY_ROW_LEAK.sub(EMPTY_ROW_FIX, text)

    # Row empty messages that still mention digest source
    text = re.sub(
        r"No rows in digest \(@_source\)\.\s*@_message",
        EMPTY_ROW_FIX,
        text,
    )
    text = re.sub(
        r"No [^<]{0,60} in digest \(@_source\)\.\s*@_message",
        EMPTY_ROW_FIX,
        text,
    )

    # Clean double blank lines inside markup lightly
    text = re.sub(r"\n{3,}", "\n\n", text)

    if text == original:
        return text
    return text


def main() -> int:
    changed = []
    scanned = 0
    for path in sorted(PAGES.glob("*.razor")):
        if path.name in SKIP_NAMES:
            continue
        if not (
            path.name.startswith(("Cp", "Erp", "Bos", "Storefront"))
            or path.name.endswith("App.razor")
        ):
            # Only product surfaces
            if path.name not in {"CpOrdersApp.razor"} and not path.name.endswith("App.razor"):
                continue
        # Always include *App.razor under product prefixes + CpOrders (route /cp/orders)
        if not (
            path.name.endswith("App.razor")
            or path.name in {"CpOrdersApp.razor"}  # legacy name without App suffix? it's CpOrdersApp? 
        ):
            continue
        if path.name.startswith(("Migration", "ZeroPhp")):
            continue

        scanned += 1
        before = path.read_text(encoding="utf-8")
        # Skip files that don't look like product list/summary UIs with leaks
        if not any(
            x in before
            for x in (
                "Open PHP",
                "Hybrid workspace",
                "JSON digest",
                "Not a broad",
                "Open in PHP",
                "same-to-same",
                "Command centre",
                "in digest (@_source)",
            )
        ):
            continue
        after = transform(before, path.name)
        if after != before:
            path.write_text(after, encoding="utf-8")
            changed.append(path.name)

    # Also handle CpOrdersApp.razor if named differently - already globbed *App.razor
    # CpOrders is CpOrdersApp? File is CpOrdersApp.razor from earlier read - wait file was CpOrdersApp? 
    # Earlier read path was CpOrdersApp.razor - no, it was Components/Pages/CpOrdersApp.razor in grep
    # but Read path was CpOrdersApp - actually Read was CpOrdersApp.razor? Read said CpOrdersApp.razor - looking back: path was CpOrdersApp.razor... 
    # Actually Read path: ./aspnet/.../CpOrdersApp.razor - NO Read said CpOrdersApp.razor from grep of CpOrdersApp.razor
    # The file is CpOrdersApp.razor based on glob *App.razor. Wait the Read was:
    # path: .../CpOrdersApp.razor - looking: `CpOrdersApp.razor` vs `CpOrdersApp` - the first Read was `CpOrdersApp.razor`? 
    # It was: aspnet/src/EcomAE.Platform/Components/Pages/CpOrdersApp.razor in grep, but Read used CpOrdersApp.razor
    # Actually Read path was: /workspace/aspnet/src/EcomAE.Platform/Components/Pages/CpOrdersApp.razor
    # Hmm the first Read says: path CpOrdersApp.razor - no: `CpOrdersApp.razor` - looking carefully:
    # `path`: `/workspace/aspnet/src/EcomAE.Platform/Components/Pages/CpOrdersApp.razor`
    # Wait it says CpOrdersApp - the content has @page "/cp/orders" - file might be CpOrdersApp.razor
    orders = PAGES / "CpOrdersApp.razor"
    if not orders.exists():
        # try alternate
        for alt in PAGES.glob("CpOrder*.razor"):
            before = alt.read_text(encoding="utf-8")
            after = transform(before, alt.name)
            if after != before:
                alt.write_text(after, encoding="utf-8")
                if alt.name not in changed:
                    changed.append(alt.name)

    print(f"scanned={scanned} changed={len(changed)}")
    for name in changed:
        print(f"  {name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
