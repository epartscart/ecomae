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
| ERP | Top-menu and dispatcher coverage are broad; several accounting, tax, invoice, fit-out, jewellery, IFRS, and presentation slices are real, but most PHP module bodies and workflows remain partial. | **55–65%** | Finish B1–B14; close OMS/P2P/inventory/GL/treasury/tax/report drill-down gaps; complete jewellery and fit-out scenarios; validate writes against throwaway tenant DBs. | 8–12 Devin sessions |
| Storefront / frontend | ASP.NET route and digest shadows exist for home, catalogue, cart, checkout, account, returns, and industry surfaces; product-host ownership and rich workflows remain PHP-primary. | **80–90%** | Theme/asset parity; search and vehicle flows; guest order and payment callbacks; customer/vendor/B2B workflows; SEO/sitemap; tenant-host dual samples. | 6–9 Devin sessions |
| Marketing / LifeOS / IP | Marketing page family and `/marketing/app` scaffold exist, but forms, brand-host routing, SEO, sitemap, and production shadow installation remain open. | **75–85%** | Contact/demo/newsletter writes, sitemap/robots/canonical parity, host-by-host rendering, rate limits, and shadow deployment. | 3–5 Devin sessions |
| BOS / Super-CP fleet / tenant hub | Fleet/readiness and operator shells exist; provisioning and operator write workflows are not yet accepted as PHP twins. | **65–75%** | Provision/extend/expire/restore demo tenants; operator audit; tenant registry writes; failure/retry states; production smoke evidence. | 3–5 Devin sessions |
| APIs / webhooks / workers | Dispatcher and catalog contracts are extensive, but operational parity is not proven for every callback, queue, retry, upload/download, cron, or scheduled compliance job. | **70–80%** | Enumerate every PHP API/cron; prove idempotency, retry, dead-letter, auth/rate limits, webhook signatures, and tenant scope. | 3–5 Devin sessions |
| Cloud / on-premises / hybrid operations | Health checks and zero-downtime foundations exist; install, licensing, registration/expiry, synchronization, encrypted backup/restore, and disaster recovery are open gates. | **80–90%** | Produce cloud, on-prem, and hybrid runbooks; test offline/on-prem registration expiry; encrypted backup/restore; sync conflict handling; rollback. | 4–6 Devin sessions plus environment waits |
| Security / tenant isolation | RBAC, scoped grants, audit history, CSRF, and company/site policies have substantial coverage, but the module-by-module denial matrix is not closed. | **35–45%** | Negative tests for every write and direct URL; cross-tenant leakage tests; country/effective-date checks; rate-limit and secret-handling review. | 3–4 Devin sessions |
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

Each process requires New/Edit/Delete/Void/Submit/Approve/Post where applicable,
field validation, audit, permission denial, source-document links, and database
corroboration.

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
