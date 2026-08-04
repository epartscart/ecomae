#!/usr/bin/env python3
"""Wave 23 wiring helper for failover/ops-guides/file-manager/server-ip."""
from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NOW = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

DIGESTS = [
    dict(
        stem="failover-status",
        pascal="FailoverStatus",
        php="/CP/control/portal/epc_platform_failover_guide",
        tables="filesystem epc-platform-status.*",
        collection="signals",
        summary=["modeFilePresent", "statusJsonPresent", "configPresent", "backupMode"],
        row=["path", "present", "kind"],
        omit="secrets inside failover config",
        title="Failover status",
        hero="Read-only failover signal files. Exposes presence/mode without LFI.",
        cols=[("Path", "Path"), ("Present", "Present"), ("Kind", "Kind")],
        kpi=["Mode file", "Status JSON", "Config", "Backup mode"],
        matcher_cp=["failover-runbook", "epc-platform-failover-guide"],
        matcher_bos=["failover"],
        empty_sum="0, 0, 0, 0",
    ),
    dict(
        stem="ops-guides",
        pascal="OpsGuides",
        php="/CP/control/cp-guideline",
        tables="control_groups + control_items",
        collection="items",
        summary=["groupCount", "itemCount", "showAnywayCount", "urlItemCount"],
        row=["id", "itemsGroup", "caption", "url", "showAnyway", "sortOrder"],
        omit="guide HTML bodies",
        title="Ops guides",
        hero="Read-only CP menu map for guideline + ERP-only onboard deeplinks.",
        cols=[
            ("Id", "Id"),
            ("Group", "ItemsGroup"),
            ("Caption", "Caption"),
            ("URL", "Url"),
            ("Show anyway", "ShowAnyway"),
        ],
        kpi=["Groups", "Items", "Show anyway", "With URL"],
        matcher_cp=["cp-guideline", "erp-only-onboard-guide", "epc-erp-only-onboard-guide"],
        matcher_bos=[],
        empty_sum="0, 0, 0, 0",
    ),
    dict(
        stem="file-manager",
        pascal="FileManager",
        php="/CP/filemanager",
        tables="filesystem /content/files",
        collection="entries",
        summary=["rootPresent", "fileCount", "dirCount", "totalBytes"],
        row=["name", "isDirectory", "sizeBytes", "extension"],
        omit="file contents",
        title="File manager",
        hero="Read-only /content/files inventory. No elFinder writes.",
        cols=[("Name", "Name"), ("Dir", "IsDirectory"), ("Bytes", "SizeBytes"), ("Ext", "Extension")],
        kpi=["Root OK", "Files", "Dirs", "Bytes"],
        matcher_cp=["file-manager"],
        matcher_bos=[],
        empty_sum="0, 0, 0, 0L",
    ),
    dict(
        stem="server-ip",
        pascal="ServerIp",
        php="/content/usefull/ip.php",
        tables="runtime host",
        collection="addresses",
        summary=["addressCount", "hasIpv4", "hasIpv6", "loopbackOnly"],
        row=["address", "addressFamily", "isLoopback"],
        omit="no outbound ipify",
        title="Server IP",
        hero="Local NIC addresses without outbound IP echo (more reliable than PHP ipify).",
        cols=[("Address", "Address"), ("Family", "AddressFamily"), ("Loopback", "IsLoopback")],
        kpi=["Addresses", "IPv4", "IPv6", "Loopback only"],
        matcher_cp=["server-ip"],
        matcher_bos=[],
        empty_sum="0, 0, 0, 0",
    ),
]


def patch_contracts() -> None:
    path = ROOT / "aspnet/src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs"
    text = path.read_text(encoding="utf-8")
    if "/cp/failover-status" in text:
        print("contracts already")
        return
    cchunks = []
    fchunks = []
    for d in DIGESTS:
        fields = ", ".join(f'"{x}"' for x in d["summary"] + ["source", "message"])
        cchunks.append(
            f'        Contract("cp", "/cp/{d["stem"]}", "{d["tables"]}", "admin-cp",\n'
            f'            ["ok", "surface", "summary", "{d["collection"]}", "count", "source", "message", "session", "note"],\n'
            f'            [{fields}],\n'
            f'            ["{d["title"]} KPIs + {d["collection"]}", "{d["omit"]}", "PHP {d["title"]} remains authoritative"],\n'
            f'            "cp/templates/bootstrap_admin/desktop.php"),\n'
        )
        fchunks.append(
            f'        new("cp", "{d["stem"]} Blazor list", "/cp/{d["stem"]}-app", "presentation-shell-scaffolded", '
            f'"Read UI over /cp/{d["stem"]} digest; {d["omit"]}; PHP {d["title"]} remains authoritative; tenant chrome stays PHP."),\n'
        )
    # Insert contracts after sitemap contract if present, else before bank-reconciliation
    if 'Contract("cp", "/cp/sitemap"' in text:
        i = text.find('Contract("cp", "/cp/sitemap"')
        j = text.find("),", i) + 2
        text = text[: j + 1] + "\n" + "".join(cchunks) + text[j + 1 :]
    else:
        needle = '        Contract("erp", "/erp/bank-reconciliation"'
        text = text.replace(needle, "".join(cchunks) + needle, 1)
    # Functions
    if 'sitemap Blazor list' in text:
        needle = '        new("cp", "sitemap Blazor list"'
        # find end of that new(...),
        i = text.find(needle)
        j = text.find("),", i) + 2
        text = text[: j + 1] + "\n" + "".join(fchunks) + text[j + 1 :]
    else:
        needle = '        new("cp", "config-items Blazor list"'
        text = text.replace(needle, "".join(fchunks) + needle, 1)
    path.write_text(text, encoding="utf-8")
    print("contracts ok")


def patch_nav_links() -> None:
    nav = ROOT / "aspnet/src/EcomAE.Platform/Presentation/LegacyChromeNavCatalog.cs"
    nt = nav.read_text(encoding="utf-8")
    if "Failover status" not in nt:
        lines = "".join(f'        new("{d["title"]}", "/cp/{d["stem"]}-app"),\n' for d in DIGESTS)
        for marker in (
            '        new("Sitemap", "/cp/sitemap-app"),\n',
            '        new("Data migrations", "/cp/data-migrations-app"),\n',
        ):
            if marker in nt:
                nt = nt.replace(marker, marker + lines, 1)
                break
        nav.write_text(nt, encoding="utf-8")
        print("nav ok")

    links = ROOT / "aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs"
    lt = links.read_text(encoding="utf-8")
    if "/cp/failover-status-app" in lt:
        print("links already")
        return
    prev = "".join(
        f'            Link("aspnet-presentation-preview", "CP {d["title"]} read UI", '
        f'"https://www.ecomae.com/cp/{d["stem"]}-app", "aspnet", "/cp/{d["stem"]}-app", '
        f'"Blazor list over /cp/{d["stem"]} digest under PhpCpDesktopChrome. {d["omit"]}. '
        f'PHP {d["title"]} remains authoritative. Tenant chrome stays PHP (same-to-same)."),\n'
        for d in DIGESTS
    )
    dig = "".join(
        f'            Link("aspnet-exact-route-shadow-live", "CP {d["stem"]} digest", '
        f'"https://www.ecomae.com/cp/{d["stem"]}", "aspnet", "/cp/{d["stem"]}", '
        f'"Wired exact-route nginx shadow (unauth 401 when installed). Part of surface digests 127/127 batch."),\n'
        for d in DIGESTS
    )
    # insert after sitemap preview if present else after geo
    for marker in (
        '            Link("aspnet-presentation-preview", "CP Sitemap read UI"',
        '            Link("aspnet-presentation-preview", "CP Geo / regions read UI"',
        '            Link("aspnet-presentation-preview", "CP Data migrations read UI"',
    ):
        if marker in lt:
            lt = lt.replace(marker, prev + marker, 1)
            break
    for marker in (
        '            Link("aspnet-exact-route-shadow-live", "CP sitemap digest"',
        '            Link("aspnet-exact-route-shadow-live", "CP geo-regions digest"',
        '            Link("aspnet-exact-route-shadow-live", "CP data-migrations digest"',
    ):
        if marker in lt:
            lt = lt.replace(marker, dig + marker, 1)
            break
    lt = lt.replace("Surface digests: wired 123", "Surface digests: wired 127")
    lt = lt.replace("Part of surface digests 123/123 batch.", "Part of surface digests 127/127 batch.")
    links.write_text(lt, encoding="utf-8")
    print("links ok")


def write_blazor() -> None:
    for d in DIGESTS:
        path = ROOT / f"aspnet/src/EcomAE.Platform/Components/Pages/Cp{d['pascal']}App.razor"
        if path.exists():
            continue
        coll_prop = d["collection"][0].upper() + d["collection"][1:]
        kpi = "\n".join(
            f'        <div class="epc-w23-kpi"><strong>@_summary.{f[0].upper()+f[1:]}.ToString(CultureInfo.InvariantCulture)</strong><span>{lab}</span></div>'
            for f, lab in zip(d["summary"], d["kpi"])
        )
        ths = "".join(f"<th>{a}</th>" for a, _ in d["cols"]) + "<th></th>"
        tds = "".join(f"<td>@row.{prop}</td>" for _, prop in d["cols"]) + '<td><a href="@_phpTab">Open in PHP</a></td>'
        path.write_text(
            f"""@page "/cp/{d['stem']}-app"
@layout Layout.PhpChromeLayout
@using System.Globalization
@using EcomAE.Platform.Auth
@using EcomAE.Platform.Migration
@using EcomAE.Platform.Presentation
@inject ISurfaceDashboardSummaryReporter Dashboards
@inject ILegacySessionValidator Sessions
@inject IHttpContextAccessor Http
@inject NavigationManager Nav

<PageTitle>{d['title']} · Control Panel · eParts Cart</PageTitle>
<PhpChromeStyles Surface="cp" />
<PhpCpDesktopChrome IsAdmin="_isAdmin">
    <style>
        .epc-w23-hero {{ margin-bottom:1rem; padding:1.1rem 1.2rem; border-radius:.7rem; color:#fff; background:linear-gradient(135deg,#0f172a 0%,#1e3a5f 45%,#0ea5e9 100%); }}
        .epc-w23-hero h1 {{ margin:.25rem 0 .35rem; font-size:clamp(1.35rem,2.8vw,1.85rem); }}
        .epc-w23-hero p {{ margin:0; color:rgba(255,255,255,.88); max-width:44rem; font-size:.92rem; }}
        .epc-w23-cta {{ display:flex; gap:.45rem; flex-wrap:wrap; margin-top:.85rem; }}
        .epc-w23-cta a {{ display:inline-flex; padding:.5rem .8rem; border-radius:.4rem; font-weight:700; font-size:.82rem; text-decoration:none; color:#0f172a; background:#fff; }}
        .epc-w23-cta a.ghost {{ background:transparent; border:1px solid rgba(255,255,255,.35); color:#fff; }}
        .epc-w23-kpis {{ display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:.65rem; margin-bottom:1rem; }}
        .epc-w23-kpi {{ background:#fff; border:1px solid #e2e8f0; border-radius:.55rem; padding:.85rem .9rem; }}
        .epc-w23-kpi strong {{ display:block; font-size:1.2rem; }}
        .epc-w23-kpi span {{ color:#64748b; font-size:.78rem; }}
        .epc-w23-table-wrap {{ background:#fff; border:1px solid #e2e8f0; border-radius:.55rem; overflow:auto; }}
        .epc-w23-table {{ width:100%; border-collapse:collapse; font-size:.86rem; }}
        .epc-w23-table th, .epc-w23-table td {{ padding:.55rem .7rem; border-bottom:1px solid #f1f5f9; text-align:left; white-space:nowrap; }}
        .epc-w23-table th {{ background:#f8fafc; color:#475569; font-size:.75rem; text-transform:uppercase; }}
        .epc-w23-note {{ margin-top:1rem; padding:.75rem .9rem; background:#f8fafc; border:1px solid #cbd5e1; border-radius:.45rem; color:#334155; font-size:.86rem; }}
        @@media (max-width:900px){{ .epc-w23-kpis{{grid-template-columns:1fr 1fr;}} }}
    </style>
    <section class="epc-w23-hero">
        <img src="@LegacyPresentationAssets.BrandMarkUrl" alt="ECOM AE" style="height:28px;background:#fff;border-radius:4px;padding:2px" />
        <h1>{d['title']}</h1>
        <p>{d['hero']}</p>
        <div class="epc-w23-cta">
            <a href="@_phpTab">Open PHP</a>
            <a class="ghost" href="@PhpModuleCatalog.HybridWorkspaceHref(\"/cp/app\", _phpTab)">Hybrid workspace</a>
            <a class="ghost" href="/cp/app">Command centre</a>
        </div>
    </section>
    <div class="epc-w23-kpis">
{kpi}
    </div>
    <div class="epc-w23-table-wrap"><table class="epc-w23-table">
        <thead><tr>{ths}</tr></thead>
        <tbody>
        @if (_rows.Count==0) {{ <tr><td colspan="{len(d['cols'])+1}">No rows (@_source). @_message</td></tr> }}
        else {{ @foreach (var row in _rows) {{ <tr>{tds}</tr> }} }}
        </tbody>
    </table></div>
    <div class="epc-w23-note">Read-only · <a href="/cp/{d['stem']}?limit=200">/cp/{d['stem']}</a> · {d['omit']} · PHP remains authoritative · Source <code>@_source</code></div>
</PhpCpDesktopChrome>
@code {{
    private const string _phpTab = "{d['php']}";
    private bool _isAdmin;
    private Cp{d['pascal']}Summary _summary = new({d['empty_sum']}, "n/a", "");
    private IReadOnlyList<Cp{d['pascal']}RowDigest> _rows = [];
    private string _source = "n/a";
    private string _message = "";
    protected override async Task OnInitializedAsync()
    {{
        var ctx = Http.HttpContext;
        if (ctx is null) return;
        var session = await Sessions.ValidateAsync(ctx, ctx.RequestAborted);
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
        {{
            Nav.NavigateTo("/cp/login", forceLoad: true);
            return;
        }}
        _isAdmin = true;
        var result = await Dashboards.BuildCp{d['pascal']}DigestAsync(200, ctx.RequestAborted);
        _summary = result.Summary;
        _rows = result.{coll_prop};
        _source = result.Source;
        _message = result.Message;
    }}
}}
""",
            encoding="utf-8",
        )
    print("blazor ok")


def patch_scripts_deploy() -> None:
    cap = ROOT / "scripts/cloudpanel_capture_module_function_parity.sh"
    ct = cap.read_text(encoding="utf-8")
    if '"failover-runbook": "cp-failover-status"' not in ct:
        cp_lines = "\n    # Wave 23 — ops/guide leftovers\n"
        for d in DIGESTS:
            for m in d["matcher_cp"]:
                cp_lines += f'    "{m}": "cp-{d["stem"]}",\n'
        if '"templates-manager": "cp-templates-manager",\n}\n' in ct:
            ct = ct.replace(
                '    "templates-manager": "cp-templates-manager",\n}\n',
                '    "templates-manager": "cp-templates-manager",\n' + cp_lines + "}\n",
            )
        bos = "".join(f'    "{m}": "cp-{d["stem"]}",\n' for d in DIGESTS for m in d["matcher_bos"])
        if bos and '"design_tokens": "cp-design-tokens",\n}\n' in ct:
            ct = ct.replace(
                '    "design_tokens": "cp-design-tokens",\n}\n',
                '    "design_tokens": "cp-design-tokens",\n' + bos + "}\n",
            )
        cap.write_text(ct, encoding="utf-8")
        print("matchers ok")

    hy = ROOT / "scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh"
    ht = hy.read_text(encoding="utf-8")
    if "cp-failover-status" not in ht:
        lines = "".join(
            f'    ("cp-{d["stem"]}", "cp", "/cp/{d["stem"]}-app", "/cp/{d["stem"]}", "{d["php"]}", "Cp{d["pascal"]}App", "PhpCpDesktopChrome", "admin"),\n'
            for d in DIGESTS
        )
        for marker in (
            '    ("cp-sitemap", "cp", "/cp/sitemap-app", "/cp/sitemap", "/CP/content/sitemap", "CpSitemapApp", "PhpCpDesktopChrome", "admin"),\n',
            '    ("cp-data-migrations", "cp", "/cp/data-migrations-app", "/cp/data-migrations", "/CP/shop/finance/erp?area=setup&tab=data_import&epc_erp_shell=1", "CpDataMigrationsApp", "PhpCpDesktopChrome", "admin"),\n',
        ):
            if marker in ht:
                ht = ht.replace(marker, marker + lines, 1)
                break
        hy.write_text(ht, encoding="utf-8")

    dig = ROOT / "scripts/cloudpanel_capture_digest_dual_samples.sh"
    dt = dig.read_text(encoding="utf-8")
    if "cp-failover-status" not in dt:
        lines = "".join(f'  [cp-{d["stem"]}]="/cp/{d["stem"]}?limit=5"\n' for d in DIGESTS)
        for marker in ('  [cp-sitemap]="/cp/sitemap?limit=5"\n', '  [cp-data-migrations]="/cp/data-migrations?limit=5"\n'):
            if marker in dt:
                dt = dt.replace(marker, marker + lines, 1)
                break
        dig.write_text(dt, encoding="utf-8")

    cmp = ROOT / "scripts/compare_digest_dual_samples.py"
    ct = cmp.read_text(encoding="utf-8")
    if "cp-failover-status" not in ct:
        sum_lines = ""
        list_lines = ""
        for d in DIGESTS:
            fields = ",".join(d["summary"] + ["source", "message"])
            sum_lines += f'    "cp-{d["stem"]}": (\n        "summary",\n        "{fields}",\n    ),\n'
            list_lines += f'    "cp-{d["stem"]}": (\n        "{d["collection"]}",\n        {json.dumps(d["row"])},\n    ),\n'
        # insert near end of SUMMARY before erp-inventory or after cp-sitemap if present
        if '"cp-sitemap": (\n        "summary",' in ct:
            ct = ct.replace('    "cp-sitemap": (\n        "summary",', sum_lines + '    "cp-sitemap": (\n        "summary",', 1)
        else:
            ct = ct.replace(
                '    "cp-data-migrations": (\n        "summary",\n        "migrationCount,completedCount,failedCount,rowCount,source,message",\n    ),\n',
                '    "cp-data-migrations": (\n        "summary",\n        "migrationCount,completedCount,failedCount,rowCount,source,message",\n    ),\n'
                + sum_lines,
            )
        if '"cp-sitemap": (\n        "pages"' in ct:
            ct = ct.replace('    "cp-sitemap": (\n        "pages"', list_lines + '    "cp-sitemap": (\n        "pages"', 1)
        else:
            ct = ct.replace(
                '    "cp-data-migrations": (\n        "migrations",',
                list_lines + '    "cp-data-migrations": (\n        "migrations",',
                1,
            )
        cmp.write_text(ct, encoding="utf-8")

    for conf, suffix, header in [
        (ROOT / "deploy/aspnet/nginx-surface-digests-shadow-example.conf", "", "shadow-approved"),
        (ROOT / "deploy/aspnet/nginx-presentation-app-shadow-example.conf", "-app", "app-read-ui-preview"),
    ]:
        t = conf.read_text(encoding="utf-8")
        if f"location = /cp/failover-status{suffix}" not in t:
            blocks = ""
            for d in DIGESTS:
                stem = d["stem"]
                blocks += (
                    f"location = /cp/{stem}{suffix} {{\n"
                    f"    proxy_pass http://127.0.0.1:5100;\n"
                    f"    proxy_set_header Host $host;\n"
                    f"    proxy_set_header X-Real-IP $remote_addr;\n"
                    f"    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;\n"
                    f"    proxy_set_header X-EcomAE-Route-Cutover cp-{stem}{suffix}-{header};\n"
                    f"}}\n"
                )
            conf.write_text(t.rstrip() + "\n" + blocks, encoding="utf-8")

    replacements = [
        (ROOT / "scripts/cloudpanel_install_surface_digest_shadows.sh", [("expected 123", "expected 127")]),
        (ROOT / "scripts/cloudpanel_install_presentation_app_shadows.sh", [("expected = 140", "expected = 144")]),
        (
            ROOT / "scripts/generate_all_yarp_design_examples.sh",
            [
                ('"deploy/aspnet/yarp-exact-routes-example.json": 140', '"deploy/aspnet/yarp-exact-routes-example.json": 144'),
                ('"deploy/aspnet/yarp-surface-digests-example.json": 123', '"deploy/aspnet/yarp-surface-digests-example.json": 127'),
            ],
        ),
        (
            ROOT / "scripts/validate_surface_digest_allowlist_sync.py",
            [("expected 123 digest locations", "expected 127 digest locations"), ("!= 123", "!= 127")],
        ),
        (
            ROOT / "aspnet/tests/EcomAE.Platform.Tests/LiveSurfaceLinkReporterTests.cs",
            [
                ("Assert.Equal(123,", "Assert.Equal(127,"),
                ("Assert.Equal(135,", "Assert.Equal(139,"),
                ("wired 123", "wired 127"),
            ],
        ),
    ]
    for path, pairs in replacements:
        t = path.read_text(encoding="utf-8")
        for a, b in pairs:
            t = t.replace(a, b)
        path.write_text(t, encoding="utf-8")
    print("scripts/deploy/tests count bumps ok")


def write_samples() -> None:
    mig = ROOT / "docs/migration/evidence/surface-parity/samples/migration"
    for d in DIGESTS:
        p = mig / f'cp-{d["stem"]}.json'
        if p.exists():
            continue
        summ = {k: 1 for k in d["summary"]}
        row = {}
        for k in d["row"]:
            row[k] = (
                "migration"
                if k
                in {
                    "path",
                    "kind",
                    "name",
                    "extension",
                    "address",
                    "addressFamily",
                    "caption",
                    "url",
                }
                else 1
            )
        doc = {
            "ok": True,
            "surface": "cp",
            "summary": {**summ, "source": "migration", "message": "TenantRegistry DB is not configured."},
            d["collection"]: [row],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": f"migration-mode; {d['omit']}; cutoverAllowed=false",
        }
        p.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")

    out = ROOT / "docs/migration/evidence/hybrid-ui-dual-samples"
    for d in DIGESTS:
        p = out / f'aspnet-cp-{d["stem"]}-hybrid-ui.json'
        if p.exists():
            continue
        p.write_text(
            json.dumps(
                {
                    "role": "aspnet-hybrid-ui-sample",
                    "stem": f'cp-{d["stem"]}',
                    "surface": "cp",
                    "appRoute": f'/cp/{d["stem"]}-app',
                    "digestRoute": f'/cp/{d["stem"]}',
                    "phpAuthoritativePath": d["php"],
                    "blazorMarker": f'Cp{d["pascal"]}App',
                    "chromeShell": "PhpCpDesktopChrome",
                    "authKind": "admin",
                    "httpStatus": None,
                    "markersFound": [],
                    "phpDeeplinkFound": False,
                    "phpAuthoritative": True,
                    "wwwPreviewOnly": True,
                    "tenantChromePhp": True,
                    "cutoverAllowed": False,
                    "readyForPhpRemoval": False,
                    "capturedAt": NOW,
                    "baseUrl": "http://127.0.0.1:5100",
                    "publicBaseUrl": "https://www.ecomae.com",
                    "note": "Contract stub. Re-run on CloudPanel with cookies + ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES=1.",
                },
                indent=2,
            )
            + "\n",
            encoding="utf-8",
        )
    print("samples ok")


def patch_validator() -> None:
    val = ROOT / "aspnet/tests/EcomAE.Platform.Tests/SurfaceDigestContractValidatorTests.cs"
    vt = val.read_text(encoding="utf-8")
    if "/cp/failover-status" in vt:
        print("validator already")
        return
    lines = ""
    for d in DIGESTS:
        coll = d["collection"]
        prop = coll[0].upper() + coll[1:]
        p = d["pascal"]
        lines += (
            f'            ["/cp/{d["stem"]}"] = new {{ ok = true, surface = "cp", '
            f'summary = (await reporter.BuildCp{p}DigestAsync(10)).Summary, '
            f'{coll} = (await reporter.BuildCp{p}DigestAsync(10)).{prop}, '
            f'count = 0, source = "migration", message = "x", session, note = "contract validation" }},\n'
        )
    for marker in (
        '            ["/cp/sitemap"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpSitemapDigestAsync(10)).Summary, pages = (await reporter.BuildCpSitemapDigestAsync(10)).Pages, count = 0, source = "migration", message = "x", session, note = "contract validation" },\n',
        '            ["/cp/data-migrations"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpDataMigrationsDigestAsync(10)).Summary, migrations = (await reporter.BuildCpDataMigrationsDigestAsync(10)).Migrations, count = 0, source = "migration", message = "x", session, note = "contract validation" },\n',
    ):
        if marker in vt:
            vt = vt.replace(marker, marker + lines, 1)
            break
    val.write_text(vt, encoding="utf-8")
    print("validator ok")


def main() -> None:
    patch_contracts()
    patch_nav_links()
    write_blazor()
    patch_scripts_deploy()
    write_samples()
    patch_validator()
    print("wave23 wire complete")


if __name__ == "__main__":
    main()
