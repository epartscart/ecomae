#!/usr/bin/env python3
"""Fail-closed readiness verifier for the Zero-PHP 100% gate."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SUMMARY = ROOT / "docs" / "migration" / "inventory" / "zero-php-100-evidence-template-summary.json"


def main() -> int:
    if not SUMMARY.exists():
        print(f"FAIL missing {SUMMARY.relative_to(ROOT)}; run scripts/generate_zero_php_100_evidence_templates.py")
        return 1
    data = json.loads(SUMMARY.read_text(encoding="utf-8"))
    status = data.get("status", {})
    blockers = []
    if status.get("true_zero_php_completion_percent") != 100.0:
        blockers.append("true zero-PHP completion is not 100%")
    if status.get("route_job_implementation_complete_percent") != 100.0:
        blockers.append("route/job implementation is not 100%")
    if status.get("route_job_parity_ready_percent") != 100.0:
        blockers.append("route/job parity-ready is not 100%")
    if status.get("route_job_shadow_or_better_percent") != 100.0:
        blockers.append("route/job shadow-or-better is not 100%")
    if status.get("php_fallback_required") is not False:
        blockers.append("PHP fallback is still required")
    if blockers:
        print("NOT READY for Zero-PHP 100%:")
        for blocker in blockers:
            print(f"- {blocker}")
        return 1
    print("READY for Zero-PHP 100%")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
