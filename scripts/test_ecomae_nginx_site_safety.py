#!/usr/bin/env python3
"""Unit tests for ecomae_nginx_site_safety.py (no network)."""
from __future__ import annotations

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MOD_PATH = ROOT / "scripts" / "ecomae_nginx_site_safety.py"


def load_mod():
    spec = importlib.util.spec_from_file_location("ecomae_nginx_site_safety", MOD_PATH)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    return mod


class NginxSiteSafetyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.m = load_mod()

    def test_classify_platform(self):
        self.assertEqual(self.m.classify_site_conf("/etc/nginx/sites-enabled/www.ecomae.com.conf"), "platform")

    def test_classify_tenant(self):
        self.assertEqual(self.m.classify_site_conf("/etc/nginx/sites-enabled/epartscart.com.conf"), "tenant")
        self.assertEqual(self.m.classify_site_conf("/etc/nginx/sites-enabled/www.electronicae.com.conf"), "tenant")

    def test_classify_industry(self):
        self.assertEqual(self.m.classify_site_conf("/etc/nginx/sites-enabled/healthcare.ecomae.com.conf"), "industry")

    def test_platform_allowed(self):
        self.m.assert_shadow_target_allowed("/etc/nginx/sites-enabled/www.ecomae.com.conf", purpose="exact-route")
        self.m.assert_shadow_target_allowed("/etc/nginx/sites-enabled/www.ecomae.com.conf", purpose="presentation")

    def test_tenant_refused_without_confirm(self):
        with self.assertRaises(SystemExit):
            self.m.assert_shadow_target_allowed(
                "/etc/nginx/sites-enabled/epartscart.com.conf",
                purpose="exact-route",
                confirm_tenant="",
            )

    def test_tenant_exact_route_with_confirm(self):
        self.m.assert_shadow_target_allowed(
            "/etc/nginx/sites-enabled/epartscart.com.conf",
            purpose="exact-route",
            confirm_tenant="YES",
        )

    def test_tenant_presentation_hard_refuse(self):
        with self.assertRaises(SystemExit):
            self.m.assert_shadow_target_allowed(
                "/etc/nginx/sites-enabled/epartscart.com.conf",
                purpose="presentation",
                confirm_tenant="YES",
                confirm_tenant_presentation="",
            )

    def test_scan_broad_cutovers(self):
        text = "server {\n  location /cp {\n    proxy_pass http://127.0.0.1:5100;\n  }\n}\n"
        hits = self.m.scan_broad_cutovers(text)
        self.assertTrue(any("location /cp" in h for h in hits))

    def test_scan_exact_ok(self):
        text = "server {\n  location = /cp/dashboard-summary {\n    proxy_pass http://127.0.0.1:5100;\n  }\n}\n"
        self.assertEqual(self.m.scan_broad_cutovers(text), [])


if __name__ == "__main__":
    raise SystemExit(unittest.main())
