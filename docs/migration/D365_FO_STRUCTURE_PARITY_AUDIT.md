# D365 F&O structure parity audit

Status: **partial parity — PHP remains authoritative**

The PHP reference is the contract for the tenant's organizational and
financial model. ASP.NET Core must not promote these workflows to ownership
until the read, write, isolation, posting, and visual gates below are proven
against the same tenant data.

## PHP reference contract

The reference implementation is distributed across:

- `content/shop/finance/epc_erp_dimensions.php`
  - polymorphic dimension assignments in `epc_erp_dim_links`
  - legal entity, business unit, class, and tenant-defined financial dimensions
  - active-value filtering, form rendering, save, load, summaries, and bulk reads
- `content/shop/finance/epc_erp_pdf_modules.php`
  - legal-entity and financial-dimension master definitions
  - seeded department, project, and cost-centre dimensions
- `content/shop/finance/epc_erp_costing.php`
  - cost-centre master records
  - weighted shared-cost allocation with exact-cent reconciliation
- the intercompany PHP finance/ERP modules
  - source and destination entities, transaction status, matching, and elimination

## ASP.NET Core coverage observed

The current implementation has these foundations:

- `ErpPmSaveWriteService` exposes legal entities, business units, dimensions,
  and dimension values through the PHP master-save whitelist.
- `ErpDimensionWriteService` resolves active fixed and tenant-defined dimension
  options and persists polymorphic assignments to `epc_erp_dim_links`.
- ERP workflows already attach dimension links to selected RFQ, delivery,
  customer, inventory, and payment records.
- ledger, budget, procurement, HR, and customer records carry selected
  legal-entity or business-unit fields.
- intercompany group, transaction, matching, and elimination flows exist.

This is **not yet full D365 F&O parity**. String `cost_centre` fields on
industry masters are not a substitute for governed financial dimensions, and
the current intercompany persistence does not yet prove the complete
due-to/due-from, currency, exchange-rate, tax, source-document, balanced
posting, and elimination evidence contract.

## Required next gates

Before any ownership promotion for this structure, add fixture-backed evidence
for:

1. explicit authorized company context per request;
2. cross-legal-entity read and write isolation;
3. required/optional dimension validation and defaulting;
4. inactive-value rejection and document/line overrides;
5. traceable dimension combinations on journals, documents, inventory,
   projects, budgets, and tax reports;
6. governed cost-centre master and allocation readback;
7. balanced intercompany due-to/due-from posting, currency policy, tax
   treatment, workflow, source-document linkage, and elimination audit;
8. same-data PHP comparison, report/export parity, and PHP rollback evidence;
9. PHP-vs-ASP.NET screenshots at matching browser sizes.

Until these gates pass, the ASP.NET routes remain guarded shadows and PHP
remains the authoritative implementation.
