# ASP.NET Core completion roadmap

**Status:** ERP acceptance remains open (0/15). Since the 2026-10-07 handover (`NOTE_FOR_DEVIN_2026-10-07.md`) Cursor owns ERP as well as BOS, Control Panel, CRM, storefront/frontend, marketing, and tenants. Cursor finishes the non-ERP surfaces first, then takes ERP over from Devin's plan (Step 7). The product objective is enterprise ERP + BOS + CRM, benchmarked against Dynamics 365, SAP, and Oracle Fusion. PHP parity is the minimum. See `ASPNET_MIGRATION_TRACKER.md`.
**Authoritative reference:** the PHP/PHP-FPM application remains the behavioural,
visual, security, deployment, and rollback reference until every exit gate passes. It is not the final enterprise standard.

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

### Step 1 — Complete ERP and prove tenant-wide functionality (ERP exit gate)

ERP is now the mandatory first completion area. Do not advance ordinary CP,
storefront, or marketing parity as the primary workstream until the ERP exit
gate passes. The ERP exit gate requires:

* PHP-vs-ASP.NET route, field, action, report, permission, visual, and UX
  reconciliation for every approved ERP module.
* Functionality tests for each business process, including happy path,
  validation rejection, permission denial, duplicate/retry behavior, audit
  evidence, and persisted database readback.
* Representative rehearsals for every tenant, industry pack, legal entity,
  business unit, branch/site, cost centre, financial dimension, and
  intercompany scope; no tenant may be promoted based only on another tenant's
  fixture.
* Tenant-isolation tests proving direct URLs, posted identifiers, exports,
  reports, attachments, and drill-downs cannot cross the resolved tenant,
  company, site, or financial scope.
* Browser desktop/mobile parity, accessibility, performance, recovery,
  rollback, UAT, and production smoke evidence.
* A guarded tenant-by-tenant ownership switch to the ASP.NET Super ERP system,
  with PHP/PHP-FPM retained as the verified fallback until the final release
  owner sign-off.

The executable acceptance inventory is `ErpTenantAcceptanceCatalog`: it keeps
the B1–B8, Jewellery, and fit-out business scenarios distinct and refuses
tenant promotion unless route parity, functionality, denial behavior, tenant
isolation, persisted readback, browser parity, recovery/rollback, and UAT/
production evidence are all recorded.

The next ERP tranches must therefore be implemented as business-process
functionality and acceptance evidence, not only route or presentation
coverage. After the ERP exit gate, resume the remaining CP and cross-surface
workstreams.

### Step 2 — Close CP evidence (3–5 sessions)

* Generate the complete CP menu matrix for tenant and Super CP hosts.
* Classify each PHP entry as a real twin, digest, redirect, or missing.
* Finish remaining body/write/action/guide pages, including single-item flows.
* Compare the same records in PHP and ASP.NET at desktop and mobile widths.
* Close the CP exit gate only when the matrix has zero digest and zero missing
  entries for the approved menu.

#### ERP business-process workstream (within Step 1; 8–12 sessions)

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

The 2026-10-05 enterprise plan also requires Finance Manager, Accountant, and
Warehouse Manager workspaces on the ERP side, and CEO, CFO, Sales, Purchasing,
Operations, and Management workspaces on the BOS side. Those workspaces are
not accepted. They read ERP. They do not keep a second ledger. No production
evidence for them is recorded in this roadmap.

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

### Step 6 — Platform lifecycle: versions, deployment models, setup, security, exit (owner questions 2026-10-08)

Audited against the code on 2026-10-08. "Exists" means working code; a document describing something does not count.

#### 6.1 Versioned releases applied per tenant

* Exists: one fleet release (`scripts/deploy_aspnet_foundation.sh` builds `/var/www/ecomae-aspnet/releases/<stamp>` and repoints `current`), automatic rollback when health fails, `scripts/rollback_aspnet_foundation.sh`, and per-tenant feature flags (`epc_tenant_feature_flags`, `CpTenantFeaturesWriteService`).
* Missing: every tenant runs the same build. There are no channels and no pinning, so a working tenant cannot stay on version N while others take N+1.
* Build:
  1. A release manifest per build: semantic version, git SHA, schema version, changelog and the minimum and maximum compatible schema.
  2. `epc_tenant_release` in the registry: tenant, channel (`stable` / `early` / `pinned`), pinned version, the applied schema version, the last upgrade and its result.
  3. Several releases side by side under `releases/`, each its own Kestrel instance on its own port. Nginx (or a YARP front) routes each host to its tenant's release. `current` remains the default for `stable`.
  4. Rollout: internal demo tenants, then `early`, then `stable` in batches, with a health and error budget check between batches and a rollback per tenant.
  5. Schema changes become versioned, additive-first migrations recorded per tenant (`epc_migrations` exists in PHP and is extended to every tenant DB). A release refuses to serve a tenant whose schema is outside its range.
  6. Feature flags stay the switch for behaviour inside one version.

#### 6.2 ERP deployment models (cloud, on-premises, hybrid)

* Cloud, active: hosted commerce plus ERP, and ERP-only on the platform host (`hosted_on=platform`, `erp_only_shared=1`, `docs/ECOM-ERP-SHARED-ACCESS.md`).
* On-premises: the PHP pack exists (`deploy/on-premises/`: `install.sh`, Docker compose, `setup-wizard.php`, signed licence activation, `backup.php`, `health-check.php`; tables `epc_onprem_licenses`, `epc_onprem_health_log`). The ASP.NET pack is scaffolds only (`deploy/on-premises-aspnet/`), and `TenantInstallationControlPlane` builds manifests without persisting them.
* Hybrid: **not implemented**. `TenantDeploymentKind` is only `Cloud | OnPremises`, and there is no sync agent and no conflict handling.
* Build:
  1. Add `Hybrid` to the deployment kind and store it per tenant.
  2. The ASP.NET on-prem installer: container images per release, an install wizard, licence activation and health reporting reusing the PHP tables.
  3. A hybrid sync agent: outbound-only HTTPS from the site to the cloud; a change log per table with a cursor and idempotent replay; declared ownership per entity (for example, masters in the cloud and transactions on site); conflicts queued for review, never silently overwritten; works offline and catches up.
  4. A deployment model column and a matrix of features per model in Super CP.

#### 6.3 ERP version and update applicability

* Exists: shared ERP code for all cloud tenants, additive lazy schema (`ErpLazySchema`, PHP `*_ensure_schema`), and row-level optimistic locking (`epc_erp_concurrency.php`). That locking is not a product version.
* Missing: a per-tenant ERP version, release notes shown to tenants, opt-in windows, and update control on premises.
* Build: this follows 6.1. Each tenant shows its "ERP version x.y.z" and "update available" with notes in its CP. Admins choose the window (now / tonight / pinned until a date, capped by the security support policy). On-prem sites pull signed packages from the licence server and apply them with backup first and automatic rollback. Security fixes are mandatory within N days.

#### 6.4 Guided setup with progress and no errors

* Exists: the Super CP onboarding panel and launch checklist (`epc_tenant_onboard_panel.php`, `epc_portal_onboard_client()`, `epc_portal_tenant_launch_checklist()`), demo provisioning that creates a DB user per demo (`epc_portal_demo.php`), the ERP guide (`erp_guide.php`), the on-prem `setup-wizard.php`, and progress stages in `TenantInstallationControlPlane` (in memory only).
* Missing: a durable, resumable ASP.NET provisioning job with preflight checks and rollback on failure.
* Build:
  1. `epc_provision_jobs` and `epc_provision_steps`: each step with status, percentage, log line and an undo action.
  2. Preflight before any write: DNS, free DB name and user, disk, SMTP, licence, industry pack and admin e-mail. The job does not start unless every check is green.
  3. Steps are idempotent and resumable. A failure stops the job, runs the undo of completed steps and shows the reason in plain language.
  4. One progress-bar UI in Super CP (cloud) and in the on-prem wizard, fed by the same job API. The tenant's first-login checklist (company, tax, chart of accounts, users, opening balances) also shows a progress bar.

#### 6.5 Database security controls

* Exists:
  - a dedicated database and DB user per tenant, and the shared-`docpart` fail-closed guard (`epc_tenant_data_guard.php`, `TenantDataGuard.cs`);
  - the protection audit tables (`epc_tdp_audit_log`, `epc_tdp_violations`) and ERP RBAC;
  - session hardening (`epc_session_security.php`);
  - login and OTP rate limits, OTP codes stored hashed, and an operator backup script (`tools/backup/epc_backup.sh`).
* **Gap to fix first**: `epc_portal_tenants.db_password` is stored in plain text. `epc_tenant_data_policy.php` tells customers that these credentials "are stored encrypted in the platform registry". Either the encryption is built (preferred) or the statement is corrected. The owner decides; the legal text is not edited without approval.
* Build:
  1. Encrypt registry DB credentials with a key outside the database (environment or KMS), with key rotation. Dual-read during the switch so PHP and ASP.NET keep working.
  2. Least-privilege DB grants per tenant user; no `GRANT`/`DROP` for application users.
  3. TLS for any non-loopback DB link, and encrypted off-host backups with a tested restore drill and recorded restore times.
  4. A wired global API rate limit.
  5. Audit logging on every privileged CP/BOS action, with a retention period.
  6. Secrets out of `config.php` into the secret store.
  7. A periodic access review report.

#### 6.6 Customer exit: their database, history and files

* Exists: policy and legal text promising export (`epc_tenant_data_policy.php` §9), per-module exports (prices, catalogue/YML, web tracker, parts agent CSV, BOS tenant config export), the document vault (with a `portability` request type that no flow fulfils), operator `mysqldump` and file backups, and ERP data **import** (`epc_erp_data_migration.php`).
* Missing: a complete exit package and its workflow.
* Build: an "Export my data" request in the tenant CP (owner role, re-authentication with an e-mail code), fulfilled by a worker job that produces:
  1. a full SQL dump of the tenant database (tables, data, history);
  2. CSV or Excel per business entity (customers, suppliers, items, orders, invoices, GL journals, payments, stock movements) with a data dictionary;
  3. all uploaded files and the document vault with every version, plus a `manifest.json` with checksums;
  4. an encrypted ZIP, shared through an expiring signed link, with an audit record of who downloaded it and when.
  After the customer confirms receipt: account read-only, then deletion after the contractual period (30 days in the policy), with a deletion certificate. On-prem customers already hold their data, and the same exporter runs locally.

Ordering: 6.5 (credential encryption) and 6.6 (exit package) are compliance promises already published, so they come first after the ERP exit gate. 6.4 and 6.1/6.3 follow, then 6.2 hybrid. None of this changes the 20.4% parity headline: these are platform capabilities beyond PHP parity.

### Step 7 — ERP intake: study Devin's ERP plan before any ERP code (owner request 2026-10-08)

When the non-ERP surfaces are done and ERP work starts, the first session reads Devin's plan and its later updates in full and reconciles them with this roadmap. No ERP code is written until that reconciliation is committed. Devin's ERP PRs are merged on `main` through #2026. Cursor's ERP fixes (#2030) are merged on top.

Read, in this order:

1. `ERP_COMPLETION_DIRECTIVE.md`. It sets:
   * the wave order: foundation, Order-to-Cash, Procure-to-Pay, Inventory/WMS, Finance, then the rest with Jewellery and Fit-Out;
   * the per-process definition of done, then LOCK;
   * the completion board of 15 processes, 0 accepted;
   * the scope freeze, the session output format and the owner blockers.
2. `ERP_OWNERSHIP_AUDIT_2026-10-07.md` and `NOTE_FOR_DEVIN_2026-10-07.md`, which hold the open accounting findings:
   1. Cash entries without a journal.
   2. An integrity scan without unbalanced-journal or sub-ledger-to-control-account checks.
   3. POS sales outside the ledger and stock.
   4. Voucher gaps after failed writes.
   5. Eight untested write services.
   6. Floating-point money in four services.
3. `TENANT_BY_TENANT_ERP_MIGRATION_GATE.md` and `evidence/tenant-by-tenant-erp-migration-gate.json`: every tenant needs its own evidence bundle.
4. The rehearsal evidence: 25 `*-cycle-*.md` notes in `evidence/write-dryruns/`, 3 more in `aspnet/docs/migration/evidence/write-dryruns/`, `erp-ajax-case-coverage-2026-10-05.md` (all 321 `ajax_erp.php` cases), and the B1–B8 recovery, rollback, UAT and tax runbooks in `evidence/decommission/`.
5. The "Enterprise platform benchmark" matrix in `ASPNET_MIGRATION_TRACKER.md` (Dynamics 365, SAP, Oracle; every row not accepted).

Devin work that never reached `main` (checked 2026-10-08 against 603 `devin/*` branches: 93 of the 119 with commits beyond `main` are squash-merged under the same title, and the other 26 were read):

* `devin/1790743008-tenant-install-control-plane`, commit `4139c40c4`, added after #1733 merged. It changes `TenantInstallationControlPlane` in three ways:
  * the server generates the enrollment request id, instead of accepting one from the caller;
  * tenant keys are restricted to ASCII `a-z 0-9 - _ .`, because `char.IsLetterOrDigit` lets any Unicode letter through today;
  * cloud tenants get a direct `ProvisioningCloudTenant` to `Synchronizing` step.

  This is security hardening. Port it with tests at the start of 6.4, or earlier if a security fix is needed.
* `devin/1790970551-b8-acceptance-audit`, which has evidence edits after #1923 merged (B8 physical tenant isolation rehearsal). Compare them with the merged B8 evidence and keep only what is still true.
* Open PR #8 (`devin/1782038638-missing-pr4-modules`, June). It holds four PHP modules that are not on `main`: bank reconciliation, intercompany, Syncron policy, and PIM custom fields, plus a `epc-erp-d365-modules-setup.php` installer. Bank reconciliation and intercompany exist on `main` in other forms; Syncron policy and PIM custom fields do not. They are not PHP reference behaviour, because they never shipped, so they do not count toward the parity ratios.
  * Owner decision, 2026-10-08: **keep Syncron policy and PIM custom fields.** Build both natively on ASP.NET as accepted enhancements, using the PR #8 code as the behaviour reference and its table names, so a later PHP deploy of the same module reads the same data:
    * PIM custom fields: `epc_pim_fields`, `epc_pim_field_options`, `epc_pim_item_values`. The fields can be shown per module (inventory, sales, purchase). A "PIM attributes" tab on `/erp/product-info-app` manages fields and options, the create-item forms render the inventory fields, and the item view shows the saved values.
    * Syncron policy: `epc_erp_inv_policies`, `epc_erp_inv_demand_forecast`, `epc_erp_inv_service_levels`. Global, category and item policies; moving-average or exponential daily demand from `epc_erp_inv_movements`; safety stock, reorder point and the stockout/reorder/overstock recommendation; a 30-day forecast run; the monthly service-level report. All of it is on `/erp/syncron-app`.
    * Fixes on top of the PR #8 code: category-scoped policies are matched (PR #8 sorted on category but never selected it); a field code that is empty or already taken gets a unique code, instead of failing on the unique key; number and date values are validated before they are saved; money and quantity arithmetic uses `decimal`.
    * The bank reconciliation and intercompany modules from PR #8 stay unported, because `main` already has its own versions. The `epc-erp-d365-modules-setup.php` installer is not needed, because ASP.NET creates the tables on first use.
* The other 23 are June–July PHP-era fixes, skill-file updates, or merge commits that later `main` work replaced. No action.

The reconciliation commit then:

* re-derives the ERP percentages from the artifacts;
* updates the board's owner from Devin to Cursor;
* orders the audit findings into the Wave 1 and Wave 5 work;
* lists the owner blockers that are still open: a second legitimate tenant DB for isolation, a production backup and restore window, human visual acceptance, and country compliance data.

## What can delay the bands

The session bands measure implementation throughput, not external waiting time.
They can extend when production credentials, tenant fixtures, payment callbacks,
country-specific compliance data, on-premise hardware, DNS/Nginx windows, or
human visual acceptance are unavailable. Those are gates to resolve, not reasons
to mark a surface complete.

## Current decision

The official execution sequence is **ERP → ERP acceptance → remaining CP /
cross-surface → storefront → operations → final cutover** (see
`ERP_COMPLETION_DIRECTIVE.md`). ERP is worked by complete business process, not
by route count, and non-essential scope is frozen until the ERP exit gate passes. PHP/PHP-FPM must remain available and
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
