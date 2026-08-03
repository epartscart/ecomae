#!/usr/bin/env bash
# Capture ASP.NET login-bridge cookie + session probe samples for Batch 3 dual-sample compare.
# Requires credentials in env (never printed). Does not claim PHP cutover.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${ECOMAE_LOGIN_COOKIE_SAMPLES_DIR:-$ROOT/docs/migration/evidence/login-session-bridge}"
BASE="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"
CONTACT="${ECOMAE_LOGIN_CONTACT:-}"
PASSWORD="${ECOMAE_LOGIN_PASSWORD:-}"
mkdir -p "$OUT_DIR"

printf '== Login cookie dual-sample capture ==\n'
printf 'Base: %s\n' "$BASE"
printf 'Out:  %s\n' "$OUT_DIR"

bash "$ROOT/scripts/cloudpanel_verify_secret_succession_configured.sh" || \
  printf 'WARN: SecretSuccession missing — stubs/live capture may fall back to PHP login.\n'

export ECOMAE_LOGIN_COOKIE_SAMPLES_DIR="$OUT_DIR"
export ECOMAE_ASPNET_BASE_URL="$BASE"
export ECOMAE_LOGIN_CONTACT="$CONTACT"
export ECOMAE_LOGIN_PASSWORD="$PASSWORD"

python3 - <<'PY'
import json, os, subprocess, tempfile, datetime
from pathlib import Path

out_dir = Path(os.environ["ECOMAE_LOGIN_COOKIE_SAMPLES_DIR"])
base = os.environ.get("ECOMAE_ASPNET_BASE_URL", "http://127.0.0.1:5100").rstrip("/")
contact = os.environ.get("ECOMAE_LOGIN_CONTACT") or ""
password = os.environ.get("ECOMAE_LOGIN_PASSWORD") or ""
overwrite = os.environ.get("ECOMAE_OVERWRITE_LOGIN_SAMPLES", "0") == "1"
now = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")

def write_stub(surface: str, kind: str, admin: bool) -> None:
    path = out_dir / f"aspnet-{surface}-login-bridge.json"
    if path.exists() and not overwrite:
        print(f"keep existing {path}")
        return
    if admin:
        cookies = [
            "admin_session=STUB; path=/; httponly; samesite=lax",
            "admin_u_id=0; path=/; httponly; samesite=lax",
        ]
    else:
        cookies = [
            "session=STUB; path=/; httponly; samesite=lax",
            "u_id=0; path=/; httponly; samesite=lax",
        ]
    doc = {
        "surface": surface,
        "role": "aspnet-login-bridge-sample",
        "capturedAt": now,
        "baseUrl": base,
        "setCookie": cookies,
        "probe": {"kind": kind, "has_backend_access": admin},
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "note": "Contract stub. Re-run with ECOMAE_LOGIN_CONTACT/PASSWORD against loopback after redeploy.",
    }
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"wrote stub {path}")

def capture(surface: str) -> None:
    jar = tempfile.NamedTemporaryFile(delete=False)
    body = tempfile.NamedTemporaryFile(delete=False)
    hdr = tempfile.NamedTemporaryFile(delete=False)
    jar.close(); body.close(); hdr.close()
    payload = json.dumps({
        "contact": contact,
        "password": password,
        "contact_type": "email",
        "surface": surface,
        "remember_me": False,
    })
    try:
        proc = subprocess.run(
            [
                "curl", "-sS", "-o", body.name, "-D", hdr.name, "-c", jar.name,
                "-w", "%{http_code}",
                "-X", "POST", f"{base}/auth/login/admin",
                "-H", "Content-Type: application/json",
                "-H", "Accept: application/json",
                "--data", payload,
            ],
            check=False,
            capture_output=True,
            text=True,
        )
        code = int((proc.stdout or "0").strip() or "0")
        set_cookie = []
        for line in Path(hdr.name).read_text(encoding="utf-8", errors="replace").splitlines():
            if line.lower().startswith("set-cookie:"):
                set_cookie.append(line.split(":", 1)[1].strip())
        cookie_parts = []
        for line in Path(jar.name).read_text(encoding="utf-8", errors="replace").splitlines():
            if not line or line.startswith("#"):
                continue
            cols = line.split("\t")
            if len(cols) >= 7:
                cookie_parts.append(f"{cols[5]}={cols[6]}")
        cookie_header = "; ".join(cookie_parts)
        probe = {}
        if cookie_header:
            p = subprocess.run(
                ["curl", "-sS", "-H", f"Cookie: {cookie_header}", f"{base}/auth/session/probe"],
                check=False, capture_output=True, text=True,
            )
            try:
                probe = json.loads(p.stdout or "{}")
            except json.JSONDecodeError:
                probe = {"raw": p.stdout}
        doc = {
            "surface": surface,
            "role": "aspnet-login-bridge-sample",
            "capturedAt": now,
            "baseUrl": base,
            "httpStatus": code,
            "setCookie": set_cookie,
            "probe": probe,
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": "Live capture from POST /auth/login/admin. BOS admin cookies do not satisfy PHP /BOS/ $_SESSION.",
        }
        path = out_dir / f"aspnet-{surface}-login-bridge.json"
        path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
        print(f"wrote {path} status={code}")
        if code >= 400 or not set_cookie:
            raise RuntimeError(f"capture weak for {surface}: status={code} cookies={len(set_cookie)}")
    finally:
        for p in (jar.name, body.name, hdr.name):
            try:
                os.unlink(p)
            except OSError:
                pass

surfaces = [
    ("cp", "Admin", True),
    ("erp", "Admin", True),
    ("bos", "Admin", True),
    ("storefront", "Customer", False),
]

if contact and password:
    for surface, kind, admin in surfaces:
        try:
            capture(surface)
        except Exception as exc:  # noqa: BLE001
            print(f"WARN: {surface} live capture failed ({exc}); writing stub")
            write_stub(surface, kind, admin)
else:
    print("No ECOMAE_LOGIN_CONTACT/PASSWORD — writing contract stubs.")
    for surface, kind, admin in surfaces:
        write_stub(surface, kind, admin)
PY

python3 "$ROOT/scripts/compare_login_cookie_dual_samples.py" \
  --samples-dir "$OUT_DIR" \
  --out "$OUT_DIR/compare-result.json"

printf 'Done. cutoverAllowed remains false. Evidence: %s\n' "$OUT_DIR"
