#!/usr/bin/env python3
"""Compare live PHP vs ASP.NET presentation HTML. Exit 1 unless ECOMAE_PRESENTATION_SOFT=1.

Honesty gate: ASP.NET product chrome is not PHP-parity until size/font/structure thresholds pass.
"""
from __future__ import annotations

import json
import os
import re
import sys
import urllib.request
from dataclasses import dataclass, asdict
from pathlib import Path

UA = "ecomae-presentation-parity-probe/1.0"


@dataclass
class Page:
    name: str
    url: str
    status: int
    bytes: int
    title: str
    has_sora_console: bool
    has_open_sans: bool
    has_pt_sans: bool
    has_gtag: bool
    has_clarity: bool
    has_login_markers: bool
    has_php_cp_login: bool
    notes: str = ""


PAIRS = [
    ("cp-unauth", "https://www.ecomae.com/CP/", "https://www.ecomae.com/cp/login", 20000, ["epc-cp-login", "authentication"]),
    ("erp-unauth", "https://www.ecomae.com/ERP/", "https://www.ecomae.com/erp/login", 80000, ["ERP Finance", "epc-erp"]),
    ("bos-unauth", "https://www.ecomae.com/BOS/", "https://www.ecomae.com/bos/login", 20000, ["bos-", "BOS"]),
    ("storefront", "https://epartscart.com/", "https://www.ecomae.com/storefront/app", 200000, ["header", "Search"]),
]


def fetch(url: str) -> tuple[int, str]:
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            body = resp.read().decode("utf-8", "replace")
            return resp.status, body
    except Exception as exc:  # noqa: BLE001
        return 0, f"ERROR: {exc}"


def analyze(name: str, url: str, body: str, status: int) -> Page:
    title_m = re.search(r"<title>([^<]+)", body, re.I)
    title = title_m.group(1).strip() if title_m else ""
    low = body.lower()
    return Page(
        name=name,
        url=url,
        status=status,
        bytes=len(body.encode("utf-8", "replace")),
        title=title,
        has_sora_console="family=sora" in low or "ecomae · zero-php console" in low,
        has_open_sans="open sans" in low,
        has_pt_sans="pt sans" in low or "family=pt+sans" in low,
        has_gtag="gtag(" in low or "googletagmanager" in low,
        has_clarity="clarity.ms" in low or "clarity(" in low,
        has_login_markers="epc-asp-login" in low or "epc-cp-login" in low or "login_form" in low,
        has_php_cp_login="epc-cp-login" in low and "authentication" in low,
    )


def main() -> int:
    results = []
    failures = []
    for key, php_url, asp_url, min_php_bytes, _markers in PAIRS:
        p_status, p_body = fetch(php_url)
        a_status, a_body = fetch(asp_url)
        php = analyze(f"{key}/php", php_url, p_body, p_status)
        asp = analyze(f"{key}/aspnet", asp_url, a_body, a_status)
        results.extend([asdict(php), asdict(asp)])

        if php.status != 200:
            failures.append(f"{key}: PHP HTTP {php.status}")
        if asp.status != 200:
            failures.append(f"{key}: ASP.NET HTTP {asp.status} (route missing or not shadowed)")
        if php.bytes < min_php_bytes:
            failures.append(f"{key}: PHP unexpectedly small ({php.bytes} < {min_php_bytes})")
        # ASP.NET must not be the migration console wrapper for product surfaces
        if asp.has_sora_console:
            failures.append(f"{key}: ASP.NET still wrapped in MigrationConsole/Sora chrome (not PHP presentation)")
        # Size heuristic: login/home scaffolds under 25% of PHP are not full presentation
        if asp.status == 200 and php.bytes > 0 and asp.bytes < max(12000, int(php.bytes * 0.25)):
            failures.append(
                f"{key}: ASP.NET body {asp.bytes}B << PHP {php.bytes}B — full-page presentation not matched"
            )
        if key == "storefront" and php.has_gtag and not asp.has_gtag:
            failures.append("storefront: PHP has gtag/analytics; ASP.NET preview missing analytics tags")
        if key == "cp-unauth" and php.has_php_cp_login and not asp.has_login_markers:
            failures.append("cp-unauth: ASP.NET login markers missing vs PHP CP login")

    # Hard functional truth
    failures.append(
        "FUNCTIONALITY: interactive CP/ERP/BOS/storefront modules remain PHP-only "
        "(see docs/migration/inventory/MODULE_FUNCTION_PARITY_STATUS.md) — digests ≠ product UX"
    )

    soft = os.environ.get("ECOMAE_PRESENTATION_SOFT", "") == "1"
    out = {
        "status": "fail" if failures and not soft else ("soft-fail" if failures else "pass"),
        "readyForPhpRemoval": False,
        "failureCount": len(failures),
        "failures": failures,
        "pages": results,
        "note": "Do not remove PHP. Hybrid ASP.NET previews are not full PHP presentation/functionality parity.",
    }

    root = Path(__file__).resolve().parents[1]
    evidence = root / "docs" / "migration" / "evidence" / "presentation" / "php-vs-aspnet-recheck.json"
    evidence.parent.mkdir(parents=True, exist_ok=True)
    evidence.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out, indent=2))
    print(f"Wrote {evidence}", file=sys.stderr)

    if failures and not soft:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
