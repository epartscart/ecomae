<?php
/**
 * Jewellery ERP — Retail Sales / POS Invoice (RIN).
 * Full retail invoice with metal & diamond tabs, old gold exchange, receipts.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$divisions = epc_jewel_divisions();

erp_page_header('<i class="fa fa-shopping-cart"></i> Retail Sales Invoice', 'POS retail invoice with metal, diamond, old-gold exchange and receipts.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Retail sales'),
));
?>
<div class="ef-window">
	<div class="ef-title">Retail Sales Invoice - (RIN)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-search"></i> Find</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-barcode"></i> Barcode</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-check-square"></i> Approve</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-undo"></i> Old Gold</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-credit-card"></i> Receipt</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_voucher_save">
		<input type="hidden" name="voc_type" value="RIN">

		<!-- Header -->
		<div class="ef-section">
			<span class="ef-section-title">Invoice Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option><option value="B1">B1</option><option value="B2">B2</option></select></div>
				<div class="ef-field"><label>Voc Type</label><input value="RIN" class="ef-readonly" readonly style="width:50px;background:#e8e8e8"></div>
				<div class="ef-field"><label>Voc Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Voc No.</label><input name="voc_no" class="ef-readonly" readonly placeholder="Auto" style="width:60px;background:#e8e8e8"></div>
				<div class="ef-field"><label>Till</label><select name="till"><option value="1">Till 1</option><option value="2">Till 2</option></select></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Customer Code</label><input name="party_code" placeholder="CSH"></div>
				<div class="ef-field ef-field-wide"><label>Customer Name</label><input name="party_name" placeholder="Cash / Walk-in"></div>
				<div class="ef-field"><label>Mobile</label><input name="mobile"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Salesman</label><input name="salesman" placeholder="SM01"></div>
				<div class="ef-field"><label>Currency</label><select name="currency"><option value="AED">AED</option><option value="USD">USD</option></select></div>
				<div class="ef-field"><label>Curr. Rate</label><input name="currency_rate" type="number" step="0.000001" value="1.000000"></div>
				<div class="ef-field"><label>TRN</label><input name="trn" placeholder="Tax Reg Number"></div>
			</div>
			<div class="ef-checks">
				<label><input type="checkbox" name="tourist_vat_refund" value="1"> Tourist VAT Refund</label>
				<label><input type="checkbox" name="apply_vat" value="1" checked> Apply VAT</label>
			</div>
		</div>

		<!-- Tabs: Metal Items | Diamond Items | Old Gold | Receipts | Other Amounts -->
		<div class="ef-tabs">
			<ul class="nav nav-tabs" role="tablist">
				<li class="active"><a href="#rin_metals" data-toggle="tab">1. Metal Items</a></li>
				<li><a href="#rin_diamonds" data-toggle="tab">2. Diamond Items</a></li>
				<li><a href="#rin_old_gold" data-toggle="tab">3. Old Gold Exchange</a></li>
				<li><a href="#rin_receipts" data-toggle="tab">4. Receipts</a></li>
				<li><a href="#rin_other" data-toggle="tab">5. Other Amounts</a></li>
			</ul>
			<div class="tab-content">
				<!-- Metal Items -->
				<div class="tab-pane active" id="rin_metals">
					<table class="ef-grid">
						<thead><tr>
							<th>No.</th><th>Metal</th><th>Karat</th><th>Purity</th>
							<th>Gross Wt</th><th>Stone Wt</th><th>Net Wt</th><th>Pure Wt</th>
							<th>Rate/Gm</th><th>Metal Amt</th><th>Making/Gm</th><th>Making Amt</th>
							<th>Stone Amt</th><th>Line Total</th>
						</tr></thead>
						<tbody>
						<?php for ($r = 0; $r < 5; $r++): ?>
						<tr>
							<td><?php echo $r + 1; ?></td>
							<td><select name="metal_lines[<?php echo $r; ?>][metal]"><option value="">—</option><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($c); ?></option><?php endforeach; ?></select></td>
							<td><input name="metal_lines[<?php echo $r; ?>][karat]" style="width:30px"></td>
							<td><input name="metal_lines[<?php echo $r; ?>][purity]" type="number" step="0.000001" value="0"></td>
							<td><input name="metal_lines[<?php echo $r; ?>][gross_wt]" type="number" step="0.001" value="0"></td>
							<td><input name="metal_lines[<?php echo $r; ?>][stone_wt]" type="number" step="0.001" value="0"></td>
							<td><input name="metal_lines[<?php echo $r; ?>][net_wt]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
							<td><input name="metal_lines[<?php echo $r; ?>][pure_wt]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
							<td><input name="metal_lines[<?php echo $r; ?>][rate_gm]" type="number" step="0.01" value="0"></td>
							<td><input name="metal_lines[<?php echo $r; ?>][metal_amt]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
							<td><input name="metal_lines[<?php echo $r; ?>][making_gm]" type="number" step="0.01" value="0"></td>
							<td><input name="metal_lines[<?php echo $r; ?>][making_amt]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
							<td><input name="metal_lines[<?php echo $r; ?>][stone_amt]" type="number" step="0.01" value="0"></td>
							<td><input name="metal_lines[<?php echo $r; ?>][line_total]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
						</tr>
						<?php endfor; ?>
						</tbody>
					</table>
				</div>
				<!-- Diamond Items -->
				<div class="tab-pane" id="rin_diamonds">
					<table class="ef-grid">
						<thead><tr><th>No.</th><th>Item Code</th><th>Description</th><th>Pcs</th><th>Gross Wt</th><th>Tag Price</th><th>Disc %</th><th>Net Price</th></tr></thead>
						<tbody>
						<?php for ($r = 0; $r < 3; $r++): ?>
						<tr>
							<td><?php echo $r + 1; ?></td>
							<td><input name="dia_lines[<?php echo $r; ?>][item_code]"></td>
							<td><input name="dia_lines[<?php echo $r; ?>][description]" style="min-width:140px"></td>
							<td><input name="dia_lines[<?php echo $r; ?>][pcs]" type="number" value="0" style="width:40px"></td>
							<td><input name="dia_lines[<?php echo $r; ?>][gross_wt]" type="number" step="0.001" value="0"></td>
							<td><input name="dia_lines[<?php echo $r; ?>][tag_price]" type="number" step="0.01" value="0"></td>
							<td><input name="dia_lines[<?php echo $r; ?>][disc_pct]" type="number" step="0.01" value="0"></td>
							<td><input name="dia_lines[<?php echo $r; ?>][net_price]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
						</tr>
						<?php endfor; ?>
						</tbody>
					</table>
				</div>
				<!-- Old Gold Exchange -->
				<div class="tab-pane" id="rin_old_gold">
					<div class="ef-section">
						<span class="ef-section-title">Old Gold Exchange</span>
						<table class="ef-grid">
							<thead><tr><th>Metal</th><th>Karat</th><th>Purity</th><th>Gross Wt</th><th>Pure Wt</th><th>Rate/Gm</th><th>Amount</th></tr></thead>
							<tbody>
							<?php for ($r = 0; $r < 3; $r++): ?>
							<tr>
								<td><select name="old_gold[<?php echo $r; ?>][metal]"><option value="">—</option><option value="G">G</option><option value="S">S</option></select></td>
								<td><input name="old_gold[<?php echo $r; ?>][karat]" style="width:30px"></td>
								<td><input name="old_gold[<?php echo $r; ?>][purity]" type="number" step="0.000001" value="0"></td>
								<td><input name="old_gold[<?php echo $r; ?>][gross_wt]" type="number" step="0.001" value="0"></td>
								<td><input name="old_gold[<?php echo $r; ?>][pure_wt]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
								<td><input name="old_gold[<?php echo $r; ?>][rate_gm]" type="number" step="0.01" value="0"></td>
								<td><input name="old_gold[<?php echo $r; ?>][amount]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
							</tr>
							<?php endfor; ?>
							</tbody>
						</table>
					</div>
				</div>
				<!-- Receipts -->
				<div class="tab-pane" id="rin_receipts">
					<div class="ef-section">
						<span class="ef-section-title">Payment Receipts</span>
						<table class="ef-grid">
							<thead><tr><th>Pay Mode</th><th>Account</th><th>Currency</th><th>FC Amount</th><th>LC Amount</th><th>Card/Ref#</th></tr></thead>
							<tbody>
							<tr>
								<td><select name="receipts[0][mode]"><option value="Cash">Cash</option><option value="Card">Card</option><option value="Bank">Bank Transfer</option><option value="Cheque">Cheque</option><option value="Old Gold">Old Gold</option></select></td>
								<td><input name="receipts[0][account]"></td>
								<td><input name="receipts[0][currency]" value="AED" style="width:50px"></td>
								<td><input name="receipts[0][fc_amount]" type="number" step="0.01" value="0"></td>
								<td><input name="receipts[0][lc_amount]" type="number" step="0.01" value="0"></td>
								<td><input name="receipts[0][card_ref]"></td>
							</tr>
							<tr>
								<td><select name="receipts[1][mode]"><option value="Cash">Cash</option><option value="Card">Card</option><option value="Bank">Bank Transfer</option></select></td>
								<td><input name="receipts[1][account]"></td>
								<td><input name="receipts[1][currency]" value="AED" style="width:50px"></td>
								<td><input name="receipts[1][fc_amount]" type="number" step="0.01" value="0"></td>
								<td><input name="receipts[1][lc_amount]" type="number" step="0.01" value="0"></td>
								<td><input name="receipts[1][card_ref]"></td>
							</tr>
							</tbody>
						</table>
					</div>
				</div>
				<!-- Other Amounts -->
				<div class="tab-pane" id="rin_other">
					<div class="ef-section">
						<span class="ef-section-title">Other Charges</span>
						<div class="ef-row">
							<div class="ef-field"><label>Hallmark Charge</label><input name="hallmark_charge" type="number" step="0.01" value="0"></div>
							<div class="ef-field"><label>Certificate Charge</label><input name="certificate_charge" type="number" step="0.01" value="0"></div>
							<div class="ef-field"><label>Delivery Charge</label><input name="delivery_charge" type="number" step="0.01" value="0"></div>
						</div>
						<div class="ef-row">
							<div class="ef-field"><label>Discount</label><input name="discount_amount" type="number" step="0.01" value="0"></div>
							<div class="ef-field"><label>Discount %</label><input name="discount_pct" type="number" step="0.01" value="0"></div>
						</div>
					</div>
				</div>
			</div>
		</div>

		<!-- Summary Totals -->
		<div style="display:flex;justify-content:flex-end;">
			<div class="ef-section" style="min-width:300px;">
				<span class="ef-section-title">Invoice Summary</span>
				<div class="ef-totals">
					<div class="ef-tot-row"><label>Metal Amount</label><input name="total_metal" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Making Amount</label><input name="total_making" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Stone Amount</label><input name="total_stone" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Diamond Amount</label><input name="total_diamond" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Other Charges</label><input name="total_other" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Less: Old Gold</label><input name="total_old_gold" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Net Amount</label><input name="net_amount" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>VAT (5%)</label><input name="vat_amount" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Round Off</label><input name="round_off" type="number" step="0.01" value="0"></div>
					<div class="ef-tot-row" style="border-bottom:2px solid #333;font-size:14px"><label><strong>GROSS TOTAL</strong></label><input name="gross_total" type="number" step="0.01" value="0" class="ef-readonly" readonly style="font-weight:700;font-size:14px"></div>
					<div class="ef-tot-row"><label>Total Received</label><input name="total_received" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Balance Due</label><input name="balance_due" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
				</div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Narration</span>
			<textarea name="narration" class="ef-narration" placeholder="Sales remarks"></textarea>
		</div>

		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
			<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Print Invoice</button>
			<button type="button" class="btn btn-warning btn-sm"><i class="fa fa-credit-card"></i> Settle Till</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Voc Type: RIN — Retail Sales Invoice</span></div>
</div>
