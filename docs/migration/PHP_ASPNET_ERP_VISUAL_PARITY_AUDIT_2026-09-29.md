# PHP → ASP.NET ERP visual and structural parity audit

**Evidence:** `php_reference.docx` supplied by the owner on 2026-09-29  
**Purpose:** explain why the current ASP.NET ERP does not yet look or behave
like the PHP reference, and convert the observed gap into mandatory migration
work rather than treating route coverage or a shared shell as completion.

## Executive conclusion

The owner's concern is valid. The production ERP remains PHP-primary, and the
attached evidence shows that the PHP reference is a mature, data-dense ERP
application rather than a collection of navigation pages. The ASP.NET work
completed so far has established route contracts, shared chrome, selected
read/write services, and parity scaffolding, but it has not yet reproduced the
module bodies, page composition, field density, workflow controls, report
layouts, and operational states shown in the reference.

The previous "coverage" measures mixed four different things:

1. **Route/catalog coverage:** a menu item or URL exists.
2. **Contract coverage:** a digest or dry-run response is available.
3. **Shell/presentation coverage:** a shared ASP.NET frame renders.
4. **Interactive parity:** the same module fields, actions, data, workflows,
   reports, permissions, and visual states work against the tenant database.

Only the fourth is sufficient for PHP removal. The attached screenshots
demonstrate why the first three must not be reported as ERP completion.

## What the PHP reference actually contains

The 31 screenshots cover the following concrete ERP capabilities:

| Evidence | Reference capability | What must be reproduced |
|---|---|---|
| `image1.png`–`image6.png` | UAE e-invoicing overview, seller profile, operator/setup state, ASP and FTA reporting | Five-corner process explanation, mandatory-field guidance, profile forms, ASP mode, FTA reporting mode, XML/Peppol terminology, validation states, and clear next actions |
| `image7.png` | VAT Return (FTA VAT 201) | Reporting-period controls, filing guidance, return-box detail, source rows, tax totals, status badges, and export/filing actions |
| `image8.png`–`image9.png` | Corporate Tax return | Official-source context, period/entity controls, return sections, explanatory text, filing evidence, and detailed tax calculations |
| `image10.png` | External audit report | A report workspace with filters, report metadata, audit-oriented output, and detailed result rows—not a link card |
| `image11.png` | Document control | Compliance document register, expiry/ownership metadata, search/filtering, and action state |
| `image12.png` | Insurance management | Policy/claim/expiry tracking with operational records and status, not a static module description |
| `image13.png` | Accounting setup | A setup workspace with company, security, batch/platform, data/integration groupings and actionable configuration |
| `image14.png` | Tenant configuration | Tenant/company context, configuration sections, effective settings, and administrative writes |
| `image15.png` | Voucher/print designer | Layout preview/configuration for vouchers, invoices, purchase orders, and reports: logo, columns, colours, terms, signatures, and output format |
| `image16.png`–`image17.png` | Automation centre and accounting automation | Process catalogue, visual flow cards, enable/run controls, workflow status, and accounting-event automation |
| `image18.png` | Document formats | Document-format definitions and output configuration |
| `image19.png` | Listing | Organization/listing setup with records and configuration, not a generic list shell |
| `image20.png` | Labour-law compliance | Country/effective-date compliance content, obligations, controls, and evidence |
| `image21.png` | Payroll | Payroll-run workspace, earnings, deductions, statutory fields, and run/status controls |
| `image22.png` | Order planning | Planning filters, inventory/order signals, action controls, error/CSRF state, and planning output |
| `image23.png` | Product information system | Product/variant structures and master-data navigation |
| `image24.png` | Customs and shipping | Inbound/outbound logistics, customs records, shipping fields, and operational status |
| `image25.png` | Fixed assets | Asset register, lifecycle, maintenance, and financial fields |
| `image26.png` | CRM | Prospects, quotations, sales orders, delivery, invoicing, and CRM navigation |
| `image27.png` | Fulfilment | Fulfilment work queue and order-to-cash operational states |
| `image28.png` | Customer setup / AR | Customer invoices, settlement, account-receivable setup, and records |
| `image29.png` | Vendor setup / AP | Vendor invoices, payments, and accounts-payable setup |
| `image30.png` | Landed cost | Voyage/cost allocation/apportionment records, totals, and actions |
| `image31.png` | Process flow | Dashboard/process-flow visualization with connected process stages and navigation |

These are not merely visual preferences. Each screenshot encodes a contract:
what the operator sees, which data is grouped together, which actions are
available at each status, what explanations are shown, and where the operator
can continue the process.

## Observed PHP presentation system

Across the screenshots, the PHP ERP consistently provides:

- a stable ERP breadcrumb (`ERP / area / module`), global search, company/legal
  entity context, operator identity, guide/help access, and a persistent ERP
  shell;
- a module-specific title and description explaining the business purpose;
- dense but structured blue/teal section bars, bordered cards, tables, tabs,
  badges, totals, and status colours;
- action panes and visible next-step controls rather than hidden generic
  quick-actions;
- explicit empty, error, validation, and compliance guidance states;
- report periods, company scope, effective dates, tax/legal references, and
  source-document detail near the data they govern;
- layouts that support both overview workspaces and detailed entry/register
  pages;
- consistent module-to-module composition while still allowing each area to
  expose its real fields and workflows.

The ERP is therefore a **page system plus a workflow system**, not just a
navigation system.

## Why the ASP.NET presentation is currently different

The mismatch is architectural and process-related; it is not caused by
ASP.NET Core being unable to reproduce the PHP design.

### 1. Migration began from contracts and route shadows

Early ASP.NET work prioritized tenant resolution, auth bridges, route
ownership, response contracts, migration telemetry, and safe PHP fallback.
That was necessary for production safety, but it produced shells and digest
pages before the full PHP page bodies were ported. A route can therefore be
correct while its module body is still a placeholder or summary.

### 2. Shared chrome was mistaken for page parity

The ASP.NET implementation now has a more premium shared ERP/CP frame, but a
shared frame cannot reproduce module-specific controls, tables, field groups,
report sections, workflow diagrams, or tax explanations. The PHP screenshots
show that most of the visual identity is inside each module body.

### 3. The PHP page is a server-rendered composition of module fragments

PHP assembles navigation, company context, help, field fragments, action
buttons, tables, status labels, and AJAX handlers from the same ERP vocabulary.
The ASP.NET port has often represented the same destination as a Razor page
plus a digest/read service. Without porting the PHP fragment structure and
state transitions, the result is semantically related but visually and
operationally different.

### 4. Data density and schema-backed states were deferred

The reference screens expose real records, totals, dates, statuses, tax boxes,
source links, and configuration values. Many ASP.NET pages initially used
contract/sample/digest data or dry-run write paths. That makes the screen look
cleaner and less dense than PHP, but it is not same-to-same parity.

### 5. ASP.NET route ownership is still mixed in production

The verified production state is not one ASP.NET ERP:

- `/erp/login` is ASP.NET-primary.
- `/erp/` remains PHP-backed.
- `/cp/shop/finance/erp?...` is a PHP compatibility redirect to
  `/cp/control?...`; the final CP page is ASP.NET-primary, but the original ERP
  path is not itself an ASP.NET module implementation.

Consequently, the owner is comparing a mature PHP ERP page with an ASP.NET
login/shell/preview surface and seeing a real difference in both ownership and
implementation maturity.

### 6. CSS and component primitives are not yet contractually locked

The PHP reference uses a long-established palette, spacing rhythm, dense table
styles, section bars, button hierarchy, badges, and responsive rules. ASP.NET
components have been added incrementally and can use different markup,
component boundaries, spacing, and defaults. A common stylesheet alone will
not fix this; each module needs a page-level visual contract and screenshot
comparison.

### 7. The verification gate was too permissive

Route existence, source-contract tests, successful builds, and full unit-suite
results can all pass while a module remains visually or functionally unlike
PHP. The gate must require authenticated browser comparison with the same
tenant, company, dates, permissions, seeded records, and action states.

## Remediation plan

### Gate V0 — freeze honest status

- Keep PHP/PHP-FPM as production authority and rollback.
- Do not call a module migrated because its route, menu item, digest, or shell
  exists.
- Mark every screenshot-covered module as `PHP reference / ASP.NET parity
  audit required` until the evidence below is attached.
- Keep `/erp/` and the legacy CP finance redirect documented as route-ownership
  gaps.

### Gate V1 — build the reference matrix

For every PHP ERP area/tab:

1. record the PHP source page, included fragments, AJAX actions, tables, and
   permission decisions;
2. capture the authenticated PHP page at a fixed viewport with a fixed tenant,
   company, date range, and seeded data;
3. identify every heading, section, tab, field, column, button, badge, help
   block, error state, empty state, redirect, and detail link;
4. map each item to an ASP.NET component/service/endpoint or mark it missing;
5. assign `read`, `write`, `workflow`, `report`, `presentation`, and `browser`
   evidence statuses independently.

### Gate V2 — port the shared ERP page contract

Create one reusable ASP.NET ERP page contract for:

- breadcrumb and area/module identity;
- global search and company/legal-entity context;
- guide/help and source/reference links;
- action pane with New/Edit/Delete/Void/Submit/Approve/Post/Export/Print
  visibility driven by status and capability;
- KPI/status strip;
- section bars and dense tables;
- validation, warning, error, empty, and success states;
- report period and filter controls;
- audit/source-document links;
- responsive layout at the PHP reference viewport sizes.

This contract is only the frame. It must not replace module-specific bodies.

### Gate V3 — implement modules in evidence order

First reproduce the modules in the supplied evidence because they expose the
largest compliance and presentation gap:

1. VAT return and Corporate Tax return;
2. e-invoice overview/profile/ASP/FTA reporting;
3. external audit report and document control;
4. accounting setup, tenant configuration, voucher/print designer;
5. automation centre and accounting automation;
6. labour law, payroll, order planning, PIM, customs/shipping, fixed assets;
7. CRM, fulfilment, AR/customer setup, AP/vendor setup, landed cost;
8. process-flow dashboard and cross-module source links.

Each module is complete only when its PHP fields, actions, data, statuses,
permissions, reports, and presentation are all evidenced.

### Gate V4 — same-data browser comparison

For each module, run PHP and ASP.NET against the same throwaway tenant
database and deterministic fixture:

- same legal entity/company and country profile;
- same operator capability set;
- same date range and filters;
- same document/status combinations;
- same viewport and browser;
- screenshots at initial, populated, validation-error, empty, and
  post-action states;
- row/total/XML/report comparison and write-side database corroboration.

A human reviewer must approve the comparison; automated tests alone cannot
close the visual gate.

### Gate V5 — exact-route promotion

Promote `/erp/` and module routes only after V4 passes for the promoted route.
Use exact route rules, not a broad `/erp/*` catch-all. Keep a reversible PHP
fallback and record release identity, health/readiness, browser evidence, and
rollback evidence.

### Gate V6 — removal decision

PHP removal remains blocked until all screenshot-covered modules and the full
ERP top-menu matrix pass V1–V5, followed by CP, tenant, storefront, BOS,
deployment-mode, security, backup/restore, and human-acceptance gates.

## Progress interpretation after this audit

The honest conclusion is:

- route/catalog coverage is materially ahead of module parity;
- shared shell/presentation work is ahead of page-body parity;
- interactive ERP parity remains substantially incomplete;
- production ERP ownership is still PHP-primary;
- the attached evidence adds a concrete, high-priority visual and structural
  backlog rather than a minor styling task.

No percentage should be increased from this evidence alone. The migration
tracker must continue to report implementation progress separately from formal
PHP-removal readiness.
