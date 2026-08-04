#!/usr/bin/env python3
"""Extract complete PHP module directories → JSON + C# for hybrid ASP.NET chrome.

Sources:
  - cp/content/shop/finance/erp/erp_nav_areas.php
  - content/general_pages/epc_bos_unified.php
  - content/general_pages/epc_cp_brochure_inventory.php

Policy: every module gets a PHP deeplink until an ASP.NET interactive port exists.
Run: python3 scripts/generate_php_module_catalog.py
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path


def cs_escape(s: str) -> str:
    return s.replace("\\", "\\\\").replace('"', '\\"')


def slug(s: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")[:80] or "item"


def parse_erp_categories(text: str) -> list[dict]:
    """Top mega-menu categories from epc_erp_nav_categories_config()."""
    fn = re.search(
        r"function\s+epc_erp_nav_categories_config\s*\([^)]*\)\s*:\s*array\s*\{.*?return\s+array\s*\((.*?)\);\s*\}",
        text,
        re.S,
    )
    if not fn:
        return []
    blob = fn.group(1)
    cats = []
    for m in re.finditer(
        r"'([a-z0-9_]+)'\s*=>\s*array\s*\(\s*"
        r"'label'\s*=>\s*'((?:\\'|[^'])*)'\s*,\s*"
        r"'short'\s*=>\s*'((?:\\'|[^'])*)'\s*,\s*"
        r"'icon'\s*=>\s*'((?:\\'|[^'])*)'\s*,\s*"
        r"'areas'\s*=>\s*array\s*\((.*?)\)\s*,",
        blob,
        re.S,
    ):
        key, label, short, icon, areas_blob = m.groups()
        area_ids = re.findall(r"'([a-z0-9_]+)'", areas_blob)
        first = area_ids[0] if area_ids else "overview"
        cats.append(
            {
                "id": key,
                "label": label,
                "short": short,
                "icon": icon,
                "areas": area_ids,
                "href": f"/ERP/?epc_erp_shell=1&area={first}",
            }
        )
    return cats


def parse_erp_areas(text: str) -> list[dict]:
    # Match area blocks: 'key' => array( 'label' => ... 'icon' => ... 'tabs' => array( ... ),
    area_re = re.compile(
        r"'([a-z0-9_]+)'\s*=>\s*array\s*\(\s*"
        r"'label'\s*=>\s*'((?:\\'|[^'])*)'\s*,\s*"
        r"'icon'\s*=>\s*'((?:\\'|[^'])*)'\s*,\s*"
        r"'desc'\s*=>\s*'((?:\\'|[^'])*)'\s*,\s*"
        r"'tabs'\s*=>\s*array\s*\((.*?)\)\s*,\s*"
        r"'groups'",
        re.S,
    )
    tab_re = re.compile(
        r"'([a-z0-9_]+)'\s*=>\s*array\s*\(\s*'label'\s*=>\s*'((?:\\'|[^'])*)'\s*,\s*'icon'\s*=>\s*'((?:\\'|[^'])*)'",
        re.S,
    )
    areas: list[dict] = []
    for m in area_re.finditer(text):
        area_key, label, icon, _desc, tabs_blob = m.groups()
        tabs = []
        for tm in tab_re.finditer(tabs_blob):
            tab_key, tab_label, tab_icon = tm.groups()
            tabs.append(
                {
                    "id": tab_key,
                    "label": tab_label,
                    "icon": tab_icon,
                    "href": f"/ERP/?epc_erp_shell=1&area={area_key}&tab={tab_key}",
                }
            )
        areas.append(
            {
                "id": area_key,
                "label": label,
                "icon": icon,
                "href": f"/ERP/?epc_erp_shell=1&area={area_key}",
                "tabs": tabs,
            }
        )
    return areas


def parse_bos_modules(text: str) -> tuple[list[dict], list[dict]]:
    sections = []
    for m in re.finditer(
        r"\$sections\['([^']+)'\]\s*=\s*array\s*\(\s*'id'\s*=>\s*'([^']+)'\s*,\s*'label'\s*=>\s*'([^']+)'",
        text,
    ):
        sections.append({"key": m.group(1), "id": m.group(2), "label": m.group(3)})

    items: list[dict] = []
    seen: set[str] = set()
    for m in re.finditer(
        r"'id'\s*=>\s*'([^']+)'\s*,\s*'label'\s*=>\s*'([^']+)'\s*,\s*'icon'\s*=>\s*'([^']+)'\s*,\s*'path'\s*=>\s*'([^']+)'",
        text,
    ):
        mid, label, icon, path = m.groups()
        if mid in seen:
            continue
        seen.add(mid)
        if path.startswith("http") or path.startswith("/"):
            href = path
        elif "epc_erp_shell" in path or path.startswith("shop/finance/erp"):
            q = path.split("?", 1)[1] if "?" in path else "epc_erp_shell=1&area=overview"
            href = f"/ERP/?{q}"
        elif path.startswith(("control/", "shop/", "general_pages/")):
            href = f"/CP/{path}"
        else:
            href = f"/BOS/?m={mid}"
        items.append({"id": mid, "label": label, "icon": icon, "path": path, "href": href})
    return sections, items


def parse_cp_brochure(text: str) -> list[dict]:
    features: list[dict] = []
    cat = "general"
    for line in text.splitlines():
        cm = re.match(r"\s*'([^']+)'\s*=>\s*array\s*\(\s*$", line)
        if cm:
            maybe = cm.group(1)
            if maybe not in {"name", "does", "url", "scope"} and (
                " " in maybe or "/" in maybe or maybe[:1].isupper()
            ):
                cat = maybe
        m = re.search(
            r"array\s*\(\s*'name'\s*=>\s*'((?:\\'|[^'])*)'\s*,\s*'does'\s*=>\s*'((?:\\'|[^'])*)'\s*,\s*'url'\s*=>\s*'((?:\\'|[^'])*)'\s*,\s*'scope'\s*=>\s*'([^']*)'\s*\)",
            line,
        )
        if not m:
            continue
        name, does, url, scope = m.groups()
        href = url if url else "/CP/"
        if href and not href.startswith(("http://", "https://", "/")):
            href = "/" + href.lstrip("/")
        if href.startswith("/cp/"):
            href = "/CP/" + href[4:]
        elif href in {"/cp", "/cp/"}:
            href = "/CP/"
        features.append(
            {
                "id": slug(name),
                "name": name,
                "does": does,
                "href": href,
                "scope": scope,
                "category": cat,
            }
        )
    return uniquify_ids(features)


def uniquify_ids(rows: list[dict], key: str = "id") -> list[dict]:
    """Ensure stable unique ids when PHP brochure inventory repeats the same name."""
    used: set[str] = set()
    base_counts: dict[str, int] = {}
    out: list[dict] = []
    for row in rows:
        base = str(row.get(key) or "item")
        base_counts[base] = base_counts.get(base, 0) + 1
        item = dict(row)
        candidate = base
        if candidate in used:
            href = str(item.get("href") or "")
            suffix = slug(href) if href else str(base_counts[base])
            candidate = f"{base}__{suffix}" if suffix else f"{base}__{base_counts[base]}"
            n = base_counts[base]
            while candidate in used:
                n += 1
                candidate = f"{base}__{n}"
        used.add(candidate)
        item[key] = candidate
        out.append(item)
    return out


def write_csharp(catalog: dict, path: Path) -> None:
    areas = catalog["erpAreas"]
    cats = catalog.get("erpCategories") or []
    bos = catalog["bosModules"]
    cp = catalog["cpBrochureFeatures"]
    sf = catalog["storefrontSurfaces"]
    lines = [
        "// <auto-generated> by scripts/generate_php_module_catalog.py — do not hand-edit.",
        "#nullable enable",
        "namespace EcomAE.Platform.Presentation;",
        "",
        "/// <summary>Complete PHP-sourced module directory for hybrid ASP.NET chrome (deeplink to PHP).",
        "/// Every CP/ERP/BOS/storefront surface is listed so nothing is omitted from navigation.</summary>",
        "public static partial class PhpModuleCatalog",
        "{",
        "    public sealed record ModuleLink(string Id, string Label, string Href, string? Icon = null, string? Group = null);",
        "",
        f"    public const int ErpAreaCount = {len(areas)};",
        f"    public const int ErpTabCount = {sum(len(a['tabs']) for a in areas)};",
        f"    public const int ErpCategoryCount = {len(cats)};",
        f"    public const int BosModuleCount = {len(bos)};",
        f"    public const int CpBrochureFeatureCount = {len(cp)};",
        f"    public const int StorefrontSurfaceCount = {len(sf)};",
        "",
        "    public static readonly IReadOnlyList<ModuleLink> ErpCategories =",
        "    [",
    ]
    for c in cats:
        lines.append(
            f'        new("{cs_escape(c["id"])}", "{cs_escape(c["label"])}", "{cs_escape(c["href"])}", "{cs_escape(c["icon"])}", "erp-category"),'
        )
    lines += ["    ];", "", "    public static readonly IReadOnlyList<ModuleLink> ErpAreas =", "    ["]
    for a in areas:
        lines.append(
            f'        new("{cs_escape(a["id"])}", "{cs_escape(a["label"])}", "{cs_escape(a["href"])}", "{cs_escape(a["icon"])}", "erp"),'
        )
    lines += ["    ];", "", "    public static readonly IReadOnlyList<ModuleLink> ErpTabs =", "    ["]
    for a in areas:
        for t in a["tabs"]:
            lines.append(
                f'        new("{cs_escape(a["id"])}/{cs_escape(t["id"])}", "{cs_escape(t["label"])}", "{cs_escape(t["href"])}", "{cs_escape(t["icon"])}", "{cs_escape(a["id"])}"),'
            )
    lines += ["    ];", "", "    public static readonly IReadOnlyList<ModuleLink> BosModules =", "    ["]
    for b in bos:
        lines.append(
            f'        new("{cs_escape(b["id"])}", "{cs_escape(b["label"])}", "{cs_escape(b["href"])}", "{cs_escape(b["icon"])}", "bos"),'
        )
    lines += ["    ];", "", "    public static readonly IReadOnlyList<ModuleLink> CpBrochureFeatures =", "    ["]
    for f in cp:
        lines.append(
            f'        new("{cs_escape(f["id"])}", "{cs_escape(f["name"])}", "{cs_escape(f["href"])}", null, "{cs_escape(f["category"])}"),'
        )
    lines += ["    ];", "", "    public static readonly IReadOnlyList<ModuleLink> StorefrontSurfaces =", "    ["]
    for s in sf:
        lines.append(
            f'        new("{cs_escape(s["id"])}", "{cs_escape(s["label"])}", "{cs_escape(s["href"])}", null, "storefront"),'
        )
    lines += ["    ];", "}"]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    out_dir = root / "aspnet/src/EcomAE.Platform/Presentation/Generated"
    out_dir.mkdir(parents=True, exist_ok=True)
    ev_dir = root / "docs/migration/evidence/presentation"
    ev_dir.mkdir(parents=True, exist_ok=True)

    erp_text = (root / "cp/content/shop/finance/erp/erp_nav_areas.php").read_text(encoding="utf-8", errors="replace")
    bos_text = (root / "content/general_pages/epc_bos_unified.php").read_text(encoding="utf-8", errors="replace")
    inv_text = (root / "content/general_pages/epc_cp_brochure_inventory.php").read_text(encoding="utf-8", errors="replace")

    areas = parse_erp_areas(erp_text)
    categories = parse_erp_categories(erp_text)
    bos_sections, bos_items = parse_bos_modules(bos_text)
    cp_features = parse_cp_brochure(inv_text)

    storefront = [
        {"id": "home", "label": "Home", "href": "https://epartscart.com/"},
        {"id": "part_search", "label": "Part search", "href": "https://epartscart.com/shop/part_search"},
        {"id": "vin_search", "label": "VIN / vehicle search", "href": "https://epartscart.com/shop/part_search"},
        {"id": "catalog", "label": "Catalogue", "href": "https://epartscart.com/"},
        {"id": "cart", "label": "Cart", "href": "https://epartscart.com/"},
        {"id": "checkout", "label": "Checkout", "href": "https://epartscart.com/"},
        {"id": "account", "label": "Account", "href": "https://epartscart.com/"},
        {"id": "garage", "label": "Garage", "href": "https://epartscart.com/"},
        {"id": "orders", "label": "Orders", "href": "https://epartscart.com/"},
        {"id": "returns", "label": "Returns", "href": "https://epartscart.com/"},
        {"id": "payments", "label": "Payments", "href": "https://epartscart.com/"},
        {"id": "support", "label": "Support", "href": "https://epartscart.com/"},
    ]

    catalog = {
        "generatedFrom": [
            "cp/content/shop/finance/erp/erp_nav_areas.php",
            "content/general_pages/epc_bos_unified.php",
            "content/general_pages/epc_cp_brochure_inventory.php",
        ],
        "policy": "hybrid-deeplink-to-php-until-aspnet-module-complete",
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "counts": {
            "erpAreas": len(areas),
            "erpTabs": sum(len(a["tabs"]) for a in areas),
            "erpCategories": len(categories),
            "bosModules": len(bos_items),
            "bosSections": len(bos_sections),
            "cpBrochureFeatures": len(cp_features),
            "storefrontSurfaces": len(storefront),
        },
        "erpAreas": areas,
        "erpCategories": categories,
        "bosSections": bos_sections,
        "bosModules": bos_items,
        "cpBrochureFeatures": cp_features,
        "storefrontSurfaces": storefront,
    }

    json_path = out_dir / "php_module_catalog.json"
    evidence_catalog = {
        **catalog,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
    }
    json_path.write_text(json.dumps(catalog, indent=2) + "\n", encoding="utf-8")
    evidence_catalog_path = ev_dir / "php_module_catalog.json"
    evidence_catalog_path.write_text(
        json.dumps(evidence_catalog, indent=2) + "\n", encoding="utf-8"
    )
    (ev_dir / "php_module_catalog_counts.json").write_text(
        json.dumps(
            {
                "counts": catalog["counts"],
                "policy": catalog["policy"],
                "cutoverAllowed": False,
                "readyForPhpRemoval": False,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    cs_path = out_dir / "PhpModuleCatalog.g.cs"
    write_csharp(catalog, cs_path)

    print(json.dumps(catalog["counts"], indent=2))
    print(f"Wrote {cs_path}")
    print(f"Wrote {json_path}")
    print(f"Wrote {evidence_catalog_path}")

    # Hard fail if catalogs are incomplete vs known PHP inventory floors.
    c = catalog["counts"]
    errors = []
    # 35 sidebar areas with tabs + 9 category aliases (parsed separately into ErpAreas).
    if c["erpAreas"] < 35:
        errors.append(f"erpAreas={c['erpAreas']} expected >=35")
    if c["erpTabs"] < 140:
        errors.append(f"erpTabs={c['erpTabs']} expected >=140")
    if c["bosModules"] < 90:
        errors.append(f"bosModules={c['bosModules']} expected >=90")
    if c["cpBrochureFeatures"] < 380:
        errors.append(f"cpBrochureFeatures={c['cpBrochureFeatures']} expected >=380")
    if errors:
        print("ERROR: catalog incomplete:", "; ".join(errors), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
