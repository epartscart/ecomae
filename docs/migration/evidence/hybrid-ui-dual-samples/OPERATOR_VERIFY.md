# Operator verify — hybrid UI dual samples

www exact-route Blazor previews only. Tenant product chrome stays PHP.

```bash
# Offline contract stubs (default)
bash scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh

# All dual-sample families + module-function inventory
bash scripts/cloudpanel_run_all_dual_sample_operators.sh
```

Live cookie overwrite (CloudPanel):

```bash
set -a; source /etc/ecomae-aspnet/platform.env; set +a
ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES=1 \
  bash scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh
```

Full PHP catalog deeplink floor (714 hrefs must be hybrid-iframe safe):

```bash
python3 scripts/validate_php_module_catalog_deeplink_floor.py
```

Hybrid Blazor shells must expose the full catalog directories (CP features, ERP
categories/areas/tabs, BOS modules, storefront surfaces):

```bash
python3 scripts/validate_hybrid_directory_full_catalog_floor.py
```

Expect compare `cutoverAllowed=false` and `aspNetInteractiveComplete=0`.
Never invent `RELEASE_OWNER_APPROVAL.md`.
