#!/usr/bin/env python3
"""Classify unmentioned PHP functions and emit a build catalog.

"Unnamed" in the inventory means the function exists in PHP but nothing in
aspnet/src mentions its identifier. Every row here already has a PHP name;
csharp_name is the PascalCase twin to implement.
"""
from __future__ import annotations

import argparse
import json
import os
import re
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FUNC_RE = re.compile(r"^\s*function\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(", re.M)
SCRIPT_OPEN = re.compile(r"<script\b", re.I)
SCRIPT_CLOSE = re.compile(r"</script>", re.I)
PHP_OPEN = re.compile(r"<\?php|<\?=")
PHP_CLOSE = re.compile(r"\?>")
VENDOR_PREFIX = (
    "lib/PHPExcel/", "lib/PHPMailer/", "lib/PclZip/", "lib/TreelaxCharts/",
    "lib/captcha/", "cp/lib/elfinder/", "content/laximo/", "lib/MobileDetect/",
    "lib/inputmask/", "cp/lib/tinymce/",
)


def is_erp(path: str) -> bool:
    return "/finance/" in path or path.startswith("content/shop/finance")


def to_csharp(name: str) -> str:
    parts = [p for p in re.split(r"[^A-Za-z0-9]+", name) if p]
    return "".join(p[:1].upper() + p[1:] if p[:1].islower() or p.isupper() else p for p in parts)


def classify_file(path: str, names: set[str]) -> dict[str, str]:
    try:
        text = open(os.path.join(ROOT, path), encoding="utf-8", errors="ignore").read()
    except OSError:
        return {n: "missing" for n in names}
    in_php = text.lstrip().startswith("<?")
    in_script = False
    out: dict[str, str] = {}
    for line in text.splitlines():
        m = FUNC_RE.match(line)
        if m and m.group(1) in names and m.group(1) not in out:
            out[m.group(1)] = "js" if (in_script and not in_php) or not in_php else "php"
        if PHP_OPEN.search(line):
            in_php = True
        if PHP_CLOSE.search(line):
            after = line.split("?>")[-1]
            in_php = "<?php" in after or "<?=" in after
        if SCRIPT_OPEN.search(line) and not in_php:
            in_script = True
        if SCRIPT_CLOSE.search(line) and not in_php:
            in_script = False
    for n in names:
        out.setdefault(n, "unknown")
    return out


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory-json", default="/tmp/gap_inv.json")
    parser.add_argument("--tsv", default=os.path.join(ROOT, "docs/migration/inventory/PHP_BUILDABLE_FUNCTIONS.tsv"))
    parser.add_argument("--md", default=os.path.join(ROOT, "docs/migration/inventory/PHP_UNMENTIONED_FUNCTIONS.md"))
    args = parser.parse_args()
    data = json.load(open(args.inventory_json, encoding="utf-8"))
    file_rows = {r["file"]: r for r in data["files"]}
    un = [f for f in data["functions"] if not f["mentioned"]]
    by_file: dict[str, list[str]] = defaultdict(list)
    for f in un:
        by_file[f["file"]].append(f["function"])

    rows = []
    counts = defaultdict(int)
    for path, names in sorted(by_file.items()):
        kinds = classify_file(path, set(names))
        vendor = any(path.startswith(v) for v in VENDOR_PREFIX)
        meta = file_rows.get(path, {})
        for name in sorted(names):
            kind = "vendor" if vendor else ("erp_" + kinds[name] if is_erp(path) else kinds[name])
            counts[kind] += 1
            build = "skip"
            if kind == "php" and meta.get("triage") == "gap":
                build = "ready" if meta.get("lines", 10**9) <= 200 else "later"
            elif kind == "php" and meta.get("mentioned"):
                build = "partial-file"
            elif kind.startswith("erp_"):
                build = "devin-erp"
            elif kind == "js":
                build = "browser-js"
            elif kind == "vendor":
                build = "third-party"
            rows.append({
                "php_name": name,
                "csharp_name": to_csharp(name),
                "file": path,
                "kind": kind,
                "lines": meta.get("lines", 0),
                "file_triage": meta.get("triage", ""),
                "build": build,
            })

    os.makedirs(os.path.dirname(args.tsv), exist_ok=True)
    with open(args.tsv, "w", encoding="utf-8") as fh:
        fh.write("php_name\tcsharp_name\tfile\tkind\tlines\tfile_triage\tbuild\n")
        for r in rows:
            fh.write("{php_name}\t{csharp_name}\t{file}\t{kind}\t{lines}\t{file_triage}\t{build}\n".format(**r))

    php_gap = [r for r in rows if r["build"] == "ready"]
    php_later = [r for r in rows if r["build"] == "later"]
    with open(args.md, "w", encoding="utf-8") as fh:
        fh.write("# Unmentioned PHP functions — named for build\n\n")
        fh.write("The inventory count **functions_unmentioned** is not a set of anonymous closures. ")
        fh.write("Each function already has a PHP name. It is counted here when `aspnet/src` does not ")
        fh.write("contain that identifier. `csharp_name` is the PascalCase twin to implement.\n\n")
        fh.write("| Class | Count | What to do |\n|---|---:|---|\n")
        fh.write(f"| Real PHP (non-ERP), gap file ≤200 lines | {len(php_gap)} | Build next — listed below |\n")
        fh.write(f"| Real PHP (non-ERP), larger gap file | {len(php_later)} | Build with the parent kernel |\n")
        fh.write(f"| Real PHP already on a mentioned file | {sum(1 for r in rows if r['build']=='partial-file')} | Finish leftover helpers on that twin |\n")
        fh.write(f"| JS functions written inside PHP templates | {counts.get('js', 0)} | Port as browser JS, not C# methods |\n")
        fh.write(f"| Vendor (PHPExcel, PclZip, …) | {counts.get('vendor', 0)} | Do not port |\n")
        fh.write(f"| ERP finance (Devin) | {sum(v for k,v in counts.items() if k.startswith('erp_'))} | Leave for Devin |\n")
        fh.write(f"| **Total unmentioned** | **{len(rows)}** | |\n\n")
        fh.write("## Ready to build (non-ERP PHP, gap file ≤200 lines)\n\n")
        fh.write("| PHP name | C# twin | File | Lines |\n|---|---|---|---:|\n")
        for r in php_gap:
            fh.write(f"| `{r['php_name']}` | `{r['csharp_name']}` | `{r['file']}` | {r['lines']} |\n")
        fh.write("\nFull table: `docs/migration/inventory/PHP_BUILDABLE_FUNCTIONS.tsv`.\n")
    print(json.dumps({"rows": len(rows), "ready": len(php_gap), "later": len(php_later), "counts": dict(counts)}, indent=2))


if __name__ == "__main__":
    main()
