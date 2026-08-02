#!/usr/bin/env python3
"""Generate final Zero-PHP 100% route/job evidence template plans.

Default mode is a dry run: it scans the full tracked PHP inventory, reads the
cutover batch plan, writes only the summary artifacts, and creates no per-item
evidence templates. Use --write only after owners are ready to fill the 3,049
route/job evidence files.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "docs" / "migration" / "inventory"
CUTOVER_BATCHES = OUT_DIR / "php-route-job-cutover-batches.json"
SUMMARY_JSON = OUT_DIR / "zero-php-100-evidence-template-summary.json"
SUMMARY_MD = OUT_DIR / "ZERO_PHP_100_EVIDENCE_TEMPLATE_SUMMARY.md"
EVIDENCE_ROOT = ROOT / "docs" / "migration" / "evidence" / "zero-php-100"

STATUS = {
    "true_zero_php_completion_percent": 35.0,
    "pending_to_100_percent": 65.0,
    "route_job_implementation_complete_percent": 0.0,
    "route_job_parity_ready_percent": 0.0,
    "route_job_shadow_or_better_percent": 0.0,
    "total_tracked_php_files": 3049,
    "job_like_php_files": 140,
    "exact_route_batches": 61,
    "php_fallback_required": True,
    "broad_cutover_allowed": False,
    "next_batch": "worker-dry-run-replacements",
}

REQUIRED_EVIDENCE_FIELDS = [
    "implementation_reference",
    "php_baseline_sample",
    "aspnet_dry_run_or_shadow_sample",
    "response_or_data_parity_comparison",
    "exact_route_cutover_data",
    "auth_tenant_permission_parity_result",
    "rollback_command",
    "rollback_approval",
    "production_smoke_status",
    "php_fallback_safety",
    "owner_approval",
]

SURFACE_PREFIXES = {
    "api/": "api",
    "cp/": "cp",
    "content/shop/finance/": "erp",
    "bos/": "bos",
    "content/cron/": "workers",
    "content/general_pages/": "storefront",
}


@dataclass(frozen=True)
class EvidenceTarget:
    item_id: str
    batch_id: str
    surface: str
    php_path: str
    evidence_path: Path


def git_tracked_php_files() -> list[str]:
    result = subprocess.run(
        ["git", "ls-files", "*.php"],
        cwd=ROOT,
        check=True,
        text=True,
        stdout=subprocess.PIPE,
    )
    return [line.strip() for line in result.stdout.splitlines() if line.strip()]


def load_batch_count() -> int:
    data = json.loads(CUTOVER_BATCHES.read_text(encoding="utf-8"))
    return int(data.get("exact_route_batches", STATUS["exact_route_batches"]))


def classify_surface(path: str) -> str:
    for prefix, surface in SURFACE_PREFIXES.items():
        if path.startswith(prefix):
            return surface
    if "cron" in path or "job" in path or "task" in path or "setup" in path:
        return "workers"
    if path.startswith("content/"):
        return "storefront"
    return "platform"


def slugify(path: str) -> str:
    stem = re.sub(r"[^A-Za-z0-9._-]+", "-", path).strip("-")
    digest = hashlib.sha1(path.encode("utf-8")).hexdigest()[:10]
    return f"{stem}-{digest}"


def build_targets(files: Iterable[str], batch_count: int) -> list[EvidenceTarget]:
    targets: list[EvidenceTarget] = []
    for index, path in enumerate(sorted(files), start=1):
        batch_number = ((index - 1) % batch_count) + 1
        batch_id = f"batch-{batch_number:02d}"
        item_id = f"php-route-job-{index:04d}"
        evidence_path = EVIDENCE_ROOT / batch_id / f"{slugify(path)}.md"
        targets.append(EvidenceTarget(item_id, batch_id, classify_surface(path), path, evidence_path))
    return targets


def render_template(target: EvidenceTarget) -> str:
    fields = "\n".join(f"- [ ] `{field}`: TODO" for field in REQUIRED_EVIDENCE_FIELDS)
    return (
        f"# Zero-PHP Evidence: {target.item_id}\n\n"
        f"- PHP path: `{target.php_path}`\n"
        f"- Surface: `{target.surface}`\n"
        f"- Cutover batch: `{target.batch_id}`\n"
        "- Target runtime: `ASP.NET Core` unless explicitly approved as AI-only Python.\n"
        "- PHP fallback: required until this evidence is complete and approved.\n\n"
        "## Required fields\n\n"
        f"{fields}\n"
    )


def write_summary(targets: list[EvidenceTarget], *, write: bool, created: int, existing: int) -> None:
    selected_batches = len({target.batch_id for target in targets})
    missing = len(targets) - existing - created
    payload = {
        "mode": "write" if write else "dry-run",
        "selected_batches": selected_batches,
        "selected_items": len(targets),
        "existing_templates": existing,
        "created_templates": created,
        "missing_templates": missing,
        "evidence_root": str(EVIDENCE_ROOT.relative_to(ROOT)),
        "required_evidence_schema_fields": REQUIRED_EVIDENCE_FIELDS,
        "status": STATUS,
        "truthfulness_guardrail": (
            "Do not report 100% until every tracked PHP route/job is live or removed, "
            "rollback approval exists, production smoke checks pass, and PHP fallback "
            "is no longer required for the item."
        ),
    }
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    SUMMARY_JSON.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    SUMMARY_MD.write_text(
        "# Zero-PHP 100% Evidence Template Summary\n\n"
        f"- Mode: **{payload['mode']}**\n"
        f"- Selected batches: **{selected_batches}**\n"
        f"- Selected items: **{len(targets)}**\n"
        f"- Existing templates: **{existing}**\n"
        f"- Created templates: **{created}**\n"
        f"- Missing templates: **{missing}**\n"
        f"- True zero-PHP completion: **{STATUS['true_zero_php_completion_percent']}%**\n"
        f"- Pending to 100%: **{STATUS['pending_to_100_percent']}%**\n\n"
        "## Required evidence schema fields\n\n"
        + "\n".join(f"- `{field}`" for field in REQUIRED_EVIDENCE_FIELDS)
        + "\n\n## Guardrail\n\n"
        + payload["truthfulness_guardrail"]
        + "\n",
        encoding="utf-8",
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="Create missing per-route/job evidence template files.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    files = git_tracked_php_files()
    batch_count = load_batch_count()
    targets = build_targets(files, batch_count)
    existing = sum(1 for target in targets if target.evidence_path.exists())
    created = 0
    if args.write:
        for target in targets:
            if target.evidence_path.exists():
                continue
            target.evidence_path.parent.mkdir(parents=True, exist_ok=True)
            target.evidence_path.write_text(render_template(target), encoding="utf-8")
            created += 1
    write_summary(targets, write=args.write, created=created, existing=existing)
    mode = "write" if args.write else "dry-run"
    print(f"Mode: {mode}")
    print(f"Selected batches: {len({target.batch_id for target in targets})}")
    print(f"Selected items: {len(targets)}")
    print(f"Existing templates: {existing}")
    print(f"Created templates: {created}")
    print(f"Missing templates: {len(targets) - existing - created}")
    print(f"Wrote {SUMMARY_JSON.relative_to(ROOT)}")
    print(f"Wrote {SUMMARY_MD.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
