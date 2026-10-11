# Wave 6 close-out — Jewellery + Fit-Out (2026-10-11)

Merged tranches: #2115 (fixing list columns), #2117 (retail voucher totals),
#2118 (repair transfer/workshop headers), #2119 (rate-type + currency tabs),
#2120 (diamond/design/pearl/color-stone tabs), #2121 (barcode tab),
#2122 (project accounting totals), #2123 (jewellery tags → epc_jw_tags),
#2124 (per-tab fix_type filter PF/SF + sales-analysis aggregate), #2125
(purchase window = open unfixed lots).

Findings fixed this wave:
- Header/cell drift on fixing, repair transfer and workshop tabs.
- Nine masters tabs rendered header specs with no data binding — now bound
  company-scoped: karat, rate_type, currency, diamond, design, pearl,
  color_stone, barcode (epc_jewel_barcode), jewellery_tag (epc_jw_tags).
- Retail sales-analysis tab rendered nothing — now the PHP
  epc_jewel_sales_analysis aggregate (voucher-date grouping over
  RIN/MSL/RSL vouchers) with a totals row.
- Fixing list showed every row on every tab — now PF/SF filtered like PHP;
  purchase window shows PF rows with unfixed_qty > 0 (open lots).
- Project accounting tables gained totals rows (budgets, transactions,
  recognition).

Remaining known gaps:
- gold_rate / gold_scheme / jw_seed_data are form-only tabs in PHP as well
  (no backing table) — parity intact.
- Live render verification blocked locally: industry-gate 404 needs a
  jewellery-enabled company via ?company=<id> on a tenant host.
- BF Fit-Out board bands raise to ~70/65/50/60/15; BJ Jewellery ~75/70/50/62/35.
