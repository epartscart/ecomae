# ERP aftersales (service job + RMA + warranty) cycle — live rehearsal evidence (2026-10-05)

Scope: `/erp/aftersales/job-create` → `job-add-line` → `job-close`, `rma-create` → `rma-resolve`, `warranty-register` on the throwaway tenant MariaDB (`ecomae`), all five `epc_as_*` tables absent at start.

## Lazy-schema parity fix (same PR)

`epc_erp_aftersales.php` `ensure_schema` runs `CREATE TABLE IF NOT EXISTS` for `epc_as_jobs`, `epc_as_job_lines`, `epc_as_rma`, `epc_as_rma_lines`, `epc_as_warranty` at entry. The three writers carried `not provisioned` fail-closed guards — PHP creates and proceeds. Added `ErpLazySchema.EnsureAftersalesAsync` (five verbatim DDL statements) wired after every `OpenAsync` in `ErpAftersalesJobWriteService` (job create/add-line/close), `ErpAftersalesRmaWriteService` (rma create/resolve), `ErpAftersalesWarrantyWriteService`.

## Steps (native-form routes — form-encoded → 302 + row; verified via HTTP + SQL)

1. `job-create` `jobNo=JOB-CYC-1&customerId=1&assetRef=SN-88&complaint=motor failure&underWarranty=1&confirmWrites=1` → 302, row `JOB-CYC-1`/`open`/`under_warranty=1` (tables lazily created on entry).
2. `job-add-line` `jobId=1&lineType=labour&description=repair 2h&qty=2&unitPrice=150&taxPercent=5&chargeable=1` → line row `labour 2.0000×150.00`; job `grand_total=315.00` (line+5% tax recomputed server-side).
3. `rma-create` without lines → 302 to `?err=Add at least one return line (item_id,qty,...)` (verbatim refusal).
4. `rma-create` `rmaNo=RMA-CYC-1&customerId=1&sourceId=5&reason=defective&restock=1&linesCsv=7,2,100` → 302 `?ok=RMA RMA-CYC-1 created`, RMA + line rows.
5. `rma-resolve` `rmaId=1&disposition=refund&refundAmount=200` → `?ok=RMA resolved (refund)`; row `status=closed`,`disposition=refund`,`refund_amount=200.00`.
6. `warranty-register` `itemId=7&serialNo=SN-88&customerId=1&months=12&startDate=2026-10-01` → row `expires_at=1822726302`.
7. `job-close` `jobId=1` → `?ok=Service job closed`; row `status=closed`.
8. Body contract: JSON `{confirmWrites:true}` on these routes returns the dry-run envelope (JSON is the dry-run path); form-encoded `confirmWrites=1` is the live write path — same as PHP's native forms. Form key for RMA lines is `linesCsv` (or single-line `itemId`/`qty`/`unitPrice`), not `lines`.

## Zero residue

`DROP TABLE epc_as_jobs, epc_as_job_lines, epc_as_rma, epc_as_rma_lines, epc_as_warranty` — `SHOW TABLES LIKE 'epc_as%'` empty.

## Tests

`dotnet test tests/EcomAE.Platform.Tests --filter Erp` → 2254/2254 pass.
