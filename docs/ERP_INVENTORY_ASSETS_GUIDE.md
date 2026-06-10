# ERP — Inventory, fixed assets & opening balances

Guide for **Client CP → Shop → ERP Finance** (tabs: **Inventory**, **Fixed assets**, **Opening balances**).

## Who this is for

Established businesses migrating from spreadsheets or another ERP need:

- Multi-warehouse stock with **weighted average cost**
- Purchase → sale → **period closing** inventory
- Perishables (batch + expiry) and **five configurable custom fields** per SKU
- **Fixed asset register** with depreciation methods and book value
- **Opening balances** on a chosen date (COA, inventory, assets)

## 1. Inventory

### Warehouses

1. Open **ERP → Inventory**.
2. Click **Sync warehouses from shop storages** to import existing `shop_storages`, or create a warehouse with code + name.
3. Each warehouse holds stock independently; transfers use movement types (future: paired transfer in/out).

### Items (SKU master)

1. **New inventory item**: SKU, name, type:
   - **Standard** — generic goods
   - **Perishable** — enables expiry/batch on movements
   - **Serialized** — use custom fields for serial tracking
2. Fill **Custom fields 1–5** (labels editable in DB table `epc_erp_inv_field_defs`).

### Movements & costing

| Movement        | Effect on qty | Effect on avg cost      |
|----------------|---------------|-------------------------|
| Purchase in    | +             | Weighted average update |
| Sale out       | −             | COGS at current average |
| Opening        | +             | Sets cost if first receipt |
| Adjustment     | +/−           | + increases avg; − uses current avg |

**Weighted average formula** when receiving stock:

`new_avg = (old_qty × old_avg + in_qty × in_cost) / (old_qty + in_qty)`

### Perishables

On purchase/opening, set **Batch** and **Expiry date**. Stock is tracked per warehouse + item + batch + variant label.

### Period closing

1. Choose **period end date** and optional warehouse.
2. **Run closing** — writes snapshot to `epc_erp_inv_closing` (qty, avg cost, value) for audit and month-end reports.

### Valuation

Dashboard KPI **Stock valuation** = Σ (qty on hand × weighted average unit cost).

## 2. Fixed assets

### Register an asset

**ERP → Fixed assets → Register new asset**

- Cost, salvage value, useful life (months)
- Method: straight line, declining balance, double declining, units of production
- **Opening accumulated depreciation** — for migrated balances (book value = cost − accumulated)

### Depreciation run

1. Select **period month** (YYYY-MM).
2. **Run depreciation** — one run per month; updates accumulated depreciation and book value per asset.

### Tracking

Set **Location** and **Tracking ID** on registration; events logged in `epc_erp_fa_tracking_log`.

## 3. Opening balances

For go-live on e.g. **1 Jan 2025** with historical balances:

1. **ERP → Opening balances → Create draft batch** (module: combined / COA / inventory / fixed assets).
2. Note the **Batch ID**.
3. Add lines:
   - **COA**: batch ID + account + debit/credit → updates `opening_balance` on post
   - **Inventory**: batch ID + warehouse + item + qty + unit cost (+ batch/expiry)
4. **Post opening batch** — applies all lines; inventory posts as `opening` movements dated **as of** batch date.

### Order of operations (recommended)

1. Create warehouses & SKUs (or sync storages).
2. Create opening batch with `as_of_date`.
3. Add inventory + COA lines.
4. Register fixed assets with opening accumulated depreciation **or** add via opening batch.
5. Post batch once reviewed.
6. Normal operations: purchase in / sale out.
7. Month-end: inventory closing, then fixed asset depreciation run.

## 4. Department access

| Department  | Tabs |
|------------|------|
| Finance    | Inventory, Fixed assets, Opening balances, GL, COA, … |
| Logistics  | Inventory, Fulfilment |
| Purchase   | Inventory, Purchases |
| Admin      | All tabs |

Configure in **ERP → Staff** (department groups).

## 5. Deploy / migrate

On server (epartscart or tenant):

```
https://www.<tenant>/epc-erp-inventory-assets-setup.php?token=epartscart-deploy-2026
```

Creates tables and syncs warehouses from shop storages.

## 6. Marketing & Super CP

Platform page: [ecomae.com/platform](https://www.ecomae.com/platform) — sections **ERP inventory** and **ERP fixed assets**.

Super CP module pack `erp` enables finance tabs per industry template.

## Technical reference

| Module | PHP |
|--------|-----|
| Inventory | `content/shop/finance/epc_erp_inventory.php` |
| Fixed assets | `content/shop/finance/epc_erp_fixed_assets.php` |
| Opening | `content/shop/finance/epc_erp_opening.php` |
| CP UI | `cp/content/shop/finance/erp/erp_tabs_*.php` |
| AJAX | `ajax_erp.php` actions `inv_*`, `fa_*`, `opening_*` |
