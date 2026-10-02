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
    if promotion.get("noPartialTenantPromotion") is not True:
        raise SystemExit("noPartialTenantPromotion must be true")

    tenants = document.get("tenants", [])
    if {tenant.get("tenant") for tenant in tenants} != EXPECTED_TENANTS:
        raise SystemExit("tenant list does not match the named ERP tenant set")
    for tenant in tenants:
        if tenant.get("status") != "blocked":
            raise SystemExit(f"{tenant.get('tenant')}: status must remain blocked")
        if tenant.get("evidence") != {}:
            raise SystemExit(f"{tenant.get('tenant')}: evidence must be direct and non-invented")

    acceptance = document.get("acceptance", {})
    if acceptance.get("readyTenantCount") != 0:
        raise SystemExit("readyTenantCount must remain zero")
    if acceptance.get("totalTenantCount") != len(EXPECTED_TENANTS):
        raise SystemExit("totalTenantCount does not match the named tenant set")
    if acceptance.get("erpExitGate") is not False:
        raise SystemExit("erpExitGate must remain false")

    print(f"PASS: {len(tenants)} named tenants remain fail-closed and unpromoted")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
