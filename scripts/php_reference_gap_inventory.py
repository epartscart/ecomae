#!/usr/bin/env python3
"""PHP reference gap inventory.

Lists every PHP file and every top-level PHP function in the PHP reference and checks whether the
ASP.NET tree (aspnet/src) mentions it by relative path, basename, route stem or function name.
A mention is only a lead (it can be a "remains PHP" note); an unmentioned file or function is a
surface nobody has ported or even mapped yet, which is what this inventory is for.

Usage: python3 scripts/php_reference_gap_inventory.py [--json out.json] [--md out.md]
"""
from __future__ import annotations

import argparse
import json
import os
import re
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASPNET_SRC = os.path.join(ROOT, "aspnet", "src")
SKIP_DIRS = {"aspnet", "vendor", "node_modules", ".git", "tests", "artifacts", "docs", "sitemap_cache", "license", "icons"}
# Migration catalogues, dry-run rehearsals and reporters list PHP paths without porting them.
CATALOG_TAGS = ("Catalog", "Inventory", "Matrix", "DryRun", "Reporter", "Dashboard", "Readiness", "Contract", "Probe", "Evidence")
FUNC_RE = re.compile(r"^\s*function\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(", re.M)
SITEMAP_RE = re.compile(r"^sitemap-[a-z]+-\d+\.php$")
THIRD_PARTY = ("lib/PHPExcel/", "lib/PHPMailer/", "lib/TreelaxCharts/", "lib/captcha/", "cp/lib/elfinder/",
               "content/laximo/com_guayaquil/", "lib/PclZip/", "lib/MobileDetect/", "lib/inputmask/", "cp/lib/tinymce/")
# Root epc-*.php / ecomae-*.php are one-off deploy, seed, repair and audit scripts run by hand on CloudPanel.
OPS_SCRIPT_RE = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)+\.php$|^config\..+\.example\.php$|^(extract|fix|set|python|err|chunk)[_-]")
OPS_DIRS = ("scripts/php/", "pyprices/", "modules/debug/")
TAB_RE = re.compile(r"(?:^|/)erp_tabs_([a-z0-9_]+)\.php$")
RETIRED_FILE = os.path.join(ROOT, "docs", "migration", "inventory", "PHP_RETIRED.tsv")


def retired_files() -> dict[str, str]:
    """`path<TAB>reason` lines; a gap may only leave the list by being ported or retired here with a reason."""
    out = {}
    if os.path.exists(RETIRED_FILE):
        with open(RETIRED_FILE, encoding="utf-8") as fh:
            for line in fh:
                if line.strip() and not line.startswith("#") and "\t" in line:
                    path, reason = line.rstrip("\n").split("\t", 1)
                    if reason.strip():
                        out[path.strip()] = reason.strip()
    return out


def triage(rel: str, mentioned: bool, tab_keys: set[str], retired: dict[str, str]) -> str:
    """mentioned | mapped-tab | third-party | ops-script | retired | gap"""
    if mentioned:
        return "mentioned"
    if rel in retired:
        return "retired"
    if rel.startswith(THIRD_PARTY):
        return "third-party"
    if rel.startswith(OPS_DIRS) or ("/" not in rel and OPS_SCRIPT_RE.match(rel)):
        return "ops-script"
    m = TAB_RE.search(rel)
    if m and m.group(1) in tab_keys:
        return "mapped-tab"
    return "gap"


def area(rel: str) -> str:
    parts = rel.split("/")
    if len(parts) == 1:
        return "(root)"
    if parts[0] in {"content", "cp"} and len(parts) > 3 and parts[1] in {"shop", "content", "general_pages"}:
        return "/".join(parts[:3]) if parts[1] != "content" or len(parts) <= 4 else "/".join(parts[:4])
    return "/".join(parts[:-1])


def php_files() -> list[str]:
    out = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        rel_dir = os.path.relpath(dirpath, ROOT)
        top = rel_dir.split(os.sep)[0]
        if top in SKIP_DIRS:
            dirnames[:] = []
            continue
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            if name.endswith(".php"):
                rel = os.path.normpath(os.path.join(rel_dir, name)).replace(os.sep, "/")
                out.append(rel)
    return sorted(out)


def aspnet_corpus() -> str:
    chunks = []
    for dirpath, dirnames, filenames in os.walk(ASPNET_SRC):
        dirnames[:] = [d for d in dirnames if d not in {"bin", "obj", "node_modules", "App_Data", "content", "wwwroot", "Migration"}]
        for name in filenames:
            if any(tag in name for tag in CATALOG_TAGS):
                continue
            if name.endswith((".cs", ".razor", ".cshtml")):
                try:
                    with open(os.path.join(dirpath, name), encoding="utf-8", errors="ignore") as fh:
                        chunks.append(fh.read())
                except OSError:
                    pass
    return "\n".join(chunks)


def category(rel: str) -> str:
    name = os.path.basename(rel)
    parts = rel.split("/")
    if SITEMAP_RE.match(name):
        return "sitemap-shard"
    if parts[0] == "lib":
        return "library"
    if "cron" in rel.lower():
        return "cron"
    if parts[0] == "api" or "/api/" in rel:
        return "api"
    if "ajax" in name.lower() or "/ajax/" in rel:
        return "cp-ajax" if parts[0] == "cp" else "ajax"
    if parts[0] == "cp":
        return "cp-page"
    if parts[0] in {"modules", "plugins", "templates"}:
        return parts[0]
    if parts[0] == "content":
        return "content"
    return "root"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--json")
    parser.add_argument("--md")
    parser.add_argument("--max-gap", type=int, help="exit 1 when more gap files remain (ratchet; lower it as gaps close)")
    args = parser.parse_args()

    corpus = aspnet_corpus()
    idents = set(re.findall(r"[A-Za-z_][A-Za-z0-9_]*", corpus))
    php_refs = set(re.findall(r"[A-Za-z0-9_./-]+\.php", corpus))
    php_ref_names = {os.path.basename(x) for x in php_refs}
    php_ref_stems = set(re.findall(r"/[A-Za-z0-9_./-]+", corpus))
    tab_keys = set(re.findall(r'\["([a-z0-9_]+)"\]\s*=', corpus))
    retired = retired_files()
    files = php_files()
    basenames = Counter(os.path.basename(f) for f in files)
    rows = []
    func_rows = []
    for rel in files:
        name = os.path.basename(rel)
        stem = rel[:-4]
        by_path = any(r == rel or r.endswith("/" + rel) or rel.endswith(r.lstrip("./")) and "/" in r for r in php_refs if r.endswith(name)) \
            or ("/" + stem) in php_ref_stems
        by_name = basenames[name] == 1 and name in php_ref_names
        try:
            with open(os.path.join(ROOT, rel), encoding="utf-8", errors="ignore") as fh:
                text = fh.read()
        except OSError:
            text = ""
        funcs = sorted(set(FUNC_RE.findall(text)))
        mentioned_funcs = [f for f in funcs if f in idents]
        for f in funcs:
            func_rows.append({"file": rel, "function": f, "mentioned": f in mentioned_funcs})
        mentioned = bool(by_path or by_name or (funcs and len(mentioned_funcs) == len(funcs)))
        rows.append({
            "file": rel,
            "category": category(rel),
            "area": area(rel),
            "triage": "sitemap-shard" if SITEMAP_RE.match(name) else triage(rel, mentioned, tab_keys, retired),
            "lines": text.count("\n") + 1,
            "mentioned": mentioned,
            "by_path": by_path,
            "functions": len(funcs),
            "functions_mentioned": len(mentioned_funcs),
        })

    by_cat = defaultdict(lambda: [0, 0])
    for r in rows:
        by_cat[r["category"]][0] += 1
        by_cat[r["category"]][1] += 1 if r["mentioned"] else 0
    gaps = [r for r in rows if r["triage"] == "gap"]
    funcs_missing = [f for f in func_rows if not f["mentioned"]]
    by_triage = Counter(r["triage"] for r in rows)

    summary = {
        "php_files": len(rows),
        "by_category": {k: {"files": v[0], "mentioned": v[1]} for k, v in sorted(by_cat.items())},
        "by_triage": dict(sorted(by_triage.items())),
        "gap_files": len(gaps),
        "gap_lines": sum(r["lines"] for r in gaps),
        "functions": len(func_rows),
        "functions_unmentioned": len(funcs_missing),
    }
    print(json.dumps(summary, indent=2))

    if args.json:
        with open(args.json, "w", encoding="utf-8") as fh:
            json.dump({"summary": summary, "files": rows, "functions": func_rows}, fh, indent=1)
    if args.md:
        with open(args.md, "w", encoding="utf-8") as fh:
            fh.write("# PHP reference gap inventory\n\n")
            fh.write("Generated by `scripts/php_reference_gap_inventory.py`. A file counts as mentioned when "
                     "`aspnet/src` names its path, its unique basename, or every function it defines. "
                     "A mention is a lead, not parity.\n\n")
            fh.write("| Category | PHP files | Mentioned in ASP.NET |\n|---|---:|---:|\n")
            for k, v in sorted(by_cat.items()):
                fh.write(f"| {k} | {v[0]} | {v[1]} |\n")
            fh.write(f"\nFunctions defined: {len(func_rows)}; not mentioned anywhere in ASP.NET: {len(funcs_missing)}.\n")
            fh.write("\n## Triage\n\n"
                     "- `mentioned`: ASP.NET names the file or all its functions (a lead; parity is tracked in the tracker).\n"
                     "- `mapped-tab`: an `erp_tabs_*.php` whose tab key is routed by `ErpPhpTabRouteMap` (writes may still be PHP).\n"
                     "- `third-party`: vendored libraries (PHPExcel, PHPMailer, elFinder, Laximo SDK, ...); replaced by NuGet/.NET, not ported.\n"
                     "- `ops-script`: one-off root `epc-*.php` deploy/seed/repair/audit scripts; replaced by migrations and workers, not ported 1:1.\n"
                     "- `sitemap-shard`: generated sitemap shards.\n"
                     "- `retired`: listed with a reason in `docs/migration/inventory/PHP_RETIRED.tsv`.\n"
                     "- `gap`: nothing in ASP.NET references it — must be ported, or retired with a reason in `PHP_RETIRED.tsv`.\n\n"
                     "| Triage | Files |\n|---|---:|\n")
            for k, v in sorted(by_triage.items()):
                fh.write(f"| {k} | {v} |\n")
            fh.write(f"\nGap files: {len(gaps)} ({sum(r['lines'] for r in gaps)} lines).\n")
            grouped = defaultdict(list)
            for r in gaps:
                grouped[r["area"]].append(r)
            order = sorted(grouped, key=lambda a: -sum(r["lines"] for r in grouped[a]))
            fh.write("\n## Gap areas (largest first)\n\n| Area | Files | Lines |\n|---|---:|---:|\n")
            for a in order:
                fh.write(f"| `{a}` | {len(grouped[a])} | {sum(r['lines'] for r in grouped[a])} |\n")
            fh.write("\n## Gap files by area\n")
            for a in order:
                fh.write(f"\n### {a} ({len(grouped[a])})\n\n")
                for r in sorted(grouped[a], key=lambda x: -x["lines"]):
                    fh.write(f"- `{r['file']}` ({r['lines']} lines, {r['functions_mentioned']}/{r['functions']} functions mentioned)\n")
    if args.max_gap is not None and len(gaps) > args.max_gap:
        raise SystemExit(f"{len(gaps)} PHP gap files exceed --max-gap {args.max_gap}")


if __name__ == "__main__":
    main()
