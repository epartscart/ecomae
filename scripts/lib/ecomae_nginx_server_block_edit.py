#!/usr/bin/env python3
"""Insert/replace classic-entry location blocks inside matching nginx server{} blocks.

CloudPanel's www.ecomae.com.conf is a mega-file with many server_name blocks
(www.ecomae.com, www.epartscart.com, other tenants). Classic-entry must edit
only the target host's server{} — never the first block in the file.

IMPORTANT: installs both exact ``location = /path`` AND prefix
``location ^~ /storefront/`` (etc.). Older versions only installed ``=`` routes,
which left /storefront/search-app on PHP → warm-up splash bounce loop.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

SERVER_START_RE = re.compile(r"(?m)^[ \t]*server\s*\{")
SERVER_NAME_RE = re.compile(r"(?im)^\s*server_name\s+([^;]+);")

# (kind, matcher) kind in {"exact", "prefix", "regex", "named"}
LocationKey = tuple[str, str]


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
        return False
    if re.search(r"(?im)^\s*return\s+30[12]\s+", joined):
        return True
    return False


def host_matches(names: list[str], host: str) -> bool:
    host = host.lower().strip()
    # Explicit wildcard target (industry pack on server_name *.ecomae.com).
    if host in {"*.ecomae.com", "wildcard-ecomae"}:
        return any(n == "*.ecomae.com" for n in names)
    variants = {host}
    if host.startswith("www."):
        variants.add(host[4:])
    else:
        variants.add("www." + host)
    for n in names:
        if n in variants:
            return True
        # *.ecomae.com matches agriculture.ecomae.com (not www/cp — those have dedicated blocks).
        if n.startswith("*.") and "." in host:
            suffix = n[1:]  # .ecomae.com
            if host.endswith(suffix) and host.count(".") >= 2:
                left = host[: -len(suffix)]
                if left and left not in {"www", "cp", "lifeos"} and "." not in left:
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
    m = re.search(r"\n[ \t]*\}\s*$", cfg)
    if m:
        return m.start() + 1
    raise SystemExit("ERROR: insertion point missing inside target server block")


def _is_proxy_5100(block_raw: str) -> bool:
    return bool(re.search(r"(?m)^\s*proxy_pass\s+http://127\.0\.0\.1:5100(?:/[^;]*)?\s*;", block_raw))


def _is_php_rewrite(block_raw: str) -> bool:
    return "rewrite ^" in block_raw or "rewrite ^ /index.php" in block_raw or "rewrite ^ /sitemap" in block_raw


def _validate_exact_block(route: str, block_raw: str) -> None:
    # Super-CP-only product shells: tenants must return 404 (never proxy BOS/IP/LifeOS).
    super_cp_only_deny = {
        "/bos",
        "/bos/",
        "/BOS",
        "/BOS/",
        "/bos/login",
        "/bos/login/",
        "/BOS/login",
        "/BOS/login/",
        "/php-reference/bos",
        "/ip",
        "/ip/",
        "/IP",
        "/IP/",
        "/ip/login",
        "/ip/login/",
        "/IP/login",
        "/IP/login/",
        "/lifeos",
        "/lifeos/",
        "/lifeos/login",
        "/lifeos/login/",
    }
    is_proxy = _is_proxy_5100(block_raw)
    is_php_ref = route.startswith("/php-reference/") and (
        _is_php_rewrite(block_raw) or "return 302" in block_raw or "alias " in block_raw
    )
    is_super_cp_deny = route in super_cp_only_deny and bool(
        re.search(r"(?m)^\s*return\s+404\s*;", block_raw)
    )
    is_sitemap = route == "/sitemap.xml" and _is_php_rewrite(block_raw)
    if not is_proxy and not is_php_ref and not is_super_cp_deny and not is_sitemap:
        raise SystemExit(
            f"ERROR: block must proxy_pass ASP.NET, php-reference, sitemap rewrite, "
            f"or super-cp-deny 404 ({route})"
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
    }:
        if re.search(r"(?m)^\s*return\s+302\s+", block_raw):
            raise SystemExit(
                f"ERROR: tenant-shared URLs must stay unchanged — no return 302 in {route}"
            )
        if not is_proxy:
            raise SystemExit(f"ERROR: shared entry {route} must proxy_pass ASP.NET")


def parse_example(
    example: str,
) -> tuple[list[tuple[str, str]], list[tuple[LocationKey, str]]]:
    """Return (named_blocks, location_blocks).

    location_blocks keys are (kind, matcher) where kind is exact|prefix|regex.
    """
    named_blocks: list[tuple[str, str]] = []
    for m in re.finditer(r"(?m)^(location @([A-Za-z0-9_]+)\s*\{.*?\n\})", example, flags=re.S):
        named_blocks.append((m.group(2), indent_block(m.group(1))))

    blocks: list[tuple[LocationKey, str]] = []

    # Exact locations
    for m in re.finditer(r"(?m)^(location = (/[^\s{]*)\s*\{.*?\n\})", example, flags=re.S):
        block_raw, route = m.group(1), m.group(2)
        if route in {"/api", "/storefront"}:
            raise SystemExit(f"ERROR: refusing broad path {route}")
        _validate_exact_block(route, block_raw)
        blocks.append((("exact", route), indent_block(block_raw)))

    # Prefix ^~ locations (storefront / framework / cp tree / …)
    for m in re.finditer(r"(?m)^(location \^~ (/[^\s{]*)\s*\{.*?\n\})", example, flags=re.S):
        block_raw, route = m.group(1), m.group(2)
        # Tenant Super-CP-only trees return 404; everything else must hit Kestrel
        is_proxy = _is_proxy_5100(block_raw)
        route_l = route.lower()
        is_super_cp_prefix_deny = (
            route_l.rstrip("/") in {"/bos", "/ip", "/lifeos"}
            or route_l.startswith("/bos/")
            or route_l.startswith("/ip/")
            or route_l.startswith("/lifeos/")
        ) and bool(re.search(r"(?m)^\s*return\s+404\s*;", block_raw))
        if not is_proxy and not is_super_cp_prefix_deny:
            raise SystemExit(f"ERROR: prefix location {route} must proxy_pass :5100 or return 404")
        # Never allow stub→/en redirects to sneak back in as exact overrides — those are gone.
        if "return 302 /en/" in block_raw:
            raise SystemExit(f"ERROR: refusing stub→/en redirect inside prefix {route}")
        blocks.append((("prefix", route), indent_block(block_raw)))

    # Regex locations (www BOS deep trees)
    for m in re.finditer(r"(?m)^(location ~ (\^[^\s{]*)\s*\{.*?\n\})", example, flags=re.S):
        block_raw, route = m.group(1), m.group(2)
        if not _is_proxy_5100(block_raw):
            raise SystemExit(f"ERROR: regex location {route} must proxy_pass :5100")
        blocks.append((("regex", route), indent_block(block_raw)))

    exact_n = sum(1 for (k, _), _ in blocks if k == "exact")
    prefix_n = sum(1 for (k, _), _ in blocks if k == "prefix")
    if exact_n < 20:
        raise SystemExit(f"ERROR: expected at least 20 exact classic-entry routes, found {exact_n}")
    if prefix_n < 1 or not any(m == "/storefront/" for (k, m), _ in blocks if k == "prefix"):
        raise SystemExit(
            "ERROR: example must include location ^~ /storefront/ → :5100 "
            "(without this, /storefront/search-app falls to PHP → warm-up splash)"
        )
    return named_blocks, blocks


def _location_header_pattern(kind: str, matcher: str) -> re.Pattern[str]:
    if kind == "exact":
        return re.compile(rf"(?m)^[ \t]*location\s*=\s*{re.escape(matcher)}\s*\{{")
    if kind == "prefix":
        # nginx treats `location /cp/` and `location ^~ /cp/` as the SAME location
        # (duplicate location "/cp/" emerg) — replacing a prefix must match both forms.
        return re.compile(rf"(?m)^[ \t]*location\s+(?:\^~\s+)?{re.escape(matcher)}\s*\{{")
    if kind == "regex":
        return re.compile(rf"(?m)^[ \t]*location\s*~\s*{re.escape(matcher)}\s*\{{")
    if kind == "named":
        return re.compile(rf"(?m)^[ \t]*location\s*@{re.escape(matcher)}\s*\{{")
    raise ValueError(kind)


def _find_location_span(text: str, kind: str, matcher: str, start_at: int = 0) -> tuple[int, int] | None:
    """Brace-aware span of a location block (regex ``.*?\\n}`` truncated blocks that
    contain nested ``if (...) { ... }`` braces, leaving orphan braces → nginx -t fail)."""
    m = _location_header_pattern(kind, matcher).search(text, start_at)
    if not m:
        return None
    i = text.find("{", m.start())
    depth = 0
    j = i
    while j < len(text):
        c = text[j]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                k = j + 1
                if k < len(text) and text[k] == "\n":
                    k += 1
                return (m.start(), k)
        j += 1
    return None


def _replace_or_insert_location(
    text: str, kind: str, matcher: str, block: str
) -> tuple[str, bool]:
    """Replace the first existing block (brace-aware), DELETE later duplicates of the
    same selector (self-heal 'duplicate location' breakage), or insert when absent.
    Returns (new_text, replaced)."""
    span = _find_location_span(text, kind, matcher)
    if span is None:
        marker = find_insert_marker(text)
        return text[:marker] + block + "\n" + text[marker:], False

    start, end = span
    text = text[:start] + block + "\n" + text[end:]
    # Drop any additional occurrences of the same selector in this server body.
    search_from = start + len(block) + 1
    while True:
        dup = _find_location_span(text, kind, matcher, search_from)
        if dup is None:
            break
        text = text[: dup[0]] + text[dup[1] :]
    return text, True


def apply_blocks_to_server_body(
    body: str,
    named_blocks: list[tuple[str, str]],
    blocks: list[tuple[LocationKey, str]],
) -> tuple[str, list[str], list[str]]:
    inserted: list[str] = []
    replaced: list[str] = []
    text = body
    for name, block in named_blocks:
        text, was_replaced = _replace_or_insert_location(text, "named", name, block)
        (replaced if was_replaced else inserted).append("@" + name)

    # Strip dangerous stub→/en exact locations before installing prefix /storefront/
    for stub in (
        "/storefront/search-app",
        "/storefront/cart-app",
        "/storefront/checkout-app",
        "/storefront/orders-app",
        "/storefront/login",
        "/storefront/garage-app",
    ):
        stub_pat = re.compile(
            rf"(?m)^[ \t]*location\s*=\s*{re.escape(stub)}\s*\{{.*?\n[ \t]*\}}\n?", re.S
        )
        text, n = stub_pat.subn(
            "  # removed stub→/en (classic-entry install — use ^~ /storefront/ → :5100)\n", text
        )
        if n:
            replaced.append(f"stripped-stub:{stub}")

    # Install exact first, then prefix (prefix must win for /storefront/search-app)
    for kind in ("exact", "prefix", "regex"):
        for (k, matcher), block in blocks:
            if k != kind:
                continue
            label = f"{k}:{matcher}"
            text, was_replaced = _replace_or_insert_location(text, k, matcher, block)
            (replaced if was_replaced else inserted).append(label)
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

    out = conf_text
    summary = {
        "host": host,
        "serverBlocksEdited": len(targets),
        "replaced": [],
        "inserted": [],
        "serverNames": [],
        "prefixStorefront": any(k == "prefix" and m == "/storefront/" for (k, m), _ in blocks),
        "prefixEnParts": any(k == "prefix" and m == "/en/parts/" for (k, m), _ in blocks),
    }
    for start, end, body, names in sorted(targets, key=lambda t: t[0], reverse=True):
        new_body, inserted, replaced = apply_blocks_to_server_body(body, named_blocks, blocks)
        out = out[:start] + new_body + out[end:]
        summary["replaced"].extend(replaced)
        summary["inserted"].extend(inserted)
        summary["serverNames"].append(names)

    return out, summary


def strip_classic_entry_from_host_servers(conf_text: str, host: str | None = None) -> tuple[str, int]:
    """Remove classic-entry exact + prefix locations from matching servers."""
    exact_routes = [
        "/",
        "/CP",
        "/CP/",
        "/cp",
        "/cp/",
        "/cp/control",
        "/cp/control/",
        "/CP/control",
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
        "/auth/login/admin",
        "/auth/login/admin/",
        "/auth/logout",
        "/auth/logout/",
        "/sitemap.xml",
        "/php-reference/home",
        "/php-reference/cp",
        "/php-reference/erp",
        "/php-reference/bos",
        "/php-reference/storefront",
    ]
    prefix_routes = [
        "/aspnet-php-assets/",
        "/_framework/",
        "/cp/",
        "/erp/",
        "/bos/",
        "/storefront/",
        "/en/parts/",
        "/parts/",
        "/marketing/",
        "/CP/",
        "/ERP/",
        "/BOS/",
        "/shop/",
        "/bos/app",
        "/bos/login",
        "/bos/logout",
        "/bos/ajax-writes",
    ]
    exact_routes_extra = [
        "/en/shop/part_search",
        "/en/shop/warehouse-search",
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

        def _maybe_strip(blk: str) -> str:
            nonlocal removed
            if (
                "127.0.0.1:5100" in blk
                or "php-reference" in blk
                or "epc_classic_php_passthrough" in blk
                or "X-EcomAE-Route-Cutover" in blk
                or "rewrite ^ /index.php" in blk
                or "rewrite ^ /sitemap" in blk
                or "return 404" in blk
            ):
                removed += 1
                return ""
            return blk

        for route in exact_routes + exact_routes_extra:
            pattern = re.compile(
                rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{.*?\n[ \t]*\}}\n?", re.S
            )
            text = pattern.sub(lambda m: _maybe_strip(m.group(0)), text)
        for route in prefix_routes:
            pattern = re.compile(
                rf"(?m)^[ \t]*location\s*\^~\s*{re.escape(route)}\s*\{{.*?\n[ \t]*\}}\n?", re.S
            )
            text = pattern.sub(lambda m: _maybe_strip(m.group(0)), text)
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
        print(f"PREFIX_STOREFRONT={summary['prefixStorefront']}")
        print(f"PREFIX_EN_PARTS={summary['prefixEnParts']}")
        for names in summary["serverNames"]:
            print(f"  server_name={names[:12]}")
        print(f"REPLACED: {len(summary['replaced'])}")
        for r in summary["replaced"]:
            print("  ~", r)
        print(f"INSERTED: {len(summary['inserted'])}")
        for r in summary["inserted"]:
            print("  +", r)
        if not summary["prefixStorefront"]:
            print("ERROR: storefront prefix missing from example", file=sys.stderr)
            return 2
        # Tenant + industry examples must own CHPU /en/parts/ (www Super-CP may omit).
        example_name = Path(args.example).name
        if "tenant" in example_name or "industry" in example_name:
            if not summary["prefixEnParts"]:
                print("ERROR: /en/parts/ prefix missing from tenant/industry classic-entry", file=sys.stderr)
                return 2
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
