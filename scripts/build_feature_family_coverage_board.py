#!/usr/bin/env python3
"""Build honest feature-family coverage board from php-catalog-coverage-board.

Ensures every CP/ERP/BOS/storefront catalog row is classified so portal tools
(mobile, Power BI, marketing, demo, etc.) are never silently dropped.
Never invents aspnetComplete or cutover.
"""
from __future__ import annotations

import argparse
import json
import re
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

FAMILY_RULES = [
    ("mobile-apps", r"mobile|android|ios|app.?store|react.?native|flutter"),
    ("power-bi-analytics", r"power.?bi|metabase|nl.?report|analytics|\bbi\b"),
    ("marketing-growth", r"marketing|broadcast|social|campaign|promo|growth|newsletter|sms.?turn"),
    ("demo-tenants", r"demo.?tenant|demo_tenants|industry.?template|template$"),
    ("ai-automation", r"\bai\b|copilot|auto.?price|automation|orchestrat|import.?orch"),
    ("integrations-api", r"integrat|webhook|api.?key|api.?client|umapi|connector"),
    ("tax-compliance", r"tax|trn|soc2|compliance|governance|failover|isolation"),
    ("pos-retail", r"\bpos\b|retail|jewellery|\bjw_|metal.?sales"),
    ("document-control", r"document|print|pdf|legal.?footer|content.?tree|content.?manager|visual.?page"),
    ("crm-customers", r"customer|crm|prospect|lead|approv|b2b|credit.?limit|balance"),
    ("pricing-catalog-cp", r"price|catalogue|catalog|brand|part.?search|vin|homepage.?product|cross.?ref"),
    ("shipping-logistics", r"ship|logistics|delivery|fulfill|warehouse|storage|wms|mhei"),
    ("hr-payroll", r"people|payroll|leave|\bhr\b|employee"),
    ("production-mfg", r"production|manufactur|quality|\bmfg\b|\bbom\b"),
    ("projects-services", r"project|service.?mgmt|rental|consultancy"),
    ("banking-cash", r"cash|bank|treasury|petty|recon|payment.?batch"),
    ("finance-gl", r"\bcoa\b|ledger|\bgl\b|journal|aging|budget|consolida|fixed.?asset|cost.?acct"),
    ("sales-oms", r"sales|invoice|order|oms|quotation|subscription|revenue|opportunity"),
    ("procurement-ap", r"purchas|procure|payable|supplier|vendor|landed|expense"),
    ("inventory-stock", r"inventory|stock|\bpim\b|master.?plan"),
    ("auth-sessions-users", r"user|group|role|auth|session|login|permission|acl"),
    ("platform-ops-bos", r"fleet|tenant|platform|health|audit|command.?center|\bbos\b|config.?sandbox|operator"),
    ("erp-workspace-misc", r"overview|workflow|approval|agenda|contact|knowledge|common|setup|enterprise"),
    ("storefront-checkout", r"storefront|checkout|payment|return|support|cart|garage|account|profile"),
    ("industry-packs", r"industry|pack|autoworkshop|spare.?part|fashion|medical|electronics"),
]


def family_of(row: dict) -> str:
    blob = " ".join(
        [
            str(row.get("id") or ""),
            str(row.get("label") or ""),
            str(row.get("phpPath") or ""),
            str(row.get("kind") or ""),
        ]
    ).lower()
    for name, pat in FAMILY_RULES:
        if re.search(pat, blob):
            return name
    return "other-unclassified"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--board",
        type=Path,
        default=ROOT / "docs/migration/evidence/surface-parity/php-catalog-coverage-board.json",
    )
    ap.add_argument(
        "--out",
        type=Path,
        default=ROOT
        / "docs/migration/evidence/module-function-parity/feature-family-coverage-board.json",
    )
    args = ap.parse_args()
    board = json.loads(args.board.read_text(encoding="utf-8"))
    items = board.get("items") or []
    by_family: dict[str, list[dict]] = defaultdict(list)
    for row in items:
        by_family[family_of(row)].append(row)

    families = []
    for name in sorted(by_family):
        rows = by_family[name]
        cov = Counter(r.get("coverage") for r in rows)
        families.append(
            {
                "family": name,
                "total": len(rows),
                "digestContract": cov.get("digest-contract", 0),
                "phpOnlyDeeplink": cov.get("php-only-deeplink", 0),
                "digestPercent": round(100.0 * cov.get("digest-contract", 0) / len(rows), 1),
                "aspnetCompleteCount": sum(1 for r in rows if r.get("aspnetComplete") is True),
                "ids": sorted(r["id"] for r in rows),
            }
        )

    out = {
        "role": "feature-family-coverage-board",
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "totalTracked": len(items),
        "familyCount": len(families),
        "coverageCounts": board.get("coverageCounts"),
        "families": families,
        "note": (
            "Every catalog row is classified into a feature family so CP/ERP portal tools "
            "(mobile, Power BI, marketing, demo, AI, etc.) stay visible. "
            "digest-contract is read-digest attachment only — not interactive aspnetComplete. "
            "Never invents RELEASE_OWNER_APPROVAL.md."
        ),
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(
        f"PASS: families={out['familyCount']} totalTracked={out['totalTracked']} "
        f"digest={out['coverageCounts']} cutoverAllowed=false"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
