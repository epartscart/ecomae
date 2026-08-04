#!/usr/bin/env python3
"""Generate missing surface-parity item/summary field floors from digest contracts.

Never invents cutoverAllowed=true / readyForPhpRemoval=true / RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMPARE = ROOT / "scripts/compare_digest_dual_samples.py"
FLOOR_DIR = ROOT / "docs/migration/evidence/surface-parity"
LIST_FLOOR = FLOOR_DIR / "list-digest-item-field-floor.json"


def extract_balanced(src: str, name: str) -> str:
    m = re.search(rf"{name}\s*=\s*\{{", src)
    if not m:
        raise SystemExit(f"missing {name}")
    i = m.end() - 1
    depth = 0
    for j in range(i, len(src)):
        if src[j] == "{":
            depth += 1
        elif src[j] == "}":
            depth -= 1
            if depth == 0:
                return src[i : j + 1]
    raise SystemExit(f"unbalanced {name}")


def parse_summary(body: str) -> dict[str, tuple[str, list[str]]]:
    out: dict[str, tuple[str, list[str]]] = {}
    for m in re.finditer(
        r'"([a-z0-9-]+)"\s*:\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,?\s*\)',
        body,
        flags=re.S,
    ):
        stem, path_key, fields = m.group(1), m.group(2), m.group(3)
        out[stem] = (path_key, [f.strip() for f in fields.split(",") if f.strip()])
    return out


def parse_hybrid(body: str) -> dict[str, tuple[str, list[str]]]:
    out: dict[str, tuple[str, list[str]]] = {}
    for m in re.finditer(
        r'"([a-z0-9-]+)"\s*:\s*\(\s*"([^"]+)"\s*,\s*\[([^\]]*)\]\s*,?\s*\)',
        body,
        flags=re.S,
    ):
        stem, coll = m.group(1), m.group(2)
        fields = re.findall(r'"([^"]+)"', m.group(3))
        out[stem] = (coll, fields)
    return out


def parse_list(body: str) -> dict[str, list[str]]:
    out: dict[str, list[str]] = {}
    for m in re.finditer(
        r'"([a-z0-9-]+)"\s*:\s*\[([^\]]*)\]',
        body,
        flags=re.S,
    ):
        out[m.group(1)] = re.findall(r'"([^"]+)"', m.group(2))
    return out


def existing_stems() -> set[str]:
    stems: set[str] = set()
    for path in FLOOR_DIR.glob("*.json"):
        name = path.name
        if name == "list-digest-item-field-floor.json":
            continue
        if not (
            name.endswith("-item-field-floor.json")
            or name.endswith("-sample-tenants-floor.json")
            or name.endswith("-object-field-floor.json")
        ):
            continue
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception:
            continue
        stem = doc.get("stem")
        if isinstance(stem, str) and stem:
            stems.add(stem)
            continue
        for suf in (
            "-item-field-floor.json",
            "-sample-tenants-floor.json",
            "-object-field-floor.json",
        ):
            if name.endswith(suf):
                stems.add(name[: -len(suf)])
                break
    if LIST_FLOOR.is_file():
        stems.update(json.loads(LIST_FLOOR.read_text(encoding="utf-8")).get("stems") or [])
    return stems


def write_floor(stem: str, summary: list[str] | None, coll: str | None, items: list[str] | None) -> Path:
    role = f"{stem}-item-field-floor"
    doc = {
        "role": role,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "stem": stem,
        "requireNonemptyMigrationSentinel": bool(items),
        "source": "scripts/compare_digest_dual_samples.py + generate_missing_item_field_floors.py",
        "note": (
            f"Locks {stem} digest field floors for PHP↔ASP.NET dual-sample. "
            "Interactive writes remain PHP. cutoverAllowed=false."
        ),
    }
    if summary:
        doc["requiredSummaryFields"] = summary
    if coll:
        doc["collectionKey"] = coll
    if items:
        doc["requiredItemFields"] = items
    path = FLOOR_DIR / f"{stem}-item-field-floor.json"
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    return path


def main() -> int:
    src = COMPARE.read_text(encoding="utf-8")
    summary = parse_summary(extract_balanced(src, "SUMMARY_CONTRACTS"))
    hybrid = parse_hybrid(extract_balanced(src, "HYBRID_LIST_ITEM_FIELDS"))
    lists = parse_list(extract_balanced(src, "LIST_ITEM_FIELDS"))
    objects = parse_list(extract_balanced(src, "OBJECT_CONTRACTS"))

    have = existing_stems()
    created: list[str] = []

    # Summary-only and hybrid stems
    for stem in sorted(set(summary) | set(hybrid)):
        if stem in have:
            continue
        s_fields = summary.get(stem, (None, []))[1] if stem in summary else None
        coll, items = (None, None)
        if stem in hybrid:
            coll, items = hybrid[stem]
        # summary path_key unused; fields list used
        if stem in summary:
            s_fields = summary[stem][1]
        write_floor(stem, s_fields, coll, items)
        created.append(stem)
        have.add(stem)

    # Object envelopes
    for stem, fields in objects.items():
        if stem in have:
            continue
        role = f"{stem}-object-field-floor"
        doc = {
            "role": role,
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "aspNetInteractiveComplete": 0,
            "stem": stem,
            "requiredObjectFields": fields,
            "source": "OBJECT_CONTRACTS + generate_missing_item_field_floors.py",
            "note": f"Locks {stem} object envelope fields. cutoverAllowed=false.",
        }
        path = FLOOR_DIR / f"{stem}-object-field-floor.json"
        path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
        created.append(stem)
        have.add(stem)

    # Expand list-digest umbrella to all LIST_ITEM_FIELDS stems
    list_doc = {
        "role": "list-digest-item-field-floor",
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "listStemCount": len(lists),
        "stems": sorted(lists),
        "requireNonemptyMigrationSentinel": True,
        "source": "scripts/compare_digest_dual_samples.py LIST_ITEM_FIELDS + generate_missing_item_field_floors.py",
        "note": (
            "Every list digest migration golden ships a non-empty item-field sentinel matching "
            "SurfacePayloadContractCatalog / LIST_ITEM_FIELDS. Live empty DB lists remain valid at runtime; "
            "interactive aspnet-complete stays 0. Never invents RELEASE_OWNER_APPROVAL.md."
        ),
    }
    LIST_FLOOR.write_text(json.dumps(list_doc, indent=2) + "\n", encoding="utf-8")

    board = {
        "role": "menu-field-parity-built-vs-pending",
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "menus": {
            "totalTracked": 726,
            "digestContract": 725,
            "phpOnlyHoldout": 1,
            "holdoutId": "cp-debug-console",
            "holdoutNote": "Intentional LFI surface — PHP deeplink only, no digest/app",
        },
        "fieldFloors": {
            "summaryContractStems": len(summary),
            "hybridListStems": len(hybrid),
            "listItemStems": len(lists),
            "objectStems": len(objects),
            "floorsCreatedThisRun": created,
            "floorsCreatedCount": len(created),
        },
        "pendingTowardZeroPhp": [
            "aspNetInteractiveComplete remains 0 — live writes still PHP",
            "Module-ajax dual-sample pairs still thin (need CloudPanel cookies for PHP ajax field samples)",
            "Authenticated digest dual-sample capture for core CP/ERP still required",
            "Exact-route shadows + RELEASE_OWNER_APPROVAL.md before PHP removal — never invent approval",
        ],
        "ok": True,
    }
    board_path = FLOOR_DIR / "menu-field-parity-built-vs-pending.json"
    board_path.write_text(json.dumps(board, indent=2) + "\n", encoding="utf-8")

    print(json.dumps({"created": len(created), "stems": created, "listStemCount": len(lists), "board": str(board_path)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
