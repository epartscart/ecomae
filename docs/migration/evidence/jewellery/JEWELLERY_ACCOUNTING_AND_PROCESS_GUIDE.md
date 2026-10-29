# Jewellery trading ERP: business process and accounting guide

Sandbox: https://indj-sandbox-uae.ecomae.com/erp?company=1. The tenant database is `indj_sandbox`; live `indj` is never written by samples or migration.

This guide covers what the ERP does today: how a jewellery trade flows through the screens, which journal each document posts, how VAT and IFRS are handled, and how to prove the books reconcile. It describes technical behaviour. It is **not** an IFRS opinion: accounting policies, estimates (NRV, ECL rates) and disclosures must be approved by a qualified accountant.

## 1. Three components everywhere: weight, rate, value

Every jewellery line carries:

| Component | Fields | Rule |
|---|---|---|
| Weight | pcs, gross wt, stone wt, net wt, purity, pure wt | pure wt = (gross − stone) × purity |
| Rate | metal rate per gram (24K board, or karat rate), making rate, currency rate | fixed (locked) or unfixed metal |
| Value | metal value, making, stone, discount, VAT, line total | metal value = pure wt × rate |

The stock master (`epc_jewel_metal_stock`) keeps pcs, grams and value per stock code at **weighted average cost**. Tags (`epc_jw_tags`) move between `in_stock` and `sold`.

## 2. Business process and screens

| Step | Screen (Jewellery menu) | Document | Stock effect |
|---|---|---|---|
| Masters | Masters: karat, currency, rate type, design, diamond, pearl, colour stone, tag/barcode. Parties and POS customers are on the Workspace › INDUS migration tab | n/a | n/a |
| Daily rate | Masters › Gold rates | `epc_gold_rates` buy/sell per karat | n/a |
| Buy metal / jewellery | Fixing › Metal purchase (RMP), Diamond purchase | Purchase voucher | + pcs, grams, value at purchase cost |
| Fix price | Fixing › Purchase / sales fixing | Fixing voucher | n/a (locks rate) |
| Sell | Retail POS (RIN), Metal sales (MSL) | Tax invoice | − at average cost; tag → sold |
| Take old gold | Retail POS › old gold exchange | on the sale | + old gold (1320) |
| Return | Sales return (SRT) | Tax credit note | + back at average cost |
| Advance | POS advance (ADV) | Receipt voucher | n/a |
| Expenses | Petty cash, Journal voucher (JVL) | Petty cash / JV | n/a |
| Repairs | Repairs: receipt → workshop → delivery | Repair job | n/a |
| Count | Stock verification | Count sheet + variance | variance report |
| Month-end | Workspace › Month-end | GL posting and IFRS & VAT reconciliation | n/a |

Opened posted records open in a separate tab with General, Accounting (GL lines) and Print preview tabs.

## 3. Posting matrix: document → journal

Each journal reference is `JW-V-{voucherId}-{suffix}`. A reference that has already been posted is never posted again (`PostOnceAsync`), so a re-run does not create duplicates. Debits must equal credits or the journal is refused (`ErpGlPostingService.Validate`). Locked or soft-closed periods refuse posting (`AssertPostingPeriodOpenAsync`).

| Document | Suffix | Dr | Cr |
|---|---|---|---|
| Metal / diamond purchase | `-PUR` | 1300 Inventory (stock cost moved, i.e. Σ line cost) + 1150 Input VAT (on making/stone) | 2000 Payable (gross) |
| Purchase, UAE reverse charge | `-RCM` | 1150 Input VAT (RCM) | 2100 Output VAT (RCM) |
| Supplier payment on voucher | `-PAY` | 2000 Payable | 1000 / 1010 / 1030 by tender |
| Sale (tax invoice) | `-SAL` | 1100 Receivable (gross) | 4000 Revenue (net) + 2100 Output VAT |
| Cost of sale | `-COGS` | 5000 COGS (average cost) | 1300 Inventory |
| Old gold exchange | `-OGX` | 1320 Old gold stock | 1100 Receivable |
| POS receipt | `-RCV` | 1000 Cash / 1030 Card / 1010 Bank / 2300 Advance | 1100 Receivable |
| Sales return (credit note) | `-SRT` | 4100 Sales returns + 2100 Output VAT reversed | 1100 Receivable |
| Return to stock | `-COGS` | 1300 Inventory | 5000 COGS |
| Refund | `-RFD` | 1100 Receivable | tender account |
| Customer advance | `-ADV` | tender account | 2300 Customer advances (contract liability) + 2100 VAT on advance |
| Journal voucher | `-JV` | lines as entered | lines as entered |
| Petty cash | `JW-PC-{id}` | expense account | 1020 Petty cash |
| Opening stock | `JW-OPEN-STK-{id}` | 1300 Inventory | 3900 Opening balance equity |

## 4. VAT (UAE Federal Decree-Law 8/2017 and Cabinet Decision 25/2018)

- **Standard rate 5%** on retail jewellery: the sale VAT basis is the full value, or making + stone + other only where the metal is under reverse charge (the VAT basis selector on the voucher).
- **Domestic reverse charge** on gold, silver and platinum bought by a VAT-registered dealer for resale or manufacture. The metal VAT is not paid to the supplier; the buyer self-accounts output VAT and recovers the same amount as input VAT. On the voucher, enter the metal under the basis "Making + stone + other (metal under RCM)"; `rcm_vat` is saved and the `-RCM` journal is posted. On the VAT return this goes to box 3 (output) and box 10 (input).
- **Credit notes** reverse output VAT on the return date.
- **Advances:** VAT is due on receipt of payment (tax point), so it is posted with the advance.
- **Tourist refund** (Planet) is in Sales › Tourist VAT.
- Eligibility depends on the counterparty being registered and the purpose of the purchase. Apply RCM per transaction, not to every purchase.

## 5. IFRS policies implemented

| Standard | Implementation | Status |
|---|---|---|
| IAS 2 Inventories | Cost = purchase price incl. making/stone; weighted average cost formula; COGS at average cost; inventory is **not** revalued to the gold price | Implemented |
| IAS 2 NRV | Month-end NRV indicator: metal content × latest entered 24K buy rate × purity, less 2% costs to sell | Report only; the write-down needs accountant approval |
| IFRS 15 Revenue | Revenue at the invoice date net of VAT; returns to contra-revenue 4100; advances held as contract liability 2300 until the sale | Implemented |
| IAS 21 FX | Foreign-currency vouchers carry the currency rate; GL posted in AED at the spot rate | Transaction posting implemented. Period-end revaluation exists in Finance (FX revaluation), not jewellery-specific |
| IFRS 9 ECL | Month-end provision matrix on unpaid sale vouchers by age | Report only; illustrative rates to calibrate |
| IAS 7 Cash flows | Month-end cash flow statement (direct method from cash journals), reconciled to the movement on 10xx | Implemented |
| IAS 1 | Statement of financial position and P&L in Report centre; statement of changes in equity in Month-end | Implemented. Notes and disclosures are prepared outside the ERP |
| IAS 16/36, IAS 37, IAS 12, IFRS 16 | General Finance modules (fixed assets, provisions, tax) | Not jewellery-specific; to review with the accountant |

## 6. Reconciliation (Workspace › Month-end › IFRS & UAE VAT reconciliation)

Pick From/To and press Reconcile. Every check shows Expected, Actual, Difference and PASS or DIFFERENCE:

1. No journal with debits ≠ credits (up to the period end).
2. Trial balance closes to zero.
3. Assets = liabilities + equity + retained result.
4. GL 1300 + 1320 = stock sub-ledger value. Only for periods ending today or later: the stock master holds the current position only.
5. Output VAT 2100 = sales + advances − credit notes + RCM.
6. Input VAT 1150 = purchase VAT + journal-voucher VAT + RCM.
7. COGS 5000 = cost of stock issued − returns.
8. Net cash flow = movement on cash and bank.
9. Opening equity + profit + capital movements = closing equity.

Purchase vouchers whose posted inventory differs from the stock they added are listed under the checks. **Posted journals are never edited.** Correct them with an approved adjusting journal (Journal voucher: Dr 1300 / Cr supplier) after checking the supplier invoice.

## 7. Sample data on the sandbox

Loaded by the INDUS samples (Workspace › INDUS migration › Load INDUS sample data), the jewellery demo, and the IFRS scenarios below:

| Scenario | Voucher | What it proves |
|---|---|---|
| 24K bar 10 g under reverse charge (2 suppliers) | RMP, `rcm_vat` 249.75 | `-PUR` + `-RCM` journals; VAT boxes 3/10 |
| Imported 22K rings invoiced in USD @ 3.6725 | RMP | AED posting at spot rate (IAS 21) |
| 22K bangle sale with old-gold exchange, cash + card | RIN | `-SAL`, `-COGS`, `-OGX`, `-RCV` |
| Credit sale dated July | RIN | ECL ageing bucket 91–180 days |
| Customer advance for an order | ADV | contract liability 2300 + VAT at tax point |
| Return of a 22K ring | SRT | contra revenue, VAT reversal, stock back |
| INDUS GH-5120, HO-210, HO-37047, SFX-218, journal, petty cash, repair, stock count | INDUS samples | full INDUS workflow |

## 8. Known limitations and decisions for the accountant

- **Old purchase vouchers:** purchases saved before the Metal purchase rebuild posted only the VAT base to 1300 (e.g. voucher #20 posted 5,000 while 43,211.75 entered stock). New postings use the stock cost. The old ones are listed in the reconciliation and need adjusting journals.
- **Daily rates are carried forward:** daily gold rates copy the last entered rate forward each day (`ErpGoldRateBoard.EnsureTodayAsync`). A wrong rate entered once (sandbox company 1: 24K 147.89 on 13 Oct) keeps being copied. Enter a correct rate daily. NRV ignores the system copies.
- **Retained earnings:** year-end close marks the year closed without posting a closing journal. Statements compute retained earnings from the cumulative P&L. Account 3900 is used both as opening-balance equity and as the P&L clearing account in the year-close payload; keep them apart if closing journals are ever posted.
- **Account names:** 1300 is still named "Parts Inventory" by the shared COA seeder; rename it to "Inventory — jewellery & metal" in the COA screen.
- **Sales return cost:** a return comes back at the current average cost, not the original sale's cost.
- **Partial stock-move failure:** a database error mid-voucher can leave some lines moved. Retrying can double them; there is no per-line idempotency marker yet.
- **AML/KYC gate:** purchases from a supplier above AED 55,000 cumulative need a complete KYC file (Cabinet Resolution 134/2025); the voucher save refuses them otherwise.

## 9. Safe deployment, backup and rollback

- The gated deploy (`scripts/cloudpanel_deploy_main_gated.sh`) runs the full test suite twice, the PHP-leak and UI-stack guards, publishes a new release and health-checks it. The last 3 releases are kept for rollback.
- Before sample writes and engine changes, the sandbox was dumped to `/root/backup_indj_sandbox_full_pre_ifrs_samples_*.sql.gz` and `/root/backup_indj_sandbox_gl_pre2505_*.sql.gz` on the server.
- Roll back the data with `gunzip -c <file> | mysql indj_sandbox`, or re-run the sandbox refresh from production. Roll back code by reverting the PR on `main` and re-running the gated deploy.
- Nothing in these changes edits a historical posted journal. Engine changes affect new postings only.

## 10. Where things are

| Need | Path |
|---|---|
| Trial balance | `/erp/trial-balance-app` |
| Balance sheet / P&L | `/erp/report-center-app?tab=balance_sheet`, `?tab=pl` |
| GL journals | `/erp/gl-journals-app` |
| IFRS & VAT reconciliation, cash flow, equity, NRV, ECL | `/erp/jewellery/workspace?tab=month-end` |
| Metal stock / ledger / metal TB | `/erp/jewellery-stock-verification-app?tab=jw_metal_stock` / `jw_stock_ledger` / `jw_metal_tb` |
| INDUS import, all 23 templates, live rows, all fields | `/erp/jewellery/workspace?tab=indus` |
| Prints | `/erp/jewellery/voucher-print?id=<id>` (`&kind=fixing|petty|repair`); customise in `/erp/print-designer-app` |
| CSV import guide | `indus_csv/INDUS_CSV_IMPORT_GUIDE.md` |
