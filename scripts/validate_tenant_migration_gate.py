#!/usr/bin/env python3
"""Validate the fail-closed tenant-by-tenant ERP migration gate contract."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


EXPECTED_TENANTS = {
    "epartscart",
    "electronicae",
    "stylenlook",
    "thejewellerytrend",
    "taxofinca",
}
REQUIRED_GATES = {
    "php-route-parity",
    "functional-happy-path",
    "validation-and-denial",
    "tenant-isolation",
    "persisted-readback",
    "browser-parity",
    "recovery-and-rollback",
    "uat-and-production",
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "path",
        nargs="?",
        default="docs/migration/evidence/tenant-by-tenant-erp-migration-gate.json",
    )
    args = parser.parse_args()

    document = json.loads(Path(args.path).read_text(encoding="utf-8"))
    if document.get("cutoverAllowed") is not False:
        raise SystemExit("cutoverAllowed must be false")
    if document.get("readyForPhpRemoval") is not False:
        raise SystemExit("readyForPhpRemoval must be false")

    promotion = document.get("promotionRule", {})
    if set(promotion.get("requiredForEachTenant", [])) != REQUIRED_GATES:
        raise SystemExit("promotion gate list does not match ErpTenantAcceptanceCatalog")
    if promotion.get("allGatesMustPass") is not True:
        raise SystemExit("allGatesMustPass must be true")
    if promotion.get("noPartialGatePromotion") is not True:
        raise SystemExit("noPartialGatePromotion must be true")
    if promotion.get("productionBackupRestoreRequired") is not True:
        raise SystemExit("productionBackupRestoreRequired must be true")
    if promotion.get("releaseOwnerApprovalRequired") is not True:
        raise SystemExit("releaseOwnerApprovalRequired must be true")

    tenants = document.get("tenants", [])
    tenant_names = [tenant.get("tenant") for tenant in tenants]
    if len(tenant_names) != len(set(tenant_names)):
        raise SystemExit("tenant list contains duplicate entries")
    if set(tenant_names) != EXPECTED_TENANTS:
        raise SystemExit("tenant list does not match the named ERP tenant set")
    for tenant in tenants:
        status = tenant.get("status")
        evidence = tenant.get("evidence")
        if status not in {"blocked", "ready"}:
            raise SystemExit(f"{tenant.get('tenant')}: invalid status={status!r}")
        if status == "blocked" and evidence != {}:
            raise SystemExit(f"{tenant.get('tenant')}: blocked tenant cannot claim evidence")
        if status == "ready":
            if not isinstance(evidence, dict):
                raise SystemExit(f"{tenant.get('tenant')}: ready evidence must be an object")
            missing = REQUIRED_GATES - set(evidence)
            if missing:
                raise SystemExit(
                    f"{tenant.get('tenant')}: ready evidence missing gates: {sorted(missing)}"
                )
            if any(not isinstance(value, str) or not value.strip() for value in evidence.values()):
                raise SystemExit(f"{tenant.get('tenant')}: ready evidence references must be non-empty")

    acceptance = document.get("acceptance", {})
    ready_count = sum(tenant.get("status") == "ready" for tenant in tenants)
    if acceptance.get("readyTenantCount") != ready_count:
        raise SystemExit("readyTenantCount does not match tenant statuses")
    if acceptance.get("totalTenantCount") != len(EXPECTED_TENANTS):
        raise SystemExit("totalTenantCount does not match the named tenant set")
    if acceptance.get("erpExitGate") is True and ready_count != len(EXPECTED_TENANTS):
        raise SystemExit("erpExitGate cannot be true while a tenant remains blocked")

    print(f"PASS: {ready_count}/{len(tenants)} tenants have complete staged evidence")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
