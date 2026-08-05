#!/usr/bin/env python3
"""Insert/replace exact-route location blocks inside matching nginx server{} blocks.

CloudPanel's www.ecomae.com.conf is a mega-file with many server_name blocks
(www.ecomae.com, www.epartscart.com, other tenants). Classic-entry must edit
only the target host's server{} — never the first block in the file.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

SERVER_START_RE = re.compile(r"(?m)^[ \t]*server\s*\{")
SERVER_NAME_RE = re.compile(r"(?im)^\s*server_name\s+([^;]+);")


def find_server_blocks(text: str) -> list[tuple[int, int, str]]:
    """Return list of (start, end, body) for top-level-ish server{ } spans."""
    blocks: list[tuple[int, int, str]] = []
    for m in SERVER_START_RE.finditer(text):
        start = m.start()
        i = m.end() - 1  # at '{'
        depth = 0
        j = i
        while j < len(text):
            ch = text[j]
            if ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth == 0:
                    end = j + 1
                    blocks.append((start, end, text[start:end]))
                    break
            j += 1
    return blocks


def server_names(body: str) -> list[str]:
    names: list[str] = []
    for m in SERVER_NAME_RE.finditer(body):
        for tok in m.group(1).split():
            t = tok.strip().lower()
            if t and t != "_":
                names.append(t)
    return names


def is_redirect_only(body: str) -> bool:
    """True if server block is essentially apex→www redirect (no app root)."""
    # Strip comments
    lines = []
    for line in body.splitlines():
        s = line.strip()
        if not s or s.startswith("#"):
            continue
        lines.append(s)
    joined = "\n".join(lines)
    if re.search(r"(?im)^\s*root\s+", joined):
        return False
    if re.search(r"(?im)^\s*location\s+", joined):
        # locations other than maybe ACME — treat as real vhost
        return False
    if re.search(r"(?im)^\s*return\s+30[12]\s+", joined):
        return True
    return False


def host_matches(names: list[str], host: str) -> bool:
    host = host.lower().strip()
    variants = {host}
    if host.startswith("www."):
        variants.add(host[4:])
    else:
        variants.add("www." + host)
    for n in names:
        if n in variants:
            return True
    return False


def indent_block(block_raw: str, indent: str = "  ") -> str:
    return "\n".join((indent + line if line.strip() else line) for line in block_raw.splitlines()).rstrip() + "\n"


def find_insert_marker(cfg: str) -> int:
    for pat in (
        r"\n[ \t]*location / \{",
        r"\n[ \t]*location /php",
        r"\n[ \t]*location ~",
        r"\n[ \t]*include[ \t]+fastcgi",
        r"\n[ \t]*include[ \t]+",
    ):
        m = re.search(pat, cfg)
        if m:
            return m.start() + 1
    # Before closing brace of server block
    m = re.search(r"\n[ \t]*\}\s*$", cfg)
    if m:
        return m.start() + 1
    raise SystemExit("ERROR: insertion point missing inside target server block")


def parse_example(example: str) -> tuple[list[tuple[str, str]], list[tuple[str, str]]]:
    named_blocks: list[tuple[str, str]] = []
    for m in re.finditer(r"(?m)^(location @([A-Za-z0-9_]+)\s*\{.*?\n\})", example, flags=re.S):
        named_blocks.append((m.group(2), indent_block(m.group(1))))

    blocks: list[tuple[str, str]] = []
    for m in re.finditer(r"(?m)^(location = (/[^\s{]*)\s*\{.*?\n\})", example, flags=re.S):
        block_raw, route = m.group(1), m.group(2)
        if route in {"/api", "/storefront"}:
            raise SystemExit(f"ERROR: refusing broad path {route}")
        # Accept proxy_pass http://127.0.0.1:5100; (URI preserved) or .../path
        is_proxy = bool(
            re.search(r"(?m)^\s*proxy_pass\s+http://127\.0\.0\.1:5100(?:/[^;]*)?\s*;", block_raw)
        )
        is_php_ref = route.startswith("/php-reference/") and (
            "rewrite ^" in block_raw or "return 302" in block_raw or "alias " in block_raw
        )
        # Tenant home may stay PHP same-to-same (full modex chrome) until Blazor dual-sample.
        is_php_same_home = route == "/" and bool(
            re.search(r"(?m)^\s*rewrite\s+\^\s+/index\.php(\s|last|;)", block_raw)
        )
        is_login_bridge = route.rstrip("/").endswith("/login") or route in {
            "/cp/login",
            "/cp/login/",
            "/erp/login",
            "/erp/login/",
            "/bos/login",
            "/bos/login/",
        }
        if not is_proxy and not is_php_ref and not is_php_same_home:
            raise SystemExit(
                f"ERROR: block must proxy_pass ASP.NET, php-reference, or PHP same-home ({route})"
            )
        if route in {
            "/",
            "/cp",
            "/cp/",
            "/CP",
            "/CP/",
            "/erp",
            "/erp/",
            "/ERP",
            "/ERP/",
            "/bos",
            "/bos/",
            "/BOS",
            "/BOS/",
        }:
            if re.search(r"(?m)^\s*return\s+302\s+", block_raw):
                raise SystemExit(
                    f"ERROR: tenant-shared URLs must stay unchanged — no return 302 in {route}"
                )
            if route == "/" and is_php_same_home:
                pass  # epartscart home: PHP presentation same-to-same
            elif not is_proxy:
                raise SystemExit(f"ERROR: shared entry {route} must proxy_pass ASP.NET")
        if is_login_bridge and not is_proxy:
            raise SystemExit(f"ERROR: login bridge {route} must proxy_pass ASP.NET")
        blocks.append((route, indent_block(block_raw)))

    # 18 shared entries (/ + cp/erp/bos ×4 + php-reference ×5) + 6 login bridges
    expected = 24
    if len(blocks) != expected:
        raise SystemExit(f"ERROR: expected {expected} classic-entry routes, found {len(blocks)}")
    return named_blocks, blocks


def apply_blocks_to_server_body(
    body: str, named_blocks: list[tuple[str, str]], blocks: list[tuple[str, str]]
) -> tuple[str, list[str], list[str]]:
    inserted: list[str] = []
    replaced: list[str] = []
    text = body
    for name, block in named_blocks:
        pattern = re.compile(rf"(?m)^[ \t]*location\s*@{re.escape(name)}\s*\{{.*?\n[ \t]*\}}\n?", re.S)
        if pattern.search(text):
            text = pattern.sub(block + "\n", text, count=1)
            replaced.append("@" + name)
        else:
            marker = find_insert_marker(text)
            text = text[:marker] + block + "\n" + text[marker:]
            inserted.append("@" + name)

    for route, block in blocks:
        pattern = re.compile(
            rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{.*?\n[ \t]*\}}\n?", re.S
        )
        if pattern.search(text):
            text = pattern.sub(block + "\n", text, count=1)
            replaced.append(route)
            continue
        marker = find_insert_marker(text)
        text = text[:marker] + block + "\n" + text[marker:]
        inserted.append(route)
    return text, inserted, replaced


def install_into_host_servers(conf_text: str, example: str, host: str) -> tuple[str, dict]:
    named_blocks, blocks = parse_example(example)
    server_blocks = find_server_blocks(conf_text)
    targets: list[tuple[int, int, str, list[str]]] = []
    for start, end, body in server_blocks:
        names = server_names(body)
        if not host_matches(names, host):
            continue
        if is_redirect_only(body):
            continue
        targets.append((start, end, body, names))

    if not targets:
        raise SystemExit(
            f"ERROR: no non-redirect server{{}} block with server_name matching {host!r}. "
            "Apex-only 301 blocks are skipped. Check www.ecomae.com.conf tenant server blocks "
            "or re-enable /etc/nginx/sites-disabled/www.epartscart.com.conf carefully."
        )

    # Rebuild from the end so offsets stay valid
    out = conf_text
    summary = {
        "host": host,
        "serverBlocksEdited": len(targets),
        "replaced": [],
        "inserted": [],
        "serverNames": [],
    }
    for start, end, body, names in sorted(targets, key=lambda t: t[0], reverse=True):
        new_body, inserted, replaced = apply_blocks_to_server_body(body, named_blocks, blocks)
        out = out[:start] + new_body + out[end:]
        summary["replaced"].extend(replaced)
        summary["inserted"].extend(inserted)
        summary["serverNames"].append(names)

    return out, summary


def strip_classic_entry_from_host_servers(conf_text: str, host: str | None = None) -> tuple[str, int]:
    """Remove classic-entry exact locations / named passthrough from matching servers.

    If host is None, strip from ALL server blocks (use for cleaning wildcard pollution).
    """
    routes = [
        "/",
        "/CP",
        "/CP/",
        "/cp",
        "/cp/",
        "/ERP",
        "/ERP/",
        "/erp",
        "/erp/",
        "/BOS",
        "/BOS/",
        "/bos",
        "/bos/",
        "/cp/login",
        "/cp/login/",
        "/erp/login",
        "/erp/login/",
        "/bos/login",
        "/bos/login/",
        "/php-reference/home",
        "/php-reference/cp",
        "/php-reference/erp",
        "/php-reference/bos",
        "/php-reference/storefront",
    ]
    named = ["epc_classic_php_passthrough"]
    server_blocks = find_server_blocks(conf_text)
    out = conf_text
    removed = 0
    for start, end, body in sorted(server_blocks, key=lambda t: t[0], reverse=True):
        names = server_names(body)
        if host is not None and not host_matches(names, host):
            continue
        text = body
        for name in named:
            pattern = re.compile(
                rf"(?m)^[ \t]*location\s*@{re.escape(name)}\s*\{{.*?\n[ \t]*\}}\n?", re.S
            )
            text, n = pattern.subn("", text)
            removed += n
        for route in routes:
            pattern = re.compile(
                rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{.*?\n[ \t]*\}}\n?", re.S
            )
            # Only strip if it looks like classic-entry (proxy :5100 or php-reference / host-gate)
            def _sub(m: re.Match[str]) -> str:
                nonlocal removed
                blk = m.group(0)
                if (
                    "127.0.0.1:5100" in blk
                    or "php-reference" in blk
                    or "epc_classic_php_passthrough" in blk
                    or "X-EcomAE-Route-Cutover" in blk
                    or "rewrite ^ /index.php" in blk
                ):
                    removed += 1
                    return ""
                return blk

            text = pattern.sub(_sub, text)
        out = out[:start] + text + out[end:]
    return out, removed


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest="cmd", required=True)

    inst = sub.add_parser("install", help="Install classic-entry example into host server blocks")
    inst.add_argument("conf")
    inst.add_argument("example")
    inst.add_argument("host", help="e.g. www.ecomae.com or www.epartscart.com")

    strip = sub.add_parser("strip", help="Strip classic-entry locations from host server blocks")
    strip.add_argument("conf")
    strip.add_argument("--host", default=None)
    strip.add_argument("--all-servers", action="store_true")

    args = p.parse_args(argv)
    path = Path(args.conf)
    text = path.read_text(encoding="utf-8")

    if args.cmd == "install":
        example = Path(args.example).read_text(encoding="utf-8")
        out, summary = install_into_host_servers(text, example, args.host)
        path.write_text(out, encoding="utf-8")
        print(f"HOST={summary['host']}")
        print(f"SERVER_BLOCKS_EDITED={summary['serverBlocksEdited']}")
        for names in summary["serverNames"]:
            print(f"  server_name={names[:12]}")
        print(f"REPLACED: {len(summary['replaced'])}")
        for r in summary["replaced"]:
            print("  ~", r)
        print(f"INSERTED: {len(summary['inserted'])}")
        for r in summary["inserted"]:
            print("  +", r)
        return 0

    if args.cmd == "strip":
        host = None if args.all_servers else args.host
        if host is None and not args.all_servers:
            raise SystemExit("ERROR: strip requires --host or --all-servers")
        out, removed = strip_classic_entry_from_host_servers(text, host)
        path.write_text(out, encoding="utf-8")
        print(f"STRIPPED={removed}")
        return 0

    return 2


if __name__ == "__main__":
    raise SystemExit(main())
