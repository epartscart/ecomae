#!/usr/bin/env bash
# Capture/refresh module-function parity inventory from full PHP catalog + hybrid TARGETS.
# Enumerates every CP/ERP/BOS/storefront catalog entry as php-only (or hybrid when TARGET matches).
# aspnet-complete stays 0. Never invents pass/approval files.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="${ECOMAE_MODULE_FUNCTION_SAMPLES_DIR:-$ROOT/docs/migration/evidence/module-function-parity}"
OVERWRITE="${ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES:-0}"
mkdir -p "$OUT_DIR"

export ECOMAE_MODULE_FUNCTION_SAMPLES_DIR="$OUT_DIR"
export ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES="$OVERWRITE"
export ECOMAE_HYBRID_CAPTURE="$ROOT/scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh"
export ECOMAE_PHP_MODULE_CATALOG="${ECOMAE_PHP_MODULE_CATALOG:-$ROOT/aspnet/src/EcomAE.Platform/Presentation/Generated/php_module_catalog.json}"

python3 - <<'PY'
import datetime
import json
import os
import re
from pathlib import Path

out_dir = Path(os.environ["ECOMAE_MODULE_FUNCTION_SAMPLES_DIR"])
overwrite = os.environ.get("ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES", "0") == "1"
repo = Path.cwd()
capture = Path(os.environ["ECOMAE_HYBRID_CAPTURE"]).read_text(encoding="utf-8")
block = capture.split("TARGETS = [", 1)[1].split("]", 1)[0]
row_re = re.compile(
    r'\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*,\s*"([^"]+)"'
)
now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

hybrid_by_php: dict[str, dict] = {}
hybrid_by_app: dict[str, dict] = {}
hybrid_modules = []
for stem, surface, app_route, digest_route, php_path in row_re.findall(block):
    status = "digest-only+hybrid-deeplink" if digest_route else "hybrid-deeplink"
    row = {
        "id": stem,
        "surface": surface,
        "kind": "hybrid-preview",
        "aspnetRoute": app_route,
        "digestRoute": digest_route or None,
        "phpPath": php_path,
        "status": status,
        "aspnetComplete": False,
        "writesRemainPhp": True,
        "humanFunctionalEvidence": False,
        "note": "Hybrid preview/deeplink only — interactive product chrome remains PHP.",
    }
    hybrid_modules.append(row)
    hybrid_by_app[app_route.rstrip("/")] = row

# Fill php path index after normalize helper exists below.

catalog_path = Path(os.environ["ECOMAE_PHP_MODULE_CATALOG"])
if not catalog_path.is_file():
    raise SystemExit(f"FAIL: missing PHP module catalog: {catalog_path}")
catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
catalog_counts = catalog.get("counts") if isinstance(catalog.get("counts"), dict) else {}


def normalize_php(url: str) -> str:
    value = url.strip()
    # Strip scheme/host for storefront absolute URLs.
    if "://" in value:
        value = "/" + value.split("://", 1)[1].split("/", 1)[-1]
    return value.rstrip("/") or "/"


BROAD_PHP_PATHS = {
    "/",
    "/CP",
    "/ERP",
    "/BOS",
    "/cp",
    "/erp",
    "/bos",
    "/storefront",
}

for hybrid in hybrid_modules:
    key = normalize_php(str(hybrid.get("phpPath") or ""))
    # Do not auto-upgrade whole surfaces when TARGET phpPath is a root chrome URL.
    if key in BROAD_PHP_PATHS:
        continue
    hybrid_by_php[key] = hybrid


def match_hybrid(php_href: str | None, app_hint: str | None = None) -> dict | None:
    # Never match catalog rows by aspnetRoute alone — only concrete PHP paths.
    if not php_href:
        return None
    key = normalize_php(php_href)
    if key in BROAD_PHP_PATHS:
        return None
    return hybrid_by_php.get(key)


def entry(
    *,
    entry_id: str,
    surface: str,
    kind: str,
    label: str,
    php_path: str,
    aspnet_route: str | None = None,
    digest_route: str | None = None,
    extra: dict | None = None,
) -> dict:
    hybrid = match_hybrid(php_path, aspnet_route)
    if hybrid:
        status = hybrid["status"]
        aspnet_route = hybrid.get("aspnetRoute") or aspnet_route
        digest_route = hybrid.get("digestRoute") or digest_route
        note = (
            f"Catalog {kind} matched hybrid TARGET {hybrid['id']}. "
            "Interactive product chrome remains PHP."
        )
    else:
        status = "php-only"
        note = (
            f"Full PHP catalog {kind} — no ASP.NET interactive parity yet; "
            "hybrid directory may deeplink to PHP."
        )
    row = {
        "id": entry_id,
        "surface": surface,
        "kind": kind,
        "label": label,
        "aspnetRoute": aspnet_route,
        "digestRoute": digest_route,
        "phpPath": php_path,
        "status": status,
        "aspnetComplete": False,
        "writesRemainPhp": True,
        "humanFunctionalEvidence": False,
        "note": note,
    }
    if extra:
        row.update(extra)
    return row


modules: list[dict] = []

for area in catalog.get("erpAreas") or []:
    if not isinstance(area, dict):
        continue
    area_id = str(area.get("id") or "")
    area_href = str(area.get("href") or f"/ERP/?epc_erp_shell=1&area={area_id}")
    modules.append(
        entry(
            entry_id=f"erp-area-{area_id}",
            surface="erp",
            kind="erp-area",
            label=str(area.get("label") or area_id),
            php_path=area_href,
            extra={"areaId": area_id},
        )
    )
    for tab in area.get("tabs") or []:
        if not isinstance(tab, dict):
            continue
        tab_id = str(tab.get("id") or "")
        tab_href = str(
            tab.get("href")
            or f"/ERP/?epc_erp_shell=1&area={area_id}&tab={tab_id}"
        )
        modules.append(
            entry(
                entry_id=f"erp-tab-{area_id}-{tab_id}",
                surface="erp",
                kind="erp-tab",
                label=str(tab.get("label") or tab_id),
                php_path=tab_href,
                extra={"areaId": area_id, "tabId": tab_id},
            )
        )

for cat in catalog.get("erpCategories") or []:
    if not isinstance(cat, dict):
        continue
    cat_id = str(cat.get("id") or "")
    modules.append(
        entry(
            entry_id=f"erp-category-{cat_id}",
            surface="erp",
            kind="erp-category",
            label=str(cat.get("label") or cat_id),
            php_path=str(cat.get("href") or f"/ERP/?epc_erp_shell=1&category={cat_id}"),
            extra={"categoryId": cat_id},
        )
    )

for bos in catalog.get("bosModules") or []:
    if not isinstance(bos, dict):
        continue
    bos_id = str(bos.get("id") or "")
    modules.append(
        entry(
            entry_id=f"bos-{bos_id}",
            surface="bos",
            kind="bos-module",
            label=str(bos.get("label") or bos_id),
            php_path=str(bos.get("href") or f"/BOS/"),
            extra={"path": bos.get("path"), "section": bos.get("section")},
        )
    )

for cp in catalog.get("cpBrochureFeatures") or []:
    if not isinstance(cp, dict):
        continue
    cp_id = str(cp.get("id") or "")
    modules.append(
        entry(
            entry_id=f"cp-{cp_id}",
            surface="cp",
            kind="cp-feature",
            label=str(cp.get("name") or cp_id),
            php_path=str(cp.get("href") or "/CP/"),
            extra={
                "category": cp.get("category"),
                "scope": cp.get("scope"),
                "does": cp.get("does"),
            },
        )
    )

for sf in catalog.get("storefrontSurfaces") or []:
    if not isinstance(sf, dict):
        continue
    sf_id = str(sf.get("id") or "")
    modules.append(
        entry(
            entry_id=f"storefront-{sf_id}",
            surface="storefront",
            kind="storefront-surface",
            label=str(sf.get("label") or sf_id),
            php_path=str(sf.get("href") or "https://epartscart.com/"),
        )
    )

# Ensure every hybrid TARGET appears even if catalog href matching missed it.
seen_ids = {m["id"] for m in modules}
for hybrid in hybrid_modules:
    if hybrid["id"] not in seen_ids and f"hybrid-{hybrid['id']}" not in seen_ids:
        row = dict(hybrid)
        row["id"] = f"hybrid-{hybrid['id']}"
        modules.append(row)

hybrid_preview_count = sum(1 for m in modules if m.get("status") != "php-only")
php_only_count = sum(1 for m in modules if m.get("status") == "php-only")

inventory_path = out_dir / "module-function-inventory.json"
if inventory_path.exists() and not overwrite:
    print(f"keep existing {inventory_path}")
else:
    doc = {
        "role": "module-function-inventory",
        "capturedAt": now,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspnetCompleteCount": 0,
        "moduleCount": len(modules),
        "hybridPreviewCount": hybrid_preview_count,
        "phpOnlyCount": php_only_count,
        "phpCatalogCounts": catalog_counts,
        "phpCatalogScopeNote": (
            "modules[] enumerates the full PHP catalog (ERP areas/tabs/categories, BOS modules, "
            "CP brochure features, storefront surfaces). Hybrid TARGET matches upgrade status; "
            "everything else remains php-only until human MODULE_FUNCTION_TEST_PASS.md exists."
        ),
        "source": (
            "aspnet/.../Generated/php_module_catalog.json + "
            "scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh TARGETS"
        ),
        "note": (
            "Full PHP module/menu/function inventory contract. "
            "aspnet-complete remains 0 until human MODULE_FUNCTION_TEST_PASS.md exists."
        ),
        "modules": modules,
    }
    inventory_path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(
        f"wrote {inventory_path} moduleCount={len(modules)} "
        f"hybridPreviewCount={hybrid_preview_count} phpOnlyCount={php_only_count} "
        f"phpCatalogCounts={catalog_counts}"
    )

readme = out_dir / "README.md"
if overwrite or not readme.exists():
    readme.write_text(
        "# Module function parity evidence\n\n"
        "Full PHP catalog inventory (CP/ERP/BOS/storefront). Hybrid TARGETS upgrade a subset "
        "to deeplink/digest status; all other entries stay `php-only`. "
        "`aspnet-complete` count stays **0** until a human attaches "
        "`docs/migration/evidence/presentation/MODULE_FUNCTION_TEST_PASS.md` containing "
        "`MODULE_FUNCTION_PARITY_PASS`.\n\n"
        "Never invent that pass file or `RELEASE_OWNER_APPROVAL.md`. "
        "`cutoverAllowed` always false.\n\n"
        "Operator:\n\n"
        "```bash\n"
        "bash scripts/cloudpanel_run_module_function_parity_operator.sh\n"
        "```\n",
        encoding="utf-8",
    )
    print(f"wrote {readme}")
PY
