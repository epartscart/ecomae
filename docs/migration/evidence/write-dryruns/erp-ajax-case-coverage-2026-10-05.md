# ERP ajax_erp.php case coverage audit — 2026-10-05

Method: extracted all 321 `case '<action>'` arms from `cp/content/shop/finance/erp/ajax_erp.php` and mapped each to a dedicated `/erp/ajax/*` route constant in `aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs`, then searched `ErpModule.cs` + `aspnet/src/EcomAE.Platform/Erp/` for every name mismatch.

## Result

- **321/321 PHP ajax_erp actions are covered by an ASP.NET endpoint or handler.**
- 48 actions use a hyphen/underscore-mismatched route name; 46 of those resolve to a same-named service or an alternate route path.
- The only two actions whose PHP action name shares no route or symbol were already live under alternate paths:
  - `sub_status` → `IErpSubscriptionStatusWriteService` at `/erp/subscriptions/status` (PHP `epc_sub_set_status` twin).
  - `coll_case_status` → `IErpCollectionsCaseStatusWriteService` at `/erp/collections/cases/status` (PHP `epc_coll_case_set_status` twin).

## Dry-run-only residual (by design)

- `ajax_erp` registry dry-run `/erp/ajax-writes/dry-run/{action}` — catch-all refusal wrapper for uncatalogued future actions; stays dry forever.
- 6 on-premises CLI rows (`health`, `license-activate`, `activate-license-cli`, `health-check-pack`, `setup-wizard`, `backup`) — stay dry; ASP.NET pack scaffold lives under `deploy/on-premises-aspnet/`.

## Consequence

Every functional ERP write/read ajax route has a live PHP twin behind `confirmWrites` with session CSRF (csrf_guard_key), verbatim PHP messages, and runtime proof on a throwaway MariaDB tenant DB. Remaining ERP acceptance gates are production-side only (deploy + recovery/UAT evidence) and require server access — they cannot be closed by code changes.
