<?php
/**
 * Jewellery ERP — Diamond Purchase (RDP).
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

erp_page_header('<i class="fa fa-diamond"></i> Diamond Purchase', 'Diamond / stone purchase with certificates.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Diamond purchase'),
));
?>
<div class="ef-window">
	<div class="ef-title">Diamond Purchase - (RDP)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-search"></i> Find</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_voucher_save">
		<input type="hidden" name="voc_type" value="RDP">

		<div class="ef-section">
			<span class="ef-section-title">Header Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option><option value="B1">B1</option><option value="B2">B2</option></select></div>
				<div class="ef-field"><label>Voc Type</label><input value="RDP" class="ef-readonly" readonly style="width:50px;background:#e8e8e8"></div>
				<div class="ef-field"><label>Voc Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Voc No.</label><input name="voc_no" class="ef-readonly" readonly placeholder="Auto" style="width:60px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Party Code</label><input name="party_code" required></div>
				<div class="ef-field ef-field-wide"><label>Party Name</label><input name="party_name"></div>
				<div class="ef-field"><label>Currency</label><select name="currency"><option value="AED">AED</option><option value="USD">USD</option></select></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Ref / Inv#</label><input name="ref_invoice_no"></div>
				<div class="ef-field"><label>Credit Days</label><input name="credit_days" type="number" value="0" style="width:50px"></div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Diamond / Stone Lines</span>
			<table class="ef-grid">
				<thead><tr>
					<th>No.</th><th>Item Code</th><th>Description</th><th>Pcs</th><th>Carat</th>
					<th>Rate/Ct</th><th>Stone Amount</th><th>Certificate #</th>
				</tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 5; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><input name="lines[<?php echo $r; ?>][item_code]"></td>
					<td><input name="lines[<?php echo $r; ?>][description]" style="min-width:140px"></td>
					<td><input name="lines[<?php echo $r; ?>][pcs]" type="number" value="0" style="width:40px"></td>
					<td><input name="lines[<?php echo $r; ?>][carat]" type="number" step="0.01" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][rate_ct]" type="number" step="0.01" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][stone_amt]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][certificate_no]"></td>
				</tr>
				<?php endfor; ?>
				</tbody>
				<tfoot><tr><td colspan="6" style="text-align:right"><strong>Total:</strong></td><td><input name="sub_total" type="number" step="0.01" value="0" class="ef-readonly" readonly></td><td></td></tr></tfoot>
			</table>
		</div>

		<div style="display:flex;justify-content:flex-end;">
			<div class="ef-section" style="min-width:280px;">
				<span class="ef-section-title">Summary</span>
				<div class="ef-totals">
					<div class="ef-tot-row"><label>Net Amount</label><input name="net_amount" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>VAT</label><input name="vat_amount" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Round Off</label><input name="round_off" type="number" step="0.01" value="0"></div>
					<div class="ef-tot-row" style="border-bottom:2px solid #333"><label><strong>GROSS TOTAL</strong></label><input name="gross_total" type="number" step="0.01" value="0" class="ef-readonly" readonly style="font-weight:700"></div>
				</div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Narration</span>
			<textarea name="narration" class="ef-narration" placeholder="Purchase remarks"></textarea>
		</div>
		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
			<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Print</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Voc Type: RDP — Diamond Purchase</span></div>
</div>
