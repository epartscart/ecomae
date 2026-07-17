"""pyapi core — config, DB pool, session auth, schema (one file).

Config: reads the existing PHP config.php (same convention as pyprices) so PHP
and Python share one source of truth; env vars override (PYAPI_*).

DB: pooled mysql-connector with a hard 3s per-statement timeout — a slow query
returns an error instead of holding a worker (the PHP-FPM 524 failure mode).

Auth: validates the same `sessions` table the web login writes, so the apps and
APIs reuse the existing login (admin cookie for CP/ERP, customer for storefront).
"""

from __future__ import annotations

import os
import re
import threading
import time
from contextlib import contextmanager
from functools import lru_cache
from pathlib import Path
from typing import Any, Iterator

import mysql.connector
from fastapi import HTTPException, Request
from mysql.connector import pooling

DOC_ROOT = Path(__file__).resolve().parent.parent
CONFIG_PHP = DOC_ROOT / "config.php"
_PARAM_RE = re.compile(r"public\s+\$(\w+)\s*=\s*'((?:[^'\\]|\\.)*)'")

QUERY_TIMEOUT_MS = 3000


@lru_cache(maxsize=1)
def _php_params() -> dict[str, str]:
    params: dict[str, str] = {}
    try:
        content = CONFIG_PHP.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return params
    for match in _PARAM_RE.finditer(content):
        params[match.group(1)] = match.group(2).replace("\\'", "'").replace("\\\\", "\\")
    return params


def php_param(name: str, default: str = "") -> str:
    env = os.environ.get(f"PYAPI_{name.upper()}")
    if env:
        return env
    return _php_params().get(name, default)


class Settings:
    """Resolved settings (env > config.php > default)."""

    @property
    def db_host(self) -> str:
        return php_param("host", "127.0.0.1")

    @property
    def db_user(self) -> str:
        return php_param("user", "")

    @property
    def db_password(self) -> str:
        return php_param("password", "")

    @property
    def db_name(self) -> str:
        return os.environ.get("PYAPI_DB", php_param("db", "docpart"))

    @property
    def tech_key(self) -> str:
        return php_param("tech_key", "")

    @property
    def pool_size(self) -> int:
        return int(os.environ.get("PYAPI_POOL_SIZE", "8"))


settings = Settings()

# ── DB pool ──────────────────────────────────────────────────────────────────

_POOL_LOCK = threading.Lock()
_POOL: pooling.MySQLConnectionPool | None = None


def _pool() -> pooling.MySQLConnectionPool:
    global _POOL
    if _POOL is None:
        with _POOL_LOCK:
            if _POOL is None:
                _POOL = pooling.MySQLConnectionPool(
                    pool_name="pyapi",
                    pool_size=settings.pool_size,
                    host=settings.db_host,
                    user=settings.db_user,
                    password=settings.db_password,
                    database=settings.db_name,
                    charset="utf8mb4",
                    autocommit=True,
                    connection_timeout=5,
                )
    return _POOL


@contextmanager
def cursor() -> Iterator[Any]:
    conn = _pool().get_connection()
    try:
        cur = conn.cursor(dictionary=True)
        for stmt in (
            f"SET SESSION MAX_EXECUTION_TIME={QUERY_TIMEOUT_MS}",
            f"SET SESSION max_statement_time={QUERY_TIMEOUT_MS / 1000}",
        ):
            try:
                cur.execute(stmt)
            except mysql.connector.Error:
                pass  # MySQL vs MariaDB naming — one of the two applies
        try:
            yield cur
        finally:
            cur.close()
    finally:
        conn.close()


def fetch_all(sql: str, params: tuple = ()) -> list[dict]:
    with cursor() as cur:
        cur.execute(sql, params)
        return list(cur.fetchall())


def fetch_one(sql: str, params: tuple = ()) -> dict | None:
    with cursor() as cur:
        cur.execute(sql, params)
        row = cur.fetchone()
        return dict(row) if row else None


def execute(sql: str, params: tuple = ()) -> int:
    with cursor() as cur:
        cur.execute(sql, params)
        return cur.rowcount


# ── Session auth (shared with the PHP web login) ────────────────────────────

def _valid_session(session: str, user_id: int, admin: bool) -> bool:
    if not session or user_id <= 0:
        return False
    try:
        if admin:
            row = fetch_one(
                "SELECT COUNT(*) AS c FROM `sessions` "
                "WHERE `session` = %s AND `type` = 1 AND `user_id` = %s",
                (session, user_id),
            )
        else:
            row = fetch_one(
                "SELECT COUNT(*) AS c FROM `sessions` WHERE `session` = %s AND `user_id` = %s",
                (session, user_id),
            )
    except Exception:
        return False
    return bool(row and int(row.get("c", 0)) == 1)


def require_admin(request: Request) -> int:
    """Authenticated CP admin user id, or 401 (same rule as epc_cp_auth_gate)."""
    session = request.cookies.get("admin_session", "")
    try:
        user_id = int(request.cookies.get("admin_u_id", "0") or "0")
    except ValueError:
        user_id = 0
    if not _valid_session(session, user_id, admin=True):
        raise HTTPException(status_code=401, detail="Admin session required")
    return user_id


def require_customer(request: Request) -> int:
    session = request.cookies.get("session", "")
    try:
        user_id = int(request.cookies.get("u_id", "0") or "0")
    except ValueError:
        user_id = 0
    if not _valid_session(session, user_id, admin=False):
        raise HTTPException(status_code=401, detail="Login required")
    return user_id


# ── Schema (ops-only DDL — run via `python -m pyapi.main setup`) ─────────────

PUSH_SCHEMA = """
CREATE TABLE IF NOT EXISTS `epc_push_devices` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `token` VARCHAR(512) NOT NULL,
  `platform` VARCHAR(16) NOT NULL DEFAULT 'android',
  `user_id` INT NOT NULL DEFAULT 0,
  `app` VARCHAR(32) NOT NULL DEFAULT 'cp',
  `updated_at` INT NOT NULL DEFAULT 0,
  UNIQUE KEY `token` (`token`(191)),
  KEY `app` (`app`),
  KEY `user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
"""


def ensure_schema() -> None:
    execute(PUSH_SCHEMA)


def state_file() -> Path:
    return Path(os.environ.get("PYAPI_WORKER_STATE", "/tmp/pyapi_worker_state.json"))


def now_ts() -> int:
    return int(time.time())
