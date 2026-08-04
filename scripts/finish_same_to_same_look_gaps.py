#!/usr/bin/env python3
"""Finish same-to-same look-gap stripping across product Blazor chrome."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMPONENTS = ROOT / "aspnet/src/EcomAE.Platform/Components"
SKIP = {
    "MigrationCompareConsole.razor",
    "ZeroPhpConsole.razor",
    "ErpOnPremisesApp.razor",
    "MarketingPreviewApp.razor",
}


def patch(path: Path, pairs: list[tuple[str, str]]) -> bool:
    text = path.read_text(encoding="utf-8")
    original = text
    for old, new in pairs:
        if old in text:
            text = text.replace(old, new)
    if text != original:
        path.write_text(text, encoding="utf-8")
        return True
    return False


def main() -> int:
    changed: list[str] = []

    # --- Shared chrome ---
    targets = {
        "Shared/Desktop/PhpCpDesktopChrome.razor": [
            ('<a href="/CP/">PHP CP</a>', '<a href="/CP/">Control panel</a>'),
            (
                "  Module bodies stay PHP via ChildContent / PhpHybridWorkspaceFrame.\n",
                "  Product module bodies render via ChildContent / workspace frame.\n",
            ),
        ],
        "Shared/Desktop/PhpErpDesktopChrome.razor": [
            ('<a href="/ERP/">PHP ERP</a>', '<a href="/ERP/">ERP workspace</a>'),
        ],
        "Shared/Desktop/PhpBosDesktopChrome.razor": [
            (
                "  Full module UX still requires PHP $_SESSION on /BOS/.\n",
                "  Fleet Command topnav mirrors live BOS mega panels.\n",
            ),
        ],
        "Shared/Desktop/PhpStorefrontDesktopChrome.razor": [
            (
                "  Commerce bodies remain on live PHP storefront hosts.\n",
                "  Storefront browse, account, and checkout surfaces.\n",
            ),
        ],
        "Shared/LegacyAdminLoginForm.razor": [
            (
                '            <a class="epc-login-php" href="@PhpLoginHref">Open PHP @SurfaceLabel login</a>',
                '            <a class="epc-login-php" href="@PhpLoginHref">Open @SurfaceLabel login</a>',
            ),
            (
                """        <p class="epc-login-bridge-note">
            ASP.NET login bridge needs <code>EcomAE__SecretSuccession</code> (PHP <code>secret_succession</code>) on the host.
            Until then, use the live PHP login — same credentials and presentation.
        </p>""",
                """        <p class="epc-login-bridge-note">
            Sign-in is temporarily unavailable on this host. Use the live @SurfaceLabel login with the same credentials.
        </p>""",
            ),
            (
                """        <p class="epc-login-bridge-note">
            Bridge writes PHP-compatible <code>sessions</code> + cookies. Full menus remain on PHP until intentional cutover.
        </p>
        <p>
            <a class="epc-login-php" href="@PhpLoginHref">Prefer PHP login</a>
        </p>""",
                """        <p>
            <a class="epc-login-php" href="@PhpLoginHref">Open classic @SurfaceLabel login</a>
        </p>""",
            ),
        ],
        "Shared/Desktop/PhpEcomaeContactOverview.razor": [
            (
                "\t\t<p style=\"color:var(--epm-muted);margin-bottom:14px\">The enquiry form posts to live site until ASP.NET contact write dual-sample. Use hybrid frame for the full form.</p>\n"
                '\t\t<a class="epm-btn epm-btn--primary" href="/marketing/contact?php=@(Uri.EscapeDataString(EcomaeMarketingPages.LiveBase + "platform/contact"))">Open PHP contact form (hybrid)</a>',
                "\t\t<p style=\"color:var(--epm-muted);margin-bottom:14px\">Send an enquiry through the contact form — response within one business day.</p>\n"
                '\t\t<a class="epm-btn epm-btn--primary" href="/marketing/contact?php=@(Uri.EscapeDataString(EcomaeMarketingPages.LiveBase + "platform/contact"))">Open contact form</a>',
            ),
        ],
        "Shared/Desktop/PhpEcomaeDemoOverview.razor": [
            (
                '\t\t\t\t<a class="epm-btn epm-btn--primary" href="/marketing/demo?php=@(Uri.EscapeDataString(EcomaeMarketingPages.LiveBase + "platform/demo"))">Open PHP demo wizard (hybrid)</a>',
                '\t\t\t\t<a class="epm-btn epm-btn--primary" href="/marketing/demo?php=@(Uri.EscapeDataString(EcomaeMarketingPages.LiveBase + "platform/demo"))">Start demo wizard</a>',
            ),
            (
                """\t<div class="epm-highlight">
\t\t<h3>Provisioning stays on PHP for now</h3>
\t\t<p style="color:var(--epm-muted);margin-bottom:14px">Industry presets, sandbox creation, and the Layla wizard remain live site until dual-sample promotes this route.</p>
\t\t<a class="epm-btn epm-btn--primary" href="/marketing/demo?php=@(Uri.EscapeDataString(EcomaeMarketingPages.LiveBase + "platform/demo"))">Start demo wizard</a>
\t</div>""",
                """\t<div class="epm-highlight">
\t\t<h3>Ready when you are</h3>
\t\t<p style="color:var(--epm-muted);margin-bottom:14px">Pick an industry preset and Layla provisions storefront, control panel, and ERP in minutes.</p>
\t\t<a class="epm-btn epm-btn--primary" href="/marketing/demo?php=@(Uri.EscapeDataString(EcomaeMarketingPages.LiveBase + "platform/demo"))">Start demo wizard</a>
\t</div>""",
            ),
        ],
        "Shared/Desktop/PhpEcomaeBrochureCpOverview.razor": [
            (
                '\t\t\t\t<a class="epm-btn epm-btn--ghost" href="/marketing/brochure-cp?php=@(Uri.EscapeDataString(EcomaeMarketingPages.LiveBase + "brochure/cp"))">Open full PHP CP brochure (hybrid)</a>',
                '\t\t\t\t<a class="epm-btn epm-btn--ghost" href="/marketing/brochure-cp?php=@(Uri.EscapeDataString(EcomaeMarketingPages.LiveBase + "brochure/cp"))">Open full CP brochure</a>',
            ),
            ("\t\t\t<strong>Hybrid workspace</strong>", "\t\t\t<strong>Side-by-side view</strong>"),
        ],
        "Shared/Desktop/PhpEcomaeBosKnowledgeOverview.razor": [
            (
                '\t\t\t\t<a class="epm-btn epm-btn--ghost" href="/marketing/bos?php=@(Uri.EscapeDataString(EcomaeMarketingPages.LiveBase + "bos"))">Open full PHP BOS hub (hybrid)</a>',
                '\t\t\t\t<a class="epm-btn epm-btn--ghost" href="/marketing/bos?php=@(Uri.EscapeDataString(EcomaeMarketingPages.LiveBase + "bos"))">Open full BOS hub</a>',
            ),
        ],
        "Shared/Desktop/PhpEcomaeLegalAliasOverview.razor": [
            ("\t\t\t<strong>Hybrid workspace</strong>", "\t\t\t<strong>Side-by-side view</strong>"),
            (
                "\t\tASP.NET @Title.ToLowerInvariant() overview scaffold — dual-sample against live site <code>/@PhpSlug</code> before exact-route cutover.",
                "\t\t@Title policy overview — open the full article from the linked page.",
            ),
        ],
    }

    for rel, pairs in targets.items():
        path = COMPONENTS / rel
        if path.exists() and patch(path, pairs):
            changed.append(rel)

    # Soften remaining marketing "Open full PHP … (hybrid)" CTAs
    for path in (COMPONENTS / "Shared/Desktop").glob("PhpEcomae*.razor"):
        text = path.read_text(encoding="utf-8")
        n = text
        n = re.sub(
            r">Open full PHP ([^<]+) \(hybrid\)<",
            r">Open full \1<",
            n,
        )
        n = re.sub(
            r">Open PHP ([^<]+) \(hybrid\)<",
            r">Open \1<",
            n,
        )
        # Visible dual-sample / PHP stack notes in markup (not razor comments)
        markup, sep, code = n.partition("@code")
        markup2 = re.sub(
            r"(remain on live site[^<.]{0,80}until dual-sample[^.]*\.)",
            "Full detail is available from the linked page.",
            markup,
            flags=re.I,
        )
        markup2 = re.sub(
            r"(stay on live site until dual-sample\.?)",
            "are available from the linked page.",
            markup2,
            flags=re.I,
        )
        markup2 = re.sub(
            r"(remain on live site <code>/legal/\*</code> until dual-sample\.?)",
            "are available from the linked legal pages.",
            markup2,
            flags=re.I,
        )
        n2 = markup2 + sep + code
        if n2 != text:
            path.write_text(n2, encoding="utf-8")
            rel = str(path.relative_to(COMPONENTS))
            if rel not in changed:
                changed.append(rel)

    # --- Product pages ---
    page_fixes = {
        "Pages/ErpLoginApp.razor": [
            ("            <span>PHP AUTHORITATIVE</span>", "            <span>LIVE WORKSPACE</span>"),
            ("                    <span>PHP area @area.Id</span>", "                    <span>Area @area.Id</span>"),
        ],
        "Pages/BosFleetApp.razor": [
            (
                '        <p style="color:#52525b;max-width:40rem">PHP-matching <code>bos-topnav</code> mega panels (<code>bos/index.php</code> + <code>epc_bos_unified.php</code> section maps). Fleet Command Center stats below; full module UX still needs PHP <code>$_SESSION</code> on <a href="/BOS/">/BOS/</a>.</p>',
                '        <p style="color:#52525b;max-width:40rem">Fleet Command Center overview across tenants, sessions, and readiness. Open any module below to continue in BOS.</p>',
            ),
            (
                '            Subtitle="@($"{PhpModuleCatalog.BosSectionCount} topnav sections from PHP epc_bos_unified.php")"',
                '            Subtitle="@($"{PhpModuleCatalog.BosSectionCount} topnav sections")"',
            ),
            (
                '            Subtitle="@($"{PhpModuleCatalog.BosModuleCount} modules from PHP epc_bos_unified.php")"',
                '            Subtitle="@($"{PhpModuleCatalog.BosModuleCount} BOS modules")"',
            ),
            (
                '        <div class="epc-bosapp-note">@_note Topnav uses bos-topnav class tree. Cookie bridge ≠ PHP BOS session — use /BOS/ for full tenant module UX.</div>',
                '        <div class="epc-bosapp-note">Open any module below to continue in the BOS workspace.</div>',
            ),
            (
                '        _note = "Admin session detected — fleet KPIs from ASP.NET BOS digests (PHP Fleet Command field set). ";',
                "        // Fleet KPIs loaded for admin session.",
            ),
        ],
        "Pages/ErpBosDashboardApp.razor": [
            (
                '                <p style="margin:0;color:#64748b;font-size:.9rem">PHP-matching <code>epc-erp-topnav</code> area-column mega panels (<code>epc_erp_render_top_nav</code>). Tab bodies stay PHP.</p>',
                '                <p style="margin:0;color:#64748b;font-size:.9rem">ERP module directory for this company workspace.</p>',
            ),
            (
                "            <h2 style=\"font-size:1.05rem\">Command center KPIs — @_s.Source</h2>",
                '            <h2 style="font-size:1.05rem">Command center KPIs</h2>',
            ),
            (
                '            Subtitle="@($"{PhpModuleCatalog.ErpCategoryCount} categories from PHP erp_nav_areas.php")"',
                '            Subtitle="@($"{PhpModuleCatalog.ErpCategoryCount} ERP categories")"',
            ),
            (
                '            Subtitle="@($"{PhpModuleCatalog.ErpAreaCount} areas · {PhpModuleCatalog.ErpTabCount} tabs from PHP erp_nav_areas.php")"',
                '            Subtitle="@($"{PhpModuleCatalog.ErpAreaCount} areas · {PhpModuleCatalog.ErpTabCount} tabs")"',
            ),
            (
                """        <div class="epc-erp-note">
            @_note Live PHP ERP UI remains at <a href="/ERP/">/ERP/</a>. Desktop chrome classes match erp_desktop.php / erp_main.php.
        </div>""",
                """        <div class="epc-erp-note">
            Open any module below to continue in the ERP workspace.
        </div>""",
            ),
            (
                '        _note = "Admin session detected — KPIs from ASP.NET ERP digest reporter (PHP erp_dashboard + command center field set). ";',
                "        // ERP KPIs loaded for admin session.",
            ),
        ],
        "Pages/CpCommandCentreApp.razor": [
            (
                """    private int _users;
    private int _sessions;
    private int _tenants;
    private int _active;
    private string _source = "n/a";
    private string _note = "";
    private bool _isAdmin;
    private string? _phpHref;""",
                """    private int _users;
    private int _sessions;
    private int _tenants;
    private int _active;
    private bool _isAdmin;
    private string? _phpHref;""",
            ),
            (
                """        _users = summary.Users;
        _sessions = summary.AdminSessions;
        _tenants = summary.PortalTenants;
        _active = summary.ActivePortalTenants;
        _source = summary.Source;
        _note = "Admin session detected — KPIs from ASP.NET digest reporter. ";""",
                """        _users = summary.Users;
        _sessions = summary.AdminSessions;
        _tenants = summary.PortalTenants;
        _active = summary.ActivePortalTenants;""",
            ),
        ],
        "Pages/BosLoginApp.razor": [
            (
                """            <p class="bos-login__secure">
                <i class="fa fa-lock"></i>
                Full BOS modules stay on <a class="epc-login-php" href="/BOS/">live /BOS/</a> (PHP).
                This bridge opens hybrid digests only.
            </p>""",
                """            <p class="bos-login__secure">
                <i class="fa fa-lock"></i>
                Secure operator access to your tenant fleet.
            </p>""",
            ),
        ],
        "Pages/StorefrontProfileApp.razor": [
            (
                "Sign in with a customer session to load your profile. Edits remain on PHP.",
                "Sign in with your store account to load your profile.",
            ),
        ],
        "Pages/StorefrontCartApp.razor": [
            (
                "Sign in with a customer session to load your authenticated cart lines. Guest carts remain on PHP.",
                "Sign in with your store account to load your cart lines.",
            ),
        ],
        "Pages/StorefrontAccountSummaryApp.razor": [
            (
                "Sign in with a customer session to load account KPIs. Edits remain on PHP.",
                "Sign in with your store account to load account summary.",
            ),
        ],
    }

    for rel, pairs in page_fixes.items():
        path = COMPONENTS / rel
        if path.exists() and patch(path, pairs):
            changed.append(rel)

    # Soften leftover "in digest (...)" empty states that reveal stack
    for path in (COMPONENTS / "Pages").glob("*.razor"):
        if path.name in SKIP:
            continue
        text = path.read_text(encoding="utf-8")
        n = text
        n = re.sub(
            r"No ([^<]+) in digest \(@_[A-Za-z]+\)\. @_[A-Za-z]+",
            r"No \1 yet.",
            n,
        )
        n = re.sub(
            r"No rows \(@_source\)\. @_message",
            "Nothing to show yet.",
            n,
        )
        if n != text:
            path.write_text(n, encoding="utf-8")
            rel = f"Pages/{path.name}"
            if rel not in changed:
                changed.append(rel)

    # Remove unused _note fields that only held stack copy (Bos/Erp dashboards)
    for rel in ("Pages/BosFleetApp.razor", "Pages/ErpBosDashboardApp.razor"):
        path = COMPONENTS / rel
        text = path.read_text(encoding="utf-8")
        n = text
        # If _note is no longer referenced in markup, drop field + leftover assignment comments already set
        markup = n.split("@code", 1)[0]
        if "_note" not in markup and "private string _note" in n:
            n = re.sub(r"\n\s*private string _note = \"\";\n", "\n", n)
            n = re.sub(r"\n\s*private string _source = \"n/a\";\n", "\n", n)
            # Drop unused _source assignment if field gone
            if "private string _source" not in n:
                n = re.sub(r"\n\s*_source = summary\.Source;\n", "\n", n)
                n = re.sub(r"\n\s*_source = result\.Source;\n", "\n", n)
        if n != text:
            path.write_text(n, encoding="utf-8")
            if rel not in changed:
                changed.append(rel)

    print(f"changed={len(changed)}")
    for c in sorted(set(changed)):
        print(f"  {c}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
