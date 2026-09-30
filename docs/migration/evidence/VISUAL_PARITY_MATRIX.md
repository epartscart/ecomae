# ASP.NET Core visual parity matrix

This matrix is an acceptance register, not a route-count report. A surface is
not accepted until functional, visual, UX, responsive, performance, and
PHP-reference evidence are present. Until then its state remains
**FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING**.

## Evidence rules

For each important screen, capture the actual PHP page and the corresponding
ASP.NET Core page at the same browser size, tenant, user, record, and filters.
Record missing elements, intentional improvements, and remaining gaps before
repeating the comparison. PHP remains the visual and behavioural reference
until the new design system is formally accepted.

| PHP route | ASP.NET route | Functional | Fields | Actions | Permission | Layout | Visual | Responsive | Report/export | Screenshot evidence | State |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| `/cp/` | `/cp/` | pending | pending | pending | pending | pending | pending | pending | pending | required | FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING |
| `/cp/orders.php` | `/cp/orders` | pending | pending | pending | pending | pending | pending | pending | pending | required | FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING |
| `/cp/crm_main.php` | `/cp/crm` | pending | pending | pending | pending | pending | pending | pending | pending | required | FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING |
| `/ERP/erp_dashboard.php` | `/erp/` | pending | pending | pending | pending | pending | pending | pending | pending | required | FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING |
| `/ERP/erp_invoice.php` | `/erp/invoices` | pending | pending | pending | pending | pending | pending | pending | pending | required | FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING |
| `/ERP/erp_purchase_order.php` | `/erp/purchase-orders` | pending | pending | pending | pending | pending | pending | pending | pending | required | FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING |
| `/ERP/erp_sales_order.php` | `/erp/sales-orders` | pending | pending | pending | pending | pending | pending | pending | pending | required | FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING |
| `/ERP/epc_erp_jewellery.php` | `/erp/jewellery/` | pending | pending | pending | pending | pending | pending | pending | pending | required | FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING |
| `/ERP/epc_erp_project_accounting.php` | `/erp/fitout/` | pending | pending | pending | pending | pending | pending | pending | pending | required | FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING |

## Reusable design-system coverage

The shared foundation is consumed through the CP/ERP presentation asset lists,
not page-specific copies. Current primitives include:

- dense enterprise tables with sticky headers and overflow handling;
- toolbars, filters, action groups, sections, KPI cards, status badges, and
  progress indicators;
- semantic positive/warning/critical/info/inactive colours with text labels;
- responsive toolbar collapse and reduced-motion handling;
- existing PHP-compatible chrome, document workspaces, finance workspaces,
  tabs, and tenant-controlled design-token boundaries.

## Root-cause audit register

### Current repository baseline

The first static audit of the ASP.NET presentation tree found 331 page
components, 169 page files still containing legacy Bootstrap table class
patterns, 66 page files already using the shared `epc-erp-table-wrap`, and 22
components consuming `ErpDocumentWorkspace`. This is a baseline for prioritising
shared migration work, not an acceptance percentage: rendered PHP comparison is
still required.

| Root cause to audit | Evidence method | Current classification |
|---|---|---|
| Generic Bootstrap or page-local styling | stylesheet and rendered DOM audit | open |
| Excessive whitespace / weak density | same-size screenshot comparison | open |
| Missing icons, badges, panels, hover/focus/loading states | component and screenshot audit | open |
| Plain tables without operational grid affordances | grid interaction test | open |
| Missing responsive and print/export behaviour | desktop/tablet/mobile capture | open |
| Missing live business context or drill-down | route/data trace | open |
| Visual changes causing load or rendering regression | browser performance measurement | open |

## Acceptance vocabulary

- **FUNCTIONALLY MIGRATED — VISUAL PARITY PENDING**: route and functional
  evidence exist, but screenshot or UX gates are incomplete.
- **VISUAL PARITY REVIEW**: same-data PHP/ASP.NET comparison is in progress.
- **ACCEPTED**: all applicable matrix columns, screenshot evidence,
  performance checks, and human review are complete.
