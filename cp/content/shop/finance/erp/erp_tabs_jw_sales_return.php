<?php
/**
 * Jewellery ERP — Sales Return.
 * Ref: Suntech Sales Return screenshot (similar to retail sales).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$returns = epc_jewel_sale_list($db_link, $companyId, 'RETURN');

erp_page_header('<i class="fa fa-undo"></i> Sales Return', 'Process customer returns with refund details.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Sales return'),
));
?>
<div class="ef-window">
	<div class="ef-title">Sales Return</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_sr_form').style.display='block'"><i class="fa fa-plus"></i> New Return</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Voc No</th><th>Voc Date</th><th>Customer</th>
				<th>Orig Invoice</th><th>Reason</th><th>Refund Amt</th>
			</tr></thead>
			<tbody>
			<?php if (empty($returns)): ?>
				<tr><td colspan="7" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($returns as $r): ?>
				<tr class="ef-grid-row" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($r['voc_no']); ?></strong></td>
					<td><?php echo epc_erp_h($r['voc_date']); ?></td>
					<td><?php echo epc_erp_h($r['party_code']); ?></td>
					<td><?php echo epc_erp_h($r['ref_voc_no'] ?? ''); ?></td>
					<td><?php echo epc_erp_h($r['return_reason'] ?? ''); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$r['net_amount'], 2); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_sr_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_sales_return_save">

			<div class="ef-section">
				<span class="ef-section-title">Return Header</span>
				<div class="ef-row">
					<div class="ef-field"><label>Branch</label><input name="branch" maxlength="10" value="HO"></div>
					<div class="ef-field"><label>Voc Type</label><input name="voc_type" maxlength="5" value="SRT" readonly></div>
					<div class="ef-field"><label>Voc Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="ef-field"><label>Voc No</label><input name="voc_no" maxlength="20" placeholder="Auto"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Customer Code</label><input name="party_code" maxlength="20" required></div>
					<div class="ef-field"><label>Customer Name</label><input name="party_name" maxlength="80" readonly></div>
					<div class="ef-field"><label>Orig Invoice No</label><input name="ref_voc_no" maxlength="20" required placeholder="Original sale invoice"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Return Reason</label>
						<select name="return_reason"><option value="Defective">Defective</option><option value="Wrong Item">Wrong Item</option><option value="Size Issue">Size Issue</option><option value="Customer Changed Mind">Customer Changed Mind</option><option value="Other">Other</option></select>
					</div>
					<div class="ef-field"><label>Salesman</label><input name="salesman" maxlength="20"></div>
					<div class="ef-field"><label>Currency</label><input name="currency" maxlength="5" value="AED"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Return Items</span>
				<table class="ef-grid">
					<thead><tr>
						<th>No.</th><th>Barcode/Item</th><th>Description</th><th>Karat</th>
						<th>Pcs</th><th>Gross Wt</th><th>Net Wt</th><th>Rate</th><th>Amount</th>
					</tr></thead>
					<tbody>
						<tr>
							<td>1</td>
							<td><input name="li_barcode" maxlength="30" style="width:80px"></td>
							<td><input name="li_description" maxlength="120" style="width:140px"></td>
							<td><input name="li_karat" maxlength="10" style="width:40px"></td>
							<td><input name="li_pcs" type="number" value="1" style="width:40px"></td>
							<td><input name="li_gross_wt" type="number" step="0.001" value="0.000" style="width:70px"></td>
							<td><input name="li_net_wt" type="number" step="0.001" value="0.000" style="width:70px"></td>
							<td><input name="li_rate" type="number" step="0.01" value="0.00" style="width:70px"></td>
							<td><input name="li_amount" type="number" step="0.01" value="0.00" style="width:80px"></td>
						</tr>
					</tbody>
				</table>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Refund Details</span>
				<div class="ef-row">
					<div class="ef-field"><label>Refund Mode</label>
						<select name="refund_mode"><option value="CASH">Cash</option><option value="CARD">Card Reversal</option><option value="CREDIT">Credit Note</option></select>
					</div>
					<div class="ef-field"><label>Refund Amount</label><input name="refund_amount" type="number" step="0.01" value="0.00"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Narration</span>
				<div class="ef-row">
					<div class="ef-field ef-field-wide"><textarea name="narration" rows="2" maxlength="500" style="width:100%"></textarea></div>
				</div>
			</div>

			<div class="ef-totals">
				<div class="ef-row">
					<div class="ef-field"><label>Total Pcs</label><input name="total_pcs" type="number" value="0" readonly></div>
					<div class="ef-field"><label>Total Gross Wt</label><input name="total_gross_wt" type="number" step="0.001" value="0.000" readonly></div>
					<div class="ef-field"><label>Net Amount</label><input name="net_amount" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>VAT</label><input name="vat_amount" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Gross Total</label><input name="gross_total" type="number" step="0.01" value="0.00" readonly></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_sr_form').style.display='none'">Cancel</button>
			</div>
			</form>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Header New Record &rarr; Function Key (F5)</span>
	</div>
</div>
