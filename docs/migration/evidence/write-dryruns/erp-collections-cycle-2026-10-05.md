# ERP collections cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

| Step | Route | Result |
|---|---|---|
| case open | POST /erp/collections/cases/save | ok — `Case saved` → `epc_coll_cases` 3 (open, balance 2500) |
| promise | POST /erp/collections/cases/promise | ok — `Promise to pay recorded` → promise_amount 1000, status `promise_to_pay`, activity row 4 |
| activity | POST /erp/collections/activity/log | ok — `Activity logged` → `epc_coll_activity` 5 (call/promised) |
| hold | POST /erp/collections/hold/set | ok — `Credit hold updated` → `epc_coll_hold` 3 (place/overdue) |
| status | POST /erp/collections/cases/status | ok — `Case status updated` → `escalated` |
| dunning | POST /erp/collections/dunning/run | ok — `Dunning run #1 — 1 notice(s)` → `epc_coll_dunning` (level 1, amount 2500) |

Verbatim refusals:
- `status:"promised"` → `Invalid case status` (allowed: new/in_progress/promise_to_pay/escalated/resolved).
- dunning run without customer line → `Enter at least one customer line (customerId|d1_30|d31_60|d61_90|d90_plus)`.

Contract note: dunning `customers` is a pipe-delimited line string, not a JSON array.

Cleanup: case/activity/hold/dunning rows deleted — residue 0.
