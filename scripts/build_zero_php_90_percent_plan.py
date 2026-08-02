#!/usr/bin/env python3
"""Build the exact-route work plan required to move true zero-PHP completion from 35% to 90%.

This does not mark routes complete. It selects the minimum planned batches that must become
live/removed before the generated progress reporter may honestly report 90% completion.
"""

import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
INPUT = ROOT / "docs/migration/inventory/php-route-job-cutover-batches.json"
OUTPUT_JSON = ROOT / "docs/migration/inventory/zero-php-90-percent-target-plan.json"
OUTPUT_MD = ROOT / "docs/migration/inventory/ZERO_PHP_90_PERCENT_TARGET_PLAN.md"

FOUNDATION_PERCENT = 35.0
TARGET_PERCENT = 90.0
IMPLEMENTATION_WEIGHT = 100.0 - FOUNDATION_PERCENT


def main() -> None:
    source = json.loads(INPUT.read_text())
    total_assignments = int(source["totalAssignments"])
    implementation_fraction_required = (TARGET_PERCENT - FOUNDATION_PERCENT) / IMPLEMENTATION_WEIGHT
    required_live_items = math.ceil(total_assignments * implementation_fraction_required)

    selected_batches = []
    selected_items = 0
    for batch in source["batches"]:
        if selected_items >= required_live_items:
            break
        selected_batches.append({
            "batch": batch["batch"],
            "items": batch["items"],
            "primarySlice": batch["primarySlice"],
            "riskCounts": batch["riskCounts"],
            "ownerCounts": batch["ownerCounts"],
            "requiredStatusBeforeCountingToward90": "live-or-removed-with-rollback-and-smoke-evidence",
        })
        selected_items += int(batch["items"])

    plan = {
        "status": "target-plan-not-completion-claim",
        "targetPercent": TARGET_PERCENT,
        "currentFoundationPercent": FOUNDATION_PERCENT,
        "currentPendingPercent": 100.0 - FOUNDATION_PERCENT,
        "implementationWeightPercent": IMPLEMENTATION_WEIGHT,
        "totalAssignments": total_assignments,
        "requiredLiveOrRemovedItems": required_live_items,
        "selectedItems": selected_items,
        "selectedBatches": len(selected_batches),
        "remainingItemsAfter90Target": total_assignments - selected_items,
        "rules": [
            "Do not report 90% until selectedItems are live or removed.",
            "Every selected item needs ASP.NET Core implementation, parity sample, exact-route proxy, rollback command, and production smoke evidence.",
            "PHP fallback remains until the selected item is live or removed with approved rollback.",
            "No broad CP, ERP, BOS, API, or storefront proxy cutover is authorized by this plan.",
        ],
        "batches": selected_batches,
    }

    OUTPUT_JSON.write_text(json.dumps(plan, indent=2) + "\n")
    OUTPUT_MD.write_text(render_markdown(plan))


def render_markdown(plan: dict) -> str:
    rows = []
    for batch in plan["batches"][:20]:
        risks = ", ".join(f"{key}:{value}" for key, value in sorted(batch["riskCounts"].items()))
        owners = ", ".join(f"{key}:{value}" for key, value in sorted(batch["ownerCounts"].items()))
        rows.append(f"| {batch['batch']} | {batch['primarySlice']} | {batch['items']} | {risks} | {owners} |")

    return "\n".join([
        "# Zero-PHP 90% Target Plan",
        "",
        "Generated from `scripts/build_zero_php_90_percent_plan.py`. This is a target execution plan, not a completion claim.",
        "",
        "## Target math",
        "",
        f"- Current foundation/planning floor: {plan['currentFoundationPercent']:.1f}%.",
        f"- Target true zero-PHP completion: {plan['targetPercent']:.1f}%.",
        f"- Implementation/live weight remaining after foundation: {plan['implementationWeightPercent']:.1f}%.",
        f"- Total tracked PHP route/job assignments: {plan['totalAssignments']}.",
        f"- Items that must be `live` or `removed` to honestly report 90%: {plan['requiredLiveOrRemovedItems']}.",
        f"- Selected exact-route batches: {plan['selectedBatches']}.",
        f"- Selected items: {plan['selectedItems']}.",
        f"- Items still remaining after the 90% target: {plan['remainingItemsAfter90Target']}.",
        "",
        "## Guardrails",
        "",
        "- Do not report 90% until all selected items are `live` or `removed`.",
        "- Every selected item needs ASP.NET Core implementation, parity sample, exact-route proxy, rollback command, and production smoke evidence.",
        "- PHP fallback remains until item-level evidence is approved.",
        "- No broad CP, ERP, BOS, API, or storefront proxy cutover is authorized.",
        "",
        "## First 20 selected batches",
        "",
        "| Batch | Primary slice | Items | Risk counts | Owner counts |",
        "| ---: | --- | ---: | --- | --- |",
        *rows,
        "",
    ])


if __name__ == "__main__":
    main()
