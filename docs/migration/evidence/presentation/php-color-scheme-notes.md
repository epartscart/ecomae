# PHP color scheme parity (product chrome)

**Locks:** `cutoverAllowed=false`, `readyForPhpRemoval=false`, `aspNetInteractiveComplete=0`.

## Goal

CP, ERP, BOS, storefront, and dashboard Blazor shells use the same color tokens as live PHP views so areas do not look like separate products.

## Canonical tokens (from PHP CSS)

| Surface | Body | Primary / accent | Chrome notes |
|---|---|---|---|
| CP | `#f0f9ff` | `#2563eb` / `#0ea5e9` | Topnav white + `#dc2626` underline; topbar CTA red gradient |
| ERP | `#f0f9ff` | `#2563eb` | Loads `epc_erp_portal/ui/professional` + CP blue theme |
| BOS | `#f0f2f5` | `#0ea5e9` | Topnav `#000000` |
| Storefront | white | `#dc2626` (automotive) / `#ff8040` (modex) | Search/CTA red |

## Changes

- Shared chrome (`Php*DesktopChrome`) tokens aligned to PHP CSS variables
- ERP stylesheet pack includes portal + ui + professional CSS proxies
- ~140 `*App.razor` heroes/CTAs normalized to surface brand gradients (no purple/green inventiveness)
- Floor: `php-color-scheme-floor.json`

## Validate

```bash
python3 scripts/validate_php_color_scheme.py
python3 scripts/validate_same_to_same_look_gaps.py
```
