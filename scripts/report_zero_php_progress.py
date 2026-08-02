#!/usr/bin/env python3
"""Report zero-PHP migration progress from tracked inventory artifacts."""
from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path
from typing import Any

IMPLEMENTED_STATUSES = {"live", "removed"}
PARITY_STATUSES = {"parity-ready", "live", "removed"}
SHADOW_STATUSES = {"aspnet-shadow", "parity-ready", "live", "removed"}


def pct(part: int, total: int) -> float:
    return round((part / total * 100.0), 2) if total else 0.0


def load(path: str) -> dict[str, Any]:
    return json.loads(Path(path).read_text())


def build_report(inventory: dict[str, Any], ownership: dict[str, Any], batches: dict[str, Any]) -> dict[str, Any]:
    total = inventory["totalPhpFiles"]
    ownership_assigned = ownership["totalAssignments"]
    batch_assignments = batches["totalAssignments"]
    assignment_statuses = Counter(row["nextStatus"] for row in ownership["assignments"])
    batch_statuses = Counter(batch["status"] for batch in batches["batches"])
    live_or_removed = sum(1 for row in ownership["assignments"] if row["nextStatus"] in IMPLEMENTED_STATUSES)
    parity_ready = sum(1 for row in ownership["assignments"] if row["nextStatus"] in PARITY_STATUSES)
    shadow_or_better = sum(1 for row in ownership["assignments"] if row["nextStatus"] in SHADOW_STATUSES)

    # Foundation has been delivered by prior PRs. The remaining production score is
    # intentionally strict: route/job implementation, parity, live smoke, and PHP
    # removal must happen before the percentage moves materially.
    foundation_percent = 35.0
    implementation_percent = pct(live_or_removed, total)
    parity_percent = pct(parity_ready, total)
    shadow_percent = pct(shadow_or_better, total)
    true_zero_php_percent = max(foundation_percent, implementation_percent)
    pending_percent = round(100.0 - true_zero_php_percent, 2)

    return {
        "status": "foundation-and-planning-complete-implementation-pending",
        "trueZeroPhpCompletionPercent": true_zero_php_percent,
        "pendingPercent": pending_percent,
        "foundationPercent": foundation_percent,
        "routeJobImplementationPercent": implementation_percent,
        "routeJobParityPercent": parity_percent,
        "routeJobShadowOrBetterPercent": shadow_percent,
        "inventory": {
            "totalPhpFiles": total,
            "jobLikeFiles": inventory["jobLikeFiles"],
            "surfaceCounts": inventory["surfaceCounts"],
        },
        "planning": {
            "ownershipAssigned": ownership_assigned,
            "ownershipAssignmentPercent": pct(ownership_assigned, total),
            "batchAssignments": batch_assignments,
            "batchAssignmentPercent": pct(batch_assignments, total),
            "totalBatches": batches["totalBatches"],
            "assignmentStatuses": dict(sorted(assignment_statuses.items())),
            "batchStatuses": dict(sorted(batch_statuses.items())),
        },
        "nextExecutionOrder": [
            "Implement batch 1 worker replacements in ASP.NET Core dry-run mode.",
            "Attach PHP-vs-ASP.NET parity samples for each route/job in the batch.",
            "Move passing batch items to aspnet-shadow, then parity-ready, then live.",
            "Repeat batches without broad proxy cutover.",
            "Remove PHP only after every item is live or removed and rollback evidence is approved.",
        ],
    }


def write_markdown(report: dict[str, Any], output: Path) -> None:
    lines = [
        "# Zero-PHP Progress Status",
        "",
        "This status is generated from the tracked inventory, ownership plan, and exact-route cutover batches. It reports true production progress separately from planning progress so we do not overstate 0% PHP readiness.",
        "",
        "## Current percentage",
        "",
        f"- True zero-PHP completion: {report['trueZeroPhpCompletionPercent']}%.",
        f"- Pending to 100%: {report['pendingPercent']}%.",
        f"- Foundation/planning floor: {report['foundationPercent']}%.",
        f"- Route/job implementation complete: {report['routeJobImplementationPercent']}%.",
        f"- Route/job parity-ready: {report['routeJobParityPercent']}%.",
        f"- Route/job shadow-or-better: {report['routeJobShadowOrBetterPercent']}%.",
        "",
        "## Inventory",
        "",
        f"- Total PHP files: {report['inventory']['totalPhpFiles']}.",
        f"- Job-like PHP files: {report['inventory']['jobLikeFiles']}.",
        "",
        "| Surface | PHP files |",
        "| --- | ---: |",
    ]
    for surface, count in report["inventory"]["surfaceCounts"].items():
        lines.append(f"| {surface} | {count} |")
    lines.extend([
        "",
        "## Planning progress",
        "",
        f"- Ownership assigned: {report['planning']['ownershipAssigned']} ({report['planning']['ownershipAssignmentPercent']}%).",
        f"- Batch assignments: {report['planning']['batchAssignments']} ({report['planning']['batchAssignmentPercent']}%).",
        f"- Total exact-route batches: {report['planning']['totalBatches']}.",
        "",
        "## Next execution order",
        "",
    ])
    for item in report["nextExecutionOrder"]:
        lines.append(f"- {item}")
    lines.extend([
        "",
        "## Guardrail",
        "",
        "Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass.",
    ])
    output.write_text("\n".join(lines) + "\n")


def main() -> int:
    if len(sys.argv) != 6:
        print("Usage: report_zero_php_progress.py <inventory.json> <ownership.json> <batches.json> <output.json> <output.md>", file=sys.stderr)
        return 2
    report = build_report(load(sys.argv[1]), load(sys.argv[2]), load(sys.argv[3]))
    Path(sys.argv[4]).write_text(json.dumps(report, indent=2) + "\n")
    write_markdown(report, Path(sys.argv[5]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
