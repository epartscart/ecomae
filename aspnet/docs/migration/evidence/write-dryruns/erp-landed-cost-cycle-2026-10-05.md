# ERP landed-cost cycle rehearsal — 2026-10-05

Cycle: sheet seed → `/erp/landed-cost/calculate` → `/erp/landed-cost/post`,
live on the throwaway tenant MariaDB.

## Steps

1. Seeded `epc_landed_cost_sheets` (LCS-CYC1, draft, method `value`, AED,
   goods 1000) + 2 lines (SKU-A 10x50=500; SKU-B 5x100=500) + 2 expenses
   (freight 200 + customs 100 = 300).
   Tables were absent beforehand — first call auto-created all three via
   `ErpLazySchema.EnsureLandedCostAsync` (PHP lazy-ensure parity, #2017).
2. `POST /erp/landed-cost/calculate {id:1, confirmWrites:true}` →
   `ok:true "Landed cost calculated for 2 lines."`
   SQL proof: value-basis split 150/150 → `allocated_cost` 15.0000 (SKU-A,
   150/10) and 30.0000 (SKU-B, 150/5); `new_unit_cost` 65.0000 / 130.0000;
   sheet `status=calculated`, `total_expenses=300.00`.
3. `POST /erp/landed-cost/post {id:1, confirmWrites:true}` →
   `ok:true "Landed costs posted to inventory."`
   SQL proof: `status=posted`, `posted_at` set.
4. Repost → verbatim refusal `Only a calculated landed-cost sheet can be
   posted.` with zero writes.

## Residue

All seeded rows deleted; the three tables dropped (restored pre-rehearsal
absence — PHP recreates them lazily).

## Notes

`calculate` and `post` take body key `id` (form accepts `sheet_id`/`sheetId`);
`sheetId` in JSON binds nothing → `A landed-cost sheet id is required.`
