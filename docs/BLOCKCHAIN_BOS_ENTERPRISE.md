# Blockchain BOS Enterprise System

ECOM AE is positioned and engineered as one **Blockchain BOS Enterprise System** — not a normal BOS with a blockchain sticker.

## Product model

| Layer | Role |
|---|---|
| Commerce / Ops / Finance / CRM / Compliance / AI | Operational BOS (MySQL) |
| Blockchain proof layer | Cryptographic integrity (hashes + Merkle anchors) |

MySQL remains the system of record. Blockchain proves selected facts; it does not store carts, sessions, or live stock.

## Modes (`epc_portal_tenants.blockchain_mode`)

| Mode | Meaning |
|---|---|
| `anchor` | **Default** — record SHA-256 proofs and Merkle-anchor batches |
| `network` | Reserved for permissioned network participation |
| `off` | Disabled for that tenant |

## Core files

| File | Purpose |
|---|---|
| `content/general_pages/epc_blockchain_bos.php` | Hash, Merkle, record, anchor, verify |
| `epc-blockchain-verify.php` | Public JSON verify API |
| Platform job `blockchain_anchor_batch` | Drains pending proofs via cron |

## Tables (platform DB)

- `epc_bc_proofs` — per business-fact proof
- `epc_bc_anchor_batches` — Merkle roots / anchor refs

## Operator UI

| Surface | Path |
|---|---|
| Invoice / e-invoice detail | Badge + Verify on document view |
| Invoice print | Blockchain proof block + absolute verify URL after totals |
| Purchase invoices | Receipt + Blockchain columns; purchase view shows GRN proof badge |
| After-sales RMA | Service → **After-sales RMA** list/detail + warranty RMA list with proof badges; create flash includes verify URL |
| ERP dashboard | Proof KPI strip (total / anchored / pending) when mode ≠ off |
| ERP proofs list | Tax → **Blockchain proofs** (`tab=blockchain_proofs`) also under Audit workbench |
| Super CP fleet | Tenant Hub → **Blockchain** tab (proofs, filters, per-tenant mode, Anchor pending now) |
| Public verify | `/epc-blockchain-verify.php` (+ link to `/blockchain`) |
| Purchase + receive | Success message includes GRN verify URL for `create_purchase` and `purchase_from_order` (ERP + Procurement) |

Staff allowlist includes `aftersales` and `blockchain_proofs` so department users can open those tabs.

Helpers: `epc_bc_bos_lookup_proof()`, `epc_bc_bos_list_proofs()`, `epc_bc_bos_list_proofs_fleet()`, `epc_bc_bos_fleet_stats()`, `epc_bc_bos_tenant_proof_stats()`, `epc_bc_bos_document_badge_html()`, `epc_bc_bos_grn_flash_for_purchase()`, `epc_bc_bos_verify_url_absolute()`, `epc_bc_bos_anchor_network()`, `epc_th_update_tenant_blockchain_mode()`, `epc_th_anchor_blockchain_pending_now()`.

## Phase complete (anchor mode)

This phase ships a complete **anchor-mode** Blockchain BOS Enterprise proof layer: record → Merkle batch → public verify across invoices/CN/GRN/RMA, with operator surfaces in ERP and Super CP. Permissioned `network` mode remains roadmap.

### Super CP fleet controls

- Per-tenant `blockchain_mode` can be changed inline on the Blockchain tab (not only Onboard edit).
- **Anchor pending now** drains up to 100 pending proofs immediately (same as cron job `blockchain_anchor_batch`).
- Anchor network label is read-only from `EPC_BC_ANCHOR_NETWORK` (default `local_merkle`).
- `network` mode is roadmap: today it records and anchors like `anchor`.

## Auto-hooks (live documents)

Best-effort after successful commit (never blocks the business transaction):

| Record type | Hook |
|---|---|
| `invoice` | `epc_einvoice_save_document` (validated tax invoices) |
| `credit_note` | `epc_einvoice_save_document` + `epc_einvoice_create_credit_note` |
| `grn` | `epc_erp_inventory_receive_purchase` (when lines posted) |
| `rma` | `epc_as_rma_create` + `epc_rma_create` |

All go through `epc_bc_bos_maybe_record_document()` which:
1. Resolves tenant `site_key`
2. Skips when `blockchain_mode=off`
3. Records proof + enqueues `blockchain_anchor_batch`

## Usage

```php
require_once __DIR__ . '/epc_blockchain_bos.php';

// Preferred (mode-aware, resolves tenant):
epc_bc_bos_maybe_record_document('invoice', 'INV-1001', [
    'total_incl_vat' => 1250.00,
    'currency_code' => 'AED',
]);

// Direct:
$out = epc_bc_bos_record_proof(
    'acme',
    'invoice',
    'INV-1001',
    ['total' => 1250.00, 'currency' => 'AED', 'customer' => 'C-9'],
    ['enqueue_anchor' => true]
);

// Public verify UI + JSON:
// GET /epc-blockchain-verify.php?proof=prf_xxx
// GET /epc-blockchain-verify.php?proof=prf_xxx&format=json
```

## Cron

Uses the existing platform jobs worker:

```cron
* * * * * php /var/www/ecomae/epc-platform-jobs-cron.php >/dev/null 2>&1
```

Optional: set `EPC_BC_ANCHOR_NETWORK` (default `local_merkle`) when wiring an external anchor endpoint.

## Marketing

Customer-facing copy uses **Blockchain BOS Enterprise System** / **one unified system** across homepage hero, sections, `/bos`, `/solutions`, docs and industry footers.

Dedicated landing (structure, process, modes, operator surfaces + premium 3D): **`/blockchain`**.

## Smoke test

```bash
php tests/erp_advanced/run_blockchain_bos_tests.php
```
