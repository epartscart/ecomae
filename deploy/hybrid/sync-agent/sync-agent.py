#!/usr/bin/env python3
"""Outbound HTTPS hybrid sync agent.

Masters pull from the cloud; transactions ingest from the site outbox.
Empty outbox / empty pull is success — this agent does not invent a send.
LOCAL_DSN is used only when the operator supplies user+password+database.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

SECRET_KEYS = {"password", "db_password", "secret", "token", "api_key", "enrollment", "enrollment_request_id"}


def load_env(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if path.is_file():
        for raw in path.read_text(encoding="utf-8").splitlines():
            line = raw.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            values[key.strip()] = value.strip()
    values.update({k: v for k, v in os.environ.items() if v})
    return values


def require_https(url: str) -> str:
    url = url.rstrip("/")
    if not url.startswith("https://"):
        raise SystemExit("CLOUD_BASE_URL must be HTTPS. The hybrid agent is outbound-only.")
    return url


def sha256_hex(payload: str) -> str:
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def sanitize(obj: object) -> dict:
    if not isinstance(obj, dict):
        return {}
    return {k: v for k, v in obj.items() if str(k).lower() not in SECRET_KEYS}


def read_json_list(path: Path) -> list[dict]:
    if not path.is_file():
        return []
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return []
    return data if isinstance(data, list) else []


def write_json(path: Path, data: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")


def post_json(url: str, body: dict, enrollment: str, timeout: int = 45) -> dict:
    payload = json.dumps(body).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=payload,
        method="POST",
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
            "X-EcomAE-Enrollment": enrollment,
        },
    )
    context = ssl.create_default_context()
    try:
        with urllib.request.urlopen(request, timeout=timeout, context=context) as response:
            raw = response.read().decode("utf-8")
    except urllib.error.HTTPError as exc:
        raw = exc.read().decode("utf-8", errors="replace")
        try:
            parsed = json.loads(raw) if raw else {}
        except json.JSONDecodeError:
            parsed = {"ok": False, "message": raw or str(exc)}
        parsed.setdefault("ok", False)
        parsed.setdefault("http_status", exc.code)
        return parsed
    except urllib.error.URLError as exc:
        return {"ok": False, "message": str(exc.reason)}
    return json.loads(raw) if raw else {}


class Agent:
    def __init__(self, env: dict[str, str], root: Path) -> None:
        self.env = env
        self.site_key = env.get("SITE_KEY", "").strip()
        self.installation_id = env.get("INSTALLATION_ID", "").strip()
        self.enrollment = env.get("ENROLLMENT_REQUEST_ID", "").strip()
        self.cloud = require_https(env.get("CLOUD_BASE_URL", ""))
        self.confirm = env.get("CONFIRM_WRITES", "0").strip() in {"1", "true", "yes"}
        self.state_dir = Path(env.get("STATE_DIR") or (root / "state"))
        self.state_dir.mkdir(parents=True, exist_ok=True)
        self.dsn = env.get("LOCAL_DSN", "").strip()
        if not self.site_key or not self.installation_id or not self.enrollment:
            raise SystemExit("SITE_KEY, INSTALLATION_ID and ENROLLMENT_REQUEST_ID are required.")
        if not self.enrollment.startswith("enr_"):
            raise SystemExit("ENROLLMENT_REQUEST_ID must start with enr_. Do not invent one.")

    def cycle(self) -> int:
        pulled = self.pull()
        ingested = self.ingest()
        return 0 if pulled and ingested else 1

    def pull(self) -> bool:
        body = {
            "siteKey": self.site_key,
            "installationId": self.installation_id,
            "confirmWrites": False,
        }
        result = post_json(self.cloud + "/cp/hybrid-sync/pull", body, self.enrollment)
        envelopes = result.get("envelopes") or []
        if not envelopes:
            print("No pending master envelopes. This agent does not invent a send.")
            return True
        inbox_path = self.state_dir / "inbox.json"
        existing = {row.get("envelopeId"): row for row in read_json_list(inbox_path)}
        for row in envelopes:
            existing[row.get("envelopeId") or row.get("EnvelopeId")] = row
            self.store_inbox_row(row)
        write_json(inbox_path, list(existing.values()))
        ids = [row.get("envelopeId") or row.get("EnvelopeId") for row in envelopes if row.get("envelopeId") or row.get("EnvelopeId")]
        if self.confirm and ids:
            ack = post_json(
                self.cloud + "/cp/hybrid-sync/ack",
                {
                    "siteKey": self.site_key,
                    "installationId": self.installation_id,
                    "envelopeIds": ids,
                    "confirmWrites": True,
                },
                self.enrollment,
            )
            print("ack", ack.get("validation_code") or ack.get("message"))
        else:
            print("Pulled", len(envelopes), "envelope(s) into", inbox_path, "(ack skipped until CONFIRM_WRITES=1).")
        return bool(result.get("ok", True))

    def ingest(self) -> bool:
        rows = self.load_outbox()
        if not rows:
            print("No local change-log rows. This agent does not invent a send.")
            return True
        ok = True
        remaining = []
        for row in rows:
            payload = sanitize(row.get("payload") or row.get("payloadJson") or {})
            if isinstance(payload, str):
                payload_json = payload
            else:
                payload_json = json.dumps(payload, separators=(",", ":"))
            envelope = {
                "envelopeId": row.get("envelopeId") or ("env_" + sha256_hex(payload_json)[:16]),
                "tenantKey": self.site_key,
                "installationId": self.installation_id,
                "entityType": row.get("entityType") or row.get("entity_type"),
                "entityId": str(row.get("entityId") or row.get("entity_id") or ""),
                "version": int(row.get("version") or 1),
                "direction": "hybrid-to-cloud",
                "payloadHash": sha256_hex(payload_json),
                "payloadJson": payload_json,
                "confirmWrites": self.confirm,
            }
            result = post_json(self.cloud + "/cp/hybrid-sync/ingest", envelope, self.enrollment)
            code = result.get("validation_code") or ""
            print("ingest", envelope["envelopeId"], code or result.get("message"))
            if result.get("ok") and code in {"accepted", "replay"} and self.confirm:
                self.mark_outbox_sent(row)
            else:
                remaining.append(row)
                if not result.get("ok"):
                    ok = False
                if code == "ownership-conflict" or code == "version-conflict":
                    self.queue_conflict(row, code, result.get("message") or "")
        write_json(self.state_dir / "outbox.json", remaining)
        return ok

    def load_outbox(self) -> list[dict]:
        rows = read_json_list(self.state_dir / "outbox.json")
        if not self.dsn:
            return rows
        parsed = urllib.parse.urlparse(self.dsn)
        if not parsed.username or not parsed.password or not (parsed.path or "").lstrip("/"):
            print("LOCAL_DSN is incomplete — refusing to invent a MySQL user. Using JSON outbox only.", file=sys.stderr)
            return rows
        sql_rows = self.mysql_fetch(
            "SELECT envelope_id, entity_type, entity_id, version, payload_json FROM epc_hybrid_sync_outbox WHERE status = 'queued' ORDER BY id ASC LIMIT 200"
        )
        for item in sql_rows:
            rows.append(
                {
                    "envelopeId": item[0],
                    "entityType": item[1],
                    "entityId": item[2],
                    "version": item[3],
                    "payloadJson": item[4],
                }
            )
        return rows

    def store_inbox_row(self, row: dict) -> None:
        if not self.dsn:
            return
        parsed = urllib.parse.urlparse(self.dsn)
        if not parsed.username or not parsed.password or not (parsed.path or "").lstrip("/"):
            return
        payload = row.get("payloadJson") or row.get("PayloadJson") or "{}"
        self.mysql_exec(
            """
            INSERT INTO epc_hybrid_sync_inbox
              (envelope_id, entity_type, entity_id, version, payload_json, payload_hash, status, created_at)
            VALUES (%s, %s, %s, %s, %s, %s, 'received', %s)
            ON DUPLICATE KEY UPDATE payload_json = VALUES(payload_json), status = 'received'
            """,
            (
                row.get("envelopeId") or row.get("EnvelopeId"),
                row.get("entityType") or row.get("EntityType"),
                row.get("entityId") or row.get("EntityId"),
                row.get("version") or row.get("Version") or 0,
                payload if isinstance(payload, str) else json.dumps(payload),
                row.get("payloadHash") or row.get("PayloadHash") or sha256_hex(payload if isinstance(payload, str) else json.dumps(payload)),
                datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S"),
            ),
        )

    def mark_outbox_sent(self, row: dict) -> None:
        if not self.dsn:
            return
        envelope_id = row.get("envelopeId") or row.get("envelope_id")
        if not envelope_id:
            return
        self.mysql_exec("UPDATE epc_hybrid_sync_outbox SET status = 'sent' WHERE envelope_id = %s", (envelope_id,))

    def queue_conflict(self, row: dict, reason: str, detail: str) -> None:
        conflicts = read_json_list(self.state_dir / "conflicts.json")
        conflicts.append(
            {
                "envelopeId": row.get("envelopeId"),
                "entityType": row.get("entityType") or row.get("entity_type"),
                "entityId": row.get("entityId") or row.get("entity_id"),
                "reason": reason,
                "detail": detail,
                "queuedAt": datetime.now(timezone.utc).isoformat(),
                "resolution": "queued",
            }
        )
        write_json(self.state_dir / "conflicts.json", conflicts)

    def mysql_connect(self):
        parsed = urllib.parse.urlparse(self.dsn)
        password = urllib.parse.unquote(parsed.password or "")
        user = urllib.parse.unquote(parsed.username or "")
        database = (parsed.path or "").lstrip("/")
        if not user or not password or not database:
            raise RuntimeError("LOCAL_DSN missing user, password, or database")
        try:
            import pymysql  # type: ignore
        except ImportError:
            print("pymysql is not installed — JSON state only.", file=sys.stderr)
            return None
        return pymysql.connect(
            host=parsed.hostname or "127.0.0.1",
            port=parsed.port or 3306,
            user=user,
            password=password,
            database=database,
            charset="utf8mb4",
        )

    def mysql_fetch(self, sql: str) -> list[tuple]:
        conn = self.mysql_connect()
        if conn is None:
            return []
        try:
            with conn.cursor() as cur:
                cur.execute(sql)
                return list(cur.fetchall())
        finally:
            conn.close()

    def mysql_exec(self, sql: str, params: tuple) -> None:
        conn = self.mysql_connect()
        if conn is None:
            return
        try:
            with conn.cursor() as cur:
                cur.execute(sql, params)
            conn.commit()
        finally:
            conn.close()


def main() -> int:
    parser = argparse.ArgumentParser(description="ecomae hybrid outbound HTTPS sync agent")
    parser.add_argument("command", choices=["pull", "ingest", "cycle"])
    parser.add_argument("--loop", action="store_true")
    parser.add_argument("--env", default="")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    env_path = Path(args.env) if args.env else root / ".env"
    env = load_env(env_path)
    agent = Agent(env, root)
    seconds = int(env.get("CYCLE_SECONDS") or "300")

    def run() -> int:
        if args.command == "pull":
            return 0 if agent.pull() else 1
        if args.command == "ingest":
            return 0 if agent.ingest() else 1
        return agent.cycle()

    if not args.loop:
        return run()
    while True:
        run()
        time.sleep(max(30, seconds))


if __name__ == "__main__":
    sys.exit(main())
