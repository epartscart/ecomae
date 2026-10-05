# ERP HR expense cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

| Step | Route | Result |
|---|---|---|
| employee | POST /erp/ajax/hr-emp-save (CYC-EMP1) | ok — `epc_hr_employees` 1 |
| claim | POST /erp/ajax/hr-expense-save (2 lines 120+80) | ok — `Expense claim saved — 200.00 AED`; `epc_hr_expenses` 1 amount 200.00 status `draft`, `lines` JSON verbatim |
| approve | POST /erp/ajax/hr-expense-status `approved` | ok — `Expense approved`, status `approved` |
| pay | `paid` | ok — `Expense paid`, status `paid` |
| verbatim status | `bogus` | ok — `Expense bogus`, status written verbatim — matches PHP `epc_hr_expense_set_status` (varchar status, no allow-list) |

Fixture gap: `epc_hr_expenses` absent from the throwaway DB (install-time table, schema-ensure stays PHP); provisioned from verbatim DDL in `content/shop/finance/epc_erp_hr.php:93` — missing it returns HTTP 500, not a clean refusal.

Cleanup: expense 1 + employee 1 deleted — residue 0.
