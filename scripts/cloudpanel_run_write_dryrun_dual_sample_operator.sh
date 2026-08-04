#!/usr/bin/env bash
# Offline/CI + CloudPanel: write-dryrun dual-sample contract floor (writes=0).
# Always seeds + compares migration-contract-golden pairs (183 unique probe paths).
# Optionally probes live ASP.NET when /health is 200.
# Never invents cutover / RELEASE_OWNER_APPROVAL.md. Never deletes PHP.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OUT_DIR="${ECOMAE_WRITE_DRYRUN_EVIDENCE_DIR:-$ROOT/docs/migration/evidence/write-dryruns}"
COMPARE_OUT="${ECOMAE_WRITE_DRYRUN_COMPARE_OUT:-$OUT_DIR/compare-result.json}"
OPERATOR_OUT="${ECOMAE_WRITE_DRYRUN_OPERATOR_OUT:-$OUT_DIR/write-dryrun-operator-result.json}"
mkdir -p "$OUT_DIR"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

export ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"

printf '== Write-dryrun dual-sample contract floor ==\n'
python3 "$ROOT/scripts/generate_write_dryrun_contract_samples.py" --dir "$OUT_DIR"
python3 "$ROOT/scripts/compare_write_dryrun_dual_samples.py" \
  --dir "$OUT_DIR" \
  --contract-only \
  --out "$COMPARE_OUT"

PROBE_OK=0
PROBE_SKIPPED=1
REQUIRE_LIVE="${ECOMAE_WRITE_DRYRUN_REQUIRE_LIVE_PROBE:-0}"
if [[ "$REQUIRE_LIVE" == "1" ]]; then
  PROBE_SKIPPED=0
  echo "write dry-run operator: ECOMAE_WRITE_DRYRUN_REQUIRE_LIVE_PROBE=1 — running live probe"
  if bash "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh"; then
    PROBE_OK=1
  fi
else
  echo "write dry-run operator: live probe optional (set ECOMAE_WRITE_DRYRUN_REQUIRE_LIVE_PROBE=1 on CloudPanel)"
fi

python3 - "$OPERATOR_OUT" "$COMPARE_OUT" "$PROBE_OK" "$PROBE_SKIPPED" "$REQUIRE_LIVE" <<'PY'
import json, sys, time
from pathlib import Path

out, compare_path = Path(sys.argv[1]), Path(sys.argv[2])
probe_ok, probe_skipped, require_live = int(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
compare = json.loads(compare_path.read_text(encoding="utf-8"))
if compare.get("cutoverAllowed") is True or compare.get("readyForPhpRemoval") is True:
    raise SystemExit("FAIL: compare-result invents cutover/removal")
if not compare.get("ok"):
    raise SystemExit(f"FAIL: write-dryrun compare not ok errors={compare.get('errors')}")
if int(compare.get("contractPairsOk") or 0) < 183:
    raise SystemExit(
        f"FAIL: contractPairsOk={compare.get('contractPairsOk')} < 183"
    )
doc = {
    "role": "write-dryrun-dual-sample-operator",
    "generatedAtUnix": int(time.time()),
    "probePassed": bool(probe_ok),
    "probeSkipped": bool(probe_skipped),
    "requireLiveProbe": require_live == "1",
    "contractPairs": compare.get("contractPairs"),
    "contractPairsOk": compare.get("contractPairsOk"),
    "cutoverAllowed": False,
    "readyForPhpRemoval": False,
    "aspNetInteractiveComplete": 0,
    "phpAuthoritative": True,
    "compareResult": str(compare_path),
    "note": (
        "Contract floor pairs Wave B probe routes with migration-contract-golden baselines. "
        "Live PHP ajax remains authoritative. Do not invent RELEASE_OWNER_APPROVAL.md."
    ),
    "next": [
        "On CloudPanel: ECOMAE_WRITE_DRYRUN_REQUIRE_LIVE_PROBE=1 bash scripts/cloudpanel_run_write_dryrun_dual_sample_operator.sh",
        "Capture paired live PHP ajax vs ASP.NET dry-run JSON when cookies available",
        "Keep cutoverAllowed=false until human dual-sample + RELEASE_OWNER_APPROVAL.md",
    ],
}
out.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
if require_live == "1" and not probe_ok:
    raise SystemExit(f"FAIL: live write dry-run probe failed; contract ok; result -> {out}")
print(
    f"PASS: write-dryrun operator contractPairsOk={doc['contractPairsOk']} "
    f"probePassed={doc['probePassed']} probeSkipped={doc['probeSkipped']} "
    f"cutoverAllowed=false -> {out}"
)
PY
