# ERP VAT-refund (BOS tourist) cycle rehearsal — 2026-10-05

Cycle: `/erp/ajax/bos-vat-refund-save` → `bos-vat-refund-status` transitions,
live on the throwaway tenant MariaDB. `epc_bos_vat_refunds` was absent
beforehand and auto-created by `ErpLazySchema.EnsureVatRefundsAsync` (#2017).

## Steps

1. `bos-vat-refund-save {tag_ref TAG-CYC, country AE, sale 1000, vat 50,
   confirmWrites:true}` → `ok:true "Refund record saved"`, id 1, status
   `recorded`. Service computed `fee_amount=4.80` (PHP percentage-fee
   behaviour; submitted refund/fee fields are recomputed, not stored).
   `tag_ref` stored empty — the body field is `tagRef`-style named binding;
   harmless fixture-side note.
2. `bos-vat-refund-status` id 1 → `validated` → `exported` → `refunded`,
   each `ok:true "Status updated"`; SQL shows `status=refunded`.
3. `targetStatus:"bogus"` → verbatim refusal `Invalid status`, zero writes;
   row stays `refunded`. Allow-list: recorded/validated/exported/refunded/void.

## Residue

Row deleted; `epc_bos_vat_refunds` dropped (restored pre-rehearsal absence —
PHP lazily recreates it).

## Notes

Status body key is `targetStatus` JSON (`status`/`target_status` accepted
only via form).
