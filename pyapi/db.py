"""Pooled MySQL access for pyapi.

Uses mysql-connector-python (already a platform dependency via pyprices) with a
small connection pool. Every query has a hard timeout so a slow query can never
pile up workers the way PHP-FPM requests did (Cloudflare 524s).
"""

from __future__ import annotations

import threading
from contextlib import contextmanager
from typing import Any, Iterator

import mysql.connector
from mysql.connector import pooling

from .config import settings

_POOL_LOCK = threading.Lock()
_POOL: pooling.MySQLConnectionPool | None = None

QUERY_TIMEOUT_MS = 3000  # fail fast: 3s per statement, not 100s


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
        try:
            cur.execute(f"SET SESSION MAX_EXECUTION_TIME={QUERY_TIMEOUT_MS}")
        except mysql.connector.Error:
            pass  # MariaDB uses max_statement_time; ignore if unsupported
        try:
            cur.execute(f"SET SESSION max_statement_time={QUERY_TIMEOUT_MS / 1000}")
        except mysql.connector.Error:
            pass
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
    """Run a write statement; return affected row count."""
    with cursor() as cur:
        cur.execute(sql, params)
        return cur.rowcount
