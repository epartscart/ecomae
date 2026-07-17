"""Configuration for pyapi — reads the existing PHP config.php (same convention
as pyprices) so PHP and Python always share one source of truth during the
migration. Environment variables override for containerized deploys."""

from __future__ import annotations

import os
import re
from functools import lru_cache
from pathlib import Path

# Document root = repo root (pyapi/ lives next to config.php)
DOC_ROOT = Path(__file__).resolve().parent.parent
CONFIG_PHP = DOC_ROOT / "config.php"

_PARAM_RE = re.compile(r"public\s+\$(\w+)\s*=\s*'((?:[^'\\]|\\.)*)'")


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
        # epartscart tenant DB. Multi-tenant routing comes in phase 2;
        # override per-deployment with PYAPI_DB.
        return os.environ.get("PYAPI_DB", php_param("db", "docpart"))

    @property
    def tech_key(self) -> str:
        return php_param("tech_key", "")

    @property
    def pool_size(self) -> int:
        return int(os.environ.get("PYAPI_POOL_SIZE", "8"))


settings = Settings()
