#!/usr/bin/env python3
"""Compare ASP.NET login-bridge cookie / probe samples against PHP-compatible contracts.

Does NOT authorize PHP removal. Always emits cutoverAllowed=false.

Sample JSON shape (see docs/migration/evidence/login-session-bridge/):
{
  "surface": "cp"|"erp"|"bos"|"storefront",
  "setCookie": ["admin_session=...; path=/; httponly", ...],
  "probe": {"kind": "Admin"|"Customer"|..., "has_backend_access": true/false},
  "phpReference": optional notes / cookie names from PHP login
}
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ADMIN_COOKIES = ("admin_session", "admin_u_id")
CUSTOMER_COOKIES = ("session", "u_id")


def cookie_names(set_cookie) -> set[str]:
    names: set[str] = set()
    if not set_cookie:
        return names
    if isinstance(set_cookie, str):
        set_cookie = [set_cookie]
    for header in set_cookie:
        part = str(header).split(";", 1)[0]
        if "=" in part:
            names.add(part.split("=", 1)[0].strip())
    return names


def evaluate_sample(doc: dict) -> dict:
    surface = str(doc.get("surface") or "cp").lower()
    names = cookie_names(doc.get("setCookie") or doc.get("set_cookie") or [])
    probe = doc.get("probe") if isinstance(doc.get("probe"), dict) else {}
    kind = str(probe.get("kind") or probe.get("Kind") or "")
    errors: list[str] = []
    warnings: list[str] = []

    if surface == "storefront":
        for n in CUSTOMER_COOKIES:
            if n not in names and not kind:
                # Allow probe-only samples (cookies already in jar).
                pass
        if names and not CUSTOMER_COOKIES[0] in names:
            errors.append("storefront sample missing session cookie name")
        if names and "admin_session" in names:
            errors.append("storefront sample must not set admin_session")
        if kind and kind.lower() != "customer":
            errors.append(f"storefront probe kind expected Customer, got {kind}")
    else:
        if names and "admin_session" not in names and not kind:
            errors.append(f"{surface} sample missing admin_session cookie name")
        if names and "session" in names and "admin_session" not in names:
            warnings.append(f"{surface} sample has customer session cookie without admin_session")
        if kind and kind.lower() != "admin":
            errors.append(f"{surface} probe kind expected Admin, got {kind}")
        if surface == "bos":
            warnings.append(
                "BOS decision: admin cookies ≠ PHP $_SESSION epc_bos_context; /BOS/ remains PHP-authoritative"
            )

    if doc.get("readyForPhpRemoval") is True or doc.get("cutoverAllowed") is True:
        errors.append("samples must not claim readyForPhpRemoval/cutoverAllowed")

    return {
        "surface": surface,
        "cookieNames": sorted(names),
        "probeKind": kind or None,
        "ok": not errors,
        "errors": errors,
        "warnings": warnings,
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--samples-dir",
        type=Path,
        default=Path("docs/migration/evidence/login-session-bridge"),
        help="Directory containing *.json samples",
    )
    ap.add_argument("--out", type=Path, default=None, help="Optional result JSON path")
    ap.add_argument(
        "--contract-only",
        action="store_true",
        help="Require php-{surface}-login-bridge.json migration-contract-golden for each aspnet surface",
    )
    args = ap.parse_args()

    samples_dir: Path = args.samples_dir
    if not samples_dir.is_dir():
        print(f"FAIL: samples dir missing: {samples_dir}", file=sys.stderr)
        return 2

    results = []
    skip_names = {
        "README.md",
        "compare-result.json",
        "OPERATOR_VERIFY.md",
    }
    for path in sorted(samples_dir.glob("*.json")):
        if path.name in skip_names or path.name.startswith("compare-"):
            continue
        if path.name.endswith(".result.json"):
            continue
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc:  # noqa: BLE001
            results.append({"file": path.name, "ok": False, "errors": [f"parse error: {exc}"]})
            continue
        if not isinstance(doc, dict):
            results.append({"file": path.name, "ok": False, "errors": ["not a JSON object"]})
            continue
        # Skip meta/result envelopes / generator outputs
        role = str(doc.get("role") or "")
        if role in {"compare-result", "login-cookie-contract-sample-generator"}:
            continue
        if "samples" in doc and "cutoverAllowed" in doc and role == "compare-result":
            continue
        evaluated = evaluate_sample(doc)
        evaluated["file"] = path.name
        results.append(evaluated)

    contract_pairs = 0
    contract_pairs_ok = 0
    missing_php: list[str] = []
    if args.contract_only:
        for surface in ("cp", "erp", "bos", "storefront"):
            contract_pairs += 1
            asp = samples_dir / f"aspnet-{surface}-login-bridge.json"
            php = samples_dir / f"php-{surface}-login-bridge.json"
            if not asp.is_file():
                missing_php.append(surface)
                results.append(
                    {
                        "file": asp.name,
                        "ok": False,
                        "errors": [f"missing aspnet-{surface}-login-bridge.json"],
                    }
                )
                continue
            if not php.is_file():
                missing_php.append(surface)
                results.append(
                    {
                        "file": php.name,
                        "ok": False,
                        "errors": [f"missing php-{surface}-login-bridge.json"],
                    }
                )
                continue
            try:
                php_doc = json.loads(php.read_text(encoding="utf-8"))
            except Exception as exc:  # noqa: BLE001
                results.append({"file": php.name, "ok": False, "errors": [f"parse error: {exc}"]})
                continue
            errs: list[str] = []
            if php_doc.get("dualSampleBaseline") != "migration-contract-golden":
                errs.append("expected dualSampleBaseline=migration-contract-golden")
            if php_doc.get("cutoverAllowed") is True or php_doc.get("readyForPhpRemoval") is True:
                errs.append("invents cutover/removal")
            if php_doc.get("phpAuthoritative") is not True:
                errs.append("phpAuthoritative must be true")
            if errs:
                results.append({"file": php.name, "ok": False, "errors": errs})
            else:
                contract_pairs_ok += 1

    ok = all(r.get("ok") for r in results) if results else False
    if args.contract_only and contract_pairs_ok < 4:
        ok = False
    out = {
        "role": "compare-result",
        "ok": ok,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "contractOnly": bool(args.contract_only),
        "contractPairs": contract_pairs,
        "contractPairsOk": contract_pairs_ok,
        "missingPhpSides": missing_php,
        "policy": "login-bridge-hybrid-batch3; PHP chrome authoritative; BOS $_SESSION stays PHP",
        "sampleCount": len(results),
        "samples": results,
    }

    text = json.dumps(out, indent=2, sort_keys=True) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")
    print(text, end="")
    if not results:
        print("FAIL: no login cookie samples found", file=sys.stderr)
        return 2
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
