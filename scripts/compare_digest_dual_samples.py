#!/usr/bin/env python3
"""Walk dual PHP/ASP.NET digest samples and compare against locked field contracts.

Pair resolution order per stem:
  1) php-{stem}.json + aspnet-{stem}.json (full dual, unless --contract-only)
  2) migration/{stem}.json + aspnet-{stem}.json (contract-only baseline; used when
     public digests are already exact-route shadowed so PHP JSON is unavailable)
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMPARE = ROOT / "scripts" / "compare_surface_payload_parity.py"

# route -> (summary path key, required summary fields)
# Covers every surface/storefront digest KPI + /cp/orders-digest (presentation shadow).
SUMMARY_CONTRACTS = {
    "cp-dashboard-summary": (
        "summary",
        "users,adminSessions,portalTenants,activePortalTenants,source,message",
    ),
    "erp-dashboard-summary": (
        "summary",
        "cashPosition,supplierCredit,supplierDebit,supplierNet,cashAccounts,activeSuppliers,activePurchases,source,message",
    ),
    "erp-accounts-summary": (
        "summary",
        "cashPosition,supplierCredit,supplierDebit,supplierNet,cashAccounts,activeSuppliers,activePurchases,source,message",
    ),
    "bos-fleet-summary": (
        "summary",
        "portalTenants,activePortalTenants,adminSessions,withDatabase,erpOnly,source,message",
    ),
    "bos-fleet-health": (
        "summary",
        "portalTenants,activePortalTenants,adminSessions,withDatabase,erpOnly,source,message",
    ),
    "storefront-account-summary": (
        "summary",
        "userId,orders,sessions,garageVehicles,source,message",
    ),
    "cp-orders-digest": (
        "summary",
        "open,today,pendingShip,source,message",
    ),
    "erp-inventory-stock": (
        "summary",
        "rowCount,qtyOnHand,stockValue,warehouseCount,itemCount,source,message",
    ),
    "bos-fleet-readiness": (
        "readiness",
        "tenants,pass,warn,fail,active,withDatabase,erpOnly,source,message",
    ),
}

# List digests: stem -> collection key (envelope ok/surface/key/count/source/message)
LIST_CONTRACTS = {
    "cp-tenants": "tenants",
    "cp-users": "users",
    "cp-groups": "groups",
    "cp-modules": "modules",
    "cp-menus": "menus",
    "cp-pages": "pages",
    "cp-currencies": "currencies",
    "cp-api-clients": "clients",
    "cp-config-items": "items",
    "cp-admin-sessions": "sessions",
    "cp-storages": "storages",
    "erp-suppliers": "suppliers",
    "erp-purchases": "purchases",
    "erp-cash-accounts": "accounts",
    "erp-cash-entries": "entries",
    "erp-coa-accounts": "accounts",
    "erp-warehouses": "warehouses",
    "erp-sales-orders": "orders",
    "erp-purchase-orders": "orders",
    "erp-invoices": "invoices",
    "erp-gl-journals": "journals",
    "bos-tenants": "tenants",
    "bos-audit-log": "entries",
    "storefront-orders": "orders",
    "storefront-garage": "vehicles",
}

# Optional item-field contracts. When the collection is non-empty, first item must include fields.
# Migration goldens listed in LIST_NONEMPTY_MIGRATION must ship a sentinel row (empty fails).
LIST_ITEM_FIELDS = {
    "cp-menus": [
        "id",
        "caption",
        "isFrontend",
        "menuUlClass",
        "menuUlId",
        "structurePresent",
        "structureParseOk",
        "nodeCount",
        "maxDepth",
        "urlLinkCount",
        "contentLinkCount",
        "unknownLinkCount",
    ],
}
LIST_NONEMPTY_MIGRATION = frozenset({"cp-menus"})

# Object digests without a collection array (top-level envelope fields).
OBJECT_CONTRACTS = {
    "storefront-profile": [
        "ok",
        "surface",
        "user_id",
        "email",
        "email_confirmed",
        "phone",
        "phone_confirmed",
        "reg_variant",
        "profile_fields",
        "source",
        "message",
        "session",
        "note",
    ],
}


def is_migration_baseline(path: Path) -> bool:
    """True for migration/ goldens or php-* seeded from those goldens after cutover."""
    if path.parent.name == "migration":
        return True
    try:
        doc = json.loads(path.read_text(encoding="utf-8"))
    except Exception:  # noqa: BLE001
        return False
    if not isinstance(doc, dict):
        return False
    if doc.get("dualSampleBaseline") == "migration-contract-golden":
        return True
    # Captured migration-mode payloads use source=migration (not live DB).
    summary = doc.get("summary") if isinstance(doc.get("summary"), dict) else {}
    readiness = doc.get("readiness") if isinstance(doc.get("readiness"), dict) else {}
    return summary.get("source") == "migration" or readiness.get("source") == "migration"


def resolve_left(samples: Path, stem: str) -> tuple[Path | None, bool]:
    """Return (left_path, used_migration_baseline).

    Prefer a real php-* capture when present and not a seeded migration baseline.
    Otherwise use migration/{stem}.json (or a seeded php-* baseline) for contract-only.
    """
    php = samples / f"php-{stem}.json"
    if php.exists() and not is_migration_baseline(php):
        return php, False
    mig = samples / "migration" / f"{stem}.json"
    if mig.exists():
        return mig, True
    if php.exists() and is_migration_baseline(php):
        return php, True
    return None, False


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--samples-dir",
        default=str(ROOT / "docs/migration/evidence/surface-parity/samples"),
        help="Directory containing php-*.json / aspnet-*.json / migration/",
    )
    parser.add_argument("--contract-only", action="store_true", help="Compare field presence only")
    parser.add_argument(
        "--out",
        type=Path,
        default=None,
        help="Optional compare-result JSON path (always cutoverAllowed=false)",
    )
    args = parser.parse_args()
    samples = Path(args.samples_dir)
    if not samples.is_dir():
        print(f"No samples directory: {samples}")
        return 0

    pairs = 0
    failed = 0
    used_migration = 0
    checked_stems: set[str] = set()

    def check_list_envelope(path: Path, key: str, label: str, stem: str = "") -> None:
        nonlocal pairs, failed
        pairs += 1
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            failed += 1
            print(f"FAIL {label}: {ex}")
            return
        required = ["ok", "surface", key, "count", "source", "message", "session", "note"]
        missing = [k for k in required if k not in doc]
        if missing:
            failed += 1
            print(f"FAIL {label}: missing {missing}")
            return

        rows = doc.get(key)
        item_fields = LIST_ITEM_FIELDS.get(stem) or []
        require_nonempty = (
            stem in LIST_NONEMPTY_MIGRATION
            and ("migration" in label or path.parent.name == "migration")
        )
        if require_nonempty and (not isinstance(rows, list) or len(rows) < 1):
            failed += 1
            print(f"FAIL {label}: expected non-empty {key}[] sentinel for item-field floor")
            return
        if item_fields and isinstance(rows, list) and rows:
            first = rows[0]
            if not isinstance(first, dict):
                failed += 1
                print(f"FAIL {label}: {key}[0] must be object")
                return
            item_missing = [f for f in item_fields if f not in first]
            if item_missing:
                failed += 1
                print(f"FAIL {label}: missing item fields {item_missing}")
                return
        print(f"PASS {label}")

    def check_object_envelope(path: Path, required: list[str], label: str) -> None:
        nonlocal pairs, failed
        pairs += 1
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            failed += 1
            print(f"FAIL {label}: {ex}")
            return
        missing = [k for k in required if k not in doc]
        if missing:
            failed += 1
            print(f"FAIL {label}: missing {missing}")
        else:
            print(f"PASS {label}")

    for stem, (path_key, require) in SUMMARY_CONTRACTS.items():
        left, from_mig = resolve_left(samples, stem)
        asp = samples / f"aspnet-{stem}.json"
        if left is None or not asp.exists():
            continue
        pairs += 1
        checked_stems.add(stem)
        if from_mig:
            used_migration += 1
        contract_only = args.contract_only or from_mig
        cmd = [
            sys.executable,
            str(COMPARE),
            "--left",
            str(left),
            "--right",
            str(asp),
            "--path",
            path_key,
            "--require",
            require,
        ]
        if contract_only:
            cmd.append("--contract-only")
        proc = subprocess.run(cmd, capture_output=True, text=True)
        label = f"migration+aspnet/{stem}" if from_mig else stem
        if proc.returncode == 0:
            print(f"PASS {label}")
        else:
            failed += 1
            print(f"FAIL {label}")
            print(proc.stdout or proc.stderr)

    for stem, key in LIST_CONTRACTS.items():
        left, from_mig = resolve_left(samples, stem)
        asp = samples / f"aspnet-{stem}.json"
        if left is not None and asp.exists():
            if from_mig:
                used_migration += 1
            check_list_envelope(
                left, key, f"{'migration' if from_mig else 'php'}-{stem}", stem=stem
            )
            check_list_envelope(asp, key, f"aspnet-{stem}", stem=stem)
            checked_stems.add(stem)

    for stem, required in OBJECT_CONTRACTS.items():
        left, from_mig = resolve_left(samples, stem)
        asp = samples / f"aspnet-{stem}.json"
        if left is not None and asp.exists():
            if from_mig:
                used_migration += 1
            check_object_envelope(left, required, f"{'migration' if from_mig else 'php'}-{stem}")
            check_object_envelope(asp, required, f"aspnet-{stem}")
            checked_stems.add(stem)

    # Contract-only floor: validate every registered migration golden that lacked an aspnet pair.
    mig = samples / "migration"
    if args.contract_only and mig.is_dir():
        for stem, (path_key, require) in SUMMARY_CONTRACTS.items():
            if stem in checked_stems:
                continue
            path = mig / f"{stem}.json"
            if not path.exists():
                continue
            pairs += 1
            checked_stems.add(stem)
            cmd = [
                sys.executable,
                str(COMPARE),
                "--left",
                str(path),
                "--right",
                str(path),
                "--path",
                path_key,
                "--require",
                require,
                "--contract-only",
            ]
            proc = subprocess.run(cmd, capture_output=True, text=True)
            if proc.returncode == 0:
                print(f"PASS migration/{stem}")
            else:
                failed += 1
                print(f"FAIL migration/{stem}")
                print(proc.stdout or proc.stderr)
        for stem, key in LIST_CONTRACTS.items():
            if stem in checked_stems:
                continue
            path = mig / f"{stem}.json"
            if path.exists():
                check_list_envelope(path, key, f"migration/{stem}", stem=stem)
                checked_stems.add(stem)
        for stem, required in OBJECT_CONTRACTS.items():
            if stem in checked_stems:
                continue
            path = mig / f"{stem}.json"
            if path.exists():
                check_object_envelope(path, required, f"migration/{stem}")
                checked_stems.add(stem)

    report = {
        "pairsChecked": pairs,
        "failed": failed,
        "migrationBaselinePairs": used_migration,
        "contractsRegistered": (
            len(SUMMARY_CONTRACTS) + len(LIST_CONTRACTS) + len(OBJECT_CONTRACTS)
        ),
        "stemsChecked": len(checked_stems),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "contractOnly": bool(args.contract_only) or used_migration > 0,
        "listItemFieldStems": sorted(LIST_ITEM_FIELDS),
        "listNonemptyMigrationStems": sorted(LIST_NONEMPTY_MIGRATION),
        "note": (
            "Digest dual-sample contract floor. List item fields enforced when present; "
            "cp-menus migration golden must keep structure-summary sentinel. "
            "Never invents RELEASE_OWNER_APPROVAL.md."
        ),
    }
    text = json.dumps(report, indent=2) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")
    print(text, end="")
    if pairs == 0:
        print("No dual php-/aspnet- digest sample pairs found (not a failure).")
        print("Capture ASP.NET samples: bash scripts/cloudpanel_capture_digest_dual_samples.sh")
        return 0
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
