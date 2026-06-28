<?php
/**
 * Jewellery ERP — Tourist VAT Refund.
 * UAE Tourist VAT Refund scheme — capture passport/purchase details for tax-free processing.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-plane"></i> Tourist VAT Refund', 'UAE Tourist Tax Refund Scheme — process VAT refunds for tourists.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Tourist VAT'),
));
?>
<div class="ef-window">
	<div class="ef-title">Tourist VAT Refund</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_tvr_form').style.display='block'"><i class="fa fa-plus"></i> New Refund</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print Tax Invoice</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Ref No</th><th>Date</th><th>Tourist Name</th>
				<th>Passport</th><th>Nationality</th><th>Invoice Amt</th><th>VAT Refund</th><th>Status</th>
			</tr></thead>
			<tbody>
				<tr><td colspan="9" style="text-align:center;color:#999">No records</td></tr>
			</tbody>
		</table>

		<div id="jw_tvr_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_tourist_vat_save">

			<div class="ef-section">
				<span class="ef-section-title">Tourist Details</span>
				<div class="ef-row">
					<div class="ef-field"><label>Tourist Name</label><input name="tourist_name" maxlength="100" required></div>
					<div class="ef-field"><label>Passport No</label><input name="passport_no" maxlength="30" required></div>
					<div class="ef-field"><label>Nationality</label><input name="nationality" maxlength="50"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Country of Residence</label><input name="country" maxlength="50"></div>
					<div class="ef-field"><label>Mobile</label><input name="mobile" maxlength="20"></div>
					<div class="ef-field"><label>Email</label><input name="email" maxlength="100" type="email"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Flight No</label><input name="flight_no" maxlength="20"></div>
					<div class="ef-field"><label>Departure Date</label><input name="departure_date" type="date"></div>
					<div class="ef-field"><label>Destination</label><input name="destination" maxlength="50"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Invoice / Purchase Details</span>
				<div class="ef-row">
					<div class="ef-field"><label>Branch</label><input name="branch" maxlength="10" value="HO"></div>
					<div class="ef-field"><label>Invoice No</label><input name="invoice_no" maxlength="30" required></div>
					<div class="ef-field"><label>Invoice Date</label><input name="invoice_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="ef-field"><label>Salesman</label><input name="salesman" maxlength="30"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Purchase Items</span>
				<table class="ef-grid">
					<thead><tr>
						<th>No.</th><th>Description</th><th>Metal</th><th>Karat</th>
						<th>Gross Wt</th><th>Amount (AED)</th><th>VAT (5%)</th><th>Total</th>
					</tr></thead>
					<tbody>
					<?php for ($r = 0; $r < 4; $r++): ?>
						<tr>
							<td><?php echo $r + 1; ?></td>
							<td><input name="items[<?php echo $r; ?>][description]" maxlength="100" style="min-width:120px"></td>
							<td><select name="items[<?php echo $r; ?>][metal]" style="width:50px"><option value="">—</option><option value="G">G</option><option value="S">S</option><option value="T">T</option></select></td>
							<td><input name="items[<?php echo $r; ?>][karat]" maxlength="10" style="width:40px"></td>
							<td><input name="items[<?php echo $r; ?>][gross_wt]" type="number" step="0.001" value="0" style="width:60px"></td>
							<td><input name="items[<?php echo $r; ?>][amount]" type="number" step="0.01" value="0.00" style="width:80px"></td>
							<td><input name="items[<?php echo $r; ?>][vat]" type="number" step="0.01" value="0.00" style="width:60px" readonly></td>
							<td><input name="items[<?php echo $r; ?>][total]" type="number" step="0.01" value="0.00" style="width:80px" readonly></td>
						</tr>
					<?php endfor; ?>
					</tbody>
				</table>
			</div>

			<div class="ef-totals">
				<div class="ef-row">
					<div class="ef-field"><label>Sub Total</label><input name="sub_total" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>VAT Amount (5%)</label><input name="vat_amount" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>Total Invoice</label><input name="total_invoice" type="number" step="0.01" value="0.00" readonly style="font-weight:bold"></div>
					<div class="ef-field"><label>Refund Amount</label><input name="refund_amount" type="number" step="0.01" value="0.00" readonly style="font-weight:bold;color:green"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Narration</span>
				<div class="ef-row">
					<div class="ef-field ef-field-wide"><textarea name="narration" rows="2" maxlength="500" style="width:100%"></textarea></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Print Tax Invoice</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_tvr_form').style.display='none'">Cancel</button>
			</div>
			</form>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Tourist VAT Refund — UAE Tax Free Scheme</span>
	</div>
</div>
