#!/usr/bin/env python3
"""Build exact-route zero-PHP implementation batches from the ownership plan."""
from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path
from typing import Any

SLICE_ORDER = {
    "worker-replacement": 0,
    "public-api-port": 1,
    "ai-service-contract": 2,
    "cp-workflow-port": 3,
    "erp-workflow-port": 4,
    "bos-admin-port": 5,
    "storefront-port": 6,
    "platform-route-port": 7,
}
RISK_ORDER = {"high": 0, "medium": 1, "low": 2}
DEFAULT_BATCH_SIZE = 50


def sort_key(row: dict[str, Any]) -> tuple[int, int, str, str]:
    return (
        SLICE_ORDER.get(row["targetSlice"], 99),
        RISK_ORDER.get(row["risk"], 99),
        row["surface"],
        row["path"],
    )


def build_batches(plan: dict[str, Any], batch_size: int = DEFAULT_BATCH_SIZE) -> dict[str, Any]:
    rows = sorted(plan["assignments"], key=sort_key)
    batches = []
    for index in range(0, len(rows), batch_size):
        chunk = rows[index:index + batch_size]
        batch_number = len(batches) + 1
        slice_counts = Counter(row["targetSlice"] for row in chunk)
        owner_counts = Counter(row["targetOwner"] for row in chunk)
        risk_counts = Counter(row["risk"] for row in chunk)
        batches.append({
            "batch": batch_number,
            "status": "planned-not-implemented",
            "cutoverMode": "exact-route-only",
            "phpFallbackRequired": True,
            "items": len(chunk),
            "primarySlice": slice_counts.most_common(1)[0][0],
            "sliceCounts": dict(sorted(slice_counts.items())),
            "ownerCounts": dict(sorted(owner_counts.items())),
            "riskCounts": dict(sorted(risk_counts.items())),
            "evidenceRequired": [
                "ASP.NET Core implementation merged",
                "unit/integration tests passing",
                "PHP-vs-ASP.NET parity sample attached",
                "operator rollback command documented",
                "exact-route proxy rule only",
                "live smoke passed before PHP fallback removal",
            ],
            "assignments": chunk,
        })
    return {
        "status": "planned-not-implemented",
        "sourcePlanStatus": plan["status"],
        "batchSize": batch_size,
        "totalBatches": len(batches),
        "totalAssignments": len(rows),
        "rules": [
            "Implement one batch at a time; do not broad-cutover CP, ERP, BOS, API, or storefront trees.",
            "Keep PHP fallback enabled until a batch is parity-ready and live smoke passes.",
            "ASP.NET Core remains the owner of route/API/auth/database/business behavior.",
            "Python is invoked only by ASP.NET Core for stateless AI-service helper results.",
        ],
        "batches": batches,
    }


def main() -> int:
    if len(sys.argv) not in {3, 4}:
        print("Usage: build_zero_php_cutover_batches.py <ownership-plan.json> <output.json> [batch-size]", file=sys.stderr)
        return 2
    plan_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2])
    batch_size = int(sys.argv[3]) if len(sys.argv) == 4 else DEFAULT_BATCH_SIZE
    if batch_size <= 0:
        print("batch-size must be positive", file=sys.stderr)
        return 2
    plan = json.loads(plan_path.read_text())
    output_path.write_text(json.dumps(build_batches(plan, batch_size), indent=2) + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
