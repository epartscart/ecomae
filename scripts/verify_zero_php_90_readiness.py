#!/usr/bin/env python3
"""Fail-closed readiness verifier for the Zero-PHP 90% gate."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SUMMARY = ROOT / "docs" / "migration" / "inventory" / "zero-php-100-evidence-template-summary.json"


def main() -> int:
    if not SUMMARY.exists():
        print(f"FAIL missing {SUMMARY.relative_to(ROOT)}; run scripts/generate_zero_php_100_evidence_templates.py")
        return 1
    status = json.loads(SUMMARY.read_text(encoding="utf-8")).get("status", {})
    completion = float(status.get("true_zero_php_completion_percent", 0.0))
    shadow = float(status.get("route_job_shadow_or_better_percent", 0.0))
    if completion < 90.0 or shadow < 90.0:
        print("NOT READY for Zero-PHP 90%:")
        print(f"- true zero-PHP completion is {completion:.1f}%")
        print(f"- route/job shadow-or-better is {shadow:.1f}%")
        return 1
    print("READY for Zero-PHP 90%")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
