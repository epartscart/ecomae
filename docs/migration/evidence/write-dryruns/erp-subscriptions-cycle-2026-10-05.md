# ERP subscriptions cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

| Step | Route | Result |
|---|---|---|
| save | POST /erp/subscriptions/save | ok — `Subscription saved` → `epc_erp_subscriptions` 1 (SUB-CYC, monthly 3000, active) |
| generate | POST /erp/subscriptions/generate | ok — `Cycle invoice #1 generated` → `epc_erp_sub_invoices` 1 (issued, 3000) + next_bill_date advanced |
| paid | POST /erp/ajax/sub-invoice-paid | ok — `Invoice marked paid` → status `paid` |
| status | POST /erp/subscriptions/status | ok — `Subscription paused` |

Fixes found by this rehearsal (same lazy-schema class as contracts #2015):
- `epc_erp_subscriptions`/`epc_erp_sub_invoices` are lazy-created by PHP `epc_sub_ensure_schema`; ASP.NET save 500'd on a tenant without them. Added `ErpSubscriptionSchema.EnsureAsync` (verbatim DDL) to save/status/generate/invoice-paid.
- `epc_hr_expenses` likewise (PHP `epc_erp_hr.php` ensure); `hr-expense-save` 500'd (#2007 cycle hit it, fixture workaround then). Added `ErpHrExpenseSchema.EnsureAsync` to expense save + status. Both paths re-verified live after DROP TABLE — routes now provision and write.

Cleanup: subscription, invoice and expense rows deleted — residue 0.
