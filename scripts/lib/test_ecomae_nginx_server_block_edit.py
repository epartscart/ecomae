#!/usr/bin/env python3
"""Smoke test: classic-entry installer must install ^~ /storefront/ (click-bounce fix)."""
from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EDIT = ROOT / "scripts/lib/ecomae_nginx_server_block_edit.py"


def load():
    spec = importlib.util.spec_from_file_location("ecomae_nginx_server_block_edit", EDIT)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    return mod


def main() -> int:
    mod = load()
    tenant = (ROOT / "deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf").read_text()
    named, blocks = mod.parse_example(tenant)
    prefixes = [m for (k, m), _ in blocks if k == "prefix"]
    assert "/storefront/" in prefixes, prefixes
    assert "/_framework/" in prefixes, prefixes

    fake = """
server {
  listen 443 ssl;
  server_name www.epartscart.com;
  root /home/ecomae/htdocs/www.epartscart.com;
  location / { try_files $uri /index.php?$args; }
  location = /storefront/search-app {
    return 302 /en/shop/part_search;
  }
}
"""
    out, summary = mod.install_into_host_servers(fake, tenant, "www.epartscart.com")
    assert "location ^~ /storefront/" in out
    assert "proxy_pass http://127.0.0.1:5100;" in out
    assert "return 302 /en/shop/part_search" not in out
    assert summary["prefixStorefront"] is True
    print("PASS classic-entry installs ^~ /storefront/ and strips stub→/en")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
