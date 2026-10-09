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
   - `content/shop/order_process`: done: the storefront `orders_background.php` shared-data helper, `get_customer_offices.php`, `checkout_login_offer.php`, `common_add_to_basket.php`, `my_quotes.php`, `my_order_not_authorized.php`, `my_orders.php`, `my_order.php`, `my_orders_items.php`, `cart.php`, `checkout_confirm.php` (see the checkpoints below). The different Control Panel helper with the same basename remains open.
   - Done: the password reset pages `content/users/forgot_password.php` and `new_password.php`, with `DP_User::available_communications()`. See the checkpoint below.
   - Done: the login rate limit `content/users/epc_login_rate_limit.php` and the hash upgrade `epc_password_upgrade.php`. See the checkpoint below.
   - Done: the contact uniqueness check `content/users/check_reg_contact.php`. See the checkpoint below.
   - Done: the page access include `content/users/check_user_access.php`, used by the CP lang editor ajax and the storefront storage toggle. See the checkpoint below.
   - Done: the trade account library `content/shop/pricing/epc_customer_trade.php` and the currency library `epc_currency.php` (except `epc_currency_js_config`), with the checkout and price gates switched to them. See the checkpoint below.
   - Done: the country list `content/users/epc_countries.php`. Retired: `content/users/epc_reg_fields_compliance.php`, which nothing includes or calls.
   - Done: the e-invoice schema `content/shop/finance/epc_einvoice_schema.php`. Partly done: the validation and save half of `epc_registration_enhanced.php` (the render half remains) and the `customer_vat_type` sync of `epc_uae_customer_vat.php`. See the checkpoint below.
   - `content/users`: `dp_user.php`, `epc_registration_enhanced.php`, `profileform.php`, `epc_reg_fields_compliance.php`, `epc_countries.php`, `epc_session_security.php`, the agreement module.
   - `content/shop/catalogue` (41 files): `printProducts.php`, `printProducts_2.php`, the remaining product pages, compare, bookmarks, SKU media, the text search algorithm, the tree lists. Done: `printProduct_Info.php` and `product_page_for_customer.php` (see the checkpoint below).
   - `content/shop/docpart` (44 files): `part_search_page.php` and `part_search_page_1.php`, the parts agent, demand intelligence, garage, fitment, cross interchange, the multivendor and commerce price ingest.
   - `modules/*`: login (password, code, social), menu, bread crumbs, slider, news, lang, and `shop/*` (cart, balance, search string, geo, ucats).
   - Front templates `expan`, `modex` and `limo`; `core/dp_core.php` and `dp_helper.php` behaviour; plugins (metadata handler, phone/tablet, error pages, shop cart).
2. **Checkout and order side effects still on PHP.**
   - Done: process-flow sync (`epc_pf_sync_order_case` and `epc_pf_sync_po_case` in `content/shop/finance/epc_erp_processflow.php`). See the checkpoint below.
   - Done: sales-invoice sale-demand capture (`epc_erp_inventory_record_sale_demand`). See the checkpoint below.
   - Done: SMS and WhatsApp Cloud API fan-out of `docpart_dispatch_notification()` (`content/notifications/send_notify_dispatch.php`, `epc_whatsapp_notify.php`). See the checkpoint below.
   - Done: the legacy SMS operators in `content/sms/handlers` (iqsms, rocketsms_by, semysms, smsaero, smsgorod_ru, smsimple, sms_ru, smstraffic, smsvizitka_com, terasms_ru) and the handler URLs of all 14 operators. See the checkpoint below.
   - Retired: `content/sms/handlers/smsaero/send_sms_old.php` (reason in `inventory/PHP_RETIRED.tsv`).
   - Done: contact confirmation (`content/users/ajax_contacts_works.php`) and the login code (`modules/login/code/frontAjax/ajax_sendCode.php`) send through the dispatcher and store their rows. See the checkpoint below.
   - Done: the legacy `send_notify.php` HTTP endpoint, including its `debug_results` upsert for e-mail and SMS. See the checkpoint below. `epc_order_whatsapp_share.php` is a CP order page include and moves to step 3.
   - Done: the order and line status protocol (`content/shop/protocol/set_order_status.php`, `set_order_item_status.php`), pay on place through it, and the post-commit part of `pay_for_order.php` for online payments. See the checkpoint below.
   - Done: creating a return request (`content/shop/returns/ajax/ajax_load_returns_data.php` with `helper.php`): line split, photos, notifications and line status. See the checkpoint below.
   - Done: order print (`content/shop/print_docs/service/print.php` with `get_html_sales_receipt.php` and `get_html_uae_tax_invoice.php`) and the document control template render. See the checkpoint below.
   - Done: the `content/shop/returns` pages (`returns.php`, `return.php` with `return_messages.php`, `add_return.php`, `assets/add_return.js.php`), the payment method picker (`content/shop/payments/epc_payment_method_picker.php`) and the obtaining-mode includes (`content/shop/obtaining_modes`). See the checkpoint below.
   - Done: the return selection on the customer order page (`my_order.php` `confirm_return()`).
   - Done: Document Control print (`content/shop/document_control/service/print.php`) with the ERP access check `epc_erp_user_can_access` and the e-invoice context `epc_dc_einvoice_context`. See the checkpoint below.
   - Retired: `content/shop/document_control/epc_document_control_cp_install.php` (PHP CP CMS installer; reason in `inventory/PHP_RETIRED.tsv`).
   - Moved to step 7: the ERP portal half of `epc_erp_access.php` (`epc_erp_portal_*`, tab rights `epc_erp_user_allowed_tabs` / `epc_erp_user_can_access_tab`). Only the ERP shell (`erp_main.php`) and the `/erp` portal pages use it.
3. **Control Panel shop pages.**
   - `cp/content/shop/order_process` (20 files): `order_card.php`, `orders_items.php` and its add, edit and reload modals, the orders detail pane, the fulfilment, OMS and WhatsApp guides, `epc_order_whatsapp_share.php`.
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
   - The BOS unified and blockchain BOS pages, and the BOC console, with the BOS operator login `epc_bos_ajax_login.php` (it fills the PHP BOS session these pages read).
   - The industry consolidation and the industry templates (`_base_template.php`, the sub-industry page and 28 industries).
   - `epc_cloudpanel_helpers.php` is moved to ops workers, not ported as a page.
6. **Price engine.** `content/shop/price_engine` (23 files, including the 6,768-line `epc_auto_price_engine.php` and the discovery adapters) plus the CP auto-price shell. This must stay on the existing importer.
7. **ERP to 100%, after the steps above.**
   - The 152 finance libraries in `content/shop/finance`. The largest are the external reports build, the UAE tax compliance, jewellery, inventory, SCM, tax toolkit, `my_balance.php`, phase 8, order planning, AML, staff, concurrency, HR law, integration, access, datalink, industry packs, period close, WMS, payroll and procurement.
   - The CP finance pages: nav areas, the dashboards, the operations editor and create operation, the payment systems, custom shipping.
   - The write side of the 118 mapped tabs.
   - The ERP portal half of `epc_erp_access.php`: portal URLs, the CP-admin session bridge, the guest session and auth post, and the tab rights by department (`epc_erp_staff_allowed_tabs`).
   - The deferred findings: cash without journals, integrity gaps, POS without GL, voucher gaps, untested services, money typed as double, the emergency-publish flag. Reposting the wrong 4000/6100 production transfers needs approval.

Each item closes only when ASP.NET does the PHP behaviour (tested on a throwaway database, full suite green), or when it is retired with a reason. The inventory gap ratchet is lowered in the same commit.

### CP and ERP UI/UX — better than the PHP reference (owner requirement, 2026-10-08)

Steps 3, 4 and 7 (CP shop, CP control and portal, ERP) close only when the page has PHP's functions and also passes the UX bar below. The storefront keeps PHP's look and flow, because customers know it. CP and ERP are operator tools and must be better than PHP. This is how the "UI/UX %" bands in the benchmark matrix go up.

Rules:
- Function parity comes first. Every PHP field, action, filter, column, message and permission stays. A design change must never drop a PHP action or hide data the operator used.
- "Better" is measured per page against the PHP page for the same URL, with before and after screenshots in the checkpoint. A page's UI/UX is not counted as better while any bar item below fails.

The UX bar for every CP and ERP page:
1. **Navigation.**
   - One top menu with no repeats and no two entries for the same data (`TopMenuIntegrityTests`).
   - Each page opens its own module and shows breadcrumbs.
   - The page title matches its menu label.
   - Every menu page is reachable in two clicks.
   - After session expiry and login, the operator returns to the same page.
2. **Speed.**
   - First paint under 1 second on the local build.
   - Filter, sort and paging work without a full page reload.
   - Long lists are paged on the server, not drawn as thousands of DOM rows.
3. **Lists.**
   - Search, sort, filters with visible chips, and a column chooser.
   - Saved views per user, CSV or Excel export of the filtered rows, and a sticky header.
   - Row actions that respect the user's rights.
   - An empty state that says what to do next.
4. **Forms.**
   - PHP's validation rules and messages, shown inline next to the field.
   - Values are kept after an error, and required fields are marked.
   - An unsaved-changes guard, and confirmation before delete, post or cancel.
   - Enter saves and Esc closes modals.
   - A success notice links to the saved record.
5. **ERP documents** (order, PO, invoice, receipt, journal and the rest) use one workspace layout, to the Dynamics 365, SAP Fiori and Oracle Fusion bar:
   - header, lines and totals;
   - a status timeline and an action bar that changes with status;
   - related documents, attachments and the audit log.
6. **Dashboards.**
   - KPI cards per role that drill down to the filtered list behind the number.
   - No number without a source.
7. **Mobile and RTL.**
   - Usable at 375 px width: tables scroll, menus collapse, buttons stay tappable.
   - Arabic pages render right to left.
8. **Accessibility.**
   - WCAG 2.1 AA: labels, contrast, focus order, keyboard menus and ARIA.
   - Checked with an automated axe scan, which must show 0 serious or critical issues.
9. **One design system for CP and ERP.**
   - Shared design tokens, buttons, tables, modals and notices.
   - No script code shown as text (rule from the CP inline-script fix).
   - No browser `alert()` for operator errors; messages appear inline.
10. **Errors.**
    - A clear message with the next step.
    - Never a stack trace, a blank page or an empty 500.

Evidence per page, recorded in its checkpoint:
- the PHP and ASP.NET screenshots;
- the axe result;
- the load timing;
- a short recording of the main task;
- tests for the parity half.

Order:
- New CP and ERP ports meet the bar when they are built.
- Pages that are already ported get a UX pass in this order: CP dashboard, orders and order card, price upload and review, catalogue product, customers and CRM board, then the ERP shell, the document workspaces (O2C, P2P, inventory, treasury) and the ERP dashboards.

### Checkpoint 2026-10-09 — plan Q1-safe (warehouse sitemap, tenant data protection, customer helpers)

Not complete.

- Ratchet 517 to 514. Three files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Safe`): `epc_sitemap_warehouse.php` (SEO price-clause and part-loc injected; storage-flag and article-match parents omitted), `epc_tenant_data_protection.php` (portal tenant row/connect injected; `enforce_access` mentioned but not golden-run because it exits), `epc_customer_mgmt_helpers.php` (finance save/VAT injected). `empty('0')` is empty.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Safe` (6). PlanQ1 suites 97 of 97. Inventory content 524 to 527 of 952. Unnamed PHP functions 7,104 to 7,055. Ready ≤200-line non-ERP functions stay 74. The weighted headline stays about 20.4%. Non-ERP pending: 348 files / 175,651 lines (was 351 / 176,954). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-talk (communication test, storefront anti-crawl)

Not complete.

- Ratchet 514 to 512. Two files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Talk`): `epc_order_communication_test.php` (notify status and trade injected), `epc_storefront_anti_crawl.php` (prices-visible and session user injected; leftover user-include basename not written). `deny` is mentioned but not golden-run because it exits. Rate-limit blocks when `count > max`. `empty('0')` tech_key is empty.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Talk` (4). PlanQ1 suites 101 of 101. Inventory content 527 to 529 of 952. Unnamed PHP functions 7,055 to 7,037. Ready ≤200-line non-ERP functions stay 74. The weighted headline stays about 20.4%. Non-ERP pending: 346 files / 175,024 lines (was 348 / 175,651). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-walk (multivendor min-price ACL)

Not complete.

- Ratchet 512 to 511. One file now has PHP 8.3 goldens and an ASP.NET twin (`PhpPlanQ1Walk`): `epc_multivendor_min_price_acl.php`. Session user stays injected. `empty('0')` restrict is open. Save keeps first-seen positive ids.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Walk` (2). PlanQ1 suites 105 of 105. Inventory content 529 to 530 of 952. Unnamed PHP functions 7,037 to 7,026. Ready ≤200-line non-ERP functions stay 74. The weighted headline stays about 20.4%. Non-ERP pending: 345 files / 174,740 lines (was 346 / 175,024). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-hold (SSO SAML, BOS health)

Not complete.

- Ratchet 511 to 509. Two files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Hold`): `epc_sso_saml.php` (AuthnRequest id/instant normalized; MariaDB `SUM(active)` is a string), `epc_bos_health_check.php` (tenant connect injected; unified parent stays a gap). Connectivity fail detail is hardcoded `Connection failed`.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Hold` (4). PlanQ1 suites 109 of 109. Inventory content 530 to 532 of 952. Unnamed PHP functions 7,026 to 7,011. Ready ≤200-line non-ERP functions 74 to 71. The weighted headline stays about 20.4%. Non-ERP pending: 343 files / 174,225 lines (was 345 / 174,740). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-keep (industry SEO, BOC tenant scope)

Not complete.

- Ratchet 509 to 507. Two files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Keep`): `epc_industry_seo.php` (crc32 presentation; live-bridge / groups injected), `epc_boc_tenant_scope.php` (unified tenant-list and Super-CP host injected). Empty demo `cp_url` becomes `https://www.ecomae.com/` plus the path. Live-bridge categories apply only when a template row parses empty.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Keep` (4). PlanQ1 suites 113 of 113. Inventory content 532 to 534 of 952. Unnamed PHP functions 7,011 to 6,985. Ready ≤200-line non-ERP functions stay 71. The weighted headline stays about 20.4%. Non-ERP pending: 341 files / 173,489 lines (was 343 / 174,225). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-open (storefront storage flags, SKU media)

Not complete.

- Ratchet 505 to 503. Two files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Open`): `epc_storefront_storage_flags.php` (no parent), `epc_sku_media.php` (CHPU builder injected so the article-match leftover stays a gap). `empty('0')` price_id / warehouse labels are empty. Upload `add_photo` is mentioned; CLI has no uploaded file so the golden is the `No upload` path. Attach file names are normalized in the snapshot.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Open` (4). PlanQ1 suites 121 of 121. Inventory content 536 to 538 of 952. Unnamed PHP functions 6,971 to 6,922. Ready ≤200-line non-ERP functions stay 71. The weighted headline stays about 20.4%. Non-ERP pending: 337 files / 170,453 lines (was 339 / 172,194). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-lock (industry consolidation, template router)

Not complete.

- Ratchet 507 to 505. Two files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Lock`): `epc_industry_consolidation.php` (portal group-map injected), `epc_industry_template_router.php` (last fallback matches undefined `$mainDir` → `/` + `retail` + `.php`). `empty('0')` theme primary does not override. `round` half-up: 97.573… → 97.6.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Lock` (4). PlanQ1 suites 117 of 117. Inventory content 534 to 536 of 952. Unnamed PHP functions 6,985 to 6,971. Ready ≤200-line non-ERP functions stay 71. The weighted headline stays about 20.4%. Non-ERP pending: 339 files / 172,194 lines (was 341 / 173,489). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-note (storefront seed data)

Not complete.

- Ratchet 518 to 517. One file now has PHP 8.3 goldens and an ASP.NET twin (`PhpPlanQ1Note`): `epc_storefront_seed_data.php`. Portal / ERP locale / theme parents are omitted so locale stays AE/AED/VAT/gcc. Non-AED product prices divide the AED list by 3.67 then convert.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Note` (2). PlanQ1 suites 93 of 93. Inventory content 523 to 524 of 952. Unnamed PHP functions 7,121 to 7,104. Ready ≤200-line non-ERP functions stay 74. The weighted headline stays about 20.4%. Non-ERP pending: 351 files / 176,954 lines (was 352 / 177,508). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-lead (AI classification, marketing pages, CP top alerts)

Not complete.

- Ratchet 521 to 518. Three files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Lead`): `epc_ai_classification.php`, `epc_ecomae_marketing_pages.php` (content catalogs already mentioned; home helpers stubbed), `epc_cp_top_alerts.php`. HTTPS uses the global config because PHP `global $DP_Config` shadows the argument. `empty('0')` is empty.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Lead` (5). PlanQ1 suites 89 of 89. Inventory content 520 to 523 of 952. Unnamed PHP functions 7,146 to 7,121. Ready ≤200-line non-ERP functions stay 74. The weighted headline stays about 20.4%. Non-ERP pending: 352 files / 177,508 lines (was 355 / 178,421). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-faq (marketing FAQ page)

Not complete.

- Ratchet 522 to 521. One file now has PHP 8.3 goldens and an ASP.NET twin (`PhpPlanQ1Faq`): `content/general_pages/epc_ecomae_faq.php` (format-answer / status-class / schema / styles / render-page plus page-local `showModule` / `filterFaq`). Home helpers stay harness stubs and unmentioned. Longest-label-first replace nests Auto Price AI inside Auto Price AI page.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Faq` (2). PlanQ1 suites 85 of 85. Inventory content 519 to 520 of 952. Unnamed PHP functions 7,152 to 7,146. Ready ≤200-line non-ERP functions stay 74. The weighted headline stays about 20.4%. Non-ERP pending: 355 files / 178,421 lines (was 356 / 178,649). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-work (orders workspace, marketing helpers, CP breadcrumb)

Not complete.

- Ratchet 525 to 522. Three files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Work`): `epc_orders_workspace_helpers.php` (currency records injected), `epc_marketing_helpers.php` (playbook catalog injected so that parent stays a gap), `epc_cp_breadcrumb.php`. `empty('0')` is empty. Badge class uses a request static cache.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Work` (5). PlanQ1 suites 81 of 81. Inventory content 517 to 519 of 952; cp-page 256 to 257 of 523. Unnamed PHP functions 7,180 to 7,152. Ready ≤200-line non-ERP functions 78 to 74. The weighted headline stays about 20.4%. Non-ERP pending: 356 files / 178,649 lines (was 359 / 179,323). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-ask (AI copilot + unified AI service)

Not complete.

- Ratchet 527 to 525. Two files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Ask`): `epc_ai_copilot.php` (intents / parse / generate-sql / execute / fleet; history LIMIT bind skipped), `epc_ai_service.php` (PII strip / route / classify / anomaly / NL report / query / stats; recent LIMIT bind skipped). Execute logs generated SQL and does not run it. PII patterns apply in PHP order.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Ask` (4). PlanQ1 suites 77 of 77. Full platform suite 6035 of 6035. Inventory content 515 to 517 of 952. Unnamed PHP functions 7,198 to 7,180. Ready ≤200-line non-ERP functions 85 to 78. The weighted headline stays about 20.4%. Non-ERP pending: 359 files / 179,323 lines (was 361 / 179,705). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-site (site context, supplier leftovers, CP ACL)

Not complete.

- Ratchet 530 to 527. Three files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Site`): `content/general_pages/epc_site_context.php` (portal/branding parents stubbed; default-contact is private), leftover functions on `epc_supplier_notifications.php` (`epc_supplier_h`, `epc_order_item_storage_id`) and `cp/content/control/control_helper.php` (`epc_cp_acl_preload`, `epc_cp_acl_expand_groups`). Cache-key regex is lowercase-only. Apply skips empty head-office fields.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Site` (5). PlanQ1 suites 73 of 73. Inventory content 513 to 515 of 952; cp-page 255 to 256 of 523. Unnamed PHP functions 7,215 to 7,198. Ready ≤200-line non-ERP functions stay 85. The weighted headline stays about 20.4%. Non-ERP pending: 361 files / 179,705 lines (was 364 / 180,357). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-ship (logistics, electronics taxonomy, social pack, worldclass)

Not complete.

- Ratchet 534 to 530. Four files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Ship`): `epc_logistics_helpers.php` (channel schema+helpers already ported; demo/setup URLs concatenated), `epc_electronics_taxonomy.php` (optional industry-taxonomy migrate not copied), `epc_social_media_pack_data.php` (helpers parent stubbed to adapt-text only), `epc_storefront_worldclass.php` (portal parent stubbed). Thread-starter `substr` is bytes.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Ship` (6). PlanQ1 suites 69 of 69. Inventory content 509 to 513 of 952. Unnamed PHP functions 7,249 to 7,215. Ready ≤200-line non-ERP functions 93 to 85. The weighted headline stays about 20.4%. Non-ERP pending: 364 files / 180,357 lines (was 368 / 181,311). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-plus (price extras, script relocate, POS markup, role home, channels)

Not complete.

- Ratchet 539 to 534. Six files now have PHP 8.3 goldens and ASP.NET twins (`PhpPlanQ1Plus`): `epc_price_extra_fields.php`, `epc_cp_script_relocate.php`, `epc_pos_terminal_markup.php` (`epc_pos_h` stubbed; helpers parent stays a gap), `epc_cp_role_home.php` (action URLs concatenated so leftover unique basenames are not written as path strings), `epc_channel_schema.php`, `epc_channel_helpers.php`. Channel schema was already a path mention; five new gap files left the inventory. Shipment `random_int`/`date` is implemented with injectable clock/rng and not golden-run.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Plus` (8). PlanQ1 suites 65 of 65. Inventory content 505 to 509 of 952; cp-page 254 to 255 of 523. Unnamed PHP functions 7,306 to 7,249. Ready ≤200-line non-ERP functions stay 93. The weighted headline stays about 20.4%. Non-ERP pending: 368 files / 181,311 lines (was 373 / 183,541). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-rest (demand ISO, industry themes, brochure photos, theme templates, packages)

Not complete.

- Ratchet 544 to 539. Five more files now have PHP 8.3 goldens and ASP.NET twins: `epc_demand_country_iso.php` (`PhpPlanQ1Rest`; ISO maps / parse / preview / import with injectable article-normalize; CHAR(2) migrate INSERT fatals on this MariaDB STRICT — no-op ISO3 path is golden), `epc_storefront_industry_themes.php`, `epc_cp_brochure_topic_photos.php`, `epc_portal_theme_templates.php` (industry maps injected so the portal parent stays unmentioned), `epc_portal_storefront_packages.php` (package dump omits header/home/footer sibling paths).
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Rest` (9). PlanQ1 suites 61 of 61. Full platform suite 6019 of 6019. Inventory content 500 to 505 of 952. Unnamed PHP functions 7,356 to 7,306. Ready ≤200-line non-ERP functions stay 93. The weighted headline stays about 20.4%. Non-ERP pending: 373 files / 183,541 lines (was 378 / 185,572). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-mark / pack (printProductBlock, FAQ, legal, DED mapping)

Not complete.

- Ratchet 548 to 544. Four more files now have PHP 8.3 goldens and ASP.NET twins: `content/shop/catalogue/helper.php` (`PhpPlanQ1Mark`; `printProductBlock` CRLF markup — tile/list, bookmarks, compare, admin, warehouse quick-edit, cart suggestion), `epc_ecomae_faq_data.php`, `epc_ecomae_legal_content.php`, `epc_ded_activity_mapping.php` (`PhpPlanQ1Pack`; DED audit/bridge take injectable maps so the industry-consolidation and portal parents stay unmentioned).
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Mark` (11) and `PlanQ1Pack` (3). All PlanQ1 suites 57 of 57. Full platform suite 6015 of 6015 after two source-walk tests also accept a worktree `.git` file. Inventory content 496 to 500 of 952. Unnamed PHP functions 7,368 to 7,356. Ready ≤200-line non-ERP functions stay 93. The weighted headline stays about 20.4%. Non-ERP pending: 378 files / 185,572 lines (was 382 / 187,848). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session. Leftover `ecomae_cpw_%` schemas: 0.

### Checkpoint 2026-10-09 — plan Q1-data / mig / hook / sec (industry catalogs, migrations, webhooks, security kernel)

Not complete.

- Ratchet 555 to 548. Seven more files now have PHP 8.3 goldens and ASP.NET twins: `epc_jewellery_retail_kiyasha_data.php`, `epc_fashion_retail_namshi_data.php`, `epc_electronics_retail_data.php` (`PhpPlanQ1Data`; footer uses portal-missing store names only), `epc_db_migrations.php` (`PhpPlanQ1Mig`; MariaDB DDL implicit-commit makes apply/rollback return `ok=false` after the row is written), `epc_webhooks.php` + `epc_events.php` (`PhpPlanQ1Hook`; no live HTTP; emit with no matching hooks; LIMIT binds skipped), `epc_security_kernel.php` (`PhpPlanQ1Sec`; CSRF `session_start` skipped). Do not mention industry helper / portal / `*_header_href` paths.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Data` (3), `PlanQ1Mig` (2), `PlanQ1Hook` (3) and `PlanQ1Sec` (2). Related suites 17 of 17. Inventory content 489 to 496 of 952. Unnamed PHP functions 7,496 to 7,368. Ready ≤200-line non-ERP functions stay 93. The weighted headline stays about 20.4%. Non-ERP pending: 382 files / 187,848 lines (was 389 / 190,451). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session.

### Checkpoint 2026-10-09 — plan Q1-twin (import, vault, licenses, BI, notifications)

Not complete.

- Ratchet 560 to 555. Five more files now have PHP 8.3 goldens and an ASP.NET twin (`PhpPlanQ1Twin`): `epc_import_orchestrator.php` (create / validate / chunk / dry-run / cancel / retry), `epc_document_vault.php` (folder / version / GDPR / search), `epc_onprem_licenses.php` (generate / activate / revoke / health; list LIMIT bind fatals on this MariaDB; signing key absent returns `signing_unavailable`), `epc_bi_metrics.php` (builtin metrics / snapshot / dashboard / fleet / compare; trend LIMIT and cleanup INTERVAL binds skipped), `epc_notifications.php` (send / list / prefs / digest; events include stays a gap). Do not write the proprietary `core/dp_` engine basenames as one path string. Industry templates stay skipped (`_base_template.php` parent).
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Twin/golden.json` (8 cases). The Twin suite is 4 of 4; Gov still 4 of 4. Inventory content 484 to 489 of 952. Unnamed PHP functions 7,550 to 7,496. Ready ≤200-line non-ERP functions stay 93. The weighted headline stays about 20.4%. Non-ERP pending: 389 files / 190,451 lines (was 394 / 191,974). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session.

### Checkpoint 2026-10-09 — plan Q1-gov (platform governance, tenant config)

Not complete.

- Ratchet 562 to 560. Two more files now have PHP 8.3 goldens and an ASP.NET twin (`PhpPlanQ1Gov`): `epc_platform_governance.php` (categories / defaults / seed / list / update / applies / active / branding-block), `epc_tenant_config.php` (groups / get / set / bulk / export / import / fleet; `epc_tenant_config_history` LIMIT bind fatals on this MariaDB). Industry templates stay skipped (`_base_template.php` parent).
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Gov/golden.json` (4 cases). The Gov suite is 4 of 4. Inventory content 482 to 484 of 952. Unnamed PHP functions 7,566 to 7,550. Ready ≤200-line non-ERP functions stay 93. The weighted headline stays about 20.4%. Non-ERP pending: 394 files / 191,974 lines (was 396 / 192,618). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session.

### Checkpoint 2026-10-09 — plan Q1-leftover + Q1-done (page cache, anomaly, CSS hubs, catalogs, SOC2)

Not complete.

- Ratchet 572 to 562. Twelve more files now have PHP 8.3 goldens and an ASP.NET twin (`PhpPlanQ1Left`, `PhpPlanQ1Done`): `epc_page_cache.php` (enabled / key / serve / flush / purge; inject cache dir; lock/exit goldens skipped), `epc_isolation_anomaly.php` (scan / record / resolve / fleet; missing audit tables stubbed; `epc_anomaly_list` LIMIT bind fatals on this MariaDB), `epc_integrations_hub_css.php`, `epc_industry_settings_css.php`, `epc_marketing_broadcast_css.php` (CSS bodies; broadcast panel parent stays a gap), `epc_cp_brochure_inventory.php`, `epc_ecomae_platform_capability_guides.php`, `epc_php_serving_deactivate.php`, `epc_cp_trace.php`, `epc_consulting_primeinvest_data.php`, `epc_ecomae_marketing_content.php`, `epc_soc2_compliance.php` (in-memory twin of the PDO helpers, MariaDB goldens). Industry templates stay skipped (`_base_template.php` parent).
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Left/golden.json` (11 cases) and `Fixtures/PlanQ1Done/golden.json` (9 cases). The Left + Done suites are 8 of 8; After + More + Next + PlanQ1 + ReadyNamed + NamedBatch still 24 of 24 (32 of 32 together). Inventory content 470 to 482 of 952. Unnamed PHP functions 7,628 to 7,566. Ready ≤200-line non-ERP functions stay 93. The weighted headline stays about 20.4%. Non-ERP pending: 396 files / 192,618 lines (was 406 / 196,954). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session.

### Checkpoint 2026-10-09 — plan Q1-after + Q1-more (live-bridge, catalog, layouts, subdomain, dealer, social CSS)

Not complete.

- Ratchet 578 to 572. Six more files now have PHP 8.3 goldens and an ASP.NET twin (`PhpPlanQ1After`, `PhpPlanQ1More`): `epc_portal_industry_live_bridge.php` (defs / URL / inject-merge / audit; SEO and portal parents stay gaps), `epc_portal_industry_catalog.php` (profile / title / unsplash / categories / render; electronics-retail images optional), `epc_storefront_layouts.php` (registry / default / meta / sections / active / js), `epc_industry_subdomain_router.php` (detect / resolve / bootstrap), `epc_dealer_portal.php` (tiers / register / list / order / auto-tier / fleet / get / update / suspend / activate / report; in-memory twin of the PDO helpers, MariaDB goldens; `epc_dealer_orders` LIMIT bind fatals on this MariaDB), `epc_social_media_hub_css.php` (CSS body; CP page-assets parent stays a gap). OEM `Functions.Common.php` stays skipped.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1After/golden.json` (4 cases) and `Fixtures/PlanQ1More/golden.json` (23 cases). The After + More suites are 8 of 8; PlanQ1Next + PlanQ1 + ReadyNamed + NamedBatch still 16 of 16 (24 of 24 together). Inventory content 464 to 470 of 952. Unnamed PHP functions 7,660 to 7,628. Ready ≤200-line non-ERP functions 97 to 93. The weighted headline stays about 20.4%. Non-ERP pending: 406 files / 196,954 lines (was 412 / 198,194). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session.

### Checkpoint 2026-10-09 — plan Q1-next (storefront logo, industry packs, promotions)

Not complete.

- Ratchet 581 to 578. Three more Q1-next files now have PHP 8.3 goldens and an ASP.NET twin (`PhpPlanQ1Next`): `epc_portal_storefront_logo.php` (hub setting / enabled / enqueue / trade label / markup / epartscart SVG; stub site profile, does not load `epc_portal.php`; hub and animated-logo parents stay gaps), `epc_industry_packs.php` (version / builtin catalog / seed / assign / tenant packs / fleet; in-memory twin of the PDO helpers, MariaDB goldens), `epc_promotions_engine.php` (create / list / apply / record usage / fleet; percentage, fixed, BOGO, free-shipping, min-order, max-discount, per-customer and usage-limit). `addContentToDump` stays unmentioned (`DP_ContentRecord` dump page). SKU-media stays skipped until `epc_sku_media.php`.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1Next/golden.json` (12 cases). The PlanQ1Next suite is 4 of 4; PlanQ1 + ReadyNamed + NamedBatch still 12 of 12 (16 of 16 together). Inventory content 461 to 464 of 952. Unnamed PHP functions 7,676 to 7,660. Ready ≤200-line non-ERP functions 112 to 97. The weighted headline stays about 20.4%. Non-ERP pending: 412 files / 198,194 lines (was 415 / 198,706). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session.

### Checkpoint 2026-10-09 — plan Q1 named helpers (bootstrap, deploy-auth, BOS lists, parity, cache, brand, genuine)

Not complete.

- Ratchet 590 to 581. Nine Q1 files from `CURSOR_NON_ERP_NEXT_PLAN.md` now have PHP 8.3 goldens and an ASP.NET twin (`PhpPlanQ1`): `epc_cp_bootstrap_light.php` (route / cookies / login / init — GET does not `session_start`), `epc_deploy_auth.php` (token / client IP / allowlist / lockdown / safe redirect; `epc_deploy_require_token` returns a status dict instead of `exit`), `epc_bos_security.php` (public/provider lists + action name; CSRF meta takes a token; entry_guard is a decision dict with no session start), `epc_php_reference_router.php` (surface / deep URI / Super-CP host), `epc_cp_common_parity.php` (targets / packs / host map), `epc_prices_office_storage_meta.php`, `epc_perf_cache.php`, `epc_portal_tenant_brand.php` (stub site profile; does not load `epc_portal.php`), `docpart_genuine_manufacturers.php` (UMAPI sync is URL-only). `epc_sku_media_cp_install.php` stays a gap until `epc_sku_media.php`.
- Evidence: PHP 8.3.6 produced `Fixtures/PlanQ1/golden.json` (35 cases). The PlanQ1 suite is 4 of 4. Inventory content 454 to 461 of 952, cp-page 253 to 254 of 523, root 73 to 74 of 491. Unnamed PHP functions 7,726 to 7,676. Ready ≤200-line non-ERP functions 162 to 112. The weighted headline stays about 20.4%. Non-ERP pending: 415 files / 198,706 lines (was 424 / 200,081). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session.

### Checkpoint 2026-10-09 — ready named helpers (accessories, synonyms, TDP, config meta, order guide)

Not complete.

- Ratchet 595 to 590. Five more self-contained non-ERP files now have PHP 8.3 goldens and an ASP.NET twin (`PhpReadyNamed`): `epc_accessories_taxonomy.php` (`epc_acc_taxonomy` / `epc_acc_classify` longest-keyword wins / `epc_acc_warehouse_regions`), `docpart_manufacturer_synonyms.php` (normalize, synonym map, names, equivalent, canonical map), `epc_tenant_data_policy.php` (sections + HTML; version `1.0.0`, month `October 2026` UTC), `epc_config_edit_meta.php` (group meta / item frontend effect / label), `epc_order_fulfilment_guide_data.php` (checklist strings + snapshot from structured inputs). The 650-line legal policy catalog and live interchange suggestions stay gaps. Search tabs, `printProducts*`, `side_menu`, page-builder render, BOC consoles, product-line href pages, CP `*_h` guide wrappers and `orders_background` stay skipped — see `CURSOR_NON_ERP_NEXT_PLAN.md`.
- Evidence: PHP 8.3.6 produced `Fixtures/ReadyNamed/golden.json` (18 cases). The ReadyNamed suite is 4 of 4. Inventory content 450 to 454 of 952, cp-page 252 to 253 of 523. Unnamed PHP functions 7,745 to 7,726. Ready ≤200-line non-ERP functions 180 to 162. The weighted headline stays about 20.4%. Non-ERP pending: 424 files / 200,081 lines (was 429 / 200,973). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session.

### Checkpoint 2026-10-09 — named-function storefront helpers (taxonomy, hashes, cache, legal, branding)

Not complete.

- Ratchet 603 to 595. Eight more self-contained non-ERP files now have PHP 8.3 goldens and an ASP.NET twin (`PhpNamedBatch`): `epc_auto_parts_taxonomy.php` (`epc_auto_tax_seed_tree`), `docpart_product_hash.php` (type-2 MD5 cart hash plus refresh for one product and a list), `epc_price_upload_guide_data.php` (`epc_guide_snapshot` / `epc_guide_channel_definitions`), `epc_crossbase_cache.php` (dir/key/path/read/write/stats; writes reject HTML ≤400 chars), `epc_complementary_parts.php` (normalize/search/render; interchange suggestions stay empty until those parents land), `epc_session_security.php` (validate/metadata/destroy take a session dictionary — GET does not call `session_start`), `epc_ecomae_legal_pages.php` (meta/canonical/related-links against a stub catalog; the 650-line policy catalog stays a gap), and `epc_branding.php` (system/hub/trade/tagline/hosted-by/CP context). Inventory `functions_unmentioned` is not a set of anonymous closures: each PHP function already has a name; the catalog in `docs/migration/inventory/PHP_UNMENTIONED_FUNCTIONS.md` assigns the PascalCase twin. Search tabs, `printProducts*`, `side_menu`, page-builder render, BOC consoles and `orders_background` stay skipped until their parents land.
- Evidence: PHP 8.3.6 produced `Fixtures/NamedBatch/golden.json` (24 cases). The NamedBatch suite is 4 of 4. Inventory content 442 to 450 of 952. Unnamed PHP functions 7,779 to 7,745. The weighted headline stays about 20.4%. Non-ERP pending: 429 files / 200,973 lines (was 437 / 201,982). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session.

### Checkpoint 2026-10-09 — small storefront fragments and retired PHP-only CP bootstraps

Not complete.

- Ratchet 749 to 717. Seven storefront fragments now have PHP 8.3 goldens and an ASP.NET twin: `set_cookie_products_style.php`, `search_strs_for_inputs.php`, `get_currency_indicator.php`, `product_description.php`, `shop_button.php`, `users_functions.php`, `cat_lang_general.php`. Five CP page scripts that answer with one JavaScript assignment (or the public multi-vendor sample CSV) are served by `PhpCpConfigScripts` at their PHP URLs, outside the CP login wall like PHP. Twenty PHP-only ops switches, CP CMS bootstrap scripts and unused `window.*` footer configs are retired in `inventory/PHP_RETIRED.tsv`.
- The products-style cookie follows PHP 8.3 `(int)` (scientific-notation strings included), PHP request-key rewriting (`.`, space and `+` become `_`), and `(int)` of a non-empty array is 1. GET does not mint a guest session.
- Evidence: PHP 8.3.6 produced `Fixtures/StorefrontFragments/golden.json` and `golden_http.json`. The fragment suite passed 5 of 5. The weighted headline stays about 20.4%.

### Checkpoint 2026-10-09 — industry footers, SEO helpers, logos and CP JS configs

Not complete.

- Ratchet 617 to 603. Fourteen more self-contained non-ERP files now have PHP 8.3 goldens and an ASP.NET twin (`PhpIndustryChrome`): the four industry footers (fashion, electronics, jewellery, consulting), the four SEO helper libraries (`store_name` / `tagline` / `apply_seo` / `patch_template` / scrub), `epc_storefront_animated_logos.php`, `epc_marketing_broadcast_templates.php` (`epc_mb_email_templates` / `epc_mb_whatsapp_templates` / `epc_mb_apply_template_vars`), `epc_channel_schema.php` (`epc_channel_ensure_schema` plus the six table names), `epc_cp_page_frame.php`, `epc_filemanager_config.php` (always emits `window.EPC_FILEMANAGER={…}` even without a session, like PHP), and `orders_items_config.php` (`window.EPC_OI={}` without an admin session). The industry data libraries stay gaps; the footer twins take the already-resolved columns/social/payments. Search tabs, `printProducts*`, `side_menu`, page-builder render, BOC consoles and `orders_background` stay skipped until their parents land.
- Filemanager and orders-items JS configs stay outside the CP login wall like PHP. GET does not mint a guest session.
- Evidence: PHP 8.3.6 produced `Fixtures/IndustryChrome/golden.json` (43 cases). The IndustryChrome suite is 4 of 4. Inventory content 430 to 442 of 952, cp-page 250 to 252 of 523. Unnamed PHP functions 7,819 to 7,779. The weighted headline stays about 20.4%. Non-ERP pending: 437 files / 201,982 lines (was 451 / 203,366). ERP finance (Devin) unchanged: 166 / 66,191.

### Checkpoint 2026-10-09 — Levam, news, favicon, auth links and industry heroes

Not complete.

- Ratchet 629 to 617. Twelve more self-contained non-ERP files now have PHP 8.3 goldens and an ASP.NET twin (`PhpSmallMore`): `levam.php`, `printSpecialSearches.php`, `modules/news/module.php`, `epc_retail_taxonomy.php`, `epc_portal_favicon.php`, `epc_storefront_auth_links.php`, `garage_login.php`, the Namshi mega-menu, and the four industry hero banners (consulting, fashion, jewellery, electronics). The hero data libraries stay gaps; the twins take the already-resolved copy. Search tabs, `printProducts*`, `side_menu`, page-builder render, BOC consoles and `orders_background` stay skipped until their parents land.
- Evidence: PHP 8.3.6 produced `Fixtures/SmallMore/golden.json` (26 cases). The SmallMore suite is 3 of 3. Inventory content 419 to 430 of 952, modules 6 to 7 of 31. Unnamed PHP functions 7,834 to 7,819. The weighted headline stays about 20.4%. Non-ERP pending: 451 files / 203,366 lines (was 463 / 204,226). ERP finance (Devin) unchanged: 166 / 66,191. GET does not mint a guest session.

### Checkpoint 2026-10-09 — next small storefront/CP includes after the eval-safe wrappers

Not complete.

- Ratchet 656 to 629. Twenty-six small non-ERP files now have PHP 8.3 goldens and an ASP.NET twin (`PhpNextSmallBatch`, `DocpartManufacturer`): `epc_portal_route_aliases.php`, `epc_moq_helpers.php`, `epc_fashion_taxonomy.php`, `epc_mobile_app_landing.php`, `modules/login/epc_social/app.php`, `tree_lists/helper.php` (`addItemToDump`), `epc_prices_ajax_init.php` (No DB Connect JSON), the marketing-broadcast wrapper plus `epc_marketing_broadcast_config.php`, `data_transfer.php`, Ilcats `content/originalnye-katalogi/settings.php` (full path — `settings.php` collides), CP `actions_alert.php` (distinct from the storefront twin), `document_control_guide.php`, `DocpartManufacturer.php`, `autoxp_clicks_control.php`, `check_admin_access.php`, `chose_car.php`, `epc_platform_health_checkup.php`, `about_program.php`, `auth_with_user.php` (GET returns Forbidden JSON and does not mint an impersonation session), `pyprices_tables_cleaner.php` (week-ago formula plus the DELETE SQL constants), `page_lang_main.php`, both language selectors (`cp/modules/lang/module.php` and `modules/lang/module.php`), `cp/content/users/helper.php` (`getInsertedGroups`, leaf-only — PHP recursively calls undefined `getAllowedGroups` on nested groups), and `epc_portal_industry_catalog_print.php`. The dead cart plugin `plugins/shop/cart/cart_handler.php` is retired (entire body commented out; ASP.NET cart merge is `StorefrontCart`). The marketing-broadcast panel body stays a gap: the wrapper missing-panel HTML is identical to PHP, but the panel path is concatenated in C# so the inventory does not treat the 430-line panel as mentioned.
- Autoxp live increment uses positional SQL (`SELECT` / `UPDATE` / `INSERT` on `shop_docpart_autoxp_clicks`). Prices ajax init and auth-with-user GET stay outside the CP login wall like PHP. GET does not mint a guest session.
- Evidence: PHP 8.3.6 produced `Fixtures/NextSmall/golden.json` (50 cases). The NextSmall suite is 4 of 4. Inventory content 409 to 419 of 952, cp-page 238 to 250 of 523, modules 4 to 6 of 31, plugins 3 to 3 of 6 (retired cart handler), root 71 to 73 of 491, retired 35 to 36. Unnamed PHP functions 7,851 to 7,834. The weighted headline stays about 20.4%. Non-ERP pending: 463 files / 204,226 lines (was 490 / 205,602). ERP finance (Devin) unchanged: 166 / 66,191.

### Checkpoint 2026-10-09 — CP eval-safe wrappers and small storefront/CP scripts

Not complete.

- Ratchet 695 to 656. Thirty-nine small non-ERP files now have PHP 8.3 goldens and an ASP.NET twin: the eval-safe CP page wrappers (payments, POS, channels, marketing, tenant hub, customer management, procurement, multi-vendor upload, logistics carriers, Document Control, CP guideline, price/OMS/fulfilment/WhatsApp/channels/logistics guides, bulk-upload hub, web-tracker include, `epc_cp_auth_settings.php`), `related_products.php`, both `customer_mgmt_guide.php` copies, `returns.php` + `router.php`, `epc_cp_page_guard.php`, `epc_cp_fast_tenant.php`, `epc_portal_industry_switch.php`, the health/governance/web-tracker/document-control JS config scripts, `api_debug.php`, `eparts_cata.php`, `eparts_product.php`, `epc_tax_advisory_taxonomy.php`, `epc_marketing_schema.php`, `prices_upload/guide.php`, and the get-in-office obtain-mode snippets (`manager_interface.php`, `show_actual_info.php`, `show_details.php`). Each wrapper is the session gate and include-or-alert only; the included module body stays its own gap. The epc_carriers copies of the same basenames stay gaps on purpose.
- Industry switch sanitizes `[^a-z0-9_]` and rejects `://` or newline in `back`. JS configs follow PHP `json_encode` flags (`JSON_HEX_TAG|JSON_HEX_AMP` vs `JSON_UNESCAPED_SLASHES`). `eparts_product` uses RFC3986 `http_build_query` and default `json_encode` (slashes escaped). GET does not mint a guest session. Industry switch stores the filter in a cookie (`epc_cp_industry_filter`) instead of PHP `$_SESSION`.
- Evidence: PHP 8.3.6 produced `Fixtures/CpSmallPages/golden.json` (66 cases). The small-pages suite passed 4 of 4. Inventory content 405 to 409 of 952, cp-page 203 to 238 of 523. Unnamed PHP functions 7,860 to 7,851. The weighted headline stays about 20.4%. Non-ERP pending: 490 files / 205,602 lines (was 529 / 206,769). ERP finance (Devin) unchanged: 166 / 66,191. The ionCube leftover `cp/old php/index.php` is retired in `PHP_RETIRED.tsv` (the inventory already counted it as mentioned via the `index.php` basename collision, so the ratchet does not move for it).

### Checkpoint 2026-10-09 — next small storefront/CP includes and PHP-only CMS classes

Not complete.

- Ratchet 717 to 695. Eleven small includes now have PHP 8.3 goldens and an ASP.NET twin: `users_agreement.php`, `epc_cata_bridge.php`, `epc_eparts_product_route.php`, `ucatalog_index.php`, `epc_pos_shell_js.php`, `docpart_href.php`, `error_pages.php`, `commerce_data_page.php`, `modal.php`, `set_edit_mode_cookie.php` (JSONP at the PHP URL, outside the CP login wall like PHP), and `cp/modules/logout/module.php`. Eleven PHP-only files are retired in `inventory/PHP_RETIRED.tsv`: the dead `email.php` probe (`exit` before any send), the CRM schema shim, the social-hub config proxy, the PHP CMS classes `DP_Template` / `DP_Module` / `DP_Content` / `DP_GeoNode` / `DP_GroupRecord` / `DP_Product` / `DP_TreeListItem`, and the deprecated `home_professional_showcase.php` alias.
- `http_build_query` for the eParts product URL follows PHP RFC1738 (spaces as `+`, null values omitted). `json_encode` for the edit-mode JSONP follows PHP's default (slashes and non-ASCII escaped). GET does not mint a guest session.
- Evidence: PHP 8.3.6 produced `Fixtures/TinyPages/golden.json` (27 cases). The TinyPages suite passed 4 of 4. Inventory content 400 to 405 of 952, cp-page 199 to 203 of 523, api 28 to 29 of 29, plugins 2 to 3 of 6, retired 24 to 35. Unnamed PHP functions 7,866 to 7,860. The weighted headline stays about 20.4%. Non-ERP pending: 529 files / 206,769 lines (was 551 / 207,335).

### Checkpoint 2026-10-09 — catalogue product count (`ajax_get_products_count.php`) like PHP

Not complete. This is the first data-plane slice of `printProducts.php` and `printProducts_2.php`. Neither list shell is ported by it.

- The ratchet stays at 749 and the weighted headline stays about 20.4%. The count endpoint already had a port. This checkpoint proves it against PHP and fixes the differences.
- Before the fixes, 39 of 182 PHP golden cases differed. All now match. The price filter used the raw storage price instead of PHP's `customer_price` (storage currency rate plus the office, storage and user-group markup band). The filter and range checks now follow PHP 8 loose comparison and `floatval`. `products_ids_str` was ignored and is now supported. Search now includes the apostrophe in `htmlspecialchars`, the article-property lookup and the imported discovery queue. List properties keep the previous OR/AND joiner like PHP.
- The count, list and page endpoints now read the session user (no guest session is created) so the markup group matches PHP.
- Evidence: PHP 8.3.6 ran the real `ajax_get_products_count.php` and `query_products_all.php` for 183 cases (176 integer outputs, 7 recorded PHP errors) on isolated databases, twice with identical output. The full suite passed 5,918 of 5,918 with 0 warnings and 0 errors. Post-test invariants: `ecomae_cpw_%` = 0, `docpart.users` = 2, `ecomae.users` = 2, `docpart.sessions` = 73.
- Intentional deviations, pinned by tests: PHP interpolates `category_id` and `products_ids_str` raw, ASP.NET accepts integers only. Where PHP throws (no offices, malformed list options, a search of one-letter tokens, string `properties_list`), ASP.NET returns an unfiltered count, ignores the filter, or rejects the request.
- Not golden-covered: the `epc_electronicae_storefront_active` category subtree, NULL currency rates, duplicate property rows, and the real `DP_User` session validity (covered by an ASP.NET HTTP test instead).
- Also fixed: the reduced `shop_orders_items_statuses_ref` fixture in `StorefrontPhpShopTests` lacked the `order` column that PHP's `ORDER BY `order`` needs (the column exists in the real table), so the order-payment test failed on `main` after the previous checkpoint.
- Next: `query_products_show.php` (sort, pagination, `generate_products_objects_by_sql.php`), then `ajax_get_products_page.php` and `printProductBlock()`.

### Checkpoint 2026-10-09 — catalogue product list and page ids (`ajax_get_products_list.php`, `ajax_get_products_page.php`) like PHP

Not complete. This is the second data-plane slice of `printProducts.php` and `printProducts_2.php`: which product ids the list and page scripts select, and in what order. The product block markup is not ported.

- The ratchet stays at 749 and the weighted headline stays about 20.4%.
- Before this slice the ASP.NET page ordered by product id only and ignored `products_sort_mode`. It now runs PHP's two statements: the page window (`query_products_show.php`: `GROUP BY id` over the per-office `UNION`, `MIN(IFNULL(customer_price, 1e10))` or `MAX(IFNULL(customer_price, 0))`, the `CASE` price grouping, name / random / price order, `LIMIT from, max`) and the final per-storage-row statement whose first row per product decides the render order. The paging window follows PHP 8 arithmetic (`startFrom * productsPerPage`, an empty maximum becomes `0, 100`). A bad JSON request is now the unfiltered catalogue, as in PHP, not "Product request is empty.". The empty page is byte-identical to PHP, including its indentation.
- Evidence: PHP 8.3.6 ran the real `ajax_get_products_page.php` (which includes the real `ajax_get_products_list.php`, `query_products_all.php`, `query_products_show.php` and `generate_products_objects_by_sql.php`) for 441 cases on isolated databases and recorded the ordered keys of `$products_objects`. 410 cases have PHP output and 31 are recorded PHP errors. All 441 match ASP.NET (31 through pinned deviations). The full suite passed 5,922 of 5,922 with 0 warnings and 0 errors.
- Covered: price and name sort in both directions, the direction and field edge values, random (compared as a set), equal prices, equal and case-insensitive names, a missing translation, multi-office `UNION` duplicates (PHP groups by product: ascending takes the lowest office price, descending the highest), one to three offices, guest / registered / wholesale markup bands, product block types 1 to 7, search, `products_ids_str`, price filter with sort and paging, unpriced products, and first, middle, last and out-of-range pages with every kind of limit value.
- Intentional deviations, pinned by tests: where PHP throws (a non-numeric or array paging operand, a negative or fractional `LIMIT`, an array sort direction, no office, a JSON string request) ASP.NET selects no product and the page shows the "nothing found" block instead of a 500. Request text never reaches SQL. One-letter search tokens and a NULL currency rate keep the count endpoint's deviations. Two translations for one caption still raise the MariaDB error, like PHP. When the pricing tables are absent (PHP cannot run) the catalogue is ordered by id.
- Not golden-covered: the markup of `printProductBlock()` (the harness stubs it), product object fields (price rounding, stock priority, buttons, images, stickers, ratings), the `epc_electronicae_storefront_active` category subtree, the per-request language of the HTTP endpoints (the tests pass it), `RAND()` order, and sub-select errors in columns that only fill product object fields.
- Next: `printProductBlock()` and `generate_products_objects_by_sql.php` product objects, then the `printProducts.php` and `printProducts_2.php` shells.

### Checkpoint 2026-10-09 — storefront order background references (`orders_background.php`) like PHP

Not complete.

- The ratchet stays at 749 and the weighted headline stays about 20.4%. The storefront and Control Panel helpers share the same basename, and the current inventory path matcher would falsely mark both complete if runtime source named the storefront path. The Control Panel helper has additional manager-office and storage rules and remains open.
- `StorefrontOrdersBackground` now loads PHP's shared order-status rows, line-status rows, line statuses excluded from totals, and office rows. It preserves `SELECT *` fields, status ordering, duplicate-ID replacement, and PHP 8.3's `NULL == 0` versus `'' != 0` behavior for `count_flag`.
- `StorefrontMyOrders` and `StorefrontMyOrdersItems` now consume that common loader instead of maintaining separate reference queries. The helper is read-only.
- Evidence: PHP 8.3 ran the authoritative helper for empty and populated/duplicate/null cases on isolated databases. The golden records include all fields and before/after row counts. The loader plus both consumer regression suites passed 16 of 16; the full suite passed 5,914 of 5,914 after a clean build with 0 warnings and 0 errors.
- All four customer order-list route aliases return 200 with fake cookies. `docpart.sessions` stayed 73 before and after. Post-test invariants: `ecomae_cpw_%` = 0, `docpart.users` = 2, `ecomae.users` = 2.
- Next storefront gap: `content/shop/catalogue/printProducts.php`.

### Checkpoint 2026-10-09 — storefront product information (`printProduct_Info.php`) like PHP

Not complete.

- Ratchet 751 to 749: `content/shop/catalogue/printProduct_Info.php` and its customer-page owner `product_page_for_customer.php` close. Inventory content 391 to 393 of 952. Unnamed PHP functions 7,870 to 7,866. The weighted headline (about 20.4%) is unchanged.
- All five product route forms now render through `StorefrontProductPage`: the gallery and no-image state; main office offer, price and currency display; stock, reserved and delivery states; request fallback; bookmark and compare state; administrator link; property table; description, reviews and evaluation hooks; variants, related and similar products.
- The previously ported multi-office offer table and add-to-basket script remain the authoritative offer and basket slices. Existing PHP endpoints remain authoritative for bookmark, compare and evaluation mutations.
- Product GETs validate an existing session but do not mint guest sessions. Intended security deviation: database, cookie and JavaScript values that PHP prints raw are context-encoded.
- Evidence: PHP 8.3 generated four catalogue goldens for no image, local image, auto-price images and supported property types. PHP 8.3 cannot execute its tree-property branch because `count(property_variants)` is fatal, and this PHP file does not map date, file or image property types; those limits are recorded rather than claimed as golden-covered. Runtime tests additionally cover main-offer states, currencies, identities, tabs, related products, no-offer fallback, missing products and hostile values.
- Clean build: 0 warnings and 0 errors. Full suite: 5,913 of 5,913 passed. All five route aliases return 200 with fake cookies; `docpart.sessions` stayed 73 before and after. Post-test invariants: `ecomae_cpw_%` = 0, `docpart.users` = 2, `ecomae.users` = 2.
- The storefront order-background helper is closed by the next checkpoint above.

### Checkpoint 2026-10-09 — checkout confirmation (`checkout_confirm.php`) like PHP

Not complete.

- Ratchet stays at 751 because the earlier ASP.NET checkout surface had already mapped this PHP file. Unnamed PHP functions 7,871 to 7,870. The weighted headline (about 20.4%) is unchanged.
- `/en/shop/checkout/confirm` and `/shop/checkout/confirm` now print PHP's confirmation page through `StorefrontCheckoutConfirm`:
  - signed-in and existing-guest ownership, checked cart lines, quantities, terms, currency formatting and totals;
  - obtaining-cookie validation and redirects, the selected obtaining handler details, complementary parts, message and buyer PO fields;
  - guest phone/e-mail validation, the user agreement, trade-account blocking, confirmation button and loader;
  - the client still posts to the ported `/content/shop/order_process/ajax_checkout_create.php`, so there is one checkout side-effect engine.
- A narrow pre-Razor middleware returns PHP's raw JSON response for a guest without an existing session. It does not mint or write a session. The `/shop/checkout_confirm` compatibility aliases intentionally remain on the earlier combined checkout page.
- Intended security deviation: PHP prints several database, cookie, HTML and JavaScript values without context escaping. The port escapes them.
- Evidence: `Fixtures/CheckoutConfirm/harness.php` ran PHP 8.3 for 14 isolated cases covering sessionless JSON, every redirect branch, missing handler, both line types, guest ownership and validation, empty checked cart, currency modes, trade block, wholesale PO, language prefix, missing regexes and hostile values. Exact byte length/SHA-256 matches and the cart snapshot proves the renderer is read-only. Full suite 5,909 of 5,909 passed after a clean build.
- Kestrel GETs without cookies return status 200, `application/json; charset=utf-8`, and `{"status":false,"code":"incorrect_session","message":""}` on the local tenant. `docpart.sessions` stayed 73 before and after. Post-test invariants: `ecomae_cpw_%` = 0, `docpart.users` = 2, `ecomae.users` = 2.

### Checkpoint 2026-10-09 — the storefront cart (`cart.php`) like PHP

Not complete.

- Ratchet 752 to 751. Inventory content 390 to 391 of 952. Unnamed PHP functions 7,873 to 7,871. The weighted headline (about 20.4%) is unchanged.
- `/en/shop/cart` and `/shop/cart` now print PHP's cart through `StorefrontCart`:
  - the guest commerce login gate, signed-in and guest ownership, empty state, cart table, checked state, quantity controls, totals and checkout link;
  - catalogue and supplier lines, delivery terms, minimum quantities, product images, repricing links, WhatsApp sharing and the garage-notepad modal;
  - trade pending/rejected checkout blocking and language-prefixed destinations;
  - PHP's GET-side writes: clearing a blocked guest cart and unchecking inaccessible catalogue lines. The port adds shopper/session ownership predicates to both writes.
- The existing ASP.NET legacy AJAX endpoints remain the single implementation for quantity, delete, order selection and garage-notepad mutations.
- Runtime translations use `StorefrontPhpTranslator`. Intended security deviation: PHP prints several database and JavaScript values without context escaping; the port escapes them.
- Evidence: `Fixtures/Cart/harness.php` ran PHP 8.3 for 15 isolated cases. Exact UTF-8 byte length and SHA-256 plus post-GET cart state cover blocked/no-session guests, guest clearing, signed and guest empty/populated carts, both line types, inaccessible-line writes, images and URLs, currency/term branches, trade states, garage states, language prefix and hostile values. The full suite passed 5,892 of 5,892.
- Kestrel GETs for both routes return 200 with fake signed-in cookies. The local tenant lacks the cart schema, so it prints the safe fallback; the golden databases prove the complete page. `docpart.sessions` stayed 73 before and after. Post-test invariants: `ecomae_cpw_%` = 0, `docpart.users` = 2, `ecomae.users` = 2.

### Checkpoint 2026-10-09 — the customer order line list (`my_orders_items.php`) like PHP

Not complete.

- Ratchet 753 to 752. Inventory content 389 to 390 of 952. Unnamed PHP functions 7,876 to 7,873. The weighted headline (about 20.4%) is unchanged.
- `/en/shop/orders/items` and `/shop/orders/items` now print PHP's standalone customer line list through `StorefrontMyOrdersItems`:
  - PHP's visitor login panel and signed-in customer ownership scope;
  - the date, order, order-status, line-status, paid and office filters from `my_orders_items_filter`, with PHP cookie decoding and loose comparisons;
  - PHP's sort-cookie whitelist and direction switching;
  - the line table with order links, item data, prices, quantities, statuses and offices, plus PHP's pagination quirks.
- Intended security deviation: PHP prints several cookie and database values without context escaping. The port escapes them.
- Evidence: `Fixtures/MyOrdersItems/harness.php` ran PHP 8.3 for 35 isolated cases covering visitor, empty/default lists, every filter, invalid and scalar cookies, sort variants and pagination positions. All 35 goldens regenerate byte-identically. The C# parity test checks 34 byte-for-byte cases and separately tests hostile-value escaping. Full suite 5,888 of 5,888 passed.
- Kestrel GETs for both route forms and `?page=2` return 200 with fake signed-in cookies. `docpart.sessions` stayed 73 before and after. Post-test invariants: `ecomae_cpw_%` = 0, `docpart.users` = 2, `ecomae.users` = 2.

### Checkpoint 2026-10-09 — the customer order card (`my_order.php`) like PHP

Not complete.

- Ratchet stays at 753 because the earlier ASP.NET order surface had already mapped this PHP file in the inventory. The weighted headline (about 20.4%) is unchanged.
- `/en/shop/orders/order` and `/shop/orders/order` now print PHP's signed-in customer order card through `StorefrontMyOrder`:
  - ownership and office checks, alerts, order header, garage links, line statuses and totals;
  - customer-currency prices, paid state, customer balance payment, partial payment, finite and unlimited overdraft, pay on place, payment picker and protocol calls;
  - UAE VAT summary, customer print links, obtaining details, return/cancel controls and the legacy message client;
  - PHP's GET side effect marks unread store messages read only after customer ownership and office validity succeed.
- The pay-on-place POST is CSRF-checked and ownership-gated. It changes the payment type, writes PHP's order log line and runs the `for_paid` robot status protocol.
- Intended security deviation: PHP prints several database values and JavaScript string values without context escaping. The port escapes them.
- Evidence: `Fixtures/MyOrder/harness.php` ran PHP 8.3 for 13 cases on isolated databases: visitor, missing and foreign orders, unpaid/partial/paid orders, sufficient and insufficient balances, finite and unlimited overdraft, negative balance, and wholesaler office-scoped balance. `StorefrontMyOrderTests` matches those cases byte for byte and separately tests hostile values, message-read ownership and pay-on-place authorization. Full suite 5,883 of 5,883 passed.
- Kestrel GETs for both route forms return 200 with fake signed-in cookies. The local tenant lacks the order schema, so it prints the safe fallback; the golden databases prove the signed-in page. `docpart.sessions` stayed 73 before and after. Post-test invariants: `ecomae_cpw_%` = 0, `docpart.users` = 2, `ecomae.users` = 2.
- The standalone customer line list (`my_orders_items.php`) remains next.

### Checkpoint 2026-10-09 — the customer orders list (`my_orders.php`) like PHP

Not complete.

- Ratchet 754 to 753. Inventory content 388 to 389 of 952. Unnamed PHP functions 7,890 to 7,876. The weighted headline (about 20.4%) is unchanged.
- `/en/shop/orders` and `/shop/orders` now print PHP's page through `StorefrontMyOrders` (`StorefrontMyOrdersApp.razor`):
  - a visitor gets the login panel (string 4559) with PHP's general login form, postfix `my_orders` and target `shop/orders`;
  - a customer gets the filter bar (dates, order number, paid, payment type, garage, status), read from the `my_orders_filter` cookie the way PHP reads it: URL-decoded, first value wins, PHP 8 loose comparisons. An invalid cookie still filters on `paid = 0` and `paid_type = 0`, as in PHP. `?garage=` for one of the customer's cars resets the filter to that car and prints PHP's cookie script;
  - the orders table sorted by the `my_orders_sort` cookie (PHP's column whitelist), with unread message badges, the garage icon, the hidden line rows and `?read=0`. The sums leave out line statuses that do not count;
  - PHP's pagination, whose links have no language prefix, as in PHP;
  - the guest order note at the end.
- The order card (`/shop/orders/order`) and the line list (`/shop/orders/items`) still use the earlier ASP.NET page until they are ported.
- Intended deviation: PHP prints the filter cookie values, garage captions and line texts without escaping. The port escapes them.
- Evidence: `Fixtures/MyOrders/harness.php` ran the real PHP 8.3 page for 26 cases on throwaway databases, each in its own process: visitor, default list, every filter field, invalid and scalar cookies, loose cookie values, four sort cookies, `?garage=` matched and foreign, `?read=0`, six pagination positions, and a customer with no orders. `StorefrontMyOrdersTests` matches all 26 byte for byte, and it also covers escaping, cookie decoding and pagination. Full suite 5,873 of 5,873 passed. Kestrel GETs with fake cookies print the visitor panel and the login form. No session rows were created.
- Local data limit: no local database has the order tables, so the signed-in list can only be checked by the golden test here.

### Checkpoint 2026-10-08 — the guest order page (`my_order_not_authorized.php`) like PHP

Not complete.

- Ratchet 755 to 754. Inventory content 387 to 388 of 952. Unnamed PHP functions 7,896 to 7,890. The weighted headline (about 20.4%) is unchanged.
- `/en/shop/orders/zakaz-bez-registracii` (also without the language prefix, plus `/shop/orders/guest` and `/storefront/guest-order-app`) now prints PHP's page through `StorefrontOrderNotAuthorized`:
  - PHP's alert block (`actions_alert.php`, CRLF, `StorefrontActionsAlert`) for `success_message`, `error_message`, `warning_message` and `info_message`;
  - with `order_id`: the order summary with PHP's status, line status and office lookups, the paid badge and the sums, then the payment block (the payment method picker, partial payment with `min_pay`, and "pay on place" for unpaid orders with no paid type). A missing order prints PHP's redirect script with string 4525;
  - the order lookup panel.
- The "pay on place" POST is handled by `StorefrontOrderNotAuthorizedPostMiddleware`. It checks the CSRF key, accepts only guest orders (`user_id=0`, `paid_type=0`, existing office), sets `paid_type=1`, writes PHP's log line and moves the order to the `for_paid` status through the order protocol. Then it redirects like PHP.
- Intended deviation: PHP prints the raw `order_id` into the page and the script. The port escapes it in HTML and gives the script the matched order id.
- Evidence: `Fixtures/OrderNotAuthorized/harness.php` ran the real PHP 8.3 page for 10 cases on throwaway databases. `StorefrontOrderNotAuthorizedTests` matches all 10 byte for byte. It also covers the hostile `order_id`, the pay-on-place write and log, the protocol failure warning, and six POSTs that must not write anything. Full suite 5,863 of 5,863 passed. Kestrel GETs with fake cookies print the alerts and the lookup panel. No session rows were created.
- Local data limit: no local database has `shop_orders_statuses_ref`, so `?order_id=` there shows "Order lookup is unavailable right now." The page now logs that failure as a warning.

### Checkpoint 2026-10-08 — the customer quotes page (`my_quotes.php`) like PHP

Not complete.

- Ratchet 756 to 755. Inventory content 386 to 387 of 952. Unnamed PHP functions stay at 7,896. The weighted headline (about 20.4%) is unchanged.
- `/en/shop/quotes` (also `/shop/quotes` and `/storefront/quotes-app`) now prints PHP's page through `StorefrontMyQuotes`:
  - a visitor gets the login panel (string 4559) with PHP's general login form, postfix `my_quotes`. A `?id=` adds the "is protected" heading and the control panel link;
  - a customer sees only their own quotes, and an administrator (CP admin session) sees all quotes, with the User column and the administrator texts;
  - the detail shows status, update date, both notes (`nl2br`), and one row per line. Alternatives replace the offer only when `offer_alternative` is 1 and both the alternative brand and article are set. Prices are in the visitor's currency (`epc_currency.php`: dealing currency, then the `epc_currency` cookie, then the `epc_country` cookie), in the configured display mode;
  - a draft with lines gets the note box and the submit script, and a quoted quote gets the accept script. Both post to the ported `ajax_quote_submit.php` and `ajax_quote_accept.php`.
- The earlier ASP.NET redesign of this page is gone. That includes its manual "add a quote line" form, which PHP does not have. The `/storefront/quotes/*` POST endpoints stay mapped.
- Evidence: `Fixtures/MyQuotes/harness.php` ran the real PHP 8.3 page for 16 cases on throwaway databases, each in its own process:
  - visitor, customer and administrator views of the list and of draft, quoted, foreign, empty, odd-status (`?id=9abc`) and missing quotes;
  - USD by cookie with `short_name_after`, a country cookie with `sign_after` and no language prefix, and mode `no`.

  `StorefrontMyQuotesTests` matches all 16 byte for byte. Full suite 5,851 of 5,852 passed; the one failure was a source check for the old page markup, which now checks the renderer and passes. Kestrel GETs with fake cookies print the visitor panel and the login form, with and without `?id=5`. No session rows were created during testing.
- Local data limit: the local `docpart` tenant has no `shop_quote_requests` table, so signed-in and administrator views show the "Quotes are unavailable right now" message there, where PHP would fail with an error page. The golden test covers those views.

### Checkpoint 2026-10-08 — the bottom panel cart refresh (`bottom_panel.php` cart block) like PHP

Not complete.

- Ratchet stays at 756, because `modules/` is not in the gap inventory. Unnamed PHP functions 7,900 to 7,896 (PHP files that also define `updateCartInfo`, `showAdded` or `hideAdded`, such as the expan, modex and limo desktop templates). The weighted headline (about 20.4%) is unchanged.
- The storefront desktop chrome now prints the cart part of PHP's bottom panel through `StorefrontBottomPanelCart`:
  - the "added" label text (string 4225) in `#mark_popup_added`;
  - `updateCartInfo()`, which posts the session's `csrf_guard_key` to the ported `ajax_get_cart_info.php` and fills `#cart_items_count`;
  - `showAdded()` and `hideAdded()`;
  - the on-load `updateCartInfo()` when the `session` cookie is set;
  - the `header_cart_items_count` class switch, only for front template 63.

  The key comes from the `sessions` row for the `session` and `u_id` cookies (PHP `DP_User::getUserSession()`). The add-to-cart scripts on the product and search pages now refresh the cart count and show the label after a successful add.
- jQuery is loaded from `/lib/jQuery/jQuery.js` only when the page has not already loaded it, because PHP's template always has jQuery and many ASP.NET storefront pages do not.
- Known limit: PHP's authentication plugin creates a guest session at the start of every page, so the bottom panel always has a key. ASP.NET creates guest sessions only on the cart, checkout, login, register and product pages, after the chrome has rendered. On a visitor's very first page, the first add therefore refreshes the count only on the next page load. A stale `session` cookie with no row prints an empty key, as PHP would. Porting the plugin's guest-session step for every storefront page is a separate item.
- Still a gap: the rest of the bottom panel. That covers the seller-request and admin "edit text" buttons, the compare and bookmarks scripts (the chrome already counts those from cookies), and the template-63 brand quick-pick.
- Evidence: `Fixtures/BottomPanelCart/harness.php` ran the real PHP 8.3 snippet for 4 cases: with and without a session cookie, template 63 and another template. The PHP file uses CRLF line endings, which the port keeps. `StorefrontBottomPanelCartTests` matches all 4 byte for byte. Full suite 5,851 passed. Kestrel GETs of `/en/shop/product?id=1` and `/en/` print the script; the on-load call appears only with a `session` cookie, and `/lib/jQuery/jQuery.js` returns 200. No session rows were created during testing.

### Checkpoint 2026-10-08 — the product page offers and the add-to-cart script (`common_add_to_basket.php`) like PHP

Not complete.

- Ratchet 757 to 756. Inventory content 385 to 386 of 952. Unnamed PHP functions 7,907 to 7,900. The weighted headline (about 20.4%) is unchanged.
- The product page (`/en/shop/product?id=`) now prints PHP's offers block through `StorefrontProductOffers`, followed by `StorefrontCommonAddToBasket` (PHP's `purchase_action` and quantity script, with the session's `csrf_guard_key` and strings 4336, 4524 and 4313). The block shows one section per office of the `my_city` geo node; offices with no stock show "request from seller" (strings 4165 and 4115).
- Each row is priced like PHP:
  - The warehouse price is converted with the storage currency rate.
  - The office/storage/group markup from `shop_offices_storages_map` is applied.
  - The CP sell-from-purchase stack (`epc_pricing.php`) then replaces that price when the offer is visible.
  - Delivery days come from `arrival_time`, `time_to_exe` and the office's `additional_time`, then `price_rounding` 1, 2 or 3 is applied.
- `check_hash` is made from the shown price, so "Add to cart" posts rows that the ported `ajax_add_to_basket.php` accepts. `EpcPricing.SellFromPurchaseAsync` now returns PHP's `visible` flag and the unrounded price; `ApplySellFromPurchaseAsync` still rounds as before.
- Removed: the page's "Add to cart" form, which did a GET to the cart page and added nothing.
- At this checkpoint the rest of the PHP customer product page (the main offer box, related products and tabs) remained open; the 2026-10-09 product-information checkpoint closes it.
- At this checkpoint the bottom panel's `updateCartInfo()` and `showAdded()` remained next; the later bottom-panel checkpoint closes that slice.
- Evidence: `Fixtures/ProductOffers/harness.php` ran the real PHP 8.3 code (`get_customer_offices.php`, `epc_pricing.php` and the page's offers snippet) for 13 cases, each in its own process on a throwaway database:
  - two offices and one office with no stock;
  - the guest-margin setting;
  - rounding modes 1, 2 and 3;
  - the retail floor and a wholesale profile;
  - a hidden storage and a stacked storage margin;
  - an empty `min_order`, no city cookie, and a product with no stock.

  `StorefrontProductOffersTests` matches every case byte for byte, plus the script. Full suite 5,849 passed. A Kestrel GET of `/en/shop/product?id=1` returns 200. The local database has no catalogue products and no `shop_storages_data` table, so the live page cannot show offers on this VM.

### Checkpoint 2026-10-08 — the checkout login offer (`checkout_login_offer.php`) like PHP

Not complete.

- Ratchet 758 to 757. Inventory content 384 to 385 of 952. Unnamed PHP functions stay at 7,907 (the page defines none). The weighted headline (about 20.4%) is unchanged.
- `/shop/checkout/login_offer` now prints PHP's page through `StorefrontCheckoutLoginOffer`: the shared sign-in form (`login_form_general.php`, postfix `login_offer_1`, target `shop/checkout/how_get`) and the "Order without registration" button (string 4523) only when `config.php` has `order_without_auth == 1`, compared like PHP 8 (`" 1"` and `1.0` count, `yes` and empty do not). The old page always showed a hand-written "Continue as guest" button.
- Sign-in from the page works: `StorefrontLoginPostMiddleware` now also takes the `authentication` POST on the login offer path, and a failed sign-in re-renders the page with PHP's alert.
- Fixed: the guest-order gate (`ajax_checkout_create.php` and the checkout write service) read `config_items.value`, a column that does not exist, so guests could always order. It now reads `order_without_auth` from `config.php` like PHP: unset allows guests, set and `!= 1` refuses them with string 4470.
- Intended deviation: PHP's `epc_redirect_safe_target()` turns the page's relative target into `/`, so a customer who signs in mid-checkout lands on the home page. That exact target now continues to `/{lang}/shop/checkout/how_get`. Every other target is still filtered as in PHP.
- Evidence: `Fixtures/LoginForm/harness.py` ran the real page (PHP 8.3) for 9 new cases (guest with the setting unset, on, off, `" 1"`, `1.0`, `yes`, empty; signed in with it on and unset). `StorefrontLoginFormTests` matches all of them, plus 24 PHP-checked loose `== 1` values, the path match, the target and the guest refusal. Full suite 5,847 passed. A Kestrel GET of `/en/shop/checkout/login_offer` returns 200 with PHP's form. The captions are blank on this VM only because the local database has no `lang_text_strings_translation` table, which also blanks `/en/users/login`.

### Checkpoint 2026-10-08 — the customer's offices (`get_customer_offices.php`) like PHP

Not complete.

- Ratchet 759 to 758. Inventory content 383 to 384 of 952. Unnamed PHP functions 7,908 to 7,907. The weighted headline (about 20.4%) is unchanged.
- `StorefrontCustomerOffices` is now the one port of the include. It replaces three private copies in the cart, bulk upload and catalogue code. The cart quantity change, the pay-on-place office check, bulk upload (`epc_bulk_helpers.php`) and the catalogue count, list and page now all use it.
- It follows PHP exactly:
  - The `my_city` cookie is bound as the raw string, so MySQL compares it as a number: `5abc` and ` 5` are geo 5, and `abc` is geo 0.
  - An empty cookie counts as unset, so the first `shop_geo` node is used.
  - With no mapped office, the first `shop_offices` row is used.
  - The office order is the map's row order.
- Fixed:
  - Bulk upload and the catalogue price filter ignored the cookie and always used the first geo node.
  - The cart parsed the cookie as an integer, so `5abc` fell back to the first geo node.
- Evidence: `Fixtures/CustomerOffices/harness.php` ran the real PHP file (8.3) for 18 cookie and database cases on throwaway databases. `StorefrontCustomerOfficesTests` matches all 18 on throwaway databases, which are then dropped.
- Kept deviation: the catalogue still treats missing `shop_geo` or map tables as "no mapped office", where PHP stops with a fatal error. Elsewhere a missing table stays an error, as before.
- Moved to step 5: `epc_bos_ajax_login.php`. It sets the PHP BOS session (`epc_bos_set_context`), which only the PHP BOS unified shell reads. `/bos/login` already signs in through ASP.NET for `/bos/app`, as recorded in `LegacySessionParityReporter`. So the BOS login closes together with the BOS unified pages.

### Checkpoint 2026-10-08 — the public free-tools API (`ajax_epc_free_tools.php`) like PHP

Not complete.

- Ratchet 760 to 759. Inventory ajax 88 to 89 of 91. Root 491 PHP files (the new generator `scripts/gen_free_tools_tables.php`), mentioned 66 to 67. Unnamed PHP functions 7,925 to 7,908 of 9,870. The weighted headline (about 20.4%) is unchanged: the free tools are a marketing utility, not a storefront or CP workflow.
- `FreeToolsAjaxEndpoint` serves `content/general_pages/ajax_epc_free_tools.php` (GET, POST, HEAD; JSON or form body, like PHP). Actions: compute, register, login, reset request and confirm, account, save, saves, delete request and confirm, with PHP's messages, statuses, `no-store` and `application/json; charset=utf-8`.
- `FreeToolsCompute` ports `epc_free_tools_compute` and its 14 calculators (VAT, corporate tax, gratuity, payroll, labour law, customs, document expiry, insurance, e-invoice, HR compliance, CSV and the rest). The country tables in `FreeToolsTables.g.cs` are generated from the PHP source by `scripts/gen_free_tools_tables.php`.
- `FreeToolsPhp` reproduces the PHP semantics the outputs depend on: `json_encode` floats and `\uXXXX` escapes, `(string)` float precision 14, `round` half away from zero (keeping `-0`), `mktime`, `date` and the `strtotime` forms the tools accept.
- `FreeToolsAccounts` ports the accounts, saves and deletion flows on the registry database (config.php's DB), with bcrypt `$2y$` cost 10 and the mail through `AuthEmailOtpEndpoints.SendHtmlMailAsync` (`epc_auth_smtp_send_html`).
- Evidence: `FreeToolsComputeParityTests` compares every tool against goldens from real PHP 8.3 (`Fixtures/FreeTools/harness.php`). `FreeToolsAjaxParityTests` replays 55 account steps on a throwaway database and compares the bodies, a mid-run dump and the final `epc_free_tool_%` tables with PHP's run (`accounts_driver.py`); the deletion mail and bcrypt are checked too.
- Intended deviations: a database error is a 500 JSON reply ("Service unavailable, please try again.") where PHP dies with a fatal error; `substr` cuts never leave half a UTF-8 character; `strtotime` forms outside the supported set show "no date"; the schema is ensured once per process, not per request; the time zone is the server's local zone.
- Still a gap: `epc_ecomae_free_tools.php`, whose page renderers are served as marketing snapshots, not ported.

### Checkpoint 2026-10-08 — CP, ERP and BOC top menus

Not complete. The ratchet stays 760 and the weighted headline (about 20.4%) is unchanged: this is menu correctness, not new PHP behaviour.

- Before: menu entries sharing one destination per view were 6 to 10 in the CP menu, 12 in the ERP menu and 17 in the BOC menu. After: 0 in every view, pinned by `TopMenuIntegrityTests` (Phase D rule).
- `TopMenuLinks` drops later entries whose destination is already in the menu (first entry wins, empty groups go). It runs on the CP menu (`CpNavMenuService`), the ERP menu (`ErpIndustryNav.FilterTopnav`) and the BOC menu (`PhpSuperCpBocNav.Nav`). PHP's `epc_cp_nav_tree` dedupes only per group by PHP URL, so the same page could appear in two groups.
- Entries that opened a neighbour's module now open their own: the payments guide (`/cp/guides-app?g=payments`), payroll (`/erp/payroll-app`, was HR), people and leave (`/erp/hr-overview-app`), and `shop/price-management` (`/cp/price-management-app`, was price lists; `shop/pricing` stays on price lists).
- ERP pages that were ported but missing from the ERP menu now have entries: favourites, chart of accounts, report scheduler, cash entries, customer groups, stock movements, inventory report, warehouses, RFID, document attachments, on-premises, multi-currency GL, order pipeline, inventory forecast.
- The desktop `LegacyDesktopChromeCatalog.ControlPanelTopnav` is not rendered by any page (the CP chrome uses `CpNavMenuService`); it still lists repeats and is not counted here.
- Open port items, where the dedupe now hides an entry because ASP.NET has no page of its own yet:
  - CP: POS terminal, order items, catalogue line lists and tree lists, customer reviews (`otzyvy-pokupatelej`), related products (`soputstvuyushhie-tovary`), special searches, homepage products (`tovary-na-glavnoj`), registration fields (`polya-registracii`) and variants, customer approvals, downloadable price lists (`prajs-listy-dlya-skachivaniya`), pickup methods (opens the logistics hub).
  - ERP: P&L and balance sheet (both open the report centre, which has no tabs), budget planning (opens budgeting), procurement (opens purchase requisitions), document expiry (opens document control), HR operations (leave), fixed assets (asset management), contracts (organisation admin), WMS (MHEI), blockchain proofs (tax), financial depth (cost accounting), accounting automation (setup), and the multi-entity tab, removed because `/erp/multi-entity-app` is remapped to a consolidations tab that does not exist.
  - BOC: industry consolidation and licence trends (industry packs), dealer portal (tenants), data policy, imports, channels, API v2, SKU photos, webhooks, MFA, AI classify, document vault, insights hub.
- Pages outside every menu (22, grouped by reason in `NotInTopMenu`):
  - Not menu destinations: the CP and ERP login pages and the ERP module host.
  - Linked from other pages: the CP and ERP dashboard summaries, navigation coverage, price lists, price edit, PO approvals, CRM activities, admin sessions, print documents, bulk upload, finance close.
  - Open placement items (no row in the production CP menu snapshots): the integrations, Life OS and operations guides, Life OS clients, VIN fields, workshop, ERP multi-entity, ERP user control.

### Checkpoint 2026-10-08 — eight PHP CP redirect pages

Not complete.

- Ratchet 768 to 760. The weighted headline (about 20.4%) and unnamed PHP functions (7,925) are unchanged.
- The PHP CP pages that only redirect now land where their PHP target lands, pinned per page by `CpPhpRedirectPageMapTests`: the customer management, Document Control, POS and tenant hub folder pages, the legacy `users/customer_mgmt` route, the old Russian print module (`shop/modul-pechati-dokumentov`, which had fallen to the CP login), `shop/finance/payment_systems`, and `shop/crm/crm`.
- `shop/crm/crm` opens the CRM board (the ERP CRM tab includes `crm_main.php`, which the board ports). It now keeps PHP's query: `tab` reduced to `[a-z_]` (last value wins), and `from` and `to` unless PHP-empty. Before, the query was dropped and the board always opened its default tab.
- Intended deviation: `users/customer_mgmt` answers 302 (the redirect middleware's status) where PHP sends 301; the landing page is the same.
- Still a gap: `erp/erp_launcher.php`, whose query forwarding into the ERP shell is not verified yet.

### Checkpoint 2026-10-08 — forty PHP CSS and JS wrapper files

Not complete.

- Ratchet 808 to 768. Inventory content 347 to 383 of 952, cp-page 182 to 186 of 523. The weighted headline (about 20.4%) is unchanged: the wrappers are presentation plumbing, not storefront or CP workflows. Unnamed PHP functions stay 7,925.
- `PhpAssetWrappers` serves the 40 PHP files that only send one repository CSS or JS file (`content/general_pages/epc_*_css.php` and `*_js.php`, `content/shop/pos/epc_pos_*`, `epc_erp_dashboard_premium_css.php`, the four portal `*_config.php` loaders). Each answers like its PHP file: the first source that exists, its content type and `Cache-Control`, and, where the PHP sends one, the ETag `"md5(mtime|size|version)"` with 304 when `If-None-Match` equals it. Without a source, the answer is PHP's 404 "… missing" text, or an empty 200 for the bare loaders.
- The portal `*_config.php` loaders run outside `cp/index.php`, so they are exempt from the CP admin gate and from the PHP-path redirect, which mapped `epc_mobile_apps_config.php` to `/cp/mobile-apps-app` by prefix.
- Live side by side (`php -S` over the repository against Kestrel): status, content type, cache header, ETag, body hash and the 304 answer are identical for 40 of 40.
- Still a gap: `epc_static_serve.php`, the static-file fallback that `index.php` calls (different shape).

### Checkpoint 2026-10-08 — four small ajax and API gaps

Not complete.

- Ratchet 812 to 808. Inventory categories: ajax 86 to 88 of 91, API 27 to 28 of 29, retired 3 to 4. The tracker's ajax ratios (storefront and API 109 of 112, Control Panel shop 74 of 75, `cp/content` 108 of 110) and the weighted headline (about 20.4%) are unchanged. Unnamed PHP functions stay 7,925.
- `api/umapi_image.php` was already served, but from `HomeCatalogWidgets.cs`, a file name the inventory skips. It now lives in `UmapiImageProxy` and matches PHP's parsing: `kind` is lower-cased but not trimmed (`" supplier"` is a 400) and `id` is PHP 8's `(int)` (`12abc` is 12, `1e3` is 1000, `7.9` is 7), pinned by a theory checked against real PHP.
- `content/general_pages/ajax_epc_social_media.php` and `ajax_epc_marketing_broadcast.php` are nginx-safe proxies that only run the CP handler (both handlers define `_ASTEXE_` themselves). They are routed to the ported handlers, so a guest gets PHP's refusals: `{"ok":false,"message":"Admin required"}` with 200, and `{"ok":false,"message":"Forbidden"}` with 403.
- Retired `cp/content/control/portal/epc-apai-ajax-probe.php`, a deploy probe of the PHP include chain that also discloses server paths (reason in `PHP_RETIRED.tsv`).
- Still open in these categories: `ajax_epc_free_tools.php` (since ported, see the free-tools checkpoint), `epc_bos_ajax_login.php`, `epc_erp_modules_ajax.php`. Also `epc_prices_ajax_init.php`, the bootstrap of the price upload endpoints, which closes with them, and `api/UCatalog/ucatalog_index.php`, which only the unported modex template includes.

### Checkpoint 2026-10-08 — the ported users pages keep the visitor's language prefix

Not complete.

- Ratios, ratchet (812) and unnamed functions (7,925) unchanged.
- `/ar`, `/ru` and `/me` users pages already reached the ported pages through `StorefrontLangAliasMiddleware`, which rewrites them onto `/en/…`. The login, register, profile and edit pages then read their `lang_href` from the rewritten path, so Arabic, Russian and Montenegrin visitors got `/en` links, form actions and redirects. They now use `StorefrontPhpHomeLinks.LangHref`, which reads the prefix the alias middleware records (the POST handlers run before the rewrite and see the real path). This closes the language prefix item of the two checkpoints that follow.

### Checkpoint 2026-10-08 — the storefront profile edit page and its save like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 74 of 75, broader `cp/content` 108 of 110. Weighted headline stays about 20.4%. Ratchet stays 812: `content/users/editform.php` was already named by the old Blazor profile app, so the inventory counted it before it was ported. Unnamed PHP functions: 7,925 (main was already at 7,925 after an unrelated merge).
- `StorefrontEditForm` renders `/users/editform` and `/en/users/editform` byte for byte. Its literal HTML comes from PHP's own tokenizer over the source (`short_open_tag=1`, as on the server). It is checked against a `php -S` golden of the real page on a throwaway database (15 cases, with the profile and user rows left behind).
  - The page: the registration fields as a JavaScript list, the variant selector (hidden for one variant, a panel for none or several, in `order`), the password block, the form checks with `min_password_len`, and the current profile values and variant.
- `StorefrontEditFormPostMiddleware` handles the `edit_user` POST in one transaction and writes PHP's redirect script back to the edit page (success text 4715, or the error of the failed step).
  - The steps: CSRF key of the customer session, the new password when one is given, the registration fields of the posted variant (stored htmlentities-escaped, a key with two rows gets a third like PHP), removal of emptied fields, then the variant itself.
  - The variant match and change use PHP 8's loose comparison (`" 2"` and `"2.0"` match 2, a missing variant stores NULL).
- Intended deviations:
  - PHP deletes every profile row whose key is not in the POST. That lets a pending or rejected trade customer delete `epc_trade_approval_status` (which then reads as approved) and wipes the VAT, tax-exempt and dealing currency keys on every save. ASP.NET only deletes registration fields.
  - A new password is stored as bcrypt (cost 12, which both logins verify) instead of `md5(password + secret_succession)`.
  - The CSRF key is compared strictly (PHP's `!=` treats two numeric-looking keys as equal), and a database error redirects with the step's translated error instead of the SQL message.
- Still open: other language prefixes (`/ar/users/editform`) are not routed to the page yet (the POST handler accepts them). The old Blazor profile app stays at `/storefront/profile-app`.

### Checkpoint 2026-10-08 — the storefront profile page and its currency change request like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 74 of 75, broader `cp/content` 108 of 110. Weighted headline stays about 20.4%. Ratchet 813 to 812: `content/users/profileform.php` closes. Unnamed PHP functions: 7,930 to 7,924.
- `StorefrontProfileForm` renders `/users/profile` and `/en/users/profile` byte for byte. It is checked against a `php -S` golden of the real page on a throwaway database (15 cases, with the profile, price setting, currency and table rows left behind).
  - The rows: trade account (type, approval label, dealing currency, change pending), VAT treatment, the wholesale tax-exempt certificate with its upload form, and the registration variant when there is more than one.
  - The contact widgets (phone when SMS is available, e-mail otherwise or when both are) with the phone mask script.
  - The profile fields of `DP_User::getUserProfile()` that are not `users` columns, in PHP array order (a later profile row overwrites in place), and the group names.
  - The edit link and the dealing currency panel (pending request, or the change form).
  - Like PHP, a GET stores a missing `customer_vat_type`, adds the missing UAE VAT price settings and creates the e-invoice tables.
- `StorefrontProfilePostMiddleware` handles the page's currency change POST: the CSRF check (stop_csrf's JSON on refusal), `epc_trade_request_currency_change()`, and the `reg_notify_admin` notice with the requested currency and the note. The page then renders with the success alert.
- Intended deviation: the POST's CSRF check always uses the customer session. PHP's `stop_csrf.php` switches to the admin session when the referer is a CP page.
- Still open: `/ar/users/profile` and other language prefixes are not routed to the page yet (the POST handler accepts them).

### Checkpoint 2026-10-08 — the storefront login page and its password sign-in like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 74 of 75, broader `cp/content` 108 of 110. Weighted headline stays about 20.4%. Ratchet 817 to 813 (inventory 816 to 813): `content/users/loginform.php`, `modules/login/login_form_general.php` and `modules/login/pass/app.php` close. Unnamed PHP functions: 7,937 to 7,930.
- `StorefrontLoginForm` renders `/users/login`, `/{lang}/users/login` and `/storefront/login` byte for byte against goldens from the real PHP files, CRLF included:
  - the auth card, the password tab (phone selector when SMS is on), the e-mail code tab with its modal, the provider buttons, and the template 59, 61 and 62 styles;
  - the whole `login_form_general.php`: the page variant, the header variant with its own heading and no register button, the postfix that grows with each form on a page, and the signed-in account links with the logout form.
- `StorefrontLoginFormLoader` loads the translations, the session CSRF key, the front template, the tenant login context, the enabled providers and the SMS flag.
- `StorefrontLoginPostMiddleware` with `StorefrontLoginPost` handles the login page POST like the storefront half of `plugins/authentication/plugin.php`. This is checked against a `php -S` golden on a throwaway database (20 cases).
  - The steps: the guest session prelude and stale purge, then the CSRF checks. Password sign-in uses bcrypt, or strict md5 with an upgrade to bcrypt. Code sign-in decrements the attempts and checks expiry.
  - On success it writes the session row and the cookies (remember me or a session cookie), moves the guest cart, syncs the UAE VAT customer and sends the customer sign-in notice. It then redirects to the safe target, else back to the page.
  - Bots and an unconfigured database render the page as a GET.
- Intended deviations (security):
  - `wrong_authentication_tag` is JSON-quoted into the alert script. PHP pastes it raw, which is a reflected XSS.
  - Password sign-in is rate-limited through `epc_login_attempts` (10 in 15 minutes). PHP has no brute-force limit there.
  - The new session's `last_activiti_time` is set at insert, because the ASP.NET session check never refreshes it.
- Still open: the logout POST of the same plugin, and the modal logo (the site profile is not ported).

### Checkpoint 2026-10-08 — the e-mail code sign-in verify with provisioning and the session like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 74 of 75, broader `cp/content` 108 of 110. Weighted headline stays about 20.4%. Ratchet 818 to 817 (inventory 817 to 816): `epc_auth_email_otp.php` closes, all seven functions ported. Unnamed PHP functions: 7,958 to 7,937.
- `AuthOtpVerifyLogin` serves `/epc-auth-verify-code.php` and `/content/general_pages/epc_auth_api_verify_code.php`. These are the sign-in that the e-mail code modal, the storefront widget and the CP modern login call.
  - The context is resolved like the send endpoint. It now also carries the return host and path, the tenant database and the ERP-only demo flag.
  - The checks run in PHP order: the e-mail and 6-digit code, then HTTPS. The matching unexpired code row for the tenant is deleted.
  - Storefront: an existing unlocked customer is confirmed. A new e-mail becomes a customer with the default registration variant, the registered group and the retail trade registration. The session row is written, `time_last_visit` is set, and the guest session's carts move to the customer. The redirect is the safe `return_url` on the tenant host, else `/en/`.
  - CP: an existing unlocked user needs backend access. The session row is type 1 with contact type `email`, the admin cookies are set and the platform ERP cookie is cleared. The redirect is `/cp/control` on the tenant or Super CP host, or the demo CP orders page (the ERP shell for ERP-only demos).
  - When the tenant host is not the request host, the answer is the signed `/epc-auth-handoff.php` link instead of cookies.
- Intended deviations (security):
  - CP sign-in does not add every backend group to an existing user. PHP's self-heal does, which turns any shop customer, or a manager, into a full CP admin.
  - A new e-mail gets a CP account only on a demo sandbox. PHP creates a full admin on live tenants too.
  - The OAuth callback had the same open CP sign-up. It now also creates CP accounts only on demo tenants.
  - The handoff link is signed with the ASP.NET session secret, which the ASP.NET handoff endpoint checks.
- Fixes found on the way:
  - Registry `TINYINT` flags were read as `True`/`False`, so every tenant looked like a demo to the e-mail code context.
  - New OAuth storefront customers now get the retail trade registration, as in PHP.
- `AuthOtpVerifyLoginTests` compares 19 cases with **goldens from the real PHP endpoint** (`Fixtures/AuthOtpVerifyLogin/harness.py`, `php -S`, throwaway schema). Each case checks the status, headers, body, the unpacked handoff, cookies, and the user, profile, group, session and cart rows. The two refused cases and three group-bind differences are pinned as deviations. Super CP is tested in ASP.NET only, because PHP's config resolution reads the platform database for that host. A corrupted golden makes exactly one case fail.
- Full suite: 5653 of 5653. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.
- Next: `loginform.php` and `profileform.php`.

### Checkpoint 2026-10-08 — PIM custom attributes and the Syncron inventory policy (owner-accepted enhancements)

Not complete. These two modules come from the unmerged Devin PR #8 and never shipped in PHP, so they do not move the parity ratios.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 74 of 75, broader `cp/content` 108 of 110. Weighted headline stays about 20.4%. Ratchet stays 818 (inventory prints 817), unnamed PHP functions stay 7,958.
- `ErpPimCustomFields` and `ErpPimWriteService` cover the field types, codes, options, per-module visibility, value validation and save, and the item display table. A "PIM attributes" tab on `/erp/product-info-app` manages them through `/erp/product-info/pim`. The inventory create-item forms render the fields and validate them before the item is created.
- `ErpSyncronPolicy` and `ErpSyncronWriteService` cover global, category (item type) and item policies, moving-average or exponential demand, safety stock, reorder point, the stockout/reorder/overstock recommendation, the 30-day forecast run and the monthly service levels. The page is `/erp/syncron-app`, posting to `/erp/syncron/action`. The ERP top nav shows it under inventory management, and `?tab=syncron` maps to it.
- Writes are admin-only with the ERP capability, need `confirmWrites`, and write an ERP audit row.
- Tests: PHP goldens from the PR #8 code (10 PIM cases, 12 Syncron cases; a corrupted golden fails exactly one case), plus write-service tests on throwaway schemas. Full suite: 5631 of 5631. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.
- Next: the login code verify with provisioning and the session, then `loginform.php` and `profileform.php`.

### Checkpoint 2026-10-08 — the registration page and the social sign-in buttons like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 74 of 75, broader `cp/content` 108 of 110. Weighted headline stays about 20.4%. Ratchet stays 818 (inventory prints 817): `regform.php` was already counted through the old page's comment. Unnamed PHP functions go from 7,963 to 7,958, all of them the page's own scripts.
- `StorefrontOAuthButtons` ports the social sign-in buttons:
  - one "Continue with X" button per configured provider, in the PHP provider order, with the `only` filter;
  - the divider, heading and Terms / Privacy checkbox that unlocks the buttons;
  - the last Google e-mail hint from its cookie, and the start links built like `http_build_query`;
  - the stylesheet printed once per page.
- `StorefrontRegForm` ports `content/users/regform.php`:
  - the already-signed-in text;
  - the CMS additional-fields script, and the variant selector (hidden with the raw caption for one variant, translated otherwise);
  - the contact selector following the available channels, and the password fields;
  - the social panel and the Retail / Wholesale tabs from the render half;
  - the captcha, the user agreement, the e-mail code modal (verify-only, then submit) and the submit checks.

  The form posts to `{lang}/users/register`, the ported register engine.
- `StorefrontRegFormLoader` reads the page's inputs:
  - the `reg_fields` and `reg_variants` rows with their translations;
  - the contact channels, and the `csrf_guard_key` of the session row;
  - the storefront login context, the site trade name (site settings contact, hub name, then the host) and the configured providers.
- `/storefront/register-app`, `/{en}/users/registration` and `/users/regform` now render this page inside the storefront chrome, with jQuery. The page creates the guest session when missing, so the form carries a real `csrf_guard_key`. The old simplified form, which posted to `StorefrontRegisterWriteService` with `confirmWrites`, is gone from the page. The service and its `/storefront/register` route are now retired too: the route, the service, its DI registration, `PhpCustomerWrites.RegisterHref` and the catalog row are removed, and a test pins that they stay gone. Registration writes go only through `/{lang}/users/register`.
- Tests, with goldens from the real PHP (php-cli, temp docroots, no database):
  - `StorefrontOAuthButtonsTests`: 11 cases.
  - `StorefrontRegFormTests`: 12 cases, using a fake `$db_link`, stubbed `DP_User` and translations, and the real render half, auth layout, user agreement and modal.
  - `StorefrontRegFormLoaderTests`: on a throwaway schema, the loaded page equals the render of the rows, translations, session and Google provider it should have read.

  A corrupted golden makes exactly one case fail in each golden set. Two source-grep tests pinned to the old page now check the loader call and the rendered form.
- Full suite: 5588 of 5588. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.
- Next: retire `StorefrontRegisterWriteService`, then the login code verify with provisioning and the session, then `loginform.php` and `profileform.php`.

### Checkpoint 2026-10-08 — the registration form render half like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 74 of 75, broader `cp/content` 108 of 110. Weighted headline stays about 20.4%. Ratchet stays 818 (inventory prints 817). No file closes: `epc_registration_enhanced.php` was already counted through its save half, so the render functions only lower the unnamed PHP functions from 7,979 to 7,963 (that file goes from 11 of 33 to 27 of 33).
- `EpcRegistrationEnhancedRender` ports the render half of the enhanced registration form:
  - The social sign-up panel: the "Register and join" title (trade name, else the login label, else "our store"), the provider buttons with the "Or" divider only when they render, the e-mail code input and the send and verify script with the return URL from the language prefix. It is empty when the modern auth core is unavailable.
  - The country select with the selected option, and the Retail / Wholesale tabs with the wholesale company, TRN and KYC / AML document fields.
  - The tab script: dial-code and address metadata maps, the phone hint, the retail country UI, the wholesale TRN rules, the pane enable and disable logic, and client validation.
  - The legacy UAE panel stays a no-op.
- The markup segments were generated from the PHP file, so the output is PHP's byte for byte.
- `EpcRegistrationEnhancedRenderTests` compares 11 cases with **goldens from the real PHP functions** (`Fixtures/RegistrationEnhancedRender/harness.py`, php-cli). Each case gets a temp docroot with the real registration and country files and stubs for the auth core, the login context and the provider buttons. The cases cover auth unavailable; defaults; quotes, `<>`, `&` and `</script>` in the trade name, tenant key and URLs; a blank trade name; no trade-name function; Unicode; empty and present buttons; four country-select variants; the tabs; and the UAE panel. A corrupted golden makes exactly one case fail.
- Full suite: 5557 of 5557. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.
- Next for `regform.php`: the page itself on the `/users/register` engine, then login verify with provisioning and the session.

### Checkpoint 2026-10-08 — the six-box e-mail code modal like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 74 of 75, broader `cp/content` 108 of 110. Weighted headline stays about 20.4%. Ratchet 819 to 818: only `epc_otp_modal.php` closes. The inventory prints 817 because the modal's default verify URL names `content/general_pages/epc_auth_api_verify_code.php`. That endpoint (login verify with user provisioning and the session) is **not ported** and still counts as a gap. Unnamed PHP functions: 7,990 to 7,979.
- `StorefrontOtpModal` ports `epc_otp_modal_render()`, used by the registration form, the storefront e-mail sign-in widget and the CP modern login:
  - The stylesheet is printed once per page (one instance per page).
  - The overlay, card, six digit boxes, Continue button, message line and resend button with its 60-second timer.
  - The script with the context, tenant key, send, verify and return URLs as PHP `json_encode` strings, and the success code: the caller's, else `epcOtpOnSuccess(data)` for verify-only, else the redirect.
  - Defaults: the modal id is lower-cased and every other byte becomes `_`; the verify URL follows `verify_only`; the label defaults to `epartscart`; the logo shows only with a URL. Text is escaped like `htmlspecialchars`.
- The markup segments were generated from the PHP file, so the output is PHP's byte for byte.
- `StorefrontOtpModalTests` compares 7 cases with **goldens from the real PHP function** (`Fixtures/OtpModal/harness.py`, php-cli). The cases cover the defaults; the registration, CP login and storefront widget configs; Unicode, emoji, quotes and `</script>` in values; a multibyte modal id; two modals on one page; and empty strings. A corrupted golden makes exactly one case fail.
- Full suite: 5546 of 5546. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.
- Next for `regform.php`: the render half of `epc_registration_enhanced.php`, then the page itself.

### Checkpoint 2026-10-08 — the e-mail sign-in code send and registration verify-only like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 74 of 75, broader `cp/content` 108 of 110. Weighted headline stays about 20.4%. Inventory gap 821 to 819 (the two `content/general_pages` endpoint shims). The OTP and SMTP libraries stay counted as gaps: login verify with session and provisioning, the operator code lookup and the Super CP SMTP settings writer are not ported yet. Unnamed PHP functions: 8,003 to 7,990.
- `AuthEmailOtpEndpoints` serves `/epc-auth-send-code.php`, `/epc-auth-otp-verify-only.php` and their `content/general_pages` aliases, which the `regform.php` e-mail verification modal calls:
  - Input is a JSON body, otherwise the form fields. The context is resolved like PHP: the posted tenant key in `epc_portal_tenants` (it needs working tenant DB credentials), the Super CP host (CP mode), the row for the host or bare host, then the local site.
  - Send: e-mail check, the HTTPS gate (HTTPS, `X-Forwarded-Proto`, port 443, or a localhost host), 5 codes per e-mail and 20 per IP per hour, the purge of rows expired a day ago, a hashed 6-digit code for 10 minutes, then the mail.
  - SMTP settings merge `config.php`, `config.local.php`, the non-empty `config.epc-smtp.php` values and the site's `integrations.smtp` when `use_tenant_smtp` is on (never on the Super CP host). The PHP precheck messages, the error classification, and the subject and HTML of the mail are kept. Mail goes out through MailKit.
  - When mail fails for a `demo_` tenant (unless `disable_demo_otp_fallback`), the code is kept for Super CP operators and the answer is the PHP "code ready" one.
  - Verify-only: digits-only code, the tenant key from `tenant_key` or `site_key`, the matching unexpired row is deleted, `verified_email` is returned.
- Intended deviations:
  - `context_json` keeps the kind, tenant key, label, mode and return URL. PHP also wrote the whole tenant row, including the tenant DB password.
  - The verify-only session flag is not set: nothing reads it.
  - There is no PHP `mail()` fallback.
- `AuthEmailOtpTests` compares 20 cases with **goldens from the real PHP endpoints** (`Fixtures/AuthEmailOtp/harness.py`, `php -S`, throwaway schema). Each case checks the status, content type, cache header, body and the rows left in `epc_auth_otp_requests`. Further tests cover a successful send whose mailed code verify-only accepts once, an SMTP authentication failure, the demo operator-code fallback, and the config merge rules. A corrupted golden makes exactly one case fail.
- Full suite: 5539 of 5539. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.
- Next for `regform.php`: the OTP modal, the render half of `epc_registration_enhanced.php`, then the page itself on the `/users/register` engine.

### Checkpoint 2026-10-08 — the user agreement module and the auth card layout like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory gap 828 to 826. Unnamed PHP functions: 8,014 to 8,011.
- `StorefrontAuthPartials` ports two includes that `regform.php` and the login page need:
  - `content/users/users_agreement_module.php`: the checkbox (strings 4751 to 4753), the `{lang}/polzovatelskoe-soglashenie` link, the script that resets the `users_agreement` cookie to `no` and sets it on change, and `check_user_agreement()` with the 4754 alert.
  - `content/users/epc_storefront_auth_layout.php`: the stylesheet link (printed once per page), the `epc-auth-page` card (`--wide` for registration) and its close.
- The output is PHP's byte for byte, including the file's CRLF line endings and translations echoed unescaped. `/content/users/epc_storefront_auth.css` is now served from the reference tree.
- `StorefrontAuthPartialsTests` compares 7 cases with **goldens from the real PHP includes** (`Fixtures/AuthPartials/harness.py`). Translations are stubbed with HTML and quotes. The cases cover three language prefixes, the default and wide cards, the stylesheet printed once, and an unknown variant. A corrupted golden makes exactly one case fail.
- Full suite: 5505 of 5505. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.
- Next for `regform.php`: the render half of `epc_registration_enhanced.php` (social block, account tabs, tab scripts, UAE panel) and the email OTP modal with its send and verify-only endpoints, which need the modern auth core.

### Checkpoint 2026-10-08 — the registration captcha (`lib/captcha`) like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory gap stays 828, because the inventory classes `lib/captcha` as third-party. Unnamed PHP functions: 8,016 to 8,014.
- `StorefrontCaptcha` ports `captcha.php` and `check_captcha.php`, the first `regform.php` dependency:
  - The image: a code of 4 to 7 shuffled characters from the PHP set, on one of the 30 PHP backgrounds. It has lines under and over the text and Agency letters at a random 20 to 30 points, with the same angle and position ranges, drawn with SkiaSharp.
  - The `captcha` cookie is `md5(code)` for two minutes on `/`. The PHP cache headers are sent.
  - The check: an empty body when `captcha_check` is missing, otherwise `true` or `false` from a PHP 8 loose comparison. A POST value overrides the query.
  - The refresh button image is served at its PHP URL.
- Intended deviations:
  - Only PNG backgrounds are picked. PHP can pick the directory's `index.html` and then fails with a 500.
  - Kestrel writes header names in its own order. The values and their order within each name match PHP.
- `StorefrontCaptchaTests` compares 10 check cases with **goldens from the real PHP** (`Fixtures/Captcha/harness.py`, `php -S`, `request_order=GP`). The cases cover GET, POST, POST over GET, a missing cookie, an empty value, the `0` and `0e1` magic hashes, and an uppercase cookie. The tests also check the image headers and cookie, that a 150×70 PNG decodes, the refresh image bytes, and that generated codes pass their own check. A corrupted golden makes exactly one case fail.
- Full suite: 5498 of 5498. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-08 — the registration form post (`/users/register`) like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory gap stays 828: `register.php` was already counted as mentioned through older route notes. Unnamed PHP functions: 8,017 to 8,016.
- `StorefrontPhpAjax.UsersRegisterAsync` ports `content/users/register.php`. It runs in one transaction:
  - The signed-in refusal, captcha, user agreement, contact type, regexp, uniqueness, empty IP and 24-hour IP checks.
  - The enhanced and UAE field checks.
  - The `users` row with the e-mail activation code or a random SMS code.
  - The `simple_register` SMS-code account: attempts, a wrong code, expiry and a generated password.
  - The registration fields (`show_for` loose match, file widgets and wholesale-only fields skipped, values through `htmlentities`) and the registered-customer group.
  - The trade account, the buyer profile and KYC documents, then the `reg_email_confirm` or `reg_phone_confirm` send (strings 4697 and 4698, or `registration_continue_if_confirm_email_fails`).
  - After the commit: `reg_notify_admin` to the admin inbox, the CRM manager and the backend groups, with the profile table, profile button and login-event block.
  - The page text: e-mail sent or not, retail approved or wholesale pending, or the SMS code form.
- `PhpHtmlEntities` is PHP 8.1 `htmlentities` (HTML 4.01 table plus `&#039;`).
- Endpoint: GET `/users/register` and `/{lang}/users/register` redirect to the form as PHP does. POST runs the port; a refusal is a 302 to `{lang}/?error_message=`. The page text goes to `/users/registered` in a ten-minute protected token and renders inside the storefront chrome. The Blazor registration page keeps `/users/registration` and `/users/regform`, and its own write route, until `regform.php` (captcha and agreement) is ported.
- Intended deviations:
  - The e-invoice schema is ensured before the transaction, and its DDL runs once per connection. In MySQL that DDL commits implicitly, so in PHP 8 a refused confirmation send keeps the account and the final `commit()` throws.
  - A 302 replaces the `<script>location=` page.
  - The `simple_register` hand-over form escapes the posted values.
  - A signed-in user gets the 4740 redirect; in PHP, `rollBack()` dies there because no transaction has started.
  - A `reg_fields` row with an invalid `show_for` is skipped on the `simple_register` path; PHP throws a TypeError there.
- `StorefrontUsersRegisterTests` compares 21 cases with **goldens from the real PHP** (`Fixtures/UsersRegister/harness.py`): the real `register.php`, `dp_user.php`, translator, trade, e-invoice, enhanced-registration and admin-notification code behind `php -S`, with only `send_notify` stubbed to record calls. The checks cover the redirect or page; the `users`, `users_profiles`, `users_groups_bind`, `sessions` and buyer rows; every notify call (name, variables, persons); and the KYC files. One Fact covers the signed-in refusal. Another drives the HTTP endpoint end to end: GET redirects, a refusal, an e-mail registration with its result token, and a wholesale post with multipart KYC uploads.
- Full suite: 5485 of 5485. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-08 — registration fields, KYC documents and e-invoice buyer profiles like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory gap ratchet: 829 to 828 (`epc_einvoice_schema.php`); unnamed PHP functions 8,017.
- `EpcRegistrationEnhanced` ports the validation and save half of the enhanced registration form:
  - The retail and wholesale required fields, the country check, the UAE 15-digit TRN, and the TRN status abroad.
  - The PEP declaration and the two required wholesale documents.
  - The UAE company fields.
  - The `users_profiles` keys (contact, address, trade, KYC text and the legacy `name`, `surname`, `company_name`).
  - The KYC documents: 8 MB at most; PDF, JPG, JPEG, PNG or WEBP; stored under `content/files/kyc/{user}/` and marked `pending_review`.
  - The UAE buyer profile and the `epc_uae_company` flag.
  - The form's render half stays a gap until the registration page is ported.
- `EpcEinvoiceBuyer` is the twin of `epc_einvoice_schema.php` (the five tables and default settings) and the buyer half of `epc_einvoice.php`: the stored or user-built buyer profile, the TIN from the TRN, the Peppol endpoint, the country normalisation and the save.
- `EpcUaeCustomerVat` ports the `customer_vat_type` sync: tax exempt, GCC, export, local B2B or local B2C, with the buyer profile country winning over `epc_reg_country`. The display-price, label and order-total functions of that library stay a gap.
- `StorefrontRegistrationEnhancedTests` compares 19 cases with **goldens from the real PHP** (`Fixtures/RegistrationEnhanced/harness.py`). It runs the PHP behind `php -S` so the documents are genuine multipart uploads. The checks cover every validation and save result, the VAT types, the buyer profiles, and the rows left in `users_profiles`, `epc_einvoice_buyer_profiles` and `epc_einvoice_settings`. They also cover the KYC files written (name and size).
- Full suite: 5462 of 5462. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-08 — trade accounts and storefront currencies like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory gap ratchet: 833 to 829 (`epc_customer_trade.php`, `epc_currency.php`, `epc_countries.php`, and the retired `epc_reg_fields_compliance.php`); unnamed PHP functions 8,032.
- `EpcCustomerTrade` is the twin of `epc_customer_trade.php`:
  - A retail registration is approved at once, with AED and the retail price profile group when they exist. Any other type becomes retail. A wholesale registration waits as pending.
  - Approve, reject (with the note) and the currency change request write the same `users_profiles` keys. Assigning a price profile leaves every other price profile group.
  - No status counts as approved; the status is compared exactly, as in PHP.
  - Storage errors are ignored, as in PHP.
- `EpcCurrency` is the twin of `epc_currency.php`: the ten supported currencies kept available, the records (a repeated ISO code replaced in place, the shop currency added when missing), the visitor's choice (the approved dealing currency, then the `epc_currency` cookie, then the `epc_country` map, then the shop currency) and the amount format.
- The checkout ajax, the checkout write service and the price access state now use the shared library. Checkout returns PHP's block message, including the rejection note. Before this, the write service had its own message and compared case-insensitively, and the price state lowercased the status.
- `StorefrontCustomerTradeTests` compares 13 cases with **goldens from the real PHP libraries** (`Fixtures/CustomerTrade/harness.py`): every operation result plus the rows left in `users_profiles`, `users_groups_bind` and `shop_currencies` match. A further test runs the checkout gate for pending, rejected-with-note and unknown statuses.
- `EpcCountries` is the twin of `epc_countries.php`: the 244 names in PHP order, the Gulf-first registration order, the dial codes, the address rules and the code normalisation (two letters, else a name matched like `strcasecmp()`). `StorefrontCountriesTests` compares them with goldens from the real PHP (`Fixtures/Countries/harness.py`).
- `epc_reg_fields_compliance.php` is retired in `inventory/PHP_RETIRED.tsv`: no PHP file includes it or calls its functions, and nothing reads the `reg_fields` columns it would add.
- Full suite: 5443 of 5443. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-08 — page access checks like PHP check_user_access.php

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory gap ratchet: 834 to 833 (`check_user_access.php`); unnamed PHP functions 8,063.
- `StorefrontPhpAjax.CheckUserAccessAsync` is the shared twin of the include. The group must reach every listed page:
  - `content_access` rules plus the groups nested under them, recursively, only below a group whose `count` is not 0.
  - A frontend page without rules is open; a backend page without rules is closed.
  - Backend pages use the admin's bound groups. Frontend pages use the customer profile groups: the guest group, the bound groups, or else the first `for_registrated` group.
  - No pages gives string 2387, a refusal 2388, both translated and `null` when untranslated.
- The denial language follows `multilang_init()` for CP ajax: the active `backend_ui_lang`, else the active `lang_cp` cookie, else the active default language (multilang off: the default language).
- The 10 CP lang editor ajax handlers and the storefront storage toggle now use it. Before this they ignored nested groups and answered the literal "2388".
- Verified against goldens from the real include with the real `dp_user.php` and `lang/dp_lang.php` (php-cli, throwaway schema, only the config stubbed): 23 cases compare the decision and the `status`, `error` and `message` values. The CP ajax suites now seed `groups` and the `lang_languages` flags, as a PHP database has them.
- 5427 of 5427 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-08 — the registration contact check like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory gap ratchet: 835 to 834 (`check_reg_contact.php`); unnamed PHP functions 8,063.
- `content/users/check_reg_contact.php` answers on the same path (GET and POST) with PHP's content type:
  - The `stop_csrf.php` check comes first (CSRF 1, 3, 3.1 and 4). The query key wins over the posted one, and a CP referer checks the admin session.
  - A type other than `email` or `phone` gives an empty body. The developer domains `@intask.pro`, `@docpart.ru` and `@docpart.net` are refused for e-mail with string 5641 (`null` when untranslated).
  - The `reg_fields` regexp must match the whole contact (4699 plus the caption). A missing row means no check.
  - Another user with the contact, compared after `htmlentities()`, gives 4700, the caption and 4701. Otherwise `{"status":true,"message":"Ok"}`.
- Verified against goldens from the real PHP script with the real `stop_csrf.php` and `dp_user.php` (php-cli with pdo_mysql on a throwaway schema; only the config and the translator are stubbed): 22 cases compare the exact body on Kestrel.
- 5404 of 5404 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — admin login rate limit and the bcrypt upgrade like PHP

Not complete.

- Ratios unchanged. Weighted headline stays about 20.4%. Inventory gap ratchet: 837 to 835 (`epc_login_rate_limit.php` and `epc_password_upgrade.php`).
- The shared ASP.NET login (`DbLegacyAdminLoginService`) now does what the PHP authentication plugins do:
  - CP, ERP, BOS and IP logins check `epc_login_attempts` first. At 10 failures from the client IP (CF-Connecting-IP, then the first X-Forwarded-For hop, then the remote address) or for the contact (`strtolower(trim())`) within 15 minutes, the login is refused with "Too many failed attempts. Please wait N minutes before trying again." The login page shows N through `?error=rate_limited&wait=N`.
  - Every refused admin attempt is recorded, including a blocked one, as in PHP. A success deletes that IP and contact's attempts and records the success. The table is created with PHP's DDL when missing, and storage errors never block a login.
  - Storefront and LifeOS logins are not rate limited, as in PHP.
  - On every surface, a legacy md5 hash that matched is replaced with bcrypt cost 12 (`$2y$12$`) before the backend group check, as in PHP.
- `epc_login_rate_limit_cleanup()` is ported (`LegacyLoginSecurity.CleanupAsync`). PHP calls it nowhere, so it is not scheduled.
- Intended deviation: a failed hash upgrade is logged and the login goes on (PHP would stop on the exception).
- Local testing note: a login on the local app now writes `epc_login_attempts` and can upgrade that account's hash. Do not log in to a local app whose tenant DB is `ecomae` or `docpart`.
- Verified on a throwaway database: wrong password and missing backend access are recorded (and the md5 hash is upgraded on the second), a success upgrades the hash, clears and records, a bcrypt account logs in again unchanged, a storefront login is not recorded but is upgraded, 10 contact failures 61 seconds old block with a 14-minute wait and record one more failure without a session, the block lifts after 15 minutes, and cleanup removes rows older than 24 hours.
- 5382 of 5382 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — password reset pages like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112 (these are pages, not ajax scripts). Weighted headline stays about 20.4%. Inventory gap ratchet: 839 to 837 (`forgot_password.php` and `new_password.php`).
- The forgot password page (`/users/forgot_password`, `/en/users/forgot_password`) shows the contact type select the way `available_communications()` decides: both types when every SMTP setting is filled and exactly one SMS operator is active, otherwise phone only or e-mail only.
- The form posts to `/storefront/forgot-password-app/send`, which follows PHP:
  - A logged-in customer goes home. No contact shows the form again. A missing or unknown type gives 4719. No account with that confirmed contact gives 4720. An active lock gives 4694.
  - E-mail: a 64-hex code and the `new_password` link in the page language. Phone: a five-digit code.
  - `forgot_password_time`, `forgot_password_code` and `{type}_code_send_lock_expired` (now + 300) are stored, then `forgot_password_by_email` or `forgot_password_by_phone` goes through the `send_notify.php` logic. A failed answer gives 4722, an unsent contact 4723. Success gives 4724 (e-mail) or the SMS code form (phone).
  - The result redirects back to the page with `?r=<id>`, and only the ids above are shown.
- `new_password` goes home unless the type, contact and stored code are valid. A wrong code (PHP loose compare) is discarded with 4729, and a code older than 30 minutes with 4730. Otherwise the password becomes `md5(new.secret_succession)` for a 10-hex new password, which is shown once under 4732.
- Intended deviations: the e-mail link URL-escapes the contact. There is no captcha (PHP only checks it in the browser). Ids without a translation show English text.
- Verified on Kestrel over a throwaway database, with the real dispatcher, a recording mailer and a recording SMS gateway: every redirect above; the stored code, time and lock; the mailed link; the lock refusal; the late and wrong code resets; a phone reset with a leading-zero code that sets the md5 password; and both `available_communications()` cases. The pages were also rendered on the local app.
- 5378 of 5378 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — the send_notify.php endpoint like PHP

Not complete.

- Ratios unchanged. Weighted headline stays about 20.4%. Inventory: 839 gap files (the page itself was already mentioned).
- `content/notifications/send_notify.php` answers on the same path (GET and POST). The secret is compared loosely ("Forbidden"). Missing input gives 4072, a bad persons list 4073 and an unknown notification 4074.
- It differs from the inline dispatcher the way PHP does: bare translations; e-mail and SMS gated on `status_ref` when `orders_statuses_notifications_settings` is set (a missing flag counts as 0); SMS marked as tried even without an operator (4077); `debug_results` upserted for e-mail and SMS; `persons` echoed in PHP key order with typed user columns.
- Verified against goldens from the real PHP script (php-cli with pdo_mysql on a throwaway schema, mailer, translator, template and curl stubbed): 11 cases compare the exact body, the `debug_results` rows and the mails.
- Intended deviations: `debug_results.status` is written as 0 or 1 (PHP writes '', which fails on an int column in strict mode); the e-mail debug text is the mailer message; file attachments are ignored.

### Checkpoint 2026-10-07 — contact confirmation and the login code like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112 (these two files were already mapped; their success paths were missing). Weighted headline stays about 20.4%. Inventory: 839 gap files.
- `ajax_contacts_works.php` now finishes like PHP.
  - Set, change and confirm write the `users` columns `{type}`, `_confirmed`, `_new`, `_code`, `_code_expired` (now + 1800), `_code_attempts` and `_code_send_lock_expired` (now + 300) in a transaction.
  - The e-mail code is `md5(md5(contact.contact_new.rand).md5(secret_succession))`; the phone code is a six-digit number.
  - It then sends `{type}_confirm_other` with `site_name`, `email_confirm_href` and `phone_confirm_code`, to the new contact on change. The send uses a second connection, as PHP's `send_notify.php` runs in another process.
  - A missing notification or dispatch error rolls back with 4697. An unsent contact rolls back with 4698. Success answers `{status, message, type, action, contact}`.
- `ajax_sendCode.php` sends `verification_code`. Then `sessions` gets `2fa_code`, `2fa_attempts` 3 and the PHP `json_encode` data (`timeSendFaCode`, `expireFaCode` + 300, `type`, `method`, `contact_string`, `contact`), and the answer is `{"status":200}`. A failed update is 5650.
- Messages are translated in the page language taken from the referer, and the confirmation link carries that language. The regexp checks follow `preg_match`'s rule: the whole value must match, and no capture group may take part.
- Intended deviation: an id with no translation is shown as the id, not as an empty message.
- Verified on Kestrel over a throwaway database, with the real dispatcher and a recording mailer:
  - Contact confirmation: a notification without `send_for_not_confirmed` gives "Code not sent" and nothing is stored. A partial regexp match gives "Bad contact". Set stores a 32-hex code and its expiry and lock, and mails the link. A locked change gives 4694. Change keeps the old e-mail, stores `email_new` and mails the new address with the `ar` link and text. A refused SMTP send rolls the confirm back.
  - Login code: the send stores a six-digit `2fa_code` that matches the mailed code.
  - 5365 of 5365 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — the legacy SMS operators and the handler URLs like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory: 839 gap files, down from 841 (ratchet `--max-gap 839`): `smsimple.class.php` is ported and `smsaero/send_sms_old.php` is retired; unnamed PHP functions 8,066.
- `CpSmsLegacyOperators` ports the ten legacy handlers. Each sends the same provider request as its `send_sms.php` (URL, query encoding, form or JSON body, basic auth, user agent) and reads the reply the same way, including PHP's loose `==` and `>=`:
  - sms_ru strips whitespace from the reply before decoding it, and an empty `sms` list prints nothing;
  - iqsms reports success for any two-part reply, with the code text as the message;
  - semysms treats a missing `code` as 0; terasms treats an undecodable reply as success; smsvizitka succeeds whenever the raw response contains "200 OK";
  - smsgorod prefixes the shop host when `domain` is on, and sends the sender only on the `char` channel.
- The order notification dispatcher and the CP communications test now reach these operators (before, they answered "not implemented on ASP.NET"). Messages use `translate_str_by_id` in English, as PHP does for a server-to-server POST.
- `/content/sms/handlers/<handler>/send_sms.php` answers in ASP.NET for all 14 operators. That is the URL the PHP dispatcher, password recovery and contact confirmation post to.
  - Legacy handlers: the database first ("Error"), then the loose `check` against `secret_succession` ("Forbidden"), then the `sms_api` row by handler (active or not). They answer `json_encode` as HTML.
  - GCC handlers: "Database error", a strict non-empty `check`, "<operator> operator not configured", and the POSTed `parameters_values` override, with the unescaped JSON of `epc_sms_exit_json`.
- Intended deviations:
  - smsimple cannot run on PHP 8: it includes a missing `lib/xmlrpc.inc`, and the xmlrpc extension was removed, so it always answers "XmlRpc libraries not available". ASP.NET sends its XML-RPC calls (`pajm.user.auth`, then `pajm.sms.send` with `signature_id`).
  - rocketsms, smsgorod and terasms no longer write request logs with phone numbers and credentials into the web root.
  - Certificates are verified (PHP turns verification off). Each call has a time limit (5 seconds for smstraffic and 20 for smsgorod and terasms, as in PHP; 25 where PHP waits forever). sms_ru, iqsms and rocketsms do not follow redirects, as in PHP.
- Verified: the real PHP handlers, run in a harness with stubbed curl, PDO and translator (`Fixtures/SmsHandlers/harness.sh`), produce 36 goldens across the nine operators that run on PHP 8. ASP.NET sends byte-equal requests (method, URL, body, headers) and prints byte-equal answers for all 36. The smsimple XML-RPC exchange, its faults and a failed login have their own tests. On Kestrel over a throwaway database: an sms_ru send through the URL, Forbidden for a wrong or missing check (legacy and GCC), "du operator not configured", the Unifonic AppSid check and 404 for an unknown handler. 5357 of 5357 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — sales-invoice sale demand like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory: 841 gap files (ratchet `--max-gap 841`, unchanged: the PHP function lives in the large inventory library, which stays a gap).
- `ErpSaleDemand` (PHP `epc_erp_inventory_record_sale_demand`) runs after every tax-invoice save commits: the manual invoice, the sales-order invoice, the order invoice and the order tax-invoice print. Credit notes (381) and other categories are skipped.
  - The order's product lines map to active ERP items by `product_id`; when none match, the invoice lines match by item name.
  - One `sale_out` movement per item in the warehouse holding the most stock (else the first active warehouse), at the average cost. Stock is reduced by at most what is on hand.
  - The `SALEINV-<id>` reference makes it run once per invoice. A database error never fails the invoice, like PHP's catch.
- Fixed with it: ASP.NET created `epc_uae_vat_advance` without `einvoice_document_id` (PHP `epc_uae_tax_compliance_ensure_schema` has it). On a tenant without that table, every order tax-invoice save rolled back with "Unknown column". The table is now created with the column, and the column is added when missing.
- Verified: the PHP harness (`php` running the real function over the same fixture: two invoices from orders, a repeat, a name-matched invoice, an invoice with nothing to match, document 0) gives byte-equal movements, stock and return values on a throwaway database. A manual invoice save and an order tax-invoice print on a running test host record the movement and take the stock. 5300 of 5300 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — Document Control print and the ERP access check like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory: 841 gap files, down from 844 (ratchet `--max-gap 841`), with `epc_document_control_cp_install.php` retired; unnamed PHP functions 8,070.
- `/content/shop/document_control/service/print.php` runs in ASP.NET at its PHP URL (`StorefrontPhpAjax.PrintDocumentControlAsync`). The CP order pane, the Document Control page and the ERP document tab already link there.
- Access is one engine, `ErpUserAccess` (PHP `epc_erp_access.php`). It applies the same checks as PHP, in the same order:
  - a CP admin session (`DP_User::isAdmin`), or a customer session in the backend group or one of its direct children (`isBackendGroup`);
  - otherwise `epc_erp_user_can_access`: the admin with content access to `shop/finance/erp`, then for the signed-in user the whole backend tree, an “Administrator” group, the CP ERP page access list with its subgroups (no list lets every signed-in user in, like PHP), an `EPC_ERP_DEPT_*` group or an active staff profile department (read only when a department group exists, like PHP), or the `EPC_ERP_TEAM` group;
  - a database error in that chain denies, like PHP's catch.
- `ErpDocumentControlRender` now renders from an e-invoice (`invoice_id`, PHP `epc_dc_einvoice_context`) as well as from an order or the preview. The invoice wins over `order_id`, and `preview` ignores both. The company row falls back to the e-invoice seller settings, and the stored buyer profile's NULL fields fall back to the document, then the defaults.
- Intended deviations: when no group has `for_backend = 1`, PHP's `isBackendGroup` builds `IN ()` and dies with a SQL error; ASP.NET treats the user as not backend. A database error while rendering shows the driver's message (PHP shows PDO's).
- Verified: 22 PHP goldens (php -S serving the real `print.php`, `dp_user.php` and `epc_erp_access.php` over the same fixture) are byte-equal on a throwaway database:
  - the guest, a mismatched cookie, a plain customer and an inactive staff profile get 403;
  - the backend child, a backend grandchild, an Administrator group, a CP ERP subgroup, a staff profile, a department group and the ERP team get the page;
  - as admin: the default template, order 40, preview with an invoice, invoice 7 as tax invoice, packing slip and receipt, invoice 8 as delivery note (no lines, no order, supply date 0), invoice 10 (no buyer), and the 400s for an inactive invoice, a missing order and an unknown template.
  - Four more tests cover the no-access-list rule, the backend tree fallback to groups 1 and 3, department codes without department groups, and denial on a database error.
  - 5295 of 5295 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — customer returns pages like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory: 844 gap files, down from 860 (ratchet `--max-gap 844`); unnamed PHP functions 8,081.
- `/shop/returns`, `/shop/returns/return?return_id=` and `/shop/returns/add_return?items=` render `StorefrontReturnsPages` (the list with the unread-messages filter, the return card with its lines, photos and manager chat, and the request form). `/content/shop/returns/assets/add_return.js.php` is served at its PHP URL with the language prefix taken from the referer. The page reads the PHP path the slug middleware rewrote, so all three URLs keep their own page.
- Opening a return marks the manager messages read, like `return.php`.
- The customer order page now has `my_order.php`'s return selection when `return_available` is 1: a checkbox per line with select all, and the button 4527. With no line checked it alerts 4537; it posts the checked ids and the session CSRF key to `ajax_check_items_returns.php`, alerts 5683 when a line is already in a return or not in a returnable status, and otherwise opens `/shop/returns/add_return?items=[…]` with the language prefix. ASP.NET posts with `fetch` (PHP uses jQuery), and the select-all box has its own id because the orders list on the same page already uses `check_uncheck_all`.
- Also in this tranche: `ajax_create_operation.php` checks the user first and uses the `DP_Config` partial minimum, the wholesaler office pay system and the payment-account handler; the payment method picker and the obtaining-mode includes (`show_details`, `manager_interface`, `show_office_info`) render like PHP on the pay, balance and checkout pages and the CP order card.
- Intended deviations:
  - `return_id` is HTML-escaped (PHP echoes it raw).
  - The form no longer carries the `DP_Config` `tech_key` (PHP prints it into customer HTML). It carries an HMAC of the session CSRF key, which `ajax_load_returns_data.php` accepts in place of the tech key for that session only.
  - `items` accepts integers only (PHP puts it into SQL unchecked); an empty list renders no lines instead of a PHP fatal.
  - `domain_path` is the request origin; the script is served as `application/javascript`; the guest redirect script renders inside the page chrome (PHP exits).
- Verified: 15 PHP goldens (`php -d short_open_tag=1` over the same fixture: guest, list, unread filter, another user, pending, decided and foreign return, list after read, request disabled, request with and without a retention period, foreign items, no items, the script) are byte-equal on a throwaway database. On a running app over a throwaway database, the three URLs render the list (returns 1–3), return 1 (total 0.3, chat with the session CSRF key), the request form (total 2289.6, derived key, script include). User 8's return 4 is “Return not found”, user 8's item 97 is not offered, and a guest is sent to the site root. The derived key from another session is Forbidden. In a browser on order 40: no line checked alerts “Check the positions to return.”, the not-returnable line 92 alerts 5683, select all toggles the five lines, and lines 90 and 91 open `/en/shop/returns/add_return?items=[90,91]` with both lines and total 2289.6. 5269 of 5269 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — order payments and UAE gateway stubs like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory: 860 gap files, down from 868 (ratchet `--max-gap 860`); unnamed PHP functions 8,105.
- `/content/shop/protocol/pay_for_order.php` runs in ASP.NET (`ShopPayForOrderService`):
  - The manager (initiator 1, office access), the customer (initiator 2, from the balance) and the payment system (initiator 3, tech key). CSRF as `stop_csrf.php` for 1 and 2.
  - The partial payment minimum, the client overdraft, the direct-pay income for managers, the expense line, the UAE advance VAT row (`epc_uae_vat_record_advance_on_payment`), the paid flag, the order logs, the paid type, the pay notifications and the robot `for_paid` status.
- The individual payment accounts (`epc_payment_accounts.php`) and `get_pay_system_parameters.php`:
  - The account linked to the operation, else resolved for the order (vendor of the largest storage, office, the office's legacy pay system as a virtual account, platform).
  - The settlements after a demo payment: one per vendor split, else one for the account, with the platform fee.
- The gateway stubs under `content/shop/finance/payment_systems/` are served at their PHP URLs:
  - `go_to_pay.php` for the 24 UAE handlers: CSRF, the pending operation of the user, then the crypto form, the "Configure gateway" alert or the demo form.
  - `pay_page_entry.php`, `pay_page.php` and `epc_demo/pay_page.php`: the demo checkout and `pay_execute`.
  - `crypto_pay_page.php`: the coin picker, the demo invoice and the live NOWPayments `/payment` call.
  - `{handler}/notification.php`: activate, `pay_notify.php`, `pay_for_order.php`, settlements, then the redirect with translation 4355.
  - `nowpayments/notification.php`: the IPN with the HMAC-SHA512 check, 400 "IPN rejected", "already processed".
  - The direct `go_to_pay.php`, `notification.php` and `epc_demo/*` answer "No handler".
- Intended deviations:
  - The demo notification always needs the demo token; PHP skips the check when the gateway is in demo mode.
  - The customer pay notification links the order's user; PHP reads an undefined `$user_id`.
  - The KKT receipt include is skipped: the file is missing in the reference and PHP fails on it.
  - The advance VAT schema is ensured before the transaction (DDL commits implicitly).
  - A notification with sum 0 notifies and pays the operation amount; PHP passes 0.
  - Not ported yet: the legacy Russian gateways (alfabank, assist, avangard, cdekpay, chronopay, maib_md, payanyway, paybox, paykeeper, paymaster, promsvbank, rbkmoney, robokassa, sbr, tinkoff, walletone, webpay_by, yandex, yookassa, docpart_emulator).
- Verified:
  - `Fixtures/PaymentGateways/harness.sh` serves the real PHP files with `php -S` on `fixture.sql`. ASP.NET gives the same bytes, status, content type and `Location` for 31 cases: go_to_pay order, top-up, live, nowpayments, code 2, CSRF 1, 3.1 and 4, no handler; pay pages including handler `0` and upper-case input; crypto pick, btc, small btc (`1.5384615384615E-5`), usdt, unknown coin, wrong coin, confirm, live with a dummy key; notifications; IPN rejected, bad signature, already processed.
  - Throwaway-DB tests cover the pay_for_order gates, partial and overdraft rules, advance VAT, the manager direct payment, the vendor and platform settlements and the idempotent IPN. The live NOWPayments payload is checked against PHP `json_encode`.
  - Over HTTP on a throwaway tenant: go_to_pay, checkout, pay_execute, the notification (302, operation activated, replay redirects without a second write), the IPN (400, applied, already processed) and the crypto invoice.
  - 5246 of 5246 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — order print like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory: 868 gap files, down from 872 (ratchet `--max-gap 868`); unnamed PHP functions 8,116.
- `/content/shop/print_docs/service/print.php` now runs in ASP.NET for the CP order card and the customer order page:
  - Admin or customer CSRF as PHP decides it (`csrf_admin`, an admin cookie without a customer session, or a `/cp/` or `/control/` referer); an empty admin key is filled from the admin session.
  - `sales_receipt` (and every unknown document) prints the receipt. `invoice_for_payment`, `uae_tax_invoice` and `fta_tax_invoice` print the UAE tax invoice: the saved e-invoice of the order, else the PINT-AE invoice built from the order (saved when it validates with the filled-in buyer), else the document control `fta_tax_invoice` template.
  - Before a tax invoice the document control company is synced from the e-invoice seller settings with PHP's force mode.
  - The customer print page now sends its CSRF key; before, PHP would have refused it.
- Intended deviations:
  - The tax invoice leaves out the blockchain BOS proof block.
  - `content/shop/document_control/service/print.php` stays on PHP until `epc_erp_user_can_access` is ported.
  - `get_html_invoice_for_payment.php` (the legacy Russian invoice) is not ported; PHP `print.php` no longer routes to it.
- Verified: the real PHP generators were run on `Fixtures/OrderPrint/fixture.sql` (`harness.php`) and their HTML is the golden. ASP.NET gives the same bytes, with only the UUID normalized, for receipt 40, tax invoice 40, the reprint of the saved tax invoice 40, the document control fallback for order 41 (no lines), and receipt 41 after the seller sync. Both save `EINV-2026-00001` once. CSRF 1, 3, 3.1 and 4, the 400 and the 404 for another customer's order are covered. 5196 of 5196 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — return requests like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory: 872 gap files (ratchet `--max-gap 872`); unnamed PHP functions 8,125.
- `/content/shop/returns/ajax/ajax_load_returns_data.php` now does the whole PHP flow instead of only the header and line rows:
  - `tech_key` check, duplicate line refusal (string 4571), the first return status (4572).
  - When fewer units are returned than ordered, the line is split: the original keeps the rest and a copy holds the returned units, which the return points to.
  - Photos (`images[line][n]`): png, jpeg, jpg or bmp only, 5 MB each, 15 MB in total; stored under `content/files/returns_images` with random names and recorded in `shop_orders_returns_items_images`. A bad photo cancels the whole request (4576–4578).
  - After the commit: `return_new_manager` to the office users and `return_new_customer` to the customer, the returned lines move to the `for_return` status with a robot history row, and the split lines get their catalogue reservation and history rows (5636, 5686) like `helper.php`.
- Intended deviations:
  - PHP's status-change history row has broken SQL and is never written; ASP.NET writes it.
  - Notifications go out after the commit, and photo files already written are deleted when the request is rolled back.
  - The line copy reads the columns of the current database only (PHP's `INFORMATION_SCHEMA` query is not limited to one database).
- Verified on a throwaway database: a 2-of-5 return splits line 90 into 3 and 2, the 1-of-1 line is returned whole, the comment is stored HTML-escaped, three photos are stored and match the uploads, managers 3 and 4 and then customer 7 are notified, and the three history rows match. A repeat request is refused, a gif or a 5 MB+1 photo is refused with nothing left behind, and a wrong key answers Forbidden. 5195 of 5195 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — order and line status protocol like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory: 872 gap files (ratchet `--max-gap 872`); unnamed PHP functions 8,128.
- New `ShopOrderProtocolService` is the one engine for order and line status changes (PHP `set_order_status.php` and `set_order_item_status.php`):
  - Order status: a manager cancel first cancels the open lines; a manager finish needs a fully paid order (else PHP string 5296, code 101) and first issues the open lines. Then the status write, `order_status_to_manager` (backend office managers) and `order_status_to_customer` (user, or the guest e-mail and phone), the WhatsApp tracking line in the order history (`epc_wa_notify_order_status_change`), and the history row (manager as himself, robot as `is_robot` = 1).
  - Line status: the optional return split (`retun=1`, one line, count checked, line and catalogue details copied, two history rows), the refund to balance when cancelling lines of a paid order (`5_refund_from_order_to_balance`, plus `6_refund_from_balance` for guests, then the paid flag is recomputed), the catalogue stock moves between exist, reserved and issued, the status write, the paid recheck, `order_item_status_*` notifications, the history, and for a manager the automatic order status (finished, cancelled, back to the paid status).
  - Status notifications now pass the status row, so SMS and WhatsApp obey `to_manager_sms` / `to_customer_sms` when `orders_statuses_notifications_settings` = 1, like PHP.
- The PHP URLs `/content/shop/protocol/set_order_status.php` (initiator 1 manager with session and CSRF, 4 robot with `tech_key`) and `set_order_item_status.php` (1 manager, 2 robot) are served by ASP.NET with the PHP JSON answers. The CP order card can call them as before.
- Pay on place (`my_order.php`) now moves the order to the `for_paid` status through the robot protocol, so it gets the notifications and history like PHP.
- Online payment (`pay_for_order.php` initiator 3) now does what PHP does after the commit: `paid_type` = 3 with its history line, `order_pay_to_manager` and `order_pay_to_customer` (amounts, paid state, order link in the template colour), and the robot status change once fully paid. The payment history line uses the PHP strings (1316, 4366, 4529, 3584/3515) instead of fixed English.
- Intended deviations:
  - An empty `tech_key` never opens the robot routes. PHP accepted any key when `tech_key` was empty.
  - The return split copies the line through its column list instead of PHP's temporary table.
  - An unreadable office manager list sends to nobody (PHP 8 would stop with a TypeError).
- Fixed on the way: translated messages in the return split were read while a transaction was open, which the driver rejects; they are now read first.
- Verified on a throwaway database: unpaid finish is refused; cancelling a paid line refunds 20.00 to the balance, returns 2 units to stock and keeps the order paid; issuing the last line finishes the order by robot with the WhatsApp tracking link to `971501111111`; a manager cancel of a guest order cancels its line, returns stock and notifies the guest directly; the return split refuses two lines and a bad count, then splits 5 into 3 and 2. The routes answer `Wrong key` 503, `Forbidden` 501, the CSRF errors and an empty body for unknown initiators. Pay on place moves order 300 to Paid; a full online payment sets paid, `paid_type` 3 and Paid, and a partial guest payment notifies the guest only. 5194 of 5194 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — notifications send SMS and WhatsApp like PHP

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75 (74 of 75 on PR #2031), broader `cp/content` 95 of 110 (108 of 110 on PR #2031). Weighted headline stays about 20.4%. Inventory: 875 gap files (ratchet `--max-gap 875`); unnamed PHP functions 8,133.
- `StorefrontNotifyDispatcher` now runs the phone branches of PHP `docpart_dispatch_notification()` for every person, after the e-mail:
  - The phone comes from `users.phone` (needs `phone_confirmed` or `send_for_not_confirmed`) or from the `direct_contact` phone (needs `send_for_not_confirmed`).
  - SMS goes when `sms_on` = 1 and an `sms_api` operator is active. The number is stripped like PHP (spaces, `+7`, brackets, `-`, `_`, `+`) and sent through the existing typed gateway (`CpSmsGateway`) with the operator's `parameters_values`. The `sms_body` template is translated and its declared vars are substituted.
  - WhatsApp goes to the same phone through the Meta Cloud API (`StorefrontWhatsappNotifier`, a twin of `epc_whatsapp_notify.php`). It needs `epc_whatsapp_api_enabled` = 1 plus the token and phone number id in `config.php`, `email_on` or `sms_on`, and the notification name in `epc_whatsapp_notify_names` (PHP default list).
  - The WhatsApp body is the SMS text, else the plain e-mail, else the order text, else `<site> — order #N`; capped at 3,500 bytes; with the Arabic line when `epc_whatsapp_bilingual_notify` is on (default). The site name is the site contact `trade_name`, then `hub_name`, then `ecomae`.
  - Every WhatsApp attempt is written to `epc_whatsapp_notify_log` (created if missing), like PHP.
- Checkout guests now pass their phone (`phone_not_auth`) with the e-mail, as PHP does, so a guest order can get the SMS and WhatsApp too.
- Intended deviations:
  - Only the four GCC/MENA operators (`epc_unifonic`, `epc_etisalat`, `epc_du`, `epc_pakistan`) send natively. A tenant on one of the legacy Russian-market handlers gets a "not implemented" SMS outcome instead of PHP's HTTP call to the handler script.
  - The site name does not consult PHP's built-in site catalogue or the tenant registry row; it reads the site settings row only.
  - Order-status SMS suppression (`status_ref`) is not ported, because no ASP.NET status notification passes it yet.
- Verified on a throwaway database with a fake SMS gateway and a fake Graph API: a confirmed user gets e-mail, SMS (`971501111111`, "Order 77 received") and WhatsApp (bilingual body, Bearer token, `/v21.0/PHONE-ID/messages`, log row status 1). An unconfirmed phone and a direct contact without `send_for_not_confirmed` get nothing. With `sms_on` = 0, WhatsApp still goes with the plain e-mail text, and a Graph error is reported and logged as status 0. A notification outside the WhatsApp list sends SMS only, and an inactive operator stops SMS. 5179 of 5179 tests pass. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

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
- Still on PHP: process-flow sync, and SMS/WhatsApp fan-out for these notifications.
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

### Checkpoint 2026-10-07 — Cursor owns ERP; journal precision and transfer posting fixed

Not complete.

- Ratios unchanged: storefront and API ajax 109 of 112, Control Panel shop, users, and requests 72 of 75, broader `cp/content` 95 of 110. Weighted headline stays about 20.4%.
- Release `20261007084903` (`debe2654e`) is live with #1970, #2027, #2028, and #2029. `/health` and `/ready` return 200.
- GL journals are balanced on their stored two-decimal values. Cash transfers post through `1090 Cash in transit` and no longer raise revenue and expense. Findings and order of work are in `docs/migration/ERP_OWNERSHIP_AUDIT_2026-10-07.md`.
- Full suite 5139 of 5139. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — every cp/content ajax endpoint has an ASP.NET route

Not complete. The ajax layer is done, but pages, storefront surfaces, and ERP are not. ERP work waits until this plan is finished.

- Broader `cp/content` ajax is 108 of 110. All 108 `ajax_*.php` endpoints are mapped. The two left are not endpoints, so they stay unmapped on purpose. `shop/prices_upload/epc_prices_ajax_init.php` is a bootstrap that the price-upload scripts include. `control/portal/epc-apai-ajax-probe.php` is a deploy probe that echoes server paths and error file and line numbers.
- Control Panel shop, users, and requests ajax is 74 of 75; the one left is that same include. Storefront and API ajax stays 109 of 112: two includes, plus the ERP modules ajax, which waits for the ERP phase. Weighted headline stays about 20.4%, because that figure weights pages and workflows as well as ajax.
- `ajax_visual_page_editor.php` `load_layout` ports `epc_vpe_layout_load`:
  - default brand, saved brand merge, and the brand/homepage fallback for other levels;
  - the six frontend levels;
  - allowed site keys (the request tenant, or on Super CP the platform, ePartsCart, and `epc_portal_tenants`);
  - preview URLs. Demo and ERP-only tenants preview on `www.ecomae.com`, as in PHP.

  `save_layout` checks `blocks_json` as PHP does, then stays Classic. Info blocks, cross-tenant brand settings, and the cache clear have no ASP.NET writer. A missing table reads as no saved layout, and nothing is created.
- The three `version_control/ajax` scripts and the five `packs_control` scripts keep the PHP `stop_csrf` and session gates and reply in their PHP shapes. They then refuse: no tmp clear, no call to the update server, no unzip or file copy. ASP.NET code ships through the deploy pipeline. Clear-tmp, delete-pack, and insert-extensions answer `Session duplication` to a signed-in admin, because PHP calls `fetchColumn()` twice on a one-row `COUNT(*)`. The eight paths stay on ajax; before this, the link map redirected them to `/cp/packs-app` and `/cp/ops-guides-app`.
- `price_review/ajax_price_review.php` runs dry. It keeps the CSRF, admin, and argument gates, then evaluates the batch with the PHP MIN/MAX/AVG, manufacturer-synonym, and plus/minus percent rules. It reports `items` and `would_review` and does not write `price` or `reviewed`. `ajax_create_csv.php` counts rows for type 1, 2, or 3 and writes no file. Both answer `status: false`, so the Control Panel page stops on the dry-run message.
- Full suite 5146 of 5146. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — auto-price discovery sources

Not complete. ERP work waits until this plan is finished.

- Broader `cp/content` ajax is 97 of 110. Of the 108 `ajax_*.php` files in `cp/content`, 11 are unmapped: the visual page editor, three version-control scripts, five packs scripts, and the two price-review scripts. Storefront and API ajax stays 109 of 112. Control Panel shop, users, and requests stays 72 of 75. Weighted headline stays about 20.4%.
- `ajax_auto_price.php` add, toggle, skip, and delete of discovery sources go through the existing `ICpAutoPriceWriteService`. They return the PHP JSON shapes, and add and toggle include the `epc_disc_source_format_row` source object. A guest gets 403 `Admin login required`, and an unknown action gets 400 `Unknown action: X`, as in PHP. The site key follows the PHP rule: the posted key, then the five known hosts, then the hostname, then `platform`.
- These requests return an explicit stays-Classic message: sources with a login or a product-line scope (the service does not store those fields), Super CP writes to another tenant's database, and the 24 search, crawl, fetch, job, approve, and warehouse actions. Schema-ensure stays Classic, so a missing table returns the service message and nothing is created.
- Full suite 5143 of 5143. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

### Checkpoint 2026-10-07 — translation editor string list

Not complete. ERP work waits until this plan is finished.

- Broader `cp/content` ajax is 96 of 110. `cp/content` holds 108 `ajax_*.php` files today, and 12 are unmapped: auto price, the visual page editor, three version-control scripts, five packs scripts, and the two price-review scripts. Storefront and API ajax stays 109 of 112. Control Panel shop, users, and requests stays 72 of 75. Weighted headline stays about 20.4%.
- `ajax_get_text_strings.php` returns the editor's paged list with the PHP filters, `has_<lang>` columns, and the current-language translation. A guest gets the CSRF refusal before the admin check, as in PHP. An unknown table or column, a sort field outside `str_key`/`description`/`current_lang_translation`, or a limit outside 1 to 5000 returns an empty body, as PHP `exit` does. The PHP `SQL` echo is not returned. The work language follows the PHP backend rule: `backend_ui_lang`, then the `lang_cp` cookie, then the active default.
- Full suite 5137 of 5137. Throwaway schemas left: 0. `docpart.users` and `ecomae.users` stay at 2.

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
- `ajax_contacts_works.php` returns `4689`, `4690`, `4691`, `4693`, and `4697`. The notify HTTP call was not made, so user 7’s email stays empty. (Superseded: the 2026-10-07 contact confirmation checkpoint sends and stores.)
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
- [~] Top menu integrity for the CP, ERP and Super CP BOC menus. The rules, enforced by `TopMenuIntegrityTests` for every CP snapshot (tenant and super view), the ERP menu per industry (generic, auto parts, jewellery, fit-out; tenant and super) and the BOC menu:
  - Every entry opens a different page: no two entries in one menu share a destination (path plus query, without `company=` and `#fragment`).
  - Every destination is a routed ASP.NET page.
  - Every CP and ERP page is in a menu, or listed in `TopMenuIntegrityTests.NotInTopMenu` with its reason.
  - Every entry opens its own module, not a neighbour's. Open items: see the checkpoint "CP, ERP and BOC top menus".

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
