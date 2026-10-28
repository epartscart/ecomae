# INDUS → ecomae jewellery CSV migration guide

Where to load: **sandbox first** — `https://indj-sandbox-uae.ecomae.com/erp/jewellery/workspace?tab=indus&company=1`.
The loader refuses any database that is not `*_sandbox` (live `indj` is never written).

## Files in this folder

| Path | What it is |
|---|---|
| `templates/NN_<key>.csv` | Blank template — header row only. Export from INDUS into these columns. |
| `samples/NN_<key>.csv` | Sample data built from real INDUS documents (GH-5120 tax invoice, HO-210 tax credit note, HO PUR 37047, SFX 218, PCR 225, GJV 491, PET 217). Loaded by **Load INDUS sample data**. |
| `INDUS_FIELD_MAP.csv` | Every column: INDUS field → ecomae `table.column` → module → workflow → GL / report it feeds. Generated from `ErpJwIndusImportSpec.cs` (test `RepoTemplatesAndFieldMapMatchTheSpec` keeps it in sync). |

The same templates, samples and field map download from the **INDUS migration** tab.

## Load order (each step depends on the ones above it)

| # | Template | INDUS source | Feeds |
|---|---|---|---|
| 01 | coa | Account Master + Trial Balance at cut-off | Opening journal `JW-OPEN-TB` → trial balance, balance sheet |
| 02 | parties | Debtor / Creditor master | Purchase, fixing, trade sale party |
| 03 | pos_customers | POS Customer / KYC | Retail sale, return, advance, repair; AML/KYC |
| 04–07 | currencies, karats, rate_types, daily_rates | Metal / Currency / Daily rate masters | Purity, rate conversion, invoice karat-rate band |
| 08 | metal_stock | Stock master + Metal Stock Balance (on date) | Stock report (pcs, grams, value); opening journal `JW-OPEN-STK` Dr 1300 / Cr 3900 |
| 09–12 | designs, diamonds, color_stones, pearls | Design / Diamond / Colour stone / Pearl masters | Item components |
| 13 | tags | Barcode / tag list | POS scan; sale → `sold`, return → `in_stock` |
| 14 | vouchers | PUR, DPU, POS, MSL, PSR, PCR, GJV, BTR, MFT headers | Stock movement + GL journal per voucher |
| 15 | voucher_lines | Line grid (stock code, pcs, gr/net/pure wt, rates, VAT) | Weight & value per stock code, COGS at average cost |
| 16 | voucher_receipts | Receipt mode grid | CASH/NET→1000, CARD/VISA→1030, BANK→1010 vs AR 1100 |
| 17 | voucher_accounts | JV detail / Other amounts | GL Dr/Cr lines |
| 18 | fixing | PFX / SFX | Fixing reports |
| 19 | petty_cash | PET | Dr expense / Cr 1020 petty cash |
| 20–21 | repairs, repair_items | Repair receipt | Repair pipeline |
| 22–23 | stock_verifications, *_lines | MSV | Physical vs computer stock |

## Rules

* `source_ref` is the unique INDUS key (e.g. `V-HO-PUR-37047`). Re-importing the same file skips rows already loaded.
* Child files (`voucher_lines`, `voucher_receipts`, `voucher_accounts`, `repair_items`, `stock_verification_lines`) carry `parent_ref` = the parent's `source_ref`.
* Dates `dd/MM/yyyy`; numbers may contain thousands commas; flags `Y`/`N`.
* INDUS voucher codes are mapped (POS→RIN, PSR→SRT, PUR→RMP, DPU→RDP, PCR→ADV, GJV→JVL, MFT→MFG, MLC→BTR); the INDUS code is kept in `source_voc_type`.
* `gl_mode=history` keeps an old voucher for reference without moving stock or posting GL — use it for transactions **before** the cut-off whose effect is already in the opening stock / trial balance.
* Unknown columns are rejected (nothing is silently dropped).

## Steps

1. Download the blank templates; export each INDUS screen/report into them (one file per template).
2. Upload with **Write** unticked → dry run lists every row error.
3. Fix, then upload again with **Write** ticked.
4. Check: Jewellery workspace → Stock / Reports, Trial balance, Dashboard. Stock value = GL 1300 inventory; AR 1100 clears when receipts are loaded.
5. **Remove INDUS-imported data** deletes only loader rows and their `JW-*` journals (sandbox only).

## Prints (INDUS layout)

`/erp/jewellery/voucher-print?id=<voucher id>` — TAX INVOICE (POS / sale), TAX CREDIT NOTE (return), METAL / DIAMOND PURCHASE, RECEIPT VOUCHER (advance), JOURNAL VOUCHER. `&kind=fixing|petty|repair&id=<row id>` for fixing, petty cash and repair receipts; `&copy=customer|party|office`. Print links are on the Purchases, Sales and Repairs tabs. Company name / TRN come from `epc_org_company_settings` keys `jewellery_print_company_name` / `jewellery_print_trn` (falls back to the e-invoice seller profile).

## Where each report is (sandbox: https://indj-sandbox-uae.ecomae.com)

| Report / screen (INDUS equivalent) | ERP path | Shows |
|---|---|---|
| ERP dashboard (KPIs, cash, AR/AP, sales) | `/erp?company=1` | All modules incl. general ERP demo data |
| Jewellery workspace — stock (Metal Stock Balance) | `/erp/jewellery/workspace?tab=stock` | PCS, gross g, value per stock code; total = GL 1300 jewellery share |
| Jewellery workspace — reports / month-end | `/erp/jewellery/workspace?tab=reports`, `?tab=month-end` | Stock + sales + purchase summary; Post to GL |
| Jewellery workspace — purchases / sales / repairs (with **Print**) | `?tab=purchases`, `?tab=sales`, `?tab=repairs` | Vouchers with INDUS-layout print links |
| INDUS migration (upload, dry run, samples, templates) | `?tab=indus` | Import result, templates table, field map |
| Metal stock master (weights) | `/erp/jewellery-stock-verification-app?tab=jw_metal_stock` | PCS, gross/net/pure g by stock code |
| Stock balance (weight) | `/erp/jewellery-stock-verification-app?tab=jw_stock_balance` | Weight balance by karat |
| Stock verification (MSV) | `/erp/jewellery-stock-verification-app?tab=jw_stock_verification` | Computer vs physical stock |
| Metal purchase (PUR / DPU) | `/erp/jewellery-fixing-app?tab=jw_metal_purchase` | Purchase vouchers |
| Sales / purchase fixing (SFX / PFX) | `/erp/jewellery-fixing-app?tab=jw_sales_fixing` | Fixings |
| Metal sales, sales return, sales analysis (weight) | `/erp/jewellery-retail-app?tab=jw_metal_sales`, `?tab=jw_sales_return`, `?tab=jw_sales_analysis` | POS/retail sales by weight and value |
| Repairs | `/erp/jewellery-repairs-app` | Repair jobs, transfer, delivery |
| Masters (karat, rate type, currency, designs, stones) | `/erp/jewellery-masters-app` | Master data |
| Trial balance | `/erp/trial-balance-app` | All GL accounts (1000 cash, 1020 petty cash, 1030 card, 1100 AR, 1300 inventory, 1320 old gold, 2100 VAT, 2300 advances, 4000 sales, 4100 returns, 5000 COGS…) |
| Balance sheet / income statement | `/erp/report-center-app?tab=balance_sheet`, `?tab=pl` | Financial statements from the same GL |
| Stock movements ledger | `/erp/stock-movements-app` | General inventory movements |

Print previews: `/erp/jewellery/voucher-print?id=<voucher id>` (TAX INVOICE, TAX CREDIT NOTE, PURCHASE, RECEIPT VOUCHER, JOURNAL VOUCHER), `&kind=fixing`, `&kind=petty`, `&kind=repair`; add `&copy=party` for the party copy.

### Reconciliation (fresh sample load)
- Jewellery stock value 685,974.38 = jewellery entries in GL 1300; the trial balance's 1300 (680,974.38) also contains the general ERP demo journal `DEMO-JNL` (−5,000).
- 24TTB: 10 PCS, 1,166.40 g, 557,434.38 (PUR 37047). Sold INDUS tags go to 0; returned ring 9872 comes back at 700.
- Sales 35,415.72, returns 1,047.62, COGS 28,240.00, old gold 1,780.00, POS advance 952.38, petty cash 4,583.36.
- Refresh instead of Remove when stock codes that existed before the import were touched: `bash scripts/cloudpanel_sandbox_db_refresh.sh indj ecomae`, then load samples again.
