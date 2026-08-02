#!/usr/bin/env python3
"""Walk dual PHP/ASP.NET digest samples and compare against locked field contracts."""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMPARE = ROOT / "scripts" / "compare_surface_payload_parity.py"

# route -> (summary path key, required summary fields)
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
    "cp-config-items": "items",
    "cp-admin-sessions": "sessions",
    "cp-storages": "storages",
    "erp-cash-accounts": "accounts",
    "erp-cash-entries": "entries",
    "bos-tenants": "tenants",
}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--samples-dir",
        default=str(ROOT / "docs/migration/evidence/surface-parity/samples"),
        help="Directory containing php-*.json and aspnet-*.json pairs",
    )
    parser.add_argument("--contract-only", action="store_true", help="Compare field presence only")
    args = parser.parse_args()
    samples = Path(args.samples_dir)
    if not samples.is_dir():
        print(f"No samples directory: {samples}")
        return 0

    pairs = 0
    failed = 0
    for stem, (path_key, require) in SUMMARY_CONTRACTS.items():
        php = samples / f"php-{stem}.json"
        asp = samples / f"aspnet-{stem}.json"
        if not php.exists() or not asp.exists():
            continue
        pairs += 1
        cmd = [
            sys.executable,
            str(COMPARE),
            "--left",
            str(php),
            "--right",
            str(asp),
            "--path",
            path_key,
            "--require",
            require,
        ]
        if args.contract_only:
            cmd.append("--contract-only")
        proc = subprocess.run(cmd, capture_output=True, text=True)
        if proc.returncode == 0:
            print(f"PASS {stem}")
        else:
            failed += 1
            print(f"FAIL {stem}")
            print(proc.stdout or proc.stderr)

    def check_list_envelope(path: Path, key: str, label: str) -> None:
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
        else:
            print(f"PASS {label}")

    for stem, key in LIST_CONTRACTS.items():
        php = samples / f"php-{stem}.json"
        asp = samples / f"aspnet-{stem}.json"
        if php.exists() and asp.exists():
            check_list_envelope(php, key, f"php-{stem}")
            check_list_envelope(asp, key, f"aspnet-{stem}")

    # Also accept migration-mode goldens for contract-only self-check.
    mig = samples / "migration"
    if args.contract_only and mig.is_dir():
        for stem, (path_key, require) in SUMMARY_CONTRACTS.items():
            path = mig / f"{stem}.json"
            if not path.exists():
                continue
            pairs += 1
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
            path = mig / f"{stem}.json"
            if path.exists():
                check_list_envelope(path, key, f"migration/{stem}")

    report = {"pairsChecked": pairs, "failed": failed, "cutoverAllowed": False}
    print(json.dumps(report))
    if pairs == 0:
        print("No dual php-/aspnet- digest sample pairs found (not a failure).")
        return 0
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
