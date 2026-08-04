#!/usr/bin/env python3
"""Fail if migration evidence claims cutover/PHP removal or invents approval files.

Never invents RELEASE_OWNER_APPROVAL.md. Designed for CI / foundation checks.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

FORBIDDEN_FILES = (
    "RELEASE_OWNER_APPROVAL.md",
    "MODULE_FUNCTION_TEST_PASS.md",
    "MODULE_FUNCTION_PARITY_PASS",
    "MODULE_FUNCTION_PARITY_PASS.md",
)

# Verdict JSON files that must explicitly set cutoverAllowed=false (absence is a drift hole).
MUST_DECLARE_CUTOVER_FALSE = (
    "tenant-safety/live-tenant-php-chrome.json",
    "tenant-safety/same-to-same-verify.json",
    "tenant-safety/industry-ecomae-frontend-parity.json",
    "tenant-safety/industry-ecomae-coverage-matrix.json",
    "tenant-safety/epartscart-frontend-cp-parity.json",
    "tenant-safety/epartscart-coverage-matrix.json",
    "presentation/php-vs-aspnet-recheck.json",
    "presentation/php_module_catalog.json",
    "presentation/php_module_catalog_counts.json",
    "module-function-parity/compare-result.json",
    "catalog-api/compare-result.json",
    "price-lookup/compare-result.json",
    "surface-parity/www-surface-field-parity.json",
    "surface-parity/surface-field-offline-result.json",
    "surface-parity/contracts-readme.json",
    "surface-parity/digest-dual-sample-contract-result.json",
    "surface-parity/harness-report.json",
    "surface-parity/presentation-asset-check.json",
    "decommission/public-probes/www-php-decommission-readiness.json",
    "decommission/public-probes/www-zero-php-completion.json",
    "decommission/public-probes/www-live-surface-links.json",
    "decommission/public-probes/www-surface-field-parity.json",
    "decommission/public-probes/www-pre-php-removal-parity-verdict.json",
    "decommission/public-probes/www-final-gate-area-tests.json",
    "decommission/public-probes/www-live-surface-stack.json",
    "decommission/public-probes/www-zero-php-residual-board.json",
    "decommission/public-probes/www-storefront-digest-shadow-probe.json",
    "on-premises-dual-samples/compare-result.json",
    "surface-parity/digest-live-sample-gaps.json",
    "decommission/functional-flows/required-flows.json",
    "decommission/functional-flows/www-functional-flow-suite.json",
    "presentation/marketing-app-dual-sample-floor.json",
    "presentation/www-marketing-app-shadow-probe.json",
    "erp-ajax-dual-samples/compare-result.json",
    "bos-ajax-dual-samples/compare-result.json",
    "module-ajax-dual-samples/compare-result.json",
    "write-dryruns/compare-result.json",
    "write-dryruns/write-dryrun-operator-result.json",
)

# Entire report-style trees (raw staging-smoke API dumps are excluded).
MUST_DECLARE_TREE_GLOBS = (
    "decommission/public-probes/*.json",
    "decommission/parity-samples/*.json",
    "decommission/functional-flows/*.json",
    "decommission/functional-flows/live-smoke/*.json",
    "hybrid-ui-dual-samples/*.json",
    "login-session-bridge/*.json",
    "catalog-miss-umapi/*.json",
    "price-lookup/*.json",
    "presentation/*.json",
    "module-function-parity/*.json",
    "module-ajax-dual-samples/*.json",
    "erp-ajax-dual-samples/*.json",
    "bos-ajax-dual-samples/*.json",
    "on-premises-dual-samples/*.json",
    "write-dryruns/*.json",
    "catalog-api/*.json",
    "tenant-safety/*.json",
    # Top-level surface-parity reports only (sample fixtures/goldens have dedicated validators).
    "surface-parity/*.json",
)


def walk_bools(obj, prefix: str = "") -> list[tuple[str, bool]]:
    items: list[tuple[str, bool]] = []
    if isinstance(obj, dict):
        for key, value in obj.items():
            path = f"{prefix}.{key}" if prefix else str(key)
            if isinstance(value, (dict, list)):
                items.extend(walk_bools(value, path))
            elif isinstance(value, bool):
                items.append((path, value))
    elif isinstance(obj, list):
        for idx, value in enumerate(obj):
            path = f"{prefix}[{idx}]"
            if isinstance(value, (dict, list)):
                items.extend(walk_bools(value, path))
            elif isinstance(value, bool):
                items.append((path, value))
    return items


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--evidence-root",
        type=Path,
        default=Path("docs/migration/evidence"),
    )
    ap.add_argument(
        "--docs-root",
        type=Path,
        default=Path("docs/migration"),
    )
    args = ap.parse_args()

    errors: list[str] = []
    scanned = 0

    for path in sorted(args.evidence_root.rglob("*.json")):
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            errors.append(f"{path}: invalid JSON ({ex})")
            continue
        scanned += 1
        if not isinstance(doc, dict):
            continue
        for key_path, value in walk_bools(doc):
            leaf = key_path.rsplit(".", 1)[-1]
            if leaf in {"cutoverAllowed", "readyForPhpRemoval", "readyToRemovePhp"} and value is True:
                errors.append(f"{path}: {key_path}=true (must stay false)")

    for name in FORBIDDEN_FILES:
        hits = sorted(args.docs_root.rglob(name))
        for hit in hits:
            errors.append(f"forbidden approval/pass artifact present: {hit}")

    required_paths: set[Path] = set()
    for rel in MUST_DECLARE_CUTOVER_FALSE:
        required_paths.add(args.evidence_root / rel)
    for pattern in MUST_DECLARE_TREE_GLOBS:
        required_paths.update(sorted(args.evidence_root.glob(pattern)))

    for path in sorted(required_paths):
        if not path.is_file():
            errors.append(f"missing required cutover-lock evidence: {path}")
            continue
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            errors.append(f"{path}: invalid JSON ({ex})")
            continue
        if not isinstance(doc, dict):
            errors.append(f"{path}: root must be object")
            continue
        if doc.get("cutoverAllowed") is not False:
            errors.append(f"{path}: cutoverAllowed must be explicitly false")
        removal = doc.get("readyForPhpRemoval")
        if removal is None:
            removal = doc.get("readyToRemovePhp")
        if removal is not False:
            errors.append(f"{path}: readyForPhpRemoval (or readyToRemovePhp) must be explicitly false")

    if errors:
        print("FAIL: migration evidence cutover locks", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: scanned {scanned} evidence JSON files; "
        "cutoverAllowed/readyForPhpRemoval stay false; no invented approval files"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
