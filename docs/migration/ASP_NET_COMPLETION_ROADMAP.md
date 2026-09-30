# ASP.NET Core completion roadmap

**Status:** planning baseline after the cross-surface audit  
**Authoritative reference:** the PHP/PHP-FPM application remains the behavioural,
visual, security, deployment, and rollback reference until every exit gate passes.

## What the percentages mean

The repository has three different measurements and they must not be combined:

1. **Inventory/shadow coverage** — routes, digests, page components, and menu
   placements that exist.
2. **Weighted migration gates** — the formal tracker headline; currently
   **20.4% complete / 79.6% pending**.
3. **Accepted interactive parity** — same-tenant PHP/ASP.NET field, action,
   workflow, report, permission, presentation, and production evidence; currently
   **0% formally accepted**.

The percentages below are **planning estimates of remaining work**, not claims
that a surface is already accepted. A slice is complete only after its PHP
matrix, writes, permissions, tenant scope, visual comparison, browser evidence,
and rollback evidence pass.

## Area-by-area position

| Area | Current position | Estimated work pending | Next completion gates | Planning band |
| --- | --- | ---: | --- | ---: |
| Super CP / tenant CP | Broad twins, dashboard and many major modules exist; remaining digest/missing-page, generated-menu, body, write, guide, and presentation reconciliation is open. | **35–45%** | Complete the DB menu→PHP file→ASP.NET page matrix; remove every digest/missing result; verify tenant/Super-CP scope; run desktop/mobile PHP comparison. | 3–5 Devin sessions |
| ERP | Top-menu and dispatcher coverage are broad; accounting, tax, invoice, fit-out, jewellery, IFRS, finance presentation, and company switching slices are real, but most PHP module bodies and workflows remain partial. | **50–60%** | Finish B1–B14; complete customer/vendor payment journals and general journals; close OMS/P2P/inventory/GL/treasury/tax/report drill-down gaps; validate company 1/2 and fit-out company 3 against throwaway tenant DBs. | 8–12 Devin sessions |
| Storefront / frontend | ASP.NET route and digest shadows exist for home, catalogue, cart, checkout, account, returns, and industry surfaces; product-host ownership and rich workflows remain PHP-primary. | **80–90%** | Theme/asset parity; search and vehicle flows; guest order and payment callbacks; customer/vendor/B2B workflows; SEO/sitemap; tenant-host dual samples. | 6–9 Devin sessions |
| Marketing / LifeOS / IP | Marketing page family and `/marketing/app` scaffold exist, but forms, brand-host routing, SEO, sitemap, and production shadow installation remain open. | **75–85%** | Contact/demo/newsletter writes, sitemap/robots/canonical parity, host-by-host rendering, rate limits, and shadow deployment. | 3–5 Devin sessions |
| BOS / Super-CP fleet / tenant hub | Fleet/readiness and operator shells exist; provisioning and operator write workflows are not yet accepted as PHP twins. | **65–75%** | Provision/extend/expire/restore demo tenants; operator audit; tenant registry writes; failure/retry states; production smoke evidence. | 3–5 Devin sessions |
| APIs / webhooks / workers | Dispatcher and catalog contracts are extensive, but operational parity is not proven for every callback, queue, retry, upload/download, cron, or scheduled compliance job. | **70–80%** | Enumerate every PHP API/cron; prove idempotency, retry, dead-letter, auth/rate limits, webhook signatures, and tenant scope. | 3–5 Devin sessions |
| Cloud / on-premises / hybrid operations | Health checks and zero-downtime foundations exist; install, licensing, registration/expiry, synchronization, encrypted backup/restore, and disaster recovery are open gates. | **80–90%** | Produce cloud, on-prem, and hybrid runbooks; test offline/on-prem registration expiry; encrypted backup/restore; sync conflict handling; rollback. | 4–6 Devin sessions plus environment waits |
| Security / tenant isolation | RBAC, scoped grants, audit history, CSRF, and company/site policies have substantial coverage, but the module-by-module denial matrix is not closed. | **35–45%** | Negative tests for every write and direct URL; cross-tenant leakage tests; country/effective-date checks; rate-limit and secret-handling review. | 3–4 Devin sessions |
| ERP role dashboards / embedded analytics | ERP already has a PHP-shaped dashboard and role preview catalogue; server-derived manager profiles and tenant-scoped Power BI embed plumbing are being promoted into the ERP home. | **65–75%** | Persist/customise role definitions per user/group; derive every KPI/card from capability and company/site scope; add CFO, CEO, Sales Manager, Purchasing Manager, and custom-role acceptance; protect Power BI embed tokens and validate desktop/mobile layouts. | 2–4 Devin sessions plus Azure/customer configuration |
| Browser acceptance / production cutover | No combined human acceptance round has passed; PHP remains the live fallback/reference. | **100%** | Round 1 CP+ERP; Round 2 storefront+BOS+marketing+workers; Round 3 full regression; shadow approval; rollback rehearsal; release-owner sign-off. | 3–4 Devin sessions plus deployment windows |

These bands deliberately do not add to 100%: they describe independent gates,
and the final cutover is controlled by the strictest unresolved gate.

## Execution order

### Step 1 — Close CP evidence (3–5 sessions)

* Generate the complete CP menu matrix for tenant and Super CP hosts.
* Classify each PHP entry as a real twin, digest, redirect, or missing.
* Finish remaining body/write/action/guide pages, including single-item flows.
* Compare the same records in PHP and ASP.NET at desktop and mobile widths.
* Close the CP exit gate only when the matrix has zero digest and zero missing
  entries for the approved menu.

### Step 2 — Close ERP by business process (8–12 sessions)

Work in process order rather than isolated route order:

1. Home/workflow/approvals and dashboard funnel.
2. Order-to-cash: order → fulfilment → delivery → invoice → return.
3. Procure-to-pay: requisition → RFQ → PO → GRN → invoice → settlement.
4. Inventory/WMS and warehouse planning.
5. Record-to-report, treasury, tax, e-invoice, and external reporting.
6. Jewellery and fit-out industry scenarios.
7. Setup, guides, document designer, and country profiles.

#### Role dashboards and embedded Power BI

The ERP home must be a role-aware workspace, not one unrestricted executive page:

* **CEO:** company-wide revenue, margin, cash, risk, operations, and cross-area
  exception tiles.
* **CFO:** liquidity, GL, AR/AP, tax, treasury, close, and audit evidence.
* **Sales Manager:** pipeline, orders, fulfilment, collections, returns, and
  customer actions without finance-only figures.
* **Purchasing Manager:** RFQs, purchase orders, receipts, supplier exposure,
  three-way match, and AP actions.
* **Custom role:** an administrator-configured combination of areas, KPI cards,
  actions, company/site scope, approval limit, and effective dates.

Power BI is part of the ERP workspace acceptance gate: reports must be embedded
inside the tenant ERP shell when a validated URL/token configuration exists,
while configuration remains in the secured Power BI control surface. API keys,
embed URLs, report IDs, and future Azure embed tokens must never be trusted from
the browser or exposed across tenants. A missing customer Azure configuration
must degrade to a clear setup state, not a broken iframe or PHP-only claim.

Each process requires New/Edit/Delete/Void/Submit/Approve/Post where applicable,
field validation, audit, permission denial, source-document links, and database
corroboration.

#### D365/F&O-style document workspaces: PO, PI, SO, and SI

The attached PHP/D365 reference screenshots establish the target document
experience for the next ERP tranche. The ASP.NET pages must become full
document workspaces rather than list pages with a small opened-record summary.
The shared document shell should provide:

* a persistent ERP command bar with New, Edit, Delete, workflow/status actions,
  posting/settlement actions, document navigation, and contextual menus;
* a document header with number, title, customer or supplier, document date,
  requested/confirmed delivery dates, currency, site/warehouse, owner,
  status, document status, approval state, source/reference fields, and
  company/tenant scope;
* collapsible F&O-style sections/tabs for General, Setup, Address, Delivery,
  Warehouse, Price and discount, Payment, Financial dimensions, References,
  Notes, Attachments, and audit history;
* a dense, horizontally scrollable lines grid with item/SKU, product name,
  description, quantity, unit, site/warehouse, delivery date, unit price or
  cost, discount, tax, line amount, inventory/availability, received or
  delivered quantity, and source-document links;
* line-level add/edit/delete, product lookup, dimensions, inventory
  reservation/availability, tax recalculation, totals, validation messages,
  and keyboard-friendly editing;
* a totals/footer area for subtotal, discounts, charges, tax, rounding,
  paid/settled amount, balance due, and document currency;
* role-aware lifecycle actions with dry-run validation before confirmed writes,
  explicit approval limits, audit entries, and safe disabled states when a
  capability or document status does not permit an action.

The four workspaces share the shell but retain document-specific semantics:

1. **PO — Purchase Order:** supplier, buyer, procurement category, delivery
   warehouse, requested/confirmed dates, RFQ/requisition source, receipt
   quantities, three-way-match state, invoice linkage, and supplier settlement.
2. **PI — Purchase Invoice:** supplier invoice number/date, supplier account,
   PO/GRN links, tax registration, payment terms, due date, three-way-match
   evidence, posting profile, approval/hold state, and AP settlement.
3. **SO — Sales Order:** customer/account and contact, delivery address,
   requested/confirmed ship dates, warehouse/fulfilment policy, reservation,
   carrier/transport, payment terms, sales tax, discounts, project/reference
   fields, delivery-note linkage, and invoice/collection state.
4. **SI — Sales Invoice:** customer and billing address, originating SO and
   delivery note, issue/due dates, tax/e-invoice authority state, payment
   terms, currency, settlement history, credit-note/cancellation controls,
   and AR balance/collection state.

The implementation order is **shared document shell → SO/SI order-to-cash
pair → PO/PI procure-to-pay pair → cross-document links and reports**. Each
pair must be accepted against the PHP reference using the same tenant records
and must pass field, line, action, permission, database, browser, responsive
desktop/mobile, visual, and rollback evidence. Until that evidence is complete,
these routes remain ASP.NET previews/shadows and PHP remains authoritative.

### Step 3 — Close storefront and remaining public surfaces (6–9 sessions)

* Reproduce the PHP themes and asset loading on each industry host.
* Test catalogue/search, vehicle search, cart, obtaining modes, guest checkout,
  payment callbacks, customer account, quotes, returns, garage, wishlist, and
  print documents.
* Complete vendor/B2B, parts agent, demand intelligence, marketing forms, and
  SEO/sitemap evidence.

### Step 4 — Close operations and deployment modes (4–6 sessions)

* Verify Super CP provisioning and demo lifecycle.
* Test tenant isolation, registration/expiry, on-prem installation, cloud
  synchronization, encrypted backup/restore, offline recovery, and rollback.
* Prove queue retry, webhook replay protection, worker scheduling, and API
  compatibility.

### Step 5 — Acceptance and PHP removal gate (3–4 sessions plus windows)

* Run three combined browser rounds against the same tenant data.
* Capture PHP/ASP.NET screenshots and field/action/report comparisons.
* Deploy only the merged release; verify exact route ownership and health.
* Rehearse rollback and preserve PHP read-only fallback for one release.
* Obtain release-owner approval, then set `CutoverAllowed`.
* Remove PHP/PHP-FPM only after `PhpSourceDeletionAllowed` is explicitly
  approved; otherwise retain the fallback.

## What can delay the bands

The session bands measure implementation throughput, not external waiting time.
They can extend when production credentials, tenant fixtures, payment callbacks,
country-specific compliance data, on-premise hardware, DNS/Nginx windows, or
human visual acceptance are unavailable. Those are gates to resolve, not reasons
to mark a surface complete.

## Current decision

The fastest safe path is **CP evidence → ERP business processes → storefront →
operations → three acceptance rounds**. PHP/PHP-FPM must remain available and
authoritative until the final gate; the current 20.4% weighted completion
headline therefore remains unchanged by route or presentation-only slices.

## Owner-requested ERP/CP professional presentation and tenant customization

The roadmap must retain the following product requirements from the owner
request and must not treat them as optional visual polish:

### Shared professional visual system

* ERP and CP must present as one coherent professional enterprise product:
  aligned grids, consistent spacing, readable hierarchy, responsive desktop and
  mobile layouts, clear empty/loading/error states, and accessible focus and
  contrast behavior.
* Use a controlled multi-colour infographic vocabulary to distinguish healthy,
  active, pending, warning, blocked, financial, inventory, customer, supplier,
  compliance, and industry-specific states. Colours must reinforce labels and
  never be the sole status signal.
* Executive dashboards must use scannable KPI cards, process-flow visuals,
  severity badges, trend/target context, and action links rather than
  unstructured tables alone.
* CP and ERP shells must retain PHP-compatible route, tenant, permission,
  fallback, and same-to-same acceptance boundaries while adopting the shared
  ASP.NET presentation components.

### D365/F&O-style ERP workspaces and forms

* Core ERP entry forms must use a reusable D365/F&O-style document shell:
  command bar/action pane, document identity and status, contextual company and
  industry chips, grouped header fields, dense editable lines, totals/footer,
  validation summary, workflow/status area, audit/source links, and guarded
  action states.
* Shared document UX applies to general ERP tenants and to PO, PI, SO, SI,
  RFQ, delivery, finance voucher, inventory, and reporting workspaces.
* Jewellery, fit-out, and other industry-specific controls remain gated by the
  trusted tenant/company industry context; presentation customization must not
  expose an industry workflow to an ineligible tenant.
* Every live action remains dry-run first and requires the existing
  `confirmWrites=true` boundary. The PHP schema and lifecycle remain
  authoritative until the relevant acceptance evidence passes.

### Tenant-controlled organization and presentation settings

Add a tenant-scoped Organization/Administration Settings module with:

* organization identity, legal entities, branches, locations, fiscal/calendar
  defaults, currency, tax/e-invoice defaults, numbering, document defaults,
  approval limits, roles/capabilities, notifications, integrations, audit and
  retention settings, backup/synchronization status, and industry-pack visibility;
* allowlisted tenant-editable labels, terminology, help text, brand colours,
  logos, fonts, density, radius, dashboard accent, status palette, navigation
  naming, document header/footer, and print/email presentation settings;
* field visibility, required/optional state, ordering, grouping, and layout
  profiles per eligible workspace, with drag-and-drop editing where supported;
* preview, draft, publish, version history, rollback, import/export, and
  reset-to-professional-defaults flows;
* tenant users may change approved presentation metadata through the settings
  UI, while platform-owned backend contracts, route policy, authorization,
  validation, SQL, audit, and industry gates remain controlled by ASP.NET;
* strict allowlists and validation prevent arbitrary HTML/CSS/script injection,
  unsafe route changes, cross-tenant reads/writes, and changes to protected
  fields or lifecycle semantics;
* settings must be tenant/company scoped, auditable, cache-safe, and compatible
  with on-premises synchronization and cloud policy control.

### Required delivery and acceptance gates

1. Define shared design tokens and reusable infographic, KPI, action-pane,
   document-header, form-section, line-grid, status, and settings components.
2. Establish the Organization Settings contract and PHP-compatible persistence
   boundary, beginning with safe presentation tokens and workspace profiles.
3. Apply the system to the ERP executive dashboard and representative PO/PI/SO/SI
   forms before expanding to every module.
4. Verify general ERP tenants retain common modules while industry-specific
   presentation and controls remain gated.
5. Test tenant isolation, RBAC, dry-run/live-write behavior, audit history,
   browser responsiveness, visual comparison against the supplied infographic
   baseline, and rollback/reset behavior.
6. Do not count presentation customization as formal migration acceptance until
   the same tenant data passes PHP-vs-ASP.NET field/action/workflow/database and
   production rollback evidence.
