#!/usr/bin/env python3
"""Fail-closed *.ecomae.com industry frontend parity gate.

Proves industry showcase hosts serve PHP product chrome (same look source),
catalogues ASP.NET /marketing/industries preview on www, and refuses cutover.
Never invents cutoverAllowed=true / RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

# Mirrored from EcomaeIndustryShowcaseHosts / epc_industry_seo_host_map + live industries page.
INDUSTRY_HOSTS = (
    ("agriculture", "Agriculture"),
    ("automotive", "Automotive"),
    ("beauty", "Beauty"),
    ("cleaning", "Cleaning"),
    ("construction", "Construction"),
    ("education", "Education"),
    ("electronics", "Electronics"),
    ("energy", "Energy"),
    ("fashion", "Fashion"),
    ("finance", "Financial"),
    ("food", "Food"),
    ("healthcare", "Healthcare"),
    ("homeliving", "Home"),
    ("hospitality", "Hospitality"),
    ("jewellery", "Jewellery"),
    ("logistics", "Logistics"),
    ("manufacturing", "Manufacturing"),
    ("media", "Media"),
    ("nonprofit", "Nonprofit"),
    ("pet", "Pet"),
    ("printing", "Printing"),
    ("professional", "Professional"),
    ("rental", "Rental"),
    ("retail", "Retail"),
    ("security", "Security"),
    ("sports", "Sports"),
    ("technology", "IT"),
    ("wholesale", "Wholesale"),
)

WWW_ASPNET_PREVIEW = (
    "https://www.ecomae.com/marketing/app",
    "https://www.ecomae.com/marketing/industries",
)

WWW_PHP_AUTHORITY = (
    "https://www.ecomae.com/",
    "https://www.ecomae.com/platform/industries",
)

FORBIDDEN_ON_INDUSTRY = (
    "/storefront/app",
    "/marketing/app",
    "/health",
)

ASPNET_STRONG = (
    "_framework/blazor",
    "blazor.web.js",
    "ecomae-php-chrome-surface",
)


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def assert_cutover_false(doc: dict, rel: str, errors: list[str]) -> None:
    if doc.get("cutoverAllowed") is True:
        errors.append(f"{rel}: cutoverAllowed must be false")
    if doc.get("readyForPhpRemoval") is True or doc.get("readyToRemovePhp") is True:
        errors.append(f"{rel}: readyForPhpRemoval must be false")


def probe(url: str, timeout: float = 18.0) -> dict:
    req = urllib.request.Request(
        url,
        headers={"User-Agent": "ecomae-industry-frontend-parity/1.0"},
        method="GET",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            body = resp.read()
            final = resp.geturl()
            status = resp.status
            ctype = resp.headers.get("Content-Type", "")
    except urllib.error.HTTPError as e:
        body = e.read() if e.fp else b""
        final = url
        status = e.code
        ctype = e.headers.get("Content-Type", "") if e.headers else ""
    except Exception as exc:  # noqa: BLE001
        return {
            "url": url,
            "error": str(exc)[:160],
            "stack": "unreachable",
            "result": "fail",
            "httpStatus": 0,
            "bytes": 0,
        }

    text = body.decode("utf-8", errors="ignore")
    low = text.lower()
    strong_asp = any(m in low for m in ASPNET_STRONG)
    phpish = any(
        x in low
        for x in (
            "epm-hub",
            "epc-static.php",
            "templates/nero",
            "bootstrap_admin",
            "ecomae platform",
            "data-epc-industry",
        )
    )
    if strong_asp and not phpish:
        stack = "aspnet"
    elif phpish or (status == 200 and "text/html" in ctype and len(body) > 20_000):
        stack = "php-html"
    elif status in (404, 403):
        stack = "absent"
    else:
        stack = "other"

    title_m = re.search(r"<title[^>]*>(.*?)</title>", text, flags=re.I | re.S)
    title = title_m.group(1).strip()[:140] if title_m else ""
    return {
        "url": url,
        "finalUrl": final,
        "httpStatus": status,
        "bytes": len(body),
        "contentType": ctype,
        "stack": stack,
        "strongAspNet": strong_asp,
        "title": title,
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--live", action="store_true")
    ap.add_argument(
        "--out",
        default="docs/migration/evidence/tenant-safety/industry-ecomae-frontend-parity.json",
    )
    ap.add_argument(
        "--matrix-out",
        default="docs/migration/evidence/tenant-safety/industry-ecomae-coverage-matrix.json",
    )
    args = ap.parse_args()

    root = Path(args.root).resolve()
    errors: list[str] = []
    warnings: list[str] = []

    # Contract: ASP.NET catalog + industries overview must exist
    hosts_cs = root / "aspnet/src/EcomAE.Platform/Presentation/EcomaeIndustryShowcaseHosts.cs"
    overview = root / "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeIndustriesOverview.razor"
    industries_app = root / "aspnet/src/EcomAE.Platform/Components/Pages/MarketingIndustriesApp.razor"
    marketing_app = root / "aspnet/src/EcomAE.Platform/Components/Pages/MarketingPreviewApp.razor"
    for path in (hosts_cs, overview, industries_app, marketing_app):
        if not path.is_file():
            errors.append(f"missing {path.relative_to(root)}")

    if hosts_cs.is_file():
        text = hosts_cs.read_text(encoding="utf-8")
        for slug, _ in INDUSTRY_HOSTS:
            if f'"{slug}"' not in text and f'("{slug}"' not in text:
                # pattern new("slug"
                if f'new("{slug}"' not in text:
                    errors.append(f"EcomaeIndustryShowcaseHosts missing slug {slug}")

    if overview.is_file():
        ov = overview.read_text(encoding="utf-8")
        if "EcomaeIndustryShowcaseHosts.All" not in ov:
            errors.append("PhpEcomaeIndustriesOverview must render full industry host grid")

    nginx = root / "scripts/ecomae_nginx_site_safety.py"
    if nginx.is_file():
        ntext = nginx.read_text(encoding="utf-8")
        for slug, _ in INDUSTRY_HOSTS[:9]:  # core markers at minimum
            if f"{slug}.ecomae.com" not in ntext:
                warnings.append(f"nginx industry markers missing {slug}.ecomae.com")

    host_rows = []
    live_probes: list[dict] = []
    php_home_ok = 0

    if args.live:
        for slug, title_needle in INDUSTRY_HOSTS:
            home = f"https://{slug}.ecomae.com/"
            result = probe(home)
            live_probes.append({**result, "slug": slug, "probeKind": "industry-home"})
            row_errors: list[str] = []
            if result.get("stack") == "unreachable":
                row_errors.append(f"{slug}: unreachable")
            elif result.get("strongAspNet"):
                row_errors.append(f"{slug}: ASP.NET Blazor markers on industry frontend")
            elif result.get("httpStatus") != 200:
                row_errors.append(f"{slug}: HTTP {result.get('httpStatus')} on /")
            elif result.get("stack") != "php-html":
                row_errors.append(f"{slug}: expected php-html, got {result.get('stack')}")
            elif title_needle.lower() not in (result.get("title") or "").lower():
                # soft — title may vary; warn only if ecomae missing
                if "ecomae" not in (result.get("title") or "").lower():
                    row_errors.append(f"{slug}: title missing ecomae/industry signal")
                else:
                    warnings.append(f"{slug}: title needle {title_needle!r} not in {result.get('title')!r}")
            if not row_errors:
                php_home_ok += 1
            errors.extend(row_errors)

            # CP + ERP should be PHP login/chrome, not Blazor
            for path, kind in (("/CP/", "cp"), ("/ERP/", "erp")):
                url = f"https://{slug}.ecomae.com{path}"
                r = probe(url)
                live_probes.append({**r, "slug": slug, "probeKind": kind})
                if r.get("strongAspNet") and r.get("httpStatus") == 200:
                    errors.append(f"{slug}{path}: ASP.NET Blazor on industry host")

            # ASP.NET preview paths on industry hosts — must not replace product `/`
            # and require same-to-same dual-sample before any cutover.
            unexpected_shadows: list[str] = []
            for path in FORBIDDEN_ON_INDUSTRY:
                url = f"https://{slug}.ecomae.com{path}"
                r = probe(url)
                live_probes.append({**r, "slug": slug, "probeKind": "aspnet-preview-check"})
                # Industry wildcard currently serves Blazor storefront SSR (~18KB) on /storefront/app.
                # Product `/` must stay PHP; these previews need same-to-same look dual-sample.
                if r.get("httpStatus") == 200 and (
                    r.get("strongAspNet")
                    or (
                        path == "/storefront/app"
                        and isinstance(r.get("bytes"), int)
                        and 10_000 <= int(r["bytes"]) <= 40_000
                    )
                ):
                    unexpected_shadows.append(path)
                    warnings.append(
                        f"{slug}{path}: ASP.NET preview on industry host "
                        f"(bytes={r.get('bytes')}) — dual-sample vs PHP `/` look before cutover"
                    )

            host_rows.append(
                {
                    "slug": slug,
                    "homeUrl": home,
                    "status": "fail" if row_errors else "php-live-ok",
                    "errors": row_errors,
                    "aspNetPreviewPaths": unexpected_shadows,
                    "httpStatus": result.get("httpStatus"),
                    "bytes": result.get("bytes"),
                    "title": result.get("title"),
                }
            )

        # www PHP authority
        for url in WWW_PHP_AUTHORITY:
            r = probe(url)
            live_probes.append({**r, "probeKind": "www-php"})
            if r.get("strongAspNet"):
                errors.append(f"www PHP authority has Blazor: {url}")
            elif r.get("httpStatus") != 200:
                errors.append(f"www PHP authority HTTP {r.get('httpStatus')}: {url}")

        # www ASP.NET marketing preview — required for look dual-sample path
        aspnet_preview_live = 0
        for url in WWW_ASPNET_PREVIEW:
            r = probe(url)
            live_probes.append({**r, "probeKind": "www-aspnet-preview"})
            if r.get("strongAspNet") or (
                r.get("httpStatus") == 200 and "blazor" in (r.get("title") or "").lower()
            ):
                aspnet_preview_live += 1
            elif r.get("httpStatus") == 200 and r.get("bytes", 0) < 50_000 and "404" not in (
                r.get("title") or ""
            ):
                # small Blazor SSR pages are OK
                if "page not found" not in (r.get("title") or "").lower():
                    aspnet_preview_live += 1
            else:
                warnings.append(
                    f"ASP.NET marketing preview not live yet: {url} "
                    f"(status={r.get('httpStatus')} title={r.get('title')!r}) — install nginx exact-route shadow"
                )
        if aspnet_preview_live == 0:
            warnings.append(
                "www /marketing/app and /marketing/industries shadows not installed — "
                "cannot dual-sample industry look on ASP.NET until CloudPanel shadow install"
            )
    else:
        for slug, _ in INDUSTRY_HOSTS:
            host_rows.append(
                {
                    "slug": slug,
                    "homeUrl": f"https://{slug}.ecomae.com/",
                    "status": "catalogued-pending-live",
                    "errors": [],
                }
            )
        warnings.append("live probe skipped (set ECOMAE_INDUSTRY_LIVE=1 / --live)")
        warnings.append("ASP.NET /marketing/* exact-route shadows must be installed on www for look dual-sample")

    warnings.append("Industry *.ecomae.com stay PHP-primary until dual-sample + human approval")
    warnings.append("aspNetInteractiveComplete=0 — never invent RELEASE_OWNER_APPROVAL.md")

    matrix = {
        "role": "industry-ecomae-frontend-coverage-matrix",
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "generatedAtUnix": int(time.time()),
        "hostCount": len(INDUSTRY_HOSTS),
        "phpHomeOk": php_home_ok if args.live else None,
        "hosts": host_rows,
        "aspNetPreviewRoutes": list(WWW_ASPNET_PREVIEW),
        "phpAuthorityRoutes": list(WWW_PHP_AUTHORITY),
        "built": [
            f"Catalogued {len(INDUSTRY_HOSTS)} industry showcase hosts (*.ecomae.com)",
            "ASP.NET EcomaeIndustryShowcaseHosts + MarketingIndustriesApp grid",
            "Nginx industry classification for *.ecomae.com",
        ],
        "pendingBeforePhpRemoval": [
            "Install www exact-route shadows for /marketing/app and /marketing/industries",
            "Dual-sample PHP /platform/industries + each industry home vs ASP.NET preview look",
            "Industry hosts currently expose /storefront/app Blazor SSR — must match PHP `/` look or be removed until approved",
            "Same epm-hub / industry hero tokens on ASP.NET as PHP",
            "Human RELEASE_OWNER_APPROVAL.md before any industry host cutover",
        ],
        "ok": not errors,
        "errors": errors,
        "warnings": warnings,
        "note": (
            "There is no industry.ecomae.com host — industries are [slug].ecomae.com. "
            "Trading maps to wholesale.ecomae.com. Live chrome stays PHP; ASP.NET compare on www only."
        ),
    }

    suite = {
        "role": "industry-ecomae-frontend-parity-suite",
        "generatedAtUnix": int(time.time()),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "readyToRemovePhp": False,
        "aspNetInteractiveComplete": 0,
        "ok": not errors,
        "errorCount": len(errors),
        "warningCount": len(warnings),
        "errors": errors,
        "warnings": warnings,
        "hostCount": len(INDUSTRY_HOSTS),
        "phpHomeOk": php_home_ok if args.live else None,
        "liveProbeCount": len(live_probes),
        "liveProbeFails": sum(1 for p in live_probes if p.get("result") == "fail"),
        "liveProbes": live_probes,
        "matrixRef": args.matrix_out,
        "phpRemovalBlockedReason": (
            "Industry *.ecomae.com frontends remain PHP-primary until www marketing "
            "ASP.NET dual-sample same-to-same + human approval."
        ),
        "note": "Fail-closed industry frontend gate for *.ecomae.com showcase hosts.",
    }

    out_path = root / args.out
    matrix_path = root / args.matrix_out
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(suite, indent=2) + "\n", encoding="utf-8")
    matrix_path.write_text(json.dumps(matrix, indent=2) + "\n", encoding="utf-8")

    print(
        json.dumps(
            {
                "ok": suite["ok"],
                "errors": len(errors),
                "warnings": len(warnings),
                "hostCount": len(INDUSTRY_HOSTS),
                "phpHomeOk": suite["phpHomeOk"],
                "liveProbes": len(live_probes),
                "phpRemovalAllowed": False,
                "out": str(out_path),
                "matrix": str(matrix_path),
            },
            indent=2,
        )
    )
    for e in errors[:40]:
        print(f"  ERROR: {e}", file=sys.stderr)
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
