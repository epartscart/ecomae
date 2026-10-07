# ASP.NET Core migration tracker (single source of pending work)

PHP reference (`/php-reference/*`) is the minimum specification for layout, colours, controls, tables, menus,
permissions, guides, and behaviour. Matching that reference is required. It is not the final standard.
ECOM AE is an enterprise ERP + BOS + CRM platform, benchmarked functionally and visually against
Dynamics 365 Finance & Operations / Sales, SAP S/4HANA / Sales Cloud, and Oracle Fusion ERP / SCM.
A module is not complete because its routes exist, and a PHP twin is not enterprise acceptance.
Digest/read-only shells do not count. The weighted percentages below measure migration gates only.
They are not an accepted-process count. ERP accepted processes remain **0/15**
(`ERP_COMPLETION_DIRECTIVE.md`).

## Enterprise platform objective and ownership

Recorded 2026-10-05 from `origin/main` `3ebfe31fa` (#2026). This section is the
plan. It does not implement features, and it does not claim ERP or BOS is complete.

Devin owns ERP, the transactional system of record. Cursor owns BOS, Control
Panel, CRM, storefront/frontend, marketing, and tenants. BOS and CRM are
experiences over shared ERP domain services, APIs, and workflows. One
transaction has one source of truth, one authorization model, one workflow
engine, one audit trail, one accounting result, and one API contract. Screens
may differ by role. Business logic is not copied into a second engine.

Cursor's in-flight PHP-parity baseline is draft PR #1970,
`cursor/cp-frontend-parity-4911`, tip
`08373de0f5840065d53d150436b680a915ab56be` (previously noted as `22df603f9`;
the tip can move). That branch stays the storefront and Control Panel minimum.
It is not rebased by this plan, and it is not enterprise completion.

BOS becomes a business-operations control tower: CRM, Customer 360, Supplier
360, executive dashboards, approvals, tasks, exceptions, KPIs, notifications,
analytics, cross-company visibility, and AI recommendations. CRM follows
Dynamics 365 Sales / SAP Sales Cloud from lead through qualification, account
and contact, opportunity, activity, quotation, and approval, then hands the
order, fulfillment, invoice, and collection to ERP. Control Panel stays the
control plane: tenant configuration, users, roles, permissions, subscriptions,
deployment, monitoring, localization, and platform admin.

Role workspaces still to accept: ERP — CFO, Finance Manager, Accountant, Sales
Manager, Purchasing Manager, Warehouse Manager (Devin); BOS — CEO, CFO, Sales,
Purchasing, Operations, Management (Cursor). The completion roadmap already
records a PHP-shaped ERP dashboard and a role-preview catalogue, and it still
lists CFO, CEO, Sales Manager, and Purchasing Manager acceptance as open.

### How to read a matrix row

`ECOM AE current` uses the completion board and the tracker checkboxes. It is
not a new measurement. `Production evidence` is `None recorded` unless a
production bundle already exists in the repo. Throwaway MariaDB rehearsals are
named where they exist and are not production evidence. `Acceptance status` is
`Not accepted` on every seeded row: the completion board accepted column is
`No` for all 15 processes, and `AspNetInteractiveCompleteCount` stays 0.
`docs/migration/evidence/presentation/MODULE_FUNCTION_PARITY_STATUS_LIVE.md`
still records interactive complete 0 for CP, ERP, BOS, and storefront.
Throwaway notes cited by filename alone are under
`docs/migration/evidence/write-dryruns/`. Paths that start with `aspnet/docs/`
are stored there instead. None of them are production evidence.

The #1987 dry-run list is corrected in `ERP_COMPLETION_DIRECTIVE.md`. In short:
e-invoice create/poll/seller-sync, document and logo/attachment writes,
customs save/submit/delete and the declaration PDF parser, and jewellery seed
now have live `confirmWrites` twins. The ajax-writes catch-all and the six
on-premises CLI rows stay dry by design. Credit-note XML/ASP submission of the
credit note is not in the throwaway credit-note rehearsal. Payroll pay posts
cash; COA GL posting stays PHP.

## Enterprise platform benchmark

Seeded from the process chains and BOS/CRM capabilities above. Recommended
enhancements are planning only.

| Capability | ECOM AE current | PHP reference | ASP.NET implementation | Dynamics 365 | SAP | Oracle | Functional gap | UX gap | Control / security gap | Recommended enhancement | Owner | Priority | Production evidence | Acceptance status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Order-to-Cash | Board B2: functional 85%, UI/UX 65%, reports 40%, writes 85%, testing 45%. Not accepted. | PHP sales order, delivery, invoice, fulfilment, and revenue chain (`epc_erp_order_fulfillment` and related libs). | Sales-order lifecycle writes exist. Throwaway consolidated O2C+P2P cycle and delivery-note cycle. Fulfilment dashboard parity still open in Phase B2. | Finance and Supply Chain order-to-cash: sales order, packing slip, invoice, collection. | S/4HANA Sales (SD) order-to-cash plus FI-AR. | Fusion Order Management through Receivables. | Isolation, rollback, UAT, and production evidence are open. Route coverage is not the process. | No accepted sales-order workspace against the D365/SAP/Oracle bar. | Tenant, company, and site isolation is not accepted. One order engine only. | Close the B2 gates on the shared ERP document workspace. | Devin | P0 | None recorded. Throwaway only: `docs/migration/evidence/write-dryruns/erp-consolidated-cycle-2026-10-05.md`, `erp-delivery-note-cycle-2026-10-05.md`. | Not accepted |
| Procure-to-Pay | Board B3: functional 85%, UI/UX 70%, reports 45%, writes 85%, testing 70%. Not accepted. | PHP requisition, RFQ, PO, receipt, invoice, match, and payment chain. | Guarded PR/PO/RFQ/receipt/invoice/match writes and a supplier scorecard read. Payment approval/export still pending in Phase B3. | Supply Chain procurement: requisition, RFQ, PO, product receipt, vendor invoice, matching. | S/4HANA sourcing and procurement through MM and FI-AP. | Fusion Procurement through Payables. | Payment approval/export and acceptance gates are open. | Supplier and buyer workspaces are not accepted. | Same authorization and audit trail as the ERP purchase document. No second PO engine. | Finish the P2P chain, including reversal, on ERP services. | Devin | P0 | None recorded. Throwaway only: `erp-po-lifecycle-cycle-2026-10-05.md`, `erp-rfq-cycle-2026-10-05.md`, `aspnet/docs/migration/evidence/write-dryruns/erp-landed-cost-cycle-2026-10-05.md`. | Not accepted |
| Inventory / WMS | Board B4: functional 75%, UI/UX 65%, reports 40%, writes 70%, testing 55%. Not accepted. | PHP inventory, warehouse, movement, transfer, quality, and MRP screens. | Receipt/issue and WMS transfer rehearsed on a throwaway DB. Phase B4 lists further WMS and MRP writes as in progress. | Warehouse management: receipt, put-away, wave, transfer, cycle count, reservation. | Extended Warehouse Management plus MM inventory. | Fusion Inventory and Warehouse Management. | Acceptance gates and remaining warehouse workflows are open. | No accepted warehouse-manager workspace. | Site and warehouse isolation is not accepted. | Close inventory and WMS as one ERP process, including reversal and audit. | Devin | P0 | None recorded. Throwaway only: `erp-inventory-cycle-2026-10-05.md`, `erp-wms-transfer-cycle-2026-10-05.md`. | Not accepted |
| Record-to-Report | Board B5 functional 75% / testing 25%; B6 reports 55% and testing 0%. Neither accepted. | PHP GL, COA, journals, period close, and IFRS pack. | Manual journal, opening batch, period lock/reopen, and a PHP-shaped IFRS pack exist. Drill-down, filing, and exact report layout remain pending in Phase B5/B6. | Finance general ledger, financial reporting, and period close. | S/4HANA Finance (FI) plus group reporting. | Fusion General Ledger and Financial Reporting. | IFRS fixture, isolation, rollback, UAT, and production evidence are open. | Finance workspaces and report layout are not accepted. | Period lock, reversal, and audit must stay on the ERP ledger. | Close journal-to-statement on one ledger, including reversal and consolidation evidence. | Devin | P0 | None recorded. Throwaway only: `erp-period-close-cycle-2026-10-05.md`, `erp-opening-batch-cycle-2026-10-05.md`. | Not accepted |
| Treasury | Board B7: functional 60%, UI/UX 55%, reports 30%, writes 60%, testing 30%. Not accepted. | PHP cash, bank, petty cash, payment batch, and reconciliation. | Cash entry, bank match, petty cash, payment batch, and bank import/reconcile have throwaway notes. Credit, settlement, and broader bank-report parity remain pending in Phase B7. | Cash and bank management inside Finance. | S/4HANA Treasury and cash management. | Fusion Cash Management. | Acceptance gates and remaining bank/report parity are open. | No accepted treasury workspace. | Payment release and bank files stay on the ERP authorization model. | Finish cash, bank, and payment release as one ERP process. | Devin | P0 | None recorded. Throwaway only: `erp-treasury-cycle-2026-10-05.md`, `erp-treasury-bank-cycle-2026-10-05.md`, `erp-cash-treasury-cycle-2026-10-05.md`. | Not accepted |
| Tax / compliance | Board B8: functional 75%, UI/UX 60%, reports 40%, writes 70%, testing 15%. Not accepted. | PHP VAT, corporate tax, e-invoice, and country profiles. UAE/FTA is one profile, not the only one. | VAT return/refund, checklist, and manual e-invoice paths exist. Create, poll, and seller sync are live `confirmWrites` twins. Credit-note document create was rehearsed; credit-note XML/ASP submission was not. Full filing, Peppol polling, and non-UAE profiles remain pending in Phase B8. | Finance tax and electronic invoicing. | S/4HANA document and reporting compliance. | Fusion Tax. | Country profiles beyond the rehearsed paths, external authority evidence, and acceptance gates are open. | No accepted tax workspace. | Statutory calculation stays in ERP and is tenant-country effective-dated. | Close tax and e-invoice, including credit-note XML/ASP, on the ERP profile. | Devin | P0 | None recorded. Throwaway only: `erp-credit-note-cycle-2026-10-05.md`, `aspnet/docs/migration/evidence/write-dryruns/erp-vat-refund-cycle-2026-10-05.md`. | Not accepted |
| Fixed assets | Board: functional 40%, UI/UX 30%, reports 10%, writes 40%, testing 15%. Not accepted. | PHP fixed-asset master, depreciation, and disposal. | A depreciation cycle was rehearsed on a throwaway DB. The board percentages stay in this band. | Fixed assets inside Finance. | S/4HANA Asset Accounting. | Fusion Assets. | Acquisition, transfer, disposal, reporting, and acceptance gates are open. | No accepted asset workspace. | Asset posting uses the ERP ledger only. | Complete the asset lifecycle and its journals on ERP. | Devin | P0 | None recorded. Throwaway only: `erp-fixed-asset-cycle-2026-10-05.md`. | Not accepted |
| HR / payroll | Board: functional 40%, UI/UX 40%, reports 20%, writes 35%, testing 20%. Phase B9 is still unchecked. Not accepted. | PHP staff, payroll, and WPS for the UAE profile. | Generate, approve, and pay posts a cash entry on a throwaway DB. COA GL posting stays PHP. | Human Resources and payroll integration to Finance. | S/4HANA HCM / payroll posting to Finance. | Fusion HCM posting to General Ledger. | Payroll GL, labour-law profiles, and acceptance gates are open. | No accepted HR workspace. | Payroll posting must hit the ERP ledger once. | Add payroll GL on the ERP ledger and close the HR process. | Devin | P1 | None recorded. Throwaway cash cycle only: `erp-payroll-cycle-2026-10-05.md`. GL posting was not rehearsed. | Not accepted |
| Production | Phase B11 is unchecked. No completion-board row and no cycle note. Not accepted. | PHP manufacturing, planning, quality, product structure, and costing tabs. | Not closed. Do not infer a percentage. | Supply Chain manufacturing and production control. | S/4HANA production planning and manufacturing. | Fusion Manufacturing. | The process chain is not accepted. | No accepted production workspace. | Material issues and receipts post through ERP inventory and the ledger. | Build the production process on ERP inventory, costing, and GL. | Devin | P1 | None recorded. | Not accepted |
| Retail | Phase B12 is unchecked. No completion-board row. Not accepted. | PHP retail, POS, and commerce integration. | POS and card-reader twins are not an accepted process. Phase A marks some CP POS twins done; that is not acceptance. | Commerce and point of sale into Finance. | S/4HANA retail / POS into SD and FI. | Fusion Retail and order capture. | POS-to-ledger, returns, and acceptance gates are open. | No accepted retail workspace. | A POS sale is an ERP transaction, not a second sales engine. | Close retail through the ERP order, inventory, and ledger. | Devin | P1 | None recorded. | Not accepted |
| Service | Phase B10 is unchecked. Not accepted. | PHP contracts, tickets, SLA, warranty, RMA, and maintenance. | An after-sales throwaway cycle exists. The phase checkbox is still open. | Customer Service cases, service orders, and agreements. | S/4HANA Service. | Fusion Service. | Warranty, SLA, and acceptance gates are open. | No accepted service workspace. | Service cost and billing post through ERP. | Close service on ERP contracts, inventory, and billing. | Devin | P1 | None recorded. Throwaway only: `erp-aftersales-cycle-2026-10-05.md`. | Not accepted |
| Jewellery | Board BJ: functional 70%, UI/UX 60%, reports 40%, writes 60%, testing 25%. Not accepted. | PHP `jw_*` pack (39 tabs) plus the INDUS field study. INDUS formulas and posting lineage stay open. | Guarded jewellery routes and a live sample-seed twin (#1993). Full 39-tab workflow, second-database isolation, rollback, UAT, and production gates remain open in Phase B-J. | No single D365 jewellery suite. Benchmark is the same document, inventory, POS, and ledger behaviour as Finance and Commerce, with metal weight and purity. | No single S/4HANA jewellery suite. Same SD/MM/FI bar, plus weight and assay. | No single Fusion jewellery suite. Same order, inventory, and costing bar, plus weight and assay. | Acceptance gates and the remaining jewellery workflow are open. | No accepted jewellery workspace. | Jewellery documents use ERP numbering, tax, stock, and GL. | Finish the jewellery process on shared ERP documents. | Devin | P1 | None recorded. | Not accepted |
| Fit-out | Board BF: functional 65%, UI/UX 60%, reports 45%, writes 55%, testing 0% (no fixture). Not accepted. | PHP project budget, transaction, and recognition. The BOQ, variation, progress-claim, and subcontract chain is a new design from `fitout.txt`, not a finished PHP module. | Budget, transaction, recognition, and estimate/BOQ header writes exist. Phase-1 of the 32-step catalog remains pending in Phase B-F. | Project Operations and Finance project accounting. | S/4HANA project system and commercial project management. | Fusion Project Management and costing. | No fixture, and the contracting chain is not accepted. | CEO, PM, finance, and procurement dashboards named in the spec are not accepted. | Estimates, commitments, and claims post through ERP projects, procurement, and GL. | Close fit-out on the shared project and procurement ledger. | Devin | P1 | None recorded. | Not accepted |
| ERP finance and operations role workspaces | Completion roadmap: a PHP-shaped ERP dashboard and role-preview catalogue exist. CFO, CEO, Sales Manager, and Purchasing Manager acceptance is still open there. Finance Manager, Accountant, and Warehouse Manager have no acceptance evidence in that roadmap. | PHP ERP home and menu, filtered by role. | Shared D365-style document workspaces are in progress on several documents. Not a closed role-workspace set. | Finance and Operations workspaces for CFO, finance manager, accountant, sales, purchasing, and warehouse. | Fiori role spaces for the same duties. | Fusion role-based infolets and springboards. | Workspaces are not an accepted process. | Side-by-side visual acceptance against PHP and the three suites is open. | Each workspace reads the caller's tenant, company, site, and permissions. | Build the six ERP workspaces on ERP queries. Do not give them a private ledger. | Devin | P0 | None recorded. | Not accepted |
| CRM lead-to-approval pipeline | Board CRM: functional 65%, UI/UX 60%, reports 30%, writes 55%, testing 0%. Not accepted. Phase A item "CRM enterprise (#1511)" checked does not accept the process. | PHP CRM leads, opportunities, activities, quotes, and tickets. | CP CRM write routes exist as twins or dry-runs. No accepted pipeline, stages, probability, expected close, quota, forecast category, win/loss, or forecast. | Dynamics 365 Sales: lead, opportunity, activity, quote, forecast. | SAP Sales Cloud pipeline, activities, and forecast. | Fusion Sales and CX pipeline (benchmark peer for the sales motion; the named Oracle bar for this plan is Fusion ERP/SCM, with sales following the same lead-to-order handoff). | Pipeline, forecast, and activity timeline are not accepted. Testing band is 0%. | No accepted sales workspace. | CRM must not insert orders, invoices, or journals. Approval uses the shared workflow engine. | Build the pipeline on shared customers, quotes, and approvals. Hand off to ERP at order. | Cursor | P1 | None recorded. | Not accepted |
| CRM handoff into order, fulfillment, invoice, and collection | The ERP Order-to-Cash row is the transaction. CRM testing on the board is 0%. | PHP quote-to-order and fulfilment libs. | ERP owns the order and invoice writes. CRM must call them. | Sales hands the won opportunity to Finance and Supply Chain. | Sales Cloud hands the order to S/4HANA SD. | Order Management and Receivables own the transaction. | The handoff is not an accepted process. | The seller's timeline and the accountant's document must show one transaction. | One order, one invoice, one collection, one GL result. | Cursor builds the CRM handoff. Devin owns the ERP documents it creates. | Cursor for the handoff; Devin for the ERP documents | P0 | None recorded. See the Order-to-Cash row for throwaway ERP cycles. | Not accepted |
| Customer 360 | No accepted customer workspace. Super CP customer board is a read/search twin, not a 360. | PHP customer, CRM, order, and balance screens, separate menus. | Those screens are not assembled into one accepted customer record. | Customer summary across sales, service, and receivables. | Customer 360 across sales and finance. | Customer hub across order and receivables. | Orders, AR, activities, and service are not one accepted view. | No accepted customer workspace. | The view is read-scoped to the tenant and company. Writes go to ERP or the shared CRM records. | Compose Customer 360 from ERP and CRM APIs. | Cursor | P1 | None recorded. | Not accepted |
| Supplier 360 | No accepted supplier workspace. Phase B3 has a PHP-computed supplier scorecard read, still inside an open P2P process. | PHP supplier, RFQ, PO, and payable screens. | Scorecard fields exist. The 360 is not accepted. | Vendor summary across procurement and payables. | Supplier 360 across MM and FI-AP. | Supplier hub across procurement and payables. | Purchases, receipts, bills, and performance are not one accepted view. | No accepted buyer workspace. | Writes stay on ERP purchase and AP documents. | Compose Supplier 360 from ERP procurement and AP. | Cursor | P1 | None recorded. | Not accepted |
| BOS executive dashboards, tasks, exceptions, KPIs, notifications, analytics | BOS interactive complete is 0. 231 BOS ajax dry-run goldens, `writes=0`, in the 2026-09-29 progress report. Phase C6 is unchecked. | PHP BOS shell, fleet, and operator menus (`bos/BOS_OPERATOR_GUIDE.md` describes the PHP shell). | ASP.NET BOS previews and fleet digests exist. Provisioning writes are not accepted. | Finance and operations workspaces plus Power BI. | Fiori overview pages and SAC. | Fusion infolets and analytics. | The control tower is not accepted. Dry-run catalogs are not operations evidence. | BOS is still an admin menu plus previews, not a control tower. | Notifications and tasks must use the shared workflow and audit trail. | Build the control tower on ERP and CRM queries. | Cursor | P1 | None recorded. | Not accepted |
| Approvals | ERP and CP have approval routes in progress. None are an accepted cross-company approval inbox. | PHP workflow and approval screens. | Shared workflow writes exist in places and are not a closed engine. | Work items and workflow in Finance and Supply Chain. | Flexible workflow / My Inbox. | BPM worklist. | One approval inbox with threshold rules is not accepted. | Role inboxes are not accepted. | One workflow engine. BOS displays it and must not store a second decision. | Expose the ERP workflow inbox in BOS. | Cursor for the inbox; Devin for the workflow engine | P0 | None recorded. | Not accepted |
| Cross-company visibility | D365 structure audit: intercompany flows exist and the due-to/due-from, currency, tax, and elimination evidence is not proven. Not accepted. | PHP intercompany finance modules. | Partial ASP.NET intercompany persistence. The audit status remains partial parity, PHP authoritative. | Cross-company worksheets and elimination in Finance. | Intercompany in S/4HANA Finance. | Fusion intercompany and balancing. | Elimination evidence and a management view are both open. | No accepted multi-company workspace. | A user sees only authorized companies. Elimination posts in ERP. | Devin closes intercompany accounting. Cursor shows the authorized companies. | Devin for posting; Cursor for the view | P0 | None recorded. | Not accepted |
| AI recommendations | Not accepted. Architecture law: Python is AI-only and must not own the transaction. | PHP pages that link to an assistant. Not a forecasting system of record. | AI sidecar scaffold keeps business writes off. | Embedded insights and customer intent. Not a second ledger. | Joule / analytics recommendations beside S/4HANA. | Fusion AI apps beside ERP. | No accepted recommendation with an audit of what the user did next. | No accepted insight panel. | A recommendation may not post, approve, or change permissions by itself. | Cursor surfaces recommendations from the Python sidecar. ERP remains the system of record. | Cursor | P2 | None recorded. | Not accepted |
| Control Panel control plane | Phase A checklist is 24/24 items and about 99% of that phase's weight. Formal interactive acceptance is still zero. CP exit (0 digest / 0 missing, plus the browser round) is open. | PHP Control Panel menus, users, groups, settings, and Super CP. | Broad ASP.NET twins. Generated menu matrix, presentation diff, and tenant/Super-CP acceptance are open. | Not the D365 product shell. The bar is a control plane that configures the ERP tenant. | Not the Fiori shell. Same control-plane bar. | Not Fusion Setup alone. Same control-plane bar. | Menu-to-page matrix, write parity, and acceptance are open. | Presentation review against PHP is open. | CP configures tenant, user, role, permission, and subscription. It does not post the ledger. | Finish CP as the control plane on the shared identity and tenant services. | Cursor | P0 | None recorded. | Not accepted |
| Storefront, marketing, and tenants | Phase C is 0/8. Interactive storefront complete is 0. Marketing scaffold exists; forms and production shadow are open in the progress report. | PHP storefront, marketing site, and tenant hosts. | PR #1970 is the current PHP-parity baseline, not a closed phase. | Commerce storefront into Finance and Supply Chain. | Commerce into S/4HANA SD. | Fusion order capture into Order Management. | Checkout, payment, tenant host, and marketing acceptance are open. | Same-to-same visual acceptance is open. | A storefront order is an ERP order. Tenant data stays in that tenant. | Continue PHP parity, then meet the commerce benchmark through ERP. | Cursor | P1 | None recorded. | Not accepted |
| Single system of record | Required by this plan and by `PROJECT_ARCHITECTURE_INSTRUCTIONS.md`. Not accepted as demonstrated. | PHP already mixes surfaces on one database per tenant. | ASP.NET modules exist. A written guarantee that every BOS/CRM write reuses ERP services is not an accepted test. | One dataverse / finance book for the legal entity. | One universal journal. | One Fusion ledger. | Duplicate engines are forbidden and not yet proven absent at acceptance. | Role UX may differ. | One authorization model, one workflow, one audit trail, one accounting result, one API contract. | Keep new BOS and CRM work on ERP contracts. Reject a second posting path in review. | Devin for the transaction; Cursor for every other surface | P0 | None recorded. | Not accepted |

Update this file in every PR that moves an item. Status legend: `[x]` done (PR merged or open), `[~]` in progress,
`[ ]` pending. Gates (`MigrationGates`) stay closed until the test rounds pass:
`ReadyForPhpRemoval=false`, `PhpSourceDeletionAllowed=false`, `CutoverAllowed=false`, `AspNetInteractiveCompleteCount=0`.

Order of work (earlier user sequence, still the migration-gate order): **CP build → ERP build → storefront/others build → one combined test of all → regression → gates.**
From 2026-10-05, Devin and Cursor work in parallel on the ownership split in "Enterprise platform objective and ownership". That split does not close a phase gate and does not change the 20.4% headline.

Progress measurement (reported to the user on every completed step): `done / total` checklist items per phase and overall,
plus pending %. Weighting: Phase A 20 %, B 45 % (B-J 8 %, B-F 7 % inside), C 15 %, D 8 %, E 7 %, F 5 %.
Current: A 24/24 items (≈99 %) · B 0/21 · C 0/8 · D 0/6 · E 0/4 · F 0/2 → **overall ≈ 20.4 % done / 79.6 % pending**. The 2026-09-29 PHP screenshot audit confirms that route/catalog and shared-shell progress must not be counted as interactive or visual ERP completion.
The headline remains unchanged when a slice improves inside an open weighted phase: this
tracker intentionally counts only closed phase gates, not partial-field or route evidence.
Current measured sub-slices that do not yet close a phase are: print-template editor
`26/26` allowlisted fields exposed in the edit UI (full HTML/CSS bodies and merge-field guidance),
CP/ERP static destination audits `34/34`,
`11/11`, `95/95`, and `224/224`, and focused print-designer verification `7/7`.
These figures are reported separately so the headline cannot overstate migration completion.

### Cross-surface audit checkpoint

The all-surface review is now explicit: CP and frontend route inventories are
not completion evidence. The current implementation inventory includes 331
ASP.NET presentation pages, while the PHP primary CP/ERP trees contain 609 CP
module files and 188 files under the primary ERP module tree. The difference
is not a one-to-one
defect count because PHP files include shared fragments and handlers, but it
confirms that a route/page count cannot replace a PHP field/action/workflow
matrix.

The remaining audit sequence is:

1. CP: generated menu → page/body → write/action → permission/audit →
   desktop/mobile presentation → tenant and Super-CP browser evidence.
2. Storefront/frontend: theme/assets → catalogue/search → cart/checkout/payment
   callbacks → customer/vendor workflows → SEO/sitemap → tenant-host evidence.
3. Marketing/demo/BOS: forms and provisioning writes, expiry/restore,
   operator workflows, brand-host routing, and visual comparison.
4. Tenant CP/ERP plus API/workers: isolation, country profile, sync, queue
   retry, backup/restore, rollback, and on-prem/hybrid registration evidence.

Until each sequence has same-tenant PHP/ASP.NET dual samples and human
acceptance, the phase headline remains **24/24 Phase A checklist items,
20.4% weighted done, 79.6% pending**; formal interactive acceptance remains
zero even where route and dry-run catalog coverage is high.

See `docs/migration/ASP_NET_COMPLETION_ROADMAP.md` for the area-by-area
pending-work bands, ordered execution steps, and session-based planning
estimates. Those estimates are not acceptance percentages and do not authorize
PHP/PHP-FPM removal.

## Production reconciliation and remaining completion gates

The production state must be reported separately from repository implementation
progress. The merged ASP.NET releases are not automatically live: each release
still requires deployment from authoritative `main`, service restart, `/health`
and `/ready` checks, `/migration/release` evidence, exact-route probes, and
rollback verification. The login bridge is operational, but the latest merged
permission-scope persistence release still requires this deployment evidence.

| Production surface | Current authority | What remains before ASP.NET ownership |
|---|---|---|
| Public frontend / marketing | PHP-primary on the live product host; ASP.NET marketing is a shadow/preview path | Install and dual-sample `/marketing/app`, complete contact/demo forms, SEO/sitemap parity, brand-host probes, and human same-to-same approval |
| Super CP | ASP.NET twins and guarded exact-route previews exist; PHP remains the live fallback for uncovered product chrome and writes | Finish the generated menu-to-page matrix, remove digest/missing pages, complete write parity and operator-guide acceptance, then run the CP browser round |
| Platform ERP | ASP.NET login/workspaces and selected write paths are live in the migration branch; PHP remains reference/fallback | Close the remaining PHP tab/action families, rebuild module-specific page bodies to the attached PHP visual/structural evidence, wire persisted scoped grants into effective sessions and every write/approval/export path, complete CT/VAT/e-invoice/IFRS/external-reporting comparisons, and run the CP+ERP browser round |
| Tenant CP / tenant ERP | Tenant product chrome remains PHP-primary by design | Prove tenant isolation, company/site scope, country profiles, direct-URL denial, tenant-host same-to-same dual samples, rollback, and explicit host-by-host shadow approval |
| Demo / industry hosts | ASP.NET fixture/catalog coverage exists; live provisioning and expiry remain guarded | Verify every demo/industry host against PHP presentation and isolated data, exercise expiry/restore, and capture production smoke evidence |
| BOS / tenant hub / APIs / workers | Mixed ASP.NET previews, PHP handlers, and sidecars | Replace provisioning writes, API gaps, scheduled jobs, webhooks, uploads/downloads, and operator flows; verify queue/retry/backup/restore behavior |

The following are therefore still **pending**, even when their route or
read-only preview exists: complete field/action/workflow parity, tenant-country
statutory behavior, server-side capability enforcement, CSRF/audit coverage,
on-premises and hybrid registration/synchronization/backup recovery, production
dual-sample evidence, and the three combined browser regression rounds. PHP
source deletion and PHP-FPM removal remain prohibited until
`/migration/php-decommission-readiness` is ready and release-owner approval is
attached.

### Non-ERP coverage plan — 2026-10-05

ERP posting stays on the ERP engine. This plan is the other surfaces. A surface is not 100% while a PHP action, field, or page the operator still opens is unanswered, redirected to a browse shell, or only a digest.

| Surface | Measured now | Still short of the PHP reference |
| --- | --- | --- |
| Storefront and API ajax | 109 of 112 (97%) | Two includes and the ERP finance ajax script stay unmapped on purpose |
| Control Panel shop, users, and requests ajax | 72 of 75 (96%) | The prices init include and price review. Both price-review scripts stay dry-run |
| Broader `cp/content` ajax | 95 of 110 | Control, portal, packs, and language ajax outside the 75 |
| Marketing, industries, LifeOS, BOS, tenant CP | ASP.NET apps and shells exist | Same-to-same page, form, and host evidence is still open. The weighted phase headline stays 24/24 Phase A and about 20.4% done until those gates close |

Next build order on this branch, excluding ERP journals: the prices init include stays unmapped, price review stays on its dry-run, then the rest of `cp/content` ajax, then storefront pages that still render a digest, then marketing, industry hosts, LifeOS, and BOS against the PHP pages for the same URL.

### PHP reference re-review — missed items plan (2026-10-07)

The ajax ratios count only ajax scripts. To make sure nothing in the PHP reference is missed, `scripts/php_reference_gap_inventory.py` now checks every PHP file and function against the ASP.NET implementation code. The migration catalogues, dry-runs, reporters and dashboards are left out, because they list PHP paths without porting them. The output is [`inventory/PHP_REFERENCE_GAP_INVENTORY.md`](inventory/PHP_REFERENCE_GAP_INVENTORY.md). Rerun it at every checkpoint with `--max-gap <current>`, so the gap count can only go down.

| Triage | Files | Meaning |
| --- | ---: | --- |
| Mentioned | 771 | ASP.NET names the file or all its functions. This is a lead only; parity stays in the rows of this tracker |
| ERP tab mapped | 118 | The `erp_tabs_*.php` key is routed by `ErpPhpTabRouteMap` (all 160 tabs are routed). Many tab writes are still PHP |
| Third-party | 316 | PHPExcel, PHPMailer, PclZip, MobileDetect, elFinder, TinyMCE, inputmask, the Laximo SDK. Replaced by .NET packages, not ported |
| Ops script | 417 | Root `epc-*.php`, `*-setup.php` and similar one-off deploy, seed, repair and audit scripts. Replaced by migrations and workers, not ported one-to-one |
| Sitemap shard | 80 | Generated sitemap files |
| **Gap** | **876 (about 298k lines)** | Nothing in ASP.NET references it. Each one must be ported, or retired with a reason in `inventory/PHP_RETIRED.tsv` |

Of 9,867 PHP functions, 8,152 are not named anywhere in ASP.NET. Natively ported pages often do not name their PHP includes, so every gap file is triaged before it is built. The gaps go into the plan in this order (ERP last, as agreed):

1. **Storefront customer pages (epartscart.com).**
   - `content/shop/order_process`: `cart.php`, `my_orders.php`, `my_orders_items.php`, `checkout_confirm.php`, `my_order_not_authorized.php`, `my_quotes.php`, `common_add_to_basket.php`, `checkout_login_offer.php`, `get_customer_offices.php`.
   - `content/users`: `dp_user.php`, `epc_registration_enhanced.php`, `profileform.php`, `epc_reg_fields_compliance.php`, `epc_countries.php`, `forgot_password.php`, `new_password.php`, `check_user_access.php`, `check_reg_contact.php`, `epc_login_rate_limit.php`, `epc_session_security.php`, `epc_password_upgrade.php`, the agreement module.
   - `content/shop/catalogue` (41 files): `printProducts.php`, `printProducts_2.php`, `printProduct_Info.php`, the product pages, compare, bookmarks, SKU media, the text search algorithm, the tree lists.
   - `content/shop/docpart` (44 files): `part_search_page.php` and `part_search_page_1.php`, the parts agent, demand intelligence, garage, fitment, cross interchange, the multivendor and commerce price ingest.
   - `modules/*`: login (password, code, social), menu, bread crumbs, slider, news, lang, and `shop/*` (cart, balance, search string, geo, ucats).
   - Front templates `expan`, `modex` and `limo`; `core/dp_core.php` and `dp_helper.php` behaviour; plugins (metadata handler, phone/tablet, error pages, shop cart).
2. **Checkout and order side effects still on PHP.**
   - Done: process-flow sync (`epc_pf_sync_order_case` and `epc_pf_sync_po_case` in `content/shop/finance/epc_erp_processflow.php`). See the checkpoint below.
   - Sales-invoice sale-demand capture: `epc_erp_inventory_record_sale_demand` runs when PHP saves a sales invoice. ASP.NET does not record it yet.
   - SMS and WhatsApp fan-out: `content/sms/handlers`, `content/notifications`, `epc_order_whatsapp_share.php`.
   - `content/shop/payments`, `content/shop/obtaining_modes`, `content/shop/returns/ajax/helper.php`, `content/shop/protocol`, `content/shop/print_docs`, `content/shop/document_control`.
3. **Control Panel shop pages.**
   - `cp/content/shop/order_process` (20 files): `order_card.php`, `orders_items.php` and its add, edit and reload modals, the orders detail pane, the fulfilment, OMS and WhatsApp guides.
   - `cp/content/shop/catalogue/product.php` and its includes.
   - `cp/content/shop/prices_upload` page bodies: `upload_file.php`, `price_review.php`, the download manager, update history, multivendor and commerce upload. These must reuse the existing price importer.
   - Smaller sets: logistics, crosses, data transfer, document control, channels, marketing, tenant hub, POS, payments, demand countries, manufacturer synonyms, eparts catalogue and mod, accessories, statistics.
   - `cp/content/users`, `cp/content/lang`, `cp/content/packs_control`, the file manager, `cp/content/requests`, the content structure dumps.
   - The CP plugins `2fa` and `authentication`; CP modules (bread crumbs, left menu, SSL check, logout).
4. **Control Panel control and portal.**
   - `cp/content/control/portal` (52 files): the auto price engine shell, social media hub, auth settings, tax toolkit, industry kit, licence trends and consolidation, marketing broadcast, the visual page editor, the fleet dashboard, the customer board, tenant e-mail settings, governance, POS tenant management, mobile apps, the BOC panels, and the guides.
   - Version control and admin-access check.
   - Root CP includes: `epc_cp_mainstream_menu.php`, `epc_oms_menu_guide_lib.php`, `epc_storefront_stub_redirect.php`, `epc_static_serve.php`, `epc_deploy_auth.php`.
5. **Marketing, platform, tenant, BOS and industries (`content/general_pages`, 205 files plus 31 industry templates).**
   - The ecomae.com platform pages, router, data, capability guides, FAQ and legal content; the brochures; the free tools; the portal demo; the web tracker.
   - The auth common, MFA, SMTP and OAuth providers; the API v1 and webhooks; Power BI.
   - Commerce isolation, tenant data protection, and the portal tenant pages and intro.
   - The BOS unified and blockchain BOS pages, and the BOC console.
   - The industry consolidation and the industry templates (`_base_template.php`, the sub-industry page and 28 industries).
   - `epc_cloudpanel_helpers.php` is moved to ops workers, not ported as a page.
6. **Price engine.** `content/shop/price_engine` (23 files, including the 6,768-line `epc_auto_price_engine.php` and the discovery adapters) plus the CP auto-price shell. This must stay on the existing importer.
7. **ERP to 100%, after the steps above.**
   - The 152 finance libraries in `content/shop/finance`. The largest are the external reports build, the UAE tax compliance, jewellery, inventory, SCM, tax toolkit, `my_balance.php`, phase 8, order planning, AML, staff, concurrency, HR law, integration, access, datalink, industry packs, period close, WMS, payroll and procurement.
   - The CP finance pages: nav areas, the dashboards, the operations editor and create operation, the payment systems, custom shipping.
   - The write side of the 118 mapped tabs.
   - The deferred findings: cash without journals, integrity gaps, POS without GL, voucher gaps, untested services, money typed as double, the emergency-publish flag. Reposting the wrong 4000/6100 production transfers needs approval.

Each item closes only when ASP.NET does the PHP behaviour (tested on a throwaway database, full suite green), or when it is retired with a reason. The inventory gap ratchet is lowered in the same commit.

### Checkpoint 2026-10-07 — process-flow order and PO cases sync like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory: 876 gap files (ratchet `--max-gap 876`); unnamed PHP functions down from 8,152 to 8,145.
- ASP.NET now runs PHP `epc_pf_sync_order_case` and `epc_pf_sync_po_case` at the same points as PHP:
  - at the end of the checkout fulfillment bootstrap (order case);
  - after each new supplier PO, after PO save, after PO status changes and goods receipt (PO case);
  - in the fulfillment status, sync and auto-post actions (order case);
  - after a sales tax invoice is saved for an order (order case).
- The process-flow tables are created with PHP's DDL if missing (`ErpPfSchema`), and the order case advances by the PHP facts: sales order, paid, procured, fulfilled, delivered, invoiced. The PO case advances on approved, partial, received and received plus invoiced, and is cancelled when the PO is cancelled.
- Checkout has no CP admin, so the case actor and the final assignee fallback is the customer, as with PHP `epc_pf_user_id()`.
- Intended deviation: PHP's checkout only syncs a new PO case when the function is already loaded, which it never is at checkout. ASP.NET always syncs it.
- Fixed: the order facts read `name`, `surname` and `email` columns that `shop_orders` does not have, so no order case could ever start. They are now read like PHP's `SELECT *`.
- Sync failures are swallowed, like PHP, so they never block checkout or a PO save.
- Still on PHP: SMS/WhatsApp fan-out, and the sales-invoice sale-demand capture.
- Verified on a throwaway database: checkout creates the two processes, the order case at step 2 (sales head) and one case per PO assigned to the customer. A rerun stays at 3 cases. Approving a PO moves its case to step 2. 5171 of 5171 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — checkout sends staff and customer order e-mails like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%.
- ASP.NET checkout now runs PHP `epc_checkout_send_order_notifications()` before the supplier LPO step, as PHP does:
  - `new_order_to_manager` goes to the admin inbox, the customer's CRM manager and the office managers. CRM comes from `users_profiles` keys in PHP priority order. Office managers come from `shop_offices.users`, filtered to backend groups and their child groups.
  - The admin inbox is the tenant `contact_json` admin_email, then from_email, then config `from_email`, otherwise `admin@` + host.
  - If the admin did not get it, one admin-only retry follows. The log line reads `Order email to admin X: sent`, `sent (retry)` or `FAILED after retry`.
  - `new_order_to_user` goes to the signed-in user (e-mail from `users`, with the `email_confirmed` / `send_for_not_confirmed` rule) or to the guest e-mail, with one retry. The log reads `Order email to customer (user #N | email): sent|FAILED`.
- Staff body ports `get_order_info_html_epc_staff.php`:
  - the Control Panel order link and the customer profile block;
  - delivery address, delivery type, payment and cart;
  - the warehouse line table with dual AED/USD prices (USD rate from currency 840);
  - the destination-aware VAT and courier totals, using the ERP VAT twins;
  - margin, weight and the comment.
- Customer body ports `get_order_info_html_for_user.php`: status and payment tables, the courier or pickup-office block, the line table, and the comment block (string 4509).
- `StorefrontNotifyDispatcher` now applies `content/notifications/template.php`:
  - logo, subject and Dubai-time date;
  - the 4929/4930 footer;
  - the office-map hide and obtain-caption replacements;
  - inline table styles.
  - It also resolves `translate_str_by_key` like PHP (`is_error`, `same`, empty when missing).
- Intended deviations:
  - PHP `require_once` wraps only the first notification in a request. ASP.NET wraps every e-mail, including LPOs.
  - The Yandex map `<script>` of `show_office_info.php` is not put into e-mails.
- Fixed: checkout now stores the order comment through `htmlentities()` like PHP. Before this, raw HTML reached the e-mails.
- Still on PHP: SMS/WhatsApp fan-out for these notifications. (Process-flow sync is done since the next checkpoint.)
- Verified on a throwaway database. The signed-in checkout sends 7 e-mails in PHP order: admin (fails), CRM, office manager, admin retry, customer, then 2 LPOs, and the totals match (630.00 gross, 600.01 net, courier VAT 2.00, total 672.00). A guest order goes to the pickup office. A missing template is logged as FAILED. 5170 of 5170 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — checkout raises supplier POs and LPO e-mails like PHP

Not complete.

- Ratios on this branch (based on `main`): storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75, broader `cp/content` 95 of 110. The open CP parity branch (PR #2031) has 74 of 75 and 108 of 110. Weighted headline stays about 20.4%.
- After the order commits, ASP.NET checkout now runs the same tail as PHP `ajax_checkout_create.php`, in the same order:
  - One `lpo_to_supplier` e-mail per warehouse on the order. This includes own warehouses and catalogue stock found through `shop_orders_items_details`. The LPO number is the customer order number.
  - The recipient comes from `connection_options` `order_email` / `supplier_order_email` / `lpo_email`, otherwise from the price list `sender_email`.
  - Each send is retried once. Every outcome goes to `shop_orders_logs` with the PHP wording: sent, FAILED, skipped (no order e-mail), and the "0 sent" hint.
  - For signed-in customers, `epc_erp_order_fulfillment_bootstrap` runs next. It creates a confirmed ERP sales order (`shop_order_id` = order) and one draft PO per supplier, with `order_id` = customer order, notes `Customer order ref #N` and title `PO for order #N — supplier`.
  - A warehouse without an ERP supplier gets one auto-created from the warehouse name, so own warehouses get a PO too. A bootstrap failure is logged as `ERP fulfillment bootstrap skipped: …` and the order stays.
- Bootstrap now runs PHP's additive fulfillment schema-ensure instead of failing closed.
- Fixed own-catalogue (product type 1) checkout. Quoted `'?'` placeholders were renumbered, so `t2_json_params` bound to a missing parameter. They now bind `t2_storage_id`, `t2_storage_id`, `t2_json_params`, as PHP does.
- Still on PHP: the staff and customer new-order e-mails (`get_order_info_html_*`), and the process-flow sync.
- Verified with a throwaway-database checkout covering an own warehouse, a price-list supplier and a supplier with no e-mail: 3 POs, 1 sales order, 2 LPO e-mails (one after a retry), the exact log lines, and an idempotent re-run. 5152 of 5152 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — storefront ajax writers resolve from services; merge with main is green

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75, broader `cp/content` 95 of 110. Weighted headline stays about 20.4%.
- The customer desk handler took two writer services. Hosts that do not register them inferred both as request bodies, and GET/POST routes refuse inferred bodies, so every route in `StorefrontPhpAjaxEndpoints.Map` returned 500 in 16 throwaway-database tests. Writer parameters on mapped handlers are now `[FromServices]`. All 18 writers are registered in `Program.cs`, so production resolution is unchanged.
- This branch: 5090 of 5090 tests pass. A trial merge with `main` (Devin's ERP work, 43 commits) merged cleanly and passes 5121 of 5121. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — catalog sync saves UMAPI rows and C110J lists 720 crosses

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan stays 95 of 110. Storefront and API ajax stays 109 of 112. Weighted headline stays about 20.4%.
- `/api/umapi_proxy.php` manufacturers now call the catalog, then `REPLACE` into `epc_umapi_manufacturers`, `epc_umapi_cache`, and `epc_umapi_sync_status`. The next request is served from those rows and does not call the catalog again. A later HTTP 402 leaves the saved Toyota row in place and the status message is `Payment Required`. VIN `1HGBH41JXMN109186` is stored in `epc_umapi_vin_cache` and reused. The key is `config.php` `umapi_api_key`, otherwise the built-in key.
- A live call to `api.umapi.ru` with that built-in key returned HTTP 402 `App key is not in the access list`. `image.umapi.ru` returned the supplier logo. Homepage grids still answer `[]` on that rejection so the widget does not show an error.
- Article C110J / JS ASAKASHI returned 720 unique crosses in 22 ms on the throwaway database. The part page seeds the list with the 5000 cap instead of 200. The client script still requests `limit=5000` with crossbase and paints every row.
- Supplier price upload of 2000 CSV rows finished in 214 ms (about 9309 rows per second). The storefront part search for `BOSCH SPEED0001` returned that offer in 16 ms. `www.epartscart.com` port 443 was refused from this environment, so the timing was measured on the storefront search service, not on the public host.

### Checkpoint 2026-10-07 — sitemap files use the existing editor

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan is 95 of 110. The previous checkpoint had 94 of 110. Storefront and API ajax stays 109 of 112.
- `ajax_create_sitemap.php` with no database says “No DB connect”. A missing sessions table says “Admin sessions are not in this database.” A guest says “Forbidden”. A missing CSRF key is “Error! CSRF 1”. A wrong key is “Error! CSRF 4”. An empty page list says “Select at least one page for the sitemap.” Missing catalogue tables say “Catalogue tables are not in this database.” and are not created. No sitemap file is written in that case.
- After the catalogue tables exist, page `about`, published category `parts`, and published product `brake` are written into `sitemap1.xml` under `https://www.epartscart.com/`. Unpublished `hidden`, its child `secret`, and unpublished product `draft` stay out. `sitemap.xml` points at `sitemap1.xml`. The answer body is `Ok`. No `epc_erp%` table is created.

### Checkpoint 2026-10-06 — governance and free tools use the existing writers

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan is 94 of 110. The previous checkpoint had 92 of 110. Storefront and API ajax stays 109 of 112.
- `ajax_platform_governance.php` with no database says “DB error”. A missing sessions table says “Admin sessions are not in this database.” A guest is HTTP 403 “Admin login required”. A tenant host is HTTP 403 “Super CP only” and the rules table is not created. Rule key `!!!` is “Invalid rule_key”. A missing rules table says “Governance-rules table is missing — schema-ensure stays Classic.” After the table exists, `auth_otp` stores active 0 and enforcement `advisory`. A missing rule says “Rule not found”. Action `seed` is HTTP 400 “Unknown action”. No `epc_erp%` table is created.
- `ajax_epc_free_tools_admin.php` with no database says “Database connection failed”. A guest is HTTP 403 “Admin login required”. A tenant host is HTTP 403 “Super CP only”. Tool `nope` is “Unknown tool”. A missing settings table says “Free-tools settings table is missing — schema-ensure stays Classic.” Deactivating `vat` stores `disabled_tools` containing `vat`. Activating `VAT` clears that list. Usage stats before the account tables exist say “Free-tools usage tables are missing — schema-ensure stays Classic.” After one account and one save, the counts are 1. Action `send` is “Unknown action”. Audit tables are not created.

### Checkpoint 2026-10-06 — website tracker uses the existing dashboard builder

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan is 92 of 110. The previous checkpoint had 91 of 110. Storefront and API ajax stays 109 of 112.
- `ajax_epc_web_tracker.php` with no database is HTTP 503 `{ok:false, error:"db"}`. A missing sessions table says “Admin sessions are not in this database.” A guest is HTTP 403 `forbidden`. A tenant host that posts another site key is HTTP 403 `tenant_scope`. This URL does not create `epc_web_tracker_sessions`, `epc_web_tracker_pageviews`, or `epc_web_tracker_events`.
- When those tables are absent the answer is HTTP 500 `query_failed` and “Website tracker tables are not installed.” After the tables exist, host `127.0.0.1` counts only `site_key` `127_0_0_1` (one session, landing `/desk/parts`). Host `ecomae.com` is super and counts both sessions under `_all`. Session id 0 says “Missing session id.” A missing id says “Session not found.” The CSV starts with “Website tracker full report” and includes the landing path. No `epc_erp%` table is created.

### Checkpoint 2026-10-06 — parts catalogues keep the PHP link rules

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan stays 91 of 110. Storefront and API ajax stays 109 of 112.
- The vehicle catalog page lists the catalogues enabled on the `parts_catalogues` search tab. Toyota with AutoXP, Ilcats, Catalogs-Parts, and Levam enabled stores four links. Ilcats becomes `https://ilcats.example/?pid=PID3&clid=CL1`. AutoXP appends id `99`. Catalogs-Parts replaces `client:;` with `client:CLIENT;`. BMW is left out because it is not in the AutoXP or Catalogs-Parts car lists. A catalogue whose show flag is off is left out.
- Missing catalogue tables are reported and are not created. The AutoXP click script with no database says “No DB connect”. A missing click table says “AutoXP click counter is not in this database.” The first click stores 1. At 2000 the answer is 0 and the count stays 2000. A later allowed click with an https target redirects and increments. No `epc_erp%` table is created.

### Checkpoint 2026-10-06 — marketing broadcast counts recipients

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan is 91 of 110. The previous checkpoint had 90 of 110.
- `ajax_marketing_broadcast.php` with no database says “DB unavailable”. A missing sessions table says “Admin sessions are not in this database.” A guest is HTTP 403 “Forbidden”. Action `send_email` is “Unknown action”. This URL does not send a campaign.
- A posted `audience_mode=manual` is ignored, matching the PHP query-string fields, and the all-audience count is 0 when `users` is missing. The `users` table is not created.
- Query `audience_mode=manual` with `a@b.test,nope,c@d.test` counts 2 email addresses. WhatsApp meta `971500000001;971500000002` counts 2 phones.
- Email template `blank` returns the catalog subject and HTML. An unknown WhatsApp template key returns the blank catalog body. `epc_marketing_broadcast_campaigns` is not created. No `epc_erp%` table is created.

### Checkpoint 2026-10-06 — portal settings use the existing writers

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan is 90 of 110. The previous checkpoint had 89 of 110.
- `ajax_portal.php` with no database says “Database connection failed”. A missing sessions table says “Admin sessions are not in this database.” A guest is HTTP 403 “Admin login required”. An unknown action is “Unknown action”.
- `save_settings` for industry `nope` says “Unknown industry code: nope”. Before `epc_portal_site_settings` exists, the save fails and does not create the table. After the table exists, host `127.0.0.1` stores industry `auto_parts` when `platform_host` was posted, access mode `full` when `full_commerce` was posted, system name `Desk Parts`, country `AE`, packs `core` and `commerce`, and not `super_platform`. Hidden group `4` is stored.
- A Super CP save with `target_host` stores the local host `ecomae.com` and says “Client host push stays Classic.” The tenant row is left as `Desk Parts`.
- `menu_items` stays on the classic helper. `seed_storefront_data` stays on the classic helper. `deploy_site` on a tenant host says deploy is only available on the platform control panel. On `ecomae.com` it says “Site deploy stays Classic.” No deploy target table is created.
- `tenant_reset_password` and `tenant_reveal_password` on a tenant host are HTTP 403 “Super CP only”. On `ecomae.com` they stay classic and the response has no password field.
- `tenant_set_active` on a tenant host is HTTP 403 and does not create `epc_portal_tenants`. Site key `!!!` is “Invalid site key”. A missing registry says “Tenant registry table is missing — schema-ensure stays Classic.” After the table exists, `epc_demo` with `active=0` stores inactive and says “Tenant disabled — storefront and CP blocked”. An unknown site key is “Tenant not in registry”. No `epc_erp%` table is created.

### Checkpoint 2026-10-06 — integrations settings use the existing writers

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan is 89 of 110. The previous checkpoint had 88 of 110.
- `ajax_integrations.php` with no database says “Database connection failed”. A missing sessions table says “Admin sessions are not in this database.” A guest is HTTP 403 “Admin login required”. This script does not check CSRF. An unknown action is “Unknown action”.
- `save_mobile` before `epc_portal_site_settings` exists says the save failed and does not create the table. With the table present and no row, the message is “No epc_portal_site_settings row exists for this host yet.” The existing mobile writer adds `integrations_json`. A 130-character app name is stored as 120 characters. `enabled=1` stores true and `pwa_enabled=0` stores false.
- `save_tenant_smtp` with no settings row says “Site settings row is missing. Schema ensure stays on the Classic twin.” After the row exists, host `mail.example.test` and password `desk-secret` are stored, encryption `starttls` is stored blank, and the mobile app name stays. A later save with an empty password keeps `desk-secret`.
- `test_tenant_smtp` to `not-an-email` says “Valid test email required”. With tenant SMTP off, the message says use tenant SMTP is off. With tenant SMTP on and no host, the message is “SMTP host and port are required.” No mail is sent.
- `save_feature_flags` on `127.0.0.1` says “Super CP only” and does not create `epc_tenant_feature_flags`. On `ecomae.com`, site key `!!!` is “Invalid site_key”. Site key `epc-demo` with `email_smtp` on saves the catalog flags through the existing writer, leaves `tenant_registry` out, and stores `email_smtp` enabled. No `epc_erp%` table is created.

### Checkpoint 2026-10-06 — social draft uses the existing social writer

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan is 88 of 110. The previous checkpoint had 87 of 110.
- `ajax_epc_social_media.php` with no database says “DB unavailable”. A missing sessions table says “Admin sessions are not in this database.” A guest is “Admin required”. A missing CSRF token is “CSRF failed”.
- `save_draft` before `epc_social_post_drafts` exists says “Social drafts table is missing — schema-ensure stays Classic.” and does not create the table.
- After the table exists, platform `Instagram`, title `Brake pads`, and caption `Front kit` store site key `127-0-0-1`, platform `instagram`, and status `draft`. Updating id 1 sets the caption to `Rear kit` and the row count stays 1. Id 99 is “Draft not found”.
- `generate_caption` stays on the classic helper. `publish_now` stays “Publishing to Meta/TikTok stays Classic.” The draft count stays 1. No `epc_erp%` table is created.

### Checkpoint 2026-10-06 — notification test uses the existing communications writer

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan is 87 of 110. The previous checkpoint had 86 of 110.
- `ajax_test_notification.php` with no database says “No DB connect”. A missing sessions table says “Admin sessions are not in this database.” A guest is “Forbidden”. A missing CSRF value is `Error! CSRF 1`.
- A post without `type` is “No params”. Type `fax` is “Incorrect type”.
- `email` to `ops@example.test` before `reg_fields` exists says “Tenant database unavailable.” and does not create the table. A pattern that does not match says “The contact does not match the required format.”
- A missing notifications table says the database is unavailable and does not create it. An empty table says “The email test notification template is not configured.” A disabled template says it is disabled. A template that refuses direct contacts says so.
- With the template enabled and SMTP unset, the message is “SMTP is not fully configured — fill the e-mail group in Configuration first.” `debug_results` is not created. No `epc_erp%` table is created.

### Checkpoint 2026-10-06 — content parent list reads the content table

Not complete.

- Control Panel shop, users, and requests ajax stays 72 of 75. The broader `cp/content` ajax scan is 86 of 110. The previous checkpoint had 85 of 110.
- `ajax_get_content_json_list.php` with a code other than `secret_succession` is “Forbidden” before the database opens. The matching code with no database says “No DB connect”.
- A CSRF value with no sessions table says “Admin sessions are not in this database.” A missing CSRF value is `Error! CSRF 1`.
- A missing `content` table says “Content is not in this database.” and does not create it.
- Page size 1, frontend page 0, returns `Home` and its child `About`. `max_level` is 2, the pagination count is 2, and the frontend total is 3. Page 1 returns `Shop`, translated from language key `10`. Backend mode returns `Backend` and total 1. `content_id` stays unused, matching PHP. The content row count stays 4. No `epc_erp%` table is created.
- The prices init include and the two price-review scripts stay open. Price review remains a dry-run with writes 0.

### Checkpoint 2026-10-06 — cross link uses the existing cross writer

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 72 are mapped. 3 stay unmapped. The previous checkpoint had 71 mapped and 4 unmapped. The broader `cp/content` ajax scan is 85 of 110 mapped.
- `ajax_epc_cross_cp.php` with no database says “No DB connect”. A missing sessions table says “Admin sessions are not in this database.” A guest is “Access denied”. A missing CSRF value is `Error! CSRF 1`. An unknown action is “Unknown action”.
- `lookup_crosses` and `import_full_catalog` stay on the classic lookup and do not create `shop_docpart_articles_analogs_list`. `add_cross_link` before that table exists says “Cross links are not in this database.” and does not create it.
- After the table exists, `add_cross_link` calls `ICpCrossWriteService.AddAsync`. Article `04465-YZZD2` / `TOYOTA` to `446610010` / `AISIN` stores both directions, `inserted` is 1, and `cp_links_for_article` is 2. The same post again is `already_linked` and the row count stays 2. The same part and brand is `same_part_same_brand`.
- `add_cross_bulk` adds `0986AF0078` / `BOSCH` and counts the existing AISIN pair as already. The table then has 4 rows. `repair_empty_brands` stays on the classic helper and does not change that count. No `epc_erp%` table is created.
- The prices init include and the two price-review scripts stay open. Price review remains a dry-run with writes 0.

### Checkpoint 2026-10-06 — commerce source list reads price lists; file and URL refresh stay classic

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 71 are mapped. 4 stay unmapped. The previous checkpoint had 70 mapped and 5 unmapped. The broader `cp/content` ajax scan is 84 of 110 mapped.
- `ajax_epc_commerce_ingest.php` with no database says “No DB Connect”. A missing CSRF value is `Error! CSRF 1`.
- `list_sources` before `shop_docpart_prices` exists returns count 0 and does not create the table. After the table exists, without a `records_count` column, the list returns 4 commerce names. `Parts.P` keeps meta role `purchase`, base `PartsBook`, and margin `12.5`. `Stock-L` is inventory and `has_url` is false. `url_only=1` returns `Parts.P` and `Spare-S`.
- `refresh_url` for price 0 is “price_id required”. A missing id is “Price list not found”. A list with no http(s) link is “No http(s) link on this price list”. `Orphan` is “Cannot detect commerce role from list name Orphan”. Price 2, which has an http link, returns “URL refresh stays on the classic importer.” and `last_updated` stays 22.
- `refresh_all` with URL-linked lists returns the same classic message, total 2, and does not change `last_updated`. Upload with no file is “Choose an Excel/CSV file, or paste a file URL”. A pasted URL stays on the classic importer and the price-list count stays 5. No `epc_erp%` table is created.
- The prices init include, price review, and crosses lookup stay open.

### Checkpoint 2026-10-06 — multivendor sample, ACL, and vendor code use the existing prices-upload writer

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 70 are mapped. 5 stay unmapped. The previous checkpoint had 69 mapped and 6 unmapped. The broader `cp/content` ajax scan is 83 of 110 mapped.
- `ajax_epc_multivendor_ingest.php` with no database says “No DB Connect”. A missing CSRF value is `Error! CSRF 1`.
- `sample` returns status true, filename `epc-multivendor-sample.csv`, and a CSV that contains `TOYOTA,446610010`.
- `min_price_acl_get` before the ACL table exists returns restrict true and does not create `epc_mv_min_price_acl`. `min_price_acl_save` before the table exists says the table is missing and does not create it. After the table exists, restrict 0, group ids `[3]`, and user ids `[7]` are stored, and the message is “Minimum price access saved”. Schema CREATE stays classic.
- `vendor_codes_list` before `shop_storages` exists returns count 0 and does not create the table. `vendor_code_save` on storage 4 changes short name `OLD` to `S-UAE` and name to `Gulf Parts`. The list then returns count 1.
- `upload` without a file returns “Choose an Excel/CSV file with multiple vendors” and does not add warehouses. File ingest stays on the classic importer. No `epc_erp%` table is created.
- Commerce ingest, the prices init include, price review, and crosses lookup stay open.

### Checkpoint 2026-10-06 — OMS item status uses the existing OMS writer

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 69 are mapped. 6 stay unmapped. The previous checkpoint had 68 mapped and 7 unmapped. The broader `cp/content` ajax scan is 82 of 110 mapped.
- `ajax_epc_orders_oms.php` with no database says “DB unavailable”. A guest is “Forbidden”. A missing CSRF value is `Error! CSRF 1`. Order 0 is “Invalid order”. A missing orders table says “Orders are not in this database.” and does not create `shop_orders`.
- `set_item_status` calls `ICpOmsWriteService.SetItemStatusAsync`. Item 4 on order 12 moves from status 1 to 2, and the order log contains “status to 2”. A missing items table says “Order items are not in this database.”
- `erp_document_map` returns “ERP document map stays on the ERP read.” and does not create `epc_erp%`. Warehouse lookup, message list, and supplier fulfillment status stay on the classic readers. The other OMS write actions call the same OMS writer.
- Commerce ingest, multivendor ingest, the prices init include, price review, and crosses lookup stay open.

### Checkpoint 2026-10-06 — order pay refund uses the existing OMS writer

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 68 are mapped. 7 stay unmapped. The previous checkpoint had 67 mapped and 8 unmapped. The broader `cp/content` ajax scan is 81 of 110 mapped.
- `ajax_order_pay_refund.php` calls `ICpOmsWriteService.PayRefundAsync`. With no database the message is “No DB connect”. A post without `direct_refund` is “Forbidden”. A missing orders table says “Orders are not in this database.” and does not create `shop_orders`.
- A manager who is not listed on the order office is “Forbidden”. An unpaid order is “Order is not paid.”
- A missing accounting-codes table says “Refund accounting is not in this database.” Order 12, paid sum `25.50`, direct refund, stores the income row on operation code 5, the cash row on operation code 6, and sets `paid` to 0. The success message is empty, matching PHP. No `epc_erp%` table is created. Manager email stays on the classic notify path.
- OMS, commerce ingest, multivendor ingest, the prices init include, price review, and crosses lookup stay open.

### Checkpoint 2026-10-05 — SAO exec checks the tech key and does not run supplier scripts

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 67 are mapped. 8 stay unmapped. The previous checkpoint had 66 mapped and 9 unmapped. The broader `cp/content` ajax scan is 80 of 110 mapped.
- `ajax_exec_action.php` with a key other than `tech_key` returns status false and “Wrong key” before the database is opened.
- With the tech key and no database, the body is `No DB connect`.
- With the tech key and no CSRF value, the message is `Error! CSRF 1`. A missing SAO table says “SAO actions are not in this database.” and does not create `shop_sao%`.
- Action 9, which is not linked to the item state, returns the PHP message “Данное действие уже выполненно другим менеджером.”
- Action 3 is linked, and the answer is status false and “Supplier action script was not executed.” `sao_message` stays empty. The supplier PHP file is not loaded.
- OMS, pay and refund, commerce ingest, multivendor ingest, the prices init include, price review, and crosses lookup stay open.

### Checkpoint 2026-10-05 — CRM, customer, and document ajax use the existing writers

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 66 are mapped. 9 stay unmapped. The previous checkpoint had 62 mapped and 13 unmapped. The broader `cp/content` ajax scan is 79 of 110 mapped.
- A POST to `ajax_crm_endpoint.php` with an action is rewritten to `/cp/crm/action`. The old page button is the operator confirm, so that post is allowed through the existing CRM writer. The response is JSON. A GET with no action is “No action”. A guest is “Access denied”.
- `save_customer` on both customer endpoints uses the existing buyer-profile writer, then upserts `users_profiles`. A missing buyer table says “Buyer profiles are not in this database.” Country `ae` and TRN `100-200` store `AE` and `100200`.
- `customer_advance` calls the existing cash settlement with entry kind `advance`, income true, and `PostGl` false. A missing accounting table says “Customer accounting is not in this database.” Amount `25.50` is stored as income 1.
- `einvoice_create` returns “E-invoice was not posted” and does not create `epc_einvoice_documents` or `epc_erp%`.
- `save_company` and `save_template` use the existing document writer. A missing company table says the table is missing and does not create it. An empty template code is “Template code required”. Logo, attachments, and seller sync stay on the classic messages.
- OMS, pay and refund, commerce ingest, multivendor ingest, the prices init include, price review, crosses lookup, and SAO stay open.

### Checkpoint 2026-10-05 — price CSV import uses the existing writer

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 62 are mapped. 13 stay unmapped. The previous checkpoint had 61 mapped and 14 unmapped. The broader `cp/content` ajax scan is 75 of 110 mapped.
- Step 5, `ajax_5_import_csv_to_db.php`, calls `ICpPriceImportService.ImportWizardDirectoryAsync`. A missing price-list table says “Price lists are not in this database.” With `initiator=js` and `clean_before` present, the old row for price 4 is replaced by article `0986`. The CSV is deleted after the import. A caller who is not an admin and has no tech key is `Forbidden`.
- Commerce ingest, multivendor ingest, the prices init include, and price review stay open.

### Checkpoint 2026-10-05 — price Excel stop and CSV cleanup

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 61 are mapped. 14 stay unmapped. The previous checkpoint had 59 mapped and 16 unmapped. The broader `cp/content` ajax scan is 74 of 110 mapped.
- Step 3 does not read the workbook. An `.xlsx` or `.xls` file returns result 0 and the `intask.pro` conversion note. A folder with only CSV returns result 1.
- Step 4 with a comma separator rewrites `a;b,c` to `a;b;c`. A missing price-list table says “Price lists are not in this database.” A caller who is not an admin and has no tech key gets the PHP spelling `Forbibben`.
- Step 5, the CSV import into the price table, is still open.

### Checkpoint 2026-10-05 — price extract, pyprices health, and the orders detail pane

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 59 are mapped. 16 stay unmapped. The previous checkpoint had 56 mapped and 19 unmapped. The broader `cp/content` ajax scan is 72 of 110 mapped.
- Price upload step 2 extracts a zip in `cp/tmp/prices_upload_files`, deletes the archive, and returns `packs_count` 1 with one success. Entries are written by file name inside that folder. A rar uses the same archive reader. Excel conversion and the CSV import are still open.
- Pyprices health, with an admin session and CSRF, returns `critical` true and “pyprices unavailable”. The pyprices service was not called.
- The orders detail pane without an admin session is HTTP 403 and “Access denied”. A missing orders table says “Orders are not in this database.” Order 12 renders `data-order-id="12"`. The HTML is the order id, not the full OMS console. The table is dropped again so `complete_sale` still creates no `shop_orders`.
- These three URLs are not redirected to a browse page.

### Checkpoint 2026-10-05 — currency, quote options, and price-upload steps 1 and 7

Not complete.

- Control Panel shop, users, and requests ajax is 75 files. 56 are mapped. 19 stay unmapped. The previous checkpoint had 48 mapped and 27 unmapped. The broader `cp/content` ajax scan is 69 of 110 mapped.
- A direct hit on the CRM, customer-management, and document-control include scripts is `No access`. Their standalone endpoints are still open. Customer advances and e-invoice creation stay on the ERP engine.
- `ajax_currency_live_rates.php?action=preview` and `schedule_get` reach the existing live-rate reader. `apply`, `schedule_save`, and `schedule_run_now` reach the existing writer. The dedicated `/cp/currencies/*` routes still require `confirmWrites=true`. The old page button is the operator confirm, so that post is allowed through the same writer. An unknown action is `bad_action`.
- Quote alternative options reach `/cp/quote-requests/alt-options`. A GET or POST with `quote_id` and `line_id` uses that reader.
- Price upload step 1, with tech key `local-tech`, clears `cp/tmp/pack_setup` and leaves `index.html`. Step 7 returns result 0 and “Price rows are not in this database.” when the table is absent, and result 1 after `ENABLE KEYS` when it exists. A call without the tech key or an admin session is `Forbidden`. Steps 2 through 5 are still open.
- These ajax URLs are not redirected to a browse page. No second FX table and no second price-import engine were added.

### Checkpoint 2026-10-05 — CP links into procurement and ERP stay on those engines

Not complete.

- Same storefront and API comparison: 112 files, 109 mapped. The three unmapped files stay an include, an include, and the ERP finance ajax script.
- Control Panel shop, users, and requests ajax is 75 files. 48 are mapped. 27 stay unmapped. The previous checkpoint had 44 mapped and 31 unmapped. The four new mappings are the procurement script and endpoint, and the ERP script and endpoint.
- A broader scan of `cp/content` ajax is 110 files, 61 mapped, 49 unmapped.
- A direct hit on `ajax_procurement.php` or `ajax_erp.php` is `No access`, matching the `_ASTEXE_` guard. Those scripts are includes. They do not post a second purchase or journal.
- A POST to `ajax_procurement_endpoint.php` is rewritten to `/cp/procurement/ajax` before any browse redirect. That dispatcher already calls the ERP write services for supplier, purchase, payment, settlement, and adjustment. A POST to `ajax_erp_endpoint.php` is rewritten to `/content/general_pages/ajax_epc_erp.php`, the ERP ajax endpoint. Neither path is redirected to a browse page.
- Still open in the 27: CRM ajax, crosses lookup that calls out, both customer-mgmt ajax pairs, document control, live currency rates, the orders detail pane, OMS, order pay/refund, the price-import writers `ajax_1` through `ajax_5` and `ajax_7`, commerce and multivendor ingest, pyprices health, the prices ajax init include, price review, quote alt options, and SAO `ajax_exec_action.php`.

### Checkpoint 2026-10-05 — CP catalogue product limits and Yandex YML export

Not complete.

- Same storefront and API comparison as the previous checkpoint: PHP files under `content/shop`, `content/users`, `content/requests`, `modules`, and `api` whose name contains `ajax`, that live in an `ajax` directory, or that live under `api`. 112 files. Path constants and quoted `.php` route strings, after comments are removed, still map 109. Three stay unmapped on purpose: `/api/UCatalog/ucatalog_index.php` (include), `/content/shop/returns/ajax/helper.php` (include), and `/content/shop/finance/epc_erp_modules_ajax.php` (ERP, not ported).
- The same Control Panel set under `cp/content/shop`, `cp/content/users`, and `cp/content/requests` whose file name contains `ajax` is still 75 files. 44 are mapped. 31 stay unmapped. The previous checkpoint had 42 mapped and 33 unmapped. The two new mappings are catalogue product operations and the Yandex YML export.
- A broader scan of `cp/content` files whose name contains `ajax` is 110 files, 57 mapped, 53 unmapped. The previous checkpoint had 55 mapped and 55 unmapped. Those two mappings are inside the 75, so the broader scan moved by the same two.
- Catalogue product operations for a non-admin session are `{"status":false}` with no message. A missing products table says “Catalogue products are not in this database.” `save_product_value_limit` stores `min_limit` 3 and `save_product_status_limit` stores `min_limit_enable` 1 on product 41. The HTML is the product id and the limit input, not the full category, warehouse, and translation chrome.
- YML export without an admin session is `Forbidden`. A missing products table uses the same catalogue sentence and writes no file. With product 41 in category 62, download mode returns `yml_dump_DBS_download.xml` and FBY create-file mode returns `yml_dump_FBY_FBS.xml`. The file is an empty `<offers>` catalog under the test `cp/tmp`. Category count is 1 and read count is 0 because office, storage, property, and image offer selection was not written. No `epc_erp%` table was created.
- Still open in the 31: CRM ajax, crosses lookup that calls out, both customer-mgmt ajax pairs, document control, live currency rates, finance ERP ajax, the orders detail pane, OMS, order pay/refund, the price-import writers `ajax_1` through `ajax_5` and `ajax_7`, commerce and multivendor ingest, pyprices health, the prices ajax init include, price review, procurement, quote alt options, and SAO `ajax_exec_action.php`.

### Checkpoint 2026-10-05 — CP marketing, workshop, crosses, price edit, user modal, bulk history, and POS register

Not complete.

- Same storefront and API comparison as the previous checkpoint: PHP files under `content/shop`, `content/users`, `content/requests`, `modules`, and `api` whose name contains `ajax`, that live in an `ajax` directory, or that live under `api`. 112 files. Path constants and quoted `.php` route strings, after comments are removed, still map 109. Three stay unmapped on purpose: `/api/UCatalog/ucatalog_index.php` (include), `/content/shop/returns/ajax/helper.php` (include), and `/content/shop/finance/epc_erp_modules_ajax.php` (ERP, not ported).
- The same Control Panel set under `cp/content/shop`, `cp/content/users`, and `cp/content/requests` whose file name contains `ajax` is still 75 files. 42 are mapped. 33 stay unmapped. The previous checkpoint had 30 mapped and 45 unmapped. Twelve of the new mappings are in that 75: both marketing ajax files, the workshop endpoint, crosses operations, crosses tmp upload, crosses CSV import, one-row price edit, the user modal, demand CSV tmp upload, bulk history, and both POS ajax files.
- A broader scan of `cp/content` files whose name contains `ajax` is 110 files, 55 mapped, 55 unmapped. The previous checkpoint had 43 mapped and 67 unmapped. Those twelve mappings are inside the 75, so the broader scan moved by the same twelve. That broader scan is not a replacement for the 75.
- Marketing accepts a type-1 admin session. `isBackendGroup` was not ported. The endpoint GET is “No action” and does not create tables. An unknown action creates `epc_marketing_task_progress`, `epc_marketing_kpi_log`, and `epc_marketing_reviews`. Toggle `measurement` / `gsc_verify` stores `is_done` 1 then 0. Completion total is 63. KPI `monthly_sessions` stores `42`. A review score of 9 is stored as 5. Snapshot `orders_total` is 0 when `shop_orders` is absent, and `ga_property` stays the PHP literal `G-J19D1KHXCG`. Guidelines HTML and live analytics were not fetched.
- Workshop CSRF failure is “CSRF failed” and `epc_ws_jobs` is not created. Create stores plate `D-999`. Status `in_progress` is stored. A bad status is “Invalid job or status”. `seed_demo` inserts the PHP demo job `WS-DEMO-001` and 8 labour ops on the existing `epc_ws_*` tables. This is not a second workshop engine. The returned job JSON is a reduced header and line list.
- Crosses without an admin session are HTTP 403 `forbidden`. A missing analogs table says “Cross links are not in this database.” Add `BOSCH` / `0986` ↔ `MANN` / `0987` inserts 1 row because PHP’s exists check treats the reverse pair as already linked. Delete of that id leaves 0. `del_search` with an empty filter is refused and does not wipe the table. The HTML is the article cells, not the full edit-button chrome.
- Price edit for a non-admin session is `{"status":false}` with no message. A missing data table says “Price rows are not in this database.” One add stores `0986` / `BOSCH` / `12.50`, save changes the price to `15.00`, and delete removes that id only. This is not an import wipe. `del_search` without a filter is refused. The HTML is “Nothing found” or the article, not the site-price helper.
- The user modal without CSRF is `Error! CSRF 1`. A missing accounting table says “Customer accounting is not in this database.” Income `1500.50` minus issue `200.00` renders `1 300.50`, with translation id `5579`, customer `7`, and group `Retail`.
- Demand and crosses upload with no file are “No file”. `pads.txt` is “Use .csv files only”. No file was written into the repo. A crosses CSV under the test `cp/tmp` imports 1 link and the file is deleted. A path outside that directory is “Invalid file path”.
- Bulk dashboard creates `epc_bulk_upload_history` and reports total 0. Mark reviewed sets `cp_reviewed_by` 9. A missing upload, including `create_crm_quote` of id 0, is “Upload not found”. A found upload’s CRM quote is “ERP CRM quote was not posted”. `process_upload` with no customer is “Select a customer first” and writes no price row. Cart and shop-quote success were not posted.
- Direct `ajax_pos.php` is `No access` (PHP’s `_ASTEXE_` guard). The endpoint runs search, open, close, and session status. `complete_sale` is “complete_sale was not posted” and does not create `shop_orders` or `epc_pos_sales`. Open then close stores `expected_cash` `25.50` with sales counted as 0 because the sales table is absent. Settings and sessions are created. Search does not create inventory tables. `calc_cart` with no customer does not insert a walk-in user. The tax toolkit was not called.
- Still open in the 33: catalogue product operations, CRM ajax, crosses lookup that calls out, both customer-mgmt ajax pairs, YML export, document control, live currency rates, finance ERP ajax, the orders detail pane, OMS, order pay/refund, the price-import writers `ajax_1` through `ajax_5` and `ajax_7`, commerce and multivendor ingest, pyprices health, the prices ajax init include, price review, procurement, quote alt options, and SAO `ajax_exec_action.php`. No `epc_erp%` table was created. `docpart.users` stayed 2 and `ecomae.users` stayed 2. After the suite, `ecomae_cpw_%` was 0. `dotnet test aspnet/tests/EcomAE.Platform.Tests` passed 5060 and failed 0.

### Checkpoint 2026-10-05 — CP price, payments, channels, logistics, photos, demand, and parts agent

Not complete.

- Same storefront and API comparison as the previous checkpoint: PHP files under `content/shop`, `content/users`, `content/requests`, `modules`, and `api` whose name contains `ajax`, that live in an `ajax` directory, or that live under `api`. 112 files. Path constants and quoted `.php` route strings, after comments are removed, still map 109. Three stay unmapped on purpose: `/api/UCatalog/ucatalog_index.php` (include), `/content/shop/returns/ajax/helper.php` (include), and `/content/shop/finance/epc_erp_modules_ajax.php` (ERP, not ported).
- The same Control Panel set under `cp/content/shop`, `cp/content/users`, and `cp/content/requests` whose file name contains `ajax` is still 75 files. 30 are mapped. 45 stay unmapped. The previous checkpoint had 18 mapped and 57 unmapped. Twelve of the new mappings are in that 75: price preview, session complete, both payment ajax files, price-upload diagnostics, prices send, channels, logistics, accessory photos, upload history, demand CSV, and parts-agent chat.
- A broader scan of `cp/content` files whose name contains `ajax` is 110 files, 43 mapped, 67 unmapped. The previous checkpoint had 31 mapped and 79 unmapped. Those twelve mappings are inside the 75, so the broader scan moved by the same twelve. That broader scan is not a replacement for the 75.
- `/cp/content/shop/prices_upload/ajax_get_price_preview.php` returns “Price rows are not in this database.” when `shop_docpart_prices_data` is absent. An empty list contains `3689`. A `BOSCH` / `0986` row appears in the HTML with heading `3690`. A type-1 session whose user id is 0 is `Forbidden` code 501.
- `/cp/content/shop/prices_upload/ajax_6_complete_session.php` with tech key `local-tech` returns result 0 and “Price lists are not in this database.” when `shop_docpart_prices` is absent. After the row exists, `last_updated` is set and `records_count` is 1. The price-data row stays. The sitemap stale file was not written.
- `/cp/content/shop/payments/ajax_payments.php` without CSRF is `Error! CSRF 1`. A missing systems table says “Payment systems are not in this database.” Activate `cash` stores active 1 on that handler and 0 on `card`, and the message is “Activated: Cash”. An empty handler is “Handler required”. An unknown handler is “Gateway not found”. `seed_dummy` says “Payment accounts are not in this database.” and does not create `epc_payment_accounts`. `mark_settlement` says “Payment settlements are not in this database.” and does not create `epc_payment_settlements`.
- `/cp/content/shop/payments/ajax_payments_endpoint.php` skips the CSRF include and checks the admin session first. A guest is “Access denied”. Bad JSON is “Invalid parameters JSON”. `system_id` 0 with `{}` is “All payment gateways disabled” and every `active` is 0. Saving system 2 stores `{"mode":"test"}` and sets that row active 1.
- `/cp/content/shop/prices_upload/ajax_epc_price_upload_diagnostics.php` creates `epc_price_upload_history` and then says “Price lists are not in this database.” when the price-list table is absent. After one list and one data row, `price_lists_total` is 1. `action=health` is `all_ok` false and the pyprices detail contains `curl not available`. Pyprices and cron HTTP were not called.
- `/cp/content/shop/prices_send/ajax_operations.php` returns `bad_request` for a body that is not JSON. `list_brands` returns `BOSCH` after the data row exists, and names the missing data table before it exists. A missing office-storage map says “Office storage markups are not in this database.” A missing warehouses table says “Warehouses are not in this database.” An unlinked warehouse is named `Sharjah`. `ensure_office_storage_links` inserts one map row, and the second call links 0. `send_prices` is status true and `sent` 0 because no SMTP call is made and no CSV is on disk. `create_prices` with no selectors, and again with a profile group while the data table exists, is “No markup profile selected (choose customers, emails+group, or a profile group).” and does not write a CSV or a price row. A selected profile while the data table is absent says “Price rows are not in this database.”
- `/cp/content/shop/channels/ajax_channels.php` returns “Access denied” for a guest. Toggle before the channel table exists says “Marketplace channels are not in this database.” `seed_channels` inserts 35 partners, including `Amazon.ae`. Toggle sets `amazon` active 0. Sample seed inserts 3 SKUs and marketplace order `AMZ-402-8819201`. Sync says “Pushed 1 SKUs to amazon”. Import sets that order to `imported`, leaves `shop_order_id` null, and does not create `shop_orders`. An unknown action is “Unknown action”. No marketplace HTTP call was made. This is not a second sales-order engine.
- `/cp/content/shop/logistics/ajax_logistics.php` seeds 20 carriers. Sample shipment before `shop_orders` exists says “Orders are not in this database.” and does not create that table. After order 41 is `successfully_created` 1, the sample tracking is `JD014600012345678901` and the cost is 57.75. Toggle sets `dhl` inactive. `create_shipment` for that order inserts a second row, the message starts with “Label created:”, and the stored label URL contains `dhl.com`. The URL is stored, not fetched. An unknown carrier is “Unknown carrier”. Order 99 is “Order not found”.
- `/cp/content/shop/accessories/ajax_epc_accessories_photos.php` returns “Unauthorized” for a bad admin session and “CSRF mismatch” for a wrong key. List without a listing id is “listing_id required”. An empty listing returns no photos. Seeded `pad.jpg` lists. `set_primary` sets `rotor.jpg` primary. Delete leaves one photo. Upload with no file is “No file”. `epc_acc_photos` follows the PHP `CREATE TABLE IF NOT EXISTS`. `epc_acc_listings` is not created. A real image upload was not implemented.
- `/cp/content/shop/prices_upload/ajax_epc_price_upload_history.php` with no rows contains “No upload history yet”. After `pads.csv` is inserted, the list contains that name. `export_db` for price 1 contains article `0986`. `price_id` 0 is “price_id required”. Download of a missing archive is HTML “Upload file not available”. The history HTML is the empty phrase and the filenames, not the full PHP chrome.
- `/cp/content/shop/demand_countries/ajax_epc_demand_csv.php` reuses the existing 7-country demand schema. Stats start at `total_tags` 0. Inserting `BOSCH` / `0986` / `SDN` makes the total 1, and `country_parts` for `SDN` returns that part. `ZZZ` is “Unknown country code”. `ARE` is “ARE is UAE stock pool — not a demand market”. Preview with no path is “Missing file path”. Any posted path is “Invalid file path” and the tag count stays 1. The CSV file was not read. This is not a second demand engine.
- `/cp/content/shop/parts_agent/ajax_epc_parts_agent_cp.php` with no content row is “CP page not registered. Run epc-parts-agent-cp-setup.php”. A content row without `content_access` says “Content access is not in this database.” An empty ACL is `2388` before CSRF. After the ACL, a missing session table says “Parts agent sessions are not in this database.” and `epc_parts_agent_session` is not created. After the test creates that table, list total is 1 for session `s1` and `auto_synced` is 0. A missing detail is “Session not found”. An unknown action is “Unknown action”. File sync was not implemented. Only a type-1 admin session passes the staff gate.
- Left open on purpose, with the reason: `/cp/content/shop/finance/erp/ajax_erp.php` and `ajax_erp_endpoint.php` (ERP journals); `/cp/content/shop/customer_mgmt/ajax_customer_mgmt.php`, `/cp/content/users/ajax_customer_mgmt.php`, and their `_endpoint.php` twins (`epc_erp_customer_settlement` / e-invoice); `/cp/content/shop/finance/ajax_currency_live_rates.php` (live FX); `/cp/content/shop/prices_upload/ajax_epc_pyprices_health.php` (pyprices process); `/cp/content/control/version_control/ajax/ajax_query_to_server.php` (update-server HTTP); `/cp/content/shop/quote_requests/ajax_epc_quote_alt_options.php` (storefront cross HTTP). POS `ajax_pos.php` `complete_sale` was not ported because it would post a sale. Price-import writers that wipe supplier prices were not ported. `ajax_order_pay_refund.php`, `ajax_epc_orders_oms.php`, and `ajax_epc_orders_detail_pane.php` stay unmapped. CRM ajax stays open because its access path is ERP. Document control stays open because save bumps an ERP row version and can sync e-invoice. SAO `ajax_exec_action.php` stays open because success includes a supplier handler script.
- `CpLocalAjax_OnThrowawayDatabase_ThenDropped` created that database and dropped it. After the suite, `SHOW DATABASES LIKE 'ecomae_cpw_%'` was 0. `docpart.users` stayed 2 and `ecomae.users` stayed 2. No `epc_erp%` table was created. No ucats, SMTP, notify, Laximo, crossbase, or supplier HTTP call was made. `epc_erp_order_fulfillment` is not called. ERP posting was not edited.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5059 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, PHP source, and the platform-host full ERP mirror. Three storefront files stay unmapped (the UCatalog include, the returns helper, and the ERP modules ajax). Forty-five Control Panel ajax files in the shop, users, and requests set stay unmapped. The broader `cp/content` ajax scan still has 67 unmapped. The ERP and external-HTTP files named above stay open for that reason.

### Checkpoint 2026-10-05 — CP lang writes, synonyms, groups, returns, and storage toggle

Not complete.

- Same storefront and API comparison as the previous checkpoint: PHP files under `content/shop`, `content/users`, `content/requests`, `modules`, and `api` whose name contains `ajax`, that live in an `ajax` directory, or that live under `api`. 112 files. Path constants and quoted `.php` route strings, after comments are removed, still map 109. Three stay unmapped on purpose: `/api/UCatalog/ucatalog_index.php` (include), `/content/shop/returns/ajax/helper.php` (include), and `/content/shop/finance/epc_erp_modules_ajax.php` (ERP, not ported).
- The same Control Panel set under `cp/content/shop`, `cp/content/users`, and `cp/content/requests` whose file name contains `ajax` is still 75 files. 18 are mapped. 57 stay unmapped. The previous checkpoint had 14 mapped and 61 unmapped. Four of the new mappings are in that 75: manufacturer synonyms, warehouse groups, return decide and finalize, and the storefront storage toggle.
- A broader scan of `cp/content` files whose name contains `ajax` is 110 files, 31 mapped, 79 unmapped. The previous checkpoint had 20 mapped and 90 unmapped. Seven of the new mappings are outside the 75: six language-editor writes and search, plus the content alias check. That broader scan is not a replacement for the 75.
- `/cp/content/lang/ajax_save_string_description.php` stores raw `a <b> &` and returns `a &lt;b&gt; &amp;`. Missing sessions, language-editor pages, and text strings are named and are not created. Restricted mode cancels the write. `/cp/content/lang/ajax_set_used_found.php` refuses `9` and stores `1`. `/cp/content/lang/ajax_save_string_translation.php` inserts `Hello`, then stores raw `Hi &` and returns `Hi &amp;`. `/cp/content/lang/ajax_create_new_string.php` stores SQL NULL for `same=no`, `has_en` 0, and a key that contains `_1_`. `/cp/content/lang/ajax_delete_not_used_found.php` removes that custom string and its translation and leaves `hello`. `/cp/content/lang/ajax_search_used_found.php` returns “Error: Error searching string” when a mapped lookup table is absent, without the SQL text, and leaves `used_found` at 1. After those tables exist, `hello` stays 1 and `orphan` becomes 2.
- `/cp/content/content/ajax_check_alias.php` returns `Forbidden` for a wrong secret, `duplicated` for alias `pads` on another row, and `ok` for the same row’s alias `solo`.
- `/cp/content/shop/manufacturers_synonyms/ajax_operations.php` returns `forbidden` for a guest. Admin without CSRF is `Error! CSRF 1` and does not create `shop_docpart_manufacturers`. Add `Bosch` is a duplicate on the second call. Synonym `BOS` lists. Delete leaves both manufacturer tables at 0. Those tables follow the PHP `CREATE TABLE IF NOT EXISTS` in that script.
- `/cp/content/shop/logistics/groups/ajax_operations.php` names a missing groups table. `get_storages` returns `Sharjah` (type 4) and leaves out `Own` (type 1). An empty table contains “No warehouse groups yet.” Group `Async` with storage 6 shows `Async` and `Sharjah`, then delete leaves 0.
- `/cp/content/shop/returns/ajax/ajax_return_action.php` is the local return status, not a GL journal. CSRF mismatch is exactly `CSRF`. Missing statuses say “Return statuses are not in this database.” Finalize before a decision is “Decide every line (Approve or Deny) before closing.” Decide `1` sets item status 8, return `status_id` 2, and a log that contains “approved”. Finalize sets `return_complete` 1, `approved_sum` 20, and a log that contains `20.00`. No new item-status row is inserted.
- `/cp/content/shop/prices_upload/ajax_epc_storefront_storage_toggle.php` returns `2388` when the prices page ACL is empty. Toggle storage 6 off sets `storefront_temp_disabled` to 1 and writes one `epc_storefront_storage_toggle_audit` row for `Sharjah`. The column and audit table follow the PHP `ALTER` and `CREATE` in that script. This is not a second inventory engine.
- Left open on purpose, with the reason: `/cp/content/shop/finance/erp/ajax_erp.php` and `ajax_erp_endpoint.php` (ERP journals); `/cp/content/shop/customer_mgmt/ajax_customer_mgmt.php`, `/cp/content/users/ajax_customer_mgmt.php`, and their `_endpoint.php` twins (`epc_erp_customer_settlement` / e-invoice); `/cp/content/shop/finance/ajax_currency_live_rates.php` (live FX); `/cp/content/shop/prices_upload/ajax_epc_pyprices_health.php` (pyprices process); `/cp/content/control/version_control/ajax/ajax_query_to_server.php` (update-server HTTP); `/cp/content/shop/quote_requests/ajax_epc_quote_alt_options.php` (storefront cross HTTP). POS `ajax_pos.php` `complete_sale` was not ported because it would post a sale. Price-import writers were not ported. `ajax_order_pay_refund.php`, `ajax_epc_orders_oms.php`, and `ajax_epc_orders_detail_pane.php` stay unmapped.
- `CpNextAjax_OnThrowawayDatabase_ThenDropped` created that database and dropped it. After the suite, `SHOW DATABASES LIKE 'ecomae_cpw_%'` was 0. `docpart.users` stayed 2 and `ecomae.users` stayed 2. No `epc_erp%` table was created. No ucats, SMTP, notify, Laximo, crossbase, or supplier HTTP call was made. `epc_erp_order_fulfillment` is not called. ERP posting was not edited.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5058 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, PHP source, and the platform-host full ERP mirror. Three storefront files stay unmapped (the UCatalog include, the returns helper, and the ERP modules ajax). Fifty-seven Control Panel ajax files in the shop, users, and requests set stay unmapped. The broader `cp/content` ajax scan still has 79 unmapped. The ERP and external-HTTP files named above stay open for that reason.

### Checkpoint 2026-10-05 — Local proxy failures, license activate, and CP ajax

Not complete.

- Same storefront and API comparison as the previous checkpoint: PHP files under `content/shop`, `content/users`, `content/requests`, `modules`, and `api` whose name contains `ajax`, that live in an `ajax` directory, or that live under `api`. 112 files. Path constants and quoted `.php` route strings, after comments are removed, now map 109. Three stay unmapped on purpose: `/api/UCatalog/ucatalog_index.php` (include), `/content/shop/returns/ajax/helper.php` (include), and `/content/shop/finance/epc_erp_modules_ajax.php` (ERP, not ported).
- The same Control Panel set under `cp/content/shop`, `cp/content/users`, and `cp/content/requests` whose file name contains `ajax` is still 75 files. 14 are mapped. 61 stay unmapped. The previous checkpoint had 10 mapped and 65 unmapped. Four of the new mappings are in that 75: unread order messages, VIN count, category templates, and the order-item object.
- Six language-editor ajax files under `cp/content/lang` are now mapped and were outside that 75. A broader scan of `cp/content` files whose name contains `ajax` is 110 files, 20 mapped, 90 unmapped. That broader scan is not a replacement for the 75.
- `/api/crossbase_status.php` returns `connected` false and “Cross-reference lookup did not return usable data”. `/api/epartscross_fitment.js.php` returns the missing-article or temporarily-unavailable comment. `/api/epc_ai_parts_expert.php` answers bootstrap, CSRF, and a local stock search, and says cross-reference and catalog fitment are temporarily unavailable. `/api/epc_parts_agent.php` answers hello, an empty chat, the VIN failure sentence, and a missing session. `/api/laximo_proxy.php` returns the credentials sentence unless `epc_laximo_catalogs` already has fresh rows. `/api/prices/upload_price.php` returns `2056`, `2060`, the no-file sentence, or `2058`, and does not create `shop_docpart_prices_data`. No ucats, SMTP, notify, Laximo, crossbase, or supplier HTTP call was made.
- `/api/ajax_get_prices_settings.php` returns `Forbidden` without the tech key and redacts database and mail passwords. `/api/v1/price/lookup.php` returns `missing_api_key`, `invalid_key_format`, or `invalid_api_key`, then one AED offer and `calls_today` 1. `/api/v1/licenses/activate.php` stores status `active` and fingerprint `fp-one`, then `signing_unavailable` because no signing key is configured. A second fingerprint is `already_activated`. `/api/v1/on-premises/health.php` names a missing license table, then writes one health-log row. No signature was invented.
- `/content/shop/catalogue/ajax_epc_sku_media.php` names missing admin sessions, returns `Unauthorized` and `CSRF mismatch`, stores brand `BOSCH`, then deletes the profile. `/content/users/ajax_epc_tax_exempt_upload.php` stores `pending_review` on `users_profiles` for a wholesale account. That is a profile flag, not a second tax engine.
- The ten Control Panel ajax URLs above answer on the PHP path. Unread count is `2` and the returns query is `1`. VIN unviewed count is `1`. Multilang is `No access`, `OFF`, or `ON`. Language flags store `is_error` 1, `is_custom` 1, and `same` `en`, then NULL. Restricted mode cancels the write. Category template `Pads` is created and deleted. The order-item object returns status `Ordered` and action `Send`. A missing table is named and is not invented except where PHP itself runs `CREATE TABLE IF NOT EXISTS` before the action (`epc_sku_*`, `epc_onprem_*`, `epc_api_clients`).
- `LocalGap_OnThrowawayDatabase_ThenDropped` created that database and dropped it. After the suite, `SHOW DATABASES LIKE 'ecomae_cpw_%'` was 0. `docpart.users` stayed 2 and `ecomae.users` stayed 2. No `epc_erp%` table was created. `epc_erp_order_fulfillment` is not called. The `/cp/orders/*` dry-run routes were not changed. ERP posting was not edited.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5057 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, PHP source, and the platform-host full ERP mirror. Three storefront files stay unmapped (the UCatalog include, the returns helper, and the ERP modules ajax). Sixty-one Control Panel ajax files in the shop, users, and requests set stay unmapped. Agent chat is not the full PHP stock and catalog tree. Laximo SOAP is not called. The price-import curl is not performed.

### Checkpoint 2026-10-05 — CP order ajax and license refusal

Not complete.

- Compared PHP ajax and API files under `content/shop`, `content/users`, `content/requests`, `modules`, and `api` with ASP.NET path constants and `MapMethods` routes: 112 files, 97 already mapped, 15 still unmapped. The Control Panel copies under `cp/content` are a separate 75 files. Ten of those PHP URLs were unmapped because the dry-run twins at `/cp/orders/*` are not the PHP URL.
- `ajax_add_comment_to_log.php`, `ajax_set_orders_viewed.php`, `ajax_get_orders_info.php`, `ajax_get_cnt_for_paid_orders.php`, and `ajax_delete_orders.php` now answer on `/cp/content/shop/order_process/`. A missing sessions table says admin sessions are not in this database. CSRF 1, 3.1, and 4 match PHP. A missing log table is named and is not created. The posted comment stores user 9, `is_manager` 1, and the encoded text. Viewed flag 1 is written for order 4 and the response message is the PHP `UPDATE` text. The unviewed count is `1`. The paid-status count is `2`. Deleting a paid order is `3481` and writes nothing. Deleting unpaid order 4 removes that order and its child rows. A missing items table rolls the delete back.
- `ajax_set_user_comment.php` stores `desk & co` on user 7 after the `comment` column exists. `ajax_get_users_autocomplete.php` returns `ID 0, 3233` and `ID 7, E-mail: nora@local.test, Телефон: 050, Nora`. Missing registration fields and user profiles are named and are not created.
- `ajax_get_returns_info.php` returns status 0 for a customer. An admin count is `2` open returns, then `1` after closed caption `3798`. `ajax_check_product_alias.php` returns `false` for a taken alias and `true` for the same product. `ajax_set_users_vin_viewed.php` sets VIN 3 viewed and refuses a non-integer list with `SQL error`.
- `/api/create_license.php` is HTTP 403, “License API disabled”. No license file was created.
- `CpOrderAjax_OnThrowawayDatabase_ThenDropped` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2. No `epc_erp%` table was created. `epc_erp_order_fulfillment` is not called. The `/cp/orders/*` dry-run routes were not changed.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5056 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, PHP source, and the platform-host full ERP mirror. Contact and login-code rows are not stored, and vendor price rows are not written, because those PHP successes depend on an HTTP call that was not made. Fifteen storefront ajax and API files are still unmapped, including ucats, Laximo, and supplier proxies whose only success path is an external HTTP call. Sixty-five other Control Panel ajax files are still unmapped.

### Checkpoint 2026-10-05 — Returns, workshop, contacts, and UCatalog

Not complete.

- `ajax_load_returns_data.php` now answers on ASP.NET. A missing order-item, return-item, status, or returns table names that gap and is not created. An empty status list is `4572.`. A wrong tech key is `Forbidden`. A posted line stores status 1, user 7, sum `10.00`, and the encoded comment. The same item again is `4571`. Notify was not called.
- `ajax_workshop_public.php` creates the PHP `epc_ws_*` tables and books job `WS-` plus the day plus `-001` with plate `D-9` and status `checkin`. Tracking returns `Check-in`. A phone whose last 7 digits differ is “No job found for that reference.”
- `ajax_garage_manager.php` returns “Access denied — garage staff login required” without creating `epc_ws_jobs`. A bad admin CSRF is “CSRF failed — refresh and retry”. A staff create stores plate `G-2` as the next job number.
- `ajax_contacts_works.php` returns `4689`, `4690`, `4691`, `4693`, and `4697`. The notify HTTP call was not made, so user 7’s email stays empty.
- `ajax_sendCode.php` returns `5648` for an unknown method and `4697` for SMTP, and does not change `2fa_code`. A send inside 30 seconds is `5656` … `5647`. `ajax_checkCode.php` returns `200` for a match, `5643: 2.` after a mismatch, `5642` when the code is expired, and `4003` when no attempts remain.
- `ajax_process.php` returns the PHP login, CSRF, profile, history, warehouse, file, and part-number sentences. A file or a cross article says “Price lists are not in this database.” No price rows were written and no supplier HTTP was called.
- `ajax_vendor_ingest.php` returns the sign-in, missing-account, approval, token, file, type, and size sentences. A CSV that would be ingested is “Import failed”. `storage_id` stays 0. No stock or price table was created.
- Direct `api/UCatalog/*.php` handlers return `No access`. `api.php` without cookie `UCatalog=1` is `Forbidden 403`. `get_marks` is status false and message `2096` with no `ABARTH`, because the saved cache is older than a day and ucats HTTP was not called. `add_garage` stores caption `Daily` for user 7. `add_notepad` stores article `A&amp;1`. A missing garage or notepad table is named and is not created.
- `LocalBatch_OnThrowawayDatabase_ThenDropped` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2. No `epc_erp%` table was created. `epc_erp_order_fulfillment` is not called.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5055 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, PHP source, and the platform-host full ERP mirror. Contact and login-code rows are not stored, and vendor price rows are not written, because those PHP successes depend on an HTTP call that was not made.

### Checkpoint 2026-10-05 — Quote lines and catalogue tree

Not complete.

- Compared PHP ajax and API files under `content/shop`, `content/users`, `content/requests`, `modules`, and `api` with the ASP.NET `MapMethods` routes and `StorefrontPhpAjax` path constants. Migration catalogs and the `/storefront/*` dry-run twins were not treated as the PHP URL.
- `ajax_add_to_quote.php`, `ajax_add_to_quote_manual.php`, `ajax_quote_submit.php`, and `ajax_quote_accept.php` now answer on ASP.NET. The `/storefront/quotes/*` dry-run routes were not changed. A missing sessions table says sessions are not in this database. A guest is `auth` with `/en/users/login`. Missing quote tables say quotes are not in this database and are not created. A bad check hash is code `35` and inserts nothing. A valid type-2 line inserts user 7, status `draft`, count 1, and the next line reuses that quote. A manual `bosch & co` / `09-86` line stores `BOSCH &amp; CO`, article `0986`, and `check_hash` `manual`. Submit stores the trimmed note and status `submitted`. Accept writes the quoted price `15` and times `3` into `shop_carts` and sets the quote `accepted`. The same line again is code `already`. A missing cart table returns “Could not complete acceptance” and is not created.
- `ajax_get_brunch_items.php` and `ajax_async_tree_loader.php` read `shop_tree_lists_items`. A missing table says catalogue tree lists are not in this database. Parent 0 returns `Rotors` then `Pads`. The async loader omits `webix_kids` when the count is 0.
- `ajax_get_to_marks.php` checks CSRF, then its only list is a ucats HTTP call. With a valid session the body is `NULL`. No car list was invented. ucats HTTP was not called.
- `QuotesTreeAndToMarks_OnThrowawayDatabase_ThenDropped` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2. No `epc_erp%` table was created. `epc_erp_order_fulfillment` is not called.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5054 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, PHP source, and the platform-host full ERP mirror. Other storefront PHP URLs that this comparison still did not map include returns load, workshop public, garage manager, contacts, login codes, bulk upload, vendor ingest, and `api/UCatalog/`.

### Checkpoint 2026-10-05 — Ucats access control

Not complete.

- `/content/shop/ucats/ucats_auth_control.php` now answers on ASP.NET. The include does not call ucats. A page URL that contains `ucats` counts the client IP in `shop_ucats_auth_control`.
- With no `bot_ips` table the body is “Bot addresses are not in this database.” and the auth table is not created. With `bot_ips` present and the auth table absent, the body is “Ucats access control is not in this database.” and the table is still not created.
- After both tables exist, the first call inserts `user_id` 0 and `queries_count` 1. The next call increments to 2. Count 101 returns `Forbidden` and stays 101. Count 100 increments to 101. A bot range that covers the client IP returns `Forbidden` and inserts nothing. A row older than a day stays at count 7 and a new row is inserted at count 1.
- `UcatsAuthControl_WritesOnThrowawayDatabase_ThenDropped` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2. No `epc_erp%` table was created. `epc_erp_order_fulfillment` is not called.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5053 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — Remaining ucats pages

Not complete.

- `product.php`, `cars.php`, `cars_models.php`, `cars_models_types.php`, `parts_list.php`, `vybor_tovara.php`, and the loader pages under `content/shop/ucats/` now answer on ASP.NET. Each file dies with `No access` unless the CMS has defined `_ASTEXE_`. GET and POST return that sentence. A posted `car_name=Toyota` does not appear. No product heading and no car list are rendered. ucats HTTP was not called.
- `catalogues.php` does not call ucats. With no config it returns `Configuration not loaded.` and prints no tile. A config with no ucats flag returns an empty body. `ucats_shiny=1` prints string `4584`, href `/shop/katalogi-ucats/shiny`, and caption `4585`. An empty `ucats_oil` does not print the oil tile.
- `ucats_auth_control.php` was not ported. That include writes `shop_ucats_auth_control`.
- `UcatsPages_LocalFailureWithoutHttp_OnThrowawayDatabase_ThenDropped` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2. No `epc_erp%` table was created. `epc_erp_order_fulfillment` is not called.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5052 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, `ucats_auth_control.php` (it writes), PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — Catalogue property filters and ucats ajax

Not complete.

- A posted catalogue `properties_list` now follows `query_products_all.php` for price, int, float, bool, list, and tree when the value table exists. A missing table still says “Catalogue property filters are not in this database.” and does not invent a row.
- Category 3 with no filter counts 3 published products. Int range 10–30 counts `1` (`Pad B`, value 20) and leaves value 5 out. The full min/max counts 3. Float range 5–12 counts `1` (`Pad B`, 9.50). Bool true only counts the value 1 product. Both bools checked counts 3. List OR of option 8 counts 2. List AND of options 4 and 8 counts 1. No checked list option counts 3. Tree “All” (level 1, value 0) counts 3 even when the tree table is absent. Tree value 7 counts 1 (`Pad B`).
- A narrowed price range without `shop_storages_data`, and again without `shop_storages` and `shop_offices_storages_map`, says the same sentence. After those tables exist, range 10–30 counts `1` (`Pad A`, price 15, exist 3 on storage 8, `interface_type` 1). Price 50, the type-2 storage, the exist-0 row, and the other category stay out. Exist stays 3 and reserved stays 0. `customer_price` is the stock price (rate 1, markup 0), PHP’s else branch when no currency rate or markup row is applied. With no `shop_geo` table the first office is used.
- The fourteen `content/shop/ucats/` product and group-field scripts return JSON `null`. That is `json_encode` of a failed decode when ucats is not configured. GET and POST were checked. No product or field list was invented. ucats HTTP was not called. The other ucats pages were not ported.
- `StorefrontPhpCatalogueDemandTests` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2. No `epc_erp%` table was created. `epc_erp_order_fulfillment` is not called.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5051 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — Catalogue list, pickup timing, demand, and garage models

Not complete.

- `ajax_get_products_count.php`, `ajax_get_products_list.php`, `ajax_get_products_page.php`, `ajax_specify_office_info.php`, and the six `ajax_epc_demand_*.php` URLs now answer on ASP.NET. `ajax_get_mark_models.php` and `ajax_get_models_types.php` return `{"status":false}`. ucats HTTP was not called.
- Category 3 with block type 1 counts `1` published product. Block type 2 counts `2`. An empty page is `<div style="text-center">4078</div>`. The tile includes `Brake Pad`, `/pads/brake-pad`, `4106`, `4111`, `4099`, `3608`, and the guest price `**`. The list URL returns an empty HTML body. Search without `str_id` says catalogue text search is not in this database. `Brake` counts `1`. `zzzz` counts `0`.
- A guest pickup is `Session error`. An empty cart is `alert-success` / `4421` / `4447`. A storage that is not on the office map is `alert-danger` / `4422`. Exist stays 5 and reserved stays 1. Mapped storage with 48 extra hours is `alert-warning` / `4442` / `4443`.
- A signed-out demand call is the sign-in sentence and creates no demand table. Meta creates the PHP demand tables and 7 registry countries. Showcase without `shop_docpart_prices_data` says price lists are not in this database and seeds 0. The default card seeds `TOYOTA` / `1310154101` for Algeria, Kenya, and Sudan. Fitment vehicle count is 0 and `fitment_source` is empty. Sudan is allowed after the account is locked. Kenya says the account can only view Sudan. An admin unknown code is “Unknown country code.” A start with no stock says there are no in-stock price-list parts. After exist 4, start says “Found 1” and the vehicle list stays empty. The step says the scan is complete, product group `Piston`, and vehicles_count 0. No network vehicle list was invented.
- `StorefrontPhpCatalogueDemandTests` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2. No `epc_erp%` table was created. `epc_erp_order_fulfillment` is not called.
- ucats product and group-field scripts still 404. A posted catalogue `properties_list` says property filters are not in this database. That is not the full PHP price, int, float, bool, list, and tree query.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5050 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, ucats product scripts, catalogue property filters, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — Customer option, reviews, and VIN messages

Not complete.

- `ajax_set_user_option.php`, `ajax_set_my_city.php`, `ajax_add_evaluation.php`, `ajax_get_product_evaluations.php`, `ajax_get_product_general_mark.php`, `content/requests/ajax_get_message.php`, `content/requests/ajax_send_message.php`, and `ajax_check_order_not_authorized.php` now answer on ASP.NET. The retired guest-order script returns an empty body. A city cookie is `my_city=12` and the body is `1`. `selected_manufacturer` stores the raw value for user 7 and session 12. A missing `users_options` table says the options are not in this database. A category key without `shop_catalogue_categories` says the categories are not in this database and inserts nothing.
- A guest review is `4088`. A stored review keeps `a &lt;b&gt; &amp; &quot;`. A second review from the same user is `4089`. The list without `users_profiles` says profiles are not in this database. A hidden name is `4091`. Marks 4 and 5 return `general_mark` `5`. A sort other than `asc` or `desc` is an empty body.
- Another user’s VIN thread is `Forbidden` code 501. `manager=1` without an admin session is 501. The owner’s messages are a JSON array. A customer send sets `viewed` to 0. An admin session with `type` 1 sends `is_customer` 0 and sets `viewed_customer` to 0. No mail was sent. `epc_erp_order_fulfillment` is not called.
- `StorefrontPhpCustomerTests` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2.
- Catalogue product list, count, and page ajax, pickup timing, ucats product scripts, demand ajax, and garage model lookup still 404. The garage model lookup calls ucats. ucats HTTP was not called.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5049 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, catalogue product-list ajax, ucats and demand ajax, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — Type-1 checkout order detail copy

Not complete.

- A type-1 checkout copies the catalogue line onto `shop_orders_items`: product id 9, manufacturer `BOSCH`, article `C110X`, article show `C110-X`, name `Pad`, count 4, `t2_price_purchase` 0. `sao_state` and `sao_robot` store storage id 8, the value PHP binds for type 1. `t2_product_json` is copied.
- Each `shop_carts_details` row is copied onto `shop_orders_items_details`. Record 1 keeps `count_reserved` 2 and purchase `4.00`. Records 3 and 4 keep `count_reserved` 1 and purchase `6.00` (warehouse `price_purchase` times the currency rate). `count_issued` and `count_canceled` are 0. PHP does not update `shop_storages_data` at checkout, so exist and reserved stay at the cart reservation. The checked cart and the copied detail ids are deleted. The unchecked `Keep` line stays. `paid` stays 0.
- While `shop_orders_items_details` is absent, checkout returns “4492. Order item details are not in this database.”, rolls the order back, and leaves the cart and the stock reservation. No detail row is invented.
- The order email logs are `FAILED after retry` and `FAILED`. No log says sent. No SMTP server and no live host were called. `epc_erp_order_fulfillment` is not called, and no `epc_erp%` table was created.
- `StorefrontPhpShopTests` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5048 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a delivered SMTP message, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — Other-office stock, catalogue captions, and unpaid order mail

Not complete.

- A type-1 increase that the current cart detail cannot cover now follows PHP sections 2.1 and 2.2. In-stock supply id 3 is reserved first. Expected supply id 4, with a future arrival time, is reserved second. Both new detail rows are office 2. The stored purchase is warehouse price `10.00`. The detail `price` stays `0.00` because that PHP insert does not write it.
- A type-1 add while `shop_properties_values_text` is absent returns “Catalogue article properties are not in this database.” and does not change stock. With the Russian property map seeded, the add stores manufacturer `BOSCH`, article `C110X`, article show `C110-X`, and name `Pad`. Article search `C110` returns catalogue row `C110-X` / `Bosch` / `Pad`. Search `S56` before that table exists still returns the clear message plus the price and standard rows already found.
- Checkout writes `Order email to admin admin@127.0.0.1: FAILED after retry` and `Order email to customer (user #7): FAILED`. A customer order message still returns plain `true` and writes `Order message email to admin admin@127.0.0.1: FAILED`. No log says sent. No SMTP server and no live host were called. `epc_erp_order_fulfillment` is not called.
- Paying order 90 for 15 inserts operation code `4_income_for_direct_pay`, income 1, active 0, `pay_orders` `90`. `pay_system` is false when the requested handler is not configured. `shop_orders.paid` stays 0. A partial amount and an amount above the debt are `Forbidden`.
- `get_table_cars` for a customer with no cars stays `5609`. The owned car table includes `edit_car(4)` title `2270`, `delete_car(4)` title `2224`, and `Toyota - Corolla`. A linked order uses `check_car(0, 4)`; after unlink it uses `check_car(1, 4)`.
- `StorefrontPhpShopTests` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2.
- The proved checkout order was still type 2, so a type-1 order detail copy was not written.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5048 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, ERP fulfillment from checkout, a type-1 order detail copy, a delivered SMTP message, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — Shop ajax and catalogue cart stock

Not complete.

- `ajax_add_to_notepad.php`, `ajax_operations_cars.php`, `ajax_checkout_create.php`, `ajax_get_order_messages.php`, `ajax_send_message.php`, `ajax_create_operation.php`, `ajax_check_items_returns.php`, `ajax_get_article_list.php`, and `ajax_check_for_order.php` now answer on ASP.NET. Guest notepad is `2063`. Another user’s car is `2064` or `No Access`. A saved notepad line stores the HTML-encoded article and a comment that starts with `4225`. Garage search, check, active, and delete write only that user’s rows. Empty garage HTML is `5609`. Checkout without the agreement cookie is `4492. 4471`. A checked type-2 line becomes an order and leaves the unchecked cart line. Staff email and `epc_erp_order_fulfillment` are not called. Order messages for another user are `Forbidden` code 501. Send returns plain `true` and stores the encoded text. A balance top-up inserts an inactive income row. Amount 0, a guest, another user’s order, and an already paid order are refused. Returns counts match the seeded rows. An empty article list returns the recent query plus names `4194`, `4195`, and `4196`. A missing stat table says the queries are not in this database and the list is empty. Check-for-order toggles the flag. A type-1 add with the PHP hash reserves warehouse stock, a later increase and decrease move that same detail, and delete releases it. A type-1 add with no hash is code `35` message `4462` and does not change stock.
- `StorefrontPhpShopTests` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2.
- Type-1 quantity increase does not search other offices. Type-1 add stores the catalogue caption and does not run the Russian property-map manufacturer/article SQL. Article search returns a clear message when `shop_properties_values_text` is absent. Checkout email, staff message email, gateway capture, and a successful order-pay insert were not proved. `get_table_cars` is a short table, not the full PHP button markup.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5048 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, other-office catalogue stock, checkout email, ERP fulfillment from checkout, gateway capture, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — SKU media lookup and type-2 cart writes

Not complete.

- `ajax_epc_sku_media_public.php`, `ajax_add_to_basket.php`, `ajax_change_count_need.php`, and `ajax_delete_cart_record.php` now answer on ASP.NET. Unknown SKU action is `Unknown action`. A missing SKU table says “SKU media is not in this database.” No `epc_sku_*` tables are created and UMAPI is not called. A stored photo and spec return the PHP URL and `12 mm`. A type-2 add stores `BOSCH` / `0986` / `Pad` and returns `{status:true}`. Duplicate, below-cost, and a bad hash stop before another insert. A blocked guest cannot add or change quantity. Quantity follows the PHP same-count, not-enough, and minimum-order codes. The translated sentences are not in this database, so messages `4467`, `4468`, and `4469` stay the string keys. Delete of another user’s line is `Alien cart`. A type-2 delete removes only `shop_carts`. Catalogue type 1 does not write `shop_storages_data`.
- `/ru/parts/{brand}/{article}`, `/ar/parts/...`, and `/me/parts/...` were already 200 on this branch. This pass did not re-port language prefixes.
- `StorefrontPhpCartTests` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2.
- Notepad, garage cars, checkout create, order messages, send message, finance create, returns check, article list, and check-for-order are still not routed. Checkout and finance were left alone.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's posting stays with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5047 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, the remaining shop ajax URLs, catalogue cart stock, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — Remaining part-search ajax and blocked guest cart

Not complete.

- `ajax_getManufacturersList.php`, the prices list, the cross-server list, `ajax_getAnalogsList.php`, `ajax_asynchron.php`, `ajax_get_info.php`, and `ajax_getProductsOfBunch2.php` now answer on ASP.NET. An empty analogs request is `result` 0, `check` 1, and an empty list. A throwaway price row returns `BOSCH` / `Pad`. The local cross table returns `BOSCH` and analog `0987` / `MANN`. A missing price or cross table returns a clear message. A supplier storage returns “Storage handler error (get manufacturers)”. Bunch 2 with office 0 hides the guest price. Another office returns “Storage handler error”. A foreign referer is `Forbidden 403`. With ucats part info off, including an image path, the body is `{"result":0}`. ucats and supplier HTTP were not called. Async messages `4192` and `4193` are the string keys because `lang_text_strings` is absent. A blocked guest cart deletes that session’s cart and detail rows and leaves the other rows. The empty-cart words for string 4494 are not in this database, so the sum stays blank.
- `StorefrontPhpPartSearchTests` created that database and dropped it. No `ecomae_cpw_%` database remained. `docpart.users` stayed 2 and `ecomae.users` stayed 2.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5044 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — Storefront PHP search and header ajax

Not complete.

- PHP part-search and desktop header URLs that were 404 now return JSON: warehouse offers, article brands, cross search, office/storage bunches, products of one bunch, cart info, unread order/return messages, and unread VIN requests. An empty article is “Empty article”. Guests do not receive warehouse price, quantity, storage, or term. Cross-search stock uses the `**` mask. A missing table returns a clear message. Header calls follow PHP CSRF (`Error! CSRF 1` and `Error! CSRF 4`).
- `StorefrontPhpAjaxTests` creates a database, counts cart total 20.00, one unread order message, and one unread VIN request, serves the cart and warehouse URLs from a local test host, then drops that database. No `docpart` rows were written. A blocked guest cart is not deleted.
- `ajax_getManufacturersList.php`, the prices and cross-server manufacturer lists, `ajax_getAnalogsList.php`, `ajax_asynchron.php`, `ajax_get_info.php`, and `ajax_getProductsOfBunch2.php` are still not routed.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5040 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, the remaining part-search ajax files, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — New-account OAuth and named-shop host resolution

Not complete.

- PHP `epc_oauth_complete_login` provisions a storefront or CP user on first sign-in. `OAuthAccountProvision` does the same. `OAuthAccountProvisionTests` creates a database and drops it. A locked user is refused. An existing unconfirmed storefront user is confirmed in place. An existing CP user needs a backend group. A new storefront user and a new CP user are inserted. Signup disabled (Super CP, industries, `allow_provision` false) inserts nothing. An empty or non-email address is refused. A missing `users` table returns “Accounts are not in this database.” No `docpart` rows were written. This environment has no `epc_oauth_config`, so a live Google browser return was not run.
- `ecomae.epc_portal_tenants` has no row for www.electronicae.com, www.stylenlook.com, www.thejewellerytrend.com, www.taxofinca.com, or industries.ecomae.com. No accounts were created and no portal rows were inserted. PHP already names those four shops and binds them to shared `docpart` when no dedicated database is stored. ASP.NET now does that bind. A dedicated registry database still wins. `industries.ecomae.com` `/cp` and `/erp` use platform `ecomae`. That host is not a Super CP host. `CpTenantHostResolutionTests` signed in on a throwaway database that stood in for the resolved name, then dropped it. `docpart.users` stayed 2 and `ecomae.users` stayed 2.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5033 passed, 0 failed.

Still open: the web tracker connection, jewellery enablement, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — Price upload, catalogue writes, and groups-tree delete

Not complete.

- `CpRow3ThrowawayTests` creates a database and drops it. PC file import and the wizard column layout wrote article 0986 / brand BOSCH. FTP, email, and URL updates returned the PHP validation messages and did not download. A cron schedule opened a launch and finished it. The deploy API listed the probe list and uploaded “Deploy probe”. After the price-list table was dropped, those channels said the lists or schedules are not in this database. Catalogue min-limit 2.50 saved, template “Pads” was created and deleted, and a missing catalogue table returned a clear message. The groups save deleted Wholesale, which was not in the posted tree, and kept Guests, Retail customers, and Administrators. A missing `groups` table returned “Groups are not in this database.” No `docpart` rows were inserted or wiped.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled. The other named shops are still not in `epc_portal_tenants`. No accounts were created.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5013 passed, 0 failed.

Still open: new-account OAuth provisioning, the web tracker connection, jewellery enablement, signed-in CP on the other named shops, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — CP search tab, language, settings, office, and user saves

Not complete.

- Signed-in www.epartscart.com `confirmWrites=true`. `/cp/search-tabs/write` is **400** “Search tabs are not in this database.” `/cp/lang/create-string` and `/cp/lang/save-translation` are **400** “Language tables are not in this database.” `/cp/config-items/write` redirects with “Settings cannot be saved because config items are not in this database.” A dry-run search-tab post stays **200** and writes nothing.
- `CpWriteThrowawayDbTests` creates a database, saves search tab 7 as disabled, an office caption “Probe HQ”, and user email `saved@local.test` while `users_profiles` and group bindings are absent, then drops that database. No `docpart` rows were inserted. Price upload, catalogue writes, and the groups-tree save were not posted.
- `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled. The other named shops are still not in `epc_portal_tenants`. No accounts were created.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5012 passed, 0 failed.

Still open: new-account OAuth provisioning, price upload, catalogue writes, the groups-tree save, the web tracker connection, jewellery enablement, signed-in CP on the other named shops, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — CP search tab, office, warehouse, and user detail opens

Not complete.

- Signed-in www.epartscart.com. `/cp/search-tabs-app?tab_id=1` is **200** and says the tab was not found, then that there are no search tabs (`shop_docpart_search_tabs` and `lang_text_strings_translation` are absent). `/cp/offices-app?office_id=1` is **200** and edits Dubai HQ (UAE, Dubai, Al Quoz 3). Region, coordinates, description, and working hours stay empty because those columns are absent. `/cp/offices-app?office_id=new` and `/cp/storages-app?storage_id=new` are **200** and list operator@local.test. `/cp/storages-app?storage_id=1` is **200** for Sharjah Industrial 6 warehouse. An empty `bg_line_color` is 0, the same as PHP `intval`. `/cp/users-app?user_id=1` is **200** for operator@local.test. `reg_variant`, `comment`, registration tables, and `users_profiles` are absent, so those fields stay empty. No rows were inserted.
- `confirmWrites=true` saves, price upload, and catalogue writes were not posted. `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- www.electronicae.com, www.stylenlook.com, www.thejewellerytrend.com, and www.taxofinca.com are still not in `epc_portal_tenants`. No accounts were created. In-repo marketing snapshots were already **200**.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5009 passed, 0 failed.

Still open: new-account OAuth provisioning, Control Panel saves that write rows, the web tracker connection, jewellery enablement, signed-in CP on the other named shops, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — CP data transfer, packs, KKT, growth, page builder, prices send, RMA, SAO, WMS

Not complete.

- Signed-in www.epartscart.com, all **200**, no MySQL banner. `/cp/data-transfer-app` lists Dubai HQ and the four groups; catalogue categories are empty (`shop_catalogue_categories` is absent). `/cp/prices-send-app` lists `prices@local.test` and `operator@local.test` with empty names (`users_profiles` is absent). `/cp/industry-packs-app`, `/cp/marketing-growth-app`, `/cp/page-builder-app`, `/cp/returns-rma-app`, and `/cp/warehouse-wms-app` show zero counts and no records. `/cp/kkt-app` opens the cashier form with empty devices (`shop_kkt_devices` is absent). `/cp/sao-app` says no SAO states are defined (`shop_sao_states` is absent). No rows were inserted.
- A rescan of 86 signed-in menu links found no missing-table banner. `/cp/web-tracker-app` still says no tracker database connection is available. Jewellery apps stay **404** because that industry module is not enabled.
- www.electronicae.com, www.stylenlook.com, www.thejewellerytrend.com, and www.taxofinca.com are still not in `epc_portal_tenants`. No accounts were created. In-repo marketing snapshots were already **200**.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5005 passed, 0 failed.

Still open: new-account OAuth provisioning, CP writes, the web tracker connection, jewellery enablement, signed-in CP on the other named shops, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — CP groups, settings, languages, payments, statistics, returns, search tabs

Not complete.

- Signed-in www.epartscart.com `/cp/groups-app` is **200** and lists the four stored groups (Administrators, Guests, Retail customers, Wholesale). `lang_text_strings_translation` is absent, so captions come from `groups.value`. `/cp/config-items-app` is **200**, source `database`, 0 groups (`config_groups` absent). `/cp/languages-app` is **200** with no installed languages and 0 strings (`lang_languages` absent). `/cp/payment-gateways-app` is **200** with Active default None and region totals 0 (`shop_payment_systems` absent). `/cp/statistics-app` is **200** with query counts 0 (`shop_stat_article_queries` absent). `/cp/returns-app` is **200** and says there are no return requests (`shop_orders_items_statuses_ref` absent). `/cp/search-tabs-app` is **200** and says there are no search tabs (`shop_docpart_search_tabs` absent). No rows were inserted.
- www.electronicae.com, www.stylenlook.com, www.thejewellerytrend.com, and www.taxofinca.com `/cp/login` stay **200**. The epartscart admin session does not open `/cp/groups-app` there (**302** to `/cp/login`). They are not in `epc_portal_tenants`. No accounts were created.
- Menu pages that still print a missing-table error: `/cp/data-transfer-app`, `/cp/industry-packs-app`, `/cp/kkt-app`, `/cp/marketing-growth-app`, `/cp/page-builder-app`, `/cp/prices-send-app`, `/cp/returns-rma-app`, `/cp/sao-app`, `/cp/warehouse-wms-app`. In-repo marketing snapshots were already **200**. VIN `email.png` and `op_*.png` are still not in the repo.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5005 passed, 0 failed.

Still open: new-account OAuth provisioning, the rest of authenticated CP (the menu pages above, writes, jewellery industry gate), signed-in CP on the other named shops, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-05 — CP catalogue, SEO, HR, modules

Not complete.

- Signed-in www.epartscart.com `/cp/product-catalogue-app` is **200** with zero products, published, unpublished, and categories. `shop_catalogue_products` is absent, so the page no longer prints that MySQL error. `/cp/seo-app` is **200** with source `database` and zero content counts (`content` is absent). `/cp/hr-overview-app` is **200** and says there are no HR records (`epc_erp_hr_records` is absent). `/cp/modules` is **200** `source: database`, count 0. `is_frontend` is not a column and the `modules` table has no rows. No rows were inserted.
- www.electronicae.com, www.stylenlook.com, www.thejewellerytrend.com, and www.taxofinca.com `/cp/login` stay **200**. The epartscart admin session does not open those hosts' catalogue pages (**302** to `/cp/login`). They are not in `epc_portal_tenants`. No accounts were created.
- Every in-repo www.ecomae.com marketing snapshot path probed this pass was already **200**. VIN `email.png` and `op_*.png` are still not in the repo.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5004 passed, 0 failed.

Still open: new-account OAuth provisioning, the rest of authenticated CP, signed-in CP on the other named shops, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-04 — CP users JSON digest

Not complete.

- Signed-in www.epartscart.com `/cp/users` is **200** with `source: database`, count 2, and the two `docpart.users` rows (`prices@local.test`, `operator@local.test`). `time_registered` and `time_last_visit` are not columns, so those fields are 0, matching PHP `epc_dl_customers`. No users were inserted.
- `/cp/users-app` still lists the same accounts. This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5002 passed, 0 failed.

Still open: new-account OAuth provisioning, the rest of authenticated CP, every tenant CP page, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-04 — CP users list

Not complete.

- Signed-in www.epartscart.com `/cp/users-app` is **200** and lists the two accounts already in `docpart.users` (`operator@local.test`, `prices@local.test`). A missing `reg_fields` table and missing `users` columns (`reg_variant`, `time_registered`, `time_last_visit`, `admin_created`) no longer abort the list. Those cells stay empty (`0`, `—`, `never`, `No`). Balances come from `shop_users_accounting` when that table exists. No users were inserted.
- `/cp` home was already the command centre. The JSON digest `/cp/users` still returns `source: database-error` because it selects `time_registered`. Other signed-in CP pages are not in this checkpoint.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5001 passed, 0 failed.

Still open: new-account OAuth provisioning, the rest of authenticated CP (including `/cp/users` JSON), every tenant CP page, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-04 — Power BI row datasets

Not complete.

- Anonymous calls to `/epc-api/v1/powerbi/kpis`, `orders`, `sales`, `stock`, `gl`, and `metrics` stay 401 `missing_api_key`. An unknown key stays 401 `invalid_api_key`.
- A `read:erp` key no longer gets 503 `erp_unavailable`. `kpis` returns 500 `internal_error` when `shop_orders` is missing, because PHP does not catch `epc_erp_dashboard`. `orders` returns 200 with `orders_unavailable` and no rows. `sales` and `stock` return 200 with the missing-table message and no rows. `metrics` returns 200 with `bi_query_failed` and no rows. `gl` returns 200 with the non-zero trial-balance lines already stored on `epc_erp_coa_accounts` and `epc_erp_gl_lines`. No rows were inserted.
- This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 5000 passed, 0 failed. Filtered `PublicAnonymousPageParityTests`: 49 passed.

Still open: new-account OAuth provisioning, authenticated CP, every tenant CP page, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-04 — keyed ERP dashboard summary

Not complete.

- A missing `X-API-Key` on `/epc-api/v1/erp/dashboard-summary` stays 401 `missing_api_key`. An unknown key stays 401 `invalid_api_key`.
- A key with `read:erp` opens that key's tenant database and runs the existing read-only dashboard KPI query. The public JSON is PHP's envelope (`tenant_site_key`, `period.from`/`to`, nine KPIs). Command-center tiles are not included. This checkpoint did not edit ERP posting and does not create schema.
- Local `ecomae` and `docpart` have no `shop_orders` table. The valid-key call returns 500 `internal_error` / `API request failed.`, which is PHP's outer catch when that query throws. No KPI numbers were invented.
- Power BI row datasets (`kpis`, `orders`, `sales`, `stock`, `gl`, `metrics`) stay 503 `erp_unavailable` after a valid key. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 4999 passed, 0 failed. Filtered `PublicAnonymousPageParityTests`: 48 passed.

Still open: new-account OAuth provisioning, authenticated CP, every tenant CP page, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, Power BI row datasets, and the platform-host full ERP mirror.

### Checkpoint 2026-10-04 — warehouse shards and public APIs

Not complete.

- www.epartscart.com `/sitemap-wh-0.php` through `/sitemap-wh-79.php` return a urlset of `/en/parts/{BRAND}/{ARTICLE}`. Local stock fills shard 0 only, so `/sitemap-wh-1.php` is an empty urlset and the tenant sitemap index lists `sitemap-wh-0.php`. `/sitemap-wh-80.php` stays 404. A missing price table stays an empty urlset.
- `/epc-api/v1` and `/health` return the PHP health JSON when the platform database opens. `/capabilities` and `/openapi.json` are public. Keyed routes without a key return 401 `missing_api_key`. An unknown key returns 401 `invalid_api_key`. An unknown path returns 404 `not_found` JSON.
- `/api/v1/catalog` and `/api/v1/catalog.php` without `action` return 400 `missing_action`. A known action with no key returns 401 `missing_api_key` on the existing catalog route. A malformed key returns 401 `invalid_key_format`.
- A valid key on `/epc-api/v1/erp/dashboard-summary` and the Power BI row datasets returns 503 `erp_unavailable`. Those dataset bodies are not ported. This checkpoint did not edit ERP posting. Devin's #1971–#1983 stay merged. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 4998 passed, 0 failed.

Still open: new-account OAuth provisioning, authenticated CP, every tenant CP page, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, keyed ERP dashboard and Power BI row datasets, and the platform-host full ERP mirror.

### Checkpoint 2026-10-04 — public sitemaps, promo redirects, storefront assets

Not complete.

- www.ecomae.com serves `/sitemap-industries.php`, `/sitemap-industries.xml`, `/sitemap-marketing.php`, `/sitemap-index.php`, `/sitemap-pages.php`, and `/sitemap-products.php`. Industry hosts 302 the industries map to www. www.epartscart.com `/sitemap-index.php` lists pages and products. Product hubs include in-stock brands; a missing CMS `content` table falls back to public storefront hubs instead of HTTP 500. `/robots.txt` on the marketing host also names `sitemap-industries.php` and `sitemap-index.php`. Warehouse `sitemap-wh-N.php` shards are not routed.
- Marketing host `/akciya`, `/en/akciya`, and `/promotion` return 301 `/` with `X-Robots-Tag: noindex`. `/en` and `/en/terms` stay 200. The same promo path on www.epartscart.com stays 404.
- In-repo storefront files that PHP links and ASP.NET was 404ing now return 200: storefront animations CSS/JS, VIN hystmodal CSS/JS, `vin_zapros.css`, and `/lib/jQuery_ui/jquery-ui.css` and `jquery-ui.js`. `email.png` and `vin.png` are not in the repo.
- Devin's #1971–#1983 are merged. This checkpoint did not edit ERP posting. `pf_seed`, `pf_clear`, `pf_sync-orders`, `opl_create_pos`, and `opl_autoplan` stay with Devin.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 4991 passed, 0 failed.

Still open: new-account OAuth provisioning, authenticated CP, every tenant CP page, production deploy, missing VIN `email.png` and `op_*.png`, PHP source, warehouse sitemap shards, `/epc-api/v1` and `/api/v1/catalog`, and the platform-host full ERP mirror.

### Checkpoint 2026-10-04 — storefront demo, CP brochure, public ERP sample

Not complete.

- www.ecomae.com `/shop` and `/shop/` return the public storefront already served at `/en`. `/shop/cart` is unchanged. www.epartscart.com `/shop` stays 404.
- www.epartscart.com `/brochure-cp`, `/brochure/cp`, and `/en/brochure-cp` return the PHP epartscart Control Panel brochure (client deck by default; scope and view query strings select the other renders). www.ecomae.com `/brochure-cp` stays the ECOM AE snapshot.
- `/erp-demo`, `/shop/erp-demo`, and `/en/erp-demo` return the read-only sample dashboard (`epc_demo_kpis`). Industry query switches jewellery, trading, construction, retail, and manufacturing. No seed, clear, or posting. The marketing-host full Super ERP mirror remains the signed-in `/erp` shell.
- `origin/main` through #1983 is merged into this branch. This checkpoint did not edit ERP posting. `pf_seed`, `pf_clear`, and `pf_sync-orders` stay dry-run.

`dotnet test aspnet/tests/EcomAE.Platform.Tests`: 4978 passed, 0 failed.

Still open: new-account OAuth provisioning, authenticated CP, every tenant CP page, production deploy, missing marketing screen PNGs and VIN `email.png`, PHP source, and the platform-host full ERP mirror.

### Checkpoint 2026-10-04 — storefront and CP install files

Not complete.

- www.epartscart.com serves `/manifest.webmanifest`, `/sw.js`, `/icons/pwa-icon-192.svg`, and `/icons/pwa-icon-512.svg`. The storefront head emits the same tags as `templates/nero/desktop.php` when the host is epartscart.com. www.ecomae.com does not.
- `/cp/manifest.webmanifest`, `/cp/sw.js`, `/cp/offline.html`, and the two CP icons answer without a login, matching `epc_cp_pwa_maybe_serve_asset`. This is not a signed-in Control Panel.

Still open: new-account OAuth provisioning, authenticated CP, production deploy, ERP posting, VIN `email.png`, tenant `/brochure-cp`, `/erp-demo`.

### Checkpoint 2026-10-04 — public accessories search

Not complete.

- www.epartscart.com `/en/accessories` calls `/content/shop/docpart/ajax_epc_accessories_search.php`. That anonymous route now returns the PHP marketplace JSON (`status`, `items`, `facets`, `currency_default: AED`). With no published ads the body is an empty catalog and the page says the categories are ready. Categories, makes, and UAE cities are seeded from `epc_pakwheels_accessories_taxonomy.json` when those tables are empty, the same way PHP `epc_acc_marketplace_search` does.
- Snapshotted www.ecomae.com marketing URLs from the PHP catalog (documentation, compare, bos articles, solutions, legal, industries, free tools, brochure) were already 200. Brochure cards use the process SVG from the previous checkpoint.

Still open: new-account OAuth provisioning, authenticated CP, production deploy, ERP posting, VIN `email.png` (file not in the repo), tenant `/brochure-cp` (no epartscart snapshot), `/erp-demo`.

### Checkpoint 2026-10-04 — brochure illustrations, favicon, UMAPI logos

Not complete.

- `/favicon.ico` and `/favicon.svg` serve the repo files browsers request on every page.
- Brochure process cards no longer 404. `/content/general_pages/epc_brochure_process_photo.php` and `/platform-assets/brochure-process.svg` return the PHP topic SVG. Marketing HTML rewrites the `.php` image URL.
- `/api/umapi_image.php` proxies supplier and manufacturer logos the way PHP does (400 for a bad id, image bytes from `image.umapi.ru`).

Still open: new-account OAuth provisioning, authenticated CP, production deploy, ERP posting, VIN `email.png` (file not in the repo), tenant `/brochure-cp` (the epartscart-branded brochure is not snapshotted; www.ecomae.com `/brochure/cp` already is).

### Checkpoint 2026-10-04 — homepage catalog cache and /platform/tools

Not complete. Homepage list actions no longer forward the rejected UMAPI key as HTTP 402.

- `/api/umapi_proxy.php` creates the PHP `epc_umapi_*` cache tables when they are missing (`CREATE TABLE IF NOT EXISTS`, same statements as `epc_ensure_cache_tables`). `action=suppliers` then returns the PHP stock-brand payload from `epc_umapi_brands` plus in-stock rows in `shop_docpart_prices_data` (`source: database`). `action=manufacturers`, `models`, and `modifications` return **200 `[]`** when that cache is empty, so the homepage widgets do not throw on `Payment Required`. TecDoc makes are not invented from parts brands. VIN and other live actions still forward the upstream status. This is not a full port of the UMAPI proxy.
- www.ecomae.com `/platform/tools` serves the existing free-tools snapshot. PHP maps that path to the same page as `/platform/free-tools`.

Still open: new-account OAuth provisioning, authenticated CP beyond login, production deploy, ERP posting, VIN `email.png` if the file is not in the repo.

### Checkpoint 2026-10-04 — OAuth callback, home widgets, shop privacy, marketing customers

Not complete. This slice only closes the next 404s on the same branch.

- `/api/epc_oauth_callback.php` (GET and POST) and `/epc-auth-handoff.php` are ASP.NET. Missing or bad state is **400** HTML (`Missing sign-in parameters.` / `This sign-in link has expired.`). A valid state with no local credentials is **503** `Google sign-in is not configured.` (the start route stays **422**). When credentials exist, the code is exchanged and an existing confirmed user gets a session cookie or a cross-host handoff. New-account provisioning is still PHP.
- Homepage `/api/umapi_proxy.php` and `/content/shop/docpart/ajax_epc_product_family.php` return JSON (catalog rows, PHP validation errors, or the upstream catalog JSON). Local product-family summary is 200. The next checkpoint stops forwarding **402** for the homepage brand and vehicle lists. This is not a full port of the 2000-line UMAPI proxy.
- www.epartscart.com `/privacy` and `/en/privacy` serve the same privacy snapshot as www.ecomae.com `/privacy`.
- www.ecomae.com `/customers` serves the PHP customer-results snapshot (`/platform/customer-results` remains the canonical path).

Still open: VIN `email.png`, authenticated CP workflows, every tenant CP page, production binary / 525 hubs, ERP posting 0/15, PHP source not decommissioned.

### Checkpoint 2026-10-04 — OAuth start, storefront shell assets, lowercase CP modules

Local Kestrel (www.epartscart.com, port 5080). Migration is **not** complete.

- Customer and CP login buttons still use `/api/epc_oauth_start.php`. That URL was 404. It now follows PHP: 400 unknown provider, 422 when the provider has no credentials (local Google is 422, not a splash), 302 to the provider when credentials exist. The callback `/api/epc_oauth_callback.php` is still 404. Session minting is not ported.
- `/modules/slider/css/style.css` and `/assets/media/logos/ecomae_mark.svg` were 404 on every storefront/CP shell. They are served from the repo / inline mark.
- Nested lowercase CP module URLs such as `/cp/shop/payments/payments` and `/cp/shop/orders/orders` 302 to the existing ASP.NET apps. Single-segment routes (`/cp/orders`, `/cp/users`, `/cp/login`) stay. ERP posting was not edited.

Still 404 on the storefront home widgets: `/api/umapi_proxy.php`, `/content/shop/docpart/ajax_epc_product_family.php`, `/content/general_pages/vin_zapros/email.png` (file is not in the repo). `/en/privacy` is 404 on tenant hosts.

### Checkpoint 2026-10-04 — named tenant homes and industries.ecomae.com

Local Kestrel: bare `/` on electronicae, stylenlook, thejewellerytrend, and taxofinca (apex and www) 404'd because only epartscart.com was rewritten to `/storefront/app`. Those hosts now use the same storefront rewrite. `/cp` already redirected to the branded CP login. `industries.ecomae.com` `/` now serves the `/platform/industries` snapshot (`X-EcomAE-Industry-Showcase: directory`). The 28 `{slug}.ecomae.com` hubs were already snapshots.

### Checkpoint 2026-10-04 — storefront lang aliases and marketing industry cards

Local Kestrel (www.epartscart.com / www.ecomae.com, port 5080):
- `/ar|/ru|/me` catalog links emitted by the lang home (`/ar/parts`, `/ru/vehicle-catalog`, …) 404 because Blazor routes are `/en/…` only. `StorefrontLangAliasMiddleware` rewrites those deep storefront paths onto the English twins and keeps the visitor prefix.
- Home industry cards `/platform/industries/{code}` 404. PHP `epc_ecomae_platform_match_path` aliases that plural path to `/platform/industry/{code}` (hyphen → underscore). Snapshots now serve the same page.

ERP posting paths were not changed.

### Checkpoint 2026-10-04 — Cursor review tranche and live fallback-route probe

Merged (local evidence only, PHP fallback and release locks unchanged):
#1938 period-close `year_month` SQL alias + SO→invoice duplicate/number-burn guard,
#1939 cumulative credit-note cap, #1940 shared ERP/CP/BOS cross-site write guard,
#1941 concurrency-safe receipt knock-off / bill paid reads, #1942 SO→invoice GL now
Dr `1100` / Cr `4000` / Cr `2100` (no `6100`), GL failure no longer swallowed,
`source_type` enum widened additively with `sales_invoice`.

Direct live probe via Cloudflare edge (`dig @1.1.1.1`, `curl --resolve`): ASP.NET
owns `/`, `/erp/login`, `/cp/login`, `/bos/login` on ecomae, epartscart,
thejewellerytrend, taxofinca (`x-ecomae-platform: primary`), but
`/php-reference/{home,cp,erp}` return **404 on all four hosts** — the PHP fallback
route blocks in `deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf`
are not installed live. SSH `root@31.97.216.247` is denied, so this is an
**operator action**: install the `/php-reference/*` blocks, reload nginx, re-probe.
Until then rollback-to-PHP is unverified and cutover/PHP removal stay disabled.

### Premium enterprise presentation gate

Functional route coverage is not sufficient for ERP/CP completion. Every ASP.NET
module must also pass a visual acceptance review against the PHP reference and
the corporate enterprise standard:

- consistent navy/blue enterprise shell, logo treatment, typography, spacing,
  responsive behavior, breadcrumbs, and company/tenant context;
- clear area navigation with active-state feedback and no empty or misleading
  destinations;
- page headers with status/context, action panes, primary/secondary action
  hierarchy, validation feedback, and predictable save/cancel behavior;
- data-dense but readable tables, filters, totals, badges, empty states, loading
  states, error states, print/export affordances, and keyboard/focus states;
- D365-style entry forms and QuickBooks-style guided workspaces must remain
  consistent across finance, purchasing, sales, inventory, projects, jewellery,
  HR, CP, BOS, tenant, and Super-CP surfaces.

The gate is **not closed** by loading the shared stylesheet: each module still
needs a side-by-side screenshot/interaction review on desktop and mobile, with
PHP fallback retained until the release owner accepts the presentation match.

### How to read the percentages

The repository currently has several intentionally separate meters:

- `726/726` catalog contracts and `142/145` presentation shadows measure
  inventory/preview wiring, not production ownership.
- The `95% / 5%` weighted decommission meter measures the residual PHP runtime
  path; it does not mean 95% of live product traffic is ASP.NET.
- The automated suite is a regression signal, not proof of deployed
  presentation or interactive parity.
- Formal migration completion remains **not ready** until the live authority
  table above, the PHP-vs-ASP.NET browser rounds, and release/rollback evidence
  all pass.
Enterprise comparison planning addendum (provisional until the user-supplied Zoho/Odoo HTML is attached):
the existing ECOM AE comparison surfaces require the matrix to keep explicit parity/evidence rows for
unified cross-module data and audit trail, full GL with dimensions/periods/consolidation, warehouse
batch/lot/serial/barcode handling, CRM-to-quote/order/invoice flow, hosted database-per-tenant and
Super CP fleet operations, integrated storefront/B2B portal, country-driven VAT and e-invoicing
(including Peppol/PINT-AE and tourist-refund handling), AI advisor/forecasting, and cryptographic/
blockchain proof claims. These are comparison/acceptance requirements, not completion claims; each
remains pending until its ASP.NET workflow, tenant isolation, PHP side-by-side evidence, human
acceptance, and production validation are recorded. The supplied local `file:///C:/...` path is not
accessible from the Devin VM, so no claims are made about content unique to that attachment.
The attached contracting evaluation adds this explicit acceptance checklist to B-F (not a new
completion percentage): `[~]` supplier quotations/RFQs; `[ ]` purchase/work orders; `[~]`
subcontractor management; `[~]` monthly subcontractor progress claims; `[~]` subcontractor payment
certificates; `[~]` site-engineer and project-manager approval levels; `[~]` vendor bills/AP invoice
flow; `[~]` payment vouchers; `[~]` client progress claims; `[~]` client payment certificates;
`[ ]` proforma/receipt handling; `[~]` work-completion certificates; `[~]` project profitability;
`[~]` budget-versus-actual reporting; `[~]` retention ledger plus release/ageing; and `[~]`
variation orders. The source also requires two parallel, reconciled chains (purchase/contract and
sales/client billing) converging on contract balance, profitability, and budget control. The
comparison's vendor pricing and marketplace/customisation observations are procurement context,
not migration scope or evidence of parity.
Latest ERP sub-slice: B0 navigation governance 4/4 controls landed (reconciliation, tenant deny flags,
explicit pack IDs, inspection projection); B5 manual GL journal posting, posted-journal reversal, and
closed-period guards, opened-journal line drill-down, PHP COA-backed account selectors, debit/credit balance summaries, and selectable posting dates are now live. The phase-level
percentage remains unchanged until a complete ERP capability gate is closed.
Legacy route-contract audit: CP shop/top-level maps resolve all 34/34 and 11/11 mapped destinations;
ERP PHP-tab mappings resolve 95/95 destinations and referenced `/erp/*-app` links resolve 224/224
non-AJAX pages. The `/cp/carts-app` legacy path now aliases the implemented carts twin; unresolved
scan entries are existing user/group routes or dedicated AJAX endpoints, not missing Razor pages.
The Parts Agent desk also no longer links back to legacy `/cp/shop/*` paths: price-list and catalogue
actions now target their implemented ASP.NET CP apps.
Platform operations now expose a drain-aware `/ready` probe that returns `503` once application
shutdown begins, with a bounded 30-second host shutdown timeout; rolling/blue-green orchestration,
the deploy health-wait helper now requires both `/health` and `/ready`, while rolling/blue-green
orchestration, session persistence, and full connection-drain verification remain pending.
Hardening review confirms BOS write routes enforce authenticated BOS capability, host gates, and
explicit confirmation before mutation; their framework antiforgery is intentionally disabled for
PHP-compatible JSON/form contracts, so a module-wide CSRF token review remains pending before
production cutover.
The follow-up inventory counted 378 ERP, 171 CP, 60 BOS, and 44 storefront
`DisableAntiforgery()` mappings; the existing `ICpCsrfGuard` is currently wired only in CP
handlers. This is an audit finding, not a permission to broadly enable framework antiforgery:
each module needs token issuance, PHP-contract compatibility, and write-by-write regression
coverage before its routes are promoted.

Conversation requirements audit (reconciled 2026-09-27):
- [x] CP and ERP menu sources remain PHP-authoritative; generated counts are not treated as proof of parity.
- [x] Tenant isolation, CSRF/RBAC, auditability, country-driven compliance, fit-out, jewellery, Python-sidecar,
      D365-style forms, document/report design, zero-downtime operations, on-premises/version support, and
      post-migration enterprise comparison are recorded below as standing requirements or phase work.
- [x] User sequencing is preserved: build CP → build ERP → build storefront/other areas → run combined testing.
- [~] Broad browser/E2E, functional, security, and PHP side-by-side testing remains intentionally deferred until
      the requested build phases are complete.
(CP dashboard twin split into 3 parts: parts 1–3 done — KPIs/chart, persisted `.eds-*` shortcuts, conditional insights + portal industry catalogue.
CP re-audit in progress: prices-edit rebuilt as the `prices_edit` twin — filter/search, profile site-price preview, paged table, inline edit, delete, search-delete;
print-docs rebuilt as the `print_doc_tuning` twin — document list, JSON `parameters_description` widgets (text/textarea/checkbox/image/profile), wholesaler office scope, live save;
SAO rebuilt as the `states_statuses_link` twin — state↔item-status grid with live save, robot queue view, SAO action palette;
data-transfer rebuilt as the `data_transfer` twin — catalogue XML/JSON export (PHP request fields, office/group scope, generated-file download)
and XML/JSON import (own-warehouse `interface_type=1` + per-user `users` JSON authorisation, clear modes 0/1/2 with explicit catalogue-clear confirmation);
CSV import completed as the `catalogue_csv_import` twin — end-category gate, 1-based column mapping for name/description/image/price/quantity plus every
category property with URL flags, skip-rows, Windows-1251/UTF-8 decoding, own-warehouse authorisation, delete-warehouse-rows / delete-category-products
(second confirmation) and per-row warnings;
KKT rebuilt as the online-cashier twin — `kkt_root_page` default settings for manual (type 2) and post-online-payment (type 1) checks with live save,
`devices.php` register list with wired-interface description, and the `checks.php` register: all 17 PHP filters, whitelisted sorting, paging,
expanded ordinary check lines/payments with order linkage and correction-check blocks (cause document, payment and six tax sums);
Tenant control center rebuilt as the `epc_tenant_control_center` twin — PHP tenant typing (commerce / demo / ERP-only demo / ERP-only) with the
PHP label and badge maps, PHP registry ordering, Listed/In registry/Accessible/Platform DB KPIs with demo-expiry access blocking, PHP admin-email
precedence (demo contact → `intro_json.admin_cp_email` → `operator_login_email` → `from_email`), PHP commerce host normalisation with storefront/CP
links and the ERP-only login URL, ERP pack column, and a masked password-state column; secrets (`db_password`, `operator_temp_password`) never leave
the service, and reveal/reset plus demo-access credential edits stay on the Classic twin;
API clients rebuilt as the `epc_api_clients_manage` twin — native `epc_api_clients` schema ensure, PHP hero/guide/curl block, create form
(product catalog / price_pro / both, label, contact email, clamped daily limit 1…1 000 000, catalog action scopes), PHP active-first listing with
key prefix / quota / scopes / status columns, and the full lifecycle: create, rotate, update, reset quota, revoke, activate. Keys are generated with
the PHP prefixes (`epc_catalog_`, `epc_pricepro_`), stored only as SHA-256 hash plus safe prefix, never selected back, and the plain key is handed to
the browser exactly once through a one-time in-memory token — never in the DB, the list payload or a URL;
Parts Agent rebuilt as the `parts_agent_chats` twin — PHP hero/toolbar, configuration card (enabled, domain, agent name, logo, subtitle, greeting,
system prompt, teaser, placeholder), stats strip (`epc_agent_cp_stats`), session filters (search over session/message/IP + date range),
`epc_parts_agent_session` list ordered by `updated_at DESC` with PHP 1–200 limit clamp and paging, `users`/`users_profiles` customer enrichment,
server-rendered transcript from `epc_parts_agent_message`, CSV export of the filtered set, and native `epc_agent_ensure_db_schema` /
`epc_agent_config_ensure_schema` DDL. Temp-file chat sync stays Classic — that folder belongs to the PHP host;
SMS/WhatsApp rebuilt as the `sms_turning` twin — `sms_api` catalogue with PHP `control_available` filter and `epc_%`-first ordering,
UAE/GCC/Pakistan guide block, current-operator/sender panel, grouped operator selector with description, and the dynamic parameter editor
(descriptor arrays and associative maps, text/number/select/checkbox/password widgets, sender fields prefilled with `+971567607011`),
saving field-by-field through the existing CSRF-gated activate endpoint — stored credentials are never echoed back and a blank secret keeps
the saved value. Actual sending stays under Communications;
Communications rebuilt as the `communications.php` + `ajax_test_notification.php` twin — SMTP readiness from all eight `config.php` fields,
From/host:port, active `sms_api` operator/handler/sender (PHP sender precedence), `debug_results` e-mail/SMS pills with stale (1 day) / old
(1 week) marking, admin-profile/session/From e-mail prefill, and a live test send: admin CP gate, confirm-write gate, `email`/`phone` type check,
`reg_fields` full-match contact validation, `test_email`/`test_phone` templates, SMTP or native SMS handler send (Unifonic/Etisalat/du/Pakistan
with UAE/PK MSISDN normalisation), `debug_results` persistence; passwords and SMS credentials never reach the browser.
Super CP info blocks rebuilt as the `epc_super_cp_info_blocks.php` twin — PHP hero/workspace intro/empty state, placement filter, PHP list
order, registry tenant dropdown, full-content editor (`?edit=` / legacy `?block_id=`), inline delete, native `epc_platform_info_blocks` schema ensure. Super CP communication rebuilt as the
`epc_super_cp_communication.php` twin — SMTP diagnostics, notification policy, task editor with `task_status` filter,
PHP task order, platform-user/tenant dropdowns, date due field, native comm/task schema ensure. Demo tenants rebuilt as the
`epc_demo_tenants_manage.php` twin — PHP hero/KPIs, demo table, +3d extend, convert to live, force delete with protected-DB guard.)

Standing rules that apply to every item below:
- Tenant isolation, per-tenant DB confidentiality, CSRF on every write, RBAC/ACL, audit log, degraded-shared-DB guard.
- Statutory logic (VAT/CT/e-invoice/payroll/labour law) is tenant-country-driven and effective-dated — UAE/FTA is one profile.
- ERP entry forms: Dynamics 365 F&O style — header/lines, action pane, field + cross-field validation, pre-validate → post,
  number sequences per document type, draft → confirmed → posted → reversed workflow, audit trail.
- Python sidecar (`pyapi`/`pyprices`) where technically better (forecasting, analytics, price-file ingest), behind a typed ASP.NET client.
- Zero downtime for tenants: rolling/blue-green releases, health-checked cutover, additive (expand/contract) migrations, persisted sessions.

---

## Phase A — Control Panel (CP)

### A1 CP module twins (PHP `cp/content/**`)
- [x] Currencies (#1486) · Order statuses, groups, config editor, languages, statistics (#1487)
- [x] Search tabs, manufacturer synonyms, crosses (#1489) · Warehouses/storages (#1490)
- [x] Offices, obtaining modes, geo tree, slider, additional texts, sitemap, menus (#1491/#1492)
- [x] Content pages manager/editor/tree (#1493) · Users manager/editor (#1494)
- [x] Product filters, quote requests, carts (#1495) · Notification settings/templates (#1496) · Orders list (#1497) — re-land #1498
- [x] Price configs (#1499) · Accessories marketplace (#1500) · Templates/plugins/modules (#1501)
- [x] VIN requests + VIN fields (#1502) · File manager (#1503) · Packs (#1504) · Bulk upload hub (#1505)
- [x] POS terminal (#1506) · Workshop desk (#1507) · Procurement desk (#1508) · Returns manager (#1509)
- [x] Price management (#1510) · CRM enterprise (#1511) · Prices-send (#1512)
- [x] Payment gateways hub — `payments_main.php` twin: `epc_payment_seed_all_gateways` catalogue (24 modern handlers, PHP names/
      descriptions/regions/parameters/demo values), dashboard/accounts/configure/legacy tabs, region sections, dynamic credential
      widgets from `shop_payment_systems.parameters`, individual `epc_payment_accounts` (platform/office/vendor, direct/connected/
      payout modes), `epc_payment_settlements` register, and all seven `ajax_payments.php` actions live behind admin+`confirmWrites`
- [x] Tenant feature registry + tenant SMTP — `epc_tenant_features.php` twin (Super-CP gate, `epc_portal_tenants` selector ordered by
      hostname with first-tenant default, catalogue checkbox matrix with PHP icons/labels, `tenant_registry` unsaveable, how-it-works
      panel, native `epc_integrations_ensure_schema` before `save_feature_flags`) and `epc_tenant_email_settings.php` presentation
      parity (platform-SMTP notice, activate-in-3-steps guide)
- [x] Mobile apps — `epc_mobile_apps.php` twin (admin-login warning, Super/Tenant CP badge, Integrations-hub action, Capacitor/PWA
      copy, full Android/iOS/PWA form with PHP placeholders and `api_base_url ?: storefrontUrl` / `deep_link_scheme ?: epartscart://`
      defaults, Android/iOS/connect publish guide and doc links); `save_mobile` merges `integrations_json.mobile` on the host-scoped
      `epc_portal_site_settings` row (host aliases first, lowest-id fallback) and adds the `integrations_json` column when missing
- [x] Super CP customer board — `epc_super_cp_customer_board.php` twin (`epc_scp_customer_board_search`): platform-registry search plus
      per-tenant scan of every live `epc_portal_tenants` database, `q` / `tenant` filters with 50-row pagination, PHP hero + workspace
      intro, Results / Platform / Tenants-scanned / Live-tenant KPIs, CRM + ERP + CP quick actions per row, PHP empty state; no password
      or credential column is ever selected and the board stays read/search only, as in PHP
- [x] Integrations Hub — `epc_integrations_hub.php` twin (`CpIntegrationsHubService` + `CpIntegrationsHubCatalog.BuildHubCards`):
      Super/Tenant brand and hero actions, Active / In catalog / Guides ready counts, tenant market label from
      `epc_price_settings.company_country_code` with the PHP UAE fallback and market notice, the six PHP categories with coloured
      cards, status and Super-CP pills, Configure or “Configured on ecomae.com”, PHP guide resolution, search + category chips
      served from the PHP `epc_integrations_hub_ui.js`, PHP empty state and three-step playbook; tenant rows follow
      `epc_tenant_feature_flags` and no integration secret is ever read or rendered
- [x] Integrations Guide — `epc_integrations_guide.php` twin (`CpIntegrationsGuideCatalog`, own `/cp/control/portal/epc_integrations_guide`
      route taken back from the generic guides hub): PHP operator handbook per catalog entry (summary, Activate steps, Tips, extra
      links) rendered in category order with the sticky contents list, Configure button on the Super/Tenant URL, Dedicated-guide button
      only when the resolved guide is not this page, tenant view drops super-only entries without a tenant URL, hides their Configure
      and the Super-only API documentation link
- [x] Super CP operator guide — `epc_super_cp_operator_guide.php` twin (`SuperCpOperatorGuideCatalog` + `/cp/super-cp-operator-guide-app`,
      route taken back from the generic guides hub): `epc_scp_guard_super_admin` behaviour (Super-CP-host-only notice, then Super CP login
      notice), PHP hero with Customer board / Tenant hub actions, both callouts (Operator role, “Tenant CP — no Operator sidebar group”),
      the six PHP module cards with summary / “Who should use it” / numbered workflow / Open link, the five-step typical-operator-day strip
      and the menu-location note
- [x] Industry settings — `industry_settings.php` twin (`/cp/industry-settings-app`, route taken back from the industry-packs digest):
      PHP brand banner and stats, ecosystem-grouped industry select, per-industry style templates with palette swatches, storefront
      layouts, branding/domain fields, the four access modes, the 27-language CP registry, contact block, ERP module matrix with
      preset labels, CP pack matrix (forced `core`, `super_platform` hidden on tenant hosts) and sidebar group/item visibility;
      `POST /cp/industry-settings/save` twins `ajax_portal.php?action=save_settings` — admin+`cp` gate, `confirmWrites`, visible→hidden
      inversion against the tenant's own `control_groups`/`control_items`, PHP normalisation (packs, ERP-module defaults per access
      mode, theme aliases) and a host-alias-scoped `epc_portal_site_settings` upsert
- [x] Social media hub — `epc_social_media_hub_panel.php` twin (`SocialMediaPackCatalog`, `CpSocialHubService`, `CpSocialCrypto`):
      the seven PHP tabs (Marketing pack, TikTok, Instagram, Connected accounts, AI advisor, Drafts, Guide), PHP brand/site-key
      resolution with the Super CP tenant selector, brand-adapted captions and hashtags, trending formats and industry hooks,
      caption generator, TikTok specs/privacy levels, Reel and guide video cards with “Use as media URL”, AES-256-CBC per-tenant
      credential vault (PHP `epc_social_crypto_key` derivation, tokens never rendered back, blank fields keep stored secrets),
      `save_account`/`test_account`/`delete_account`/`save_draft` on `POST /cp/social-hub/write` and the PHP hub stylesheet;
      live sending to Meta/TikTok still runs on the Classic twin
- [x] Marketing broadcast — `epc_marketing_broadcast.php` / `_panel.php` twin (`MarketingBroadcastCatalog`,
      `CpMarketingBroadcastService`, `CpMarketingBroadcastRecipients`, `CpMarketingBroadcastWriteService`): PHP tab order
      (Email, WhatsApp, History, Guide), KPI strip (customers with email/phone, emails sent, WhatsApp sent, campaigns),
      the four PHP audience modes (`all`, `with_orders`, `group`, `manual`) with group picker and manual list parsing,
      four email + five WhatsApp templates with `{{customer_name}}`/`{{shop_name}}`/`{{shop_url}}` merge tags, live email
      preview and chat-style WhatsApp bubble, SMTP readiness diagnostics, 1–100 batch clamp, PHP-compatible
      `epc_marketing_broadcast_campaigns`/`_log` schema with per-recipient logging, `send_email` via the tenant SMTP
      service and `send_whatsapp` as PHP Phase 1 `wa.me` link preparation on `POST /cp/marketing-broadcast/write`
      (admin + `cp` gate, `confirmWrites`), plus the PHP broadcast stylesheet; automatic WhatsApp Cloud API delivery
      stays on the Classic twin until a verified provider integration exists
- [x] Power BI — `epc_power_bi.php` + `content/general_pages/epc_power_bi.php` twin (`PowerBiCatalog`, `CpPowerBiService`,
      `CpPowerBiWriteService.EnsureSchemaAsync`): PHP hero and guide link, capability matrix (available now vs needs customer
      Azure/Power BI Pro vs Phase-A exclusions), the five-step connect guide and `X-API-Key` curl example, the seven-dataset
      Web-connector catalogue (`catalog`, `kpis`, `orders`, `sales`, `stock`, `gl`, `metrics`) with paths/formats/scope/params,
      Super CP tenant selector with the PHP host-match then first-tenant fallback, workspace config form (workspace/Azure tenant/
      default report/dataset IDs, embed mode, embed URL, notes), registered-report table and add-report form, native
      `epc_power_bi_config`/`epc_power_bi_reports` schema ensure before reads and writes, PHP embed resolution
      (`config_missing` / `needs_azure` / `url_missing` / `url_invalid` / `ready`) with the HTTPS `*.powerbi.com|.us` allowlist
      and iframe preview, and the tenant-scope guard so a non-operator CP cannot post another tenant's `site_key`;
      Azure embed-token minting is deliberately not implemented until the tenant supplies its own AAD app and capacity
- [x] CP top menu — DB-driven `control_groups`/`control_items` twin (#1513) + `/cp/...` row mapping through `MapCpPhpPath`
- [~] **CP dashboard twin** (`epc_tenant_cp_dashboard.php`) — built in 3 parts:
  - [x] Part 1: `CpTenantDashboardService` — PHP-exact `epc_tcp_dash_stats` SQL (orders today/7d/prev-7d, catalogue, warehouse/goods qty,
        SKU, vendors, clients, pending tasks, returns, VIN), per-KPI failure isolation, `epc_tcp_dash_stats:v7:<db>` 120 s cache,
        `TenantDataGuard` containment → zero-safe values, dynamic 7-day labels/counts, PHP `epc_tcp_dash_change` formatter,
        `epc_co_profile` company name + base currency, Finance pulse only when finance data exists
  - [x] Part 2: persistent per-user shortcuts — `CpShortcutCatalog` (PHP catalogue, auto_parts + generic default strips, CP tone map),
        `CpTenantDashboardService.LoadShortcutsAsync` (reads `epc_user_shortcuts` for surface `cp`/`both` on the per-tenant connection,
        seeds industry defaults only on first visit, zero-safe when the table/DB is unavailable), `POST /cp/dashboard/shortcut`
        (`shortcut_add` / `shortcut_delete` / `shortcut_delete_key` / `shortcut_reset` / `shortcut_reorder`, admin session + `cp`
        capability + CSRF, catalogue keys resolved server-side so posted URLs can't be trusted, `javascript:`/`data:` rejected),
        `.eds-*` grid/editor markup at PHP geometry (172 px min tile, 12 px gap, 118 px min height, 40×40 icon, hover lift)
  - [x] Part 3: conditional insights suite projection (CP variant, CSS only when insight HTML exists), DB industry label/icon for all
        industries incl. jewellery and fit-out, tenant ERP/storefront links through `MapCpPhpPath`
- [ ] Remaining CP PHP twins to verify/finish: `epc_super_cp_customer_board`,
      `epc_super_cp_operator_guide`, `epc_integrations_hub` + each integration settings page (WhatsApp, payment
      gateways, trackers, social media, Power BI, APIs, parts agent), `industry_settings`, `tenant_hub`, `sms-operatory`,
      `communications`, `epc_pos_tenant_manage`, `usergroups`, `content_tree` write actions, customers/documents/logistics single-item pages
- [ ] Auto-generated matrix: every DB menu item → PHP file → ASP.NET route → page type (twin / digest / missing); fix every digest/missing
- [ ] Presentation diff per CP page vs PHP (layout blocks, table columns, buttons, colours, icons, guide/help panels, breadcrumb)
- [ ] Guides: `cp-guideline`, module guide pages, operator guide, OMS menu guide (`epc_oms_menu_guide_lib.php`)
- [ ] ASP.NET-only CP routes PHP does not have → redirect/park (no invented menu entries)
- [ ] 53 CP routes whose PHP lives under ERP tabs (jewellery-*, AML/SOC2, blockchain proofs, landed cost, budgets, tickets/SLA,
      promotions/marketing, approvals/workflow) → alias to the ERP twins built in Phase B

### A2 CP exit
- [ ] Matrix shows 0 digest / 0 missing for tenant and super menus; parity tests + full suite green

---

## Phase B — ERP (PHP `cp/content/shop/finance/erp/**` + `content/shop/finance/**`)

- [~] **B0 ERP top menu** — `erp_nav_areas.php` twin (`ErpNavTree` + `IErpNavMenuService`): 35 areas and 154 placements in PHP order, RBAC
      `allowedTabs`, industry filter (jewellery `jw_*`), commerce filter, enabled modules, report injection, favourites, company
      picker, AP/AR/GL chain nav; replace `LegacyDesktopChromeCatalog.ErpTopnav()`; 0 invented tabs.
      Added a versioned `ErpNavAudience` policy and duplicate-placement audit so tenant version, industry,
      country and Super ERP deny rules can be evaluated without silently hiding PHP modules. The runtime
      now carries the active legal entity country and supports explicit tenant module-pack allow sets.
      A source-to-artifact reconciliation command now compares all PHP category/area/tab order,
      labels, icons, descriptions, groups, jewellery/raw flags, links, duplicates, and empty hrefs.
      Runtime visibility decisions now expose stable reason codes for industry filtering, explicit
      deny rules, module-pack restrictions, and PHP-default visibility. Persisted entitlement
      storage now reads explicit `erp.nav.disabled.*` tenant feature flags and recognizes only
      exact PHP catalog IDs/keys/areas from assigned industry-pack `modules` JSON. Generic pack
      labels remain non-authoritative, so they cannot accidentally hide PHP placements. Version
      persistence and Super ERP administration remain pending. The policy now also exposes a
      complete 154-placement inspection projection with visible/hidden reason codes for future
      Super ERP review screens.
- [~] B1 Home & workflow — ERP workflow tasks, approval queue records, and agenda events now open in shared D365-style workspaces with persisted queue/event/task context; dashboard, processflow, workflow automation, contacts, documents, knowledge base, AI assistant, and full PHP workflow action parity remain pending
- [~] B2 Order-to-Cash / Syncron-style OMS — sales orders, delivery notes, invoices, revenue, fulfilment, subscriptions, proposals,
      ASP.NET sales-order save/status/invoice/delete/cancel lifecycle is live; cancellation now uses the
      dedicated reversal-safe lifecycle endpoint with a required operator reason. Remaining fulfilment
      dashboard parity (payment/stock/delivery/returns funnel) is still pending.
      leads/opportunities/CRM; libs `epc_erp_order_fulfillment`, `epc_fulfillment_queue`, `epc_order_erp_pipeline`,
      `epc_order_supplier_fulfillment`, `epc_erp_scm`, `epc_erp_order_planning` (no `epc_erp_syncron_policy.php` exists in repo — confirm with user)
 - [~] B3 Procure-to-Pay — supplier settlement and PHP-compatible draft payment-batch creation now have ASP.NET form/JSON writes; purchase requisitions now have guarded PHP-parity save, line, submit, decision, and conversion lifecycle actions plus a shared D365-style ERP header/lines workspace; opened PO/RFQ/payable detail, persisted PHP SCM RFQ request-line readback, guarded supplier-response inserts, read-only supplier response/ranking/best-per-line/recommendation/award-preview projections, and a guarded complete-quote RFQ award-to-draft-PO handoff now use the shared document workspace, while guarded PO lifecycle routes cover save, status, receipt, invoice conversion, and delete; guarded three-way-match decision execution now persists PHP-compatible receipt/invoice/tolerance evidence behind an explicit confirmation gate; supplier portal scorecard/detail presentation now exposes the PHP-computed contact, received-count, lead-time, response, RFQ-count, score-breakdown, and structured recent activity fields; payment approval/export and broader supplier portal writes remain pending
- [~] B4 Inventory & warehouse — inventory categories/snapshots, forecast rows, guarded demand-history recording, order-planning recommendations, warehouse/movement, inventory ledger, RFID sessions, stock transfers, quality orders/NCRs, persisted quality test-result readback, guarded quality-result recording, WMS work assignment/receiving/wave/work lifecycle writes, virtual-warehouse locations, persisted MRP planned-order readback, guarded MRP planned-order firming, and guarded demand/on-hand-driven MRP regeneration now open in shared D365-style workspaces with persisted operational context; PHP remains the schema owner and Classic fallback
- [~] B5 Record-to-Report / GL — manual journal and opening-balance lines now use PHP COA/inventory-backed selectors, opening-batch line drill-down, pre-post validation, and transactional live posting for COA/cash-bank-only batches; inventory and fixed-asset batches remain explicitly gated until their shared transaction services are available; selectable posting date, list/detail debit-credit balance summaries, and posted-journal reversal now use the
      live validated GL endpoints with balanced-line validation, reversal safeguards, and audit logging; GL, COA, opening balances, aging, P&L, balance sheet, trial balance, year end, period close, fiscal periods,
      dimensions, cost models, budgeting, consolidation (BU/group/IC), multi-entity, multi-currency GL, revaluation, fixed assets, expenses, projects; project transaction form now preserves the PHP category field
 - [~] B6 IFRS report pack + drill-down — external reporting now renders a PHP-shaped 43-section/page-aware annual IFRS/IFRS 18 pack with cover, contents, primary statements, 30+ notes/disclosures, applicability index, VAT/Corporate Tax/e-invoice bridge, deterministic sample fallback, and expandable invoice/bill source schedules; annual live invoice and purchase rows now take precedence when available, expose clickable source-document actions, and show source kind plus net/VAT/gross metadata, while missing source populations remain clearly labelled sample schedules; account → journal → voucher drill-down, filing/scheduler, executive dashboard, print designer, and exact PHP report-layout parity remain pending
 - [~] B7 Cash & treasury — cash-entry posting, bank-statement line matching, bank-instrument create/status lifecycle, generic petty-cash float creation, payment-batch draft validation/write, and PHP-compatible payment-batch dimension-link writes now have ASP.NET coverage with PHP-style cash-account and dimension inputs; the cash-entry workspace now presents aligned D365 F&O-style New/Create, receipt-journal, payment-journal, Options, amend, and audit-safe Void actions while preserving PHP RV-/PV- voucher flows; cash forecast now supports PHP-compatible forecast selection/detail navigation and scoped projection readback, while collections/dunning now preserves PHP empty-state wording in the opened queue/action-log workspace; credit, settlement, advances, withholding, payment approval/export, and broader bank import/report parity remain pending
- [~] B8 Tax & compliance (tenant-country profiles) — VAT return/refund and tourist VAT writes, UAE FTA legislation fetch/checklist/regen, CT adjustment writes, authenticated manual ASP-portal e-invoice submission, and shared D365-style opened e-invoice header/lines/event presentation now have ASP.NET coverage; full return filing, e-invoice API/Peppol polling/retries/credit notes, electronic reporting, AML, blockchain proofs, customs/shipping, and non-UAE country profiles remain pending
- [ ] B9 HR & payroll — staff, HR ops, recruitment, performance, labour-law profiles, payroll, WPS (UAE profile)
- [ ] B10 Service & after-sales — contracts, tickets, SLA, warranty/RMA, insurance, doc expiry, plant maintenance
- [ ] B11 Production — manufacturing, MFG planning, quality, product structure, PLM, costing
- [ ] B12 Retail & commerce — retail, POS, card reader, e-commerce/CRM integration, marketing
- [ ] B13 Setup & administration — ERP setup, security roles (RBAC), platform, data import/migration, integration, tenant config,
      org admin, business units, shortcut icons, on-premises, audit, DB integrity, governance
- [ ] B14 Guides — ERP guide, full/advanced/operator guides, process flows
- [~] **B-J Jewellery pack** (39 `jw_*` tabs + INDUS study): PHP references are now explicitly inventoried in `ASP_NET_CORE_MIGRATION_PLAN.md`; ASP.NET has guarded tenant/company navigation, master/repair/stock/fixing/retail projections and dedicated dry-run/write routes for a substantial subset. The current POS tranche now carries explicit tag/barcode linkage, sale availability and source-invoice return guards, PHP-owned tender receipt rows, old-gold exchange, scheme redemption, sale-return adjustment, and refund-due fields through the native UI and JSON contract. These are implementation advances, not formal acceptance: the throwaway confirmed purchase/fixing/tag/sale/return rehearsal, authenticated browser readback, physical second-database isolation, recovery/rollback, UAT, and production gates remain open. Remaining parity includes the full 39-tab workflow, purchase/GRN/assay, POS/returns/exchange, workshop register/search, weight/value ledgers, country-driven tax/compliance, shared PR/PO/SO fields, live MariaDB validation, production shadow evidence, and human acceptance. Masters (karat, rate type, metal stock, design, diamond, pearl, colour stone,
      currency, gold rate, tags, barcode, divisions, prefixes, cost/price types, price lists, daily rates), purchasing (metal, diamond,
      fixing), manufacturing/stock (verification, balance, weight ledger, valuation, transfers), sales/POS (retail, metal, fixing, return,
      advance, old-gold exchange, gold scheme, multi-currency tender), repairs/workshop chain, finance (weight + value TB, JV, petty cash,
      tourist VAT), field injection into inventory/PO/SO for jewellery tenants; all 26 `jw_*_save` ajax actions
- [~] **B-F Fit-out / interior-contracting pack** (user spec `fitout.txt`; ASP.NET now exposes live PHP-compatible project budget, project transaction, and project-recognition writes over the shared project-accounting foundation, plus additive tenant-isolated estimate/BOQ header and line writes with D365-style forms and the explicit phased 32-step delivery catalog at `/migration/fitout`; recognition preserves PHP POC/completed/straight-line calculation, WIP semantics, pre-post validation, and selectable as-of dates; the full pack extends PHP `epc_erp_project_accounting.php`
      `epc_prja_budget/txn/recognition` + `erp_tabs_projects.php`, industry codes `construction_contracting`/`building_materials`):
      Lead → Estimate/BOQ (sections, items, material/labour/subcontract/equipment/overhead rates, markup, revisions, Excel import)
      → Quotation → Contract (advance/retention/warranty) → Project + hierarchical cost codes → budget/committed/actual/forecast
      ledger (`project_transactions` per project + cost code) → PR/RFQ/PO/GRN (3-way match, tolerance) → material issue → subcontract
      orders/measurements/certifications → site daily reports (photos, mobile) → timesheets at cost rate → equipment usage →
      variations (approved only revise contract) → RFI/drawings revisions → QA/QC inspections/snags → weighted BOQ progress
      (versioned/approved) → progress claims/invoices (gross, retention, advance recovery, VAT) → project P&L/forecast profit;
      configurable approval engine (thresholds, levels), numbering (`QT-2026-00001` …), RBAC roles, dashboards (CEO/PM/finance/procurement),
      reports (sales/estimation/projects/procurement/inventory/subcontract/finance); shares ERP masters (customer, supplier, item,
      COA, tax, warehouse, currency); D365-style forms; phased P1 core → P2 operations → P3 finance → P4 advanced; MVP = 32-step scenario
- [~] All 321 `ajax_erp.php` actions have an ASP.NET dispatcher case (CSRF + RBAC) — tracked by a parity test
- [~] **ERP document/report designer** — per-tenant/company (optional branch) templates for vouchers, invoices, orders, statements,
      reports: logo, header/footer, fonts, colours, columns, layout, number/date formats, print/PDF/email variants, preview,
      versioning + effective dating, audit, rollback, tenant isolation, safe template content
- [ ] Statutory profile tests for ≥ 2 countries (UAE + one non-UAE)

---

## Phase C — Storefront & remaining areas
- [ ] C1 Storefront themes (`expan`, `limo`, `modex`, `nero`) CSS/asset parity; fix `/php-reference/home` 404 (live 404 on all product hosts re-confirmed 2026-10-04; nginx blocks not installed, SSH denied)
- [ ] C2 Catalogue & search (search tabs, spare-parts search, ucats/umapi/laximo, vehicle catalog) — search latency (~6 s)
- [ ] C3 Cart / checkout / payments (gateway callbacks, obtaining modes, guest order)
- [ ] C4 Customer area (account, requests, balance, quotes, returns, garage, wishlist, print docs)
- [ ] C5 Vendor / B2B (vendor portal, bulk upload, parts agent, demand intelligence, channels, marketing)
- [ ] C6 BOS / Super-CP / tenant hub (provisioning writes, operator guides). BOS is the control tower in the enterprise benchmark matrix, not an accepted admin menu. It consumes ERP; it does not post a second ledger.
- [ ] C7 Marketing / LifeOS / IP / API v1 (forms, SEO/sitemaps)
- [ ] C8 Cron & workers — every PHP cron has a Worker twin (currency rates, e-invoice poll, legislation fetch, demo expiry, cache warm, webhooks)

---

## Phase D — Platform hardening & operations (during and after migration)
- [ ] Tenant isolation review per module (no cross-tenant reads, degraded-shared guard, credentials never leak)
- [ ] Security: CSRF on all writes, RBAC/ACL parity, audit on all mutations, rate limits, secure headers
 - [~] Zero-downtime releases: readiness/liveness probes, bounded graceful drain, and deploy health waiting are implemented; rolling/blue-green Kestrel, persisted session state,
      backward-compatible app/DB versions, expand-then-contract migrations, rollback runbook
- [ ] On-premises installation package + multi-version compatibility (1000 tenants, multi-industry, multi-country)
- [ ] Versioning, licensing/rights, compliance & policy documentation
- [ ] VPS: prune 15 parallel PHP-FPM versions (after test round 1)

---

## Phase E — Testing rounds & gates (user sequence)
- [ ] Round 1: CP + ERP combined browser test (both industries, tenant + super hosts) via `testing-erp-aspnet-local`, side-by-side with `/php-reference`
- [ ] Round 2: storefront + BOS + marketing + workers
- [ ] Round 3: full regression → `AspNetInteractiveCompleteCount = N`
- [ ] Gates: `CutoverAllowed` → PHP reference read-only one release → `PhpSourceDeletionAllowed` (with user approval)

---

## Phase F — Enterprise benchmark (standing target)

The acceptance benchmark is Dynamics 365 Finance & Operations / Sales, SAP S/4HANA / Sales Cloud, and Oracle Fusion ERP / SCM. The seeded matrix is in "Enterprise platform benchmark" above. Every row is **Not accepted**. Older comparison names (Odoo, QuickBooks, Peachtree/Sage 50, Zoho, 1C) stay historical context. They are not this acceptance bar. PHP removal is not what closes Phase F.

- [ ] Accept the seeded matrix rows with production evidence (isolation, rollback, UAT, and a human acceptance bundle). Routes and throwaway rehearsals do not count.
- [ ] Build every missing capability on the shared ERP engine. Do not add a second business engine in BOS, CRM, or the Control Panel.

---

## Open questions for the user
1. Syncron: no `epc_erp_syncron_policy.php` in repo — send original policy/docs or confirm twinning `epc_erp_scm.php` + `epc_erp_order_fulfillment.php`.
2. ERP-only staff role (PHP RBAC allows it; ASP.NET auth currently grants CP+ERP together) — confirm wanted.
3. Jewellery legacy schema open items (INDUS study) — the supplied studies are
   now preserved in
   `docs/migration/evidence/jewellery/INDUS_LIVE_FIELD_AND_WORKFLOW_MAPPING.md`.
   Their observed field/menu inventory informs target mapping, while actual
   INDUS keys, formulas, posting lineage, lifecycle states, permissions, and
   report definitions remain open until read-only schema or approved
   transaction evidence is available; nothing proposed is treated as a legacy
   column.
4. Fit-out pack: PHP only has generic project accounting (budget/txn/recognition); the BOQ/variation/progress-claim/subcontract chain is
   a new DB-backed design per the user's `fitout.txt` spec. The ASP.NET phased catalog is now explicit at `/migration/fitout`; the
   Phase-1 scope remains pending implementation and live tenant-database validation.
