<?php
/**
 * Jewellery ERP — Retail Sales (POS).
 * Ref: Suntech Retail Sales screenshots (complex form with metal rates table, item detail popup).
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
$sales = epc_jewel_sale_list($db_link, $companyId, 'RETAIL');

erp_page_header('<i class="fa fa-shopping-bag"></i> Retail Sales (POS)', 'Point-of-sale retail jewellery sales with metal rates and payments.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Retail POS'),
));
?>
<div class="ef-window">
	<div class="ef-title">Retail Sales (POS)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_rs_form').style.display='block'"><i class="fa fa-plus"></i> New Invoice</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Inv No</th><th>Inv Date</th><th>Customer</th>
				<th>Salesman</th><th>Items</th><th>Net Amt</th><th>Payment</th>
			</tr></thead>
			<tbody>
			<?php if (empty($sales)): ?>
				<tr><td colspan="8" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($sales as $s): ?>
				<tr class="ef-grid-row" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($s['voc_no']); ?></strong></td>
					<td><?php echo epc_erp_h($s['voc_date']); ?></td>
					<td><?php echo epc_erp_h($s['party_code']); ?></td>
					<td><?php echo epc_erp_h($s['salesman']); ?></td>
					<td><?php echo (int)$s['total_items']; ?></td>
					<td style="text-align:right"><?php echo number_format((float)$s['net_amount'], 2); ?></td>
					<td><?php echo epc_erp_h($s['payment_mode']); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_rs_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_retail_sale_save">

			<div class="ef-section">
				<span class="ef-section-title">Invoice Header</span>
				<div class="ef-row">
					<div class="ef-field"><label>Branch</label><input name="branch" maxlength="10" value="HO"></div>
					<div class="ef-field"><label>Voc Type</label><input name="voc_type" maxlength="5" value="RSL" readonly></div>
					<div class="ef-field"><label>Inv Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="ef-field"><label>Inv No</label><input name="voc_no" maxlength="20" placeholder="Auto"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Customer Code</label><input name="party_code" maxlength="20"></div>
					<div class="ef-field"><label>Customer Name</label><input name="party_name" maxlength="80"></div>
					<div class="ef-field"><label>Mobile</label><input name="customer_mobile" maxlength="20"></div>
					<div class="ef-field"><label>Salesman</label><input name="salesman" maxlength="20"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Currency</label><input name="currency" maxlength="5" value="AED"></div>
					<div class="ef-field"><label>Price Type</label>
						<select name="price_type"><option value="GEN">General</option><option value="TAG">Tag</option><option value="WSL">Wholesale</option></select>
					</div>
					<div class="ef-field"><label>TRN No</label><input name="trn_no" maxlength="20"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Metal Rates</span>
				<table class="ef-grid" style="max-width:500px">
					<thead><tr><th>Metal</th><th>Rate Type</th><th>Rate</th></tr></thead>
					<tbody>
						<tr><td>Gold</td><td>GMS</td><td><input name="gold_rate_gms" type="number" step="0.01" value="0.00" style="width:100px"></td></tr>
						<tr><td>Silver</td><td>GMS</td><td><input name="silver_rate_gms" type="number" step="0.01" value="0.00" style="width:100px"></td></tr>
						<tr><td>Platinum</td><td>GMS</td><td><input name="platinum_rate_gms" type="number" step="0.01" value="0.00" style="width:100px"></td></tr>
					</tbody>
				</table>
			</div>

			<div class="ef-tabs">
				<button type="button" class="ef-tab active" onclick="jwRsTab(this,'items')">1. Line Items</button>
				<button type="button" class="ef-tab" onclick="jwRsTab(this,'payment')">2. Payment</button>
				<button type="button" class="ef-tab" onclick="jwRsTab(this,'exchange')">3. Old Exchange</button>
			</div>

			<div id="jw_rs_items" class="ef-tab-pane">
				<table class="ef-grid">
					<thead><tr>
						<th>No.</th><th>Barcode/Item</th><th>Description</th><th>Karat</th>
						<th>Pcs</th><th>Gross Wt</th><th>Net Wt</th><th>Rate</th>
						<th>Metal Amt</th><th>MC Amt</th><th>Stone Amt</th><th>Total</th>
					</tr></thead>
					<tbody>
						<tr>
							<td>1</td>
							<td><input name="li_barcode" maxlength="30" style="width:80px"></td>
							<td><input name="li_description" maxlength="120" style="width:120px"></td>
							<td><input name="li_karat" maxlength="10" style="width:40px"></td>
							<td><input name="li_pcs" type="number" value="1" style="width:40px"></td>
							<td><input name="li_gross_wt" type="number" step="0.001" value="0.000" style="width:70px"></td>
							<td><input name="li_net_wt" type="number" step="0.001" value="0.000" style="width:70px"></td>
							<td><input name="li_rate" type="number" step="0.01" value="0.00" style="width:70px"></td>
							<td><input name="li_metal_amt" type="number" step="0.01" value="0.00" style="width:70px"></td>
							<td><input name="li_mc_amt" type="number" step="0.01" value="0.00" style="width:70px"></td>
							<td><input name="li_stone_amt" type="number" step="0.01" value="0.00" style="width:70px"></td>
							<td><input name="li_total" type="number" step="0.01" value="0.00" style="width:80px"></td>
						</tr>
					</tbody>
				</table>
			</div>

			<div id="jw_rs_payment" class="ef-tab-pane" style="display:none">
				<div class="ef-section">
					<span class="ef-section-title">Payment Details</span>
					<div class="ef-row">
						<div class="ef-field"><label>Payment Mode</label>
							<select name="payment_mode"><option value="CASH">Cash</option><option value="CARD">Card</option><option value="BANK">Bank Transfer</option><option value="CHEQUE">Cheque</option><option value="MIXED">Mixed</option></select>
						</div>
						<div class="ef-field"><label>Cash Amount</label><input name="cash_amount" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Card Amount</label><input name="card_amount" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Card No (Last 4)</label><input name="card_no" maxlength="4"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Bank Transfer</label><input name="bank_amount" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Cheque No</label><input name="cheque_no" maxlength="20"></div>
						<div class="ef-field"><label>Cheque Amount</label><input name="cheque_amount" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Advance Adj</label><input name="advance_adj" type="number" step="0.01" value="0.00"></div>
					</div>
				</div>
			</div>

			<div id="jw_rs_exchange" class="ef-tab-pane" style="display:none">
				<div class="ef-section">
					<span class="ef-section-title">Old Gold / Exchange</span>
					<div class="ef-row">
						<div class="ef-field"><label>Metal</label>
							<select name="ex_metal"><option value="G">Gold</option><option value="S">Silver</option></select>
						</div>
						<div class="ef-field"><label>Karat</label><input name="ex_karat" maxlength="10"></div>
						<div class="ef-field"><label>Gross Wt</label><input name="ex_gross_wt" type="number" step="0.001" value="0.000"></div>
						<div class="ef-field"><label>Purity</label><input name="ex_purity" type="number" step="0.000001" value="0.000000"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Net Wt</label><input name="ex_net_wt" type="number" step="0.001" value="0.000"></div>
						<div class="ef-field"><label>Rate</label><input name="ex_rate" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Exchange Value</label><input name="ex_value" type="number" step="0.01" value="0.00"></div>
					</div>
				</div>
			</div>

			<div class="ef-totals">
				<div class="ef-row">
					<div class="ef-field"><label>Total Pcs</label><input name="total_pcs" type="number" value="0" readonly></div>
					<div class="ef-field"><label>Total Gross Wt</label><input name="total_gross_wt" type="number" step="0.001" value="0.000" readonly></div>
					<div class="ef-field"><label>Sub Total</label><input name="sub_total" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>Discount</label><input name="discount" type="number" step="0.01" value="0.00"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Net Amount</label><input name="net_amount" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>VAT 5%</label><input name="vat_amount" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Round Off</label><input name="round_off" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label><strong>Gross Total</strong></label><input name="gross_total" type="number" step="0.01" value="0.00" readonly style="font-weight:bold"></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-success btn-sm" disabled><i class="fa fa-print"></i> Save &amp; Print</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_rs_form').style.display='none'">Cancel</button>
			</div>
			</form>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Header New Record &rarr; Function Key (F5)</span>
	</div>
</div>
<script>
function jwRsTab(btn, pane){
	document.querySelectorAll('.ef-tab').forEach(function(t){t.classList.remove('active');});
	btn.classList.add('active');
	['items','payment','exchange'].forEach(function(p){
		document.getElementById('jw_rs_'+p).style.display=(p===pane)?'block':'none';
	});
}
</script>
