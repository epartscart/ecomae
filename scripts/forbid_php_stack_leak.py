#!/usr/bin/env python3
"""Refuse new PHP files and production UI that mentions PHP / classic / classical.

Existing PHP stays on disk (readyToRemovePhp=true). New product work is ASP.NET Core only.
Gated deploy runs this before publish so a mistaken SHA cannot ship stack words or new PHP.
"""
from __future__ import annotations

import hashlib
import re
import subprocess
import sys
from pathlib import Path

FROZEN_PHP_COUNT = 3091
FROZEN_ASPNET_PHP_COUNT = 19
FROZEN_PHP_PATH_SHA256 = "6a9e1a66b7c5ce640e7f5552ad7ac18eafc1718971e1030744804b3812f820da"

WORD = re.compile(r"\b(PHP|classic|classical|ASP\.NET)\b", re.I)
SKIP_RAZOR = {"MigrationCompareConsole.razor", "ZeroPhpConsole.razor"}
SKIP_DIR_PARTS = {"Migration"}
ALLOW_REFERENCE_HREF = {"ErpModuleApp.razor", "PhpHybridWorkspaceFrame.razor"}
VISIBLE_ATTRS = (
    "title", "subtitle", "placeholder", "aria-label", "alt", "label",
    "aria-description", "aria-roledescription",
)


def repo_root() -> Path:
    here = Path(__file__).resolve()
    for p in [here.parent, *here.parents]:
        if (p / "docs/governance/php-source-allowlist.txt").exists():
            return p
    raise SystemExit("REPO_ROOT_MISSING")


def read_allowlist(root: Path) -> list[str]:
    lines = (root / "docs/governance/php-source-allowlist.txt").read_text(encoding="utf-8").splitlines()
    return sorted({ln.strip() for ln in lines if ln.strip() and not ln.startswith("#")})


def tracked_php(root: Path) -> list[str]:
    out = subprocess.check_output(["git", "ls-files", "*.php"], cwd=root, text=True)
    return sorted({ln.strip() for ln in out.splitlines() if ln.strip()})


def sha256_paths(paths: list[str]) -> str:
    return hashlib.sha256(("\n".join(paths) + "\n").encode("utf-8")).hexdigest()


def visible_copy(text: str) -> str:
    text = re.sub(r"@\*.*?\*@", "", text, flags=re.S)
    text = re.sub(r"<!--.*?-->", "", text, flags=re.S)
    text = re.sub(r"<script\b[^>]*>.*?</script>", "", text, flags=re.S | re.I)
    text = re.sub(r"<style\b[^>]*>.*?</style>", "", text, flags=re.S | re.I)
    text = re.sub(r"@code\s*\{.*\}\s*$", "", text, flags=re.S)
    attrs = []
    for name in VISIBLE_ATTRS:
        attrs.extend(re.findall(rf'{name}\s*=\s*"([^"]*)"', text, flags=re.I))
        attrs.extend(re.findall(rf"{name}\s*=\s*'([^']*)'", text, flags=re.I))
    # Drop tags / attributes so only text nodes remain.
    text = re.sub(r"<[^>]+>", " ", text)
    return " ".join(attrs + [text])


def scan_razor(root: Path) -> list[str]:
    components = root / "aspnet/src/EcomAE.Platform/Components"
    offenders: list[str] = []
    for path in components.rglob("*.razor"):
        if path.name in SKIP_RAZOR or any(part in SKIP_DIR_PARTS for part in path.parts):
            continue
        raw = path.read_text(encoding="utf-8", errors="replace")
        visible = visible_copy(raw)
        for match in WORD.finditer(visible):
            offenders.append(f"{path.relative_to(root)}: '{match.group(0)}'")
        if re.search(r"PhpReferenceOnlyHref\s*\(", raw) and path.name not in ALLOW_REFERENCE_HREF:
            offenders.append(f"{path.relative_to(root)}: PhpReferenceOnlyHref")
        if re.search(r'<a[^>]+href\s*=\s*["\']/php-reference/', raw, flags=re.I):
            offenders.append(f"{path.relative_to(root)}: /php-reference link")
    return offenders


def main() -> int:
    root = repo_root()
    allow = read_allowlist(root)
    tracked = tracked_php(root)
    errors: list[str] = []

    if len(allow) != FROZEN_PHP_COUNT:
        errors.append(f"allowlist_count={len(allow)} expected={FROZEN_PHP_COUNT}")
    if sha256_paths(allow) != FROZEN_PHP_PATH_SHA256:
        errors.append("allowlist_sha256_mismatch")
    extra = [p for p in tracked if p not in set(allow)]
    if extra:
        errors.append("new_php_forbidden:\n  " + "\n  ".join(extra[:40]))
    aspnet_count = sum(1 for p in allow if p.startswith("aspnet/"))
    if aspnet_count != FROZEN_ASPNET_PHP_COUNT:
        errors.append(f"aspnet_php_count={aspnet_count} expected={FROZEN_ASPNET_PHP_COUNT}")

    leaks = scan_razor(root)
    if leaks:
        errors.append("ui_stack_words:\n  " + "\n  ".join(leaks[:80]))

    if errors:
        print("RESULT=FAIL php_stack_leak_or_new_php", file=sys.stderr)
        print("\n".join(errors), file=sys.stderr)
        print("New product work is ASP.NET Core only. Do not add PHP files or PHP/classic UI words.", file=sys.stderr)
        return 2

    print(f"RESULT=PASS php_count={len(tracked)} ui_stack_words=0")
    return 0


if __name__ == "__main__":
    sys.exit(main())
