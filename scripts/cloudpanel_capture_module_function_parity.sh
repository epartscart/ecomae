#!/usr/bin/env bash
# Capture/refresh module-function parity inventory stubs from hybrid UI TARGETS.
# Default: contract stubs only (aspnet-complete=0). Never invents pass/approval files.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="${ECOMAE_MODULE_FUNCTION_SAMPLES_DIR:-$ROOT/docs/migration/evidence/module-function-parity}"
OVERWRITE="${ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES:-0}"
mkdir -p "$OUT_DIR"

export ECOMAE_MODULE_FUNCTION_SAMPLES_DIR="$OUT_DIR"
export ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES="$OVERWRITE"
export ECOMAE_HYBRID_CAPTURE="$ROOT/scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh"

python3 - <<'PY'
import datetime
import os
import re
from pathlib import Path

out_dir = Path(os.environ["ECOMAE_MODULE_FUNCTION_SAMPLES_DIR"])
overwrite = os.environ.get("ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES", "0") == "1"
capture = Path(os.environ["ECOMAE_HYBRID_CAPTURE"]).read_text(encoding="utf-8")
block = capture.split("TARGETS = [", 1)[1].split("]", 1)[0]
row_re = re.compile(
    r'\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*,\s*"([^"]+)"'
)
now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
modules = []
for stem, surface, app_route, digest_route, php_path in row_re.findall(block):
    status = "digest-only+hybrid-deeplink" if digest_route else "hybrid-deeplink"
    modules.append(
        {
            "id": stem,
            "surface": surface,
            "aspnetRoute": app_route,
            "digestRoute": digest_route or None,
            "phpPath": php_path,
            "status": status,
            "aspnetComplete": False,
            "writesRemainPhp": True,
            "humanFunctionalEvidence": False,
            "note": "Preview/digest/hybrid only — interactive product chrome remains PHP.",
        }
    )

inventory_path = out_dir / "module-function-inventory.json"
if inventory_path.exists() and not overwrite:
    print(f"keep existing {inventory_path}")
else:
    import json

    doc = {
        "role": "module-function-inventory",
        "capturedAt": now,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspnetCompleteCount": 0,
        "moduleCount": len(modules),
        "source": "scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh TARGETS",
        "note": (
            "Contract inventory derived from hybrid UI TARGETS. "
            "aspnet-complete remains 0 until human MODULE_FUNCTION_TEST_PASS.md exists."
        ),
        "modules": modules,
    }
    inventory_path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {inventory_path} modules={len(modules)}")

readme = out_dir / "README.md"
if overwrite or not readme.exists():
    readme.write_text(
        "# Module function parity evidence\n\n"
        "Contract floor only. `aspnet-complete` count stays **0** until a human attaches "
        "`docs/migration/evidence/presentation/MODULE_FUNCTION_TEST_PASS.md` containing "
        "`MODULE_FUNCTION_PARITY_PASS`.\n\n"
        "Never invent that pass file or `RELEASE_OWNER_APPROVAL.md`. "
        "`cutoverAllowed` always false.\n\n"
        "Operator:\n\n"
        "```bash\n"
        "bash scripts/cloudpanel_run_module_function_parity_operator.sh\n"
        "```\n",
        encoding="utf-8",
    )
    print(f"wrote {readme}")
PY
