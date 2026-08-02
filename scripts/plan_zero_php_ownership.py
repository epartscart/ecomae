#!/usr/bin/env python3
"""Generate a deterministic zero-PHP ownership plan from the PHP inventory.

This does not mark parity complete. It assigns a target owner so the team can
work the remaining PHP retirement route-by-route without broad cutover.
"""
from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path
from typing import Any

AI_HELPER_MARKERS = (
    "ai",
    "agent",
    "analytics",
    "bi_",
    "classification",
    "copilot",
    "forecast",
    "image",
    "ocr",
    "pdf",
    "predict",
    "recommend",
    "search",
)

JOB_MARKERS = (
    "backup",
    "cleanup",
    "cron",
    "import",
    "queue",
    "sitemap",
    "sync",
    "worker",
)


def target_owner(item: dict[str, Any]) -> str:
    path = item["path"].lower()
    if any(marker in path for marker in AI_HELPER_MARKERS):
        return "aspnet-with-python-ai-helper"
    return "aspnet-core"


def target_slice(item: dict[str, Any], owner: str) -> str:
    path = item["path"].lower()
    surface = item["surface"]
    if item.get("jobLike") or any(marker in path for marker in JOB_MARKERS):
        return "worker-replacement"
    if owner == "aspnet-with-python-ai-helper":
        return "ai-service-contract"
    if surface == "cp":
        return "cp-workflow-port"
    if surface == "erp":
        return "erp-workflow-port"
    if surface == "bos":
        return "bos-admin-port"
    if surface == "api":
        return "public-api-port"
    if surface == "storefront":
        return "storefront-port"
    return "platform-route-port"


def risk_level(item: dict[str, Any], owner: str) -> str:
    if item.get("jobLike"):
        return "high"
    if item["surface"] in {"cp", "erp", "bos"}:
        return "high"
    if owner == "aspnet-with-python-ai-helper":
        return "medium"
    return "medium" if item["surface"] in {"api", "storefront"} else "low"


def build_plan(inventory: dict[str, Any]) -> dict[str, Any]:
    assignments = []
    for item in inventory["items"]:
        owner = target_owner(item)
        assignments.append(
            {
                "path": item["path"],
                "surface": item["surface"],
                "jobLike": item["jobLike"],
                "currentStatus": item["migrationStatus"],
                "targetOwner": owner,
                "targetSlice": target_slice(item, owner),
                "risk": risk_level(item, owner),
                "nextStatus": "owner-assigned-pending-parity",
                "cutoverRule": "exact-route-only-with-php-fallback-until-live-parity",
            }
        )

    owner_counts = Counter(row["targetOwner"] for row in assignments)
    slice_counts = Counter(row["targetSlice"] for row in assignments)
    risk_counts = Counter(row["risk"] for row in assignments)

    return {
        "status": "owner-assigned-pending-parity",
        "sourceInventoryStatus": inventory["status"],
        "totalAssignments": len(assignments),
        "ownerCounts": dict(sorted(owner_counts.items())),
        "sliceCounts": dict(sorted(slice_counts.items())),
        "riskCounts": dict(sorted(risk_counts.items())),
        "rules": [
            "ASP.NET Core owns every public route, API, auth decision, database transaction, business workflow, job orchestration, and final response.",
            "Python is allowed only for stateless AI-service helper work behind ASP.NET Core.",
            "PHP remains fallback only until exact-route parity, rollback evidence, and production smoke checks pass.",
            "No broad proxy cutover is allowed from this plan.",
        ],
        "assignments": assignments,
    }


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: plan_zero_php_ownership.py <inventory.json> <output.json>", file=sys.stderr)
        return 2
    inventory_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2])
    inventory = json.loads(inventory_path.read_text())
    plan = build_plan(inventory)
    output_path.write_text(json.dumps(plan, indent=2) + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
