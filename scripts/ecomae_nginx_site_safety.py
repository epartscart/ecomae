#!/usr/bin/env python3
"""Classify CloudPanel nginx site conf targets for ASP.NET shadow installs.

Live tenant / industry presentation (frontend, CP, ERP, BOS) must remain PHP.
Only the platform www host is the default exact-route shadow target.
"""
from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

PLATFORM_BASENAMES = frozenset(
    {
        "www.ecomae.com.conf",
        "www.ecomae.com",
    }
)

# Dedicated Super CP host may receive diagnostics/API exact-routes only when
# explicitly pointed — never product chrome / presentation apps by default.
PLATFORM_OPTIONAL_BASENAMES = frozenset(
    {
        "cp.ecomae.com.conf",
        "cp.ecomae.com",
    }
)

TENANT_BASENAME_MARKERS = (
    "epartscart",
    "electronicae",
    "stylenlook",
    "thejewellerytrend",
    "taxofinca",
)

# Named live production tenants — presentation shadows are NEVER allowed.
# Product chrome (storefront / CP / ERP) must remain byte-identical PHP:
# theme, colouring, structure, fonts, hero/splash, fields — no ASP.NET hybrid.
LIVE_PRODUCTION_TENANT_MARKERS = frozenset(TENANT_BASENAME_MARKERS)

LIVE_PRODUCTION_TENANT_HOSTS = (
    "epartscart.com",
    "www.epartscart.com",
    "www.electronicae.com",
    "electronicae.com",
    "www.stylenlook.com",
    "stylenlook.com",
    "www.thejewellerytrend.com",
    "thejewellerytrend.com",
    "www.taxofinca.com",
    "taxofinca.com",
)

# Full industry showcase set (epc_industry_seo_host_map / EcomaeIndustryShowcaseHosts).
# Any other *.ecomae.com (except www/cp) also classifies as industry below.
INDUSTRY_ECOMAE_MARKERS = (
    "agriculture.ecomae.com",
    "automotive.ecomae.com",
    "beauty.ecomae.com",
    "cleaning.ecomae.com",
    "construction.ecomae.com",
    "education.ecomae.com",
    "electronics.ecomae.com",
    "energy.ecomae.com",
    "fashion.ecomae.com",
    "finance.ecomae.com",
    "food.ecomae.com",
    "healthcare.ecomae.com",
    "homeliving.ecomae.com",
    "hospitality.ecomae.com",
    "jewellery.ecomae.com",
    "logistics.ecomae.com",
    "manufacturing.ecomae.com",
    "media.ecomae.com",
    "nonprofit.ecomae.com",
    "pet.ecomae.com",
    "printing.ecomae.com",
    "professional.ecomae.com",
    "rental.ecomae.com",
    "retail.ecomae.com",
    "security.ecomae.com",
    "sports.ecomae.com",
    "technology.ecomae.com",
    "wholesale.ecomae.com",
)

BROAD_LOCATION_PATTERNS = (
    r"(?m)^[ \t]*location\s+/cp\s*\{",
    r"(?m)^[ \t]*location\s+/erp\s*\{",
    r"(?m)^[ \t]*location\s+/bos\s*\{",
    r"(?m)^[ \t]*location\s+/storefront\s*\{",
    r"(?m)^[ \t]*location\s+/api\s*\{",
    r"(?m)^[ \t]*location\s+/CP\s*\{",
    r"(?m)^[ \t]*location\s+/ERP\s*\{",
    r"(?m)^[ \t]*location\s+/BOS\s*\{",
)


_SERVER_NAME_RE = __import__("re").compile(r"(?im)^\s*server_name\s+([^;]+);")


def _server_name_tokens(conf_path: str | Path) -> list[str]:
    try:
        text = Path(conf_path).read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return []
    names: list[str] = []
    for m in _SERVER_NAME_RE.finditer(text):
        for tok in m.group(1).split():
            t = tok.strip().lower()
            if t and t != "_":
                names.append(t)
    return names


def classify_site_conf(conf_path: str | Path) -> str:
    """Return platform | platform-optional | tenant | industry | unknown."""
    name = Path(conf_path).name.lower()
    if name in {n.lower() for n in PLATFORM_BASENAMES}:
        return "platform"
    if name in {n.lower() for n in PLATFORM_OPTIONAL_BASENAMES}:
        return "platform-optional"
    for marker in TENANT_BASENAME_MARKERS:
        if marker in name:
            return "tenant"
    # Content-based: dedicated or multi-host conf whose server_name is a live tenant.
    tokens = _server_name_tokens(conf_path)
    for tok in tokens:
        for host in LIVE_PRODUCTION_TENANT_HOSTS:
            if tok == host or tok.endswith("." + host.removeprefix("www.")):
                return "tenant"
        for marker in LIVE_PRODUCTION_TENANT_MARKERS:
            if marker in tok and tok.endswith(".com"):
                return "tenant"
    for marker in INDUSTRY_ECOMAE_MARKERS:
        if marker in name:
            return "industry"
    # wildcard-ecomae / *.ecomae.com showcase — industry, never "epartscart by filename".
    if name.startswith("wildcard-ecomae") or any(
        tok == "*.ecomae.com" or tok.endswith(".ecomae.com") for tok in tokens
    ):
        if name not in {n.lower() for n in PLATFORM_BASENAMES}:
            # Pure industry wildcard without tenant server_name
            if not any("epartscart" in t for t in tokens):
                return "industry"
    # Any other *.ecomae.com site that is not www/cp is treated as industry/showcase.
    if name.endswith(".ecomae.com.conf") or name.endswith(".ecomae.com"):
        return "industry"
    return "unknown"


def assert_shadow_target_allowed(
    conf_path: str | Path,
    *,
    purpose: str = "exact-route",
    confirm_tenant: str | None = None,
    confirm_tenant_presentation: str | None = None,
) -> None:
    """Raise SystemExit if the nginx site conf must not receive this shadow.

    purpose:
      - exact-route: digests / catalog / price / health (tenant requires confirm)
      - presentation: Blazor /app|/login (tenant/industry HARD refuse unless special flag)
    """
    kind = classify_site_conf(conf_path)
    confirm_tenant = confirm_tenant if confirm_tenant is not None else os.environ.get(
        "ECOMAE_CONFIRM_TENANT_HOST_SHADOW", ""
    )
    confirm_tenant_presentation = (
        confirm_tenant_presentation
        if confirm_tenant_presentation is not None
        else os.environ.get("ECOMAE_CONFIRM_TENANT_PRESENTATION_SHADOW", "")
    )
    confirm_live_parity = os.environ.get(
        "ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW", ""
    )

    if kind == "platform":
        return

    name = Path(conf_path).name.lower()
    tokens = _server_name_tokens(conf_path)
    is_named_live = any(marker in name for marker in LIVE_PRODUCTION_TENANT_MARKERS) or any(
        any(marker in tok for marker in LIVE_PRODUCTION_TENANT_MARKERS) for tok in tokens
    )

    if purpose == "presentation":
        if is_named_live:
            if confirm_live_parity == "YES":
                print(
                    f"WARNING: presentation/login parity shadow on NAMED live tenant "
                    f"{conf_path} (ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES). "
                    "TARGET is 100% ASP.NET / 0 PHP — but same-to-same dual-sample must be proven. "
                    "Never broad location / /cp /erp. Prefer www.ecomae.com until parity green.",
                    file=sys.stderr,
                )
                return
            raise SystemExit(
                f"ERROR: refusing presentation/login shadow on live production tenant "
                f"site conf {conf_path} (parity gate). "
                "epartscart / electronicae / stylenlook / thejewellerytrend / taxofinca "
                "stay PHP-primary until ASP.NET same-to-same evidence. "
                "Unlock ONLY with ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES "
                "(not ECOMAE_CONFIRM_TENANT_PRESENTATION_SHADOW). "
                "Default scaffolding host remains www.ecomae.com."
            )
        if kind in {"tenant", "industry", "unknown", "platform-optional"}:
            if confirm_tenant_presentation == "YES" and kind in {"tenant", "industry"}:
                print(
                    f"WARNING: presentation shadow on {kind} host {conf_path} "
                    "(ECOMAE_CONFIRM_TENANT_PRESENTATION_SHADOW=YES). "
                    "Live tenant UI may change — prefer platform www only.",
                    file=sys.stderr,
                )
                return
            raise SystemExit(
                f"ERROR: refusing presentation/login shadow on {kind} site conf {conf_path}. "
                "Use default ECOMAE_NGINX_SITE_CONF=/etc/nginx/sites-enabled/www.ecomae.com.conf. "
                "Override with ECOMAE_CONFIRM_TENANT_PRESENTATION_SHADOW=YES (non-named tenants). "
                "Named live tenants require ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES."
            )
        return

    # exact-route purpose
    if kind == "platform-optional":
        if confirm_tenant == "YES":
            print(
                f"WARNING: exact-route shadow on optional platform host {conf_path}",
                file=sys.stderr,
            )
            return
        raise SystemExit(
            f"ERROR: refusing exact-route shadow on {conf_path} without "
            "ECOMAE_CONFIRM_TENANT_HOST_SHADOW=YES. Default target is www.ecomae.com only."
        )

    if is_named_live:
        if confirm_live_parity == "YES":
            print(
                f"WARNING: exact-route ASP.NET parity shadow on NAMED live tenant "
                f"{conf_path} (ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES). "
                "End-state is 100% ASP.NET / 0 PHP. Exact-route only — never broad cutover. "
                "Require dual-sample same-to-same before traffic promotion.",
                file=sys.stderr,
            )
            return
        raise SystemExit(
            f"ERROR: refusing ASP.NET exact-route shadow on live production tenant "
            f"site conf {conf_path} (parity gate — not a permanent PHP ban). "
            "Named live tenants stay PHP-primary until ASP.NET same-to-same evidence. "
            "Unlock exact-route parity shadows with "
            "ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES. "
            "ECOMAE_CONFIRM_TENANT_HOST_SHADOW alone is not enough for named live tenants. "
            "Default scaffolding host remains www.ecomae.com."
        )

    if kind in {"tenant", "industry", "unknown"}:
        if confirm_tenant == "YES":
            print(
                f"WARNING: exact-route shadow on {kind} host {conf_path}. "
                "Exact-route only — never broad locations. Target end-state: ASP.NET.",
                file=sys.stderr,
            )
            return
        raise SystemExit(
            f"ERROR: refusing ASP.NET shadow install on {kind} site conf {conf_path}. "
            "Shadows default to www.ecomae.com only. "
            "Set ECOMAE_CONFIRM_TENANT_HOST_SHADOW=YES only for an approved exact-route on that host. "
            "Named live tenants require ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES."
        )


def scan_broad_cutovers(conf_text: str) -> list[str]:
    import re

    hits: list[str] = []
    for pattern in BROAD_LOCATION_PATTERNS:
        for m in re.finditer(pattern, conf_text):
            hits.append(m.group(0).strip())
    return hits


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("conf", help="Path to nginx site conf")
    parser.add_argument(
        "--purpose",
        choices=("exact-route", "presentation"),
        default="exact-route",
    )
    parser.add_argument("--classify-only", action="store_true")
    parser.add_argument("--scan-broad", action="store_true", help="Scan conf for broad location cutovers")
    args = parser.parse_args(argv)

    kind = classify_site_conf(args.conf)
    print(f"class={kind}")
    print(f"conf={args.conf}")
    if args.classify_only:
        return 0

    if args.scan_broad:
        text = Path(args.conf).read_text(encoding="utf-8", errors="replace")
        hits = scan_broad_cutovers(text)
        if hits:
            print("BROAD_CUTOVER_HITS:")
            for h in hits:
                print(f"  {h}")
            return 1
        print("BROAD_CUTOVER_HITS: none")
        return 0

    assert_shadow_target_allowed(args.conf, purpose=args.purpose)
    print("allowed=yes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
