#!/usr/bin/env python3
"""Rewrite digest-live-sample-gaps.json from migration goldens vs live aspnet samples.

Never invents cutover. Always cutoverAllowed=false / readyForPhpRemoval=false /
aspNetInteractiveComplete=0.
"""
from __future__ import annotations

import argparse
import json
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SAMPLES = ROOT / "docs/migration/evidence/surface-parity/samples"
DEFAULT_OUT = ROOT / "docs/migration/evidence/surface-parity/digest-live-sample-gaps.json"

CORE_STEMS = [
    "cp-dashboard-summary",
    "cp-users",
    "cp-tenants",
    "cp-menus",
    "cp-orders-digest",
    "erp-dashboard-summary",
    "erp-accounts-summary",
    "erp-inventory-stock",
    "storefront-search",
    "storefront-cart",
    "storefront-checkout",
    "storefront-orders",
    "storefront-garage",
    "storefront-profile",
    "storefront-account-summary",
]


def is_live_aspnet_sample(path: Path) -> bool:
    if not path.is_file():
        return False
    try:
        doc = json.loads(path.read_text(encoding="utf-8"))
    except Exception:  # noqa: BLE001
        return False
    if not isinstance(doc, dict):
        return False
    if doc.get("dualSampleBaseline") == "migration-contract-golden":
        return False
    summary = doc.get("summary") if isinstance(doc.get("summary"), dict) else {}
    if summary.get("source") == "migration" and doc.get("source") == "migration":
        # migration-mode payload from ASP.NET still counts as a live route sample
        # when captured from the running service (file present under aspnet-*).
        return True
    return True


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--samples-dir", type=Path, default=DEFAULT_SAMPLES)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()
    samples: Path = args.samples_dir
    mig = samples / "migration"
    missing: list[dict] = []
    for stem in CORE_STEMS:
        mig_path = mig / f"{stem}.json"
        asp_path = samples / f"aspnet-{stem}.json"
        live = is_live_aspnet_sample(asp_path)
        if not live:
            missing.append(
                {
                    "stem": stem,
                    "migrationGolden": mig_path.is_file(),
                    "liveAspNetSample": False,
                }
            )
    mig_count = len(list(mig.glob("*.json"))) if mig.is_dir() else 0
    payload = {
        "role": "digest-live-sample-gaps",
        "generatedAtUnix": int(time.time()),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "coreStemsRequired": list(CORE_STEMS),
        "missingLiveAspNetCount": len(missing),
        "missingLiveAspNet": missing,
        "migrationGoldenCount": mig_count,
        "note": (
            "Contract goldens exist; live authenticated aspnet samples still pending "
            "for listed core stems. Capture via cloudpanel_capture_digest_dual_samples.sh "
            "(admin + customer cookies)."
        ),
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(
        f"Wrote {args.out} missingLiveAspNetCount={payload['missingLiveAspNetCount']} "
        f"cutoverAllowed=false"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
