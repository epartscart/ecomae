<?php
/**
 * Jewellery ERP — POS Advance Receipt (ADV).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-money"></i> POS Advance', 'Advance payment receipt from customer.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'POS advance'),
));
?>
<div class="ef-window">
	<div class="ef-title">POS Advance Receipt - (ADV)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_voucher_save">
		<input type="hidden" name="voc_type" value="ADV">

		<div class="ef-section">
			<span class="ef-section-title">Advance Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Adv. No.</label><input name="voc_no" class="ef-readonly" readonly placeholder="Auto" style="width:60px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Customer Code</label><input name="party_code" required></div>
				<div class="ef-field ef-field-wide"><label>Customer Name</label><input name="party_name"></div>
				<div class="ef-field"><label>Mobile</label><input name="mobile"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Advance Amount</label><input name="advance_amount" type="number" step="0.01" value="0" required></div>
				<div class="ef-field"><label>Currency</label><input name="currency" value="AED" style="width:50px"></div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Payment Mode</span>
			<table class="ef-grid">
				<thead><tr><th>Pay Mode</th><th>Account</th><th>Amount</th><th>Card/Ref#</th></tr></thead>
				<tbody>
				<tr>
					<td><select name="receipts[0][mode]"><option value="Cash">Cash</option><option value="Card">Card</option><option value="Bank">Bank Transfer</option></select></td>
					<td><input name="receipts[0][account]"></td>
					<td><input name="receipts[0][amount]" type="number" step="0.01" value="0"></td>
					<td><input name="receipts[0][card_ref]"></td>
				</tr>
				</tbody>
			</table>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Purpose / Item Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>For Item</label><input name="for_item" placeholder="Ring / Necklace / Custom order"></div>
				<div class="ef-field"><label>Est. Delivery</label><input name="est_delivery" type="date"></div>
			</div>
			<textarea name="narration" class="ef-narration" placeholder="Advance purpose / remarks"></textarea>
		</div>

		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
			<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Print Receipt</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Voc Type: ADV — POS Advance</span></div>
</div>
