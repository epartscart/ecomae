# ERP payroll cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

Same authenticated session, live routes only.

| Step | Route | Result |
|---|---|---|
| staff | SQL seed `epc_erp_staff_profiles` + `epc_erp_hr_records` (PHP schema, basic 6000 + allow 500) | profile 1457 |
| generate | POST /erp/ajax/payroll-generate `periodLabel=2026-01` | ok — run 1, line 6500.00 gross/net, days_worked 30 |
| approve | POST /erp/ajax/payroll-approve `id=1` | ok — run → `approved` |
| pay | POST /erp/ajax/payroll-pay `runId=1,cashAccountId=1` | ok — "Salaries paid — 6,500.00 AED"; cash entry 16 (payment 6500, account 1), run+lines → `paid` |
| double-pay guard | same POST again | verbatim refusal `Already paid` |

Fixture note: `epc_erp_payroll_runs`/`epc_erp_payroll_lines`/`epc_erp_staff_profiles` are PHP install-time tables absent from the local fixture — provisioned with verbatim DDL from `epc_erp_payroll.php`/`epc_erp_staff.php` plus the install-time `epc_erp_schema_add_column_if_missing` columns (monthly_basic/allowances, standard_days, extra_days, daily_rate, gross_pay); `epc_erp_hr_records` needed the salary/bank/days_worked columns. Same fixture-gap class as `users`/`epc_fin_periods`, not a code bug.

Cleanup: cash entry, payroll lines+run, seeded staff deleted — residue 0.
