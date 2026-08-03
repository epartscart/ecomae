#!/usr/bin/env python3
"""Compare live PHP vs ASP.NET presentation HTML. Exit 1 unless ECOMAE_PRESENTATION_SOFT=1.

Honesty gate: ASP.NET product chrome is not PHP-parity until chrome-asset + structure
thresholds pass. Full interactive module UX remains a separate functionality gate.
"""
from __future__ import annotations

import json
import os
import re
import sys
import urllib.request
from dataclasses import dataclass, asdict, field
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
    has_inter: bool
    has_gtag: bool
    has_clarity: bool
    has_login_markers: bool
    has_php_cp_login: bool
    chrome_asset_hits: list[str] = field(default_factory=list)
    notes: str = ""


PAIRS = [
    ("cp-unauth", "https://www.ecomae.com/CP/", "https://www.ecomae.com/cp/login", 20000, [
        "epc_cp_login", "open+sans", "font-awesome", "authentication", "epc-cp-login",
    ]),
    ("erp-unauth", "https://www.ecomae.com/ERP/", "https://www.ecomae.com/erp/login", 80000, [
        "open+sans", "fraunces", "font-awesome", "ERP Finance", "epc-asp-login-erp",
    ]),
    ("bos-unauth", "https://www.ecomae.com/BOS/", "https://www.ecomae.com/bos/login", 20000, [
        "epc_bos_shell", "font-awesome", "epc-asp-login-bos", "BOS", "family=inter",
    ]),
    ("storefront", "https://epartscart.com/", "https://www.ecomae.com/storefront/app", 200000, [
        "pt+sans", "open+sans", "style_color", "gtag", "Search",
    ]),
]

PHP_ASSET_MARKERS = [
    "open+sans",
    "pt+sans",
    "family=inter",
    "fraunces",
    "font-awesome",
    "epc_cp_login",
    "epc_cp_ui_css",
    "epc_bos_shell",
    "erp_theme",
    "style_color",
    "epc-static.php",
    "googletagmanager",
    "gtag(",
    "ecomae-php-chrome-surface",
]


def fetch(url: str) -> tuple[int, str]:
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            body = resp.read().decode("utf-8", "replace")
            return resp.status, body
    except Exception as exc:  # noqa: BLE001
        return 0, f"ERROR: {exc}"


def has_open_sans(low: str) -> bool:
    return "open+sans" in low or "open sans" in low or "family=open+sans" in low


def has_pt_sans(low: str) -> bool:
    return "pt+sans" in low or "pt sans" in low or "family=pt+sans" in low


def has_inter(low: str) -> bool:
    return "family=inter" in low or "fonts.googleapis.com/css2?family=inter" in low


def chrome_hits(low: str) -> list[str]:
    return [m for m in PHP_ASSET_MARKERS if m.lower() in low]


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
        has_sora_console="ecomae · zero-php console" in low or ("family=sora" in low and "migration" in low and "console" in low),
        has_open_sans=has_open_sans(low),
        has_pt_sans=has_pt_sans(low),
        has_inter=has_inter(low),
        has_gtag="gtag(" in low or "googletagmanager" in low,
        has_clarity="clarity.ms" in low or "clarity(" in low,
        has_login_markers="epc-asp-login" in low or "epc-cp-login" in low or "login_form" in low or "epc-login-form" in low,
        has_php_cp_login=("epc-cp-login" in low and "authentication" in low) or "epc_cp_login" in low,
        chrome_asset_hits=chrome_hits(low),
    )


def main() -> int:
    results = []
    failures = []
    warnings = []
    for key, php_url, asp_url, min_php_bytes, required_markers in PAIRS:
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

        if asp.has_sora_console:
            failures.append(f"{key}: ASP.NET still wrapped in MigrationConsole/Sora chrome (not PHP presentation)")

        # Chrome asset parity (fonts/CSS/meta) — primary Batch 1 gate for login/shell bridges.
        missing = [m for m in required_markers if m.lower() not in a_body.lower()]
        # Allow synonym matches already captured in analyze flags
        if key == "cp-unauth" and asp.has_open_sans:
            missing = [m for m in missing if m != "open+sans"]
        if key == "erp-unauth" and asp.has_open_sans:
            missing = [m for m in missing if m != "open+sans"]
        if key == "bos-unauth" and asp.has_inter:
            missing = [m for m in missing if m != "family=inter"]
        if key == "storefront" and asp.has_pt_sans:
            missing = [m for m in missing if m != "pt+sans"]
        if key == "storefront" and asp.has_open_sans:
            missing = [m for m in missing if m != "open+sans"]
        if key == "storefront" and asp.has_gtag:
            missing = [m for m in missing if m != "gtag"]

        if missing:
            failures.append(f"{key}: ASP.NET missing chrome markers {missing}")

        if len(asp.chrome_asset_hits) < 3:
            failures.append(
                f"{key}: ASP.NET chrome-asset hits too low ({len(asp.chrome_asset_hits)}): {asp.chrome_asset_hits}"
            )

        # Size: warn when scaffold is thin; hard-fail only if extremely small vs PHP.
        if asp.status == 200 and php.bytes > 0:
            hard_floor = max(9000, int(php.bytes * 0.08))
            soft_floor = max(12000, int(php.bytes * 0.25))
            if asp.bytes < hard_floor:
                failures.append(
                    f"{key}: ASP.NET body {asp.bytes}B << PHP {php.bytes}B — presentation scaffold too thin (hard floor {hard_floor}B)"
                )
            elif asp.bytes < soft_floor:
                warnings.append(
                    f"{key}: ASP.NET body {asp.bytes}B < soft floor {soft_floor}B vs PHP {php.bytes}B — continue Batch 2 desktop chrome depth"
                )

        if key == "storefront" and php.has_gtag and not asp.has_gtag:
            failures.append("storefront: PHP has gtag/analytics; ASP.NET preview missing analytics tags")
        if key == "cp-unauth" and php.has_php_cp_login and not (asp.has_login_markers or asp.has_php_cp_login):
            failures.append("cp-unauth: ASP.NET login markers missing vs PHP CP login")
        if key in {"cp-unauth", "erp-unauth", "bos-unauth"} and not asp.has_open_sans and key != "bos-unauth":
            failures.append(f"{key}: ASP.NET missing Open Sans webfont link")
        if key == "bos-unauth" and not (asp.has_inter or asp.has_open_sans):
            failures.append("bos-unauth: ASP.NET missing Inter/Open Sans webfont link")

    # Hard functional truth — never greenlight PHP removal from presentation-only work.
    failures.append(
        "FUNCTIONALITY: interactive CP/ERP/BOS/storefront modules remain PHP-authoritative "
        "(hybrid deeplinks ≠ aspnet-complete; see docs/migration/inventory/MODULE_FUNCTION_PARITY_STATUS.md)"
    )

    soft = os.environ.get("ECOMAE_PRESENTATION_SOFT", "") == "1"
    # Presentation chrome can be "chrome-pass" while functionality failure remains.
    chrome_failures = [f for f in failures if not f.startswith("FUNCTIONALITY:")]
    status = "fail"
    if soft:
        status = "soft-fail" if failures else "pass"
    elif not chrome_failures:
        status = "chrome-pass-functionality-pending"
    else:
        status = "fail"

    out = {
        "status": status,
        "readyForPhpRemoval": False,
        "failureCount": len(failures),
        "warningCount": len(warnings),
        "failures": failures,
        "warnings": warnings,
        "pages": results,
        "note": (
            "Do not remove PHP. Batch 1 targets chrome asset/font/analytics parity for login/shell bridges. "
            "Full desktop presentation + interactive modules remain Batch 2+."
        ),
    }

    root = Path(__file__).resolve().parents[1]
    evidence = root / "docs" / "migration" / "evidence" / "presentation" / "php-vs-aspnet-recheck.json"
    evidence.parent.mkdir(parents=True, exist_ok=True)
    evidence.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": status, "failureCount": len(failures), "warningCount": len(warnings), "failures": failures, "warnings": warnings}, indent=2))
    print(f"Wrote {evidence}")

    # Exit 0 only for soft mode or chrome-pass (functionality failure still listed but Batch 1 chrome ok).
    if soft:
        return 0
    if status == "chrome-pass-functionality-pending":
        return 0
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
