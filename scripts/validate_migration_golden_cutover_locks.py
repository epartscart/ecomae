#!/usr/bin/env python3
"""Ensure migration dual-sample goldens declare cutover locks and match the generator.

Never invents RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import ast
import json
import sys
from pathlib import Path


def load_generator_names(path: Path) -> set[str]:
    tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
    for node in tree.body:
        if not isinstance(node, ast.FunctionDef) or node.name != "main":
            continue
        for stmt in node.body:
            if not isinstance(stmt, ast.Assign):
                continue
            for target in stmt.targets:
                if isinstance(target, ast.Name) and target.id == "samples":
                    if isinstance(stmt.value, ast.Dict):
                        names: set[str] = set()
                        for key in stmt.value.keys:
                            if isinstance(key, ast.Constant) and isinstance(key.value, str):
                                names.add(key.value)
                        return names
    raise SystemExit(f"FAIL: could not parse samples dict from {path}")


def load_compare_digest_stems(path: Path) -> set[str]:
    tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
    stems: set[str] = set()
    for node in tree.body:
        if not isinstance(node, ast.Assign):
            continue
        for target in node.targets:
            if not isinstance(target, ast.Name):
                continue
            if target.id not in {"SUMMARY_CONTRACTS", "LIST_CONTRACTS", "OBJECT_CONTRACTS"}:
                continue
            if isinstance(node.value, ast.Dict):
                for key in node.value.keys:
                    if isinstance(key, ast.Constant) and isinstance(key.value, str):
                        stems.add(key.value)
    return stems


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--migration-dir",
        type=Path,
        default=Path("docs/migration/evidence/surface-parity/samples/migration"),
    )
    ap.add_argument(
        "--generator",
        type=Path,
        default=Path("scripts/generate_migration_digest_contract_samples.py"),
    )
    ap.add_argument(
        "--compare-digest",
        type=Path,
        default=Path("scripts/compare_digest_dual_samples.py"),
    )
    args = ap.parse_args()
    errors: list[str] = []

    generator_names = load_generator_names(args.generator)
    disk_names = {p.name for p in args.migration_dir.glob("*.json")}
    missing_disk = sorted(generator_names - disk_names)
    extra_disk = sorted(disk_names - generator_names)
    if missing_disk:
        errors.append(f"generator names missing on disk: {missing_disk}")
    if extra_disk:
        errors.append(f"disk goldens not in generator: {extra_disk}")

    digest_stems = load_compare_digest_stems(args.compare_digest)
    digest_files = {f"{stem}.json" for stem in digest_stems}
    missing_digest = sorted(digest_files - generator_names)
    if missing_digest:
        errors.append(f"digest compare stems missing from generator: {missing_digest}")

    locked = 0
    for path in sorted(args.migration_dir.glob("*.json")):
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            errors.append(f"{path.name}: invalid JSON ({ex})")
            continue
        if not isinstance(doc, dict):
            errors.append(f"{path.name}: root must be object")
            continue
        if doc.get("cutoverAllowed") is not False:
            errors.append(f"{path.name}: cutoverAllowed must be explicitly false")
        if doc.get("readyForPhpRemoval") is not False:
            errors.append(f"{path.name}: readyForPhpRemoval must be explicitly false")
        if doc.get("dualSampleBaseline") != "migration-contract-golden":
            errors.append(f"{path.name}: dualSampleBaseline must be migration-contract-golden")
        if (
            doc.get("cutoverAllowed") is False
            and doc.get("readyForPhpRemoval") is False
            and doc.get("dualSampleBaseline") == "migration-contract-golden"
        ):
            locked += 1

    gen_text = args.generator.read_text(encoding="utf-8")
    if 'payload["cutoverAllowed"] = False' not in gen_text and "cutoverAllowed = False" not in gen_text:
        errors.append("generator must stamp cutoverAllowed = False on write")

    if errors:
        print("FAIL: migration golden cutover locks", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: goldens={len(disk_names)} locked={locked} "
        f"generator={len(generator_names)} digestContracts={len(digest_stems)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
